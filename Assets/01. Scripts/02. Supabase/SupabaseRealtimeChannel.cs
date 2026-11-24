using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DOTORION.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DOTORION.EventTests")]

namespace DOTORION.Supabase
{
    /// <summary>
    /// A private, receive-only invalidation channel. Snapshots, transitions and
    /// nudge cursors remain owned by the REST backend, including during outages.
    /// </summary>
    public sealed class SupabaseRealtimeChannel : ITeamChangeSignals, IDisposable
    {
        private const int MaximumMessageBytes = 64 * 1024;
        private readonly Uri _endpoint;
        private readonly string _topic;
        private readonly ISupabaseSessionProvider _sessions;
        private readonly Func<Uri, CancellationToken, Task<WebSocket>> _connect;
        private readonly RealtimeTimings _timings;
        private readonly ConcurrentQueue<TeamChangeKind> _signals = new ConcurrentQueue<TeamChangeKind>();
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly object _disposeGate = new object();
        private readonly Task _runner;
        private readonly System.Random _random = new System.Random();
        private long _reference;
        private int _live;
        private int _joinGeneration;
        private int _disposed;
        private bool _stopped;
        private bool _outageReported;

        public SupabaseRealtimeChannel(
            string projectUrl, string publishableKey,
            ISupabaseSessionProvider sessionProvider, Guid teamId)
            : this(projectUrl, publishableKey, sessionProvider, teamId, ConnectAsync, new RealtimeTimings())
        {
        }

        // The socket seam exercises the wire protocol without connecting tests to
        // a real project or requiring credentials in the test assembly.
        internal SupabaseRealtimeChannel(
            string projectUrl, string publishableKey,
            ISupabaseSessionProvider sessionProvider, Guid teamId,
            Func<Uri, CancellationToken, Task<WebSocket>> connect, RealtimeTimings timings)
        {
            if (!Uri.TryCreate(projectUrl, UriKind.Absolute, out var project)
                || (project.Scheme != "https" && project.Scheme != "http"))
                throw new ArgumentException("An HTTP(S) project URL is required.", nameof(projectUrl));
            if (string.IsNullOrWhiteSpace(publishableKey))
                throw new ArgumentException("A publishable key is required.", nameof(publishableKey));
            if (teamId == Guid.Empty)
                throw new ArgumentException("A team id is required.", nameof(teamId));

            _sessions = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _connect = connect ?? throw new ArgumentNullException(nameof(connect));
            _timings = timings ?? throw new ArgumentNullException(nameof(timings));
            var endpoint = new UriBuilder(project)
            {
                Scheme = project.Scheme == "https" ? "wss" : "ws",
                Path = project.AbsolutePath.TrimEnd('/') + "/realtime/v1/websocket",
                Query = "apikey=" + Uri.EscapeDataString(publishableKey) + "&vsn=1.0.0",
                Fragment = string.Empty
            };
            if (project.IsDefaultPort) endpoint.Port = -1;
            _endpoint = endpoint.Uri;
            _topic = "realtime:team:" + teamId.ToString("D");
            _runner = Task.Run(RunAsync);
        }

        public bool IsLive => Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _live) != 0;
        public int JoinGeneration => Volatile.Read(ref _joinGeneration);
        public bool TryDequeue(out TeamChangeKind kind) => _signals.TryDequeue(out kind);
        internal Task Completion => _runner;

        private string NextReference() => Interlocked.Increment(ref _reference).ToString(CultureInfo.InvariantCulture);

        private async Task RunAsync()
        {
            var token = _lifetime.Token;
            var backoff = _timings.ReconnectSeconds;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var generation = JoinGeneration;
                    try
                    {
                        await RunConnectionAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Exception messages can contain URLs or authentication data.
                        // Report the outage once, not every failed retry.
                        if (!token.IsCancellationRequested && !_outageReported)
                        {
                            _outageReported = true;
                            Debug.LogWarning("DOTORION Realtime disconnected; using adaptive polling.");
                        }
                    }

                    if (JoinGeneration != generation) backoff = _timings.ReconnectSeconds;
                    await Task.Delay(TimeSpan.FromSeconds(backoff * (1d + _random.NextDouble() * 0.2d)), token)
                        .ConfigureAwait(false);
                    backoff = Math.Min(60d, backoff * 2d);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            finally
            {
                Volatile.Write(ref _live, 0);
                _sendGate.Dispose();
                lock (_disposeGate)
                {
                    _stopped = true;
                    _lifetime.Dispose();
                }
            }
        }

        private async Task RunConnectionAsync(CancellationToken lifetime)
        {
            WebSocket socket = null;
            Task receive = null, maintain = null, refreshToken = null;
            using (var connection = CancellationTokenSource.CreateLinkedTokenSource(lifetime))
            {
                var token = connection.Token;
                try
                {
                    SupabaseAuthSession session;
                    using (var opening = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        // A socket that never opens or acknowledges its join must
                        // leave polling active rather than hanging a retry forever.
                        opening.CancelAfter(TimeSpan.FromSeconds(_timings.JoinTimeoutSeconds));
                        session = await _sessions.GetValidSessionAsync(opening.Token).ConfigureAwait(false);
                        socket = await _connect(_endpoint, opening.Token).ConfigureAwait(false);
                    }

                    var state = new ConnectionState(NextReference());
                    await SendAsync(socket, new JoinMessage
                    {
                        topic = _topic, @event = "phx_join", @ref = state.JoinRef, join_ref = state.JoinRef,
                        payload = new JoinPayload { access_token = session.AccessToken }
                    }, token).ConfigureAwait(false);

                    receive = ReceiveAsync(socket, state, token);
                    maintain = MaintainAsync(socket, state, token);
                    refreshToken = RefreshTokenAsync(socket, state, session.AccessToken, token);
                    var ended = await Task.WhenAny(receive, maintain, refreshToken).ConfigureAwait(false);
                    await ended.ConfigureAwait(false);
                    throw new IOException("Realtime connection ended.");
                }
                finally
                {
                    Volatile.Write(ref _live, 0);
                    connection.Cancel();
                    // Finish all users before closing the socket or disposing the
                    // send semaphore. Dispose never blocks Unity's main thread.
                    await IgnoreFailureAsync(receive).ConfigureAwait(false);
                    await IgnoreFailureAsync(maintain).ConfigureAwait(false);
                    await IgnoreFailureAsync(refreshToken).ConfigureAwait(false);
                    // A join reply already being handled when cancellation arrived
                    // must not leave IsLive true during the reconnect delay.
                    Volatile.Write(ref _live, 0);
                    if (socket != null)
                    {
                        await CloseAsync(socket).ConfigureAwait(false);
                        socket.Dispose();
                    }
                }
            }
        }

        private static async Task<WebSocket> ConnectAsync(Uri endpoint, CancellationToken token)
        {
            var socket = new ClientWebSocket();
            try
            {
                await socket.ConnectAsync(endpoint, token).ConfigureAwait(false);
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private async Task SendAsync(WebSocket socket, object message, CancellationToken token)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
            using (var sending = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                sending.CancelAfter(TimeSpan.FromSeconds(10));
                await _sendGate.WaitAsync(sending.Token).ConfigureAwait(false);
                try
                {
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, sending.Token)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _sendGate.Release();
                }
            }
        }

        private async Task ReceiveAsync(WebSocket socket, ConnectionState state, CancellationToken token)
        {
            var buffer = new byte[8192];
            var utf8 = new UTF8Encoding(false, true);
            using (var message = new MemoryStream())
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (frame.MessageType == WebSocketMessageType.Close)
                        throw new IOException("Realtime socket closed.");
                    if (frame.MessageType != WebSocketMessageType.Text || message.Length + frame.Count > MaximumMessageBytes)
                        throw new InvalidDataException("Invalid Realtime frame.");
                    message.Write(buffer, 0, frame.Count);
                    if (!frame.EndOfMessage) continue;
                    token.ThrowIfCancellationRequested();
                    HandleMessage(utf8.GetString(message.GetBuffer(), 0, (int)message.Length), state);
                    message.SetLength(0);
                }
            }
        }

        private void HandleMessage(string json, ConnectionState state)
        {
            var envelope = JsonUtility.FromJson<Envelope>(json);
            if (envelope == null) return;
            if (envelope.topic == "phoenix" && envelope.@event == "phx_reply")
            {
                var reply = JsonUtility.FromJson<ReplyMessage>(json);
                lock (state.Gate)
                {
                    if (envelope.@ref != null && state.Heartbeats.ContainsKey(envelope.@ref))
                    {
                        if (reply.payload?.status != "ok") throw new IOException("Heartbeat rejected.");
                        state.Heartbeats.Remove(envelope.@ref);
                    }
                }
                return;
            }
            if (envelope.topic != _topic
                || (!string.IsNullOrEmpty(envelope.join_ref) && envelope.join_ref != state.JoinRef)) return;

            if (envelope.@event == "phx_error" || envelope.@event == "phx_close")
                throw new IOException("Realtime channel closed.");
            if (envelope.@event == "phx_reply")
            {
                var reply = JsonUtility.FromJson<ReplyMessage>(json);
                if (reply.payload?.status == "error") throw new IOException("Realtime request rejected.");
                if (envelope.@ref == state.JoinRef && reply.payload?.status == "ok")
                {
                    lock (state.Gate)
                    {
                        if (state.Joined) return;
                        state.Joined = true;
                        Interlocked.Increment(ref _joinGeneration);
                        Volatile.Write(ref _live, 1);
                    }
                    _outageReported = false;
                    Debug.Log("DOTORION Realtime connected; using team signals.");
                }
                return;
            }
            if (envelope.@event != "broadcast") return;
            lock (state.Gate)
            {
                if (!state.Joined) return;
            }
            var broadcast = JsonUtility.FromJson<BroadcastMessage>(json);
            if (broadcast.payload?.@event != "team_signal") return;
            switch (broadcast.payload.payload?.kind)
            {
                case "state": _signals.Enqueue(TeamChangeKind.State); break;
                case "member": _signals.Enqueue(TeamChangeKind.Member); break;
                case "nudge": _signals.Enqueue(TeamChangeKind.Nudge); break;
            }
        }

        private async Task MaintainAsync(WebSocket socket, ConnectionState state, CancellationToken token)
        {
            var nextHeartbeat = _timings.HeartbeatSeconds;
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(_timings.TickSeconds), token).ConfigureAwait(false);
                var now = state.Clock.Elapsed.TotalSeconds;
                string heartbeatRef = null;
                lock (state.Gate)
                {
                    if (!state.Joined && now >= _timings.JoinTimeoutSeconds)
                        throw new TimeoutException("Realtime join timed out.");
                    foreach (var sentAt in state.Heartbeats.Values)
                    {
                        // Track every outstanding ref: sending a newer heartbeat
                        // must not extend the deadline of an unanswered one.
                        if (now - sentAt >= _timings.HeartbeatTimeoutSeconds)
                            throw new TimeoutException("Realtime heartbeat timed out.");
                    }
                    if (now >= nextHeartbeat)
                    {
                        heartbeatRef = NextReference();
                        state.Heartbeats.Add(heartbeatRef, now);
                        nextHeartbeat = now + _timings.HeartbeatSeconds;
                    }
                }
                if (heartbeatRef != null)
                {
                    await SendAsync(socket, new HeartbeatMessage { @ref = heartbeatRef }, token).ConfigureAwait(false);
                }
            }
        }

        private async Task RefreshTokenAsync(
            WebSocket socket, ConnectionState state, string accessToken, CancellationToken token)
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(_timings.TokenRefreshSeconds), token).ConfigureAwait(false);
                SupabaseAuthSession session;
                using (var refreshing = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    // Token I/O has its own loop so it cannot delay heartbeats.
                    refreshing.CancelAfter(TimeSpan.FromSeconds(15));
                    session = await _sessions.GetValidSessionAsync(refreshing.Token).ConfigureAwait(false);
                }
                if (string.Equals(accessToken, session.AccessToken, StringComparison.Ordinal)) continue;
                await SendAsync(socket, new AccessTokenMessage
                {
                    topic = _topic, @ref = NextReference(), join_ref = state.JoinRef,
                    payload = new AccessTokenPayload { access_token = session.AccessToken }
                }, token).ConfigureAwait(false);
                accessToken = session.AccessToken;
            }
        }

        private async Task CloseAsync(WebSocket socket)
        {
            try
            {
                using (var closing = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
                {
                    await _sendGate.WaitAsync(closing.Token).ConfigureAwait(false);
                    try
                    {
                        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, closing.Token)
                                .ConfigureAwait(false);
                    }
                    finally { _sendGate.Release(); }
                }
            }
            catch (Exception)
            {
                // A best-effort goodbye must not prevent resource cleanup.
            }
        }

        private static async Task IgnoreFailureAsync(Task task)
        {
            if (task == null) return;
            try { await task.ConfigureAwait(false); }
            catch (Exception) { }
        }

        public void Dispose()
        {
            lock (_disposeGate)
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                Volatile.Write(ref _live, 0);
                if (!_stopped) _lifetime.Cancel();
            }
        }

        private sealed class ConnectionState
        {
            public readonly object Gate = new object();
            public readonly string JoinRef;
            public readonly Stopwatch Clock = Stopwatch.StartNew();
            public readonly Dictionary<string, double> Heartbeats = new Dictionary<string, double>();
            public bool Joined;
            public ConnectionState(string joinRef) { JoinRef = joinRef; }
        }

        internal sealed class RealtimeTimings
        {
            public double HeartbeatSeconds = 25;
            public double HeartbeatTimeoutSeconds = 60;
            public double JoinTimeoutSeconds = 15;
            public double TokenRefreshSeconds = 60;
            public double TickSeconds = 1;
            public double ReconnectSeconds = 1;
        }

        // Separate DTOs prevent JsonUtility from serializing unrelated payload
        // fields or forcing hand-built JSON containing access tokens.
        [Serializable] private sealed class EmptyPayload { }
        [Serializable] private sealed class BroadcastConfig { public bool ack = false; public bool self = false; }
        [Serializable] private sealed class PresenceConfig { public bool enabled = false; }
        [Serializable] private sealed class JoinConfig
        {
            public BroadcastConfig broadcast = new BroadcastConfig();
            public PresenceConfig presence = new PresenceConfig();
            public EmptyPayload[] postgres_changes = new EmptyPayload[0];
            public bool @private = true;
        }
        [Serializable] private sealed class JoinPayload
        {
            public JoinConfig config = new JoinConfig();
            public string access_token;
        }
        [Serializable] private sealed class JoinMessage
        {
            public string topic, @event, @ref, join_ref;
            public JoinPayload payload;
        }
        [Serializable] private sealed class HeartbeatMessage
        {
            public string topic = "phoenix", @event = "heartbeat", @ref;
            public EmptyPayload payload = new EmptyPayload();
        }
        [Serializable] private sealed class AccessTokenPayload { public string access_token; }
        [Serializable] private sealed class AccessTokenMessage
        {
            public string topic, @ref, join_ref;
            public string @event = "access_token";
            public AccessTokenPayload payload;
        }
#pragma warning disable 0649 // These fields are populated by JsonUtility.
        [Serializable] private sealed class Envelope { public string topic, @event, @ref, join_ref; }
        [Serializable] private sealed class ReplyPayload { public string status; }
        [Serializable] private sealed class ReplyMessage { public ReplyPayload payload; }
        [Serializable] private sealed class SignalPayload { public string kind; }
        [Serializable] private sealed class BroadcastPayload { public string @event; public SignalPayload payload; }
        [Serializable] private sealed class BroadcastMessage { public BroadcastPayload payload; }
#pragma warning restore 0649
    }
}

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DOTORION.Core;
using DOTORION.Supabase;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DOTORION.Tests
{
    public sealed class SupabaseRealtimeChannelTests
    {
        private static readonly Guid TeamId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        private const string Disconnected = "DOTORION Realtime disconnected; using adaptive polling.";

        [Test]
        public async Task JoinMustMatchAndFragmentedBroadcastsAreFiltered()
        {
            var socket = new FakeSocket();
            Uri endpoint = null;
            var channel = new SupabaseRealtimeChannel("http://localhost:54321", "test-publishable", new Sessions(), TeamId,
                (uri, token) => { endpoint = uri; return Task.FromResult<WebSocket>(socket); }, Timings());
            try
            {
                var join = await NextMessage(socket, "phx_join");
                Assert.That(endpoint.Scheme, Is.EqualTo("ws"));
                Assert.That(endpoint.Port, Is.EqualTo(54321));
                Assert.That(endpoint.AbsolutePath, Is.EqualTo("/realtime/v1/websocket"));
                Assert.That(join.topic, Is.EqualTo("realtime:team:" + TeamId.ToString("D")));
                Assert.That(join.join_ref, Is.EqualTo(join.@ref));
                Assert.That(join.payload.config.@private, Is.True);
                Assert.That(join.payload.config.broadcast.ack, Is.False);
                Assert.That(join.payload.config.broadcast.self, Is.False);
                Assert.That(join.payload.config.presence.enabled, Is.False);
                Assert.That(join.payload.config.postgres_changes, Is.Empty);
                Assert.That(channel.IsLive, Is.False);

                Reply(socket, join, "ok", "wrong-ref");
                Broadcast(socket, join.topic, "nudge");
                await Task.Delay(30);
                Assert.That(channel.IsLive, Is.False);
                Assert.That(channel.JoinGeneration, Is.Zero);
                Assert.That(channel.TryDequeue(out _), Is.False);

                Reply(socket, join, "ok");
                await Until(() => channel.IsLive);
                Reply(socket, join, "ok");
                Broadcast(socket, "realtime:team:other", "state");
                Broadcast(socket, join.topic, "unknown");
                Broadcast(socket, join.topic, "member", "not_team_signal");
                socket.Feed(JsonUtility.ToJson(new WireMessage { topic = join.topic, @event = "system" }));
                Broadcast(socket, join.topic, "state", fragmented: true);
                Broadcast(socket, join.topic, "member");
                Broadcast(socket, join.topic, "nudge");
                var received = new ConcurrentQueue<TeamChangeKind>();
                await Until(() =>
                {
                    while (channel.TryDequeue(out var kind)) received.Enqueue(kind);
                    return received.Count == 3;
                });
                Assert.That(received.ToArray(), Is.EqualTo(new[] { TeamChangeKind.State, TeamChangeKind.Member, TeamChangeKind.Nudge }));
                Assert.That(channel.JoinGeneration, Is.EqualTo(1));
            }
            finally { await Stop(channel); }
            Assert.That(socket.Disposed, Is.True);
            Assert.That(socket.CloseAttempts, Is.EqualTo(1));
        }

        [Test]
        public async Task ReconnectRequiresANewAcknowledgmentAndIncrementsGeneration()
        {
            var first = new FakeSocket();
            var second = new FakeSocket();
            var attempts = 0;
            var channel = Channel(() => Interlocked.Increment(ref attempts) == 1 ? first : second);
            try
            {
                var join = await NextMessage(first, "phx_join");
                Reply(first, join, "ok");
                await Until(() => channel.IsLive);
                LogAssert.Expect(LogType.Warning, Disconnected);
                first.Feed(JsonUtility.ToJson(new WireMessage { topic = join.topic, @event = "phx_close" }));
                var rejoin = await NextMessage(second, "phx_join");
                Assert.That(channel.IsLive, Is.False);
                Assert.That(rejoin.@ref, Is.Not.EqualTo(join.@ref));
                Reply(second, rejoin, "ok");
                await Until(() => channel.JoinGeneration == 2 && channel.IsLive);
                Assert.That(first.Disposed, Is.True);
            }
            finally { await Stop(channel); }
        }

        [Test]
        public async Task RejectedAndUnansweredJoinsStayOfflineAndOnlyLogOneOutage()
        {
            var first = new FakeSocket();
            var second = new FakeSocket();
            var third = new FakeSocket();
            var attempts = 0;
            var timings = Timings();
            timings.JoinTimeoutSeconds = 0.2;
            var channel = Channel(() =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                return attempt == 1 ? first : attempt == 2 ? second : third;
            }, timings: timings);
            try
            {
                var join = await NextMessage(first, "phx_join");
                LogAssert.Expect(LogType.Warning, Disconnected);
                Reply(first, join, "error");
                await NextMessage(second, "phx_join");
                await NextMessage(third, "phx_join");
                Assert.That(channel.IsLive, Is.False);
                Assert.That(channel.JoinGeneration, Is.Zero);
            }
            finally { await Stop(channel); }
        }

        [Test]
        public async Task NewHeartbeatsCannotHideAnOlderMissingReply()
        {
            var first = new FakeSocket();
            var second = new FakeSocket();
            var attempts = 0;
            var timings = Timings();
            timings.HeartbeatSeconds = 0.04;
            timings.HeartbeatTimeoutSeconds = 0.2;
            var channel = Channel(() => Interlocked.Increment(ref attempts) == 1 ? first : second, timings: timings);
            try
            {
                var join = await NextMessage(first, "phx_join");
                Reply(first, join, "ok");
                var heartbeat = await NextMessage(first, "heartbeat");
                Assert.That(heartbeat.topic, Is.EqualTo("phoenix"));
                Reply(first, heartbeat, "ok");
                LogAssert.Expect(LogType.Warning, Disconnected);
                await NextMessage(second, "phx_join");
                Assert.That(channel.IsLive, Is.False);
                Assert.That(first.Disposed, Is.True);
            }
            finally { await Stop(channel); }
        }

        [Test]
        public async Task RotatedTokenIsSentWithoutRejoiningAndSendsAreSerialized()
        {
            var socket = new FakeSocket();
            var sessions = new Sessions();
            var timings = Timings();
            timings.TokenRefreshSeconds = 0.04;
            timings.HeartbeatSeconds = 0.04;
            var channel = Channel(() => socket, sessions, timings);
            try
            {
                var join = await NextMessage(socket, "phx_join");
                Reply(socket, join, "ok");
                await Until(() => channel.IsLive);
                sessions.Rotate();
                var update = await NextMessage(socket, "access_token");
                Assert.That(update.topic, Is.EqualTo(join.topic));
                Assert.That(update.join_ref, Is.EqualTo(join.join_ref));
                Assert.That(update.payload.access_token, Is.EqualTo("test-access-after"));
                Assert.That(update.@ref, Is.Not.EqualTo(join.@ref));
                Assert.That(channel.JoinGeneration, Is.EqualTo(1));
                Assert.That(socket.ConcurrentSendDetected, Is.False);
            }
            finally { await Stop(channel); }
        }

        private static SupabaseRealtimeChannel.RealtimeTimings Timings()
        {
            return new SupabaseRealtimeChannel.RealtimeTimings { TickSeconds = 0.01, ReconnectSeconds = 0.01 };
        }

        private static SupabaseRealtimeChannel Channel(Func<FakeSocket> sockets, Sessions sessions = null,
            SupabaseRealtimeChannel.RealtimeTimings timings = null)
        {
            return new SupabaseRealtimeChannel("https://example.invalid", "test-publishable", sessions ?? new Sessions(), TeamId,
                (uri, token) => Task.FromResult<WebSocket>(sockets()), timings ?? Timings());
        }

        private static async Task Stop(SupabaseRealtimeChannel channel)
        {
            channel.Dispose();
            channel.Dispose();
            Assert.That(channel.IsLive, Is.False);
            Assert.That(await Task.WhenAny(channel.Completion, Task.Delay(3000)), Is.SameAs(channel.Completion));
            await channel.Completion;
        }

        private static async Task Until(Func<bool> condition)
        {
            var clock = Stopwatch.StartNew();
            while (!condition())
            {
                if (clock.Elapsed.TotalSeconds > 3) Assert.Fail("Timed out waiting for the fake Realtime socket.");
                await Task.Delay(5);
            }
        }

        private static async Task<WireMessage> NextMessage(FakeSocket socket, string eventName)
        {
            WireMessage found = null;
            await Until(() =>
            {
                while (socket.Sent.TryDequeue(out var message))
                {
                    if (message.@event == eventName) { found = message; return true; }
                }
                return false;
            });
            return found;
        }

        private static void Reply(FakeSocket socket, WireMessage request, string status, string reference = null)
        {
            socket.Feed(JsonUtility.ToJson(new WireMessage
            {
                topic = request.topic, @event = "phx_reply", @ref = reference ?? request.@ref, join_ref = request.join_ref,
                payload = new Payload { status = status }
            }));
        }

        private static void Broadcast(FakeSocket socket, string topic, string kind,
            string eventName = "team_signal", bool fragmented = false)
        {
            socket.Feed(JsonUtility.ToJson(new WireMessage
            {
                topic = topic, @event = "broadcast",
                payload = new Payload { @event = eventName, payload = new Signal { kind = kind } }
            }), fragmented);
        }

        private sealed class Sessions : ISupabaseSessionProvider
        {
            private int _rotated;
            public void Rotate() => Interlocked.Exchange(ref _rotated, 1);
            public Task<SupabaseAuthSession> GetValidSessionAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new SupabaseAuthSession(TeamId,
                    Volatile.Read(ref _rotated) == 0 ? "test-access-before" : "test-access-after",
                    "test-refresh", DateTimeOffset.UtcNow.AddHours(1)));
            }
        }

        private sealed class FakeSocket : WebSocket
        {
            public readonly ConcurrentQueue<WireMessage> Sent = new ConcurrentQueue<WireMessage>();
            private readonly ConcurrentQueue<Frame> _incoming = new ConcurrentQueue<Frame>();
            private readonly SemaphoreSlim _available = new SemaphoreSlim(0);
            private int _state = (int)WebSocketState.Open;
            private int _sending;
            public bool ConcurrentSendDetected;
            public bool Disposed;
            public int CloseAttempts;
            public override WebSocketState State => (WebSocketState)Volatile.Read(ref _state);
            public override WebSocketCloseStatus? CloseStatus => null;
            public override string CloseStatusDescription => null;
            public override string SubProtocol => null;
            public override void Abort() => Volatile.Write(ref _state, (int)WebSocketState.Aborted);
            public override void Dispose() { Disposed = true; _available.Dispose(); }
            public override Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken token)
                => CloseOutputAsync(status, description, token);
            public override Task CloseOutputAsync(WebSocketCloseStatus status, string description, CancellationToken token)
            {
                CloseAttempts++;
                Volatile.Write(ref _state, (int)WebSocketState.CloseSent);
                return Task.CompletedTask;
            }
            public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool end, CancellationToken token)
            {
                if (Interlocked.Increment(ref _sending) != 1) ConcurrentSendDetected = true;
                try
                {
                    await Task.Delay(1, token);
                    Sent.Enqueue(JsonUtility.FromJson<WireMessage>(Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count)));
                }
                finally { Interlocked.Decrement(ref _sending); }
            }
            public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token)
            {
                await _available.WaitAsync(token);
                if (!_incoming.TryDequeue(out var frame)) throw new InvalidOperationException();
                Array.Copy(frame.Bytes, 0, buffer.Array, buffer.Offset, frame.Bytes.Length);
                return new WebSocketReceiveResult(frame.Bytes.Length, WebSocketMessageType.Text, frame.End);
            }
            public void Feed(string json, bool fragmented = false)
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                var size = fragmented ? 3 : bytes.Length;
                for (var offset = 0; offset < bytes.Length; offset += size)
                {
                    var length = Math.Min(size, bytes.Length - offset);
                    var part = new byte[length];
                    Array.Copy(bytes, offset, part, 0, length);
                    _incoming.Enqueue(new Frame { Bytes = part, End = offset + length == bytes.Length });
                    _available.Release();
                }
            }
            private sealed class Frame { public byte[] Bytes; public bool End; }
        }

#pragma warning disable 0649
        [Serializable] private sealed class WireMessage { public string topic, @event, @ref, join_ref; public Payload payload; }
        [Serializable] private sealed class Payload { public string status, @event, access_token; public Config config; public Signal payload; }
        [Serializable] private sealed class Config { public bool @private; public BroadcastOptions broadcast; public PresenceOptions presence; public Signal[] postgres_changes; }
        [Serializable] private sealed class BroadcastOptions { public bool ack, self; }
        [Serializable] private sealed class PresenceOptions { public bool enabled; }
        [Serializable] private sealed class Signal { public string kind; }
#pragma warning restore 0649
    }
}

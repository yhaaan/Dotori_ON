using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TeamOverlay.Core;
using UnityEngine;

namespace TeamOverlay.Supabase
{
    /// <summary>
    /// The live backend. Mutations go through the transaction-safe RPCs and the
    /// roster is read from <c>member_current_state</c> under RLS.
    ///
    /// Team events are derived by diffing consecutive snapshots rather than read
    /// from <c>team_events</c>. That keeps this a single polled request and means
    /// a missed poll degrades into a late event instead of a lost one. Swapping in
    /// a Realtime subscription later only has to replace <see cref="Poll"/>.
    /// </summary>
    public sealed class SupabaseTeamBackend : ITeamBackend, ITeamStatistics, IDisposable
    {
        private const string StateSelect =
            "member_id,attendance_session_id,attendance_status,activity_status," +
            "connection_status,checked_in_at,status_started_at,last_heartbeat_at," +
            "last_checked_out_at,updated_at,status_note,members!inner(display_name,avatar_key,sort_order,is_active)";

        private readonly ObservableStream<TeamEvent> _events = new ObservableStream<TeamEvent>();
        private readonly object _gate = new object();
        private readonly ISupabaseSessionProvider _sessionProvider;
        private readonly ISupabaseHttpTransport _transport;
        private readonly string _projectUrl;
        private readonly string _publishableKey;
        private readonly Guid _memberId;
        private readonly Guid _clientInstanceId;

        private Dictionary<string, MemberState> _previousSnapshot;
        private Guid? _openAttendanceSessionId;
        private bool _disposed;

        public SupabaseTeamBackend(
            string projectUrl,
            string publishableKey,
            ISupabaseHttpTransport transport,
            ISupabaseSessionProvider sessionProvider,
            Guid memberId,
            Guid clientInstanceId)
        {
            _projectUrl = (projectUrl ?? string.Empty).TrimEnd('/');
            if (!Uri.TryCreate(_projectUrl, UriKind.Absolute, out _))
            {
                throw new ArgumentException("A valid Supabase project URL is required.", nameof(projectUrl));
            }

            _publishableKey = !string.IsNullOrWhiteSpace(publishableKey)
                ? publishableKey
                : throw new ArgumentException("A Supabase publishable key is required.", nameof(publishableKey));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));

            if (memberId == Guid.Empty)
            {
                throw new ArgumentException("A member id is required.", nameof(memberId));
            }

            if (clientInstanceId == Guid.Empty)
            {
                throw new ArgumentException("A client instance id is required.", nameof(clientInstanceId));
            }

            _memberId = memberId;
            _clientInstanceId = clientInstanceId;
        }

        public string LocalMemberId => _memberId.ToString("D");

        public IObservable<TeamEvent> Events => _events;

        /// <summary>
        /// The open attendance session, or null when this member is clocked out.
        /// Heartbeats are addressed to a specific session, so a heartbeat before
        /// the first successful state read is skipped rather than guessed.
        /// </summary>
        public Guid? OpenAttendanceSessionId
        {
            get
            {
                lock (_gate)
                {
                    return _openAttendanceSessionId;
                }
            }
        }

        public async Task<IReadOnlyList<MemberState>> GetTeamStateAsync(
            CancellationToken cancellationToken)
        {
            var response = await SendAuthorizedAsync(
                "GET",
                _projectUrl + "/rest/v1/member_current_state?select=" + StateSelect,
                null,
                cancellationToken);

            var members = ParseStates(response.Body);
            PublishTransitions(members);
            return members;
        }

        public async Task CheckInAsync(CancellationToken cancellationToken)
        {
            var body = JsonUtility.ToJson(new CheckInRequest
            {
                p_member_id = _memberId.ToString("D"),
                p_client_instance_id = _clientInstanceId.ToString("D")
            });
            var response = await CallRpcAsync("check_in", body, cancellationToken);
            CaptureSessionId(response.Body);
        }

        public async Task ChangeActivityAsync(
            ActivityStatus status,
            CancellationToken cancellationToken)
        {
            var body = JsonUtility.ToJson(new ChangeActivityRequest
            {
                p_member_id = _memberId.ToString("D"),
                p_new_status = ToServerActivity(status)
            });
            var response = await CallRpcAsync("change_activity", body, cancellationToken);
            CaptureSessionId(response.Body);
        }

        public async Task CheckOutAsync(
            CheckoutReason reason,
            CancellationToken cancellationToken)
        {
            var body = JsonUtility.ToJson(new CheckOutRequest
            {
                p_member_id = _memberId.ToString("D"),
                p_reason = ToServerCheckoutReason(reason)
            });
            await CallRpcAsync("check_out", body, cancellationToken);
            lock (_gate)
            {
                _openAttendanceSessionId = null;
            }
        }

        public async Task<IReadOnlyList<MemberDailyStat>> GetDailyStatsAsync(
            string memberId,
            DateTime fromLocalDate,
            DateTime toLocalDate,
            CancellationToken cancellationToken)
        {
            var body = JsonUtility.ToJson(new DailyStatsRequest
            {
                p_member_id = string.IsNullOrWhiteSpace(memberId) ? LocalMemberId : memberId,
                p_from = FormatLocalDate(fromLocalDate),
                p_to = FormatLocalDate(toLocalDate)
            });
            var response = await CallRpcAsync("member_daily_stats", body, cancellationToken);

            DailyStatArrayDocument document;
            try
            {
                document = JsonUtility.FromJson<DailyStatArrayDocument>(
                    "{\"items\":" + response.Body + "}");
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("Supabase 통계 응답을 읽지 못했습니다.", exception);
            }

            var results = new List<MemberDailyStat>(document?.items?.Length ?? 0);
            foreach (var item in document?.items ?? new DailyStatDocument[0])
            {
                results.Add(new MemberDailyStat(
                    ParseLocalDate(item.local_date),
                    item.attendance_seconds,
                    item.work_seconds,
                    item.break_seconds,
                    item.meal_seconds));
            }

            return new ReadOnlyCollection<MemberDailyStat>(results);
        }

        public async Task<IReadOnlyList<TeamRankingEntry>> GetWorkRankingAsync(
            DateTime fromLocalDate,
            DateTime toLocalDate,
            CancellationToken cancellationToken)
        {
            var body = JsonUtility.ToJson(new RankingRequest
            {
                p_from = FormatLocalDate(fromLocalDate),
                p_to = FormatLocalDate(toLocalDate)
            });
            var response = await CallRpcAsync("team_work_ranking", body, cancellationToken);

            RankingArrayDocument document;
            try
            {
                document = JsonUtility.FromJson<RankingArrayDocument>(
                    "{\"items\":" + response.Body + "}");
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("Supabase 랭킹 응답을 읽지 못했습니다.", exception);
            }

            var results = new List<TeamRankingEntry>(document?.items?.Length ?? 0);
            foreach (var item in document?.items ?? new RankingDocument[0])
            {
                results.Add(new TeamRankingEntry(
                    item.member_id,
                    item.display_name,
                    item.sort_order,
                    item.work_seconds,
                    item.attendance_seconds));
            }

            return new ReadOnlyCollection<TeamRankingEntry>(results);
        }

        public async Task SetStatusNoteAsync(string note, CancellationToken cancellationToken)
        {
            var body = JsonUtility.ToJson(new StatusNoteRequest
            {
                p_member_id = _memberId.ToString("D"),
                // The server treats an empty string as "clear", so a null note does
                // not need a separate call.
                p_note = string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim()
            });
            await CallRpcAsync("set_status_note", body, cancellationToken);
        }

        public async Task SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            Guid sessionId;
            lock (_gate)
            {
                if (!_openAttendanceSessionId.HasValue)
                {
                    return;
                }

                sessionId = _openAttendanceSessionId.Value;
            }

            var body = JsonUtility.ToJson(new HeartbeatRequest
            {
                p_member_id = _memberId.ToString("D"),
                p_attendance_session_id = sessionId.ToString("D"),
                p_client_instance_id = _clientInstanceId.ToString("D")
            });

            try
            {
                await CallRpcAsync("heartbeat", body, cancellationToken);
            }
            catch (SupabaseApiException exception) when (
                exception.ServerMessage == "attendance_session_not_open"
                || exception.ServerMessage == "client_instance_mismatch"
                || exception.ServerMessage == "member_state_session_mismatch")
            {
                // The server already closed or reassigned this session. Forget it
                // so the caller stops heartbeating into a session that is gone.
                lock (_gate)
                {
                    if (_openAttendanceSessionId == sessionId)
                    {
                        _openAttendanceSessionId = null;
                    }
                }

                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _events.Dispose();
        }

        private async Task<SupabaseHttpResponse> CallRpcAsync(
            string function,
            string body,
            CancellationToken cancellationToken)
        {
            return await SendAuthorizedAsync(
                "POST",
                _projectUrl + "/rest/v1/rpc/" + function,
                body,
                cancellationToken);
        }

        private async Task<SupabaseHttpResponse> SendAuthorizedAsync(
            string method,
            string url,
            string body,
            CancellationToken cancellationToken)
        {
            var session = await _sessionProvider.GetValidSessionAsync(cancellationToken);
            var request = new SupabaseHttpRequest(
                method,
                url,
                body,
                new Dictionary<string, string>
                {
                    { "apikey", _publishableKey },
                    { "Authorization", "Bearer " + session.AccessToken }
                });

            var response = await _transport.SendAsync(request, cancellationToken);
            SupabaseErrors.EnsureSuccess(response);
            return response;
        }

        private void CaptureSessionId(string body)
        {
            StateDocument document;
            try
            {
                document = JsonUtility.FromJson<StateDocument>(body);
            }
            catch (ArgumentException)
            {
                return;
            }

            if (document == null
                || !Guid.TryParse(document.attendance_session_id, out var sessionId))
            {
                return;
            }

            lock (_gate)
            {
                _openAttendanceSessionId = sessionId;
            }
        }

        private IReadOnlyList<MemberState> ParseStates(string body)
        {
            StateArrayDocument document;
            try
            {
                document = JsonUtility.FromJson<StateArrayDocument>("{\"items\":" + body + "}");
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("Supabase 팀 상태 응답을 읽지 못했습니다.", exception);
            }

            if (document?.items == null)
            {
                throw new InvalidOperationException("Supabase 팀 상태 응답이 비어 있습니다.");
            }

            var members = new List<MemberState>(document.items.Length);
            foreach (var item in document.items)
            {
                if (item?.members == null || !item.members.is_active)
                {
                    continue;
                }

                members.Add(ToMemberState(item));
            }

            var localState = members.Find(member =>
                string.Equals(member.MemberId, LocalMemberId, StringComparison.Ordinal));
            lock (_gate)
            {
                _openAttendanceSessionId = localState != null && localState.IsClockedIn
                    ? ParseGuid(FindSessionId(document.items, LocalMemberId))
                    : null;
            }

            return new ReadOnlyCollection<MemberState>(members);
        }

        /// <summary>
        /// Emits the transitions between the previous snapshot and this one. The
        /// first snapshot only seeds the baseline: replaying the whole roster as
        /// fresh check-ins would ring the notification tone on every launch.
        /// </summary>
        private void PublishTransitions(IReadOnlyList<MemberState> members)
        {
            var current = new Dictionary<string, MemberState>(members.Count, StringComparer.Ordinal);
            foreach (var member in members)
            {
                current[member.MemberId] = member;
            }

            Dictionary<string, MemberState> previous;
            lock (_gate)
            {
                previous = _previousSnapshot;
                _previousSnapshot = current;
            }

            if (previous == null)
            {
                return;
            }

            foreach (var member in members)
            {
                if (!previous.TryGetValue(member.MemberId, out var before))
                {
                    continue;
                }

                if (before.AttendanceStatus != member.AttendanceStatus)
                {
                    Publish(
                        member.IsClockedIn ? TeamEventType.MemberCheckedIn : TeamEventType.MemberCheckedOut,
                        member);
                }
                else if (member.IsClockedIn && before.ActivityStatus != member.ActivityStatus)
                {
                    Publish(TeamEventType.MemberActivityChanged, member);
                }
            }
        }

        private void Publish(TeamEventType type, MemberState state)
        {
            _events.Publish(new TeamEvent(
                state.MemberId + "|" + state.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture) + "|" + type,
                type,
                state.MemberId,
                state.UpdatedAtUtc,
                state,
                state.ActivityStatus));
        }

        private static string FindSessionId(StateDocument[] items, string memberId)
        {
            foreach (var item in items)
            {
                if (item != null && string.Equals(item.member_id, memberId, StringComparison.OrdinalIgnoreCase))
                {
                    return item.attendance_session_id;
                }
            }

            return null;
        }

        private static MemberState ToMemberState(StateDocument document)
        {
            var attendance = ParseAttendance(document.attendance_status);
            var isClockedIn = attendance == AttendanceStatus.ClockedIn;
            return new MemberState(
                document.member_id,
                document.members.display_name,
                document.members.avatar_key,
                document.members.sort_order,
                attendance,
                isClockedIn ? ParseActivity(document.activity_status) : (ActivityStatus?)null,
                ParseConnection(document.connection_status),
                isClockedIn ? ParseTimestamp(document.checked_in_at) : null,
                isClockedIn ? ParseTimestamp(document.status_started_at) : null,
                ParseTimestamp(document.last_heartbeat_at),
                ParseTimestamp(document.last_checked_out_at),
                ParseTimestamp(document.updated_at) ?? DateTimeOffset.UtcNow,
                document.status_note);
        }

        private static AttendanceStatus ParseAttendance(string value)
        {
            return string.Equals(value, "clocked_in", StringComparison.Ordinal)
                ? AttendanceStatus.ClockedIn
                : AttendanceStatus.ClockedOut;
        }

        private static ActivityStatus ParseActivity(string value)
        {
            switch (value)
            {
                case "break": return ActivityStatus.Break;
                case "meal": return ActivityStatus.Meal;
                default: return ActivityStatus.Working;
            }
        }

        private static ConnectionStatus ParseConnection(string value)
        {
            switch (value)
            {
                case "connected": return ConnectionStatus.Connected;
                case "degraded": return ConnectionStatus.Degraded;
                default: return ConnectionStatus.Disconnected;
            }
        }

        private static DateTimeOffset? ParseTimestamp(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                throw new InvalidOperationException("Supabase 시각 값을 읽지 못했습니다: " + value);
            }

            return parsed.ToUniversalTime();
        }

        private static Guid? ParseGuid(string value)
        {
            return Guid.TryParse(value, out var parsed) ? parsed : (Guid?)null;
        }

        /// <summary>
        /// The server takes a local calendar date, so this must not go through UTC:
        /// converting would shift the day for anyone east of Greenwich.
        /// </summary>
        private static string FormatLocalDate(DateTime localDate)
        {
            return localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static DateTime ParseLocalDate(string value)
        {
            return DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? parsed
                : throw new InvalidOperationException("Supabase 날짜 값을 읽지 못했습니다: " + value);
        }

        private static string ToServerActivity(ActivityStatus status)
        {
            switch (status)
            {
                case ActivityStatus.Working: return "working";
                case ActivityStatus.Break: return "break";
                case ActivityStatus.Meal: return "meal";
                default: throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private static string ToServerCheckoutReason(CheckoutReason reason)
        {
            switch (reason)
            {
                case CheckoutReason.Manual: return "manual";
                case CheckoutReason.AppExit: return "app_exit";
                case CheckoutReason.OsShutdown: return "os_shutdown";
                default:
                    // auto_timeout and admin are reserved for server-side closure;
                    // check_out rejects them with checkout_reason_not_allowed.
                    throw new ArgumentOutOfRangeException(
                        nameof(reason),
                        reason,
                        "Only manual, app exit and OS shutdown may be sent by a client.");
            }
        }

        [Serializable]
        private sealed class StateArrayDocument
        {
            public StateDocument[] items;
        }

        [Serializable]
        private sealed class StateDocument
        {
            public string member_id;
            public string attendance_session_id;
            public string attendance_status;
            public string activity_status;
            public string connection_status;
            public string checked_in_at;
            public string status_started_at;
            public string last_heartbeat_at;
            public string last_checked_out_at;
            public string updated_at;
            public string status_note;
            public MemberDocument members;
        }

        [Serializable]
        private sealed class MemberDocument
        {
            public string display_name;
            public string avatar_key;
            public int sort_order;
            public bool is_active;
        }

        [Serializable]
        private sealed class CheckInRequest
        {
            public string p_member_id;
            public string p_client_instance_id;
        }

        [Serializable]
        private sealed class ChangeActivityRequest
        {
            public string p_member_id;
            public string p_new_status;
        }

        [Serializable]
        private sealed class CheckOutRequest
        {
            public string p_member_id;
            public string p_reason;
        }

        [Serializable]
        private sealed class DailyStatsRequest
        {
            public string p_member_id;
            public string p_from;
            public string p_to;
        }

        [Serializable]
        private sealed class RankingRequest
        {
            public string p_from;
            public string p_to;
        }

        [Serializable]
        private sealed class DailyStatArrayDocument
        {
            public DailyStatDocument[] items;
        }

        [Serializable]
        private sealed class DailyStatDocument
        {
            public string local_date;
            public int attendance_seconds;
            public int work_seconds;
            public int break_seconds;
            public int meal_seconds;
        }

        [Serializable]
        private sealed class RankingArrayDocument
        {
            public RankingDocument[] items;
        }

        [Serializable]
        private sealed class RankingDocument
        {
            public string member_id;
            public string display_name;
            public int sort_order;
            public int work_seconds;
            public int attendance_seconds;
        }

        [Serializable]
        private sealed class StatusNoteRequest
        {
            public string p_member_id;
            public string p_note;
        }

        [Serializable]
        private sealed class HeartbeatRequest
        {
            public string p_member_id;
            public string p_attendance_session_id;
            public string p_client_instance_id;
        }
    }
}

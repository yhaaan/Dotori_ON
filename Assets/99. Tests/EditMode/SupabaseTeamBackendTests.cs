using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using DOTORION.Core;
using DOTORION.Supabase;

namespace DOTORION.Tests.EditMode
{
    public sealed class SupabaseTeamBackendTests
    {
        private static readonly Guid LocalMemberId =
            new Guid("11111111-2222-4333-8444-555555555555");
        private static readonly Guid TeammateId =
            new Guid("22222222-3333-4444-8555-666666666666");
        private static readonly Guid ClientInstanceId =
            new Guid("33333333-4444-4555-8666-777777777777");
        private static readonly Guid SessionId =
            new Guid("44444444-5555-4666-8777-888888888888");

        [Test]
        public async Task GetTeamState_MapsRowsAndSendsAuthorizedRequest()
        {
            var transport = new QueueTransport(Ok(
                "[" + ClockedIn(TeammateId, "하늘", 0) + "," + ClockedOut(LocalMemberId, "길동", 1) + "]"));
            var backend = CreateBackend(transport);

            var members = await backend.GetTeamStateAsync(CancellationToken.None);

            Assert.That(members.Count, Is.EqualTo(2));
            var teammate = Find(members, TeammateId);
            Assert.That(teammate.DisplayName, Is.EqualTo("하늘"));
            Assert.That(teammate.AttendanceStatus, Is.EqualTo(AttendanceStatus.ClockedIn));
            Assert.That(teammate.ActivityStatus, Is.EqualTo(ActivityStatus.Working));
            Assert.That(teammate.ConnectionStatus, Is.EqualTo(ConnectionStatus.Connected));
            Assert.That(teammate.CheckedInAtUtc, Is.Not.Null);

            var local = Find(members, LocalMemberId);
            Assert.That(local.AttendanceStatus, Is.EqualTo(AttendanceStatus.ClockedOut));
            Assert.That(local.ActivityStatus, Is.Null);
            Assert.That(local.CheckedInAtUtc, Is.Null);
            Assert.That(local.LastCheckedOutAtUtc, Is.Not.Null);

            var request = transport.Requests[0];
            Assert.That(request.Method, Is.EqualTo("GET"));
            Assert.That(request.Url, Does.Contain("/rest/v1/member_current_state?select="));
            Assert.That(request.Url, Does.Contain("members!inner"));
            Assert.That(request.Headers["Authorization"], Is.EqualTo("Bearer access-token"));
            Assert.That(request.Headers["apikey"], Is.EqualTo("sb_publishable_test"));
        }

        [Test]
        public async Task GetTeamState_InactiveMemberIsExcluded()
        {
            var transport = new QueueTransport(Ok(
                "[" + ClockedOut(TeammateId, "퇴사자", 2, isActive: false) + "]"));
            var backend = CreateBackend(transport);

            var members = await backend.GetTeamStateAsync(CancellationToken.None);

            Assert.That(members, Is.Empty);
        }

        [Test]
        public async Task FirstSnapshot_PublishesNoEvents()
        {
            var transport = new QueueTransport(Ok("[" + ClockedIn(TeammateId, "하늘", 0) + "]"));
            var backend = CreateBackend(transport);
            var observer = new RecordingObserver();
            backend.Events.Subscribe(observer);

            await backend.GetTeamStateAsync(CancellationToken.None);

            Assert.That(observer.Events, Is.Empty);
        }

        [Test]
        public async Task TeammateClockingIn_PublishesCheckedInEvent()
        {
            var transport = new QueueTransport(
                Ok("[" + ClockedOut(TeammateId, "하늘", 0) + "]"),
                Ok("[" + ClockedIn(TeammateId, "하늘", 0) + "]"));
            var backend = CreateBackend(transport);
            var observer = new RecordingObserver();
            backend.Events.Subscribe(observer);

            await backend.GetTeamStateAsync(CancellationToken.None);
            await backend.GetTeamStateAsync(CancellationToken.None);

            Assert.That(observer.Events.Count, Is.EqualTo(1));
            Assert.That(observer.Events[0].Type, Is.EqualTo(TeamEventType.MemberCheckedIn));
            Assert.That(observer.Events[0].ActorMemberId, Is.EqualTo(TeammateId.ToString("D")));
        }

        [Test]
        public async Task TeammateChangingActivity_PublishesActivityChangedEvent()
        {
            var transport = new QueueTransport(
                Ok("[" + ClockedIn(TeammateId, "하늘", 0) + "]"),
                Ok("[" + ClockedIn(TeammateId, "하늘", 0, activity: "meal") + "]"));
            var backend = CreateBackend(transport);
            var observer = new RecordingObserver();
            backend.Events.Subscribe(observer);

            await backend.GetTeamStateAsync(CancellationToken.None);
            await backend.GetTeamStateAsync(CancellationToken.None);

            Assert.That(observer.Events.Count, Is.EqualTo(1));
            Assert.That(observer.Events[0].Type, Is.EqualTo(TeamEventType.MemberActivityChanged));
            Assert.That(observer.Events[0].ActivityStatus, Is.EqualTo(ActivityStatus.Meal));
        }

        [Test]
        public async Task CheckIn_PostsRpcAndRemembersSessionForHeartbeat()
        {
            var transport = new QueueTransport(
                Ok(StateObject(LocalMemberId, SessionId)),
                Ok(StateObject(LocalMemberId, SessionId)));
            var backend = CreateBackend(transport);

            await backend.CheckInAsync(CancellationToken.None);
            await backend.SendHeartbeatAsync(CancellationToken.None);

            Assert.That(transport.Requests[0].Url, Does.EndWith("/rest/v1/rpc/check_in"));
            Assert.That(transport.Requests[0].Body, Does.Contain(LocalMemberId.ToString("D")));
            Assert.That(transport.Requests[0].Body, Does.Contain(ClientInstanceId.ToString("D")));

            Assert.That(transport.Requests[1].Url, Does.EndWith("/rest/v1/rpc/heartbeat"));
            Assert.That(transport.Requests[1].Body, Does.Contain(SessionId.ToString("D")));
            Assert.That(backend.OpenAttendanceSessionId, Is.EqualTo(SessionId));
        }

        [Test]
        public async Task Heartbeat_WithoutOpenSession_SendsNothing()
        {
            var transport = new QueueTransport();
            var backend = CreateBackend(transport);

            await backend.SendHeartbeatAsync(CancellationToken.None);

            Assert.That(transport.Requests, Is.Empty);
        }

        [Test]
        public async Task Heartbeat_OnClosedSession_ForgetsSessionAndSurfacesTheError()
        {
            var transport = new QueueTransport(
                Ok(StateObject(LocalMemberId, SessionId)),
                new SupabaseHttpResponse(400, "{\"message\":\"attendance_session_not_open\"}"));
            var backend = CreateBackend(transport);
            await backend.CheckInAsync(CancellationToken.None);

            Assert.ThrowsAsync<SupabaseApiException>(
                async () => await backend.SendHeartbeatAsync(CancellationToken.None));
            Assert.That(backend.OpenAttendanceSessionId, Is.Null);
        }

        [Test]
        public async Task CheckOut_ClearsOpenSession()
        {
            var transport = new QueueTransport(
                Ok(StateObject(LocalMemberId, SessionId)),
                Ok(StateObject(LocalMemberId, null)));
            var backend = CreateBackend(transport);
            await backend.CheckInAsync(CancellationToken.None);

            await backend.CheckOutAsync(CheckoutReason.Manual, CancellationToken.None);

            Assert.That(transport.Requests[1].Url, Does.EndWith("/rest/v1/rpc/check_out"));
            Assert.That(transport.Requests[1].Body, Does.Contain("\"manual\""));
            Assert.That(backend.OpenAttendanceSessionId, Is.Null);
        }

        [Test]
        public void CheckOut_RefusesServerOnlyReasons()
        {
            var backend = CreateBackend(new QueueTransport());

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                async () => await backend.CheckOutAsync(CheckoutReason.AutoTimeout, CancellationToken.None));
        }

        [Test]
        public async Task ChangeActivity_SendsServerEnumText()
        {
            var transport = new QueueTransport(Ok(StateObject(LocalMemberId, SessionId)));
            var backend = CreateBackend(transport);

            await backend.ChangeActivityAsync(ActivityStatus.Break, CancellationToken.None);

            Assert.That(transport.Requests[0].Url, Does.EndWith("/rest/v1/rpc/change_activity"));
            Assert.That(transport.Requests[0].Body, Does.Contain("\"break\""));
        }

        [Test]
        public async Task PeriodStats_ParsesEachBucketAndKeepsLocalDates()
        {
            var transport = new QueueTransport(Ok(
                "[{\"bucket_start\":\"2026-08-25\",\"bucket_end\":\"2026-08-25\","
                + "\"attendance_seconds\":0,"
                + "\"work_seconds\":0,\"break_seconds\":0,\"meal_seconds\":0,\"daily_check_in_days\":0},"
                + "{\"bucket_start\":\"2026-08-27\",\"bucket_end\":\"2026-08-27\","
                + "\"attendance_seconds\":3097,"
                + "\"work_seconds\":482,\"break_seconds\":1628,\"meal_seconds\":987,\"daily_check_in_days\":1}]"));
            var backend = CreateBackend(transport);

            var stats = await backend.GetPeriodStatsAsync(
                LocalMemberId.ToString("D"),
                StatisticsRange.Resolve(StatisticsPeriod.LastSevenDays, new DateTime(2026, 8, 27)),
                CancellationToken.None);

            Assert.That(stats.Count, Is.EqualTo(2));
            Assert.That(stats[0].BucketStart, Is.EqualTo(new DateTime(2026, 8, 25)));
            Assert.That(stats[0].HasActivity, Is.False);
            Assert.That(stats[0].HasDailyCheckIn, Is.False);

            var busy = stats[1];
            Assert.That(busy.BucketStart, Is.EqualTo(new DateTime(2026, 8, 27)));
            Assert.That(busy.BucketEnd, Is.EqualTo(new DateTime(2026, 8, 27)));
            Assert.That(busy.AttendanceSeconds, Is.EqualTo(3097));
            Assert.That(busy.WorkSeconds, Is.EqualTo(482));
            Assert.That(busy.HasDailyCheckIn, Is.True);
            Assert.That(
                busy.WorkSeconds + busy.BreakSeconds + busy.MealSeconds,
                Is.EqualTo(busy.AttendanceSeconds),
                "activity must account for the whole attendance span");

            Assert.That(transport.Requests[0].Url, Does.EndWith("/rest/v1/rpc/member_period_stats"));
            Assert.That(transport.Requests[0].Body, Does.Contain("2026-08-21"));
            Assert.That(transport.Requests[0].Body, Does.Contain("2026-08-27"));
            Assert.That(transport.Requests[0].Body, Does.Contain("\"p_bucket\":\"day\""));
        }

        [Test]
        public async Task PeriodStats_SendsTheLocalDateWithoutShiftingThroughUtc()
        {
            // A date converted through UTC would land on the previous day for
            // anyone east of Greenwich, quietly reporting the wrong day.
            var transport = new QueueTransport(Ok("[]"));
            var backend = CreateBackend(transport);

            await backend.GetPeriodStatsAsync(
                null,
                StatisticsRange.Resolve(
                    StatisticsPeriod.LastSevenDays,
                    new DateTime(2026, 1, 1, 0, 30, 0)),
                CancellationToken.None);

            Assert.That(transport.Requests[0].Body, Does.Contain("2026-01-01"));
            Assert.That(transport.Requests[0].Body, Does.Not.Contain("2025-12-31"));
        }

        [Test]
        public async Task PeriodStats_LeavesTheStartDateOutWhenAskingForEverything()
        {
            // An empty string is not a date to Postgres, so the all-time request
            // has to omit p_from and let the server default resolve it.
            var transport = new QueueTransport(Ok("[]"));
            var backend = CreateBackend(transport);

            await backend.GetPeriodStatsAsync(
                null,
                StatisticsRange.Resolve(StatisticsPeriod.AllTime, new DateTime(2026, 8, 27)),
                CancellationToken.None);

            Assert.That(transport.Requests[0].Body, Does.Not.Contain("p_from"));
            Assert.That(transport.Requests[0].Body, Does.Contain("\"p_bucket\":\"month\""));
            Assert.That(transport.Requests[0].Body, Does.Contain("2026-08-27"));
        }

        [Test]
        public async Task Ranking_ParsesEveryMetricInServerOrder()
        {
            var transport = new QueueTransport(Ok(
                "[{\"member_id\":\"" + TeammateId.ToString("D") + "\","
                + "\"display_name\":\"하늘\",\"sort_order\":0,"
                + "\"work_seconds\":482,\"attendance_seconds\":3097,"
                + "\"break_seconds\":1628,\"meal_seconds\":987},"
                + "{\"member_id\":\"" + LocalMemberId.ToString("D") + "\","
                + "\"display_name\":\"길동\",\"sort_order\":1,"
                + "\"work_seconds\":278,\"attendance_seconds\":2879,"
                + "\"break_seconds\":2000,\"meal_seconds\":601}]"));
            var backend = CreateBackend(transport);

            var ranking = await backend.GetRankingAsync(
                StatisticsRange.Resolve(StatisticsPeriod.LastSevenDays, new DateTime(2026, 8, 27)),
                CancellationToken.None);

            Assert.That(ranking.Count, Is.EqualTo(2));
            Assert.That(ranking[0].DisplayName, Is.EqualTo("하늘"));
            Assert.That(ranking[0].WorkSeconds, Is.EqualTo(482));
            Assert.That(ranking[0].SecondsFor(RankingMetric.Break), Is.EqualTo(1628));
            Assert.That(ranking[1].DisplayName, Is.EqualTo("길동"));
            Assert.That(ranking[1].SecondsFor(RankingMetric.Meal), Is.EqualTo(601));
            Assert.That(transport.Requests[0].Url, Does.EndWith("/rest/v1/rpc/team_activity_ranking"));
        }

        [Test]
        public async Task Ranking_LeavesTheStartDateOutWhenAskingForEverything()
        {
            var transport = new QueueTransport(Ok("[]"));
            var backend = CreateBackend(transport);

            await backend.GetRankingAsync(
                StatisticsRange.Resolve(StatisticsPeriod.AllTime, new DateTime(2026, 8, 27)),
                CancellationToken.None);

            Assert.That(transport.Requests[0].Body, Does.Not.Contain("p_from"));
        }

        [Test]
        public async Task FirstPoll_SeedsTheNudgeCursorWithoutReplayingOldNudges()
        {
            // Opening the app must not ring the bell for every poke sent while it
            // was closed, so the first read only records where the log ends.
            var transport = new QueueTransport(
                Ok("[" + ClockedIn(TeammateId, "하늘", 0) + "]"),
                Ok("[" + ClockedIn(TeammateId, "하늘", 0) + "]"));
            transport.NudgeResponses.Enqueue(Ok(
                "[{\"id\":\"old\",\"actor_member_id\":\"" + TeammateId.ToString("D") + "\","
                + "\"target_member_id\":null,\"created_at\":\"2026-08-28T01:00:00+00:00\"}]"));
            var backend = CreateBackend(transport);
            var observer = new RecordingObserver();
            backend.Events.Subscribe(observer);

            await backend.GetTeamStateAsync(CancellationToken.None);

            Assert.That(observer.Events.Count, Is.Zero);

            transport.NudgeResponses.Enqueue(Ok(
                "[{\"id\":\"fresh\",\"actor_member_id\":\"" + TeammateId.ToString("D") + "\","
                + "\"target_member_id\":\"" + LocalMemberId.ToString("D") + "\","
                + "\"created_at\":\"2026-08-28T02:00:00+00:00\"}]"));
            await backend.GetTeamStateAsync(CancellationToken.None);

            Assert.That(observer.Events.Count, Is.EqualTo(1));
            Assert.That(observer.Events[0].Type, Is.EqualTo(TeamEventType.MemberNudged));
            Assert.That(observer.Events[0].ActorMemberId, Is.EqualTo(TeammateId.ToString("D")));
            Assert.That(observer.Events[0].TargetMemberId, Is.EqualTo(LocalMemberId.ToString("D")));
            Assert.That(observer.Events[0].State.DisplayName, Is.EqualTo("하늘"));

            // The second read is a window that opens where the first one closed.
            var nudgeReads = transport.Requests.FindAll(request => request.Url.Contains("/team_events"));
            Assert.That(nudgeReads[0].Url, Does.Contain("order=created_at.desc"));
            Assert.That(nudgeReads[1].Url, Does.Contain("created_at=gt."));
            Assert.That(nudgeReads[1].Url, Does.Contain("2026-08-28T01"));
        }

        [Test]
        public async Task SendNudge_OmitsTheTargetForTheWholeTeam()
        {
            var transport = new QueueTransport(Ok("null"), Ok("null"));
            var backend = CreateBackend(transport);

            await backend.SendNudgeAsync(TeammateId.ToString("D"), CancellationToken.None);
            await backend.SendNudgeAsync(null, CancellationToken.None);

            Assert.That(transport.Requests[0].Url, Does.EndWith("/rest/v1/rpc/send_nudge"));
            Assert.That(transport.Requests[0].Body, Does.Contain(TeammateId.ToString("D")));
            Assert.That(transport.Requests[1].Body, Does.Not.Contain("p_target_member_id"));
        }

        [Test]
        public async Task SetAvatarKey_PostsTheCatalogKeyForTheLocalMember()
        {
            var transport = new QueueTransport(Ok("null"), Ok("null"));
            var backend = CreateBackend(transport);

            await backend.SetAvatarKeyAsync("smile-01", CancellationToken.None);
            // Blank is how the picker clears an icon; the server normalises it
            // back to 'default', so it must not need an RPC of its own.
            await backend.SetAvatarKeyAsync("   ", CancellationToken.None);

            Assert.That(transport.Requests[0].Url, Does.EndWith("/rest/v1/rpc/set_avatar_key"));
            Assert.That(transport.Requests[0].Body, Does.Contain("smile-01"));
            Assert.That(transport.Requests[0].Body, Does.Contain(LocalMemberId.ToString("D")));
            Assert.That(transport.Requests[1].Url, Does.EndWith("/rest/v1/rpc/set_avatar_key"));
            Assert.That(transport.Requests[1].Body, Does.Contain("\"p_avatar_key\":\"\""));
        }

        private static SupabaseTeamBackend CreateBackend(ISupabaseHttpTransport transport)
        {
            return new SupabaseTeamBackend(
                "https://project.supabase.co",
                "sb_publishable_test",
                transport,
                new StubSessionProvider(),
                LocalMemberId,
                ClientInstanceId);
        }

        private static MemberState Find(IReadOnlyList<MemberState> members, Guid memberId)
        {
            foreach (var member in members)
            {
                if (member.MemberId == memberId.ToString("D"))
                {
                    return member;
                }
            }

            Assert.Fail("Member " + memberId + " was not mapped.");
            return null;
        }

        private static SupabaseHttpResponse Ok(string body)
        {
            return new SupabaseHttpResponse(200, body);
        }

        private static string ClockedIn(
            Guid memberId,
            string displayName,
            int sortOrder,
            string activity = "working")
        {
            return "{\"member_id\":\"" + memberId.ToString("D") +
                   "\",\"attendance_session_id\":\"" + SessionId.ToString("D") +
                   "\",\"attendance_status\":\"clocked_in\"" +
                   ",\"activity_status\":\"" + activity + "\"" +
                   ",\"connection_status\":\"connected\"" +
                   ",\"checked_in_at\":\"2026-08-26T01:00:00+00:00\"" +
                   ",\"status_started_at\":\"2026-08-26T01:10:00+00:00\"" +
                   ",\"last_heartbeat_at\":\"2026-08-26T01:30:00+00:00\"" +
                   ",\"last_checked_out_at\":null" +
                   ",\"updated_at\":\"2026-08-26T01:30:00+00:00\"" +
                   ",\"members\":" + MemberEmbed(displayName, sortOrder, true) + "}";
        }

        private static string ClockedOut(
            Guid memberId,
            string displayName,
            int sortOrder,
            bool isActive = true)
        {
            return "{\"member_id\":\"" + memberId.ToString("D") +
                   "\",\"attendance_session_id\":null" +
                   ",\"attendance_status\":\"clocked_out\"" +
                   ",\"activity_status\":null" +
                   ",\"connection_status\":\"disconnected\"" +
                   ",\"checked_in_at\":null" +
                   ",\"status_started_at\":null" +
                   ",\"last_heartbeat_at\":null" +
                   ",\"last_checked_out_at\":\"2026-08-25T09:00:00+00:00\"" +
                   ",\"updated_at\":\"2026-08-25T09:00:00+00:00\"" +
                   ",\"members\":" + MemberEmbed(displayName, sortOrder, isActive) + "}";
        }

        private static string MemberEmbed(string displayName, int sortOrder, bool isActive)
        {
            return "{\"display_name\":\"" + displayName +
                   "\",\"avatar_key\":\"default\",\"sort_order\":" + sortOrder +
                   ",\"is_active\":" + (isActive ? "true" : "false") + "}";
        }

        private static string StateObject(Guid memberId, Guid? sessionId)
        {
            var session = sessionId.HasValue
                ? "\"" + sessionId.Value.ToString("D") + "\""
                : "null";
            return "{\"member_id\":\"" + memberId.ToString("D") +
                   "\",\"attendance_session_id\":" + session +
                   ",\"attendance_status\":\"clocked_in\"" +
                   ",\"activity_status\":\"working\"" +
                   ",\"connection_status\":\"connected\"" +
                   ",\"checked_in_at\":\"2026-08-26T01:00:00+00:00\"" +
                   ",\"status_started_at\":\"2026-08-26T01:00:00+00:00\"" +
                   ",\"last_heartbeat_at\":\"2026-08-26T01:00:00+00:00\"" +
                   ",\"last_checked_out_at\":null" +
                   ",\"updated_at\":\"2026-08-26T01:00:00+00:00\"}";
        }

        private sealed class StubSessionProvider : ISupabaseSessionProvider
        {
            public Task<SupabaseAuthSession> GetValidSessionAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new SupabaseAuthSession(
                    LocalMemberId,
                    "access-token",
                    "refresh-token",
                    new DateTimeOffset(2026, 8, 26, 3, 0, 0, TimeSpan.Zero)));
            }
        }

        private sealed class RecordingObserver : IObserver<TeamEvent>
        {
            public List<TeamEvent> Events { get; } = new List<TeamEvent>();

            public void OnNext(TeamEvent value)
            {
                Events.Add(value);
            }

            public void OnError(Exception error)
            {
                Assert.Fail("Unexpected observer error: " + error);
            }

            public void OnCompleted()
            {
            }
        }

        private sealed class QueueTransport : ISupabaseHttpTransport
        {
            private readonly Queue<SupabaseHttpResponse> _responses;

            public QueueTransport(params SupabaseHttpResponse[] responses)
            {
                _responses = new Queue<SupabaseHttpResponse>(responses);
            }

            public List<SupabaseHttpRequest> Requests { get; } =
                new List<SupabaseHttpRequest>();

            /// <summary>Nudge reads queued separately; an empty queue answers "no nudges".</summary>
            public Queue<SupabaseHttpResponse> NudgeResponses { get; } =
                new Queue<SupabaseHttpResponse>();

            public Task<SupabaseHttpResponse> SendAsync(
                SupabaseHttpRequest request,
                CancellationToken cancellationToken)
            {
                Requests.Add(request);
                if (request.Url.Contains("/team_events"))
                {
                    return Task.FromResult(
                        NudgeResponses.Count > 0 ? NudgeResponses.Dequeue() : Ok("[]"));
                }

                if (_responses.Count == 0)
                {
                    throw new InvalidOperationException("No fake response was queued.");
                }

                return Task.FromResult(_responses.Dequeue());
            }
        }
    }
}

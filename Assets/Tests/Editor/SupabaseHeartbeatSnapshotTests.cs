using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DOTORION.Core;
using DOTORION.Events.Subjugation;
using DOTORION.Supabase;
using NUnit.Framework;
using UnityEngine;

namespace DOTORION.Tests
{
    public sealed class SupabaseHeartbeatSnapshotTests
    {
        private static readonly Guid MemberId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        private static readonly Guid SessionId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        private static readonly DateTimeOffset CheckIn = new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public async Task ExistingHeartbeatReplyKeepsLocalGameplayFreshWithoutAnotherRosterRead()
        {
            var transport = new Transport { Response = Reply(SessionId, CheckIn, CheckIn) };
            using (var backend = Backend(transport))
            {
                await backend.CheckInAsync(CancellationToken.None);
                var staleSnapshot = Snapshot(CheckIn, CheckIn);
                var now = CheckIn.AddSeconds(200);
                Assert.That(SubjugationEvent.CanAdvance(staleSnapshot, now), Is.False);
                Assert.That(backend.ApplyConfirmedHeartbeat(staleSnapshot), Is.SameAs(staleSnapshot));
                transport.Response = Reply(SessionId, CheckIn, now);
                await backend.SendHeartbeatAsync(CancellationToken.None);
                var confirmed = backend.ApplyConfirmedHeartbeat(staleSnapshot);
                Assert.That(SubjugationEvent.CanAdvance(confirmed, now), Is.True);
                Assert.That(confirmed.LastHeartbeatAtUtc, Is.EqualTo(now));
                Assert.That(confirmed.StatusNote, Is.EqualTo(staleSnapshot.StatusNote));
                Assert.That(confirmed.ActivityStatus, Is.EqualTo(staleSnapshot.ActivityStatus));
                Assert.That(transport.Calls, Is.EqualTo(2), "Check-in and the existing heartbeat are the only requests.");

                transport.Response = new SupabaseHttpResponse(500, "{}");
                try { await backend.SendHeartbeatAsync(CancellationToken.None); Assert.Fail("Expected a failed heartbeat."); }
                catch (SupabaseApiException) { }
                Assert.That(backend.ApplyConfirmedHeartbeat(staleSnapshot).LastHeartbeatAtUtc, Is.EqualTo(now));
                Assert.That(SubjugationEvent.CanAdvance(backend.ApplyConfirmedHeartbeat(staleSnapshot), now.AddSeconds(181)), Is.False);

                transport.Response = new SupabaseHttpResponse(200, "{}");
                await backend.CheckOutAsync(CheckoutReason.Manual, CancellationToken.None);
                Assert.That(backend.ApplyConfirmedHeartbeat(staleSnapshot), Is.SameAs(staleSnapshot));
                var calls = transport.Calls;
                await backend.SendHeartbeatAsync(CancellationToken.None);
                Assert.That(transport.Calls, Is.EqualTo(calls), "Clocked-out heartbeats still make no request.");
            }
        }

        [Test]
        public async Task OldSessionRepliesAndNewerSnapshotsAreNeverOverwritten()
        {
            var transport = new Transport { Response = Reply(SessionId, CheckIn, CheckIn.AddSeconds(200)) };
            using (var backend = Backend(transport))
            {
                await backend.CheckInAsync(CancellationToken.None);
                await backend.SendHeartbeatAsync(CancellationToken.None);
                var newer = Snapshot(CheckIn, CheckIn.AddSeconds(220));
                Assert.That(backend.ApplyConfirmedHeartbeat(newer), Is.SameAs(newer));
                var other = new MemberState("other", "Other", "default", 1, AttendanceStatus.ClockedIn,
                    ActivityStatus.Working, ConnectionStatus.Connected, CheckIn, CheckIn, CheckIn, null, CheckIn);
                Assert.That(backend.ApplyConfirmedHeartbeat(other), Is.SameAs(other));

                var nextSession = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
                var nextCheckIn = CheckIn.AddMinutes(10);
                transport.Response = Reply(nextSession, nextCheckIn, nextCheckIn);
                await backend.CheckInAsync(CancellationToken.None);
                transport.Response = Reply(SessionId, CheckIn, nextCheckIn.AddSeconds(100));
                await backend.SendHeartbeatAsync(CancellationToken.None);
                var nextSnapshot = Snapshot(nextCheckIn, nextCheckIn);
                Assert.That(backend.ApplyConfirmedHeartbeat(nextSnapshot), Is.SameAs(nextSnapshot));
            }
        }

        private static MemberState Snapshot(DateTimeOffset checkedIn, DateTimeOffset heartbeat)
        {
            return new MemberState(MemberId.ToString("D"), "Member", "default", 0, AttendanceStatus.ClockedIn,
                ActivityStatus.Working, ConnectionStatus.Connected, checkedIn, checkedIn, heartbeat, null, checkedIn, "Working");
        }
        private static SupabaseTeamBackend Backend(Transport transport)
        {
            return new SupabaseTeamBackend("https://example.invalid", "test-publishable", transport, new Sessions(), MemberId, Guid.NewGuid());
        }
        private static SupabaseHttpResponse Reply(Guid session, DateTimeOffset checkedIn, DateTimeOffset heartbeat)
        {
            return new SupabaseHttpResponse(200, JsonUtility.ToJson(new StateReply
            {
                member_id = MemberId.ToString("D"), attendance_session_id = session.ToString("D"),
                checked_in_at = checkedIn.ToString("O", CultureInfo.InvariantCulture),
                last_heartbeat_at = heartbeat.ToString("O", CultureInfo.InvariantCulture)
            }));
        }
        private sealed class Transport : ISupabaseHttpTransport
        {
            public int Calls;
            public SupabaseHttpResponse Response;
            public Task<SupabaseHttpResponse> SendAsync(SupabaseHttpRequest request, CancellationToken token)
            {
                Calls++;
                return Task.FromResult(Response);
            }
        }
        private sealed class Sessions : ISupabaseSessionProvider
        {
            public Task<SupabaseAuthSession> GetValidSessionAsync(CancellationToken token)
                => Task.FromResult(new SupabaseAuthSession(MemberId, "test-access", "test-refresh", CheckIn.AddHours(1)));
        }
        [Serializable] private sealed class StateReply
        {
            public string member_id, attendance_session_id, checked_in_at, last_heartbeat_at;
        }
    }
}

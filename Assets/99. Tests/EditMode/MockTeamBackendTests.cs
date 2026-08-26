using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamOverlay.Backend.Mock;
using TeamOverlay.Core;

namespace TeamOverlay.Tests.EditMode
{
    public sealed class MockTeamBackendTests
    {
        private DateTimeOffset _now;
        private MockTeamBackend _backend;

        [SetUp]
        public void SetUp()
        {
            _now = new DateTimeOffset(2026, 8, 25, 3, 0, 0, TimeSpan.Zero);
            _backend = new MockTeamBackend(() => _now);
        }

        [TearDown]
        public void TearDown()
        {
            _backend.Dispose();
        }

        [Test]
        public async Task InitialRoster_HasFourMembersInStableOrder_WithUtcCheckoutTimes()
        {
            var states = await _backend.GetTeamStateAsync(CancellationToken.None);

            Assert.That(states, Has.Count.EqualTo(4));
            Assert.That(
                states.Select(state => state.MemberId).ToArray(),
                Is.EqualTo(new[] { "member-01", "member-02", "member-03", "member-04" }));
            Assert.That(states.Select(state => state.SortOrder).ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(_backend.LocalMemberId, Is.EqualTo("member-01"));
            Assert.That(states.All(state => state.AttendanceStatus == AttendanceStatus.ClockedOut), Is.True);
            Assert.That(states.All(state => state.ActivityStatus == null), Is.True);
            Assert.That(states.All(state => state.LastCheckedOutAtUtc.HasValue), Is.True);
            Assert.That(
                states.All(state => state.LastCheckedOutAtUtc.Value.Offset == TimeSpan.Zero),
                Is.True);
        }

        [Test]
        public async Task CheckIn_StartsWorkingAndPublishesOneCompleteSnapshot()
        {
            var observer = new RecordingObserver<TeamEvent>();
            using (_backend.Events.Subscribe(observer))
            {
                await _backend.CheckInAsync(CancellationToken.None);

                var local = await GetMemberAsync(_backend.LocalMemberId);
                Assert.That(local.AttendanceStatus, Is.EqualTo(AttendanceStatus.ClockedIn));
                Assert.That(local.ActivityStatus, Is.EqualTo(ActivityStatus.Working));
                Assert.That(local.ConnectionStatus, Is.EqualTo(ConnectionStatus.Connected));
                Assert.That(local.CheckedInAtUtc, Is.EqualTo(_now));
                Assert.That(local.ActivityStartedAtUtc, Is.EqualTo(_now));
                Assert.That(local.LastHeartbeatAtUtc, Is.EqualTo(_now));

                Assert.That(observer.Values, Has.Count.EqualTo(1));
                Assert.That(observer.Values[0].Type, Is.EqualTo(TeamEventType.MemberCheckedIn));
                Assert.That(observer.Values[0].ActorMemberId, Is.EqualTo(_backend.LocalMemberId));
                Assert.That(observer.Values[0].OccurredAtUtc, Is.EqualTo(_now));
                Assert.That(observer.Values[0].State, Is.SameAs(local));
                Assert.That(observer.Values[0].ActivityStatus, Is.EqualTo(ActivityStatus.Working));

                _now = _now.AddMinutes(5);
                await _backend.CheckInAsync(CancellationToken.None);
                Assert.That(observer.Values, Has.Count.EqualTo(1), "duplicate check-in must be idempotent");
                Assert.That((await GetMemberAsync(_backend.LocalMemberId)).CheckedInAtUtc, Is.EqualTo(_now.AddMinutes(-5)));
            }
        }

        [Test]
        public async Task ChangeActivity_RequiresCheckIn_ThenUpdatesImmediately()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _backend.ChangeActivityAsync(ActivityStatus.Break, CancellationToken.None));

            var observer = new RecordingObserver<TeamEvent>();
            using (_backend.Events.Subscribe(observer))
            {
                await _backend.CheckInAsync(CancellationToken.None);
                _now = _now.AddMinutes(20);
                await _backend.ChangeActivityAsync(ActivityStatus.Meal, CancellationToken.None);

                var local = await GetMemberAsync(_backend.LocalMemberId);
                Assert.That(local.ActivityStatus, Is.EqualTo(ActivityStatus.Meal));
                Assert.That(local.ActivityStartedAtUtc, Is.EqualTo(_now));
                Assert.That(local.CheckedInAtUtc, Is.EqualTo(_now.AddMinutes(-20)));
                Assert.That(observer.Values.Last().Type, Is.EqualTo(TeamEventType.MemberActivityChanged));
                Assert.That(observer.Values.Last().ActivityStatus, Is.EqualTo(ActivityStatus.Meal));

                await _backend.ChangeActivityAsync(ActivityStatus.Meal, CancellationToken.None);
                Assert.That(observer.Values, Has.Count.EqualTo(2), "same-activity changes must be idempotent");
            }
        }

        [Test]
        public async Task CheckOut_ClearsOpenSessionAndRecordsLastCheckout()
        {
            var observer = new RecordingObserver<TeamEvent>();
            using (_backend.Events.Subscribe(observer))
            {
                await _backend.CheckInAsync(CancellationToken.None);
                _now = _now.AddHours(2);
                await _backend.CheckOutAsync(CheckoutReason.Manual, CancellationToken.None);

                var local = await GetMemberAsync(_backend.LocalMemberId);
                Assert.That(local.AttendanceStatus, Is.EqualTo(AttendanceStatus.ClockedOut));
                Assert.That(local.ActivityStatus, Is.Null);
                Assert.That(local.ConnectionStatus, Is.EqualTo(ConnectionStatus.Disconnected));
                Assert.That(local.CheckedInAtUtc, Is.Null);
                Assert.That(local.ActivityStartedAtUtc, Is.Null);
                Assert.That(local.LastCheckedOutAtUtc, Is.EqualTo(_now));
                Assert.That(local.UpdatedAtUtc.Offset, Is.EqualTo(TimeSpan.Zero));

                var checkoutEvent = observer.Values.Last();
                Assert.That(checkoutEvent.Type, Is.EqualTo(TeamEventType.MemberCheckedOut));
                Assert.That(checkoutEvent.CheckoutReason, Is.EqualTo(CheckoutReason.Manual));
                Assert.That(checkoutEvent.State, Is.SameAs(local));

                await _backend.CheckOutAsync(CheckoutReason.AppExit, CancellationToken.None);
                Assert.That(observer.Values, Has.Count.EqualTo(2), "duplicate checkout must be idempotent");
            }
        }

        [Test]
        public async Task Heartbeat_UpdatesUtcLivenessWithoutPublishingUserEvent()
        {
            var observer = new RecordingObserver<TeamEvent>();
            using (_backend.Events.Subscribe(observer))
            {
                await _backend.CheckInAsync(CancellationToken.None);
                _now = _now.AddSeconds(45);
                await _backend.SendHeartbeatAsync(CancellationToken.None);

                var local = await GetMemberAsync(_backend.LocalMemberId);
                Assert.That(local.LastHeartbeatAtUtc, Is.EqualTo(_now));
                Assert.That(local.UpdatedAtUtc, Is.EqualTo(_now));
                Assert.That(observer.Values, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task MockControl_ChecksInFirstOfflineTeammateAndPublishesOneEvent()
        {
            IMockTeamBackendControls controls = _backend;
            var observer = new RecordingObserver<TeamEvent>();
            using (_backend.Events.Subscribe(observer))
            {
                await controls.TriggerFakeTeammateCheckInAsync(CancellationToken.None);

                var teammate = await GetMemberAsync("member-02");
                Assert.That(teammate.IsClockedIn, Is.True);
                Assert.That(teammate.ActivityStatus, Is.EqualTo(ActivityStatus.Working));
                Assert.That(teammate.CheckedInAtUtc, Is.EqualTo(_now));
                Assert.That(observer.Values, Has.Count.EqualTo(1));
                Assert.That(observer.Values[0].Type, Is.EqualTo(TeamEventType.MemberCheckedIn));
                Assert.That(observer.Values[0].ActorMemberId, Is.EqualTo("member-02"));
                Assert.That(observer.Values[0].ActorMemberId, Is.Not.EqualTo(_backend.LocalMemberId));

                await controls.TriggerFakeTeammateCheckInAsync("member-02", CancellationToken.None);
                Assert.That(observer.Values, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task SnapshotsUseUtcAndAttendanceElapsedIsCalculatedLocally()
        {
            _now = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.FromHours(9));
            await _backend.CheckInAsync(CancellationToken.None);

            var local = await GetMemberAsync(_backend.LocalMemberId);
            Assert.That(local.CheckedInAtUtc.Value.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(local.CheckedInAtUtc.Value.Hour, Is.EqualTo(3));
            Assert.That(
                local.GetAttendanceElapsed(local.CheckedInAtUtc.Value.AddMinutes(37)),
                Is.EqualTo(TimeSpan.FromMinutes(37)));
            Assert.That(
                local.GetAttendanceElapsed(local.CheckedInAtUtc.Value.AddMinutes(-1)),
                Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public async Task CancelledMutation_DoesNotChangeStateOrPublishEvent()
        {
            var observer = new RecordingObserver<TeamEvent>();
            using (_backend.Events.Subscribe(observer))
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.That(
                    async () => await _backend.CheckInAsync(cancellation.Token),
                    Throws.InstanceOf<OperationCanceledException>());

                Assert.That((await GetMemberAsync(_backend.LocalMemberId)).IsClockedIn, Is.False);
                Assert.That(observer.Values, Is.Empty);
            }
        }

        private async Task<MemberState> GetMemberAsync(string memberId)
        {
            var states = await _backend.GetTeamStateAsync(CancellationToken.None);
            return states.Single(state => state.MemberId == memberId);
        }

        private sealed class RecordingObserver<T> : IObserver<T>
        {
            public List<T> Values { get; } = new List<T>();

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
                Assert.Fail(error.ToString());
            }

            public void OnNext(T value)
            {
                Values.Add(value);
            }
        }
    }
}

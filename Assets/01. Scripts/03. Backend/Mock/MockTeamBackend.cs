using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using TeamOverlay.Core;

namespace TeamOverlay.Backend.Mock
{
    /// <summary>
    /// In-memory first-milestone backend. Its four-member roster and ordering are
    /// stable across runs, while all mutations follow the same async contract that
    /// the later Supabase implementation will use.
    /// </summary>
    public sealed class MockTeamBackend : ITeamBackend, IMockTeamBackendControls, IDisposable
    {
        public const string DefaultLocalMemberId = "member-01";

        private readonly object _gate = new object();
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly List<MemberState> _members;
        private readonly ObservableStream<TeamEvent> _events = new ObservableStream<TeamEvent>();
        private bool _isDisposed;

        public MockTeamBackend()
            : this(() => DateTimeOffset.UtcNow)
        {
        }

        public MockTeamBackend(Func<DateTimeOffset> utcNow)
        {
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            var now = GetUtcNow();
            _members = new List<MemberState>
            {
                CreateClockedOutMember(DefaultLocalMemberId, "나", "avatar-local", 0, now.AddHours(-18)),
                CreateClockedOutMember("member-02", "뱁버드", "avatar-mint", 1, now.AddHours(-12)),
                CreateClockedOutMember("member-03", "잔다", "avatar-gold", 2, now.AddHours(-4)),
                CreateClockedOutMember("member-04", "메이비", "avatar-coral", 3, now.AddHours(-2))
            };
        }

        public string LocalMemberId => DefaultLocalMemberId;

        public IObservable<TeamEvent> Events => _events;

        public Task<IReadOnlyList<MemberState>> GetTeamStateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ThrowIfDisposed();
                IReadOnlyList<MemberState> snapshot = new ReadOnlyCollection<MemberState>(
                    new List<MemberState>(_members));
                return Task.FromResult(snapshot);
            }
        }

        public Task CheckInAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TeamEvent teamEvent = null;

            lock (_gate)
            {
                ThrowIfDisposed();
                var index = FindMemberIndex(LocalMemberId);
                var current = _members[index];
                if (current.IsClockedIn)
                {
                    return Task.CompletedTask;
                }

                var now = GetUtcNow();
                var next = CreateClockedInState(current, ActivityStatus.Working, now);
                _members[index] = next;
                teamEvent = CreateEvent(TeamEventType.MemberCheckedIn, next, now, ActivityStatus.Working);
            }

            _events.Publish(teamEvent);
            return Task.CompletedTask;
        }

        public Task ChangeActivityAsync(ActivityStatus status, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(typeof(ActivityStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            TeamEvent teamEvent = null;
            lock (_gate)
            {
                ThrowIfDisposed();
                var index = FindMemberIndex(LocalMemberId);
                var current = _members[index];
                if (!current.IsClockedIn)
                {
                    throw new InvalidOperationException("The local member must check in before changing activity.");
                }

                if (current.ActivityStatus == status)
                {
                    return Task.CompletedTask;
                }

                var now = GetUtcNow();
                var next = new MemberState(
                    current.MemberId,
                    current.DisplayName,
                    current.AvatarKey,
                    current.SortOrder,
                    AttendanceStatus.ClockedIn,
                    status,
                    current.ConnectionStatus,
                    current.CheckedInAtUtc,
                    now,
                    current.LastHeartbeatAtUtc,
                    current.LastCheckedOutAtUtc,
                    now);
                _members[index] = next;
                teamEvent = CreateEvent(TeamEventType.MemberActivityChanged, next, now, status);
            }

            _events.Publish(teamEvent);
            return Task.CompletedTask;
        }

        public Task CheckOutAsync(CheckoutReason reason, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(typeof(CheckoutReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }

            TeamEvent teamEvent = null;
            lock (_gate)
            {
                ThrowIfDisposed();
                var index = FindMemberIndex(LocalMemberId);
                var current = _members[index];
                if (!current.IsClockedIn)
                {
                    return Task.CompletedTask;
                }

                var now = GetUtcNow();
                var next = new MemberState(
                    current.MemberId,
                    current.DisplayName,
                    current.AvatarKey,
                    current.SortOrder,
                    AttendanceStatus.ClockedOut,
                    null,
                    ConnectionStatus.Disconnected,
                    null,
                    null,
                    current.LastHeartbeatAtUtc,
                    now,
                    now);
                _members[index] = next;
                teamEvent = CreateEvent(
                    TeamEventType.MemberCheckedOut,
                    next,
                    now,
                    null,
                    reason);
            }

            _events.Publish(teamEvent);
            return Task.CompletedTask;
        }

        public Task SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ThrowIfDisposed();
                var index = FindMemberIndex(LocalMemberId);
                var current = _members[index];
                if (!current.IsClockedIn)
                {
                    return Task.CompletedTask;
                }

                var now = GetUtcNow();
                _members[index] = new MemberState(
                    current.MemberId,
                    current.DisplayName,
                    current.AvatarKey,
                    current.SortOrder,
                    current.AttendanceStatus,
                    current.ActivityStatus,
                    ConnectionStatus.Connected,
                    current.CheckedInAtUtc,
                    current.ActivityStartedAtUtc,
                    now,
                    current.LastCheckedOutAtUtc,
                    now);
            }

            // Heartbeats maintain liveness but intentionally do not flood the
            // user-facing team event stream.
            return Task.CompletedTask;
        }

        public Task TriggerFakeTeammateCheckInAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string memberId = null;
            lock (_gate)
            {
                ThrowIfDisposed();
                foreach (var member in _members)
                {
                    if (member.MemberId != LocalMemberId && !member.IsClockedIn)
                    {
                        memberId = member.MemberId;
                        break;
                    }
                }
            }

            // Once every teammate is online, the development button becomes an
            // idempotent no-op instead of creating a duplicate check-in event.
            return memberId == null
                ? Task.CompletedTask
                : TriggerFakeTeammateCheckInAsync(memberId, cancellationToken);
        }

        public Task TriggerFakeTeammateCheckInAsync(string memberId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(memberId))
            {
                throw new ArgumentException("A teammate id is required.", nameof(memberId));
            }

            TeamEvent teamEvent = null;
            lock (_gate)
            {
                ThrowIfDisposed();
                if (memberId == LocalMemberId)
                {
                    throw new ArgumentException("Mock teammate controls cannot mutate the local member.", nameof(memberId));
                }

                var index = FindMemberIndex(memberId);
                var current = _members[index];
                if (current.IsClockedIn)
                {
                    return Task.CompletedTask;
                }

                var now = GetUtcNow();
                var next = CreateClockedInState(current, ActivityStatus.Working, now);
                _members[index] = next;
                teamEvent = CreateEvent(TeamEventType.MemberCheckedIn, next, now, ActivityStatus.Working);
            }

            _events.Publish(teamEvent);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
            }

            _events.Dispose();
        }

        private static MemberState CreateClockedOutMember(
            string memberId,
            string displayName,
            string avatarKey,
            int sortOrder,
            DateTimeOffset lastCheckedOutAtUtc)
        {
            var timestamp = lastCheckedOutAtUtc.ToUniversalTime();
            return new MemberState(
                memberId,
                displayName,
                avatarKey,
                sortOrder,
                AttendanceStatus.ClockedOut,
                null,
                ConnectionStatus.Disconnected,
                null,
                null,
                null,
                timestamp,
                timestamp);
        }

        private static MemberState CreateClockedInState(
            MemberState current,
            ActivityStatus activityStatus,
            DateTimeOffset now)
        {
            return new MemberState(
                current.MemberId,
                current.DisplayName,
                current.AvatarKey,
                current.SortOrder,
                AttendanceStatus.ClockedIn,
                activityStatus,
                ConnectionStatus.Connected,
                now,
                now,
                now,
                current.LastCheckedOutAtUtc,
                now);
        }

        private static TeamEvent CreateEvent(
            TeamEventType type,
            MemberState state,
            DateTimeOffset occurredAtUtc,
            ActivityStatus? activityStatus = null,
            CheckoutReason? checkoutReason = null)
        {
            return new TeamEvent(
                Guid.NewGuid().ToString("N"),
                type,
                state.MemberId,
                occurredAtUtc,
                state,
                activityStatus,
                checkoutReason);
        }

        private int FindMemberIndex(string memberId)
        {
            for (var index = 0; index < _members.Count; index++)
            {
                if (string.Equals(_members[index].MemberId, memberId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            throw new KeyNotFoundException($"Unknown member id: {memberId}");
        }

        private DateTimeOffset GetUtcNow()
        {
            return _utcNow().ToUniversalTime();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MockTeamBackend));
            }
        }
    }
}

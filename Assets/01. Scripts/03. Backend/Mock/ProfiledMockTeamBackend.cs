using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using TeamOverlay.Core;

namespace TeamOverlay.Backend.Mock
{
    /// <summary>
    /// Keeps the first-run profile visible while the application is still using
    /// the serverless mock backend. The production backend will use the Supabase
    /// Auth UUID as LocalMemberId; the mock keeps its deterministic roster ids.
    /// </summary>
    public sealed class ProfiledMockTeamBackend : ITeamBackend, IMockTeamBackendControls, IDisposable
    {
        private readonly MockTeamBackend _inner;
        private readonly string _displayName;
        private readonly IObservable<TeamEvent> _events;

        public ProfiledMockTeamBackend(string displayName)
            : this(displayName, new MockTeamBackend())
        {
        }

        internal ProfiledMockTeamBackend(string displayName, MockTeamBackend inner)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A profile display name is required.", nameof(displayName));
            }

            _displayName = displayName.Trim();
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _events = new ProfiledEventObservable(_inner.Events, MapEvent);
        }

        public string LocalMemberId => _inner.LocalMemberId;

        public IObservable<TeamEvent> Events => _events;

        public async Task<IReadOnlyList<MemberState>> GetTeamStateAsync(
            CancellationToken cancellationToken)
        {
            var source = await _inner.GetTeamStateAsync(cancellationToken);
            var mapped = new List<MemberState>(source.Count);
            foreach (var state in source)
            {
                mapped.Add(MapState(state));
            }

            return new ReadOnlyCollection<MemberState>(mapped);
        }

        public Task CheckInAsync(CancellationToken cancellationToken)
        {
            return _inner.CheckInAsync(cancellationToken);
        }

        public Task ChangeActivityAsync(
            ActivityStatus status,
            CancellationToken cancellationToken)
        {
            return _inner.ChangeActivityAsync(status, cancellationToken);
        }

        public Task CheckOutAsync(
            CheckoutReason reason,
            CancellationToken cancellationToken)
        {
            return _inner.CheckOutAsync(reason, cancellationToken);
        }

        public Task SetStatusNoteAsync(string note, CancellationToken cancellationToken)
        {
            return _inner.SetStatusNoteAsync(note, cancellationToken);
        }

        public Task SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            return _inner.SendHeartbeatAsync(cancellationToken);
        }

        public Task TriggerFakeTeammateCheckInAsync(CancellationToken cancellationToken)
        {
            return _inner.TriggerFakeTeammateCheckInAsync(cancellationToken);
        }

        public Task TriggerFakeTeammateCheckInAsync(
            string memberId,
            CancellationToken cancellationToken)
        {
            return _inner.TriggerFakeTeammateCheckInAsync(memberId, cancellationToken);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        private MemberState MapState(MemberState state)
        {
            if (!string.Equals(state.MemberId, LocalMemberId, StringComparison.Ordinal))
            {
                return state;
            }

            return new MemberState(
                state.MemberId,
                _displayName,
                state.AvatarKey,
                state.SortOrder,
                state.AttendanceStatus,
                state.ActivityStatus,
                state.ConnectionStatus,
                state.CheckedInAtUtc,
                state.ActivityStartedAtUtc,
                state.LastHeartbeatAtUtc,
                state.LastCheckedOutAtUtc,
                state.UpdatedAtUtc,
                state.StatusNote);
        }

        private TeamEvent MapEvent(TeamEvent source)
        {
            var mappedState = MapState(source.State);
            if (ReferenceEquals(mappedState, source.State))
            {
                return source;
            }

            return new TeamEvent(
                source.EventId,
                source.Type,
                source.ActorMemberId,
                source.OccurredAtUtc,
                mappedState,
                source.ActivityStatus,
                source.CheckoutReason,
                source.TargetMemberId);
        }

        private sealed class ProfiledEventObservable : IObservable<TeamEvent>
        {
            private readonly IObservable<TeamEvent> _source;
            private readonly Func<TeamEvent, TeamEvent> _map;

            public ProfiledEventObservable(
                IObservable<TeamEvent> source,
                Func<TeamEvent, TeamEvent> map)
            {
                _source = source;
                _map = map;
            }

            public IDisposable Subscribe(IObserver<TeamEvent> observer)
            {
                if (observer == null)
                {
                    throw new ArgumentNullException(nameof(observer));
                }

                return _source.Subscribe(new MappingObserver(observer, _map));
            }
        }

        private sealed class MappingObserver : IObserver<TeamEvent>
        {
            private readonly IObserver<TeamEvent> _destination;
            private readonly Func<TeamEvent, TeamEvent> _map;

            public MappingObserver(
                IObserver<TeamEvent> destination,
                Func<TeamEvent, TeamEvent> map)
            {
                _destination = destination;
                _map = map;
            }

            public void OnNext(TeamEvent value)
            {
                _destination.OnNext(_map(value));
            }

            public void OnError(Exception error)
            {
                _destination.OnError(error);
            }

            public void OnCompleted()
            {
                _destination.OnCompleted();
            }
        }
    }
}

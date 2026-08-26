using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeamOverlay.Core
{
    /// <summary>
    /// Backend boundary consumed by UI and app services. Implementations own the
    /// authoritative clock and always return timestamps in UTC.
    /// </summary>
    public interface ITeamBackend
    {
        string LocalMemberId { get; }

        IObservable<TeamEvent> Events { get; }

        Task<IReadOnlyList<MemberState>> GetTeamStateAsync(CancellationToken cancellationToken);

        Task CheckInAsync(CancellationToken cancellationToken);

        Task ChangeActivityAsync(ActivityStatus status, CancellationToken cancellationToken);

        Task CheckOutAsync(CheckoutReason reason, CancellationToken cancellationToken);

        Task SendHeartbeatAsync(CancellationToken cancellationToken);
    }
}

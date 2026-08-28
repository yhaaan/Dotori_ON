using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DOTORION.Core
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

        /// <summary>Sets the local member's card note; null or blank clears it.</summary>
        Task SetStatusNoteAsync(string note, CancellationToken cancellationToken);

        /// <summary>
        /// Sets the local member's profile icon to a catalog key. Null or blank
        /// restores the default, which draws the name initial instead.
        /// </summary>
        Task SetAvatarKeyAsync(string avatarKey, CancellationToken cancellationToken);

        Task SendHeartbeatAsync(CancellationToken cancellationToken);
    }
}

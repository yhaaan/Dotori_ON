using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeamOverlay.Core
{
    /// <summary>
    /// One member as the developer dashboard sees them: the roster row plus the
    /// totals underneath it. Assembled by the server in one query, because four
    /// members times four round trips is a screen that opens slowly for no reason.
    /// </summary>
    public sealed class AdminMemberSummary
    {
        public AdminMemberSummary(
            string memberId,
            string displayName,
            bool isActive,
            bool isAdmin,
            int sessionCount,
            int attendanceSeconds,
            int totalPoints,
            DateTimeOffset? lastCheckedOutAtUtc)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                throw new ArgumentException("A member id is required.", nameof(memberId));
            }

            MemberId = memberId;
            DisplayName = displayName ?? string.Empty;
            IsActive = isActive;
            IsAdmin = isAdmin;
            SessionCount = Math.Max(0, sessionCount);
            AttendanceSeconds = Math.Max(0, attendanceSeconds);
            TotalPoints = Math.Max(0, totalPoints);
            LastCheckedOutAtUtc = lastCheckedOutAtUtc;
        }

        public string MemberId { get; }

        public string DisplayName { get; }

        public bool IsActive { get; }

        public bool IsAdmin { get; }

        public int SessionCount { get; }

        /// <summary>Every session ever, with an open one counted up to now.</summary>
        public int AttendanceSeconds { get; }

        public int TotalPoints { get; }

        /// <summary>Null for a member who has never finished a session.</summary>
        public DateTimeOffset? LastCheckedOutAtUtc { get; }
    }

    /// <summary>
    /// The developer dashboard's view of the team. Separate from the rest because
    /// it is the only place that reads the roster as rows of data rather than as
    /// people, and the only place that can erase one.
    /// </summary>
    public interface ITeamAdmin
    {
        Task<IReadOnlyList<AdminMemberSummary>> GetMemberOverviewAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// Erases a member and every record under them, freeing their team slot.
        /// There is no undo, and the server refuses to erase the caller.
        /// </summary>
        Task DeleteMemberAsync(string memberId, CancellationToken cancellationToken);
    }
}

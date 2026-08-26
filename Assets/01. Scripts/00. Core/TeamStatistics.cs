using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeamOverlay.Core
{
    /// <summary>
    /// One local day of totals. Attendance is the whole open-to-close span, while
    /// work counts only the working intervals inside it, so the two are different
    /// numbers on purpose: "how long the app was on" is not "how long I worked".
    /// </summary>
    public sealed class MemberDailyStat
    {
        public MemberDailyStat(
            DateTime localDate,
            int attendanceSeconds,
            int workSeconds,
            int breakSeconds,
            int mealSeconds)
        {
            LocalDate = localDate.Date;
            AttendanceSeconds = Math.Max(0, attendanceSeconds);
            WorkSeconds = Math.Max(0, workSeconds);
            BreakSeconds = Math.Max(0, breakSeconds);
            MealSeconds = Math.Max(0, mealSeconds);
        }

        public DateTime LocalDate { get; }

        public int AttendanceSeconds { get; }

        public int WorkSeconds { get; }

        public int BreakSeconds { get; }

        public int MealSeconds { get; }

        public bool HasActivity => AttendanceSeconds > 0;
    }

    /// <summary>A member's place in the work-time ranking for a date range.</summary>
    public sealed class TeamRankingEntry
    {
        public TeamRankingEntry(
            string memberId,
            string displayName,
            int sortOrder,
            int workSeconds,
            int attendanceSeconds)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                throw new ArgumentException("A member id is required.", nameof(memberId));
            }

            MemberId = memberId;
            DisplayName = displayName ?? string.Empty;
            SortOrder = sortOrder;
            WorkSeconds = Math.Max(0, workSeconds);
            AttendanceSeconds = Math.Max(0, attendanceSeconds);
        }

        public string MemberId { get; }

        public string DisplayName { get; }

        public int SortOrder { get; }

        public int WorkSeconds { get; }

        public int AttendanceSeconds { get; }
    }

    /// <summary>
    /// Statistics are a separate capability from attendance: the mock backend has
    /// no history worth reporting, so callers ask for this interface rather than
    /// forcing every backend to fabricate numbers.
    /// </summary>
    public interface ITeamStatistics
    {
        /// <summary>
        /// Per-day totals for one member, inclusive of both dates. Dates are team
        /// local dates, not UTC.
        /// </summary>
        Task<IReadOnlyList<MemberDailyStat>> GetDailyStatsAsync(
            string memberId,
            DateTime fromLocalDate,
            DateTime toLocalDate,
            CancellationToken cancellationToken);

        /// <summary>Work-time ranking for the team, highest first.</summary>
        Task<IReadOnlyList<TeamRankingEntry>> GetWorkRankingAsync(
            DateTime fromLocalDate,
            DateTime toLocalDate,
            CancellationToken cancellationToken);
    }
}

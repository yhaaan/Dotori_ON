using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DOTORION.Core
{
    /// <summary>How wide a slice of history the statistics panel is showing.</summary>
    public enum StatisticsPeriod
    {
        LastSevenDays = 0,
        ThisMonth = 1,
        AllTime = 2
    }

    /// <summary>
    /// How the server folds days together before sending them. A month of daily
    /// rows does not fit the overlay, so a wider period asks for wider buckets
    /// instead of more rows.
    /// </summary>
    public enum StatisticsBucket
    {
        Day = 0,
        Week = 1,
        Month = 2
    }

    /// <summary>Which number the ranking is sorted and drawn by.</summary>
    public enum RankingMetric
    {
        Work = 0,
        Attendance = 1,
        Break = 2,
        Meal = 3
    }

    /// <summary>
    /// A period turned into the dates and bucket size a request needs. A null
    /// <see cref="FromLocalDate"/> means "everything on record": the client has no
    /// way to know when the team started, so the server resolves that end.
    /// </summary>
    public sealed class StatisticsRange
    {
        private StatisticsRange(
            StatisticsPeriod period,
            DateTime? fromLocalDate,
            DateTime toLocalDate,
            StatisticsBucket bucket)
        {
            Period = period;
            FromLocalDate = fromLocalDate?.Date;
            ToLocalDate = toLocalDate.Date;
            Bucket = bucket;
        }

        public StatisticsPeriod Period { get; }

        public DateTime? FromLocalDate { get; }

        public DateTime ToLocalDate { get; }

        public StatisticsBucket Bucket { get; }

        public static StatisticsRange Resolve(StatisticsPeriod period, DateTime todayLocal)
        {
            var today = todayLocal.Date;
            switch (period)
            {
                case StatisticsPeriod.ThisMonth:
                    // Days, not weeks: the month is drawn as a calendar, where a
                    // week-sized bucket has nowhere to go. Thirty-one rows would
                    // not fit a list, but thirty-one squares fit a grid.
                    return new StatisticsRange(
                        period,
                        new DateTime(today.Year, today.Month, 1),
                        today,
                        StatisticsBucket.Day);
                case StatisticsPeriod.AllTime:
                    return new StatisticsRange(period, null, today, StatisticsBucket.Month);
                default:
                    return new StatisticsRange(
                        StatisticsPeriod.LastSevenDays,
                        today.AddDays(-6),
                        today,
                        StatisticsBucket.Day);
            }
        }
    }

    /// <summary>
    /// One bucket of totals. Attendance is the whole open-to-close span, while
    /// work counts only the working intervals inside it, so the two are different
    /// numbers on purpose: "how long the app was on" is not "how long I worked".
    /// </summary>
    public sealed class MemberPeriodStat
    {
        public MemberPeriodStat(
            DateTime bucketStart,
            DateTime bucketEnd,
            int attendanceSeconds,
            int workSeconds,
            int breakSeconds,
            int mealSeconds,
            int dailyCheckInDays = 0)
        {
            BucketStart = bucketStart.Date;
            BucketEnd = bucketEnd.Date < BucketStart ? BucketStart : bucketEnd.Date;
            AttendanceSeconds = Math.Max(0, attendanceSeconds);
            WorkSeconds = Math.Max(0, workSeconds);
            BreakSeconds = Math.Max(0, breakSeconds);
            MealSeconds = Math.Max(0, mealSeconds);
            DailyCheckInDays = Math.Max(0, dailyCheckInDays);
        }

        public DateTime BucketStart { get; }

        /// <summary>Inclusive. Equal to <see cref="BucketStart"/> for a daily bucket.</summary>
        public DateTime BucketEnd { get; }

        public int AttendanceSeconds { get; }

        public int WorkSeconds { get; }

        public int BreakSeconds { get; }

        public int MealSeconds { get; }

        public int DailyCheckInDays { get; }

        public bool HasDailyCheckIn => DailyCheckInDays > 0;

        public bool HasActivity => AttendanceSeconds > 0;

        public int SecondsFor(RankingMetric metric)
        {
            switch (metric)
            {
                case RankingMetric.Attendance: return AttendanceSeconds;
                case RankingMetric.Break: return BreakSeconds;
                case RankingMetric.Meal: return MealSeconds;
                default: return WorkSeconds;
            }
        }
    }

    /// <summary>
    /// A member's totals for a date range. Every metric travels with the entry so
    /// switching the ranked metric re-sorts four members locally instead of
    /// costing another round trip.
    /// </summary>
    public sealed class TeamRankingEntry
    {
        public TeamRankingEntry(
            string memberId,
            string displayName,
            int sortOrder,
            int workSeconds,
            int attendanceSeconds,
            int breakSeconds,
            int mealSeconds,
            int totalPoints,
            int streakDays)
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
            BreakSeconds = Math.Max(0, breakSeconds);
            MealSeconds = Math.Max(0, mealSeconds);
            TotalPoints = Math.Max(0, totalPoints);
            StreakDays = Math.Max(0, streakDays);
        }

        public string MemberId { get; }

        public string DisplayName { get; }

        public int SortOrder { get; }

        public int WorkSeconds { get; }

        public int AttendanceSeconds { get; }

        public int BreakSeconds { get; }

        public int MealSeconds { get; }

        /// <summary>
        /// Check-in points and the run of days behind them. Unlike everything
        /// else here they do not belong to the ranked date range - they are the
        /// whole record - and they travel with the entry because the ranking is
        /// the screen where teammates are read side by side.
        /// </summary>
        public int TotalPoints { get; }

        public int StreakDays { get; }

        public int SecondsFor(RankingMetric metric)
        {
            switch (metric)
            {
                case RankingMetric.Attendance: return AttendanceSeconds;
                case RankingMetric.Break: return BreakSeconds;
                case RankingMetric.Meal: return MealSeconds;
                default: return WorkSeconds;
            }
        }
    }

    /// <summary>
    /// Statistics are a separate capability from attendance: the mock backend has
    /// no history worth reporting, so callers ask for this interface rather than
    /// forcing every backend to fabricate numbers.
    /// </summary>
    public interface ITeamStatistics
    {
        /// <summary>
        /// Totals for one member, bucketed as the range asks. Dates are team local
        /// dates, not UTC.
        /// </summary>
        Task<IReadOnlyList<MemberPeriodStat>> GetPeriodStatsAsync(
            string memberId,
            StatisticsRange range,
            CancellationToken cancellationToken);

        /// <summary>Every member's totals for the range, work time first.</summary>
        Task<IReadOnlyList<TeamRankingEntry>> GetRankingAsync(
            StatisticsRange range,
            CancellationToken cancellationToken);
    }
}

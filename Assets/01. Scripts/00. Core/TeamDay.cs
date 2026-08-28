using System;

namespace TeamOverlay.Core
{
    /// <summary>
    /// A day here starts at 06:00, not midnight, matching the server's
    /// <c>team_local_date</c>. The team works past midnight often enough that a
    /// calendar boundary in the middle of the evening split one sitting across
    /// two days, and a check-in at 01:00 counted for a day that had not started.
    ///
    /// The client needs the same rule and not just the server: between midnight
    /// and six it would otherwise ask about tomorrow, and the checkout summary
    /// would look for a row the server files under yesterday.
    /// </summary>
    public static class TeamDay
    {
        /// <summary>Keep in step with the server's <c>team_day_offset</c>.</summary>
        public const int StartHour = 6;

        public static DateTime Today => DateFor(DateTime.Now);

        /// <summary>
        /// The business date a local moment falls in. 03:00 on the 29th belongs
        /// to the 28th; 06:00 is where the 29th begins.
        /// </summary>
        public static DateTime DateFor(DateTime localMoment)
        {
            return localMoment.AddHours(-StartHour).Date;
        }
    }
}

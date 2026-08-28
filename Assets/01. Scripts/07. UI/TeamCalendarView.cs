using System;
using System.Collections.Generic;
using TeamOverlay.Core;
using UnityEngine;

namespace TeamOverlay.UI
{
    /// <summary>
    /// The month laid out as a calendar. A month of daily rows never fit the
    /// list the other periods use, and a shape everyone already reads says more
    /// about a month than seven of its thirty-one days could.
    /// </summary>
    public sealed class TeamCalendarView : MonoBehaviour
    {
        /// <summary>
        /// Six rows of seven. Five rows fit most months, but a 31 day month that
        /// starts on a Saturday needs the sixth, so the grid always has it.
        /// </summary>
        public const int WeekCount = 6;

        public const int DaysPerWeek = 7;

        public const int CellCount = WeekCount * DaysPerWeek;

        [Header("Prefab references")]
        [SerializeField] private TeamCalendarDayView[] _cells = new TeamCalendarDayView[CellCount];

        /// <summary>
        /// Fills the grid from daily buckets. Anything that is not a day bucket is
        /// refused rather than drawn wrong: a week-sized total has no single
        /// square it belongs in.
        /// </summary>
        public void Bind(StatisticsRange range, IReadOnlyList<MemberPeriodStat> stats)
        {
            if (range == null || range.Bucket != StatisticsBucket.Day)
            {
                ClearAll();
                return;
            }

            var month = range.FromLocalDate ?? range.ToLocalDate;
            var firstOfMonth = new DateTime(month.Year, month.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            var offset = MondayFirstIndex(firstOfMonth.DayOfWeek);
            var today = DateTime.Today;

            var byDay = new Dictionary<int, MemberPeriodStat>();
            var maximumSeconds = 0;
            if (stats != null)
            {
                foreach (var stat in stats)
                {
                    if (stat.BucketStart.Year != month.Year || stat.BucketStart.Month != month.Month)
                    {
                        continue;
                    }

                    byDay[stat.BucketStart.Day] = stat;
                    if (stat.WorkSeconds > maximumSeconds)
                    {
                        maximumSeconds = stat.WorkSeconds;
                    }
                }
            }

            for (var index = 0; index < _cells.Length; index++)
            {
                var cell = _cells[index];
                if (cell == null) continue;
                var dayOfMonth = index - offset + 1;
                if (dayOfMonth < 1 || dayOfMonth > daysInMonth)
                {
                    cell.Clear();
                    continue;
                }

                byDay.TryGetValue(dayOfMonth, out var stat);
                var date = firstOfMonth.AddDays(dayOfMonth - 1);
                cell.Bind(dayOfMonth, stat, maximumSeconds, date == today);
            }
        }

        public void ClearAll()
        {
            foreach (var cell in _cells)
            {
                cell?.Clear();
            }
        }

        /// <summary>
        /// Column zero is Monday, which is how a Korean calendar is read and how
        /// the header row in the prefab is labelled. DayOfWeek starts on Sunday.
        /// </summary>
        private static int MondayFirstIndex(DayOfWeek dayOfWeek)
        {
            return ((int)dayOfWeek + 6) % DaysPerWeek;
        }
    }
}

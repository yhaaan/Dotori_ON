using System;
using TeamOverlay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.UI
{
    public sealed class TeamPeriodStatRowView : MonoBehaviour
    {
        private const string DayLabels = "일월화수목금토";

        [SerializeField] private Text _dateLabel;
        [SerializeField] private Text _workLabel;
        [SerializeField] private Text _attendanceLabel;
        [SerializeField] private Text _otherLabel;
        [SerializeField] private Image _workBar;
        [SerializeField] private Image _attendanceBar;

        public void Bind(
            MemberPeriodStat stat,
            StatisticsBucket bucket,
            int maximumWorkSeconds,
            int maximumAttendanceSeconds)
        {
            _dateLabel.text = FormatBucket(stat, bucket);
            _workLabel.text = "작업 " + FormatDuration(stat.WorkSeconds);
            _attendanceLabel.text = "총 " + FormatDuration(stat.AttendanceSeconds);
            _otherLabel.text = "휴식 " + FormatDuration(stat.BreakSeconds)
                + "  식사 " + FormatDuration(stat.MealSeconds);
            SetBarRatio(_workBar, stat.WorkSeconds, maximumWorkSeconds);
            SetBarRatio(_attendanceBar, stat.AttendanceSeconds, maximumAttendanceSeconds);
        }

        /// <summary>
        /// A row is one day, one week or one month, so the label has to say which:
        /// "08.27 (목)" reads as a date, "08.24~08.30" as a span, "2026.08" as a
        /// month. The span is clipped to the requested range on the server, so a
        /// partial first or last week shows its real dates.
        /// </summary>
        public static string FormatBucket(MemberPeriodStat stat, StatisticsBucket bucket)
        {
            switch (bucket)
            {
                case StatisticsBucket.Week:
                    return stat.BucketStart.ToString("MM.dd") + "~" + stat.BucketEnd.ToString("MM.dd");
                case StatisticsBucket.Month:
                    return stat.BucketStart.ToString("yyyy.MM");
                default:
                    return stat.BucketStart.ToString("MM.dd")
                        + " (" + DayLabels[(int)stat.BucketStart.DayOfWeek] + ")";
            }
        }

        public static string FormatDuration(int seconds)
        {
            var safeSeconds = Math.Max(0, seconds);
            return (safeSeconds / 3600).ToString("00") + ":" + ((safeSeconds / 60) % 60).ToString("00");
        }

        private static float Ratio(int value, int maximum)
        {
            return maximum <= 0 ? 0f : Mathf.Clamp01((float)value / maximum);
        }

        internal static void SetBarRatio(Image bar, int value, int maximum)
        {
            if (bar == null)
            {
                return;
            }

            var rect = bar.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(Ratio(value, maximum), 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            bar.type = Image.Type.Simple;
        }
    }
}

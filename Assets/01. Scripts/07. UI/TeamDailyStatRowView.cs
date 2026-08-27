using System;
using TeamOverlay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.UI
{
    public sealed class TeamDailyStatRowView : MonoBehaviour
    {
        [SerializeField] private Text _dateLabel;
        [SerializeField] private Text _workLabel;
        [SerializeField] private Text _attendanceLabel;
        [SerializeField] private Text _otherLabel;
        [SerializeField] private Image _workBar;
        [SerializeField] private Image _attendanceBar;

        public void Bind(MemberDailyStat stat, int maximumWorkSeconds, int maximumAttendanceSeconds)
        {
            var dayLabels = "\uC77C\uC6D4\uD654\uC218\uBAA9\uAE08\uD1A0";
            _dateLabel.text = stat.LocalDate.ToString("MM.dd") + " (" + dayLabels[(int)stat.LocalDate.DayOfWeek] + ")";
            _workLabel.text = "\uC791\uC5C5 " + FormatDuration(stat.WorkSeconds);
            _attendanceLabel.text = "\uCD9C\uADFC " + FormatDuration(stat.AttendanceSeconds);
            _otherLabel.text = "\uD734\uC2DD " + FormatDuration(stat.BreakSeconds)
                + "  \uC2DD\uC0AC " + FormatDuration(stat.MealSeconds);
            _workBar.fillAmount = Ratio(stat.WorkSeconds, maximumWorkSeconds);
            _attendanceBar.fillAmount = Ratio(stat.AttendanceSeconds, maximumAttendanceSeconds);
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
    }
}

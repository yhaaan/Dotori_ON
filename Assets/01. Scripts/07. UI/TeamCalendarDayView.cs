using System;
using TeamOverlay.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamOverlay.UI
{
    /// <summary>One square of the month calendar: a date and what was worked on it.</summary>
    public sealed class TeamCalendarDayView : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>
        /// Where the fill stops. A day that ran away with the month would
        /// otherwise leave every other square nearly unfilled, and the point of
        /// the grid is comparing the ordinary days to each other.
        /// </summary>
        private const float MaximumFill = 0.85f;

        /// <summary>
        /// Past this much fill the square is bright enough that the light text
        /// stops reading and the dark one starts.
        /// </summary>
        private const float DarkTextAbove = 0.45f;

        [Header("Prefab references")]
        [SerializeField] private Image _background;
        [SerializeField] private Text _dayLabel;
        [SerializeField] private Text _durationLabel;

        /// <summary>Raised on any square, because the breakdown is a whole-grid mode.</summary>
        public event Action Clicked;

        /// <summary>
        /// A square inside the month. <paramref name="maximumSeconds"/> is the
        /// busiest day of the month, so the shading is relative to the month being
        /// looked at rather than to some fixed idea of a full day. The shading
        /// stays on attendance in both modes: only the numbers swap, so the shape
        /// of the month does not move under the reader.
        /// </summary>
        public void Bind(
            int dayOfMonth,
            MemberPeriodStat stat,
            int maximumSeconds,
            bool isToday,
            bool showBreakdown)
        {
            gameObject.SetActive(true);
            var attendance = stat?.AttendanceSeconds ?? 0;
            var fill = maximumSeconds <= 0 ? 0f : MaximumFill * attendance / maximumSeconds;
            var onBrightFill = fill > DarkTextAbove;

            if (_background != null)
            {
                _background.color = Color.Lerp(TeamOverlayPalette.Card, TeamOverlayPalette.Working, fill);
            }

            if (_dayLabel != null)
            {
                _dayLabel.text = dayOfMonth.ToString();
                // Today keeps the accent whatever the fill is: it is the square
                // the reader is looking for, and it is the one still running.
                _dayLabel.color = isToday
                    ? TeamOverlayPalette.Accent
                    : (onBrightFill ? TeamOverlayPalette.Window : TeamOverlayPalette.TextSecondary);
                _dayLabel.fontStyle = isToday ? FontStyle.Bold : FontStyle.Normal;
            }

            BindDuration(stat, attendance, showBreakdown, onBrightFill);
        }

        private void BindDuration(
            MemberPeriodStat stat,
            int attendance,
            bool showBreakdown,
            bool onBrightFill)
        {
            if (_durationLabel == null)
            {
                return;
            }

            // A day with nothing on it is left blank rather than filled with
            // 00:00, so the days that did happen are what the eye lands on.
            if (attendance <= 0)
            {
                _durationLabel.text = string.Empty;
                return;
            }

            if (!showBreakdown)
            {
                _durationLabel.fontSize = TotalFontSize;
                _durationLabel.color = onBrightFill
                    ? TeamOverlayPalette.Window
                    : TeamOverlayPalette.TextPrimary;
                _durationLabel.text = TeamPeriodStatRowView.FormatDuration(attendance);
                return;
            }

            // Three numbers do not fit three labels in a square this size, so the
            // colours carry the meaning instead. They are the same three the
            // ranking metrics use, and the legend under the grid names them.
            _durationLabel.fontSize = BreakdownFontSize;
            _durationLabel.color = TeamOverlayPalette.TextPrimary;
            _durationLabel.text = string.Join(
                "\n",
                Tinted(stat.WorkSeconds, TeamOverlayPalette.Working),
                Tinted(stat.BreakSeconds, TeamOverlayPalette.Break),
                Tinted(stat.MealSeconds, TeamOverlayPalette.Meal));
        }

        private const int TotalFontSize = 11;
        private const int BreakdownFontSize = 8;

        private static string Tinted(int seconds, Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" +
                   TeamPeriodStatRowView.FormatDuration(seconds) + "</color>";
        }

        /// <summary>A square for a day in a neighbouring month, which is left out.</summary>
        public void Clear()
        {
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke();
            }
        }
    }
}

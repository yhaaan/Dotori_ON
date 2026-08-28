using TeamOverlay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.UI
{
    /// <summary>One square of the month calendar: a date and what was worked on it.</summary>
    public sealed class TeamCalendarDayView : MonoBehaviour
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

        /// <summary>
        /// A square inside the month. <paramref name="maximumSeconds"/> is the
        /// busiest day of the month, so the shading is relative to the month being
        /// looked at rather than to some fixed idea of a full day.
        /// </summary>
        public void Bind(int dayOfMonth, MemberPeriodStat stat, int maximumSeconds, bool isToday)
        {
            gameObject.SetActive(true);
            var seconds = stat?.WorkSeconds ?? 0;
            var fill = maximumSeconds <= 0 ? 0f : MaximumFill * seconds / maximumSeconds;

            if (_background != null)
            {
                _background.color = Color.Lerp(TeamOverlayPalette.Card, TeamOverlayPalette.Working, fill);
            }

            var textColor = fill > DarkTextAbove
                ? TeamOverlayPalette.Window
                : TeamOverlayPalette.TextSecondary;
            if (_dayLabel != null)
            {
                _dayLabel.text = dayOfMonth.ToString();
                // Today keeps the accent whatever the fill is: it is the square
                // the reader is looking for, and it is the one still running.
                _dayLabel.color = isToday ? TeamOverlayPalette.Accent : textColor;
                _dayLabel.fontStyle = isToday ? FontStyle.Bold : FontStyle.Normal;
            }

            if (_durationLabel != null)
            {
                // A day with nothing on it is left blank rather than filled with
                // 00:00, so the days that did happen are what the eye lands on.
                _durationLabel.text = seconds > 0
                    ? TeamPeriodStatRowView.FormatDuration(seconds)
                    : string.Empty;
                _durationLabel.color = fill > DarkTextAbove
                    ? TeamOverlayPalette.Window
                    : TeamOverlayPalette.TextPrimary;
            }
        }

        /// <summary>A square for a day in a neighbouring month, which is left out.</summary>
        public void Clear()
        {
            gameObject.SetActive(false);
        }
    }
}

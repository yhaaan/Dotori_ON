using System;
using DOTORION.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DOTORION.UI
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
        [SerializeField] private Image _dailyGiftImage;

        private Color _authoredBackgroundColor;
        private bool _backgroundColorCaptured;

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
            bool showBreakdown,
            Sprite dailyGiftSprite = null)
        {
            gameObject.SetActive(true);
            var attendance = stat?.AttendanceSeconds ?? 0;
            var fill = maximumSeconds <= 0 ? 0f : MaximumFill * attendance / maximumSeconds;
            var onBrightFill = fill > DarkTextAbove;

            ApplyDailyGift(dailyGiftSprite);

            if (_background != null)
            {
                // Start from the colour authored on the prefab. The runtime used
                // to force Palette.Card even on an empty day, which silently
                // replaced any base colour chosen in the Inspector. Only the
                // attendance fill is a runtime colour decision now.
                if (!_backgroundColorCaptured)
                {
                    _authoredBackgroundColor = _background.color;
                    _backgroundColorCaptured = true;
                }

                _background.color = Color.Lerp(
                    _authoredBackgroundColor,
                    DOTORIONPalette.Working,
                    fill);
            }

            if (_dayLabel != null)
            {
                _dayLabel.text = dayOfMonth.ToString();
                // Today keeps the accent whatever the fill is: it is the square
                // the reader is looking for, and it is the one still running.
                _dayLabel.color = isToday
                    ? DOTORIONPalette.Accent
                    : (onBrightFill ? DOTORIONPalette.Window : DOTORIONPalette.TextSecondary);
            }

            BindDuration(stat, attendance, showBreakdown, onBrightFill);
        }

        private void ApplyDailyGift(Sprite sprite)
        {
            if (_dailyGiftImage == null)
            {
                var gift = new GameObject(
                    "DailyGift",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                gift.transform.SetParent(transform, false);
                _dailyGiftImage = gift.GetComponent<Image>();
            }

            if (sprite == null)
            {
                _dailyGiftImage.gameObject.SetActive(false);
                return;
            }

            _dailyGiftImage.sprite = sprite;
            _dailyGiftImage.color = Color.white;
            _dailyGiftImage.preserveAspect = true;
            _dailyGiftImage.raycastTarget = false;
            _dailyGiftImage.SetNativeSize();

            var rect = _dailyGiftImage.rectTransform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-2f, -2f);
            rect.SetAsLastSibling();
            _dailyGiftImage.gameObject.SetActive(true);
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
                    ? DOTORIONPalette.Window
                    : DOTORIONPalette.TextPrimary;
                _durationLabel.text = TeamPeriodStatRowView.FormatDuration(attendance);
                return;
            }

            // Three numbers do not fit three labels in a square this size, so the
            // colours carry the meaning instead. They are the same three the
            // ranking metrics use, and the legend under the grid names them.
            _durationLabel.fontSize = TotalFontSize;
            _durationLabel.color = DOTORIONPalette.TextPrimary;
            _durationLabel.text = string.Join(
                "\n",
                Tinted(stat.WorkSeconds, DOTORIONPalette.Working),
                Tinted(stat.BreakSeconds, DOTORIONPalette.Break),
                Tinted(stat.MealSeconds, DOTORIONPalette.Meal));
        }

        private const int TotalFontSize = 11;

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

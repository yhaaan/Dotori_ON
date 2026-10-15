using System;
using System.Collections.Generic;
using System.Linq;
using DOTORION.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.UI
{
    public sealed class TeamStatisticsPanelView : MonoBehaviour
    {
        private static readonly StatisticsPeriod[] Periods =
        {
            StatisticsPeriod.LastSevenDays,
            StatisticsPeriod.ThisMonth,
            StatisticsPeriod.AllTime
        };

        private static readonly RankingMetric[] Metrics =
        {
            RankingMetric.Work,
            RankingMetric.Attendance,
            RankingMetric.Break,
            RankingMetric.Meal
        };

        [SerializeField] private Button _dailyTabButton;
        [SerializeField] private Button _rankingTabButton;
        [SerializeField] private Button[] _periodButtons = new Button[3];
        [SerializeField] private Button[] _metricButtons = new Button[4];
        [SerializeField] private GameObject _dailyContent;
        [SerializeField] private GameObject _rankingContent;
        [SerializeField] private Text _periodLabel;
        [SerializeField] private Text _summaryText;
        [SerializeField] private Text _feedbackText;
        [SerializeField] private TeamPeriodStatRowView[] _statRows;
        [SerializeField] private TeamRankingRowView[] _rankingRows;
        [SerializeField] private TeamCalendarView _calendar;

        private bool _initialized;
        private bool _showRanking;
        private StatisticsPeriod _period = StatisticsPeriod.LastSevenDays;
        private StatisticsBucket _bucket = StatisticsBucket.Day;
        private RankingMetric _metric = RankingMetric.Work;
        private IReadOnlyList<TeamRankingEntry> _ranking;
        private string _localMemberId;
        private bool _selectionDirty = true;
        private readonly Dictionary<Button, ColorBlock> _authoredButtonColors = new Dictionary<Button, ColorBlock>();
        private Button _previousMonthButton;
        private Button _currentMonthButton;
        private Button _nextMonthButton;

        /// <summary>
        /// Raised when the person picks a different period. Only the app can serve
        /// it, because a new period is a new request.
        /// </summary>
        public event Action<StatisticsPeriod> PeriodChangeRequested;

        /// <summary>
        /// Raised by the buttons under the calendar: -1 for the month before,
        /// +1 for the month after, 0 for back to the current month. Like a
        /// period, a month is a new request, so the app serves it.
        /// </summary>
        public event Action<int> MonthStepRequested;

        public StatisticsPeriod Period => _period;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _calendar?.Initialize();
            BuildMonthNavigation();
            _dailyTabButton?.onClick.AddListener(() => SelectTab(false));
            _rankingTabButton?.onClick.AddListener(() => SelectTab(true));

            for (var index = 0; index < _periodButtons.Length && index < Periods.Length; index++)
            {
                var period = Periods[index];
                _periodButtons[index]?.onClick.AddListener(() => PeriodChangeRequested?.Invoke(period));
            }

            // The metric never leaves this panel: every entry already carries all
            // four numbers, so switching one is a re-sort, not another request.
            for (var index = 0; index < _metricButtons.Length && index < Metrics.Length; index++)
            {
                var metric = Metrics[index];
                _metricButtons[index]?.onClick.AddListener(() => SelectMetric(metric));
            }

            SelectTab(false);
            SetPeriod(_period);
            SelectMetric(_metric);
        }

        /// <summary>
        /// Remembers which period is showing. The app owns the actual load, and
        /// the panel artwork owns how the choice looks.
        /// </summary>
        public void SetPeriod(StatisticsPeriod period)
        {
            _period = period;
            _selectionDirty = true;
            SetMonthNavigationVisible(period == StatisticsPeriod.ThisMonth);
        }

        /// <summary>
        /// Greys out what cannot be pressed: 이전 달 at the first recorded month,
        /// and 이번 달 while the current month is already showing.
        /// </summary>
        public void SetMonthNavigation(bool canGoBack, bool showingCurrentMonth)
        {
            if (_previousMonthButton != null) _previousMonthButton.interactable = canGoBack;
            if (_currentMonthButton != null) _currentMonthButton.interactable = !showingCurrentMonth;
        }

        /// <summary>
        /// &lt; 이번 달 &gt; along the bottom of the panel. They are clones of the
        /// 월간 period button so every skin draws them with its own button art.
        /// They belong to the panel rather than the calendar so the ranking tab
        /// gets them too: a month is one choice for both tabs, and the ranking
        /// has to be able to make it without a trip back to the calendar.
        /// </summary>
        private void BuildMonthNavigation()
        {
            var template = _periodButtons.Length > 1 ? _periodButtons[1] : null;
            if (template == null)
            {
                return;
            }

            var height = ((RectTransform)template.transform).sizeDelta.y;
            var width = ((RectTransform)template.transform).sizeDelta.x;
            _previousMonthButton = CloneMonthButton(template, "PreviousMonth", "<", -1, 0f, 32f, height);
            _currentMonthButton = CloneMonthButton(template, "CurrentMonth", "이번 달", 0, 0.5f, width, height);
            _nextMonthButton = CloneMonthButton(template, "NextMonth", ">", 1, 1f, 32f, height);
            SetMonthNavigationVisible(_period == StatisticsPeriod.ThisMonth);
        }

        /// <summary>Only 월간 is a month; 7일 and 누적 have nothing to step through.</summary>
        private void SetMonthNavigationVisible(bool visible)
        {
            foreach (var button in new[] { _previousMonthButton, _currentMonthButton, _nextMonthButton })
            {
                if (button != null) button.gameObject.SetActive(visible);
            }
        }

        private Button CloneMonthButton(Button template, string name, string label, int step, float anchorX, float width, float height)
        {
            var button = Instantiate(template, transform);
            button.name = name;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => MonthStepRequested?.Invoke(step));
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }

            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 0f);
            rect.pivot = new Vector2(anchorX, 0f);
            // Inset like the calendar's squares and the ranking rows, so the
            // arrows line up under either; 10 up from the panel's bottom edge
            // is just below the calendar's sixth row.
            rect.anchoredPosition = new Vector2((0.5f - anchorX) * 18f, 10f);
            rect.sizeDelta = new Vector2(width, height);
            return button;
        }

        public void ShowLoading(StatisticsRange range)
        {
            SetPeriodLabel(range);
            ClearData();
            ShowFeedback("통계를 불러오는 중…", false);
        }

        public void ShowError(StatisticsRange range, string message)
        {
            SetPeriodLabel(range);
            ClearData();
            ShowFeedback(message, true);
        }

        public void Bind(
            StatisticsRange range,
            IReadOnlyList<MemberPeriodStat> stats,
            IReadOnlyList<TeamRankingEntry> ranking,
            string localMemberId)
        {
            SetPeriodLabel(range);
            _bucket = range?.Bucket ?? StatisticsBucket.Day;
            _localMemberId = localMemberId;
            _ranking = ranking ?? Array.Empty<TeamRankingEntry>();
            var buckets = stats ?? Array.Empty<MemberPeriodStat>();
            BindDailyContent(range, buckets);
            BindSummary(buckets);
            BindRankingRows();
            ShowFeedback(string.Empty, false);
            SelectTab(_showRanking);
        }

        private void ClearData()
        {
            _ranking = null;
            SetRowsVisible(false);
            if (_summaryText != null)
            {
                _summaryText.text = string.Empty;
            }
        }

        /// <summary>
        /// The month gets the calendar and the other periods keep the list. They
        /// are two readings of the same daily buckets, so only one is ever shown
        /// and the other is emptied rather than left holding a stale month.
        /// </summary>
        private void BindDailyContent(StatisticsRange range, IReadOnlyList<MemberPeriodStat> stats)
        {
            var showCalendar = range != null && range.Period == StatisticsPeriod.ThisMonth;
            if (_calendar != null)
            {
                _calendar.gameObject.SetActive(showCalendar);
            }

            if (showCalendar)
            {
                foreach (var row in _statRows) row.gameObject.SetActive(false);
                _calendar?.Bind(range, stats);
                return;
            }

            BindStatRows(stats);
        }

        private void BindStatRows(IReadOnlyList<MemberPeriodStat> stats)
        {
            // Newest first, and only as many buckets as there are rows: an all-time
            // range keeps growing, while the summary below still covers every one.
            var ordered = stats.OrderByDescending(entry => entry.BucketStart).ToArray();
            var maximumWork = ordered.Length == 0 ? 0 : ordered.Max(entry => entry.WorkSeconds);
            var maximumAttendance = ordered.Length == 0 ? 0 : ordered.Max(entry => entry.AttendanceSeconds);
            for (var index = 0; index < _statRows.Length; index++)
            {
                var hasEntry = index < ordered.Length;
                _statRows[index].gameObject.SetActive(hasEntry);
                if (hasEntry)
                {
                    _statRows[index].Bind(ordered[index], _bucket, maximumWork, maximumAttendance);
                }
            }
        }

        private void BindSummary(IReadOnlyList<MemberPeriodStat> stats)
        {
            if (_summaryText == null)
            {
                return;
            }

            var work = stats.Sum(entry => entry.WorkSeconds);
            var attendance = stats.Sum(entry => entry.AttendanceSeconds);
            // Averaging over every bucket would let untouched weekends drag the
            // number down, so the divisor is the buckets that had any attendance.
            var activeBuckets = stats.Count(entry => entry.HasActivity);
            var average = activeBuckets == 0 ? 0 : work / activeBuckets;
            _summaryText.text = "합계 작업 " + TeamPeriodStatRowView.FormatDuration(work)
                + "  ·  총 " + TeamPeriodStatRowView.FormatDuration(attendance)
                + "  ·  " + AverageLabel(_bucket) + " " + TeamPeriodStatRowView.FormatDuration(average)
                + " (" + activeBuckets + UnitLabel(_bucket) + ")";
        }

        private void SelectMetric(RankingMetric metric)
        {
            _metric = metric;
            _selectionDirty = true;
            BindRankingRows();
        }

        private void BindRankingRows()
        {
            var ranking = _ranking ?? Array.Empty<TeamRankingEntry>();
            var ordered = ranking
                .OrderByDescending(entry => entry.SecondsFor(_metric))
                .ThenBy(entry => entry.SortOrder)
                .ToArray();
            var maximum = ordered.Length == 0 ? 0 : ordered.Max(entry => entry.SecondsFor(_metric));
            for (var index = 0; index < _rankingRows.Length; index++)
            {
                var hasEntry = index < ordered.Length;
                _rankingRows[index].gameObject.SetActive(hasEntry);
                if (hasEntry)
                {
                    _rankingRows[index].Bind(
                        index + 1,
                        ordered[index],
                        _metric,
                        maximum,
                        string.Equals(ordered[index].MemberId, _localMemberId, StringComparison.Ordinal));
                }
            }
        }

        private void SelectTab(bool showRanking)
        {
            _showRanking = showRanking;
            if (_dailyContent != null) _dailyContent.SetActive(!showRanking);
            if (_rankingContent != null) _rankingContent.SetActive(showRanking);
            _selectionDirty = true;
        }

        private void OnEnable()
        {
            DOTORIONPalette.ThemeChanged += MarkSelectionDirty;
            _selectionDirty = true;
        }

        private void OnDisable()
        {
            DOTORIONPalette.ThemeChanged -= MarkSelectionDirty;
        }

        private void MarkSelectionDirty()
        {
            _selectionDirty = true;
        }

        /// <summary>
        /// Marking waits for the end of the frame because the theme bindings
        /// repaint these same buttons when a theme changes or the panel opens,
        /// and whichever of us runs last would otherwise win.
        /// </summary>
        private void LateUpdate()
        {
            if (!_selectionDirty)
            {
                return;
            }

            _selectionDirty = false;
            ShadeSelection(_dailyTabButton, !_showRanking);
            ShadeSelection(_rankingTabButton, _showRanking);
            for (var index = 0; index < _periodButtons.Length && index < Periods.Length; index++)
            {
                ShadeSelection(_periodButtons[index], Periods[index] == _period);
            }

            for (var index = 0; index < _metricButtons.Length && index < Metrics.Length; index++)
            {
                ShadeSelection(_metricButtons[index], Metrics[index] == _metric);
            }
        }

        /// <summary>
        /// The picked button rests in its pressed colour, so it keeps looking
        /// clicked. Unity's Selected state is set to the same colour; otherwise
        /// the button just clicked would show its plain selected tint until
        /// focus moved elsewhere.
        /// </summary>
        private void ShadeSelection(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            if (!_authoredButtonColors.TryGetValue(button, out var authored))
            {
                authored = button.colors;
                _authoredButtonColors[button] = authored;
            }

            var colors = button.colors;
            var pressed = DOTORIONThemeBinding.ThemedColor(button, "pressedColor", authored.pressedColor);
            colors.normalColor = selected ? pressed : DOTORIONThemeBinding.ThemedColor(button, "normalColor", authored.normalColor);
            colors.selectedColor = selected ? pressed : DOTORIONThemeBinding.ThemedColor(button, "selectedColor", authored.selectedColor);
            button.colors = colors;
        }

        private void SetRowsVisible(bool visible)
        {
            foreach (var row in _statRows) row.gameObject.SetActive(visible);
            foreach (var row in _rankingRows) row.gameObject.SetActive(visible);
            if (!visible)
            {
                _calendar?.ClearAll();
            }
        }

        private void SetPeriodLabel(StatisticsRange range)
        {
            if (_periodLabel == null || range == null)
            {
                return;
            }

            var from = range.FromLocalDate.HasValue
                ? range.FromLocalDate.Value.ToString("yyyy.MM.dd")
                : "처음";
            _periodLabel.text = from + " - " + range.ToLocalDate.ToString("yyyy.MM.dd");
        }

        private static string AverageLabel(StatisticsBucket bucket)
        {
            switch (bucket)
            {
                case StatisticsBucket.Week: return "주평균";
                case StatisticsBucket.Month: return "월평균";
                default: return "일평균";
            }
        }

        private static string UnitLabel(StatisticsBucket bucket)
        {
            switch (bucket)
            {
                case StatisticsBucket.Week: return "주";
                case StatisticsBucket.Month: return "개월";
                default: return "일";
            }
        }

        private void ShowFeedback(string message, bool isError)
        {
            if (_feedbackText == null)
            {
                return;
            }

            _feedbackText.text = message;
            _feedbackText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TeamOverlay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.UI
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

        /// <summary>
        /// Raised when the person picks a different period. Only the app can serve
        /// it, because a new period is a new request.
        /// </summary>
        public event Action<StatisticsPeriod> PeriodChangeRequested;

        public StatisticsPeriod Period => _period;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _calendar?.Initialize();
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

        /// <summary>Marks which period is showing. The app owns the actual load.</summary>
        public void SetPeriod(StatisticsPeriod period)
        {
            _period = period;
            for (var index = 0; index < _periodButtons.Length && index < Periods.Length; index++)
            {
                Tint(
                    _periodButtons[index],
                    Periods[index] == period ? TeamOverlayPalette.Accent : TeamOverlayPalette.Button);
            }
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
            for (var index = 0; index < _metricButtons.Length && index < Metrics.Length; index++)
            {
                Tint(
                    _metricButtons[index],
                    Metrics[index] == metric
                        ? TeamRankingRowView.MetricColor(metric)
                        : TeamOverlayPalette.Button);
            }

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
            Tint(_dailyTabButton, showRanking ? TeamOverlayPalette.Button : TeamOverlayPalette.Accent);
            Tint(_rankingTabButton, showRanking ? TeamOverlayPalette.Accent : TeamOverlayPalette.Button);
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
            _feedbackText.color = isError ? TeamOverlayPalette.Danger : TeamOverlayPalette.TextSecondary;
            _feedbackText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private static void Tint(Button button, Color color)
        {
            var image = button != null ? button.GetComponent<Image>() : null;
            if (image != null) image.color = color;
        }
    }
}

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
        [SerializeField] private Button _dailyTabButton;
        [SerializeField] private Button _rankingTabButton;
        [SerializeField] private GameObject _dailyContent;
        [SerializeField] private GameObject _rankingContent;
        [SerializeField] private Text _periodLabel;
        [SerializeField] private Text _feedbackText;
        [SerializeField] private TeamDailyStatRowView[] _dailyRows;
        [SerializeField] private TeamRankingRowView[] _rankingRows;

        private bool _initialized;
        private bool _showRanking;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _dailyTabButton?.onClick.AddListener(() => SelectTab(false));
            _rankingTabButton?.onClick.AddListener(() => SelectTab(true));
            SelectTab(false);
        }

        public void ShowLoading(DateTime fromLocalDate, DateTime toLocalDate)
        {
            SetPeriod(fromLocalDate, toLocalDate);
            SetRowsVisible(false);
            ShowFeedback("\uCD5C\uADFC 7\uC77C \uD1B5\uACC4\uB97C \uBD88\uB7EC\uC624\uB294 \uC911\u2026", false);
        }

        public void ShowError(DateTime fromLocalDate, DateTime toLocalDate, string message)
        {
            SetPeriod(fromLocalDate, toLocalDate);
            SetRowsVisible(false);
            ShowFeedback(message, true);
        }

        public void Bind(
            DateTime fromLocalDate,
            DateTime toLocalDate,
            IReadOnlyList<MemberDailyStat> dailyStats,
            IReadOnlyList<TeamRankingEntry> ranking,
            string localMemberId)
        {
            SetPeriod(fromLocalDate, toLocalDate);
            BindDailyRows(dailyStats ?? Array.Empty<MemberDailyStat>());
            BindRankingRows(ranking ?? Array.Empty<TeamRankingEntry>(), localMemberId);
            ShowFeedback(string.Empty, false);
            SelectTab(_showRanking);
        }

        private void BindDailyRows(IReadOnlyList<MemberDailyStat> stats)
        {
            var ordered = stats.OrderByDescending(entry => entry.LocalDate).ToArray();
            var maximumWork = ordered.Length == 0 ? 0 : ordered.Max(entry => entry.WorkSeconds);
            var maximumAttendance = ordered.Length == 0 ? 0 : ordered.Max(entry => entry.AttendanceSeconds);
            for (var index = 0; index < _dailyRows.Length; index++)
            {
                var hasEntry = index < ordered.Length;
                _dailyRows[index].gameObject.SetActive(hasEntry);
                if (hasEntry)
                {
                    _dailyRows[index].Bind(ordered[index], maximumWork, maximumAttendance);
                }
            }
        }

        private void BindRankingRows(IReadOnlyList<TeamRankingEntry> ranking, string localMemberId)
        {
            var maximumWork = ranking.Count == 0 ? 0 : ranking.Max(entry => entry.WorkSeconds);
            for (var index = 0; index < _rankingRows.Length; index++)
            {
                var hasEntry = index < ranking.Count;
                _rankingRows[index].gameObject.SetActive(hasEntry);
                if (hasEntry)
                {
                    _rankingRows[index].Bind(
                        index + 1,
                        ranking[index],
                        maximumWork,
                        string.Equals(ranking[index].MemberId, localMemberId, StringComparison.Ordinal));
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
            foreach (var row in _dailyRows) row.gameObject.SetActive(visible);
            foreach (var row in _rankingRows) row.gameObject.SetActive(visible);
        }

        private void SetPeriod(DateTime fromLocalDate, DateTime toLocalDate)
        {
            if (_periodLabel != null)
            {
                _periodLabel.text = fromLocalDate.ToString("yyyy.MM.dd") + " - " + toLocalDate.ToString("yyyy.MM.dd");
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

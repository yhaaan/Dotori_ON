using TeamOverlay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.UI
{
    public sealed class TeamRankingRowView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Text _rankLabel;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _pointsLabel;
        [SerializeField] private Text _workLabel;
        [SerializeField] private Text _attendanceLabel;
        [SerializeField] private Image _workBar;

        public void Bind(
            int rank,
            TeamRankingEntry entry,
            RankingMetric metric,
            int maximumSeconds,
            bool isLocalMember)
        {
            _rankLabel.text = rank.ToString();
            _nameLabel.text = entry.DisplayName;
            if (_pointsLabel != null)
            {
                // A streak of zero is left off rather than shown as "0일 연속",
                // which reads as a boast about nothing.
                _pointsLabel.text = entry.StreakDays > 0
                    ? entry.TotalPoints + "P  ·  " + entry.StreakDays + "일 연속"
                    : entry.TotalPoints + "P";
            }
            _workLabel.text = MetricName(metric) + " "
                + TeamPeriodStatRowView.FormatDuration(entry.SecondsFor(metric));
            _workLabel.color = MetricColor(metric);
            TeamPeriodStatRowView.SetBarRatio(_workBar, entry.SecondsFor(metric), maximumSeconds);
            if (_workBar != null)
            {
                _workBar.color = MetricColor(metric);
            }

            _background.color = isLocalMember
                ? new Color(0.14f, 0.25f, 0.37f, 1f)
                : TeamOverlayPalette.Card;

            // The second line is the number the ranked one is most often compared
            // against: work against the whole session, anything else against work.
            var comparison = metric == RankingMetric.Work
                ? RankingMetric.Attendance
                : RankingMetric.Work;
            _attendanceLabel.text = MetricName(comparison) + " "
                + TeamPeriodStatRowView.FormatDuration(entry.SecondsFor(comparison));
            _attendanceLabel.color = MetricColor(comparison);
        }

        public static string MetricName(RankingMetric metric)
        {
            switch (metric)
            {
                case RankingMetric.Attendance: return "총";
                case RankingMetric.Break: return "휴식";
                case RankingMetric.Meal: return "식사";
                default: return "작업";
            }
        }

        public static Color MetricColor(RankingMetric metric)
        {
            switch (metric)
            {
                case RankingMetric.Attendance: return TeamOverlayPalette.Accent;
                case RankingMetric.Break: return TeamOverlayPalette.Break;
                case RankingMetric.Meal: return TeamOverlayPalette.Meal;
                default: return TeamOverlayPalette.Working;
            }
        }
    }
}

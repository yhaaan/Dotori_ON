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
        [SerializeField] private Text _workLabel;
        [SerializeField] private Text _attendanceLabel;
        [SerializeField] private Image _workBar;

        public void Bind(int rank, TeamRankingEntry entry, int maximumWorkSeconds, bool isLocalMember)
        {
            _rankLabel.text = rank.ToString();
            _nameLabel.text = entry.DisplayName;
            _workLabel.text = "\uC791\uC5C5 " + TeamDailyStatRowView.FormatDuration(entry.WorkSeconds);
            _workBar.fillAmount = maximumWorkSeconds <= 0
                ? 0f
                : Mathf.Clamp01((float)entry.WorkSeconds / maximumWorkSeconds);
            _background.color = isLocalMember
                ? new Color(0.14f, 0.25f, 0.37f, 1f)
                : TeamOverlayPalette.Card;
            _attendanceLabel.text = "\uCD9C\uADFC " + TeamDailyStatRowView.FormatDuration(entry.AttendanceSeconds);
        }
    }
}

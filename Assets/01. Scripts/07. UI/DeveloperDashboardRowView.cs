using System;
using TeamOverlay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.UI
{
    /// <summary>One member as a row of numbers, with the button that erases them.</summary>
    public sealed class DeveloperDashboardRowView : MonoBehaviour
    {
        private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9d);

        [Header("Prefab references")]
        [SerializeField] private Image _background;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _sessionsLabel;
        [SerializeField] private Text _attendanceLabel;
        [SerializeField] private Text _pointsLabel;
        [SerializeField] private Text _lastSeenLabel;
        [SerializeField] private Button _deleteButton;

        private string _memberId;

        /// <summary>Carries the member id. Erasing is confirmed by the panel, not here.</summary>
        public event Action<string> DeleteRequested;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (_deleteButton != null)
            {
                _deleteButton.onClick.AddListener(() => DeleteRequested?.Invoke(_memberId));
            }
        }

        private bool _initialized;

        public void Bind(AdminMemberSummary member, bool isLocalMember)
        {
            gameObject.SetActive(true);
            _memberId = member.MemberId;

            if (_background != null)
            {
                _background.color = isLocalMember ? TeamOverlayPalette.Card : TeamOverlayPalette.CardOffline;
            }

            if (_nameLabel != null)
            {
                _nameLabel.text = member.DisplayName + (member.IsActive ? string.Empty : " (비활성)");
                _nameLabel.color = isLocalMember
                    ? TeamOverlayPalette.Accent
                    : TeamOverlayPalette.TextPrimary;
            }

            if (_sessionsLabel != null) _sessionsLabel.text = member.SessionCount + "회";
            if (_attendanceLabel != null)
            {
                _attendanceLabel.text = TeamPeriodStatRowView.FormatDuration(member.AttendanceSeconds);
            }

            if (_pointsLabel != null) _pointsLabel.text = member.TotalPoints + "P";
            if (_lastSeenLabel != null)
            {
                _lastSeenLabel.text = member.LastCheckedOutAtUtc.HasValue
                    ? member.LastCheckedOutAtUtc.Value.ToOffset(KoreaOffset).ToString("MM/dd HH:mm")
                    : "기록 없음";
            }

            // The server refuses to erase the caller, so offering the button on
            // your own row would only produce an error you cannot act on.
            if (_deleteButton != null)
            {
                _deleteButton.gameObject.SetActive(!isLocalMember);
            }
        }

        public void Clear()
        {
            gameObject.SetActive(false);
        }
    }
}

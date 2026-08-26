using System;
using TeamOverlay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.UI
{
    /// <summary>The editable per-member card prefab.</summary>
    public sealed class TeamMemberCardView : MonoBehaviour
    {
        private static readonly TimeSpan KoreaOffset = TimeSpan.FromHours(9d);

        [Header("Prefab references")]
        [SerializeField] private Image _background;
        [SerializeField] private Image _avatarBackground;
        [SerializeField] private Text _avatarText;
        [SerializeField] private Text _timerText;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _detailText;

        public void Bind(MemberState member, bool isLocalMember, DateTimeOffset nowUtc)
        {
            var isOnline = member.AttendanceStatus == AttendanceStatus.ClockedIn;
            var accent = StatusColor(member, isOnline);

            _background.color = isOnline ? TeamOverlayPalette.Card : TeamOverlayPalette.CardOffline;
            _avatarBackground.color = accent;
            _avatarText.text = InitialFor(member.DisplayName);
            _nameText.text = member.DisplayName;
            _nameText.color = isLocalMember ? TeamOverlayPalette.Accent : TeamOverlayPalette.TextPrimary;
            _statusText.text = StatusLabel(member, isOnline);
            _statusText.color = accent;

            if (isOnline && member.CheckedInAtUtc.HasValue)
            {
                _timerText.text = FormatElapsed(member.GetAttendanceElapsed(nowUtc));
                _timerText.color = TeamOverlayPalette.TextPrimary;

                // The note is what the person chose to say about right now, so it
                // outranks the check-in time, which the timer already implies.
                var hasNote = !string.IsNullOrWhiteSpace(member.StatusNote);
                _detailText.text = hasNote
                    ? member.StatusNote
                    : "출근 " + FormatKoreaTime(member.CheckedInAtUtc.Value, includeDate: false);
                _detailText.color = hasNote
                    ? TeamOverlayPalette.TextPrimary
                    : TeamOverlayPalette.TextSecondary;
            }
            else
            {
                _timerText.text = "--:--:--";
                _timerText.color = TeamOverlayPalette.Offline;
                _detailText.text = member.LastCheckedOutAtUtc.HasValue
                    ? "마지막 퇴근\n" + FormatKoreaTime(member.LastCheckedOutAtUtc.Value, includeDate: true)
                    : "출근 기록 없음";
                _detailText.color = TeamOverlayPalette.TextSecondary;
            }
        }

        private static string StatusLabel(MemberState member, bool isOnline)
        {
            if (!isOnline)
            {
                return "오프라인";
            }

            switch (member.ActivityStatus)
            {
                case ActivityStatus.Working: return "작업중";
                case ActivityStatus.Break: return "쉬는중";
                case ActivityStatus.Meal: return "식사중";
                default: return "온라인";
            }
        }

        private static Color StatusColor(MemberState member, bool isOnline)
        {
            if (!isOnline)
            {
                return TeamOverlayPalette.Offline;
            }

            switch (member.ActivityStatus)
            {
                case ActivityStatus.Working: return TeamOverlayPalette.Working;
                case ActivityStatus.Break: return TeamOverlayPalette.Break;
                case ActivityStatus.Meal: return TeamOverlayPalette.Meal;
                default: return TeamOverlayPalette.Accent;
            }
        }

        private static string InitialFor(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) ? "?" : displayName.Trim().Substring(0, 1);
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            var totalHours = (int)Math.Floor(elapsed.TotalHours);
            return $"{totalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }

        private static string FormatKoreaTime(DateTimeOffset utc, bool includeDate)
        {
            return utc.ToOffset(KoreaOffset).ToString(includeDate ? "MM/dd HH:mm" : "HH:mm");
        }
    }
}

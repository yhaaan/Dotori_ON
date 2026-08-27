using TeamOverlay.Core;
using UnityEngine;

namespace TeamOverlay.UI
{
    /// <summary>
    /// The one place a member's state turns into a label and a colour. The card
    /// and the mini overlay show the same person at the same moment, so a status
    /// that reads differently between them would be a bug either way.
    /// </summary>
    public static class MemberStatusDisplay
    {
        public static bool IsOnline(MemberState member)
        {
            return member != null && member.AttendanceStatus == AttendanceStatus.ClockedIn;
        }

        public static string Label(MemberState member)
        {
            if (!IsOnline(member))
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

        public static Color Accent(MemberState member)
        {
            if (!IsOnline(member))
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
    }
}

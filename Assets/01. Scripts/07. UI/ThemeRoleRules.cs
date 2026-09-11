using UnityEngine;

namespace DOTORION.UI
{
    // Old catalogs can participate without regenerating their IDs or touching user themes.
    public static class ThemeRoleRules
    {
        public static ThemeColorRole RoleOf(ThemeSlot slot)
        {
            if (slot.ColorRole != ThemeColorRole.None) return slot.ColorRole;
            if (slot.Target == ThemeTarget.SpriteField || slot.Target == ThemeTarget.ButtonSprite || slot.Color.a < .01f)
                return ThemeColorRole.None;
            var label = (slot.Label ?? "").ToLowerInvariant();
            var member = label.Substring(label.LastIndexOf('/') + 1);
            var hex = ColorUtility.ToHtmlStringRGB(slot.Color);
            if (slot.Target == ThemeTarget.ButtonColor)
            {
                switch (member)
                {
                    case "highlightedcolor": return ThemeColorRole.ButtonHover;
                    case "pressedcolor": return ThemeColorRole.ButtonPressed;
                    case "disabledcolor": return ThemeColorRole.ButtonDisabled;
                    case "selectedcolor": return hex == "FFFFFF" ? ThemeColorRole.None : ThemeColorRole.ButtonHover;
                    default: return ThemeColorRole.None;
                }
            }
            if (slot.Target == ThemeTarget.ColorField)
            {
                if (member.Contains("onlinecard")) return ThemeColorRole.Card;
                if (member.Contains("offlinecard")) return ThemeColorRole.CardOffline;
                if (member.Contains("nameonline")) return ThemeColorRole.TextOnAccent;
                if (member.Contains("nameoffline")) return ThemeColorRole.TextSecondary;
                if (member.Contains("working")) return ThemeColorRole.Working;
                if (member.Contains("break")) return ThemeColorRole.Break;
                if (member.Contains("meal")) return ThemeColorRole.Meal;
                if (member.Contains("offline")) return ThemeColorRole.Offline;
                if (member.Contains("online")) return ThemeColorRole.Accent;
                if (member.Contains("unselected")) return ThemeColorRole.None;
                if (member.Contains("selected")) return ThemeColorRole.Accent;
                if (member.Contains("error") || member.Contains("dotnote")) return ThemeColorRole.Danger;
                if (member.Contains("dotidle")) return ThemeColorRole.TextPrimary;
            }
            bool text = label.Contains("text/appearance");
            // White on authored art means no tint, not a shared white palette entry.
            if (!text && hex == "FFFFFF" && (slot.Sprite != null || slot.RuntimeSprite || slot.Target == ThemeTarget.ColorField)) return ThemeColorRole.None;
            if (label.Contains("modalbackdrop") || label.Contains("deleteconfirm")) return ThemeColorRole.ModalBackdrop;
            switch (hex)
            {
                case "4FD1A1": return ThemeColorRole.Working;
                case "F4C95D": return ThemeColorRole.Break;
                case "FF8E72": return ThemeColorRole.Meal;
                case "6EA8FE": case "5B8EFE": return ThemeColorRole.Accent;
                case "E97171": case "DE2323": return ThemeColorRole.Danger;
                case "667386": return ThemeColorRole.Offline;
                case "F4F7FB": return ThemeColorRole.TextPrimary;
                case "A9B5C6": case "7D8CAD": return ThemeColorRole.TextSecondary;
            }
            if (text) return hex == "FFFFFF" || hex == "000000" || hex == "101621" ? ThemeColorRole.TextPrimary : ThemeColorRole.TextSecondary;
            var path = label.Split('·')[0].TrimEnd();
            var name = path.Substring(path.LastIndexOf('/') + 1);
            if (name == "windowbackground" || name == "minioverlaypanel") return ThemeColorRole.Window;
            if (name.Contains("topbar") || name.Contains("dragstrip")) return ThemeColorRole.TopBar;
            if (name == "localcontrols" || name == "controlbar") return ThemeColorRole.ControlBar;
            if (hex == "181F2A" || hex == "BECDEB") return ThemeColorRole.CardOffline;
            if (hex == "273449" || name.Contains("button") || name == "nudge") return ThemeColorRole.Button;
            if (hex == "34445D") return ThemeColorRole.ButtonHover;
            return ThemeColorRole.Card;
        }

        public static string Label(ThemeColorRole role)
        {
            switch (role)
            {
                case ThemeColorRole.Window: return "창 배경";
                case ThemeColorRole.TopBar: return "상단 바";
                case ThemeColorRole.Card: return "패널 / 카드";
                case ThemeColorRole.CardOffline: return "비활성 카드";
                case ThemeColorRole.ControlBar: return "하단 조작 영역";
                case ThemeColorRole.TextPrimary: return "기본 글자";
                case ThemeColorRole.TextSecondary: return "보조 글자";
                case ThemeColorRole.TextOnAccent: return "상태 배경 위 글자";
                case ThemeColorRole.Working: return "작업 중";
                case ThemeColorRole.Break: return "휴식";
                case ThemeColorRole.Meal: return "식사";
                case ThemeColorRole.Offline: return "오프라인";
                case ThemeColorRole.Accent: return "강조 / 선택";
                case ThemeColorRole.Danger: return "경고 / 알림";
                case ThemeColorRole.Button: return "버튼 배경";
                case ThemeColorRole.ButtonHover: return "버튼 호버 틴트";
                case ThemeColorRole.ButtonPressed: return "버튼 누름 틴트";
                case ThemeColorRole.ButtonDisabled: return "버튼 비활성 틴트";
                case ThemeColorRole.ModalBackdrop: return "팝업 뒤 배경";
                default: return "팔레트 미적용";
            }
        }
    }
}

using UnityEngine;

namespace DOTORION.UI
{
    public static class DOTORIONPalette
    {
        public static readonly Color Window = Hex("101621");
        public static readonly Color TopBar = Hex("171F2D");
        public static readonly Color Card = Hex("1C2635");
        public static readonly Color CardOffline = Hex("181F2A");
        public static readonly Color ControlBar = Hex("121A26");
        public static readonly Color TextPrimary = Hex("F4F7FB");
        public static readonly Color TextSecondary = Hex("A9B5C6");
        public static readonly Color Working = Hex("4FD1A1");
        public static readonly Color Break = Hex("F4C95D");
        public static readonly Color Meal = Hex("FF8E72");
        public static readonly Color Offline = Hex("667386");
        public static readonly Color Accent = Hex("6EA8FE");
        public static readonly Color Danger = Hex("E97171");
        public static readonly Color Button = Hex("273449");
        public static readonly Color ButtonHover = Hex("34445D");

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString("#" + value, out var color) ? color : Color.magenta;
        }
    }
}

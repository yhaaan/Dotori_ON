using System;
using System.Collections.Generic;
using UnityEngine;

namespace DOTORION.UI
{
    public enum ThemeMode { Original, Tint, SpriteSwap }
    public enum ThemeTarget { Graphic, ColorField, SpriteField, ButtonColor, ButtonSprite }
    public enum ThemeColorRole
    {
        None, Window, TopBar, Card, CardOffline, ControlBar,
        TextPrimary, TextSecondary, TextOnAccent,
        Working, Break, Meal, Offline, Accent, Danger,
        Button, ButtonHover, ButtonPressed, ButtonDisabled, ModalBackdrop
    }
    [Serializable]
    public sealed class ThemePaletteOverride
    {
        public ThemeColorRole Role;
        public Color Color = Color.white;
    }
    [Serializable]
    public sealed class ThemeSpriteOverride
    {
        public Sprite Original;
        public Sprite Replacement;
        public bool PreserveTint = true;
    }
    [Serializable]
    public sealed class ThemeSlot
    {
        public string Id;
        public string Label;
        public ThemeTarget Target;
        public ThemeMode Mode;
        public Color Color = Color.white;
        public Sprite Sprite;
        public bool RuntimeColor;
        public bool RuntimeSprite;
        public bool AllowsSprite;
        public ThemeColorRole ColorRole;
    }
    [Serializable]
    public sealed class ThemeOverride
    {
        public string Id;
        public ThemeMode Mode;
        public Color Color = Color.white;
        public Sprite Sprite;
    }
    [CreateAssetMenu(menuName = "DOTORI ON/Theme Catalog")]
    public sealed class DOTORIONThemeCatalog : ScriptableObject
    {
        public List<ThemeSlot> Slots = new List<ThemeSlot>();
    }
}

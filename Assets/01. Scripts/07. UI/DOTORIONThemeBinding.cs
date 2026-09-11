using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.UI
{
    [DefaultExecutionOrder(-1000)]
    public sealed class DOTORIONThemeBinding : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public ThemeSlot Slot;
            public Component Target;
            public string Member;
            public Selectable.Transition OriginalTransition;
        }
        public List<Entry> Entries = new List<Entry>();
        private void OnEnable() { DOTORIONPalette.ThemeChanged += Apply; Apply(); }
        private void OnDisable() { DOTORIONPalette.ThemeChanged -= Apply; }
        public void Apply() => Apply(DOTORIONPalette.Active);
        public static Color BaseColor(Graphic graphic, Color fallback)
        {
            var binding = graphic.GetComponent<DOTORIONThemeBinding>();
            var entry = binding != null ? binding.Entries.Find(e => e.Target == graphic && e.Slot.Target == ThemeTarget.Graphic) : null;
            if (entry == null) return fallback;
            var value = DOTORIONPalette.Active.Find(entry.Slot.Id);
            if (value != null) return value.Mode == ThemeMode.Original ? fallback : value.Color;
            return DOTORIONPalette.Active.ResolveRoleColor(ThemeRoleRules.RoleOf(entry.Slot), fallback);
        }
        public void Apply(DOTORIONTheme theme)
        {
            foreach (var entry in Entries)
                if (entry.Slot.Target == ThemeTarget.ButtonSprite && entry.Target is Selectable control)
                    control.transition = entry.OriginalTransition;
            foreach (var entry in Entries)
            {
                if (entry.Target == null) continue;
                var slot = entry.Slot;
                var value = theme != null ? theme.Find(slot.Id) : null;
                var mode = value != null ? value.Mode : ThemeMode.Original;
                var color = theme != null ? theme.ResolveColor(slot) : slot.Color;
                var sprite = theme != null ? theme.ResolveSprite(slot) : slot.Sprite;
                switch (slot.Target)
                {
                    case ThemeTarget.Graphic:
                        var graphic = (Graphic)entry.Target;
                        if (!slot.RuntimeColor) graphic.color = color;
                        if (!slot.RuntimeSprite && graphic is Image image) image.sprite = sprite;
                        break;
                    case ThemeTarget.ColorField:
                    case ThemeTarget.SpriteField:
                        var field = entry.Target.GetType().GetField(entry.Member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        field?.SetValue(entry.Target, slot.Target == ThemeTarget.ColorField ? (object)color : sprite);
                        break;
                    case ThemeTarget.ButtonColor:
                        var selectable = (Selectable)entry.Target;
                        object colors = selectable.colors;
                        typeof(ColorBlock).GetProperty(entry.Member)?.SetValue(colors, color);
                        selectable.colors = (ColorBlock)colors;
                        break;
                    case ThemeTarget.ButtonSprite:
                        var button = (Selectable)entry.Target;
                        object sprites = button.spriteState;
                        typeof(SpriteState).GetProperty(entry.Member)?.SetValue(sprites, sprite);
                        button.spriteState = (SpriteState)sprites;
                        if (sprite != null && (sprite != slot.Sprite || (mode == ThemeMode.SpriteSwap && value.Sprite != null))) button.transition = Selectable.Transition.SpriteSwap;
                        break;
                }
            }
        }
    }
}

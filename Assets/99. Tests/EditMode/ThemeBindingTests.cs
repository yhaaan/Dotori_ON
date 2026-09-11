using DOTORION.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.Tests.EditMode
{
    public sealed class ThemeBindingTests
    {
        [Test]
        public void SharedPaletteChangesRepeatedSlotsAndCodePaletteButKeepsAlpha()
        {
            var theme = ScriptableObject.CreateInstance<DOTORIONTheme>();
            try
            {
                theme.Palette.Add(new ThemePaletteOverride { Role = ThemeColorRole.Working, Color = Color.magenta });
                foreach (var label in new[] { "Card · TeamMemberCardView/_workingColor", "Mini · MiniMemberRowView/_workingColor" })
                {
                    var slot = new ThemeSlot { Label = label, Target = ThemeTarget.ColorField, Color = new Color(.3f, .8f, .6f, .55f) };
                    Assert.That(theme.ResolveColor(slot), Is.EqualTo(new Color(1, 0, 1, .55f)));
                }
                Assert.That(theme.Working, Is.EqualTo(Color.magenta));
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [Test]
        public void LocalExceptionWinsAndRemovingItRestoresSharedPalette()
        {
            var theme = ScriptableObject.CreateInstance<DOTORIONTheme>();
            try
            {
                var slot = new ThemeSlot { Id = "title", Label = "Panel/Title · Text/Appearance", Color = Color.white };
                theme.Palette.Add(new ThemePaletteOverride { Role = ThemeColorRole.TextPrimary, Color = Color.blue });
                Assert.That(theme.ResolveColor(slot), Is.EqualTo(Color.blue));
                var local = new ThemeOverride { Id = slot.Id, Mode = ThemeMode.Tint, Color = Color.red };
                theme.Overrides.Add(local);
                Assert.That(theme.ResolveColor(slot), Is.EqualTo(Color.red));
                local.Mode = ThemeMode.Original;
                Assert.That(theme.ResolveColor(slot), Is.EqualTo(Color.white));
                theme.Overrides.Clear();
                Assert.That(theme.ResolveColor(slot), Is.EqualTo(Color.blue));
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [Test]
        public void SharedSpriteReplacementUsesAuthoredSourceAndLeavesWhiteArtworkUntinted()
        {
            var theme = ScriptableObject.CreateInstance<DOTORIONTheme>();
            var texture = new Texture2D(2, 2);
            var original = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            var replacement = Sprite.Create(texture, new Rect(1, 1, 1, 1), Vector2.zero);
            try
            {
                theme.Palette.Add(new ThemePaletteOverride { Role = ThemeColorRole.TextPrimary, Color = Color.red });
                theme.Palette.Add(new ThemePaletteOverride { Role = ThemeColorRole.Card, Color = Color.red });
                theme.Sprites.Add(new ThemeSpriteOverride { Original = original, Replacement = replacement });
                foreach (var id in new[] { "one", "two" })
                {
                    var slot = new ThemeSlot { Id = id, Label = "Panel/Image · Image/Appearance", Sprite = original, Color = Color.white };
                    Assert.That(theme.ResolveSprite(slot), Is.SameAs(replacement));
                    Assert.That(theme.ResolveColor(slot), Is.EqualTo(Color.white));
                }
                theme.Sprites.Clear();
                Assert.That(theme.ResolveSprite(new ThemeSlot { Sprite = original }), Is.SameAs(original));
            }
            finally { Object.DestroyImmediate(theme); Object.DestroyImmediate(original); Object.DestroyImmediate(replacement); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void ExistingPrefabStatusFieldsFollowSharedPaletteWithoutRecollecting()
        {
            var theme = ScriptableObject.CreateInstance<DOTORIONTheme>();
            theme.Palette.Add(new ThemePaletteOverride { Role = ThemeColorRole.Working, Color = Color.magenta });
            try
            {
                foreach (var name in new[] { "TeamMemberCard", "MiniMemberRow" })
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02. Prefabs/" + name + ".prefab");
                    var instance = Object.Instantiate(asset);
                    try
                    {
                        foreach (var binding in instance.GetComponentsInChildren<DOTORIONThemeBinding>(true)) binding.Apply(theme);
                        var component = name == "TeamMemberCard" ? (Component)instance.GetComponent<TeamMemberCardView>() : instance.GetComponent<MiniMemberRowView>();
                        var field = new UnityEditor.SerializedObject(component).FindProperty("_workingColor");
                        Assert.That(field.colorValue, Is.EqualTo(Color.magenta));
                    }
                    finally { Object.DestroyImmediate(instance); }
                }
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [Test]
        public void SpriteSwapAndClonedBindingsUseTheirOwnGraphic()
        {
            var go = new GameObject("Theme test", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var theme = ScriptableObject.CreateInstance<DOTORIONTheme>();
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            GameObject clone = null;
            try
            {
                var binding = go.AddComponent<DOTORIONThemeBinding>();
                binding.Entries.Add(new DOTORIONThemeBinding.Entry { Target = go.GetComponent<Image>(), Slot = new ThemeSlot { Id = "image", Color = Color.cyan } });
                clone = Object.Instantiate(go);
                theme.Overrides.Add(new ThemeOverride { Id = "image", Mode = ThemeMode.SpriteSwap, Sprite = sprite, Color = Color.white });
                clone.GetComponent<DOTORIONThemeBinding>().Apply(theme);
                Assert.That(clone.GetComponent<Image>().sprite, Is.SameAs(sprite));
                Assert.That(go.GetComponent<Image>().sprite, Is.Null);
                clone.GetComponent<DOTORIONThemeBinding>().Apply(null);
                Assert.That(clone.GetComponent<Image>().sprite, Is.Null);
                Assert.That(clone.GetComponent<Image>().color, Is.EqualTo(Color.cyan));
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                Object.DestroyImmediate(go); Object.DestroyImmediate(theme);
                Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ThemeSwitchAndMissingOverrideRestoreAuthoredValues()
        {
            var go = new GameObject("Theme test", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var theme = ScriptableObject.CreateInstance<DOTORIONTheme>();
            try
            {
                var image = go.GetComponent<Image>();
                var binding = go.AddComponent<DOTORIONThemeBinding>();
                binding.Entries.Add(new DOTORIONThemeBinding.Entry { Target = image, Slot = new ThemeSlot { Id = "panel", Color = Color.cyan } });
                theme.Overrides.Add(new ThemeOverride { Id = "panel", Mode = ThemeMode.Tint, Color = Color.magenta });
                binding.Apply(theme);
                Assert.That(image.color, Is.EqualTo(Color.magenta));
                binding.Apply(null);
                Assert.That(image.color, Is.EqualTo(Color.cyan));
                theme.Overrides.Clear();
                binding.Apply(theme);
                Assert.That(image.color, Is.EqualTo(Color.cyan));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(theme); }
        }

        [Test]
        public void RuntimeColorIsNotOverwrittenAndButtonStatesRestore()
        {
            var go = new GameObject("Theme test", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var theme = ScriptableObject.CreateInstance<DOTORIONTheme>();
            try
            {
                var image = go.GetComponent<Image>();
                image.color = Color.green;
                var button = go.GetComponent<Button>();
                var binding = go.AddComponent<DOTORIONThemeBinding>();
                binding.Entries.Add(new DOTORIONThemeBinding.Entry { Target = image, Slot = new ThemeSlot { Id = "status", RuntimeColor = true } });
                binding.Entries.Add(new DOTORIONThemeBinding.Entry { Target = button, Member = "pressedColor", Slot = new ThemeSlot { Id = "pressed", Target = ThemeTarget.ButtonColor, Color = Color.gray } });
                theme.Overrides.Add(new ThemeOverride { Id = "status", Mode = ThemeMode.Tint, Color = Color.red });
                theme.Overrides.Add(new ThemeOverride { Id = "pressed", Mode = ThemeMode.Tint, Color = Color.blue });
                binding.Apply(theme);
                Assert.That(image.color, Is.EqualTo(Color.green));
                Assert.That(button.colors.pressedColor, Is.EqualTo(Color.blue));
                binding.Apply(null);
                Assert.That(button.colors.pressedColor, Is.EqualTo(Color.gray));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(theme); }
        }
    }
}

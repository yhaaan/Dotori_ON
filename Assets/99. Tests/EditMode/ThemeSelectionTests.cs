using System.Collections.Generic;
using DOTORION.UI;
using NUnit.Framework;
using UnityEngine;

namespace DOTORION.Tests.EditMode
{
    /// <summary>
    /// The round theme buttons' choice: which theme each button stands for, and
    /// which one a saved name brings back after a restart.
    /// </summary>
    public sealed class ThemeSelectionTests
    {
        private readonly List<DOTORIONTheme> _created = new List<DOTORIONTheme>();

        [TearDown]
        public void TearDown()
        {
            foreach (var theme in _created)
            {
                Object.DestroyImmediate(theme);
            }

            _created.Clear();
        }

        [Test]
        public void At_ReturnsTheThemeBehindEachButton()
        {
            var basic = Theme("Basic");
            var pink = Theme("Pink");
            var themes = new[] { basic, pink };

            Assert.That(ThemeSelection.At(themes, 0), Is.SameAs(basic));
            Assert.That(ThemeSelection.At(themes, 1), Is.SameAs(pink));
        }

        [Test]
        public void At_ReturnsNullForAButtonWithNoTheme()
        {
            var themes = new[] { Theme("Basic") };

            Assert.That(ThemeSelection.At(themes, 1), Is.Null);
            Assert.That(ThemeSelection.At(themes, -1), Is.Null);
            Assert.That(ThemeSelection.At(null, 0), Is.Null);
            Assert.That(ThemeSelection.At(new DOTORIONTheme[] { null }, 0), Is.Null);
        }

        [Test]
        public void Resolve_BringsBackTheSavedThemeByName()
        {
            var basic = Theme("Basic");
            var pink = Theme("Pink");

            Assert.That(ThemeSelection.Resolve(new[] { basic, pink }, basic, "Pink"), Is.SameAs(pink));
        }

        [Test]
        public void Resolve_FallsBackWhenNothingOrAnUnknownNameWasSaved()
        {
            var basic = Theme("Basic");
            var pink = Theme("Pink");
            var themes = new[] { basic, pink };

            Assert.That(ThemeSelection.Resolve(themes, basic, null), Is.SameAs(basic));
            Assert.That(ThemeSelection.Resolve(themes, basic, string.Empty), Is.SameAs(basic));
            Assert.That(ThemeSelection.Resolve(themes, basic, "Deleted"), Is.SameAs(basic));
            Assert.That(ThemeSelection.Resolve(null, basic, "Pink"), Is.SameAs(basic));
        }

        [Test]
        public void LabelOf_PrefersTheDisplayNameAndFallsBackToTheAssetName()
        {
            var named = Theme("PinkSkinTheme");
            named.DisplayName = "Pink";
            var unnamed = Theme("DarkTheme");

            Assert.That(ThemeSelection.LabelOf(named), Is.EqualTo("Pink"));
            Assert.That(ThemeSelection.LabelOf(unnamed), Is.EqualTo("DarkTheme"));
            Assert.That(ThemeSelection.LabelOf(null), Is.Empty);
        }

        [Test]
        public void ShippedThemes_AreTwoAndLookDifferentOnTheirButtons()
        {
            var app = UnityEditor.AssetDatabase.LoadAssetAtPath<DOTORIONApp>(
                "Assets/Resources/DOTORION/DOTORIONApp.prefab");
            var themes = new UnityEditor.SerializedObject(app).FindProperty("_themes");
            Assert.That(themes.arraySize, Is.EqualTo(2));

            var first = (DOTORIONTheme)themes.GetArrayElementAtIndex(0).objectReferenceValue;
            var second = (DOTORIONTheme)themes.GetArrayElementAtIndex(1).objectReferenceValue;
            // The buttons differ by colour alone, so two themes with the same
            // swatch would leave two identical circles to choose between.
            Assert.That(first.Swatch, Is.Not.EqualTo(second.Swatch));
            Assert.That(ThemeSelection.LabelOf(first), Is.Not.Empty);
            Assert.That(ThemeSelection.LabelOf(second), Is.Not.Empty);
        }

        private DOTORIONTheme Theme(string name)
        {
            var theme = ScriptableObject.CreateInstance<DOTORIONTheme>();
            theme.name = name;
            _created.Add(theme);
            return theme;
        }
    }
}

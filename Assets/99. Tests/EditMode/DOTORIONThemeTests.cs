using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOTORION.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DOTORION.Tests.EditMode
{
    /// <summary>
    /// Pins the seam the colour themes hang off: every slot a theme asset holds
    /// has to be readable through <see cref="DOTORIONPalette"/>, and it has to be
    /// the matching slot. The palette repeats the theme's list of names by hand,
    /// so a wrong slot there - Card reading CardOffline - would ship a colour
    /// that looks plausible and is simply the wrong one.
    /// </summary>
    public sealed class DOTORIONThemeTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            // The palette is global, so a test that left a theme in it would
            // hand that theme to whatever ran next.
            DOTORIONPalette.Use(null);

            foreach (var asset in _created)
            {
                Object.DestroyImmediate(asset);
            }

            _created.Clear();
        }

        [Test]
        public void EverySlot_IsReachableThroughThePalette()
        {
            var onTheme = ColorMembers(typeof(DOTORIONTheme), BindingFlags.Instance);
            var onPalette = ColorMembers(typeof(DOTORIONPalette), BindingFlags.Static);

            Assert.That(onTheme, Is.Not.Empty);
            Assert.That(
                onPalette,
                Is.EquivalentTo(onTheme),
                "A slot on the theme with no property on the palette can never be " +
                "read, and a property on the palette with no slot cannot be themed.");
        }

        [Test]
        public void Palette_ReadsEverySlotFromTheActiveTheme()
        {
            var theme = CreateThemeWithDistinctColors();
            DOTORIONPalette.Use(theme);

            foreach (var slot in ColorMembers(typeof(DOTORIONTheme), BindingFlags.Instance))
            {
                Assert.That(
                    ReadColor(typeof(DOTORIONPalette), BindingFlags.Static, slot, null),
                    Is.EqualTo(ReadColor(typeof(DOTORIONTheme), BindingFlags.Instance, slot, theme)),
                    "DOTORIONPalette." + slot + " does not read the theme's " + slot + " slot.");
            }
        }

        [Test]
        public void Palette_FallsBackToTheBuiltInSchemeWithNoTheme()
        {
            // An app prefab with an empty theme slot has to keep drawing, and it
            // has to draw what the overlay has always drawn.
            DOTORIONPalette.Use(CreateThemeWithDistinctColors());
            DOTORIONPalette.Use(null);

            Assert.That(DOTORIONPalette.Active, Is.SameAs(DOTORIONPalette.BuiltIn));
            Assert.That(DOTORIONPalette.Card, Is.EqualTo(DOTORIONPalette.BuiltIn.Card));
            Assert.That(DOTORIONPalette.TextPrimary, Is.EqualTo(DOTORIONPalette.BuiltIn.TextPrimary));
        }

        [Test]
        public void ThemeChanged_FiresOnlyWhenTheThemeActuallyChanges()
        {
            var theme = CreateThemeWithDistinctColors();
            var fired = 0;

            DOTORIONPalette.ThemeChanged += Count;
            try
            {
                DOTORIONPalette.Use(theme);
                Assert.That(fired, Is.EqualTo(1));

                // Repainting every graphic in the overlay is not free, so setting
                // the theme it already has must not ask for one.
                DOTORIONPalette.Use(theme);
                Assert.That(fired, Is.EqualTo(1));

                DOTORIONPalette.Use(null);
                Assert.That(fired, Is.EqualTo(2));
            }
            finally
            {
                DOTORIONPalette.ThemeChanged -= Count;
            }

            void Count() => fired++;
        }

        [Test]
        public void ShippedThemeAsset_IsWiredIntoTheAppPrefab()
        {
            var theme = AssetDatabase.LoadAssetAtPath<DOTORIONTheme>(
                "Assets/Resources/DOTORION/DarkTheme.asset");
            Assert.That(theme, Is.Not.Null, "Run DOTORI ON/Create Missing Theme Asset.");

            var app = AssetDatabase.LoadAssetAtPath<DOTORIONApp>(
                "Assets/Resources/DOTORION/DOTORIONApp.prefab");
            Assert.That(app, Is.Not.Null);

            // Unwired the overlay still draws, off the built-in scheme, so this
            // would not look broken - it would just make the asset dead weight
            // that nothing reads.
            var reference = new SerializedObject(app).FindProperty("_theme");
            Assert.That(reference, Is.Not.Null);
            Assert.That(reference.objectReferenceValue, Is.SameAs(theme));
        }

        private DOTORIONTheme CreateThemeWithDistinctColors()
        {
            var theme = ScriptableObject.CreateInstance<DOTORIONTheme>();
            _created.Add(theme);

            // Every slot gets a colour no other slot has, so a property reading
            // the neighbouring slot cannot pass by coincidence.
            var data = new SerializedObject(theme);
            var slots = ColorMembers(typeof(DOTORIONTheme), BindingFlags.Instance);
            for (var index = 0; index < slots.Count; index++)
            {
                var field = data.FindProperty(FieldNameOf(slots[index]));
                Assert.That(field, Is.Not.Null, "No serialized field behind " + slots[index] + ".");
                field.colorValue = new Color((index + 1) / 32f, (index + 1) / 64f, 1f - index / 32f, 1f);
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            return theme;
        }

        /// <summary>The slot names a type exposes as colours, sorted for comparing.</summary>
        private static List<string> ColorMembers(System.Type type, BindingFlags scope)
        {
            return type
                .GetProperties(BindingFlags.Public | scope)
                .Where(property => property.PropertyType == typeof(Color))
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToList();
        }

        private static Color ReadColor(System.Type type, BindingFlags scope, string name, object target)
        {
            return (Color)type.GetProperty(name, BindingFlags.Public | scope).GetValue(target);
        }

        private static string FieldNameOf(string slot)
        {
            return "_" + char.ToLowerInvariant(slot[0]) + slot.Substring(1);
        }
    }
}

using System;
using UnityEngine;

namespace DOTORION.UI
{
    /// <summary>
    /// The colours the overlay draws with, read through whichever
    /// <see cref="DOTORIONTheme"/> is active.
    ///
    /// These used to be the values themselves, as compile-time constants, so a
    /// colour could only change by rebuilding. The names and every call site are
    /// unchanged - <c>DOTORIONPalette.Card</c> still hands back a colour - but
    /// the answer now comes from an asset. That is what makes a second theme
    /// possible: it is a second asset, not a second branch.
    ///
    /// Nothing here loads an asset on its own. <see cref="Use"/> is called once
    /// with the theme the app prefab carries, the same way the sound asset is
    /// handed over, so reading a colour stays a field read rather than a
    /// <c>Resources.Load</c> per repaint. With nothing handed over the built-in
    /// scheme answers, which is the scheme the overlay has always shipped.
    /// </summary>
    public static class DOTORIONPalette
    {
        private static DOTORIONTheme _active;
        private static DOTORIONTheme _builtIn;

        /// <summary>
        /// Raised after <see cref="Use"/> swaps the theme.
        ///
        /// Anything holding a colour it copied earlier is now holding the old
        /// theme's colour and has to repaint. Colours read straight off this
        /// class are fine; cached ones are not. Cache the slot, not the colour.
        /// </summary>
        public static event Action ThemeChanged;

        /// <summary>The theme every colour below is read from.</summary>
        public static DOTORIONTheme Active => _active != null ? _active : BuiltIn;

        /// <summary>
        /// The scheme compiled into the build, used when no asset is assigned.
        /// It is an ordinary theme instance rather than a second set of
        /// constants, so there is one definition of the dark scheme and it lives
        /// on <see cref="DOTORIONTheme"/> as the field defaults.
        /// </summary>
        public static DOTORIONTheme BuiltIn
        {
            get
            {
                if (_builtIn == null)
                {
                    _builtIn = ScriptableObject.CreateInstance<DOTORIONTheme>();
                    _builtIn.name = "Built-in";
                    // Never written to disk and never carried into a scene: it
                    // exists only so Active always has something to answer with.
                    _builtIn.hideFlags = HideFlags.HideAndDontSave;
                }

                return _builtIn;
            }
        }

        /// <summary>
        /// Switches the scheme. Passing null goes back to <see cref="BuiltIn"/>,
        /// so an app prefab with an empty theme slot still draws.
        /// </summary>
        public static void Use(DOTORIONTheme theme)
        {
            if (_active == theme) return;
            _active = theme;
            ThemeChanged?.Invoke();
        }

        public static Color Window => Active.Window;
        public static Color TopBar => Active.TopBar;
        public static Color Card => Active.Card;
        public static Color CardOffline => Active.CardOffline;
        public static Color ControlBar => Active.ControlBar;
        public static Color TextPrimary => Active.TextPrimary;
        public static Color TextSecondary => Active.TextSecondary;
        public static Color Working => Active.Working;
        public static Color Break => Active.Break;
        public static Color Meal => Active.Meal;
        public static Color Offline => Active.Offline;
        public static Color Accent => Active.Accent;
        public static Color Danger => Active.Danger;
        public static Color Button => Active.Button;
        public static Color ButtonHover => Active.ButtonHover;
    }
}

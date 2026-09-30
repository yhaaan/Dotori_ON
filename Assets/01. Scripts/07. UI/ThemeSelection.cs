using System.Collections.Generic;

namespace DOTORION.UI
{
    /// <summary>
    /// Which of the shipped themes is in use, and which one each round button in
    /// the settings panel stands for.
    ///
    /// The choice is stored by asset name rather than by position, so adding or
    /// reordering themes later does not silently turn someone's pink overlay
    /// back into the default one. A name that no longer matches anything falls
    /// back to the prefab's own theme instead of drawing nothing.
    /// </summary>
    public static class ThemeSelection
    {
        public static DOTORIONTheme Resolve(
            IReadOnlyList<DOTORIONTheme> themes,
            DOTORIONTheme fallback,
            string savedName)
        {
            if (themes != null && !string.IsNullOrEmpty(savedName))
            {
                foreach (var theme in themes)
                {
                    if (theme != null && theme.name == savedName)
                    {
                        return theme;
                    }
                }
            }

            return fallback;
        }

        /// <summary>
        /// The theme behind the round button at <paramref name="index"/>, or
        /// null when the button has no theme (an empty slot, or a prefab with
        /// more buttons than <c>_themes</c> has entries).
        /// </summary>
        public static DOTORIONTheme At(IReadOnlyList<DOTORIONTheme> themes, int index)
        {
            return themes != null && index >= 0 && index < themes.Count ? themes[index] : null;
        }

        public static string LabelOf(DOTORIONTheme theme)
        {
            if (theme == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(theme.DisplayName) ? theme.name : theme.DisplayName;
        }
    }
}

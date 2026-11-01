using System;
using UnityEngine;

namespace DOTORION.UI
{
    /// <summary>
    /// Which picture sits behind the member cards, apart from the colour theme.
    /// Pictures other than the default are rewards: something else unlocks them
    /// (the Chiikawa event at R200) and the unlock stays on this install after
    /// that event is gone.
    /// </summary>
    public static class MainBackground
    {
        public const string Default = "default";
        public const string Chiikawa = "chiikawa";
        public static readonly string[] Ids = { Default, Chiikawa };

        private const string SelectedKey = "DOTORION.Background";
        private const string UnlockPrefix = "DOTORION.Background.Unlocked.";

        /// <summary>Raised when the choice or an unlock changes, so the window and the settings row repaint.</summary>
        public static event Action Changed;

        public static string Label(string id) => id == Chiikawa ? "치이카와" : "기본";

        public static bool IsUnlocked(string id) => id == Default || PlayerPrefs.GetInt(UnlockPrefix + id, 0) == 1;

        /// <summary>The picture in use. A choice that is no longer unlocked falls back to the default.</summary>
        public static string Selected
        {
            get
            {
                var id = PlayerPrefs.GetString(SelectedKey, Default);
                return Array.IndexOf(Ids, id) >= 0 && IsUnlocked(id) ? id : Default;
            }
        }

        /// <summary>The next unlocked picture after the one in use, wrapping round; the same one if nothing else is unlocked.</summary>
        public static string Next()
        {
            var start = Array.IndexOf(Ids, Selected);
            for (var step = 1; step <= Ids.Length; step++)
            {
                var id = Ids[(start + step) % Ids.Length];
                if (IsUnlocked(id)) return id;
            }

            return Selected;
        }

        public static void Select(string id)
        {
            if (Array.IndexOf(Ids, id) < 0 || !IsUnlocked(id) || id == Selected) return;
            PlayerPrefs.SetString(SelectedKey, id);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Returns true only the first time, so the caller can announce the reward once.</summary>
        public static bool Unlock(string id)
        {
            if (Array.IndexOf(Ids, id) < 0 || IsUnlocked(id)) return false;
            PlayerPrefs.SetInt(UnlockPrefix + id, 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes a reward back (developer reset). If it was in use the default comes back.</summary>
        public static void Lock(string id)
        {
            if (id == Default || !IsUnlocked(id)) return;
            PlayerPrefs.DeleteKey(UnlockPrefix + id);
            if (PlayerPrefs.GetString(SelectedKey, Default) == id) PlayerPrefs.DeleteKey(SelectedKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>The picture for an id, or null for the default (the theme's own window art shows through).</summary>
        public static Texture2D Texture(string id) =>
            id == Chiikawa ? Resources.Load<Texture2D>("DOTORION/Backgrounds/ChiikawaMain") : null;
    }
}

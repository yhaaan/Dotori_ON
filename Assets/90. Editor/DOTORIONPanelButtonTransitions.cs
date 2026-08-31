using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.Editor
{
    /// <summary>
    /// Switches the settings and avatar picker buttons from the default colour
    /// tint to sprite swapping.
    ///
    /// The tint multiplies over whatever the button is wearing, which darkens
    /// hand-drawn artwork on hover and again on press. Sprite swapping leaves
    /// the artwork alone: hover is given no sprite at all, so nothing happens,
    /// and press shows the pressed sheet frame.
    ///
    /// After this runs the Inspector owns the sprites - Button's own inspector
    /// shows Highlighted/Pressed/Selected/Disabled slots under Sprite Swap - so
    /// this is a one-off setup, not something to keep running.
    /// </summary>
    public static class DOTORIONPanelButtonTransitions
    {
        private const string SheetPath = "Assets/05. Sprites/UI_sheet.aseprite";
        private const string PressedSpriteName = "UI_sheet_36";

        /// <summary>The buttons the two panels own, by path inside the main view prefab.</summary>
        private static readonly string[] ButtonPaths =
        {
            "WindowBackground/SettingsPanel/AlwaysOnTopToggle",
            "WindowBackground/SettingsPanel/MuteToggle",
            "WindowBackground/SettingsPanel/AutoStartToggle",
            // Every avatar tile is cloned from this at runtime, so setting the
            // template is what sets all of them.
            "AvatarPickerPanel/Viewport/Content/OptionTemplate",
            "AvatarPickerPanel/Confirm"
        };

        [MenuItem("DOTORI ON/Set Panel Buttons To Sprite Swap")]
        public static void Apply()
        {
            var pressed = AssetDatabase.LoadAllAssetsAtPath(SheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(sprite => sprite.name == PressedSpriteName);
            if (pressed == null)
            {
                Debug.LogError("Could not find " + PressedSpriteName + " in " + SheetPath + ".");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.MainViewPath);
            try
            {
                var changed = 0;
                foreach (var path in ButtonPaths)
                {
                    var target = root.transform.Find(path);
                    if (target == null)
                    {
                        Debug.LogWarning("Not found, skipped: " + path);
                        continue;
                    }

                    var button = target.GetComponent<Button>();
                    if (button == null)
                    {
                        Debug.LogWarning("No Button component, skipped: " + path);
                        continue;
                    }

                    button.transition = Selectable.Transition.SpriteSwap;
                    var state = button.spriteState;
                    // Left empty on purpose: with no sprite the button keeps the
                    // one it already has, which is what "no hover effect" means.
                    state.highlightedSprite = null;
                    state.selectedSprite = null;
                    state.disabledSprite = null;
                    state.pressedSprite = pressed;
                    button.spriteState = state;
                    changed++;
                }

                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.MainViewPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                Debug.Log("Sprite swap set on " + changed + " of " + ButtonPaths.Length +
                          " panel buttons. Pressed sprite: " + PressedSpriteName + ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

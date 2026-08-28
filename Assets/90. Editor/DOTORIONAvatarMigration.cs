using DOTORION.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.Editor
{
    /// <summary>
    /// Adds the avatar picker to prefabs that already exist, instead of asking
    /// for a full rebuild. The generated prefabs are hand-edited after they are
    /// created - that is the documented workflow - so regenerating them to gain
    /// one feature would throw away every layout tweak made since.
    ///
    /// It is written to be safe to run twice: everything it adds is skipped when
    /// it is already there.
    /// </summary>
    public static class DOTORIONAvatarMigration
    {
        [MenuItem("DOTORI ON/Add Avatar Picker To Existing Prefabs")]
        public static void AddAvatarPicker()
        {
            var changed = MigrateCard() | MigrateMainView() | MigrateApp();
            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(changed
                ? "Avatar picker added to the DOTORI ON prefabs."
                : "The DOTORI ON prefabs already have the avatar picker. Nothing changed.");
        }

        private static bool MigrateCard()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.CardPath);
            try
            {
                var view = root.GetComponent<TeamMemberCardView>();
                var serialized = new SerializedObject(view);
                if (serialized.FindProperty("_avatarButton").objectReferenceValue != null &&
                    serialized.FindProperty("_avatarIcon").objectReferenceValue != null)
                {
                    return false;
                }

                var avatar = serialized.FindProperty("_avatarBackground").objectReferenceValue as Image;
                if (avatar == null)
                {
                    Debug.LogError("TeamMemberCard is missing its _avatarBackground reference.");
                    return false;
                }

                DOTORIONPrefabBuilder.AttachAvatarPicking(avatar, out var button, out var icon);
                serialized.FindProperty("_avatarButton").objectReferenceValue = button;
                serialized.FindProperty("_avatarIcon").objectReferenceValue = icon;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.CardPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool MigrateMainView()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.MainViewPath);
            try
            {
                var view = root.GetComponent<DOTORIONView>();
                var serialized = new SerializedObject(view);
                var panelProperty = serialized.FindProperty("_avatarPickerPanel");
                var backgroundProperty = serialized.FindProperty("_windowBackground");
                var changed = false;

                if (panelProperty.objectReferenceValue == null)
                {
                    var existing = root.transform.Find("AvatarPickerPanel");
                    var panel = existing != null
                        ? existing.GetComponent<AvatarPickerPanelView>()
                        : DOTORIONPrefabBuilder.BuildAvatarPickerPanel(
                            root.transform,
                            DOTORIONPrefabBuilder.PreviewFont());
                    panelProperty.objectReferenceValue = panel;
                    // The picker starts closed; the view opens it, and a prefab
                    // that shipped it open would cover the cards on launch.
                    panel.gameObject.SetActive(false);
                    changed = true;
                }

                if (backgroundProperty.objectReferenceValue == null)
                {
                    var background = root.transform.Find("WindowBackground") as RectTransform;
                    if (background == null)
                    {
                        Debug.LogError("DOTORIONCanvas is missing its WindowBackground child.");
                    }
                    else
                    {
                        backgroundProperty.objectReferenceValue = background;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    return false;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.MainViewPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool MigrateApp()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.AppPath);
            try
            {
                var app = root.GetComponent<DOTORIONApp>();
                var serialized = new SerializedObject(app);
                var catalogProperty = serialized.FindProperty("_avatarCatalog");
                if (catalogProperty.objectReferenceValue != null)
                {
                    return false;
                }

                catalogProperty.objectReferenceValue = DOTORIONPrefabBuilder.EnsureAvatarCatalogAsset();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.AppPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Writes the current icon geometry into the existing prefabs. Separate
        /// from <see cref="AddAvatarPicker"/> because it overwrites those specific
        /// rects: it is for taking a revised icon size, not for a prefab that is
        /// merely missing the feature.
        /// </summary>
        [MenuItem("DOTORI ON/Apply Avatar Icon Layout")]
        public static void ApplyIconLayout()
        {
            ApplyCardLayout();
            ApplyPickerLayout();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Profile icons now draw at " +
                      DOTORIONPrefabBuilder.AvatarIconSize + "px on the card and in the picker.");
        }

        private static void ApplyCardLayout()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.CardPath);
            try
            {
                var avatar = root.transform.Find("Avatar") as RectTransform;
                if (avatar == null)
                {
                    Debug.LogError("TeamMemberCard is missing its Avatar child.");
                    return;
                }

                var size = DOTORIONPrefabBuilder.AvatarIconSize;
                avatar.anchoredPosition = new Vector2(0f, -DOTORIONPrefabBuilder.CardAvatarTop);
                avatar.sizeDelta = new Vector2(size, size);

                var icon = avatar.Find("Icon") as RectTransform;
                if (icon != null)
                {
                    UiFactory.Stretch(icon);
                }

                SetLine(root, "Name", DOTORIONPrefabBuilder.CardNameTop, 18f);
                SetLine(root, "Status", DOTORIONPrefabBuilder.CardStatusTop, 18f);
                SetLine(root, "Detail", DOTORIONPrefabBuilder.CardDetailTop, 27f);
                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.CardPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetLine(GameObject cardRoot, string childName, float top, float height)
        {
            var text = cardRoot.transform.Find(childName)?.GetComponent<Text>();
            if (text == null)
            {
                Debug.LogWarning("TeamMemberCard is missing its " + childName + " child.");
                return;
            }

            DOTORIONPrefabBuilder.SetCardLine(text, top, height);
        }

        private static void ApplyPickerLayout()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.MainViewPath);
            try
            {
                var panel = root.transform.Find("AvatarPickerPanel") as RectTransform;
                if (panel == null)
                {
                    Debug.LogError("DOTORIONCanvas has no AvatarPickerPanel. " +
                                   "Run DOTORI ON/Add Avatar Picker To Existing Prefabs first.");
                    return;
                }

                panel.sizeDelta = new Vector2(
                    panel.sizeDelta.x,
                    DOTORIONPrefabBuilder.AvatarPickerPanelHeight);

                var grid = panel.Find("Viewport/Content")?.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    var cell = DOTORIONPrefabBuilder.AvatarCellSize;
                    grid.cellSize = new Vector2(cell, cell);
                    grid.spacing = new Vector2(
                        DOTORIONPrefabBuilder.AvatarCellSpacing,
                        DOTORIONPrefabBuilder.AvatarCellSpacing);
                    grid.constraintCount = DOTORIONPrefabBuilder.AvatarGridColumns;
                }

                var templateIcon = panel.Find("Viewport/Content/OptionTemplate/Icon") as RectTransform;
                if (templateIcon != null)
                {
                    var padding = DOTORIONPrefabBuilder.AvatarCellPadding;
                    UiFactory.Stretch(templateIcon, padding, padding, padding, padding);
                }

                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.MainViewPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Batch-mode entry point so a build machine can run the same step.</summary>
        public static void AddAvatarPickerFromCommandLine() => AddAvatarPicker();
    }
}

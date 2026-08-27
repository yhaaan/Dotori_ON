using TeamOverlay.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.Editor
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
    public static class TeamOverlayAvatarMigration
    {
        [MenuItem("Team Overlay/Add Avatar Picker To Existing Prefabs")]
        public static void AddAvatarPicker()
        {
            var changed = MigrateCard() | MigrateMainView() | MigrateApp();
            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(changed
                ? "Avatar picker added to the Team Overlay prefabs."
                : "The Team Overlay prefabs already have the avatar picker. Nothing changed.");
        }

        private static bool MigrateCard()
        {
            var root = PrefabUtility.LoadPrefabContents(TeamOverlayPrefabBuilder.CardPath);
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

                TeamOverlayPrefabBuilder.AttachAvatarPicking(avatar, out var button, out var icon);
                serialized.FindProperty("_avatarButton").objectReferenceValue = button;
                serialized.FindProperty("_avatarIcon").objectReferenceValue = icon;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TeamOverlayPrefabBuilder.CardPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool MigrateMainView()
        {
            var root = PrefabUtility.LoadPrefabContents(TeamOverlayPrefabBuilder.MainViewPath);
            try
            {
                var view = root.GetComponent<TeamOverlayView>();
                var serialized = new SerializedObject(view);
                var panelProperty = serialized.FindProperty("_avatarPickerPanel");
                var backgroundProperty = serialized.FindProperty("_windowBackground");
                var changed = false;

                if (panelProperty.objectReferenceValue == null)
                {
                    var existing = root.transform.Find("AvatarPickerPanel");
                    var panel = existing != null
                        ? existing.GetComponent<AvatarPickerPanelView>()
                        : TeamOverlayPrefabBuilder.BuildAvatarPickerPanel(
                            root.transform,
                            TeamOverlayPrefabBuilder.PreviewFont());
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
                        Debug.LogError("TeamOverlayCanvas is missing its WindowBackground child.");
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
                PrefabUtility.SaveAsPrefabAsset(root, TeamOverlayPrefabBuilder.MainViewPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool MigrateApp()
        {
            var root = PrefabUtility.LoadPrefabContents(TeamOverlayPrefabBuilder.AppPath);
            try
            {
                var app = root.GetComponent<TeamOverlayApp>();
                var serialized = new SerializedObject(app);
                var catalogProperty = serialized.FindProperty("_avatarCatalog");
                if (catalogProperty.objectReferenceValue != null)
                {
                    return false;
                }

                catalogProperty.objectReferenceValue = TeamOverlayPrefabBuilder.EnsureAvatarCatalogAsset();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TeamOverlayPrefabBuilder.AppPath);
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
        [MenuItem("Team Overlay/Apply Avatar Icon Layout")]
        public static void ApplyIconLayout()
        {
            ApplyCardLayout();
            ApplyPickerLayout();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Profile icons now draw at " +
                      TeamOverlayPrefabBuilder.AvatarIconSize + "px on the card and in the picker.");
        }

        private static void ApplyCardLayout()
        {
            var root = PrefabUtility.LoadPrefabContents(TeamOverlayPrefabBuilder.CardPath);
            try
            {
                var avatar = root.transform.Find("Avatar") as RectTransform;
                if (avatar == null)
                {
                    Debug.LogError("TeamMemberCard is missing its Avatar child.");
                    return;
                }

                var size = TeamOverlayPrefabBuilder.AvatarIconSize;
                avatar.anchoredPosition = new Vector2(0f, -TeamOverlayPrefabBuilder.CardAvatarTop);
                avatar.sizeDelta = new Vector2(size, size);

                var icon = avatar.Find("Icon") as RectTransform;
                if (icon != null)
                {
                    UiFactory.Stretch(icon);
                }

                SetLine(root, "Name", TeamOverlayPrefabBuilder.CardNameTop, 18f);
                SetLine(root, "Status", TeamOverlayPrefabBuilder.CardStatusTop, 18f);
                SetLine(root, "Detail", TeamOverlayPrefabBuilder.CardDetailTop, 27f);
                PrefabUtility.SaveAsPrefabAsset(root, TeamOverlayPrefabBuilder.CardPath);
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

            TeamOverlayPrefabBuilder.SetCardLine(text, top, height);
        }

        private static void ApplyPickerLayout()
        {
            var root = PrefabUtility.LoadPrefabContents(TeamOverlayPrefabBuilder.MainViewPath);
            try
            {
                var panel = root.transform.Find("AvatarPickerPanel") as RectTransform;
                if (panel == null)
                {
                    Debug.LogError("TeamOverlayCanvas has no AvatarPickerPanel. " +
                                   "Run Team Overlay/Add Avatar Picker To Existing Prefabs first.");
                    return;
                }

                panel.sizeDelta = new Vector2(
                    panel.sizeDelta.x,
                    TeamOverlayPrefabBuilder.AvatarPickerPanelHeight);

                var grid = panel.Find("Viewport/Content")?.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    var cell = TeamOverlayPrefabBuilder.AvatarCellSize;
                    grid.cellSize = new Vector2(cell, cell);
                    grid.spacing = new Vector2(
                        TeamOverlayPrefabBuilder.AvatarCellSpacing,
                        TeamOverlayPrefabBuilder.AvatarCellSpacing);
                    grid.constraintCount = TeamOverlayPrefabBuilder.AvatarGridColumns;
                }

                var templateIcon = panel.Find("Viewport/Content/OptionTemplate/Icon") as RectTransform;
                if (templateIcon != null)
                {
                    var padding = TeamOverlayPrefabBuilder.AvatarCellPadding;
                    UiFactory.Stretch(templateIcon, padding, padding, padding, padding);
                }

                PrefabUtility.SaveAsPrefabAsset(root, TeamOverlayPrefabBuilder.MainViewPath);
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

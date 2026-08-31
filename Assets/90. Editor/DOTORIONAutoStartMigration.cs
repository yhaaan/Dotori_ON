using DOTORION.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.Editor
{
    /// <summary>
    /// Adds the "start with Windows" row to the settings panel of a prefab that
    /// already exists, the way the avatar picker was added. Rebuilding the main
    /// view instead would be the shorter route and the wrong one: the prefab has
    /// been hand-edited since it was generated and carries artwork - sprites,
    /// nine-slice borders, tuned rects - that exists nowhere else.
    ///
    /// The row is cloned from the notification-sound row rather than built from
    /// the UI factory, so it inherits whatever styling that row has picked up
    /// and the three switches keep looking like each other.
    ///
    /// Safe to run twice: it does nothing once the row is wired up.
    /// </summary>
    public static class DOTORIONAutoStartMigration
    {
        [MenuItem("DOTORI ON/Add Auto Start Row To Existing Prefabs")]
        public static void AddAutoStartRow()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.MainViewPath);
            try
            {
                if (!Migrate(root))
                {
                    Debug.Log("The DOTORI ON settings panel already has the auto start row. Nothing changed.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.MainViewPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Auto start row added to the DOTORI ON settings panel.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool Migrate(GameObject root)
        {
            var view = root.GetComponent<DOTORIONView>();
            if (view == null)
            {
                Debug.LogError("The main view prefab has no DOTORIONView component.");
                return false;
            }

            var panelView = new SerializedObject(view)
                .FindProperty("_settingsPanel").objectReferenceValue as SettingsPanelView;
            if (panelView == null)
            {
                Debug.LogError("DOTORIONView is missing its _settingsPanel reference.");
                return false;
            }

            var panelData = new SerializedObject(panelView);
            if (panelData.FindProperty("_autoStartButton").objectReferenceValue != null &&
                panelData.FindProperty("_autoStartValue").objectReferenceValue != null)
            {
                return false;
            }

            var panel = panelView.transform;
            var label = panel.Find("MuteLabel");
            var hint = panel.Find("MuteHint");
            var toggle = panel.Find("MuteToggle");
            if (label == null || hint == null || toggle == null)
            {
                Debug.LogError(
                    "The settings panel has no MuteLabel/MuteHint/MuteToggle to copy the new row from.");
                return false;
            }

            // Everything below the new row moves down by one row, and the panel
            // grows by the same amount, so the version line keeps the bottom
            // margin it had.
            MoveDownOneRow(panel.Find("VersionLabel"));
            MoveDownOneRow(panel.Find("VersionValue"));

            var newLabel = CloneRowPart(label, "AutoStartLabel");
            var newHint = CloneRowPart(hint, "AutoStartHint");
            var newToggle = CloneRowPart(toggle, "AutoStartToggle");

            SetText(newLabel, DOTORIONPrefabBuilder.AutoStartRowLabel);
            SetText(newHint, DOTORIONPrefabBuilder.AutoStartRowHint);
            // The switch word is repainted from the registry the moment the panel
            // is shown; off is only what the prefab sits at until then.
            SetText(newToggle, "꺼짐");

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(
                panelRect.sizeDelta.x, DOTORIONPrefabBuilder.SettingsPanelHeight);

            var button = newToggle.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("The copied MuteToggle has no Button component.");
                return false;
            }

            panelData.FindProperty("_autoStartButton").objectReferenceValue = button;
            panelData.FindProperty("_autoStartValue").objectReferenceValue =
                newToggle.GetComponentInChildren<Text>();
            panelData.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// Clones one part of a row and drops it one row lower. The copy keeps
        /// its source's anchors and size, so only the vertical offset changes.
        /// </summary>
        private static Transform CloneRowPart(Transform source, string name)
        {
            var copy = Object.Instantiate(source.gameObject, source.parent);
            copy.name = name;
            copy.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
            MoveDownOneRow(copy.transform);
            return copy.transform;
        }

        /// <summary>
        /// Rows are anchored to the top of the panel with the offset written as a
        /// negative y (see UiFactory.AnchorTop), so moving down is subtracting.
        /// </summary>
        private static void MoveDownOneRow(Transform target)
        {
            if (target == null)
            {
                return;
            }

            var rect = target.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = new Vector2(
                rect.anchoredPosition.x,
                rect.anchoredPosition.y - DOTORIONPrefabBuilder.SettingsRowStep);
        }

        private static void SetText(Transform target, string value)
        {
            var text = target.GetComponent<Text>() ?? target.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}

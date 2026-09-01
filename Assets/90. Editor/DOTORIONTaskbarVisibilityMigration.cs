using DOTORION.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.Editor
{
    /// <summary>
    /// Adds the taskbar-visibility row without rebuilding the hand-dressed main
    /// prefab. The new controls inherit the auto-start row's current artwork.
    /// </summary>
    public static class DOTORIONTaskbarVisibilityMigration
    {
        [MenuItem("DOTORI ON/Add Taskbar Visibility Row To Existing Prefabs")]
        public static void AddTaskbarVisibilityRow()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.MainViewPath);
            try
            {
                if (!Migrate(root))
                {
                    Debug.Log(
                        "The DOTORI ON settings panel already has the taskbar visibility row. Nothing changed.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.MainViewPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Taskbar visibility row added to the DOTORI ON settings panel.");
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
            if (panelData.FindProperty("_hideFromTaskbarButton").objectReferenceValue != null &&
                panelData.FindProperty("_hideFromTaskbarValue").objectReferenceValue != null)
            {
                return false;
            }

            var panel = panelView.transform;
            var label = panel.Find("AutoStartLabel");
            var hint = panel.Find("AutoStartHint");
            var toggle = panel.Find("AutoStartToggle");
            if (label == null || hint == null || toggle == null)
            {
                Debug.LogError(
                    "The settings panel has no AutoStartLabel/AutoStartHint/AutoStartToggle to copy.");
                return false;
            }

            MoveDownOneRow(panel.Find("VersionLabel"));
            MoveDownOneRow(panel.Find("VersionValue"));

            var newLabel = CloneRowPart(label, "HideFromTaskbarLabel");
            var newHint = CloneRowPart(hint, "HideFromTaskbarHint");
            var newToggle = CloneRowPart(toggle, "HideFromTaskbarToggle");

            SetText(newLabel, DOTORIONPrefabBuilder.HideFromTaskbarRowLabel);
            SetText(newHint, DOTORIONPrefabBuilder.HideFromTaskbarRowHint);
            SetText(newToggle, "꺼짐");

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(
                panelRect.sizeDelta.x,
                DOTORIONPrefabBuilder.SettingsPanelHeight);

            var button = newToggle.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("The copied AutoStartToggle has no Button component.");
                return false;
            }

            panelData.FindProperty("_hideFromTaskbarButton").objectReferenceValue = button;
            panelData.FindProperty("_hideFromTaskbarValue").objectReferenceValue =
                newToggle.GetComponentInChildren<Text>();
            panelData.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static Transform CloneRowPart(Transform source, string name)
        {
            var copy = Object.Instantiate(source.gameObject, source.parent);
            copy.name = name;
            copy.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
            MoveDownOneRow(copy.transform);
            return copy.transform;
        }

        private static void MoveDownOneRow(Transform target)
        {
            if (target == null)
            {
                return;
            }

            var rect = target.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(
                    rect.anchoredPosition.x,
                    rect.anchoredPosition.y - DOTORIONPrefabBuilder.SettingsRowStep);
            }
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

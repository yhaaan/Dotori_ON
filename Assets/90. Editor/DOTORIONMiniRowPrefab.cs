using System.Globalization;
using DOTORION.UI;
using UnityEditor;
using UnityEngine;

namespace DOTORION.Editor
{
    /// <summary>
    /// Pulls the mini overlay's first row out into its own prefab and makes the
    /// other three instances of it, so a change to one line reaches all four.
    ///
    /// The rows were built inline and were four copies of the same thing, which
    /// meant every art change had to be made four times and the fourth one was
    /// the one that got forgotten. Only the position stays per instance - that
    /// is the one thing the rows are supposed to disagree about.
    ///
    /// Run once. It is written to be safe to run again: a canvas whose rows are
    /// already instances is left alone.
    /// </summary>
    public static class DOTORIONMiniRowPrefab
    {
        public const string MiniRowPath = "Assets/02. Prefabs/MiniMemberRow.prefab";

        private const string PanelName = "MiniOverlayPanel";
        private const int RowCount = 4;

        [MenuItem("DOTORI ON/Extract Mini Row Prefab")]
        public static void Extract()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.MainViewPath);
            try
            {
                var panel = root.transform.Find(PanelName);
                if (panel == null)
                {
                    Debug.LogError("The main view prefab has no " + PanelName + ".");
                    return;
                }

                var rows = new Transform[RowCount];
                for (var index = 0; index < RowCount; index++)
                {
                    rows[index] = panel.Find(RowName(index));
                    if (rows[index] == null)
                    {
                        Debug.LogError("The mini overlay is missing " + RowName(index) + ".");
                        return;
                    }
                }

                if (PrefabUtility.GetCorrespondingObjectFromSource(rows[0].gameObject) != null)
                {
                    Debug.Log("The mini rows are already prefab instances. Nothing to do.");
                    return;
                }

                // The first row is the one that becomes the asset, so it is also
                // the one whose hand-tuned look everything else inherits.
                var asset = PrefabUtility.SaveAsPrefabAsset(rows[0].gameObject, MiniRowPath);
                if (asset == null)
                {
                    Debug.LogError("Could not write " + MiniRowPath + ".");
                    return;
                }

                var placed = new MiniMemberRowView[RowCount];
                for (var index = 0; index < RowCount; index++)
                {
                    var old = (RectTransform)rows[index];
                    var position = old.anchoredPosition;
                    var siblingIndex = old.GetSiblingIndex();
                    UnityEngine.Object.DestroyImmediate(old.gameObject);

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, panel);
                    instance.name = RowName(index);
                    var rect = (RectTransform)instance.transform;
                    rect.SetSiblingIndex(siblingIndex);
                    // Everything else comes from the asset. The position is the
                    // only override, which is what makes editing one row enough.
                    rect.anchoredPosition = position;
                    placed[index] = instance.GetComponent<MiniMemberRowView>();
                }

                var panelView = panel.GetComponent<MiniOverlayPanelView>();
                if (panelView == null)
                {
                    Debug.LogError("The mini overlay panel has no MiniOverlayPanelView.");
                    return;
                }

                var serialized = new SerializedObject(panelView);
                var rowsProperty = serialized.FindProperty("_rows");
                rowsProperty.arraySize = RowCount;
                for (var index = 0; index < RowCount; index++)
                {
                    rowsProperty.GetArrayElementAtIndex(index).objectReferenceValue = placed[index];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.MainViewPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(MiniRowPath);
                Debug.Log("Extracted " + MiniRowPath + " and rebound the four mini rows to it.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string RowName(int index) =>
            "MiniRow_" + (index + 1).ToString(CultureInfo.InvariantCulture);
    }
}

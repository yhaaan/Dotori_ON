using DOTORION.UI;
using UnityEditor;
using UnityEngine;

namespace DOTORION.Editor
{
    /// <summary>
    /// Points DOTORIONView at the background that shows before the person clocks
    /// in. The image itself is hand-placed artwork in the prefab - this only
    /// fills in the reference, so nothing anyone drew is touched.
    ///
    /// Safe to run twice: it does nothing once the field is set.
    /// </summary>
    public static class DOTORIONOfflineBackgroundMigration
    {
        private const string OfflineBackgroundPath = "WindowBackground/IMG_Background_offline";

        [MenuItem("DOTORI ON/Wire Offline Background")]
        public static void WireOfflineBackground()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.MainViewPath);
            try
            {
                var view = root.GetComponent<DOTORIONView>();
                if (view == null)
                {
                    Debug.LogError("The main view prefab has no DOTORIONView component.");
                    return;
                }

                var target = root.transform.Find(OfflineBackgroundPath);
                if (target == null)
                {
                    Debug.LogError("Could not find " + OfflineBackgroundPath + " in the main view prefab.");
                    return;
                }

                var data = new SerializedObject(view);
                var field = data.FindProperty("_offlineBackground");
                if (field == null)
                {
                    Debug.LogError("DOTORIONView has no _offlineBackground field. Let the scripts compile first.");
                    return;
                }

                if (field.objectReferenceValue == target.gameObject)
                {
                    Debug.Log("The offline background is already wired. Nothing changed.");
                    return;
                }

                field.objectReferenceValue = target.gameObject;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.MainViewPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Offline background wired to " + OfflineBackgroundPath + ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

using DOTORION.UI;
using UnityEditor;
using UnityEngine;

namespace DOTORION.Editor
{
    /// <summary>Removes development-only controls if an older builder or prefab reintroduces them.</summary>
    public sealed class DOTORIONProductionUiSanitizer : AssetPostprocessor
    {
        private static bool _scheduled;

        [MenuItem("DOTORI ON/Remove Development Controls From UI")]
        public static void SanitizeMainPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.MainViewPath);
            try
            {
                var fake = root.transform.Find("WindowBackground/TopBar/FakeCheckIn");
                if (fake == null) return;

                var view = root.GetComponent<DOTORIONView>();
                var serialized = new SerializedObject(view);
                var legacyReference = serialized.FindProperty("_fakeEventButton");
                if (legacyReference != null)
                {
                    legacyReference.objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                Object.DestroyImmediate(fake.gameObject);
                var drag = root.transform.Find("WindowBackground/TopBar/WindowDragArea") as RectTransform;
                if (drag != null) drag.offsetMax = new Vector2(-215f, drag.offsetMax.y);
                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.MainViewPath);
                Debug.Log("Removed development-only fake check-in from the production DOTORIONCanvas prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (_scheduled) return;
            foreach (var path in importedAssets)
            {
                if (path != DOTORIONPrefabBuilder.MainViewPath) continue;
                _scheduled = true;
                EditorApplication.delayCall += () =>
                {
                    _scheduled = false;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DOTORIONPrefabBuilder.MainViewPath);
                    if (prefab != null && prefab.transform.Find("WindowBackground/TopBar/FakeCheckIn") != null)
                        SanitizeMainPrefab();
                };
                break;
            }
        }
    }
}

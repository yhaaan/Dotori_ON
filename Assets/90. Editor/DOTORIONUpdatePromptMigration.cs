using DOTORION.UI;
using UnityEditor;
using UnityEngine;

namespace DOTORION.Editor
{
    /// <summary>
    /// Creates the update modal and hands it to the app prefab that already
    /// exists.
    ///
    /// The modal itself is built from scratch rather than migrated, because it
    /// is new - there is no hand-tuned artwork in it to lose. Only the app
    /// prefab is touched, and only by one field.
    ///
    /// Safe to run twice: it does nothing once the field points at the modal.
    /// </summary>
    public static class DOTORIONUpdatePromptMigration
    {
        [MenuItem("DOTORI ON/Add Update Prompt To Existing Prefabs")]
        public static void AddUpdatePrompt()
        {
            var prompt = AssetDatabase.LoadAssetAtPath<UpdatePromptView>(DOTORIONPrefabBuilder.UpdatePromptPath);
            var built = false;
            if (prompt == null)
            {
                prompt = DOTORIONPrefabBuilder.BuildUpdatePrompt();
                built = true;
            }

            if (prompt == null)
            {
                Debug.LogError("Could not create the update prompt prefab.");
                return;
            }

            var wired = WireIntoApp(prompt);
            if (built || wired)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(built || wired
                ? "Update prompt ready: " + (built ? "modal built" : "modal already existed") +
                  ", app prefab " + (wired ? "wired" : "already wired") + "."
                : "The DOTORI ON prefabs already have the update prompt. Nothing changed.");
        }

        private static bool WireIntoApp(UpdatePromptView prompt)
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.AppPath);
            try
            {
                var app = root.GetComponent<DOTORIONApp>();
                if (app == null)
                {
                    Debug.LogError("The app prefab has no DOTORIONApp component.");
                    return false;
                }

                var data = new SerializedObject(app);
                var field = data.FindProperty("_updatePromptPrefab");
                if (field == null)
                {
                    Debug.LogError("DOTORIONApp has no _updatePromptPrefab field. Let the scripts compile first.");
                    return false;
                }

                if (field.objectReferenceValue == prompt)
                {
                    return false;
                }

                field.objectReferenceValue = prompt;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.AppPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

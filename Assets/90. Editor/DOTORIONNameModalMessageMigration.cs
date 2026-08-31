using DOTORION.UI;
using UnityEditor;
using UnityEngine;

namespace DOTORION.Editor
{
    /// <summary>
    /// Points the name modal's message line at the title.
    ///
    /// The modal used to say two things at once - a title that invited, and a
    /// line under the field that hinted and then reported errors. There is one
    /// line now, so the title carries both and the hint can be deleted from the
    /// prefab afterwards.
    ///
    /// Safe to run twice: it does nothing once the field points at the title.
    /// </summary>
    public static class DOTORIONNameModalMessageMigration
    {
        private const string TitlePath = "ModalBackdrop/NamePanel/Title";

        [MenuItem("DOTORI ON/Point Name Modal Message At Title")]
        public static void PointMessageAtTitle()
        {
            var root = PrefabUtility.LoadPrefabContents(DOTORIONPrefabBuilder.NameViewPath);
            try
            {
                var view = root.GetComponent<FirstRunNameView>();
                if (view == null)
                {
                    Debug.LogError("The name modal prefab has no FirstRunNameView component.");
                    return;
                }

                var title = root.transform.Find(TitlePath);
                if (title == null)
                {
                    Debug.LogError("Could not find " + TitlePath + " in the name modal prefab.");
                    return;
                }

                var text = title.GetComponent<UnityEngine.UI.Text>();
                if (text == null)
                {
                    Debug.LogError(TitlePath + " has no Text component.");
                    return;
                }

                var data = new SerializedObject(view);
                var field = data.FindProperty("_messageText");
                if (field == null)
                {
                    Debug.LogError("FirstRunNameView has no _messageText field. Let the scripts compile first.");
                    return;
                }

                if (field.objectReferenceValue == text)
                {
                    Debug.Log("The name modal message already points at the title. Nothing changed.");
                    return;
                }

                field.objectReferenceValue = text;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, DOTORIONPrefabBuilder.NameViewPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Name modal message now points at " + TitlePath +
                          ". The old hint line under the field can be deleted.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

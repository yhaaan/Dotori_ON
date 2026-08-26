using NUnit.Framework;
using TeamOverlay.UI;
using UnityEditor;
using UnityEngine;

namespace TeamOverlay.Tests.EditMode
{
    public sealed class PrefabAssetTests
    {
        [Test]
        public void EditableUiPrefabs_HaveCompleteSerializedReferences()
        {
            var card = AssetDatabase.LoadAssetAtPath<TeamMemberCardView>(
                "Assets/02. Prefabs/TeamMemberCard.prefab");
            var main = AssetDatabase.LoadAssetAtPath<TeamOverlayView>(
                "Assets/02. Prefabs/TeamOverlayCanvas.prefab");
            var name = AssetDatabase.LoadAssetAtPath<FirstRunNameView>(
                "Assets/02. Prefabs/FirstRunNameModal.prefab");
            var app = AssetDatabase.LoadAssetAtPath<TeamOverlayApp>(
                "Assets/Resources/TeamOverlay/TeamOverlayApp.prefab");

            Assert.That(card, Is.Not.Null);
            Assert.That(main, Is.Not.Null);
            Assert.That(name, Is.Not.Null);
            Assert.That(app, Is.Not.Null);

            var mainData = new SerializedObject(main);
            var cards = mainData.FindProperty("_cards");
            Assert.That(cards.arraySize, Is.EqualTo(4));
            for (var index = 0; index < cards.arraySize; index++)
                Assert.That(cards.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null);
            AssertReference(mainData, "_checkInButton");
            AssertReference(mainData, "_exitButton");
            AssertReference(mainData, "_switchAccountButton");
            AssertReference(mainData, "_statusNoteInput");
            AssertReference(mainData, "_windowDragHandle");

            var nameData = new SerializedObject(name);
            AssertReference(nameData, "_nameInput");
            AssertReference(nameData, "_confirmButton");
            AssertReference(nameData, "_feedbackText");

            var appData = new SerializedObject(app);
            Assert.That(appData.FindProperty("_mainViewPrefab").objectReferenceValue, Is.EqualTo(main));
            Assert.That(appData.FindProperty("_firstRunNamePrefab").objectReferenceValue, Is.EqualTo(name));
        }

        private static void AssertReference(SerializedObject data, string propertyName)
        {
            var property = data.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
        }
    }
}

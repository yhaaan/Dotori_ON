using System.Collections.Generic;
using NUnit.Framework;
using DOTORION.UI;
using UnityEditor;
using UnityEngine;

namespace DOTORION.Tests.EditMode
{
    public sealed class TeamAvatarCatalogTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
            {
                Object.DestroyImmediate(asset);
            }

            _created.Clear();
        }

        [Test]
        public void ValidKeys_MatchWhatTheDatabaseWouldAccept()
        {
            Assert.That(TeamAvatarCatalog.IsValidKey("smile"), Is.True);
            Assert.That(TeamAvatarCatalog.IsValidKey("cat_02.v1-b"), Is.True);
            Assert.That(TeamAvatarCatalog.IsValidKey(new string('a', 64)), Is.True);

            // The server's members_avatar_key_check rejects all of these, so the
            // picker has to as well or picking one is a request that always fails.
            Assert.That(TeamAvatarCatalog.IsValidKey(new string('a', 65)), Is.False);
            Assert.That(TeamAvatarCatalog.IsValidKey("웃는얼굴"), Is.False);
            Assert.That(TeamAvatarCatalog.IsValidKey("smile face"), Is.False);
            Assert.That(TeamAvatarCatalog.IsValidKey("icons/smile"), Is.False);
            Assert.That(TeamAvatarCatalog.IsValidKey(""), Is.False);
            Assert.That(TeamAvatarCatalog.IsValidKey(null), Is.False);
        }

        [Test]
        public void Options_AreTheSpritesInListOrderAndKeyedByFileName()
        {
            var catalog = CreateCatalog("smile", "sleepy");

            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog.Options[0].Key, Is.EqualTo("smile"));
            Assert.That(catalog.Options[1].Key, Is.EqualTo("sleepy"));
            Assert.That(catalog.Find("smile"), Is.SameAs(catalog.Options[0].Sprite));
        }

        [Test]
        public void UnusableEntries_AreSkippedAndReported()
        {
            // A Korean file name, a duplicate and the reserved key: each one would
            // otherwise become a cell that cannot be saved.
            var catalog = CreateCatalog("smile", "웃음", "smile", TeamAvatarCatalog.DefaultKey);

            Assert.That(catalog.Count, Is.EqualTo(1));
            Assert.That(catalog.Options[0].Key, Is.EqualTo("smile"));
            Assert.That(catalog.Problems().Count, Is.EqualTo(3));
        }

        [Test]
        public void DefaultAndUnknownKeys_HaveNoArtwork()
        {
            var catalog = CreateCatalog("smile");

            Assert.That(catalog.Find(TeamAvatarCatalog.DefaultKey), Is.Null);
            Assert.That(catalog.Find("removed-in-a-later-build"), Is.Null);
            Assert.That(catalog.Find(null), Is.Null);
        }

        [Test]
        public void ShippedCatalogAsset_HasNothingItHadToSkip()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TeamAvatarCatalog>(
                "Assets/Resources/DOTORION/TeamAvatarCatalog.asset");
            if (catalog == null)
            {
                Assert.Ignore("The avatar catalog asset has not been created yet.");
            }

            // Catches a badly named icon at build time instead of leaving a hole
            // in the picker that nobody notices until someone tries to pick it.
            Assert.That(catalog.Problems(), Is.Empty, string.Join(" / ", catalog.Problems()));
        }

        private TeamAvatarCatalog CreateCatalog(params string[] spriteNames)
        {
            var catalog = ScriptableObject.CreateInstance<TeamAvatarCatalog>();
            _created.Add(catalog);

            var sprites = new Sprite[spriteNames.Length];
            for (var index = 0; index < spriteNames.Length; index++)
            {
                sprites[index] = CreateSprite(spriteNames[index]);
            }

            var serialized = new SerializedObject(catalog);
            var icons = serialized.FindProperty("_icons");
            icons.arraySize = sprites.Length;
            for (var index = 0; index < sprites.Length; index++)
            {
                icons.GetArrayElementAtIndex(index).objectReferenceValue = sprites[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            catalog.Refresh();
            return catalog;
        }

        private Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(4, 4);
            _created.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            _created.Add(sprite);
            return sprite;
        }
    }
}

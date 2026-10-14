using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DOTORION.UI
{
    /// <summary>
    /// The photos a member has put into the avatar picker's cells, kept on this
    /// PC only.
    ///
    /// Every cell of the picker is a slot: the catalog icons are
    /// <c>icon.&lt;key&gt;</c> and the empty row under them is <c>custom.0</c> to
    /// <c>custom.7</c>. A slot with a photo shows the photo instead of its icon.
    /// None of this reaches the server - the server only ever holds the photo
    /// being worn - so the set is private to the person and costs nothing to
    /// keep. It lives under <c>persistentDataPath/AvatarSlots/&lt;member&gt;</c>,
    /// which app updates leave alone.
    ///
    /// Which slot is being worn is remembered here too, as the slot and the
    /// server version it was uploaded as. The roster only says <c>photo.12</c>;
    /// this is how the picker knows that photo.12 is the third custom cell.
    /// </summary>
    public sealed class AvatarSlotStore : IDisposable
    {
        public const int CustomSlotCount = 8;
        private const string IconPrefix = "icon.";
        private const string CustomPrefix = "custom.";
        private const string WornFile = "worn.json";

        private readonly string _directory;
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly HashSet<string> _missing = new HashSet<string>(StringComparer.Ordinal);
        private WornRecord _worn;

        [Serializable]
        private sealed class WornRecord
        {
            public string slot;
            public int version;
        }

        public AvatarSlotStore(string memberId)
        {
            _directory = Path.Combine(Application.persistentDataPath, "AvatarSlots", SafeName(memberId ?? "local"));
            _worn = ReadWorn();
        }

        public static string IconSlot(string catalogKey) => IconPrefix + catalogKey;

        public static string CustomSlot(int index) => CustomPrefix + index;

        /// <summary>The catalog key behind an icon slot, or null for a custom one.</summary>
        public static string CatalogKeyOf(string slotId)
        {
            return slotId != null && slotId.StartsWith(IconPrefix, StringComparison.Ordinal)
                ? slotId.Substring(IconPrefix.Length)
                : null;
        }

        /// <summary>The slot worn as <c>photo.&lt;version&gt;</c>, if this PC uploaded it.</summary>
        public string WornSlotFor(int version) => _worn != null && _worn.version == version ? _worn.slot : null;

        public bool Has(string slotId) => Find(slotId) != null;

        /// <summary>The slot's photo, or null when the cell shows its default.</summary>
        public Sprite Find(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
            {
                return null;
            }

            if (_sprites.TryGetValue(slotId, out var sprite))
            {
                return sprite;
            }

            if (_missing.Contains(slotId))
            {
                return null;
            }

            var png = ReadPng(slotId);
            sprite = png != null ? CreateSprite(png, slotId) : null;
            if (sprite == null)
            {
                _missing.Add(slotId);
                return null;
            }

            _sprites[slotId] = sprite;
            return sprite;
        }

        public byte[] ReadPng(string slotId)
        {
            try
            {
                var path = PathFor(slotId);
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read the avatar slot " + slotId + ": " + exception.Message);
                return null;
            }
        }

        public void Save(string slotId, byte[] png)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllBytes(PathFor(slotId), png);
            Forget(slotId);
        }

        /// <summary>Puts the cell back to its default: the catalog icon, or an empty "+".</summary>
        public void Delete(string slotId)
        {
            try
            {
                var path = PathFor(slotId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not clear the avatar slot " + slotId + ": " + exception.Message);
            }

            Forget(slotId);
            if (_worn != null && _worn.slot == slotId)
            {
                SetWorn(null, 0);
            }
        }

        /// <summary>Records that <paramref name="slotId"/> was uploaded as <c>photo.&lt;version&gt;</c>.</summary>
        public void SetWorn(string slotId, int version)
        {
            _worn = slotId == null ? null : new WornRecord { slot = slotId, version = version };
            try
            {
                Directory.CreateDirectory(_directory);
                var path = Path.Combine(_directory, WornFile);
                if (_worn == null)
                {
                    File.Delete(path);
                }
                else
                {
                    File.WriteAllText(path, JsonUtility.ToJson(_worn));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not remember the worn avatar slot: " + exception.Message);
            }
        }

        public void Dispose()
        {
            foreach (var sprite in _sprites.Values)
            {
                DestroySprite(sprite);
            }

            _sprites.Clear();
        }

        private WornRecord ReadWorn()
        {
            try
            {
                var path = Path.Combine(_directory, WornFile);
                return File.Exists(path) ? JsonUtility.FromJson<WornRecord>(File.ReadAllText(path)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Forget(string slotId)
        {
            _missing.Remove(slotId);
            if (_sprites.TryGetValue(slotId, out var sprite))
            {
                DestroySprite(sprite);
                _sprites.Remove(slotId);
            }
        }

        private string PathFor(string slotId) => Path.Combine(_directory, SafeName(slotId) + ".png");

        private static Sprite CreateSprite(byte[] png, string slotId)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(png))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "Slot_" + slotId;
            return sprite;
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
        }

        private static string SafeName(string value)
        {
            var characters = value.ToCharArray();
            for (var index = 0; index < characters.Length; index++)
            {
                var character = characters[index];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '.' && character != '_')
                {
                    characters[index] = '_';
                }
            }

            return new string(characters);
        }
    }
}

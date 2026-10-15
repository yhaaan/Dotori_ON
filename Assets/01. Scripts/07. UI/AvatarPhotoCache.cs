using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DOTORION.Core;
using UnityEngine;

namespace DOTORION.UI
{
    /// <summary>
    /// Members' profile photos, kept so each version crosses the network once.
    ///
    /// The roster names a photo by version (<c>photo.3</c>). The first time a
    /// version is asked for, it is read from disk if an earlier run saved it,
    /// and otherwise fetched in the background; the card shows the name initial
    /// until then and picks the photo up on its next repaint. Disk copies live
    /// under <c>persistentDataPath/AvatarPhotos</c>, one file per member, so a
    /// restart costs no download either.
    /// </summary>
    public sealed class AvatarPhotoCache : IDisposable
    {
        /// <summary>How long to leave a failed fetch alone before trying again.</summary>
        private const float RetryDelaySeconds = 60f;

        private readonly ITeamAvatarPhotos _source;
        private readonly string _directory;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly HashSet<string> _fetching = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _retryAt = new Dictionary<string, float>(StringComparer.Ordinal);

        // Cards repaint five times a second; a version that is not on disk is
        // remembered so that is not five file probes a second while it downloads.
        private readonly HashSet<string> _notOnDisk = new HashSet<string>(StringComparer.Ordinal);

        private sealed class Entry
        {
            public int Version;
            public Sprite Sprite;
        }

        public AvatarPhotoCache(ITeamAvatarPhotos source)
        {
            _source = source;
            _directory = Path.Combine(Application.persistentDataPath, "AvatarPhotos");
        }

        /// <summary>
        /// The photo a photo key names, or null while it is still on its way or
        /// when the key is a catalog icon. An older photo of the same member is
        /// returned in the meantime rather than flashing back to the initial.
        /// </summary>
        public Sprite Find(string memberId, string avatarKey)
        {
            if (string.IsNullOrEmpty(memberId) || !AvatarPhotoKey.TryParse(avatarKey, out var wanted))
            {
                return null;
            }

            _entries.TryGetValue(memberId, out var entry);
            if (entry != null && entry.Version >= wanted)
            {
                return entry.Sprite;
            }

            if (TryLoadFromDisk(memberId, wanted))
            {
                return _entries[memberId].Sprite;
            }

            Fetch(memberId);
            return entry?.Sprite;
        }

        /// <summary>
        /// Takes a photo the local member just uploaded, so their own card shows it
        /// without downloading what this machine already has.
        /// </summary>
        public void Put(string memberId, int version, byte[] png)
        {
            if (!string.IsNullOrEmpty(memberId) && png != null && Store(memberId, version, png))
            {
                Save(memberId, version, png);
            }
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            foreach (var entry in _entries.Values)
            {
                Destroy(entry.Sprite);
            }

            _entries.Clear();
        }

        private async void Fetch(string memberId)
        {
            if (_source == null || _fetching.Contains(memberId) ||
                (_retryAt.TryGetValue(memberId, out var retryAt) && Time.realtimeSinceStartup < retryAt))
            {
                return;
            }

            _fetching.Add(memberId);
            try
            {
                var photo = await _source.GetAvatarPhotoAsync(memberId, _lifetime.Token);
                if (_lifetime.IsCancellationRequested)
                {
                    return;
                }

                if (photo == null || !Store(memberId, photo.Version, photo.Png))
                {
                    _retryAt[memberId] = Time.realtimeSinceStartup + RetryDelaySeconds;
                    return;
                }

                _retryAt.Remove(memberId);
                Save(memberId, photo.Version, photo.Png);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                // A missing photo is cosmetic - the card still has the initial -
                // so this is a warning, and the next try waits instead of
                // hammering a server that just failed.
                Debug.LogWarning("Could not fetch the profile photo of " + memberId + ": " + exception.Message);
                _retryAt[memberId] = Time.realtimeSinceStartup + RetryDelaySeconds;
            }
            finally
            {
                _fetching.Remove(memberId);
            }
        }

        private bool Store(string memberId, int version, byte[] png)
        {
            if (_entries.TryGetValue(memberId, out var existing) && existing.Version >= version)
            {
                return false;
            }

            // Mipmapped: the card shows a 96 pixel photo at 48 or 72 pixels below
            // 200 % UI scale, and without mips that shrink samples only some of
            // the pixels and looks grainy.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!texture.LoadImage(png))
            {
                UnityEngine.Object.Destroy(texture);
                return false;
            }

            texture.filterMode = FilterMode.Trilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = "Photo_" + memberId + "_" + version;
            if (existing != null)
            {
                Destroy(existing.Sprite);
            }

            _entries[memberId] = new Entry { Version = version, Sprite = sprite };
            return true;
        }

        private bool TryLoadFromDisk(string memberId, int version)
        {
            var path = PathFor(memberId, version);
            if (_notOnDisk.Contains(path))
            {
                return false;
            }

            try
            {
                if (File.Exists(path) && Store(memberId, version, File.ReadAllBytes(path)))
                {
                    return true;
                }

                _notOnDisk.Add(path);
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read the cached profile photo: " + exception.Message);
                return false;
            }
        }

        /// <summary>Writes the new version and drops that member's older ones.</summary>
        private void Save(string memberId, int version, byte[] png)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                var prefix = SafeName(memberId) + ".";
                foreach (var file in Directory.GetFiles(_directory, prefix + "*.png"))
                {
                    File.Delete(file);
                }

                File.WriteAllBytes(PathFor(memberId, version), png);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not cache the profile photo: " + exception.Message);
            }
        }

        private string PathFor(string memberId, int version)
        {
            return Path.Combine(_directory, SafeName(memberId) + "." + version + ".png");
        }

        /// <summary>Member ids are UUIDs, but a file name should not trust that.</summary>
        private static string SafeName(string memberId)
        {
            var characters = memberId.ToCharArray();
            for (var index = 0; index < characters.Length; index++)
            {
                var character = characters[index];
                if (!char.IsLetterOrDigit(character) && character != '-')
                {
                    characters[index] = '_';
                }
            }

            return new string(characters);
        }

        private static void Destroy(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
        }
    }
}

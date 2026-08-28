using System;
using System.Collections.Generic;
using UnityEngine;

namespace DOTORION.UI
{
    /// <summary>One pickable profile icon: the stored key and the artwork it draws.</summary>
    public readonly struct AvatarOption
    {
        public AvatarOption(string key, Sprite sprite)
        {
            Key = key;
            Sprite = sprite;
        }

        public string Key { get; }

        public Sprite Sprite { get; }
    }

    /// <summary>
    /// The one asset that decides which profile icons the team can pick from.
    /// Drop sprites into the list and the picker grows by that many cells; the
    /// grid and the window are sized for whatever is in here, so adding icons
    /// never needs a code change.
    ///
    /// The stored key is the sprite's asset name, not a path or a URL. That keeps
    /// the artwork on the client - the server only ever holds a short name - and
    /// makes a removed icon degrade into the name initial instead of a broken
    /// image. Renaming a sprite file therefore resets whoever had picked it.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TeamAvatarCatalog",
        menuName = "DOTORI ON/Avatar Catalog",
        order = 1)]
    public sealed class TeamAvatarCatalog : ScriptableObject
    {
        /// <summary>The stored key that means "no icon picked"; the card draws the name initial.</summary>
        public const string DefaultKey = "default";

        /// <summary>Mirrors the <c>members_avatar_key_check</c> constraint.</summary>
        public const int MaximumKeyLength = 64;

        [Header("프로필 아이콘 (파일 이름이 곧 저장되는 키)")]
        [Tooltip("여기에 스프라이트를 추가하면 선택 목록이 그만큼 늘어납니다. " +
                 "파일 이름은 영문/숫자/._- 만, 64자 이하로 지어 주세요.")]
        [SerializeField] private Sprite[] _icons = Array.Empty<Sprite>();

        private readonly List<AvatarOption> _options = new List<AvatarOption>();
        private Dictionary<string, Sprite> _byKey;

        /// <summary>Every usable icon, in list order. Empty and invalid entries are dropped.</summary>
        public IReadOnlyList<AvatarOption> Options
        {
            get
            {
                EnsureIndex();
                return _options;
            }
        }

        public int Count => Options.Count;

        /// <summary>The artwork for a stored key, or null when nothing matches it.</summary>
        public Sprite Find(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || string.Equals(key, DefaultKey, StringComparison.Ordinal))
            {
                return null;
            }

            EnsureIndex();
            return _byKey.TryGetValue(key.Trim(), out var sprite) ? sprite : null;
        }

        public bool Contains(string key) => Find(key) != null;

        /// <summary>The key a sprite is stored under.</summary>
        public static string KeyOf(Sprite sprite) => sprite == null ? null : sprite.name;

        /// <summary>
        /// Whether the server would accept this key. Checking it here turns a
        /// badly named file into a build-time test failure instead of a request
        /// the database rejects with <c>invalid_avatar_key</c>.
        /// </summary>
        public static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > MaximumKeyLength)
            {
                return false;
            }

            foreach (var character in key)
            {
                var allowed =
                    (character >= 'A' && character <= 'Z') ||
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') ||
                    character == '.' || character == '_' || character == '-';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reports what the picker had to skip. The editor test reads this so a
        /// file named in Korean, or two files with the same name, fails the build
        /// rather than quietly going missing from the grid.
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < _icons.Length; index++)
            {
                var sprite = _icons[index];
                if (sprite == null)
                {
                    problems.Add(index + "번 칸이 비어 있습니다.");
                    continue;
                }

                var key = KeyOf(sprite);
                if (key == null || key.Length > MaximumKeyLength || !IsValidKey(key))
                {
                    problems.Add("'" + key + "'는 저장할 수 없는 이름입니다. 영문/숫자/._- 만 " +
                                 MaximumKeyLength + "자까지 쓸 수 있습니다.");
                }
                else if (string.Equals(key, DefaultKey, StringComparison.Ordinal))
                {
                    problems.Add("'" + DefaultKey + "'는 '아이콘 없음'을 뜻하는 예약된 이름입니다.");
                }
                else if (!seen.Add(key))
                {
                    problems.Add("'" + key + "'가 두 번 들어 있습니다.");
                }
            }

            return problems;
        }

        /// <summary>Rebuilds the lookup after the list is edited in the Inspector.</summary>
        public void Refresh()
        {
            _byKey = null;
            EnsureIndex();
        }

        private void OnEnable() => _byKey = null;

        private void OnValidate() => _byKey = null;

        private void EnsureIndex()
        {
            if (_byKey != null)
            {
                return;
            }

            _byKey = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            _options.Clear();
            foreach (var sprite in _icons)
            {
                var key = KeyOf(sprite);
                if (key == null ||
                    !IsValidKey(key) ||
                    string.Equals(key, DefaultKey, StringComparison.Ordinal) ||
                    _byKey.ContainsKey(key))
                {
                    continue;
                }

                _byKey.Add(key, sprite);
                _options.Add(new AvatarOption(key, sprite));
            }
        }
    }
}

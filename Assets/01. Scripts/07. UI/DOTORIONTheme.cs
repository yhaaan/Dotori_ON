using UnityEngine;

namespace DOTORION.UI
{
    /// <summary>
    /// One colour scheme, as an asset.
    ///
    /// The overlay used to carry its colours as compiled-in constants, which
    /// made a second scheme a second build. Here the same named slots live in an
    /// asset instead, so a light theme is a copy of this asset with different
    /// values in it rather than a branch in the drawing code.
    ///
    /// The defaults below are the scheme the overlay has always shipped, and
    /// they are the only place those values are written: <see cref="DOTORIONPalette"/>
    /// falls back to a fresh instance of this class when no asset is assigned,
    /// so an unassigned reference looks exactly like the old build rather than
    /// like a bug.
    ///
    /// Slots are named for what they are for, not for what colour they are.
    /// <see cref="Card"/> stays the card's background in a light theme even
    /// though nothing about it is dark there.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DOTORIONTheme",
        menuName = "DOTORI ON/Theme",
        order = 1)]
    public sealed class DOTORIONTheme : ScriptableObject
    {
        [Tooltip("테마 버튼을 눌렀을 때 피드백에 쓰는 이름입니다. 비우면 에셋 이름을 씁니다.")]
        public string DisplayName;

        /// <summary>The colour of the round button that switches to this theme.</summary>
        public Color Swatch => Accent;

        public DOTORIONThemeCatalog Catalog;
        public System.Collections.Generic.List<ThemeOverride> Overrides = new System.Collections.Generic.List<ThemeOverride>();
        public ThemeOverride Find(string id) => Overrides.Find(value => value.Id == id);
        public System.Collections.Generic.List<ThemePaletteOverride> Palette = new System.Collections.Generic.List<ThemePaletteOverride>();
        public System.Collections.Generic.List<ThemeSpriteOverride> Sprites = new System.Collections.Generic.List<ThemeSpriteOverride>();

        public Color ResolveRoleColor(ThemeColorRole role, Color baseline)
        {
            var entry = role == ThemeColorRole.None ? null : Palette.Find(value => value.Role == role);
            if (entry == null) return baseline;
            // Alpha controls masking / disabled states independently of the theme hue.
            return new Color(entry.Color.r, entry.Color.g, entry.Color.b, baseline.a);
        }

        public Color ResolveColor(ThemeSlot slot)
        {
            var local = Find(slot.Id);
            if (local != null) return local.Mode == ThemeMode.Original ? slot.Color : local.Color;
            var image = slot.Sprite != null ? Sprites.Find(value => value.Original == slot.Sprite && value.Replacement != null) : null;
            if (image != null && !image.PreserveTint) return new Color(1, 1, 1, slot.Color.a);
            return ResolveRoleColor(ThemeRoleRules.RoleOf(slot), slot.Color);
        }

        public Sprite ResolveSprite(ThemeSlot slot)
        {
            var local = Find(slot.Id);
            if (local != null) return local.Mode == ThemeMode.SpriteSwap && local.Sprite != null ? local.Sprite : slot.Sprite;
            var image = slot.Sprite != null ? Sprites.Find(value => value.Original == slot.Sprite) : null;
            return image != null && image.Replacement != null ? image.Replacement : slot.Sprite;
        }
        [Header("배경")]
        [Tooltip("창 전체의 바탕색입니다.")]
        [SerializeField] private Color _window = Hex("101621");

        [Tooltip("창 위쪽 제목 줄입니다.")]
        [SerializeField] private Color _topBar = Hex("171F2D");

        [Tooltip("팀원 카드 한 장의 바탕색입니다.")]
        [SerializeField] private Color _card = Hex("1C2635");

        [Tooltip("퇴근한 팀원의 카드 바탕색. 켜져 있는 카드보다 가라앉습니다.")]
        [SerializeField] private Color _cardOffline = Hex("181F2A");

        [Tooltip("출근·퇴근 버튼이 놓인 아래쪽 줄입니다.")]
        [SerializeField] private Color _controlBar = Hex("121A26");

        [Header("글자")]
        [Tooltip("이름과 숫자처럼 읽으라고 있는 글자입니다.")]
        [SerializeField] private Color _textPrimary = Hex("F4F7FB");

        [Tooltip("설명과 단위처럼 곁들이는 글자입니다.")]
        [SerializeField] private Color _textSecondary = Hex("A9B5C6");

        [Header("상태")]
        [Tooltip("근무 중. 상태색은 뜻을 나르므로 테마가 바뀌어도 알아볼 수 있어야 합니다.")]
        [SerializeField] private Color _working = Hex("4FD1A1");

        [Tooltip("휴식 중입니다.")]
        [SerializeField] private Color _break = Hex("F4C95D");

        [Tooltip("식사 중입니다.")]
        [SerializeField] private Color _meal = Hex("FF8E72");

        [Tooltip("퇴근했거나 접속이 끊긴 상태입니다.")]
        [SerializeField] private Color _offline = Hex("667386");

        [Header("강조")]
        [Tooltip("오늘 날짜나 기본 버튼처럼 눈이 먼저 가야 하는 곳입니다.")]
        [SerializeField] private Color _accent = Hex("6EA8FE");

        [Tooltip("삭제처럼 되돌릴 수 없는 동작입니다.")]
        [SerializeField] private Color _danger = Hex("E97171");

        [Header("버튼")]
        [Tooltip("평상시 버튼 바탕색입니다.")]
        [SerializeField] private Color _button = Hex("273449");

        [Tooltip("마우스를 올렸을 때의 버튼 바탕색입니다.")]
        [SerializeField] private Color _buttonHover = Hex("34445D");

        public Color Window => ResolveRoleColor(ThemeColorRole.Window, _window);
        public Color TopBar => ResolveRoleColor(ThemeColorRole.TopBar, _topBar);
        public Color Card => ResolveRoleColor(ThemeColorRole.Card, _card);
        public Color CardOffline => ResolveRoleColor(ThemeColorRole.CardOffline, _cardOffline);
        public Color ControlBar => ResolveRoleColor(ThemeColorRole.ControlBar, _controlBar);
        public Color TextPrimary => ResolveRoleColor(ThemeColorRole.TextPrimary, _textPrimary);
        public Color TextSecondary => ResolveRoleColor(ThemeColorRole.TextSecondary, _textSecondary);
        public Color Working => ResolveRoleColor(ThemeColorRole.Working, _working);
        public Color Break => ResolveRoleColor(ThemeColorRole.Break, _break);
        public Color Meal => ResolveRoleColor(ThemeColorRole.Meal, _meal);
        public Color Offline => ResolveRoleColor(ThemeColorRole.Offline, _offline);
        public Color Accent => ResolveRoleColor(ThemeColorRole.Accent, _accent);
        public Color Danger => ResolveRoleColor(ThemeColorRole.Danger, _danger);
        public Color Button => ResolveRoleColor(ThemeColorRole.Button, _button);
        public Color ButtonHover => ResolveRoleColor(ThemeColorRole.ButtonHover, _buttonHover);

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString("#" + value, out var color) ? color : Color.magenta;
        }
    }
}

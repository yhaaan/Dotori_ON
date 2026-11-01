using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>
    /// The settings panel. It collects the switches that are about this install
    /// rather than about the team, which is why they left the top bar: one slot
    /// on a 480 wide row now opens all of them instead of each taking its own.
    /// </summary>
    public sealed class SettingsPanelView : MonoBehaviour
    {
        [Header("Prefab references")]
        [SerializeField] private Button _alwaysOnTopButton;
        [SerializeField] private Text _alwaysOnTopValue;
        [SerializeField] private Button _muteButton;
        [SerializeField] private Text _muteValue;
        [SerializeField] private Button _autoStartButton;
        [SerializeField] private Text _autoStartValue;
        [SerializeField] private Button _hideFromTaskbarButton;
        [SerializeField] private Text _hideFromTaskbarValue;
        [SerializeField] private Button _uiScaleButton;
        [SerializeField] private Text _uiScaleValue;
        [SerializeField] private Text _versionText;

        private sealed class BackgroundIcon
        {
            public string Id;
            public Button Button;
            public Image Ring;
            public Graphic Picture;
            public Image Band;
        }

        private readonly List<BackgroundIcon> _backgroundIcons = new List<BackgroundIcon>();
        private RectTransform _backgroundLabel;
        private DOTORIONTheme _activeTheme;
        private string _background = MainBackground.Default;

        [Header("Theme buttons")]
        [Tooltip("제목 옆의 둥근 버튼들. 왼쪽부터 DOTORIONApp의 _themes 순서입니다.")]
        [SerializeField] private Image[] _themeSwatches;

        [SerializeField] private Sprite _swatchSprite;

        [Tooltip("지금 쓰는 테마의 버튼에 입히는, 바깥에 고리가 있는 그림.")]
        [SerializeField] private Sprite _swatchSelectedSprite;

        private bool _initialized;

        public event Action AlwaysOnTopToggleRequested;

        public event Action MuteToggleRequested;

        public event Action AutoStartToggleRequested;

        public event Action HideFromTaskbarToggleRequested;

        public event Action UiScaleChangeRequested;

        public event Action<int> ThemeSelectRequested;

        public event Action<string> BackgroundSelectRequested;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            EnsureUiScaleRow();
            EnsureBackgroundPicker();
            _initialized = true;
            _alwaysOnTopButton?.onClick.AddListener(() => AlwaysOnTopToggleRequested?.Invoke());
            _muteButton?.onClick.AddListener(() => MuteToggleRequested?.Invoke());
            _autoStartButton?.onClick.AddListener(() => AutoStartToggleRequested?.Invoke());
            _hideFromTaskbarButton?.onClick.AddListener(() => HideFromTaskbarToggleRequested?.Invoke());
            _uiScaleButton?.onClick.AddListener(() => UiScaleChangeRequested?.Invoke());
            for (var index = 0; _themeSwatches != null && index < _themeSwatches.Length; index++)
            {
                var slot = index;
                _themeSwatches[index]?.GetComponent<Button>()?.onClick
                    .AddListener(() => ThemeSelectRequested?.Invoke(slot));
            }
        }

        /// <summary>
        /// Paints each round button with its own theme's colour and rings the
        /// one in use. Unlike a toggle the buttons never change colour: each one
        /// is always the theme it selects, so the ring is the only thing that
        /// moves. A button with no theme behind it is hidden.
        /// </summary>
        public void SetThemes(IReadOnlyList<DOTORIONTheme> themes, DOTORIONTheme active)
        {
            for (var index = 0; _themeSwatches != null && index < _themeSwatches.Length; index++)
            {
                var swatch = _themeSwatches[index];
                if (swatch == null)
                {
                    continue;
                }

                var theme = ThemeSelection.At(themes, index);
                swatch.gameObject.SetActive(theme != null);
                if (theme == null)
                {
                    continue;
                }

                swatch.color = theme.Swatch;
                var sprite = theme == active ? _swatchSelectedSprite : _swatchSprite;
                if (sprite != null)
                {
                    swatch.sprite = sprite;
                }
            }

            _activeTheme = active;
            PaintBackgrounds();
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            SetSwitch(_alwaysOnTopValue, enabled);
        }

        /// <summary>
        /// The row reads as the sound being on or off rather than as the mute
        /// being on or off: a switch that lights up when muted says the opposite
        /// of what the person just did.
        /// </summary>
        public void SetMuted(bool muted)
        {
            SetSwitch(_muteValue, !muted);
        }

        /// <summary>
        /// Whether Windows starts the app at login. Unlike the two switches
        /// above it, this one is not a preference the app keeps - it reflects a
        /// registry entry the person can also remove from Task Manager, so the
        /// row is repainted from what the registry says rather than from a field.
        /// </summary>
        public void SetAutoStart(bool enabled)
        {
            SetSwitch(_autoStartValue, enabled);
        }

        /// <summary>
        /// Whether the main window stays out of the taskbar and is represented
        /// by its notification-area icon instead.
        /// </summary>
        public void SetHiddenFromTaskbar(bool hidden)
        {
            SetSwitch(_hideFromTaskbarValue, hidden);
        }

        public void SetUiScalePercent(int percent)
        {
            if (_uiScaleValue != null)
            {
                _uiScaleValue.text = percent + "%";
            }
        }

        /// <summary>Rings the main background in use. Pictures not yet owned have no button at all.</summary>
        public void SetBackground(string selected)
        {
            _background = selected;
            PaintBackgrounds();
        }

        public void SetVersion(string version)
        {
            if (_versionText != null)
            {
                _versionText.text = version ?? string.Empty;
            }
        }

        public void SetBusy(bool busy)
        {
            if (_alwaysOnTopButton != null) _alwaysOnTopButton.interactable = !busy;
            if (_muteButton != null) _muteButton.interactable = !busy;
            if (_autoStartButton != null) _autoStartButton.interactable = !busy;
            if (_hideFromTaskbarButton != null) _hideFromTaskbarButton.interactable = !busy;
            if (_uiScaleButton != null) _uiScaleButton.interactable = !busy;
            foreach (var icon in _backgroundIcons) icon.Button.interactable = !busy;
            for (var index = 0; _themeSwatches != null && index < _themeSwatches.Length; index++)
            {
                var button = _themeSwatches[index] != null ? _themeSwatches[index].GetComponent<Button>() : null;
                if (button != null) button.interactable = !busy;
            }
        }

        /// <summary>
        /// Older hand-dressed prefabs predate this row. Clone the taskbar row at
        /// runtime instead of rebuilding the whole prefab and losing its artwork.
        /// New prefabs already contain and serialize the same controls.
        /// </summary>
        private void EnsureUiScaleRow()
        {
            var label = transform.Find("UiScaleLabel");
            var hint = transform.Find("UiScaleHint");
            var toggle = transform.Find("UiScaleToggle");
            var sourceLabel = transform.Find("HideFromTaskbarLabel");
            var sourceHint = transform.Find("HideFromTaskbarHint");
            var sourceToggle = transform.Find("HideFromTaskbarToggle");
            if ((label == null || hint == null || toggle == null) &&
                sourceLabel != null && sourceHint != null && sourceToggle != null)
            {
                label = Clone(sourceLabel, "UiScaleLabel");
                hint = Clone(sourceHint, "UiScaleHint");
                toggle = Clone(sourceToggle, "UiScaleToggle");
            }

            if (label == null || hint == null || toggle == null || sourceToggle == null)
            {
                return;
            }

            var uiScaleTop = sourceToggle.GetComponent<RectTransform>().anchoredPosition.y - RowStep;
            SetAnchoredY(label, uiScaleTop);
            SetAnchoredY(hint, uiScaleTop);
            SetAnchoredY(toggle, uiScaleTop);
            SetText(label, "UI 크기");
            SetText(hint, "4K 모니터에서 전체 화면을 확대합니다.");
            SetText(toggle, "100%");

            _uiScaleButton = toggle.GetComponent<Button>();
            _uiScaleValue = toggle.GetComponentInChildren<Text>();

            SetAnchoredY(transform.Find("VersionLabel"), uiScaleTop - RowStep);
            SetAnchoredY(transform.Find("VersionValue"), uiScaleTop - RowStep);
            var panelRect = transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, PanelHeight);
            }
        }

        private const float IconTop = -8f;
        private const float IconSize = 23f;
        private const float IconStep = 24f;
        private const float IconRight = 12f;
        // The swatch sprite's disk sits 4 pixels inside its 23 pixel square.
        private const float DiskInset = 4f;

        /// <summary>
        /// Round main background buttons at the right end of the title row,
        /// drawn like the theme buttons on the left: a disk showing the picture,
        /// and the theme's ring around the one in use. The default disk is a
        /// tiny window in the theme's swatch colour. Only owned pictures show,
        /// so without a reward the row holds the default alone.
        /// </summary>
        private void EnsureBackgroundPicker()
        {
            if (_backgroundIcons.Count > 0 || _swatchSprite == null)
            {
                return;
            }

            var ids = MainBackground.Ids;
            var source = transform.Find("HideFromTaskbarLabel");
            if (source != null)
            {
                var label = _backgroundLabel = (RectTransform)Clone(source, "BackgroundPickerLabel");
                label.anchorMin = label.anchorMax = label.pivot = Vector2.one;
                label.sizeDelta = new Vector2(48f, IconSize);
                var text = label.GetComponent<Text>() ?? label.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = "배경";
                    text.alignment = TextAnchor.MiddleRight;
                }
            }

            var swatchButton = _themeSwatches != null && _themeSwatches.Length > 0 && _themeSwatches[0] != null
                ? _themeSwatches[0].GetComponent<Button>()
                : null;
            for (var index = 0; index < ids.Length; index++)
            {
                var id = ids[index];
                var root = new GameObject("Background_" + id, typeof(RectTransform), typeof(Image), typeof(Button));
                root.transform.SetParent(transform, false);
                var rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                rect.sizeDelta = new Vector2(IconSize, IconSize);
                var ring = root.GetComponent<Image>();
                ring.sprite = _swatchSprite;

                var mask = Fill(new GameObject("Mask", typeof(RectTransform), typeof(Image), typeof(Mask)), root.transform, 0f);
                var maskImage = mask.GetComponent<Image>();
                maskImage.sprite = _swatchSprite;
                maskImage.raycastTarget = false;
                mask.GetComponent<Mask>().showMaskGraphic = false;

                var texture = MainBackground.Icon(id);
                Graphic picture;
                Image band = null;
                if (texture != null)
                {
                    var raw = Fill(new GameObject("Picture", typeof(RectTransform), typeof(RawImage)), mask.transform, DiskInset).GetComponent<RawImage>();
                    raw.texture = texture;
                    picture = raw;
                }
                else
                {
                    picture = Fill(new GameObject("Picture", typeof(RectTransform), typeof(Image)), mask.transform, DiskInset).GetComponent<Image>();
                    var bandRect = (RectTransform)Fill(new GameObject("TitleBar", typeof(RectTransform), typeof(Image)), picture.transform, 0f).transform;
                    bandRect.anchorMin = new Vector2(0f, 1f);
                    bandRect.sizeDelta = new Vector2(0f, 5f);
                    bandRect.offsetMin = new Vector2(0f, -5f);
                    band = bandRect.GetComponent<Image>();
                    band.raycastTarget = false;
                }

                var button = root.GetComponent<Button>();
                button.targetGraphic = picture;
                if (swatchButton != null)
                {
                    button.colors = swatchButton.colors;
                }

                button.onClick.AddListener(() => BackgroundSelectRequested?.Invoke(id));
                _backgroundIcons.Add(new BackgroundIcon { Id = id, Button = button, Ring = ring, Picture = picture, Band = band });
            }

            PaintBackgrounds();
        }

        private static GameObject Fill(GameObject child, Transform parent, float inset)
        {
            child.transform.SetParent(parent, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return child;
        }

        private void PaintBackgrounds()
        {
            var ringColor = _activeTheme != null ? _activeTheme.Swatch : Color.gray;
            var owned = _backgroundIcons.FindAll(icon => MainBackground.IsUnlocked(icon.Id));
            foreach (var icon in _backgroundIcons)
            {
                var slot = owned.IndexOf(icon);
                icon.Button.gameObject.SetActive(slot >= 0);
                if (slot < 0)
                {
                    continue;
                }

                // Packed against the right edge, the last owned picture outermost.
                ((RectTransform)icon.Button.transform).anchoredPosition =
                    new Vector2(-(IconRight + (owned.Count - 1 - slot) * IconStep), IconTop);
                var selected = icon.Id == _background;
                icon.Ring.sprite = selected && _swatchSelectedSprite != null ? _swatchSelectedSprite : _swatchSprite;
                icon.Ring.color = selected ? ringColor : Color.clear;
                if (icon.Band != null)
                {
                    // Skinned themes draw their window from sprites, so the swatch is the colour that matches what shows.
                    icon.Picture.color = Color.Lerp(ringColor, Color.white, .55f);
                    icon.Band.color = ringColor;
                }
            }

            if (_backgroundLabel != null)
            {
                _backgroundLabel.anchoredPosition = new Vector2(-(IconRight + owned.Count * IconStep + 2f), IconTop);
            }
        }

        private Transform Clone(Transform source, string name)
        {
            var copy = Instantiate(source.gameObject, transform);
            copy.name = name;
            return copy.transform;
        }

        private static void SetAnchoredY(Transform target, float y)
        {
            var rect = target as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
            }
        }

        private static void SetText(Transform target, string value)
        {
            var text = target != null
                ? target.GetComponent<Text>() ?? target.GetComponentInChildren<Text>()
                : null;
            if (text != null)
            {
                text.text = value;
            }
        }

        private const float RowStep = 36f;
        private const float PanelHeight = 268f;

        /// <summary>
        /// The row says which way the switch is set in words. Only the words are
        /// written: the colour and the font are whatever the prefab was dressed
        /// in, and repainting them here would undo that on every refresh.
        /// </summary>
        private static void SetSwitch(Text value, bool enabled)
        {
            if (value == null)
            {
                return;
            }

            value.text = enabled ? "켜짐" : "꺼짐";
        }
    }
}

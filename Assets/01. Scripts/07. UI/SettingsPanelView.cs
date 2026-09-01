using System;
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

        private bool _initialized;

        public event Action AlwaysOnTopToggleRequested;

        public event Action MuteToggleRequested;

        public event Action AutoStartToggleRequested;

        public event Action HideFromTaskbarToggleRequested;

        public event Action UiScaleChangeRequested;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            EnsureUiScaleRow();
            _initialized = true;
            _alwaysOnTopButton?.onClick.AddListener(() => AlwaysOnTopToggleRequested?.Invoke());
            _muteButton?.onClick.AddListener(() => MuteToggleRequested?.Invoke());
            _autoStartButton?.onClick.AddListener(() => AutoStartToggleRequested?.Invoke());
            _hideFromTaskbarButton?.onClick.AddListener(() => HideFromTaskbarToggleRequested?.Invoke());
            _uiScaleButton?.onClick.AddListener(() => UiScaleChangeRequested?.Invoke());
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

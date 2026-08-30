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
        [SerializeField] private Text _versionText;

        private bool _initialized;

        public event Action AlwaysOnTopToggleRequested;

        public event Action MuteToggleRequested;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _alwaysOnTopButton?.onClick.AddListener(() => AlwaysOnTopToggleRequested?.Invoke());
            _muteButton?.onClick.AddListener(() => MuteToggleRequested?.Invoke());
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
        }

        /// <summary>
        /// The row says which way the switch is set in words. The button's own
        /// surface is artwork, so nothing repaints it.
        /// </summary>
        private static void SetSwitch(Text value, bool enabled)
        {
            if (value == null)
            {
                return;
            }

            value.text = enabled ? "켜짐" : "꺼짐";
            value.color = enabled ? DOTORIONPalette.TextPrimary : DOTORIONPalette.TextSecondary;
        }
    }
}

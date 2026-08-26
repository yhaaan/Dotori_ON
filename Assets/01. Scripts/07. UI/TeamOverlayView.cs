using System;
using System.Collections.Generic;
using System.Linq;
using TeamOverlay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TeamOverlay.UI
{
    /// <summary>The editable main overlay prefab.</summary>
    public sealed class TeamOverlayView : MonoBehaviour
    {
        private const int MemberCount = 4;

        [Header("Optional typography override")]
        [SerializeField] private Font _fontOverride;
        [Header("Prefab references")]
        [SerializeField] private TeamMemberCardView[] _cards = new TeamMemberCardView[MemberCount];
        [SerializeField] private Button _checkInButton;
        [SerializeField] private Button _checkOutButton;
        [SerializeField] private Button _workingButton;
        [SerializeField] private Button _breakButton;
        [SerializeField] private Button _mealButton;
        [SerializeField] private Button _fakeEventButton;
        [SerializeField] private Button _topmostButton;
        [SerializeField] private Button _minimizeButton;
        [SerializeField] private Button _exitButton;
        [SerializeField] private Button _switchAccountButton;
        [SerializeField] private Text _topmostLabel;
        [SerializeField] private Text _feedbackText;
        [SerializeField] private WindowDragHandle _windowDragHandle;

        private readonly List<Button> _interactiveButtons = new List<Button>();
        private bool _initialized;

        public event Action CheckInRequested;
        public event Action CheckOutRequested;
        public event Action<ActivityStatus> ActivityChangeRequested;
        public event Action FakeCheckInRequested;
        public event Action AlwaysOnTopToggleRequested;
        public event Action MinimizeRequested;
        public event Action ExitRequested;
        public event Action SwitchAccountRequested;

        public void Initialize(Action beginWindowDrag)
        {
            if (_initialized) return;
            _initialized = true;

            UiFactory.EnsureEventSystem();
            UiFactory.ApplyApplicationFont(transform, _fontOverride);
            if (_windowDragHandle != null) _windowDragHandle.Initialize(beginWindowDrag);

            AddListener(_checkInButton, () => CheckInRequested?.Invoke());
            AddListener(_checkOutButton, () => CheckOutRequested?.Invoke());
            AddListener(_workingButton, () => ActivityChangeRequested?.Invoke(ActivityStatus.Working));
            AddListener(_breakButton, () => ActivityChangeRequested?.Invoke(ActivityStatus.Break));
            AddListener(_mealButton, () => ActivityChangeRequested?.Invoke(ActivityStatus.Meal));
            AddListener(_fakeEventButton, () => FakeCheckInRequested?.Invoke());
            AddListener(_topmostButton, () => AlwaysOnTopToggleRequested?.Invoke());
            AddListener(_minimizeButton, () => MinimizeRequested?.Invoke());
            AddListener(_exitButton, () => ExitRequested?.Invoke());
            AddListener(_switchAccountButton, () => SwitchAccountRequested?.Invoke());

            AddInteractive(_checkInButton);
            AddInteractive(_checkOutButton);
            AddInteractive(_workingButton);
            AddInteractive(_breakButton);
            AddInteractive(_mealButton);
            AddInteractive(_fakeEventButton);
            AddInteractive(_switchAccountButton);
        }

        public void Bind(IReadOnlyList<MemberState> members, string localMemberId, DateTimeOffset nowUtc)
        {
            var orderedMembers = members
                .OrderBy(member => member.SortOrder)
                .ThenBy(member => member.MemberId, StringComparer.Ordinal)
                .Take(MemberCount)
                .ToArray();

            for (var index = 0; index < _cards.Length; index++)
            {
                var card = _cards[index];
                if (card == null) continue;
                var hasMember = index < orderedMembers.Length;
                card.gameObject.SetActive(hasMember);
                if (hasMember)
                {
                    var member = orderedMembers[index];
                    card.Bind(member, member.MemberId == localMemberId, nowUtc);
                }
            }

            var localMember = orderedMembers.FirstOrDefault(member => member.MemberId == localMemberId);
            var isClockedIn = localMember != null && localMember.AttendanceStatus == AttendanceStatus.ClockedIn;
            SetActive(_checkInButton, !isClockedIn);
            SetActive(_checkOutButton, isClockedIn);
            SetActive(_workingButton, isClockedIn);
            SetActive(_breakButton, isClockedIn);
            SetActive(_mealButton, isClockedIn);
            if (isClockedIn) SetActivitySelection(localMember.ActivityStatus.GetValueOrDefault());
        }

        public void SetBusy(bool busy)
        {
            foreach (var button in _interactiveButtons) button.interactable = !busy;
            if (busy) _feedbackText.text = "상태를 반영하는 중…";
        }

        public void ShowFeedback(string message, bool isError = false)
        {
            _feedbackText.text = message;
            _feedbackText.color = isError ? TeamOverlayPalette.Danger : TeamOverlayPalette.TextSecondary;
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            if (_topmostLabel != null) _topmostLabel.text = enabled ? "TOP✓" : "TOP";
            if (_topmostButton == null) return;
            var background = _topmostButton.GetComponent<Image>();
            if (background != null)
                background.color = enabled ? TeamOverlayPalette.Accent : TeamOverlayPalette.Button;
        }

        private void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private void AddInteractive(Button button)
        {
            if (button != null) _interactiveButtons.Add(button);
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null) button.gameObject.SetActive(active);
        }

        private void SetActivitySelection(ActivityStatus selected)
        {
            Tint(_workingButton, selected == ActivityStatus.Working ? TeamOverlayPalette.Working : TeamOverlayPalette.Button);
            Tint(_breakButton, selected == ActivityStatus.Break ? TeamOverlayPalette.Break : TeamOverlayPalette.Button);
            Tint(_mealButton, selected == ActivityStatus.Meal ? TeamOverlayPalette.Meal : TeamOverlayPalette.Button);
        }

        private static void Tint(Button button, Color color)
        {
            if (button == null) return;
            var background = button.GetComponent<Image>();
            if (background != null) background.color = color;
        }
    }
}

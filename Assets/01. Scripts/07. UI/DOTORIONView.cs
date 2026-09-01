using System;
using System.Collections.Generic;
using System.Linq;
using DOTORION.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.UI
{
    /// <summary>The editable main overlay prefab.</summary>
    public sealed class DOTORIONView : MonoBehaviour
    {
        private const int MemberCount = 4;

        [Header("Prefab references")]
        [SerializeField] private TeamMemberCardView[] _cards = new TeamMemberCardView[MemberCount];
        [SerializeField] private Button _checkInButton;
        [SerializeField] private Button _checkOutButton;
        [SerializeField] private Button _workingButton;
        [SerializeField] private Button _breakButton;
        [SerializeField] private Button _mealButton;
        [SerializeField] private Button _fakeEventButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _minimizeButton;
        [SerializeField] private Button _exitButton;
        [SerializeField] private Button _miniModeButton;
        [SerializeField] private Button _statsButton;
        [SerializeField] private Button _teamNudgeButton;
        [SerializeField] private Button _dailyCheckInButton;
        [SerializeField] private Text _dailyCheckInPointsLabel;
        [SerializeField] private InputField _statusNoteInput;

        [Tooltip("출근 전에만 보이는 배경. 비워 두어도 나머지는 그대로 동작합니다.")]
        [SerializeField] private GameObject _offlineBackground;

        /// <summary>
        /// Two faces for the attendance button: one that says today is still
        /// there to take, one that says it is already taken. Leaving either
        /// empty keeps whatever the prefab draws, so the button is never blanked
        /// by a field nobody filled in.
        /// </summary>
        [Header("Daily check-in sprites")]
        [SerializeField] private Sprite _dailyCheckInAvailableSprite;
        [SerializeField] private Sprite _dailyCheckInClaimedSprite;

        private bool _dailyCheckInSupported;
        private bool _isClockedIn;
        [SerializeField] private Text _feedbackText;
        [SerializeField] private Text _versionLabel;
        [SerializeField] private WindowDragHandle _windowDragHandle;
        [SerializeField] private TeamStatisticsPanelView _statisticsPanel;
        [SerializeField] private SettingsPanelView _settingsPanel;
        [SerializeField] private RectTransform _windowBackground;
        [SerializeField] private AvatarPickerPanelView _avatarPickerPanel;
        [SerializeField] private MiniOverlayPanelView _miniPanel;
        [SerializeField] private DeveloperDashboardView _dashboardPanel;

        private readonly List<Button> _interactiveButtons = new List<Button>();
        private Canvas _canvas;
        private CanvasScaler _canvasScaler;
        private float _lastScaleFactor;
        private int _pendingTextRefreshes;
        private CanvasScaler.ScaleMode _fullScaleMode;
        private Vector2 _fullReferenceResolution;
        private float _fullScaleFactor;
        private float _uiScale = 1f;
        private bool _initialized;

        public event Action CheckInRequested;
        public event Action CheckOutRequested;
        public event Action<ActivityStatus> ActivityChangeRequested;
        public event Action FakeCheckInRequested;
        public event Action SettingsToggleRequested;
        public event Action AlwaysOnTopToggleRequested;

        /// <summary>Silences the notification sounds, and brings them back.</summary>
        public event Action MuteToggleRequested;

        /// <summary>Starts the app with Windows, or stops it doing so.</summary>
        public event Action AutoStartToggleRequested;

        /// <summary>Keeps the app in the Windows notification area instead of the taskbar.</summary>
        public event Action HideFromTaskbarToggleRequested;

        public event Action UiScaleChangeRequested;

        public event Action MinimizeRequested;
        public event Action ExitRequested;

        /// <summary>
        /// Raised by a double click on the local member's own name. It is the same
        /// sign-out and sign-in the top bar used to offer as a button, moved onto
        /// the name it actually changes.
        /// </summary>
        public event Action SwitchAccountRequested;

        /// <summary>The gift button: showing up today, which is not clocking in.</summary>
        public event Action DailyCheckInRequested;

        public event Action MiniModeRequested;

        /// <summary>Raised by a double click anywhere on the mini overlay.</summary>
        public event Action MiniModeExitRequested;

        public event Action<string> StatusNoteSubmitted;
        public event Action StatsToggleRequested;
        public event Action<StatisticsPeriod> StatisticsPeriodChangeRequested;

        /// <summary>Carries the member id to poke, or null for the whole team.</summary>
        public event Action<string> NudgeRequested;

        /// <summary>Raised when the local member clicks their own profile icon.</summary>
        public event Action AvatarPickerRequested;

        public event Action AvatarPickerCloseRequested;

        /// <summary>Carries the catalog key the local member picked.</summary>
        public event Action<string> AvatarPicked;

        public bool IsStatisticsVisible => _statisticsPanel != null && _statisticsPanel.gameObject.activeSelf;

        public bool IsSettingsVisible => _settingsPanel != null && _settingsPanel.gameObject.activeSelf;

        public bool IsAvatarPickerVisible => _avatarPickerPanel != null && _avatarPickerPanel.gameObject.activeSelf;

        public bool IsMiniModeVisible => _miniPanel != null && _miniPanel.gameObject.activeSelf;

        public bool IsDashboardVisible => _dashboardPanel != null && _dashboardPanel.gameObject.activeSelf;

        /// <summary>The dashboard itself, so the app can wire its buttons directly.</summary>
        public DeveloperDashboardView Dashboard => _dashboardPanel;

        /// <summary>
        /// How much taller the window has to be while the picker is open. Read
        /// from the prefab rather than repeated as a constant, so moving the panel
        /// in the Inspector cannot leave the window the wrong size.
        /// </summary>
        public float AvatarPickerHeight =>
            _avatarPickerPanel != null
                ? ((RectTransform)_avatarPickerPanel.transform).sizeDelta.y
                : 0f;

        public void Initialize(Action beginWindowDrag)
        {
            if (_initialized) return;
            _initialized = true;

            UiFactory.EnsureEventSystem();
            // Captured rather than hardcoded so tuning the scaler in the prefab
            // survives a trip through the mini overlay.
            _canvas = GetComponent<Canvas>();
            _lastScaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
            _canvasScaler = GetComponent<CanvasScaler>();
            if (_canvasScaler != null)
            {
                _fullScaleMode = _canvasScaler.uiScaleMode;
                _fullReferenceResolution = _canvasScaler.referenceResolution;
                _fullScaleFactor = _canvasScaler.scaleFactor;
            }

            if (_windowDragHandle != null) _windowDragHandle.Initialize(beginWindowDrag);
            // Application.version is the bundleVersion the build was stamped with,
            // so a teammate can read which zip they are running off the title bar.
            if (_versionLabel != null) _versionLabel.text = "v" + Application.version;
            _settingsPanel?.SetVersion("DOTORI ON v" + Application.version);

            AddListener(_checkInButton, () => CheckInRequested?.Invoke());
            AddListener(_checkOutButton, () => CheckOutRequested?.Invoke());
            AddListener(_workingButton, () => ActivityChangeRequested?.Invoke(ActivityStatus.Working));
            AddListener(_breakButton, () => ActivityChangeRequested?.Invoke(ActivityStatus.Break));
            AddListener(_mealButton, () => ActivityChangeRequested?.Invoke(ActivityStatus.Meal));
            AddListener(_fakeEventButton, () => FakeCheckInRequested?.Invoke());
            AddListener(_settingsButton, () => SettingsToggleRequested?.Invoke());
            AddListener(_minimizeButton, () => MinimizeRequested?.Invoke());
            AddListener(_exitButton, () => ExitRequested?.Invoke());
            AddListener(_miniModeButton, () => MiniModeRequested?.Invoke());
            AddListener(_dailyCheckInButton, () => DailyCheckInRequested?.Invoke());
            AddListener(_statsButton, () => StatsToggleRequested?.Invoke());
            AddListener(_teamNudgeButton, () => NudgeRequested?.Invoke(null));
            foreach (var card in _cards)
            {
                if (card == null) continue;
                card.Initialize();
                // Every tile stays inert until a Bind says whose card it is; the
                // one belonging to the local member is the only clickable one.
                card.SetAvatarEditable(false);
                card.SetRenameAvailable(false);
                card.NudgeRequested += memberId => NudgeRequested?.Invoke(memberId);
                card.AvatarEditRequested += () => AvatarPickerRequested?.Invoke();
                card.RenameRequested += () => SwitchAccountRequested?.Invoke();
            }

            AddInteractive(_checkInButton);
            AddInteractive(_checkOutButton);
            AddInteractive(_workingButton);
            AddInteractive(_breakButton);
            AddInteractive(_mealButton);
            AddInteractive(_fakeEventButton);
            AddInteractive(_miniModeButton);
            AddInteractive(_dailyCheckInButton);
            AddInteractive(_statsButton);
            AddInteractive(_settingsButton);
            AddInteractive(_teamNudgeButton);

            if (_statisticsPanel != null)
            {
                _statisticsPanel.Initialize();
                _statisticsPanel.PeriodChangeRequested +=
                    period => StatisticsPeriodChangeRequested?.Invoke(period);
                _statisticsPanel.gameObject.SetActive(false);
            }

            if (_settingsPanel != null)
            {
                _settingsPanel.Initialize();
                _settingsPanel.AlwaysOnTopToggleRequested += () => AlwaysOnTopToggleRequested?.Invoke();
                _settingsPanel.MuteToggleRequested += () => MuteToggleRequested?.Invoke();
                _settingsPanel.AutoStartToggleRequested += () => AutoStartToggleRequested?.Invoke();
                _settingsPanel.HideFromTaskbarToggleRequested +=
                    () => HideFromTaskbarToggleRequested?.Invoke();
                _settingsPanel.UiScaleChangeRequested += () => UiScaleChangeRequested?.Invoke();
                _settingsPanel.gameObject.SetActive(false);
            }

            if (_avatarPickerPanel != null)
            {
                _avatarPickerPanel.Initialize();
                _avatarPickerPanel.AvatarPicked += key => AvatarPicked?.Invoke(key);
                _avatarPickerPanel.CloseRequested += () => AvatarPickerCloseRequested?.Invoke();
                SetAvatarPickerVisible(false);
            }

            if (_dashboardPanel != null)
            {
                _dashboardPanel.Initialize();
                _dashboardPanel.gameObject.SetActive(false);
            }

            if (_miniPanel != null)
            {
                _miniPanel.Initialize(beginWindowDrag);
                _miniPanel.RestoreRequested += () => MiniModeExitRequested?.Invoke();
                SetMiniModeVisible(false);
            }

            if (_statusNoteInput != null)
            {
                _statusNoteInput.onEndEdit.AddListener(note => StatusNoteSubmitted?.Invoke(note));
            }
        }

        /// <summary>
        /// Swaps the whole overlay for the mini one. The canvas scaler has to go
        /// with it: it normally scales the layout to a 480 wide window, which at
        /// mini width would shrink the text to a quarter size instead of showing
        /// a smaller window. The mini overlay is authored in real pixels.
        /// </summary>
        public void SetMiniModeVisible(bool visible)
        {
            if (_miniPanel == null)
            {
                return;
            }

            _miniPanel.gameObject.SetActive(visible);
            if (_windowBackground != null)
            {
                _windowBackground.gameObject.SetActive(!visible);
            }

            RefreshTextRenderingSoon();

            if (_canvasScaler == null)
            {
                return;
            }

            if (visible)
            {
                _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                _canvasScaler.scaleFactor = _uiScale;
                return;
            }

            _canvasScaler.uiScaleMode = _fullScaleMode;
            _canvasScaler.referenceResolution = _fullReferenceResolution;
            _canvasScaler.scaleFactor = _fullScaleFactor;
        }

        /// <summary>
        /// A dynamic font atlas is repacked whenever glyphs are wanted at a size
        /// it does not hold yet, which moves every glyph already in it. uGUI
        /// keeps the vertices it built for text that has not changed, so those
        /// keep pointing at where their glyph used to be and the text comes back
        /// smeared while everything else looks right. Swapping in the mini
        /// overlay does exactly that: it asks for smaller type while the full
        /// overlay is switched off and cannot notice.
        ///
        /// Two things make the size change, and both are covered here: the swap
        /// itself, and the window resize Unity applies a frame or two later,
        /// which briefly leaves the full layout scaled to a 130 wide window.
        /// </summary>
        private void Update()
        {
            if (_canvas == null)
            {
                return;
            }

            var scaleFactor = _canvas.scaleFactor;
            if (!Mathf.Approximately(scaleFactor, _lastScaleFactor))
            {
                _lastScaleFactor = scaleFactor;
                RefreshTextRenderingSoon();
            }

            if (_pendingTextRefreshes <= 0)
            {
                return;
            }

            _pendingTextRefreshes--;
            foreach (var text in GetComponentsInChildren<Text>(true))
            {
                text.FontTextureChanged();
            }
        }

        /// <summary>
        /// Spread over a few frames rather than done once, because the resize
        /// that settles the scale has not landed yet on the frame that asks.
        /// </summary>
        private void RefreshTextRenderingSoon()
        {
            _pendingTextRefreshes = 3;
        }

        /// <summary>
        /// Shows where the daily check-in stands. A backend with no history
        /// cannot offer one at all, and a button that is only ever refused is
        /// worse than no button, so an unsupported backend hides it outright.
        /// </summary>
        public void SetDailyCheckIn(DailyCheckInState state, bool supported)
        {
            _dailyCheckInSupported = supported && state != null;
            ApplyDailyCheckInVisibility();

            if (!supported || state == null)
            {
                return;
            }

            // Claimed days stay visible rather than hidden: the button is also
            // where the point total lives, and a total that vanished for the rest
            // of the day would read as having lost the points. The colour is the
            // prefab's, like every other surface here; only the number changes.
            if (_dailyCheckInPointsLabel != null)
            {
                _dailyCheckInPointsLabel.text = state.TotalPoints + "P";
            }

            SetDailyCheckInSprite(state.ClaimedToday
                ? _dailyCheckInClaimedSprite
                : _dailyCheckInAvailableSprite);
        }

        /// <summary>
        /// The button only shows while you are clocked in - the server refuses a
        /// check-in from someone who has not arrived, and a button that is only
        /// ever refused is worse than no button. The point total stays either
        /// way: it is what you have earned, not something you can do right now.
        /// </summary>
        private void ApplyDailyCheckInVisibility()
        {
            SetActive(_dailyCheckInButton, _dailyCheckInSupported && _isClockedIn);
            if (_dailyCheckInPointsLabel != null)
            {
                _dailyCheckInPointsLabel.gameObject.SetActive(_dailyCheckInSupported);
            }
        }

        private void SetDailyCheckInSprite(Sprite sprite)
        {
            if (sprite == null || _dailyCheckInButton == null)
            {
                return;
            }

            var image = _dailyCheckInButton.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
            }
        }

        public void SetDashboardVisible(bool visible)
        {
            if (_dashboardPanel != null)
            {
                _dashboardPanel.gameObject.SetActive(visible);
            }
        }

        /// <summary>Hands the icon artwork to the cards and the picker.</summary>
        public void SetAvatarCatalog(TeamAvatarCatalog catalog)
        {
            foreach (var card in _cards)
            {
                if (card != null) card.SetAvatarCatalog(catalog);
            }

            _avatarPickerPanel?.SetCatalog(catalog);
        }

        /// <summary>
        /// Opens the picker above the rest of the overlay. The panel sits at the
        /// top of the canvas and the window background is pushed down under it, so
        /// the window can grow upwards without any of the existing layout moving
        /// relative to itself.
        /// </summary>
        public void SetAvatarPickerVisible(bool visible)
        {
            if (_avatarPickerPanel == null)
            {
                return;
            }

            _avatarPickerPanel.gameObject.SetActive(visible);
            if (_windowBackground != null)
            {
                var inset = visible ? AvatarPickerHeight : 0f;
                _windowBackground.offsetMax = new Vector2(_windowBackground.offsetMax.x, -inset);
            }
        }

        public void SetAvatarPickerSelection(string avatarKey)
        {
            _avatarPickerPanel?.SetSelected(avatarKey);
        }

        public void Bind(
            IReadOnlyList<MemberState> members,
            string localMemberId,
            DateTimeOffset nowUtc,
            UnreadNoteTracker unreadNotes)
        {
            var orderedMembers = MemberCardOrder.Sort(members, MemberCount);

            for (var index = 0; index < _cards.Length; index++)
            {
                var card = _cards[index];
                if (card == null) continue;
                var hasMember = index < orderedMembers.Length;
                card.gameObject.SetActive(hasMember);
                if (hasMember)
                {
                    var member = orderedMembers[index];
                    card.Bind(member, nowUtc);
                }
            }

            for (var index = 0; index < _cards.Length; index++)
            {
                var card = _cards[index];
                if (card == null || index >= orderedMembers.Length) continue;
                card.SetAvatarEditable(orderedMembers[index].MemberId == localMemberId);
            }

            var localMember = orderedMembers.FirstOrDefault(member => member.MemberId == localMemberId);
            var isClockedIn = localMember != null && localMember.AttendanceStatus == AttendanceStatus.ClockedIn;
            for (var index = 0; index < _cards.Length; index++)
            {
                var card = _cards[index];
                if (card == null || index >= orderedMembers.Length) continue;
                var member = orderedMembers[index];
                card.SetNudgeAvailable(
                    isClockedIn
                    && member.MemberId != localMemberId
                    && member.AttendanceStatus == AttendanceStatus.ClockedIn);
                card.SetRenameAvailable(member.MemberId == localMemberId);
            }

            _miniPanel?.Bind(orderedMembers, unreadNotes);

            _isClockedIn = isClockedIn;
            ApplyDailyCheckInVisibility();

            SetActive(_teamNudgeButton, isClockedIn);
            SetActive(_checkInButton, !isClockedIn);
            // Shown exactly while the check-in button is: the window reads as
            // asleep until the person is actually on the clock.
            if (_offlineBackground != null) _offlineBackground.SetActive(!isClockedIn);
            SetActive(_checkOutButton, isClockedIn);
            SetActive(_workingButton, isClockedIn);
            SetActive(_breakButton, isClockedIn);
            SetActive(_mealButton, isClockedIn);
            BindStatusNote(localMember, isClockedIn);
            if (localMember != null) SetAvatarPickerSelection(localMember.AvatarKey);
        }

        /// <summary>
        /// Mirrors the server's note into the field, but never while it has focus:
        /// a poll landing mid-sentence would otherwise overwrite what is being
        /// typed. Writing a note requires an open session, so the field is hidden
        /// when clocked out.
        /// </summary>
        private void BindStatusNote(MemberState localMember, bool isClockedIn)
        {
            if (_statusNoteInput == null)
            {
                return;
            }

            _statusNoteInput.gameObject.SetActive(isClockedIn);
            if (!isClockedIn || _statusNoteInput.isFocused)
            {
                return;
            }

            var note = localMember?.StatusNote ?? string.Empty;
            if (!string.Equals(_statusNoteInput.text, note, StringComparison.Ordinal))
            {
                _statusNoteInput.text = note;
            }
        }

        public void SetBusy(bool busy)
        {
            foreach (var button in _interactiveButtons) button.interactable = !busy;
            _avatarPickerPanel?.SetBusy(busy);
            _settingsPanel?.SetBusy(busy);
            if (busy) _feedbackText.text = "상태를 반영하는 중…";
        }

        /// <summary>
        /// The colour is the prefab's. <paramref name="isError"/> stays in the
        /// signature because every caller already says which it is, and the day
        /// the line wants to look different again this is where it reads it.
        /// </summary>
        public void ShowFeedback(string message, bool isError = false)
        {
            _feedbackText.text = message;
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            _settingsPanel?.SetAlwaysOnTop(enabled);
        }

        public void SetMuted(bool muted)
        {
            _settingsPanel?.SetMuted(muted);
        }

        public void SetAutoStart(bool enabled)
        {
            _settingsPanel?.SetAutoStart(enabled);
        }

        public void SetHiddenFromTaskbar(bool hidden)
        {
            _settingsPanel?.SetHiddenFromTaskbar(hidden);
        }

        public void SetUiScalePercent(int percent)
        {
            _uiScale = Mathf.Clamp(percent / 100f, 1f, 2f);
            _settingsPanel?.SetUiScalePercent(Mathf.RoundToInt(_uiScale * 100f));
            if (IsMiniModeVisible && _canvasScaler != null)
            {
                _canvasScaler.scaleFactor = _uiScale;
            }

            RefreshTextRenderingSoon();
        }

        /// <summary>Opens under the compact layout, the way the statistics panel does.</summary>
        public void SetSettingsVisible(bool visible)
        {
            if (_settingsPanel != null)
            {
                _settingsPanel.gameObject.SetActive(visible);
            }
        }

        public void SetStatisticsVisible(bool visible)
        {
            if (_statisticsPanel != null)
            {
                _statisticsPanel.gameObject.SetActive(visible);
            }
        }

        public void SetStatisticsPeriod(StatisticsPeriod period)
        {
            _statisticsPanel?.SetPeriod(period);
        }

        public void ShowStatisticsLoading(StatisticsRange range)
        {
            _statisticsPanel?.ShowLoading(range);
        }

        public void BindStatistics(
            StatisticsRange range,
            IReadOnlyList<MemberPeriodStat> stats,
            IReadOnlyList<TeamRankingEntry> ranking,
            string localMemberId)
        {
            _statisticsPanel?.Bind(range, stats, ranking, localMemberId);
        }

        public void ShowStatisticsError(StatisticsRange range, string message)
        {
            _statisticsPanel?.ShowError(range, message);
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
    }
}

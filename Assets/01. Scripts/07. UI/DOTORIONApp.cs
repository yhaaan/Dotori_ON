using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DOTORION.Audio;
using DOTORION.Backend.Mock;
using DOTORION.Core;
using DOTORION.Identity;
using DOTORION.Platform.Windows;
using DOTORION.Supabase;
using DOTORION.Update;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DOTORION.UI
{
    /// <summary>
    /// Composition root. Owns identity, the backend and the two editable UI
    /// prefabs, and moves the application between the sign-in screen and the
    /// overlay.
    /// </summary>
    public sealed class DOTORIONApp : MonoBehaviour
    {
        private const string AppPrefabResourcePath = "DOTORION/DOTORIONApp";

        // Polling stands in for a Realtime subscription. Four members at three
        // seconds is a handful of requests a minute, and a dropped poll self-heals
        // on the next tick instead of leaving the roster stale until a reconnect.
        private const float TeamStatePollSeconds = 3f;

        /// <summary>
        /// How long to wait before asking again for a check-in state that did
        /// not arrive. Long enough that a backend which is simply down is not
        /// asked twenty times a minute, short enough that the button appears
        /// while the person is still looking at the window.
        /// </summary>
        private const float DailyCheckInRetrySeconds = 60f;

        // Comfortably inside the server's three-minute stale-session timeout, so a
        // single failed heartbeat never clocks anyone out.
        private const float HeartbeatSeconds = 45f;

        private const float MaximumPollBackoffSeconds = 30f;

        /// <summary>
        /// How long the desk has to stay quiet before the overlay says so on the
        /// person's behalf. Long enough that a phone call or a coffee run does not
        /// flip anyone's status the moment they look away, short enough that the
        /// work time the statistics rank people by is not simply the time the app
        /// was open.
        /// </summary>
        private const double IdleBreakSeconds = 600d;

        private const float IdleCheckSeconds = 5f;

        /// <summary>
        /// Whether the overlay was last left in mini mode. It is a view
        /// preference and nothing else, so it lives in PlayerPrefs rather than in
        /// the crash-safe identity store next to the things worth recovering.
        /// </summary>
        private const string MiniModePreferenceKey = "DOTORION.MiniMode";

        /// <summary>
        /// Whether the notification sounds are silenced. A view preference like
        /// the mini overlay's, and kept in the same place for the same reason.
        /// </summary>
        private const string MutePreferenceKey = "DOTORION.Muted";

        [Header("Prefab references")]
        [SerializeField] private DOTORIONView _mainViewPrefab;
        [SerializeField] private FirstRunNameView _firstRunNamePrefab;

        [Tooltip("효과음 설정 에셋. 비워 두면 코드로 만든 기본 알림음만 납니다.")]
        [SerializeField] private DOTORIONSounds _sounds;

        [Tooltip("프로필 아이콘 목록 에셋. 비워 두면 아이콘 대신 이름 첫 글자만 보입니다.")]
        [SerializeField] private TeamAvatarCatalog _avatarCatalog;

        [Tooltip("새 버전을 알리는 모달 프리팹. 비워 두면 업데이트 확인을 하지 않습니다.")]
        [SerializeField] private UpdatePromptView _updatePromptPrefab;
        [Header("Development")]
        [Tooltip("Runs the overlay against the in-memory roster instead of Supabase. Identity still uses the real project.")]
        [SerializeField] private bool _useMockBackend;

        private static DOTORIONApp _instance;

        private UpdatePromptView _updatePrompt;
        private UpdateDownloader _updateDownloader;
        private UpdateManifest _offeredUpdate;
        private bool _updateInProgress;

        private readonly ConcurrentQueue<TeamEvent> _pendingEvents = new ConcurrentQueue<TeamEvent>();
        private CancellationTokenSource _lifetime;
        private LocalIdentityProfileStore _identityStore;
        private LocalIdentityProfile _identityProfile;
        private HttpClientSupabaseTransport _supabaseTransport;
        private SupabaseIdentityClient _supabaseIdentity;
        private ITeamBackend _backend;
        private IMockTeamBackendControls _mockControls;
        private IDisposable _eventSubscription;
        private IReadOnlyList<MemberState> _members;
        private DOTORIONView _view;
        private FirstRunNameView _firstRunNameView;
        private NotificationTonePlayer _tonePlayer;
        private WindowsOverlayWindow _window;
        private float _nextTimerRefresh;
        private float _nextTeamStatePoll;

        /// <summary>
        /// The team day the check-in state was last read for. Only a reply from
        /// the server moves it, so a failed read is retried instead of leaving
        /// the button hidden for the rest of the day.
        /// </summary>
        private DateTime _dailyCheckInDay = DateTime.MinValue;

        private float _nextDailyCheckInAttempt;
        private float _nextHeartbeat;
        private float _nextIdleCheck;
        private readonly IdleActivityPolicy _idlePolicy = new IdleActivityPolicy(IdleBreakSeconds);
        private readonly UnreadNoteTracker _unreadNotes = new UnreadNoteTracker();
        private int _consecutivePollFailures;
        private bool _refreshInProgress;
        private bool _heartbeatInProgress;
        private bool _identityActivationInProgress;
        private bool _signOutInProgress;
        private bool _renamePending;
        private bool _renameInProgress;
        private bool _dashboardBusy;
        private bool _mutationInProgress;
        private int _statisticsRequestId;
        private StatisticsPeriod _statisticsPeriod = StatisticsPeriod.LastSevenDays;
        private bool _quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null || FindAnyObjectByType<DOTORIONApp>() != null)
            {
                return;
            }

            var prefab = Resources.Load<GameObject>(AppPrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError(
                    "Resources/" + AppPrefabResourcePath + ".prefab is missing. " +
                    "Run DOTORI ON/Create Missing Editable UI Prefabs to recreate it.");
                return;
            }

            var instance = Instantiate(prefab);
            instance.name = prefab.name;
            DontDestroyOnLoad(instance);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;

            _lifetime = new CancellationTokenSource();
            _identityStore = new LocalIdentityProfileStore();
            _supabaseTransport = new HttpClientSupabaseTransport();

            _window = gameObject.AddComponent<WindowsOverlayWindow>();
            _window.ClockOutAndExitRequested += HandleClockOutAndExitRequested;
            _window.SessionEndingRequested += HandleSessionEndingRequested;
            _window.SuspendingRequested += HandleSuspendingRequested;
            _window.Configure();
            _window.SetAlwaysOnTop(true);
            _tonePlayer = gameObject.AddComponent<NotificationTonePlayer>();
            _tonePlayer.UseSounds(_sounds);
            _tonePlayer.SetMuted(PlayerPrefs.GetInt(MutePreferenceKey, 0) == 1);

            if (_firstRunNamePrefab == null)
            {
                Debug.LogError("DOTORIONApp is missing its first-run name prefab reference.");
                return;
            }

            _firstRunNameView = Instantiate(_firstRunNamePrefab, transform);
            _firstRunNameView.name = _firstRunNamePrefab.name;

            if (_updatePromptPrefab != null)
            {
                _updatePrompt = Instantiate(_updatePromptPrefab, transform);
                _updatePrompt.name = _updatePromptPrefab.name;
                _updatePrompt.Initialize();
                _updatePrompt.Confirmed += HandleUpdateConfirmed;
                _updatePrompt.Dismissed += HandleUpdateDismissed;
            }
            _firstRunNameView.Initialize();
            _firstRunNameView.Submitted += HandleFirstRunNameSubmitted;
            _firstRunNameView.Cancelled += HandleRenameCancelled;
        }

        private async void Start()
        {
            if (_firstRunNameView == null)
            {
                return;
            }

            IdentityProfileLoadResult loadResult = null;
            try
            {
                loadResult = _identityStore.Load();
                if (loadResult.Status == IdentityProfileLoadStatus.Corrupt)
                {
                    _firstRunNameView.Show();
                    _firstRunNameView.ShowError("저장된 이름 정보가 손상되었습니다. 자동으로 새 ID를 만들지 않았습니다.");
                    return;
                }

                if (loadResult.Status == IdentityProfileLoadStatus.StorageUnavailable)
                {
                    _firstRunNameView.Show();
                    _firstRunNameView.ShowError("이름 저장소에 접근할 수 없습니다. 폴더 권한을 확인해주세요.");
                    return;
                }

                if (!loadResult.HasProfile)
                {
                    // No stored name yet: never create an anonymous Auth user before
                    // the person has told us which member they are signing in as.
                    _firstRunNameView.Show();
                    return;
                }

                var profile = loadResult.Profile;
                var member = await SignInAsync(
                    DisplayNamePolicy.Validate(profile.DisplayName),
                    _lifetime.Token);
                ActivateIdentity(profile, member);
                await RefreshStateAsync();
                _view.ShowFeedback(RestoredProfileFeedback(loadResult));
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (_view != null)
                {
                    // Sign-in already succeeded, so keep the overlay and report the
                    // failure there instead of stacking the modal on top of it.
                    _view.ShowFeedback(IdentityStartupError(exception), true);
                    return;
                }

                _firstRunNameView.Show(loadResult?.Profile?.DisplayName);
                _firstRunNameView.ShowError(IdentityStartupError(exception));
            }
        }

        private void Update()
        {
            if (_backend == null || _view == null)
            {
                return;
            }

            while (_pendingEvents.TryDequeue(out var teamEvent))
            {
                var isLocalActor = string.Equals(
                    teamEvent.ActorMemberId,
                    _backend.LocalMemberId,
                    StringComparison.Ordinal);
                if (teamEvent.Type == TeamEventType.MemberCheckedIn && !isLocalActor)
                {
                    _tonePlayer.Play(TeamSound.TeammateCheckedIn);
                }
                else if (teamEvent.Type == TeamEventType.MemberCheckedOut && !isLocalActor)
                {
                    _tonePlayer.Play(TeamSound.TeammateCheckedOut);
                }
                else if (teamEvent.Type == TeamEventType.MemberStatusNoteChanged && !isLocalActor)
                {
                    // Only worth interrupting for once. A note rewritten while it
                    // is still unread does not deserve a second flash.
                    if (_unreadNotes.Add(teamEvent.State.MemberId) && !OverlayIsBeingRead())
                    {
                        _view.ShowFeedback(teamEvent.State.DisplayName + "님이 메모를 남겼습니다.");
                        // The overlay is either behind something or shrunk to the
                        // mini one, where a note has nowhere to appear.
                        _window.FlashTaskbar();
                    }
                }
                else if (teamEvent.Type == TeamEventType.MemberNudged)
                {
                    _tonePlayer.Play(TeamSound.NudgeReceived);
                    _view.ShowFeedback(teamEvent.State.DisplayName + (teamEvent.TargetMemberId == null
                        ? "님이 팀을 호출했습니다."
                        : "님이 콕 찔렀습니다."));
                    // The overlay is usually behind something when a nudge lands,
                    // so the sound alone would be missed.
                    _window.FlashTaskbar();
                }
            }

            var now = Time.unscaledTime;
            if (now >= _nextTeamStatePoll)
            {
                _nextTeamStatePoll = now + TeamStatePollSeconds;
                RefreshStateWithoutWaiting();

                // The check-in state is read once and then only after claiming,
                // so without this the button would still say "taken" the morning
                // after. Six o'clock is when the team's day turns over, and the
                // person watching should see the button change on its own. The
                // same check picks up a read that failed at sign-in, which would
                // otherwise leave the button hidden until tomorrow.
                if (_view != null && _dailyCheckInDay != TeamDay.Today &&
                    now >= _nextDailyCheckInAttempt)
                {
                    _nextDailyCheckInAttempt = now + DailyCheckInRetrySeconds;
                    LoadDailyCheckIn();
                }
            }

            if (now >= _nextHeartbeat)
            {
                _nextHeartbeat = now + HeartbeatSeconds;
                SendHeartbeatWithoutWaiting();
            }

            if (now >= _nextIdleCheck)
            {
                _nextIdleCheck = now + IdleCheckSeconds;
                ApplyIdleActivity();
            }

            if (DashboardShortcutPressed())
            {
                ToggleDashboard();
            }

            if (OverlayIsBeingRead())
            {
                // The notes are on the cards in front of them, so there is
                // nothing left to announce.
                _unreadNotes.ClearAll();
            }

            if (_members != null && now >= _nextTimerRefresh)
            {
                _nextTimerRefresh = now + 0.2f;
                _view.Bind(_members, _backend.LocalMemberId, DateTimeOffset.UtcNow, _unreadNotes);
            }
        }

        private void HandleFirstRunNameSubmitted(string submittedName)
        {
            if (_renamePending)
            {
                HandleRenameSubmitted(submittedName);
                return;
            }

            SignInWithName(submittedName);
        }

        private void HandleRenameCancelled()
        {
            if (!_renamePending || _renameInProgress)
            {
                return;
            }

            _renamePending = false;
            _firstRunNameView.Hide();
        }

        /// <summary>
        /// The rename is one request: the server moves the member row and the Auth
        /// credentials together or moves neither, so there is no state where the
        /// account and its name disagree. What can still come apart is the local
        /// pointer, written after. If that write fails the next launch cannot sign
        /// in, and typing the new name on the sign-in screen puts it right.
        /// </summary>
        private async void HandleRenameSubmitted(string submittedName)
        {
            if (_renameInProgress || _backend == null || _view == null || _quitting)
            {
                return;
            }

            var validation = DisplayNamePolicy.Validate(submittedName);
            if (!validation.IsValid)
            {
                _firstRunNameView.ShowError(DisplayNameError(validation.Error));
                return;
            }

            if (!(_backend is ITeamMemberRename rename))
            {
                _firstRunNameView.ShowError("현재 백엔드는 이름 변경을 지원하지 않습니다.");
                return;
            }

            // Renaming to the name you already have is a way of closing the modal,
            // not a request the server should be asked to refuse as taken.
            if (string.Equals(
                    validation.UniqueNameKey,
                    _identityProfile?.UniqueNameKey,
                    StringComparison.Ordinal))
            {
                HandleRenameCancelled();
                return;
            }

            _renameInProgress = true;
            _firstRunNameView.SetBusy(true);
            try
            {
                await rename.RenameAsync(validation.DisplayName, _lifetime.Token);
                _identityProfile = _identityStore.Rename(validation.DisplayName);
                _renamePending = false;
                _firstRunNameView.Hide();
                await RefreshStateAsync();
                _view?.ShowFeedback(
                    "이름을 " + validation.DisplayName + "(으)로 바꿨습니다. 기록은 그대로입니다.");
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _firstRunNameView.ShowError(BackendError(exception));
            }
            finally
            {
                _renameInProgress = false;
                _firstRunNameView.SetBusy(false);
            }
        }

        private async void SignInWithName(string submittedName)
        {
            if (_identityActivationInProgress || _signOutInProgress || _backend != null || _quitting)
            {
                return;
            }

            var validation = DisplayNamePolicy.Validate(submittedName);
            if (!validation.IsValid)
            {
                _firstRunNameView.ShowError(DisplayNameError(validation.Error));
                return;
            }

            _identityActivationInProgress = true;
            _firstRunNameView.SetBusy(true);
            try
            {
                var remoteMember = await SignInAsync(validation, _lifetime.Token);
                var profile = EnsureLocalProfile(validation, remoteMember);
                ActivateIdentity(profile, remoteMember);
                await RefreshStateAsync();
                _view.ShowFeedback(profile.DisplayName + " 이름으로 로그인했습니다.");
                _firstRunNameView.Hide();
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (SupabaseApiException exception)
            {
                Debug.LogException(exception);
                ReportSignInFailure(SupabaseRegistrationError(exception));
            }
            catch (SupabaseIdentityRecoveryException exception)
            {
                Debug.LogException(exception);
                ReportSignInFailure(exception.Message);
            }
            catch (InvalidDataException exception)
            {
                Debug.LogException(exception);
                ReportSignInFailure("기존 이름 정보가 손상되어 새 ID를 만들지 않았습니다. 저장 파일을 복구해주세요.");
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogException(exception);
                ReportSignInFailure("이름을 저장할 권한이 없습니다. 폴더 권한을 확인해주세요.");
            }
            catch (IOException exception)
            {
                Debug.LogException(exception);
                ReportSignInFailure("이름을 저장하지 못했습니다: " + exception.Message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ReportSignInFailure(IdentityStartupError(exception));
            }
            finally
            {
                _identityActivationInProgress = false;
                _firstRunNameView.SetBusy(false);
            }
        }

        /// <summary>
        /// A sign-in failure after the overlay is already up belongs on the overlay,
        /// not on a modal stacked over a working session.
        /// </summary>
        private void ReportSignInFailure(string message)
        {
            if (_view != null)
            {
                _firstRunNameView.Hide();
                _view.ShowFeedback(message, true);
                return;
            }

            _firstRunNameView.ShowError(message);
        }

        /// <summary>
        /// Signs out and returns to the name screen. The visible name is the member
        /// identity, so this is a re-login rather than a rename: the same name signs
        /// back into the same member and keeps accumulating, a different name signs
        /// in as a different member.
        /// </summary>
        /// <summary>
        /// Double clicking your own name used to sign you out and back in as
        /// somebody new, which left every session, interval and check-in behind
        /// under the old name. It renames in place now. What it no longer offers
        /// is signing in as a different member on this PC; nothing asked for that
        /// except the rename it was standing in for.
        /// </summary>
        private void HandleSwitchAccountRequested()
        {
            if (_quitting || _signOutInProgress || _identityActivationInProgress ||
                _renamePending || _backend == null || _view == null)
            {
                return;
            }

            if (!(_backend is ITeamMemberRename))
            {
                _view.ShowFeedback("현재 백엔드는 이름 변경을 지원하지 않습니다.", true);
                return;
            }

            _renamePending = true;
            _firstRunNameView.ShowForRename(_identityProfile?.DisplayName);
        }

        /// <summary>
        /// Signs in as the account the entered name designates. The name is the
        /// account, so the same name reaches the same member from any PC and the
        /// stored session is only a cache that saves one round trip.
        /// </summary>
        private async Task<SupabaseMemberRecord> SignInAsync(
            DisplayNameValidationResult validation,
            CancellationToken cancellationToken)
        {
            if (!validation.IsValid)
            {
                throw new ArgumentException("The display name is invalid.", nameof(validation));
            }

            var sessionStore = new WindowsCredentialSupabaseAuthSessionStore(
                CredentialTargetFor(validation.UniqueNameKey));
            _supabaseIdentity = new SupabaseIdentityClient(
                SupabaseProjectConfig.ProjectUrl,
                SupabaseProjectConfig.PublishableKey,
                _supabaseTransport,
                sessionStore);

            var bootstrap = await _supabaseIdentity.InitializeForNameAsync(validation, cancellationToken);
            if (bootstrap.Member != null)
            {
                return bootstrap.Member;
            }

            // The account exists but holds no slot yet. Checking capacity first
            // keeps a full team from turning every attempt into an Auth user that
            // owns nothing and that the client has no permission to delete.
            var capacity = await _supabaseIdentity.GetTeamCapacityAsync(cancellationToken);
            if (!capacity.HasRoom)
            {
                throw new SupabaseIdentityRecoveryException(
                    "팀의 " + capacity.Capacity + "자리가 모두 찼습니다. 기존 팀원이 나가야 합류할 수 있습니다.",
                    null);
            }

            try
            {
                return await _supabaseIdentity.ClaimMemberNameAsync(validation.DisplayName, cancellationToken);
            }
            catch (SupabaseApiException) when (bootstrap.CreatedAnonymousUser)
            {
                // This launch created the account and the claim still failed, so it
                // owns nothing. Drop the cached session rather than keeping a
                // credential for an account that never joined.
                DiscardUnclaimedCredential(sessionStore);
                throw;
            }
        }

        private LocalIdentityProfile EnsureLocalProfile(
            DisplayNameValidationResult validation,
            SupabaseMemberRecord remoteMember)
        {
            var current = _identityStore.Load();
            if (current.HasProfile)
            {
                if (string.Equals(current.Profile.UniqueNameKey, validation.UniqueNameKey, StringComparison.Ordinal))
                {
                    return current.Profile;
                }

                // The sign-in screen is only reachable while signed out, so a stale
                // pointer to a different name is replaced rather than blocking login.
                _identityStore.Clear();
            }

            return _identityStore.Create(remoteMember.DisplayName);
        }

        private void ActivateIdentity(LocalIdentityProfile profile, SupabaseMemberRecord remoteMember)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (remoteMember == null)
            {
                throw new ArgumentNullException(nameof(remoteMember));
            }

            if (_backend != null)
            {
                return;
            }

            if (_mainViewPrefab == null)
            {
                throw new InvalidOperationException("DOTORIONApp is missing its main view prefab reference.");
            }

            _identityProfile = profile;
            _backend = CreateBackend(profile, remoteMember);
            _mockControls = _backend as IMockTeamBackendControls;
            _eventSubscription = _backend.Events.Subscribe(new TeamEventObserver(_pendingEvents));
            _nextTeamStatePoll = Time.unscaledTime + TeamStatePollSeconds;
            _nextHeartbeat = Time.unscaledTime + HeartbeatSeconds;

            _view = Instantiate(_mainViewPrefab, transform);
            _view.name = _mainViewPrefab.name;
            _view.Initialize(_window.BeginWindowDrag);
            _view.CheckInRequested += HandleCheckInRequested;
            _view.CheckOutRequested += HandleCheckOutRequested;
            _view.ActivityChangeRequested += HandleActivityChangeRequested;
            _view.FakeCheckInRequested += HandleFakeCheckInRequested;
            _view.SettingsToggleRequested += HandleSettingsToggleRequested;
            _view.AlwaysOnTopToggleRequested += HandleAlwaysOnTopToggleRequested;
            _view.MuteToggleRequested += HandleMuteToggleRequested;
            _view.AutoStartToggleRequested += HandleAutoStartToggleRequested;
            _view.MinimizeRequested += HandleMinimizeRequested;
            _view.ExitRequested += HandleClockOutAndExitRequested;
            _view.SwitchAccountRequested += HandleSwitchAccountRequested;
            _view.StatsToggleRequested += HandleStatsToggleRequested;
            _view.StatisticsPeriodChangeRequested += HandleStatisticsPeriodChangeRequested;
            _view.NudgeRequested += HandleNudgeRequested;
            _view.StatusNoteSubmitted += HandleStatusNoteSubmitted;
            _view.AvatarPickerRequested += HandleAvatarPickerRequested;
            _view.AvatarPickerCloseRequested += CloseAvatarPicker;
            _view.AvatarPicked += HandleAvatarPicked;
            _view.MiniModeRequested += HandleMiniModeRequested;
            _view.DailyCheckInRequested += HandleDailyCheckInRequested;
            if (_view.Dashboard != null)
            {
                _view.Dashboard.RefreshRequested += LoadDashboard;
                _view.Dashboard.DeleteConfirmed += HandleDeleteMemberConfirmed;
                _view.Dashboard.SignOutRequested += HandleDashboardSignOutRequested;
                _view.Dashboard.CloseRequested += CloseDashboard;
            }
            _view.MiniModeExitRequested += HandleMiniModeExitRequested;
            _view.SetAvatarCatalog(_avatarCatalog);
            _view.SetAlwaysOnTop(_window.IsAlwaysOnTop);
            _view.SetMuted(_tonePlayer.IsMuted);
            _view.SetAutoStart(WindowsStartupRegistration.IsEnabled());
            _firstRunNameView.Hide();
            CheckForUpdate();
            _view.SetDailyCheckIn(null, _backend is ITeamCheckIn);
            LoadDailyCheckIn();
            if (PlayerPrefs.GetInt(MiniModePreferenceKey, 0) == 1)
            {
                ApplyMiniMode(true);
            }
        }

        /// <summary>
        /// The mock backend stays reachable so the overlay can be exercised in the
        /// Editor without a network, but the shipped default is the live backend.
        /// </summary>
        private ITeamBackend CreateBackend(LocalIdentityProfile profile, SupabaseMemberRecord remoteMember)
        {
            if (_useMockBackend)
            {
                return new ProfiledMockTeamBackend(profile.DisplayName);
            }

            return new SupabaseTeamBackend(
                SupabaseProjectConfig.ProjectUrl,
                SupabaseProjectConfig.PublishableKey,
                _supabaseTransport,
                _supabaseIdentity,
                remoteMember.Id,
                profile.ClientInstanceId);
        }

        private void TeardownSession()
        {
            _idlePolicy.Reset();
            CloseDashboard();
            CloseSettingsPanel();
            // The name screen is a full size window, and the stored preference is
            // left alone so signing back in comes up the way it was left.
            ApplyMiniMode(false);
            CloseStatisticsPanel();
            CloseAvatarPicker();
            // Nothing still in flight may bind into the next session's panel.
            _statisticsRequestId++;
            if (_view != null)
            {
                DetachView();
                Destroy(_view.gameObject);
                _view = null;
            }

            _eventSubscription?.Dispose();
            _eventSubscription = null;
            (_backend as IDisposable)?.Dispose();
            _backend = null;
            _mockControls = null;
            _supabaseIdentity = null;
            _identityProfile = null;
            _members = null;
            _nextTimerRefresh = 0f;
            _nextTeamStatePoll = 0f;
            _nextHeartbeat = 0f;
            _refreshInProgress = false;
            _heartbeatInProgress = false;
            _consecutivePollFailures = 0;
            while (_pendingEvents.TryDequeue(out _))
            {
            }
        }

        private void DetachView()
        {
            _view.CheckInRequested -= HandleCheckInRequested;
            _view.CheckOutRequested -= HandleCheckOutRequested;
            _view.ActivityChangeRequested -= HandleActivityChangeRequested;
            _view.FakeCheckInRequested -= HandleFakeCheckInRequested;
            _view.SettingsToggleRequested -= HandleSettingsToggleRequested;
            _view.AlwaysOnTopToggleRequested -= HandleAlwaysOnTopToggleRequested;
            _view.MuteToggleRequested -= HandleMuteToggleRequested;
            _view.AutoStartToggleRequested -= HandleAutoStartToggleRequested;
            _view.MinimizeRequested -= HandleMinimizeRequested;
            _view.ExitRequested -= HandleClockOutAndExitRequested;
            _view.SwitchAccountRequested -= HandleSwitchAccountRequested;
            _view.StatsToggleRequested -= HandleStatsToggleRequested;
            _view.StatisticsPeriodChangeRequested -= HandleStatisticsPeriodChangeRequested;
            _view.NudgeRequested -= HandleNudgeRequested;
            _view.StatusNoteSubmitted -= HandleStatusNoteSubmitted;
            _view.AvatarPickerRequested -= HandleAvatarPickerRequested;
            _view.AvatarPickerCloseRequested -= CloseAvatarPicker;
            _view.AvatarPicked -= HandleAvatarPicked;
            _view.MiniModeRequested -= HandleMiniModeRequested;
            _view.DailyCheckInRequested -= HandleDailyCheckInRequested;
            if (_view.Dashboard != null)
            {
                _view.Dashboard.RefreshRequested -= LoadDashboard;
                _view.Dashboard.DeleteConfirmed -= HandleDeleteMemberConfirmed;
                _view.Dashboard.SignOutRequested -= HandleDashboardSignOutRequested;
                _view.Dashboard.CloseRequested -= CloseDashboard;
            }
            _view.MiniModeExitRequested -= HandleMiniModeExitRequested;
        }

        private MemberState LocalMember()
        {
            if (_members == null || _backend == null)
            {
                return null;
            }

            foreach (var member in _members)
            {
                if (string.Equals(member.MemberId, _backend.LocalMemberId, StringComparison.Ordinal))
                {
                    return member;
                }
            }

            return null;
        }

        private bool IsLocalMemberClockedIn()
        {
            var localMember = LocalMember();
            return localMember != null && localMember.AttendanceStatus == AttendanceStatus.ClockedIn;
        }

        private void HandleCheckInRequested()
        {
            RunMutation(token => _backend.CheckInAsync(token), "출근했습니다. 기본 상태는 작업중입니다.");
        }

        private void HandleCheckOutRequested()
        {
            RunMutation(
                token => _backend.CheckOutAsync(CheckoutReason.Manual, token),
                "퇴근했습니다.",
                ShowTodaySummary);
        }

        /// <summary>
        /// Replaces the plain "퇴근했습니다" with what the day came to. The numbers
        /// are the ones the statistics panel already reports, asked for after the
        /// checkout has landed so the session just closed is counted in them.
        /// A backend without history, or a day the server has nothing for, simply
        /// leaves the original message alone.
        /// </summary>
        private async void ShowTodaySummary()
        {
            var requestedBackend = _backend;
            if (!(requestedBackend is ITeamStatistics statistics))
            {
                return;
            }

            try
            {
                var range = StatisticsRange.Resolve(StatisticsPeriod.LastSevenDays, TeamDay.Today);
                var stats = await statistics.GetPeriodStatsAsync(
                    requestedBackend.LocalMemberId,
                    range,
                    _lifetime.Token);
                if (_view == null || !ReferenceEquals(_backend, requestedBackend))
                {
                    return;
                }

                var today = TodayFrom(stats);
                if (today != null)
                {
                    _view.ShowFeedback(TodaySummary(today));
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                // A summary is a nicety on top of a checkout that already
                // succeeded, so a failure here stays in the log.
                Debug.LogWarning("Could not load the checkout summary: " + exception.Message);
            }
        }

        private static MemberPeriodStat TodayFrom(IReadOnlyList<MemberPeriodStat> stats)
        {
            if (stats == null)
            {
                return null;
            }

            var today = TeamDay.Today;
            foreach (var stat in stats)
            {
                if (stat.BucketStart == today && stat.HasActivity)
                {
                    return stat;
                }
            }

            return null;
        }

        /// <summary>
        /// Break and meal are dropped when they are zero rather than shown as
        /// 00:00: the line has to fit the feedback strip, and a zero says nothing
        /// the reader did not already know.
        /// </summary>
        private static string TodaySummary(MemberPeriodStat today)
        {
            var summary = "오늘 근무 " + TeamPeriodStatRowView.FormatDuration(today.AttendanceSeconds) +
                          " · 작업 " + TeamPeriodStatRowView.FormatDuration(today.WorkSeconds);
            if (today.BreakSeconds > 0)
            {
                summary += " · 휴식 " + TeamPeriodStatRowView.FormatDuration(today.BreakSeconds);
            }

            if (today.MealSeconds > 0)
            {
                summary += " · 식사 " + TeamPeriodStatRowView.FormatDuration(today.MealSeconds);
            }

            return summary;
        }

        private static IEnumerable<string> MembersWithNotes(IReadOnlyList<MemberState> members)
        {
            foreach (var member in members)
            {
                if (!string.IsNullOrWhiteSpace(member.StatusNote))
                {
                    yield return member.MemberId;
                }
            }
        }

        /// <summary>
        /// Whether someone is actually looking at the notes. The mini overlay has
        /// no room for one, and a window behind something else is not being read
        /// however focused the app thinks it is.
        /// </summary>
        private bool OverlayIsBeingRead()
        {
            return Application.isFocused
                && _view != null
                && !_view.IsMiniModeVisible
                && !_view.IsDashboardVisible;
        }

        /// <summary>
        /// Lets the desk going quiet, and waking up again, move the local status.
        /// The rules live in <see cref="IdleActivityPolicy"/>; what is here is the
        /// state it needs and the two requests it can ask for.
        /// </summary>
        private void ApplyIdleActivity()
        {
            var localMember = LocalMember();
            if (localMember == null || localMember.AttendanceStatus != AttendanceStatus.ClockedIn)
            {
                // Nothing to move, and the next session must not be resumed out of
                // a break this one entered.
                _idlePolicy.Reset();
                return;
            }

            if (_mutationInProgress || _quitting || _signOutInProgress)
            {
                return;
            }

            switch (_idlePolicy.Evaluate(_window.IdleSeconds, localMember.ActivityStatus.GetValueOrDefault()))
            {
                case IdleActivityAction.StartBreak:
                    RunMutation(
                        token => _backend.ChangeActivityAsync(ActivityStatus.Break, token),
                        "자리를 비운 것 같아 쉬는중으로 바꿨습니다.");
                    break;
                case IdleActivityAction.ResumeWork:
                    RunMutation(
                        token => _backend.ChangeActivityAsync(ActivityStatus.Working, token),
                        "돌아오신 것 같아 작업중으로 되돌렸습니다.");
                    break;
            }
        }

        private void HandleActivityChangeRequested(ActivityStatus status)
        {
            RunMutation(
                token => _backend.ChangeActivityAsync(status, token),
                "상태를 " + ActivityLabel(status) + "(으)로 바꿨습니다.");
        }

        private void HandleFakeCheckInRequested()
        {
            if (_mockControls == null)
            {
                return;
            }

            RunMutation(
                token => _mockControls.TriggerFakeTeammateCheckInAsync(token),
                "가짜 팀원 출근 이벤트를 발생시켰습니다.");
        }

        private void HandleStatusNoteSubmitted(string note)
        {
            var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (string.Equals(LocalStatusNote(), trimmed, StringComparison.Ordinal))
            {
                // onEndEdit also fires when the field merely loses focus, so an
                // unchanged note must not cost a request.
                return;
            }

            RunMutation(
                token => _backend.SetStatusNoteAsync(trimmed, token),
                trimmed == null ? "메모를 지웠습니다." : "메모를 남겼습니다.");
        }

        private string LocalStatusNote()
        {
            return LocalMember()?.StatusNote;
        }

        /// <summary>
        /// The settings panel grows the window downwards the way the statistics
        /// one does, so the two cannot be open at once, and neither can be open
        /// while a panel that moves the window some other way is.
        /// </summary>
        private void HandleSettingsToggleRequested()
        {
            if (_view == null || _quitting || _signOutInProgress)
            {
                return;
            }

            if (_view.IsSettingsVisible)
            {
                CloseSettingsPanel();
                return;
            }

            CloseStatisticsPanel();
            CloseAvatarPicker();
            CloseDashboard();
            if (_view.IsMiniModeVisible)
            {
                ApplyMiniMode(false);
                PersistMiniMode(false);
            }

            _view.SetSettingsVisible(true);
            _window.ExpandForSettings();
        }

        private void CloseSettingsPanel()
        {
            _view?.SetSettingsVisible(false);
            _window?.CollapseSettings();
        }

        private void HandleMuteToggleRequested()
        {
            if (_view == null || _tonePlayer == null)
            {
                return;
            }

            var muted = !_tonePlayer.IsMuted;
            _tonePlayer.SetMuted(muted);
            PlayerPrefs.SetInt(MutePreferenceKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            _view.SetMuted(muted);
            _view.ShowFeedback(muted ? "알림음을 껐습니다." : "알림음을 켰습니다.");
        }

        private void HandleAlwaysOnTopToggleRequested()
        {
            _window.ToggleAlwaysOnTop();
            _view.SetAlwaysOnTop(_window.IsAlwaysOnTop);
            _view.ShowFeedback(_window.IsAlwaysOnTop ? "항상 위를 켰습니다." : "항상 위를 껐습니다.");
        }

        /// <summary>
        /// The one switch here with nothing behind it in PlayerPrefs. The
        /// registry entry is the setting, so the state is read back rather than
        /// tracked, and a write that Windows refuses puts the row back to
        /// whatever it actually says instead of leaving it lying.
        /// </summary>
        private void HandleAutoStartToggleRequested()
        {
            if (_view == null)
            {
                return;
            }

            if (!WindowsStartupRegistration.IsSupported)
            {
                _view.ShowFeedback("자동 시작은 윈도우 빌드에서만 됩니다.", true);
                return;
            }

            var enable = !WindowsStartupRegistration.IsEnabled();
            if (!WindowsStartupRegistration.SetEnabled(enable))
            {
                _view.SetAutoStart(WindowsStartupRegistration.IsEnabled());
                _view.ShowFeedback("자동 시작을 바꾸지 못했습니다.", true);
                return;
            }

            _view.SetAutoStart(enable);
            _view.ShowFeedback(enable
                ? "윈도우를 켤 때 같이 실행합니다."
                : "윈도우를 켤 때 실행하지 않습니다.");
        }

        /// <summary>
        /// Asks GitHub what the newest release is, once, after the overlay is up.
        ///
        /// Nothing is said when this fails. Somebody working without a route to
        /// GitHub would otherwise be told so every single time they start, about
        /// a thing they did not ask for.
        /// </summary>
        private async void CheckForUpdate()
        {
            if (_updatePrompt == null || !WindowsUpdateApplier.IsSupported)
            {
                return;
            }

            try
            {
                _updateDownloader = _updateDownloader ?? new UpdateDownloader();
                var manifest = await _updateDownloader.FetchManifestAsync(_lifetime.Token);
                if (_quitting || _updatePrompt == null)
                {
                    return;
                }

                if (UpdateCheck.Evaluate(Application.version, manifest, out var offered)
                    != UpdateAvailability.Available)
                {
                    return;
                }

                _offeredUpdate = manifest;
                _updatePrompt.Show(offered.ToString());
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                Debug.LogWarning("Update check failed: " + exception.Message);
            }
        }

        /// <summary>
        /// Downloads the release, starts the helper, and only then quits.
        ///
        /// The order matters both ways round. Quitting before the helper exists
        /// would leave the person with a closed app and no update; starting the
        /// helper and then staying up would leave it waiting on a process that
        /// never goes away.
        /// </summary>
        private async void HandleUpdateConfirmed()
        {
            if (_updateInProgress || _offeredUpdate == null || _updatePrompt == null)
            {
                return;
            }

            _updateInProgress = true;
            try
            {
                var progress = new Progress<float>(fraction => _updatePrompt?.ShowProgress(fraction));
                var zipPath = await _updateDownloader.DownloadAsync(
                    _offeredUpdate, WindowsUpdateApplier.WorkFolder, progress, _lifetime.Token);

                if (_quitting || _updatePrompt == null)
                {
                    return;
                }

                var script = Resources.Load<TextAsset>("DOTORION/ApplyUpdate.ps1");
                if (!WindowsUpdateApplier.Launch(zipPath, script != null ? script.text : null))
                {
                    _updateInProgress = false;
                    _updatePrompt.ShowError("업데이트를 시작하지 못했습니다.");
                    return;
                }

                _updatePrompt.ShowApplying();
                // The ordinary exit: the clock-out still has to happen, and the
                // helper is already waiting for this process to be gone.
                HandleClockOutAndExitRequested();
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                _updateInProgress = false;
                _updatePrompt?.ShowError("내려받지 못했습니다. 잠시 뒤 다시 시도해 주세요.");
                Debug.LogWarning("Update download failed: " + exception.Message);
            }
        }

        /// <summary>
        /// Not now. Nothing is remembered - the check runs again on the next
        /// start, which is the only reminder an overlay like this should give.
        /// </summary>
        private void HandleUpdateDismissed()
        {
            _updatePrompt?.Hide();
        }

        private void HandleMinimizeRequested()
        {
            _window.Minimize();
        }

        /// <summary>
        /// Reads where the check-in stands so the button opens in the right state
        /// rather than the person finding out by pressing it.
        /// </summary>
        private async void LoadDailyCheckIn()
        {
            var requestedBackend = _backend;
            if (!(requestedBackend is ITeamCheckIn checkIn))
            {
                // Nothing to read and nothing to retry: the button is hidden for
                // good on a backend that has no check-in at all.
                _dailyCheckInDay = TeamDay.Today;
                return;
            }

            try
            {
                var state = await checkIn.GetDailyCheckInStateAsync(_lifetime.Token);
                if (_view != null && ReferenceEquals(_backend, requestedBackend))
                {
                    _dailyCheckInDay = TeamDay.Today;
                    _view.SetDailyCheckIn(state, true);
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                // The overlay is perfectly usable without knowing, so a failure
                // here leaves the button alone instead of interrupting anyone.
                Debug.LogWarning("Could not read the daily check-in: " + exception.Message);
            }
        }

        private async void HandleDailyCheckInRequested()
        {
            var requestedBackend = _backend;
            if (_view == null || _quitting || _signOutInProgress ||
                !(requestedBackend is ITeamCheckIn checkIn))
            {
                return;
            }

            try
            {
                var state = await checkIn.ClaimDailyCheckInAsync(_lifetime.Token);
                if (_view == null || !ReferenceEquals(_backend, requestedBackend))
                {
                    return;
                }

                _view.SetDailyCheckIn(state, true);
                // Pressing twice is an ordinary thing to do, so the second press
                // reports where things stand rather than reading as a failure.
                var streak = state.StreakDays > 0 ? " · " + state.StreakDays + "일 연속" : string.Empty;
                _view.ShowFeedback(state.AwardedPoints > 0
                    ? "출석했습니다. +" + state.AwardedPoints + "P (누적 " + state.TotalPoints + "P)" + streak
                    : "오늘은 이미 출석했습니다. 누적 " + state.TotalPoints + "P" + streak);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _view?.ShowFeedback(BackendError(exception), true);
            }
        }

        /// <summary>
        /// Ctrl+Shift+D, read straight from the keyboard rather than through a
        /// button, because the dashboard is not part of the overlay anybody uses.
        /// The new Input System is the only handler this project builds with, so
        /// there is no legacy Input to fall back to.
        /// </summary>
        private static bool DashboardShortcutPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null
                && keyboard.dKey.wasPressedThisFrame
                && keyboard.ctrlKey.isPressed
                && keyboard.shiftKey.isPressed;
        }

        private void ToggleDashboard()
        {
            if (_view == null || _backend == null || _quitting || _signOutInProgress)
            {
                return;
            }

            if (_view.IsDashboardVisible)
            {
                CloseDashboard();
                return;
            }

            if (!(_backend is ITeamAdmin))
            {
                _view.ShowFeedback("현재 백엔드는 대시보드를 지원하지 않습니다.", true);
                return;
            }

            // The panels below all move the window themselves, so none of them may
            // be left open while the dashboard owns the window rect.
            CloseStatisticsPanel();
            CloseSettingsPanel();
            CloseAvatarPicker();
            if (_view.IsMiniModeVisible)
            {
                ApplyMiniMode(false);
                PersistMiniMode(false);
            }

            _view.SetDashboardVisible(true);
            _window.ExpandForDashboard();
            LoadDashboard();
        }

        private void CloseDashboard()
        {
            _view?.SetDashboardVisible(false);
            _window?.CollapseDashboard();
        }

        private async void LoadDashboard()
        {
            var requestedBackend = _backend;
            if (_view?.Dashboard == null || !(requestedBackend is ITeamAdmin admin))
            {
                return;
            }

            _dashboardBusy = true;
            _view.Dashboard.SetBusy(true);
            _view.Dashboard.ShowFeedback("불러오는 중…", false);
            try
            {
                var members = await admin.GetMemberOverviewAsync(_lifetime.Token);
                if (_view?.Dashboard == null || !ReferenceEquals(_backend, requestedBackend))
                {
                    return;
                }

                _view.Dashboard.Bind(members, requestedBackend.LocalMemberId);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _view?.Dashboard?.ShowFeedback(BackendError(exception), true);
            }
            finally
            {
                _dashboardBusy = false;
                _view?.Dashboard?.SetBusy(false);
            }
        }

        /// <summary>
        /// The view has already asked, naming the member. There is no undo, and
        /// the server refuses to erase the caller.
        /// </summary>
        private async void HandleDeleteMemberConfirmed(string memberId)
        {
            var requestedBackend = _backend;
            if (_dashboardBusy || _view?.Dashboard == null || !(requestedBackend is ITeamAdmin admin))
            {
                return;
            }

            _dashboardBusy = true;
            _view.Dashboard.SetBusy(true);
            try
            {
                await admin.DeleteMemberAsync(memberId, _lifetime.Token);
                if (!ReferenceEquals(_backend, requestedBackend))
                {
                    return;
                }

                await RefreshStateAsync();
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _view?.Dashboard?.ShowFeedback(BackendError(exception), true);
            }
            finally
            {
                _dashboardBusy = false;
                _view?.Dashboard?.SetBusy(false);
            }

            LoadDashboard();
        }

        /// <summary>
        /// Forgets which member this PC is signed in as and returns to the name
        /// screen, where any existing name signs back in to that member. The
        /// session is closed first: it was opened by this install's client
        /// instance id, and signing back in mints a new one that the server would
        /// refuse to heartbeat or close it with.
        /// </summary>
        private async void HandleDashboardSignOutRequested()
        {
            if (_quitting || _signOutInProgress || _identityActivationInProgress || _backend == null)
            {
                return;
            }

            _signOutInProgress = true;
            var previousName = _identityProfile?.DisplayName;
            _view?.Dashboard?.SetBusy(true);
            try
            {
                try
                {
                    if (IsLocalMemberClockedIn())
                    {
                        await _backend.CheckOutAsync(CheckoutReason.Manual, _lifetime.Token);
                    }
                }
                catch (Exception exception) when (!(exception is OperationCanceledException))
                {
                    Debug.LogWarning("Best-effort checkout before sign-out failed: " + exception.Message);
                }

                CloseDashboard();
                TeardownSession();
                _identityStore.Clear();
                _firstRunNameView.Show(previousName);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _view?.Dashboard?.ShowFeedback("로그아웃하지 못했습니다: " + exception.Message, true);
            }
            finally
            {
                _signOutInProgress = false;
            }
        }

        private void HandleMiniModeRequested()
        {
            if (_view == null || _quitting || _signOutInProgress)
            {
                return;
            }

            ApplyMiniMode(true);
            PersistMiniMode(true);
        }

        private void HandleMiniModeExitRequested()
        {
            if (_view == null || _quitting)
            {
                return;
            }

            ApplyMiniMode(false);
            PersistMiniMode(false);
        }

        /// <summary>
        /// The statistics panel and the avatar picker each move the window
        /// themselves, so neither may be left open while the mini overlay owns
        /// the window rect.
        /// </summary>
        private void ApplyMiniMode(bool enabled)
        {
            if (enabled)
            {
                CloseStatisticsPanel();
                CloseSettingsPanel();
                CloseAvatarPicker();
            }

            _view?.SetMiniModeVisible(enabled);
            if (enabled)
            {
                _window?.EnterMiniMode();
            }
            else
            {
                _window?.ExitMiniMode();
            }
        }

        private static void PersistMiniMode(bool enabled)
        {
            PlayerPrefs.SetInt(MiniModePreferenceKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void HandleStatsToggleRequested()
        {
            if (_view == null || _backend == null || _quitting || _signOutInProgress)
            {
                return;
            }

            if (_view.IsStatisticsVisible)
            {
                CloseStatisticsPanel();
                return;
            }

            CloseSettingsPanel();
            _view.SetStatisticsVisible(true);
            _window.ExpandForStatistics();
            LoadStatistics(_statisticsPeriod);
        }

        private void HandleNudgeRequested(string targetMemberId)
        {
            if (_backend is ITeamNudges nudges)
            {
                RunMutation(
                    token => nudges.SendNudgeAsync(targetMemberId, token),
                    targetMemberId == null ? "팀을 호출했습니다." : "콕 찔렀습니다.");
            }
            else
            {
                _view.ShowFeedback("현재 백엔드는 호출을 지원하지 않습니다.", true);
            }
        }

        private void HandleStatisticsPeriodChangeRequested(StatisticsPeriod period)
        {
            if (_view == null || _backend == null || !_view.IsStatisticsVisible)
            {
                return;
            }

            LoadStatistics(period);
        }

        private async void LoadStatistics(StatisticsPeriod period)
        {
            _statisticsPeriod = period;
            _view.SetStatisticsPeriod(period);
            var range = StatisticsRange.Resolve(period, TeamDay.Today);
            _view.ShowStatisticsLoading(range);

            var requestedBackend = _backend;
            var statistics = requestedBackend as ITeamStatistics;
            if (statistics == null)
            {
                _view.ShowStatisticsError(range, "현재 백엔드는 통계를 지원하지 않습니다.");
                return;
            }

            // Switching period supersedes whatever is still in flight, so a slow
            // answer cannot land on top of a newer one.
            var request = ++_statisticsRequestId;
            try
            {
                var statsTask = statistics.GetPeriodStatsAsync(
                    requestedBackend.LocalMemberId,
                    range,
                    _lifetime.Token);
                var rankingTask = statistics.GetRankingAsync(range, _lifetime.Token);
                await Task.WhenAll(statsTask, rankingTask);

                if (IsCurrentStatisticsRequest(request, requestedBackend))
                {
                    _view.BindStatistics(
                        range,
                        statsTask.Result,
                        rankingTask.Result,
                        requestedBackend.LocalMemberId);
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (IsCurrentStatisticsRequest(request, requestedBackend))
                {
                    _view.ShowStatisticsError(range, "통계를 불러오지 못했습니다: " + exception.Message);
                }
            }
        }

        private bool IsCurrentStatisticsRequest(int request, ITeamBackend requestedBackend)
        {
            return _view != null
                && request == _statisticsRequestId
                && ReferenceEquals(_backend, requestedBackend);
        }

        private void CloseStatisticsPanel()
        {
            _view?.SetStatisticsVisible(false);
            _window?.RestoreCompactHeight();
        }

        /// <summary>
        /// The two panels are mutually exclusive because each one moves the window
        /// itself: opening both would leave the statistics panel's downward growth
        /// and the picker's upward growth fighting over the same window rect.
        /// </summary>
        private void HandleAvatarPickerRequested()
        {
            if (_view == null || _backend == null || _quitting || _signOutInProgress)
            {
                return;
            }

            if (_view.IsAvatarPickerVisible)
            {
                CloseAvatarPicker();
                return;
            }

            if (_view.IsStatisticsVisible)
            {
                CloseStatisticsPanel();
            }

            CloseSettingsPanel();
            _view.SetAvatarPickerVisible(true);
            _window.ExpandForAvatarPicker();
        }

        private void CloseAvatarPicker()
        {
            _view?.SetAvatarPickerVisible(false);
            _window?.CollapseAvatarPicker();
        }

        private void HandleAvatarPicked(string avatarKey)
        {
            RunMutation(
                token => _backend.SetAvatarKeyAsync(avatarKey, token),
                "프로필 아이콘을 바꿨습니다.");
        }

        /// <summary>
        /// Windows is signing out. The shell is holding the shutdown open for us,
        /// so the last checkout gets a real attempt and is recorded as
        /// <see cref="CheckoutReason.OsShutdown"/> instead of being swept up three
        /// minutes later as a stale session.
        /// </summary>
        /// <summary>
        /// The machine is going to sleep with a session still open. The server
        /// already closes those on its own, backdated to the last heartbeat, so
        /// the record is right either way; what this buys is the roster being
        /// right sooner, instead of showing someone as at their desk for up to
        /// the three minutes it takes that sweep to notice.
        ///
        /// Nothing waits for a sleeping machine the way the shell waits for a
        /// shutdown, so this is one attempt with about two seconds to land, and
        /// the app stays running for the other side of the sleep.
        /// </summary>
        private async void HandleSuspendingRequested()
        {
            if (_quitting || _signOutInProgress || _backend == null || !IsLocalMemberClockedIn())
            {
                return;
            }

            try
            {
                // Reported as an OS shutdown because that is what it is from
                // here: the machine is being taken away and this is the last
                // request on the way out. AutoTimeout would claim the server
                // stopped hearing from us, which is a different story.
                await _backend.CheckOutAsync(CheckoutReason.OsShutdown, _lifetime.Token);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                Debug.LogWarning("Best-effort checkout on suspend failed: " + exception.Message);
            }
        }

        private async void HandleSessionEndingRequested()
        {
            if (_quitting)
            {
                return;
            }

            _quitting = true;
            try
            {
                if (_backend != null)
                {
                    await _backend.CheckOutAsync(CheckoutReason.OsShutdown, _lifetime.Token);
                }
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                Debug.LogWarning("Best-effort checkout on session end failed: " + exception.Message);
            }
            finally
            {
                // Release the block first: the person is waiting on a shutdown
                // screen, and the server closes the session on its own if this
                // attempt failed.
                _window.CompleteSessionEnd();
                _window.PrepareForExit();
                Application.Quit();
            }
        }

        private async void HandleClockOutAndExitRequested()
        {
            if (_quitting)
            {
                return;
            }

            _quitting = true;
            try
            {
                if (_backend != null)
                {
                    await _backend.CheckOutAsync(CheckoutReason.AppExit, _lifetime.Token);
                }
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                Debug.LogWarning("Best-effort checkout on exit failed: " + exception.Message);
            }
            finally
            {
                _window.PrepareForExit();
                Application.Quit();
            }
        }

        /// <summary>
        /// <paramref name="onSucceeded"/> runs only after the change landed and
        /// the roster was refreshed, so a follow-up request sees the new state.
        /// </summary>
        private async void RunMutation(
            Func<CancellationToken, Task> mutation,
            string successMessage,
            Action onSucceeded = null)
        {
            if (_mutationInProgress || _quitting || _signOutInProgress || _backend == null || _view == null)
            {
                return;
            }

            _mutationInProgress = true;
            _view.SetBusy(true);
            try
            {
                await mutation(_lifetime.Token);
                await RefreshStateAsync();
                _view?.ShowFeedback(successMessage);
                onSucceeded?.Invoke();
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _view?.ShowFeedback(BackendError(exception), true);
            }
            finally
            {
                _mutationInProgress = false;
                if (_view != null)
                {
                    _view.SetBusy(false);
                }
            }
        }

        private async Task RefreshStateAsync()
        {
            if (_backend == null || _view == null)
            {
                return;
            }

            var members = await _backend.GetTeamStateAsync(_lifetime.Token);
            if (_backend == null || _view == null)
            {
                return;
            }

            _members = members;
            // A note that was withdrawn before anyone looked has nothing left to
            // announce, and neither does a member who went home or left the team.
            _unreadNotes.RetainOnly(MembersWithNotes(members));
            _view.Bind(_members, _backend.LocalMemberId, DateTimeOffset.UtcNow, _unreadNotes);
        }

        /// <summary>
        /// A poll that outlives its interval must not stack another request behind
        /// it, or a slow network turns into an ever-growing queue of requests.
        /// </summary>
        private async void RefreshStateWithoutWaiting()
        {
            if (_refreshInProgress || _mutationInProgress || _signOutInProgress)
            {
                return;
            }

            _refreshInProgress = true;
            try
            {
                await RefreshStateAsync();
                _consecutivePollFailures = 0;
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                // A dead connection would otherwise fill the console with the same
                // exception every three seconds, so back off and log once per
                // streak instead of once per attempt.
                if (_consecutivePollFailures == 0)
                {
                    Debug.LogException(exception);
                }

                _consecutivePollFailures++;
                _nextTeamStatePoll = Time.unscaledTime + PollBackoffSeconds(_consecutivePollFailures);
                _view?.ShowFeedback(BackendError(exception), true);
            }
            finally
            {
                _refreshInProgress = false;
            }
        }

        private static float PollBackoffSeconds(int consecutiveFailures)
        {
            var seconds = TeamStatePollSeconds * (1 << Math.Min(consecutiveFailures, 4));
            return Math.Min(seconds, MaximumPollBackoffSeconds);
        }

        private async void SendHeartbeatWithoutWaiting()
        {
            if (_heartbeatInProgress || _signOutInProgress || _backend == null)
            {
                return;
            }

            _heartbeatInProgress = true;
            try
            {
                await _backend.SendHeartbeatAsync(_lifetime.Token);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                // A missed heartbeat is recoverable: the server only auto-closes a
                // session after three minutes without one, so this stays a warning
                // and never interrupts what the person is doing.
                Debug.LogWarning("Heartbeat failed: " + exception.Message);
            }
            finally
            {
                _heartbeatInProgress = false;
            }
        }

        private static string BackendError(Exception exception)
        {
            if (exception is HttpRequestException || exception is TaskCanceledException)
            {
                return "서버에 연결하지 못했습니다. 다시 시도하는 중입니다.";
            }

            if (exception is SupabaseApiException apiException)
            {
                return AttendanceError(apiException);
            }

            return "요청을 처리하지 못했습니다: " + exception.Message;
        }

        /// <summary>
        /// The attendance RPCs raise stable machine-readable messages. Anything not
        /// listed here falls back to the shared registration wording rather than
        /// showing a raw server code.
        /// </summary>
        private static string AttendanceError(SupabaseApiException exception)
        {
            switch (exception.ServerMessage)
            {
                case "member_already_clocked_in":
                    return "이미 출근 상태입니다.";
                case "member_not_clocked_in":
                    return "출근 상태가 아닙니다.";
                case "attendance_session_not_open":
                    return "출근 세션이 이미 종료되었습니다. 다시 출근해주세요.";
                case "member_name_taken":
                    return "이미 쓰고 있는 이름입니다.";
                case "client_instance_mismatch":
                    return "다른 PC에서 출근한 세션입니다. 그 PC에서 퇴근해주세요.";
                case "member_identity_mismatch":
                    return "로그인 정보가 일치하지 않습니다. 앱을 다시 실행해주세요.";
                case "member_not_registered_or_inactive":
                    return "등록되지 않았거나 비활성화된 계정입니다.";
                default:
                    return SupabaseRegistrationError(exception);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (_firstRunNameView != null)
            {
                _firstRunNameView.Submitted -= HandleFirstRunNameSubmitted;
            }

            if (_view != null)
            {
                DetachView();
            }

            if (_window != null)
            {
                _window.ClockOutAndExitRequested -= HandleClockOutAndExitRequested;
                _window.SessionEndingRequested -= HandleSessionEndingRequested;
                _window.SuspendingRequested -= HandleSuspendingRequested;
            }

            _lifetime?.Cancel();
            _eventSubscription?.Dispose();
            _updateDownloader?.Dispose();
            (_backend as IDisposable)?.Dispose();
            _supabaseTransport?.Dispose();
            _lifetime?.Dispose();
        }

        private static void DiscardUnclaimedCredential(ISupabaseAuthSessionStore sessionStore)
        {
            try
            {
                sessionStore.Delete();
            }
            catch (Exception exception)
            {
                // Losing the cleanup must never mask the claim failure the caller
                // is about to report.
                Debug.LogWarning("Could not discard the unclaimed Auth credential: " + exception.Message);
            }
        }

        private static string CredentialTargetFor(string uniqueNameKey)
        {
            return SupabaseProjectConfig.CredentialTarget + "." + uniqueNameKey;
        }

        private static string IdentityStartupError(Exception exception)
        {
            if (exception is SupabaseIdentityRecoveryException)
            {
                return exception.Message;
            }

            if (exception is SupabaseApiException apiException)
            {
                return SupabaseRegistrationError(apiException);
            }

            if (exception is HttpRequestException || exception is TaskCanceledException)
            {
                return "Supabase에 연결하지 못했습니다. 인터넷 연결을 확인하고 다시 시도해주세요.";
            }

            return "로그인 정보를 확인하지 못했습니다: " + exception.Message;
        }

        private static string SupabaseRegistrationError(SupabaseApiException exception)
        {
            switch (exception.ServerMessage)
            {
                case "nudge_too_soon":
                    return "너무 자주 호출했습니다. 잠시 뒤에 다시 해 주세요.";
                case "member_not_clocked_in":
                    return "출근 중일 때만 호출할 수 있습니다.";
                case "member_name_taken":
                    return "이미 다른 사람이 사용 중인 이름입니다.";
                case "member_name_already_claimed":
                    return "이 계정에는 이미 다른 이름이 등록되어 있습니다.";
                case "team_full":
                    return "팀의 네 자리가 모두 등록되었습니다.";
                case "invalid_member_name":
                    return "서버에서 허용하지 않는 이름입니다.";
                case "authentication_required":
                    return "로그인이 만료되었습니다. 앱을 다시 실행해주세요.";
                default:
                    return "Supabase 요청을 처리하지 못했습니다: " + exception.Message;
            }
        }

        private static string DisplayNameError(DisplayNameValidationError error)
        {
            switch (error)
            {
                case DisplayNameValidationError.Required:
                    return "표시할 이름을 입력해주세요.";
                case DisplayNameValidationError.TooLong:
                    return "이름은 16자 이하로 입력해주세요.";
                case DisplayNameValidationError.UnsupportedCharacter:
                    return "이름에는 한글, 영문, 숫자, 공백, 밑줄과 하이픈만 사용할 수 있습니다.";
                case DisplayNameValidationError.LetterOrNumberRequired:
                    return "이름에는 글자나 숫자가 하나 이상 필요합니다.";
                default:
                    return "사용할 수 없는 이름입니다.";
            }
        }

        private static string RestoredProfileFeedback(IdentityProfileLoadResult loadResult)
        {
            var name = loadResult.Profile.DisplayName;
            if (loadResult.Status == IdentityProfileLoadStatus.RecoveredFromBackup
                || loadResult.Status == IdentityProfileLoadStatus.RecoveredFromTemporaryFile)
            {
                return loadResult.StorageRepairSucceeded
                    ? name + " 프로필을 복구하고 자동 로그인했습니다 · Supabase Auth 연결"
                    : name + " 프로필을 임시 복구했습니다. 저장소 확인이 필요합니다.";
            }

            return name + " 프로필로 자동 로그인했습니다 · Supabase Auth 연결";
        }

        private static string ActivityLabel(ActivityStatus status)
        {
            switch (status)
            {
                case ActivityStatus.Working: return "작업중";
                case ActivityStatus.Break: return "쉬는중";
                case ActivityStatus.Meal: return "식사중";
                default: return status.ToString();
            }
        }

        private sealed class TeamEventObserver : IObserver<TeamEvent>
        {
            private readonly ConcurrentQueue<TeamEvent> _queue;

            public TeamEventObserver(ConcurrentQueue<TeamEvent> queue)
            {
                _queue = queue;
            }

            public void OnNext(TeamEvent value)
            {
                if (value != null)
                {
                    _queue.Enqueue(value);
                }
            }

            public void OnError(Exception error)
            {
                Debug.LogException(error);
            }

            public void OnCompleted()
            {
            }
        }
    }
}

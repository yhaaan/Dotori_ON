using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TeamOverlay.Audio;
using TeamOverlay.Backend.Mock;
using TeamOverlay.Core;
using TeamOverlay.Identity;
using TeamOverlay.Platform.Windows;
using TeamOverlay.Supabase;
using UnityEngine;

namespace TeamOverlay.UI
{
    /// <summary>
    /// Composition root. Owns identity, the backend and the two editable UI
    /// prefabs, and moves the application between the sign-in screen and the
    /// overlay.
    /// </summary>
    public sealed class TeamOverlayApp : MonoBehaviour
    {
        private const string AppPrefabResourcePath = "TeamOverlay/TeamOverlayApp";

        // Polling stands in for a Realtime subscription. Four members at three
        // seconds is a handful of requests a minute, and a dropped poll self-heals
        // on the next tick instead of leaving the roster stale until a reconnect.
        private const float TeamStatePollSeconds = 3f;

        // Comfortably inside the server's three-minute stale-session timeout, so a
        // single failed heartbeat never clocks anyone out.
        private const float HeartbeatSeconds = 45f;

        private const float MaximumPollBackoffSeconds = 30f;

        [Header("Prefab references")]
        [SerializeField] private TeamOverlayView _mainViewPrefab;
        [SerializeField] private FirstRunNameView _firstRunNamePrefab;
        [Header("Development")]
        [Tooltip("Runs the overlay against the in-memory roster instead of Supabase. Identity still uses the real project.")]
        [SerializeField] private bool _useMockBackend;

        private static TeamOverlayApp _instance;

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
        private TeamOverlayView _view;
        private FirstRunNameView _firstRunNameView;
        private NotificationTonePlayer _tonePlayer;
        private WindowsOverlayWindow _window;
        private float _nextTimerRefresh;
        private float _nextTeamStatePoll;
        private float _nextHeartbeat;
        private int _consecutivePollFailures;
        private bool _refreshInProgress;
        private bool _heartbeatInProgress;
        private bool _identityActivationInProgress;
        private bool _signOutInProgress;
        private bool _mutationInProgress;
        private int _statisticsRequestId;
        private StatisticsPeriod _statisticsPeriod = StatisticsPeriod.LastSevenDays;
        private bool _quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null || FindAnyObjectByType<TeamOverlayApp>() != null)
            {
                return;
            }

            var prefab = Resources.Load<GameObject>(AppPrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError(
                    "Resources/" + AppPrefabResourcePath + ".prefab is missing. " +
                    "Run Team Overlay/Create Missing Editable UI Prefabs to recreate it.");
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
            Screen.SetResolution(480, 220, FullScreenMode.Windowed);

            _lifetime = new CancellationTokenSource();
            _identityStore = new LocalIdentityProfileStore();
            _supabaseTransport = new HttpClientSupabaseTransport();

            _window = gameObject.AddComponent<WindowsOverlayWindow>();
            _window.ClockOutAndExitRequested += HandleClockOutAndExitRequested;
            _window.SessionEndingRequested += HandleSessionEndingRequested;
            _window.Configure();
            _window.SetAlwaysOnTop(true);
            _tonePlayer = gameObject.AddComponent<NotificationTonePlayer>();

            if (_firstRunNamePrefab == null)
            {
                Debug.LogError("TeamOverlayApp is missing its first-run name prefab reference.");
                return;
            }

            _firstRunNameView = Instantiate(_firstRunNamePrefab, transform);
            _firstRunNameView.name = _firstRunNamePrefab.name;
            _firstRunNameView.Initialize();
            _firstRunNameView.Submitted += HandleFirstRunNameSubmitted;
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
                if (teamEvent.Type == TeamEventType.MemberCheckedIn
                    && !string.Equals(teamEvent.ActorMemberId, _backend.LocalMemberId, StringComparison.Ordinal))
                {
                    _tonePlayer.Play();
                }
            }

            var now = Time.unscaledTime;
            if (now >= _nextTeamStatePoll)
            {
                _nextTeamStatePoll = now + TeamStatePollSeconds;
                RefreshStateWithoutWaiting();
            }

            if (now >= _nextHeartbeat)
            {
                _nextHeartbeat = now + HeartbeatSeconds;
                SendHeartbeatWithoutWaiting();
            }

            if (_members != null && now >= _nextTimerRefresh)
            {
                _nextTimerRefresh = now + 0.2f;
                _view.Bind(_members, _backend.LocalMemberId, DateTimeOffset.UtcNow);
            }
        }

        private async void HandleFirstRunNameSubmitted(string submittedName)
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
        private async void HandleSwitchAccountRequested()
        {
            if (_quitting || _signOutInProgress || _identityActivationInProgress || _backend == null)
            {
                return;
            }

            _signOutInProgress = true;
            var previousName = _identityProfile?.DisplayName;
            _view.SetBusy(true);
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
                if (_view != null)
                {
                    _view.SetBusy(false);
                    _view.ShowFeedback("로그아웃하지 못했습니다: " + exception.Message, true);
                }
                else
                {
                    _firstRunNameView.Show(previousName);
                    _firstRunNameView.ShowError("로그아웃하지 못했습니다: " + exception.Message);
                }
            }
            finally
            {
                _signOutInProgress = false;
            }
        }

        /// <summary>
        /// Resolves the Supabase member for a name. The Auth session is stored per
        /// name, so re-entering a name used on this PC restores that member instead
        /// of creating a second anonymous user.
        /// </summary>
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
                throw new InvalidOperationException("TeamOverlayApp is missing its main view prefab reference.");
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
            _view.AlwaysOnTopToggleRequested += HandleAlwaysOnTopToggleRequested;
            _view.MinimizeRequested += HandleMinimizeRequested;
            _view.ExitRequested += HandleClockOutAndExitRequested;
            _view.SwitchAccountRequested += HandleSwitchAccountRequested;
            _view.StatsToggleRequested += HandleStatsToggleRequested;
            _view.StatisticsPeriodChangeRequested += HandleStatisticsPeriodChangeRequested;
            _view.StatusNoteSubmitted += HandleStatusNoteSubmitted;
            _view.SetAlwaysOnTop(_window.IsAlwaysOnTop);
            _firstRunNameView.Hide();
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
            CloseStatisticsPanel();
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
            _view.AlwaysOnTopToggleRequested -= HandleAlwaysOnTopToggleRequested;
            _view.MinimizeRequested -= HandleMinimizeRequested;
            _view.ExitRequested -= HandleClockOutAndExitRequested;
            _view.SwitchAccountRequested -= HandleSwitchAccountRequested;
            _view.StatsToggleRequested -= HandleStatsToggleRequested;
            _view.StatisticsPeriodChangeRequested -= HandleStatisticsPeriodChangeRequested;
            _view.StatusNoteSubmitted -= HandleStatusNoteSubmitted;
        }

        private bool IsLocalMemberClockedIn()
        {
            if (_members == null || _backend == null)
            {
                return false;
            }

            foreach (var member in _members)
            {
                if (string.Equals(member.MemberId, _backend.LocalMemberId, StringComparison.Ordinal))
                {
                    return member.AttendanceStatus == AttendanceStatus.ClockedIn;
                }
            }

            return false;
        }

        private void HandleCheckInRequested()
        {
            RunMutation(token => _backend.CheckInAsync(token), "출근했습니다. 기본 상태는 작업중입니다.");
        }

        private void HandleCheckOutRequested()
        {
            RunMutation(token => _backend.CheckOutAsync(CheckoutReason.Manual, token), "퇴근했습니다.");
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
            if (_members == null || _backend == null)
            {
                return null;
            }

            foreach (var member in _members)
            {
                if (string.Equals(member.MemberId, _backend.LocalMemberId, StringComparison.Ordinal))
                {
                    return member.StatusNote;
                }
            }

            return null;
        }

        private void HandleAlwaysOnTopToggleRequested()
        {
            _window.ToggleAlwaysOnTop();
            _view.SetAlwaysOnTop(_window.IsAlwaysOnTop);
            _view.ShowFeedback(_window.IsAlwaysOnTop ? "항상 위를 켰습니다." : "항상 위를 껐습니다.");
        }

        private void HandleMinimizeRequested()
        {
            _window.Minimize();
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

            _view.SetStatisticsVisible(true);
            _window.ExpandForStatistics();
            LoadStatistics(_statisticsPeriod);
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
            var range = StatisticsRange.Resolve(period, DateTime.Today);
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
        /// Windows is signing out. The shell is holding the shutdown open for us,
        /// so the last checkout gets a real attempt and is recorded as
        /// <see cref="CheckoutReason.OsShutdown"/> instead of being swept up three
        /// minutes later as a stale session.
        /// </summary>
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

        private async void RunMutation(Func<CancellationToken, Task> mutation, string successMessage)
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
            _view.Bind(_members, _backend.LocalMemberId, DateTimeOffset.UtcNow);
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
            }

            _lifetime?.Cancel();
            _eventSubscription?.Dispose();
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

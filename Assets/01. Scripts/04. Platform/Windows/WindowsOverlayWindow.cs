using System;
using System.Threading;
using UnityEngine;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif

namespace DOTORION.Platform.Windows
{
    /// <summary>
    /// Keeps all Win32 details behind a small Unity-facing API. In the Editor and
    /// on non-Windows platforms it remains a safe no-op so the mock flow is still
    /// testable without native window mutations.
    /// </summary>
    public sealed class WindowsOverlayWindow : MonoBehaviour
    {
        public bool IsAlwaysOnTop { get; private set; } = true;

        /// <summary>
        /// The window is showing the name-and-status only mini overlay. Declared
        /// outside the Windows-only block because the Unity side reads it on
        /// every platform.
        /// </summary>
        public bool IsMiniMode { get; private set; }

        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Seconds since this desktop last saw keyboard or mouse input. Zero
        /// wherever that cannot be asked - the Editor, and any non-Windows
        /// build - because a machine that cannot report input must not read as
        /// one nobody is sitting at.
        /// </summary>
        public double IdleSeconds
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                var info = new WindowsNativeMethods.LastInputInfo
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(WindowsNativeMethods.LastInputInfo))
                };
                if (!WindowsNativeMethods.GetLastInputInfo(ref info))
                {
                    return 0d;
                }

                // Both sides come off the same 32 bit millisecond counter, which
                // wraps about every 49 days. Unsigned subtraction stays correct
                // across the wrap; a signed one would report weeks of idleness.
                return unchecked(WindowsNativeMethods.GetTickCount() - info.dwTime) / 1000d;
#else
                return 0d;
#endif
            }
        }

        // Both events below are raised only from the Windows-only message loop, so
        // an Editor or non-Windows compile sees a declaration with no raise site.
        // That is the intended shape here rather than an oversight.
#pragma warning disable 67
        /// <summary>Raised when the window is closed, by its close button or Alt+F4.</summary>
        public event Action ClockOutAndExitRequested;

        /// <summary>
        /// Windows is shutting down or signing the user out. The handler has until
        /// <see cref="CompleteSessionEnd"/> is called to finish a last checkout;
        /// a shutdown block reason keeps the shell waiting in the meantime.
        /// </summary>
        public event Action SessionEndingRequested;

        /// <summary>
        /// The machine is going to sleep. Unlike a shutdown there is nothing that
        /// will wait for us, so a handler gets one best-effort attempt and no
        /// promise it lands.
        /// </summary>
        public event Action SuspendingRequested;
#pragma warning restore 67

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        public const int OverlayWindowWidth = 480;
        public const int CompactWindowHeight = 220;

        /// <summary>
        /// Extra height the statistics panel needs under the compact layout. It
        /// mirrors the panel's own height in the prefab, which PrefabAssetTests
        /// pins so the two cannot drift apart.
        /// </summary>
        public const int StatisticsPanelHeight = 424;

        /// <summary>
        /// Extra height the avatar picker needs. Unlike the statistics panel this
        /// one is taken off the top of the window, so it also mirrors the panel's
        /// height in the prefab; PrefabAssetTests pins the pair.
        /// </summary>
        public const int AvatarPickerPanelHeight = 160;

        /// <summary>
        /// The mini overlay's client size. It mirrors the panel's own size in the
        /// prefab, which PrefabAssetTests pins so the two cannot drift apart.
        /// Roughly one member card, which is all a name-and-status list needs.
        /// </summary>
        /// <summary>
        /// Extra height the developer dashboard needs. Like the statistics panel
        /// it is taken off the bottom, and it mirrors the panel's own height in
        /// the prefab; PrefabAssetTests pins the pair.
        /// </summary>
        public const int DashboardPanelHeight = 300;

        /// <summary>
        /// Extra height the settings panel needs. Like the statistics panel it is
        /// taken off the bottom, and it mirrors the panel's own height in the
        /// prefab; PrefabAssetTests pins the pair.
        /// </summary>
        public const int SettingsPanelHeight = 160;

        public const int MiniWindowWidth = 77;

        public const int MiniWindowHeight = 130;

        /// <summary>
        /// How opaque the mini overlay is, out of 255. It sits on top of whatever
        /// the person is actually working on, so it reads better as a pane you can
        /// see through than as a solid box; anything much below this and the
        /// status text starts fighting the desktop behind it.
        /// </summary>
        public const byte MiniWindowAlpha = 225;

        /// <summary>Backstop for a window whose messages no longer reach us.</summary>
        private const float WindowAuditIntervalSeconds = 5f;

        private static WindowsOverlayWindow _activeInstance;

        private WindowsNativeMethods.WindowProcedure _windowProcedure;
        private WindowsNativeMethods.EnumerateWindowsProcedure _enumerateProcedure;
        private IntPtr _windowHandle;
        private IntPtr _previousWindowProcedure;
        private IntPtr _windowProcedurePointer;
        private bool _configurationRequested;
        private bool _allowNativeClose;
        private float _nextInitializationAttempt;
        private int _exitPending;
        private int _suspendPending;
        private int _sessionEndPending;
        private bool _sessionEndReported;
        private bool _statisticsExpanded;
        private bool _avatarPickerExpanded;
        private bool _dashboardExpanded;
        private bool _settingsExpanded;
        private bool _growsUpward;
        private int _windowStateDirty;
        private float _nextWindowAudit;
#endif

        /// <summary>Grows the window so the developer dashboard fits under the overlay.</summary>
        public void ExpandForDashboard()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _dashboardExpanded = true;
            _statisticsExpanded = false;
            _avatarPickerExpanded = false;
            _settingsExpanded = false;
            _growsUpward = false;
            ApplyContentSize();
#endif
        }

        /// <summary>Shrinks the window back down from the developer dashboard.</summary>
        public void CollapseDashboard()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _dashboardExpanded = false;
            _growsUpward = false;
            ApplyContentSize();
#endif
        }

        /// <summary>Grows the window so the settings panel fits under the compact layout.</summary>
        public void ExpandForSettings()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _settingsExpanded = true;
            _growsUpward = false;
            ApplyContentSize();
#endif
        }

        /// <summary>Shrinks the window back down from the settings panel.</summary>
        public void CollapseSettings()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _settingsExpanded = false;
            _growsUpward = false;
            ApplyContentSize();
#endif
        }

        /// <summary>Grows the window so the statistics panel fits under the compact layout.</summary>
        public void ExpandForStatistics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _statisticsExpanded = true;
            _growsUpward = false;
            ApplyContentSize();
#endif
        }

        /// <summary>
        /// Grows the window upwards so the avatar picker fits above the overlay.
        /// It opens over the cards rather than under the controls because that is
        /// where the icon being changed is; the bottom edge stays where the person
        /// put the window.
        /// </summary>
        public void ExpandForAvatarPicker()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _avatarPickerExpanded = true;
            _growsUpward = true;
            ApplyContentSize();
#endif
        }

        /// <summary>Shrinks the window back down from the avatar picker.</summary>
        public void CollapseAvatarPicker()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _avatarPickerExpanded = false;
            _growsUpward = true;
            ApplyContentSize();
#endif
        }

        /// <summary>
        /// Blinks the taskbar button until the person looks at the window. A nudge
        /// that only plays a sound is missed whenever the overlay is behind
        /// something, and stealing focus instead would interrupt whatever they are
        /// doing, which is exactly what a nudge must not do.
        /// </summary>
        public void FlashTaskbar()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!EnsureWindowHandle())
            {
                return;
            }

            var info = new WindowsNativeMethods.FlashWindowInfo
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WindowsNativeMethods.FlashWindowInfo)),
                hwnd = _windowHandle,
                dwFlags = WindowsNativeMethods.FlashwTray | WindowsNativeMethods.FlashwTimerNoFg,
                uCount = 3,
                dwTimeout = 0
            };
            WindowsNativeMethods.FlashWindowEx(ref info);
#endif
        }

        /// <summary>
        /// Shrinks the window down to the name-and-status list and makes it
        /// translucent. The panel sizes come from the prefab, so nothing here
        /// depends on which layout the overlay was showing beforehand.
        /// </summary>
        public void EnterMiniMode()
        {
            IsMiniMode = true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _statisticsExpanded = false;
            _avatarPickerExpanded = false;
            _dashboardExpanded = false;
            _settingsExpanded = false;
            _growsUpward = false;
            ApplyWindowOpacity();
            ApplyContentSize();
#endif
        }

        /// <summary>Brings the full overlay back, opaque and at its normal size.</summary>
        public void ExitMiniMode()
        {
            IsMiniMode = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _growsUpward = false;
            ApplyWindowOpacity();
            ApplyContentSize();
#endif
        }

        /// <summary>Shrinks the window back to the compact layout.</summary>
        public void RestoreCompactHeight()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _statisticsExpanded = false;
            _growsUpward = false;
            ApplyContentSize();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        /// <summary>
        /// Nudges the window until its client area - the part Unity actually
        /// draws - is exactly as tall as the layout needs.
        ///
        /// It corrects by the difference rather than by a measured frame
        /// thickness: right after a style change the frame Windows reports can
        /// still be the old one, and trusting it once baked the dead title bar's
        /// 39px into the window for good. Measuring again on the next tick
        /// finishes the job instead.
        /// </summary>
        private void ApplyContentSize()
        {
            if (!EnsureWindowHandle() ||
                WindowsNativeMethods.IsIconic(_windowHandle) ||
                !WindowsNativeMethods.GetWindowRect(_windowHandle, out var window) ||
                !WindowsNativeMethods.GetClientRect(_windowHandle, out var client))
            {
                return;
            }

            var contentWidth = IsMiniMode ? MiniWindowWidth : OverlayWindowWidth;
            var contentHeight = IsMiniMode
                ? MiniWindowHeight
                : CompactWindowHeight +
                  (_statisticsExpanded ? StatisticsPanelHeight : 0) +
                  (_avatarPickerExpanded ? AvatarPickerPanelHeight : 0) +
                  (_dashboardExpanded ? DashboardPanelHeight : 0) +
                  (_settingsExpanded ? SettingsPanelHeight : 0);
            var widthDelta = contentWidth - client.Width;
            var heightDelta = contentHeight - client.Height;
            if (widthDelta == 0 && heightDelta == 0)
            {
                return;
            }

            var width = Math.Max(1, window.Width + widthDelta);
            var height = Math.Max(1, window.Height + heightDelta);
            var left = window.Left;
            var top = window.Top;
            if (_growsUpward)
            {
                // Holding the bottom edge still is what makes the picker look like
                // it unfolds upwards: every pixel gained is taken off the top.
                // Correcting by the same delta the height moves by keeps this
                // idempotent, so a second pass on the next tick is a no-op.
                top -= heightDelta;
            }

            // A window that just changed size may no longer fit where it sat: a
            // panel unfolding upwards can run past the top of the screen, and
            // leaving the mini overlay nearly quadruples the width in place.
            // Either way the part that spilled over cannot be clicked, so the
            // window is pulled back inside the monitor it is on.
            if (TryGetWorkArea(out var workArea))
            {
                left = Math.Max(workArea.Left, Math.Min(left, workArea.Right - width));
                top = Math.Max(workArea.Top, Math.Min(top, workArea.Bottom - height));
            }

            WindowsNativeMethods.SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                left,
                top,
                width,
                height,
                WindowsNativeMethods.SwpNoZOrder | WindowsNativeMethods.SwpNoActivate);
        }

        /// <summary>
        /// Turns the whole window translucent in mini mode and solid again on the
        /// way out. Unity re-asserts its own styles whenever it touches the
        /// window, so like the frame stripping this is re-applied rather than set
        /// once; it finds nothing to do on almost every pass.
        /// </summary>
        private void ApplyWindowOpacity()
        {
            if (!EnsureWindowHandle())
            {
                return;
            }

            var exStyle = WindowsNativeMethods.GetWindowLongPointer(
                    _windowHandle,
                    WindowsNativeMethods.GwlExStyle)
                .ToInt64();
            var isLayered = (exStyle & WindowsNativeMethods.WsExLayered) != 0;
            if (IsMiniMode)
            {
                if (!isLayered)
                {
                    WindowsNativeMethods.SetWindowLongPointer(
                        _windowHandle,
                        WindowsNativeMethods.GwlExStyle,
                        new IntPtr(exStyle | WindowsNativeMethods.WsExLayered));
                }

                // Re-sent even when the style was already there: Unity recreating
                // the swap chain resets the alpha but not the style bit.
                WindowsNativeMethods.SetLayeredWindowAttributes(
                    _windowHandle,
                    0,
                    MiniWindowAlpha,
                    WindowsNativeMethods.LwaAlpha);
                return;
            }

            if (!isLayered)
            {
                return;
            }

            WindowsNativeMethods.SetWindowLongPointer(
                _windowHandle,
                WindowsNativeMethods.GwlExStyle,
                new IntPtr(exStyle & ~WindowsNativeMethods.WsExLayered));
            WindowsNativeMethods.SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                WindowsNativeMethods.SwpNoMove |
                WindowsNativeMethods.SwpNoSize |
                WindowsNativeMethods.SwpNoZOrder |
                WindowsNativeMethods.SwpNoActivate |
                WindowsNativeMethods.SwpFrameChanged);
        }

        private bool TryGetWorkArea(out WindowsNativeMethods.Rect workArea)
        {
            workArea = default;
            var monitor = WindowsNativeMethods.MonitorFromWindow(
                _windowHandle,
                WindowsNativeMethods.MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }

            var info = new WindowsNativeMethods.MonitorInfo
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WindowsNativeMethods.MonitorInfo))
            };
            if (!WindowsNativeMethods.GetMonitorInfoW(monitor, ref info))
            {
                return false;
            }

            workArea = info.rcWork;
            return true;
        }

        /// <summary>
        /// Unity re-asserts its own windowed style whenever it touches the window,
        /// so stripping the frame once at startup loses the race and the title bar
        /// comes back for good. This runs on the frames the window actually
        /// changed, and finds nothing to do on almost all of them.
        /// </summary>
        private void EnforceOverlayWindow()
        {
            if (!EnsureWindowHandle() || WindowsNativeMethods.IsIconic(_windowHandle))
            {
                return;
            }

            var style = WindowsNativeMethods.GetWindowLongPointer(
                    _windowHandle,
                    WindowsNativeMethods.GwlStyle)
                .ToInt64();
            if ((style & (WindowsNativeMethods.WsCaption | WindowsNativeMethods.WsThickFrame)) != 0)
            {
                ApplyOverlayWindowStyle();
            }

            ApplyWindowOpacity();
            ApplyContentSize();
        }
#endif

        public bool Configure()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _configurationRequested = true;
            return TryInitialize();
#else
            return false;
#endif
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            IsAlwaysOnTop = enabled;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (EnsureWindowHandle())
            {
                WindowsNativeMethods.SetWindowPos(
                    _windowHandle,
                    enabled ? WindowsNativeMethods.HwndTopMost : WindowsNativeMethods.HwndNoTopMost,
                    0,
                    0,
                    0,
                    0,
                    WindowsNativeMethods.SwpNoMove |
                    WindowsNativeMethods.SwpNoSize |
                    WindowsNativeMethods.SwpNoActivate);
            }
#endif
        }

        public void ToggleAlwaysOnTop()
        {
            SetAlwaysOnTop(!IsAlwaysOnTop);
        }

        public void BeginWindowDrag()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!EnsureWindowHandle())
            {
                return;
            }

            WindowsNativeMethods.ReleaseCapture();
            WindowsNativeMethods.SendMessage(
                _windowHandle,
                WindowsNativeMethods.WmNcLButtonDown,
                new IntPtr(WindowsNativeMethods.HtCaption),
                IntPtr.Zero);
#endif
        }

        public void Minimize()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!EnsureWindowHandle())
            {
                return;
            }

            WindowsNativeMethods.ShowWindow(_windowHandle, WindowsNativeMethods.SwMinimize);
#endif
        }

        /// <summary>
        /// Releases the shutdown block so Windows can finish signing out. Safe to
        /// call when no shutdown is in progress and on non-Windows platforms.
        /// </summary>
        public void CompleteSessionEnd()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_windowHandle != IntPtr.Zero)
            {
                WindowsNativeMethods.ShutdownBlockReasonDestroy(_windowHandle);
            }
#endif
        }

        /// <summary>
        /// Call before Application.Quit so WM_CLOSE is no longer converted to a
        /// tray hide and all native resources are released deterministically.
        /// </summary>
        public void PrepareForExit()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _allowNativeClose = true;
            CleanupNativeResources();
#endif
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_configurationRequested && !IsInitialized && Time.unscaledTime >= _nextInitializationAttempt)
            {
                _nextInitializationAttempt = Time.unscaledTime + 0.25f;
                TryInitialize();
            }

            // The window procedure flags the frames that actually changed the
            // window, so nothing is polled while nothing happens. The slow audit
            // is only there for the case the hook is gone - if Unity ever replaces
            // the window procedure or the window itself, no message arrives to
            // tell us the frame came back.
            if (IsInitialized &&
                (Interlocked.Exchange(ref _windowStateDirty, 0) != 0 ||
                 Time.unscaledTime >= _nextWindowAudit))
            {
                _nextWindowAudit = Time.unscaledTime + WindowAuditIntervalSeconds;
                EnforceOverlayWindow();
            }

            if (Interlocked.Exchange(ref _exitPending, 0) != 0)
            {
                ClockOutAndExitRequested?.Invoke();
            }

            if (Interlocked.Exchange(ref _suspendPending, 0) != 0)
            {
                SuspendingRequested?.Invoke();
            }

            var sessionEnding = Interlocked.CompareExchange(ref _sessionEndPending, 0, 0) == 1;
            if (sessionEnding && !_sessionEndReported)
            {
                _sessionEndReported = true;
                SessionEndingRequested?.Invoke();
            }
            else if (!sessionEnding && _sessionEndReported)
            {
                // Another application vetoed the shutdown, so allow a later one.
                _sessionEndReported = false;
            }
#endif
        }

        private void OnApplicationQuit()
        {
            PrepareForExit();
        }

        private void OnDestroy()
        {
            PrepareForExit();
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private bool TryInitialize()
        {
            if (IsInitialized)
            {
                return true;
            }

            if (!EnsureWindowHandle())
            {
                return false;
            }

            ApplyOverlayWindowStyle();
            ApplyWindowOpacity();
            ApplyContentSize();
            HookWindowProcedure();
            SetAlwaysOnTop(IsAlwaysOnTop);

            IsInitialized = true;
            return true;
        }

        private bool EnsureWindowHandle()
        {
            if (_windowHandle != IntPtr.Zero && WindowsNativeMethods.IsWindow(_windowHandle))
            {
                return true;
            }

            _windowHandle = IntPtr.Zero;
            var processId = WindowsNativeMethods.GetCurrentProcessId();
            var activeWindow = WindowsNativeMethods.GetActiveWindow();
            if (IsCurrentProcessWindow(activeWindow, processId))
            {
                _windowHandle = activeWindow;
                return true;
            }

            _enumerateProcedure = (candidate, state) =>
            {
                if (!IsCurrentProcessWindow(candidate, processId))
                {
                    return true;
                }

                _windowHandle = candidate;
                return false;
            };
            WindowsNativeMethods.EnumWindows(_enumerateProcedure, IntPtr.Zero);
            return _windowHandle != IntPtr.Zero;
        }

        private static bool IsCurrentProcessWindow(IntPtr candidate, uint processId)
        {
            if (candidate == IntPtr.Zero || !WindowsNativeMethods.IsWindowVisible(candidate))
            {
                return false;
            }

            WindowsNativeMethods.GetWindowThreadProcessId(candidate, out var candidateProcessId);
            if (candidateProcessId != processId ||
                WindowsNativeMethods.GetWindow(candidate, WindowsNativeMethods.GwOwner) != IntPtr.Zero)
            {
                return false;
            }

            // Unity standalone player windows use UnityWndClass. Keeping the PID
            // fallback makes the lookup resilient if Unity renames that class.
            return true;
        }

        private void ApplyOverlayWindowStyle()
        {
            var style = WindowsNativeMethods.GetWindowLongPointer(
                    _windowHandle,
                    WindowsNativeMethods.GwlStyle)
                .ToInt64();
            style &= ~WindowsNativeMethods.WsCaption;
            style &= ~WindowsNativeMethods.WsThickFrame;
            style &= ~WindowsNativeMethods.WsMinimizeBox;
            style &= ~WindowsNativeMethods.WsMaximizeBox;
            style |= WindowsNativeMethods.WsPopup | WindowsNativeMethods.WsVisible;

            WindowsNativeMethods.SetWindowLongPointer(
                _windowHandle,
                WindowsNativeMethods.GwlStyle,
                new IntPtr(style));
            WindowsNativeMethods.SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                WindowsNativeMethods.SwpNoMove |
                WindowsNativeMethods.SwpNoSize |
                WindowsNativeMethods.SwpNoZOrder |
                WindowsNativeMethods.SwpNoActivate |
                WindowsNativeMethods.SwpFrameChanged);
        }

        private void HookWindowProcedure()
        {
            if (_previousWindowProcedure != IntPtr.Zero)
            {
                return;
            }

            _activeInstance = this;
            _windowProcedure = StaticWindowProcedure;
            _windowProcedurePointer = Marshal.GetFunctionPointerForDelegate(_windowProcedure);
            _previousWindowProcedure = WindowsNativeMethods.SetWindowLongPointer(
                _windowHandle,
                WindowsNativeMethods.GwlWndProc,
                _windowProcedurePointer);
        }

        [MonoPInvokeCallback(typeof(WindowsNativeMethods.WindowProcedure))]
        private static IntPtr StaticWindowProcedure(
            IntPtr windowHandle,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter)
        {
            var instance = _activeInstance;
            return instance != null
                ? instance.HandleWindowMessage(windowHandle, message, wordParameter, longParameter)
                : WindowsNativeMethods.DefWindowProc(windowHandle, message, wordParameter, longParameter);
        }

        private IntPtr HandleWindowMessage(
            IntPtr windowHandle,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter)
        {
            if (message == WindowsNativeMethods.WmStyleChanged ||
                message == WindowsNativeMethods.WmWindowPosChanged ||
                message == WindowsNativeMethods.WmDpiChanged)
            {
                // Re-applying the style from inside the window procedure would
                // re-enter it, so Update does the work on the next frame. Our own
                // SetWindowPos raises this too; that pass simply finds nothing to
                // correct and stops.
                Interlocked.Exchange(ref _windowStateDirty, 1);
            }
            else if (message == WindowsNativeMethods.WmQueryEndSession)
            {
                // Agree to the shutdown, but register a reason so the shell waits
                // while Update runs the final checkout. The window procedure has to
                // return promptly, so no work happens here.
                if (Interlocked.Exchange(ref _sessionEndPending, 1) == 0)
                {
                    _allowNativeClose = true;
                    WindowsNativeMethods.ShutdownBlockReasonCreate(
                        windowHandle,
                        "퇴근 기록을 저장하는 중입니다.");
                }

                return new IntPtr(1);
            }
            else if (message == WindowsNativeMethods.WmEndSession)
            {
                if (wordParameter == IntPtr.Zero)
                {
                    // The shutdown was cancelled by another application.
                    Interlocked.Exchange(ref _sessionEndPending, 0);
                    _allowNativeClose = false;
                    WindowsNativeMethods.ShutdownBlockReasonDestroy(windowHandle);
                }

                return IntPtr.Zero;
            }
            else if (message == WindowsNativeMethods.WmPowerBroadcast &&
                     wordParameter.ToInt64() == WindowsNativeMethods.PbtApmSuspend)
            {
                // Windows gives roughly two seconds here and cannot be asked for
                // more, so the window procedure only raises a flag and Update does
                // whatever it can with the time that is left.
                Interlocked.Exchange(ref _suspendPending, 1);
                return new IntPtr(1);
            }
            else if (message == WindowsNativeMethods.WmGetMinMaxInfo)
            {
                // Windows refuses to make a window narrower than the system
                // minimum tracking width, which is wider than the mini overlay,
                // so a resize to it would silently come back clamped. Windows
                // fills the limits first and we only lower the floor.
                var result = InvokePreviousWindowProcedure(
                    windowHandle,
                    message,
                    wordParameter,
                    longParameter);
                var limits = (WindowsNativeMethods.MinMaxInfo)Marshal.PtrToStructure(
                    longParameter,
                    typeof(WindowsNativeMethods.MinMaxInfo));
                limits.ptMinTrackSize.X = Math.Min(limits.ptMinTrackSize.X, MiniWindowWidth);
                limits.ptMinTrackSize.Y = Math.Min(limits.ptMinTrackSize.Y, MiniWindowHeight);
                Marshal.StructureToPtr(limits, longParameter, false);
                return result;
            }
            else if (message == WindowsNativeMethods.WmClose && !_allowNativeClose)
            {
                // This used to hide the window to the tray. With no tray icon that
                // would strand a running process nothing can restore, and
                // forceSingleInstance would then block every later launch, so a
                // close now goes down the normal clock-out-and-exit path.
                Interlocked.Exchange(ref _exitPending, 1);
                return IntPtr.Zero;
            }

            return InvokePreviousWindowProcedure(windowHandle, message, wordParameter, longParameter);
        }

        private IntPtr InvokePreviousWindowProcedure(
            IntPtr windowHandle,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter)
        {
            return _previousWindowProcedure != IntPtr.Zero
                ? WindowsNativeMethods.CallWindowProc(
                    _previousWindowProcedure,
                    windowHandle,
                    message,
                    wordParameter,
                    longParameter)
                : WindowsNativeMethods.DefWindowProc(windowHandle, message, wordParameter, longParameter);
        }

        private void CleanupNativeResources()
        {
            if (_windowHandle != IntPtr.Zero &&
                _previousWindowProcedure != IntPtr.Zero &&
                WindowsNativeMethods.IsWindow(_windowHandle))
            {
                var current = WindowsNativeMethods.GetWindowLongPointer(
                    _windowHandle,
                    WindowsNativeMethods.GwlWndProc);
                if (current == _windowProcedurePointer)
                {
                    WindowsNativeMethods.SetWindowLongPointer(
                        _windowHandle,
                        WindowsNativeMethods.GwlWndProc,
                        _previousWindowProcedure);
                }
            }

            _previousWindowProcedure = IntPtr.Zero;
            _windowProcedurePointer = IntPtr.Zero;
            _windowProcedure = null;
            if (_activeInstance == this)
            {
                _activeInstance = null;
            }

            IsInitialized = false;
        }
#endif
    }
}

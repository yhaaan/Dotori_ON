using System;
using System.Threading;
using UnityEngine;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif

namespace TeamOverlay.Platform.Windows
{
    /// <summary>
    /// Keeps all Win32 details behind a small Unity-facing API. In the Editor and
    /// on non-Windows platforms it remains a safe no-op so the mock flow is still
    /// testable without native window mutations.
    /// </summary>
    public sealed class WindowsOverlayWindow : MonoBehaviour
    {
        public bool IsAlwaysOnTop { get; private set; } = true;

        public bool IsInitialized { get; private set; }

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
        private int _sessionEndPending;
        private bool _sessionEndReported;
        private bool _statisticsExpanded;
        private int _windowStateDirty;
        private float _nextWindowAudit;
#endif

        /// <summary>Grows the window so the statistics panel fits under the compact layout.</summary>
        public void ExpandForStatistics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _statisticsExpanded = true;
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

        /// <summary>Shrinks the window back to the compact layout.</summary>
        public void RestoreCompactHeight()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _statisticsExpanded = false;
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

            var contentHeight = CompactWindowHeight + (_statisticsExpanded ? StatisticsPanelHeight : 0);
            var widthDelta = OverlayWindowWidth - client.Width;
            var heightDelta = contentHeight - client.Height;
            if (widthDelta == 0 && heightDelta == 0)
            {
                return;
            }

            WindowsNativeMethods.SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                0,
                0,
                Math.Max(1, window.Width + widthDelta),
                Math.Max(1, window.Height + heightDelta),
                WindowsNativeMethods.SwpNoMove |
                WindowsNativeMethods.SwpNoZOrder |
                WindowsNativeMethods.SwpNoActivate);
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
            else if (message == WindowsNativeMethods.WmClose && !_allowNativeClose)
            {
                // This used to hide the window to the tray. With no tray icon that
                // would strand a running process nothing can restore, and
                // forceSingleInstance would then block every later launch, so a
                // close now goes down the normal clock-out-and-exit path.
                Interlocked.Exchange(ref _exitPending, 1);
                return IntPtr.Zero;
            }

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

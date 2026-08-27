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

        public bool IsMinimized { get; private set; }

        public event Action Initialized;

        public event Action StateChanged;

        /// <summary>Raised for the tray-menu exit and for Alt+F4.</summary>
        public event Action ClockOutAndExitRequested;

        /// <summary>
        /// Windows is shutting down or signing the user out. The handler has until
        /// <see cref="CompleteSessionEnd"/> is called to finish a last checkout;
        /// a shutdown block reason keeps the shell waiting in the meantime.
        /// </summary>
        public event Action SessionEndingRequested;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        public const int OverlayWindowWidth = 480;
        public const int CompactWindowHeight = 220;

        /// <summary>
        /// Extra height the statistics panel needs under the compact layout. It
        /// mirrors the panel's own height in the prefab, which PrefabAssetTests
        /// pins so the two cannot drift apart.
        /// </summary>
        public const int StatisticsPanelHeight = 424;

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
        private int _compactWindowHeight;
#endif

        /// <summary>
        /// Grows the window by exactly the statistics panel's height, remembering
        /// the height it grew from.
        /// </summary>
        public void ExpandForStatistics()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!TryGetWindowSize(out var width, out var height))
            {
                return;
            }

            if (_compactWindowHeight <= 0)
            {
                _compactWindowHeight = height;
            }

            // The canvas scales on width, so a window wider than the reference
            // width renders the panel's design pixels proportionally taller.
            var panelHeight = Mathf.RoundToInt(
                StatisticsPanelHeight * (float)width / OverlayWindowWidth);
            ResizeWindow(width, _compactWindowHeight + panelHeight);
#endif
        }

        /// <summary>
        /// Puts the window back to the exact height it had before the statistics
        /// panel opened. Collapsing to the compact constant instead assumed the
        /// window was still the size this class asked for at startup; Unity sizes
        /// the client area rather than the window rect, so the window it hands us
        /// can be taller than that constant and the compact layout came back
        /// clipped.
        /// </summary>
        public void RestoreCompactHeight()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!TryGetWindowSize(out var width, out _))
            {
                return;
            }

            ResizeWindow(width, _compactWindowHeight > 0 ? _compactWindowHeight : CompactWindowHeight);
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private bool TryGetWindowSize(out int width, out int height)
        {
            width = OverlayWindowWidth;
            height = CompactWindowHeight;
            if (!EnsureWindowHandle() ||
                !WindowsNativeMethods.GetWindowRect(_windowHandle, out var bounds))
            {
                return false;
            }

            width = bounds.Width;
            height = bounds.Height;
            return true;
        }

        private void ResizeWindow(int width, int height)
        {
            WindowsNativeMethods.SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                0,
                0,
                Math.Max(1, width),
                Math.Max(1, height),
                WindowsNativeMethods.SwpNoMove |
                WindowsNativeMethods.SwpNoZOrder |
                WindowsNativeMethods.SwpNoActivate);
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
            StateChanged?.Invoke();
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
            IsMinimized = true;
            StateChanged?.Invoke();
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
            HookWindowProcedure();
            SetAlwaysOnTop(IsAlwaysOnTop);

            IsInitialized = true;
            IsMinimized = false;
            Initialized?.Invoke();
            StateChanged?.Invoke();
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
                OverlayWindowWidth,
                CompactWindowHeight,
                WindowsNativeMethods.SwpNoMove |
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
            if (message == WindowsNativeMethods.WmQueryEndSession)
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

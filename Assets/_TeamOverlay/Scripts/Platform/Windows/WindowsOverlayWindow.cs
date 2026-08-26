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

        public bool IsHiddenToTray { get; private set; }

        public bool IsMinimized { get; private set; }

        public event Action Initialized;

        public event Action StateChanged;

        public event Action RestoreRequested;

        public event Action ClockOutAndExitRequested;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const uint NotifyIconVersion4 = 4;
        private const uint NimSetVersion = 0x00000004;
        private const uint MenuShow = 1;
        private const uint MenuClockOutAndExit = 2;

        private static WindowsOverlayWindow _activeInstance;

        private WindowsNativeMethods.WindowProcedure _windowProcedure;
        private WindowsNativeMethods.EnumerateWindowsProcedure _enumerateProcedure;
        private IntPtr _windowHandle;
        private IntPtr _previousWindowProcedure;
        private IntPtr _windowProcedurePointer;
        private uint _taskbarCreatedMessage;
        private WindowsNativeMethods.NotifyIconData _trayData;
        private bool _configurationRequested;
        private bool _trayAdded;
        private bool _allowNativeClose;
        private float _nextInitializationAttempt;
        private int _restorePending;
        private int _hidePending;
        private int _menuPending;
        private int _recreateTrayPending;
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
            IsHiddenToTray = false;
            StateChanged?.Invoke();
#endif
        }

        public void HideToTray()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!EnsureWindowHandle())
            {
                return;
            }

            AddTrayIcon();
            WindowsNativeMethods.ShowWindow(_windowHandle, WindowsNativeMethods.SwHide);
            IsHiddenToTray = true;
            IsMinimized = false;
            StateChanged?.Invoke();
#endif
        }

        public void ShowFromTray()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!EnsureWindowHandle())
            {
                return;
            }

            WindowsNativeMethods.ShowWindow(_windowHandle, WindowsNativeMethods.SwRestore);
            WindowsNativeMethods.ShowWindow(_windowHandle, WindowsNativeMethods.SwShow);
            SetAlwaysOnTop(IsAlwaysOnTop);
            WindowsNativeMethods.SetForegroundWindow(_windowHandle);
            IsHiddenToTray = false;
            IsMinimized = false;
            RestoreRequested?.Invoke();
            StateChanged?.Invoke();
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

            if (Interlocked.Exchange(ref _recreateTrayPending, 0) != 0)
            {
                _trayAdded = false;
                AddTrayIcon();
            }

            if (Interlocked.Exchange(ref _hidePending, 0) != 0)
            {
                HideToTray();
            }

            if (Interlocked.Exchange(ref _restorePending, 0) != 0)
            {
                ShowFromTray();
            }

            if (Interlocked.Exchange(ref _menuPending, 0) != 0)
            {
                ShowTrayMenu();
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
            _taskbarCreatedMessage = WindowsNativeMethods.RegisterWindowMessage("TaskbarCreated");
            AddTrayIcon();
            SetAlwaysOnTop(IsAlwaysOnTop);

            IsInitialized = true;
            IsHiddenToTray = false;
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
                480,
                220,
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
            if (message == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
            {
                Interlocked.Exchange(ref _recreateTrayPending, 1);
            }
            else if (message == WindowsNativeMethods.WmClose && !_allowNativeClose)
            {
                Interlocked.Exchange(ref _hidePending, 1);
                return IntPtr.Zero;
            }
            else if (message == WindowsNativeMethods.TrayCallbackMessage)
            {
                var rawMessage = unchecked((uint)longParameter.ToInt64());
                var trayMessage = rawMessage & 0xFFFFu;
                switch (trayMessage)
                {
                    case WindowsNativeMethods.WmLButtonUp:
                    case WindowsNativeMethods.WmLButtonDoubleClick:
                        Interlocked.Exchange(ref _restorePending, 1);
                        return IntPtr.Zero;
                    case WindowsNativeMethods.WmRButtonUp:
                    case WindowsNativeMethods.WmContextMenu:
                        Interlocked.Exchange(ref _menuPending, 1);
                        return IntPtr.Zero;
                }
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

        private void AddTrayIcon()
        {
            if (_windowHandle == IntPtr.Zero || _trayAdded)
            {
                return;
            }

            var icon = WindowsNativeMethods.SendMessage(
                _windowHandle,
                WindowsNativeMethods.WmGetIcon,
                new IntPtr(WindowsNativeMethods.IconSmall2),
                IntPtr.Zero);
            if (icon == IntPtr.Zero)
            {
                icon = WindowsNativeMethods.SendMessage(
                    _windowHandle,
                    WindowsNativeMethods.WmGetIcon,
                    new IntPtr(WindowsNativeMethods.IconSmall),
                    IntPtr.Zero);
            }

            if (icon == IntPtr.Zero)
            {
                icon = WindowsNativeMethods.GetClassLongPointer(
                    _windowHandle,
                    WindowsNativeMethods.GclpHIconSmall);
            }

            if (icon == IntPtr.Zero)
            {
                icon = WindowsNativeMethods.LoadIcon(
                    IntPtr.Zero,
                    new IntPtr(WindowsNativeMethods.IdiApplication));
            }

            _trayData = new WindowsNativeMethods.NotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WindowsNativeMethods.NotifyIconData)),
                hWnd = _windowHandle,
                uID = 1,
                uFlags = WindowsNativeMethods.NifMessage |
                         WindowsNativeMethods.NifIcon |
                         WindowsNativeMethods.NifTip,
                uCallbackMessage = WindowsNativeMethods.TrayCallbackMessage,
                hIcon = icon,
                szTip = "Team Overlay",
                uTimeoutOrVersion = NotifyIconVersion4
            };

            _trayAdded = WindowsNativeMethods.ShellNotifyIcon(
                WindowsNativeMethods.NimAdd,
                ref _trayData);
            if (_trayAdded)
            {
                WindowsNativeMethods.ShellNotifyIcon(NimSetVersion, ref _trayData);
            }
        }

        private void ShowTrayMenu()
        {
            if (!EnsureWindowHandle())
            {
                return;
            }

            var menu = WindowsNativeMethods.CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                return;
            }

            try
            {
                WindowsNativeMethods.AppendMenu(
                    menu,
                    WindowsNativeMethods.MfString,
                    new UIntPtr(MenuShow),
                    "열기");
                WindowsNativeMethods.AppendMenu(
                    menu,
                    WindowsNativeMethods.MfSeparator,
                    UIntPtr.Zero,
                    string.Empty);
                WindowsNativeMethods.AppendMenu(
                    menu,
                    WindowsNativeMethods.MfString,
                    new UIntPtr(MenuClockOutAndExit),
                    "퇴근 후 종료");

                WindowsNativeMethods.GetCursorPos(out var point);
                WindowsNativeMethods.SetForegroundWindow(_windowHandle);
                var command = WindowsNativeMethods.TrackPopupMenu(
                    menu,
                    WindowsNativeMethods.TpmRightButton |
                    WindowsNativeMethods.TpmNoNotify |
                    WindowsNativeMethods.TpmReturnCommand,
                    point.X,
                    point.Y,
                    0,
                    _windowHandle,
                    IntPtr.Zero);
                WindowsNativeMethods.PostMessage(
                    _windowHandle,
                    WindowsNativeMethods.WmNull,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (command == MenuShow)
                {
                    ShowFromTray();
                }
                else if (command == MenuClockOutAndExit)
                {
                    ClockOutAndExitRequested?.Invoke();
                }
            }
            finally
            {
                WindowsNativeMethods.DestroyMenu(menu);
            }
        }

        private void CleanupNativeResources()
        {
            if (_trayAdded)
            {
                WindowsNativeMethods.ShellNotifyIcon(
                    WindowsNativeMethods.NimDelete,
                    ref _trayData);
                _trayAdded = false;
            }

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

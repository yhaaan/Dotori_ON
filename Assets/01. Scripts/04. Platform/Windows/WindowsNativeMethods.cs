#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace DOTORION.Platform.Windows
{
    internal static class WindowsNativeMethods
    {
        internal const int GwlStyle = -16;
        internal const int GwlExStyle = -20;
        internal const int GwlWndProc = -4;

        internal const long WsCaption = 0x00C00000L;
        internal const long WsThickFrame = 0x00040000L;
        internal const long WsMinimizeBox = 0x00020000L;
        internal const long WsMaximizeBox = 0x00010000L;
        internal const long WsPopup = unchecked((long)0x80000000);
        internal const long WsVisible = 0x10000000L;

        internal const long WsExLayered = 0x00080000L;
        internal const long WsExToolWindow = 0x00000080L;
        internal const long WsExAppWindow = 0x00040000L;

        /// <summary>Tells SetLayeredWindowAttributes to read the alpha argument.</summary>
        internal const uint LwaAlpha = 0x00000002;

        internal const uint SwMinimize = 6;
        internal const uint SwHide = 0;
        internal const uint SwShowNoActivate = 4;
        internal const uint SwRestore = 9;

        internal const uint SwpNoSize = 0x0001;
        internal const uint SwpNoMove = 0x0002;
        internal const uint SwpNoZOrder = 0x0004;
        internal const uint SwpNoActivate = 0x0010;
        internal const uint SwpFrameChanged = 0x0020;

        internal const uint WmClose = 0x0010;
        internal const uint WmQueryEndSession = 0x0011;
        internal const uint WmEndSession = 0x0016;
        internal const uint WmNcLButtonDown = 0x00A1;
        internal const uint WmWindowPosChanged = 0x0047;
        internal const uint WmStyleChanged = 0x007D;
        internal const uint WmDpiChanged = 0x02E0;
        internal const uint WmGetMinMaxInfo = 0x0024;
        internal const uint WmPowerBroadcast = 0x0218;
        internal const uint WmGetIcon = 0x007F;
        internal const uint WmLButtonUp = 0x0202;
        internal const uint WmLButtonDoubleClick = 0x0203;
        internal const uint WmTrayIcon = 0x8001;

        internal const int IconSmall = 0;
        internal const int IconSmall2 = 2;
        internal const int IdiApplication = 32512;

        internal const uint TrayIconId = 1;
        internal const uint NimAdd = 0x00000000;
        internal const uint NimDelete = 0x00000002;
        internal const uint NifMessage = 0x00000001;
        internal const uint NifIcon = 0x00000002;
        internal const uint NifTip = 0x00000004;

        /// <summary>The machine is suspending. There is no way to delay it.</summary>
        internal const int PbtApmSuspend = 0x0004;

        internal const int HtCaption = 2;
        internal const uint GwOwner = 4;

        internal static readonly IntPtr HwndTopMost = new IntPtr(-1);
        internal static readonly IntPtr HwndNoTopMost = new IntPtr(-2);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate IntPtr WindowProcedure(
            IntPtr windowHandle,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal delegate bool EnumerateWindowsProcedure(IntPtr windowHandle, IntPtr state);

        internal const uint FlashwTray = 0x00000002;
        internal const uint FlashwTimerNoFg = 0x0000000C;

        [StructLayout(LayoutKind.Sequential)]
        internal struct FlashWindowInfo
        {
            internal uint cbSize;
            internal IntPtr hwnd;
            internal uint dwFlags;
            internal uint uCount;
            internal uint dwTimeout;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct NotifyIconData
        {
            internal uint cbSize;
            internal IntPtr hWnd;
            internal uint uID;
            internal uint uFlags;
            internal uint uCallbackMessage;
            internal IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            internal string szTip;
            internal uint dwState;
            internal uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            internal string szInfo;
            internal uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            internal string szInfoTitle;
            internal uint dwInfoFlags;
            internal Guid guidItem;
            internal IntPtr hBalloonIcon;
        }

        /// <summary>
        /// Carries the tick the desktop last saw keyboard or mouse input. The
        /// value is for the whole interactive session, not this process, which is
        /// what makes it a usable "away from the desk" signal.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct LastInputInfo
        {
            internal uint cbSize;
            internal uint dwTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
        }

        /// <summary>
        /// The layout Windows hands to WM_GETMINMAXINFO. Only ptMinTrackSize is
        /// interesting here: its default is the system minimum window width,
        /// which is wider than the mini overlay.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct MinMaxInfo
        {
            internal Point ptReserved;
            internal Point ptMaxSize;
            internal Point ptMaxPosition;
            internal Point ptMinTrackSize;
            internal Point ptMaxTrackSize;
        }

        internal const uint MonitorDefaultToNearest = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        internal struct MonitorInfo
        {
            internal uint cbSize;
            internal Rect rcMonitor;
            internal Rect rcWork;
            internal uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;

            internal int Width => Right - Left;

            internal int Height => Bottom - Top;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumerateWindowsProcedure procedure, IntPtr state);

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentProcessId();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr windowHandle, uint command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr windowHandle);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr windowHandle, out Rect rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetClientRect(IntPtr windowHandle, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FlashWindowEx(ref FlashWindowInfo info);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern uint RegisterWindowMessageW(string message);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr windowHandle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern IntPtr LoadIconW(IntPtr instance, IntPtr iconName);

        [DllImport(
            "shell32.dll",
            CharSet = CharSet.Unicode,
            EntryPoint = "Shell_NotifyIconW",
            ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShellNotifyIconW(uint message, ref NotifyIconData data);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLongPtr32(IntPtr windowHandle, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLongPtr32(IntPtr windowHandle, int index, int value);

        internal static IntPtr GetWindowLongPointer(IntPtr windowHandle, int index)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(windowHandle, index)
                : new IntPtr(GetWindowLongPtr32(windowHandle, index));
        }

        internal static IntPtr SetWindowLongPointer(IntPtr windowHandle, int index, IntPtr value)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(windowHandle, index, value)
                : new IntPtr(SetWindowLongPtr32(windowHandle, index, value.ToInt32()));
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr windowHandle,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr windowHandle, uint command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReleaseCapture();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetLastInputInfo(ref LastInputInfo info);

        // Read rather than Environment.TickCount so the comparison is against the
        // same counter LastInputInfo is stamped from.
        [DllImport("kernel32.dll")]
        internal static extern uint GetTickCount();

        // Uniform whole-window alpha. Per-pixel transparency would need the
        // framebuffer alpha URP does not preserve here, and a status widget reads
        // fine as a single translucent pane anyway.
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetLayeredWindowAttributes(
            IntPtr windowHandle,
            uint colorKey,
            byte alpha,
            uint flags);

        // Windows kills a process a few seconds after WM_ENDSESSION. Registering a
        // block reason during WM_QUERYENDSESSION asks the shell to keep waiting and
        // shows the person why, which is the only supported way to buy enough time
        // for a network round trip on the way down.
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShutdownBlockReasonCreate(IntPtr windowHandle, string reason);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShutdownBlockReasonDestroy(IntPtr windowHandle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(
            IntPtr windowHandle,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr CallWindowProc(
            IntPtr previousProcedure,
            IntPtr windowHandle,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr DefWindowProc(
            IntPtr windowHandle,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter);
    }
}
#endif

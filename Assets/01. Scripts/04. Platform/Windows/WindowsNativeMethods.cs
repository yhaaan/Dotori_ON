#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace TeamOverlay.Platform.Windows
{
    internal static class WindowsNativeMethods
    {
        internal const int GwlStyle = -16;
        internal const int GwlWndProc = -4;
        internal const int GclpHIconSmall = -34;

        internal const long WsCaption = 0x00C00000L;
        internal const long WsThickFrame = 0x00040000L;
        internal const long WsMinimizeBox = 0x00020000L;
        internal const long WsMaximizeBox = 0x00010000L;
        internal const long WsPopup = unchecked((long)0x80000000);
        internal const long WsVisible = 0x10000000L;

        internal const uint SwHide = 0;
        internal const uint SwShow = 5;
        internal const uint SwMinimize = 6;
        internal const uint SwRestore = 9;

        internal const uint SwpNoSize = 0x0001;
        internal const uint SwpNoMove = 0x0002;
        internal const uint SwpNoZOrder = 0x0004;
        internal const uint SwpNoActivate = 0x0010;
        internal const uint SwpFrameChanged = 0x0020;

        internal const uint WmNull = 0x0000;
        internal const uint WmClose = 0x0010;
        internal const uint WmQueryEndSession = 0x0011;
        internal const uint WmEndSession = 0x0016;
        internal const uint WmContextMenu = 0x007B;
        internal const uint WmGetIcon = 0x007F;
        internal const uint WmNcLButtonDown = 0x00A1;
        internal const uint WmLButtonUp = 0x0202;
        internal const uint WmLButtonDoubleClick = 0x0203;
        internal const uint WmRButtonUp = 0x0205;
        internal const uint WmApp = 0x8000;
        internal const uint TrayCallbackMessage = WmApp + 0x3F;

        internal const int HtCaption = 2;
        internal const int IconSmall = 0;
        internal const int IconSmall2 = 2;
        internal const int IdiApplication = 32512;
        internal const uint GwOwner = 4;

        internal const uint NimAdd = 0x00000000;
        internal const uint NimDelete = 0x00000002;
        internal const uint NifMessage = 0x00000001;
        internal const uint NifIcon = 0x00000002;
        internal const uint NifTip = 0x00000004;

        internal const uint MfString = 0x00000000;
        internal const uint MfSeparator = 0x00000800;
        internal const uint TpmRightButton = 0x0002;
        internal const uint TpmNoNotify = 0x0080;
        internal const uint TpmReturnCommand = 0x0100;

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

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
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

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumerateWindowsProcedure procedure, IntPtr state);

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

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLongPtr32(IntPtr windowHandle, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLongPtr32(IntPtr windowHandle, int index, int value);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW", SetLastError = true)]
        private static extern IntPtr GetClassLongPtr64(IntPtr windowHandle, int index);

        [DllImport("user32.dll", EntryPoint = "GetClassLongW", SetLastError = true)]
        private static extern uint GetClassLongPtr32(IntPtr windowHandle, int index);

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

        internal static IntPtr GetClassLongPointer(IntPtr windowHandle, int index)
        {
            return IntPtr.Size == 8
                ? GetClassLongPtr64(windowHandle, index)
                : new IntPtr(unchecked((int)GetClassLongPtr32(windowHandle, index)));
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
        internal static extern bool SetForegroundWindow(IntPtr windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReleaseCapture();

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

        [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

        [DllImport("user32.dll", EntryPoint = "LoadIconW", CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadIcon(IntPtr instanceHandle, IntPtr iconName);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AppendMenu(
            IntPtr menuHandle,
            uint flags,
            UIntPtr itemIdentifier,
            string itemText);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyMenu(IntPtr menuHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint TrackPopupMenu(
            IntPtr menuHandle,
            uint flags,
            int x,
            int y,
            int reserved,
            IntPtr ownerWindow,
            IntPtr reservedRectangle);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(
            IntPtr windowHandle,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter);

        [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint RegisterWindowMessage(string messageName);
    }
}
#endif

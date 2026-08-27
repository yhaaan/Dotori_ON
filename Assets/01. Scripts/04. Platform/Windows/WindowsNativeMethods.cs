#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace TeamOverlay.Platform.Windows
{
    internal static class WindowsNativeMethods
    {
        internal const int GwlStyle = -16;
        internal const int GwlWndProc = -4;

        internal const long WsCaption = 0x00C00000L;
        internal const long WsThickFrame = 0x00040000L;
        internal const long WsMinimizeBox = 0x00020000L;
        internal const long WsMaximizeBox = 0x00010000L;
        internal const long WsPopup = unchecked((long)0x80000000);
        internal const long WsVisible = 0x10000000L;

        internal const uint SwMinimize = 6;

        internal const uint SwpNoSize = 0x0001;
        internal const uint SwpNoMove = 0x0002;
        internal const uint SwpNoZOrder = 0x0004;
        internal const uint SwpNoActivate = 0x0010;
        internal const uint SwpFrameChanged = 0x0020;

        internal const uint WmClose = 0x0010;
        internal const uint WmQueryEndSession = 0x0011;
        internal const uint WmEndSession = 0x0016;
        internal const uint WmNcLButtonDown = 0x00A1;

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

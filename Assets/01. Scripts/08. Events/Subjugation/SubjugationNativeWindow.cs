using System;
using DOTORION.Platform.Windows;
using UnityEngine;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
#endif

namespace DOTORION.Events.Subjugation
{
    /// <summary>Optional owned, per-pixel alpha window. It never changes Unity's window styles or swap chain.</summary>
    public sealed class SubjugationNativeWindow : IDisposable
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static SubjugationNativeWindow _active;
        private static readonly WindowProc Procedure = Dispatch;
        private IntPtr _handle, _dc, _bitmap, _previousBitmap, _bits;
        private byte[] _bytes;
        private int _width, _height, _shopRequested, _roundRequested, _autoRequested;
        public bool AutoHovered { get; private set; }
        private volatile bool _dragging, _docked = true;
        private bool _moved, _placed;
        private volatile bool _integrated;
        private Thread _thread;
        private IntPtr _ownerHandle;
        private Point _press, _origin;
        private const string ClassName = "DOTORION.Subjugation.v1";

        public bool IsAvailable => _handle != IntPtr.Zero;
        public bool ConsumeRoundRequest() => Interlocked.Exchange(ref _roundRequested, 0) != 0;
        public bool ConsumeAutoRequest() => Interlocked.Exchange(ref _autoRequested, 0) != 0;
        public bool ConsumeShopRequest() => Interlocked.Exchange(ref _shopRequested, 0) != 0;

        public void Present(WindowsOverlayWindow owner, Color32[] pixels, bool visible, int sourceWidth = 480, int sourceHeight = 64, bool integrated = false)
        {
            _integrated = integrated;
            var main = owner.NativeHandle;
            if (main == IntPtr.Zero) return;
            _ownerHandle = main;
            if (_handle == IntPtr.Zero)
            {
                if (!visible) return;
                if (_thread != null) return;
                _active = this;
                // Give the optional window its own message loop. Unity's window pump is not a contract
                // for windows created from the scripting thread; using it can leave dragging inert.
                _thread = new Thread(RunWindow) { IsBackground = true, Name = "DOTORION event window" };
                _thread.Start();
                return;
            }
            GetCursorPos(out var hover); GetWindowRect(_handle, out var hoverRect);
            var hx = hover.x - hoverRect.left; var hy = hover.y - hoverRect.top;
            AutoHovered = visible && hx >= _width * 453 / 480 && hx < _width * 477 / 480 && hy >= _height * 22 / 64 && hy < _height * 40 / 64;
            if (!visible || IsIconic(main) || !IsWindowVisible(main)) { ShowWindow(_handle, 0); return; }
            var width = Mathf.RoundToInt(480 * owner.UiScale);
            var height = Mathf.RoundToInt(64 * owner.UiScale);
            if (_width != width || _height != height) Allocate(width, height);
            if (_bits == IntPtr.Zero) return;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var p = pixels[(sourceHeight - 1 - y * sourceHeight / height) * sourceWidth + x * sourceWidth / width];
                var offset = (y * width + x) * 4;
                _bytes[offset] = (byte)(p.b * p.a / 255);
                _bytes[offset + 1] = (byte)(p.g * p.a / 255);
                _bytes[offset + 2] = (byte)(p.r * p.a / 255);
                _bytes[offset + 3] = p.a;
            }
            Marshal.Copy(_bytes, 0, _bits, _bytes.Length);
            GetWindowRect(_handle, out var rect);
            var position = new Point { x = rect.left, y = rect.top };
            if (!_placed && owner.IsMiniMode && GetWindowRect(main, out var mini))
            {
                _docked = false; position = new Point { x = mini.left, y = Math.Max(0, mini.top - height) };
            }
            _placed = true;
            if ((_integrated || _docked) && !_dragging && GetWindowRect(main, out var parent))
                position = new Point { x = parent.left, y = _integrated ? parent.top : parent.top - height };
            var size = new Point { x = width, y = height };
            var source = new Point();
            var blend = new Blend { operation = 0, alpha = 255, format = 1 };
            UpdateLayeredWindow(_handle, IntPtr.Zero, ref position, ref size, _dc, ref source, 0, ref blend, 2);
            // Follow the main window's always-on-top preference without taking focus.
            var top = (GetWindowLong(main, -20) & 8) != 0;
            SetWindowPos(_handle, new IntPtr(top ? -1 : -2), 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
            ShowWindow(_handle, 4);
        }

        private void RunWindow()
        {
            var wc = new WindowClass { size = (uint)Marshal.SizeOf<WindowClass>(), procedure = Marshal.GetFunctionPointerForDelegate(Procedure), instance = GetModuleHandle(null), name = ClassName, cursor = LoadCursor(IntPtr.Zero, new IntPtr(32512)) };
            if (RegisterClassEx(ref wc) == 0 && Marshal.GetLastWin32Error() != 1410) return;
            _handle = CreateWindowEx(0x00080000 | 0x00000080 | 0x08000000, ClassName, "치이카와 토벌 대작전", 0x80000000, 0, 0, 480, 64, _ownerHandle, IntPtr.Zero, wc.instance, IntPtr.Zero);
            if (_handle == IntPtr.Zero) return;
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref message); DispatchMessage(ref message); }
            _handle = IntPtr.Zero;
        }

        private void Allocate(int width, int height)
        {
            ReleaseSurface();
            _width = width; _height = height;
            _dc = CreateCompatibleDC(IntPtr.Zero);
            var info = new BitmapInfo { size = 40, width = width, height = -height, planes = 1, bitCount = 32 };
            _bitmap = CreateDIBSection(_dc, ref info, 0, out _bits, IntPtr.Zero, 0);
            if (_bitmap == IntPtr.Zero) return;
            _previousBitmap = SelectObject(_dc, _bitmap);
            _bytes = new byte[width * height * 4];
        }

        [MonoPInvokeCallback(typeof(WindowProc))]
        private static IntPtr Dispatch(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            var self = _active;
            if (self == null) return DefWindowProc(hwnd, message, wParam, lParam);
            if (message == 0x21) return new IntPtr(3); // MA_NOACTIVATE
            if (message == 0x201)
            {
                var x = (short)(lParam.ToInt64() & 0xffff);
                var y = (short)((lParam.ToInt64() >> 16) & 0xffff);
                if (x >= self._width * 348 / 480 && y >= self._height * 43 / 64)
                {
                    Interlocked.Exchange(ref self._shopRequested, 1);
                    return IntPtr.Zero;
                }
                if (x >= self._width * 348 / 480 && y >= self._height * 22 / 64 && y < self._height * 40 / 64)
                {
                    if (x >= self._width * 453 / 480) Interlocked.Exchange(ref self._autoRequested, 1);
                    else Interlocked.Exchange(ref self._roundRequested, 1);
                    return IntPtr.Zero;
                }
                if (self._integrated) return IntPtr.Zero;
                GetCursorPos(out self._press);
                GetWindowRect(hwnd, out var r);
                self._origin = new Point { x = r.left, y = r.top };
                self._dragging = true; self._moved = false;
                SetCapture(hwnd);
                return IntPtr.Zero;
            }
            if (message == 0x200 && self._dragging)
            {
                GetCursorPos(out var point);
                if (Math.Abs(point.x - self._press.x) + Math.Abs(point.y - self._press.y) > 4) self._moved = true;
                if (self._moved)
                {
                    self._docked = false;
                    SetWindowPos(hwnd, IntPtr.Zero, self._origin.x + point.x - self._press.x, self._origin.y + point.y - self._press.y, 0, 0, 0x1 | 0x4 | 0x10);
                }
                return IntPtr.Zero;
            }
            if (message == 0x202 && self._dragging)
            {
                self._dragging = false;
                ReleaseCapture();
                GetWindowRect(hwnd, out var r);
                if (GetWindowRect(self._ownerHandle, out var main))
                    self._docked = Math.Abs(r.left - main.left) < 32 && Math.Abs(r.bottom - main.top) < 24;
                return IntPtr.Zero;
            }
            if (message == 0x215) self._dragging = false; // capture lost
            if (message == 0x10) { DestroyWindow(hwnd); return IntPtr.Zero; }
            if (message == 0x2) { PostQuitMessage(0); return IntPtr.Zero; }
            if (message == 0x203) { self._docked = true; return IntPtr.Zero; }
            return DefWindowProc(hwnd, message, wParam, lParam);
        }

        private void ReleaseSurface()
        {
            if (_previousBitmap != IntPtr.Zero && _dc != IntPtr.Zero) SelectObject(_dc, _previousBitmap);
            if (_bitmap != IntPtr.Zero) DeleteObject(_bitmap);
            if (_dc != IntPtr.Zero) DeleteDC(_dc);
            _bitmap = _dc = _previousBitmap = _bits = IntPtr.Zero;
        }
        public void Dispose()
        {
            if (_handle != IntPtr.Zero) PostMessage(_handle, 0x10, IntPtr.Zero, IntPtr.Zero);
            _thread?.Join(1000);
            _thread = null;
            _handle = IntPtr.Zero;
            ReleaseSurface();
            if (_active == this) _active = null;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WindowProc(IntPtr h, uint m, IntPtr w, IntPtr l);
        [StructLayout(LayoutKind.Sequential)] private struct Point { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeMessage { public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public Point point; public uint reserved; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { public byte operation, flags, alpha, format; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass { public uint size, style; public IntPtr procedure; public int classExtra, windowExtra; public IntPtr instance, icon, cursor, background; public string menu, name; public IntPtr smallIcon; }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public uint size; public int width, height; public ushort planes, bitCount; public uint compression, imageSize; public int xPixels, yPixels; public uint used, important; }
        [DllImport("user32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass c);
        [DllImport("kernel32", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string n);
        [DllImport("user32", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint e, string c, string n, uint s, int x, int y, int w, int h, IntPtr p, IntPtr menu, IntPtr i, IntPtr param);
        [DllImport("user32", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int GetMessage(out NativeMessage m, IntPtr h, uint min, uint max);
        [DllImport("user32")] private static extern bool TranslateMessage(ref NativeMessage m);
        [DllImport("user32", CharSet = CharSet.Unicode)] private static extern IntPtr DispatchMessage(ref NativeMessage m);
        [DllImport("user32", CharSet = CharSet.Unicode)] private static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32")] private static extern void PostQuitMessage(int code);
        [DllImport("user32", CharSet = CharSet.Unicode)] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
        [DllImport("user32")] private static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32")] private static extern bool GetWindowRect(IntPtr h, out NativeRect r);
        [DllImport("user32")] private static extern bool IsIconic(IntPtr h);
        [DllImport("user32")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32")] private static extern bool ShowWindow(IntPtr h, int c);
        [DllImport("user32")] private static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int z, uint f);
        [DllImport("user32")] private static extern bool GetCursorPos(out Point p);
        [DllImport("user32")] private static extern IntPtr SetCapture(IntPtr h);
        [DllImport("user32")] private static extern bool ReleaseCapture();
        [DllImport("user32")] private static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dest, ref Point p, ref Point size, IntPtr dc, ref Point src, uint key, ref Blend b, uint flags);
        [DllImport("gdi32")] private static extern IntPtr CreateCompatibleDC(IntPtr h);
        [DllImport("gdi32")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint use, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32")] private static extern bool DeleteObject(IntPtr o);
        [DllImport("gdi32")] private static extern bool DeleteDC(IntPtr dc);
#else
        public bool IsAvailable => false;
        public bool AutoHovered => false;
        public bool ConsumeRoundRequest() => false;
        public bool ConsumeAutoRequest() => false;
        public bool ConsumeShopRequest() => false;
        public void Present(WindowsOverlayWindow owner, Color32[] pixels, bool visible, int sourceWidth = 480, int sourceHeight = 64, bool integrated = false) { }
        public void Dispose() { }
#endif
    }
}

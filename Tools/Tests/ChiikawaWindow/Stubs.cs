using System;
namespace UnityEngine
{
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r=r; this.g=g; this.b=b; this.a=a; } }
    public static class Mathf { public static int RoundToInt(float x) => (int)Math.Round(x); }
}
namespace AOT { public sealed class MonoPInvokeCallbackAttribute : Attribute { public MonoPInvokeCallbackAttribute(Type t) {} } }
namespace DOTORION.Platform.Windows { public sealed class WindowsOverlayWindow { public IntPtr NativeHandle; public float UiScale = 1; public bool IsMiniMode; } }

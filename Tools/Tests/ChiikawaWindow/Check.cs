using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using DOTORION.Events.Subjugation;
using DOTORION.Platform.Windows;
using UnityEngine;

public static class ChiikawaNativeCheck
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int l,t,r,b; }
    [StructLayout(LayoutKind.Sequential)] private struct Msg { public IntPtr h; public uint m; public UIntPtr w; public IntPtr l; public uint time; public int x,y; public uint reserved; }
    [DllImport("user32",CharSet=CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint e,string c,string n,uint s,int x,int y,int w,int h,IntPtr p,IntPtr menu,IntPtr inst,IntPtr param);
    [DllImport("user32",CharSet=CharSet.Unicode)] private static extern int GetMessage(out Msg m,IntPtr h,uint min,uint max);
    [DllImport("user32")] private static extern bool TranslateMessage(ref Msg m);
    [DllImport("user32",CharSet=CharSet.Unicode)] private static extern IntPtr DispatchMessage(ref Msg m);
    [DllImport("user32",CharSet=CharSet.Unicode)] private static extern IntPtr FindWindow(string c,string n);
    [DllImport("user32")] private static extern bool GetWindowRect(IntPtr h,out Rect r);
    [DllImport("user32")] private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int z,uint f);
    [DllImport("user32",CharSet=CharSet.Unicode)] private static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("user32",CharSet=CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("user32")] private static extern bool ShowWindow(IntPtr h,int n);
    [DllImport("user32")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32")] private static extern bool PostThreadMessage(uint id,uint msg,IntPtr w,IntPtr l);
    [DllImport("kernel32")] private static extern uint GetCurrentThreadId();
    private static void Check(bool value,string label) { if(!value) throw new Exception("FAIL: "+label); Console.WriteLine("PASS: "+label); }

    public static void Run()
    {
        var ready=new ManualResetEventSlim(); IntPtr owner=IntPtr.Zero; uint threadId=0;
        var thread=new Thread(()=>
        {
            threadId=GetCurrentThreadId();
            owner=CreateWindowEx(0x08000080,"STATIC","DOTORION native test owner",0x80000000,200,200,480,220,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
            ShowWindow(owner,4); ready.Set();
            while(GetMessage(out var msg,IntPtr.Zero,0,0)>0) { TranslateMessage(ref msg); DispatchMessage(ref msg); }
        }) { IsBackground=true };
        thread.Start();
        if (!ready.Wait(5000)) throw new Exception("Test owner creation timed out");
        var native=new SubjugationNativeWindow();
        try
        {
            var window=new WindowsOverlayWindow { NativeHandle=owner };
            var pixels=new Color32[480*64];
            native.Present(window,pixels,false);
            Check(!native.IsAvailable,"full mode does not create an independent event window");
            for(var y=0;y<15;y++) for(var x=0;x<480;x++) pixels[y*480+x]=new Color32(180,220,255,180);
            for(var i=0;i<30;i++) { native.Present(window,pixels,true); Thread.Sleep(20); if(native.IsAvailable && i>3) break; }
            var h=(IntPtr)typeof(SubjugationNativeWindow).GetField("_handle",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(native);
            Check(h!=IntPtr.Zero,"native window created");
            GetWindowRect(h,out var r); GetWindowRect(owner,out var main);
            Check(r.l==main.l && r.b==main.t && r.r-r.l==480 && r.b-r.t==64,"480x64 docked above owner");
            Check(SendMessage(h,0x21,IntPtr.Zero,IntPtr.Zero).ToInt32()==3,"no activation on click");
            PostMessage(h,0x201,IntPtr.Zero,new IntPtr((55<<16)|400)); Thread.Sleep(40);
            Check(native.ConsumeShopRequest(),"currency click opens roster request");
            Check(!native.ConsumeShopRequest(),"click request consumed once");
            SendMessage(h,0x201,IntPtr.Zero,new IntPtr((30<<16)|400));
            Check(native.ConsumeRoundRequest(),"round click opens round selector");
            Check(!native.ConsumeRoundRequest(),"round request consumed once");
            SendMessage(h,0x201,IntPtr.Zero,new IntPtr((30<<16)|465));
            Check(native.ConsumeAutoRequest(),"auto advance click toggles option");
            Check(!native.ConsumeAutoRequest(),"toggle request consumed once");
            native.Present(window,pixels,false); Check(!IsWindowVisible(h),"toggle hides window");
            native.Present(window,pixels,true); Check(IsWindowVisible(h),"toggle restores window");
            typeof(SubjugationNativeWindow).GetField("_docked",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(native,false);
            SetWindowPos(h,IntPtr.Zero,300,400,0,0,0x1|0x4|0x10);
            SetWindowPos(owner,IntPtr.Zero,230,230,0,0,0x1|0x4|0x10);
            native.Present(window,pixels,true); GetWindowRect(h,out r);
            Check(r.l==300 && r.t==400,"detached window stays independent of owner");
            typeof(SubjugationNativeWindow).GetField("_docked",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(native,true);
            native.Present(window,pixels,true); GetWindowRect(h,out r); GetWindowRect(owner,out main);
            Check(r.l==main.l && r.b==main.t,"redock follows owner");
            window.UiScale=2; native.Present(window,pixels,true); GetWindowRect(h,out r);
            Check(r.r-r.l==960 && r.b-r.t==128,"pixel window follows 200 percent UI scale");
            var highResolution = new Color32[960 * 128];
            highResolution[0] = new Color32(100, 200, 50, 255);
            native.Present(window, highResolution, true, 960, 128);
            var nativeBytes = (byte[])typeof(SubjugationNativeWindow).GetField("_bytes", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(native);
            var bottomLeft = 127 * 960 * 4;
            Check(nativeBytes[bottomLeft] == 50 && nativeBytes[bottomLeft + 1] == 200 && nativeBytes[bottomLeft + 2] == 100,
                "high-resolution source maps directly to physical window pixels");
            native.Present(window, highResolution, true, 960, 128, true);
            GetWindowRect(h, out r); GetWindowRect(owner, out main);
            Check(r.l == main.l && r.t == main.t, "integrated alpha layer covers the reserved strip inside owner");
            highResolution[0] = new Color32(0, 0, 0, 153);
            native.Present(window, highResolution, true, 960, 128, true);
            nativeBytes = (byte[])typeof(SubjugationNativeWindow).GetField("_bytes", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(native);
            Check(nativeBytes[bottomLeft] == 0 && nativeBytes[bottomLeft + 1] == 0 && nativeBytes[bottomLeft + 2] == 0 && nativeBytes[bottomLeft + 3] == 153,
                "result background stays black with 60 percent per-pixel alpha");
            SendMessage(h, 0x201, IntPtr.Zero, new IntPtr((10 << 16) | 100));
            Check(!(bool)typeof(SubjugationNativeWindow).GetField("_dragging", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(native),
                "integrated result layer cannot detach");
            native.Present(window, highResolution, true, 960, 128);
            GetWindowRect(h, out r); GetWindowRect(owner, out main);
            Check(r.b == main.t, "leaving integrated result restores above-owner position");
            native.Dispose();
            Check(!native.IsAvailable,"native resources and window closed");
            window.IsMiniMode = true; native = new SubjugationNativeWindow();
            for(var i=0;i<30;i++) { native.Present(window,pixels,true); Thread.Sleep(20); if(native.IsAvailable && i>3) break; }
            var miniHandle=(IntPtr)typeof(SubjugationNativeWindow).GetField("_handle",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(native);
            Check(!(bool)typeof(SubjugationNativeWindow).GetField("_docked",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(native),"mini mode starts detached");
            GetWindowRect(miniHandle,out var detached);
            SetWindowPos(owner,IntPtr.Zero,260,300,0,0,0x1|0x4|0x10);
            native.Present(window,pixels,true); GetWindowRect(miniHandle,out var movedMini);
            Check(detached.l==movedMini.l && detached.t==movedMini.t,"mini event position stays independent during main window movement");
        }
        finally
        {
            native.Dispose(); PostMessage(owner,0x10,IntPtr.Zero,IntPtr.Zero);
            PostThreadMessage(threadId,0x12,IntPtr.Zero,IntPtr.Zero); thread.Join(1000);
        }
    }
}

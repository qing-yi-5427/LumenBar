using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Windows.Graphics;
namespace LumenBar.WinUI;
internal static class Desktop
{
    [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;public int Width=>Right-Left;public int Height=>Bottom-Top;}
    [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
    [StructLayout(LayoutKind.Sequential)] struct Margins {public int Left,Right,Top,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct BlurBehind {public uint Flags;public int Enable;public nint Region;public int TransitionOnMaximized;}
    public static nint Handle(Window w)=>WinRT.Interop.WindowNative.GetWindowHandle(w);
    public static double Scale(Window w)=>GetDpiForWindow(Handle(w))/96d;
    public static Rect Bounds(Window w){GetWindowRect(Handle(w),out var r);return r;}
    public static Rect Work(Window w){var m=new MonitorInfo {Size=Marshal.SizeOf<MonitorInfo>()};GetMonitorInfo(MonitorFromWindow(Handle(w),2),ref m);return m.Work;}
    public static Rect WorkAt(int x,int y){var m=new MonitorInfo {Size=Marshal.SizeOf<MonitorInfo>()};GetMonitorInfo(MonitorFromPoint(new Point {X=x,Y=y},2),ref m);return m.Work;}
    public static Point Cursor(){GetCursorPos(out var p);return p;}
    public static void Configure(Window w,bool topmost,bool resize)
    {
        var presenter=(OverlappedPresenter)w.AppWindow.Presenter;presenter.SetBorderAndTitleBar(false,false);presenter.IsResizable=resize;presenter.IsMaximizable=false;presenter.IsMinimizable=false;presenter.IsAlwaysOnTop=topmost;
        w.AppWindow.IsShownInSwitchers=false;
        var h=Handle(w);SetWindowLongPtr(h,-20,new nint((GetWindowLongPtr(h,-20).ToInt64()|0x80)&~0x40000));
        int none=1;DwmSetWindowAttribute(h,33,ref none,4);
        int noBorder=unchecked((int)0xFFFFFFFE);DwmSetWindowAttribute(h,34,ref noBorder,4);
        // A window region is a hard visual clip, not just a hit-test boundary.
        SetWindowRgn(h,0,true);
    }
    public static void ConfigureSettings(Window w)
    {
        var p=(OverlappedPresenter)w.AppWindow.Presenter;p.SetBorderAndTitleBar(true,true);p.IsResizable=true;p.IsMaximizable=false;
        w.AppWindow.TitleBar.PreferredTheme=Microsoft.UI.Windowing.TitleBarTheme.UseDefaultAppMode;
        int round=2;DwmSetWindowAttribute(Handle(w),33,ref round,4);
    }
    // A transparent Composition brush alone does not clear the HWND's GDI backing surface.
    // Based on WinUIEx TransparentTintBackdrop (MIT; see THIRD_PARTY_NOTICES.txt).
    public static void ConfigureTransparentClient(Window w)
    {
        var h=Handle(w);var margins=new Margins();Marshal.ThrowExceptionForHR(DwmExtendFrameIntoClientArea(h,ref margins));
        var region=CreateRectRgn(-2,-2,-1,-1);
        try
        {
            var blur=new BlurBehind {Flags=3,Enable=1,Region=region};
            Marshal.ThrowExceptionForHR(DwmEnableBlurBehindWindow(h,ref blur));
        }
        finally{DeleteObject(region);}
        var dc=GetDC(h);
        if(dc!=0)try{ClearTransparentClient(h,dc);}finally{ReleaseDC(h,dc);}
    }
    public static bool ClearTransparentClient(nint window,nint dc)
    {
        if(dc==0 || !GetClientRect(window,out var rect))return false;
        // BLACK_BRUSH writes zero RGB and zero alpha into the redirected surface.
        return FillRect(dc,ref rect,GetStockObject(4))!=0;
    }
    public static void Lock(Window w,bool locked)
    {
        var h=Handle(w);long style=GetWindowLongPtr(h,-20).ToInt64();SetWindowLongPtr(h,-20,new nint(locked?style|0x80020:style&~0x80020));
        if(locked)SetLayeredWindowAttributes(h,0,255,2);
        ((OverlappedPresenter)w.AppWindow.Presenter).IsResizable=false;
    }
    public static bool Locked(Window w)=>(GetWindowLongPtr(Handle(w),-20).ToInt64()&0x80020)==0x80020;
    public static void RefreshFrame(Window w)=>SetWindowPos(Handle(w),0,0,0,0,0,0x27);
    public static bool Hotkey(Window w,int id,uint key)=>RegisterHotKey(Handle(w),id,0x4003,key);
    public static void RemoveHotkeys(Window w){UnregisterHotKey(Handle(w),701);UnregisterHotKey(Handle(w),702);}
    public delegate nint SubclassProc(nint h,uint msg,nint w,nint l,nuint id,nuint data);
    [DllImport("comctl32.dll")] public static extern bool SetWindowSubclass(nint h,SubclassProc proc,nuint id,nuint data);
    [DllImport("comctl32.dll")] public static extern bool RemoveWindowSubclass(nint h,SubclassProc proc,nuint id);
    [DllImport("comctl32.dll")] public static extern nint DefSubclassProc(nint h,uint msg,nint w,nint l);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(nint h,int id,uint mod,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint h,int id);
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint h,out Rect r);
    [DllImport("user32.dll")] static extern bool GetClientRect(nint h,out Rect r);
    [DllImport("user32.dll")] static extern nint GetDC(nint h);
    [DllImport("user32.dll")] static extern int ReleaseDC(nint h,nint dc);
    [DllImport("user32.dll")] static extern int FillRect(nint dc,ref Rect rect,nint brush);
    [DllImport("gdi32.dll")] static extern nint GetStockObject(int objectType);
    [DllImport("user32.dll")] static extern nint MonitorFromWindow(nint h,int flags);
    [DllImport("user32.dll")] static extern nint MonitorFromPoint(Point p,int flags);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point p);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(nint h,ref MonitorInfo info);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(nint h);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(nint h,uint key,byte alpha,uint flags);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern nint GetWindowLongPtr(nint h,int i);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern nint SetWindowLongPtr(nint h,int i,nint value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("gdi32.dll")] static extern nint CreateRectRgn(int l,int t,int r,int b);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint h);
    [DllImport("user32.dll")] static extern int SetWindowRgn(nint h,nint r,bool redraw);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(nint h,int attribute,ref int value,int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(nint h,ref Margins margins);
    [DllImport("dwmapi.dll")] static extern int DwmEnableBlurBehindWindow(nint h,ref BlurBehind blur);
}

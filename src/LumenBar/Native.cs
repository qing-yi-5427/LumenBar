using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Screen=System.Windows.Forms.Screen;

namespace LumenBar;

public static class Native
{
    public record Display(string Id,string Label,int X,int Y,int Width,int Height,int WorkX,int WorkY,int WorkWidth,int WorkHeight,uint Dpi);
    public static List<Display> Displays() => Screen.AllScreens.Select((s,i)=>
    {
        var monitor=MonitorFromPoint(new PointI { X=s.Bounds.Left+1,Y=s.Bounds.Top+1 },2);
        uint dpi=96; GetDpiForMonitor(monitor,0,out dpi,out _);
        return new Display(s.DeviceName,$"显示器 {i+1}{(s.Primary ? " · 主屏" : "")} · {s.Bounds.Width}×{s.Bounds.Height}",s.Bounds.X,s.Bounds.Y,s.Bounds.Width,s.Bounds.Height,s.WorkingArea.X,s.WorkingArea.Y,s.WorkingArea.Width,s.WorkingArea.Height,dpi == 0 ? 96 : dpi);
    }).ToList();
    public static Display Target(string id) => Displays().FirstOrDefault(x=>x.Id==id) ?? Displays().First(x=>x.Id==Screen.PrimaryScreen!.DeviceName);
    public static bool Dark(Preferences p) => p.Theme=="Dark" || p.Theme=="Wallpaper" && WallpaperLuminance()<0.55 || p.Theme=="System" && (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) as int? ?? 1)==0;
    static double WallpaperLuminance(){var c=WallpaperSample();return(c.R*.2126+c.G*.7152+c.B*.0722)/255;}
    static Color? wallpaperCache;
    public static void InvalidateWallpaper()=>wallpaperCache=null;
    static Color WallpaperSample()
    {
        if(wallpaperCache is Color c)return c;
        try
        {
            var sb=new StringBuilder(2048);SystemParametersInfo(0x73,(uint)sb.Capacity,sb,0);
            var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=new Uri(sb.ToString());bitmap.DecodePixelWidth=24;bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();
            var converted=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);var bytes=new byte[converted.PixelWidth*converted.PixelHeight*4];converted.CopyPixels(bytes,converted.PixelWidth*4,0);
            long r=0,g=0,b=0;for(int i=0;i<bytes.Length;i+=4){b+=bytes[i];g+=bytes[i+1];r+=bytes[i+2];}int n=bytes.Length/4;
            return (wallpaperCache=Color.FromRgb((byte)(r/n),(byte)(g/n),(byte)(b/n))).Value;
        }
        catch{return (wallpaperCache=Color.FromRgb(32,49,60)).Value;}
    }
    public static Color WallpaperColor(bool dark)
    {
        var sample=WallpaperSample();double baseline=dark?24:244;
        return Color.FromRgb((byte)(baseline*.78+sample.R*.22),(byte)(baseline*.78+sample.G*.22),(byte)(baseline*.78+sample.B*.22));
    }
    public static bool Material(Window w,Preferences preferences,bool moving=false)
    {
        bool dark=Dark(preferences);
        var handle=new WindowInteropHelper(w).Handle;
        if(handle==IntPtr.Zero) return false;
        var source=HwndSource.FromHwnd(handle); if(source?.CompositionTarget is not null) source.CompositionTarget.BackgroundColor=Colors.Transparent;
        int mode=dark?1:0;DwmSetWindowAttribute(handle,20,ref mode,4);
        int corner=2;DwmSetWindowAttribute(handle,33,ref corner,4);
        int noBorder=-2;DwmSetWindowAttribute(handle,34,ref noBorder,4);
        var margins=new Margins { Left=-1,Right=-1,Top=-1,Bottom=-1 };DwmExtendFrameIntoClientArea(handle,ref margins);
        // Explicit accent policy keeps the blur independent of focus. WPF only
        // paints a faint highlight above it; a thick content fill would hide the blur.
        int material=1;DwmSetWindowAttribute(handle,38,ref material,4);
        var tint=preferences.WallpaperTint?WallpaperColor(dark):(dark?Color.FromRgb(25,34,42):Color.FromRgb(240,245,249));
        bool permitted=!SystemParameters.HighContrast && (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","EnableTransparency",1) as int? ?? 1)!=0;
        double alpha=dark?preferences.GlassOpacity:Math.Min(.94,.40+preferences.GlassOpacity*.65);
        var accent=new Accent { State=permitted?(moving?3:4):0,Flags=2,Color=((uint)(alpha*255)<<24)|((uint)tint.B<<16)|((uint)tint.G<<8)|tint.R };
        var ptr=Marshal.AllocHGlobal(Marshal.SizeOf<Accent>());bool applied=false;
        try
        {
            Marshal.StructureToPtr(accent,ptr,false);var data=new CompositionData { Attribute=19,Data=ptr,Size=Marshal.SizeOf<Accent>() };
            applied=SetWindowCompositionAttribute(handle,ref data)!=0 && permitted;
        }
        catch(EntryPointNotFoundException) { }
        finally{Marshal.FreeHGlobal(ptr);}
        if(!applied && permitted){material=3;applied=DwmSetWindowAttribute(handle,38,ref material,4)==0;}
        var style=GetWindowLongPtr(handle,-20).ToInt64();SetWindowLongPtr(handle,-20,new IntPtr((style | 0x80) & ~0x40000));
        return applied;
    }
    public static void Place(Window w,int x,int y,int width,int height,bool topmost=true)
    {
        var h=new WindowInteropHelper(w).Handle;
        if(h!=IntPtr.Zero) SetWindowPos(h,new IntPtr(topmost?-1:-2),x,y,width,height,0x10);
    }
    public static RectI WindowRect(Window w){GetWindowRect(new WindowInteropHelper(w).Handle,out var r);return r;}
    public static bool RegisterUnlock(Window w) => RegisterHotKey(new WindowInteropHelper(w).Handle,701,0x4003,0x4C);
    public static bool RegisterSettings(Window w)=>RegisterHotKey(new WindowInteropHelper(w).Handle,702,0x4003,0x53);
    public static void RemoveUnlock(Window w){UnregisterHotKey(new WindowInteropHelper(w).Handle,701);UnregisterHotKey(new WindowInteropHelper(w).Handle,702);}
    public static void ClickThrough(Window w,bool enabled)
    {
        var h=new WindowInteropHelper(w).Handle;var style=GetWindowLongPtr(h,-20).ToInt64();
        style=enabled ? style | 0x80020 : style & ~0x20;
        if(!w.AllowsTransparency && !enabled)style&=~0x80000;
        SetWindowLongPtr(h,-20,new IntPtr(style));
        if(enabled && !w.AllowsTransparency)SetLayeredWindowAttributes(h,0,255,2);
    }
    public static bool IsClickThrough(Window w)=>(GetWindowLongPtr(new WindowInteropHelper(w).Handle,-20).ToInt64() & 0x80020)==0x80020;
    public static long Style(Window w)=>GetWindowLongPtr(new WindowInteropHelper(w).Handle,-20).ToInt64();
    [StructLayout(LayoutKind.Sequential)] public struct RectI { public int Left,Top,Right,Bottom; public int Width=>Right-Left;public int Height=>Bottom-Top; }
    [StructLayout(LayoutKind.Sequential)] struct PointI { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] struct Margins { public int Left,Right,Top,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Accent { public int State,Flags;public uint Color;public int Animation; }
    [StructLayout(LayoutKind.Sequential)] struct CompositionData {public int Attribute;public IntPtr Data;public int Size;}
    [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr h,ref CompositionData data);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out RectI r);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(PointI point,int flags);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h,uint key,byte alpha,uint flags);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h,ref Margins margins);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool SystemParametersInfo(uint action,uint param,StringBuilder value,uint flags);
}

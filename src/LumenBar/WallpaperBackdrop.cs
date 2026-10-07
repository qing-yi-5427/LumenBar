using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace LumenBar;
// Reads wallpaper files, never screenshots or other applications' surfaces.
public static class WallpaperBackdrop
{
    sealed record MonitorImage(Rect Bounds,BitmapSource? Image);
    static List<MonitorImage>? monitors;
    static int position=4;
    static Color background=Color.FromRgb(24,38,48);
    public static void Invalidate()=>monitors=null;
    static void Load()
    {
        if(monitors is not null)return;
        monitors=new();IDesktopWallpaper? desktop=null;
        try
        {
            desktop=(IDesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"))!)!;
            desktop.GetPosition(out position);desktop.GetBackgroundColor(out uint c);background=Color.FromRgb((byte)c,(byte)(c>>8),(byte)(c>>16));
            desktop.GetMonitorDevicePathCount(out uint count);
            for(uint i=0;i<count;i++)
            {
                desktop.GetMonitorDevicePathAt(i,out string id);desktop.GetMonitorRECT(id,out var r);desktop.GetWallpaper(id,out string path);
                BitmapSource? bitmap=null;
                if(File.Exists(path))
                {var b=new BitmapImage();b.BeginInit();b.UriSource=new Uri(path);b.CacheOption=BitmapCacheOption.OnLoad;b.EndInit();b.Freeze();bitmap=b;}
                monitors.Add(new(new Rect(r.Left,r.Top,r.Width,r.Height),bitmap));
            }
        }
        catch(COMException){ }
        catch(IOException){ }
        catch(NotSupportedException){ }
        finally{if(desktop is not null)Marshal.ReleaseComObject(desktop);}
    }
    public static BitmapSource Sample(Rect screen,int width,int height)
    {
        Load();width=Math.Max(1,width);height=Math.Max(1,height);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(background),null,new Rect(0,0,width,height));
            dc.PushTransform(new ScaleTransform(width/screen.Width,height/screen.Height));dc.PushTransform(new TranslateTransform(-screen.X,-screen.Y));
            Rect span=Rect.Empty;foreach(var m in monitors!)span.Union(m.Bounds);
            foreach(var m in monitors!)
            {
                if(m.Image is not {} image || !m.Bounds.IntersectsWith(screen))continue;
                var bounds=position==5?span:m.Bounds;
                dc.PushClip(new RectangleGeometry(m.Bounds));
                if(position==1)
                {
                    var brush=new ImageBrush(image){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(bounds.X,bounds.Y,image.PixelWidth,image.PixelHeight),Stretch=Stretch.Fill};
                    dc.DrawRectangle(brush,null,bounds);
                }
                else
                {
                    double w=image.PixelWidth,h=image.PixelHeight;
                    if(position==2){w=bounds.Width;h=bounds.Height;}
                    else if(position is 3 or 4 or 5)
                    {double factor=position==3?Math.Min(bounds.Width/w,bounds.Height/h):Math.Max(bounds.Width/w,bounds.Height/h);w*=factor;h*=factor;}
                    dc.DrawImage(image,new Rect(bounds.X+(bounds.Width-w)/2,bounds.Y+(bounds.Height-h)/2,w,h));
                }
                dc.Pop();
            }
            dc.Pop();dc.Pop();
        }
        var output=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);output.Render(visual);output.Freeze();return output;
    }
    // Methods appear in Windows SDK vtable order; only the read calls are used.
    [ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string? id,[MarshalAs(UnmanagedType.LPWStr)]string wallpaper);
        void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string? id,[MarshalAs(UnmanagedType.LPWStr)]out string wallpaper);
        void GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)]out string id);
        void GetMonitorDevicePathCount(out uint count);
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)]string id,out Native.RectI rect);
        void SetBackgroundColor(uint color);void GetBackgroundColor(out uint color);
        void SetPosition(int position);void GetPosition(out int position);
    }
}

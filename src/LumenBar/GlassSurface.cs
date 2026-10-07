using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
namespace LumenBar;
public static class Ui
{
    public static TextBlock Text(string value,double size=13,string brush="TextBrush")
    {var t=new TextBlock {Text=value,FontSize=size};t.SetResourceReference(TextBlock.ForegroundProperty,brush);return t;}
    public static Button Button(string value,Action action,string? tip=null)
    {var b=new Button {Content=value,ToolTip=tip};b.Click+=(_,_)=>action();return b;}
}
public sealed class GlassSurface : Grid
{
    readonly System.Windows.Shapes.Rectangle backdrop=new() {IsHitTestVisible=false};
    readonly Border progress=new() {IsHitTestVisible=false,HorizontalAlignment=HorizontalAlignment.Left};
    readonly DispatcherTimer update=new() {Interval=TimeSpan.FromMilliseconds(32)};
    LiquidEffect? shader;
    Window? owner;
    Preferences preferences=new();
    UIElement? child;
    public bool Capsule {get;set;}
    public double? Progress {get;set;}
    public Color ProgressColor {get;set;}=Color.FromRgb(72,193,160);
    public static string? ShaderError {get;private set;}
    public UIElement? Child {get=>child;set{if(child is not null)Children.Remove(child);child=value;if(value is not null)Children.Add(value);}}
    public GlassSurface()
    {
        Children.Add(backdrop);Children.Add(progress);
        try{shader=new LiquidEffect();backdrop.Effect=shader;}
        catch(Exception e) when(e is InvalidOperationException or System.Runtime.InteropServices.COMException or DllNotFoundException){ShaderError=e.Message;}
        update.Tick+=(_,_)=>{update.Stop();RefreshBackdrop();};
        SizeChanged+=(_,_)=>Schedule();
        Loaded+=(_,_)=>{owner=Window.GetWindow(this);if(owner is not null)owner.LocationChanged+=Moved;RefreshBackdrop();};
        Unloaded+=(_,_)=>{update.Stop();if(owner is not null)owner.LocationChanged-=Moved;owner=null;};
    }
    void Moved(object? sender,EventArgs e)=>Schedule();
    void Schedule(){if(!update.IsEnabled)update.Start();}
    public static void ConfigureWindow(Window window,bool resizable)
    {
        window.WindowStyle=WindowStyle.None;window.AllowsTransparency=true;window.Background=Brushes.Transparent;window.ShowInTaskbar=false;
        window.ResizeMode=resizable?ResizeMode.CanResize:ResizeMode.NoResize;
        WindowChrome.SetWindowChrome(window,new WindowChrome {CaptionHeight=0,ResizeBorderThickness=new Thickness(resizable?6:0),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
    }
    public bool Apply(Window window,Preferences p){preferences=p;RefreshBackdrop();return shader is not null;}
    public void RefreshBackdrop()
    {
        if(!IsLoaded || ActualWidth<1 || ActualHeight<1)return;
        double radius=Capsule?Math.Min(ActualWidth,ActualHeight)/2:24;
        Clip=new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight),radius,radius);
        var dark=Native.Dark(preferences);
        if(SystemParameters.HighContrast)
        {backdrop.Effect=null;backdrop.Fill=SystemColors.WindowBrush;progress.Visibility=Visibility.Collapsed;return;}
        backdrop.Effect=shader;
        var origin=PointToScreen(new Point());var end=PointToScreen(new Point(ActualWidth,ActualHeight));
        var bitmap=WallpaperBackdrop.Sample(new Rect(origin,end),(int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight));
        backdrop.Fill=new ImageBrush(bitmap) {Stretch=Stretch.Fill};
        if(shader is not null)
        {
            shader.Geometry=new(ActualWidth,ActualHeight,radius,dark?1:0);
            var tint=preferences.WallpaperTint?Native.WallpaperColor(dark):dark?Color.FromRgb(20,28,35):Color.FromRgb(245,248,250);
            // Settings use a denser version of the same lens for text legibility.
            double alpha=Capsule?.10+preferences.GlassOpacity*.48:.55+preferences.GlassOpacity*.35;
            if(!dark)alpha=Math.Max(alpha,Capsule?.32:.68);
            tint.A=(byte)Math.Clamp(alpha*255,0,255);shader.Tint=tint;
        }
        progress.Visibility=Progress is null?Visibility.Collapsed:Visibility.Visible;
        progress.Width=ActualWidth*Math.Clamp(Progress??0,0,1);
        var colour=ProgressColor;colour.A=dark?(byte)66:(byte)72;
        progress.Background=new LinearGradientBrush(colour,Color.FromArgb((byte)(colour.A*.55),colour.R,colour.G,colour.B),90);
    }
}

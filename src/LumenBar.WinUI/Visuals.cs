using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
namespace LumenBar.WinUI;
internal static class Visuals
{
    public static bool Dark(Preferences p)=>p.Theme=="Dark" || (p.Theme=="Wallpaper" || p.Theme=="System") && (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) as int? ?? 1)==0;
    public static SolidColorBrush Brush(byte a,byte r,byte g,byte b)=>new(ColorHelper.FromArgb(a,r,g,b));
    public static TextBlock Text(string text,double size=14,bool muted=false)=>new(){Text=text,FontSize=size,FontFamily=new FontFamily("Segoe UI Variable"),TextWrapping=TextWrapping.Wrap,Opacity=muted?.72:1};
    public static Button Button(string text,Action action){var b=new Button {Content=text,FontSize=14,MinHeight=36,Padding=new Thickness(12,8,12,8)};b.Click+=(_,_)=>action();return b;}
    public static void Theme(FrameworkElement root,Preferences p){root.RequestedTheme=Dark(p)?ElementTheme.Dark:ElementTheme.Light;}
}
internal sealed class Capsule : Grid
{
    readonly SystemBackdropElement? glass;readonly Border fill=new();readonly Border? previewBase;readonly Grid ink=new(){Width=180,Height=42,ColumnSpacing=8};readonly Viewbox scale=new();string displayStyle="Capsule";
    readonly TextBlock name,value;readonly double? fraction;
    readonly RectangleClip outline;
    readonly Grid foreground=new(){Background=Visuals.Brush(0,0,0,0)};
    internal BackdropEdgeSmoothing? Edges {get;}
    public Capsule(string title,string number,double? fraction,Preferences p,bool nativeBackdrop=true)
    {
        this.fraction=fraction;
        var visual=ElementCompositionPreview.GetElementVisual(foreground);
        // XAML's visual ancestry can inherit hard clipping. The rounded silhouette
        // must explicitly opt into coverage antialiasing in the compositor.
        visual.BorderMode=CompositionBorderMode.Soft;
        outline=visual.Compositor.CreateRectangleClip();visual.Clip=outline;
        if(nativeBackdrop)
        {
            Edges=new BackdropEdgeSmoothing();glass=new(){SystemBackdrop=Edges,IsHitTestVisible=false};
            glass.Loaded+=(_,_)=>{glass.SystemBackdrop=new DesktopAcrylicBackdrop();Layout();};
            glass.Unloaded+=(_,_)=>
            {
                if(!glass.IsLoaded)
                {
                    // Remove the old placement visual before closing its owner.
                    ElementCompositionPreview.SetElementChildVisual(glass,null);
                    Edges.Detach();glass.SystemBackdrop=Edges;
                }
            };
            Children.Add(glass);
        }
        else{previewBase=new Border();Children.Add(previewBase);}
        foreground.Children.Add(fill);Children.Add(foreground);
        ink.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(92)});ink.ColumnDefinitions.Add(new ColumnDefinition());
        name=Visuals.Text(title,20);name.TextWrapping=TextWrapping.NoWrap;name.VerticalAlignment=VerticalAlignment.Center;ink.Children.Add(new Viewbox {Child=name,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center});
        value=Visuals.Text(number,28);value.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;value.TextWrapping=TextWrapping.NoWrap;
        var numberScale=new Viewbox {Child=value,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(numberScale,1);ink.Children.Add(numberScale);
        scale.Child=ink;scale.Stretch=Stretch.Uniform;foreground.Children.Add(scale);Appearance(p);SizeChanged+=(_,_)=>Layout();Loaded+=(_,_)=>Layout();
    }
    public void Appearance(Preferences p)
    {
        Visuals.Theme(this,p);displayStyle=p.DisplayStyle;
        var accent=new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
        fill.Background=Visuals.Brush(displayStyle=="Bar"?(byte)220:Visuals.Dark(p)?(byte)70:(byte)40,accent.R,accent.G,accent.B);
        if(previewBase is not null)previewBase.Background=Visuals.Dark(p)?Visuals.Brush(255,47,47,47):Visuals.Brush(255,255,255,255);
        Layout();
    }
    void Layout()
    {
        double radius=Math.Min(ActualWidth,ActualHeight)/2;
        if(glass is not null)
        {
            // Apply the radius directly to the external link, not through an
            // ancestor clip that crosses the XAML / desktop compositor boundary.
            glass.CornerRadius=new CornerRadius(radius);
            ElementCompositionPreview.GetElementVisual(glass).BorderMode=CompositionBorderMode.Soft;
            var placement=Edges?.IsConnected==true?ElementCompositionPreview.GetElementChildVisual(glass):null;
            if(placement is not null)placement.BorderMode=CompositionBorderMode.Soft;
        }
        if(previewBase is not null)previewBase.CornerRadius=new CornerRadius(radius);
        outline.Right=(float)ActualWidth;outline.Bottom=(float)ActualHeight;var corner=new System.Numerics.Vector2((float)radius);outline.TopLeftRadius=corner;outline.TopRightRadius=corner;outline.BottomLeftRadius=corner;outline.BottomRightRadius=corner;
        fill.HorizontalAlignment=HorizontalAlignment.Left;fill.Visibility=displayStyle=="Minimal"?Visibility.Collapsed:Visibility.Visible;
        fill.VerticalAlignment=displayStyle=="Bar"?VerticalAlignment.Bottom:VerticalAlignment.Stretch;
        fill.Height=displayStyle=="Bar"?Math.Max(2,ActualHeight*.05):double.NaN;
        fill.Margin=displayStyle=="Bar"?new Thickness(ActualWidth*.12,0,0,ActualHeight*.12):new Thickness(0);
        fill.CornerRadius=displayStyle=="Bar"?new CornerRadius(2):new CornerRadius(0);
        fill.Width=ActualWidth*(displayStyle=="Bar"?.76:1)*Math.Clamp(fraction??0,0,1);
        scale.Margin=new Thickness(ActualWidth*.065,ActualHeight*.10,ActualWidth*.065,ActualHeight*.10);
    }
}

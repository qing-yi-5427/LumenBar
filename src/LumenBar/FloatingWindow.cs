using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Microsoft.Win32;
namespace LumenBar;
public sealed class FloatingWindow : Window
{
    readonly App app;
    readonly Grid metrics=new() { Margin=new Thickness(3) };
    Preferences? appearance;
    int CapsuleCount=>app.Settings.Enabled.Count+(app.Settings.Enabled.Contains("codex")?1:0);
    bool placing;
    public bool UnlockHotkeyAvailable {get;private set;}
    public bool SettingsHotkeyAvailable {get;private set;}
    public bool MaterialActive {get;private set;}
    public FloatingWindow(App app)
    {
        this.app=app;Title=app.Demo?"LumenBar · 示例数据":"LumenBar";MinWidth=100;MinHeight=48;Topmost=app.Settings.AlwaysOnTop;
        GlassSurface.ConfigureWindow(this,true);Content=metrics;
        SourceInitialized+=(_,_)=>
        {
            UnlockHotkeyAvailable=Native.RegisterUnlock(this);SettingsHotkeyAvailable=Native.RegisterSettings(this);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(Messages);
            ApplyAppearance();Position();ApplyLock();
        };
        SizeChanged+=(_,_)=>Render();app.Changed+=Render;
        MouseLeftButtonDown+=(_,e)=>
        {
            if(app.Settings.Locked)return;
            if(e.ClickCount==2){app.OpenSettings();return;}
            try{DragMove();}catch(InvalidOperationException){}SaveBounds();
        };
        SystemEvents.UserPreferenceChanged+=PreferencesChanged;SystemEvents.DisplaySettingsChanged+=DisplaysChanged;
        Closed+=(_,_)=>{Native.RemoveUnlock(this);app.Changed-=Render;SystemEvents.UserPreferenceChanged-=PreferencesChanged;SystemEvents.DisplaySettingsChanged-=DisplaysChanged;};
        Render();
    }
    void PreferencesChanged(object sender,UserPreferenceChangedEventArgs e)=>Dispatcher.BeginInvoke(()=>{Native.InvalidateWallpaper();WallpaperBackdrop.Invalidate();app.ApplyTheme();ApplyAppearance();app.RefreshSettingsAppearance();});
    void DisplaysChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(Position);
    public void ApplyAppearance(){appearance=app.Settings;Render();Topmost=app.Settings.AlwaysOnTop;}
    public void PreviewAppearance(Preferences p){appearance=p;foreach(var surface in metrics.Children.OfType<GlassSurface>())surface.Apply(this,p);}
    public void ApplyLock()
    {
        ResizeMode=app.Settings.Locked?ResizeMode.NoResize:ResizeMode.CanResize;
        WindowChrome.GetWindowChrome(this).ResizeBorderThickness=new Thickness(app.Settings.Locked?0:6);
        Native.ClickThrough(this,app.Settings.Locked);
    }
    IntPtr Messages(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)
    {
        if(msg==0x7C && w.ToInt64()==-20 && app.Settings.Locked)
        {var style=Marshal.PtrToStructure<StyleStruct>(l);style.NewStyle|=0x80020;Marshal.StructureToPtr(style,l,false);handled=true;}
        if(msg==0x312)
        {if(w.ToInt32()==701){app.ToggleLock();handled=true;}if(w.ToInt32()==702){app.OpenSettings();handled=true;}}
        if(msg==0x214 && !app.Settings.Locked)
        {
            var rect=Marshal.PtrToStructure<Native.RectI>(l);var scale=VisualTreeHelper.GetDpi(this).DpiScaleX;
            var fitted=CompactLayout.Fit(rect.Width/scale,rect.Height/scale,CapsuleCount,w.ToInt32() is 3 or 6);
            int width=(int)Math.Round(fitted.Width*scale),height=(int)Math.Round(fitted.Height*scale);
            if(w.ToInt32() is 1 or 4 or 7)rect.Left=rect.Right-width;else rect.Right=rect.Left+width;
            if(w.ToInt32() is 3 or 4 or 5)rect.Top=rect.Bottom-height;else rect.Bottom=rect.Top+height;
            Marshal.StructureToPtr(rect,l,false);handled=true;return new IntPtr(1);
        }
        if(msg==0x232){SaveBounds();ApplyAppearance();Native.ClickThrough(this,app.Settings.Locked);}
        return IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)] struct StyleStruct {public uint OldStyle,NewStyle;}
    void SaveBounds()
    {
        if(placing)return;var r=Native.WindowRect(this);app.Settings.FloatingLeft=r.Left;app.Settings.FloatingTop=r.Top;
        app.Settings.FloatingWidthPixels=r.Width;app.Settings.FloatingHeightPixels=r.Height;app.SavePosition();
    }
    public void Position()
    {
        placing=true;
        try
        {
            var d=Native.Target(app.Settings.Monitor);double scale=d.Dpi/96.0;
            double width=Math.Clamp(app.Settings.FloatingWidthPixels/scale,100,d.WorkWidth/scale);
            double height=app.Settings.FloatingHeightPixels/scale ?? width/3.2*Math.Max(1,CapsuleCount);
            var layout=CompactLayout.Fit(width,height,CapsuleCount);
            double fit=Math.Min(1,Math.Min(d.WorkWidth/scale/layout.Width,d.WorkHeight/scale/layout.Height));
            int pxWidth=(int)Math.Ceiling(layout.Width*scale*fit),pxHeight=(int)Math.Ceiling(layout.Height*scale*fit);
            Width=pxWidth/scale;Height=pxHeight/scale;
            int x=(int)(app.Settings.FloatingLeft ?? d.WorkX+d.WorkWidth-pxWidth-24),y=(int)(app.Settings.FloatingTop ?? d.WorkY+100);
            if(!Native.Displays().Any(m=>x>=m.WorkX && x+pxWidth<=m.WorkX+m.WorkWidth && y>=m.WorkY && y+pxHeight<=m.WorkY+m.WorkHeight))
            {x=Math.Clamp(x,d.WorkX,d.WorkX+d.WorkWidth-pxWidth);y=Math.Clamp(y,d.WorkY,d.WorkY+d.WorkHeight-pxHeight);}
            Native.Place(this,x,y,pxWidth,pxHeight,app.Settings.AlwaysOnTop);
        }
        finally{placing=false;}
    }
    public void Render()
    {
        var data=app.Settings.Enabled.Where(Providers.Names.ContainsKey).Select(id=>app.Readings.TryGetValue(id,out var r)?r:Providers.Empty(id)).ToList();
        var pills=new System.Collections.Generic.List<(string Name,string Value,double? Progress,Color Color)>();
        foreach(var reading in data)
        {
            if(reading.Id=="codex")
            {
                foreach(var (minutes,label) in new[]{(300,"Codex · 5h"),(10080,"Codex · 周")})
                {var m=CapsuleData.Quota(reading,minutes);pills.Add((label,m?.Display??"—",m?.RemainingPercent/100,Color.FromRgb(77,202,165)));}
            }
            else pills.Add((reading.Name,reading.Error is null?reading.Headline:"—",CapsuleData.CashProgress(reading,app.Settings),reading.Id=="deepseek"?Color.FromRgb(91,148,241):reading.Id=="mimo"?Color.FromRgb(235,166,87):Color.FromRgb(167,138,231)));
        }
        int columns=CompactLayout.Fit(Math.Max(100,ActualWidth),Math.Max(48,ActualHeight),pills.Count).Columns;
        metrics.Children.Clear();metrics.RowDefinitions.Clear();metrics.ColumnDefinitions.Clear();
        for(int i=0;i<columns;i++)metrics.ColumnDefinitions.Add(new ColumnDefinition());
        for(int i=0;i<(pills.Count+columns-1)/columns;i++)metrics.RowDefinitions.Add(new RowDefinition());
        for(int i=0;i<pills.Count;i++)
        {
            var r=pills[i];var block=new Grid {Width=190,Height=44};
            block.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(93)});block.ColumnDefinitions.Add(new ColumnDefinition());
            var name=Ui.Text(r.Name,13);name.FontWeight=FontWeights.Medium;name.VerticalAlignment=VerticalAlignment.Center;name.TextWrapping=TextWrapping.NoWrap;block.Children.Add(name);
            var number=Ui.Text(r.Value,25);number.FontWeight=FontWeights.SemiBold;number.TextWrapping=TextWrapping.NoWrap;
            var value=new Viewbox {Child=number,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(value,1);block.Children.Add(value);
            var tile=new Viewbox {Child=block,Stretch=Stretch.Uniform,Margin=new Thickness(17,6,17,6)};
            var surface=new GlassSurface {Capsule=true,Progress=r.Progress,ProgressColor=r.Color,Child=tile,Margin=new Thickness(3)};
            Grid.SetRow(surface,i/columns);Grid.SetColumn(surface,i%columns);metrics.Children.Add(surface);surface.Apply(this,appearance??app.Settings);
        }
        MaterialActive=GlassSurface.ShaderError is null;
        var menu=new ContextMenu();
        void Item(string text,Action action){var item=new MenuItem {Header=text};item.Click+=(_,_)=>action();menu.Items.Add(item);}
        Item("设置…     Ctrl + Alt + S",app.OpenSettings);Item("立即刷新",()=>_=app.Refresh());
        Item(app.Settings.Locked?"解锁     Ctrl + Alt + L":"锁定并穿透鼠标     Ctrl + Alt + L",app.ToggleLock);
        menu.Items.Add(new Separator());Item("退出",()=>app.Shutdown());ContextMenu=menu;
        if(IsLoaded)Native.ClickThrough(this,app.Settings.Locked);
    }
}

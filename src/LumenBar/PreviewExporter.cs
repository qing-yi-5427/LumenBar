using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace LumenBar;
public static class PreviewExporter
{
    public static async Task Export(App app)
    {
        var folder=Path.Combine(AppContext.BaseDirectory,"previews");Directory.CreateDirectory(folder);
        if(GlassSurface.ShaderError is not null)throw new InvalidOperationException(GlassSurface.ShaderError);
        await Save(app.Floating,Path.Combine(folder,"floating.png"));
        CheckMinimal(app.Floating,app.Settings.Enabled.Count);
        app.ToggleLock();if(!Native.IsClickThrough(app.Floating))throw new InvalidOperationException($"Lock failed to apply click-through style: {Native.Style(app.Floating):X} locked={app.Settings.Locked}");
        await Task.Delay(500);app.Floating.Render();app.Floating.UpdateLayout();
        if(!Native.IsClickThrough(app.Floating))throw new InvalidOperationException("Click-through lost after rendering");
        await Save(app.Floating,Path.Combine(folder,"floating-locked.png"));
        app.ToggleLock();if(Native.IsClickThrough(app.Floating))throw new InvalidOperationException("Unlock failed to clear click-through style");
        app.Settings.Theme="Dark";app.ApplyTheme();app.Floating.ApplyAppearance();app.Floating.Render();await Save(app.Floating,Path.Combine(folder,"floating-dark.png"));
        app.Settings.FloatingWidthPixels=780;app.Settings.FloatingHeightPixels=140;app.Floating.Position();app.Floating.Render();await Save(app.Floating,Path.Combine(folder,"floating-wide.png"));
        app.Settings.Enabled.Add("openrouter");await app.Refresh();app.Settings.FloatingWidthPixels=400;app.Settings.FloatingHeightPixels=320;app.Floating.Position();app.Floating.Render();await Save(app.Floating,Path.Combine(folder,"floating-grid.png"));
        var settings=new SettingsWindow(app);settings.Show();await Save(settings,Path.Combine(folder,"settings-display.png"));
        settings.SelectPage(1);await Save(settings,Path.Combine(folder,"settings-account.png"));settings.SelectPage(2);await Save(settings,Path.Combine(folder,"settings-glass.png"));
        settings.SelectPage(3);await Save(settings,Path.Combine(folder,"settings-progress.png"));
        settings.SelectAccount("mimo");await Save(settings,Path.Combine(folder,"settings-mimo.png"));settings.SelectAccount("deepseek");settings.SelectAccount("openrouter");settings.SelectAccount("codex");
        settings.SelectPage(1);settings.SelectPage(0);settings.Close();
        app.Settings.Theme="Light";app.ApplyTheme();app.Floating.ApplyAppearance();await Save(app.Floating,Path.Combine(folder,"floating-light.png"));
        CheckMinimal(app.Floating,app.Settings.Enabled.Count);
        File.WriteAllText(Path.Combine(folder,"checks.txt"),$"PASS: independent capsules; Codex 5h/week separate; no buttons or scroll viewers.\nPASS: click-through persists after render and clears on unlock.\nPASS: compact, wide, grid and all settings tabs rendered, including all provider editors and repeated tab switching.\nGlass shader compiled: {app.Floating.MaterialActive}\nNote: images render the actual wallpaper-based WPF shader. This does not verify interaction with the live desktop or cross-monitor DPI.\n");
    }
    static void CheckMinimal(Window window,int count)
    {
        window.UpdateLayout();int texts=0;
        void Visit(DependencyObject node)
        {
            if(node is ScrollViewer or Button)throw new InvalidOperationException("Unexpected scroll or action control in floating window");
            if(node is TextBlock)texts++;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Visit(VisualTreeHelper.GetChild(node,i));
        }
        Visit(window);if(texts!=(count+1)*2)throw new InvalidOperationException("Floating window must show a name and value per capsule, with separate Codex windows");
    }
    static async Task Save(Window w,string path)
    {
        await w.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        w.UpdateLayout();var dpi=VisualTreeHelper.GetDpi(w);
        void Refresh(DependencyObject node){if(node is GlassSurface s)s.RefreshBackdrop();for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Refresh(VisualTreeHelper.GetChild(node,i));}Refresh(w);
        await w.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var image=new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(w.ActualHeight*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);
        image.Render(w);
        // Same wallpaper coordinates as the glass, without capturing the desktop.
        var visual=new DrawingVisual();using(var drawing=visual.RenderOpen())
        {var rect=Native.WindowRect(w);drawing.DrawImage(WallpaperBackdrop.Sample(new Rect(rect.Left,rect.Top,rect.Width,rect.Height),image.PixelWidth,image.PixelHeight),new Rect(0,0,w.ActualWidth,w.ActualHeight));drawing.DrawImage(image,new Rect(0,0,w.ActualWidth,w.ActualHeight));}
        var composite=new RenderTargetBitmap(image.PixelWidth,image.PixelHeight,dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);composite.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(composite));using var file=File.Create(path);encoder.Save(file);
    }
}

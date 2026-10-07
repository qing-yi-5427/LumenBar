using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices;
namespace LumenBar.WinUI;
internal static class Diagnostics
{
    public static async Task Run(App app)
    {
        var folder=Path.Combine(AppContext.BaseDirectory,"previews");Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"checks.txt"),"RUNNING: "+DateTimeOffset.Now);
        try
        {
            await Task.Delay(2000);CheckGlass(app);CheckBackgroundErase(app.Floating);await CheckRoundedCoverage(app);await Save(app.Floating.Surface,Path.Combine(folder,"floating.png"));
            File.WriteAllLines(Path.Combine(folder,"backdrop-modes.txt"),Descendants(app.Floating.Surface).OfType<Capsule>().Select(c=>$"External backdrop mode: {c.Edges?.OriginalMode} -> {c.Edges?.CurrentMode}"));
            CheckStartup();CheckPlacement(app);
            if(app.Tray is not {Registered:true} tray || !tray.IsPresent())throw new InvalidOperationException("Tray icon not registered with Windows Shell");
            tray.CheckRecovery();tray.Execute(3);if(!Desktop.Locked(app.Floating))throw new InvalidOperationException("Tray lock command failed");tray.Execute(3);if(Desktop.Locked(app.Floating))throw new InvalidOperationException("Tray unlock command failed");
            var host=Descendants(app.Floating.Surface).OfType<Microsoft.UI.Xaml.Controls.Canvas>().Single();
            var retained=host.Children[0];host.Children.RemoveAt(0);await Task.Delay(100);
            GC.Collect();GC.WaitForPendingFinalizers();host.Children.Insert(0,retained);await Task.Delay(100);CheckGlass(app);
            app.ToggleLock();await Task.Delay(300);if(!Desktop.Locked(app.Floating))throw new InvalidOperationException("Click-through style missing");CheckBackgroundErase(app.Floating);app.ToggleLock();if(Desktop.Locked(app.Floating))throw new InvalidOperationException("Click-through style retained after unlock");CheckBackgroundErase(app.Floating);
            foreach(var (w,h,name) in new[]{(780,250,"wide"),(420,270,"grid"),(192,280,"narrow")}){app.Floating.ResizeForCheck(w,h);await Task.Delay(250);CheckGlass(app);await CheckRoundedCoverage(app);await Save(app.Floating.Surface,Path.Combine(folder,"floating-"+name+".png"));}
            var original=app.Readings["codex"];
            app.Readings["codex"]=original with {Metrics=original.Metrics.Where(m=>m.WindowMinutes!=300).ToList()};app.Floating.Render();await Task.Delay(200);
            if(app.Floating.CapsuleCount!=3)throw new InvalidOperationException("Missing 5h capsule retained");
            await Save(app.Floating.Surface,Path.Combine(folder,"floating-weekly-only.png"));
            app.Readings["codex"]=original with {Metrics=original.Metrics.Select(m=>m.WindowMinutes==300?m with {Value=0,RemainingPercent=0}:m).ToList()};app.Floating.Render();await Task.Delay(200);
            if(app.Floating.CapsuleCount!=4)throw new InvalidOperationException("Zero 5h capsule hidden");
            app.Readings["codex"]=original;app.Floating.Render();
            await Task.Delay(200);GC.Collect();GC.WaitForPendingFinalizers();await Task.Delay(100);
            // New windows isolate RenderTargetBitmap's composition-cache effects.
            for(int i=0;i<4;i++){var sample=new SettingsWindow(app);sample.SelectPage(i);sample.Activate();await Task.Delay(300);CheckSettings(sample);await Save(sample.Surface,Path.Combine(folder,"settings-"+i+".png"));CheckGlass(app);sample.Close();}
            var settings=new SettingsWindow(app);settings.Activate();await Task.Delay(100);
            settings.SelectPage(3);
            var startupToggle=Descendants(settings.Surface).OfType<Microsoft.UI.Xaml.Controls.ToggleSwitch>().Single(t=>t.Name=="StartupToggle");
            startupToggle.IsOn=true;if(!app.Startup.Enabled)throw new InvalidOperationException("Startup toggle did not apply");
            startupToggle.IsOn=false;if(app.Startup.Enabled)throw new InvalidOperationException("Startup toggle did not turn off");
            foreach(var id in Providers.Names.Keys){settings.SelectAccount(id);await Task.Delay(50);}
            settings.SelectPage(2);await Task.Delay(100);
            var theme=Descendants(settings.Surface).OfType<Microsoft.UI.Xaml.Controls.ComboBox>().First();
            var reading=app.Readings["codex"];theme.SelectedIndex=1;await Task.Delay(100);
            if(app.Settings.Theme!="Light" || !ReferenceEquals(reading,app.Readings["codex"]))throw new InvalidOperationException("Appearance did not update live, or readings were unnecessarily cleared");
            CheckSettings(settings);CheckBackgroundErase(app.Floating);await Save(settings.Surface,Path.Combine(folder,"settings-light.png"));
            settings.SelectPage(0);settings.SelectPage(2);settings.SelectPage(2);await Task.Delay(100);
            foreach(var style in new[]{"Bar","Minimal","Capsule"})
            {
                settings.SelectDisplayStyle(style);await Task.Delay(100);CheckGlass(app);
                if(app.Settings.DisplayStyle!=style)throw new InvalidOperationException("Display style did not update immediately");
                await Save(app.Floating.Surface,Path.Combine(folder,"floating-style-"+style+".png"));
            }
            var width=Descendants(settings.Surface).OfType<Microsoft.UI.Xaml.Controls.Slider>().First();width.Value=360;await Task.Delay(100);
            if(app.Settings.FloatingWidthPixels!=360 || Desktop.Bounds(app.Floating).Width!=360)throw new InvalidOperationException("Size slider did not immediately resize the floating window");
            settings.SelectPage(0);await Task.Delay(100);
            var provider=Descendants(settings.Surface).OfType<Microsoft.UI.Xaml.Controls.ToggleSwitch>().First();provider.IsOn=false;await Task.Delay(100);
            if(app.Settings.Enabled.Contains("codex") || app.Floating.CapsuleCount!=2)throw new InvalidOperationException("Provider selection did not update live");
            provider.IsOn=true;await Task.Delay(100);
            if(!app.Settings.Enabled.Contains("codex"))throw new InvalidOperationException("Provider restore failed");
            settings.Close();
            File.WriteAllText(Path.Combine(folder,"checks.txt"),"PASS: WinUI 3 launched; native Acrylic capsules over a transparent window root.\nPASS: WM_ERASEBKGND clears a prefilled opaque white test bitmap to RGBA zero, including locked/unlocked and light/dark configurations. No desktop pixels are captured.\nPASS: missing 5h hidden, zero 5h visible; wide/grid/narrow layouts rendered.\nPASS: four redesigned settings pages opaque; repeated page navigation and three live display styles.\nPASS: lock/unlock window styles; settings update the floating window immediately without clearing readings.\nRenderTargetBitmap is a layout export; composition effects may be omitted. Real desktop optics, dragging and mouse passthrough require interactive acceptance.\n");
            File.AppendAllText(Path.Combine(folder,"checks.txt"),"PASS: rendered progress curves contain intermediate alpha coverage at 100%, 150%, and 200% render scales.\nPASS: actual ExternalBackdropBorderMode reads back Soft; native glass has its own rounded clip without ancestor/window-region clipping. Unload/reload and forced finalization after removing capsules completed without a native crash.\n");
            tray.Dispose();if(tray.IsPresent())throw new InvalidOperationException("Tray icon was not removed on disposal");
            File.AppendAllText(Path.Combine(folder,"checks.txt"),"PASS: Windows Shell registered the generated icon; recovery, lock/unlock commands and icon removal work. Startup setting toggles live in demo mode; actual registry add/read/remove verified in an isolated non-startup test key. No real startup registration changed.\n");
            File.AppendAllText(Path.Combine(folder,"checks.txt"),"PASS: actual HWND physical size and position restored from a freshly loaded settings file after moving/resizing; disconnected monitor coordinates recover to visible work area.\n");
        }
        catch(Exception e){File.WriteAllText(Path.Combine(folder,"checks.txt"),"FAIL: "+e);}
        app.Quit();
    }
    static void CheckPlacement(App app)
    {
        string directory=Path.Combine(Path.GetTempPath(),"LumenBar-placement-"+Guid.NewGuid().ToString("N"));
        var original=System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(app.Settings))!;
        try
        {
            var work=Desktop.Work(app.Floating);
            app.Floating.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(work.Left+80,work.Top+90,360,225));
            app.Floating.SaveBounds();new SettingsStore(directory).Save(app.Settings);
            app.Floating.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(work.Left+200,work.Top+200,192,300));
            app.Apply(new SettingsStore(directory).Load());
            var actual=Desktop.Bounds(app.Floating);
            if(actual.Left!=work.Left+80 || actual.Top!=work.Top+90 || actual.Width!=360 || actual.Height!=225)throw new InvalidOperationException("Stored physical window bounds did not restore exactly");
            app.Settings.FloatingLeft=-100000;app.Settings.FloatingTop=-100000;app.Floating.Position();
            actual=Desktop.Bounds(app.Floating);work=Desktop.Work(app.Floating);
            if(actual.Left<work.Left || actual.Top<work.Top || actual.Right>work.Right || actual.Bottom>work.Bottom)throw new InvalidOperationException("Disconnected monitor leaves window off screen");
        }
        finally{app.Apply(original);Directory.Delete(directory,true);}
    }
    static void CheckStartup()
    {
        if(StartupRegistration.CommandFor(@"C:\Program Files\LumenBar\LumenBar.exe")!="\"C:\\Program Files\\LumenBar\\LumenBar.exe\" --startup")throw new InvalidOperationException("Startup path is not quoted");
        string keyPath=@"Software\LumenBar\Diagnostics\Startup-"+Guid.NewGuid().ToString("N");
        try
        {
            var startup=new StartupRegistration(testKeyPath:keyPath);
            if(startup.Enabled)throw new InvalidOperationException("New startup registration should be off");
            startup.SetEnabled(true);if(!startup.Enabled)throw new InvalidOperationException("Startup registration was not stored");
            startup.SetEnabled(false);if(startup.Enabled)throw new InvalidOperationException("Startup registration was not removed");
            using var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);
            if(key?.GetValue("LumenBar") is not null)throw new InvalidOperationException("Startup command remains after disabling");
        }
        finally{Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(keyPath,false);}
    }
    // Test the rendered progress edge, not merely the configured BorderMode.
    // RTB omits native Acrylic, but includes our composition clip and progress fill.
    static async Task CheckRoundedCoverage(App app)
    {
        var capsule=Descendants(app.Floating.Surface).OfType<Capsule>().First();
        foreach(var density in new[]{1d,1.5,2d})
        {
            var target=new RenderTargetBitmap();
            await target.RenderAsync(capsule,(int)Math.Round(capsule.ActualWidth*density),(int)Math.Round(capsule.ActualHeight*density));
            var pixels=(await target.GetPixelsAsync()).ToArray();
            // The first row contains no text. Hard clipping has only zero/full
            // coverage; a smooth arc needs several intermediate alpha values.
            var alpha=Enumerable.Range(0,target.PixelWidth/2).Select(x=>pixels[x*4+3]).Distinct().ToArray();
            byte interior=alpha.Max();
            if(alpha.Count(a=>a>1 && a<interior)<3)throw new InvalidOperationException($"Rounded progress edge has no antialiasing ramp at scale {density}");
        }
    }
    static IEnumerable<Microsoft.UI.Xaml.DependencyObject> Descendants(Microsoft.UI.Xaml.DependencyObject root)
    {
        yield return root;
        for(int i=0;i<Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var child in Descendants(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    // Exercise the actual HWND erase handler using an in-memory test surface, never a screen DC.
    static void CheckBackgroundErase(Microsoft.UI.Xaml.Window window)
    {
        int width=window.AppWindow.Size.Width,height=window.AppWindow.Size.Height;
        var header=new BitmapHeader {Size=40,Width=width,Height=-height,Planes=1,BitCount=32};
        var dc=CreateCompatibleDC(0);var bitmap=CreateDIBSection(dc,ref header,0,out var pixels,0,0);
        if(dc==0 || bitmap==0 || pixels==0){if(bitmap!=0)DeleteObject(bitmap);if(dc!=0)DeleteDC(dc);throw new InvalidOperationException("Cannot create background test surface");}
        var previous=SelectObject(dc,bitmap);
        try
        {
            var white=Enumerable.Repeat((byte)255,width*height*4).ToArray();Marshal.Copy(white,0,pixels,white.Length);
            if(SendMessage(Desktop.Handle(window),0x14,dc,0)!=1)throw new InvalidOperationException("Window does not handle background clearing");
            GdiFlush();Marshal.Copy(pixels,white,0,white.Length);
            if(white.Any(b=>b!=0))throw new InvalidOperationException("Opaque backing pixels remain underneath the capsule edge");
        }
        finally{SelectObject(dc,previous);DeleteObject(bitmap);DeleteDC(dc);}
    }
    [StructLayout(LayoutKind.Sequential)] struct BitmapHeader {public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,SizeImage;public int XPelsPerMeter,YPelsPerMeter;public uint ClrUsed,ClrImportant;}
    [DllImport("gdi32.dll")] static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] static extern nint CreateDIBSection(nint dc,ref BitmapHeader header,uint usage,out nint pixels,nint section,uint offset);
    [DllImport("gdi32.dll")] static extern nint SelectObject(nint dc,nint value);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] static extern bool GdiFlush();
    [DllImport("user32.dll")] static extern nint SendMessage(nint window,uint message,nint w,nint l);
    static void CheckGlass(App app)
    {
        if(!Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())throw new InvalidOperationException("Desktop Acrylic unsupported on this machine");
        if(app.Floating.SystemBackdrop is not TransparentBackdrop)throw new InvalidOperationException("Window root must stay transparent at rounded edges");
        var backgrounds=Descendants(app.Floating.Surface).OfType<Microsoft.UI.Xaml.Controls.SystemBackdropElement>().ToArray();
        if(backgrounds.Length!=app.Floating.CapsuleCount || backgrounds.Any(b=>b.SystemBackdrop is not Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop))throw new InvalidOperationException("A native Acrylic capsule is missing");
        if(Descendants(app.Floating.Surface).OfType<Capsule>().Any(c=>c.Edges?.CurrentMode!=1 || Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(c).Clip is not null))throw new InvalidOperationException("External backdrop has hard edges or an ancestor clip");
        var region=CreateRectRgn(0,0,0,0);
        try{if(region==0 || GetWindowRgn(Desktop.Handle(app.Floating),region)!=0)throw new InvalidOperationException("Floating window still has a binary window region");}
        finally{if(region!=0)DeleteObject(region);}
        foreach(var b in backgrounds)
        {
            var placement=Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementChildVisual(b);
            if(Math.Abs(b.CornerRadius.TopLeft-Math.Min(b.ActualWidth,b.ActualHeight)/2)>.1 || placement?.BorderMode!=Microsoft.UI.Composition.CompositionBorderMode.Soft)throw new InvalidOperationException("Native backdrop does not have a directly rounded, soft placement clip");
        }
    }
    static void CheckSettings(SettingsWindow window)
    {
        if(window.SystemBackdrop is not null || Descendants(window.Surface).OfType<Microsoft.UI.Xaml.Controls.SystemBackdropElement>().Any() || window.Surface is not Microsoft.UI.Xaml.Controls.Grid {Background: Microsoft.UI.Xaml.Media.SolidColorBrush {Color.A:255}})throw new InvalidOperationException("Settings background is not opaque");
    }
    [DllImport("gdi32.dll")] static extern nint CreateRectRgn(int l,int t,int r,int b);
    [DllImport("user32.dll")] static extern int GetWindowRgn(nint window,nint region);
    static async Task Save(Microsoft.UI.Xaml.FrameworkElement element,string path)
    {
        element.InvalidateMeasure();element.UpdateLayout();var target=new RenderTargetBitmap();await target.RenderAsync(element);var bytes=(await target.GetPixelsAsync()).ToArray();
        using var stream=File.Create(path);var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream.AsRandomAccessStream());encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)target.PixelWidth,(uint)target.PixelHeight,96,96,bytes);await encoder.FlushAsync();
    }
}

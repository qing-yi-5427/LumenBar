using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace LumenBar;

public partial class App : Application
{
    public SettingsStore Store { get; } = new();
    public Preferences Settings { get; private set; } = new();
    public Dictionary<string,Reading> Readings { get; } = new();
    public bool Demo { get; private set; }
    public bool Busy { get; private set; }
    public FloatingWindow Floating { get; private set; } = null!;
    SettingsWindow? settingsWindow;
    readonly Providers providers=new();
    readonly DispatcherTimer refreshTimer=new();
    CancellationTokenSource refreshToken=new();
    bool queued;
    int generation;
    Mutex? mutex;
    EventWaitHandle? wake;
    RegisteredWaitHandle? wakeRegistration;
    public event Action? Changed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Demo=e.Args.Contains("--demo");
        var name="Local\\LumenBar"+(Demo?".Demo":"");
        mutex=new Mutex(true,name,out var first);
        wake=new EventWaitHandle(false,EventResetMode.AutoReset,name+".Wake");
        if(!first){wake.Set();Shutdown();return;}
        wakeRegistration=ThreadPool.RegisterWaitForSingleObject(wake,(_,_)=>Dispatcher.BeginInvoke(()=> { if(Settings.Locked)ToggleLock();Floating.Show();Floating.Activate();OpenSettings(); }),null,Timeout.Infinite,false);
        Settings=Demo ? new Preferences() : Store.Load();
        ApplyTheme();
        foreach(var id in Settings.Enabled.Where(Providers.Names.ContainsKey)) Readings[id]=Providers.Empty(id);
        Floating=new FloatingWindow(this); MainWindow=Floating;
        Floating.Show();
        if(Demo) SetDemo();
        refreshTimer.Interval=TimeSpan.FromSeconds(Settings.RefreshSeconds);
        refreshTimer.Tick+=async(_,_)=>await Refresh(); refreshTimer.Start();
        if(!Demo) _=Refresh();
        if(!Demo && (Store.ShowIntro || e.Args.Contains("--settings")))OpenSettings();
        if(Store.LoadWarning is not null) MessageBox.Show(Store.LoadWarning,"LumenBar");
        if(e.Args.Contains("--render-preview"))
        {
            var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(2) };
            timer.Tick+=async(_,_)=> {timer.Stop(); await PreviewExporter.Export(this); Shutdown();};timer.Start();
        }
    }
    void SetDemo()
    {
        Readings.Clear();
        Readings["codex"]=new("codex","Codex","示例数据",new(){new("5 小时剩余",72,"%",72,DateTimeOffset.Now.AddHours(2),300,"codex"),new("本周剩余",86,"%",86,DateTimeOffset.Now.AddDays(3),10080,"codex")},DateTimeOffset.Now);
        Readings["deepseek"]=new("deepseek","DeepSeek","示例数据",new(){new("API 可用余额",128.50m,"CNY")},DateTimeOffset.Now);
        Readings["mimo"]=new("mimo","MiMo","示例数据",new(){new("账户余额",46.80m,"CNY")},DateTimeOffset.Now);
        foreach(var id in Readings.Keys.Where(id=>!Settings.Enabled.Contains(id)).ToList())Readings.Remove(id);
        if(Settings.Enabled.Contains("openrouter"))Readings["openrouter"]=new("openrouter","OpenRouter","示例数据",new(){new("账户余额",24.60m,"USD")},DateTimeOffset.Now);
        Changed?.Invoke();
    }
    public async Task Refresh()
    {
        if(Demo){SetDemo();return;}
        if(Busy){queued=true;return;}
        Busy=true;Changed?.Invoke();
        int version=generation; var settings=Settings; var token=refreshToken.Token;
        try
        {
            var items=await Task.WhenAll(settings.Enabled.Where(Providers.Names.ContainsKey).Select(async id=>
            {
                try { return await providers.ReadAsync(id,settings,token); }
                catch(OperationCanceledException) { return Failure(id,"连接超时，请稍后刷新",true); }
                catch(UnauthorizedAccessException ex) { return Failure(id,ex.Message,false); }
                catch(FormatException ex) { return Failure(id,ex.Message,false); }
                catch(System.Text.Json.JsonException) { return Failure(id,"平台响应格式变化或会话过期",false); }
                catch(System.Net.Http.HttpRequestException) { return Failure(id,"网络连接失败，请检查网络",true); }
                catch(InvalidOperationException ex) { return Failure(id,ex.Message,true); }
                catch { return Failure(id,"读取失败，请检查凭据与平台状态",false); }
            }));
            if(version==generation && !token.IsCancellationRequested)
            { Readings.Clear();foreach(var item in items) Readings[item.Id]=item; }
        }
        finally
        {
            Busy=false;Changed?.Invoke();
            if(queued){queued=false;_=Refresh();}
        }
    }
    Reading Failure(string id,string error,bool keep) => keep && id!="codex" && Readings.TryGetValue(id,out var previous) && previous.Updated is not null ? previous with { Error=error } : Providers.Empty(id) with { Error=error };
    public void OpenSettings()
    {
        if(settingsWindow is not null){settingsWindow.Activate();return;}
        settingsWindow=new SettingsWindow(this);settingsWindow.Closed+=(_,_)=>settingsWindow=null;settingsWindow.Show();
    }
    public void RefreshSettingsAppearance()=>settingsWindow?.ApplyAppearance();
    public void PreviewAppearance(Preferences p){ApplyTheme(p);Floating.PreviewAppearance(p);}
    public void Apply(Preferences settings)
    {
        if(!Demo) Store.Save(settings);
        Settings=settings;generation++;refreshToken.Cancel();refreshToken.Dispose();refreshToken=new();
        Readings.Clear();foreach(var id in settings.Enabled.Where(Providers.Names.ContainsKey))Readings[id]=Providers.Empty(id);
        ApplyTheme();Floating.ApplyAppearance();Floating.Position();Floating.ApplyLock();Floating.Show();
        refreshTimer.Interval=TimeSpan.FromSeconds(settings.RefreshSeconds);
        Changed?.Invoke();_=Refresh();
    }
    public void SavePosition()
    {
        if(Demo)return;
        try{Store.Save(Settings);}catch{ /* Keep the last durable preferences if the disk is unavailable. */ }
    }
    public void ToggleLock()
    {
        Settings.Locked=!Settings.Locked;Floating.ApplyLock();Changed?.Invoke();SavePosition();
    }
    public void ApplyTheme(Preferences? preview=null)
    {
        if(SystemParameters.HighContrast)
        {
            Resources["TextBrush"]=SystemColors.WindowTextBrush;Resources["MutedBrush"]=SystemColors.WindowTextBrush;
            Resources["SurfaceBrush"]=SystemColors.WindowBrush;Resources["ControlBrush"]=SystemColors.WindowBrush;
            Resources["LineBrush"]=SystemColors.WindowTextBrush;Resources["AccentBrush"]=SystemColors.HighlightBrush;Resources["SelectedBrush"]=SystemColors.HighlightBrush;return;
        }
        bool dark=Native.Dark(preview ?? Settings);
        void Brush(string key,string hex)=>Resources[key]=(SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        Brush("TextBrush",dark?"#F1F5F7":"#182B35");Brush("MutedBrush",dark?"#C5D1D8":"#425862");
        Brush("SurfaceBrush",dark?"#B01A2A35":"#C0EEF5F8");Brush("ControlBrush",dark?"#18FFFFFF":"#60FFFFFF");
        Brush("LineBrush",dark?"#20FFFFFF":"#201C3847");Brush("AccentBrush",dark?"#A2E3D3":"#216959");
        Brush("SelectedBrush",dark?"#285EB79E":"#2899CBBF");
    }
    protected override void OnExit(ExitEventArgs e)
    {
        refreshTimer.Stop();refreshToken.Cancel();providers.Dispose();wakeRegistration?.Unregister(null);wake?.Dispose();mutex?.Dispose();base.OnExit(e);
    }
}

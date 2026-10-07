using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
namespace LumenBar.WinUI;
public partial class App : Application
{
    public Preferences Settings {get;private set;}=new();
    public Dictionary<string,Reading> Readings {get;}=new();
    public bool Demo {get;}=Environment.GetCommandLineArgs().Contains("--demo")||Environment.GetCommandLineArgs().Contains("--probe");
    public FloatingWindow Floating {get;private set;}=null!;
    internal TrayIcon? Tray {get;private set;}
    internal StartupRegistration Startup {get;private set;}=null!;
    public event Action? Changed;
    readonly SettingsStore store=new();readonly Providers providers=new();readonly DispatcherTimer timer=new();
    SettingsWindow? settingsWindow;
    Mutex? mutex;EventWaitHandle? wake;RegisteredWaitHandle? registration;
    CancellationTokenSource refreshToken=new();bool busy,queued,quitting;
    public App(){if(Environment.GetCommandLineArgs().Contains("--probe")||Environment.GetCommandLineArgs().Contains("--diagnose"))UnhandledException+=(_,e)=>File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"probe-error.txt"),e.Exception.ToString());InitializeComponent();}
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var queue=DispatcherQueue.GetForCurrentThread();var name="Local\\LumenBar"+(Demo?".Demo":"");
        mutex=new Mutex(true,name,out bool first);wake=new EventWaitHandle(false,EventResetMode.AutoReset,name+".Wake");
        bool startupLaunch=Environment.GetCommandLineArgs().Contains("--startup");
        if(!first){if(!startupLaunch)wake.Set();Exit();return;}
        registration=ThreadPool.RegisterWaitForSingleObject(wake,(_,_)=>queue.TryEnqueue(()=>{if(Settings.Locked)ToggleLock();Floating.Activate();OpenSettings();}),null,Timeout.Infinite,false);
        Settings=Demo?new Preferences():store.Load();
        Startup=new StartupRegistration(Demo);
        if(Demo){Settings.Theme="Dark";Settings.BalanceReferences["deepseek:CNY"]=200;Settings.BalanceReferences["mimo:CNY"]=100;}
        Floating=new FloatingWindow(this);Floating.Activate();Floating.Position();Floating.ApplyLock();
        Tray=new TrayIcon(this);
        timer.Interval=TimeSpan.FromSeconds(Settings.RefreshSeconds);timer.Tick+=async(_,_)=>await Refresh();timer.Start();_=Refresh();
        if(!Demo&&!startupLaunch&&(store.ShowIntro||Environment.GetCommandLineArgs().Contains("--settings")))OpenSettings();
        if(Environment.GetCommandLineArgs().Contains("--probe"))_=Diagnostics.Run(this);
    }
    public async Task Refresh()
    {
        if(Demo)
        {
            Readings.Clear();
            Readings["codex"]=new("codex","Codex","示例数据",new(){new("5 小时剩余",72,"%",72,DateTimeOffset.Now.AddHours(2),300,"codex"),new("本周剩余",86,"%",86,DateTimeOffset.Now.AddDays(3),10080,"codex")},DateTimeOffset.Now);
            Readings["deepseek"]=new("deepseek","DeepSeek","示例数据",new(){new("账户余额",128.50m,"CNY")},DateTimeOffset.Now);
            Readings["mimo"]=new("mimo","MiMo","示例数据",new(){new("账户余额",46.80m,"CNY")},DateTimeOffset.Now);
            Readings["openrouter"]=new("openrouter","OpenRouter","示例数据",new(){new("账户余额",24.60m,"USD")},DateTimeOffset.Now);Changed?.Invoke();return;
        }
        if(busy){queued=true;return;}busy=true;var prefs=Settings;var token=refreshToken.Token;
        try
        {
            var results=await Task.WhenAll(prefs.Enabled.Select(async id=>
            {
                try{return await providers.ReadAsync(id,prefs,token);}
                catch(UnauthorizedAccessException e){return Providers.Empty(id) with {Error=e.Message};}
                catch(FormatException e){return Providers.Empty(id) with {Error=e.Message};}
                catch(OperationCanceledException){return Providers.Empty(id) with {Error="连接超时，请稍后刷新"};}
                catch{return Providers.Empty(id) with {Error="暂时无法读取，请检查网络与账户连接"};}
            }));
            if(!token.IsCancellationRequested){Readings.Clear();foreach(var r in results)Readings[r.Id]=r;Changed?.Invoke();}
        }
        finally{busy=false;if(queued){queued=false;_=Refresh();}}
    }
    public void OpenSettings()
    {if(settingsWindow is null){settingsWindow=new SettingsWindow(this);settingsWindow.Closed+=(_,_)=>settingsWindow=null;}settingsWindow.Activate();}
    public void Apply(Preferences p)
    {
        if(p.FloatingWidthPixels!=Settings.FloatingWidthPixels || p.FloatingHeightPixels!=Settings.FloatingHeightPixels)p.LastFloatingSize=null;
        bool reconnect=Settings.CodexPath!=p.CodexPath || !Settings.Enabled.SequenceEqual(p.Enabled) || !Settings.ProtectedSecrets.OrderBy(x=>x.Key).SequenceEqual(p.ProtectedSecrets.OrderBy(x=>x.Key));
        if(!Demo)store.Save(p);
        foreach(var id in Settings.ProtectedSecrets.Keys.Union(p.ProtectedSecrets.Keys).ToArray())
            if(Settings.ProtectedSecrets.GetValueOrDefault(id)!=p.ProtectedSecrets.GetValueOrDefault(id))Readings.Remove(id);
        if(Settings.CodexPath!=p.CodexPath)Readings.Remove("codex");
        Settings=p;timer.Interval=TimeSpan.FromSeconds(p.RefreshSeconds);Floating.Render();Floating.Position();Floating.ApplyLock();
        Floating.SaveBounds();Tray?.UpdateTooltip();
        if(reconnect){refreshToken.Cancel();refreshToken.Dispose();refreshToken=new();_=Refresh();}
    }

    public void Preview(Preferences p)=>Floating.Appearance(p);
    public void ToggleLock(){Settings.Locked=!Settings.Locked;Floating.ApplyLock();SavePosition();settingsWindow?.UpdateLockState();}
    public void SavePosition(){if(!Demo)try{store.Save(Settings);}catch(IOException){}}
    public void Quit(){if(quitting)return;quitting=true;Floating?.SaveBounds();Tray?.Dispose();timer.Stop();refreshToken.Cancel();providers.Dispose();registration?.Unregister(null);wake?.Dispose();mutex?.Dispose();Exit();}
}

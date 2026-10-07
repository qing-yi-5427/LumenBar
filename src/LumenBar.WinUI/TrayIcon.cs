using System.ComponentModel;
using System.Runtime.InteropServices;
namespace LumenBar.WinUI;

internal sealed class TrayIcon : IDisposable
{
    internal const uint Callback=0x8000+711;
    readonly App app;
    readonly nint window,icon;
    readonly uint taskbarCreated=RegisterWindowMessage("TaskbarCreated");
    NotifyData data;
    bool disposed;
    public bool Registered {get;private set;}
    public static string IconPath=>Path.Combine(AppContext.BaseDirectory,"Assets","LumenBar.ico");
    public TrayIcon(App app)
    {
        this.app=app;window=Desktop.Handle(app.Floating);
        icon=LoadImage(0,IconPath,1,32,32,0x10);
        if(icon==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"无法加载应用图标。");
        data=new NotifyData {Size=(uint)Marshal.SizeOf<NotifyData>(),Window=window,Id=1,Flags=1|2|4|0x20|0x80,Message=Callback,Icon=icon,Tip="LumenBar",Info="",InfoTitle="",Guid=app.Demo?new("85C9BFBC-5A40-4EC4-8FAD-B9E0BE29B82E"):new("E080C063-0445-4136-AFBA-18FA0F0065D0")};
        Add();app.Changed+=UpdateTooltip;
    }
    void Add()
    {
        if(disposed)return;
        Registered=Shell_NotifyIcon(0,ref data);
        if(Registered){data.Version=4;Shell_NotifyIcon(4,ref data);}
    }
    public void UpdateTooltip()
    {
        if(disposed)return;
        var lines=new List<string>{"LumenBar"};
        foreach(var id in app.Settings.Enabled)
        {
            if(!app.Readings.TryGetValue(id,out var reading))continue;
            if(id=="codex")foreach(var (minutes,title) in CapsuleData.CodexWindows(reading))lines.Add($"{title}：{CapsuleData.Quota(reading,minutes)?.Display??"—"}");
            else lines.Add($"{reading.Name}：{(reading.Error is null?reading.Headline:"—")}");
        }
        var tip=string.Join("\n",lines);data.Tip=tip.Length<128?tip:tip[..126]+"…";
        if(!Registered)Add();else if(!Shell_NotifyIcon(1,ref data)){Registered=false;Add();}
    }
    public bool HandleMessage(uint message,nint w,nint l)
    {
        if(disposed)return false;
        if(message==taskbarCreated){Registered=false;Add();return true;}
        if(message!=Callback)return false;
        uint notification=(uint)(l.ToInt64()&0xffff);
        if(notification is 0x400 or 0x401)app.OpenSettings();
        else if(notification==0x7b)ShowMenu(w);
        return true;
    }
    void ShowMenu(nint position)
    {
        var menu=CreatePopupMenu();if(menu==0)return;
        try
        {
            AppendMenu(menu,0,1,"打开设置");AppendMenu(menu,0,2,"立即刷新");
            AppendMenu(menu,app.Settings.Locked?8u:0u,3,app.Settings.Locked?"解锁悬浮窗":"锁定悬浮窗");
            AppendMenu(menu,0x800,0,null);AppendMenu(menu,0,4,"退出 LumenBar");
            int x=unchecked((short)(position.ToInt64()&0xffff)),y=unchecked((short)((position.ToInt64()>>16)&0xffff));
            if(x==-1&&y==-1){GetCursorPos(out var point);x=point.X;y=point.Y;}
            SetForegroundWindow(window);
            uint command=TrackPopupMenuEx(menu,0x100|0x2,x,y,window,0);
            PostMessage(window,0,0,0);
            if(command!=0)app.Floating.DispatcherQueue.TryEnqueue(()=>Execute(command));
        }
        finally{DestroyMenu(menu);}
    }
    internal void Execute(uint command)
    {
        if(disposed)return;
        switch(command){case 1:app.OpenSettings();break;case 2:_=app.Refresh();break;case 3:app.ToggleLock();break;case 4:app.Quit();break;}
    }
    public bool IsPresent()
    {
        var id=new IconId {Size=(uint)Marshal.SizeOf<IconId>(),Window=window,Id=1,Guid=data.Guid};
        return Shell_NotifyIconGetRect(ref id,out _)>=0;
    }
    internal void CheckRecovery()
    {
        Shell_NotifyIcon(2,ref data);Registered=false;
        HandleMessage(taskbarCreated,0,0);
        if(!Registered||!IsPresent())throw new InvalidOperationException("Tray recovery failed");
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;app.Changed-=UpdateTooltip;
        Shell_NotifyIcon(2,ref data);Registered=false;DestroyIcon(icon);
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct NotifyData
    {
        public uint Size;public nint Window;public uint Id,Flags,Message;public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Tip;
        public uint State,StateMask;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string InfoTitle;
        public uint InfoFlags;public Guid Guid;public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] struct IconId{public uint Size;public nint Window;public uint Id;public Guid Guid;}
    [StructLayout(LayoutKind.Sequential)] struct Point{public int X,Y;}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern bool Shell_NotifyIcon(uint message,ref NotifyData data);
    [DllImport("shell32.dll")]static extern int Shell_NotifyIconGetRect(ref IconId id,out Desktop.Rect rect);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern nint LoadImage(nint instance,string path,uint type,int width,int height,uint flags);
    [DllImport("user32.dll")]static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")]static extern nint CreatePopupMenu();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool AppendMenu(nint menu,uint flags,nuint id,string? text);
    [DllImport("user32.dll")]static extern uint TrackPopupMenuEx(nint menu,uint flags,int x,int y,nint window,nint parameters);
    [DllImport("user32.dll")]static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]static extern bool PostMessage(nint window,uint message,nint w,nint l);
}

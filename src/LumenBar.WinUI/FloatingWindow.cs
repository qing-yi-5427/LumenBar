using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
namespace LumenBar.WinUI;
public sealed class FloatingWindow : Window
{
    readonly App app;readonly Grid root=new();readonly Canvas capsules=new();
    readonly Desktop.SubclassProc hook;bool placing,positioned;readonly HoldDrag drag=new();uint? dragPointer;
    public bool UnlockHotkeyAvailable {get;}
    public int CapsuleCount=>capsules.Children.Count;
    public FrameworkElement Surface=>root;
    public FloatingWindow(App app)
    {
        this.app=app;Title="LumenBar";Content=root;SystemBackdrop=new TransparentBackdrop();root.Children.Add(capsules);
        Desktop.Configure(this,app.Settings.AlwaysOnTop,false);hook=Messages;Desktop.SetWindowSubclass(Desktop.Handle(this),hook,1,0);Desktop.RefreshFrame(this);Desktop.ConfigureTransparentClient(this);
        AppWindow.SetIcon(TrayIcon.IconPath);
        UnlockHotkeyAvailable=Desktop.Hotkey(this,701,0x4C);Desktop.Hotkey(this,702,0x53);
        root.SizeChanged+=(_,_)=>Layout();app.Changed+=Render;
        root.PointerPressed+=(_,e)=>
        {
            if(app.Settings.Locked || !e.GetCurrentPoint(root).Properties.IsLeftButtonPressed)return;
            if(!root.CapturePointer(e.Pointer))return;
            var cursor=Desktop.Cursor();var bounds=Desktop.Bounds(this);dragPointer=e.Pointer.PointerId;
            drag.Begin(cursor.X,cursor.Y,bounds.Left,bounds.Top,Environment.TickCount64,Desktop.Scale(this));e.Handled=true;
        };
        root.PointerMoved+=(_,e)=>
        {
            if(dragPointer!=e.Pointer.PointerId)return;
            if(app.Settings.Locked || !e.GetCurrentPoint(root).Properties.IsLeftButtonPressed){EndDrag();return;}
            var cursor=Desktop.Cursor();
            if(drag.Move(cursor.X,cursor.Y,true,Environment.TickCount64) is { } point){AppWindow.Move(new PointInt32(point.X,point.Y));e.Handled=true;}
        };
        root.PointerReleased+=(_,e)=>{if(dragPointer==e.Pointer.PointerId)EndDrag();};
        root.PointerCanceled+=(_,e)=>{if(dragPointer==e.Pointer.PointerId)EndDrag();};
        root.PointerCaptureLost+=(_,e)=>{if(dragPointer==e.Pointer.PointerId)EndDrag();};
        Activated+=(_,e)=>{if(e.WindowActivationState==WindowActivationState.Deactivated)EndDrag();};
        root.DoubleTapped+=(_,e)=>{if(!app.Settings.Locked)app.OpenSettings();e.Handled=true;};
        root.RightTapped+=(_,e)=>
        {
            if(app.Settings.Locked)return;var menu=new MenuFlyout();
            void Item(string name,Action action){var item=new MenuFlyoutItem {Text=name};item.Click+=(_,_)=>action();menu.Items.Add(item);}
            Item("设置 · Ctrl + Alt + S",app.OpenSettings);Item("立即刷新",()=>_=app.Refresh());Item("锁定 · Ctrl + Alt + L",app.ToggleLock);Item("退出",app.Quit);menu.ShowAt(root,e.GetPosition(root));e.Handled=true;
        };
        AppWindow.Closing+=(_,_)=>SaveBounds();
        Closed+=(_,_)=>{Desktop.RemoveHotkeys(this);Desktop.RemoveWindowSubclass(Desktop.Handle(this),hook,1);app.Changed-=Render;app.Quit();};Render();
    }
    void EndDrag(){dragPointer=null;bool moved=drag.End();root.ReleasePointerCaptures();if(moved)SaveBounds();}
    public void Render()
    {
        int previousCount=CapsuleCount;capsules.Children.Clear();var p=app.Settings;Visuals.Theme(root,p);
        foreach(var id in app.Settings.Enabled)
        {
            var r=app.Readings.GetValueOrDefault(id)??Providers.Empty(id);
            if(id=="codex")foreach(var (mins,title) in CapsuleData.CodexWindows(r))
            {var m=CapsuleData.Quota(r,mins);capsules.Children.Add(new Capsule(title,m?.Display??"—",m?.RemainingPercent/100,p));}
            else capsules.Children.Add(new Capsule(r.Name,r.Error is null?r.Headline:"—",CapsuleData.CashProgress(r,app.Settings),p));
        }
        if(previousCount>0 && previousCount!=CapsuleCount){Position();SaveBounds();}else Layout();
    }
    public void Appearance(Preferences p){Visuals.Theme(root,p);foreach(var c in capsules.Children.OfType<Capsule>())c.Appearance(p);}
    public void ResetAppearance(){Appearance(app.Settings);}
    public void ApplyLock(){EndDrag();Desktop.Lock(this,app.Settings.Locked);Desktop.Configure(this,app.Settings.AlwaysOnTop,false);Desktop.ConfigureTransparentClient(this);}
    public void Position()
    {
        placing=true;try
        {
            var p=app.Settings;
            var work=p.FloatingLeft is double left && p.FloatingTop is double top?Desktop.WorkAt((int)left,(int)top):Desktop.Work(this);
            // Establish the target monitor's DPI before fitting the physical pixel size.
            if(!positioned)AppWindow.Move(new PointInt32(Math.Clamp((int)(p.FloatingLeft??work.Left),work.Left,work.Right-1),Math.Clamp((int)(p.FloatingTop??work.Top),work.Top,work.Bottom-1)));
            double dpi=Desktop.Scale(this);
            double w=Math.Clamp(app.Settings.FloatingWidthPixels,120,work.Width)/dpi;
            var fit=CompactLayout.Fit(w,app.Settings.FloatingHeightPixels is int h?h/dpi:w/3.2*CapsuleCount,CapsuleCount);
            double shrink=Math.Min(1,Math.Min(work.Width/(fit.Width*dpi),work.Height/(fit.Height*dpi)));
            int width=(int)(fit.Width*dpi*shrink),height=(int)(fit.Height*dpi*shrink);
            if(p.LastFloatingSize is {Width:>0,Height:>0} saved && saved.CapsuleCount==CapsuleCount)
            {double scale=Math.Min(1,Math.Min(work.Width/(double)saved.Width,work.Height/(double)saved.Height));width=Math.Max(1,(int)(saved.Width*scale));height=Math.Max(1,(int)(saved.Height*scale));}
            int x=Math.Clamp((int)(app.Settings.FloatingLeft??work.Right-width-28),work.Left,work.Right-width),y=Math.Clamp((int)(app.Settings.FloatingTop??work.Top+100),work.Top,work.Bottom-height);
            AppWindow.MoveAndResize(new RectInt32(x,y,width,height));positioned=true;Layout();
        }
        finally{placing=false;}
    }
    public void ResizeForCheck(int width,int height){AppWindow.Resize(new SizeInt32(width,height));Layout();}
    void Layout()
    {
        if(root.ActualWidth<=0||root.ActualHeight<=0)return;
        var fit=CompactLayout.Fit(root.ActualWidth,root.ActualHeight,CapsuleCount);int cols=fit.Columns,rows=fit.Rows;
        double cw=root.ActualWidth/cols,ch=root.ActualHeight/rows,gap=Math.Clamp(ch*.065,2,6),dpi=Desktop.Scale(this);
        int i=0;
        foreach(var c in capsules.Children.OfType<Capsule>())
        {// Align capsule layout to physical pixel boundaries.
            int px=(int)Math.Round((i%cols*cw+gap)*dpi),py=(int)Math.Round((i/cols*ch+gap)*dpi);
            int pw=(int)Math.Round((cw-gap*2)*dpi),ph=(int)Math.Round((ch-gap*2)*dpi);
            c.Width=pw/dpi;c.Height=ph/dpi;Canvas.SetLeft(c,px/dpi);Canvas.SetTop(c,py/dpi);i++;}
    }
    internal void SaveBounds(){if(placing||!positioned)return;var r=Desktop.Bounds(this);if(r.Width<=0||r.Height<=0)return;app.Settings.FloatingLeft=r.Left;app.Settings.FloatingTop=r.Top;app.Settings.LastFloatingSize=new(r.Width,r.Height,CapsuleCount);app.SavePosition();}
    nint Messages(nint h,uint msg,nint w,nint l,nuint id,nuint data)
    {
        if(app.Tray?.HandleMessage(msg,w,l)==true)return 0;
        if(msg==0x14 && Desktop.ClearTransparentClient(h,w))return 1; // WM_ERASEBKGND: never restore the default white HWND background.
        if(msg==0x31E){Desktop.ConfigureTransparentClient(this);return 0;} // DWM composition restarted.
        if(msg==0x83)return 0; // Full client area; no invisible resize frame offset.
        if(msg==0xA3 && w==2){app.OpenSettings();return 0;}
        if(msg==0x312){if(w==701)app.ToggleLock();if(w==702)app.OpenSettings();return 0;}
        if(msg==0x232)SaveBounds();
        if(msg==0x1A)DispatcherQueue.TryEnqueue(()=>{ResetAppearance();});
        if(msg==0x7E)DispatcherQueue.TryEnqueue(Position);
        return Desktop.DefSubclassProc(h,msg,w,l);
    }
}

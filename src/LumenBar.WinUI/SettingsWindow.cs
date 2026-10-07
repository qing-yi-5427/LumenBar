using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Windows.Graphics;
namespace LumenBar.WinUI;
public sealed class SettingsWindow : Window
{
    readonly App app;
    readonly Grid root=(Grid)XamlReader.Load("<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Background='{ThemeResource ApplicationPageBackgroundThemeBrush}'/>");
    readonly List<ToggleButton> tabs=new();
    readonly StackPanel body=new(){Spacing=14,Margin=new Thickness(24,20,24,24)};
    readonly TextBlock feedback=Visuals.Text("所有更改自动保存",12,true),syncStatus=Visuals.Text("",12,true);
    readonly Grid preview=new(){ColumnSpacing=10,RowSpacing=10};
    readonly DispatcherTimer inputTimer=new(){Interval=TimeSpan.FromMilliseconds(650)};
    readonly Dictionary<string,string> pendingSecrets=new();
    string? pendingPath;
    string selectedAccount="codex";
    int currentPage;
    bool changing;
    bool synchronizingLock;
    ToggleSwitch? lockToggle;
    Action? updateConnection;
    public FrameworkElement Surface=>root;
    public SettingsWindow(App app)
    {
        this.app=app;Title="LumenBar · 设置";Content=root;Desktop.ConfigureSettings(this);Visuals.Theme(root,app.Settings);
        AppWindow.SetIcon(TrayIcon.IconPath);
        root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
        var header=new Grid {Padding=new Thickness(24,18,24,18),ColumnSpacing=20};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var brand=new StackPanel {Spacing=4};brand.Children.Add(Label("LumenBar",24));brand.Children.Add(Visuals.Text("账户额度，随时可见",13,true));header.Children.Add(brand);
        var status=new StackPanel {Spacing=5,HorizontalAlignment=HorizontalAlignment.Right};var refresh=Visuals.Button("刷新额度",()=>_=app.Refresh());status.Children.Add(refresh);status.Children.Add(syncStatus);Grid.SetColumn(status,1);header.Children.Add(status);root.Children.Add(header);
        var navigation=new Grid {ColumnSpacing=6,Margin=new Thickness(24,0,24,0)};string[] names={"额度总览","账户连接","悬浮窗","偏好设置"};
        for(int i=0;i<names.Length;i++){int index=i;navigation.ColumnDefinitions.Add(new ColumnDefinition());var tab=new ToggleButton {Content=names[i],HorizontalAlignment=HorizontalAlignment.Stretch,MinHeight=36,FontSize=14};tab.Click+=(_,_)=>SelectPage(index);Grid.SetColumn(tab,i);navigation.Children.Add(tab);tabs.Add(tab);}
        Grid.SetRow(navigation,1);root.Children.Add(navigation);
        var scroll=new ScrollViewer {Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,2);root.Children.Add(scroll);
        var footer=new Grid {Padding=new Thickness(24,10,24,14),ColumnSpacing=16};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        feedback.VerticalAlignment=VerticalAlignment.Center;footer.Children.Add(feedback);var quit=Visuals.Button("退出",app.Quit);Grid.SetColumn(quit,1);footer.Children.Add(quit);Grid.SetRow(footer,3);root.Children.Add(footer);
        inputTimer.Tick+=(_,_)=>FlushInput();app.Changed+=ReadingsChanged;Closed+=(_,_)=>{FlushInput();inputTimer.Stop();app.Changed-=ReadingsChanged;};
        double scale=Desktop.Scale(this);var work=Desktop.Work(this);int width=Math.Min((int)(700*scale),work.Width),height=Math.Min((int)(780*scale),work.Height);
        AppWindow.MoveAndResize(new RectInt32(work.Left+(work.Width-width)/2,work.Top+(work.Height-height)/2,width,height));
        SelectPage(0);UpdateSync();
    }
    static Preferences Copy(Preferences value)=>JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(value))!;
    bool Change(Action<Preferences> edit)
    {
        try
        {
            var next=Copy(app.Settings);edit(next);
            if(next.Enabled.Count==0)throw new FormatException("请至少保留一个显示项目。");
            changing=true;app.Apply(next);Visuals.Theme(root,next);feedback.Text=app.Demo?"示例数据 · 更改不写入账户配置":"已自动保存";if(currentPage==2)UpdatePreview();return true;
        }
        catch(FormatException e){feedback.Text=e.Message;return false;}
        catch{feedback.Text="保存失败，请检查设置目录权限。";return false;}
        finally{changing=false;}
    }
    void QueueInput(){inputTimer.Stop();inputTimer.Start();feedback.Text="正在等待输入完成…";}
    void FlushInput()
    {
        inputTimer.Stop();if(pendingSecrets.Count==0 && pendingPath is null)return;
        try
        {
            var encrypted=new Dictionary<string,string>();
            foreach(var (id,entry) in pendingSecrets)
            {
                if(string.IsNullOrWhiteSpace(entry))continue;
                var text=entry.Trim();if(id=="mimo")text=Providers.NormalizeCookie(text);else if(text.Any(char.IsWhiteSpace))throw new FormatException("API Key 不能包含空格或换行。");
                encrypted[id]=SettingsStore.Protect(text);
            }
            if(Change(p=>{foreach(var (id,value) in encrypted)p.ProtectedSecrets[id]=value;if(pendingPath is not null)p.CodexPath=pendingPath;})){pendingSecrets.Clear();pendingPath=null;}
        }
        catch(FormatException e){feedback.Text=e.Message;}
        catch{feedback.Text="凭据未保存，请检查输入。";}
    }
    public void SelectPage(int index)
    {
        if(index<0 || index>=tabs.Count)return;
        FlushInput();currentPage=index;updateConnection=null;for(int i=0;i<tabs.Count;i++)tabs[i].IsChecked=i==index;
        if(preview.Parent is Panel oldParent)oldParent.Children.Remove(preview);body.Children.Clear();preview.Children.Clear();
        if(index==0)DisplayPage();else if(index==1)AccountPage();else if(index==2)AppearancePage();else ProgressPage();
    }
    static TextBlock Label(string text,double size=16){var label=Visuals.Text(text,size);label.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;return label;}
    static Border Card(UIElement child,double padding=16)
    {
        var card=(Border)XamlReader.Load("<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Background='{ThemeResource CardBackgroundFillColorDefaultBrush}' BorderBrush='{ThemeResource CardStrokeColorDefaultBrush}' BorderThickness='1' CornerRadius='8'/>");card.Padding=new Thickness(padding);card.Child=child;return card;
    }
    static Grid Row(string title,string description,FrameworkElement control)
    {
        var row=new Grid {ColumnSpacing=20};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var text=new StackPanel {Spacing=4,VerticalAlignment=VerticalAlignment.Center};text.Children.Add(Label(title,14));if(description.Length>0)text.Children.Add(Visuals.Text(description,12,true));row.Children.Add(text);control.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(control,1);row.Children.Add(control);return row;
    }
    void UpdateSync(){var latest=app.Readings.Values.Where(r=>r.Updated is not null).Select(r=>r.Updated).Max();syncStatus.Text=app.Demo?"示例数据":latest is null?"等待连接":$"更新于 {latest.Value.LocalDateTime:HH:mm}";}
    void ReadingsChanged(){UpdateSync();if(changing)return;if(currentPage==0){body.Children.Clear();DisplayPage();}else if(currentPage==2)UpdatePreview();else updateConnection?.Invoke();}
    void DisplayPage()
    {
        body.Children.Add(Row("账户概览","名称旁的开关控制悬浮窗显示。",new TextBlock()));
        var cards=new Grid {ColumnSpacing=12,RowSpacing=12};cards.ColumnDefinitions.Add(new ColumnDefinition());cards.ColumnDefinitions.Add(new ColumnDefinition());cards.RowDefinitions.Add(new RowDefinition());cards.RowDefinitions.Add(new RowDefinition());int index=0;
        foreach(var (id,name) in Providers.Names)
        {
            var r=app.Readings.GetValueOrDefault(id)??Providers.Empty(id);var content=new StackPanel {Spacing=12};
            var toggle=new ToggleSwitch {IsOn=app.Settings.Enabled.Contains(id),OnContent="",OffContent="",MinWidth=0};bool reverting=false;
            toggle.Toggled+=(_,_)=>{if(reverting)return;if(!Change(p=>{if(toggle.IsOn){if(!p.Enabled.Contains(id))p.Enabled.Add(id);}else p.Enabled.Remove(id);})){reverting=true;toggle.IsOn=app.Settings.Enabled.Contains(id);reverting=false;}};
            content.Children.Add(Row(name,"",toggle));
            if(id=="codex")
            {
                var quotas=new Grid {ColumnSpacing=20};int col=0;
                foreach(var (minutes,title) in CapsuleData.CodexWindows(r))
                {
                    quotas.ColumnDefinitions.Add(new ColumnDefinition());var metric=CapsuleData.Quota(r,minutes);var block=new StackPanel {Spacing=5};block.Children.Add(Visuals.Text(minutes==300?"5 小时剩余":"本周剩余",12,true));block.Children.Add(Label(metric?.Display??"—",30));
                    if(metric?.RemainingPercent is {} percent)block.Children.Add(new ProgressBar {Value=percent,Height=3,Minimum=0,Maximum=100,IsIndeterminate=false});Grid.SetColumn(block,col++);quotas.Children.Add(block);
                }
                content.Children.Add(quotas);
            }
            else
            {
                content.Children.Add(Visuals.Text("可用余额",12,true));content.Children.Add(Label(r.Error is null?r.Headline:"—",30));
                if(CapsuleData.CashProgress(r,app.Settings) is {} fraction)content.Children.Add(new ProgressBar {Value=fraction*100,Height=3});
            }
            var link=Visuals.Button(r.Error is null && r.Updated is not null?"管理账户":"连接账户",()=>SelectAccount(id));link.HorizontalAlignment=HorizontalAlignment.Left;link.FontSize=12;link.Padding=new Thickness(10,4,10,4);link.MinHeight=28;content.Children.Add(link);
            var card=Card(content);Grid.SetColumn(card,index%2);Grid.SetRow(card,index/2);cards.Children.Add(card);index++;
        }
        body.Children.Add(cards);
        body.Children.Add(Visuals.Text("Codex 的 5h 与周额度分别显示；接口未提供 5h 时自动收起。",12,true));
    }
    public void SelectAccount(string id){selectedAccount=id;SelectPage(1);}
    void AccountPage()
    {
        body.Children.Add(Choices(Providers.Names.Values.ToArray(),Array.IndexOf(Providers.Names.Keys.ToArray(),selectedAccount),i=>SelectAccount(Providers.Names.Keys.ElementAt(i))));string id=selectedAccount;
        var form=new StackPanel {Spacing=14};body.Children.Add(Card(form));
        if(id=="codex")
        {
            form.Children.Add(Label("本机 Codex 登录"));form.Children.Add(Visuals.Text("读取 ChatGPT 订阅的 5h 和周额度，不需要 API Key。",14,true));
            var path=new TextBox {Text=app.Settings.CodexPath,Header="Codex 程序路径",PlaceholderText="留空自动寻找 codex.exe"};path.TextChanged+=(_,_)=>{pendingPath=path.Text.Trim();QueueInput();};path.LostFocus+=(_,_)=>FlushInput();form.Children.Add(path);
        }
        else
        {
            var password=new PasswordBox {Header=id=="mimo"?"网页登录 Cookie":"API Key",PlaceholderText=app.Settings.ProtectedSecrets.ContainsKey(id)?"已加密保存 · 留空保留原值":"输入后自动加密保存于本机",MaxLength=20000};password.PasswordChanged+=(_,_)=>{pendingSecrets[id]=password.Password;QueueInput();};password.LostFocus+=(_,_)=>FlushInput();form.Children.Add(password);
            form.Children.Add(Visuals.Button("清除已保存的凭据",()=>{password.Password="";pendingSecrets.Remove(id);Change(p=>p.ProtectedSecrets.Remove(id));}));
            if(id=="mimo"){form.Children.Add(Visuals.Button("打开 MiMo 控制台",()=>Process.Start(new ProcessStartInfo("https://platform.xiaomimimo.com/#/console/balance"){UseShellExecute=true})));form.Children.Add(Visuals.Text("登录后按 F12 → 网络 → 刷新余额页，复制 balance 请求的 Cookie。普通模型 API Key 不能读取此余额。",14,true));}
        }
        var status=new StackPanel {Spacing=10};status.Children.Add(Label("连接状态"));var details=Visuals.Text("",14,true);status.Children.Add(details);body.Children.Add(Card(status));
        updateConnection=()=>{var r=app.Readings.GetValueOrDefault(id);details.Text=r?.Error??(r?.Updated is null?"填写账户后自动查询。":string.Join("\n",r.Metrics.Select(m=>$"{m.Label}  {m.Display}"+(m.Reset is null?"":" · "+m.ResetText))));};updateConnection();
        body.Children.Add(Visuals.Text("账户凭据仅加密保存在本机。修改完成后自动连接，无需保存。",12,true));
    }
    public void SelectDisplayStyle(string style)
    {
        if(style is not ("Capsule" or "Bar" or "Minimal"))return;
        if(Change(p=>p.DisplayStyle=style))SelectPage(2);
    }
    void UpdatePreview()
    {
        preview.Children.Clear();preview.ColumnDefinitions.Clear();preview.RowDefinitions.Clear();preview.ColumnDefinitions.Add(new ColumnDefinition());preview.ColumnDefinitions.Add(new ColumnDefinition());
        var items=new List<(string Title,string Value,double? Fraction)>();
        foreach(var id in app.Settings.Enabled)
        {
            var r=app.Readings.GetValueOrDefault(id)??Providers.Empty(id);
            if(id=="codex")foreach(var (minutes,title) in CapsuleData.CodexWindows(r)){var m=CapsuleData.Quota(r,minutes);items.Add((title,m?.Display??"—",m?.RemainingPercent/100));}
            else items.Add((r.Name,r.Error is null?r.Headline:"—",CapsuleData.CashProgress(r,app.Settings)));
        }
        for(int i=0;i<items.Count;i++){if(i%2==0)preview.RowDefinitions.Add(new RowDefinition());var item=items[i];var capsule=new Capsule(item.Title,item.Value,item.Fraction,app.Settings,false){Height=44};Grid.SetRow(capsule,i/2);Grid.SetColumn(capsule,i%2);preview.Children.Add(capsule);}
    }
    void AppearancePage()
    {
        var live=new StackPanel {Spacing=10};live.Children.Add(Row("实时预览","",Visuals.Text("使用当前账户数据",12,true)));live.Children.Add(preview);body.Children.Add(Card(live,12));UpdatePreview();
        string[] modes={"System","Light","Dark"};var theme=Choices(new[]{"跟随系统","浅色","深色"},Math.Max(0,Array.IndexOf(modes,app.Settings.Theme)),i=>{if(Change(p=>p.Theme=modes[i]))SelectPage(2);});theme.MinWidth=150;body.Children.Add(Card(Row("颜色模式","使用 Windows 原生颜色与强调色",theme)));
        var styles=new Grid {ColumnSpacing=8};int styleIndex=0;
        foreach(var (key,title) in new[]{("Capsule","胶囊填充"),("Bar","细进度条"),("Minimal","仅数字")})
        {
            int column=styleIndex++;styles.ColumnDefinitions.Add(new ColumnDefinition());var sample=Copy(app.Settings);sample.DisplayStyle=key;
            var content=new StackPanel {Spacing=8};content.Children.Add(new Capsule("Codex", "72%",.72,sample,false){Width=142,Height=40});content.Children.Add(Visuals.Text(title,12));
            var button=new ToggleButton {Content=content,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Center,Padding=new Thickness(8),IsChecked=app.Settings.DisplayStyle==key};button.Click+=(_,_)=>SelectDisplayStyle(key);Grid.SetColumn(button,column);styles.Children.Add(button);
        }
        body.Children.Add(styles);
        var size=new StackPanel {Spacing=8};var actual=Visuals.Text("",12,true);size.Children.Add(Row("悬浮窗尺寸","像素 · 自动适配内容",actual));
        void UpdateActual(){var r=Desktop.Bounds(app.Floating);actual.Text=$"{r.Width} × {r.Height}";}
        var controls=new Grid {ColumnSpacing=24};controls.ColumnDefinitions.Add(new ColumnDefinition());controls.ColumnDefinitions.Add(new ColumnDefinition());
        var width=new Slider {Header="宽度",Minimum=160,Maximum=1200,StepFrequency=4,Value=Math.Clamp(app.Settings.FloatingWidthPixels,160,1200)};
        var bounds=Desktop.Bounds(app.Floating);var height=new Slider {Header="高度",Minimum=60,Maximum=1000,StepFrequency=4,Value=Math.Clamp(bounds.Height,60,1000)};
        width.ValueChanged+=(_,_)=>{Change(p=>p.FloatingWidthPixels=(int)width.Value);UpdateActual();};height.ValueChanged+=(_,_)=>{Change(p=>p.FloatingHeightPixels=(int)height.Value);UpdateActual();};controls.Children.Add(width);Grid.SetColumn(height,1);controls.Children.Add(height);size.Children.Add(controls);UpdateActual();
        var reset=Visuals.Button("恢复紧凑尺寸",()=>{Change(p=>{p.FloatingWidthPixels=192;p.FloatingHeightPixels=null;});SelectPage(2);});reset.HorizontalAlignment=HorizontalAlignment.Left;reset.FontSize=12;reset.MinHeight=24;reset.Padding=new Thickness(8,2,8,2);size.Children.Add(reset);body.Children.Add(Card(size,12));

    }
    void ProgressPage()
    {
        var behaviour=new StackPanel {Spacing=12};var top=new ToggleSwitch {IsOn=app.Settings.AlwaysOnTop,OnContent="",OffContent="",MinWidth=0};top.Toggled+=(_,_)=>Change(p=>p.AlwaysOnTop=top.IsOn);behaviour.Children.Add(Row("始终置顶","",top));
        var locked=new ToggleSwitch {IsOn=app.Settings.Locked,OnContent="",OffContent="",MinWidth=0};lockToggle=locked;locked.Toggled+=(_,_)=>{if(!synchronizingLock)Change(p=>p.Locked=locked.IsOn);};behaviour.Children.Add(Row("锁定位置与点击","可从托盘菜单或 Ctrl + Alt + L 解锁",locked));body.Children.Add(Card(behaviour));
        bool startupChanging=false;var startup=new ToggleSwitch {Name="StartupToggle",IsOn=app.Startup.Enabled,OnContent="",OffContent="",MinWidth=0};
        startup.Toggled+=(_,_)=>
        {
            if(startupChanging)return;
            try{app.Startup.SetEnabled(startup.IsOn);feedback.Text=app.Demo?"示例模式 · 不修改系统启动项":startup.IsOn?"已开启开机自启":"已关闭开机自启";}
            catch{startupChanging=true;try{startup.IsOn=app.Startup.Enabled;}catch{startup.IsOn=false;}finally{startupChanging=false;}feedback.Text="无法修改启动项，请检查当前用户的权限。";}
        };
        body.Children.Add(Card(Row("开机自启","登录 Windows 后自动显示悬浮窗",startup)));
        int[] intervals={30,120,300,900};var interval=Choices(new[]{"30 秒","2 分钟","5 分钟","15 分钟"},Math.Max(0,Array.IndexOf(intervals,app.Settings.RefreshSeconds)),i=>Change(p=>p.RefreshSeconds=intervals[i]));interval.MinWidth=150;body.Children.Add(Card(Row("自动刷新","按间隔更新已开启的账户",interval)));
        body.Children.Add(Label("余额进度"));body.Children.Add(Visuals.Text("Codex 使用真实额度比例。现金余额可设置参考金额；留空只显示金额。底色跟随 Windows 强调色。",14,true));
        foreach(var id in Providers.Names.Keys.Where(id=>id!="codex"))
        {
            string currency=app.Readings.GetValueOrDefault(id)?.Metrics.FirstOrDefault()?.Unit??(id=="openrouter"?"USD":"CNY"),key=CapsuleData.ReferenceKey(id,currency);
            var input=new TextBox {Width=150,Text=app.Settings.BalanceReferences.TryGetValue(key,out var v)?v.ToString(CultureInfo.InvariantCulture):"",PlaceholderText="参考金额",MaxLength=16};
            input.TextChanged+=(_,_)=>{if(string.IsNullOrWhiteSpace(input.Text)){Change(p=>p.BalanceReferences.Remove(key));return;}if(!decimal.TryParse(input.Text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var amount)||amount<=0){feedback.Text="参考金额应大于 0，或留空；无效输入不会保存。";return;}Change(p=>p.BalanceReferences[key]=amount);};body.Children.Add(Card(Row(Providers.Names[id],currency+" · 参考金额",input)));
        }
        body.Children.Add(Visuals.Text("例如：参考金额 100、余额 60 → 底色填充 60%。\n超过参考金额显示满格；不同币种分别保存。",14,true));
    }
    public void UpdateLockState()
    {if(lockToggle is null)return;synchronizingLock=true;try{lockToggle.IsOn=app.Settings.Locked;}finally{synchronizingLock=false;}}
    static ComboBox Choices(string[] labels,int selected,Action<int> action)
    {
        var combo=new ComboBox {HorizontalAlignment=HorizontalAlignment.Stretch,ItemsSource=labels,SelectedIndex=selected};combo.SelectionChanged+=(_,_)=>{if(combo.SelectedIndex>=0)action(combo.SelectedIndex);};return combo;
    }
}

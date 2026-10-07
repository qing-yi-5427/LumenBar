using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace LumenBar;
public sealed class SettingsWindow : Window
{
    readonly App app;
    readonly Preferences draft;
    readonly GlassSurface surface=new();
    readonly Grid page=new();
    readonly TextBlock feedback=Ui.Text("",11,"MutedBrush");
    readonly Dictionary<string,PasswordBox> secrets=new();
    readonly HashSet<string> removed=new();
    readonly TextBox codexPath=new();
    readonly StackPanel accountBody=new();
    readonly List<Button> navigation=new();
    readonly Dictionary<string,string> referenceEdits=new();
    string selectedAccount="codex";
    int selectedPage;
    public SettingsWindow(App app)
    {
        this.app=app;draft=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(app.Settings))!;
        Title="LumenBar · 设置";Width=610;Height=550;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        GlassSurface.ConfigureWindow(this,false);Content=surface;
        foreach(var id in Providers.Names.Keys.Where(x=>x!="codex"))secrets[id]=new PasswordBox {MaxLength=20000};
        codexPath.Text=draft.CodexPath;
        var root=new Grid {Margin=new Thickness(28,22,28,20)};surface.Child=root;
        root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
        var header=new Grid {Margin=new Thickness(0,0,0,22)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var heading=new StackPanel();var brand=Ui.Text("LumenBar",23);brand.FontWeight=FontWeights.SemiBold;heading.Children.Add(brand);
        var sub=Ui.Text("只看你关心的余额",12,"MutedBrush");sub.Margin=new Thickness(0,4,0,0);heading.Children.Add(sub);header.Children.Add(heading);
        var close=Ui.Button("关闭",Close);close.VerticalAlignment=VerticalAlignment.Top;Grid.SetColumn(close,1);header.Children.Add(close);root.Children.Add(header);
        header.MouseLeftButtonDown+=(_,e)=>{if(e.OriginalSource is not FrameworkElement el || !HasButtonParent(el)){try{DragMove();}catch(InvalidOperationException){}}};
        var tabs=new UniformGrid {Columns=4,Margin=new Thickness(0,0,0,24)};
        foreach(var (name,index) in new[]{("显示项目",0),("账户连接",1),("玻璃外观",2),("进度底色",3)})
        {var button=Ui.Button(name,()=>SelectPage(index));button.Margin=new Thickness(3,0,3,0);button.Padding=new Thickness(12,10,12,10);navigation.Add(button);tabs.Children.Add(button);}
        Grid.SetRow(tabs,1);root.Children.Add(tabs);Grid.SetRow(page,2);root.Children.Add(page);
        var footer=new Grid {Margin=new Thickness(0,18,0,0)};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var left=new StackPanel();left.Children.Add(feedback);var quit=Ui.Button("退出程序",()=>app.Shutdown());quit.Background=Brushes.Transparent;quit.Padding=new Thickness(0,6,0,0);quit.HorizontalAlignment=HorizontalAlignment.Left;quit.FontSize=11;left.Children.Add(quit);footer.Children.Add(left);
        var save=Ui.Button("保存更改",Save);save.Padding=new Thickness(20,10,20,10);save.VerticalAlignment=VerticalAlignment.Center;save.SetResourceReference(BackgroundProperty,"SelectedBrush");Grid.SetColumn(save,1);footer.Children.Add(save);Grid.SetRow(footer,3);root.Children.Add(footer);
        SourceInitialized+=(_,_)=>ApplyAppearance();Closed+=(_,_)=>{app.ApplyTheme();app.Floating.ApplyAppearance();};SelectPage(0);
        if(app.Demo)feedback.Text="示例数据 · 设置不会保存";
    }
    static bool HasButtonParent(DependencyObject node){for(var p=node;p is not null;p=VisualTreeHelper.GetParent(p))if(p is Button)return true;return false;}
    public void ApplyAppearance()=>surface.Apply(this,draft);
    void Preview(){app.PreviewAppearance(draft);ApplyAppearance();}
    public void SelectPage(int index)
    {
        selectedPage=index;page.Children.Clear();for(int i=0;i<navigation.Count;i++)navigation[i].SetResourceReference(BackgroundProperty,i==index?"SelectedBrush":"ControlBrush");
        if(index==0)DisplayPage();else if(index==1)AccountsPage();else if(index==2)AppearancePage();else ProgressPage();
    }
    Border Card(UIElement child,Thickness? margin=null)
    {var b=new Border {Child=child,CornerRadius=new CornerRadius(12),Padding=new Thickness(18),Margin=margin??new Thickness(0)};b.SetResourceReference(Border.BackgroundProperty,"ControlBrush");return b;}
    void DisplayPage()
    {
        var body=new StackPanel();page.Children.Add(body);var grid=new UniformGrid {Columns=2};body.Children.Add(grid);
        foreach(var pair in Providers.Names)
        {
            var content=new StackPanel();var toggle=new CheckBox {Content=pair.Value,FontSize=15,FontWeight=FontWeights.SemiBold,IsChecked=draft.Enabled.Contains(pair.Key)};
            var id=pair.Key;toggle.Checked+=(_,_)=>{if(!draft.Enabled.Contains(id))draft.Enabled.Add(id);};toggle.Unchecked+=(_,_)=>draft.Enabled.Remove(id);content.Children.Add(toggle);
            var r=app.Readings.GetValueOrDefault(id);string state=id=="codex"?"5h 与周额度 · 两颗胶囊":r is {Error:null,Updated:not null}?r.Headline:app.Settings.ProtectedSecrets.ContainsKey(id)?"已配置账户":"尚未连接";
            var detail=Ui.Text(state,12,"MutedBrush");detail.Margin=new Thickness(0,12,0,0);content.Children.Add(detail);grid.Children.Add(Card(content,new Thickness(4)));
        }
        var info=Ui.Text("开启后出现在悬浮窗中，关闭会保留已保存的账户凭据。",12,"MutedBrush");info.Margin=new Thickness(4,14,0,0);body.Children.Add(info);
        var gestures=Ui.Text("拖动移动  ·  边缘缩放  ·  双击或右键打开设置",11,"MutedBrush");gestures.Margin=new Thickness(4,18,0,0);body.Children.Add(gestures);
        var actions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(4,14,0,0)};
        var locking=Ui.Button(app.Settings.Locked?"解锁悬浮窗":"锁定悬浮窗",()=>{app.ToggleLock();SelectPage(0);});locking.Padding=new Thickness(12,7,12,7);actions.Children.Add(locking);
        var tip=Ui.Text("Ctrl + Alt + L 解锁",11,"MutedBrush");tip.Margin=new Thickness(14,0,0,0);tip.VerticalAlignment=VerticalAlignment.Center;actions.Children.Add(tip);body.Children.Add(actions);
        if(!app.Floating.UnlockHotkeyAvailable)feedback.Text="解锁快捷键被占用；重新运行程序也能解锁。";
    }
    void AccountsPage()
    {
        if(accountBody.Parent is Panel previous)previous.Children.Remove(accountBody);
        var panel=new StackPanel();page.Children.Add(panel);
        panel.Children.Add(Choices(Providers.Names.Values.ToArray(),Array.IndexOf(Providers.Names.Keys.ToArray(),selectedAccount),i=>SelectAccount(Providers.Names.Keys.ElementAt(i))));
        accountBody.Margin=new Thickness(0,20,0,0);panel.Children.Add(accountBody);ShowAccount();
    }
    public void SelectAccount(string id){selectedAccount=id;SelectPage(1);}
    void ShowAccount()
    {
        accountBody.Children.Clear();string id=selectedAccount;
        if(id=="codex")
        {
            accountBody.Children.Add(Label("本机 Codex"));accountBody.Children.Add(Note("直接读取 ChatGPT 订阅额度，不需要 API Key。"));accountBody.Children.Add(codexPath);
            var choose=Ui.Button("选择 codex.exe",()=>{var dialog=new Microsoft.Win32.OpenFileDialog {Filter="Codex|codex.exe|应用程序|*.exe"};if(dialog.ShowDialog(this)==true)codexPath.Text=dialog.FileName;});choose.Margin=new Thickness(0,10,0,0);choose.HorizontalAlignment=HorizontalAlignment.Left;accountBody.Children.Add(choose);
        }
        else
        {
            accountBody.Children.Add(Label(id=="mimo"?"网页登录 Cookie":"API Key"));
            accountBody.Children.Add(Note(draft.ProtectedSecrets.ContainsKey(id)&&!removed.Contains(id)?"已加密保存。留空保留，填写新值替换。":"只存储于本机，使用 Windows 用户加密。"));
            accountBody.Children.Add(secrets[id]);
            var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,0)};
            var remove=Ui.Button("清除凭据",()=>{removed.Add(id);secrets[id].Clear();feedback.Text="保存后清除 "+Providers.Names[id]+" 凭据";});row.Children.Add(remove);
            if(id=="mimo")
            {var login=Ui.Button("打开 MiMo",()=>Process.Start(new ProcessStartInfo("https://platform.xiaomimimo.com/#/console/balance"){UseShellExecute=true}));login.Margin=new Thickness(10,0,0,0);row.Children.Add(login);}
            accountBody.Children.Add(row);
            if(id=="mimo")accountBody.Children.Add(Note("登录后按 F12 → 网络 → 刷新余额页，复制 balance 请求的 Cookie。普通模型 API Key 无法读取此余额。"));
        }
        var reading=app.Readings.GetValueOrDefault(id);
        var status=new StackPanel {Margin=new Thickness(0,18,0,0)};status.Children.Add(Label("连接状态"));
        status.Children.Add(Note(reading?.Error ?? (reading?.Updated is null?"保存后查询账户。":string.Join("\n",reading.Metrics.Select(m=>$"{m.Label} {m.Display}"+(m.Reset is null?"":" · "+m.ResetText))))));
        if(reading?.Updated is not null)status.Children.Add(Note($"上次读取 {reading.Updated.Value.LocalDateTime:MM-dd HH:mm} · {reading.Source}"));
        accountBody.Children.Add(status);
    }
    void AppearancePage()
    {
        var body=new StackPanel();page.Children.Add(body);
        body.Children.Add(Label("外观"));var modes=new[]{"Wallpaper","System","Light","Dark"};
        body.Children.Add(Choices(new[]{"跟随壁纸","跟随系统","浅色","深色"},Math.Max(0,Array.IndexOf(modes,draft.Theme)),i=>{draft.Theme=modes[i];Preview();}));
        var density=new Grid {Margin=new Thickness(0,18,0,12)};density.ColumnDefinitions.Add(new ColumnDefinition());density.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(230)});
        var densityLabel=Label("玻璃浓度");densityLabel.VerticalAlignment=VerticalAlignment.Center;density.Children.Add(densityLabel);
        var slider=new Slider {Minimum=.08,Maximum=.75,Value=draft.GlassOpacity,VerticalAlignment=VerticalAlignment.Center,ToolTip="左侧更通透，右侧更清晰"};slider.ValueChanged+=(_,_)=>{draft.GlassOpacity=slider.Value;Preview();};Grid.SetColumn(slider,1);density.Children.Add(slider);body.Children.Add(density);
        var tint=new CheckBox {Content="以壁纸色彩染色",IsChecked=draft.WallpaperTint,Margin=new Thickness(0,7,0,14)};tint.Checked+=(_,_)=>{draft.WallpaperTint=true;Preview();};tint.Unchecked+=(_,_)=>{draft.WallpaperTint=false;Preview();};body.Children.Add(tint);
        var pin=new CheckBox {Content="始终置顶",IsChecked=draft.AlwaysOnTop,Margin=new Thickness(0,0,0,18)};pin.Checked+=(_,_)=>draft.AlwaysOnTop=true;pin.Unchecked+=(_,_)=>draft.AlwaysOnTop=false;body.Children.Add(pin);
        body.Children.Add(Label("刷新间隔"));var seconds=new[]{30,120,300,900};body.Children.Add(Choices(new[]{"30 秒","2 分钟","5 分钟","15 分钟"},Math.Max(0,Array.IndexOf(seconds,draft.RefreshSeconds)),i=>draft.RefreshSeconds=seconds[i]));
        var reset=Ui.Button("恢复紧凑尺寸",()=>{draft.FloatingHeightPixels=null;draft.FloatingWidthPixels=192;feedback.Text="保存后恢复紧凑尺寸";});reset.Margin=new Thickness(0,16,0,0);reset.HorizontalAlignment=HorizontalAlignment.Left;body.Children.Add(reset);
        body.Children.Add(Note("液态玻璃使用当前静态壁纸折射，不采样其他应用窗口。"));
    }
    void ProgressPage()
    {
        var body=new StackPanel();page.Children.Add(body);
        body.Children.Add(Label("用胶囊底色表示剩余比例"));
        body.Children.Add(Note("Codex 的 5h 与周额度直接使用平台百分比。\n现金余额可填写参考金额；留空不显示进度，金额本身始终正常显示。"));
        foreach(var id in Providers.Names.Keys.Where(x=>x!="codex"))
        {
            string unit=app.Readings.GetValueOrDefault(id)?.Metrics.FirstOrDefault()?.Unit ?? (id=="openrouter"?"USD":"CNY");
            string key=CapsuleData.ReferenceKey(id,unit);
            var row=new Grid {Margin=new Thickness(0,6,0,6)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(145)});
            var label=Ui.Text(Providers.Names[id]+" · "+unit,13);label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);
            var field=new TextBox {Text=referenceEdits.TryGetValue(key,out var edit)?edit:draft.BalanceReferences.TryGetValue(key,out var amount)?amount.ToString(System.Globalization.CultureInfo.InvariantCulture):"",ToolTip="底色占满胶囊时对应的金额",MaxLength=16};
            field.TextChanged+=(_,_)=>referenceEdits[key]=field.Text;Grid.SetColumn(field,1);row.Children.Add(field);body.Children.Add(row);
        }
        body.Children.Add(Note("例如参考金额 100，余额 60 时填充 60%。\n超过参考金额显示满格；不同币种分别保存。"));
    }
    FrameworkElement Choices(string[] labels,int selected,Action<int> change)
    {
        var row=new UniformGrid {Columns=labels.Length};var buttons=new List<Button>();
        for(int i=0;i<labels.Length;i++)
        {int index=i;var b=Ui.Button(labels[i],()=>{for(int j=0;j<buttons.Count;j++)buttons[j].SetResourceReference(BackgroundProperty,j==index?"SelectedBrush":"ControlBrush");change(index);});b.FontSize=12;b.Padding=new Thickness(5,9,5,9);b.Margin=new Thickness(0,0,i==labels.Length-1?0:5,0);b.SetResourceReference(BackgroundProperty,i==selected?"SelectedBrush":"ControlBrush");buttons.Add(b);row.Children.Add(b);}
        return row;
    }
    static TextBlock Label(string text){var t=Ui.Text(text,13);t.FontWeight=FontWeights.Medium;t.Margin=new Thickness(0,0,0,8);return t;}
    static TextBlock Note(string text){var t=Ui.Text(text,12,"MutedBrush");t.Margin=new Thickness(0,6,0,10);return t;}
    void Save()
    {
        try
        {
            if(draft.Enabled.Count==0)throw new FormatException("请至少开启一个显示项目。");
            foreach(var pair in referenceEdits)
            {
                if(string.IsNullOrWhiteSpace(pair.Value)){draft.BalanceReferences.Remove(pair.Key);continue;}
                if(!decimal.TryParse(pair.Value,System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out var amount) || amount<=0)throw new FormatException("参考金额请填写大于 0 的数值，或留空。");
                draft.BalanceReferences[pair.Key]=amount;
            }
            foreach(var id in removed)draft.ProtectedSecrets.Remove(id);
            foreach(var pair in secrets)if(!string.IsNullOrWhiteSpace(pair.Value.Password))
            {string text=pair.Value.Password.Trim();if(pair.Key=="mimo")text=Providers.NormalizeCookie(text);else if(text.Any(char.IsWhiteSpace))throw new FormatException("API Key 不能包含空格或换行。");draft.ProtectedSecrets[pair.Key]=SettingsStore.Protect(text);}
            draft.CodexPath=codexPath.Text.Trim();draft.Locked=app.Settings.Locked;
            if(draft.Enabled.Count!=app.Settings.Enabled.Count)draft.FloatingHeightPixels=null;
            app.Apply(draft);Close();
        }
        catch(FormatException ex){feedback.Text=ex.Message;}
        catch{feedback.Text="无法保存，请检查凭据格式或目录权限。";}
    }
}

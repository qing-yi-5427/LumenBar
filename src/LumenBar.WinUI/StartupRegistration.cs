using Microsoft.Win32;
namespace LumenBar.WinUI;

// The OS registration is the source of truth, so removing it outside the app is respected.
internal sealed class StartupRegistration
{
    const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    readonly string keyPath;
    readonly bool simulate;
    bool simulatedEnabled;
    public StartupRegistration(bool simulate=false,string? testKeyPath=null)
    {this.simulate=simulate;keyPath=testKeyPath??RunKey;}
    public static string CommandFor(string executable)=>$"\"{executable}\" --startup";
    public bool Enabled
    {
        get
        {
            if(simulate)return simulatedEnabled;
            using var key=Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue("LumenBar") is string command && string.Equals(command,CommandFor(Environment.ProcessPath!),StringComparison.OrdinalIgnoreCase);
        }
    }
    public void SetEnabled(bool enabled)
    {
        if(simulate){simulatedEnabled=enabled;return;}
        using var key=Registry.CurrentUser.CreateSubKey(keyPath,true);
        if(enabled)
        {
            var executable=Environment.ProcessPath??throw new IOException("无法确定程序位置。");
            var command=CommandFor(executable);
            if(command.Length>260)throw new IOException("程序路径过长，请先将程序放到较短的目录。");
            key.SetValue("LumenBar",command,RegistryValueKind.String);
        }
        else key.DeleteValue("LumenBar",false);
    }
}

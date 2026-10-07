using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace LumenBar;

public sealed class SettingsStore
{
    public string DirectoryPath { get; }
    public SettingsStore(string? directoryPath=null)=>DirectoryPath=directoryPath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LumenBar");
    public string? LoadWarning { get; private set; }
    public bool ShowIntro {get;private set;}
    public Preferences Load()
    {
        var path = Path.Combine(DirectoryPath, "settings.json");
        if(!File.Exists(path)){ShowIntro=true;return new();}
        try
        {
            var json=File.ReadAllText(path);
            var p = JsonSerializer.Deserialize<Preferences>(json) ?? new();
            using var document=JsonDocument.Parse(json);
            if(!document.RootElement.TryGetProperty("SchemaVersion",out _))
            {
                ShowIntro=true;
                p.FloatingHeightPixels=null;p.GlassOpacity=0.22;p.Locked=false;
            }
            if(p.Theme is not ("System" or "Light" or "Dark"))p.Theme="System";
            if(p.DisplayStyle is not ("Capsule" or "Bar" or "Minimal"))p.DisplayStyle="Capsule";
            p.RefreshSeconds = Math.Clamp(p.RefreshSeconds,30,3600);
            p.GlassOpacity = double.IsFinite(p.GlassOpacity) ? Math.Clamp(p.GlassOpacity,0.08,0.75) : 0.22;
            p.Enabled ??= new(); p.ProtectedSecrets ??= new();p.BalanceReferences ??= new();
            p.Enabled=p.Enabled.FindAll(Providers.Names.ContainsKey);if(p.Enabled.Count==0)p.Enabled.Add("codex");
            return p;
        }
        catch { LoadWarning = "设置文件无法读取，已使用默认设置；原文件会保留为备份。"; return new(); }
    }
    public void Save(Preferences p)
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath,"settings.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(p,new JsonSerializerOptions { WriteIndented=true }));
        if(File.Exists(path)) File.Replace(temp,path,path+".bak",true); else File.Move(temp,path);
    }
    public static string Protect(string plain) => Convert.ToBase64String(Crypt(Encoding.UTF8.GetBytes(plain),true));
    public static string Unprotect(string encrypted) => Encoding.UTF8.GetString(Crypt(Convert.FromBase64String(encrypted),false));
    static byte[] Crypt(byte[] bytes,bool encrypt)
    {
        var input = new Blob { Count=bytes.Length, Data=Marshal.AllocHGlobal(bytes.Length) };
        try
        {
            Marshal.Copy(bytes,0,input.Data,bytes.Length);
            Blob output;
            var ok = encrypt ? CryptProtectData(ref input,null,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output) : CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok) throw new InvalidOperationException("Windows 无法读取此用户的加密凭据，请重新填写。");
            try { var result=new byte[output.Count]; Marshal.Copy(output.Data,result,0,result.Length); return result; }
            finally { LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(input.Data); }
    }
    [StructLayout(LayoutKind.Sequential)] struct Blob { public int Count; public IntPtr Data; }
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true)] static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);
}

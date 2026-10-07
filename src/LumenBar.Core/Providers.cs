using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LumenBar;

public sealed class Providers : IDisposable
{
    readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect=false, UseCookies=false }) { Timeout=TimeSpan.FromSeconds(20), MaxResponseContentBufferSize=1024*1024 };
    public static readonly Dictionary<string,string> Names = new() { ["codex"]="Codex", ["deepseek"]="DeepSeek", ["mimo"]="MiMo", ["openrouter"]="OpenRouter" };
    public static Reading Empty(string id) => new(id,Names[id],id == "codex" ? "本地 Codex 登录" : "未连接账户",new());
    public async Task<Reading> ReadAsync(string id,Preferences p,CancellationToken token)
    {
        if(id == "codex") return await ReadCodexAsync(p.CodexPath,token);
        if(!p.ProtectedSecrets.TryGetValue(id,out var encrypted)) return Empty(id) with { Error=id == "mimo" ? "请在设置中填写 MiMo 登录 Cookie" : "请在设置中填写 API Key" };
        var secret=SettingsStore.Unprotect(encrypted).Trim();
        using var req = new HttpRequestMessage(HttpMethod.Get,id switch
        {
            "deepseek" => "https://api.deepseek.com/user/balance",
            "mimo" => "https://platform.xiaomimimo.com/api/v1/balance",
            "openrouter" => "https://openrouter.ai/api/v1/credits",
            _ => throw new NotSupportedException()
        });
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if(id == "mimo")
        {
            secret=NormalizeCookie(secret);
            req.Headers.Add("Cookie",secret);
            req.Headers.Add("Origin","https://platform.xiaomimimo.com");
            req.Headers.Referrer=new Uri("https://platform.xiaomimimo.com/");
            req.Headers.Add("x-timeZone","UTC+08:00");
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0.0.0 Safari/537.36");
        }
        else req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",secret);
        using var response=await http.SendAsync(req,token);
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || (int)response.StatusCode is >=300 and <400)
            throw new UnauthorizedAccessException(id == "mimo" ? "MiMo 会话过期，请重新登录并更新 Cookie" : "凭据无效或权限不足，请检查设置");
        if(response.StatusCode == HttpStatusCode.TooManyRequests) throw new InvalidOperationException("请求过于频繁，请稍后刷新或增加间隔");
        if(!response.IsSuccessStatusCode) throw new InvalidOperationException($"平台暂时不可用（HTTP {(int)response.StatusCode}）");
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return id switch { "deepseek" => Parsers.DeepSeek(doc.RootElement), "mimo" => Parsers.MiMo(doc.RootElement), "openrouter" => Parsers.OpenRouter(doc.RootElement), _ => throw new NotSupportedException() };
    }
    public static string NormalizeCookie(string value)
    {
        value=value.Trim();
        if(value.StartsWith("Cookie:",StringComparison.OrdinalIgnoreCase)) value=value[7..].Trim();
        if(value.Contains('\r') || value.Contains('\n')) throw new FormatException("请粘贴单行 Cookie 请求头");
        var parts=value.Split(';',StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if(!parts.Any(x=>x.StartsWith("api-platform_serviceToken=",StringComparison.Ordinal) && x.Length>26) || !parts.Any(x=>x.StartsWith("userId=",StringComparison.Ordinal) && x.Length>7))
            throw new FormatException("MiMo Cookie 需要 api-platform_serviceToken 和 userId");
        return value;
    }
    public static string FindCodex(string configured)
    {
        if(!string.IsNullOrWhiteSpace(configured))
        {
            if(File.Exists(configured) && Path.GetExtension(configured).Equals(".exe",StringComparison.OrdinalIgnoreCase)) return configured;
            throw new InvalidOperationException("请选择有效的 codex.exe 文件");
        }
        foreach(var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var file=Path.Combine(dir.Trim('"'),"codex.exe");
            if(File.Exists(file)) return file;
        }
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");
        if(Directory.Exists(root))
        {
            var found=Directory.EnumerateFiles(root,"codex.exe",SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if(found is not null) return found;
        }
        throw new InvalidOperationException("未找到 Codex CLI，请在设置中选择 codex.exe");
    }
    public static async Task<Reading> ReadCodexAsync(string path,CancellationToken outer)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(outer);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token=timeout.Token;
        var start=new ProcessStartInfo(FindCodex(path)) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true, WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
        start.ArgumentList.Add("app-server");
        using var process=Process.Start(start) ?? throw new InvalidOperationException("无法启动 Codex 额度接口");
        // Drain stderr without retaining account details or diagnostic secrets.
        var stderr=Task.Run(async()=> { try { while(await process.StandardError.ReadLineAsync(token) is not null) {} } catch(OperationCanceledException) {} catch(IOException) {} });
        try
        {
            await process.StandardInput.WriteLineAsync("{\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"lumenbar\",\"version\":\"0.1.0\"}}}");
            await Response(1);
            await process.StandardInput.WriteLineAsync("{\"method\":\"initialized\",\"params\":{}}");
            await process.StandardInput.WriteLineAsync("{\"id\":2,\"method\":\"account/rateLimits/read\",\"params\":{}}");
            return Parsers.Codex(await Response(2));
        }
        finally
        {
            if(!process.HasExited) process.Kill(true);
            timeout.Cancel();
            await stderr;
        }
        async Task<JsonElement> Response(int id)
        {
            while(true)
            {
                var line=await process.StandardOutput.ReadLineAsync(token);
                if(line is null) throw new InvalidOperationException("Codex 接口已退出，请确认已登录");
                using var doc=JsonDocument.Parse(line);
                var e=doc.RootElement;
                if(!e.TryGetProperty("id",out var n) || n.ValueKind!=JsonValueKind.Number || n.GetInt32()!=id) continue;
                if(e.TryGetProperty("error",out _)) throw new UnauthorizedAccessException("无法读取 Codex 额度，请在 Codex 中登录 ChatGPT 账户");
                return e.GetProperty("result").Clone();
            }
        }
    }
    public void Dispose()=>http.Dispose();
}

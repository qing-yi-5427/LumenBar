using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace LumenBar;

public record Metric(string Label, decimal Value, string Unit, double? RemainingPercent = null, DateTimeOffset? Reset = null, int? WindowMinutes = null, string? Bucket = null)
{
    public string Display => Unit == "%" ? $"{Value:0.#}%" : Unit switch { "CNY" => $"¥{Value:N2}", "USD" => $"${Value:N2}", _ => $"{Value:N2} {Unit}" };
    public string ResetText => Reset is null ? "" : Reset <= DateTimeOffset.Now ? "等待平台重置" : $"{Reset.Value.ToLocalTime():MM-dd HH:mm} 重置";
}
public record Reading(string Id, string Name, string Source, List<Metric> Metrics, DateTimeOffset? Updated = null, string? Error = null)
{
    public string Headline => Metrics.FirstOrDefault()?.Display ?? "—";
    public bool Stale => Error is not null && Updated is not null;
}
public record FloatingSize(int Width,int Height,int CapsuleCount);
public class Preferences
{
    public int SchemaVersion { get; set; } = 2;
    public string Theme { get; set; } = "System";
    public string DisplayStyle { get; set; } = "Capsule";
    public bool AlwaysOnTop { get; set; } = true;
    public string Monitor { get; set; } = "";
    public int RefreshSeconds { get; set; } = 120;
    public double GlassOpacity { get; set; } = 0.22;
    public bool WallpaperTint { get; set; } = true;
    public int FloatingWidthPixels { get; set; } = 192;
    public int? FloatingHeightPixels { get; set; }
    public FloatingSize? LastFloatingSize { get; set; }
    public bool Locked { get; set; }
    public double? FloatingLeft { get; set; }
    public double? FloatingTop { get; set; }
    public string CodexPath { get; set; } = "";
    public List<string> Enabled { get; set; } = new() { "codex", "deepseek", "mimo" };
    public Dictionary<string, string> ProtectedSecrets { get; set; } = new();
    public Dictionary<string, decimal> BalanceReferences { get; set; } = new();
}

public static class Parsers
{
    static JsonElement Required(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v : throw new FormatException($"平台未返回 {key}");
    public static decimal Number(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetDecimal(),
        JsonValueKind.String when decimal.TryParse(e.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) => n,
        _ => throw new FormatException("平台返回了无法识别的数值")
    };
    public static Reading DeepSeek(JsonElement e)
    {
        var list = Required(e, "balance_infos").EnumerateArray().Select(x => new Metric("API 可用余额", Number(Required(x, "total_balance")), Required(x, "currency").GetString()!)).ToList();
        if(list.Count == 0) throw new FormatException("平台未返回余额");
        return new("deepseek", "DeepSeek", "官方余额 API", list, DateTimeOffset.Now);
    }
    public static Reading OpenRouter(JsonElement e)
    {
        var d = Required(e, "data");
        return new("openrouter", "OpenRouter", "官方 Credits API", new() { new("账户可用余额", Number(Required(d,"total_credits")) - Number(Required(d,"total_usage")), "USD") }, DateTimeOffset.Now);
    }
    public static Reading MiMo(JsonElement e)
    {
        var code = Number(Required(e,"code"));
        if(code == 401 || code == 403) throw new UnauthorizedAccessException("MiMo 会话已过期，请更新 Cookie");
        if(code != 0) throw new FormatException($"MiMo 返回错误代码 {code}");
        var d = Required(e,"data");
        var currency = Required(d,"currency").GetString();
        if(string.IsNullOrWhiteSpace(currency)) throw new FormatException("MiMo 未提供币种");
        var list = new List<Metric> { new("账户可用余额",Number(Required(d,"balance")),currency) };
        foreach(var pair in new[] { ("cashBalance","充值余额"), ("giftBalance","赠送余额") })
            if(d.TryGetProperty(pair.Item1,out var n) && n.ValueKind != JsonValueKind.Null) list.Add(new(pair.Item2,Number(n),currency));
        return new("mimo","MiMo","MiMo 控制台会话",list,DateTimeOffset.Now);
    }
    public static Reading Codex(JsonElement e)
    {
        var list = new List<Metric>();
        if (e.TryGetProperty("rateLimitsByLimitId", out var byId) && byId.ValueKind == JsonValueKind.Object && byId.EnumerateObject().Any())
        {
            foreach (var bucket in byId.EnumerateObject().OrderBy(x=>x.Name == "codex" ? 0 : 1)) AddBucket(bucket.Value, bucket.Name == "codex" ? "" : bucket.Name + " · ");
        }
        else if (e.TryGetProperty("rateLimits", out var legacy) && legacy.ValueKind == JsonValueKind.Object) AddBucket(legacy, "");
        if (list.Count == 0) throw new FormatException("当前登录方式未提供 Codex 订阅额度");
        return new("codex", "Codex", "本地 Codex · 官方额度接口", list, DateTimeOffset.Now);
        void AddBucket(JsonElement bucket, string prefix)
        {
            foreach(var name in new[] { "primary", "secondary" })
            {
                if(!bucket.TryGetProperty(name,out var w) || w.ValueKind != JsonValueKind.Object) continue;
                if(!w.TryGetProperty("usedPercent",out var used) || used.ValueKind == JsonValueKind.Null) continue;
                var remain = Math.Clamp(100 - Number(used),0,100);
                var label = name == "primary" ? "当前周期剩余" : "长周期剩余";
                int? minutes=null;
                if(w.TryGetProperty("windowDurationMins",out var dur) && dur.ValueKind == JsonValueKind.Number)
                { var mins = dur.GetInt32(); minutes=mins; label = mins == 10080 ? "本周剩余" : mins % 60 == 0 ? $"{mins / 60} 小时剩余" : $"{mins} 分钟剩余"; }
                DateTimeOffset? reset = w.TryGetProperty("resetsAt",out var r) && r.ValueKind == JsonValueKind.Number ? DateTimeOffset.FromUnixTimeSeconds(r.GetInt64()) : null;
                list.Add(new(prefix + label, remain, "%", (double)remain, reset, minutes, prefix.Length==0?"codex":prefix));
            }
        }
    }
}

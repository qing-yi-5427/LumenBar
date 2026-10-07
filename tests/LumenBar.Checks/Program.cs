using System.Text.Json;
using LumenBar;

int passed=0;
void Check(string name,Action action){action();passed++;Console.WriteLine("PASS "+name);}
void Equal<T>(T actual,T expected){if(!EqualityComparer<T>.Default.Equals(actual,expected))throw new Exception($"Expected {expected}, got {actual}");}
void Reject(Action action){try{action();}catch(FormatException){return;}throw new Exception("Malformed data was accepted");}
JsonElement Json(string text)=>JsonDocument.Parse(text).RootElement;

Check("Codex prefers per-bucket results and keeps every bucket",()=>{
    var r=Parsers.Codex(Json("""{"rateLimits":{"primary":{"usedPercent":99}},"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":25,"windowDurationMins":300,"resetsAt":1800000000},"secondary":{"usedPercent":100,"windowDurationMins":10080}},"spark":{"primary":{"usedPercent":10}}}}"""));
    Equal(r.Metrics.Count,3);Equal(r.Metrics[0].Value,75m);Equal(r.Metrics[1].Value,0m);Equal(r.Metrics[2].Value,90m);Equal(r.Metrics[0].Label,"5 小时剩余");
});
Check("Missing Codex windows are unknown, never 100 percent",()=>Reject(()=>Parsers.Codex(Json("""{"rateLimits":{"primary":null,"secondary":null}}"""))));
Check("Null Codex used percent is unknown",()=>Reject(()=>Parsers.Codex(Json("""{"rateLimits":{"primary":{"usedPercent":null}}}"""))));
Check("Legacy Codex and clamping",()=>Equal(Parsers.Codex(Json("""{"rateLimits":{"primary":{"usedPercent":120}}}""")).Metrics[0].Value,0m));
Check("DeepSeek preserves separate currencies and decimal precision",()=>{
    var r=Parsers.DeepSeek(Json("""{"balance_infos":[{"currency":"CNY","total_balance":"0.01"},{"currency":"USD","total_balance":"20.50"}]}"""));Equal(r.Metrics.Count,2);Equal(r.Metrics[0].Value,0.01m);Equal(r.Metrics[1].Unit,"USD");
});
Check("Invalid balances fail instead of showing zero",()=>Reject(()=>Parsers.DeepSeek(Json("""{"balance_infos":[{"currency":"CNY","total_balance":"unknown"}]}"""))));
Check("MiMo does not add components to total twice",()=>{
    var r=Parsers.MiMo(Json("""{"code":0,"data":{"balance":"46.8","currency":"CNY","cashBalance":"30","giftBalance":"16.8"}}"""));Equal(r.Metrics[0].Value,46.8m);Equal(r.Metrics.Count,3);
});
Check("MiMo rejects missing currency",()=>Reject(()=>Parsers.MiMo(Json("""{"code":0,"data":{"balance":"0"}}"""))));
Check("MiMo expired sessions do not become zero balances",()=>{
    try{Parsers.MiMo(Json("""{"code":401}"""));}catch(UnauthorizedAccessException){return;}throw new Exception("Session error ignored");
});
Check("OpenRouter preserves negative balances",()=>Equal(Parsers.OpenRouter(Json("""{"data":{"total_credits":10,"total_usage":10.3}}""")).Metrics[0].Value,-0.3m));
Check("Cookie normalization retains only a single-line header",()=>Equal(Providers.NormalizeCookie("Cookie: api-platform_serviceToken=sample; userId=123"),"api-platform_serviceToken=sample; userId=123"));
Check("Cookie rejects injected header",()=>Reject(()=>Providers.NormalizeCookie("api-platform_serviceToken=sample; userId=123\r\nX-Test: 1")));
Check("DPAPI credentials survive round trip, never stored in plaintext",()=>{
    var secret="test-key-only-not-a-real-credential";var cipher=SettingsStore.Protect(secret);Equal(SettingsStore.Unprotect(cipher),secret);if(cipher.Contains(secret))throw new Exception("Plaintext persisted");
});
Check("Tall resize rejects large unused vertical space",()=>{var l=CompactLayout.Fit(128,900,3);if(l.Height>250)throw new Exception("Excess empty height");Equal(l.Columns*l.Rows,3);});
Check("Wide resize distributes all providers without empty cells",()=>{var l=CompactLayout.Fit(800,100,4);Equal(l.Columns,4);Equal(l.Rows,1);});
Check("Resize scales without requiring scroll at supported provider counts",()=>{
    foreach(int count in new[]{1,2,3,4,5})foreach(var size in new[]{(100d,48d),(128d,900d),(800d,80d),(450d,450d)})
    {var l=CompactLayout.Fit(size.Item1,size.Item2,count);Equal(l.Columns*l.Rows,count);double ratio=(l.Width/l.Columns)/(l.Height/l.Rows);if(ratio<2.59 || ratio>4.01)throw new Exception("Layout density out of bounds");}
});
Check("Weekly-only Codex never appears as a five-hour balance",()=>{
    var r=Parsers.Codex(Json("""{"rateLimits":{"primary":{"usedPercent":12,"windowDurationMins":10080}}}"""));Equal(CapsuleData.Quota(r,300),null);Equal(CapsuleData.Quota(r,10080)!.Value,88m);
});
Check("Codex capsule selects the primary account bucket, not Spark",()=>{
    var r=Parsers.Codex(Json("""{"rateLimitsByLimitId":{"spark":{"primary":{"usedPercent":20,"windowDurationMins":300}},"codex":{"primary":{"usedPercent":75,"windowDurationMins":300},"secondary":{"usedPercent":40,"windowDurationMins":10080}}}}"""));Equal(CapsuleData.Quota(r,300)!.Value,25m);Equal(CapsuleData.Quota(r,10080)!.Value,60m);
});
Check("Cash progress requires a reference in the matching currency",()=>{
    var r=new Reading("deepseek","DeepSeek","test",new(){new("balance",60,"CNY")});var p=new Preferences();Equal(CapsuleData.CashProgress(r,p),null);p.BalanceReferences["deepseek:USD"]=100;Equal(CapsuleData.CashProgress(r,p),null);p.BalanceReferences["deepseek:CNY"]=100;Equal(CapsuleData.CashProgress(r,p),.6);Equal(CapsuleData.CashProgress(r with {Error="stale"},p),null);
});
if(args.Contains("--live-codex"))
{
    var r=await Providers.ReadCodexAsync("",CancellationToken.None);
    Console.WriteLine("LIVE CODEX: "+string.Join("; ",r.Metrics.Select(m=>$"{m.Label} {m.Display}")));
}

Check("Five-hour capsule is omitted for missing, failed or unrelated quota",()=>{
    var weekly=new Reading("codex","Codex","test",new(){new("week",88,"%",88,null,10080,"codex")});
    Equal(CapsuleData.CodexWindows(weekly).Count(),1);
    var zero=weekly with {Metrics=new(){new("5h",0,"%",0,null,300,"codex"),weekly.Metrics[0]}};
    Equal(CapsuleData.CodexWindows(zero).Count(),2);Equal(CapsuleData.CodexWindows(zero).First().Minutes,300);
    Equal(CapsuleData.CodexWindows(zero with {Error="offline"}).Count(),1);
    Equal(CapsuleData.CodexWindows(weekly with {Metrics=new(){new("Spark",50,"%",50,null,300,"spark")}}).Count(),1);
});
Check("A click or brief press cannot start window movement",()=>{
    var drag=new HoldDrag();drag.Begin(100,100,40,50,1000,1.5);
    Equal(drag.Move(130,150,true,1100),null);Equal(drag.End(),false);
    Equal(drag.Move(180,190,true,1400),null);
});
Check("Holding and dragging uses screen offsets and stops at release",()=>{
    var drag=new HoldDrag();drag.Begin(100,100,-1600,200,1000,1.5);
    Equal(drag.Move(103,101,true,1300),null);
    Equal(drag.Move(120,125,true,1300),((int X,int Y)?)(-1580,225));
    Equal(drag.Move(180,150,true,1400),((int X,int Y)?)(-1520,250));
    Equal(drag.End(),true);Equal(drag.Move(200,200,true,1500),null);
});
Check("Lost button state and canceled capture never leave a sticky drag",()=>{
    var drag=new HoldDrag();drag.Begin(100,100,20,20,0,1);
    Equal(drag.Move(110,110,false,300),null);Equal(drag.Pressed,false);
    drag.Begin(100,100,20,20,500,1);drag.Move(110,110,true,800);drag.End();
    Equal(drag.Move(150,150,true,1000),null);
});
Check("A fresh settings store restores physical size and negative monitor coordinates",()=>{
    string directory=Path.Combine(Path.GetTempPath(),"LumenBar-position-"+Guid.NewGuid().ToString("N"));
    try{
        var p=new Preferences {FloatingLeft=-1560,FloatingTop=-420,FloatingWidthPixels=360,FloatingHeightPixels=280,LastFloatingSize=new(360,225,4)};
        new SettingsStore(directory).Save(p);var restored=new SettingsStore(directory).Load();
        Equal(restored.FloatingLeft,-1560d);Equal(restored.FloatingTop,-420d);Equal(restored.LastFloatingSize,p.LastFloatingSize);Equal(restored.FloatingHeightPixels,280);
        restored.FloatingLeft=-1200;new SettingsStore(directory).Save(restored);Equal(new SettingsStore(directory).Load().FloatingLeft,-1200d);
    }finally{Directory.Delete(directory,true);}
});
Console.WriteLine($"Total: {passed} checks passed.");

using System;
using System.Linq;
namespace LumenBar;
public static class CapsuleData
{
    // A weekly-only response must not be mistaken for the five-hour allowance.
    public static Metric? Quota(Reading reading,int minutes)=>reading.Error is null
        ? reading.Metrics.FirstOrDefault(m=>m.Unit=="%" && m.WindowMinutes==minutes && m.Bucket=="codex") : null;
    public static System.Collections.Generic.IEnumerable<(int Minutes,string Title)> CodexWindows(Reading reading)
    {
        if(Quota(reading,300) is not null)yield return (300,"Codex · 5h");
        yield return (10080,"Codex · 周");
    }
    public static string ReferenceKey(string id,string currency)=>id+":"+currency;
    public static double? CashProgress(Reading reading,Preferences settings)
    {
        var metric=reading.Metrics.FirstOrDefault();
        if(reading.Error is not null || metric is null || metric.Unit=="%")return null;
        return settings.BalanceReferences.TryGetValue(ReferenceKey(reading.Id,metric.Unit),out var reference) && reference>0
            ? metric.Value<=0?0:metric.Value>=reference?1:(double)(metric.Value/reference) : null;
    }
}

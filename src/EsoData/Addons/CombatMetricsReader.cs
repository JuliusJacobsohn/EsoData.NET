using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Addons;

/// <summary>Persisted fight metadata. Character names do not establish account/world identity.</summary>
public sealed record CombatFightSummary(long Id, string? CharacterName, string? Label, string? Zone,
    string? Subzone, DateTimeOffset? StartedAt, string? LocalTime, double? DamageDurationSeconds,
    double? HealingDurationSeconds, double? DamagePerSecond, double? IncomingDamagePerSecond,
    double? HealingPerSecond, double? IncomingHealingPerSecond, bool HasEncodedDetails, bool HasCombatLog);

public sealed record CombatMetricsReport(SourceInfo Source, long? FormatVersion,
    IReadOnlyList<CombatFightSummary> Fights, IReadOnlyList<string> Diagnostics);

/// <summary>Reads the plain summary fields CMX retains outside its compressed detail payload.</summary>
public static class CombatMetricsReader
{
    public static CombatMetricsReport Read(string path) => Parse(SavedVariables.Read(path),
        ReaderSupport.Source("CombatMetrics", path));

    public static CombatMetricsReport Parse(LuaTable document, SourceInfo? source = null)
    {
        var root = document.Table("CombatMetricsFightDataSV")
            ?? throw new FormatException("Combat Metrics saved-fight root is missing.");
        var fights = new List<CombatFightSummary>();
        var diagnostics = new List<string>();
        foreach (var entry in root.Where(e => e.Key.IsNumeric).OrderBy(e => LuaTable.AsInteger(e.Key.Value)))
        {
            if (LuaTable.AsInteger(entry.Key.Value) is not long id || id <= 0 || entry.Value is not LuaTable fight)
            {
                diagnostics.Add($"Unreadable saved fight at index {entry.Key.Value}.");
                continue;
            }
            var name = fight.Table("charData")?.String("name") ?? fight.String("char");
            if (name?.IndexOf('^') is int suffix && suffix >= 0) name = name[..suffix];
            var calculated = fight.Table("calculated");
            double? Metric(string key) => Number(calculated?[key]);
            fights.Add(new(id, name, fight.String("fightlabel"),
                fight.String("zone"), fight.String("subzone"), ReaderSupport.Time(fight.Integer("date")),
                fight.String("time"), Number(fight["dpstime"]), Number(fight["hpstime"]),
                Metric("DPSOut"), Metric("DPSIn"), Metric("HPSOut"), Metric("HPSIn"),
                fight["encodedStrings"] is not null, fight.Boolean("log") == true || fight["stringlog"] is LuaTable
                    || fight["log"] is LuaTable));
        }
        return new(source ?? new("CombatMetrics"), root.Integer("version"), fights, diagnostics);
    }

    private static double? Number(object? value) => value switch
    {
        long n => n,
        double n when double.IsFinite(n) => n,
        _ => null
    };
}

using System.Globalization;
using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Addons;

internal static class UespCharacterDetails
{
    internal static ResearchSummary? Research(LuaTable? data)
    {
        if (data is null) return null;
        var crafts = new List<CraftResearch>();
        var names = Keys(data).Where(x => x.Contains(':')).Select(x => x[..x.IndexOf(':')]).Distinct().Order();
        foreach (var craft in names)
        {
            var prefix = craft + ":Trait:";
            var lines = Keys(data).Where(x => x.StartsWith(prefix, StringComparison.Ordinal))
                .Select(x => x[prefix.Length..])
                .Where(x => x is not ("Known" or "Total"))
                .Select(x => StripSuffix(x, ":Known", ":Total", ":Unknown")).Distinct().Order()
                .Select(name => new ResearchLineSummary(name, ReaderSupport.Int(data, prefix + name + ":Known"),
                    ReaderSupport.Int(data, prefix + name + ":Total"), data.String(prefix + name),
                    data.String(prefix + name + ":Unknown"))).ToArray();
            var activePrefix = craft + ":Trait";
            var active = Keys(data).Where(x => x.StartsWith(activePrefix, StringComparison.Ordinal))
                .Select(x => int.TryParse(x[activePrefix.Length..], out var index) ? index : 0)
                .Where(x => x > 0).Distinct().Order()
                .Select(index => new ActiveResearch(index, data.String(activePrefix + index),
                    data.String(craft + ":Item" + index), Number(data[craft + ":Time" + index]))).ToArray();
            crafts.Add(new(craft, ReaderSupport.Int(data, prefix + "Known"), ReaderSupport.Int(data, prefix + "Total"),
                ReaderSupport.Int(data, craft + ":Open"), lines, active));
        }
        return new(ReaderSupport.Time(data.Integer("Timestamp")), crafts);
    }

    internal static ChampionState? Champion(LuaTable? data)
    {
        if (data is null) return null;
        var disciplines = Keys(data).Where(x => x.EndsWith(":Points", StringComparison.Ordinal) ||
                x.EndsWith(":Unspent", StringComparison.Ordinal))
            .Select(x => StripSuffix(x, ":Points", ":Unspent")).Where(x => x != "Total").Distinct().Order()
            .Select(name => new ChampionDiscipline(name, ReaderSupport.Int(data, name + ":Points"),
                ReaderSupport.Int(data, name + ":Unspent"))).ToArray();
        Dictionary<int, long>? slots = data.Table("Slots") is LuaTable slotData
            ? slotData.Where(x => int.TryParse(x.Key.Value, out _) && LuaTable.AsInteger(x.Value) is not null)
                .ToDictionary(x => int.Parse(x.Key.Value, CultureInfo.InvariantCulture), x => LuaTable.AsInteger(x.Value)!.Value)
            : null;
        var stars = new List<ChampionStar>();
        foreach (var row in data.Tables())
        {
            if (row.Value.Integer("skillId") is not long id || ReaderSupport.Int(row.Value, "points") is not int points) continue;
            stars.Add(new(id, row.Value.Integer("id"), row.Key.Value, points,
                ReaderSupport.Int(row.Value, "slot"), row.Value.String("desc")));
        }
        return new(ReaderSupport.Int(data, "Total:Spent"), ReaderSupport.Int(data, "Total:Unspent"),
            disciplines, slots, stars.OrderBy(x => x.SkillId).ToArray());
    }

    internal static IReadOnlyDictionary<string, int>? SkillLines(LuaTable? data) => data is null ? null
        : data.Where(x => !x.Key.IsNumeric && LuaTable.AsInteger(x.Value) is >= int.MinValue and <= int.MaxValue)
            .ToDictionary(x => x.Key.Value, x => (int)LuaTable.AsInteger(x.Value)!.Value);

    internal static CharacterStatistics? Statistics(LuaTable data)
    {
        var stats = data.Table("Stats");
        var advanced = data.Table("AdvancedStats");
        if (stats is null && advanced is null) return null;
        var current = new StatBuilder();
        var bars = new SortedDictionary<int, StatBuilder>();
        foreach (var entry in stats ?? [])
        {
            if (entry.Key.IsNumeric || Number(entry.Value) is not double value) continue;
            var name = entry.Key.Value;
            var computed = name.StartsWith("Computed:", StringComparison.Ordinal);
            if (computed) name = name[9..];
            var target = current;
            var colon = name.IndexOf(':');
            if (name.StartsWith("Bar", StringComparison.Ordinal) && colon > 3 && int.TryParse(name[3..colon], out var bar))
            {
                if (!bars.TryGetValue(bar, out target)) bars[bar] = target = new();
                name = name[(colon + 1)..];
            }
            (computed ? target.Computed : target.Values)[name] = value;
        }
        return new(ReaderSupport.Int(data, "ActiveWeaponBar"), ReaderSupport.Int(data, "ActiveAbilityBar"),
            stats is null ? null : current.Build(), bars.ToDictionary(x => x.Key, x => x.Value.Build()),
            advanced?.Tables().OrderBy(x => x.Key.Value).Select(x => new AdvancedStatistic(x.Key.Value,
                x.Value.Integer("statId"), x.Value.String("category"), ReaderSupport.Int(x.Value, "formatType"),
                Number(x.Value["flatValue"]), Number(x.Value["percentValue"]))).ToArray());
    }

    private sealed class StatBuilder
    {
        internal Dictionary<string, double> Values { get; } = [];
        internal Dictionary<string, double> Computed { get; } = [];
        internal StatisticValues Build() => new(Values, Computed);
    }
    private static IEnumerable<string> Keys(LuaTable data) => data.Where(x => !x.Key.IsNumeric).Select(x => x.Key.Value);
    private static string StripSuffix(string value, params string[] suffixes)
    {
        foreach (var suffix in suffixes)
            if (value.EndsWith(suffix, StringComparison.Ordinal)) return value[..^suffix.Length];
        return value;
    }
    private static double? Number(object? value) => value switch
    {
        long n => n,
        double n when double.IsFinite(n) => n,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) => n,
        _ => null
    };
}

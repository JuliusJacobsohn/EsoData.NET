using System.Globalization;
using System.Text.RegularExpressions;
using EsoData.Lua;

namespace EsoData.Pricing;

/// <summary>Reads TTC's downloaded tables as data; never executes their Lua function wrappers.</summary>
public static partial class TtcPriceReader
{
    public static PriceCatalog Read(string addonDirectory, string region = "EU", string language = "EN")
    {
        region = region.ToUpperInvariant(); language = language.ToUpperInvariant();
        if (region is not ("EU" or "NA")) throw new ArgumentException("Region must be EU or NA.", nameof(region));
        if (!Regex.IsMatch(language, "^[A-Z]{2}$", RegexOptions.CultureInvariant)) throw new ArgumentException("Use a two-letter language.", nameof(language));
        var path = Path.Combine(addonDirectory, $"PriceTable{region}.lua");
        var catalog = Parse(File.ReadAllText(path), File.ReadAllText(Path.Combine(addonDirectory, $"ItemLookUpTable_{language}.lua")), region, language);
        catalog.Source = catalog.Source with { Path = Path.GetFullPath(path), FileWrittenAt = File.GetLastWriteTimeUtc(path) };
        return catalog;
    }

    public static PriceCatalog Parse(string prices, string lookup, string region = "EU", string language = "EN")
    {
        region = region.ToUpperInvariant();
        if (region is not ("EU" or "NA")) throw new ArgumentException("Region must be EU or NA.", nameof(region));
        var table = Extract(prices, "PriceTable");
        var names = Extract(lookup, "ItemLookUpTable");
        var catalog = new PriceCatalog { Source = new(region, language.ToUpperInvariant(),
            UpdatedAt: table.Integer("TimeStamp") is long time ? DateTimeOffset.FromUnixTimeSeconds(time) : null) };
        foreach (var name in names.Tables())
        foreach (var id in name.Value)
            if (id.Key.IsNumeric && int.TryParse(id.Key.Value, out var type) && LuaTable.AsInteger(id.Value) is long number)
                catalog.Items.Add(new(number, name.Key.Value, type));
        var data = table.Table("Data") ?? throw new FormatException("TTC price table has no Data table.");
        foreach (var item in data.Tables())
        foreach (var quality in item.Value.Tables())
        foreach (var level in quality.Value.Tables())
        foreach (var trait in level.Value.Tables())
        {
            var key = new PriceKey(Number(item.Key), checked((int)Number(quality.Key)), checked((int)Number(level.Key)), checked((int)Number(trait.Key)), []);
            Visit(trait.Value, key);
        }
        return catalog;

        void Visit(LuaTable node, PriceKey key)
        {
            if (node["A"] is not null || node["Avg"] is not null)
            {
                catalog.Entries.Add(new(key, new(Decimal(node, "Avg", "A"), Decimal(node, "Min", "N"), Decimal(node, "Max", "X"),
                    Integer(node, "EntryCount", "EC"), Integer(node, "AmountCount", "AC"), Decimal(node, "SuggestedPrice", "S"),
                    Decimal(node, "SaleAvg", "SA"), Integer(node, "SaleEntryCount", "SE"), Integer(node, "SaleAmountCount", "SAC"))));
                return;
            }
            if (key.Extra.Length >= 8) throw new FormatException("Unexpected TTC variant depth.");
            foreach (var child in node.Tables()) Visit(child.Value, key with { Extra = [.. key.Extra, child.Key.Value] });
        }
    }
    private static long Number(LuaKey key) => key.IsNumeric && long.TryParse(key.Value, out var number)
        ? number : throw new FormatException("Expected numeric TTC variant key.");
    private static long? Integer(LuaTable node, string full, string shortName) => LuaTable.AsInteger(node[full] ?? node[shortName]);
    private static decimal? Decimal(LuaTable node, string full, string shortName) => (node[full] ?? node[shortName]) switch
    {
        long n => n, double n => checked((decimal)n), null => null,
        _ => throw new FormatException("Expected numeric TTC price.")
    };
    private static LuaTable Extract(string text, string field)
    {
        var match = Regex.Match(text, @"\bself\." + field + @"\s*=\s*(?=\{)", RegexOptions.CultureInvariant);
        if (!match.Success) throw new FormatException($"TTC {field} assignment is missing.");
        return SavedVariables.TablePrefix(text[(match.Index + match.Length)..]);
    }
}

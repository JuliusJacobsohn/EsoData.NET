using System.Globalization;
using System.Text.Json;

namespace EsoData.Catalogs;

/// <summary>Imports exportJson.php's minedItemSummary/minedSkills tables. HTTP and caching remain caller-owned.</summary>
public static class UespCatalog
{
    public static GameCatalog Parse(string json, string? sourceUrl = null, string? version = null)
    {
        using var document = JsonDocument.Parse(json);
        var catalog = new GameCatalog();
        catalog.Sources.Add(new("UESP exportJson", sourceUrl, version, DateTimeOffset.UtcNow));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("Expected a UESP export object.");
        var recognized = false;
        foreach (var table in root.EnumerateObject())
        {
            if (table.Value.ValueKind != JsonValueKind.Array) continue;
            if (table.Name.StartsWith("minedItemSummary", StringComparison.Ordinal))
            {
                recognized = true;
                foreach (var row in table.Value.EnumerateArray())
                {
                    var id = Integer(row, "itemId") ?? throw new FormatException("UESP itemId is missing.");
                    catalog.Items[id] = new(id, Text(row, "name"), Integer(row, "setId"), Int(row, "equipType"),
                        Int(row, "armorType"), Int(row, "weaponType"), Int(row, "trait"));
                }
            }
            else if (table.Name.StartsWith("minedSkills", StringComparison.Ordinal))
            {
                recognized = true;
                foreach (var row in table.Value.EnumerateArray())
                {
                    var id = Integer(row, "id") ?? throw new FormatException("UESP skill id is missing.");
                    catalog.Skills[id] = new(id, Text(row, "name"), Integer(row, "baseAbilityId"), Int(row, "rank"),
                        Int(row, "morph"), Integer(row, "isPassive") is long p ? p != 0 : null,
                        Integer(row, "craftedId"), Text(row, "skillLine"));
                }
            }
        }
        if (!recognized) throw new FormatException("No minedItemSummary or minedSkills tables found in the UESP export.");
        return catalog;
    }
    private static string? Text(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
    private static long? Integer(JsonElement row, string name) => long.TryParse(Text(row, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    private static int? Int(JsonElement row, string name) => Integer(row, name) is long n ? checked((int)n) : null;
}

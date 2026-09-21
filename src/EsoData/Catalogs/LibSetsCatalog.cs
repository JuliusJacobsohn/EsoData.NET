using System.Text.RegularExpressions;
using EsoData.Lua;

namespace EsoData.Catalogs;

/// <summary>Reads LibSets' literal set-name and compressed item-ID tables without executing addon code.</summary>
public static partial class LibSetsCatalog
{
    public static GameCatalog Read(string addonDirectory)
    {
        var data = Path.Combine(addonDirectory, "Data");
        var names = File.ReadAllText(Path.Combine(data, "LibSets_Data_SetNames.lua"));
        var items = File.ReadAllText(Path.Combine(data, "LibSets_Data_SetItemIds.lua"));
        return Parse(names, items, Path.GetFullPath(addonDirectory));
    }
    public static GameCatalog Parse(string setNamesLua, string setItemIdsLua, string? location = null)
    {
        var names = Extract(setNamesLua, "LIBSETS_TABLEKEY_SETNAMES");
        var ids = Extract(setItemIdsLua, "LIBSETS_TABLEKEY_SETITEMIDS");
        var catalog = new GameCatalog();
        var version = ApiVersion().Match(setNamesLua);
        catalog.Sources.Add(new("LibSets", location, version.Success ? version.Groups[1].Value : null, DateTimeOffset.UtcNow));
        var setIds = names.Select(x => x.Key).Concat(ids.Select(x => x.Key)).Distinct();
        foreach (var key in setIds)
        {
            if (!key.IsNumeric) continue;
            var setId = long.Parse(key.Value);
            var labels = (names.Get(key) as LuaTable ?? new()).Where(x => x.Value is string)
                .ToDictionary(x => x.Key.Value, x => (string)x.Value!);
            var items = new List<long>();
            foreach (var value in (ids.Get(key) as LuaTable)?.ArrayValues() ?? [])
            {
                if (value is long id) items.Add(id);
                else if (value is string range)
                {
                    var pieces = range.Split(',');
                    if (pieces.Length != 2 || !long.TryParse(pieces[0], out var start) || !int.TryParse(pieces[1], out var count) || start <= 0 || count < 0)
                        throw new FormatException($"Invalid LibSets range '{range}'.");
                    for (long offset = 0; offset <= count; offset++) items.Add(checked(start + offset));
                }
                else throw new FormatException("Unrecognized LibSets item ID entry.");
            }
            catalog.Sets[setId] = new(setId, labels, items);
            foreach (var id in items) catalog.Items[id] = new(id, SetId: setId);
        }
        return catalog;
    }
    private static LuaTable Extract(string source, string key)
    {
        var match = Regex.Match(source, @"setDataPreloaded\s*\[\s*" + key + @"\s*\]\s*=\s*(?=\{)");
        if (!match.Success) throw new FormatException($"Could not find the LibSets {key} literal table.");
        return SavedVariables.TablePrefix(source[(match.Index + match.Length)..]);
    }
    [GeneratedRegex(@"Last updated:\s*API\s+(\d+)")]
    private static partial Regex ApiVersion();
}

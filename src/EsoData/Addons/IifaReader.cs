using EsoData.Items;
using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Addons;

/// <summary>Reads IIfA's account/server DBv3 inventory and character directory.</summary>
public static class IifaReader
{
    public static AddonData Read(string path) => Parse(SavedVariables.Read(path), ReaderSupport.Source("IIfA", path));
    public static AddonData Parse(LuaTable document, SourceInfo? source = null)
    {
        var result = new AddonData { Source = source ?? new("IIfA"), Raw = document };
        var root = document.Table("IIFA_DATABASE");
        if (root is null) { result.Diagnostics.Add("IIFA_DATABASE is missing."); return result; }
        foreach (var account in root.Tables())
        foreach (var server in account.Value.Table("servers")?.Tables() ?? [])
        {
            var characters = server.Value.Table("CharIdToName");
            foreach (var pair in characters ?? new LuaTable())
                result.Characters.Add(new(server.Key.Value, account.Key.Value, pair.Key.Value, pair.Value as string));
            var groups = new Dictionary<string, List<ItemStack>>();
            foreach (var entry in server.Value.Table("DBv3")?.Tables() ?? [])
            {
                if (!ItemLink.TryParse(entry.Value.String("itemLink") ?? entry.Key.Value, out var link))
                {
                    result.Diagnostics.Add($"No usable item link for DBv3 entry {entry.Key.Value}.");
                    continue;
                }
                foreach (var location in entry.Value.Table("locations")?.Tables() ?? [])
                {
                    if (!groups.TryGetValue(location.Key.Value, out var items)) groups[location.Key.Value] = items = [];
                    foreach (var slot in location.Value.Table("bagSlot") ?? new LuaTable())
                    {
                        if (LuaTable.AsInteger(slot.Value) is not long count) continue;
                        items.Add(new(link!, count, location.Key.Value, ReaderSupport.Int(location.Value, "bagID"),
                            LuaTable.AsInteger(slot.Key.Value), entry.Value.String("itemName"), ReaderSupport.Int(entry.Value, "itemQuality")));
                    }
                }
            }
            foreach (var group in groups)
                result.Inventories.Add(new(server.Key.Value, account.Key.Value,
                    characters?[group.Key] is not null ? group.Key : null, null, group.Value));
        }
        return result;
    }
}

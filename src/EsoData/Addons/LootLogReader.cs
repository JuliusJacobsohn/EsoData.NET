using EsoData.Items;
using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Addons;

/// <summary>Installation-wide retained loot observations, not current ownership or a complete run log.</summary>
public sealed class LootHistory
{
    public SourceInfo Source { get; set; } = new("Loot Log");
    public List<LootEvent> Events { get; set; } = [];
    public List<string> Diagnostics { get; set; } = [];
}

public sealed record LootEvent(string Server, DateTimeOffset ReceivedAt, string Link, long Quantity,
    string RecipientAccount, string RecipientCharacter, long Flags, long? ItemId,
    bool Personal, bool Notable, bool SetItem);

/// <summary>Reads LootLog's hourly history buckets, including packed and legacy table entries.</summary>
public static class LootLogReader
{
    public static LootHistory Read(string path) => Parse(SavedVariables.Read(path), ReaderSupport.Source("Loot Log", path));

    public static LootHistory Parse(LuaTable root, SourceInfo? source = null)
    {
        var result = new LootHistory { Source = source ?? new("Loot Log") };
        if (root.Table("LootLogHistory") is not { } history)
        {
            result.Diagnostics.Add("LootLogHistory is missing; no retained history was observed.");
            return result;
        }
        foreach (var server in history.Tables())
            foreach (var bucket in server.Value.Tables())
                foreach (var entry in bucket.Value)
                {
                    object?[] fields = entry.Value switch
                    {
                        string packed => packed.Split(';', 6).Cast<object?>().ToArray(),
                        LuaTable legacy => Enumerable.Range(1, 6).Select(i => legacy[(long)i]).ToArray(),
                        _ => []
                    };
                    if (fields.Length != 6 || ReaderSupport.Time(LuaTable.AsInteger(fields[0])) is not { } time
                        || LuaTable.AsInteger(fields[2]) is not > 0 || LuaTable.AsInteger(fields[5]) is not > 0
                        || string.IsNullOrEmpty(ReaderSupport.Text(fields[1])))
                    {
                        result.Diagnostics.Add($"Skipped malformed loot entry {server.Key.Value}/{bucket.Key.Value}/{entry.Key.Value}.");
                        continue;
                    }
                    var link = ReaderSupport.Text(fields[1]);
                    var flags = LuaTable.AsInteger(fields[5])!.Value;
                    // The addon multiplies prime flags; these are not bit flags.
                    result.Events.Add(new(server.Key.Value, time, link, LuaTable.AsInteger(fields[2])!.Value,
                        ReaderSupport.Text(fields[3]), ReaderSupport.Text(fields[4]), flags,
                        ItemLink.TryParse(link, out var item) ? item!.ItemId : null,
                        flags % 2 == 0, flags % 3 == 0, flags % 5 == 0));
                }
        result.Events = result.Events.OrderByDescending(e => e.ReceivedAt).ToList();
        return result;
    }
}

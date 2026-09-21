using System.Numerics;
using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Addons;

public sealed record SetCollection(string Server, string Account, DateTimeOffset? ObservedAt,
    IReadOnlyDictionary<long, long> SetMasks)
{
    /// <summary>slotMask comes from collection-piece metadata, not a piece ordinal.</summary>
    public bool? IsCollected(long setId, long slotMask)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slotMask);
        return SetMasks.TryGetValue(setId, out var mask) ? (mask & slotMask) == slotMask : null;
    }
    public int? CollectedSlotCount(long setId) => SetMasks.TryGetValue(setId, out var mask)
        ? BitOperations.PopCount((ulong)mask) : null;
}

public sealed record SetCollectionData(SourceInfo Source, IReadOnlyList<SetCollection> Accounts, LuaTable Raw);

/// <summary>Reads current and legacy LibMultiAccountSets collections.</summary>
public static class SetCollectionReader
{
    public static SetCollectionData Read(string path) => Parse(SavedVariables.Read(path), ReaderSupport.Source("LibMultiAccountSets", path));
    public static SetCollectionData Parse(LuaTable document, SourceInfo? source = null)
    {
        var accounts = new List<SetCollection>();
        var current = document.Table("LibMultiAccountSetsData2");
        var root = current ?? document.Table("LibMultiAccountSetsData")
            ?? throw new FormatException("No LibMultiAccountSets collection data found.");
        foreach (var server in root.Tables())
        foreach (var account in server.Value)
        {
            var masks = new Dictionary<long, long>();
            long? timestamp;
            if (current is not null)
            {
                var encoded = CodesEncoding.JoinChunks(account.Value) ?? "";
                if (encoded.Length < 6 || encoded.Length % 6 != 0) throw new FormatException("Incomplete set collection field.");
                timestamp = CodesEncoding.DecodeInteger(encoded.AsSpan(0, 6));
                for (var i = 6; i < encoded.Length; i += 6)
                    masks[i / 6] = CodesEncoding.DecodeInteger(encoded.AsSpan(i, 6));
            }
            else
            {
                if (account.Value is not LuaTable legacy) continue;
                timestamp = legacy.Integer("timestamp");
                foreach (var entry in legacy.Where(x => x.Key.IsNumeric))
                    if (LuaTable.AsInteger(entry.Value) is long n) masks[long.Parse(entry.Key.Value)] = n;
            }
            accounts.Add(new(server.Key.Value, account.Key.Value, ReaderSupport.Time(timestamp), masks));
        }
        return new(source ?? new("LibMultiAccountSets"), accounts, document);
    }
}

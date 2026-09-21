using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Addons;

public sealed record KnowledgeEntry(long ItemId, bool? Known, int Index);
public sealed record ResearchTimer(int TraitIndex, TimeSpan Duration, TimeSpan RemainingAtScan)
{
    public TimeSpan Remaining(DateTimeOffset scannedAt, DateTimeOffset now) => RemainingAtScan - (now - scannedAt);
}
public sealed record ResearchKnowledge(IReadOnlyList<bool?> Traits, IReadOnlyDictionary<string, int> MaxSlots,
    IReadOnlyList<ResearchTimer> Timers, string? Signature);
public sealed class CharacterKnowledge
{
    public required CharacterReference Character { get; init; }
    public DateTimeOffset? ObservedAt { get; init; }
    public Dictionary<string, IReadOnlyList<KnowledgeEntry>> Categories { get; } = [];
    public ResearchKnowledge? Research { get; internal set; }
    public required LuaTable Raw { get; init; }
}
public sealed record CharacterKnowledgeData(SourceInfo Source, IReadOnlyList<CharacterKnowledge> Characters,
    LuaTable Raw, IReadOnlyList<string> Diagnostics);

/// <summary>Decodes LCK using the master lists saved with those same character bitfields.</summary>
public static class CharacterKnowledgeReader
{
    public static CharacterKnowledgeData Read(string path) => Parse(SavedVariables.Read(path), ReaderSupport.Source("LibCharacterKnowledge", path));
    public static CharacterKnowledgeData Parse(LuaTable document, SourceInfo? source = null)
    {
        var root = document.Table("LibCharacterKnowledgeData") ?? throw new FormatException("LibCharacterKnowledgeData is missing.");
        var master = root.Table("masterList") ?? throw new FormatException("LCK masterList is missing.");
        var width = ReaderSupport.Int(master, "fieldSize") ?? throw new FormatException("LCK fieldSize is missing.");
        if (width <= 0) throw new FormatException("LCK fieldSize must be positive.");
        var definitions = new Dictionary<string, long[]>();
        foreach (var category in new[] { "recipes", "plans", "motifs", "grimoires", "scripts" })
        {
            var encoded = CodesEncoding.JoinChunks(master[category]);
            if (encoded is null) continue;
            if (encoded.Length % width != 0) throw new FormatException($"Incomplete LCK {category} master list.");
            definitions[category] = Enumerable.Range(0, encoded.Length / width)
                .Select(i => CodesEncoding.DecodeInteger(encoded.AsSpan(i * width, width))).ToArray();
        }
        var characters = new List<CharacterKnowledge>();
        var diagnostics = new List<string>();
        foreach (var server in root.Table("characters")?.Tables() ?? [])
        foreach (var entry in server.Value.Tables())
        {
            var character = new CharacterKnowledge
            {
                Character = new(server.Key.Value, entry.Value.String("account") ?? "", entry.Key.Value, entry.Value.String("name")),
                ObservedAt = ReaderSupport.Time(entry.Value.Integer("timestamp")), Raw = entry.Value
            };
            foreach (var category in new[] { "recipes", "plans", "motifs" })
                ReadCategory(category, CodesEncoding.JoinChunks(entry.Value[category]));
            if (entry.Value.String("sc")?.Split(':') is { Length: 2 } scribing)
            {
                ReadCategory("grimoires", scribing[0]);
                ReadCategory("scripts", scribing[1]);
            }
            if (entry.Value.String("rt") is string rt)
            {
                if (root.Table("diagnostics")?.Integer("researchTraits") is long count)
                    character.Research = Research(rt, checked((int)count), root.Table("diagnostics")?.String("researchSignature"));
                else diagnostics.Add($"Research trait count is missing for character {entry.Key.Value}; raw rt retained.");
            }
            characters.Add(character);

            void ReadCategory(string category, string? bits)
            {
                if (bits is null || !definitions.TryGetValue(category, out var ids)) return;
                var values = ids.Select((id, index) => new KnowledgeEntry(id, CodesEncoding.ReadBit(bits, index + 1), index + 1)).ToArray();
                character.Categories[category] = values;
                if (bits.Length * 6 < ids.Length) diagnostics.Add($"Truncated {category} bitfield for character {entry.Key.Value}; missing bits are null.");
            }
        }
        return new((source ?? new("LibCharacterKnowledge")) with { ApiVersion = master.Integer("api") }, characters, document, diagnostics);
    }
    private static ResearchKnowledge Research(string data, int count, string? signature)
    {
        if (count < 0) throw new FormatException("Invalid research trait count.");
        var bytes = checked((count + 35) / 36 * 6);
        if (data.Length < bytes + 1 || (data.Length - bytes - 1) % 10 != 0)
            throw new FormatException("Incomplete LCK research data.");
        var traits = Enumerable.Range(1, count).Select(i => CodesEncoding.ReadBit(data[..bytes], i)).ToArray();
        var packed = CodesEncoding.DecodeInteger(data.AsSpan(bytes, 1));
        var slots = new Dictionary<string, int>
        {
            ["Blacksmithing"] = (int)(packed & 3) + 1, ["Clothing"] = (int)((packed >> 2) & 3) + 1,
            ["Woodworking"] = (int)((packed >> 4) & 3) + 1, ["Jewelry"] = 1
        };
        var timers = new List<ResearchTimer>();
        for (var i = bytes + 1; i < data.Length; i += 10)
            timers.Add(new((int)CodesEncoding.DecodeInteger(data.AsSpan(i, 2)),
                TimeSpan.FromSeconds(CodesEncoding.DecodeInteger(data.AsSpan(i + 2, 4))),
                TimeSpan.FromSeconds(CodesEncoding.DecodeInteger(data.AsSpan(i + 6, 4)))));
        return new(traits, slots, timers, signature);
    }
}

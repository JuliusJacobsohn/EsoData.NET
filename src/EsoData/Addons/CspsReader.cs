using EsoData.Formats;
using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Addons;

public sealed record CspsProfile(CharacterReference Character, string ProfileId, string? Name,
    DateTimeOffset? SavedAt, CspsBuild Build, string? EquipmentUniqueIds, LuaTable Raw);
public sealed record CspsData(SourceInfo Source, IReadOnlyList<CspsProfile> Profiles, LuaTable Raw);

/// <summary>Saved plans, not proof that their allocations were applied in-game.</summary>
public static class CspsReader
{
    public static CspsData Read(string path) => Parse(SavedVariables.Read(path), ReaderSupport.Source("CarosSkillPointSaver", path));
    public static CspsData Parse(LuaTable document, SourceInfo? source = null)
    {
        var root = document.Table("CSPSSavedVariables") ?? throw new FormatException("CSPSSavedVariables is missing.");
        var profiles = new List<CspsProfile>();
        foreach (var world in root.Tables())
        foreach (var account in world.Value.Tables())
        foreach (var character in account.Value.Table("$AccountWide")?.Table("charData")?.Tables() ?? [])
        {
            var reference = new CharacterReference(world.Key.Value, account.Key.Value, character.Key.Value,
                character.Value.String("$lastCharacterName"));
            if (character.Value.Table("werte") is not null || character.Value.String("comp1") is not null)
                profiles.Add(ReadProfile(reference, "0", character.Value));
            foreach (var profile in character.Value.Table("profiles")?.Tables() ?? [])
                profiles.Add(ReadProfile(reference, profile.Key.Value, profile.Value));
            if (character.Value.Table("auxProfile") is LuaTable auxiliary)
                profiles.Add(ReadProfile(reference, "aux", auxiliary));
        }
        return new(source ?? new("CarosSkillPointSaver"), profiles, document);
    }
    private static CspsProfile ReadProfile(CharacterReference character, string id, LuaTable profile)
    {
        var skills = profile.Table("werte");
        var active = Chunks(skills?["prog"]);
        var passive = Chunks(skills?["pass"]);
        var extended = skills?.String("scribeStyleSubclass") ?? string.Join('*', new[] { "crafted", "styles", "subclasses" }
            .Select(key => skills?.String(key) ?? "-"));
        var first = profile.String("comp1")?.Split('#');
        var second = profile.String("comp2")?.Split('#');
        string F(int index, string legacy) => first is null ? ReaderSupport.Text(profile[legacy]) : BuildText.Field(first, index);
        string S(int index, string legacy) => second is null ? ReaderSupport.Text(profile[legacy]) : BuildText.Field(second, index);
        var build = CspsBuild.Parse(string.Join('#', new[]
        {
            skills is null ? "-" : $"{active}*{passive}*{extended}", F(1, "hbwerte"), F(0, "attribute"), F(6, "mundus"),
            F(2, "cp2werte") + "*" + F(3, "cp2hbwerte"), S(0, "gearComp"), F(4, "qs"), S(2, "outfitComp"), F(5, "role")
        }));
        return new(character, id, profile.String("name"), ReaderSupport.Time(profile.Integer("lastSaved")), build, S(1, "gearCompUnique"), profile);
    }
    private static string Chunks(object? value)
    {
        if (value is string text) return text;
        if (value is not LuaTable table) return "-";
        return string.Join(',', table.Where(x => x.Key.Value.StartsWith("part", StringComparison.Ordinal) &&
                int.TryParse(x.Key.Value.AsSpan(4), out _)).OrderBy(x => int.Parse(x.Key.Value.AsSpan(4)))
            .Select(x => x.Value as string).Where(x => !string.IsNullOrEmpty(x)));
    }
}

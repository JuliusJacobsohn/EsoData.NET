using System.Globalization;

namespace EsoData.Formats;

public sealed record ActiveSkill(long AbilityId, int Morph);
public sealed record PassiveSkill(long AbilityId, int Rank);
public sealed record CraftedSkill(long CraftedAbilityId, long Script1, long Script2, long Script3);
public sealed record SkillStyle(long AbilityId, long CollectibleId);
public sealed record CspsSkills(IReadOnlyList<ActiveSkill> Active, IReadOnlyList<PassiveSkill> Passive,
    IReadOnlyList<CraftedSkill>? Crafted = null, IReadOnlyList<SkillStyle>? Styles = null, IReadOnlyList<long>? Subclasses = null);
public sealed record BarSlot(long AbilityId, bool IsCrafted = false);
public sealed record ChampionStar(long Id, int? Points = null);
public sealed record CspsChampionPoints(IReadOnlyList<ChampionStar> Allocations, IReadOnlyList<IReadOnlyList<long?>> Bars);

/// <summary>CSPS gear uses enchantment effect IDs, not glyph item IDs. Slots are positional.</summary>
public sealed record CspsGearSlot(long SetId = 0, int Type = 0, int Trait = 0, int Quality = 0,
    long EnchantmentEffectId = 0, long? PoisonItemId = null, long PoisonVariant = 0, bool IsMara = false);

/// <summary>Hub crafted slots use a representative ability ID, not a native CSPS crafted ID.</summary>
public sealed record HubBarSlot(long AbilityId, IReadOnlyList<long>? Scripts = null);
public sealed record HubGearSlot(int EquipSlot, int Type = 0, long SetId = 0, int Trait = 0,
    long GlyphItemId = 0, long? PoisonItemId = null, long? PoisonVariant = null);
public sealed record Consumable(long ItemId, long VariantId);

internal static class BuildText
{
    public static bool Empty(string s) => s is "" or "-";
    public static long Number(string s) => long.Parse(s, CultureInfo.InvariantCulture);
    public static int Int(string s) => checked((int)Number(s));
    public static string N(long n) => n.ToString(CultureInfo.InvariantCulture);
    public static string Join(IEnumerable<string> values, char separator = ',') => string.Join(separator, values);
    public static string[] Entries(string text, char separator = ',') => Empty(text) || text == "0" ? [] : text.Split(separator);
    public static string Field(string[] fields, int i, string fallback = "-") => i < fields.Length ? fields[i] : fallback;
    public static long[] Numbers(string text) => Entries(text).Select(Number).ToArray();
    public static string Numbers(IEnumerable<long>? values) => values is null ? "-" : Join(values.Select(N));
    public static string[] Parts(string entry, char separator, int count)
    {
        var parts = entry.Split(separator);
        if (parts.Length != count) throw new FormatException($"Expected {count} fields in '{entry}'.");
        return parts;
    }
}

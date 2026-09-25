namespace EsoData.Builds;

/// <summary>A guide's intended setup, independent of ownership, numeric catalog coverage and an executable allocation.</summary>
public sealed class GuideSetup
{
    public string Variant { get; set; } = "";
    public string? GameUpdate { get; set; }
    public string? Class { get; set; }
    public bool PureClass { get; set; }
    public string? PreferredRace { get; set; }
    public List<string> Masteries { get; set; } = [];
    public List<GuideAbility> Abilities { get; set; } = [];
    public List<GuidePassive> Passives { get; set; } = [];
    public List<GuideEquipment> Equipment { get; set; } = [];
    public List<GuideChampion> Champion { get; set; } = [];
    public BuildAttributes? Attributes { get; set; }
    public string? AttributeCondition { get; set; }
    public string? Mundus { get; set; }
    public string? Food { get; set; }
    public List<GuidePotion> Potions { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}
public sealed record GuideAbility(string Slot, string Name, string? BaseSkill = null,
    int? TargetRank = null, GuideScripts? Scripts = null);
public sealed record GuideScripts(string Focus, string Signature, string Affix);
/// <summary>A null Rank means maximum rank, whose numeric value must be resolved from a catalog, not guessed.</summary>
public sealed record GuidePassive(string SkillLine, string Name, string Priority, int? Rank = null,
    bool Excluded = false, string? Condition = null);
public sealed record GuideEquipment(int Slot, string Set, string Type, string[] Traits, string Enchantment,
    int? Quality = null, string? Note = null);
/// <summary>Null Points means the guide specifies the star but no exact allocation.</summary>
public sealed record GuideChampion(string Discipline, string Name, int? Points = null, string? Condition = null);
public sealed record GuidePotion(string Name, string[] Effects, string[] Ingredients);
public sealed record GuideFinding(string Path, string Desired, string Status, string? Observed = null, string? Detail = null);

public static class GuideSetupValidation
{
    public static void Validate(GuideSetup target)
    {
        if (string.IsNullOrWhiteSpace(target.Variant)) throw new ArgumentException("Guide variant is required.");
        var slots = Enumerable.Range(1, 6).SelectMany(i => new[] { $"front.{i}", $"back.{i}" }).ToHashSet();
        if (target.Abilities.Any(a => !slots.Contains(a.Slot) || string.IsNullOrWhiteSpace(a.Name) || a.TargetRank is < 1 or > 4)
            || target.Abilities.Select(a => a.Slot).Distinct().Count() != target.Abilities.Count)
            throw new ArgumentException("Guide abilities require distinct front.1..6/back.1..6 slots and valid ranks.");
        if (target.Equipment.Select(e => e.Slot).Distinct().Count() != target.Equipment.Count
            || target.Equipment.Any(e => e.Slot is not (0 or 1 or 2 or 3 or 4 or 5 or 6 or 8 or 9 or 10 or 11 or 12 or 16 or 20 or 21)
                || string.IsNullOrWhiteSpace(e.Set) || e.Quality is < 1 or > 5))
            throw new ArgumentException("Guide equipment has duplicate/unsupported slots or invalid values.");
        if (target.Champion.Any(c => c.Points < 0 || c.Discipline is not ("Craft" or "Warfare" or "Fitness")))
            throw new ArgumentException("Invalid guide champion selection.");
    }
}

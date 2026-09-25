using System.Text.Json.Serialization;
using EsoData.Accounts;
using EsoData.Formats;

namespace EsoData.Builds;

[Flags]
public enum BuildSections { None = 0, Skills = 1, Bars = 2, Attributes = 4, ChampionPoints = 8, Equipment = 16, Mundus = 32, All = 63 }
public sealed class CharacterBuild
{
    public BuildSections Sections { get; set; }
    public Dictionary<long, SkillPurchase> Skills { get; set; } = [];
    public AbilityBars Bars { get; set; } = new();
    public BuildAttributes Attributes { get; set; } = new();
    public Dictionary<long, int> ChampionPoints { get; set; } = [];
    public long?[] ChampionSlots { get; set; } = new long?[12];
    public Dictionary<int, EquipmentChoice> Equipment { get; set; } = [];
    public List<CraftedSkill> ScribedSkills { get; set; } = [];
    public long? Mundus { get; set; }
    public long[]? RuntimeClassLines { get; set; }
    public List<SkillStyle> SkillStyles { get; set; } = [];
    public string? NativeTemplate { get; set; }
    public CharacterBuild DeepClone() => AccountJson.Clone(this);
    [JsonIgnore] public int SkillPointCost => Skills.Values.Sum(s => s.IsPassive ? s.Rank : 1 + (s.Morph > 0 ? 1 : 0));
}
public sealed class SkillPurchase
{
    public int Rank { get; set; } = 1;
    public int Morph { get; set; }
    public bool IsPassive { get; set; }
}
public sealed class AbilityBars
{
    public long?[] Front { get; set; } = new long?[6];
    public long?[] Back { get; set; } = new long?[6];
    public long?[]? Overload { get; set; }
}
public sealed class BuildAttributes
{
    public int Health { get; set; }
    public int Magicka { get; set; }
    public int Stamina { get; set; }
}
public sealed class EquipmentChoice
{
    public string? OwnedReference { get; set; }
    public string? Link { get; set; }
    public long? ItemId { get; set; }
    public long? SetId { get; set; }
    public int? Type { get; set; }
    public int? Trait { get; set; }
    public int? Quality { get; set; }
    public long? EnchantmentEffectId { get; set; }
}
public sealed class BuildPlan
{
    public GuideSetup? Target { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AccountKey { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public int Revision { get; set; }
    public CharacterBuild Build { get; set; } = new();
    public CharacterBuild Baseline { get; set; } = new();
    public List<GuideReference> Guides { get; set; } = [];
    public BuildConstraints Constraints { get; set; } = new();
    public List<BuildRequirement> Requirements { get; set; } = [];
    public List<CraftingOrder> Crafting { get; set; } = [];
}
public sealed record GuideReference(string Url, DateTimeOffset RetrievedAt, string? Variant = null);
public sealed class BuildConstraints
{
    public bool FullRespec { get; set; } = true;
    public bool RequireFullBars { get; set; }
    public bool AvoidMaxedMorphs { get; set; }
    public int ReserveSkillPoints { get; set; }
    public List<long> CoreLevelingSkills { get; set; } = [];
    public List<long> OptionalLevelingSkills { get; set; } = [];
}
public enum RequirementKind { Skill, SkillLine, Knowledge, Item, Allocation, Manual }
public sealed class BuildRequirement
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public RequirementKind Kind { get; set; }
    public long? GameId { get; set; }
    public string? Category { get; set; }
    public int Amount { get; set; } = 1;
    public bool Optional { get; set; }
    public int Priority { get; set; }
    public List<string> DependsOn { get; set; } = [];
    public bool? Completed { get; set; }
    public string? Evidence { get; set; }
}
public sealed class CraftingOrder
{
    public long ItemId { get; set; }
    public int Level { get; set; } = 50;
    public int ChampionPoints { get; set; } = 160;
    public int Quality { get; set; } = 4;
    public int StyleId { get; set; } = 1;
    public int Quantity { get; set; } = 1;
    public long EnchantmentItemId { get; set; }
    public int? EnchantmentQuality { get; set; }
}

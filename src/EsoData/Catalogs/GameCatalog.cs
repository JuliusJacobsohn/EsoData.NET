using System.Text.Json;
using System.Text.Json.Serialization;
using EsoData.Formats;

namespace EsoData.Catalogs;

public sealed record CatalogSource(string Name, string? Location = null, string? Version = null, DateTimeOffset? ReadAt = null);
public sealed record ItemDefinition(long Id, string? Name = null, long? SetId = null, int? EquipType = null,
    int? ArmorType = null, int? WeaponType = null, int? Trait = null);
/// <summary>Describes one crafted result using data carried by an external item catalog.</summary>
public sealed record CraftedItemSelector(long SetId, int? EquipType = null, int? ArmorType = null,
    int? WeaponType = null, int? Trait = null)
{
    /// <summary>Finds exactly one item ID, or explains which additional field is needed.</summary>
    public ItemDefinition Resolve(GameCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (SetId <= 0) throw new ArgumentOutOfRangeException(nameof(SetId));
        var matches = catalog.Items.Values.Where(item => item.SetId == SetId
            && (EquipType is null || item.EquipType == EquipType)
            && (ArmorType is null || item.ArmorType == ArmorType)
            && (WeaponType is null || item.WeaponType == WeaponType)
            && (Trait is null || item.Trait == Trait)).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new KeyNotFoundException("No matching item definition. Import metadata with equipment and trait fields."),
            _ => throw new InvalidOperationException($"{matches.Length} item definitions match. Specify the remaining equipment, armor, weapon or trait fields.")
        };
    }
}
public sealed record SkillDefinition(long Id, string? Name = null, long? BaseAbilityId = null,
    int? Rank = null, int? Morph = null, bool? IsPassive = null, long? CraftedId = null, string? SkillLine = null);
public sealed record SkillLineDefinition(long Id, string Name, string? FullName = null, string? ClassType = null);
public sealed record SetDefinition(long Id, IReadOnlyDictionary<string, string> Names, IReadOnlyList<long> ItemIds);
public sealed record CollectionPiece(long SetId, long PieceId, long SlotMask);
public sealed record ResearchTrait(int Index, int CraftingType, int LineIndex, int TraitIndex, int TraitType, string? Name = null);

/// <summary>A caller-owned, refreshable JSON catalog. No game ID database is compiled into the package.</summary>
public sealed class GameCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
        WriteIndented = true, NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
    public List<CatalogSource> Sources { get; set; } = [];
    public Dictionary<long, ItemDefinition> Items { get; set; } = [];
    public Dictionary<long, SkillDefinition> Skills { get; set; } = [];
    public Dictionary<long, SkillLineDefinition> SkillLines { get; set; } = [];
    public Dictionary<long, SetDefinition> Sets { get; set; } = [];
    public List<CollectionPiece> CollectionPieces { get; set; } = [];
    public List<ResearchTrait> ResearchTraits { get; set; } = [];
    public string? ResearchSignature { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; set; }
    public static GameCatalog Read(string path) => FromJson(File.ReadAllText(path));
    public static GameCatalog FromJson(string json) => JsonSerializer.Deserialize<GameCatalog>(json, JsonOptions)
        ?? throw new FormatException("Catalog must be a JSON object.");
    /// <summary>
    /// Combines refreshable catalogs in order. Later non-null item/skill fields win, so load installed
    /// membership data first and external descriptive metadata afterwards.
    /// </summary>
    public static GameCatalog Merge(IEnumerable<GameCatalog> catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        var merged = new GameCatalog();
        foreach (var catalog in catalogs)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            merged.Sources.AddRange(catalog.Sources);
            foreach (var set in catalog.Sets.Values)
                merged.Sets[set.Id] = merged.Sets.TryGetValue(set.Id, out var existing)
                    ? Merge(existing, set)
                    : set;
            foreach (var item in catalog.Items.Values)
                merged.Items[item.Id] = merged.Items.TryGetValue(item.Id, out var existing) ? Merge(existing, item) : item;
            foreach (var skill in catalog.Skills.Values)
                merged.Skills[skill.Id] = merged.Skills.TryGetValue(skill.Id, out var existing) ? Merge(existing, skill) : skill;
            foreach (var line in catalog.SkillLines.Values) merged.SkillLines[line.Id] = line;
            merged.CollectionPieces.AddRange(catalog.CollectionPieces.Where(x => !merged.CollectionPieces.Contains(x)));
            merged.ResearchTraits.AddRange(catalog.ResearchTraits.Where(x => !merged.ResearchTraits.Contains(x)));
            merged.ResearchSignature = catalog.ResearchSignature ?? merged.ResearchSignature;
        }
        return merged;
    }
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
    public void Write(string path) => File.WriteAllText(path, ToJson());
    public IEnumerable<SetDefinition> FindSets(string name, string language = "en") => Sets.Values
        .Where(s => s.Names.TryGetValue(language, out var text) && text.Contains(name, StringComparison.OrdinalIgnoreCase));
    public IEnumerable<ItemDefinition> FindItems(long setId, int? equipType = null, int? trait = null) => Items.Values
        .Where(x => x.SetId == setId && (equipType is null || x.EquipType == equipType) && (trait is null || x.Trait == trait));
    public IEnumerable<SkillDefinition> FindSkills(string name) => Skills.Values
        .Where(s => s.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>Resolves selected morph rank one for CSPS's active list. Requires appropriate skill metadata.</summary>
    public ActiveSkill ToActiveSkill(long abilityId)
    {
        var skill = GetSkill(abilityId);
        if (skill.IsPassive != false || skill.Morph is null || skill.BaseAbilityId is null)
            throw new InvalidOperationException("Active/passive, morph and base ability metadata are required.");
        var rankOne = skill.Rank == 1 ? skill : Skills.Values.SingleOrDefault(x =>
            x.BaseAbilityId == skill.BaseAbilityId && x.Morph == skill.Morph && x.Rank == 1);
        return new(rankOne?.Id ?? throw new InvalidOperationException("Selected morph rank-one ID is missing."), skill.Morph.Value);
    }
    public BarSlot ToBarSlot(long abilityId)
    {
        var skill = GetSkill(abilityId);
        if (skill.CraftedId is > 0) return new(skill.CraftedId.Value, true);
        if (skill.BaseAbilityId is not > 0) throw new InvalidOperationException("Unmorphed base ability metadata is missing.");
        return new(skill.BaseAbilityId.Value);
    }
    private SkillDefinition GetSkill(long id) => Skills.TryGetValue(id, out var skill) ? skill
        : throw new KeyNotFoundException($"Ability {id} is not in this catalog.");
    private static ItemDefinition Merge(ItemDefinition existing, ItemDefinition later) => new(later.Id,
        later.Name ?? existing.Name, later.SetId ?? existing.SetId, later.EquipType ?? existing.EquipType,
        later.ArmorType ?? existing.ArmorType, later.WeaponType ?? existing.WeaponType, later.Trait ?? existing.Trait);
    private static SetDefinition Merge(SetDefinition existing, SetDefinition later)
    {
        var names = new Dictionary<string, string>(existing.Names);
        foreach (var pair in later.Names) names[pair.Key] = pair.Value;
        return new(later.Id, names, existing.ItemIds.Concat(later.ItemIds).Distinct().Order().ToArray());
    }
    private static SkillDefinition Merge(SkillDefinition existing, SkillDefinition later) => new(later.Id,
        later.Name ?? existing.Name, later.BaseAbilityId ?? existing.BaseAbilityId, later.Rank ?? existing.Rank,
        later.Morph ?? existing.Morph, later.IsPassive ?? existing.IsPassive, later.CraftedId ?? existing.CraftedId,
        later.SkillLine ?? existing.SkillLine);
}

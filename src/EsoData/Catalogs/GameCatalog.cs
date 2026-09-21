using System.Text.Json;
using System.Text.Json.Serialization;
using EsoData.Formats;

namespace EsoData.Catalogs;

public sealed record CatalogSource(string Name, string? Location = null, string? Version = null, DateTimeOffset? ReadAt = null);
public sealed record ItemDefinition(long Id, string? Name = null, long? SetId = null, int? EquipType = null,
    int? ArmorType = null, int? WeaponType = null, int? Trait = null);
public sealed record SkillDefinition(long Id, string? Name = null, long? BaseAbilityId = null,
    int? Rank = null, int? Morph = null, bool? IsPassive = null, long? CraftedId = null, string? SkillLine = null);
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
    public Dictionary<long, SetDefinition> Sets { get; set; } = [];
    public List<CollectionPiece> CollectionPieces { get; set; } = [];
    public List<ResearchTrait> ResearchTraits { get; set; } = [];
    public string? ResearchSignature { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; set; }
    public static GameCatalog Read(string path) => FromJson(File.ReadAllText(path));
    public static GameCatalog FromJson(string json) => JsonSerializer.Deserialize<GameCatalog>(json, JsonOptions)
        ?? throw new FormatException("Catalog must be a JSON object.");
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
}

using System.Text.Json;
using System.Text.Json.Serialization;
using EsoData.Addons;
using EsoData.Builds;
using EsoData.Models;

namespace EsoData.Accounts;

public static class AccountJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new FormatException("Expected a JSON object.");
    public static T Clone<T>(T value) => Read<T>(Write(value));
}

public sealed class EsoAccount
{
    public string Name { get; set; } = "";
    public string Server { get; set; } = "";
    public List<EsoCharacter> Characters { get; set; } = [];
    public List<Storage> SharedStorage { get; set; } = [];
    public Dictionary<long, long>? SetCollections { get; set; }
    public List<AccountSource> Sources { get; set; } = [];
    [JsonIgnore] public string Key => Server.ToUpperInvariant() + "/" + Name.ToUpperInvariant();
    [JsonIgnore] public IEnumerable<OwnedItem> Inventory => SharedStorage.Concat(Characters.SelectMany(c => c.Storage)).SelectMany(s => s.Items);
    public EsoAccount DeepClone() => AccountJson.Clone(this);
    public EsoCharacter Character(string idOrName)
    {
        var matches = Characters.Where(c => c.Id == idOrName || string.Equals(c.Name, idOrName, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new ArgumentException(matches.Length == 0
            ? $"Character '{idOrName}' was not found." : $"Character '{idOrName}' is ambiguous. Use its ID.");
    }
}

public sealed class EsoCharacter
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public int? Level { get; set; }
    public string? Class { get; set; }
    public string? Race { get; set; }
    public CharacterProgress Progress { get; set; } = new();
    public CharacterBuild Build { get; set; } = new();
    public List<Storage> Storage { get; set; } = [];
    public List<SavedBuild> SavedBuilds { get; set; } = [];
    public CharacterStatistics? RecordedStatistics { get; set; }
}

public sealed class CharacterProgress
{
    public int? TotalSkillPoints { get; set; }
    public int? UnspentSkillPoints { get; set; }
    public int? TotalChampionPoints { get; set; }
    public Dictionary<string, int>? SkillLines { get; set; }
    public List<SkillProgress>? Skills { get; set; }
    public Dictionary<string, int> ChampionBudgets { get; set; } = [];
    public Dictionary<string, Dictionary<long, bool?>> Knowledge { get; set; } = [];
    public ResearchKnowledge? ResearchKnowledge { get; set; }
    public ResearchSummary? Research { get; set; }
}

public sealed record SkillProgress(long AbilityId, string? Name, int Rank, bool IsPassive, bool? Purchased = null,
    int? Morph = null, bool? IsUltimate = null);
public sealed record SavedBuild(string Id, string? Name, DateTimeOffset? SavedAt, CharacterBuild Build);
public sealed class Storage
{
    public string Location { get; set; } = "";
    public string? CharacterId { get; set; }
    public List<OwnedItem> Items { get; set; } = [];
}
public sealed class OwnedItem
{
    public string Reference { get; set; } = "";
    public long ItemId { get; set; }
    public string? Name { get; set; }
    public string Link { get; set; } = "";
    public long Count { get; set; }
    public string Location { get; set; } = "";
    public string? CharacterId { get; set; }
    public int? Quality { get; set; }
    public long? Slot { get; set; }
    public long? SetId { get; set; }
    public int? Trait { get; set; }
    public int? ArmorType { get; set; }
    public int? WeaponType { get; set; }
    public int? EquipType { get; set; }
    public bool? CharacterBound { get; set; }
}
public sealed class AccountSource
{
    public string Provider { get; set; } = "";
    public string? Path { get; set; }
    public DateTimeOffset? FileWrittenAt { get; set; }
    public DateTimeOffset ReadAt { get; set; }
    public List<SourceCoverage> Coverage { get; set; } = [];
    public List<string> Diagnostics { get; set; } = [];
}
public sealed record SourceCoverage(string Section, string? CharacterId, string? Location,
    DateTimeOffset? ScannedAt, bool Complete);
public sealed record AccountInput(string SavedVariablesPath, string? DefaultServer = null);
public sealed class AccountLoadResult
{
    public List<EsoAccount> Accounts { get; set; } = [];
    public List<string> Diagnostics { get; set; } = [];
}

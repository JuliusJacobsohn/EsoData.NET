using EsoData.Items;
using EsoData.Lua;

namespace EsoData.Models;

public sealed record CharacterReference(string Server, string Account, string Id, string? Name = null, string? SourceAccountId = null);
public sealed record SourceInfo(string Addon, string? Path = null, DateTimeOffset? FileWrittenAt = null, long? ApiVersion = null);
public sealed record ItemStack(ItemLink Link, long Count, string Location, int? BagId = null,
    long? Slot = null, string? Name = null, int? Quality = null, long? SourceIndex = null);
public sealed record Inventory(string Server, string Account, string? CharacterId,
    DateTimeOffset? ObservedAt, IReadOnlyList<ItemStack> Items, string? SourceAccountId = null);
public sealed record SkillAllocation(long AbilityId, int Rank, bool IsPassive = false, string? Name = null);
public sealed record ChampionAllocation(long SkillId, int Points);
public sealed record Attributes(int Health, int Magicka, int Stamina);

/// <summary>Observed state. Missing information stays null; this is not a proposed CSPS profile.</summary>
public sealed class CharacterState
{
    public required CharacterReference Character { get; init; }
    public DateTimeOffset? ObservedAt { get; init; }
    public long? ApiVersion { get; init; }
    public int? Level { get; init; }
    public string? Class { get; init; }
    public string? Race { get; init; }
    public int? UnspentSkillPoints { get; init; }
    public int? TotalSkillPoints { get; init; }
    public int? ChampionPoints { get; init; }
    public Attributes? Attributes { get; init; }
    public IReadOnlyList<SkillAllocation> Skills { get; init; } = [];
    public IReadOnlyList<ChampionAllocation> ChampionAllocations { get; init; } = [];
    public IReadOnlyDictionary<string, ItemLink> Equipment { get; init; } = new Dictionary<string, ItemLink>();
    public required LuaTable Raw { get; init; }
}

/// <summary>One reader's results. Sources are not merged automatically to avoid double-counting inventory.</summary>
public sealed class AddonData
{
    public required SourceInfo Source { get; init; }
    public List<CharacterReference> Characters { get; } = [];
    public List<CharacterState> CharacterStates { get; } = [];
    public List<Inventory> Inventories { get; } = [];
    public List<string> Diagnostics { get; } = [];
    public required LuaTable Raw { get; init; }
}

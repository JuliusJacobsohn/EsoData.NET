namespace EsoData.Models;

/// <summary>Counts and display labels observed by the addon, without inferred trait IDs.</summary>
public sealed record ResearchSummary(DateTimeOffset? ObservedAt, IReadOnlyList<CraftResearch> Crafts);
public sealed record CraftResearch(string Craft, int? KnownTraits, int? TotalTraits, int? OpenSlots,
    IReadOnlyList<ResearchLineSummary> Lines, IReadOnlyList<ActiveResearch> Active);
public sealed record ResearchLineSummary(string Name, int? KnownTraits, int? TotalTraits,
    string? KnownDisplay, string? UnknownDisplay);

/// <summary>Remaining time at the research observation. Passing this timer does not confirm learning.</summary>
public sealed record ActiveResearch(int Index, string? Trait, string? Item, double? RemainingSeconds);

/// <summary>Observed CP. Slot skill ID zero is an explicitly empty slot; a missing slot is unobserved.</summary>
public sealed record ChampionState(int? SpentPoints, int? UnspentPoints,
    IReadOnlyList<ChampionDiscipline> Disciplines, IReadOnlyDictionary<int, long>? Slots,
    IReadOnlyList<ChampionStar> Stars);
public sealed record ChampionDiscipline(string Name, int? SpentPoints, int? UnspentPoints);
public sealed record ChampionStar(long SkillId, long? AbilityId, string DisplayName, int Points,
    int? Slot, string? Description);

/// <summary>Source stat names/units are retained; computed values are separated from game stats.
/// Cached bars can have been observed at different times and have no individual timestamps.</summary>
public sealed record CharacterStatistics(int? ActiveWeaponBar, int? ActiveAbilityBar,
    StatisticValues? Current, IReadOnlyDictionary<int, StatisticValues> Bars,
    IReadOnlyList<AdvancedStatistic>? Advanced);
public sealed record StatisticValues(IReadOnlyDictionary<string, double> Values,
    IReadOnlyDictionary<string, double> ComputedValues);
public sealed record AdvancedStatistic(string Name, long? StatId, string? Category,
    int? FormatType, double? FlatValue, double? PercentValue);

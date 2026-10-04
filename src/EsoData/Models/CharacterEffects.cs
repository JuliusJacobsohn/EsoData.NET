namespace EsoData.Models;

/// <summary>Active effects observed when the addon scanned the character. No remaining duration is recorded.</summary>
public sealed record CharacterEffects(IReadOnlyList<ObservedEffect>? Active,
    LastFoodObservation? LastFood);
public sealed record ObservedEffect(long AbilityId, string? Name, string? Description, string? Icon);
/// <summary>The last recorded meal, which does not establish that its buff is still active.</summary>
public sealed record LastFoodObservation(string? Name, string? Link, string? Description,
    int? Type, int? Level, int? ChampionPoints);

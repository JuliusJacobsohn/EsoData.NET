namespace EsoData.Catalogs;

/// <summary>External CP rules. Missing rules remain unknown rather than guessed.</summary>
public sealed record ChampionDefinition(long Id, string Name, string Discipline, int MaximumPoints,
    bool Slottable, int MinimumSlottablePoints = 1, IReadOnlyDictionary<long, int>? Prerequisites = null);
/// <summary>Exact externally supplied recipe for a result variant. Costs include improvement/glyph materials.</summary>
public sealed record CraftingRecipe(long ItemId, int Level, int ChampionPoints, int Quality,
    int StyleId, long EnchantmentItemId, int? EnchantmentQuality, IReadOnlyDictionary<long, long> Materials,
    string? KnowledgeCategory = null, long? KnowledgeItemId = null, string? CrafterId = null);

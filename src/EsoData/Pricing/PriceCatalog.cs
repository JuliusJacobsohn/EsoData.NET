using EsoData.Accounts;

namespace EsoData.Pricing;

public sealed record PriceSource(string Region, string Language, string? Path = null,
    DateTimeOffset? UpdatedAt = null, DateTimeOffset? FileWrittenAt = null);
public sealed record PriceIdentity(long TtcItemId, string Name, int SpecializedItemType);
/// <summary>TTC IDs are not ESO item IDs. Quality is TTC's zero-based quality; level is 50 + CP for champion items.</summary>
public sealed record PriceKey(long TtcItemId, int Quality, int Level, int Trait, string[] Extra);
public sealed record PriceStatistics(decimal? Average, decimal? Minimum, decimal? Maximum,
    long? ListingCount, long? ListedUnits, decimal? Suggested, decimal? SaleAverage, long? SaleCount, long? SoldUnits)
{
    public decimal? SuggestedMaximum => Suggested * 1.25m;
}
public sealed record PriceEntry(PriceKey Key, PriceStatistics Statistics);
public enum PriceMatchStatus { Matched, CatalogUnavailable, UnknownItem, NotListed, NeedsMetadata }
public sealed record ItemPrice(PriceMatchStatus Status, PriceEntry? Entry = null, string? Detail = null,
    int Candidates = 0)
{
    /// <summary>Market estimate, not proof an owned/bound item can be sold. Never substitutes zero for unknown.</summary>
    public decimal? EstimatedUnitPrice => Entry?.Statistics.Suggested ?? Entry?.Statistics.SaleAverage ?? Entry?.Statistics.Average;
    public string? EstimateBasis => Entry is null ? null : Entry.Statistics.Suggested.HasValue ? "Suggested"
        : Entry.Statistics.SaleAverage.HasValue ? "SaleAverage" : Entry.Statistics.Average.HasValue ? "ListingAverage" : null;
}

/// <summary>Complete local market catalog, independent of any account or inventory. No network access.</summary>
public sealed class PriceCatalog
{
    public PriceSource Source { get; set; } = new("EU", "EN");
    public List<PriceIdentity> Items { get; set; } = [];
    public List<PriceEntry> Entries { get; set; } = [];
    public List<string> Diagnostics { get; set; } = [];

    public ItemPrice Match(OwnedItem item) => new PriceMatcher(this).Match(item);
    /// <summary>Associates every storage item, including bank, character bags, equipped storage and craft bag.</summary>
    public void Associate(EsoAccount account)
    {
        if (!string.Equals(AccountLoader.NormalizeServer(account.Server), Source.Region, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Price catalog region does not match account server.", nameof(account));
        account.PriceSource = Source;
        var matcher = new PriceMatcher(this);
        foreach (var item in account.Inventory) item.Price = matcher.Match(item);
    }
}

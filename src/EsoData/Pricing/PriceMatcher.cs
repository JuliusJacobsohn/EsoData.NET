using System.Globalization;
using EsoData.Accounts;
using EsoData.Items;

namespace EsoData.Pricing;

/// <summary>Matches only known variant dimensions. Missing metadata is not evidence of a default trait or level.</summary>
internal sealed class PriceMatcher
{
    private readonly ILookup<string, PriceIdentity> names;
    private readonly ILookup<long, PriceEntry> entries;
    public PriceMatcher(PriceCatalog catalog)
    {
        names = catalog.Items.ToLookup(i => Normalize(i.Name));
        entries = catalog.Entries.ToLookup(e => e.Key.TtcItemId);
    }
    public ItemPrice Match(OwnedItem item)
    {
        var identities = names[Normalize(item.Name ?? "")].Distinct().ToArray();
        if (identities.Length == 0) return new(PriceMatchStatus.UnknownItem, Detail: "No exact name in the selected TTC language lookup.");
        if (item.SpecializedItemType.HasValue) identities = identities.Where(i => i.SpecializedItemType == item.SpecializedItemType).ToArray();
        if (identities.Length != 1) return new(PriceMatchStatus.NeedsMetadata, Detail: "Specialized item type is missing or ambiguous.", Candidates: identities.Length);
        var identity = identities[0];
        var candidates = entries[identity.TtcItemId].ToArray();
        if (candidates.Length == 0) return new(PriceMatchStatus.NotListed, Detail: "No prices for this TTC item in the region snapshot.");
        ItemLink.TryParse(item.Link, out var link);
        var quality = item.Quality;
        if (!quality.HasValue) return new(PriceMatchStatus.NeedsMetadata, Detail: "Item quality is unknown.");
        candidates = candidates.Where(e => e.Key.Quality == Math.Max(0, quality.Value - 1)).ToArray();
        // TTC categories 250/300 are weapon/armor. Potions/poisons and glyphs also require levels.
        var leveled = identity.SpecializedItemType is >= 0 and <= 29 or 250 or 300 or 450 or 1400 or 950 or 1000 or 1250;
        var level = item.RequiredChampionPoints is > 0 ? 50 + item.RequiredChampionPoints : item.RequiredLevel;
        level ??= link is null ? null : LinkLevel(link, leveled);
        if (!level.HasValue) return new(PriceMatchStatus.NeedsMetadata, Detail: "Required item level/CP is unknown.");
        candidates = candidates.Where(e => e.Key.Level == level).ToArray();
        if (identity.SpecializedItemType is 250 or 300 or 0 && !item.Trait.HasValue)
            return new(PriceMatchStatus.NeedsMetadata, Detail: "Equipment trait is unknown.", Candidates: candidates.Length);
        var trait = Trait(item.Trait ?? 0);
        if (!trait.HasValue) return new(PriceMatchStatus.NeedsMetadata, Detail: "Unmapped equipment trait.");
        candidates = candidates.Where(e => e.Key.Trait == trait).ToArray();
        string[] extra = [];
        if (identity.SpecializedItemType == 300)
        {
            if (item.ArmorType is not (1 or 2 or 3)) return new(PriceMatchStatus.NeedsMetadata, Detail: "Armor weight is unknown.");
            extra = [(item.ArmorType.Value + 1).ToString(CultureInfo.InvariantCulture)];
        }
        else if (identity.SpecializedItemType is 450 or 1400)
        {
            if (link is null) return new(PriceMatchStatus.NeedsMetadata, Detail: "Potion/poison item link is missing.");
            var effects = PotionEffects(link.Fields[20], identity.SpecializedItemType == 1400);
            if (effects is null) return new(PriceMatchStatus.NeedsMetadata, Detail: "Unknown potion effect code.");
            extra = [effects];
        }
        else if (candidates.Any(e => e.Key.Extra.Length > 0))
            return new(PriceMatchStatus.NeedsMetadata, Detail: "Additional variant dimensions (for example master-writ requirements) are needed.", Candidates: candidates.Length);
        candidates = candidates.Where(e => e.Key.Extra.SequenceEqual(extra)).ToArray();
        return candidates.Length switch
        {
            1 => new(PriceMatchStatus.Matched, candidates[0], Candidates: 1),
            0 => new(PriceMatchStatus.NotListed, Detail: "No price for the exact observed variant."),
            _ => new(PriceMatchStatus.NeedsMetadata, Detail: "Multiple prices match.", Candidates: candidates.Length)
        };
    }
    private static string Normalize(string name) => name.Split('^')[0].Trim().ToLowerInvariant();
    private static int? LinkLevel(ItemLink link, bool leveled)
    {
        if (!leveled) return 1;
        if (link.Subtype is >= 359 and <= 370) return 210; // CP160 dropped/crafted item links.
        if (link.Subtype is >= 125 and <= 174) return 50 + ((int)(link.Subtype - 125) % 10 + 1) * 10;
        if (link.Subtype is >= 231 and <= 312 && (link.Subtype - 231) % 18 <= 9)
            return 160 + (int)(link.Subtype - 231) / 18 * 10;
        if (link.Subtype is >= 20 and <= 34 || link.Level < 50) return link.Level;
        return null;
    }
    // Protocol enum translation, not an item-ID database.
    private static int? Trait(int trait) => trait switch
    {
        0 => -1, 1 => 0, 2 => 1, 3 => 2, 4 or 16 or 33 => 3, 5 => 4, 6 or 15 => 5,
        7 => 6, 8 => 7, 9 or 20 or 27 => 15, 10 or 19 or 24 => 16, 11 => 8, 12 => 9,
        13 => 10, 14 => 11, 17 => 12, 18 => 13, 21 => 18, 22 => 17, 23 => 19,
        25 or 26 => 14, 28 => 24, 29 => 22, 30 => 25, 31 => 21, 32 => 23, _ => null
    };
    private static string? PotionEffects(long encoded, bool poison)
    {
        int[] map = [0,13,1,14,2,15,7,20,8,21,5,17,6,18,3,58,4,16,12,23,9,10,11,22,24,28,25,27,19,26];
        var effects = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            var code = (int)((encoded >> (i * 8)) & 255);
            if (code == 0) continue;
            if (code > map.Length) return null;
            var id = map[code - 1];
            effects.Add(poison ? id == 58 ? 59 : id + 29 : id);
        }
        return string.Join('|', effects.Order());
    }
}

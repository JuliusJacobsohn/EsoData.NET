namespace EsoData.Items;

public enum ItemQuality { Normal = 1, Fine = 2, Superior = 3, Epic = 4, Legendary = 5 }

/// <summary>Builds links for resolved crafted item IDs; the item ID already identifies its trait.</summary>
public static class CraftedItem
{
    public static int NormalizeLevel(int level)
    {
        if (level is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(level));
        return level < 3 ? 1 : level / 2 * 2;
    }
    public static int Subtype(ItemQuality quality, int championPoints = 0)
    {
        if ((int)quality is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(quality));
        if (championPoints < 0 || championPoints > 160 || championPoints % 10 != 0)
            throw new ArgumentOutOfRangeException(nameof(championPoints), "Crafting CP must be 0, 10, 20, ... 160.");
        var q = (int)quality;
        if (championPoints == 160) return 365 + q;
        if (championPoints > 100) return (championPoints - 110) * 18 / 10 + 235 + q;
        if (championPoints > 0) return 115 + q * 10 + championPoints / 10 - 1;
        return 19 + q;
    }
    public static ItemLink Create(long itemId, int level, ItemQuality quality, int styleId = 1,
        int championPoints = 0, long enchantmentItemId = 0, ItemQuality? enchantmentQuality = null)
    {
        if (itemId <= 0) throw new ArgumentOutOfRangeException(nameof(itemId));
        var normalized = NormalizeLevel(level);
        if (normalized != level) throw new ArgumentException($"Level {level} is not craftable; use {normalized} explicitly.", nameof(level));
        if (championPoints > 0 && level != 50) throw new ArgumentException("CP items require level 50.");
        var subtype = Subtype(quality, championPoints);
        var glyphSubtype = enchantmentItemId == 0 ? 0 : Subtype(enchantmentQuality ?? quality, championPoints);
        return new([itemId, subtype, level, enchantmentItemId, glyphSubtype, enchantmentItemId == 0 ? 0 : level,
            0, 0, 0, 0, 0, 0, 0, 0, 0, styleId, 1, 0, 0, 10000, 0]);
    }
}

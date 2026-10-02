namespace EsoData.Catalogs;

/// <summary>Maps trait variants to the same stickerbook piece using captured piece IDs and item metadata.</summary>
public static class CollectionPieceLookup
{
    public static CollectionPiece? Resolve(long itemId, GameCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var exact = catalog.CollectionPieces.Where(p => p.PieceId == itemId).DistinctBy(p => (p.SetId, p.SlotMask)).ToArray();
        if (exact.Length == 1) return exact[0];
        if (exact.Length > 1 || !catalog.Items.TryGetValue(itemId, out var item)
            || item.SetId is not > 0 || item.EquipType is null || item.ArmorType is null || item.WeaponType is null) return null;
        var matches = catalog.CollectionPieces.Where(p => p.SetId == item.SetId
            && catalog.Items.TryGetValue(p.PieceId, out var representative)
            && representative.EquipType == item.EquipType && representative.ArmorType == item.ArmorType
            && representative.WeaponType == item.WeaponType).DistinctBy(p => p.SlotMask).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    /// <summary>Returns null when either the piece mapping or observed account collection is unavailable.</summary>
    public static bool? IsCollected(long itemId, GameCatalog catalog, IReadOnlyDictionary<long, long>? masks)
    {
        var piece = Resolve(itemId, catalog);
        return piece is not null && masks?.TryGetValue(piece.SetId, out var mask) == true
            ? (mask & piece.SlotMask) == piece.SlotMask : null;
    }
}

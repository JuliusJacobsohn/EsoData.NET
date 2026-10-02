using EsoData.Catalogs;
using EsoData.Items;
using EsoData.Lua;

namespace EsoData.Addons;

/// <summary>Optional game-API piece definitions captured in an existing addon's SavedVariables.
/// Masks are supplied by the game, never inferred from a piece's position or item ID.</summary>
public static class SetCollectionCatalog
{
    public static GameCatalog Read(string path) => Parse(SavedVariables.Read(path), path);

    public static GameCatalog Parse(LuaTable document, string? location = null)
    {
        var catalog = new GameCatalog();
        var sets = document.Table("LibMultiAccountSetsSavedVariables")?.Table("EsoDataPieces");
        if (sets is null) return catalog;
        catalog.Sources.Add(new("ESO collection piece API capture", location, ReadAt: DateTimeOffset.UtcNow));
        foreach (var set in sets.Tables())
        {
            if (!set.Key.IsNumeric || !long.TryParse(set.Key.Value, out var setId) || setId <= 0) continue;
            foreach (var row in set.Value.ArrayValues().OfType<LuaTable>())
            {
                if (LuaTable.AsInteger(row[1]) is not long pieceId || pieceId <= 0
                    || LuaTable.AsInteger(row[2]) is not long mask || mask <= 0 || (mask & (mask - 1)) != 0)
                    throw new FormatException("Collection pieces require positive IDs and a single-bit game slot mask.");
                var label = row[3] as string;
                if (label is not null && ItemLink.TryParse(label, out var link)) label = link!.Label;
                if (string.IsNullOrWhiteSpace(label)) label = null;
                catalog.CollectionPieces.Add(new(setId, pieceId, mask, label));
            }
        }
        return catalog;
    }
}

using EsoData.Catalogs;

namespace EsoData.Tests;

public class CollectionPieceLookupTests
{
    [Fact]
    public void TraitVariantsShareACollectionEntryButDifferentWeaponsDoNot()
    {
        var catalog = new GameCatalog
        {
            CollectionPieces = [new(7, 101, 8192), new(7, 102, 2)],
            Items = { [101] = new(101, SetId: 7, EquipType: 6, ArmorType: 0, WeaponType: 13, Trait: 1),
                [102] = new(102, SetId: 7, EquipType: 6, ArmorType: 0, WeaponType: 15, Trait: 1),
                [201] = new(201, SetId: 7, EquipType: 6, ArmorType: 0, WeaponType: 13, Trait: 4),
                [202] = new(202, SetId: 7, EquipType: 6, ArmorType: 0, WeaponType: 15, Trait: 4) }
        };
        Assert.True(CollectionPieceLookup.IsCollected(201, catalog, new Dictionary<long, long> { [7] = 8192 }));
        Assert.False(CollectionPieceLookup.IsCollected(202, catalog, new Dictionary<long, long> { [7] = 8192 }));
        Assert.Null(CollectionPieceLookup.IsCollected(201, catalog, null));
        Assert.Null(CollectionPieceLookup.IsCollected(999, catalog, new Dictionary<long, long> { [7] = 8192 }));
        catalog.CollectionPieces.Add(new(7, 103, 4));
        catalog.Items[103] = catalog.Items[101] with { Id = 103 };
        Assert.Null(CollectionPieceLookup.Resolve(201, catalog));
        Assert.Equal(8192, CollectionPieceLookup.Resolve(101, catalog)!.SlotMask);
    }
}

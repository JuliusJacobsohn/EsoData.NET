using EsoData.Accounts;
using EsoData.Catalogs;

namespace EsoData.Tests;

public sealed class EnchantingTests
{
    private const string Source = """
        local glyphInfo = {{10,11,900001,900002,"Reduce Example","Example",ITEMTYPE_GLYPH_WEAPON,ITEMTYPE_GLYPH_ARMOR,900003}}
        local enchantLevelInfo = {{-1,900004,366,50,lvl=nil,cp=160},{1,900005,366,50,lvl=nil,cp=160}}
        local qualityItemIdInfo = {900006,900007,900008,900009,900010}
        local cpQualityInfo = {[160]={366,367,368,369,370}}
        executableCodeMustNeverRun()
        """;

    [Fact]
    public void RefreshableTablesSelectPolarityQualityAndQuantity()
    {
        var catalog = LazyEnchantingCatalog.Parse(Source);
        var recipe = catalog.Recipe(new() { ItemId = 900002, Quality = 5, Quantity = 2 });
        Assert.Equal(900005, recipe.PotencyItemId);
        Assert.Equal(900010, recipe.AspectItemId);
        Assert.Equal(2, recipe.Materials[900003]);
        Assert.Equal("/script LLC_UserRequests:CraftEnchantingItemId(900005,900003,900010,true,\"EsoData\",nil,2)", recipe.ChatCommand);
        Assert.Equal(900004, catalog.Recipe(new() { ItemId = 900001, Quality = 4 }).PotencyItemId);
        Assert.Throws<ArgumentException>(() => catalog.Recipe(new() { ItemId = 900002, ChampionPoints = 120 }));
        Assert.Throws<ArgumentException>(() => catalog.Recipe(new() { ItemId = 900002, Quantity = 0 }));
    }

    [Fact]
    public void ExplicitGlyphOverridesDefaultAndMissingDefinitionsRemainUnknown()
    {
        var catalog = LazyEnchantingCatalog.Parse(Source);
        var items = UespCatalog.Parse("""{"minedItemSummary":[{"itemId":901000,"defaultEnchantId":10}]}""");
        var item = new OwnedItem { ItemId = 901000, Quality = 4,
            Link = "|H0:item:901000:363:50:900002:370:50:0:0:0:0:0:0:0:0:0:1:0:0:0:0:0|h|h" };
        Assert.Equal(new EnchantmentObservation(11, "Example", 5, "applied-glyph"), catalog.Observe(item, items));
        item.Link = "|H0:item:901000:363:50:0:0:0:0:0:0:0:0:0:0:0:0:1:0:0:0:0:0|h|h";
        Assert.Equal(4, catalog.Observe(item, items).Quality);
        Assert.Equal("Reduce Example", catalog.Observe(item, items).Name);
        Assert.Null(catalog.Observe(item, new()).EffectId);
    }
}

using EsoData.Catalogs;

namespace EsoData.Tests;

public class CatalogTests
{
    [Fact]
    public void LibSetsRangesIncludeStartAndFollowingCount()
    {
        var catalog = LibSetsCatalog.Parse("""
            -- Last updated: API 999999
            local unrelated = function() error('never executed') end
            setDataPreloaded[LIBSETS_TABLEKEY_SETNAMES] = {[7]={en='Example Set',de='Beispiel'}}
            """, "setDataPreloaded[LIBSETS_TABLEKEY_SETITEMIDS] = {[7]={'100,2',200}}");
        Assert.Equal(new long[] { 100, 101, 102, 200 }, catalog.Sets[7].ItemIds);
        Assert.Equal(7, catalog.Items[101].SetId);
        Assert.Equal("999999", catalog.Sources[0].Version);
        Assert.Single(catalog.FindSets("example"));
        var json = catalog.ToJson();
        Assert.Equal("Beispiel", GameCatalog.FromJson(json).Sets[7].Names["de"]);
    }

    [Fact]
    public void UespAcceptsStringNumbersAndSeparatesMorphFromBase()
    {
        var catalog = UespCatalog.Parse("""
            {"minedItemSummary":[{"itemId":"900001","setId":"7","equipType":"3","trait":"6"}],
              "minedSkills":[
                {"id":"900102","name":"Example Morph","baseAbilityId":"900100","rank":"1","morph":"2","isPassive":"0"},
                {"id":"900103","name":"Example Morph","baseAbilityId":"900100","rank":"2","morph":"2","isPassive":"0"}
              ]}
            """);
        Assert.Single(catalog.FindItems(7, trait: 6));
        Assert.Equal(900102, catalog.ToActiveSkill(900103).AbilityId);
        Assert.Equal(900100, catalog.ToBarSlot(900103).AbilityId);
        Assert.Equal(2, catalog.ToActiveSkill(900103).Morph);
        Assert.Throws<KeyNotFoundException>(() => catalog.ToBarSlot(999));
    }

    [Fact]
    public void UespSkillLinesKeepCurrentClassIdsOutOfCode()
    {
        var catalog = UespCatalog.Parse("""
            {"minedSkillLines":[{"id":"7","name":"Ardent Flame","fullName":"Dragonknight::Ardent Flame","classType":"Dragonknight"}]}
            """);
        Assert.Equal("Dragonknight", catalog.SkillLines[7].ClassType);
        Assert.Equal(7, GameCatalog.FromJson(catalog.ToJson()).SkillLines[7].Id);
    }

    [Fact]
    public void MergedCatalogResolvesOneCraftedItemFromMembershipAndMetadata()
    {
        var membership = LibSetsCatalog.Parse("""
            setDataPreloaded[LIBSETS_TABLEKEY_SETNAMES] = {[7]={en='Example Set'}}
            """, "setDataPreloaded[LIBSETS_TABLEKEY_SETITEMIDS] = {[7]={100}}");
        var metadata = UespCatalog.Parse("""
            {"minedItemSummary":[{"itemId":"100","name":"Example Helm","setId":"7","equipType":"1","armorType":"2","weaponType":"0","trait":"11"}]}
            """);

        var item = new CraftedItemSelector(7, EquipType: 1, ArmorType: 2, Trait: 11)
            .Resolve(GameCatalog.Merge([membership, metadata]));

        Assert.Equal(100, item.Id);
        Assert.Equal("Example Helm", item.Name);
        Assert.Equal(7, item.SetId);
    }
}

using EsoData.Accounts;
using EsoData.Items;
using EsoData.Pricing;

namespace EsoData.Tests;

public class PriceTests
{
    private const string Lookup = "function TTC:Load() self.ItemLookUpTable={['test reagent']={[1600]=7},['test cuirass']={[300]=8},['test ring']={[0]=9}} end";
    private const string Prices = """
        function TTC:Load() self.PriceTable={TimeStamp=1700000000,Data={
          [7]={[0]={[1]={[-1]={A=10.5,N=2,X=100,EC=4,AC=10,S=8,SA=7,SE=2,SAC=5}}}},
          [8]={[3]={[210]={[13]={[4]={Avg=200,Min=100,Max=300,EntryCount=2,AmountCount=2}}}}},
          [9]={[4]={[210]={[17]={A=400},[18]={A=500}}}}
        }} end
        """;
    [Fact]
    public void CompleteCatalogAndStatisticsDoNotRequireAnAccount()
    {
        var catalog = TtcPriceReader.Parse(Prices, Lookup);
        Assert.Equal(4, catalog.Entries.Count);
        Assert.Equal(3, catalog.Items.Count);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), catalog.Source.UpdatedAt);
        var price = catalog.Entries[0].Statistics;
        Assert.Equal(10.5m, price.Average); Assert.Equal(7m, price.SaleAverage);
        Assert.Equal(10m, price.SuggestedMaximum); Assert.Equal(4, price.ListingCount);
        Assert.Equal(5, price.SoldUnits);
        Assert.Equal(4, AccountJson.Read<PriceCatalog>(AccountJson.Write(catalog)).Entries.Count);
    }
    [Fact]
    public void AllStorageGetsAssociatedWithoutCountingUnknownAsZero()
    {
        var catalog = TtcPriceReader.Parse(Prices, Lookup);
        var account = new EsoAccount { Server = "EU", SharedStorage = [new() { Location = "Bank", Items =
            [new() { Name = "Test Reagent", Quality = 1, Count = 3, Link = CraftedItem.Create(500, 1, ItemQuality.Normal).ToString() }] }],
            Characters = [new() { Id = "1", Storage = [new() { Items = [new() { Name = "unlisted" }] }] }] };
        catalog.Associate(account);
        Assert.Equal(24m, account.SharedStorage[0].Items[0].EstimatedStackPrice);
        Assert.Equal(PriceMatchStatus.UnknownItem, account.Characters[0].Storage[0].Items[0].Price.Status);
        Assert.Null(account.Characters[0].Storage[0].Items[0].EstimatedStackPrice);
        Assert.Equal(24m, account.DeepClone().SharedStorage[0].Items[0].EstimatedStackPrice);
        account.Server = "NA"; Assert.Throws<ArgumentException>(() => catalog.Associate(account));
    }
    [Fact]
    public void ExactEquipmentVariantAndJewelryTraitsAreRequired()
    {
        var catalog = TtcPriceReader.Parse(Prices, Lookup);
        var item = new OwnedItem { Name = "Test Cuirass^ns", Quality = 4, Trait = 18, ArmorType = 3,
            Link = CraftedItem.Create(600, 50, ItemQuality.Epic, championPoints: 160).ToString() };
        Assert.Equal(200m, catalog.Match(item).Entry?.Statistics.Average);
        item.ArmorType = 1; Assert.Equal(PriceMatchStatus.NotListed, catalog.Match(item).Status);
        item.ArmorType = null; Assert.Equal(PriceMatchStatus.NeedsMetadata, catalog.Match(item).Status);
        item.Name = "Test Ring"; item.Quality = 5; item.Trait = 22;
        Assert.Equal(400m, catalog.Match(item).Entry?.Statistics.Average);
        item.Trait = 21; Assert.Equal(500m, catalog.Match(item).Entry?.Statistics.Average);
        item.Trait = null; Assert.Equal(PriceMatchStatus.NeedsMetadata, catalog.Match(item).Status);
    }
    [Fact]
    public void NestedPotionAndWritKeysAreRetainedAndNoLuaIsExecuted()
    {
        var catalog = TtcPriceReader.Parse("self.PriceTable={Data={[1]={[0]={[210]={[-1]={['1|3|5']={A=20}}}}},[2]={[3]={[1]={[-1]={[8]={[3]={[10]={[13]={A=5000}}}}}}}}}}", "self.ItemLookUpTable={}");
        Assert.Equal(new[] { "1|3|5" }, catalog.Entries[0].Key.Extra);
        Assert.Equal(new[] { "8", "3", "10", "13" }, catalog.Entries[1].Key.Extra);
        Assert.Throws<FormatException>(() => TtcPriceReader.Parse("self.PriceTable={Data=os.execute('bad')}", Lookup));
    }
}

using EsoData.Accounts;
using EsoData.Catalogs;
using EsoData.Items;
using EsoData.Pricing;

if (args.Length < 2) { Console.Error.WriteLine("Usage: EsoData.Prices <SavedVariables> <AddOns> [EU|NA]"); return 1; }
var region = args.ElementAtOrDefault(2) ?? "EU";
var prices = TtcPriceReader.Read(Path.Combine(args[1], "TamrielTradeCentre"), region);
var definitions = LibSetsCatalog.Read(Path.Combine(args[1], "LibSets"));
var data = AccountLoader.Load([new(args[0])], definitions, [prices]);
Console.WriteLine(AccountJson.Write(new { prices.Source, Identities = prices.Items.Count, Variants = prices.Entries.Count }));
foreach (var account in data.Accounts)
{
    Console.WriteLine(AccountJson.Write(new { Status = account.Inventory.GroupBy(i => new { i.Price.Status, i.Price.Detail })
        .Select(g => new { g.Key, Stacks = g.Count() }) }));
    foreach (var item in account.Inventory.Where(i => i.Price.Status == PriceMatchStatus.NeedsMetadata).Take(12))
        Console.WriteLine(AccountJson.Write(new { item.Name, item.Quality, item.Trait, item.ArmorType,
            Subtype = ItemLink.Parse(item.Link).Subtype, Level = ItemLink.Parse(item.Link).Level, item.Price.Detail }));
}
return 0;

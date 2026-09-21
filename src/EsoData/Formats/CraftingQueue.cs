using EsoData.Items;

namespace EsoData.Formats;

/// <summary>Dolgubon's Lazy Set Crafter 'Import Links' text. Repeated links preserve quantities.</summary>
public static class CraftingQueue
{
    public static IReadOnlyList<ItemLink> Read(string text) => ItemLink.FindAll(text).ToArray();
    public static string Write(IEnumerable<ItemLink> items) => string.Join(Environment.NewLine, items.Select(x =>
    {
        if (x.Fields.Count != 21) throw new ArgumentException("Lazy Set Crafter's importer accepts exactly 21 fields.", nameof(items));
        return x.WithoutLabel();
    }));
}

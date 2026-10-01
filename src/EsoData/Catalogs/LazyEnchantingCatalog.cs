using System.Text.RegularExpressions;
using EsoData.Accounts;
using EsoData.Builds;
using EsoData.Items;
using EsoData.Lua;

namespace EsoData.Catalogs;

public sealed record GlyphDefinition(long ItemId, long EffectId, string Name, string Category, long EssenceItemId, int Polarity);
public sealed record GlyphRecipe(GlyphDefinition Glyph, long PotencyItemId, long AspectItemId, int Quantity,
    IReadOnlyDictionary<long, long> Materials, string ChatCommand);
public sealed record EnchantmentObservation(long? EffectId, string? Name, int? Quality, string Source);

/// <summary>Reads current LibLazyCrafting rune tables; no game identifiers are bundled or Lua executed.</summary>
public sealed class LazyEnchantingCatalog
{
    public List<GlyphDefinition> Glyphs { get; } = [];
    private readonly List<(int Polarity, long ItemId, int? Level, int? Cp)> potency = [];
    private readonly List<long> aspects = [];
    private readonly Dictionary<int, long[]> cpSubtypes = [];

    public static LazyEnchantingCatalog Read(string addonDirectory) => Parse(File.ReadAllText(Path.Combine(addonDirectory, "Enchanting.lua")));
    public static LazyEnchantingCatalog Parse(string source)
    {
        var result = new LazyEnchantingCatalog();
        // Only these symbolic category values occur in the literal glyph table.
        foreach (var category in new[] { "ARMOR", "WEAPON", "JEWELRY" })
            source = source.Replace("ITEMTYPE_GLYPH_" + category, "\"" + category + "\"", StringComparison.Ordinal);
        foreach (var row in Extract(source, "glyphInfo").Tables().Select(x => x.Value))
            for (var side = 0; side < 2; side++)
                result.Glyphs.Add(new(Number(row[3 + side]), Number(row[1 + side]),
                    row[5 + side] as string ?? throw new FormatException("Missing glyph name."),
                    row[7 + side] as string ?? throw new FormatException("Missing glyph category."), Number(row[9]), side == 0 ? -1 : 1));
        foreach (var row in Extract(source, "enchantLevelInfo").Tables().Select(x => x.Value))
            result.potency.Add((checked((int)Number(row[1])), Number(row[2]),
                row.Integer("lvl") is long lvl ? checked((int)lvl) : null, row.Integer("cp") is long cp ? checked((int)cp) : null));
        result.aspects.AddRange(Extract(source, "qualityItemIdInfo").ArrayValues().Select(Number));
        foreach (var row in Extract(source, "cpQualityInfo").Tables())
            result.cpSubtypes.Add(int.Parse(row.Key.Value), row.Value.ArrayValues().Select(Number).ToArray());
        return result;
    }

    public GlyphRecipe Recipe(CraftingOrder order)
    {
        if (order.Quantity is < 1 or > 1000 || order.Quality < 1 || order.Quality > aspects.Count)
            throw new ArgumentException("Use quantity 1..1000 and a supported quality.");
        var glyph = Glyphs.SingleOrDefault(g => g.ItemId == order.ItemId) ?? throw new KeyNotFoundException("Unknown glyph ID.");
        var rune = potency.SingleOrDefault(p => p.Polarity == glyph.Polarity &&
            (order.ChampionPoints > 0 ? order.Level == 50 && p.Cp == order.ChampionPoints : p.Cp is null && p.Level == order.Level));
        if (rune.ItemId == 0) throw new ArgumentException("No exact potency level. Choose a supported glyph level explicitly.");
        var aspect = aspects[order.Quality - 1];
        return new(glyph, rune.ItemId, aspect, order.Quantity,
            new Dictionary<long, long> { [rune.ItemId] = order.Quantity, [glyph.EssenceItemId] = order.Quantity, [aspect] = order.Quantity },
            $"/script LLC_UserRequests:CraftEnchantingItemId({rune.ItemId},{glyph.EssenceItemId},{aspect},true,\"EsoData\",nil,{order.Quantity})");
    }

    public EnchantmentObservation Observe(OwnedItem item, GameCatalog catalog)
    {
        if (!ItemLink.TryParse(item.Link, out var link)) return new(null, null, null, "unobserved");
        if (link!.EnchantmentItemId > 0)
        {
            var glyph = Glyphs.SingleOrDefault(g => g.ItemId == link.EnchantmentItemId);
            var subtype = link.Fields[4];
            var quality = cpSubtypes.Values.Select(v => Array.IndexOf(v, subtype) + 1).FirstOrDefault(q => q > 0);
            return new(glyph?.EffectId, glyph?.Name, quality == 0 ? null : quality, "applied-glyph");
        }
        var effect = catalog.Items.GetValueOrDefault(item.ItemId)?.DefaultEnchantmentEffectId;
        return new(effect, Glyphs.FirstOrDefault(g => g.EffectId == effect)?.Name, effect is null ? null : item.Quality,
            effect is null ? "unknown-default" : "built-in; scales with item quality");
    }

    private static long Number(object? value) => LuaTable.AsInteger(value) ?? throw new FormatException("Expected a numeric rune field.");
    private static LuaTable Extract(string source, string name)
    {
        var match = Regex.Match(source, @"\blocal\s+" + name + @"\s*=\s*(?=\{)");
        if (!match.Success) throw new FormatException($"Missing LibLazyCrafting {name} table.");
        return SavedVariables.TablePrefix(source[(match.Index + match.Length)..]);
    }
}

using EsoData.Models;
using static EsoData.Formats.BuildText;

namespace EsoData.Formats;

/// <summary>ESO-Hub addondata text or build-editor URL. This format cannot express partial passive ranks or gear quality.</summary>
public sealed class HubBuild
{
    private readonly string[] fields;
    public HubBuild() => fields = ["0", "0", "0", "0:0:0", "0", "0", "0", "0,0,0,0,0,0", "0,0,0,0,0,0", "0", "0,0,0,0,0,0,0,0,0,0,0,0", "0", "0", "0", "0"];
    private HubBuild(string[] fields) => this.fields = fields;
    public static HubBuild Parse(string text)
    {
        text = text.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
        {
            var pair = uri.Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2))
                .FirstOrDefault(x => Uri.UnescapeDataString(x[0]) == "addondata");
            if (pair?.Length != 2) throw new FormatException("URL has no addondata query parameter.");
            text = Uri.UnescapeDataString(pair[1].Replace('+', ' '));
        }
        var fields = text.Split(';');
        if (fields.Length < 15) throw new FormatException("ESO-Hub addondata requires fifteen semicolon fields.");
        return new(fields);
    }
    public IReadOnlyList<string> Fields => Array.AsReadOnly(fields);
    public override string ToString() => Join(fields, ';');
    public string ToUrl(string language = "en") => $"https://eso-hub.com/{Uri.EscapeDataString(language)}/build-editor?addondata={Uri.EscapeDataString(ToString())}";
    public long ClassId { get => Number(fields[0]); set => fields[0] = N(value); }
    public IReadOnlyList<long> RaceIds { get => Numbers(fields[1]); set => fields[1] = Numbers(value); }
    public int Role { get => Int(fields[2]); set => fields[2] = N(value); }
    public Attributes Attributes { get => CspsBuild.ReadAttributes(fields[3], ':'); set => fields[3] = $"{N(value.Health)}:{N(value.Magicka)}:{N(value.Stamina)}"; }
    public long CurseId { get => Number(fields[4]); set => fields[4] = N(value); }
    public long MundusId { get => Number(fields[5]); set => fields[5] = N(value); }
    public IReadOnlyList<long> Subclasses { get => Numbers(fields[6]); set => fields[6] = Numbers(value); }
    public IReadOnlyList<HubBarSlot?> FrontBar { get => ReadBar(fields[7]); set => fields[7] = WriteBar(value); }
    public IReadOnlyList<HubBarSlot?> BackBar { get => ReadBar(fields[8]); set => fields[8] = WriteBar(value); }
    /// <summary>Purchased ability IDs; passive entries mean maximum rank in CSPS's importer.</summary>
    public IReadOnlyList<long> ExtraAbilities { get => Numbers(fields[9]); set => fields[9] = Numbers(value); }
    public IReadOnlyList<ChampionStar?> SlottedChampionPoints
    {
        get => fields[10].Split(',').Select(ReadStar).ToArray();
        set
        {
            if (value.Count != 12) throw new ArgumentException("Hub CP has twelve slots, four per discipline.");
            fields[10] = Join(value.Select(WriteStar));
        }
    }
    public IReadOnlyList<ChampionStar> OtherChampionPoints
    {
        get => Entries(fields[11]).Select(x => ReadStar(x)!).ToArray();
        set => fields[11] = value.Count == 0 ? "0" : Join(value.Select(x => WriteStar(x)));
    }
    public IReadOnlyList<HubGearSlot> Gear
    {
        get => Entries(fields[12]).Select(ReadGear).ToArray();
        set => fields[12] = value.Count == 0 ? "0" : Join(value.Select(g => g.PoisonItemId is long poison ?
            $"{N(g.EquipSlot)}:{N(poison)}" + (g.PoisonVariant is long v ? ":" + N(v) : "") :
            $"{N(g.EquipSlot)}:{N(g.Type)}:{N(g.SetId)}:{N(g.Trait)}:{N(g.GlyphItemId)}"));
    }
    public IReadOnlyList<Consumable> Food { get => ReadConsumables(fields[13]); set => fields[13] = WriteConsumables(value); }
    public IReadOnlyList<Consumable> Potions { get => ReadConsumables(fields[14]); set => fields[14] = WriteConsumables(value); }
    private static HubGearSlot ReadGear(string text)
    {
        var p = text.Split(':');
        return p.Length switch
        {
            2 or 3 => new(Int(p[0]), PoisonItemId: Number(p[1]), PoisonVariant: p.Length == 3 ? Number(p[2]) : null),
            5 => new(Int(p[0]), Int(p[1]), Number(p[2]), Int(p[3]), Number(p[4])),
            _ => throw new FormatException("Unrecognized Hub gear entry; raw Fields remain available.")
        };
    }
    private static IReadOnlyList<HubBarSlot?> ReadBar(string text) => text.Split(',').Select(x =>
    {
        if (x == "0" || Empty(x)) return null;
        var p = x.Split(':');
        if (p.Length is not (1 or 4)) throw new FormatException("Hub bar entries need an ability ID, optionally with three scripts.");
        return new HubBarSlot(Number(p[0]), p.Length == 4 ? p.Skip(1).Select(Number).ToArray() : null);
    }).ToArray();
    private static string WriteBar(IReadOnlyList<HubBarSlot?> bar)
    {
        if (bar.Count != 6 || bar.Any(x => x?.Scripts is { Count: not 3 })) throw new ArgumentException("Hub bars have six slots; crafted skills require three scripts.");
        return Join(bar.Select(x => x is null ? "0" : N(x.AbilityId) + (x.Scripts is null ? "" : ":" + Join(x.Scripts.Select(N), ':'))));
    }
    private static ChampionStar? ReadStar(string text)
    {
        if (text == "0" || Empty(text)) return null;
        var p = text.Split(':');
        if (p.Length is < 1 or > 2) throw new FormatException("Invalid CP entry.");
        return new(Number(p[0]), p.Length == 2 ? Int(p[1]) : null);
    }
    private static string WriteStar(ChampionStar? star) => star is null ? "0" : N(star.Id) + (star.Points is int n ? ":" + N(n) : "");
    private static Consumable[] ReadConsumables(string text) => Entries(text).Select(x => Parts(x, ':', 2)).Select(p => new Consumable(Number(p[0]), Number(p[1]))).ToArray();
    private static string WriteConsumables(IReadOnlyList<Consumable> values) => values.Count == 0 ? "0" : Join(values.Select(x => $"{N(x.ItemId)}:{N(x.VariantId)}"));
}

using EsoData.Models;
using static EsoData.Formats.BuildText;

namespace EsoData.Formats;

/// <summary>Native CSPS paste text. Unedited sections, including future sections, round-trip verbatim.</summary>
public sealed class CspsBuild
{
    private readonly string[] sections;
    public CspsBuild() => sections = Enumerable.Repeat("-", 9).ToArray();
    private CspsBuild(string[] sections) => this.sections = sections;
    public static CspsBuild Parse(string text)
    {
        var sections = text.Trim().Split('#');
        if (sections.Length < 9) throw new FormatException("Native CSPS text requires nine # sections.");
        return new(sections);
    }
    /// <summary>Use raw sections when an addon introduces an as-yet unmodeled extension.</summary>
    public IReadOnlyList<string> Sections => Array.AsReadOnly(sections);
    public override string ToString() => Join(sections, '#');

    public CspsSkills? Skills
    {
        get
        {
            if (Empty(sections[0])) return null;
            var fields = sections[0].Split('*');
            var active = Entries(Field(fields, 0)).Select(x => Parts(x, ':', 2)).Select(p => new ActiveSkill(Number(p[0]), Int(p[1]))).ToArray();
            var passive = Entries(Field(fields, 1)).Select(x => Parts(x, ':', 2)).Select(p => new PassiveSkill(Number(p[0]), Int(p[1]))).ToArray();
            var crafted = Entries(Field(fields, 2)).Select(x => Parts(x, ':', 4))
                .Select(p => new CraftedSkill(Number(p[0]), Number(p[1]), Number(p[2]), Number(p[3]))).ToArray();
            var styles = Entries(Field(fields, 3)).Select(x => Parts(x, ':', 2)).Select(p => new SkillStyle(Number(p[0]), Number(p[1]))).ToArray();
            return new(active, passive, crafted, styles, Numbers(Field(fields, 4)));
        }
        set => sections[0] = value is null ? "-" : Join(new[] {
            Join(value.Active.Select(x => $"{N(x.AbilityId)}:{N(x.Morph)}")),
            Join(value.Passive.Select(x => $"{N(x.AbilityId)}:{N(x.Rank)}")),
            Join((value.Crafted ?? []).Select(x => $"{N(x.CraftedAbilityId)}:{N(x.Script1)}:{N(x.Script2)}:{N(x.Script3)}")),
            Join((value.Styles ?? []).Select(x => $"{N(x.AbilityId)}:{N(x.CollectibleId)}")), Numbers(value.Subclasses) }
            .Select(x => x.Length == 0 ? "-" : x), '*');
    }
    /// <summary>Ordinary slots use the unmorphed rank-one ID, unlike Skills.Active.</summary>
    public IReadOnlyList<IReadOnlyList<BarSlot?>>? Bars
    {
        get => Empty(sections[1]) ? null : sections[1].Split(';').Select(bar =>
            (IReadOnlyList<BarSlot?>)bar.Split(',').Select(slot => Empty(slot) ? null :
                slot.StartsWith('c') ? new BarSlot(Number(slot[1..]), true) : new BarSlot(Number(slot))).ToArray()).ToArray();
        set
        {
            if (value is not null && (value.Count is < 1 or > 3 || value.Any(b => b.Count != 6)))
                throw new ArgumentException("CSPS supports one to three bars of six slots.");
            sections[1] = value is null ? "-" : Join(value.Select(bar => Join(bar.Select(slot => slot is null ? "-" :
                (slot.IsCrafted ? "c" : "") + N(slot.AbilityId)))), ';');
        }
    }
    public Attributes? Attributes
    {
        get => Empty(sections[2]) ? null : ReadAttributes(sections[2], ';');
        set => sections[2] = value is null ? "-" : $"{N(value.Health)};{N(value.Magicka)};{N(value.Stamina)}";
    }
    internal static Attributes ReadAttributes(string text, char separator)
    {
        var p = Parts(text, separator, 3); return new(Int(p[0]), Int(p[1]), Int(p[2]));
    }
    public long? Mundus { get => Empty(sections[3]) ? null : Number(sections[3]); set => sections[3] = value is null ? "-" : N(value.Value); }
    public CspsChampionPoints? ChampionPoints
    {
        get
        {
            if (Empty(sections[4])) return null;
            var fields = sections[4].Split('*');
            var points = Entries(fields[0], ';').Select(x => Parts(x, '-', 2)).Select(p => new ChampionStar(Number(p[0]), Int(p[1]))).ToArray();
            var bars = Empty(Field(fields, 1)) ? [] : fields[1].Split(';').Select(bar =>
                (IReadOnlyList<long?>)bar.Split(',').Select(x => Empty(x) ? (long?)null : Number(x)).ToArray()).ToArray();
            return new(points, bars);
        }
        set
        {
            if (value is not null && value.Allocations.Any(x => x.Points is null or < 0))
                throw new ArgumentException("Native CP allocations require explicit nonnegative points.");
            if (value is not null && (value.Bars.Count != 3 || value.Bars.Any(b => b.Count != 4)))
                throw new ArgumentException("Native CP has three bars of four slots.");
            sections[4] = value is null ? "-" : Join(value.Allocations.Select(x => $"{N(x.Id)}-{N(x.Points!.Value)}"), ';') + "*" +
                Join(value.Bars.Select(bar => Join(bar.Select(x => x is null ? "-" : N(x.Value)))), ';');
        }
    }
    public string GearText { get => sections[5]; set => sections[5] = value; }
    public IReadOnlyList<CspsGearSlot?>? Gear
    {
        get => Empty(GearText) ? null : GearText.Split(';').Select(ReadGear).ToArray();
        set
        {
            if (value is not null && value.Count != 16) throw new ArgumentException("Native CSPS gear has sixteen positions.");
            GearText = value is null ? "-" : Join(value.Select(x => x is null ? "0" : x.IsMara ? "mara:44904" :
                x.PoisonItemId is long poison ? $"{N(poison)}:{N(x.PoisonVariant)}" :
                $"{N(x.SetId)}:{N(x.Type)}:{N(x.Trait)}:{N(x.Quality)}:{N(x.EnchantmentEffectId)}"), ';');
        }
    }
    private static CspsGearSlot? ReadGear(string text)
    {
        if (Empty(text) || text == "0") return null;
        if (text == "mara:44904") return new(IsMara: true);
        var p = text.Split(':');
        return p.Length switch
        {
            2 => new(PoisonItemId: Number(p[0]), PoisonVariant: Number(p[1])),
            5 => new(Number(p[0]), Int(p[1]), Int(p[2]), Int(p[3]), Number(p[4])),
            _ => throw new FormatException("Unrecognized CSPS gear entry; use GearText to preserve it.")
        };
    }
    public string QuickslotsText { get => sections[6]; set => sections[6] = value; }
    public string OutfitText { get => sections[7]; set => sections[7] = value; }
    public int? Role { get => Empty(sections[8]) ? null : Int(sections[8]); set => sections[8] = value is null ? "-" : N(value.Value); }
}

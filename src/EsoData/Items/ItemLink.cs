using System.Globalization;
using System.Text.RegularExpressions;

namespace EsoData.Items;

/// <summary>An ESO item link. Unknown numeric fields are preserved, not interpreted as game facts.</summary>
public sealed partial class ItemLink
{
    private readonly long[] fields;
    public int LinkStyle { get; }
    public string Label { get; }
    public IReadOnlyList<long> Fields => Array.AsReadOnly(fields);
    public long ItemId => fields[0];
    public long Subtype => fields[1];
    public int Level => checked((int)fields[2]);
    /// <summary>Required character level, decoded from the link variant; null for an unsupported encoding.</summary>
    public int? RequiredLevel => RequiredChampionPoints is > 0 ? 50
        : Subtype is >= 20 and <= 34 || Level is >= 1 and < 50 ? Level : null;
    /// <summary>Required CP, or zero for a recognized non-CP link. Unknown variants remain null.</summary>
    public int? RequiredChampionPoints
    {
        get
        {
            // ESO protocol variants, also documented by UESP's ESO_ITEMINTTYPE_LEVELMAP:
            // https://github.com/uesp/uesp-esolog/blob/master/esoCommon.php
            // 358 includes green quest rewards; 359..370 cover the other CP160 variants.
            if (Subtype is >= 358 and <= 370) return 160;
            if (Subtype is >= 125 and <= 174) return ((int)(Subtype - 125) % 10 + 1) * 10;
            if (Subtype is >= 231 and <= 312 && (Subtype - 231) % 18 <= 9)
                return 110 + (int)(Subtype - 231) / 18 * 10;
            if (Subtype is >= 20 and <= 34 || Level is >= 1 and < 50) return 0;
            return null;
        }
    }
    public long EnchantmentItemId => fields[3];
    /// <summary>The chosen trait on a transmuted/reconstructed item; zero means use the item's catalog trait.</summary>
    public int? TraitOverride => fields[6] > 0 ? checked((int)fields[6]) : null;
    public int? ResolveTrait(int? catalogTrait) => TraitOverride ?? catalogTrait;
    public int StyleId => checked((int)fields[15]);
    public bool IsCrafted => fields[16] != 0;

    public ItemLink(IEnumerable<long> fields, int linkStyle = 1, string label = "")
    {
        this.fields = fields.ToArray();
        if (this.fields.Length < 21 || this.fields.Any(x => x < 0)) throw new ArgumentException("An item link needs at least 21 non-negative fields.", nameof(fields));
        if (linkStyle < 0) throw new ArgumentOutOfRangeException(nameof(linkStyle));
        if (label.Contains("|h", StringComparison.Ordinal)) throw new ArgumentException("Label contains a link delimiter.", nameof(label));
        LinkStyle = linkStyle; Label = label;
    }
    public static ItemLink Parse(string text)
    {
        if (!TryParse(text, out var result)) throw new FormatException("Not an ESO item link.");
        return result!;
    }
    public static bool TryParse(string text, out ItemLink? result)
    {
        result = null;
        var m = LinkPattern().Match(text.Trim());
        if (!m.Success || m.Length != text.Trim().Length) return false;
        var parts = m.Groups[2].Value.Split(':');
        if (parts.Length < 21 || !int.TryParse(m.Groups[1].Value, out var style)) return false;
        var numbers = new long[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!long.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return false;
        result = new(numbers, style, m.Groups[3].Value);
        return true;
    }
    public static IEnumerable<ItemLink> FindAll(string text)
    {
        foreach (Match match in LinkPattern().Matches(text))
            if (TryParse(match.Value, out var link)) yield return link!;
    }
    public override string ToString() => $"|H{LinkStyle}:item:{string.Join(':', fields.Select(x => x.ToString(CultureInfo.InvariantCulture)))}|h{Label}|h";
    public string WithoutLabel() => new ItemLink(fields, LinkStyle).ToString();

    [GeneratedRegex(@"\|H(\d+):item:(\d+(?::\d+){20,})\|h((?:[^|]|\|\|)*)\|h", RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();
}

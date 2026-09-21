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
    public long EnchantmentItemId => fields[3];
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

    [GeneratedRegex(@"\|H(\d+):item:(\d+(?::\d+){20,})\|h([^|]*)\|h", RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();
}

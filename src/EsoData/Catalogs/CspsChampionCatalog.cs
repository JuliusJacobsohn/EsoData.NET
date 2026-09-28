using System.Text.RegularExpressions;

namespace EsoData.Catalogs;

/// <summary>A CP name and discipline discovered from an installed CSPS data file.</summary>
public sealed record ChampionNameDefinition(long Id, string Name, string Discipline);

/// <summary>Reads the refreshable Champion Point labels shipped with Caro's Skill Point Saver.</summary>
public static partial class CspsChampionCatalog
{
    private static readonly string[] Disciplines = ["Craft", "Warfare", "Fitness"];

    public static IReadOnlyList<ChampionNameDefinition> Read(string addonDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonDirectory);
        return Parse(File.ReadAllText(Path.Combine(addonDirectory, "data", "cpinfo.lua")));
    }

    public static IReadOnlyList<ChampionNameDefinition> Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var definitions = new Dictionary<long, ChampionNameDefinition>();
        var discipline = 0;
        var sawDefinition = false;

        using var lines = new StringReader(source);
        while (lines.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (sawDefinition)
                {
                    discipline++;
                    sawDefinition = false;
                }
                continue;
            }

            var match = Entry().Match(line);
            if (!match.Success) continue;
            if (discipline >= Disciplines.Length)
                throw new FormatException("CSPS cpinfo.lua contains more than three CP discipline blocks.");

            var id = long.Parse(match.Groups[1].Value);
            var name = Annotation().Replace(match.Groups[2].Value.Trim(), "").Trim();
            if (name.Length == 0) throw new FormatException($"CSPS champion star {id} has no comment label.");
            if (!definitions.TryAdd(id, new(id, name, Disciplines[discipline])))
                throw new FormatException($"CSPS champion star {id} is duplicated.");
            sawDefinition = true;
        }

        if (definitions.Count == 0) throw new FormatException("CSPS cpinfo.lua contains no champion definitions.");
        return definitions.Values.OrderBy(x => x.Id).ToArray();
    }

    [GeneratedRegex(@"^\s*\[(\d+)\]\s*=.*?--\s*(.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex Entry();

    [GeneratedRegex(@"\s*\([^)]*\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex Annotation();
}

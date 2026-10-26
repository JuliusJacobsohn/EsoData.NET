using System.Globalization;
using System.Text.RegularExpressions;

namespace EsoData.Addons;

/// <summary>Locates supported addons and their saved files without executing addon code or reading account data.</summary>
public static partial class AddonDiscovery
{
    private sealed record Provider(string Id, string Name, string Feature, string[] SavedFiles);
    private sealed record Manifest(string Id, string Path, Dictionary<string, string> Headers);
    private static readonly Provider[] Providers =
    [
        new("IIfA", "Inventory Insight", "Account inventory", ["IIfA.lua"]),
        new("uespLog", "uespLog", "Character state and builds", ["uespLog.lua"]),
        new("LibCharacterKnowledge", "LibCharacterKnowledge", "Crafting and scribing knowledge", ["LibCharacterKnowledge.lua"]),
        new("LibMultiAccountSets", "LibMultiAccountSets", "Set collections", ["LibMultiAccountSets.lua"]),
        new("CarosSkillPointSaver", "Caro's Skill Point Saver", "Saved builds and CSPS imports", ["CarosSkillPointSaver.lua"]),
        new("LibSets", "LibSets", "Set definitions", []),
        new("DolgubonsLazySetCrafter", "Dolgubon's Lazy Set Crafter", "Equipment crafting queues", ["DolgubonsLazySetCrafter.lua"]),
        new("LibLazyCrafting", "LibLazyCrafting", "Crafting recipes and queues", []),
        new("TamrielTradeCentre", "Tamriel Trade Centre", "Locally downloaded prices", []),
        new("LootLog", "Loot Log", "Retained group drops", ["LootLog.lua"]),
        new("CombatMetrics", "Combat Metrics", "Saved combat reports", ["CombatMetricsFightData.lua"])
    ];

    /// <summary>Defaults to the OS Documents ESO/live folders. When one path is supplied, the other is its sibling.</summary>
    public static AddonInspection Inspect(string? addonsPath = null, string? savedVariablesPath = null)
    {
        var live = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Elder Scrolls Online", "live");
        addonsPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(addonsPath ?? (savedVariablesPath is null ? Path.Combine(live, "AddOns")
            : Path.Combine(Parent(savedVariablesPath), "AddOns"))));
        savedVariablesPath = Path.GetFullPath(savedVariablesPath ?? Path.Combine(Parent(addonsPath), "SavedVariables"));
        var diagnostics = new List<string>();
        var addonsFound = DirectoryFound(addonsPath, diagnostics);
        var savedFound = DirectoryFound(savedVariablesPath, diagnostics);
        var manifests = new List<Manifest>();
        var complete = addonsFound == false;
        if (addonsFound == true)
        {
            try
            {
                // Libraries can be embedded in an addon's subdirectories. Skip nested links to avoid cycles.
                foreach (var path in Directory.EnumerateFiles(addonsPath, "*", new EnumerationOptions
                { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint }))
                {
                    if (!Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)) continue;
                    var id = Path.GetFileNameWithoutExtension(path);
                    if (!id.Equals(Path.GetFileName(Path.GetDirectoryName(path)), StringComparison.OrdinalIgnoreCase)) continue;
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    manifests.Add(new(id, path, headers));
                    try
                    {
                        foreach (var line in File.ReadLines(path))
                        {
                            var text = line.Trim();
                            if (!text.StartsWith("##", StringComparison.Ordinal)) continue;
                            var colon = text.IndexOf(':');
                            if (colon > 2) headers[text[2..colon].Trim()] = text[(colon + 1)..].Trim();
                        }
                    }
                    catch (Exception error) when (FileError(error)) { diagnostics.Add($"Cannot read manifest {path}: {error.Message}"); }
                }
                complete = true;
            }
            catch (Exception error) when (FileError(error)) { diagnostics.Add($"Addon scan incomplete at {addonsPath}: {error.Message}"); }
        }
        var index = manifests.ToLookup(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var addons = Providers.Select(provider =>
        {
            var installations = index[provider.Id].OrderBy(m => m.Path, StringComparer.Ordinal).Select(m =>
                new AddonManifest(m.Path, m.Headers.GetValueOrDefault("Version"), Number(m.Headers.GetValueOrDefault("AddOnVersion")),
                    m.Headers.GetValueOrDefault("APIVersion"), Dependencies(m))).ToArray();
            return new AddonInstallation(provider.Id, provider.Name, provider.Feature,
                installations.Length > 0 ? true : complete ? false : null, installations,
                provider.SavedFiles.Select(f => InspectFile(Path.Combine(savedVariablesPath, f), diagnostics)).ToArray());
        }).ToArray();
        return new(addonsPath, savedVariablesPath, addonsFound, savedFound, addons, diagnostics);

        AddonDependency[] Dependencies(Manifest manifest) => DependencyPattern().Matches(
            manifest.Headers.GetValueOrDefault("DependsOn", "").Split(';', 2)[0]).Select(match =>
        {
            var id = match.Groups["id"].Value;
            var minimum = Number(match.Groups["version"].Value);
            var found = index[id].ToArray();
            bool? installed = found.Length > 0 ? true : complete ? false : null;
            var versions = found.Select(m => Number(m.Headers.GetValueOrDefault("AddOnVersion"))).ToArray();
            bool? satisfied = installed == false ? false : installed is null ? null : minimum is null ? true
                : versions.Any(v => v >= minimum) ? true : versions.All(v => v.HasValue) ? false : null;
            return new AddonDependency(id, minimum, installed, satisfied);
        }).ToArray();
    }

    private static string Parent(string path) => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))) ?? Path.GetPathRoot(Path.GetFullPath(path))!;
    private static bool FileError(Exception error) => error is IOException or UnauthorizedAccessException;
    private static long? Number(string? text) => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
    private static bool? DirectoryFound(string path, List<string> diagnostics)
    {
        try { return File.GetAttributes(path).HasFlag(FileAttributes.Directory); }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return false; }
        catch (Exception error) when (FileError(error)) { diagnostics.Add($"Cannot inspect directory {path}: {error.Message}"); return null; }
    }
    private static AddonSavedFile InspectFile(string path, List<string> diagnostics)
    {
        try
        {
            var info = new FileInfo(path);
            return new(path, true, info.Length, info.LastWriteTimeUtc);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return new(path, false, null, null); }
        catch (Exception error) when (FileError(error)) { diagnostics.Add($"Cannot inspect saved file {path}: {error.Message}"); return new(path, null, null, null); }
    }

    [GeneratedRegex(@"(?<id>[A-Za-z0-9_.-]+)(?:\s*>=\s*(?<version>\d+))?", RegexOptions.CultureInvariant)]
    private static partial Regex DependencyPattern();
}

public sealed record AddonInspection(string AddonsPath, string SavedVariablesPath, bool? AddonsDirectoryFound,
    bool? SavedVariablesDirectoryFound, IReadOnlyList<AddonInstallation> Addons, IReadOnlyList<string> Diagnostics);
public sealed record AddonInstallation(string Id, string Name, string Feature, bool? Installed,
    IReadOnlyList<AddonManifest> Installations, IReadOnlyList<AddonSavedFile> SavedFiles);
public sealed record AddonManifest(string Path, string? Version, long? AddOnVersion, string? ApiVersion,
    IReadOnlyList<AddonDependency> RequiredDependencies);
public sealed record AddonDependency(string Id, long? MinimumVersion, bool? Installed, bool? Satisfied);
public sealed record AddonSavedFile(string Path, bool? Exists, long? Bytes, DateTimeOffset? FileWrittenAt);

using EsoData.Addons;

namespace EsoData.Tests;

public sealed class AddonDiscoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EsoData.Addons", Guid.NewGuid().ToString("N"));
    private string Addons => Path.Combine(root, "AddOns");
    private string Saves => Path.Combine(root, "SavedVariables");
    private void Manifest(string relative, string headers, string extension = ".txt")
    {
        var folder = Path.Combine(Addons, relative);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, Path.GetFileName(folder) + extension), headers);
    }

    [Fact]
    public void ReportsInstalledAddonAndSavedFileIndependentlyWithoutParsingSavedData()
    {
        Manifest("IIfA", "## Title: Inventory Insight\n## Version: 3.93\n## AddOnVersion: 30093\n## APIVersion: 101046 101047");
        Directory.CreateDirectory(Saves);
        var path = Path.Combine(Saves, "IIfA.lua");
        File.WriteAllText(path, "deliberately not valid Lua");
        var before = File.GetLastWriteTimeUtc(path);
        var report = AddonDiscovery.Inspect(Addons, Saves);
        var inventory = Assert.Single(report.Addons, a => a.Id == "IIfA");
        Assert.True(inventory.Installed);
        Assert.Equal("3.93", Assert.Single(inventory.Installations).Version);
        var saved = Assert.Single(inventory.SavedFiles);
        Assert.True(saved.Exists);
        Assert.Equal(new FileInfo(path).Length, saved.Bytes);
        Assert.Equal(new DateTimeOffset(before), saved.FileWrittenAt);
        Assert.Equal("deliberately not valid Lua", File.ReadAllText(path));
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
        Assert.Empty(report.Diagnostics);
    }

    [Fact]
    public void SavedDataAndEmptyAddonFoldersDoNotProveInstallation()
    {
        Directory.CreateDirectory(Path.Combine(Addons, "IIfA"));
        Directory.CreateDirectory(Saves);
        File.WriteAllText(Path.Combine(Saves, "IIfA.lua"), "old saved data");
        var report = AddonDiscovery.Inspect(Addons + Path.DirectorySeparatorChar);
        Assert.Equal(Saves, report.SavedVariablesPath);
        var inventory = Assert.Single(report.Addons, a => a.Id == "IIfA");
        Assert.False(inventory.Installed);
        Assert.True(Assert.Single(inventory.SavedFiles).Exists);
    }

    [Fact]
    public void FindsNestedLibrariesAndChecksRequiredDependencyMinimumVersions()
    {
        Manifest("IIfA", "## DependsOn: LibAsync>=3 MissingLib LibUnknown>=5; comment\n## OptionalDependsOn: NotRequired");
        Manifest(Path.Combine("OtherAddon", "libs", "LibAsync"), "## AddOnVersion: 2");
        Manifest("LibUnknown", "## Version: human readable");
        Manifest(Path.Combine("OtherAddon", "libs", "LibCharacterKnowledge"), "## Version: 3.1.2");
        var report = AddonDiscovery.Inspect(Addons);
        Assert.True(Assert.Single(report.Addons, a => a.Id == "LibCharacterKnowledge").Installed);
        var dependencies = Assert.Single(Assert.Single(report.Addons, a => a.Id == "IIfA").Installations).RequiredDependencies;
        Assert.Equal(3, dependencies.Count);
        Assert.True(dependencies[0].Installed);
        Assert.False(dependencies[0].Satisfied);
        Assert.False(dependencies[1].Installed);
        Assert.False(dependencies[1].Satisfied);
        Assert.True(dependencies[2].Installed);
        Assert.Null(dependencies[2].Satisfied);
    }

    [Fact]
    public void RecognizesModernManifestsAndDoesNotCountLegacyAndModernAsTwoInstallations()
    {
        Manifest("LibSets", "## Version: legacy");
        Manifest("LibSets", "## Version: modern\n## DependsOn: EmbeddedLibrary>=5", ".ADDON");
        Manifest(Path.Combine("OtherAddon", "libs", "EmbeddedLibrary"), "## AddOnVersion: 5", ".addon");
        var addon = Assert.Single(AddonDiscovery.Inspect(Addons).Addons, a => a.Id == "LibSets");
        Assert.True(addon.Installed);
        var installation = Assert.Single(addon.Installations);
        Assert.Equal("modern", installation.Version);
        Assert.True(Assert.Single(installation.RequiredDependencies).Satisfied);
    }

    [Fact]
    public void MissingDirectoriesReturnExplicitMissingStatusesAndDoNotCreateFiles()
    {
        var report = AddonDiscovery.Inspect(Addons, Saves);
        Assert.False(report.AddonsDirectoryFound);
        Assert.False(report.SavedVariablesDirectoryFound);
        Assert.All(report.Addons, a => Assert.False(a.Installed));
        Assert.All(report.Addons.SelectMany(a => a.SavedFiles), f => Assert.False(f.Exists));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void KeepsDuplicateInstallationsAndAcceptsManifestFilenameCasing()
    {
        Manifest("IIfA", "## Version: one");
        Manifest(Path.Combine("OtherAddon", "libs", "IIfA"), "## Version: two");
        var report = AddonDiscovery.Inspect(savedVariablesPath: Saves);
        Assert.Equal(Addons, report.AddonsPath);
        Assert.Equal(2, Assert.Single(report.Addons, a => a.Id == "IIfA").Installations.Count);
        File.Move(Path.Combine(Addons, "IIfA", "IIfA.txt"), Path.Combine(Addons, "IIfA", "iifa.TXT"));
        Assert.Equal(2, Assert.Single(AddonDiscovery.Inspect(Addons).Addons, a => a.Id == "IIfA").Installations.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

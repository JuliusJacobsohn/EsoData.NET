using EsoData.Addons;
using EsoData.Catalogs;
using EsoData.Formats;
using EsoData.Lua;

if (args.Length < 2)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  inspect <SavedVariables directory>");
    Console.WriteLine("  catalog <LibSets addon directory> [output.json]");
    Console.WriteLine("  csps <file containing native CSPS text>");
    return 1;
}

switch (args[0])
{
    case "inspect":
        Inspect("IIfA.lua", path =>
        {
            var data = IifaReader.Read(path);
            Console.WriteLine($"IIfA: {data.Characters.Count} characters, {data.Inventories.Count} locations, " +
                $"{data.Inventories.Sum(x => x.Items.Count)} stacks, {data.Diagnostics.Count} diagnostics; file saved {data.Source.FileWrittenAt:u}");
            foreach (var diagnostic in data.Diagnostics.Take(5)) Console.WriteLine("  " + diagnostic);
        });
        Inspect("LibCharacterKnowledge.lua", path =>
        {
            var data = CharacterKnowledgeReader.Read(path);
            Console.WriteLine($"LCK: {data.Characters.Count} characters, {data.Characters.Count(x => x.Research is not null)} research scans, {data.Diagnostics.Count} diagnostics");
        });
        Inspect("LibMultiAccountSets.lua", path =>
        {
            var data = SetCollectionReader.Read(path);
            Console.WriteLine($"LMAS: {data.Accounts.Count} account/server collections");
        });
        Inspect("CarosSkillPointSaver.lua", path =>
        {
            var data = CspsReader.Read(path);
            // Access all modeled fields as a useful check of the installed profile format.
            foreach (var profile in data.Profiles)
            {
                _ = profile.Build.Skills; _ = profile.Build.Bars; _ = profile.Build.Attributes;
                _ = profile.Build.ChampionPoints; _ = profile.Build.Gear; _ = profile.Build.Role; _ = profile.Build.Mundus;
            }
            Console.WriteLine($"CSPS: {data.Profiles.Count} saved profiles");
        });
        Inspect("DolgubonsLazySetCrafter.lua", path =>
            Console.WriteLine($"Lazy Set Crafter: {LazySetCrafterReader.Read(path).Requests.Count} queued requests"));
        Inspect("uespLog.lua", path =>
        {
            var data = UespLogReader.Read(path);
            Console.WriteLine($"uespLog: {data.CharacterStates.Count} character observations, {data.Inventories.Count} inventories, {data.Diagnostics.Count} diagnostics");
        });
        break;
    case "catalog":
        var catalog = LibSetsCatalog.Read(args[1]);
        Console.WriteLine($"LibSets: {catalog.Sets.Count} sets, {catalog.Items.Count} item IDs");
        if (args.Length > 2) { catalog.Write(args[2]); Console.WriteLine($"Wrote {Path.GetFullPath(args[2])}"); }
        break;
    case "csps":
        var build = CspsBuild.Parse(File.ReadAllText(args[1]));
        Console.WriteLine($"Active skills: {build.Skills?.Active.Count ?? 0}; passives: {build.Skills?.Passive.Count ?? 0}");
        Console.WriteLine(build);
        break;
    default:
        Console.Error.WriteLine("Unknown command."); return 1;
}
return 0;

void Inspect(string filename, Action<string> action)
{
    var path = Path.Combine(args[1], filename);
    if (File.Exists(path)) action(path);
    else Console.WriteLine($"{filename}: not present; skipped");
}

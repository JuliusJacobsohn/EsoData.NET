using System.Diagnostics;
using System.Text.Json;
using EsoData.Addons;
using EsoData.Catalogs;
using EsoData.Formats;

// Read-only diagnostic. Prints timing/size aggregates, never account contents.
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: EsoData.Benchmark <SavedVariables directory> <AddOns directory>");
    return 1;
}

var saves = Path.GetFullPath(args[0]);
var addons = Path.GetFullPath(args[1]);
var readers = new (string File, Func<string, object> Read)[]
{
    ("IIfA.lua", p => IifaReader.Read(p)),
    ("LibCharacterKnowledge.lua", p => CharacterKnowledgeReader.Read(p)),
    ("CarosSkillPointSaver.lua", p => CspsReader.Read(p)),
    ("LibMultiAccountSets.lua", p => SetCollectionReader.Read(p)),
    ("uespLog.lua", p => UespLogReader.Read(p)),
    ("DolgubonsLazySetCrafter.lua", p => LazySetCrafterReader.Read(p))
};

foreach (var (file, read) in readers)
{
    var path = Path.Combine(saves, file);
    if (!File.Exists(path)) { Console.WriteLine(JsonSerializer.Serialize(new { operation = file, missing = true })); continue; }
    Measure(file, () => read(path), new FileInfo(path).Length);
}

Measure("all-account-readers", () => readers.Where(r => File.Exists(Path.Combine(saves, r.File)))
    .Select(r => r.Read(Path.Combine(saves, r.File))).ToArray());
if (Directory.Exists(Path.Combine(addons, "LibSets")))
    Measure("LibSets-catalog", () => LibSetsCatalog.Read(Path.Combine(addons, "LibSets")));

var cspsPath = Path.Combine(saves, "CarosSkillPointSaver.lua");
if (File.Exists(cspsPath))
{
    var profiles = CspsReader.Read(cspsPath).Profiles;
    Measure("CSPS-text-roundtrip-all-profiles", () => profiles
        .Select(p => CspsBuild.Parse(p.Build.ToString()).ToString()).ToArray());
}
return 0;

static void Measure(string operation, Func<object> run, long? bytes = null)
{
    var first = Stopwatch.StartNew();
    GC.KeepAlive(run());
    var firstMs = first.Elapsed.TotalMilliseconds;
    var samples = new double[7];
    for (var i = 0; i < samples.Length; i++)
    {
        var timer = Stopwatch.StartNew();
        GC.KeepAlive(run());
        samples[i] = timer.Elapsed.TotalMilliseconds;
    }
    Array.Sort(samples);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        operation, bytes, firstMs = Math.Round(firstMs, 3),
        medianMs = Math.Round(samples[samples.Length / 2], 3),
        minMs = Math.Round(samples[0], 3), maxMs = Math.Round(samples[^1], 3), samples = samples.Length
    }));
}

# EsoData.NET

[![CI](https://github.com/JuliusJacobsohn/EsoData.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/JuliusJacobsohn/EsoData.NET/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/EsoData.NET.svg)](https://www.nuget.org/packages/EsoData.NET)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> [!WARNING]
> Created with substantial help from **OpenAI Codex** and tested against my own use cases. Please do not treat this project as a measure of my abilities as a developer, for better or worse.

A standalone **.NET 10** library for reading Elder Scrolls Online addon data into C# models and generating build and crafting imports. No runtime dependencies, database, MCP server, game process access, or network connection is required for local formats. Use it from a console app, desktop app, web backend, or your own MCP server.

The [account-first replacement architecture](docs/account-architecture.md) defines the next design: a mutable account graph, shared build analysis and partial exports. It is a design document; the API examples below describe the current release.

## Install

```shell
dotnet add package EsoData.NET
```

The package is `EsoData.NET`; its assembly and root namespace are `EsoData`. Requires .NET 10. Release packages are also attached to [GitHub releases](https://github.com/JuliusJacobsohn/EsoData.NET/releases).

## What it supports

| Source or format | Typed support |
|---|---|
| IIfA | Account/server character directory and item stacks by character, bank, craft bag and other saved locations |
| LibCharacterKnowledge | Recipes, furnishing plans, motifs, scribing unlocks, research flags and timers |
| LibMultiAccountSets | Current compressed and legacy account collection masks, including 36-bit values |
| uespLog | Character observations, purchased skills and skill-line ranks, CP slots/allocations/budgets, research summaries/timers, current and per-bar stats, equipped items and saved inventories |
| Caro's Skill Point Saver | Default, named and auxiliary saved profiles; native CSPS text import/export with skills, passive ranks, bars, attributes, CP and gear |
| ESO-Hub | `addondata` text and build-editor URLs, including skills, CP, equipment and consumables |
| Dolgubon's Lazy Set Crafter | Read saved crafting queues; generate and read item-link lists for **Import Links** |
| LibSets | Read current installed set names and compressed item-ID membership tables |
| UESP JSON / external JSON | Read item and skill metadata; save/load searchable catalogs independently of the assembly |

Lua readers retain the original parsed tables in `Raw`, including fields not yet modeled. They parse data **without executing Lua**. There is no automatic write-back to addon SavedVariables.

## Read account inventory

```csharp
using EsoData.Addons;

var inventory = IifaReader.Read(Path.Combine(savedVariablesDirectory, "IIfA.lua"));

foreach (var location in inventory.Inventories)
foreach (var stack in location.Items)
    Console.WriteLine($"{stack.Name}: {stack.Count} in {stack.Location}, item {stack.Link.ItemId}");

Console.WriteLine($"Last disk save: {inventory.Source.FileWrittenAt}");
foreach (var message in inventory.Diagnostics)
    Console.WriteLine(message);
```

Paths are caller-supplied. On Windows, the live server directory is usually under `Documents/Elder Scrolls Online/live/SavedVariables`; Documents may be redirected. Inventory locations are kept separate. The library does not merge different addons' observations and accidentally count the same bank twice.

```csharp
var knowledge = CharacterKnowledgeReader.Read(Path.Combine(savedVariablesDirectory, "LibCharacterKnowledge.lua"));
var profiles = CspsReader.Read(Path.Combine(savedVariablesDirectory, "CarosSkillPointSaver.lua"));
var observations = UespLogReader.Read(Path.Combine(savedVariablesDirectory, "uespLog.lua"));
```

Missing scans stay missing. In knowledge records, `Known = null` means unavailable; `false` is an observed unset bit. A missing category is not proof that the character knows nothing. A saved CSPS profile is a plan, not proof it is applied.

### Character research, CP and statistics

```csharp
foreach (var character in observations.CharacterStates)
{
    Console.WriteLine($"{character.Character.Name}, observed {character.ObservedAt}");
    foreach (var craft in character.Research?.Crafts ?? [])
        Console.WriteLine($"{craft.Craft}: {craft.KnownTraits}/{craft.TotalTraits}; {craft.OpenSlots} research slots free");

    Console.WriteLine($"Unspent CP: {character.Champion?.UnspentPoints}");
    // Slot values are champion skill IDs; zero means explicitly empty.
    var slots = character.Champion?.Slots;
    var currentStats = character.Statistics?.Current?.Values;
    var skillLineRanks = character.SkillLineRanks;
}
```

`Research` includes per-line counts, original display labels, active research and its own observation timestamp. Display labels are not trait-ID mappings; a bracketed trait may still be researching. `Champion` adds per-discipline budgets, named stars, their distinct skill/ability IDs and slots while retaining the existing `ChampionAllocations` API. `Statistics` separates current, saved-bar, computed and advanced values, retaining source names and units (including unknown future stat names). Cached bars can come from different moments; buffs and equipment affect the observations. Missing sections are `null`. See [format details](docs/formats.md#uesp-character-details).

## Resolve IDs from refreshable catalogs

```csharp
using EsoData.Catalogs;

var membership = LibSetsCatalog.Read(Path.Combine(addonsDirectory, "LibSets"));
var metadata = UespCatalog.Parse(File.ReadAllText("uesp-items-and-skills.json"));
var catalog = GameCatalog.Merge([membership, metadata]);
catalog.Write("catalog.json");

var reloaded = GameCatalog.Read("catalog.json");
foreach (var set in reloaded.FindSets("Order"))
    Console.WriteLine($"{set.Id}: {set.Names["en"]}, {set.ItemIds.Count} item IDs");
```

Run that again after updating LibSets to refresh its catalog. Set membership alone does not reveal each item's trait or equipment type. Enrich item/skill metadata from UESP's `exportJson.php` output with `UespCatalog.Parse(json, sourceUrl, version)`, or provide a catalog in the library's JSON shape. `GameCatalog.Merge` preserves LibSets membership and lets later descriptive fields supplement it. Fetching, refresh scheduling and storage belong to your application. UESP access/availability is not guaranteed by this library.

Catalogs record provenance and keep IDs as data. No game database is bundled, downloaded on startup, or pinned to an ESO patch. `GameCatalog` also accepts externally supplied collection-piece slot masks and research-index mappings. **Those mappings are not included in the corresponding addon saves.** See [formats and data boundaries](docs/formats.md).

## Generate a crafting import

```csharp
using EsoData.Formats;
using EsoData.Items;

// Numeric values come from the refreshable catalog, not the package.
var resolved = new CraftedItemSelector(SetId: 642, EquipType: 1, ArmorType: 2, Trait: 11)
    .Resolve(catalog);
long resolvedItemId = resolved.Id;
var item = CraftedItem.Create(resolvedItemId, level: 32, quality: ItemQuality.Epic);
string pasteIntoLazySetCrafter = CraftingQueue.Write([item, item]);
```

Repeated links are intentional, for example two identical rings. The **item ID already determines the trait**; setting an arbitrary field in the link cannot turn a different item into Training gear. Supply `enchantmentItemId` to embed a resolved glyph. `CraftedItem.NormalizeLevel(33)` returns `32`; creation requires that you choose the craftable level explicitly. CP tiers and quality are encoded by the same formulas used in the inspected addon source.

Generating a queue does not establish that a crafter has the research, style, station access or materials to fulfill it. Crafting and applying remain in-game actions.

## Read and edit a complete CSPS allocation

```csharp
using EsoData.Formats;

var build = CspsBuild.Parse(File.ReadAllText("build.txt"));
Console.WriteLine($"Purchased passives: {build.Skills?.Passive.Count ?? 0}");
build.Attributes = new(64, 0, 0); // Health, Magicka, Stamina
File.WriteAllText("updated-build.txt", build.ToString());
```

`CspsBuild.Skills` holds active morph choices, purchased passive ranks, crafted skills/scripts, styles and subclasses. `Bars` is separate. Assign a new `CspsSkills` value to change purchases. `CspsBuild` also exposes `ChampionPoints`, `Gear`, `Mundus` and `Role`; quickslots/outfits stay as raw text. Parsed sections you do not edit, including additional future sections, are preserved verbatim.

Ordinary native hotbar IDs are **unmorphed rank-one IDs**; active purchase IDs are **selected-morph rank-one IDs**. With complete metadata, `GameCatalog.ToActiveSkill(id)` and `ToBarSlot(id)` resolve that distinction. They report missing metadata instead of guessing.

```csharp
var hub = HubBuild.Parse(hubUrlOrAddonData);
string hubUrl = hub.ToUrl();
```

Hub and native CSPS are separate models because they express different things. Hub cannot express partial passive ranks or gear quality; native CSPS text lacks class/race and item levels. Automatic lossless conversion is not promised. Select **CSPS** for native text and **eso-hub.com** for Hub data in the addon import dropdown.

## Freshness and compatibility

- SavedVariables are the last disk save, not live game memory. `/reloadui` or a normal logout flushes observed state. Characters/banks must first have been observed by the addon.
- File modification times and available observation timestamps are exposed. There is no background file watcher or implied live connection.
- API/version numbers are metadata, not allowlists. New patch numbers are accepted; malformed structure produces a parse error or an explicit diagnostic.
- Research indexes need a mapping matching the research layout; collection bits need actual slot masks, not piece ordinal numbers. Elapsed research timers do not become confirmed learned traits automatically.
- UESP character data requires its character-saving feature to be enabled. Its raw save can contain private account information; choose what you serialize or share.
- Export codecs have source/format tests. Generated builds have **not** been applied in-game as part of the automated test suite.

## Build and try it

```shell
dotnet build EsoData.sln -c Release
dotnet test EsoData.sln -c Release
dotnet pack src/EsoData/EsoData.csproj -c Release -o artifacts/packages

dotnet run --project samples/EsoData.Example -- inspect "path/to/SavedVariables"
dotnet run --project samples/EsoData.Example -- catalog "path/to/AddOns/LibSets" "catalog.json"
```

Tests use small synthetic fixtures. No private account saves or copied game databases are committed. The example skips absent addons. CI builds, tests and packs on Windows and Linux. See [release instructions](docs/releasing.md) for NuGet trusted publishing and GitHub releases.

## Sources and maintenance

The implementation follows addon serializers and their maintained source, rather than copied ID lists. Source links and format assumptions are in [docs/formats.md](docs/formats.md). When a format changes, update that reader and a small regression fixture. Ordinary game-content additions need refreshed catalog data, not larger source files.

This package handles data access and encoding. It does not include a build optimizer, custom bridge addon, archive extractor, game automation, or a database. Those can be separate consumers/providers when needed.

Maintained by Julius Jacobsohn. Licensed under MIT. Not affiliated with or endorsed by ZeniMax Media or Bethesda. The Elder Scrolls and related names belong to their respective owners.

# EsoData.NET

[![CI](https://github.com/JuliusJacobsohn/EsoData.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/JuliusJacobsohn/EsoData.NET/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/EsoData.NET.svg)](https://www.nuget.org/packages/EsoData.NET)

A standalone .NET library for reading [The Elder Scrolls: Online](https://www.elderscrollsonline.com/) addon data into C# models, inspecting accounts and characters, and generating build and crafting imports.

> [!WARNING]
> Created with substantial help from **GPT-6.1 Sol** and tested against my own use cases. Please do not treat this project as a measure of my abilities as a developer, for better or worse.

## Features

- Inspect characters and account-wide inventory, including banks, craft bags and storage.
- Read skills, Champion Points, crafting knowledge, research and set collections.
- Edit proposed builds, compare them with saved character state, and check their requirements.
- Read and write CSPS builds, work with ESO-Hub builds, and generate crafting imports.
- Read retained group loot, saved combat summaries and local TTC market prices.

The library has no runtime package dependencies. It reads local files without executing Lua or changing game files; applications control catalog downloads and data storage. For use from an AI client, [EsoMcp.NET](https://github.com/JuliusJacobsohn/EsoMcp.NET) exposes this library through MCP tools.

## Installation

Requires **.NET 10**.

```shell
dotnet add package EsoData.NET
```

## Quick start

For this inventory example, install and enable **Inventory Insight (IIfA)** using [Minion](https://minion.mmoui.com/). In ESO, log in to the characters you want to inspect, open the bank and relevant storage, then use `/reloadui` or log out normally to save the addon's observations.

Create a console app and add the package:

```shell
dotnet new console -n EsoExample
cd EsoExample
dotnet add package EsoData.NET
```

Replace `Program.cs` with the following. It finds the usual **Windows** live-server SavedVariables directory through your Documents folder, including when Documents is redirected to OneDrive:

```csharp
using EsoData.Accounts;

var savedVariablesPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    "Elder Scrolls Online", "live", "SavedVariables");

var loaded = AccountLoader.Load([new AccountInput(savedVariablesPath)]);

foreach (var diagnostic in loaded.Diagnostics)
    Console.WriteLine(diagnostic);

foreach (var account in loaded.Accounts)
{
    Console.WriteLine($"{account.Name} ({account.Server}): {account.Characters.Count} characters");
    foreach (var item in account.Inventory.Take(10))
        Console.WriteLine($"  {item.Name}: {item.Count} in {item.Location}");
}
```

Run it with `dotnet run`. For another installation, server folder or operating system, set `savedVariablesPath` to your actual SavedVariables directory. If no accounts appear, check that `IIfA.lua` exists there and that the addon has saved data.

Inventory loading does not require `catalog.json`. A catalog supplies additional game definitions for lookups, build analysis and exports; the [catalog guide](https://github.com/JuliusJacobsohn/EsoData.NET/blob/main/docs/usage.md#resolve-ids-from-refreshable-catalogs) shows how to create and save one from installed addon data and external definitions before using it.

## Supported addons and formats

| Source or format | Support |
|---|---|
| Inventory Insight (IIfA) | Characters and inventory across saved storage locations |
| uespLog | Character state, skills, CP, equipment, research and statistics |
| LibCharacterKnowledge | Recipes, plans, motifs, scribing knowledge and research |
| LibMultiAccountSets | Account set collections |
| Caro's Skill Point Saver (CSPS) | Saved profiles and native build text import/export |
| ESO-Hub | Build-editor URLs and addon data |
| Dolgubon's Lazy Set Crafter / LibLazyCrafting | Crafting queues, equipment imports and glyph recipes |
| LibSets / UESP JSON / external catalogs | Set, item, skill and other game definitions |
| Loot Log / Combat Metrics | Retained drops and saved fight summaries |
| Tamriel Trade Centre | Local market prices and inventory price associations |

Available data depends on which addons have recorded it. SavedVariables describe the last disk save; log in to relevant characters, visit storage and save again to refresh them.

## Documentation and examples

- [Usage guide](https://github.com/JuliusJacobsohn/EsoData.NET/blob/main/docs/usage.md): accounts, build editing and export, catalogs, crafting, collections and pricing.
- [Supported formats and data coverage](https://github.com/JuliusJacobsohn/EsoData.NET/blob/main/docs/formats.md).
- [Account and build architecture](https://github.com/JuliusJacobsohn/EsoData.NET/blob/main/docs/account-architecture.md).
- [Example programs](https://github.com/JuliusJacobsohn/EsoData.NET/tree/main/samples): addon inspection, catalogs, CSPS text, pricing and benchmarks.

## Development

```shell
dotnet build EsoData.sln -c Release
dotnet test EsoData.sln -c Release --no-build
```

Tests use synthetic fixtures; no game installation or private account files are required. CI runs on Windows and Linux. See [release instructions](https://github.com/JuliusJacobsohn/EsoData.NET/blob/main/docs/releasing.md) for packaging and publication.

Maintained by Julius Jacobsohn. Not affiliated with or endorsed by ZeniMax Media or Bethesda.

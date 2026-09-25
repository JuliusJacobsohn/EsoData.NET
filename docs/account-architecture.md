# Account-first architecture

Architecture decision, 2026-09-25. The account/build API is implemented in 1.1.0; this document also records the broader domain intent. Actual source coverage and limitations are described in the README. Compatibility with the former MCP projections is not a requirement.

## What the library is for

Load an ESO account, inspect it, change a proposed character configuration, calculate what that change requires, and export the supported parts. A consumer should not have to join addon records, translate IDs, interpret Lua, count skill points, or understand CSPS serialization.

The default public entry point loads one account on one server. A directory can contain several accounts; discovery returns identities, and loading requires an unambiguous identity. Account/server boundaries are never inferred from character display names alone.

## Object graph

```text
EsoAccount
  Identity (account + server)
  Characters[]
    Identity (game character ID + display name)
    Progress (level, skill-point total, skill lines, ability/morph XP,
              unlocks, research, recipes, motifs, scribing, riding, etc.)
    Build (attributes, skill purchases/passive ranks, bars, CP,
           equipment selections, consumables, supported other sections)
    Backpack
    SavedBuilds[]
    RecordedStatistics
  SharedStorage (bank, craft bag, observed housing/other storage)
  Collections
  Sources[] (including coverage and section scan times)
```

This is a target model, not a claim that current sources expose all these fields. Each adapter declares what it can provide. Implement typed domains when a source supports them; retain unknown IDs rather than rejecting a game update. No giant untyped Raw property is necessary for normal consumers. Parser diagnostics/raw inspection remain separate development facilities.

Models are ordinary mutable C# objects. Nested properties and collections can be edited directly. A deep copy creates an independent working account or build. Computed budget totals are derived rather than separately writable properties that can disagree.

Progress and allocation are different: knowing/leveling a skill or morph survives a respec; purchasing it and slotting it belong to the build. A skill-line rank does not establish the XP rank of every ability. Setting a desired morph never fabricates observed progression.

The current build and saved profiles use the same domain build representation. Importing a saved CSPS profile adds a saved build; it never establishes that the game applied it. Partial profiles carry explicit included sections, so an absent section does not become a reset.

Recorded statistics are measurements, not a full game simulator. Editing equipment or attributes does not magically recalculate measured DPS, resistance or health. Reports identify when a measurement no longer describes the proposed build. Derived facts such as set counts and point budgets are calculated normally.

## Ownership and item identity

- A physical stack belongs to one location. Account-wide inventory is a computed enumeration across locations, not another stored copy.
- Equipment links to an owned instance when identity is supplied, or carries a separately marked desired item specification. A catalog item ID is not an instance ID.
- Preserve instance IDs when available. Otherwise expose a source/location/slot reference valid for that load, without pretending it survives transfers. Match variants by relevant properties; never collapse two different enchantments or traits merely because item IDs match.
- A planned transfer changes the proposal, not observed ownership. Other-account bound items are unavailable; unknown binding is not transferable by assumption.
- A collection unlock and a physical owned item remain separate. Reconstruction availability depends on more than collection membership.
- Count active set pieces independently for each weapon bar, using resolved weapon metadata. Never count both weapon bars simultaneously.

## Source metadata without field wrappers

`Sources` is a central list of source documents with addon/provider, file-written time, read time, optional API version, diagnostics, and coverage entries. Coverage identifies character/location/section, scan time when supplied, completeness, and whether that source supplied the selected section. Ordinary fields do not carry `Observation<T>` wrappers.

One source file can include scans from different days. Its file-written time is not substituted for every character's scan time. Unknown scan times remain unknown. Multiple local files from one addon remain distinct sources.

Missing sections are nullable. A present complete empty section means observed empty. Partially observed sections are described by coverage. Within knowledge, known/unknown/not-known must remain distinguishable where the source permits it. There is no universal unknown wrapper around every integer or string.

## Loading and merging

1. Read each configured local source once per load with a small stable-file check.
2. Adapt source data into typed sections and coverage.
3. Resolve account and character identities using source mappings. Report unresolved identities; do not merge on a name guess.
4. Merge by coherent scope: character section or storage location. Prefer newer comparable scan data for overlapping complete scopes, then source capability/completeness and a deterministic provider preference for ties or incomparable timestamps. Document the decision centrally.
5. A complete snapshot replaces that scope, including removals. Do not union old and new inventory and resurrect sold items. Partial data fills disjoint known scopes; conflicts remain diagnostics rather than invented certainty.
6. Keep allocations/bars from one coherent build observation when available. Supplement genuinely missing sections from another source with visible mixed-source coverage; do not manufacture a supposedly simultaneous snapshot.
7. Resolve referenced definitions through a separate catalog and compute useful account/build views.

The library owns these decisions. MCP, a website and a console program must receive the same account from the same inputs. Consumers can choose source preferences without implementing their own mergers.

## Catalogs and identifiers

Definitions are shared catalog data, independent of account ownership. Local JSON/catalog providers remain replaceable and refreshable; licensed redistribution is a separate decision. Account loading does not need a network connection.

An active skill has a stable semantic identity plus selected morph, progression and allocation. Catalog adapters retain the mappings between ability ranks, base abilities, crafted abilities, champion stars and runtime class-line identifiers. Those identifiers are not interchangeable numeric values. Small distinct ID types are justified where confusing them changes behavior; thousands of constants are not.

Resolve human-readable selectors once. Exact unambiguous names are convenient; ambiguous names return a short candidate list. Unknown mappings produce a targeted diagnostic. No name matching by taking the first substring result. Definitions missing from local catalogs can be fetched explicitly in a batch by the host and passed to the library.

## Editing, analysis and exporting

Illustrative API shape, intentionally not the existing API:

```csharp
var account = AccountLoader.Load(sources, accountIdentity, catalog);
var proposed = account.DeepClone();
var character = proposed.Characters.Single(c => c.Id == characterId);

character.Build.Attributes.Magicka = 64;
character.Build.Attributes.Health = 0;
character.Build.Attributes.Stamina = 0;
character.Build.Bars.Front.Ultimate = catalog.Skills.Resolve("Undo");

var report = BuildAnalysis.Compare(account, proposed, characterId, catalog);
var export = CspsExporter.Export(character.Build,
    sections: BuildSections.Skills | BuildSections.Bars | BuildSections.Attributes);
```

The load/copy/edit/compare/export path is the main sample. No database, MCP, event bus, repository abstraction or background refresh is required by the library.

Analysis returns structured facts and diagnostics:

- Incremental purchase cost, refundable points, full-respec cost, total available points and remaining points. These are distinct values.
- Ability/morph unlock and XP requirements, rank limits, chosen class lines, bar/ultimate compatibility, and missing catalog evidence.
- CP allocations per discipline, known prerequisites and caps, slottable status, slots and unspent budgets. Matching only the grand total is insufficient.
- Equipment coverage and per-bar set counts; exact owned matches, transfer candidates, alternatives, missing pieces and unknown properties.
- Crafting feasibility for a selected crafter: research/recipe/style knowledge, materials and exact quantities, improvement passives, target quality, glyphs and craft level where supported by catalog data. Unknown recipes/material rules produce unknown requirements, never invented quantities.
- Target-vs-current differences and relevant leveling candidates. A planner supplies priorities; the library filters and counts using observed progression. It does not turn an arbitrary recommendation into a game rule.

Validation distinguishes malformed/unsupported export data, known unmet requirements, and unknown requirements. Future target builds may intentionally contain unmet requirements. A ready-now export reports those gaps and cannot label them fully applicable. Respeccing uses the total budget, not just currently unspent points. A stored build target does not spend game points.

Exports explicitly select sections. Omitted means untouched; an included empty section means the format's explicit clear behavior, where supported. Unsupported sections produce a diagnostic. Canonical native CSPS text is the default; ESO-Hub is an explicitly selected alternative. Item-link/glyph encoders and equipment-slot conversion live here, not in MCP.

Format validation and round-trip checks are not an in-game application test. Comparing a later observed account to a saved proposal yields applied/different/unobserved per included section.

## Freshness and storage

Default: reload local files for each requested account operation. No account cache, file watcher, projection database or refresh scheduler. An operation loads once and runs all its queries against that object. Remote catalog downloads are a separate explicit update operation, not repeated with every request.

The host may persist both refreshed accounts and authored builds as JSON in SQLite. These are separate records: a refresh never overwrites a plan. The MCP does this on every relevant request. SQLite is persistence, not a prerequisite for the library or a reason to hide the typed graph. Explicit offline usage retains original source timestamps and is not silently substituted when live loading fails.

On 2026-09-25 the read-only benchmark on the developer's local data measured all six account readers at 75.879 ms median (seven samples after a warmup), LibSets separately at 23.372 ms, and CSPS text round-tripping all saved profiles at 0.007 ms. The existing forced MCP refresh took 3.362 s including its additional work. These are different workloads; the comparison does not isolate SQLite overhead or benchmark the future merger. They justify measuring a direct loader before adding caching.

Reproduce with `dotnet run --project samples/EsoData.Benchmark -c Release -- <SavedVariables> <AddOns>`. Build/startup time is excluded; files are likely in the OS filesystem cache; the benchmark does not change sources or print account data.

## Implementation order and acceptance

1. Account graph + adapters + deterministic merging. Verify account isolation, complete-scope removals, duplicate inventories, mixed timestamps and missing sections.
2. Shared build model + catalog resolution + clone/edit + native CSPS export. Verify the prior base-ID/morph/subclass failure cases with synthetic fixtures and independently reviewed format expectations.
3. Structured build comparison/validation. Cover respec vs incremental budgets, full requested bars, per-discipline CP and unavailable morphs.
4. Equipment/crafting comparison with explicit unknown requirements. Cover requested purple quality without gold improvement materials and separate glyph identity/quality.
5. Apply verification and requirement progress for named targets. Include draft future requirements without claiming observed unlocks.

Keep code files small and domain-specific. Tests cover real boundary failures, not every property getter. No pinned game build numbers. No dual legacy/new architecture retained indefinitely; breaking releases and updated consumers are acceptable.

## Out of scope

A full combat simulator, guaranteed optimal builds, general website scraping, automatic in-game actions, automatic GitHub changes, market-price predictions, and a custom bridge addon. Missing source capabilities can motivate adapters later; the graph must not pretend those capabilities exist today.

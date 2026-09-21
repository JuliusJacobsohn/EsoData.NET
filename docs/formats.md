# Formats and data boundaries

The implementation was checked against installed addon serializers and upstream source on 2026-09-21. Dates/API numbers below describe the inspection, not required versions. No game-content tables are embedded.

## Source entry points

| Source | Entry point |
|---|---|
| IIfA | Installed `IIfA` source: `IIFA_DATABASE[account].servers[server].DBv3`, `CharIdToName`; [addon project](https://www.esoui.com/downloads/info851-InventoryInsight.html) |
| LCK | [Author's SavedVariables documentation](https://eso.code65536.com/addons/libcharacterknowledge/sv.html); installed `Internal.lua`, `Public.lua`, `LibCodesCommonCode.lua` |
| LMAS | [Author's addon release](https://www.esoui.com/downloads/info2843-LibMultiAccountSets.html); `Internal.lua`, `Public.lua`, `LibCodesCommonCode.lua` |
| CSPS | [Addon project](https://www.esoui.com/downloads/info2901-CarosSkillPointSaver.html); `csps_external.lua`, `CarosSkillPointSaver.lua`, `csps_gear.lua`, `csps_cp2.lua` |
| Lazy Set Crafter | [Addon project](https://www.esoui.com/downloads/info1697-DolgubonsLazySetCrafter.html); `Crafter.lua`, `Mail.lua` |
| LibSets | [Maintainer's repository](https://github.com/Baertram/LibSets); `Data/LibSets_Data_SetNames.lua`, `Data/LibSets_Data_SetItemIds.lua` |
| UESP observations | [uespLogCharData.lua](https://github.com/uesp/uesp-esoapps/blob/master/uespLog/uespLogCharData.lua) |
| UESP catalog JSON | [exportJson.php](https://github.com/uesp/uesp-esolog/blob/master/exportJson.php), [schema fields in parseLog.php](https://github.com/uesp/uesp-esolog/blob/master/parseLog.php) |
| Optional future archive extraction | [UESP's extractor](https://github.com/uesp/uesp-esoapps/tree/master/EsoExtractData), a separate concern from addon save parsing |

## Lua and item links

`SavedVariables.Parse` handles global data assignments, nested tables, string/integer keys, implicit arrays, booleans, nil, numbers, escaped UTF-8 strings, long strings and comments. It does not evaluate functions, identifiers as values, concatenation or arbitrary Lua expressions. `Raw` exposes fields not modeled by a reader. Unsupported executable addon source is not a SavedVariables document.

`ItemLink` preserves all numeric payload fields, link style and label; extra future fields survive round trips. Character/item-instance IDs retain 64-bit precision. The link's item ID is not a set ID. Trait and equipment identity require metadata; level and quality alone do not identify a craftable piece.

## Knowledge and collections

LCK uses LibCodesCommonCode's alphabet `0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz#%`. Integer fields are big-endian base 64. Knowledge bits are high-bit-first within each six-bit character. This is not MIME Base64.

Recipes/plans/motifs map bit positions through **the same save's** master lists and saved `fieldSize`. Scribing positions are actual grimoire/script IDs; zero source-item entries preserve holes. Missing bits are unavailable, not false. Numeric chunks are joined in order, with gaps rejected because they would shift every subsequent ID.

Research `rt` contains trait flags, research slot counts, then ten-character timer records. The saved diagnostics provide the trait count; no fixed 324-trait database is compiled in. Timers retain duration and remaining-at-scan time. `ResearchTimer.Remaining` may return a negative duration: that does not confirm completion was observed.

**Research mapping gap:** global trait indexes are created by the game API's crafting/line/trait iteration order, which is not serialized. A provider can populate `GameCatalog.ResearchTraits` and `ResearchSignature`. The consumer must ensure this describes the scanned layout before resolving indexes; the library does not automatically join an arbitrary catalog to LCK.

LMAS Data2 encodes a timestamp followed by six-character (36-bit) masks, with entry `setId + 1` representing a set. Masks use the game's collection slot flags. They are not indexes into a list of items. `IsCollected(setId, slotMask)` requires the real mask. `GameCatalog.CollectionPieces` is an optional place for externally exported piece/slot mappings. Missing accounts/fields return no observation; explicit zero masks are retained, even for IDs that a catalog may identify as uncollectible.

## Native CSPS

Nine `#` sections: `skills#hotbars#attributes#mundus#cp#gear#quickslots#outfit#role`. Empty or `-` top-level sections are skipped by the importer; they do not universally clear a category. Import checkboxes also affect application.

Skills contain five `*` sections: active `abilityId:morph`, passive `abilityId:rank`, crafted `craftedId:script1:script2:script3`, styles `abilityId:collectibleId`, subclasses (skill-line IDs). Active IDs are rank one of the selected morph. Hotbars instead use unmorphed rank-one IDs, or `c<craftedId>` for crafted skills. Bars have six slots, with `-` empty. A third bar is supported.

Attributes are health/magicka/stamina. CP allocations are `id-points` separated by semicolons, followed by `*` and three four-slot bars. Gear has sixteen positional slots: head, shoulders, chest, hands, waist, legs, feet, neck, ring1, ring2, main hand, off hand, backup main, backup off, poison, backup poison. Normal gear is `setId:type:trait:quality:enchantmentEffectId`. Poison and Mara entries have separate shapes. No item level is encoded.

Quickslots and outfits remain raw because their action types and appearance IDs have separate semantics. Extra sections are preserved. Typed property getters decode a section when accessed; retaining an unknown raw section does not guarantee the current typed getter understands it. Reassigning a modeled section uses the supported grammar.

Saved profiles use `werte`, `comp1` and `comp2`, not this pasted layout. `CspsReader` reconstructs native text, preserves equipment instance IDs separately, and handles legacy field fallbacks. History and unmodeled settings remain in `Raw`.

## ESO-Hub

Fifteen semicolon fields: class, races, role, attributes, curse, Mundus, subclass lines, front bar, back bar, extra abilities, slotted CP, other CP, gear, food, potions. The leading number is **class ID**, not a format version.

Bars use selected-morph IDs. Crafted bars use a representative ability ID plus three scripts, unlike native CSPS's crafted ID. Passive entries express maximum rank, not partial ranks. Gear specifies API equip-slot IDs and glyph item IDs; it omits level and quality. CP import in CSPS can add prerequisite points, so encoded spending is not necessarily final spending.

Parsing a URL reads and decodes only the `addondata` parameter; unrelated query parameters and fragments are ignored. Native↔Hub conversion is deliberately not implicit: it requires identity mappings and can lose information.

## Catalogs and update strategy

`LibSetsCatalog.Read` extracts two literal tables from the installed addon without running its Lua. Compressed item range `"100,2"` expands to `100,101,102`. This provides localized set names and membership, not complete item statistics, traits, bonuses or collection-piece masks.

`UespCatalog.Parse` imports `minedItemSummary` and `minedSkills` JSON arrays, accepting numeric strings. It maps identity, names, types, traits and skill rank/morph/base metadata. HTTP, retries, endpoint credentials and cache policy belong to callers. No live UESP service is required by the package; endpoint access was not verified as available for a production consumer.

`GameCatalog` is plain JSON containing sources, items, skills, sets and optional collection/research mappings. Reload or replace it after addon/data updates. A future game-API exporter or game-file extractor can produce this shape without changing the existing readers. Unknown top-level JSON properties are retained. Schema and catalog-content versioning remain independent of ESO API numbers.

## Validation scope

Automated tests use synthetic IDs and records, not usable build recommendations. Real local IIfA, LCK, CSPS, Lazy Set Crafter and LibSets files were also read successfully during initial development; private observations are not published. LMAS and uespLog coverage is based on upstream serializers and synthetic fixtures. In-game application/crafting is outside these tests.

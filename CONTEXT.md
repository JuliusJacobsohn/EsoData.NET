# ESO addon data

Characters, ownership, progression and build configurations represented by ESO addon data.

## Language

**Account**: One ESO account's characters and shared possessions on one server. Another account or server is a separate ownership pool.

**Character**: An individual game character identified within its account/server. Its display name is not its identity.

**Progression**: A character's earned levels, skill and morph progress, unlocks and crafting knowledge. Progression is distinct from how its points are currently allocated.

**Build**: A configuration of skill purchases, passive ranks, bars, attributes, champion points, equipment and consumables. The same configuration can describe an observed setup or a proposal; a proposal does not establish that it has been applied.

**Target**: A desired build and its explicit requirements, including any alternatives and optional branches.

**Source coverage**: The characters, storage locations and data sections a source observed, including their available scan times and completeness. A recently saved source need not have recently observed every character.

**Item instance**: A particular owned item or stack at a location. Its catalog definition is not its identity, and a collection unlock is not an owned instance.

**Catalog**: Shared definitions of game items, sets, skills and their identifiers, independent of any account's possessions or progression.

**Observation**: State recorded by an addon at an available timestamp. It does not establish current live state.

**Research summary**: Observed known/total trait counts by craft and research line, display labels, available slots and active timers. A timer expiring does not establish confirmed knowledge, and display labels are not numeric trait IDs.

**Champion state**: Observed spent/unspent points, purchased stars and slot assignments. A champion skill ID and its underlying ability ID are different identities.

**Character statistics**: Observed game statistics and addon computations, separated by current and saved bar contexts. Values retain source units and are not guaranteed to describe an unbuffed character.

**Skill line rank**: A saved progression rank for a skill line. It does not establish that all abilities in that line are purchased.

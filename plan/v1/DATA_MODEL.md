# Authoritative Data Model

Status: scaffold

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [WORLD vision](../../vision/WORLD.md), [ORGANISMS vision](../../vision/ORGANISMS.md), and [NUTRIENTS vision](../../vision/NUTRIENTS.md).

# Purpose

Define the canonical state representation shared by simulation systems without coupling it to network or save-file schemas.

# Identity model

Define strongly typed, stable identifiers for at least:

- World and rules version.
- Tile and grid coordinate.
- Species and lineage event.
- Organism and dead remnant.
- Player/controller assignment.
- Trait and trait-family definitions.
- Resource, reservoir, reaction, and flow category.
- Simulation event and command.
- Abiogenesis origin and player-knowledge record.

Identifiers must survive array compaction, save/load, and client resynchronization. The detailed plan must decide identifier width, allocation strategy, reuse policy, and whether IDs encode type or world information.

# State ownership

| State | Canonical owner |
| --- | --- |
| Fixed geography and climate baselines | Tile/world state |
| Current environment and resource pools | Tile state |
| Atmospheric gases | Tile-local reservoirs |
| Gas-source and sink remainders | Tile state |
| Signed gas-exchange remainders | Canonical undirected tile-edge state |
| DNA and mutation balance | Species state |
| Position, structural matter, reserves, age, and behavior | Organism state |
| Remaining consumable contents | Dead-remnant state |
| Parent-child relationships | Lineage store |
| Controller and sandbox lock | Gameplay state |
| Discovered tiles, last observations, and observation timestamps | Per-actor knowledge state |
| Seed, tick, calendar, deadline, rules version | World state |

# Stored versus derived data

The detailed plan should classify every field as:

- Authoritative stored state.
- Derived each tick or on demand.
- Cached with an invalidation rule.
- Historical aggregate.
- Presentation-only state that must not enter the simulation.

Likely derived values include relative health, species population, species average health, tile/species reproductive trend, and some environmental values. Whether to cache them depends on computation cost and replay/version behavior.

Live visibility is derived from the completed world's controlled-species occupancy. Discovery and last-known observations are stored authoritative state because they persist after occupation ends and across sandbox control transfers. A save must preserve at least the discovered tile set, last full observation allowed for each previously live tile, its observation tick, and any coarse neighbor summary fixed at discovery. Client selection and camera state remain presentation-only.

Lineage must distinguish `AbiogenesisOriginId` from `SpeciesId`. Root species reference their origin and have no parent species; non-root species reference exactly one parent species. An origin never carries DNA, population, mutation points, control, or extinction state.

# Numeric representation

The [resource model](RESOURCE_MODEL.md) and [range proof](RESOURCE_CALIBRATION.md) fix authoritative matter and energy storage as nonnegative signed 64-bit game-native integer quanta with checked 128-bit intermediate arithmetic. Resource definitions provide integer CHNOPS composition vectors, and energy-bearing resources provide integer energy density. State mutation must reject overflow, underflow, and the configured `10^17` account ceiling rather than silently clamp, wrap, or introduce floating-point tolerance into conservation.

The detailed pass must still decide and document:

- Concentration versus absolute quantity for tile pools.
- Within-tile coordinate, velocity, and interaction-distance units.
- Temperature, elevation, water depth, moisture, and insolation units.
- Exact fixed-point encodings for rates, ratios, and probabilities.
- Remainder accumulation and named rounding rules.
- Numeric choices for non-resource domains.

# Data-oriented layout

Explore a tile-partitioned layout with dense organism columns rather than one deeply referenced object graph. The detailed plan should compare:

- Array-of-structures and structure-of-arrays layouts.
- Stable IDs plus dense slots and an ID-to-slot index.
- Moving organisms between tile-owned collections.
- Tombstones versus swap removal.
- Species lookup and spatial-neighbor indexing.
- Snapshot-friendly read models.

# Event model

Classify events by purpose:

- Authoritative state-transition events.
- Replay inputs and command results.
- Resource-ledger transactions.
- Historical/analytical events such as death causes and speciation.
- Ephemeral client notifications.
- Metrics that should be aggregated rather than retained individually.

# Pseudocode to add

```text
AllocateStableId(entityType)
ResolveIdToDenseSlot(id)
MoveOrganismBetweenTiles(organismId, sourceTile, targetTile)
RemoveOrganismAndCreateRemnant(organismId, deathRecord)
BuildImmutableReadSnapshot(completedTick)
```

# Required decisions

- [ ] Canonical ID representation and allocation.
- [x] Integer resource scale and range proof; see [RESOURCE_CALIBRATION.md](RESOURCE_CALIBRATION.md).
- [ ] Dense storage and indexing strategy.
- [ ] Stored/derived/cached field inventory.
- [ ] Event retention categories.
- [ ] Actor knowledge, observation snapshot, and visibility-state schemas.
- [ ] Abiogenesis-origin representation and root-species invariants.
- [ ] Spatial indexing required within a tile for v1.
- [ ] Maximum expected counts used to size and benchmark structures.

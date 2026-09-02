# Authoritative Data Model

Status: first ownership and numeric-contract pass; identifiers, dense layout, knowledge schemas, and event retention pending

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
| DNA, mutation balance/remainder, speciation cooldown/ordinal, pressure state, and autonomous intent including its material/biological-opportunity snapshot | Species state |
| Position, structural matter, charged reserves, spent reserve-carrier balances, available-store resource balances, metabolic binding cohorts, age, reproduction cooldown/ordinal, and behavior | Organism state |
| Tile, position, original packing profile, and remaining consumable contents | Dead-remnant state |
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

Likely derived values include relative health, species population, species average health, tile/species reproductive trend, and some environmental values. Health may be cached and projected to clients, but save/load and replay must reproduce it from authoritative organism state, compiled DNA, and environment as defined in [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md).

The logical organism schema, chronological-versus-biological age distinction, and condition-cache invalidation inputs are also defined in [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md). Current environmental stress is derived rather than durable organism state unless a future mechanic adds a specifically named accumulated condition.

Available-store resource balances are authoritative. Used load per capacity group is composition-derived and may either be recomputed or cached with invalidation on every balance mutation; DNA-compiled group capacities and desired-inventory policies are immutable for an organism between DNA/lifecycle transitions. Persisted saves must not rely on an unverified cached used-load value. The capacity groups, load formula, and transition invariants are defined in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).

For organisms with `CatalyticCarrierRetention`, charged `ReserveOrganic` and zero-energy `SpentReserveCarrier` balances are separate authoritative columns sharing one compiled capacity. Exact spend, recharge, assimilation, and inheritance rules are defined in [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md).

Live visibility is derived from the completed world's controlled-species occupancy. Discovery and last-known observations are stored authoritative state because they persist after occupation ends and across sandbox control transfers. A save must preserve at least the discovered tile set, last full observation allowed for each previously live tile, its observation tick, and any coarse neighbor summary fixed at discovery. Client selection and camera state remain presentation-only.

Lineage must distinguish `AbiogenesisOriginId` from `SpeciesId`. Root species reference their origin and have no parent species; non-root species reference exactly one parent species. An origin never carries DNA, population, mutation points, control, or extinction state.

The first species-evolution and lineage records are defined in [EVOLUTION.md](EVOLUTION.md). Immutable genomes may be deduplicated by canonical trait set and rules hash, but species and lineage events never deduplicate. Mutation balance uses signed 64-bit `MutationQ` at one million units per point; its exact income remainder is authoritative unsigned 128-bit state. Saves and hashes also include the absolute speciation cooldown, evolution revision, speciation ordinal, next autonomous-evaluation tick and ordinal, pressure accumulators, and any autonomous intent with its initial material- or biological-opportunity and per-plan breakdown.

For every exact `ResourceId` referenced by a selectable material-opportunity profile, tile history retains one-hour source, passive-loss, inbound-exchange, outbound-exchange, and biological-uptake buckets across the 168-hour autonomous scoring window, plus checked rolling totals. These aggregates are historical state, not client telemetry reconstructed from visible deltas. Missing world-start prehistory is zero-filled, and ring position, buckets, totals, and save/load behavior must reproduce future eviction and opportunity scores exactly; see [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md).

Selectable predation profiles add a sparse per-tile, ordered species-pair 168-hour ring containing eligible detections/contacts, attempts, successes, prey deaths, immediate resource-class grants, and remnant remainder. Only nonzero pairs are materialized, but their buckets, rolling totals, and cursors are historical state until the final nonzero hour ages out. These aggregates drive `BiologicalOpportunityProfile` scoring and must not be reconstructed from client-visible events; see [PREDATION.md](PREDATION.md).

# Numeric representation

The [resource model](RESOURCE_MODEL.md) and [range proof](RESOURCE_CALIBRATION.md) fix authoritative matter and energy storage as nonnegative signed 64-bit game-native integer quanta with checked 128-bit intermediate arithmetic. Resource definitions provide integer CHNOPS composition vectors, and energy-bearing resources provide integer energy density. State mutation must reject overflow, underflow, and the configured `10^17` account ceiling rather than silently clamp, wrap, or introduce floating-point tolerance into conservation.

The shared `RatioQ` encoding for health, normalized factors, and probabilities is provisionally fixed at one million units per `1.0`, with checked integer arithmetic and named rounding; see [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md).

The first world contract selects signed integer tile coordinates, normalized unsigned tile-local `LocalCoordQ`, integer-meter elevation/depth, milli-degree Celsius temperature, integer micrometers-per-hour precipitation, and `RatioQ` moisture/cloud/turbidity/volcanism/insolation. The default grid is `32 × 17`, with wrapped `x` and bounded signed `y`; see [WORLD_AND_CLIMATE.md](WORLD_AND_CLIMATE.md).

Organism and remnant radius are derived from assigned geometric structure/body-equivalent matter, compiled organization, and the pinned spatial rule pack rather than stored as freely mutable geometry. Organism structure remains one conserved resource balance with explicit geometric and organization assignments; see [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md). The first founder radius, structure targets, Brownian scale, and range encodings are defined in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).

Completed movement and migration events retain separately attributed active, Brownian-like passive, and directional environmental displacement components plus the admitted crossing class. The passive component records the already DNA-modified realized vector; its profile and multiplier are derived from the species' compiled DNA rather than stored redundantly on each organism. Directional environmental displacement is identically zero in the v1 rules, but reserving the field prevents later current-, wind-, buoyancy-, or propagule-driven dispersal from being encoded as active velocity or an unexplained tile teleport. The organism itself remains the conserved moving entity; displacement never copies biological state.

The detailed pass must still decide and document:

- Concentration versus absolute quantity for tile pools.
- Final configured bounds for the signed per-hour velocity and interaction-distance encodings built over normalized `LocalCoordQ`; the first logical encoding and geometry contract are defined in [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md).
- Exact fixed-point encodings for rates and non-health normalized domains that cannot use `RatioQ`.
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
- Historical/analytical events such as death-risk profiles, realized death triggers, and speciation.
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
- [x] First organism stored/derived/cached field inventory and health contract; see [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md). Other entity inventories remain open.
- [x] Founder available-store balance ownership, capacity-group load derivation, and DNA-compiled baseline limits; see [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- [ ] Event retention categories.
- [ ] Actor knowledge, observation snapshot, and visibility-state schemas.
- [ ] Abiogenesis-origin representation and root-species invariants.
- [x] First logical within-tile spatial index: derived `16 × 16` bins for organisms and remains with exact-distance filtering and stable ordering; see [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md). Dense physical layout remains open.
- [ ] Maximum expected counts used to size and benchmark structures.

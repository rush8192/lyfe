# Authoritative Data Model

Status: first identity, ownership, numeric, transactional-page, and dense-layout pass decided; physical sizes, knowledge schemas, and event retention pending

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [WORLD vision](../../vision/WORLD.md), [ORGANISMS vision](../../vision/ORGANISMS.md), [NUTRIENTS vision](../../vision/NUTRIENTS.md), [rule-pack authoring and compilation](RULE_PACK_AUTHORING_AND_COMPILATION.md), [moddability](MODDABILITY.md), [keyed randomness](KEYED_RANDOMNESS.md), and [deterministic parallel execution](DETERMINISTIC_PARALLEL_EXECUTION.md).

# Purpose

Define the canonical state representation shared by simulation systems without coupling it to network or save-file schemas.

# Identity model

The canonical representation and allocation rules are defined in [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md). In summary, hot simulation entities use strongly typed opaque world-local `uint64` IDs allocated monotonically per entity type by canonical owner-thread commit; tiles and compiled definitions use strongly typed `uint32` IDs; server/application identities use opaque 128-bit values. Definition IDs and stable keys are explicitly assigned in typed active/tombstoned registries as defined in [RULE_PACK_AUTHORING_AND_COMPILATION.md](RULE_PACK_AUTHORING_AND_COMPILATION.md). Zero is reserved, IDs are never reused, and dense slots never escape storage.

Stable identifiers exist for at least:

- World, base-rule-pack, canonical mod-set, selected world-pack/profile/options, and final compiled-rules identity.
- Tile and grid coordinate.
- Species and lineage event.
- Organism and dead remnant.
- Player/controller assignment.
- Trait and trait-family definitions.
- Resource, reservoir, reaction, and flow category.
- Simulation event and command.
- Abiogenesis origin and player-knowledge record.

Identifiers survive array compaction, save/load, and client resynchronization. Runtime IDs encode no type, world, tile, species, time, or slot; the strong field type or explicit generic-reference kind supplies type, while enclosing state supplies world context. Next-ID counters are authoritative state. Parallel evaluators emit creation intents, and canonical commit order assigns IDs.

# Random compatibility state

Each world stores a 128-bit root seed, pinned RNG algorithm ID, and RNG schema version. The authoritative simulation has no mutable global or per-worker random cursor. Stable domain IDs plus tick/entity/definition coordinates address draws, while subsystem-owned ordinals persist only for repeated semantic events that a tick cannot uniquely name. See [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md).

# State ownership

| State | Canonical owner |
| --- | --- |
| Fixed geography and climate baselines | Tile/world state |
| Current environment and resource pools | Tile state |
| Atmospheric gases | Tile-local reservoirs |
| Gas-source and sink remainders | Tile state |
| Signed gas-exchange remainders | Canonical undirected tile-edge state |
| DNA, mutation balance/remainder, speciation cooldown/ordinal, pressure state, and autonomous intent including its material/biological-opportunity snapshot | Species state |
| Position, structural matter and its geometric/organization/storage assignments, charged reserves, spent reserve-carrier balances, available-store resource balances, metabolic binding cohorts, age, reproduction cooldown/ordinal, selected behavior/target/dwell, and enabled behavior-pressure memory | Organism state |
| Tile, position, original packing profile, and remaining consumable contents | Dead-remnant state |
| Parent-child relationships | Lineage store |
| Controller and sandbox lock | Gameplay state |
| Discovered tiles, last observations, and observation timestamps | Per-actor knowledge state |
| 128-bit root seed, RNG algorithm/schema IDs, tick, calendar, deadline, base-pack/mod-set/world-pack/profile/options/final-rules identities, and certification class | World state |

# Stored versus derived data

Every field is classified as primary authoritative state, immutable compiled state, materialized current state, phase materialization, rebuildable structural index, historical aggregate, or presentation-only state. [MATERIALIZED_DERIVED_STATE.md](MATERIALIZED_DERIVED_STATE.md) defines the single-source rule: a gameplay-relevant formula has one owner, is evaluated at one named barrier when dependencies change, stores its result, and is never independently reproduced by consumers.

Relative health, species population/average health, current behavior distributions, reproductive trend, used storage load, commissioned capacity, stored usable energy, body radius, age throughput, active capability state, current tile conditions, tile access/opportunity values, and similar repeatedly consumed numbers are materialized derived state. They are not independently writable; their completed runtime values are stored, hashed, and diffed. Pure values may be rebuilt by their sole owner before a load completes, while histories, remainders, moving averages, and other information-bearing recurrence state are primary and saved. Debug recomputation remains a validation oracle during normal execution.

The logical organism schema, chronological-versus-biological age distinction, and condition-materialization inputs are also defined in [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md). Current environmental stress is a materialized condition assessment rather than a primary accumulated injury; a future persistent poisoning/injury mechanic must add specifically named concrete state.

An organism with a history-using behavior retains the fixed-point recent-energy-coverage and recent-acquisition-coverage moving averages defined in [BEHAVIOR_AND_RESOURCE_PRESSURE.md](BEHAVIOR_AND_RESOURCE_PRESSURE.md). The behavior owner materializes composite pressure, channel deficits, utilities, and the chosen next-tick directive once at its named barrier; completed outputs that affect the next tick are stored, while discarded candidate scratch is not. Optional behavior-memory columns exist only for organisms whose compiled DNA uses them, but slot migration, save/load, and speciation preserve the logical values exactly.

Available-store resource balances are primary authoritative state. Used load per capacity group is a materialized derived value updated by the owning balance mutator in the same commit, or by a mandatory barrier before any capacity consumer. DNA-compiled group capacities and desired-inventory policies are immutable between DNA/lifecycle transitions. Saves may omit used-load caches because the sole capacity owner can rebuild the complete table before load validation/publication; a serialized compatible cache must match that result. The capacity groups, load formula, and transition invariants are defined in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).

For organisms with `CatalyticCarrierRetention`, charged `ReserveOrganic` and zero-energy `SpentReserveCarrier` balances are separate authoritative columns sharing one currently commissioned capacity. The maximum and storage-structure target are DNA-compiled; current capacity is derived from authoritative structural assignments and organization activation. Exact commissioning is defined in [ENERGY_STORAGE.md](ENERGY_STORAGE.md), while spend, recharge, assimilation, and inheritance rules are defined in [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md).

Live visibility is derived from the completed world's controlled-species occupancy. Discovery and last-known observations are stored authoritative state because they persist after occupation ends and across sandbox control transfers. A save must preserve at least the discovered tile set, last full observation allowed for each previously live tile, its observation tick, and any coarse neighbor summary fixed at discovery. Client selection and camera state remain presentation-only.

Current behavior distributions are materialized once from organism behavior after phase 9 and stored with the completed species/tile aggregate generation. A last-observed per-tile distribution retained after a tile becomes reduced is actor knowledge and persists with its observation tick. Neither current nor retained aggregates are permitted inputs to individual behavior; the aggregation direction is organism state to observation only.

Lineage must distinguish `AbiogenesisOriginId` from `SpeciesId`. Root species reference their origin and have no parent species; non-root species reference exactly one parent species. An origin never carries DNA, population, mutation points, control, or extinction state.

The first species-evolution and lineage records are defined in [EVOLUTION.md](EVOLUTION.md). Immutable genomes may be deduplicated by canonical trait set and rules hash, but species and lineage events never deduplicate. Mutation balance uses signed 64-bit `MutationQ` at one million units per point; its exact income remainder is authoritative unsigned 128-bit state. Saves and hashes also include the absolute speciation cooldown, evolution revision, speciation ordinal, next autonomous-evaluation tick and ordinal, pressure accumulators, and any autonomous intent with its initial material- or biological-opportunity and per-plan breakdown.

For every exact `ResourceId` referenced by a selectable material-opportunity profile, tile history retains one-hour source, passive-loss, inbound-exchange, outbound-exchange, and biological-uptake buckets across the 168-hour autonomous scoring window, plus checked rolling totals. These aggregates are historical state, not client telemetry reconstructed from visible deltas. Missing world-start prehistory is zero-filled, and ring position, buckets, totals, and save/load behavior must reproduce future eviction and opportunity scores exactly; see [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md).

Every terrestrial tile retains a 48-hour `RatioQ` surface-moisture ring plus two checked rolling 24-hour totals. World moisture spin-up initializes the negative-hour samples ending at tick `-1`; gameplay does not zero-fill this condition history. Ring position, samples, and totals are authoritative historical state and persist through save/load. They are shared tile history, not duplicated per organism and not derived from what a client has discovered. The small v1 grid allocates the ring eagerly so acquiring `MoistureConservation` later never lacks prior observations. See [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md).

Selectable predation profiles add a sparse per-tile, ordered species-pair 168-hour ring containing eligible detections/contacts, attempts, successes, prey deaths, immediate resource-class grants, and remnant remainder. Only nonzero pairs are materialized, but their buckets, rolling totals, and cursors are historical state until the final nonzero hour ages out. These aggregates drive `BiologicalOpportunityProfile` scoring and must not be reconstructed from client-visible events; see [PREDATION.md](PREDATION.md).

# Numeric representation

The [resource model](RESOURCE_MODEL.md) and [range proof](RESOURCE_CALIBRATION.md) fix authoritative matter and energy storage as nonnegative signed 64-bit game-native integer quanta with checked 128-bit intermediate arithmetic. Resource definitions provide integer CHNOPS composition vectors, and energy-bearing resources provide integer energy density. State mutation must reject overflow, underflow, and the configured `10^17` account ceiling rather than silently clamp, wrap, or introduce floating-point tolerance into conservation.

The shared `RatioQ` encoding for health, normalized factors, and probabilities is provisionally fixed at one million units per `1.0`, with checked integer arithmetic and named rounding; see [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md).

The first world contract selects signed integer tile coordinates, normalized unsigned tile-local `LocalCoordQ`, integer-meter elevation/depth, milli-degree Celsius temperature, integer micrometers-per-hour precipitation, and `RatioQ` moisture/cloud/turbidity/volcanism/insolation. The default grid is `32 × 17`, with wrapped `x` and bounded signed `y`; see [WORLD_AND_CLIMATE.md](WORLD_AND_CLIMATE.md).

Organism and remnant radius are derived from assigned geometric structure/body-equivalent matter, compiled organization, and the pinned spatial rule pack rather than stored as freely mutable geometry. Organism structure remains one conserved resource balance with explicit geometric, organization, and storage assignments; storage is non-geometric and commissions reserve capacity rather than radius. See [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md) and [ENERGY_STORAGE.md](ENERGY_STORAGE.md). The first founder radius, structure targets, Brownian scale, and range encodings are defined in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).

Completed movement and migration events retain separately attributed active, Brownian-like passive, and directional environmental displacement components plus the admitted crossing class. The passive component records the already DNA-modified realized vector; its profile and multiplier are derived from the species' compiled DNA rather than stored redundantly on each organism. Directional environmental displacement is identically zero in the v1 rules, but reserving the field prevents later current-, wind-, buoyancy-, or propagule-driven dispersal from being encoded as active velocity or an unexplained tile teleport. The organism itself remains the conserved moving entity; displacement never copies biological state.

The detailed pass must still decide and document:

- Concentration versus absolute quantity for tile pools.
- Final configured bounds for the signed per-hour velocity and interaction-distance encodings built over normalized `LocalCoordQ`; the first logical encoding and geometry contract are defined in [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md).
- Exact fixed-point encodings for rates and non-health normalized domains that cannot use `RatioQ`.
- Remainder accumulation and named rounding rules.
- Numeric choices for non-resource domains.

# Data-oriented layout

The first implementation uses tile-partitioned, lazily allocated, chunked structure-of-arrays stores for organisms and remnants, dense resource-major tile matrices, field-major tile condition columns, ordinary dense species stores, domain-shaped immutable genomes plus typed compiled phenotypes, and append-only lineage/events. Stable-ID-to-slot lookup begins with standard typed dictionaries rather than object references. Details and fallback benchmarks are defined in [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md) and [RESOURCE_STORAGE_AND_EVALUATION.md](RESOURCE_STORAGE_AND_EVALUATION.md).

The exclusive runner mutates one ordinary owner-held world. Complete phase plans are preflighted before commit; no partial boundary is published or saved, and an unexpected post-mutation defect faults that in-memory world. Detached save/publication snapshots keep background work away from mutable arrays. The prototype starts with uniform signed-64-bit resource columns, provisional `256`-row chunks, tile-partitioned SoA, common inline state, and dense bounded resource vectors. It instruments these choices end to end before implementing alternatives. Targeted comparisons are permitted only when the corresponding measurement is material:

- another organism/remnant chunk capacity when chunk occupancy, metadata, iteration, or copying is limiting;
- Tile-partitioned SoA versus global SoA plus tile membership under adversarial population clustering.
- Detached snapshot-copy bandwidth versus later copy-on-write/double-buffering if boundary pauses miss the target.
- Core versus a concrete typed side store for bounded behavior/binding state, only after an actual optional field demonstrates a material cost.
- Dense resource vectors versus a hybrid only if catalogue growth demonstrates a crossover.

Every physical store must pair its typed mutation operations with dirty tracking as specified in [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md). Sparse writes record stable IDs by logical field group; dense writes may mark a tile/store chunk. Creates, removals, and relocations are explicit structural operations. Dense slots and dirty metadata are physical implementation details: neither enters saves, authoritative hashes, or network schemas.

The first dense-layout prototype must prevent mutation through untracked array references. Debug shadow hashes or mutation epochs compare actual changed columns/stores with the sealed phase journal and fail on missing coverage. Derived projection dependencies—such as reserve/environment changes invalidating health—belong in an explicit field dependency catalogue rather than ad hoc serializer knowledge.

# Event model

Classify events by purpose:

- Authoritative state-transition events.
- Replay inputs and command results.
- Resource-ledger transactions.
- Historical/analytical events such as death-risk profiles, realized death triggers, and speciation.
- Canonical notable-event facts derived at completed boundaries for the factual chronicle.
- Actor attention alerts and automatic-pause transitions; non-pausing presentation notifications may remain ephemeral.
- Metrics that should be aggregated rather than retained individually.

Canonical notable events store facts and evidence references, not localized narrative prose. Player pins, layouts, labels, and hypothesis notes are presentation/profile records and must not enter world simulation state, autonomous evidence, or deterministic world hashes. An evolution goal or attention policy becomes authoritative actor state only when server evaluation while disconnected or an automatic clock pause depends on it; see [PLAYER_LOOP_AND_NARRATIVE.md](PLAYER_LOOP_AND_NARRATIVE.md).

An internal change journal is not another authoritative event category. It identifies which final values a downstream projection may need to reread and references retained event facts where necessary; it does not duplicate the resource ledger, command log, or event store.

# Canonical storage operations

ID allocation, locator resolution, swap removal, migration, transactional world forks, and immutable completed handles are specified with pseudocode in [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md). Atomic tick ownership and save/publication capture are specified in [WORLD_EXECUTION_AND_OWNERSHIP.md](WORLD_EXECUTION_AND_OWNERSHIP.md).

# Required decisions

- [x] Canonical ID representation, allocation, zero/non-reuse policy, and creation ordering; final range proof remains. See [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md).
- [x] Integer resource scale and range proof; see [RESOURCE_CALIBRATION.md](RESOURCE_CALIBRATION.md).
- [x] First owner-mutable dense storage, standard locator, migration/removal, concrete side-store, detached-snapshot, and derived-index strategy; chunk size and later specializations remain benchmark decisions. See [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md).
- [x] Mutation/change-capture boundary, stable-ID dirty journals, structural operations, and dense-versus-sparse marking contract; physical dirty-set representation remains open. See [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md).
- [x] First hot organism-resource, internal reservation/claim, tile-stock matrix, tile-effective-view, and compiled-phenotype strategy; final slot catalogue, page grouping, and thresholds remain benchmark decisions. See [RESOURCE_STORAGE_AND_EVALUATION.md](RESOURCE_STORAGE_AND_EVALUATION.md).
- [x] First organism stored/derived/cached field inventory and health contract; see [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md). Other entity inventories remain open.
- [x] Founder available-store balance ownership, capacity-group load derivation, and DNA-compiled baseline limits; see [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- [x] Energy-carrier pool ownership, compiled maximum versus structure-commissioned capacity, and storage-structure assignment; see [ENERGY_STORAGE.md](ENERGY_STORAGE.md).
- [x] Root-seed, RNG algorithm/schema compatibility state, semantic draw addressing, and commit-owned occurrence ordinals; permanent numeric domain assignments remain implementation scaffold work. See [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md).
- [ ] Event retention categories.
- [ ] Actor knowledge, observation snapshot, and visibility-state schemas.
- [ ] Abiogenesis-origin representation and root-species invariants.
- [x] First logical and physical within-tile spatial-index strategy: derived pooled `16 × 16` bins for organisms and remains, rebuilt after movement, with exact-distance filtering and stable ordering; see [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md) and [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md).
- [ ] Maximum expected counts used to size and benchmark structures.

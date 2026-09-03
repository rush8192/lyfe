# Entity Identity and Authoritative Storage

Status: first v1 physical-layout contract with sane starting defaults; alternate chunk sizes and dense-versus-sparse specializations are gated on representative profiling

Sources: [authoritative data model](DATA_MODEL.md), [world execution and ownership](WORLD_EXECUTION_AND_OWNERSHIP.md), [state change and client synchronization](STATE_CHANGE_AND_CLIENT_SYNC.md), [resource model](RESOURCE_MODEL.md), [resource storage and evaluation](RESOURCE_STORAGE_AND_EVALUATION.md), [organism state and health](ORGANISM_STATE_AND_HEALTH.md), and [spatial organisms](SPATIAL_ORGANISMS_AND_BEHAVIOR.md).

# Purpose

Define stable identity, physical state layout, lookup and compaction, transactional world versions, tile/spatial indexes, optional organism state, and the boundary between primary state, stored gameplay materializations, rebuildable structural indexes, saves, and client projections.

# Design priorities

In order:

1. preserve deterministic and atomic world semantics;
2. make whole-population and tile-local hot loops contiguous and allocation-light;
3. preserve stable identity across movement, compaction, save/load, and protocol sync;
4. make state mutation and change capture inseparable;
5. keep physical layout replaceable behind the logical model;
6. support tens of thousands of organisms comfortably and benchmark around 100,000;
7. leave a measured path toward larger worlds without prematurely building a general ECS or distributed database.

# Identity domains

V1 uses strongly typed opaque identifiers. Numeric equality across different ID types has no meaning.

| Domain | Proposed representation | Allocation |
| --- | --- | --- |
| `WorldId`, `ActorId`, `ProjectionStreamId`, client request ID | 128-bit externally opaque value | Server/application layer; outside hot simulation loops |
| `TileId` | strongly typed `uint32` | Fixed at generation from canonical row-major world coordinate order |
| `OrganismId`, `RemnantId`, `SpeciesId`, `GenomeId`, `AbiogenesisOriginId`, `LineageEventId`, `SimulationEventId`, authoritative command ID | strongly typed `uint64` | Separate world-local monotonically increasing counter per type |
| `TraitId`, `TraitFamilyId`, `ResourceId`, `ReactionId`, behavior/cause/category IDs | strongly typed `uint32` compiled definition ID | Rule compiler manifest under one rules hash |

Illustrative C# only:

```text
readonly record struct OrganismId(ulong Value);
readonly record struct TileId(uint Value);
readonly record struct ResourceId(uint Value);

EntityRef:
    entityKind
    uint64 value
```

Protocol fields remain typed wherever possible. A generic reference carries both kind and value. JavaScript bindings must preserve `uint64` exactly as `bigint`, string, or a generated long wrapper.

## ID rules

- Runtime entity IDs are world-local; every external reference also has world context.
- IDs encode no tile, species, timestamp, dense slot, array generation, or gameplay meaning.
- IDs are never reused within a world, including after extinction, removal, compaction, or load.
- Next-ID counters are authoritative save state and participate in deterministic hashes.
- Zero is reserved as `None` where the logical field is optional; real allocation begins at one.
- Overflow fails the transaction explicitly. The range proof must show it is unreachable for supported world horizons.
- Definition IDs are stable only with their exact compiled rules manifest/hash. Save and protocol decoding must reject or migrate mismatched manifests.

IDs are allocated only during canonical owner-thread commit. Parallel evaluators produce creation intents with deterministic creation keys; they never increment a counter.

```text
CreationKey examples:
    offspring: (phase, tileId, parentOrganismId, reproductionOrdinal)
    remnant:   (terminalPhase, tileId, deadOrganismId)
    species:   (commandReplayOrder, ancestorSpeciesId, speciationOrdinal)

AssignIds(intents):
    stableSort by creation key
    for intent in order:
        intent.id = ++counterFor(intent.entityType)
```

# Transactional world versions

The authoritative root is a directory of page/chunk handles plus small scalar state:

```text
WorldVersionRoot:
    worldMetadataPage
    tilePages
    organismPartitionsByTile
    remnantPartitionsByTile
    speciesPages
    genomePages
    locatorPagesByEntityType
    resourceAndEdgePages
    historyPages
    actorKnowledgePages
    eventAndLineagePages
    nextIdCounters
    completedTick
    worldRevision
```

At a completed boundary, pages reachable from the completed root are immutable. `BeginTransactionalFork` shallow-copies the root directory and shares pages. The first write to a shared page obtains a pooled page, copies its contents, and updates the working root; further writes during the same transaction mutate that private page.

Success freezes the working pages and atomically replaces the completed root. Failure releases private working pages and leaves the old root untouched. Save/publication handles retain immutable page references with bounded leases.

This coarse copy-on-write transaction is the initial choice because it simultaneously provides:

- true failed-tick discard without a fragile inverse/undo path;
- immutable save and publication views;
- cheap sharing of cold state and rule definitions;
- mutation-local dirty metadata;
- a clear single-writer implementation.

Hot organism/resource pages may be copied nearly every tick. Pages therefore come from typed pools, avoid per-row objects, and are sized through the representative benchmark. If copy bandwidth prevents the target tick rate, the comparison must include double-buffered hot columns and a checked undo journal before weakening atomicity.

# Tile storage

The generated world has a fixed tile count and topology. `TileId` is the stable row-major index into tile tables, while signed `(x, y)` remains stored/derivable world geometry.

Tile data is split by update behavior:

- `TileFixedPage`: coordinate, elevation/depth, substrate/lithology, climate baselines, neighbor IDs, and other generation-fixed facts.
- `TileConditionPage`: current temperature, precipitation, moisture, insolation, cloud/turbidity, volcanism, and condition remainders.
- `TileResourcePage`: dense resource-major matrices indexed by compiled tile-reservoir slot and `TileId`, physically paged/grouped by reservoir and measured mutation behavior.
- `TileHistoryPage`: fixed rings/rolling totals required by simulation decisions.
- `TileMembershipDirectory`: handles for living-organism and remnant partitions.

Frequently iterated tile properties use columns indexed by `TileId`, not a graph of tile objects. The four canonical neighbor IDs are compiled once; wrapped `x`, bounded `y`, and edge compatibility are not rediscovered in hot loops.

Canonical undirected edge state—gas/dissolved exchange remainders and any later transport state—uses one dense edge table generated in canonical `(minTileId, maxTileId, edgeKind)` order. Both tiles reference the same `EdgeId`; no exchange remainder is duplicated.

# Living-organism layout

V1 begins with tile-partitioned, chunked structure-of-arrays storage. A partition is created lazily for an occupied tile, and each chunk initially contains `256` rows. This is a sane implementation default rather than a claimed optimum; a second chunk size is implemented only if representative occupancy, metadata, iteration, or copy profiles show that chunk sizing materially constrains capacity.

```text
TileOrganismPartition:
    chunks[]
    count

OrganismChunk:
    count
    ids[]
    speciesIds[]
    xQ[]
    yQ[]
    velocityXQ[]
    velocityYQ[]
    birthTicks[]
    lifecyclePhaseIds[]
    structuralMatterAndAssignments[...][]
    resourceBalanceByCompiledSlot[slot][row]
    actionAndReproductionState[...][]
    behaviorState[...][]
    optionalComponentHandles[...][]
    dirtyByLogicalFieldGroup
```

Column groups are logical, not promises that every field resides in one allocation. Hot phases should touch only the arrays they consume. For example, movement need not pull nutrient vectors into cache, and gas/resource projection need not traverse behavior memory.

## Why tile-partitioned SoA first

- Most interaction, resource access, environmental stress, and behavior observation is tile-local.
- Phase partitions and spatial bins can operate on contiguous tile-owned rows.
- Migration is expected to be much rarer than within-tile tick processing, making row copy at a boundary an acceptable tradeoff.
- It aligns with deterministic tile worker ranges and dirty publication scopes.
- Lazy chunks avoid hundreds of large empty per-tile allocations.

The benchmark must compare this to one global chunked SoA plus tile membership vectors if migration copying or highly uneven tile populations dominate. The logical APIs and IDs cannot depend on which wins.

# Locator tables and dense slots

Monotonic world-local IDs permit a paged direct locator rather than a large object dictionary:

```text
EntityLocation:
    present
    tileId?       // organisms and remnants
    chunkIndex
    rowIndex

PagedLocator<OrganismId, EntityLocation>
PagedLocator<RemnantId, EntityLocation>
PagedLocator<SpeciesId, DenseLocation>
```

The ID value selects a locator page and offset. Unallocated/dead IDs retain an absent sentinel. Locator pages are part of the transactional runtime root and use the same copy-on-write rules. They are logically redundant in the durable format: a save may serialize and verify them as an acceleration structure or omit them and deterministically rebuild them from entity rows before publication.

Dense slots are ephemeral locations. Every move, append, and swap removal updates the relevant locator in the same typed transaction.

```text
RemoveOrganism(id):
    location = organismLocator.RequirePresent(id)
    victim = partition[location]
    tail = partition.LastRow()

    if tail.id != victim.id:
        CopyRow(tail, victim)
        organismLocator.Set(tail.id, victim.location)

    partition.RemoveLast()
    organismLocator.SetAbsent(id)
    changes.RecordRemove(id)
```

Iteration order over dense rows is never a conflict-resolution or RNG input. Evaluators use stable IDs in keyed randomness, and shared outcomes sort by specified logical keys before commit.

# Tile migration

Migration preserves the organism ID and every authoritative balance:

```text
MoveOrganismToTile(id, destinationTile, destinationPosition):
    source = locator.RequirePresent(id)
    row = sourcePartition.ReadCompleteLogicalRow(source)
    row.tileId = destinationTile       // implicit in partition or explicit in view
    row.position = destinationPosition

    sourcePartition.RemoveForMigrationBySwap(
        source,
        updateSwappedTailLocator = true,
        doNotMarkMigratingIdAbsent = true)
    destination = destinationPartition.Append(row)
    locator.Set(id, destination)
    changes.RecordRelocation(id, source.tileId, destinationTile)
```

Append and source removal are one atomic store operation. No phase can observe the organism in both or neither tile. A migration does not create a new biological entity or duplicate resource accounts.

# Organism resource balances

The rule compiler assigns dense slots for resources permitted in each organism reservoir/capacity group. Chunks store fixed-size integer vectors resource-major by compiled slot so one reaction can scan the same resource across rows without per-organism dictionaries. Full layout, allocation scratch, claims, and tile matrices are defined in [RESOURCE_STORAGE_AND_EVALUATION.md](RESOURCE_STORAGE_AND_EVALUATION.md).

Separate logical balances remain separate columns even when they share capacity or composition, including:

- available organic and inorganic macronutrient forms;
- bioavailable micronutrients;
- charged `ReserveOrganic`;
- zero-energy `SpentReserveCarrier`;
- structure and its geometric/organization/storage assignments;
- bounded metabolic-binding cohorts where logically required.

Used load, elemental totals needed by multiple consumers, stored-energy summaries, health, and stress are stored gameplay materializations with one owner and explicit dependency generations. They are not alternate authoritative balances, and consumers never independently recompute them.

The first prototype keeps the bounded common v1 slots dense per compatible group. Rare future mutually exclusive compound families belong in typed optional balance components rather than a generic sparse map. Changing physical encoding must not change whole-extent reaction arithmetic or save/protocol identity.

# Optional and capability-specific organism state

Do not place every possible late-game field in every organism row. Optional state uses typed component stores keyed by stable organism ID, each with its own dense rows and paged locator:

- history-using behavior pressure memory;
- bounded metabolic-binding cohorts when the common inline representation is wasteful;
- dormancy-specific state;
- advanced predation/hunting state beyond common behavior target fields;
- future reproduction or multicellular state.

An optional component may exist only when the compiled DNA/lifecycle capability requires it. Trait activation, speciation, and lifecycle transitions create/remove/migrate components through canonical commit operations. Component absence is a typed logical state, not a null object reference followed in a hot loop.

Small fields used by nearly all founders remain in the core chunk even if conceptually optional; the benchmark, not aesthetic purity, sets this boundary.

# Species, genomes, lineage, and events

- `SpeciesStore` is a small dense paged store with mutation economy, evolution state, stored aggregate materializations, control/lock state, and a durable `GenomeId` plus rebuildable runtime phenotype slot.
- `GenomeStore` retains domain-shaped immutable acquired traits. A separate compiled-phenotype table interns immutable typed numerical profiles by genome plus rules hash. Species identity never deduplicates even when genomes match.
- Lineage and abiogenesis records use append-only paged stores with stable IDs and explicit parent/origin references.
- Canonical events and death records use append-only chunked stores under their retention policies. Large contributing-input payloads live in typed side pages referenced by event ID.
- Population and average health are stored derived aggregates with a single reducer and named update barrier; mutation remainders and autonomous intent are primary authoritative species state.

Append-only does not mean unbounded. Retention/compaction creates a new canonical retained store at a completed boundary while preserving all IDs still referenced by saves, gameplay history, or protocol contracts.

# Remnants

Remnants use a separate tile-partitioned chunked SoA, not a special organism lifecycle flag. Their common fields include stable ID, position, original packing profile, creation/death tick, radius inputs, decay cohort state, and dense remaining consumable contents.

This keeps living hot loops free from dead rows and lets remnant decay, spatial indexing, partial consumption, and final removal use their own cadence and columns. Organism death atomically transfers all conserved contents into a newly allocated remnant before removing the organism.

# Spatial indexes

The `16 × 16` within-tile organism/remnant bins are derived scratch indexes:

- rebuilt or deterministically updated at the named phase boundaries;
- backed by pooled flat arrays of bin offsets and dense row/stable-ID entries;
- queried in canonical neighboring-bin and stable-ID order, followed by exact distance filtering;
- absent from saves, authoritative hashes, and network state;
- freely rebuildable after load, compaction, or worker-count changes.

The first implementation should rebuild after movement because it is simple and linear. Incremental maintenance is considered only if profiling shows rebuild cost dominates.

# Actor knowledge and historical storage

Actor knowledge is authoritative but cold relative to organism metabolism:

- discovered/live tile sets use fixed-world-size bitsets per actor;
- fixed coarse discovery facts and last-observed tile records use arrays keyed by `TileId` where practical;
- retained observation tick and last-known behavior/resource summaries occupy typed optional pages;
- actor presentation-only camera, panels, notes, and filters remain outside world storage unless a separately defined profile store owns them.

Fixed-duration one-hour histories use circular arrays with an authoritative cursor and checked rolling totals. Dimensions are compiled from tracked resource/profile definitions rather than per-tick dictionaries. Sparse predation species-pair histories use a deterministic keyed table plus dense ring records and expire only after their final nonzero bucket ages out.

# Materialized state, indexes, and scratch data

Every non-primary structure declares an owner, dependencies, and lifetime. Gameplay-relevant derived numbers are stored materializations; structural acceleration data remains rebuildable:

| Data | Lifetime | Update/rebuild |
| --- | --- | --- |
| Phase read view | One phase | Sealed after prior phase commit |
| Spatial bins | Named interaction/behavior phase | Rebuild after movement/lifecycle changes |
| Organism health/capacity/effective values | Stored named materialization | Owning builder updates from dirty reserve, structure, age, environment, lifecycle, or compiled phenotype before consumers |
| Tile current/effective values | Stored completed-tick materialization | Climate/environment owner updates once per affected tile/barrier |
| Tile/species behavior distributions | Stored completed-tick aggregate | Behavior/species owner updates once after behavior/membership changes |
| Species population/average health | Stored completed-tick aggregate | Species owner reduces once after lifecycle and health materialization |
| Protocol projection | Per stream revision | Server-only from change journal and immutable publication values |
| Save locator/index validation | Load boundary | Rebuild/verify from canonical rows |

Completed gameplay materializations enter save/hash/change capture according to [MATERIALIZED_DERIVED_STATE.md](MATERIALIZED_DERIVED_STATE.md). Scratch arenas and pooled buffers belong to the runner/phase worker slot, are cleared deterministically, and never enter save, hash, or protocol state. Their capacity may differ without changing outcomes.

# Mutation and change capture

Stores expose typed mutators rather than writable collections. A mutator:

1. resolves the stable ID and validates presence/type;
2. obtains a private transactional page on first write;
3. validates arithmetic, capacity, ownership, and phase rules;
4. changes the authoritative columns;
5. records the stable ID or dirty chunk under a logical field group;
6. records resource ledger/event facts where required;
7. returns no mutable reference that can escape the commit.

Structural operations update rows, locators, owner counts, and the `PhaseChangeSet` together. Debug builds compare page mutation epochs/hashes with dirty coverage. This implements [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md) without making Protocol Buffer fields part of storage.

# Publication and saves

The completed root is canonical; client projections are not. For a due publication, the runner copies only required authorized-source values into immutable typed publication pages while it holds the completed version. Background projectors transform those pages into actor-specific Protocol Buffer batches.

A full save handle retains the complete completed root. The persistence worker serializes logical stable-ID records and compiled dense resource arrays, never raw C# object memory, page-pool addresses, padding, or transient dirty bits. Save schema order is canonical by entity type and stable ID, so different dense compaction histories yield equivalent saves/hashes.

Holding a save/publication version increments page leases. Pools may reuse a page only after every root/handle releases it. Bounded handle counts prevent slow I/O or encoding from retaining unbounded memory.

# Memory and range planning

The prototype records at minimum:

- bytes per living organism core row and per optional component;
- bytes per remnant, species, tile, edge, history, locator entry, and actor;
- page/chunk occupancy and slack by tile;
- bytes copied on first write per tick and per phase;
- pooled page high-water marks and lease age;
- spatial scratch and intent/outcome buffer sizes;
- dirty density by logical field group;
- save/publication retained-version overhead;
- GC allocations per tick after warmup.

Large object heap thresholds, array headers, alignment, and generated-code layout must be measured in .NET rather than estimated only from field widths. Pooled arrays are cleared according to data-sensitivity and correctness needs before reuse.

# Invariants and tests

- Every live stable ID resolves to exactly one present logical row of the correct type; every present row resolves back to itself.
- No removed ID is ever reallocated.
- Canonical allocation yields identical IDs across worker counts and dense compaction histories.
- Swap removal and migration update locators and change journals atomically.
- Transaction failure leaves the prior completed root, counters, locators, ledgers, events, and hashes unchanged.
- A retained completed/save/publication handle never observes later mutations.
- Page pooling cannot reuse a leased page or expose stale live rows.
- Dense resource vectors conserve the same matter as the logical ledger and never alias charged and spent carriers.
- Rebuilding declared structural locators and spatial indexes produces identical logical results; stored gameplay materializations are instead loaded and validated against their generations.
- Save/load and canonical hashing are independent of chunk capacity, page addresses, slack rows, dirty bits, and dense row order where order is not logical.
- The 100,000-organism benchmark stays within the eventual memory, allocation, copy-bandwidth, tick, save, and publication budgets.

# Prototype decisions still requiring measurements

- Organism/remnant chunk capacity starts at `256` rows; compare another size only if chunk-related measurements justify the experiment.
- Page granularity for hot spatial, resource, lifecycle, and behavior column groups.
- Core versus optional placement of behavior memory and metabolic-binding cohorts.
- Tile-partitioned SoA versus global SoA plus tile membership under uniform and highly clustered populations.
- Dense resource-vector crossover if the compiled catalogue grows.
- Locator page size and whether locators are serialized or rebuilt on load.
- Copy-on-write bandwidth versus double-buffered hot columns under the fastest target speed.
- Exact pool, retained-version, and background save/publication memory limits.

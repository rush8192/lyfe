# Resource Storage and Effective-Parameter Evaluation

Status: first v1 hot-data contract with uniform-width and chunk defaults; the exact internal-slot catalogue still requires compiler verification, while alternate representations are gated on representative profiling

Sources: [resource model](RESOURCE_MODEL.md), [entity identity and storage](ENTITY_IDENTITY_AND_STORAGE.md), [internal storage and allocation](INTERNAL_STORAGE_AND_ALLOCATION.md), [world and climate](WORLD_AND_CLIMATE.md), [trait compilation](TRAIT_SYSTEM.md), [rule-pack authoring and compilation](RULE_PACK_AUTHORING_AND_COMPILATION.md), and [simulation loop](SIMULATION_LOOP.md).

# Purpose

Choose physical representations for the state LYFE reads and writes most often: organism resource balances, resource reservations/bindings, tile stocks, tile conditions, and the effective numerical parameters used by metabolic and environmental kernels.

DNA remains a domain-shaped species definition. The cold-path contract in [RULE_PACK_AUTHORING_AND_COMPILATION.md](RULE_PACK_AUTHORING_AND_COMPILATION.md) fully compiles each canonical genome into immutable typed numerical profiles with permanent logical IDs and pre-resolved runtime handles; organism ticks never traverse the trait graph or perform stable-key lookup. Compact DNA encoding is not a v1 optimization target.

# Workload assumptions

- Every living organism evaluates maintenance and one or more resource/metabolic gates each tick.
- Many organism balances change each tick; position and reserve are also high-churn.
- External claims and environmental reactions repeatedly group work by tile and resource.
- Gas sources, sinks, and transport sweep the same resource across all tiles/edges.
- Tile conditions update once per tick, then are reused by every organism in that tile.
- A species' DNA changes only at speciation/evolution boundaries and is shared by all current members.
- The representative benchmark contains approximately `100,000` organisms and `544` tiles.

The scale asymmetry matters. One additional dense signed-64-bit organism slot costs approximately `0.8 MB` per `100,000` organisms per populated world version, before page slack and metadata. One tile slot across `544` tiles costs only about `4.25 KiB`. Tile stocks should therefore favor simple dense execution; organism slots deserve a deliberate core/optional boundary.

# Decision summary

- Keep authored DNA as an immutable acquired-trait set over the domain graph, with full provenance and presentation structure.
- Compile each unique genome into immutable typed subsystem profiles containing final numerical values, flags, dense resource slot IDs, and short process plans.
- Species reference a compiled phenotype; organisms store a species ID, not copied DNA parameters.
- Group organisms by tile physically and derive tile/species iteration cohorts when reusing a phenotype materially helps a hot phase.
- Store common organism balances as resource-major columns within an organism chunk—an array-of-structure-of-arrays layout.
- Give frequently used scalar accounts such as structure, charged reserve, and spent carrier dedicated columns even though they remain ledger resources logically.
- Keep the bounded v1 common resource catalogue dense. Add rare future resource families through typed optional component stores rather than per-organism dictionaries.
- Resolve one organism's internal process allocation through a short compiled process plan and worker-local scratch; do not persist ordinary per-tick reservations.
- Store tile stocks in dense resource-major matrices grouped by reservoir and transport class.
- Store raw/stateful tile conditions field-major, then materialize small phase-specific effective tables once per affected tile and reuse them for its organisms.
- Keep raw stocks and stateful environmental variables as primary state; store gameplay-relevant effective values, accessible quantities, stresses, capacities, and reaction opportunities under explicit owners and dependency generations.

# Option space: organism resource balances

| Layout | Strengths | Weaknesses | V1 disposition |
| --- | --- | --- | --- |
| Dictionary/map per organism | Flexible and naturally sparse | Pointer chasing, hashing, high allocation/metadata cost, unstable iteration hazards | Reject for hot authoritative balances |
| Row-major fixed vector per organism | One organism's complete inventory is contiguous; simple transfer/copy | A reaction scanning one resource across many organisms has a wide stride; copies unused slots into cache | Do not use as the primary hot layout |
| Global resource-major SoA | Excellent whole-population resource sweeps | Poor tile locality; structural changes and migration coordination are harder | Useful comparison, not first choice |
| Tile-chunk resource-major SoA | Good tile locality, vectorizable same-resource access, bounded migration copies, aligns with phase workers | Resource arrays multiply by chunk capacity and may waste slack | Selected first implementation |
| Hybrid dense core plus sparse map | Can save rare-slot memory | Map path remains expensive/complex and makes reaction code bifurcate | Use typed optional stores instead of generic maps |

# Organism hot resource layout

Within each tile-owned organism chunk, balances are resource-major:

```text
OrganismResourceColumns:
    structuralMatterQ[row]
    chargedReserveQ[row]
    spentReserveCarrierQ[row]

    availableMacronutrientQ[compiledCoreMacroSlot][row]
    freeMicronutrientQ[compiledMicronutrientSlot][row]
    committedMicronutrientQ[compiledMicronutrientSlot][row]
    ingestedMatterQ[compiledIngestedSlot][row]

    bindingComponentHandle[row]?      // only when not stored in common inline form
```

Dedicated scalar columns remain ordinary ledger accounts semantically. They are specialized physically because nearly every relevant tick reads them, they drive health/capacity checks, and they should not require an indirect slot lookup.

The first rule compiler produces separate dense manifests per reservoir/capacity group rather than one universal rectangular vector. A resource may occupy a slot only in compatible groups. Reactions contain pre-resolved typed handles such as `(AvailableMacro, slot 3)` or `(FreeMicronutrient, slot 8)`.

```text
CompiledInternalResourceHandle:
    storageGroup
    denseSlot
    resourceId                  // retained for validation/explanation, not lookup
```

V1 begins with all resources that can ordinarily reside in a common group represented densely for that group. This includes the fixed fourteen micronutrients. If future late-game content introduces many mutually exclusive internal compounds, a capability creates a typed optional balance component with its own dense manifest; it does not add a hash map to every organism.

## First internal slot-manifest hypothesis

The current v1 resource catalogue implies this starting layout. It records existing semantics rather than adding new resources:

| Physical group | First dense contents | Quantity slots per organism |
| --- | --- | ---: |
| Dedicated core | `StructuralBiomass`, `ReserveOrganic`, `SpentReserveCarrier` | `3` |
| Available dissolved macro | Organic and inorganic CHNOPS (`12`), `Ammonia`, `HydrogenSulfide`, `LabileDissolvedOrganic`, `ReducedFermentationProducts` | `16` |
| Free micronutrients | Ca, Fe, K, Na, Mg, Zn, Cu, I, F, Se, Mn, Mo, Ni, Co | `14` |
| Committed micronutrients | The same fourteen IDs, owned by structural/catalytic quotas | `14` |
| Ingested particulate | Initially `StructuralBiomass`; present only with ingestion capability | Optional component, `1` initial slot |

The common core is therefore `47` resource quantities before binding totals, capacity materializations, or non-resource organism fields. Uniform 64-bit storage would use about `376 bytes` per organism, or `37.6 MB` per `100,000` organisms per populated world version, for these balances alone. `IngestedMatterBuffer` remains optional, and `SpentReserveCarrier` may be measured as an optional column if its all-organism zero cost outweighs the extra component lookup.

The manifest compiler must confirm this list against every v1 reaction, acquisition, reproduction, death, and remnant route. A resource cannot enter a group merely because it exists in the global catalogue.

## Starting physical width and deferred alternatives

All ledger APIs, reaction arithmetic, deltas, save fields, protocol values, and reconciliation continue to use nonnegative signed-64-bit quantities with checked 128-bit intermediates. This is the logical numeric contract.

The first implementation also stores every organism quantity in a signed 64-bit physical column. Matching the logical type keeps accessors, debugging, saves, migrations, and reaction kernels straightforward. The approximate `37.6 MB` common-resource cost at `100,000` organisms is a reasonable baseline, not by itself evidence that narrower storage is necessary.

The following are deferred options, not fields to implement in the first storage slice. A narrower unsigned representation may be investigated only when an end-to-end representative profile shows organism-column memory residency or copy/cache bandwidth to be a material capacity bottleneck, and the rules compiler and organism range proof establish a hard maximum for the complete supported world/rules horizon:

| Candidate column family | First candidate width | Required proof |
| --- | --- | --- |
| Free and committed micronutrients | `uint16` | Every capacity, quota, inheritance, and transfer result remains `<= 65,535` |
| Dissolved macro balances | `uint32` | Every compiled capacity/load combination bounds each quantity below `2^32` |
| Structure, charged reserve, spent carrier | `uint32` | Maximum scale/storage/offspring/remnant state remains below `2^32` |
| Tile stocks, flow totals, histories, ledger arithmetic | `int64` | Existing world/lifetime range proof |

Typed group accessors promote a stored value to `long`, apply checked signed deltas and `Int128` intermediates, validate the group's physical maximum, and only then narrow on commit. Reactions do not cast or know the physical width themselves.

```text
ResourceColumnAccessor.ApplyDelta(row, signedDeltaQ):
    currentQ = PromoteToInt64(column[row])
    nextQ = checked(currentQ + signedDeltaQ)
    require 0 <= nextQ <= compiledGroupPhysicalMaximum
    column[row] = checked_narrow(nextQ)
```

A rule pack whose proved maximum exceeds the selected width is rejected by that storage schema rather than wrapping or clamping. Moving to a wider column is an explicit save/storage-schema migration but does not change the logical resource model.

If that gate is reached, the prototype compares the uniform-`int64` reference against the relevant bounded-width family only. Narrow columns are adopted only if the end-to-end simulation and publication benefit remains material after accessor, dispatch, schema-migration, and client costs; correctness never depends on them.

## Chunk and copy behavior

Resource columns share the organism chunk's logical row index but may live in separate arrays from spatial and behavior columns. This lets movement touch spatial arrays without pulling every micronutrient column into cache, and lets metabolism mark balance fields without dirtying identity data.

Migration copies the organism's complete logical resource row once to the destination tile partition. Whole-tick metabolism greatly outnumbers cross-tile migration, so local hot access is favored over zero-copy migration.

# Internal process allocation

There are three distinct concepts:

1. `Balance`: authoritative matter currently owned by the organism.
2. `BindingCohort`: authoritative matter temporarily unavailable through a named process until a release tick.
3. `TickReservation`: ephemeral phase scratch preventing two admitted plans from spending the same free matter in one transaction.

Ordinary tick reservations are never stored back into the organism. The compiler turns each phenotype's enabled internal machinery and allocation policy into a short canonical plan:

```text
CompiledProcessPlan:
    processes[] in (priorityBand, processId) order

CompiledProcess:
    processId
    reactionId?
    activationFlags
    inputHandles[]               // pre-resolved storage group + dense slot
    catalystQuotaHandles[]
    outputHandles[]
    integerStoichiometry[]
    workLoadPerOpportunity
    extentCeiling
    probabilityThreshold?
    bindingDurationTicks?
    allocationPriorityBand
    allocationWeight
    holdbackRules[]
    fixedAndUseCostHandles[]
```

The process list is expected to be small for one phenotype. It is an immutable domain-oriented record array, not a compressed bytecode interpreter. Engine kernels switch on typed process/reaction kinds and consume the compiled numbers.

Worker-local scratch uses fixed or pooled arrays sized from the maximum compiled plan, not one heap object per organism/process:

```text
EvaluateInternalAllocation(organismRow, phenotype, phaseView, scratch):
    scratch.Reset()
    scratch.LoadFreeQuantities(
        balances = organismRow.resourceColumns,
        bindings = organismRow.bindingCohorts)

    for priorityBand in phenotype.processPlan:
        compute mandatory and protected holdbacks once
        compute whole feasible extents from free quantities and work budget
        allocate conflicting inputs by compiled weight and canonical remainder rule
        record TickReservation and compact ProcessIntent in scratch/output arena

    return compact intents; persist no TickReservation
```

Only successful commit changes balances or creates/releases binding cohorts. Failed candidate processes leave no authoritative allocation artifact. This retains the shared-resource/priority behavior defined in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md) while avoiding a general optimization solver or per-tick allocation graph.

## Claims against shared tile resources

External claims use flat tile-local buffers grouped by compiled resource slot and priority class:

```text
TileClaimBuffer:
    groupOffsets[(reservoirKind, resourceSlot, priorityClass)]
    claims[]:
        organismId
        organismRowRef           // phase-local only
        processPlanIndex
        requestedQ
        contentionWeightQ
        deterministicRemainderKey
```

The evaluator already visits a tile partition, so claims append to its worker-owned tile buffer without hashing. Resolution scans one contiguous claim range per stock, applies proportional/priority rules, and emits compact grants. Stable IDs and named keys—not append order—govern residual allocation and randomness.

Debug ledger entries are reconstructed from applied transfers. Production does not allocate an account or transaction object for each `(organism, resource)` pair.

# Option space: tile resource stocks

| Layout | Strengths | Weaknesses | V1 disposition |
| --- | --- | --- | --- |
| Map per tile | Flexible | Unnecessary overhead at 544 tiles; poor gas sweeps | Reject |
| Tile-major dense `[tile][resource]` | Good when consuming many stocks for one tile | Strided world-wide gas/source/transport sweeps | Viable benchmark fallback |
| Resource-major dense `[resource][tile]` | Excellent source/sink/transport and resource projection; compact contiguous columns | A tile kernel touches several columns | Selected |
| Blocked 2D/AoSoA | Balances both access directions | More addressing/code complexity | Revisit only if measured catalogue/tiles justify it |

Tile stocks use separate resource-major matrices by reservoir and transport behavior:

```text
TileResourceState:
    atmosphereQ[atmosphericSlot][tileId]
    organicPoolQ[organicTileSlot][tileId]
    inorganicPoolQ[inorganicTileSlot][tileId]
    micronutrientPoolQ[micronutrientSlot][tileId]

    sourceSinkRemainderQ[sourceSinkRuleSlot][tileId]
    edgeExchangeRemainderQ[mobileResourceSlot][edgeId]
```

The compiler also emits dense slot lists such as `FastMixingAtmosphericSlots`, `VolcanicLocalSlots`, `AttritingGasSlots`, `DissolvedMobileSlots`, and `TileBoundSlots`. Hot phases iterate these lists without checking tags or traversing definitions.

At `544` tiles, a single `long` column is small enough that simple whole-column sweeps should remain cache-friendly. Atmospheric exchange reads stable source columns and writes paired delta/remainder columns; biological claims later read the same current stock by `(slot, tileId)`.

Tile stock pages are grouped by reservoir and, where measurements justify it, by small resource batches. Changing one gas must not copy unrelated historical or actor-knowledge pages. Exact page grouping is a benchmark decision behind the matrix API.

# Tile conditions and effective parameters

Tile data falls into three categories:

| Category | Examples | Storage |
| --- | --- | --- |
| Fixed/generated | coordinate, elevation/depth, substrate, climate normals, neighbor IDs | Immutable dense columns |
| Stateful authoritative | surface moisture, volcanic pulse state, source/sink and weather remainders, retained condition histories | Owner-mutable dense columns/rings |
| Current materialized | temperature, precipitation, cloud, insolation, aquatic light, access factors, phase opportunities | Stored deterministic columns/tables produced once at their named barrier |

Raw/current fields are stored field-major by `TileId`, which is simple for climate generation, transport, projection, and change tracking:

```text
TileCurrentConditionColumns:
    temperatureMilliC[tileId]
    precipitationMicrometersPerHour[tileId]
    surfaceMoistureQ[tileId]
    cloudQ[tileId]
    insolationQ[tileId]
    aquaticLightQ[tileId]
    volcanismQ[tileId]
```

Do not recompute identical depth, moisture, light, medium, or boundary-access transforms for each organism. After the condition/resource phase that supplies their inputs, materialize compact phase-specific tables once per tile:

```text
TilePhysiologyView:
    temperatureMilliC
    surfaceMoistureQ
    chemicalExposureInputs[]     // configured raw or stock-derived exposure inputs
    mediumClass

TileAcquisitionView:
    lightOpportunityQ
    waterBoundaryAccessQ
    gasAccessByClassQ[]
    dissolvedAccessByClassQ[]
    geologicalAccessQ

TileMovementView:
    mediumClass
    passiveMediumMultiplierQ
    edgeCompatibilityByDirectionQ[]
```

These are arrays-of-small-structs or tightly grouped field arrays selected by the consuming kernel. They contain only tile/environment facts. A `TilePhysiologyView` does not independently define organism health, because tolerance ranges and costs are phenotype-specific; the health owner combines the stored tile inputs and compiled phenotype once at its named barrier and stores the result.

The owning condition/allocation builders load one tile view, then process many rows from that tile. They combine tile facts with each organism's compiled phenotype and internal state once, store the resulting condition or phase opportunity, and seal it for consumers:

```text
conditionState = conditionOwner.MaterializeStress(
    tilePhysiologyView, phenotype.tolerance, organismState)
allocationState = allocationOwner.MaterializeReactionOpportunity(
    tileAcquisitionView,
    tileStocks,
    phenotype.metabolism,
    organismBalances)
```

## Dependency and lifetime rules

- Climate update dirties physiology, acquisition, and movement materializations for affected tiles; their owning builder stores replacements before consumers run.
- Tile stock/source/transport/biological changes dirty only effective values that actually depend on stock; the sole tile resource-effective builder refreshes them before the next consumer and at the final completed boundary. Fixed depth/medium access values remain stored and reusable.
- A DNA change creates/reuses a new compiled phenotype; it does not invalidate tile-only views.
- An organism state change dirties organism health/opportunity materializations only, never a shared tile view.
- Phase views are immutable after construction and live only through their declared consumer phases.
- Completed gameplay-relevant effective values are part of the completed world, save, hash, and client projection where authorized. Transient allocation/claim arenas and structural iteration views remain unsaved.

If a value takes only fixed geography plus rule constants—such as aquatic atmospheric-access factor by depth and gas access class—it is materialized once at world creation. If it reads current moisture/light/volcanism, its owner updates and stores it once per changed tile/tick. If it reads DNA or organism reserves, its owning phenotype/organism builder materializes it at the named barrier. The complete contract is [MATERIALIZED_DERIVED_STATE.md](MATERIALIZED_DERIVED_STATE.md).

# DNA and compiled phenotype

## Domain representation

The authoritative/domain genome remains:

```text
Genome:
    genomeId
    acquiredTraitIds             // immutable canonical set
    parentGenomeId?
    rulesHash
    canonicalGenomeHash
```

Trait families, display parents, prerequisites, incompatibilities, effects, and provenance remain typed domain records. There is no requirement to encode DNA as a packed bit vector, component mask, bytecode program, or organism-sized blob.

## Runtime representation

`CompileDNA` produces an immutable `CompiledPhenotype` organized by the biological subsystem that consumes it:

```text
CompiledPhenotype:
    genomeId
    rulesHash

    storage: CompiledStorageProfile
    metabolism: CompiledMetabolismProfile
    tolerance: CompiledToleranceProfile
    movement: CompiledMovementProfile
    sensingAndBehavior: CompiledBehaviorProfile
    lifecycle: CompiledLifecycleProfile
    predationAndDefense: CompiledInteractionProfile
    evolution: CompiledEvolutionProfile

    activationRequirements[]
    ongoingCostChannels[]
    explanationProvenance
    canonicalCompiledHash
```

Profiles use named strongly typed fields for frequently read parameters and short arrays for variable sets such as enabled reactions. They may internally contain bit flags for quick capability tests, but the domain model does not become a bitset-only representation.

Compiled phenotypes are interned per `(rulesHash, canonical acquiredTraitIds)`. A runtime-only dense phenotype slot may accelerate lookup, but it is rebuilt from `GenomeId` and is not a new persistent identity. Species reference the phenotype; organisms retain only `SpeciesId`. When an evaluation visits many organisms of one species, a derived tile/species cohort resolves the phenotype once and passes it to the kernel.

A species DNA change actually creates a descendant species/genome under v1 speciation rules rather than mutating all existing organisms in place. Preview compilation is side-effect-free; accepted compilation creates/interns the immutable profile before founders receive the new species reference.

# Species grouping and phenotype access

Three options were considered:

- physically partition organisms by `(tile, species)`, maximizing phenotype reuse but fragmenting storage as diversity rises;
- keep mixed tile chunks and look up phenotype per row, simplest but potentially branch-heavy;
- keep tile chunks authoritative and derive pooled per-tile species cohorts for phases that benefit.

V1 selects the third. The basic row always stores `SpeciesId`. At a named phase, a tile partition can build stable cohort ranges or row-index lists keyed by dense `SpeciesSlot`. A metabolism/tolerance kernel resolves the compiled phenotype once per cohort. Movement or generic lifecycle phases may simply scan mixed rows when grouping would cost more than it saves.

Cohorts are derived scratch, rebuilt after membership/speciation changes, and never determine conflict order or enter saves/hashes. The benchmark decides which phases enable them.

# Reaction kernel strategy

Reactions remain typed data, not one generated C# method per trait and not an unrestricted interpreter.

- The rule compiler validates mass/energy balance and converts resource IDs to dense handles.
- The DNA compiler selects enabled reactions and composes final ceilings, probabilities, costs, and regulation parameters.
- The phase kernel dispatches by a small closed reaction/process kind and reads a compact compiled record.
- Whole extents use direct indexed loads and checked integer arithmetic.
- Shared-work and resource allocation occurs once through the compiled process plan.
- Applied outputs write direct balance columns and tile matrices through typed mutators/change tracking.

If dispatch overhead is measurable, reactions may be grouped by kind within a phenotype plan or handled by specialized loops. Dynamic code generation, expression compilation, and native plugins are outside v1.

# Save, replay, and protocol boundaries

- Saves store logical balances by stable resource ID under a rules hash, not raw dense slot numbers alone. A compact save may include the manifest once and then dense values in its declared canonical slot order.
- Loading validates the manifest and rebuilds runtime handles/compiled phenotypes before the world can run.
- Completed gameplay materializations—tile climate/resource-effective state, organism condition/capacity values, and species aggregates—are hashed and may be serialized as complete compatible cache sections; otherwise their sole owners rebuild them before load completes. Historical recurrence inputs are primary and always saved. Tick reservations, claim arenas, tile/species row cohorts, and worker buffers are never saved.
- Client projections use stable resource IDs and actor-authorized values, never dense internal slot IDs.
- Reordering compiled runtime slots in a later compatible engine must not change logical state hashes or replay arithmetic; changing the rules manifest/hash is an explicit rules-version decision.

# Measurement and targeted experiments

The representative prototype first implements one complete baseline: uniform-`int64` quantities, `256`-row tile chunks, dense common resources, mixed tile rows with direct phenotype lookup, resource-major tile stocks, stored once-per-tile effective values, ordinary owner-mutable arrays, and detached save/publication snapshots. It profiles the entire tick, save, projection, encoding, client-apply, and render path before creating alternative storage implementations.

The following are candidate targeted experiments only when the baseline identifies their corresponding metric as material:

1. `512` versus the starting `256` organism rows per chunk when metadata, iteration overhead, occupancy, or copy behavior is limiting.
2. Tile-chunk resource-major columns against row-major fixed organism vectors.
3. Dense common internal slots against a synthetic hybrid at several nonzero densities.
4. Mixed tile scan with per-row phenotype lookup against derived tile/species cohorts.
5. Resource-major tile matrices against tile-major matrices for gas transport, all-resource tile claims, and projection capture.
6. Snapshot-copy cost versus a later copy-on-write or double-buffered capture strategy, only if boundary pause time is material.

Record CPU time, cache misses where tooling permits, bytes copied, allocations, peak memory, branch behavior where available, and code complexity. Run at:

- `10,000`, `50,000`, and `100,000` organisms;
- uniform and highly clustered tile populations;
- few-species and many-coexisting-species worlds;
- primitive one-reaction and late-game multi-process phenotypes;
- low, medium, and high internal resource occupancy.

# Invariants and acceptance tests

- A compiled resource handle resolves to exactly one compatible stable `ResourceId` under its rules hash.
- Dense balance reads/writes and save round trips preserve every signed-64-bit quantity exactly.
- Tick reservations cannot outlive their phase or make authoritative matter disappear.
- Binding cohorts reduce free availability without duplicating or moving the physical balance.
- Multiple enabled processes cannot spend the same free quantum in one tick.
- Claim-buffer grouping and residual allocation are invariant under worker completion, chunk capacity, and dense row order.
- Tile resource transport and claims produce identical results under resource-major and reference scalar implementations.
- Phase-specific tile views equal direct formula evaluation for every tile and are invalidated by every declared input.
- A phenotype compiled from the same domain DNA/rules produces identical typed numerical profiles and provenance regardless of authoring/acquisition order.
- An organism never traverses trait prerequisites/effects during a tick.
- Physical slot order, tile/species cohort construction, and protocol subscriptions do not affect world hashes.

# Remaining concrete inputs

- Compiler verification of the proposed `47`-slot common organism manifest against the final v1 reaction/acquisition catalogue.
- Conditional per-group maximum range proof and bounded-width experiment, only if uniform-`int64` organism storage is a measured bottleneck.
- Whether a measured occupancy or access bottleneck justifies moving any binding-cohort fields out of their simplest starting representation.
- Whether measured snapshot-copy bandwidth or chunk occupancy justifies changing the starting `256`-row chunks or owner-mutable field grouping.
- Exact `TilePhysiologyView`, `TileAcquisitionView`, and `TileMovementView` field lists after the first production kernels exist.
- Phases in which derived tile/species cohorts outperform simple mixed scans.
- Maximum compiled processes, reaction inputs/outputs, allocation bands, holdbacks, and binding cohorts used to size scratch without unbounded allocation.

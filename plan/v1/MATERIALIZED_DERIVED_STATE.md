# Materialized Derived State and Single-Source Computation

Status: first v1 ownership and materialization contract; exact field inventory and page grouping will be finalized with the engine prototype

Sources: [authoritative data model](DATA_MODEL.md), [resource storage and evaluation](RESOURCE_STORAGE_AND_EVALUATION.md), [trait compilation](TRAIT_SYSTEM.md), [world and climate](WORLD_AND_CLIMATE.md), [organism state and health](ORGANISM_STATE_AND_HEALTH.md), and [state change and client synchronization](STATE_CHANGE_AND_CLIENT_SYNC.md).

# Purpose

Make “single source of truth” concrete for values derived from DNA, tile state, organism balances, and world aggregates.

LYFE frequently combines the same inputs in death, acquisition, metabolism, health, reproduction, behavior, evolution, explanation, and client projection. A formula must not be reimplemented or independently recalculated by each consumer. Its owning subsystem materializes the value once when dependencies change, stores that result in a named versioned table, and every downstream consumer reads it.

# Core rule

For every gameplay-relevant derived value:

1. exactly one subsystem owns its formula;
2. the owner computes it at a named deterministic barrier;
3. the result is stored in the working/completed world or immutable compiled-rules state;
4. dependency mutation marks the materialization dirty or updates it in the same transaction;
5. no downstream system may independently reproduce the formula;
6. a dirty materialization cannot be consumed;
7. debug or load-time recomputation may verify it, but never supplies an alternate gameplay result.

“Derived” describes provenance, not permission to recalculate anywhere. “Stored” means materialized in its owning runtime/version table for all consumers. Completed values that affect continuation or player-observable state are also serialized under the save policy below.

# What qualifies for materialization

Materialize a derived value when at least one is true:

- two or more phases/subsystems consume it;
- many organisms reuse the same tile/species calculation;
- the calculation traverses definitions, composes modifiers, expands resources, evaluates a curve, or otherwise costs more than a trivial scalar operation;
- it is used for authoritative branching, probability, contention, capacity, health, or resource accounting;
- the player can inspect or receive the value and expects every explanation to match gameplay;
- its change should naturally produce a client diff or historical aggregate;
- retaining it materially simplifies deterministic continuation or fault diagnosis.

Do not materialize a value solely because it can be named. A one-use addition, comparison, array offset, or whole-extent quotient can remain inside its one owning kernel. The distinction is configured/documented, not decided ad hoc by each caller.

Conversely, do not remove or duplicate a shared gameplay materialization merely to save a field or a few bytes. Single-source consistency is the default. Changing its storage lifetime or physical encoding requires representative evidence that it materially constrains simulation or synchronization capacity while retaining one formula owner and identical consumer-visible semantics.

# State categories

| Category | Examples | Owner/lifetime | Saved? |
| --- | --- | --- | --- |
| Primary authoritative state | resource balances, structure assignments, birth tick, moisture memory, acquired traits | World/entity stores | Yes |
| Immutable compiled state | dense resource handles, reaction records, final phenotype values, process plans, fixed tile transforms | Rule/genome/world compiler | Stored once; save references rules/genome hashes and validates compiled hashes |
| Materialized current state | tile current/effective parameters, accessible stocks, organism used load/capacity/health/activation, species aggregates | Named phase builder in transactional pages | Completed-boundary values needed for continuation or observation are saved |
| Phase materialization | internal allocation results, claim/grant tables, death-risk scratch before a death record | Named phase arena/output | Not saved because no save occurs mid-transaction |
| Derived structural index | ID locator if omitted durably, spatial bins, tile/species row cohorts | Storage/index builder | Rebuilt/verified; never a gameplay formula source |
| Presentation-only derivation | chart pixels, interpolation, localized prose | Client | No |

# One-way dependency graph

```text
Domain rules ──compile──> CompiledRuleTables
      │                         │
Acquired DNA ──compile──> CompiledPhenotype
      │                         │
World fixed inputs ──────> WorldStaticEffectiveTables
                                │
Current climate/resource state ─┴─> TileCurrentEffectiveState
                                           │
Organism balances/structure/lifecycle ─────┴─> OrganismMaterializedState
                                                      │
Completed organism state ─────────────────────────────┴─> Species/Tile Aggregates
                                                                 │
                                                Events, saves, projections, UI
```

Arrows never point back upward. Client projections, charts, aggregates, health summaries, or cached effective values cannot become inputs to the rules/DNA compiler or primary balances unless a separately specified gameplay command transfers an explicit player decision.

# Ownership and update barriers

| Materialization | Sole writer | Recompute/update boundary | Consumers |
| --- | --- | --- | --- |
| `CompiledRuleTables` | Rule-pack compiler | Rule-pack load only | World generator, DNA compiler, every kernel |
| `CompiledPhenotype` | DNA compiler | New unique genome/preview; immutable afterward | Organism phases, evolution preview, explanation |
| `WorldStaticEffectiveTables` | World compiler | World creation/load migration only | Climate, resource transport, movement, projection |
| `TileClimateState` | Phase 1 climate owner | Every tick for affected/all tiles | Phases 2–11 |
| `TileResourceEffectiveState` | Tile resource-effective builder | After phase 2 sources/sinks/transport and every later barrier that changes relevant tile stocks; mandatory final refresh | Death exposure, movement gates, acquisition, metabolism, display |
| `OrganismAllocationState` | Named phase-5/7 allocator | Once per allocation barrier | Competing internal processes and commit |
| `OrganismConditionState` | Health/condition builder | Named pre-death and end-health barriers | Death, metabolism modifiers, reproduction, behavior, aggregation, display |
| `OrganismCapacityState` | Structure/resource mutator + capacity builder | In same commit as dependency change or next mandatory barrier | Admission, health, reproduction, display |
| `SpeciesAggregateState` | Phase 10 species reducer | Once after lifecycle/behavior completion | Mutation income, autonomy, gameplay, projection |
| `ActorKnowledgeProjectionState` | Phase 11 knowledge owner | Completed boundary | Authorization/projector |

# Dirty domains and generation stamps

Primary mutators mark explicit dependency domains rather than asking consumers to detect change:

```text
DirtyDomain:
    TileClimate
    TileResourceStock(resourceClassOrSlot)
    OrganismResourceBalance(capacityGroupOrSlot)
    OrganismBinding(resourceSlot)
    OrganismStructure
    OrganismLifecycle
    OrganismBehaviorMemory
    SpeciesMembership
    SpeciesGenome
```

Materialized tables carry the smallest practical generation stamp:

```text
MaterializationStamp:
    rulesHash
    worldRevision
    completedTick
    producingPhase
    dependencyGeneration(s)
```

Release builds may represent generations through page/table epochs rather than a stamp on every row. Debug builds must prove that no consumer reads a dirty or mismatched result. Lazy “getter recalculates on miss” behavior is prohibited in authoritative phases because it makes ownership, timing, parallelism, and change capture harder to reason about.

At the barrier, the owner processes dirty IDs/tiles in canonical order, writes the new materialization through typed mutators, records logical field changes, clears the exact dirty domains, then seals the view for consumers.

# Compiled rule tables

The human-authored rule pack remains domain-oriented. `CompileRulePack` produces one immutable numerical runtime root:

```text
CompiledRuleTables:
    rulesHash
    resourceManifest
    reservoirSlotManifests
    reactionTable
    processKindTable
    costChannelTable
    curveTable
    probabilityTable
    transportClassSlotLists
    exposureClassTable
    behaviorAndPressureDefinitions
    traitGraphAndCompiledEffects
    scenarioRules
    canonicalCompiledHash
```

## Resource manifests

One stable `ResourceId` maps to one definition and zero or one dense slot per compatible reservoir group:

```text
CompiledResourceDefinition:
    resourceId
    kind
    compositionVector
    biologicalForm
    phase
    energyDensityQ
    storageLoadQ
    allowedReservoirMask
    uptakeAndTransportFlags
    slotByReservoirGroup[]
```

Every process, source, sink, transport rule, and projection mapper uses these shared handles. No subsystem builds a competing `ResourceId -> slot` map or re-expands composition in a hot phase.

## Reactions and curves

Reaction balance, dense input/output handles, integer stoichiometry, energy yield, whole-extent rules, and typed boundary accounts compile once into the reaction table. Health, tolerance, probability, and age curves compile into canonical fixed-point coefficients/tables once. Runtime kernels look them up by dense typed handle and never reinterpret authoring expressions.

# Compiled phenotype tables

The domain genome retains the exact acquired traits and provenance. Its materialized numerical result is one immutable `CompiledPhenotype` as defined in [RESOURCE_STORAGE_AND_EVALUATION.md](RESOURCE_STORAGE_AND_EVALUATION.md).

The compiler stores both:

- final operational values consumed by kernels; and
- provenance/explanation entries that produced each value.

The explanation layer reads the same stored final value. It does not repeat additive/multiplicative trait composition to “explain” the result.

```text
CompiledNumeric<T>:
    value: T
    provenanceRange

CompiledMetabolismProfile:
    processPlan
    sharedWorkBudgetQ
    reserveMobilizationLimitQ
    biomassAssemblyLimitQ
    regulationProfile
    fixedCostTotalsByChannel[]
```

When a new genome appears, compilation and validation complete before any organism receives its species reference. The compiled result and hash become immutable. A later engine optimization may change its physical layout only if the logical compiled hash and all numerical outputs remain identical under the same rules version.

# Materialized tile state

## Static effective tables

World creation materializes values that depend only on fixed geography and rules:

```text
WorldStaticEffectiveTables:
    neighborTileId[direction][tile]
    edgeId[direction][tile]
    mediumClass[tile]
    gasAccessFactorQ[gasAccessClass][tile]
    dissolvedEdgeCompatibilityBaseQ[edge]
    lightDepthAttenuationQ[tile]
    fixedClimateCoefficients[coefficient][tile]
    sourceProfileHandles[tile]
```

All later phases read these values. They do not call the elevation/depth/topology formulas again.

## Current climate materialization

Phase 1 writes one canonical current value for every condition used later:

```text
TileClimateState:
    temperatureMilliC[tile]
    precipitationMicrometersPerHour[tile]
    surfaceMoistureQ[tile]
    cloudQ[tile]
    surfaceSolarOpportunityQ[tile]
    aquaticLightOpportunityQ[tile]
    volcanismQ[tile]
    effectiveDissolvedAccessQ[tile]
    effectiveGeologicalAccessQ[tile]
    passiveMovementMediumMultiplierQ[tile]
    climateGeneration
```

Surface moisture and active volcanic pulses retain their primary historical state; the other fields are derived but materialized. Phases 2–11 and client publication read this table, not the climate formulas.

## Post-ledger resource materialization

After phase 2 changes stocks and transport, the tile resource-effective builder writes the values used by organism death, movement, and external-intent evaluation:

```text
TileResourceEffectiveState:
    accessibleAtmosphericStockQ[atmosphericSlot][tile]
    accessibleDissolvedStockQ[dissolvedSlot][tile]
    chemicalExposureQ[exposureClass][tile]
    captureOpportunityQ[opportunityClass][tile]
    limitingTileResourceClass?[tile]
    resourceGeneration
```

This table is not another resource account. It cannot be debited and never participates in conservation. Claims still debit the primary stock matrix, but request construction, exposure, and explanation use the stored effective values. A grant resolver caps against the same primary stock generation named in the materialization stamp.

Biological acquisition, environmental production, waste release, decay, or any later phase that changes a relevant tile stock dirties this table. The same builder refreshes affected tiles before another authoritative consumer and performs a mandatory final refresh before the completed boundary is hashed, saved, or projected. There is one formula owner even though several phase barriers may schedule it.

If a value is meaningful only after organism-specific traits—such as temperature stress against a phenotype's tolerated range—the tile table stores the environmental side of the calculation, not one value per possible species. An occupied tile/species cohort may materialize a shared combined response when two or more phases demonstrably reuse it; that optimization has an explicit owner and sparse `(tile, species)` lifetime.

# Materialized organism state

Frequently consumed results are stored in dedicated resource/condition pages:

```text
OrganismMaterializedState:
    usedLoadQ[capacityGroup][row]
    boundQuantityQ[bindableResourceSlot][row]
    commissionedCapacityQ[capacityGroup][row]
    storedUsableEnergyQ[row]
    bodyRadiusQ[row]
    ageThroughputFactorQ[row]
    activeCapabilityMask[row]

    preDeathHealthQ[row]
    endHealthQ[row]
    limitingHealthFactorId[row]
    significantStressMask[row]
    materializationGenerations[...] 
```

Rules:

- Balance mutations update used load and stored usable energy in the same transaction or mark the row for the mandatory next barrier before any consumer.
- Binding creation/release updates `boundQuantityQ`; allocators use it rather than resumming cohorts for every process.
- Structure/organization changes update commissioned capacities and body radius once.
- Age advance updates the stored age-throughput factor once for the tick; consumers do not reevaluate the age curve.
- Pre-external quota promotion and state/environment gates update the active capability mask at its named barrier.
- Health is evaluated at its named snapshots and stored. Reproduction, behavior, mutation aggregation, events, and client projection all use the same `endHealthQ`.
- Detailed factor/provenance data may live in typed diagnostic side pages or aggregate masks, but the final operational value is never reconstructed for display.

The materialized columns do not become independently writable attributes. Only their owner can update them from dirty primary dependencies.

# Phase allocation materialization

Internal allocation is transient but still single-source. The allocator computes free inventory, protected holdbacks, whole feasible extents, shared-work assignments, and probabilistic opportunity admissions once into an `OrganismAllocationState` held in the phase arena. Every process execution consumes that stored plan.

No reaction kernel independently asks “how much resource would I get?” after allocation. Resolution may reduce a shared external grant, but it writes the final grant into the same plan before execution. The plan expires only after commit/discard of the phase.

```text
OrganismAllocationState:
    organismId
    allocationBarrier
    inputStockGeneration
    processAllocations[]:
        processPlanIndex
        admittedWholeExtents
        reservedInternalInputs[]
        requestedExternalInputs[]
        grantedExternalInputs[]
        assignedWorkQ
        attemptDrawResult?
```

# Materialized aggregates

Phase 10 stores, rather than repeatedly recounts:

```text
SpeciesAggregateState:
    population
    populationByOccupiedTile[]
    averageEndHealthQ
    behaviorCounts[]
    deathRiskAndCauseBuckets[]
    reproductionTrend
    currentMutationIncomeInputs
    aggregateGeneration
```

Tile resource-flow buckets are already materialized from applied ledger transactions. Evolution, alerts, player explanations, histories, and publication read these stored aggregates. Individual organism behavior remains prohibited from reading species-wide aggregates.

# Save and load policy

The completed world save includes materialized current values that:

- feed the next tick or a future decision;
- represent the completed boundary shown to the player;
- would otherwise require rerunning an already committed phase; or
- are needed to diagnose/hash the exact completed result.

This includes completed tile climate/resource-effective state, organism capacity/condition materializations, and species aggregates. Phase arenas and rebuildable structural indexes remain excluded.

Compiled rule/phenotype tables may be stored once in a rules package/cache rather than duplicated in every save, but the save records their canonical hashes and genome references. Load resolves those exact compiled tables and rejects a mismatch.

Load does not silently replace a stored materialization by recalculating it under current code. It validates schema/rules/dependency stamps and may run a debug verification oracle. Rebuilding a missing materialization belongs to an explicit versioned migration that produces a new compatible save, not ordinary continuation.

# Hashing, diffs, and explanations

- Completed materialized gameplay values participate in the authoritative state hash alongside their primary dependencies. This detects stale update bugs early.
- Change capture marks the materialized field when its owner writes a new value. Projectors read it directly rather than independently deriving health, access, capacity, or aggregate values.
- Client explanations carry the stored value plus stored provenance/input references. A client may format or scale it, but does not reproduce authoritative formulas.
- A debug recomputation oracle compares primary inputs with materialized outputs at configured boundaries. A mismatch faults validation; the recomputed value is never substituted into the live world.

# Update pseudocode

```text
CommitPhase(primaryOutcomes):
    ApplyPrimaryMutations(primaryOutcomes, dirtyDomains, changes)

    for builder in MaterializationBuildersOwnedByThisBarrier:
        targets = builder.ResolveDirtyTargets(dirtyDomains)
        for target in targets in canonical stable order:
            value = builder.ComputeFromDeclaredDependencies(target)
            builder.Store(value, dependencyGenerations, changes)
        builder.ClearOwnedDirtyDomains(targets)

    AssertNoConsumerVisibleDirtyDomains()
    SealNextPhaseView()
```

```text
CompileGenome(domainGenome, compiledRules):
    validate domain trait set
    compose every typed numerical attribute once
    resolve resource/reaction/process handles once
    build subsystem profiles and explanation provenance
    hash canonical outputs
    store immutable CompiledPhenotype
```

# Failure behavior

- Missing, dirty, stale, or mismatched materialized state is an invariant failure, not a cache miss.
- Overflow or invalid domain output aborts the working transaction.
- A compiler failure prevents the genome/rule pack/world from becoming active.
- Materialization builders may not consume client visibility, subscriptions, wall-clock time, or unordered worker results.
- A failed builder leaves the previous completed root authoritative through the normal copy-on-write transaction.

# Tests

- Every declared dependency mutation marks or synchronously updates its materialization.
- Deliberately omitted dirty marks are caught by debug dependency-generation tests.
- No authoritative consumer invokes a derivation formula outside its owning compiler/builder.
- Stored compiled phenotype values match golden trait compositions and explanation provenance.
- Stored tile effective values match scalar formula oracles across world/climate/resource fixtures.
- Stored organism used load, bound totals, capacities, energy, radius, throughput, activation, and health match recomputation oracles after every relevant mutation type.
- Phase allocation computes each process admission once and prevents independent re-admission during execution.
- Stored species/tile aggregates reconcile with entity/ledger state at every completed boundary.
- Save/load preserves materialized values and generations exactly; a mismatch fails rather than silently healing.
- State changes and client projections expose the same stored operational values used by gameplay.
- Worker count, page layout, dirty-target ordering, and subscriptions do not change materialized results or hashes.

# First implementation sequence

1. Implement compiled resource/reaction tables and stable dense handles.
2. Implement domain `Genome` plus typed `CompiledPhenotype` and golden compilation fixtures.
3. Implement world-static tile effective tables during generation/load.
4. Implement phase-1 `TileClimateState` and phase-2 `TileResourceEffectiveState` with dependency generations.
5. Implement organism used-load, bound-total, capacity, stored-energy, radius, age-throughput, activation, and named health materializations.
6. Make internal allocation emit one phase-owned stored plan consumed by process execution.
7. Persist and hash completed materializations; add debug recomputation oracles.
8. Profile materialization and field/page grouping in the complete simulation/publication path. Change a materialization's lifetime or representation only after the optimization gate is met, while preserving one formula owner and one stored result for every shared gameplay value.

# Remaining detailed choices

- Exact materialized field catalogue after all v1 kernel inputs are enumerated.
- Whether sparse occupied `(tile, species)` effective-response materializations save enough repeated work to justify storage.
- Per-row versus page/table dependency generations in release builds.
- Which detailed explanation inputs remain always resident versus generated from retained provenance on demand.
- Save schema grouping and compatibility/migration behavior for newly added materialized fields.

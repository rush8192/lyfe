# Deterministic Parallel Evaluation, Reduction, and Commit

Status: first v1 execution-semantics contract; concrete C# buffer types and phase-specific performance activation remain for the engine prototype

Sources: [simulation loop](SIMULATION_LOOP.md), [keyed randomness](KEYED_RANDOMNESS.md), [world execution and ownership](WORLD_EXECUTION_AND_OWNERSHIP.md), [entity identity and storage](ENTITY_IDENTITY_AND_STORAGE.md), [materialized derived state](MATERIALIZED_DERIVED_STATE.md), [resource storage and evaluation](RESOURCE_STORAGE_AND_EVALUATION.md), and [state change and client synchronization](STATE_CHANGE_AND_CLIENT_SYNC.md).

# Purpose

Define one authoritative phase result independent of worker count, work partitioning, completion order, dense storage order, and scheduling. Parallel workers may accelerate evaluation, but they cannot change gameplay semantics, allocate stable IDs, mutate world state, resolve shared contention opportunistically, or append globally ordered events.

# Decision summary

- The scalar one-worker executor is the reference implementation and first vertical-slice default.
- Parallel and scalar execution call the same phase evaluator, reducer, preflight, and commit code; parallelism changes only how independent work items are evaluated.
- Every phase begins from an immutable, generation-stamped phase view.
- Workers write only to isolated bounded/pooled outcome buffers. An outcome carries a stable logical key and the input generation it observed.
- The owner concatenates buffers, validates them, groups by explicit conflict keys, and resolves each group from the same phase snapshot.
- Integer/fixed-point reductions use checked `Int128`/`UInt128` accumulators and round once at the named owner. Floating-point reduction is prohibited.
- Accepted creations receive IDs only after resolution, in total `CreationKey` order.
- The world owner applies one canonical mutation plan through typed store/ledger mutators, runs required materialization builders, and seals the phase journal.
- A phase barrier is internal to the working tick. No partial phase or tick becomes saveable, queryable, or publishable.
- Parallel execution is enabled phase by phase only after representative profiling shows a meaningful benefit.

# Semantic model

Each phase is logically a pure evaluation followed by one authoritative transition:

```text
(stablePhaseView, acceptedBoundaryInputs)
    -> evaluated outcomes
    -> canonical grouped resolution
    -> preflighted mutation plan
    -> authoritative phase state + phase journal
```

Changing worker count may change where and when an outcome is computed. It cannot change the stable input, semantic random address, conflict group, arithmetic, accepted outcome, assigned ID, mutation order, materialized result, event order, or journal.

# Starting execution policy

The first end-to-end implementation sets effective simulation degree of parallelism to one while using the final worker-buffer and reduction interfaces. This supplies a readable correctness oracle without building a disposable separate engine.

After the representative profile exists:

- enable parallel evaluation for a phase only when its evaluation cost is material;
- prefer tile-level work because resources, spatial interactions, and organisms are tile-owned;
- retain owner-thread resolution/commit initially;
- introduce parallel reducers or disjoint-page commit only if those exact steps become bottlenecks and equivalence tests remain exhaustive;
- keep world-level command admission, stable ID assignment, root swap, and publication sealing owner-only.

The runtime worker count and partition size are operational configuration, not authoritative state. They do not enter saves, RNG addresses, events, state hashes, or protocol values.

# Phase execution classes

```text
PhaseExecutionClass:
    OwnerOnly
    IndependentMap
    MapThenGroupedResolve
    ExactAggregateReduce
```

| Phase | Initial class | Logical work key | Reduction/commit notes |
| --- | --- | --- | --- |
| 0. Command admission | `OwnerOnly` | accepted replay order | Revalidate and atomically apply commands; allocate command-originated creations canonically |
| 1. Climate/conditions | `IndependentMap` | `TileId` | Store one tile materialization; any shared weather event uses a separately defined group/owner |
| 2. Environmental ledger | `MapThenGroupedResolve` | `TileId`, `EdgeId`, or source-rule range | Edge transport emits paired deltas from one stable stock view; reduce by tile/account and preserve edge-owned remainders |
| 3. Intrinsic death | `IndependentMap` | `OrganismId` | Evaluate all positive causes; commit age/death in organism order and assign remnant IDs by creation key |
| 4. Movement | `IndependentMap` | `OrganismId` | Integrate from one survivor view; apply relocations in canonical source/destination/entity order |
| 5. External intent | `IndependentMap` | `OrganismId` | Produce immutable claims/actions only; no shared debit or target mutation |
| 6. External resolution | `MapThenGroupedResolve` | primarily `TileId` | Tiles are independent in v1; within each tile resolve ordinary claims, scavenging, and predation in the specified semantic order |
| 7. Internal metabolism | `MapThenGroupedResolve` | `OrganismId` | Organism transaction is private, but tile waste/death/remnant contributions reduce by stable tile/account keys |
| 8. Lifecycle/reproduction | `IndependentMap` | `OrganismId` | One parent transaction; accepted offspring IDs use reproduction creation keys |
| 9. Behavior | `IndependentMap` | `OrganismId` | Observations come from the sealed behavior view; choices write next-tick state only |
| 10. Species systems | `ExactAggregateReduce` | `SpeciesId` plus tile where required | Combine integer counts/sums, materialize aggregates, then evaluate each species in stable order |
| 11. Finalization | `OwnerOnly` initially | canonical store/field order | Final histories, knowledge, validation, logical hash, root commit, and journal seal |

This table defines semantics, not a promise to create one task per work key. A worker may process a contiguous physical range or several tiles, provided every outcome retains its stable logical identity.

# Phase descriptors and stable views

```text
ParallelPhaseDescriptor:
    phaseId
    executionClass
    viewBuilder
    workEnumerator
    evaluator
    outcomeValidator
    groupKeySelector?
    resolver
    preflight
    commit
    materializationBuilders[]
    parallelActivationPolicy
```

```text
PhaseViewStamp:
    workingWorldRevision
    tick
    phaseId
    primaryStoreGenerations[]
    materializationGenerations[]
    rulesHash
```

`SealPhaseReadView` exposes immutable pages and completed materializations valid for that phase. A worker cannot acquire writable spans, retain a view after the barrier, load newer working state, or ask a projector/client for data.

Every outcome repeats the minimum view stamp needed to reject stale or cross-phase buffers. Release builds may validate one shared buffer stamp rather than each record; debug builds can validate both.

# Work partitioning

The canonical work set is defined by logical entities/tiles/edges present in the phase view. The physical scheduler may partition it differently at different worker counts.

```text
WorkPartition:
    phaseId
    diagnosticPartitionOrdinal       // never gameplay input
    logicalRangeDescription
    workItemsOrPhysicalRanges
```

Rules:

- Partition and worker IDs never enter RNG addresses, outcome keys, remainders, IDs, events, or hashes.
- Dense row, chunk, locator-page, and thread order are not logical order.
- A physical range may enumerate rows in its cheapest order because keyed randomness and total outcome keys remove scheduling meaning.
- Spatial queries return candidates in canonical stable-ID order after exact filtering, or the evaluator explicitly sorts its chosen candidate set before a weighted draw.
- Each logical work item is evaluated exactly once. Debug coverage compares the enumerated input-key set with emitted terminal status/outcome coverage.
- Empty partitions are harmless and produce no semantic artifact.

# Worker outputs

Workers receive a phase view, compiled rule/phenotype handles, a work partition, and a private pooled buffer set:

```text
PhaseWorkerOutput:
    phaseId
    viewStamp
    evaluatedWorkCount
    outcomes[]
    contributions[]
    creationIntents[]
    diagnosticCounters
    failure?

OutcomeEnvelope:
    outcomeKey
    kind
    expectedEntityGeneration?
    payload

OutcomeKey:
    phaseId
    semanticCategoryId
    scopeId                  // commonly TileId or SpeciesId
    primaryStableId
    secondaryStableIdOrDefinitionId
    localOrdinal
```

The key is a total logical order. Every field has a documented meaning for the outcome kind. If two non-mergeable records have the same key, the phase fails; buffer append order never resolves the ambiguity.

Worker buffers:

- are ordinary typed arrays/lists in the first implementation;
- may be pooled after profiling, but pooling cannot expose old entries;
- carry stable IDs rather than writable dense-row references across the barrier;
- contain proposed deltas/transactions, not direct references to mutable accounts;
- are discarded entirely if the phase or tick fails;
- expose diagnostic counters separately so metrics cannot affect semantic output.

# Canonical reduction pipeline

The following is the shared skeleton for phases 1 through 10. Owner-only phases 0 and 11 bypass work scheduling but use the same typed mutation, materialization, invariant, and journal boundaries; their explicit ordering appears in [WORLD_EXECUTION_AND_OWNERSHIP.md](WORLD_EXECUTION_AND_OWNERSHIP.md).

```text
ExecutePhase(workingWorld, descriptor):
    view = descriptor.viewBuilder.Seal(workingWorld)
    work = descriptor.workEnumerator.Enumerate(view)
    partitions = SchedulePhysicalPartitions(work, runtimeWorkerCount)

    buffers = EvaluatePartitions(partitions, view, descriptor.evaluator)
    if any worker failed:
        fail with CanonicalWorkerFailure(buffers)

    records = Concatenate(buffers)
    ValidateCoverageAndViewStamps(work, records, view.stamp)
    SortByTotalLogicalKey(records)

    resolved = descriptor.resolver.ResolveFromStableView(view, records)
    mutationPlan = descriptor.preflight.ValidateAndBuild(view, resolved)
    AssignAcceptedCreationIds(mutationPlan.creationIntents)

    descriptor.commit.ApplyThroughTypedMutators(workingWorld, mutationPlan)
    RunOwnedMaterializationBuilders(workingWorld, descriptor.phaseId)
    ValidatePhaseInvariants(workingWorld, mutationPlan)
    SealPhaseJournalAndNextView(workingWorld)
```

The sort implementation need not itself be stable because the comparison key is total. Records that legitimately contribute to one aggregate share a documented group prefix but retain a unique contributor suffix. Reducers ignore physical record adjacency beyond the canonical grouping contract.

# Reduction arithmetic

## Counts and sums

- Accumulate nonnegative counts and quantities in checked `UInt128`.
- Accumulate signed resource deltas and fixed-point numerators in checked `Int128`.
- Combine all exact contributions first, apply a named division/rounding rule once, validate the destination range, then narrow once.
- Do not round worker partials, average averages, or sum floating-point values.
- A configuration/range proof must bound the complete group total, not merely one worker partial.

Example species health aggregation:

```text
for organism in species contributors:
    population += 1
    healthSum += organism.endHealthQ

averageHealthQ = DivideWithNamedRounding(healthSum, population)
```

Workers may emit exact `(population, healthSum)` partials. Combining them is mathematically identical because no division occurs until the sole species reducer.

## Resource deltas

Every contribution names a concrete ledger account and cause. The reducer groups by canonical account key, sums exact signed deltas, and preserves the underlying transaction bundle needed for conservation validation.

```text
AccountDeltaKey:
    reservoirKind
    ownerStableId              // tile, organism, remnant, or boundary account
    resourceId
    causeCategory
    transactionBundleId
```

Netting deltas for efficient application cannot erase the debit/credit pairing, energy disposition, or cause record required by the conservation oracle. A group preflight rejects underflow, overflow, incompatible storage, stale generation, or an unaccounted output before any mutation is applied.

## Fractional remainders

Persistent rate/exchange remainders have exactly one logical owner such as `(edgeId, resourceId, exchangeRuleId)` or `(tileId, resourceId, sinkRuleId)`. The owner is evaluated once from its prior remainder and writes one next remainder. Workers never independently round partitions of the same remainder.

## Random residual rank

When indivisible residual matter intentionally rotates among otherwise eligible claimants, the resolver uses the registered keyed-random domain and stable claimant/group address from [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md). It sorts by `(randomRank, canonicalClaimantKey)`. Random rank changes allocation within the already proved residual only; it never changes group supply or eligibility.

# Shared conflict groups

## Ordinary tile-resource claims

```text
OrdinaryClaimGroupKey:
    tileId
    reservoirKind
    resourceId
    priorityClass
```

Before weighting, merge requests from the same organism/account under the rule-defined process-allocation policy so splitting one request cannot multiply influence. Resolve each priority class from the remaining stable supply, use checked proportional arithmetic, then distribute whole residual quanta by keyed rank. Emit grants in canonical claimant order.

## Coupled reaction claims

A claim requiring several resources is one atomic bundle. Within a tile, construct deterministic connected conflict components over the resources and claims they share. Resolve the whole component with the normative coupled-claim algorithm; do not independently reduce one input and strand another. Component identity/order derives from the smallest stable resource/claim key, not graph traversal order.

## Remnant scavenging

Claims group by `RemnantId`, then by content/destination constraints. Admission costs and cooldown behavior are decided from the phase snapshot as already specified. Resolution cannot grant more than the remnant held at the snapshot or more than each claimant can accept. All ungranted matter remains in the one remnant transaction.

## Predation

Within one tile:

1. validate every admitted attack against the shared post-movement snapshot;
2. evaluate every keyed kill draw, including simultaneous mutual attacks;
3. determine the set of prey deaths once—multiple successes still create one death/remnant;
4. exclude attackers that are themselves dead from immediate feeding;
5. group successful surviving predators' capped resource claims by prey and resolve feeding weights/remainders;
6. transfer grants and all ungranted prey contents through one atomic death/remnant transaction;
7. emit one death record carrying all positive risks and successful triggers.

This is simultaneous outcome resolution, not “first attacker in the buffer wins.” Newly created predation remnants are absent from the phase-5 scavenging snapshot and cannot cascade into another current-tick action.

# Movement and migration commit

Movement evaluation emits each organism's final source/destination tile, final local position, component-attributed displacement, energy debit, and migration result. V1 does not model physical collision exclusion, so two organisms may independently occupy nearby/equal representable coordinates.

Accepted movement plans sort by:

```text
(sourceTileId, destinationTileId, organismId)
```

The world owner applies complete logical row migrations through the storage transaction. Swap-removal side effects update locators but have no semantic order. A destination append slot is never an RNG or event input.

# Stable creation and ID assignment

Workers use temporary phase-local references and never reserve numeric IDs:

```text
PendingCreation:
    entityType
    creationKey
    payload

CreationKey:
    phaseId
    creationKind
    scopeTileOrSpeciesId
    primarySourceId
    secondarySourceIdOrOrdinal
    localOrdinal
```

After conflict resolution rejects invalid creations, the owner sorts accepted creations independently per entity type by the complete key, rejects duplicates, increments the authoritative next-ID counter, and patches all phase-local references before applying mutations.

Examples remain:

- offspring: lifecycle phase, parent tile, parent organism, successful-reproduction ordinal;
- intrinsic remnant: terminal phase, death tile, dead organism;
- predation remnant: external-resolution phase, tile, prey organism;
- descendant species: accepted command replay order, ancestor species, speciation ordinal.

A failed or rejected creation consumes no ID. A later failed phase discards the entire tick fork, including tentative counter changes, so replay from the prior completed root assigns the same IDs.

# Preflight and authoritative commit

The resolver produces a `PhaseMutationPlan`, not arbitrary callbacks:

```text
PhaseMutationPlan:
    expectedViewStamp
    accountTransactions[]
    entityFieldWrites[]
    relocations[]
    removals[]
    acceptedCreations[]
    bindingAndCooldownWrites[]
    historyContributions[]
    canonicalEventDrafts[]
    dirtyDomains[]
```

Preflight validates the complete plan against the stable view and interactions among its own accepted outcomes. It must prove:

- every referenced entity/account exists in the expected generation or is an accepted pending creation;
- every debit, credit, capacity, reservation, binding, and cooldown is valid;
- all resource/energy transaction bundles reconcile;
- a removed entity is terminal exactly once;
- a parent reproduces at most once and an offspring cannot act in its birth tick;
- all outputs have storage, waste, remnant, or explicit boundary destinations;
- ID and arithmetic counters cannot overflow;
- every required materialization will be synchronized before a consumer.

The initial commit is single-owner and applies records in canonical semantic category/key order through typed mutators. This order aids diagnostics but cannot be used to implement “remaining supply”; shared allocation was already fixed by the resolver. Mutators update primary state and change capture together. Materialization builders then produce the one stored derived result for the next consumers.

# Phase journal and publication

Each successful internal phase seals a phase-local journal in canonical logical order. Journals from phases `0..11` merge in phase order into the tick journal. The journal identifies what changed; it is not a replay log and cannot substitute for primary state or events.

Only after the full working tick validates and its root swaps atomically does the server expose the completed view and sealed `TickChangeSet`. Projection and client batching may lag or coalesce without altering reduction or commit.

# Errors, cancellation, and diagnostics

- A worker catches/returns a structured failure tagged with its logical work key. The runner reports the failure with the smallest canonical key as primary and may retain the others diagnostically; completion time does not choose the visible cause.
- Any worker, coverage, stale-view, reduction, preflight, materialization, conservation, or commit failure discards the entire working tick.
- Shutdown/cancellation during worker evaluation discards the working tick and stops at the prior completed root. Retrying the tick evaluates the same addresses and outcomes.
- Operational deadlines may report that the configured speed cannot be sustained, but they cannot skip work or partially commit a phase.
- Diagnostic counters merge separately from semantic outputs and may vary in timing/partition detail; canonical outcome totals must not.
- Logs, tracing, and metrics receive no writable state and invoke no RNG.

# Scalar/parallel equivalence oracle

Every scenario capable of parallel execution can run through:

```text
Reference: workerCount = 1, smallest/simple partitions
Candidate: workerCount in supported set, varied partition sizes and schedules
```

Compare after every phase in debug test runs:

- canonical primary-state hash by logical store/field;
- completed materialized-state hash and generations;
- account/conservation transaction digest;
- creation keys and assigned IDs;
- canonical event/death records;
- phase and tick change journals;
- persistent remainders and ordinals.

Comparisons should locate the first divergent phase and smallest logical key rather than returning only one final world hash.

# Performance instrumentation

Per phase record:

- stable input work count and outcome/contribution count;
- evaluation, concatenate/sort, group resolution, preflight, commit, materialization, validation, and journal time;
- worker utilization and skew by tile/work range;
- buffer capacity/high-water mark and allocations;
- bytes sorted and number/size of conflict groups;
- exact-reduction and keyed-rank counts;
- copied transactional pages and dirty fields;
- scalar versus enabled-worker speedup and total server throughput under projection load.

Do not enable complex reducers merely because evaluation scales in a microbenchmark. The relevant result is end-to-end tick capacity with server/client synchronization active.

# Required tests

- One, two, and supported maximum workers produce identical phase hashes, final hashes, IDs, events, remainders, materializations, and logical journals.
- Randomized worker delays, reversed buffer concatenation, changed partition sizes, chunk compaction, and dense-row permutations do not change results.
- Each logical input is covered exactly once; missing and duplicate work fail deterministically.
- Equal outcome keys fail unless their schema declares an exact associative group contribution.
- Partial sums combine in checked wide integers and round only once at the owner.
- Edge transport processes each canonical edge once, preserves paired conservation, and writes one edge remainder.
- Ordinary and coupled claims remain within stock, preserve priorities/weights, and are invariant under claim enumeration.
- Multiple scavengers cannot overdraw a remnant; cooldown/cost outcomes match the stable admission snapshot.
- Mutual/multiple predation produces the same deaths, feeding exclusions, grants, triggers, and single remnants under every schedule.
- Movement/migration preserves identity and balances under arbitrary swap-removal/append slots.
- Rejected creations consume no IDs; accepted IDs follow creation-key order and survive save/load replay.
- A failure injected in evaluation, reduction, preflight, commit, materialization, or final validation publishes nothing and restores the prior completed root.
- Client subscriptions, projection load, save activity, logging, and metrics do not change any semantic result.
- Parallel activation can be disabled without changing a save's future or RNG outcomes.

# Initial implementation sequence

1. Define phase/view stamps, total outcome keys, typed worker buffers, and the scalar scheduler.
2. Implement the shared concatenate/validate/sort/preflight/commit skeleton with phase-level divergence diagnostics.
3. Implement exact aggregate and account-delta reducers with wide arithmetic and one-time rounding.
4. Implement canonical creation keys, pending references, and owner-only ID assignment.
5. Port phases incrementally through the skeleton: climate, intrinsic death, movement, metabolism/lifecycle, behavior/species aggregation, then environmental and external conflict groups.
6. Implement the phase-specific ordinary claim, coupled claim, scavenging, and simultaneous predation resolvers.
7. Integrate materialization builders and phase/tick change journals into the same commit path.
8. Add randomized schedule/partition equivalence tests and failure injection.
9. Profile the complete scalar engine; enable bounded parallel evaluation only for measured phases and rerun equivalence/throughput suites.

# Remaining implementation inputs

- Exact typed outcome/payload catalogue per phase after the first C# kernel signatures exist.
- Canonical semantic category IDs and total key layouts for every outcome/event kind.
- Initial physical partition target and supported worker-count set after reference hardware is selected.
- Buffer high-water policies and behavior when a pathological tile exceeds an expected bounded buffer size.
- Whether any phase needs a deterministic external sort/spill path at future million-organism scale; v1 keeps this in memory.
- Exact phase-hash/diff diagnostic encoding and how much is retained outside test/debug builds.
- Whether profiling ever justifies parallel group resolution or disjoint-page commit after parallel evaluation.

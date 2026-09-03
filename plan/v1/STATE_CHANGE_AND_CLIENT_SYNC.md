# State Change Capture and Client Synchronization

Status: first v1 design contract; exact Protocol Buffer field numbers, queue limits, publication cadence, and compression thresholds remain to be calibrated

Sources: [architecture](ARCHITECTURE.md), [authoritative data model](DATA_MODEL.md), [simulation loop](SIMULATION_LOOP.md), [server protocol](SERVER_AND_PROTOCOL.md), [browser client](CLIENT.md), and [exploration vision](../../vision/INTERFACE.md).

# Purpose

Define how an authoritative tick produces cheap, deterministic change information and how the server turns that information into actor-authorized, mergeable updates that a thin client can apply in bulk.

This is a first-order data-model concern rather than a serialization afterthought. Every authoritative store must support efficient change capture, but the simulation model must remain independent of Protocol Buffers, WebSockets, client subscriptions, and rendering cadence.

# V1 decisions

- All authoritative mutations pass through typed phase-commit APIs. The same write that changes a store records its stable entity ID, logical field group, and any structural operation in a phase-local change builder.
- Phase changes merge deterministically into one internal `TickChangeSet`, sealed only after the tick commits successfully.
- The internal change set is an invalidation and structural-change journal, not a network payload and not a second copy of the world. Projectors read final values from the completed authoritative state.
- Actor authorization and connection interest are applied server-side after commit. Hidden state never enters a client message.
- Each connection has a normalized projection stream with its own strictly increasing revision. A `ProjectionBatch` transforms exactly one stream revision into the next.
- Ordinary patches contain absolute replacement values, never arithmetic increments. This makes consecutive batches safely coalescible.
- Creates, removals, visibility replacements, keyed history updates, and ordered events have explicit semantics; they are not inferred from nullable fields.
- The client applies a complete batch atomically to a normalized cache and notifies React and PixiJS only after the target revision is committed.
- Publication cadence is independent of simulation tick cadence. A slow client may receive a coalesced later state without slowing or changing the simulation.
- Stable IDs cross every boundary. Dense storage slots, array indexes, object references, and memory layout never appear in the protocol.

# Non-goals

- The change journal is not an event-sourced simulation log. Saves and deterministic replay remain based on authoritative snapshots, accepted commands, retained canonical events, and rules versions.
- Clients do not replay simulation rules to infer missing state.
- V1 does not promise every intermediate organism position at high simulation speed. It promises the exact authoritative state at the published tick plus retained events and history required by the projection contract.
- Protocol layout does not dictate the simulation's physical structure-of-arrays layout.

# Three distinct representations

Keeping these representations separate prevents client needs from contaminating simulation ownership:

| Representation | Owner | Purpose | May contain hidden state? | Durable? |
| --- | --- | --- | --- | --- |
| Authoritative world | Simulation | Complete source of truth | Yes | Saved/checkpointed |
| `TickChangeSet` | Simulation/runtime boundary | Identify authoritative writes and invalidations from one committed tick | Yes | Normally transient; bounded diagnostic retention is allowed |
| Projection snapshot/batch | Server projection layer | Transform one authorized client cache into another | No | Bounded reconnect retention only |

The journal answers “which authoritative facts may now differ?” The projector answers “which authorized values must this stream replace?” The Protocol Buffer schema answers “how are those replacements transported?”

# Revisions and ordering

V1 uses separate counters for separate consistency domains:

- `CompletedTick`: simulated time boundary represented by the authoritative state.
- `WorldRevision`: monotonically increases for every successfully committed world-state transaction, including a tick and any future non-tick safe-boundary transaction. V1 normally advances it once per completed tick.
- `ActorKnowledgeRevision`: changes when that actor's authoritative discovery/observation state changes.
- `ProjectionStreamId`: identifies one connection/resumable interest projection for one actor and world.
- `StreamRevision`: starts at the stream snapshot revision and advances once per emitted logical batch, regardless of how many ticks the batch spans.

Strict stream revisions are sufficient for state application; per-entity protocol revisions are unnecessary in v1. Gameplay commands retain their own expected revisions where stale decisions matter, such as `EvolutionRevision`.

A batch declares:

```text
ProjectionBatchHeader:
    projectionStreamId
    worldId
    actorId
    protocolVersion
    rulesHash
    baseStreamRevision
    targetStreamRevision
    fromExclusiveTick
    throughCompletedTick
    actorKnowledgeRevision
    batchId
    generatedAtServerTime       // diagnostic; never simulation input
    projectionHash?             // optional periodic canonical cache hash
```

`baseStreamRevision` must equal the client's committed revision. A duplicate whose target revision is already committed is ignored. Any other gap, overlap, wrong stream ID, rules mismatch, or base mismatch suspends incremental application and starts replay-from-retention or full resynchronization.

# Change capture in authoritative storage

## Mutation gateway

Simulation systems evaluate against stable phase views and return intents or phase outcomes. A deterministic committer is the only code allowed to mutate authoritative stores. Mutable arrays are not handed to parallel evaluators, projectors, metrics, or protocol encoders.

Each generated or handwritten store API combines mutation and marking:

```text
OrganismStore.SetPosition(id, tileId, xQ, yQ, changeBuilder)
OrganismStore.AdjustReserve(id, signedDelta, ledgerRef, changeBuilder)
OrganismStore.SetBehavior(id, behavior, target, changeBuilder)
OrganismStore.Create(id, initialState, changeBuilder)
OrganismStore.Remove(id, terminalReason, changeBuilder)
TileResourceStore.ApplyTransactions(tileId, transactions, changeBuilder)
```

There must be no separate call that asks the caller to remember to mark a write. Debug builds use mutation epochs and shadow column/store hashes to fail a tick if authoritative data changed without corresponding dirty or structural coverage.

## Stable identity over dense layout

Stores may map stable IDs to compact dense slots and use swap removal. Change tracking records the stable ID before compaction. A move between tile-owned collections is one logical relocation even if it becomes a remove/insert physically.

IDs are never reused within a world. Therefore `remove(id)` followed later by `create(id)` is invalid rather than an ambiguous resurrection.

## Sparse and dense dirty tracking

The physical strategy varies by write density:

- Sparse fields use phase-local changed-ID buffers or slot bitsets. The phase commit canonicalizes them into deduplicated stable-ID sets.
- Related scalar columns share a logical field group when clients nearly always consume them together, such as `Position`, `ReserveState`, `BehaviorState`, or `CurrentTileConditions`.
- High-churn columns may mark a whole tile/store chunk dirty. Position, velocity, age-dependent presentation, or a globally mixed gas can then be materialized as a typed columnar block instead of generating one heap event per scalar write.
- Gameplay-relevant derived values follow [MATERIALIZED_DERIVED_STATE.md](MATERIALIZED_DERIVED_STATE.md). A reserve, environment, age-boundary, structure, or compiled-DNA change dirties the owning health/capacity materializer; its barrier stores the new value and marks that logical field. Projectors read the stored operational value rather than independently deriving it.
- Time-derived fields such as chronological age should generally send birth tick plus current tick, not one dirty age value per organism per hour.

Dirty marks are implementation metadata. They do not enter saves, world hashes, biological decisions, or random keys.

## Internal change shape

The first C# representation uses straightforward typed buffers and stable-ID lists alongside the dense stores. It must express at least:

```text
TickChangeSet:
    worldRevision
    completedTick
    storeChanges[]
    aggregateInvalidations[]
    knowledgeChangesByActor[]
    canonicalEventRefs[]          // stable order; facts already retained by owner
    commandResultRefs[]           // stable order and correlation

StoreChanges:
    entityType
    creates[]                     // stable IDs
    removals[]                    // stable ID plus authoritative terminal reason
    relocations[]                 // stable ID, source tile, destination tile
    dirtyByLogicalFieldGroup[]    // field group plus changed IDs or dirty chunks
```

Resource transactions and death-risk records retain their own canonical facts. The change set references them or invalidates their projected aggregate; it does not duplicate the resource ledger or event store.

# Tick integration

Every successful phase commit returns a phase-local change set. The tick builder merges these in canonical phase order. A failed phase discards its staged writes and change metadata together, so a partially applied tick can never be published.

```text
AdvanceOneTick(world, admittedCommands):
    transaction = world.BeginTick()
    changes = TickChangeBuilder(transaction.nextWorldRevision)

    for phase in CanonicalPhaseOrder:
        stableView = transaction.BuildPhaseView(phase)
        outcomes = phase.Evaluate(stableView)       // parallel where allowed
        phaseChanges = transaction.CommitPhase(
            phase,
            DeterministicallyMerge(outcomes))
        changes.Merge(phaseChanges)

    transaction.FinalizeHistoriesAndKnowledge(changes)
    transaction.ValidateInvariants()
    completed = transaction.CommitAtomically()
    tickChanges = changes.Seal(completed.tick, completed.worldRevision)

    eventSink.Observe(completed.readView, tickChanges)
    metricsSink.Observe(completed.readView, tickChanges)
    projectionHub.Observe(completed.readView, tickChanges)
```

The simulation library exposes a completed read view and the internal change set; it does not emit Protocol Buffer objects. Publication is downstream of atomic commit and cannot feed information, timing, subscriptions, or backpressure into later simulation outcomes.

Change-set ordering and contents must be identical across supported worker counts and dense-slot reorderings. Whether a dirty ID set is stored as a bitset or buffer is not itself observable; sealing emits canonical stable-ID/chunk ordering when a consumer requires iteration.

## Completed-state lifetime

An asynchronous projector must never retain a view of arrays that the next tick can mutate underneath it. `CommitAtomically` therefore produces the immutable page-root defined in [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md). When publication is due, the runner exports bounded immutable publication-value pages or grants a bounded completed-root lease; no asynchronous consumer receives the working arrays.

The projection hub synchronously captures lightweight stream authorization/invalidation decisions at commit and materializes values from the newest compatible completed boundary according to [WORLD_EXECUTION_AND_OWNERSHIP.md](WORLD_EXECUTION_AND_OWNERSHIP.md). It releases superseded handles promptly. Slow encoders cannot pin unbounded historical world versions: their work is cancelled/coalesced to a newer handle or replaced by a snapshot under the configured memory budget. Socket delivery never holds a world read lease.

# Actor-authorized projection

One world change can produce different outputs for different actors and connections. The projection layer combines:

1. the completed authoritative read view;
2. the tick change set;
3. authoritative actor knowledge and control;
4. that connection's interest subscriptions; and
5. the prior normalized projection contract for the stream.

Subscriptions only narrow authorized information. The projector must not serialize hidden values and expect the browser to mask them.

An interest update is a projection-stream operation, not a simulation command and not a source of randomness. Narrowing emits removals/replacements as needed. Broadening emits complete current projections for newly included scopes because the client cannot infer the unseen interval. Either case advances the stream revision while leaving `WorldRevision` unchanged.

```text
ObserveTick(completedView, tickChanges):
    for stream interested in possibly affected scopes:
        invalidations = AuthorizeAndMap(
            tickChanges,
            completedView.actorKnowledge(stream.actorId),
            stream.interest)
        stream.accumulator.Merge(invalidations)

PublishDueStream(stream, latestCompletedView):
    logicalBatch = stream.accumulator.MaterializeAbsoluteValues(
        latestCompletedView,
        stream.baseRevision)
    encodedParts = EncodeAndChunk(logicalBatch)
    stream.outboundQueue.Enqueue(encodedParts)
    stream.accumulator.ResetTo(logicalBatch.targetRevision)
```

The journal selects what to read; the completed state supplies the values. This avoids copying old and new payloads during the tick and naturally collapses repeated writes to a final value.

# Projection operations

Use typed Protocol Buffer messages and shared-field-mask columnar blocks, not arbitrary string maps or serialized C# objects. A logical batch contains sections equivalent to:

```text
ProjectionBatch:
    header
    definitionUpserts[]
    tileProjectionReplacements[]
    speciesAndLineageUpserts[]
    entityCreates[]
    entityPatchBlocks[]
    entityRemovals[]
    aggregateAndHistoryUpserts[]
    clockAndRunState?
    commandResults[]
    orderedEvents[]
```

## Absolute field patches

A patch replaces the named fields with their authoritative projected values at `throughCompletedTick`. It never means add, toggle, append, or apply a simulation transaction.

A high-volume block groups entities that share a field mask and scope:

```text
OrganismPatchBlock:
    tileId
    fieldMask
    organismIds[]
    xQ[]?
    yQ[]?
    activeVelocityXQ[]?
    activeVelocityYQ[]?
    reserveQ[]?
    healthQ[]?
    behaviorIds[]?
```

All present arrays have identical length and align by index with `organismIds`. IDs are in canonical ascending order for deterministic encoding and stable application. The first implementation uses this one columnar block form for high-volume organism patches and ordinary typed records for the other operations. It does not add adaptive row/column switching, bit packing, a custom binary codec, or density-specific encodings until representative server encoding, wire, client decode/apply, and rendering measurements identify a material bottleneck. Clients retain the same logical semantics if such an optimization is later introduced.

Position samples include their authoritative sample tick and may include active velocity for visual interpolation. Intermediate paths omitted by coalescing are not authoritative history and must not be invented or used for interactions.

## Creates and removals

A create contains every required field for that entity's current projection. Later patches in the same logical batch are folded into the create.

A removal includes one client-safe reason:

- `Destroyed`, only when the actor is authorized to know the terminal fact;
- `LeftProjection`, when the entity still exists but moved outside interest;
- `VisibilityLost`, when the containing tile became reduced or unknown;
- `ProjectionReset`, when a stream is being rebuilt.

The reason prevents the UI from narrating every cache eviction as a death. It must itself pass authorization and may be downgraded to `LeftProjection`.

## Tile projection replacement

Unknown, reduced, and live tiles are distinct projection variants. A knowledge transition replaces the complete tile projection atomically rather than patching nullable secrets:

- `Unknown -> Reduced` supplies the authorized reduced snapshot.
- `Reduced -> Live` supplies a complete live snapshot. Hidden intermediate changes are never reconstructed as deltas.
- `Live -> Reduced` supplies the retained observation and evicts exact changing conditions, live-only histories, organisms, and remains in the same batch.
- A sandbox control transfer may cause many replacements; above a measured size threshold the server sends a fresh authorized projection snapshot.

If a controlled organism migrates into a previously reduced tile, the completed-boundary live replacement supersedes a separate movement patch for entities contained in that replacement.

## Keyed aggregates and histories

Chart and aggregate updates are replacements/upserts by a stable semantic key such as `(tileId, resourceId, flowCategory, hourBucket)`. They are never blind list appends. Downsampled buckets carry their interval and aggregation version, so a newer bucket can replace an earlier partial one.

This rule makes aggregation batches coalescible and prevents retries from double-counting resource flows.

## Ordered, non-coalescible records

Canonical notable events, authorized death/speciation facts, attention alerts, pause transitions, and command results retain stable IDs and canonical order. Coalescing deduplicates them by ID but never converts them into latest-state patches or discards them merely because their entities were later removed.

Ephemeral visual notifications may be dropped by explicit product policy; authoritative or player-relevant retained records may not.

# Merge algebra

Two batches are directly mergeable only when they have the same stream, protocol/rules contract, and `A.targetStreamRevision == B.baseStreamRevision`. The merged batch spans `A.base -> B.target` and uses B's through-tick and projection hash.

For each stable entity or keyed record, merge in chronological order:

| Earlier operation | Later operation | Merged state operation |
| --- | --- | --- |
| create | patch | create with final patched values |
| create | remove | omit both if the entity did not exist at the base; preserve ordered events |
| patch | patch | one patch with the last absolute value for each field |
| patch | remove | remove |
| remove | create with same ID | invalid because IDs are never reused |
| keyed upsert | keyed upsert | last complete value for that key |
| tile replacement | later tile replacement/patch | final complete projection plus applicable final patches |
| ordered event | any state operation | retain event once by event ID |

If an accumulator cannot prove whether an entity existed at the base, it conservatively retains the valid create/remove sequence internally or materializes from its base manifest. The wire batch should normally contain only the minimal final transformation.

Batch merging must be associative at the state level:

```text
Apply(Apply(S, A), B) == Apply(S, Merge(A, B))
```

It need not be commutative; chronological order is meaningful. Property tests enforce the algebra for randomly generated valid operation sequences.

# Atomic client application

The TypeScript protocol adapter validates a batch fully before mutating visible state. It applies to a transaction/draft of the normalized cache in dependency order:

1. verify stream, base revision, schema, rules hash, part completeness, and structural validity;
2. upsert referenced definitions;
3. apply complete tile projection replacements and their eviction sets;
4. upsert species and lineage records required by entities;
5. create entities and apply typed absolute patches;
6. apply explicit entity removals and repair derived tile-membership indexes;
7. upsert keyed aggregates and history buckets;
8. update clock/run state, command results, and ordered event indexes;
9. validate referential and visibility invariants;
10. atomically publish `targetStreamRevision` and notify selectors/renderers once.

```text
ApplyProjectionBatch(cache, batch):
    if batch.targetRevision <= cache.revision:
        return DuplicateIgnored
    if batch.streamId != cache.streamId or
       batch.baseRevision != cache.revision:
        return NeedsResynchronization

    draft = cache.BeginDraft()
    ValidateAndApplyInDependencyOrder(draft, batch)
    draft.AssertNoUnauthorizedProjectionShapes()
    cache.CommitDraft(draft, batch.targetRevision)
    renderScheduler.NotifyOneCommittedRevision(batch.changedScopes)
```

React subscribes to coarse normalized selectors rather than one component per organism. PixiJS receives bulk changed-ID/column ranges, updates instance buffers and object pools once, and interpolates from the prior received sample to the new one. Neither renderer observes a partially applied batch.

# Transport, chunking, and recovery

A semantic batch may be divided into transport parts only after merge/materialization:

```text
ProjectionBatchPart:
    batchId
    partIndex
    partCount
    payloadChecksum
    payloadBytes
```

The client stages parts and exposes nothing until all parts validate. A missing part, checksum failure, decode failure, or timeout discards the staging area and requests recovery.

The server retains a bounded ring of already encoded or reproducible batches per resumable stream and records the latest client acknowledgement. On reconnect:

- replay retained consecutive batches when the client's stream revision is still available and its interest/authorization contract remains valid;
- otherwise send a new complete authorized `ProjectionSnapshot` with a new stream identity or reset revision;
- never fill historical intervals that were hidden from the actor.

Snapshots and batches populate the same normalized projection schema. This prevents separate “load model” and “update model” semantics from drifting.

# Backpressure and publication cadence

The simulation owner never waits on projection, encoding, sockets, acknowledgements, or rendering.

- Every stream has bounded pending-state and outbound-byte budgets.
- Completed-state read leases and pending encoders have a separate bounded memory/time budget; obsolete projection work is cancelled rather than pinning mutable-world history.
- Before serialization, pending ordinary state invalidations coalesce to the newest absolute values across any number of completed ticks.
- Ordered retained events and command results use bounded durable/reconnect retention appropriate to their owner; they are not silently collapsed into state.
- If an encoded delta queue becomes stale, the server may discard unsent state batches and rematerialize one batch from the client's acknowledged base to the newest state.
- If the base is no longer available, the interest set changed incompatibly, authorization changed extensively, or the replacement exceeds configured complexity, enqueue a fresh authorized snapshot.
- Continued failure to drain beyond measured limits produces a warning and then disconnect; world advancement continues.

Default organism/map update rates, event retention, byte limits, merge CPU budget, snapshot threshold, and disconnect policy require benchmark calibration. The protocol must support tuning these without changing simulation rules.

# Determinism, hashing, and security

- Producing no projections, one projection, or many projections must yield the same authoritative world hash and future simulation.
- A canonical `TickChangeSet` for the same seed, commands, rules, and worker count contract must identify the same logical changes. Physical buffer capacity and allocation order are excluded.
- Network batching and update cadence need not be replay-identical wall-clock behavior. Applying all batches through a completed tick must equal a direct authorized projection at that tick.
- `projectionHash` is distinct from the authoritative world hash. It covers canonical normalized state authorized for that stream, not transport chunking or presentation state.
- Authorization tests inspect encoded messages, not just UI behavior. No hidden current resource, organism, remnant, exact history, behavior, or causal detail may appear in bytes sent to an unauthorized stream.
- Projection code is read-only. It receives no RNG and cannot invoke commands, update actor knowledge, or mutate cached authoritative derivations.

# Required tests and acceptance criteria

## Mutation coverage

- Every authoritative store mutation produces the expected structural or logical-field dirty mark.
- Debug shadow hashes catch deliberate unmarked writes.
- Create, swap removal, tile migration, speciation, death/remnant creation, resource transfer, and knowledge transitions retain stable IDs and correct invalidations.
- A failed tick publishes no change set or projection and leaves the prior revision valid.

## Projection correctness

- Starting from any projection snapshot, applying every emitted batch through tick `T` equals a direct authorized projection at `T`.
- Sequential and merged application satisfy the merge law for generated valid operation sequences.
- Duplicate application is harmless; gaps, corrupt parts, wrong bases, and wrong rules fail before visible mutation.
- Reduced-to-live sends a complete live replacement; live-to-reduced evicts all live-only entities and current values atomically.
- An organism leaving interest is not falsely reported as dead, and a hidden death is not disclosed through a removal reason.
- Keyed history retries and merges never double-count flows.
- Definitions precede references and removals leave no dangling normalized indexes.

## Determinism and isolation

- Supported worker counts and dense-slot permutations produce identical canonical logical change sets and projection results.
- Subscription sets, update cadence, disconnected clients, and projection load do not alter the world hash.
- Two actors with different knowledge receive only their authorized projections from the same tick.
- Save/load continuation produces equivalent future tick changes; transient stream revisions and dirty buffers are correctly rebuilt rather than persisted as world truth.

## Performance

The representative 100,000-organism benchmark records:

- phase-commit dirty-tracking CPU and allocations;
- changed IDs and dirty density per logical field group;
- projection materialization and merge time per stream;
- uncompressed and encoded bytes by operation type;
- snapshot size versus coalesced-batch size;
- client decode, validation, atomic apply, normalized-index repair, PixiJS upload, and post-commit React render count;
- memory held per stream for accumulator, retention, staging, and acknowledgement state.

Acceptance thresholds are intentionally deferred until the dense storage prototype and first client slice exist, but instrumentation is required in that prototype.

# Implementation sequence

1. Define stable IDs, logical field groups, typed store mutators, and debug mutation coverage beside the first dense organism/tile stores.
2. Implement `PhaseChangeSet`, deterministic tick merge, and a diagnostic text/JSON inspector without networking.
3. Define normalized projection schemas for world metadata, unknown/reduced/live tiles, organisms, remains, species, lineage, and clock state.
4. Implement direct full projection and prove actor authorization before implementing deltas.
5. Implement change-driven projection materialization and verify it equals the direct projection oracle.
6. Add mergeable Protocol Buffer batches, the atomic TypeScript cache applier, and property tests.
7. Add publication cadence, acknowledgements, retention, reconnect, chunking, and backpressure.
8. Profile the complete server-to-client path and tune ordinary queue/batch budgets; introduce alternate field grouping, density thresholds, or application compression only for a demonstrated bottleneck.

# Remaining implementation choices

These do not block entity identity/storage design, but must be fixed before the first networked vertical slice is complete:

- Exact stable-ID encoding and non-reuse strategy.
- Structure-of-arrays chunk size and dirty-set representation per store.
- Logical field-group catalogue and generated mutation/projection metadata.
- Protocol Buffer package/version and JavaScript 64-bit mapping.
- Default projection cadence by interest/zoom and maximum batch/part size.
- Stream retention, acknowledgement, snapshot, and disconnect byte/time limits.
- Canonical projection-hash encoding and cadence.
- Whether application-level compression improves representative payloads beyond transport compression.

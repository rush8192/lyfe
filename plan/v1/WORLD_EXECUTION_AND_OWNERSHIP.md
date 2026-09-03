# World Execution and Ownership

Status: first v1 execution and deterministic reduction contract; exact speed presets, operational worker cap, queue limits, and shutdown timeouts remain to be calibrated

Sources: [architecture](ARCHITECTURE.md), [technology decisions](TECHNOLOGY.md), [simulation loop](SIMULATION_LOOP.md), [keyed randomness](KEYED_RANDOMNESS.md), [deterministic parallel execution](DETERMINISTIC_PARALLEL_EXECUTION.md), [state change and client synchronization](STATE_CHANGE_AND_CLIENT_SYNC.md), and [persistence and replay](PERSISTENCE_AND_REPLAY.md).

# Purpose

Define which component owns a world, how ticks and safe-boundary commands execute, where parallelism is allowed, and how networking, publication, saves, faults, and shutdown remain outside authoritative mutation.

# Executive decision

One world is one single-writer deterministic state machine.

The server process owns a `WorldRunner` with exclusive write access to one loaded `SimulationWorld`. ASP.NET request threads, WebSocket sessions, projection workers, persistence workers, metrics consumers, and simulation phase workers never mutate that world directly. They exchange typed messages or immutable completed-state artifacts with the runner.

V1 local/server deployment loads at most one active world per process. The process may list many saved worlds, but loading another requires stopping or unloading the current one. This gives a heavy simulation an explicit CPU and memory budget and makes a whole world the natural unit for restart, migration to larger hardware, and fault containment.

The APIs, identifiers, configuration, and actor model remain world-scoped so a future service can supervise many world processes. Multiple players may eventually connect to the same world runner; multiplayer does not require multiple writers.

# Why ownership begins at the world

- Cross-tile movement, gas exchange, resource transport, world climate, lineage, commands, and state hashes all require deterministic world-level barriers.
- A tile or organism actor model would replace straightforward array iteration with enormous message volume and distributed ordering concerns.
- Internal tile-parallel evaluation still captures the useful concurrency without fragmenting authority.
- One-world deployment can scale vertically for the target tens of thousands of organisms and can later scale horizontally by assigning different worlds to different processes or hosts.
- Nothing in this choice prevents a future, separately designed distributed-world engine if profiling eventually demonstrates that one machine cannot meet a chosen world size.

# Runtime components

```text
ASP.NET Core / Kestrel
  ├── HTTP control plane
  ├── WebSocket sessions
  └── authentication/actor and envelope validation
               │ typed requests; no world references
               ▼
WorldHost
  ├── saved-world catalogue
  ├── one loaded WorldRunner
  └── lifecycle and process-level resource limits
               │
               ▼
WorldRunner — exclusive writer
  ├── bounded command/control mailbox
  ├── wall-clock scheduler
  ├── one owner-mutable SimulationWorld
  ├── deterministic phase coordinator
  └── completed-boundary publication
        │               │                 │
        ▼               ▼                 ▼
  bounded phase    ProjectionHub     PersistenceWorker
  worker slots     and encoders      and save store
        │               │                 │
        └──── immutable outcomes/artifacts only ────┘
```

## `Lyfe.Simulation`

Owns world state, rule execution, deterministic boundary semantics, typed state mutation, stable simulation IDs, resource accounting, canonical simulation events, and state hashing. It knows nothing about ASP.NET, sockets, client sessions, wall-clock speed, file locations, or Protocol Buffer types.

## `WorldRunner`

Owns the loaded simulation instance and is the only caller allowed to mutate it. It assigns safe-boundary work, advances ticks, invokes bounded phase parallelism, coordinates detached save/publication captures, and exposes lifecycle status.

## `WorldHost`

Owns process lifecycle and the saved-world catalogue. It creates, loads, starts, stops, and unloads the single v1 runner. Its interface is intentionally capable of returning a world-scoped handle rather than exposing a global current-world singleton.

## Connection/session layer

Authenticates an actor, validates protocol shape and size, enforces connection rate limits, assigns an ingress sequence, deduplicates client request IDs, and enqueues candidate commands. It cannot make state-dependent gameplay acceptance decisions.

## Projection hub

Consumes sealed changes and bounded immutable publication values after commit. It owns actor authorization, connection interest, stream revisions, mergeable client batches, reconnect retention, and outbound backpressure. It cannot call simulation mutation APIs.

## Persistence worker

Serializes a detached logical save snapshot copied at one completed boundary. File/database I/O and compression occur away from the world owner. A save failure reports failure for that request but does not roll back or fault a healthy world.

# World lifecycle

The runner has one primary lifecycle state:

```text
Unloaded
   │ load/create
   ▼
Loading ──failure──> Faulted
   │ success
   ▼
PausedReady <──────> Running
     │                  │
     ├──── fault ───────┤
     ▼                  ▼
  Stopping <──────── Faulted
     │
     ▼
  Unloaded / ProcessStopped
```

`Saving`, `Publishing`, and `ClientDisconnected` are not world lifecycle states. They are bounded operations over detached boundary snapshots and may overlap later ticks. `GameEnded` is authoritative gameplay state; its runner normally remains `PausedReady` so the client can inspect and save the final world.

| State | Tick advancement | Mailbox behavior | Read/save behavior |
| --- | --- | --- | --- |
| `Unloaded` | None | Reject world commands | Catalogue metadata only |
| `Loading` | None | Reject or wait behind load request | No partial world publication |
| `PausedReady` | None | Drain run control, queries, and eligible boundary transactions | Publish/query/save completed state |
| `Running` | Scheduled sequential ticks | Drain at safe boundaries | Publish/save completed state |
| `Faulted` | None | Reject simulation commands; allow diagnostics and access to the last durable save | Never publish or save the partially mutated in-memory tick |
| `Stopping` | Finish/discard according to current safe boundary, admit no new gameplay work | Drain only shutdown completion | Complete or cancel background work by policy |

# Mailboxes and request classes

One bounded multi-producer/single-consumer mailbox feeds each runner. Messages are classified before enqueueing:

- `RunControl`: pause, resume, speed, stop, and deadline-related operations.
- `WorldCommandCandidate`: evolution, control transfer, lock, and later multiplayer decisions.
- `ExactBoundaryQuery`: previews or diagnostics that must read the authoritative completed version rather than a possibly older client projection.
- `CaptureRequest`: save, checkpoint, full projection, or diagnostic hash capture.
- `LifecycleRequest`: unload or shutdown.

High-volume presentation queries and subscriptions bypass the runner and read actor-authorized projection state. They must not consume mailbox capacity needed for authoritative work.

The mailbox has explicit count and byte/complexity limits. Full admission behavior will be calibrated, but overload must return a typed retryable error; it must not allocate without bound.

# Command ordering and idempotency

Every candidate carries:

```text
WorldCommandEnvelope:
    worldId
    actorId
    clientCommandId
    ingressSequence
    receivedServerTime          // diagnostic only
    expectedRevisionFields
    payload
```

The connection boundary assigns a process-monotonic `ingressSequence` after envelope validation. Concurrent real clients may therefore produce a different authoritative order according to arrival, which is legitimate; deterministic replay records and reuses the chosen order.

At a safe boundary, the runner drains a bounded batch, sorts by `ingressSequence`, and performs state-dependent authority and precondition validation. The retained command result contains accepted/rejected status, reason, authoritative application tick/world revision, and assigned replay order. Retrying the same `(actorId, clientCommandId)` returns the retained result and cannot apply twice.

Server receipt is not gameplay acceptance. A queued acknowledgement may be sent immediately, but only the runner's boundary result is authoritative.

# Safe-boundary transactions

There are two externally atomic boundary-mutation kinds:

- `TickTransaction`: advances simulated time by exactly one configured tick and executes all phases.
- `BoundaryCommandTransaction`: applies commands that are explicitly valid while paused without advancing time, such as a confirmed player speciation or sandbox control transfer.

A boundary command transaction increments `WorldRevision` but not `CompletedTick`. Its effects are saveable and publishable immediately. Newly created organisms still cannot act until the next tick. Commands whose rules require tick-phase inputs remain queued for the next tick rather than being forced into the command-only transaction.

While running, eligible player commands are normally admitted at the next tick's phase-0 boundary and commit as part of that tick transaction. While paused, the same semantic transaction may commit independently so a player can make and inspect a decision without advancing the world. The command log records the transaction kind, tick boundary, world revision, and order, so replay is unambiguous.

# Tick execution and internal parallelism

```text
WorldRunner.RunOneTick():
    assert lifecycle == Running
    boundaryBatch = DrainAndOrderMailbox()
    changes = TickChangeBuilder(world.nextWorldRevision)
    try:
        world.ExecuteOwnerOnlyCommandAdmissionPhase(boundaryBatch, changes)
        world.RunOwnedMaterializationBuilders(CommandAdmission, changes)
        changes.SealPhase(CommandAdmission)

        for phase in CanonicalTickPhases1Through10:
            stableView = world.SealPhaseReadView()
            work = phase.EnumerateLogicalWork(stableView)
            partitions = phase.SchedulePhysicalPartitions(
                work, operationalWorkerCount)
            outcomesBySlot = RunOnBoundedWorkers(partitions, stableView)
            resolved = phase.CanonicalReduceAndPreflight(
                stableView, outcomesBySlot)
            world.CommitPhaseThroughTypedMutators(resolved, changes)
            world.RunOwnedMaterializationBuilders(phase, changes)
            changes.SealPhase(phase)

        world.ExecuteOwnerOnlyFinalizationPhase(changes)
        world.RunOwnedMaterializationBuilders(Finalization, changes)
        world.ValidateCompleteTick()
        boundary = world.SealCompletedBoundary(changes)
        CaptureDuePublicationAndSaveSnapshots(world, boundary)
    catch error:
        EnterFaulted(error, lastPublishedBoundaryMetadata)
```

Parallel phase workers receive immutable phase views and isolated output buffers. They never allocate entity IDs, mutate stores, append directly to global event logs, or resolve shared contention. Physical partitions may change with worker count and have no logical identity. The owner validates stable outcome keys, performs exact grouped resolution, assigns IDs by accepted creation key, preflights the complete mutation plan, commits through typed mutators, and runs materialization builders. [DETERMINISTIC_PARALLEL_EXECUTION.md](DETERMINISTIC_PARALLEL_EXECUTION.md) is normative.

The first vertical slice uses one worker through the final worker-buffer/reduction interface. Representative profiling may enable a configured bounded degree of parallel evaluation phase by phase on the .NET worker pool. A dedicated simulation worker pool is warranted only if the server benchmark demonstrates interference. Worker count changes may change performance but not state, IDs, events, materializations, journals, or hashes.

V1 deliberately does not require a copy-on-write world root, MVCC, undo journal, or whole-tick rollback. The owner mutates ordinary dense state only after each phase's complete plan passes preflight, and no external reader sees that state until the full boundary validates. If an unexpected evaluator, commit, materialization, arithmetic, or invariant defect occurs after mutation begins, the runner faults and the partial in-memory world is discarded on unload/restart; recovery uses the last durable save/checkpoint. Copy-on-write or double-buffered hot state may be evaluated later only if near-zero-pause snapshots or in-process failed-tick recovery becomes a measured product requirement.

# Wall-clock scheduling

Simulation time and wall time remain separate:

- One tick always advances the configured simulated duration, initially one hour.
- A speed preset chooses a target wall-clock interval or `Unpaced` mode.
- Pause takes effect after the current transaction reaches a completed boundary.
- No speed setting skips simulation ticks or combines biological phases.
- `Unpaced` runs the next tick as soon as the prior boundary work completes, with cooperative yields so control requests and process cancellation remain responsive.

For paced modes, the runner uses a monotonic clock. If a tick finishes late, it may begin the next tick immediately, but it does not accumulate unbounded catch-up debt. After a configurable bounded burst it resets the wall deadline and publishes `SpeedLimited` status with measured achieved rate. This affects only responsiveness, never simulated order or results.

Exact presets, maximum burst, yield cadence, and response-time targets remain gameplay/performance calibration decisions.

## Runner control loop

```text
WorldRunner.MainLoop():
    lifecycle = PausedReady

    until stopRequested:
        DrainControlAndBoundaryRequests()

        if pauseRequested:
            lifecycle = PausedReady

        if lifecycle == PausedReady:
            CommitEligiblePausedBoundaryCommands()
            ProcessExactQueriesAndCaptureRequests()
            WaitForMailboxOrResume()
            continue

        if lifecycle == Running:
            WaitUntilTargetDeadlineOrControlSignal()
            if pauseRequested:
                continue
            RunOneTick()
            ProcessCompletedBoundaryCaptures()
            if gameEnded or automaticPauseTriggered:
                lifecycle = PausedReady

    lifecycle = Stopping
    FinishShutdownFromLastCompletedBoundary()
```

A pause requested during evaluation becomes effective after the tick completes; it does not interrupt biological phases or publish half a tick. Once a tick has committed any phase, graceful cancellation does not interrupt it. A forced shutdown timeout may terminate the process and lose progress since the last durable save, but can never publish the partial tick. Resume establishes a new monotonic wall deadline and carries no catch-up debt from time spent paused.

Loading constructs and validates a complete world, rebuilds declared structural indexes and permitted derived materializations, and computes the required hash before installing the runner's first completed boundary. No connection receives a partial loading view.

# Completed-state publication

At every successful transaction, the runner seals a `TickChangeSet` or boundary change set. The projection hub synchronously records only the lightweight invalidation, event-reference, actor-knowledge, and stream-interest consequences that must not be lost.

When a stream publication is due, the runner copies the required final values into a bounded immutable `PublicationSnapshot` before starting the next tick. Protocol mapping, compression, chunking, and socket I/O remain asynchronous. A connection or resynchronization may request a full authorized capture at the next safe boundary.

This is the v1 interpretation of the completed-read contract in [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md): it contains compact copied publication values rather than a retained view of the entire world, and a full snapshot is created only for a full projection/save/hash need. It never exposes live mutable arrays.

# Save and checkpoint execution

```text
RequestSave(request):
    enqueue CaptureRequest

AtCompletedBoundary(request):
    if incompatible save already capturing:
        coalesce request or return SaveBusy
    saveSnapshot = SaveSnapshotBuilder.CopyCanonicalState(world)
    persistenceWorker.Enqueue(saveSnapshot, request)

PersistenceWorker:
    serialize canonical state
    checksum, compress, and atomically replace destination
    publish request result
```

The boundary operation is a measured synchronous copy into a save-owned logical snapshot; file I/O, checksumming, compression, and atomic replacement remain asynchronous. At most one full save serialization per world should run initially. Snapshot memory and copy duration are bounded and observed; further requests coalesce by destination/policy or receive `SaveBusy`. If the copy alone later violates pause/latency targets, snapshot-specific copy-on-write becomes an evidence-driven optimization.

# Fault containment

| Failure | Required behavior |
| --- | --- |
| Invalid/rejected command | Record typed result; world continues |
| Tick evaluation, arithmetic, invariant, or commit failure | Publish nothing, mark in-memory world invalid, enter `Faulted`, and recover from the last durable save if requested |
| Projection/encoding failure | Reset or disconnect affected stream; world continues |
| Slow client | Coalesce, snapshot, warn, or disconnect; world continues |
| Save serialization/write failure | Preserve prior valid save and running world; report request failure |
| Metrics/diagnostic sink failure | Disable/fail sink according to policy; never affect world |
| Process crash | Recover from the last atomic save/checkpoint; v1 does not promise write-ahead recovery of later accepted commands |

Fault reports retain world/tick/revision, phase, exception classification, rules hash, command batch IDs, state-hash/checkpoint references, and bounded diagnostic context. They must not include secrets from another actor projection.

# Shutdown

Graceful shutdown is ordered:

1. stop accepting new sessions and lifecycle/gameplay requests;
2. request pause/stop on the runner;
3. allow the active authoritative tick to finish; if a forced timeout kills it, do not publish or save the partial state;
4. optionally capture the configured shutdown save from the resulting completed boundary;
5. complete/cancel projection and persistence work without invalidating an atomic save;
6. flush retained command/event metadata required by policy;
7. release snapshots and stop the host.

Cancellation is observed before a tick and while an evaluation phase has not yet committed. After any phase commit, graceful shutdown lets the tick finish; only forced process termination may cut it short. Exact graceful and forced timeouts remain deployment configuration.

# Scaling path

V1 intentionally excludes distributed execution of one world. The supported path is:

1. use tile/range parallelism within one process;
2. optimize data layout and allocation from profiling;
3. give a hot world more cores/memory;
4. run different worlds in different processes/containers/hosts;
5. add a routing/catalogue service when multiple world processes are operationally useful.

Competitive multiplayer adds actors, commands, and shared-clock policy to one runner. It does not change authoritative ownership.

# Invariants and tests

- Exactly one runner may mutate a loaded world.
- No background worker retains a mutable store reference or `Span<T>` after its phase/barrier.
- A tick or boundary mutation is externally visible in full or not at all.
- A failed partially mutated in-memory tick is never queried, projected, or saved; recovery starts from the last durable checkpoint.
- Command retries cannot apply twice; replay uses recorded application order rather than network timing.
- Worker count, completion order, projection load, saves, queries, pause duration, and wall-clock speed do not change authoritative results.
- Slow/failing clients, encoders, metrics sinks, and save destinations cannot block tick ownership beyond their bounded capture work.
- Only one loaded world exists in a v1 process, while no public interface assumes an unqualified global world.
- Graceful shutdown finishes a valid boundary; forced termination may lose unsaved progress but never publishes a partial boundary.

# Remaining calibration and implementation choices

- Speed presets, target wall intervals, unpaced yield cadence, and overload thresholds.
- Mailbox count/byte limits, maximum boundary batch, and command-result retention.
- Initial worker-count policy and whether benchmark evidence warrants dedicated workers.
- Publication cadence and maximum synchronous publication-copy budget.
- Save-snapshot copy budget, concurrency, memory budget, and graceful/forced shutdown timeouts.
- Operational process/container model for a future hosted multi-world service.

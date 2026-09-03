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
  ├── SimulationWorld working/completed versions
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

Owns world state, rule execution, deterministic transactions, typed state mutation, stable simulation IDs, resource accounting, canonical simulation events, and state hashing. It knows nothing about ASP.NET, sockets, client sessions, wall-clock speed, file locations, or Protocol Buffer types.

## `WorldRunner`

Owns the loaded simulation instance and is the only caller allowed to begin, commit, or discard authoritative transactions. It assigns safe-boundary work, advances ticks, invokes bounded phase parallelism, coordinates save/publication captures, and exposes lifecycle status.

## `WorldHost`

Owns process lifecycle and the saved-world catalogue. It creates, loads, starts, stops, and unloads the single v1 runner. Its interface is intentionally capable of returning a world-scoped handle rather than exposing a global current-world singleton.

## Connection/session layer

Authenticates an actor, validates protocol shape and size, enforces connection rate limits, assigns an ingress sequence, deduplicates client request IDs, and enqueues candidate commands. It cannot make state-dependent gameplay acceptance decisions.

## Projection hub

Consumes sealed changes and bounded immutable publication values after commit. It owns actor authorization, connection interest, stream revisions, mergeable client batches, reconnect retention, and outbound backpressure. It cannot call simulation mutation APIs.

## Persistence worker

Serializes an immutable save handle captured at one completed boundary. File/database I/O and compression occur away from the world owner. A save failure reports failure for that request but does not roll back or fault a healthy world.

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

`Saving`, `Publishing`, and `ClientDisconnected` are not world lifecycle states. They are bounded operations over immutable completed versions and may overlap later ticks. `GameEnded` is authoritative gameplay state; its runner normally remains `PausedReady` so the client can inspect and save the final world.

| State | Tick advancement | Mailbox behavior | Read/save behavior |
| --- | --- | --- | --- |
| `Unloaded` | None | Reject world commands | Catalogue metadata only |
| `Loading` | None | Reject or wait behind load request | No partial world publication |
| `PausedReady` | None | Drain run control, queries, and eligible boundary transactions | Publish/query/save completed state |
| `Running` | Scheduled sequential ticks | Drain at safe boundaries | Publish/save completed state |
| `Faulted` | None | Reject simulation commands; allow diagnostics/export of last completed version where valid | Never publish failed working state |
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

There are two authoritative transaction kinds:

- `TickTransaction`: advances simulated time by exactly one configured tick and executes all phases.
- `BoundaryCommandTransaction`: applies commands that are explicitly valid while paused without advancing time, such as a confirmed player speciation or sandbox control transfer.

A boundary command transaction increments `WorldRevision` but not `CompletedTick`. Its effects are saveable and publishable immediately. Newly created organisms still cannot act until the next tick. Commands whose rules require tick-phase inputs remain queued for the next tick rather than being forced into the command-only transaction.

While running, eligible player commands are normally admitted at the next tick's phase-0 boundary and commit as part of that tick transaction. While paused, the same semantic transaction may commit independently so a player can make and inspect a decision without advancing the world. The command log records the transaction kind, tick boundary, world revision, and order, so replay is unambiguous.

# Tick execution and internal parallelism

```text
WorldRunner.RunOneTick():
    assert lifecycle == Running
    boundaryBatch = DrainAndOrderMailbox()
    working = completedWorld.BeginTransactionalFork()

    try:
        working.ExecuteOwnerOnlyCommandAdmissionPhase(boundaryBatch)
        working.RunOwnedMaterializationBuilders(CommandAdmission)
        working.SealPhaseJournal(CommandAdmission)

        for phase in CanonicalTickPhases1Through10:
            stableView = working.SealPhaseReadView()
            work = phase.EnumerateLogicalWork(stableView)
            partitions = phase.SchedulePhysicalPartitions(
                work, operationalWorkerCount)
            outcomesBySlot = RunOnBoundedWorkers(partitions, stableView)
            resolved = phase.CanonicalReduceAndPreflight(
                stableView, outcomesBySlot)
            working.CommitPhaseThroughTypedMutators(resolved)
            working.RunOwnedMaterializationBuilders(phase)
            working.SealPhaseJournal(phase)

        working.ExecuteOwnerOnlyFinalizationPhase()
        working.RunOwnedMaterializationBuilders(Finalization)
        working.ValidateCompleteTick()
        working.SealPhaseJournal(Finalization)
        completedWorld = working.Commit()
        PublishCompletedBoundary(completedWorld)
    catch error:
        working.Discard()
        EnterFaulted(error, completedWorld)
```

Parallel phase workers receive immutable phase views and isolated output buffers. They never allocate entity IDs, mutate stores, append directly to global event logs, or resolve shared contention. Physical partitions may change with worker count and have no logical identity. The owner validates stable outcome keys, performs exact grouped resolution, assigns IDs by accepted creation key, preflights the complete mutation plan, commits through typed mutators, and runs materialization builders. [DETERMINISTIC_PARALLEL_EXECUTION.md](DETERMINISTIC_PARALLEL_EXECUTION.md) is normative.

The first vertical slice uses one worker through the final worker-buffer/reduction interface. Representative profiling may enable a configured bounded degree of parallel evaluation phase by phase on the .NET worker pool. A dedicated simulation worker pool is warranted only if the server benchmark demonstrates interference. Worker count changes may change performance but not state, IDs, events, materializations, journals, or hashes.

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
    FinishShutdownFromLastCompletedRoot()
```

A pause requested during evaluation becomes effective after that transaction commits; it does not interrupt biological phases and publish half a tick. A forced shutdown timeout may cancel evaluation and discard the entire working fork. Resume establishes a new monotonic wall deadline and carries no catch-up debt from time spent paused.

Loading constructs and validates a complete root, rebuilds only declared structural indexes, validates stored gameplay materializations and their dependency generations, and computes the required hash before installing the runner's first completed version. No connection receives a partial loading view. A missing gameplay materialization requires an explicit save migration rather than ordinary load-time reconstruction.

# Completed-state publication

At every successful transaction, the runner seals a `TickChangeSet` or boundary change set. The projection hub synchronously records only the lightweight invalidation, event-reference, actor-knowledge, and stream-interest consequences that must not be lost.

When a stream publication is due, the runner exports the required final values into a bounded immutable `PublicationReadHandle` before allowing those particular mutable pages to be reused. Protocol mapping, compression, chunking, and socket I/O remain asynchronous. A connection or resynchronization may request a full authorized capture at the next safe boundary.

This is the v1 interpretation of the `CompletedReadHandle` contract in [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md): it may contain compact typed publication-value pages rather than a copy of the entire world, and a full handle is created only for a full projection/save/hash need. It never exposes live mutable arrays.

# Save and checkpoint execution

```text
RequestSave(request):
    enqueue CaptureRequest

AtCompletedBoundary(request):
    if incompatible save already capturing:
        coalesce request or return SaveBusy
    saveHandle = completedWorld.RetainImmutableVersion()
    persistenceWorker.Enqueue(saveHandle, request)

PersistenceWorker:
    serialize canonical state
    checksum, compress, and atomically replace destination
    release saveHandle
    publish request result
```

The cheap boundary operation is retaining a completed version and its pages, not performing file I/O. At most one full save serialization per world should run initially. A retained save can increase copy-on-write pressure, so memory and duration are bounded and observed; further requests coalesce by destination/policy or receive `SaveBusy`.

# Fault containment

| Failure | Required behavior |
| --- | --- |
| Invalid/rejected command | Record typed result; world continues |
| Tick evaluation, arithmetic, invariant, or commit failure | Discard transactional fork, retain last completed version, enter `Faulted` |
| Projection/encoding failure | Reset or disconnect affected stream; world continues |
| Slow client | Coalesce, snapshot, warn, or disconnect; world continues |
| Save serialization/write failure | Preserve prior valid save and running world; report request failure |
| Metrics/diagnostic sink failure | Disable/fail sink according to policy; never affect world |
| Process crash | Recover from last atomic save/checkpoint and command log; never claim uncommitted memory as saved |

Fault reports retain world/tick/revision, phase, exception classification, rules hash, command batch IDs, state-hash/checkpoint references, and bounded diagnostic context. They must not include secrets from another actor projection.

# Shutdown

Graceful shutdown is ordered:

1. stop accepting new sessions and lifecycle/gameplay requests;
2. request pause/stop on the runner;
3. allow the active authoritative transaction to finish, or discard its transactional fork at an explicit timeout-safe boundary;
4. optionally capture the configured shutdown save from the last completed version;
5. complete/cancel projection and persistence work without invalidating an atomic save;
6. flush retained command/event metadata required by policy;
7. release world versions and stop the host.

Cancellation is observed between phases for evaluation work, but a cancelled phase never commits a partial outcome. Exact graceful and forced timeouts remain deployment configuration.

# Scaling path

V1 intentionally excludes distributed execution of one world. The supported path is:

1. use tile/range parallelism within one process;
2. optimize data layout and allocation from profiling;
3. give a hot world more cores/memory;
4. run different worlds in different processes/containers/hosts;
5. add a routing/catalogue service when multiple world processes are operationally useful.

Competitive multiplayer adds actors, commands, and shared-clock policy to one runner. It does not change authoritative ownership.

# Invariants and tests

- Exactly one writer may hold a working world transaction.
- No background worker retains a mutable store reference or `Span<T>` after its phase/barrier.
- A tick or boundary transaction is visible in full or not at all.
- The last completed version remains readable after a transactional failure.
- Command retries cannot apply twice; replay uses recorded application order rather than network timing.
- Worker count, completion order, projection load, saves, queries, pause duration, and wall-clock speed do not change authoritative results.
- Slow/failing clients, encoders, metrics sinks, and save destinations cannot block tick ownership beyond their bounded capture work.
- Only one loaded world exists in a v1 process, while no public interface assumes an unqualified global world.
- Shutdown under every phase either commits one valid completed boundary or discards the working fork.

# Remaining calibration and implementation choices

- Speed presets, target wall intervals, unpaced yield cadence, and overload thresholds.
- Mailbox count/byte limits, maximum boundary batch, and command-result retention.
- Initial worker-count policy and whether benchmark evidence warrants dedicated workers.
- Publication cadence and maximum synchronous publication-copy budget.
- Save concurrency, memory budget, and graceful/forced shutdown timeouts.
- Operational process/container model for a future hosted multi-world service.

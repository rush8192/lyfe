# V1 Architecture

Status: first world-execution, deterministic worker/reduction, and ownership pass decided; exact operational limits pending calibration

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [INTERFACE vision](../../vision/INTERFACE.md), [technology decisions](TECHNOLOGY.md), [rule-pack authoring and compilation](RULE_PACK_AUTHORING_AND_COMPILATION.md), [moddability](MODDABILITY.md), [keyed randomness](KEYED_RANDOMNESS.md), and [deterministic parallel execution](DETERMINISTIC_PARALLEL_EXECUTION.md).

# Purpose

Define the process boundaries, project structure, dependency direction, runtime ownership, and concurrency model that allow one authoritative simulation to serve a thin browser client today and additional clients or players later.

# Inherited decisions

- The simulation is authoritative on a centralized server.
- Client and server remain separate even for local single-player.
- V1 includes free sandbox and survival, not competitive multiplayer.
- The simulation is a headless C# library on .NET 10.
- ASP.NET Core hosts the server boundary.
- The first client is TypeScript, React, and PixiJS.
- Determinism must not depend on execution speed or parallel scheduling.
- One `WorldRunner` is the exclusive writer for a loaded world.
- A v1 server process loads at most one active world, although every public interface remains explicitly world-scoped.

# System context

```text
Browser client
  ├── HTTP setup/save requests
  └── WebSocket commands and subscriptions
             │
             ▼
ASP.NET Core server
  ├── connection and command handling
  ├── simulation clock ownership
  ├── persistence orchestration
  ├── actor-authorized projection and change coalescing
  └── protocol mapping, retention, and backpressure
             │
             ▼
Pure simulation library
  ├── authoritative state
  ├── deterministic tick pipeline
  ├── resource ledger and events
  ├── phase/tick change journal
  └── completed-state queries and snapshots
```

# Proposed implementation layout

```text
implementation/v1/
  Lyfe.sln
  src/
    Lyfe.Simulation/
    Lyfe.Server/
    Lyfe.Protocol/
  tests/
    Lyfe.Simulation.Tests/
    Lyfe.Server.Tests/
    Lyfe.Determinism.Tests/
    Lyfe.Scenarios/
    Lyfe.Benchmarks/
  client/
  protocol/
  rules/v1/
  mods/
  world-packs/
  tools/Lyfe.RuleTool/
```

This layout is provisional until dependency rules and code generation are tested.

# Dependency rules to define

- [ ] `Lyfe.Simulation` depends only on general-purpose libraries justified by the simulation plan.
- [ ] `Lyfe.Server` may depend on simulation and protocol projects; simulation never depends on server.
- [ ] Generated protocol types do not become the authoritative internal model.
- [ ] Internal `TickChangeSet` types contain stable IDs and logical invalidations, never actor visibility, subscriptions, transport types, or dense slots.
- [ ] Client packages do not import server implementation code.
- [ ] Benchmarks invoke the simulation library directly without networking.
- [ ] A headless scenario runner can restore one completed-boundary snapshot, apply alternate ordered command sets, advance to authored observation landmarks, and emit stable machine-readable metrics without changing simulation state.
- [ ] Save/replay adapters do not leak storage concerns into organism logic.
- [x] Strict authoring, validation, normalization, hashing, and compilation remain cold-path namespaces inside `Lyfe.Simulation`; the small `Lyfe.RuleTool` executable references that library, and tick kernels receive only immutable compiled artifacts. See [RULE_PACK_AUTHORING_AND_COMPILATION.md](RULE_PACK_AUTHORING_AND_COMPILATION.md).

# Runtime ownership

The detailed contract is [WORLD_EXECUTION_AND_OWNERSHIP.md](WORLD_EXECUTION_AND_OWNERSHIP.md): a `WorldHost` manages one v1 loaded `WorldRunner`; that runner has exclusive write access, consumes a bounded mailbox, owns wall-clock scheduling, and commits transactional ticks or safe-boundary commands. ASP.NET sessions, projection, persistence, and metrics remain downstream or message-based. Internal phase workers receive stable reads and isolated outputs only.

The runner retains the last immutable completed root while a working copy-on-write transaction advances. Publication and saves use bounded immutable handles; encoding, sockets, compression, and file I/O cannot hold mutable arrays or delay the world beyond bounded capture work. Failures outside the tick are isolated, while a tick/invariant failure discards the working fork and faults the runner on the prior completed version.

# Concurrency model

The simulation behaves as one logical state machine even when internal phases use parallel workers. The normative execution semantics are in [DETERMINISTIC_PARALLEL_EXECUTION.md](DETERMINISTIC_PARALLEL_EXECUTION.md); in summary:

- A single owner for advancing each world.
- Phase barriers around parallel work.
- Per-tile or per-worker output buffers.
- Deterministic merging of movements, interactions, and resource claims.
- Read-only publication of completed state to network serializers.
- Deterministic phase-local change capture and tick-level merge through the typed authoritative mutation boundary.
- Cancellation and shutdown only between safe boundaries.

All biological randomness follows [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md). Workers receive semantic-address access only; no global or per-worker random stream exists.

The authoritative mutation, projection, batching, and client-application boundary is fixed in [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md). Projection workers consume completed read views and sealed tick changes after commit. They may lag, coalesce, snapshot, or disconnect without delaying the world owner or changing future simulation state.

# Observability

The server and simulation need distinct metrics. At minimum, plan counters for tick duration, queue depth, organism count, resource-event volume, snapshot generation, encoded bytes, connected clients, save duration, allocations, GC pauses, and state-hash failures.

Balance scenarios need a third, side-effect-free observation surface. `EventSink` and `MetricsSink` consume completed phase/tick results and cannot participate in claims, random decisions, organism behavior, or autonomous evolution. The batch runner should share immutable rule packs and initial snapshots across scenario branches while giving every mutable world an isolated owner. [OPENING_STRATEGY_VALIDATION.md](OPENING_STRATEGY_VALIDATION.md) defines the first required branch-comparison output.

# Required artifacts for the detailed pass

- [x] Component/dependency diagram.
- [x] Server and world lifecycle state machine.
- [x] Thread and queue ownership diagram.
- [x] Error taxonomy and shutdown sequence.
- [x] Pseudocode for loading, running, pausing, saving, and stopping a world.
- Headless scenario/batch-runner lifecycle, checkpoint-branching contract, and stable result schema.
- [x] Change-journal, projection-hub, and per-stream lifecycle contract; see [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md).
- [x] Decision on one-world-per-process versus multi-world hosting for v1.
- [x] World lifecycle, exclusive ownership, safe-boundary, save, fault, and shutdown contracts; exact operational limits remain open.
- [x] Semantic keyed-randomness contract and deterministic scalar/parallel evaluation, reduction, preflight, commit, and equivalence-oracle contract; permanent RNG domain values and concrete payload types remain implementation scaffold work. See [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md) and [DETERMINISTIC_PARALLEL_EXECUTION.md](DETERMINISTIC_PARALLEL_EXECUTION.md).
- [x] Rule-pack format, permanent typed identity policy, layer composition, validation/reporting pipeline, canonical hash boundary, global compiler, full DNA compiler, and authoring CLI responsibilities; concrete records and catalogue assignments remain. See [RULE_PACK_AUTHORING_AND_COMPILATION.md](RULE_PACK_AUTHORING_AND_COMPILATION.md).
- [x] V1 local balance-mod and closed-schema world-pack seams, field-level opt-in policy, immutable overlay composition, complete profile selection, conflict behavior, combined identity, and client/server boundaries; general content/code mods and distribution UX remain future work. See [MODDABILITY.md](MODDABILITY.md).

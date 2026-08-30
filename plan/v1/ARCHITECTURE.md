# V1 Architecture

Status: scaffold

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [INTERFACE vision](../../vision/INTERFACE.md), and [technology decisions](TECHNOLOGY.md).

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
  └── protocol mapping
             │
             ▼
Pure simulation library
  ├── authoritative state
  ├── deterministic tick pipeline
  ├── resource ledger and events
  └── state queries and snapshots
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
    Lyfe.Benchmarks/
  client/
  protocol/
```

This layout is provisional until dependency rules and code generation are tested.

# Dependency rules to define

- [ ] `Lyfe.Simulation` depends only on general-purpose libraries justified by the simulation plan.
- [ ] `Lyfe.Server` may depend on simulation and protocol projects; simulation never depends on server.
- [ ] Generated protocol types do not become the authoritative internal model.
- [ ] Client packages do not import server implementation code.
- [ ] Benchmarks invoke the simulation library directly without networking.
- [ ] Save/replay adapters do not leak storage concerns into organism logic.

# Runtime ownership to specify

- Server startup, shutdown, and world-loading lifecycle.
- Whether a server process hosts one world or several worlds in v1.
- Simulation-thread ownership and the boundary with ASP.NET worker threads.
- Command queues, query queues, and immutable snapshot publication.
- Pause, speed, deadline, and overload behavior.
- Fault containment when a tick, client connection, or save fails.

# Concurrency model

The simulation should behave as one logical state machine even when internal phases use parallel workers. The architecture plan must define:

- A single owner for advancing each world.
- Phase barriers around parallel work.
- Per-tile or per-worker output buffers.
- Deterministic merging of movements, interactions, and resource claims.
- Read-only publication of completed state to network serializers.
- Cancellation and shutdown only between safe boundaries.

# Observability

The server and simulation need distinct metrics. At minimum, plan counters for tick duration, queue depth, organism count, resource-event volume, snapshot generation, encoded bytes, connected clients, save duration, allocations, GC pauses, and state-hash failures.

# Required artifacts for the detailed pass

- Component/dependency diagram.
- Server and world lifecycle state machine.
- Thread and queue ownership diagram.
- Error taxonomy and shutdown sequence.
- Pseudocode for starting, pausing, saving, and stopping a world.
- Decision on one-world-per-process versus multi-world hosting.

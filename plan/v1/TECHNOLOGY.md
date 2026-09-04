# V1 Technology Decisions

This document records the initial technology and architectural choices for LYFE v1. These choices flow from the vision documents and should be revisited only when implementation evidence shows that they cannot meet the vision.

## Decision summary

| Area | V1 choice |
| --- | --- |
| Authoritative simulation | A custom, headless, data-oriented engine written in C# |
| Runtime | .NET 10 LTS with C# 14, using the normal JIT runtime |
| Server host | ASP.NET Core with Kestrel |
| First client | TypeScript, React, and PixiJS |
| Primary real-time transport | Binary Protocol Buffer messages over WebSockets |
| Control-plane transport | HTTP for setup, metadata, saves, and other request-response operations |
| Simulation ownership | The server is authoritative; clients never advance or resolve simulation state |
| Deployment boundary | Server and client remain separate processes even for local single-player play |
| Server packaging | OCI-compatible Linux image built by a multi-stage Dockerfile; Docker Compose is the reference portable local deployment path |
| Rule authoring and mods | Strict UTF-8 JSON, typed C# authoring records, generated JSON Schema, validated data-only balance overlays, and one immutable compiled rule set |

Competitive multiplayer is not part of v1. Nevertheless, the server boundary, command model, clock ownership, protocol, and player-control state must not assume that only one client or human actor can ever exist.

The detailed container decision and operational contract are in [DEPLOYMENT_AND_PORTABILITY.md](DEPLOYMENT_AND_PORTABILITY.md). Containers package and validate the server boundary; they do not replace native development, enter the simulation library, or commit v1 to a production orchestrator.

# Core simulation and server

## C# and .NET 10

The authoritative simulation and server will use C# 14 on .NET 10 LTS. C# provides the preferred balance of development speed, maintainability, tooling, networking support, and sufficient performance for the initial target of tens of thousands of organisms.

The choice depends on using a data-oriented hot loop rather than an allocation-heavy object graph. In performance-sensitive simulation code, we will prefer:

- Dense arrays of value types for frequently processed state.
- State partitioned by tile where that supports locality and independent work.
- Stable integer identifiers rather than direct object references between entities.
- Reused buffers, pooled arrays, and bounded temporary storage.
- `Span<T>`, `Memory<T>`, and related APIs where they make contiguous data processing clearer or reduce allocation.
- Explicit profiling before applying unsafe code, manual memory management, or SIMD optimizations.

Per-tick code should minimize heap allocations and avoid LINQ, reflection, and virtual dispatch across every organism in hot paths. These are guidelines for measured hot paths, not bans on ordinary C# elsewhere in the project.

## Performance decision policy

V1 begins with clear, conventional, well-bounded representations and profiles the complete path from simulation update through projection, encoding, client application, and rendering. It does not optimize a field, kernel, or message solely because that component can be made smaller or faster in isolation.

The starting defaults are signed 64-bit physical columns for all matter and energy quantities, ordinary strongly typed fields rather than packed bits, one straightforward typed Protocol Buffer representation per operation, and standard runtime/library facilities. A specialization is justified only when representative workload measurements show that its resource—CPU, memory residency, copy bandwidth, allocation/GC, wire bytes, decode/apply cost, or render cost—materially limits the intended world capacity or player experience. The proposal must include the end-to-end result, complexity and migration cost, determinism/correctness coverage, and performance of the unaffected parts of the system.

This policy does not weaken the data-oriented baseline or the single-source materialization rule. Dense storage, bounded buffers, and stored gameplay derivations are architectural correctness and scale choices. Narrow integer columns, bit packing, SIMD, custom codecs, adaptive sparse/dense forms, native kernels, and finely specialized page groups are deferred optimizations until the relevant bottleneck is demonstrated.

The initial implementation will use the normal .NET JIT runtime. Native AOT is not a starting requirement because startup latency is less important than sustained simulation throughput, profiling visibility, and compatibility. Production server configurations should evaluate Server GC, but garbage-collector settings must be selected from measurements rather than assumed in advance.

## Standalone simulation library

The simulation will be implemented as a pure library with no dependency on ASP.NET Core, React, PixiJS, Unity, Godot, or another game engine. It owns:

- World, tile, species, organism, and dead-remain state.
- The deterministic tick pipeline.
- Versioned counter-based simulation randomness addressed by stable semantic keys.
- Resource transfers and flow accounting.
- Mutation, speciation, lineage, lifecycle, and environmental rules.
- Saveable authoritative state and replay-relevant events.
- Stable simulation identifiers.

The library must run in tests, benchmarks, offline analysis tools, and the server host without a graphical environment.

The server host owns networking, connected-client sessions, command validation, clock control, persistence orchestration, and conversion between internal state and protocol messages. Networking code must not mutate simulation data directly. It queues validated commands for the simulation to accept at explicit tick boundaries.

## Explicit tick pipeline

The core engine uses explicit ordered phases rather than allowing organisms or asynchronous tasks to mutate shared state arbitrarily. The authoritative order and dependency table are defined in [SIMULATION_LOOP.md](SIMULATION_LOOP.md). In summary, a tick resolves commands; environment and non-biological resources; intrinsic death; movement; external acquisition/capture/interactions; internal metabolism and maintenance; lifecycle and reproduction; next-tick behavior; species systems; and final publication. Each phase commits before a dependent phase takes its fresh snapshot.

Parallel execution may accelerate work inside a phase, particularly across tiles, but worker completion order must not affect results. [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md) fixes `Philox4x64-10`, the permanent domain/address schema, and integer conversions. [DETERMINISTIC_PARALLEL_EXECUTION.md](DETERMINISTIC_PARALLEL_EXECUTION.md) fixes sealed phase views, isolated worker outputs, exact grouped reduction, canonical ID assignment, owner-only commit, and scalar/parallel equivalence. The first vertical slice runs the same pipeline with one worker and activates parallel evaluation only for measured phases.

The engine will not begin with a general-purpose ECS dependency. Dense custom storage is a better initial match for deterministic phase ordering and tile partitioning. An ECS library may be evaluated later if a representative prototype demonstrates a concrete benefit without weakening determinism.

Rules, traits, reactions, scenarios, and balance values cross the one-way cold-path boundary in [RULE_PACK_AUTHORING_AND_COMPILATION.md](RULE_PACK_AUTHORING_AND_COMPILATION.md). `System.Text.Json` metadata source generation loads strict domain records; explicit registries, semantic validators, normalization, and canonical hashing produce immutable global artifacts and typed compiled phenotypes. JSON, schema validation, stable-key maps, and file I/O never enter the organism tick path.

The same boundary is the supported mod seam. V1 should accept locally installed, declarative balance overlays that replace only explicitly registered fields before whole-pack validation and compilation, plus complete closed-schema world-generation profiles selected during world setup. It does not execute mod code or hot-reload an active world. The exact surface, provenance, conflict, save, client, and future-extension rules are in [MODDABILITY.md](MODDABILITY.md).

The first execution and storage contracts are now specified in [WORLD_EXECUTION_AND_OWNERSHIP.md](WORLD_EXECUTION_AND_OWNERSHIP.md) and [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md): one loaded world has one exclusive runner/writer; organisms/remnants begin in ordinary `256`-row tile-partitioned chunked structure-of-arrays stores; live stable-ID lookup begins with standard dictionaries; phase workers return isolated outcomes for owner-only preflight and commit; and saves/publications use detached boundary snapshots. Copy-on-write roots, custom locator paging, and alternative chunk/layout schemes require representative evidence.

## Determinism and conservation

Deterministic replay and internal mass balance are architectural properties, not later testing enhancements. The implementation must support:

- Stable iteration and interaction ordering.
- Root-seeded semantic-address random decisions that do not depend on call order, storage, or scheduling.
- State hashes for replay and parallelism tests.
- Resource-ledger checks across organisms, remains, tiles, transformations, sources, and sinks.
- Saving and restoring the root seed, RNG algorithm/schema IDs, semantic event ordinals, and rules state required for continuation.
- The exact base-pack, canonical mod-set, world-pack/profile/options, compiler, final-rules/scenario/world-rules, and RNG compatibility identities stored with every world.

The [resource deep dive](RESOURCE_MODEL.md) and [range proof](RESOURCE_CALIBRATION.md) select signed 64-bit game-native integer quanta for authoritative matter and energy quantities, with checked 128-bit intermediates. [Organism health calibration](ORGANISM_HEALTH_CALIBRATION.md) provisionally selects a parts-per-million fixed-point ratio for health, normalized factors, and probabilities. The first [world and climate contract](WORLD_AND_CLIMATE.md) adds signed tile coordinates, normalized fixed-point local positions, integer-meter elevation/depth, milli-degree Celsius temperature, integer precipitation rates, and the shared ratio type for normalized conditions. Spatial planning fixes `LocalCoordQ` as unsigned 32-bit tile fractions and velocity/displacement as signed 64-bit `LocalCoordQ` units per simulated hour, with wide checked squared-distance arithmetic. Final configured maxima and fixed-point representations for remaining rate domains still require the same cross-platform range proof.

# Server communication

## HTTP and WebSockets

ASP.NET Core and Kestrel will host the server interface.

HTTP is appropriate for bounded request-response operations such as:

- Creating and listing worlds.
- Reading world metadata and configuration.
- Starting setup flows.
- Saving, loading, and checkpoint management.
- Health, diagnostics, and static client assets where appropriate.

A persistent WebSocket connection will carry real-time commands and server updates. The initial message families should distinguish:

- Client commands and command results.
- World and tile snapshots.
- State deltas.
- Organism and species details.
- Simulation and lineage events.
- Resource-flow aggregates.
- Clock, pause, speed, and connection status.

Clients receive a complete actor-authorized snapshot when initially connecting or resynchronizing, followed by deltas and events during ordinary play. The server must filter state according to unknown, reduced, and live tile knowledge before serialization, then may narrow it further based on selected organisms or species, requested charts, and zoom level. Hidden current state is never sent for client-side masking. Such filtering changes transmission and presentation only; it does not change authoritative simulation fidelity.

## Protocol Buffers

Protocol Buffers will define the cross-language wire contract. Schema files are independent project artifacts and must not be generated from internal C# classes. Both C# and TypeScript bindings will be generated from the same versioned definitions.

Protocol evolution must follow additive compatibility rules: field numbers are never reused, removed fields are reserved, and messages include enough version context to reject incompatible commands cleanly. Internal save-state representation may reuse appropriate schemas, but network messages and saves are not required to have identical layouts.

Authoritative signed/unsigned 64-bit Protocol Buffer values map to C# `long`/`ulong` and TypeScript `bigint`. They never map to JavaScript `number`; presentation code converts only a proved bounded/scaled display value. JSON diagnostics or HTTP representations that cannot carry `bigint` encode these quantities as canonical decimal strings.

The scaffold pins `Google.Protobuf 3.36.1` and `Grpc.Tools 2.83.0` for C#, plus Buf CLI `1.72.0` and `protoc-gen-es 2.14.1` for TypeScript. Both bindings generate from the versioned `lyfe.v1` sources, and TypeScript receives native `bigint` mappings without handwritten numeric wrappers. If representative profiling later shows that Protocol Buffer encoding dominates server or client performance, FlatBuffers may be evaluated against the same message workload. We will not change formats based only on microbenchmarks.

# First client

## TypeScript, React, and PixiJS

The first client will be a browser application:

- TypeScript provides static checking around protocol messages and UI state.
- React provides the application shell, setup flows, inspectors, controls, mutation interface, and other stateful panels.
- PixiJS renders the tile map, within-tile organisms, dead remains, movement, and other dense 2D world content.
- DOM, SVG, or a focused charting library may render resource histories and the tree of life where that is more suitable than a canvas.

PixiJS should initially use its production-oriented WebGL renderer. WebGPU may be evaluated later after browser behavior and the LYFE rendering workload justify it.

The client is intentionally replaceable. It may interpolate received coordinates, manage camera state, select presentation detail, and compute display-only aggregates. It must not perform authoritative metabolism, movement, reproduction, resource resolution, mutation income, or random decisions. Presentation-only randomness must remain isolated from simulation randomness.

For local single-player play, the server runs locally and the browser connects through the same public boundary that a remote client would use. Development convenience must not introduce an in-process shortcut that couples the client to C# simulation objects.

# Alternatives considered

## Rust

Rust remains a technically strong alternative for a high-throughput deterministic engine because it provides native execution, explicit memory control, and compile-time concurrency guarantees without garbage collection. It was not selected because its learning curve and friction around frequently changing, interconnected state pose a material delivery and maintenance risk for this project.

Rust should be reconsidered only if representative C# prototypes fail the required simulation throughput, memory, or latency targets by a meaningful margin after profiling and reasonable data-layout improvements. A small advantage in an isolated benchmark is insufficient justification for changing languages.

## C++

C++ offers maximum control over layout, allocation, and execution, but would place greater responsibility for memory safety, concurrency safety, build integration, and long-term maintenance on the project. It is not selected for v1 and should be reconsidered only if the team gains a strong C++ requirement or existing expertise that changes this tradeoff.

## Java, Go, Python, and Node.js

Java is capable of the workload but offers no clear advantage over C# for LYFE. Go is attractive for network services but less compelling for the central data-oriented simulation. Python and Node.js remain useful for analysis, content tooling, build tasks, and test orchestration but will not host the authoritative simulation.

## Unity, Godot, and Bevy as the authoritative engine

The authoritative simulation will not be embedded in a rendering-oriented game engine. Doing so would unnecessarily couple server state, deterministic scheduling, headless deployment, and future clients to one presentation stack. These engines may still be considered for future client implementations that consume the public protocol.

## A mixed-language core

V1 will not combine a C# server with a Rust or C++ simulation library. A foreign-function boundary would introduce a second toolchain, cross-language memory ownership, duplicated debugging concerns, and more complex deployment before profiling demonstrates a need. Native kernels remain a later optimization option for isolated, proven bottlenecks.

# Validation before broad implementation

Validation should grow in stages. The first milestone is a minimal end-to-end walking skeleton, not a prematurely production-shaped benchmark. It proves that the chosen boundaries compose before the full biological model or performance machinery is built.

Stage A should include:

- One tile, one founder phenotype, one compiled mass-balanced reaction, and roughly `100..1,000` organisms.
- Real stable identifiers, owner-mutable dense state, keyed randomness, and the same phase interfaces intended for v1.
- A canonical state hash, logical change capture, detached save/reload, and a direct actor-authorized projection snapshot.
- Repeated scalar runs that produce identical resource ledgers and hashes.
- A generated binary projection consumed by the browser plus one absolute delta/cache-resynchronization path.

Stage B grows the same executable path to the default grid and approximately `10,000` organisms, adds the opening lifecycle/resource systems, expands the browser snapshot/deltas into the playable observation-and-decision loop, and profiles the complete path. Stage C exercises clustered `50,000` and `100,000` organism worlds, parallel execution, representative histories, save/reload, and protocol load before performance claims or specialized storage work are accepted. The `100,000` case is a capacity benchmark, not a gate that blocks learning from the first executable tick.

We should record:

- Sustained ticks per second at several organism counts.
- Median, high-percentile, and worst observed tick duration.
- Total memory and estimated memory per organism and tile.
- Bytes allocated per tick after warmup and garbage-collection pause behavior.
- Scaling across worker counts once Stage C introduces parallel execution.
- Snapshot and delta size and encoding time.
- Equality of state hashes across repeated runs, save/reload boundaries, and supported worker counts.
- Complexity and maintainability of the resulting code.

Passing thresholds will be defined alongside the representative world size and fastest-speed pacing target. If the prototype fails, we first profile the data model and algorithms. The language decision is reopened only when evidence indicates that the managed runtime is the limiting factor rather than the model itself.

# Versioning and references

The implementation scaffold should pin the .NET SDK, Node.js runtime, package-manager version, Protocol Buffer compiler and generators, and material library versions. Reproducible toolchains are part of deterministic development even when they are not themselves part of simulation determinism.

Relevant upstream documentation:

- [.NET 10 overview](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [.NET garbage-collector configuration](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector)
- [`Span<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.span-1?view=net-10.0)
- [`ArrayPool<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1?view=net-10.0)
- [ASP.NET Core WebSockets](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)
- [Protocol Buffers](https://protobuf.dev/overview/)
- [PixiJS renderers](https://pixijs.com/8.x/guides/components/renderers)
- [FlatBuffers](https://flatbuffers.dev/)

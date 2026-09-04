# Engineering Readiness Review

Status: v1 foundation review complete and Stage-A walking skeleton implemented; Stage B can grow the same seams into the playable opening

Sources: [technology](TECHNOLOGY.md), [architecture](ARCHITECTURE.md), [world ownership](WORLD_EXECUTION_AND_OWNERSHIP.md), [data model](DATA_MODEL.md), [entity storage](ENTITY_IDENTITY_AND_STORAGE.md), [rule compilation](RULE_PACK_AUTHORING_AND_COMPILATION.md), [moddability](MODDABILITY.md), [persistence](PERSISTENCE_AND_REPLAY.md), [client synchronization](STATE_CHANGE_AND_CLIENT_SYNC.md), and [validation](VALIDATION_AND_PERFORMANCE.md).

# Verdict

The core engineering direction is coherent enough to begin implementation. The high-cost decisions that are difficult to reverse have appropriate boundaries:

- a headless C#/.NET authoritative simulation and thin clients;
- one exclusive owner per world and no mutable state exposed to request, persistence, projection, or worker code;
- deterministic integer/fixed-point rules and semantically keyed randomness;
- stable identities at durable/domain boundaries, independent of dense storage positions;
- domain-shaped authored DNA and rules compiled into immutable numerical runtime profiles;
- data-oriented hot state, with tile-partitioned organism/remnant stores and resource-major tile stocks;
- mutation and logical change capture through the same typed operations;
- actor-authorized server projections, absolute mergeable patches, and full-snapshot recovery;
- exact rule/world/mod compatibility identity and deterministic saves/replay; and
- official configuration and world generation using the same bounded data seams later exposed to mods.

The review also found several designs that were individually defensible but collectively imposed too much machinery before evidence. They are simplified below. None changes the gameplay or long-term architectural direction.

# Simplifications adopted

## Ordinary owner-mutable world before MVCC

V1 begins with one ordinary mutable world behind its exclusive runner. A complete phase plan is evaluated and preflighted before owner commit; only a fully validated tick boundary is published or saved. An unexpected failure after mutation begins faults that in-memory world, and recovery uses the latest durable save.

The first implementation does not include a generic copy-on-write page root, MVCC leases, pooled-page rollback, or an undo journal. Detached boundary copies allow asynchronous save and projection work. Copy-on-write or double buffering is reconsidered only if snapshot pause measurements or a stronger availability requirement justify its correctness and maintenance cost.

## Standard locators before custom paging

Stable IDs remain mandatory, but the first live-ID locator is a standard typed dictionary. Dense rows and swap removal remain physical details. A segmented/direct locator is allowed only after measured lookup, memory, or GC cost demonstrates a material benefit.

## Concrete fields before a generic component framework

Confirmed compact v1 organism state belongs in normal typed column groups. A large and rare capability may later receive an explicit typed side store. V1 does not first build an ECS-like optional-component lifecycle or a custom locator per component.

## Permanent numeric IDs only where they earn their cost

Resources, traits, reactions, persistent command/event kinds, RNG domains, protocol vocabulary, and canonical hash tags require stable numeric identities because they affect hot lookup, saves, replay, network data, or deterministic addresses.

Cold mod parameters and player-selectable world options use stable namespaced string keys generated from the typed authoring contract. Their schema/API versions and package hashes already enforce compatibility. V1 does not maintain a numeric allocation and tombstone ledger for every tunable property.

Internal-only outcome-buffer and change-journal discriminators may use ordinary code enums. They become permanent compatibility vocabulary only if they later cross a save, replay, protocol, mod, or RNG boundary.

## Runtime materialization without mandatory cache persistence

The single-source rule remains strong: every shared gameplay derivation has one owner, one named barrier, one stored runtime result, and no consumer-side alternate formula.

Pure materializations need not become mandatory save schema. If all dependencies are saved or immutable, load may invoke the sole production owner to rebuild the complete table before hashing, publication, or continuation. Historical rings, moving averages, remainders, schedules, knowledge, and retained evidence contain information and remain primary saved state. Optional serialized materialization caches are a measured load-time optimization.

## Vertical growth instead of a tooling waterfall

The first real tick must not wait for the complete opening rule pack, polished mod tools, parallel execution, or a production-scale benchmark. It must still consume real compiled artifacts rather than hard-coded substitutes. Compiler, simulation, persistence, projection, and validation then grow together in executable vertical slices.

# First executable foundation

The recommended implementation order is:

1. Pin the solution/toolchain and establish architecture tests for dependency direction and prohibited nondeterministic APIs.
2. Define minimal typed IDs, canonical writers, strict rule/world records, and one tiny official source bundle.
3. Compile one resource set, one mass-balanced founding reaction, one founder genome, one tile profile, and one scenario into the real runtime forms.
4. Run one scalar owner-controlled tick over roughly `100..1,000` organisms, using keyed randomness and the intended phase/evaluator/commit interfaces.
5. Reconcile the resource ledger and produce the same `WorldStateHashV1` on repeated runs.
6. Emit a logical `TickChangeSet`, a direct actor-authorized full projection, and a detached save; reload, rebuild indexes/materializations, and reproduce the hash.
7. Add the first browser view from that projection, then add one absolute delta path and resynchronization.
8. Expand to the opening lifecycle and default grid around `10,000` organisms while profiling the real workload.
9. Add parallel evaluators through the existing reducer only where a measured phase benefits.
10. Validate clustered `50,000` and `100,000` organism capacity before adopting specialized layouts or claiming the v1 scale target.

This is a walking skeleton, not a disposable alternate architecture. Each seam is the intended production seam with deliberately small content.

# Minimum acceptance for the walking skeleton

- The minimal official rule and world sources fail closed and compile deterministically.
- No tick kernel reads authoring records, JSON, mod keys, or unresolved references.
- The same seed, sources, and commands produce the same ledger and logical state hash on repeated runs.
- One completed tick conserves every modeled element across declared sources, sinks, and transfers.
- A failed preflight applies nothing; an injected post-mutation defect faults without publishing or saving a partial boundary.
- Save capture and projection capture do not retain mutable world arrays.
- Save/reload plus required rebuilds reproduce the recorded logical hash and next tick.
- The direct authorized projection contains no data outside the actor's knowledge, and client state never uses dense server slots as identity.
- Logical mutation and dirty/change capture cannot diverge through an untracked general-purpose write path.

# Decisions that belong in the scaffold

These choices should be made and pinned while creating the repository structure because concrete code makes their tradeoffs visible. They are bounded substitutions, not unresolved architecture:

- exact .NET SDK, Node.js, package manager, test framework, analyzer, formatter, and CI versions;
- project/package names and dependency-enforcement mechanism;
- the Protocol Buffer C# and TypeScript generators; the semantic mapping is already fixed as `long`/`ulong` and native TypeScript `bigint` for authoritative 64-bit quantities;
- initial numeric assignments for durable definition/RNG/protocol/hash vocabulary;
- the first canonical hash tags and golden vectors;
- provisional chunk capacity and array-pool policy; and
- local development hosting and process-start commands.

# Decisions required before their feature slice

These do not block the simulation foundation, but must be closed before the named boundary is exposed:

| Before | Decision |
| --- | --- |
| First externally useful save | Container, atomic replacement/recovery procedure, checksums, compression, metadata header, and corruption errors |
| First command UI | Command envelope, client-command idempotency key, queued/applied/rejected states, and correlation semantics |
| First delta client | Concrete Protocol Buffer messages, stream/batch limits, acknowledgement retention, publication cadence, and resync thresholds |
| First autosave UX | Save cadence and a clear unsaved-progress indicator; v1 acknowledges applied commands without write-ahead durability |
| First parallel phase | Partition function, bounded worker outputs, exact reduction order, cancellation point, and scalar-equivalence fixtures |
| First externally installable mod/world pack | Discovery roots, bounded loader limits, package layout, diagnostics, and local trust messaging |
| First public compatibility promise | Engine/save/protocol version policy and duration for retaining old complete rule/world packages |
| Production scenario/range freeze | Default deadline, final-tick outcome precedence, maximum tick/event ordinals, and the resulting numeric range proof |

# Intentional non-blockers

The following should remain calibrated or deferred rather than guessed into the foundation:

- production queue, batch, history-retention, reconnect, and backpressure numbers;
- final hardware budgets and fastest-speed thresholds;
- narrower resource columns, bit packing, SIMD, custom codecs, or native kernels;
- custom locators, adaptive sparse/dense balances, and generalized optional components;
- copy-on-write snapshots, in-process failed-tick rollback, durable command journaling, and multi-world process scheduling;
- polished mod installation/UI, general content extension, scripts, or executable mods;
- save migrations across changed simulation semantics; and
- deployment orchestration, accounts, matchmaking, or competitive multiplayer services.

Each enters only with a gameplay/product requirement or representative measurement.

# Assumptions to challenge during implementation

## Tile partitioning

Tile-locality matches most work and spatial interaction, but highly clustered populations or frequent migration could make a tile's chunks and commit buffers pathological. Preserve the logical storage interface and benchmark clustered/coastal migration cases; do not embed tile/chunk positions into identity or protocol.

## One-hour tick

The configured one-hour tick is a balance choice, not an excuse to bake `1` into rates, cooldowns, or movement. Compile per-hour rules into checked per-tick forms and include tick-duration semantics in compatibility identity.

## Full-fidelity 1:1 organisms

The vision requires displayed organisms to correspond to simulated organisms, but this does not require publishing or rendering every off-screen organism every tick. Server interest projection and aggregate rendering must preserve truth while bounding bandwidth and frame cost.

## Determinism across hosts

Integer arithmetic and Philox remove major variation, but collection enumeration, parallel reductions, text normalization, serializer behavior, overflow configuration, and library upgrades can still diverge. Golden vectors and logical hashes must run on every supported architecture in CI before cross-host replay is promised.

## Mod compatibility

A schema-valid mod combination can still create a biologically unplayable world. Mandatory safety/conservation validation and optional experience certification must remain separate, visible results. Do not attempt to prove fun or balance as a compiler invariant.

## Network deltas

Generating logical dirtiness at mutation time is the right seam, but emitting per-scalar heap objects would defeat it. The first implementation should use bounded typed buffers and allow chunk-level dirtiness; projection profiling decides the granularity.

# Complexity budget

A foundation mechanism is admitted before the first opening-game slice only when it protects at least one of:

- authoritative correctness or conservation;
- deterministic replay and stable identity;
- server/client trust or hidden-information boundaries;
- an expensive-to-reverse persistence/protocol contract; or
- a measured capacity bottleneck.

Otherwise the plan preserves an interface seam and uses the simplest implementation. This rule is especially important because LYFE's biological breadth is already the primary source of necessary complexity.

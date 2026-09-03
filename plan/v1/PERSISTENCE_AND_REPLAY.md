# Persistence, Checkpoints, and Replay

Status: first save-content, detached boundary-snapshot, compatibility, state-hash, and v1 crash-durability pass; container, replay export, and retention details pending

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [GAMEPLAY vision](../../vision/GAMEPLAY.md), [player loop and narrative](PLAYER_LOOP_AND_NARRATIVE.md), [keyed randomness](KEYED_RANDOMNESS.md), [deterministic parallel execution](DETERMINISTIC_PARALLEL_EXECUTION.md), [rule-pack authoring and compilation](RULE_PACK_AUTHORING_AND_COMPILATION.md), [moddability](MODDABILITY.md), and [technology decisions](TECHNOLOGY.md).

# Purpose

Define how complete worlds are saved, resumed, checkpointed, verified, and replayed under one exact compiled-rules, mod-set, and world-generation identity.

# Save contents

A complete save must account for:

- World identity, seed, mode, normalized setup options, and the complete simulation-compatibility identity: engine simulation version, base-pack identity, canonically ordered balance-mod package identities/mod-set hash, final mechanics and registry-manifest hashes, rule-compiler version, scenario identity, world-pack package/profile identity, selected-world-options hash, compiled-world-profile/world-rules hashes, and RNG algorithm/schema/domain-manifest identity.
- Tick duration, current tick/calendar, final date, pause, and speed state as applicable.
- The 128-bit root seed, pinned RNG algorithm ID, RNG schema version, and subsystem-owned semantic event ordinals; there is no mutable global or per-worker random stream/cursor.
- Fixed geography, baselines, current conditions, integer resource accounts, and named gases.
- Every living organism and dead remnant, including structural matter, available stores, charged energy-bearing reserves, zero-energy spent reserve carriers, and active metabolic binding cohorts with release ticks.
- Every living organism's selected behavior, typed target, selection tick, dwell boundary, and enabled recent energy/acquisition-coverage memory.
- Persisted fractional remainders used by sources, sinks, exchange, rates, and energy costs.
- Species genome references, mutation balances and UInt128 income remainders, evolution revisions/ordinals, absolute speciation cooldowns, pressure accumulators, autonomous intents/evaluation ordinals including material- and biological-opportunity snapshots, aggregates needed for exact continuation, and locks.
- Exact one-hour tile flow-history rings and rolling totals for resources referenced by selectable material-opportunity profiles, covering the 168-hour scoring window and including source, passive loss, inbound/outbound exchange, and biological uptake.
- Sparse per-tile ordered species-pair predation-history rings and rolling totals for the 168-hour biological-opportunity window, including eligible encounters, attempts, successes, deaths, grants, and remnant remainder.
- Complete immutable genome, abiogenesis, speciation-event, species-lineage, extinction, and controller state.
- Abiogenesis origin, root-species membership, and per-actor discovered tiles, last observations—including retained per-tile behavior distributions—and observation timestamps.
- Pending accepted commands or proof that saves occur only at a boundary with none.
- Historical aggregates and event retention needed by gameplay and the client.
- Canonical notable-event facts, deduplication/hysteresis state, active consequence-review anchors, and any actor evolution goal or attention policy whose evaluation can cause an authoritative pause.
- Completed materialized tile climate/resource-effective values, organism condition/capacity/activation values, and species/tile aggregates with their dependency generations and compiled hashes.

The save contains the complete authoritative world even where a player lacks visibility. Loading or reconnecting must rebuild only that actor's authorized unknown/reduced/live projection. Replays restore knowledge state at checkpoints and reproduce visibility transitions from controlled-species occupancy; watching a replay must not retroactively fill hidden historical intervals unless a separate omniscient presentation mode is explicitly selected after the run.

Gameplay-relevant derived values follow the materialize-once contract in [MATERIALIZED_DERIVED_STATE.md](MATERIALIZED_DERIVED_STATE.md). Histories, moving averages, remainders, next-action schedules, actor knowledge, and any other information-bearing recurrence input are primary and saved. Pure completed values such as used load/capacity, organism health/activation, current tile effective values, and current species aggregates remain stored runtime single sources but may be omitted from the baseline save and rebuilt through their sole owners before load validation/publication. A save may carry them as an all-or-nothing compatible acceleration cache. Ephemeral candidate lists, discarded behavior utilities, phase allocation/claim arenas, spatial indexes, and presentation-only derivations remain excluded. Last-observed behavior distributions remain persistent actor knowledge with their observation tick.

Chronicle titles and prose, panel layout, visual pins, and private notes are presentation/profile data and are excluded from authoritative state hashes. Canonical notable-event facts and an automatic pause actually applied by an attention policy are historical records. Replaying the simulation regenerates the same facts; presentation may render them with different wording. Private annotations never become autonomous-evolution evidence.

# Snapshot consistency

Saves are captured only from a completed tick or safe command boundary. The runner synchronously copies canonical logical state into a detached `SaveSnapshot` for that exact tick/revision, then the persistence worker serializes, compresses, checksums, and atomically replaces the destination asynchronously. The world need not remain paused after the bounded copy. Initially only one full save serialization runs per world; compatible requests may coalesce and others return a typed busy result. A save must be atomic from the loader's perspective and must not replace a valid prior save with a partial file. Copy-on-write world versions are deferred unless measured snapshot pauses justify them. See [WORLD_EXECUTION_AND_OWNERSHIP.md](WORLD_EXECUTION_AND_OWNERSHIP.md) and [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md).

Internal phase/tick dirty buffers and per-connection projection-stream state are not canonical save contents. After load, stores begin with clean change metadata and the server builds a fresh actor-authorized projection snapshot or resumes only from separately retained compatible stream batches. Accepted command records, canonical events, authoritative actor knowledge, and histories remain saved according to their own ownership; a transient `TickChangeSet` never substitutes for them. See [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md).

# V1 durability contract

A successfully completed save/checkpoint is the v1 crash-durability boundary. A boundary command result means the command was authoritatively applied to the running world, but it does not promise synchronous write-ahead persistence. A process or machine failure may therefore lose progress since the most recent completed save. The client should display unsaved progress and may request/autoschedule boundary saves according to later UX policy.

The in-memory accepted-command log supports deterministic replay export and diagnostics. It is not a write-ahead log and is not sufficient by itself for crash recovery. Adding durable command journaling, group commit, or exactly-once recovery is future hosted-service work and must be justified by a stronger availability promise.

# Replay model

The detailed plan should distinguish:

- Deterministic continuation from a full snapshot.
- Replay from an initial snapshot plus ordered commands.
- Periodic checkpoints used to seek within a long replay.
- Presentation replay versus authoritative re-simulation.

```text
Replay(initialSnapshot, commandLog, targetTick):
    validate the complete simulation-compatibility identity
    load nearest checkpoint at or before target
    apply commands at recorded ticks and order
    advance every intervening tick
    compare optional checkpoint hashes
    return state at target or a divergence report
```

# Format and evolution

Decide:

- Container format and file layout.
- Schema ownership separate from in-memory C# layout.
- Compression and checksums.
- Save metadata readable without loading the full world.
- Compatibility policy across rule and engine versions.
- Movement/migration event provenance, including the v1-zero environmental-displacement component, and compatibility behavior when later rule packs add environment-driven transport.
- Retention and size limits for events and resource histories.
- Whether small data-only base/mod/world-pack sources and locks are embedded in saves by default or exported as a portable sidecar bundle.

# Determinism diagnostics and state hash

`WorldStateHashV1` uses SHA-256 over a versioned canonical logical writer. The preamble includes `WorldStateHashSchemaVersion`, the complete simulation-compatibility identity (including RNG algorithm/schema/domain-manifest identity), root seed, completed tick, and world revision. Records then appear in fixed type/field-tag order; entity-bearing stores are sorted by stable ID, fixed tile/edge arrays use canonical IDs, maps become sorted key/value sequences, optional values carry presence markers, integers use fixed-width little-endian encoding, and strings use length-prefixed UTF-8 NFC.

The hash includes all primary continuation state, next-ID counters, RNG ordinals, fractional remainders, retained histories/evidence that future behavior or autonomy can read, actor control/knowledge state, canonical event/log state promised by replay, and completed gameplay materializations consumed by the next boundary. Including a materialization in the logical hash does not require storing its cache bytes: load rebuilds any omitted pure materialization before verifying the hash. The hash excludes dense slots and row order where nonsemantic, locators, spatial indexes, dirty bits, capacity slack, scratch buffers, pool state, metrics, logs, wall-clock scheduling, projection streams, encoded messages, and presentation-only state.

Hashing the logical model is intentionally independent of the save-container bytes and physical in-memory layout. A save stores its completed logical hash and load recomputes it after validation/rebuilding indexes; mismatch rejects the load. Debug/determinism fixtures may hash after every phase or tick. Production computes it at creation/load/save, at authored diagnostic checkpoints, and optionally at a configurable low cadence—not obligatorily every tick.

A hierarchical diagnostic mode also records per-store and per-tile/species digests so a divergence report can identify the first mismatched tick, phase, subsystem, tile, resource, or entity. These child digests aid diagnosis but do not replace the canonical whole-world hash.

# Local and future server storage

V1 may use local files, but persistence interfaces should not assume the client has filesystem access or that the future server uses the same storage backend. The server mediates all save operations.

# Required decisions and tests

- [ ] Save/checkpoint schema and container.
- [ ] Atomic write and recovery procedure.
- [x] Completed-boundary detached logical snapshot and background serialization ownership; exact container, memory, concurrency, and timeout limits remain open.
- [x] RNG continuation state and validation boundary: canonical root seed, pinned algorithm ID, RNG schema version, domain-manifest hash, and persistent semantic ordinals are saved; mutable stream cursors do not exist. Exact container encoding remains open. See [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md).
- [ ] Replay command-log format.
- [x] `WorldStateHashV1` algorithm, logical scope/exclusions, canonical order, load verification, and debug/production cadence; concrete numeric record/field tags and golden vectors remain scaffold work.
- [x] Exact base/mod-set/world-pack/profile/options/final-rule/world compatibility identity and fail-closed load boundary; see [RULE_PACK_AUTHORING_AND_COMPILATION.md](RULE_PACK_AUTHORING_AND_COMPILATION.md) and [MODDABILITY.md](MODDABILITY.md).
- [ ] Engine/save-schema migration and retained multi-version compatibility policy.
- [ ] Event/history retention and compression.
- [ ] Canonical-event versus presentation-preference storage boundary, including export/privacy behavior for private notes.
- [ ] Corruption detection and user-visible errors.
- [ ] Round-trip equality tests.
- [ ] Long replay and deliberate-divergence tests.
- [x] V1 crash durability ends at the last completed save; accepted commands are not synchronously write-ahead durable.

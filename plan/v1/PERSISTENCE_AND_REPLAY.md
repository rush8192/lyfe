# Persistence, Checkpoints, and Replay

Status: scaffold

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [GAMEPLAY vision](../../vision/GAMEPLAY.md), and [technology decisions](TECHNOLOGY.md).

# Purpose

Define how complete worlds are saved, resumed, checkpointed, verified, and replayed under a specific simulation-rules version.

# Save contents

A complete save must account for:

- World identity, seed, mode, configuration, and rules version/hash.
- Tick duration, current tick/calendar, final date, pause, and speed state as applicable.
- Random-stream or counter state.
- Fixed geography, baselines, current conditions, integer resource accounts, and named gases.
- Every living organism and dead remnant, including structural matter, available stores, energy-bearing reserves, and active metabolic binding cohorts with release ticks.
- Persisted fractional remainders used by sources, sinks, exchange, rates, and energy costs.
- Species DNA, mutation balances, aggregates needed for exact continuation, and locks.
- Complete lineage and controller state.
- Abiogenesis origin, root-species membership, and per-actor discovered tiles, last observations, and observation timestamps.
- Pending accepted commands or proof that saves occur only at a boundary with none.
- Historical aggregates and event retention needed by gameplay and the client.

The save contains the complete authoritative world even where a player lacks visibility. Loading or reconnecting must rebuild only that actor's authorized unknown/reduced/live projection. Replays restore knowledge state at checkpoints and reproduce visibility transitions from controlled-species occupancy; watching a replay must not retroactively fill hidden historical intervals unless a separate omniscient presentation mode is explicitly selected after the run.

Derived stored-energy, elemental totals, and organism health need not be duplicated in the save when they can be recomputed exactly from resource definitions, account quantities, DNA, and environment. If health is retained as diagnostic snapshot data, load must recompute and verify or discard it according to [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md).

# Snapshot consistency

Saves should be captured only from a completed tick boundary. Define whether the simulation pauses briefly, copies an immutable snapshot, or uses another approach. A save must be atomic from the loader's perspective and must not replace a valid prior save with a partial file.

# Replay model

The detailed plan should distinguish:

- Deterministic continuation from a full snapshot.
- Replay from an initial snapshot plus ordered commands.
- Periodic checkpoints used to seek within a long replay.
- Presentation replay versus authoritative re-simulation.

```text
Replay(initialSnapshot, commandLog, targetTick):
    validate rules and configuration hashes
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
- Whether migrations are supported in v1 or incompatible saves are rejected clearly.
- Retention and size limits for events and resource histories.

# Determinism diagnostics

Store or derive state hashes at selected checkpoints. A divergence report should identify the first mismatched tick and, where possible, the subsystem, tile, resource, or entity that differs.

# Local and future server storage

V1 may use local files, but persistence interfaces should not assume the client has filesystem access or that the future server uses the same storage backend. The server mediates all save operations.

# Required decisions and tests

- [ ] Save/checkpoint schema and container.
- [ ] Atomic write and recovery procedure.
- [ ] Snapshot boundary and background serialization.
- [ ] Replay command-log format.
- [ ] State-hash scope and cadence.
- [ ] Version-compatibility policy.
- [ ] Event/history retention and compression.
- [ ] Corruption detection and user-visible errors.
- [ ] Round-trip equality tests.
- [ ] Long replay and deliberate-divergence tests.

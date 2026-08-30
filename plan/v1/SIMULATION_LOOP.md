# Simulation Loop and Action Resolution

Status: scaffold

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [ORGANISMS vision](../../vision/ORGANISMS.md), and [technology decisions](TECHNOLOGY.md).

# Purpose

Define the fixed-tick state machine, command boundary, deterministic action ordering, parallel work, and event publication for the authoritative world.

# Time model

- The default tick represents one simulated hour and is configurable per world.
- Speed changes alter real processing cadence, never simulated tick duration.
- Pause stops tick advancement at a safe boundary.
- No tick skipping or coarse fast-forward exists.
- Calendar state drives seasons and the configured final date.

The detailed plan must specify wall-clock scheduling, catch-up policy, whether the fastest mode runs unpaced, and how overload is surfaced without silently dropping ticks.

# Candidate phase order

The phase list in [TECHNOLOGY.md](TECHNOLOGY.md) is a starting hypothesis, not yet final. The detailed pass must resolve dependencies among:

- Player-command admission.
- Calendar and environmental updates.
- External sources, environmental sinks, atmospheric mixing, and nutrient exchange. Gas phases specifically resolve source, attrition, exchange, then biological claims.
- Aging and maintenance.
- Sensing and behavior selection.
- Resource absorption and metabolism.
- Predation, scavenging, and reproduction.
- Movement and cross-tile migration.
- Stress and death evaluation.
- Remnant creation and decomposition.
- Species aggregates, mutation income, and autonomous speciation.
- Historical aggregation, events, state hashes, and client publication.
- Per-actor live-tile derivation, discovery/last-observation updates, and visibility-filtered publication.

# Intent-based resolution

Organism evaluation should generally produce immutable intents rather than immediately changing shared targets:

```text
AdvanceTick(world, commands):
    apply commands accepted for this tick
    update calendar and environment
    resolve gas sources, environmental attrition, and neighbor exchange
    readView = build stable phase view
    intents = evaluate organisms, tiles, and autonomous systems
    resourceGrants = proportionally resolve resource claims with stable remainders
    resolved = resolve reactions and conflicting interactions(intents, resourceGrants)
    apply resolved transactions in canonical order
    finalize movements, births, deaths, and speciation
    update aggregates and histories
    update actor knowledge from completed controlled-species occupancy
    validate invariants and compute optional state hash
    publish actor-authorized events and deltas
```

The detailed version must state which phases require fresh reads after prior writes and which can share one snapshot.

# Deterministic randomness

Define a random-stream scheme that covers world generation, weather, organism decisions, interaction outcomes, founder selection, and autonomous evolution. It must:

- Reproduce with the same seed, rules, configuration, and ordered commands.
- Remain unchanged when presentation requests differ.
- Avoid dependence on worker completion order.
- Permit save/reload without changing the next draw.
- Support diagnostic attribution of important random outcomes.

Compare mutable named streams with counter-based/key-derived draws. Document how new random call sites affect compatibility with existing saves.

# Parallelism

The likely first parallel boundary is tile-level evaluation. Cross-tile movements and exchanges should be emitted into deterministic boundary buffers and applied after a barrier. The plan must cover load imbalance when a few tiles contain most organisms.

# Commands and queries

- Commands carry actor, expected world, and desired application tick or server receipt order.
- The server validates mode and control authority before admission.
- Accepted commands receive a deterministic order and become replay inputs.
- Queries and client subscriptions cannot mutate simulation or consume simulation randomness.

# Failure policy

Define behavior for invalid commands, invariant failures, tick exceptions, excessive tick duration, client disconnects, save requests during a tick, and shutdown. A partially applied tick must never be published or saved as a valid completed state.

# Required decisions and artifacts

- [ ] Final phase dependency graph.
- [ ] Intent and resolution data structures.
- [ ] Conflict rules for shared prey, remains, and resources.
- [ ] Birth/death/movement and live/reduced visibility transitions within the same tick.
- [ ] RNG stream/key design.
- [ ] Parallel partition and deterministic merge.
- [ ] Tick scheduler and overload policy.
- [ ] Pseudocode for pause, speed change, deadline, and command admission.
- [ ] Sequence diagrams for an ordinary tick and a speciation tick.

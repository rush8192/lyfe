# Validation, Testing, and Performance

Status: scaffold

Sources: all vision documents and [technology decisions](TECHNOLOGY.md).

# Purpose

Define how v1 proves biological rules, mass balance, deterministic replay, protocol correctness, visual truthfulness, and useful performance.

# Test layers

## Unit tests

- Trait validation and compiled effects.
- Resource composition expansion, transfers, and statically balanced reactions.
- Energy-carrier conversion and capacity enforcement.
- Health, stress, and probability curves.
- Reproduction allocation.
- Neighbor lookup and x wrapping.
- Calendar and climate functions.
- Protocol validation and command authority.

## Property and generative tests

- Matter is conserved absent declared sources and sinks.
- Named compounds and generic pools never duplicate the same matter.
- No reservoir becomes negative.
- Reproduction cannot create resources.
- Death transfers all remaining contents exactly once.
- Proportional contention grants no more than the available pool and is independent of input order.
- Trait graphs never accept invalid prerequisites or incompatibilities.
- Generated worlds satisfy topology and founding-tile guarantees.
- Generated survival worlds contain a deterministic eligible hydrogen/sulfide paired region.
- Save/load preserves authoritative state.
- Configured account and tick horizons satisfy the resource range proof.
- Element expansion and world reconciliation use checked `Int128` when totals exceed `long`.

## Determinism tests

- Same seed/configuration/commands produce equal state hashes.
- Single-threaded and supported parallel worker counts agree.
- Pause and real-time speed changes do not alter results.
- Client subscriptions and presentation requests do not alter results.
- Save/reload continuation matches uninterrupted execution.
- Replay identifies intentional divergence.

## Integration and scenario tests

- Founding species survives a known viable setup.
- The sulfide fixture reaches its first split at tick 250 with the stated first-tick ledger and dark-period reserve minimum; the hydrogen fixture reaches its first split near tick 334.
- The provisional mutation-income fixture reaches hydrogen's 40 MP option near tick 338 and sulfur's 60 MP option near tick 511.
- Scarcity can cause starvation and recovery.
- Reproduction, death, remains, and decomposition form a complete cycle.
- The bounded hydrogen fixture reconciles every CHNOPS element against its declared volcanic and water boundaries.
- The volcanic starting fixture supports adapted founders but rapidly harms otherwise identical organisms without sulfur tolerance.
- Missing manganese, molybdenum, and copper prevent population growth through their gated advanced capabilities.
- Oxygen production spreads through tile-local atmosphere.
- Volcanic gas fields reproduce their activity-dependent center/neighbor plateaus, conserve exchange, and cannot sustain 15 equivalent founders in a source-free first-ring tile.
- Speciation preserves ancestor/descendant balances and lineage.
- Sandbox control/locks and survival loss/completion follow mode rules.
- Paired Survival initialization creates equal-sized independent roots, ordinary autonomous competition, and no false lineage parent edge.
- Unknown, reduced, and live tile projections never disclose disallowed state; migration, vacancy, save/load, reconnect, and sandbox control transfer preserve knowledge correctly.
- Browser resynchronizes after missing deltas.

# Representative performance benchmark

Begin with the workload defined in [TECHNOLOGY.md](TECHNOLOGY.md): approximately 100,000 organisms across several hundred tiles with metabolism, movement, reproduction, death, remains, atmospheric exchange, resource histories, save/reload, state hashing, and representative protocol encoding.

Record:

- Ticks per second and tick-time distribution.
- Scaling by organism count, tile distribution, and worker count.
- Total memory and bytes allocated per tick after warmup.
- Garbage-collection frequency and pauses.
- Time spent by simulation phase.
- Save size/time and load time.
- Snapshot/delta encoding size and time.
- Client frame time, visible entity count, and update cost.

# Acceptance thresholds to define

- Reference server hardware and operating system.
- Reference browser/client hardware.
- Required organism and tile counts.
- Fastest-speed sustained tick rate.
- Maximum acceptable high-percentile tick duration.
- Save/load and reconnect expectations.
- Memory budget.
- Visible-entity and chart performance.

Thresholds should represent playable worlds, not isolated microbenchmarks.

# Instrumentation

Every major simulation phase should expose elapsed time and processed counts. Plan debug-only resource reconciliation, per-tile hot-spot reporting, allocation and GC metrics, command queue depth, client queue depth, encoded bandwidth, and deterministic state hashes.

# Continuous integration

Define CI stages for formatting, static analysis, unit tests, deterministic fixtures, protocol compatibility, client tests, and a bounded benchmark smoke test. Longer soak, replay, and performance suites may run separately but must be repeatable and retain comparable results.

# Technology reconsideration gate

C# is reconsidered only after representative profiling shows that the managed runtime—not an avoidable algorithm, layout, allocation pattern, or serialization choice—is preventing required performance by a meaningful margin. Any Rust or C++ comparison must run the same workload and determinism checks.

# Required artifacts

- [ ] Test taxonomy and project mapping.
- [ ] Canonical small deterministic fixtures.
- [ ] Resource-conservation oracle.
- [ ] State-hash definition.
- [ ] Benchmark world/rule pack.
- [ ] Reference hardware and acceptance thresholds.
- [ ] Profiling and metric naming conventions.
- [ ] CI matrix and performance-regression policy.
- [ ] Soak-test and failure-injection plan.

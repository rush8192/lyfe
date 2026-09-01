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
- Age-derived metabolic-throughput curves and whole-extent rounding.
- Reproduction eligibility, allocation, cooldown scheduling, and keyed jitter bounds.
- Simple-scavenging admission, transfer caps, contention, action cost, and cooldown scheduling.
- Structural-food reaction CHNOPS/energy balance, throughput limits, output-capacity admission, and waste routing.
- Evolved reproduction-profile composition, growth-floor selection, lifecycle transition costs, and dormancy action gates.
- Neighbor lookup and x wrapping.
- Calendar and climate functions.
- Periodic elevation/weather seams, bounded polar neighbors, depth-light anchors, moisture recurrence, and starting-region scoring/repair.
- Local-coordinate arithmetic, exhaustive-versus-indexed proximity results, placement, same-tile interaction locality, zero-mean keyed Brownian displacement, and one-edge migration traversal.
- Protocol validation and command authority.

## Property and generative tests

- Matter is conserved absent declared sources and sinks.
- Named compounds and generic pools never duplicate the same matter.
- No reservoir becomes negative.
- Reproduction cannot create resources.
- An organism reproduces on the first tick all deterministic gates pass, never because of a separate success roll; failed gates do not reroll or extend its scheduled cooldown.
- Primitive cooldown scheduling always produces `24..27 h`, and save/load or iteration-order changes reproduce the same keyed value for each organism and reproduction ordinal.
- A basic scavenger acts at most once per two simulated hours, never exceeds `200` reserve, `256` dissolved-load, or `8` micronutrient units per action, and starts cooldown after an admitted zero-grant contention loss but not after pre-admission rejection.
- High-throughput scavenging uses caps of exactly `300` reserve, `384` dissolved load, and `12` micronutrients at a `35`-energy cost without shortening the two-hour cooldown.
- Each structural-food extent debits `C100 H170 O40 N20 P2 S1`, credits `32 ReserveOrganic + 1 SpentStructuralResidue(C68 H106 O8 N20 P2 S1)`, stores exactly 32 energy, and dissipates exactly eight of the 40 gross recoverable units.
- `SpentStructuralResidue` is rejected as input to v1 energy-yielding catabolism and its 90-day mineralization credits exactly 68 `InorganicCarbon`, 106 `InorganicHydrogen`, 8 `InorganicOxygen`, 20 `InorganicNitrogen`, 2 `InorganicPhosphorus`, and 1 `InorganicSulfur` per whole residue unit, without usable energy.
- At mature structure `1,000`, particulate ingestion cannot exceed 50 structure-equivalent units per tick, buffered load cannot exceed 250, and digestion cannot exceed ten whole structural extents per hour before other limiters.
- Dormancy never permits growth, reproduction, active movement, predation, or scavenging; resistant dormancy cannot enter below `110%` of mature structure and routes exactly half of structure to the resistant decay cohort on death.
- Before 30 biological days, age does not reduce metabolic extent; at the primitive age-factor floor it caps otherwise admissible extent at exactly 75%, without changing per-extent mass or energy accounting.
- Death transfers all remaining contents exactly once.
- Founder growth never spends reserve below `4,000`; primitive fission cannot commit below `0.60` health or produce a result below `4,000` reserve.
- Reference-condition remnant cohorts reproduce their 7-, 14-, 30-, and 90-day half-lives within fixed-point tolerance, and passive organic-to-inorganic mineralization reproduces its 90-day half-life without changing elemental totals.
- Proportional contention grants no more than the available pool and is independent of input order.
- Trait graphs never accept invalid prerequisites or incompatibilities.
- Generated worlds satisfy topology and founding-tile guarantees.
- Generated survival worlds contain a deterministic eligible hydrogen/sulfide paired region.
- Default generated worlds satisfy the configured aquatic fraction and paired-region count or fail explicitly after the same bounded attempt sequence.
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
- The sulfide square fixture reaches its first split at tick 250 with the stated first-tick ledger and dark-period reserve minimum; the generated-climate reference reaches tick `263` and accepted generated starts remain in `250..275`; the hydrogen fixture reaches its first split near tick 334.
- The provisional mutation-income fixture reaches hydrogen's 40 MP option near tick 338 and sulfur's 60 MP option near tick 511.
- Scarcity can cause starvation and recovery.
- Reproduction, death, remains, and decomposition form a complete cycle.
- At the reference condition, a 14-day-old ordinary remnant retains approximately 25% of dissolved contents, 50% of reserve compounds, and 72% of structure before scavenging; the sequential 30-day structural and 90-day mineralization paths remain conservative.
- The bounded hydrogen fixture reconciles every CHNOPS element against its declared volcanic and water boundaries.
- The volcanic starting fixture supports adapted founders but rapidly harms otherwise identical organisms without sulfur tolerance.
- Missing manganese, molybdenum, and copper prevent population growth through their gated advanced capabilities.
- Oxygen production spreads through tile-local atmosphere.
- Volcanic gas fields reproduce their activity-dependent center/neighbor plateaus, conserve exchange, and cannot sustain 15 equivalent founders in a source-free first-ring tile.
- Speciation preserves ancestor/descendant balances and lineage.
- Sandbox control/locks and survival loss/completion follow mode rules.
- Paired Survival initialization creates equal-sized independent roots, ordinary autonomous competition, and no false lineage parent edge.
- Unknown, reduced, and live tile projections never disclose disallowed state; migration, vacancy, save/load, reconnect, and sandbox control transfer preserve knowledge correctly.
- Spatial outcomes are invariant under derived-index rebuilds, dense-slot reordering, worker count, and non-interacting organism-position permutations; clustered 100,000-organism maps remain within the benchmark budget.
- The spatial rule-pack fixture reproduces the founder-radius, discrete Brownian RMS, analytic uniform-density encounter rates, and 30-day chance-encounter bands in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).
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

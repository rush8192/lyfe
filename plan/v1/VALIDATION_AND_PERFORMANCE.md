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
- Effective-population table lookup, fixed-point mutation income/remainder, price/complexity composition, and DNA modifier provenance.
- Speciation validation, founder rounding/keyed selection, balance duplication, cooldown scheduling, organism-state preservation, and lineage records.
- Autonomous pressure decay/evidence, candidate scoring, persistent-intent invalidation, commit curve, tile selection, and explanation records.
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
- Each ordinary structural-decay extent credits exactly four `LabileDissolvedOrganic(C6 H12 O6)` plus `C76 H122 O16 N20 P2 S1` in generic organic elements, while direct digestion and decay can never debit the same structure.
- Each fermentation extent debits one `LabileDissolvedOrganic(C6 H12 O6)`, credits two `ReserveOrganic(CH2O)` and one `ReducedFermentationProducts(C4 H8 O4)`, stores two of three gross energy units, and dissipates one.
- Each direct respiratory extent consumes `1 LabileDissolvedOrganic + 3 O2`, emits exactly `3` assimilable `CH2O + 3 CO2 + 3 H2O`, exposes 18 energy opportunity, and stores at most `3` new plus `13` recharged reserve. Each reduced-product extent analogously consumes `1 ReducedFermentationProducts + 2 O2`, emits `2 CH2O + 2 CO2 + 2 H2O`, and stores at most `2` new plus `10` recharged reserve from 14 opportunity units.
- Each oxygenic extent consumes exactly `1 CO2 + 1 H2O(boundary)`, credits `1 ReserveOrganic + 1 O2`, stores one of two gross opportunity units, and emits no oxygen when the fixed-carbon output lacks an admitted destination.
- Charged and spent reserve carriers conserve identical `CH2O` matter through spending, recharge, reproduction, death, and decay; newly assimilated carrier matter is exactly debited from respiratory fuel, and only the charged state contributes stored energy and reserve health.
- Coupled external claims allocate whole stoichiometric extents, never strand one input, remain proportional before keyed integer residuals, and are invariant under claim enumeration and worker count.
- Generic organic elements, spent reserve, `SpentStructuralResidue`, and `ReducedFermentationProducts` are invalid fermentation inputs; no spend-decay-ferment cycle restores reserve.
- `LabileDissolvedOrganic` and `ReducedFermentationProducts` reproduce their 14- and 90-day reference half-lives, and no v1 health or reaction rule reads pH or acidity.
- Atomic uptake-and-fermentation may process more than 512 load in one tick only when the excess is consumed by the same committed bundle; retained available-store load never exceeds capacity.
- Organic uptake requests linearly up to its compiled ceiling without reading an undeclared concentration or half-saturation field; actual stock and proportional contention alone reduce grants.
- Without `MetabolicRegulation`, an acquired organic pathway pays full active upkeep even with no substrate. With regulation, phase-5 suppression retains exactly 25% of marked pathway upkeep plus the non-suppressible 5-energy control cost; an emitted claim cannot be retroactively suppressed after losing contention.
- The fully active sulfur heterotrophy bridge adds exactly 30 energy/hour from regulation, generalized catabolism, uptake, and fermentation before inherited-pathway upkeep and stress.
- On the `32 × 17` wrapped-x/bounded-y uniform-aquatic fixture, `0.15%` dissolved exchange plus the 14-day labile loss produces first-, second-, and third-ring ratios within `23.0..24.2%`, `7.6..8.4%`, and `2.6..3.1%`, with RMS spread `1.65..1.77` tiles.
- A continuous one-corpse/day labile source grants a fully draining axial first-ring consumer `37..41` substrate/hour, while every second-ring position remains below the `32.5` substrate/hour fermentation-maintenance threshold.
- A fully wet aquatic/terrestrial edge puts less than 6% of source stock in the first land tile and less than 1% in the next; zero terrestrial moisture produces zero dissolved exchange.
- Dissolved exchange conserves each resource, processes wrapped edges exactly once, never crosses bounded-y edges, preserves edge remainders through save/load, and is invariant under edge order and worker count.
- An ideal uncontested hydrogen-derived fermentation fixture with age throughput fixed at `1.0` converges on 108 successful extents, 216 reserve output, and 151 reserve surplus per organism-hour before other stress and nutrient costs.
- The expected-value lifecycle authoring fixture reaches the first hydrogen-derived fermenter division in `690..696 h`; the former 64-opportunity counterfactual remains below one expected direct offspring over the primitive senescence curve.
- At mature structure `1,000`, particulate ingestion cannot exceed 50 structure-equivalent units per tick, buffered load cannot exceed 250, and digestion cannot exceed ten whole structural extents per hour before other limiters.
- Dormancy never permits growth, reproduction, active movement, predation, or scavenging; resistant dormancy cannot enter below `110%` of mature structure and routes exactly half of structure to the resistant decay cohort on death.
- Before 30 biological days, age does not reduce metabolic extent; at the primitive age-factor floor it caps otherwise admissible extent at exactly 75%, without changing per-extent mass or energy accounting.
- Death transfers all remaining contents exactly once.
- Founder growth never spends reserve below `4,000`; primitive fission cannot commit below `0.60` health or produce a result below `4,000` reserve.
- Reference-condition remnant cohorts reproduce their 7-, 14-, 30-, and 90-day half-lives within fixed-point tolerance, and passive organic-to-inorganic mineralization reproduces its 90-day half-life without changing elemental totals.
- Proportional contention grants no more than the available pool and is independent of input order.
- Trait graphs never accept invalid prerequisites or incompatibilities.
- Mutation income is zero at population zero, monotone in population/health/modifier, matches the pinned effective-population table, and is identical across tick partitioning wherever the configured tick-duration arithmetic is exact.
- Controlled, uncontrolled, and sandbox-locked copies of the same species state receive identical mutation income; locks affect only autonomous decision state.
- Every speciation pays the simple trait-cost sum once, gives both branches the identical post-price balance and income remainder, moves exactly the selected organisms, leaves at least one ancestor, and creates no matter or energy.
- Founder counts are independently `max(1, floor(localPopulation × fraction))` for each selected tile; keyed selection is invariant under dense order, worker count, and save/load.
- Both branches receive the first tick boundary at least 168 hours after speciation as their next eligible speciation tick.
- Autonomous candidate and commit outcomes are invariant under candidate enumeration order and client visibility/subscriptions, while different world seeds can produce exploratory variation.
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
- A 100-producer stable-turnover fixture approaches `285.816` labile production/hour after structural-decay warm-up and supports only about three hydrogen-derived or two-to-three sulfur-bridge fermenters; a producer-free fermenter ecosystem eventually becomes extinct.
- Dissolved-organic specialization splits one scarce 120-unit pool `40/80` against an otherwise equal base claimant, returns to ordinary proportional fairness among equal specialists, and lowers all-specialist fermentation carrying capacity through its added upkeep. It leaves fermentation yield unchanged but lets a supplied proto-eukaryotic direct respirer reach `152` successes and `456` new carrier units/hour.
- At the reference condition, a 14-day-old ordinary remnant retains approximately 25% of dissolved contents, 50% of reserve compounds, and 72% of structure before scavenging; the sequential 30-day structural and 90-day mineralization paths remain conservative.
- The bounded hydrogen fixture reconciles every CHNOPS element against its declared volcanic and water boundaries.
- The volcanic starting fixture supports adapted founders but rapidly harms otherwise identical organisms without sulfur tolerance.
- Missing manganese, molybdenum, and copper prevent population growth through their gated advanced capabilities.
- The reference oxygenic producer resolves exactly `7,772` daily extents, pays `90/hour` canonical upkeep, reaches its expected-value `446.9 h` capital landmark, and remains inside the initial executable `440..500 h` first-division band.
- Predation range, hard-size eligibility, equal-profile `0.50` kill probability, `0.02..0.95` eligible clamp, action debits, simultaneous mutual kills, feeding caps/weights, and remnant conservation reproduce [PREDATION.md](PREDATION.md) under iteration, worker, and save/load permutations.
- Predation ignores controller ownership; same-species attacks reject for every profile, while evolved kin discrimination excludes exactly the configured one- and two-edge relatives without protecting unrelated species.
- Producer-prey-predator fixtures contain low-density predator failure, a directed-hunting viability region, defense counterselection, prey-depletion feedback, and at least one bounded coexistence region without untracked matter or energy.
- The isolated movement fixtures reproduce every locomotion profile's speed, acceleration, heading clamp, upkeep, and distance debit. Mature Scale-I advanced movement costs `108/hour` at `16 R₀/hour`; Scale II costs `243/hour`; boundary segmentation cannot change either debit.
- Environmental anchoring, baseline suspension, and environmental drifting preserve zero mean while producing `0.25x`, `1.0x`, and `1.5x` passive RMS. The uniform 100-organism estimate yields approximately `1.32`, `5.29`, and `7.94` raw boundary intersections/day respectively, and neither passive strategy gains sensed direction or bypasses migration admission.
- A complete Scale-I maximum-search predator reproduces `320/hour` passive cost, `428/hour` including movement, and approximately `33.40` fully recovered primitive-prey equivalents per 30-day replacement. Uniform analytical fixtures place base and first-upgrade viability near `350` and `275` primitive prey respectively before population feedback.
- Scale II rejects primitive prey through the hard size gate, accepts Scale-I prey, and reproduces the approximate `0.431` Scale-I-prey/day replacement target plus the `106..134` uniform-prey analytical range.
- Oxygen production spreads through tile-local atmosphere; the 100-reference-producer central-source fixture approaches `448,579` in its source tile and `297,581` world mean before consumption, while producer/respirer fixtures conserve O2 and lower the plateau through biological demand.
- Volcanic gas fields reproduce their activity-dependent center/neighbor plateaus, conserve exchange, and cannot sustain 15 equivalent founders in a source-free first-ring tile.
- Speciation preserves ancestor/descendant balances and lineage.
- A banked-balance branching fixture cannot speciate either resulting branch during the seven-day refractory period and does not lose income during that period.
- Autonomous pressure fixtures favor—but do not guarantee—matching starvation, thermal, moisture, toxin, and predation responses, retain a reachable expensive goal while saving, and produce a complete rationale record.
- Autonomous material-opportunity fixtures ignore generic organic waste, respond monotonically to the exact named stock and recent flow budget, score every valid founding-tile count, and preserve the same rationale and selection across save/load, iteration order, worker count, and client visibility.
- Sandbox control/locks and survival loss/completion follow mode rules.
- Paired Survival initialization creates equal-sized independent roots, ordinary autonomous competition, and no false lineage parent edge.
- Unknown, reduced, and live tile projections never disclose disallowed state; migration, vacancy, save/load, reconnect, and sandbox control transfer preserve knowledge correctly.
- Spatial outcomes are invariant under derived-index rebuilds, dense-slot reordering, worker count, and non-interacting organism-position permutations; clustered 100,000-organism maps remain within the benchmark budget.
- The spatial rule-pack fixture reproduces the founder-radius, discrete Brownian RMS, analytic uniform-density encounter rates, and 30-day chance-encounter bands in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).
- V1 directional environmental displacement is exactly zero. Active and DNA-scaled Brownian-like passive migrants preserve one organism identity and account state, record distinct movement provenance, and trigger the same occupation-based exploration transition; no dispersal path copies an organism or reveals a destination before arrival.
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

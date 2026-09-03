# Validation, Testing, and Performance

Status: test taxonomy and extensive subsystem invariants defined; benchmark contract, state hash, CI policy, and production scenario implementations remain

Sources: all vision documents, [technology decisions](TECHNOLOGY.md), [rule-pack authoring and compilation](RULE_PACK_AUTHORING_AND_COMPILATION.md), and [moddability](MODDABILITY.md).

# Purpose

Define how v1 proves biological rules, mass balance, deterministic replay, protocol correctness, visual truthfulness, and useful performance.

# Test layers

## Unit tests

- Strict rule parsing, source-mapped diagnostics, typed registries/tombstones, reference resolution, normalization, canonical hashes, and full deterministic phenotype compilation.
- Mod-parameter registry generation, policy/shape/unit checks, expected-base-value checks, canonical disjoint-overlay composition, dependency validation, and duplicate-writer rejection.
- World-pack manifest/profile parsing, closed algorithm/reference validation, world-parameter option bounds, scenario/profile constraint intersection, canonical profile hashing, and complete-record enforcement.
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
- Benefit-timing classification—including construction-dependent `Maturing` proposals—proposal dominance, evolution-goal invalidation, alert hysteresis, and notable-event deduplication.

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
- The energy-storage ladder commissions exactly `25,000`, `50,000`, and `150,000` capacity from `60`, `160`, and `560` conserved storage-structure assignments; mutation grants none. It reproduces the upkeep, zero-income endurance, stable-habitat replacement, and observable-policy seasonal-draw landmarks in [ENERGY_STORAGE.md](ENERGY_STORAGE.md), including a final `130,362` draw against `150,000` capacity. Expanded capacity never changes energy density, reaction yield, throughput, body radius, or movement cost.
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
- The empty mod set is semantically identical to direct official-pack compilation; reordering disjoint mod inputs does not change canonical mod-set or final-mechanics identity, while any changed authoritative value does.
- A save/load cycle retains exact base-pack, mod-package, mod-set, final-mechanics, and certification identities and refuses a missing or mismatched required package rather than silently using a nearby version.
- World-pack file order and installed-package enumeration do not change profile hashes. Any effective profile or selected-option change changes compiled-world identity, even when a sampled seed happens to yield the same visible tiles.
- The same biological/mod rule set, world-pack hash, profile, options, and seed reproduces fixed geography, climate baselines, tile stocks, atmospheric initialization, bounded start repair, and generation diagnostics across save/load and supported worker counts.
- Single-threaded and supported parallel worker counts agree.
- The pinned `Philox4x64-10` implementation matches Random123 known-answer vectors; all authoritative domains use permanent numeric IDs and documented coordinate schemas.
- Keyed results are invariant under call/enumeration order, physical partitioning, worker delay, dense-row compaction, observation, logging, and rejected/failed unrelated work.
- Exact bounded draws, fixed-point Bernoulli thresholds, weighted choices, stable ranks, and per-trial keyed binomials match their scalar golden/reference implementations.
- Each parallel phase covers its logical work exactly once; isolated outputs reduce to the same preflighted mutation plan, creation IDs, materializations, events, and phase journal as the one-worker oracle.
- Pause and real-time speed changes do not alter results.
- Client subscriptions and presentation requests do not alter results.
- Save/reload continuation matches uninterrupted execution.
- Replay identifies intentional divergence.
- Authoritative world hashes and future outcomes are unchanged by the number of projection streams, subscriptions, publication cadence, coalescing, reconnects, or disconnected clients.
- Supported worker counts and dense-slot permutations produce the same canonical logical tick-change journal.
- Boundary command transactions while paused replay with the same world revision and do not advance simulated tick/time.

## Execution and storage tests

- Exactly one `WorldRunner` can hold a working transaction; server, projection, persistence, and phase workers cannot acquire mutable store access.
- A deliberate failure in every tick phase discards private pages and leaves the prior completed root, IDs, counters, ledgers, events, locators, and hash unchanged.
- Retained save/publication handles never observe later mutations, and pooled pages cannot be reused until every lease is released.
- Stable entity IDs are never reused; canonical creation ordering is invariant under worker completion and dense layout.
- Every present row and paged locator round-trips exactly after append, swap removal, death/remnant creation, migration, compaction, and save/load.
- Rebuilding declared structural spatial indexes produces the same queries as an uninterrupted maintained instance; completed gameplay aggregates/materializations load byte-identically and validate rather than being silently rebuilt.
- Slow/failing clients, encoders, metrics sinks, and save destinations do not alter tick scheduling or authoritative results beyond bounded capture cost.
- Lifecycle and shutdown failure injection exposes either the old or new completed transaction, never a partial boundary.
- Dense organism resource handles resolve to the correct stable resource/group, and no two admitted internal processes spend the same free quantum.
- The compiled v1 internal manifest admits exactly the resources reachable through declared acquisition/reaction/transfer routes, and rejects a route without compatible storage.
- If profiling ever justifies a bounded-width organism column, it must prove every rules/scenario maximum, match the uniform-`int64` reference under randomized valid transactions, reject overflow before mutation, and round-trip as the same logical 64-bit quantity.
- Ephemeral tick reservations disappear after their phase, while authoritative binding cohorts preserve physical balances and reduce only free availability.
- Resource-major tile matrices match a scalar reference for sources, sinks, transport, contention, and projection under every configured resource class.
- Phase-specific tile effective views match direct formula evaluation and invalidate on every declared climate, stock, and fixed-access dependency.
- Equivalent domain DNA compiles to identical typed numerical profiles, dense handles, process order, provenance, and hashes; organism ticks never traverse trait definitions.
- Every gameplay-relevant derived field has one registered owner/dependency set; a dirty or generation-mismatched read fails rather than lazily recalculating.
- Stored tile, organism, and species materializations match independent debug oracles, enter completed hashes/diffs/saves as declared, and survive load without silent replacement.
- Reproduction, behavior, mutation income, autonomous evolution, events, explanations, and client projections read the same stored `endHealthQ` rather than evaluating health independently.

## State synchronization tests

- Applying every batch from an authorized snapshot through tick `T` equals a direct authorized projection at `T`.
- Applying consecutive batches and applying their merged batch produce the same normalized client state; generated operation sequences cover create/patch/remove, tile replacement, keyed-history upsert, and ordered-event combinations.
- Duplicate batches are harmless; gaps, wrong bases, wrong streams, incomplete parts, checksum failures, and structural errors fail before the visible cache changes.
- Reduced-to-live replacement supplies a complete live tile, while live-to-reduced atomically evicts current conditions, organisms, remains, and hidden chart points.
- Removal reasons do not turn scope loss into death or disclose a hidden death.
- Encoded-message inspection proves that actor and subscription projection cannot leak hidden current state.
- Save/load begins with clean dirty metadata and produces an equivalent subsequent change stream from the same commands.

## Integration and scenario tests

- Founding species survives a known viable setup.
- The sulfide square fixture reaches its first split at tick 250 with the stated first-tick ledger and dark-period reserve minimum; the generated-climate reference reaches tick `263` and accepted generated starts remain in `250..275`; the hydrogen fixture reaches its first split near tick 334.
- The coupled paired-opening authoring fixture reaches hydrogen's `40 MP` landmark during hours `335..350`, sulfur's `60 MP` landmark and player speciation during hours `480..520`, and its seven-day branch check during hours `648..688`; the eight-key reference is `343 / 496 / 664` respectively.
- The same fixture keeps at least `95%` of each root alive through first reproduction, moves exactly half the local sulfur population at speciation, retains at least 45 members in each sulfur branch through the refractory interval, reaches `700..850` total organisms by day 45, creates remnants through ordinary senescence, and exposes fixed nitrogen as a growth constraint without acute adapted-founder chemical mortality.
- The mirrored opening-strategy authoring matrix reaches all eight player decisions and review boundaries, retains every descendant, constructs rather than grants storage, produces no energy from uptake alone, and makes intent-specific differences visible. Its first four-seed and focused 30-day landmarks are recorded in [OPENING_STRATEGY_VALIDATION.md](OPENING_STRATEGY_VALIDATION.md); production acceptance requires the larger key/generated-map pass described there.
- Across accepted generated starts and longer opening horizons, both founders can reach a meaningful first decision, but population growth and environmental variation expose at least one causally legible limiting pressure rather than settling into indefinite unattended expansion. A no-response control should decline or materially underperform an appropriate adaptation within its authored window. No founder has or suffers predation until the required traits, eligible contact, and ordinary predation rules actually produce it.
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
- The 100-organism, 30-day synthetic clustered spatial fixture keeps roughly `45%..65%` of `0.25x` anchored organisms within the initial `16 R₀` colony radius versus roughly `3%..15%` at baseline, while also showing fewer unique partners and more repeated contact. The executable engine should reproduce the qualitative ordering and remain near the authoring bands in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md) before trait prices are frozen.
- Terrestrial fixtures reject organisms lacking `WetSurfaceColonization`; reproduce the staged active/dormant moisture bands; return activity `1.00`, `0.25`, and `0` at the preferred, hard, and zero-moisture anchors; apply the `0.50` terrestrial passive-medium multiplier exactly once; and demonstrate a wet-surface niche plus a dry interval in which tolerance or dormancy helps without becoming free. Lithology preserves whole-world resource totals while creating local strengths and shortages; geological access returns `0.88`, `0.52`, and `0.20` at moisture `0.85`, `0.40`, and `0`, and never credits matter. Across 64 named reaction keys, the representative paired wet coast derives median `1.20..1.35` gross capture from ordinary light rules without a land scalar, while its clear-water control is approximately `1.15`; the separate `20 m` gas control produces exactly `1.20x` accessible terrestrial CO2 and only affects capture when carbon limits claims. The smooth `0.15..0.85` seasonal isolation fixture keeps the always-active wet phenotype below `2%` annual moisture survival, intermittent tolerance below a `10,000` reserve draw, and the fully costed compartmentalized-storage dormant phenotype at a `130,000..135,000` draw against `150,000` capacity. `MoistureConservation` uses a 24-hour falling-trend warning at `activeHard + 0.05`, exits after a rising-trend recovery another `0.10` higher, and has a 24-hour minimum dwell without reading future weather. Its eight-seed, 128-organism dry-season cohort retains median `121` versus `16` for the current-hard and hindsight comparators, while the no-storage cohort loses all members. Primitive volcanic founders remain below replacement on non-volcanic land, and sterile-land succession admits consumers and predators only after producer-created stocks and prey density exist. Weather remains movement-neutral under v1 configuration.
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
- Both founder choices expose at least two non-dominated, different-intent opening proposals, including one immediate proposal, and preparatory paths expose their complete remaining milestone without claiming active benefit.
- Decision summaries, alerts, automatic pause boundaries, consequence reviews, and canonical notable events replay identically and disclose only actor-authorized evidence.
- Every immediate opening proposal produces a relevant observable difference within its authored consequence window unless its preview identified the realized environmental blocker; the common 168-hour review still emits even where a slower intent-specific landmark remains pending.

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
- Dirty-tracking cost and density, projection materialization/coalescing time, bytes by operation type, per-stream retention memory, client atomic-apply time, normalized-index repair time, PixiJS buffer upload, and React notifications per committed batch.
- Bytes copied per transactional page group, page-pool high-water mark, locator cost, chunk occupancy/slack, tile-partition migration cost, retained-root lease overhead, and tile-partitioned versus global-SoA comparison under uniform and clustered populations.
- Row-major versus tile-chunk resource-major organism balances, dense-core versus synthetic hybrid occupancy, mixed-row phenotype lookup versus derived tile/species cohorts, resource-major versus tile-major stock matrices, and per-organism versus once-per-tile environmental transforms.
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

# Optimization gate

The first vertical slice uses the documented sane defaults and gathers an end-to-end profile before implementing competing physical representations. An optimization proposal must identify a measured limiting resource, reproduce it with a representative world and client workload, report whole-system results rather than only a microbenchmark, preserve a simple reference implementation or oracle, and justify its implementation, testing, migration, and maintenance cost. Smaller fields or messages are not automatically better if they increase tick CPU, copying, allocation, encoding, client-apply cost, or design complexity elsewhere.

Uniform signed-64-bit resource columns, `256`-row chunks, ordinary typed fields, and the initial Protocol Buffer patch shapes remain the baseline until this gate is met. Correctness-driven structure—stable IDs, deterministic barriers, materialized single-source values, bounded queues, and actor-authorized projections—is not optional performance tuning.

# Instrumentation

Every major simulation phase should expose elapsed time and processed counts. Plan debug-only resource reconciliation, per-tile hot-spot reporting, allocation and GC metrics, command queue depth, client queue depth, encoded bandwidth, and deterministic state hashes.

The headless scenario runner must additionally expose stable common outcomes and intent-specific counters without becoming a simulation input. It should restore shared completed-boundary checkpoints for alternate command branches, evaluate named time/event landmarks, and emit deterministic JSON suitable for golden comparisons and balance analysis. Counterfactual branches are development fixtures, not player-visible Survival backtracking.

# Continuous integration

Define CI stages for formatting, static analysis, unit tests, deterministic fixtures, protocol compatibility, client tests, and a bounded benchmark smoke test. Longer soak, replay, and performance suites may run separately but must be repeatable and retain comparable results.

# Technology reconsideration gate

C# is reconsidered only after representative profiling shows that the managed runtime—not an avoidable algorithm, layout, allocation pattern, or serialization choice—is preventing required performance by a meaningful margin. Any Rust or C++ comparison must run the same workload and determinism checks.

# Required artifacts

- [ ] Test taxonomy and project mapping.
- [ ] Canonical small deterministic fixtures.
- [x] Balance-mod validation, composition laws, compatibility identity, and certification boundary; concrete generated test cases remain implementation work. See [MODDABILITY.md](MODDABILITY.md).
- [x] Closed-schema world-pack/profile validation, option, identity, deterministic-generation, and certification boundary; representative external profiles and expanded seed suites remain implementation work. See [MODDABILITY.md](MODDABILITY.md) and [WORLD_AND_CLIMATE.md](WORLD_AND_CLIMATE.md).
- [ ] Resource-conservation oracle.
- [ ] State-hash definition.
- [x] State-change/projection correctness taxonomy and merge-law requirements; concrete property-test generators and performance thresholds remain open. See [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md).
- [x] World ownership/failure and entity identity/storage test contracts; concrete benchmark thresholds remain open. See [WORLD_EXECUTION_AND_OWNERSHIP.md](WORLD_EXECUTION_AND_OWNERSHIP.md) and [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md).
- [ ] Benchmark world/rule pack.
- [ ] Reference hardware and acceptance thresholds.
- [ ] Profiling and metric naming conventions.
- [ ] CI matrix and performance-regression policy.
- [ ] Soak-test and failure-injection plan.

# Survival Opening Validation

Status: first coupled executable viability fixture and provisional acceptance bands; mirrored opening-strategy extension complete

Sources: [gameplay](GAMEPLAY.md), [founding metabolisms](FOUNDING_METABOLISMS.md), [hydrogen founder fixture](ONE_TILE_STARTING_CONFIGURATION.md), [sulfur founder fixture](SULFUR_TILE_STARTING_CONFIGURATION.md), [gas transport](GAS_TRANSPORT_AND_ATTRITION.md), [health calibration](ORGANISM_HEALTH_CALIBRATION.md), [lifecycle and recycling](LIFECYCLE_AND_RECYCLING.md), [evolution](EVOLUTION.md), and [behavior](BEHAVIOR_AND_RESOURCE_PRESSURE.md).

# Purpose

Define one reproducible end-to-end Survival opening that tests whether the independently calibrated systems form a coherent game loop. The scenario is an integration oracle and balance probe, not a replacement for subsystem fixtures or a claim that the production engine has already been implemented.

This fixture establishes that both founders receive a viable foothold; it does not by itself establish the complete challenge curve. Its controlled tiles and short opening intentionally isolate metabolism, reproduction, health, mutation timing, and early decay. Production balance must additionally show, across generated worlds and longer horizons, that environmental variability and population pressure expose understandable limiting factors, while predation remains absent until evolved capabilities and ecological contact make it possible. See the challenge charter in [GAMEPLAY.md](GAMEPLAY.md).

The first pass answers four immediate questions:

1. Do both founding metabolisms survive, gather, grow, and reproduce under one shared tick sequence?
2. Does the sulfide specialist retain a visible opening-speed advantage without invalidating the hydrogen lineage?
3. Does the common health and mutation economy produce the intended first player decision within a few real-world minutes at the fastest provisional speed?
4. Does speciation preserve a viable ancestor and descendant while resource and age pressure begin to create the next ecological problem?

# Canonical setup

The reference command chooses:

- Survival mode and world seed `0x4C594645`.
- The reference sulfide-anoxygenic-phototrophy profile for the player.
- The reference hydrogen-acetogenesis profile for the autonomous competitor.
- Two adjacent activity-`0.80` volcanic ocean tiles selected by the paired-start generator contract.
- `100` mature founders per root species, each with `1,000` structure, `5,000 / 10,000` reserve, its committed founding quotas, zero free additional quota, and zero mutation points.
- One-hour ticks, the generated sulfur daily-light sequence, and `BaselineActivity` for every founder.
- No efficiency-versus-tolerance setup variant. Those packages remain a separate tuning task.

The player's tile and all of its edge-sharing neighbors begin discovered. The player's occupied tile is `Live`; the neighboring competitor tile is only `Reduced`, so its organisms and exact changing state are not exposed. The authoring executable validates simulation state rather than client projections; the production scenario must additionally assert these visibility states.

# Executable authoring model

[survival_opening_validation.py](calibration/survival_opening_validation.py) runs the scenario over eight deterministic keys by default. It includes:

- A `15 × 15` wrapped-`x`, bounded-`y` aquatic gas field with the selected source, attrition, and exchange equations.
- Whole individual organisms with age, reserve, structure, reproduction cooldown, additional-quota progress, species identity, and the acquired regulation flag.
- Hydrogen acetogenesis, sulfide phototrophy under the generated hourly light curve, maintenance, sulfur exposure cost, and whole-unit biomass assembly.
- Primitive `0.5`-opportunity-per-hour additional-quota acquisition.
- Intrinsic death before action, age-limited throughput, deterministic fission with keyed `0..3 h` cooldown jitter, and young offspring.
- Derived health and completed-tick mutation income.
- A phase-boundary player purchase of `MetabolicRegulation` at `60 MP`, deterministic one-tile `50%` founder selection, copied post-price balances, retained organism state, and the `168 h` refractory interval.
- A 45-day diagnostic tail with senescence, cohesive remnant totals, and first-stage exponential decay.

Run it from the repository root:

```text
python3 plan/v1/calibration/survival_opening_validation.py --seeds 8
```

Use `--json` to emit the complete reference state and min/median/max summary.

This program uses floating-point authoring arithmetic and hash-keyed draws. It is not authoritative engine code. It omits generated-map selection and repair, spatial movement, predation and scavenging, per-element remnant reconciliation, detailed autonomous candidate scoring and commitment, optional founder packages, production fixed-point remainders, state hashing, persistence, protocol projections, and parallel reduction. Exact production results may differ inside the accepted bands after those systems replace the authoring approximations.

The separate [opening strategy validation](OPENING_STRATEGY_VALIDATION.md) now extends this baseline with mirrored authority, full-grid passive movement, trait branches, storage commissioning, dissolved organics, fermentation, and intent-specific consequence metrics. This original executable remains the smaller viability regression fixture; the extension does not silently expand its acceptance bands or replace its reaction-level diagnostics.

The canonical run deliberately controls the sulfur founder. Survival setup also permits the hydrogen founder. The first authoring-level mirrored choice matrix is now executable in [OPENING_STRATEGY_VALIDATION.md](OPENING_STRATEGY_VALIDATION.md); the production scenario must reproduce it with the exact rule pack, generated-map state, fixed-point arithmetic, and command/replay contracts rather than silently treating the autonomous competitor's policy as player behavior.

# First result

The eight-key run passes the initial acceptance rules. Milestones are measured as completed simulated hours:

| Milestone | Eight-key result | Provisional band | Fastest-speed reference at `2 ticks/s` |
| --- | ---: | ---: | ---: |
| Sulfur first reproduction | `272` in every key | `250..275` | `2.27 min` |
| Hydrogen first reproduction | `334` in every key | `330..340` | `2.78 min` |
| Hydrogen reaches `40 MP` | `343` in every key | `335..350` | `2.86 min` |
| Sulfur reaches `60 MP` | `496` in every key | `480..520` | `4.13 min` |
| Player speciation commits | `496` in every key | `480..520` | `4.13 min` |
| Seven-day branch check | `664` in every key | `648..688` | `5.53 min` |

The deterministic values are expected in this narrow run: cooldown jitter is not the binding first-reproduction gate, and quota variance has ample time to converge. Later scarcity, death, movement, and behavioral choices should create seeded divergence without moving the opening landmarks outside their bands.

At the seven-day post-speciation check, the reference has `200` hydrogen organisms, `100` sulfur ancestors, and `100` controlled regulated descendants. Both sulfur branches therefore remain viable throughout the refractory interval; speciation has moved matter-bearing organisms rather than copying or healing them.

The 45-day tail produces:

| Diagnostic | Eight-key result |
| --- | ---: |
| Total living population | `753..768`, median `760` |
| Senescence deaths | `28..38`, median `33.5` |
| Acute chemical deaths | `0` |
| Maintenance-failure deaths | `0` |
| Hydrogen-tile NH3 at hour 1,080 | `14` in the reference key |
| Sulfur-tile NH3 at hour 1,080 | `5` in the reference key |

The reference key ends with `386` hydrogen-root organisms, `190` sulfur-root organisms, and `189` regulated sulfur descendants. Its average health values are approximately `0.942`, `0.787`, and `0.777`. The five-energy constitutive regulation cost is therefore visible but does not make the first stepping-stone branch nonviable.

# Resource interpretation

The paired field reproduces the documented no-biology center stocks for H2, H2S, and SO2 before founders are inserted. Because generated-world initialization iterates source, sink, and exchange for every configured gas, local NH3 begins near `2.11 million` in this field rather than the isolated one-tile fixture's deliberately authored `10 million` account. That distinction is intentional:

- The one-tile documents remain reaction and accounting fixtures.
- This paired scenario represents a generated field initialized near environmental equilibrium.
- The world generator must record which initialization contract produced a saved world's stocks.

By day 45, fixed nitrogen is nearly exhausted and structural growth stalls. Adults still capture enough founding fuel to pay maintenance, so this pressure does not imply immediate starvation. Accordingly, the approximately `208` sulfur and `311` hydrogen figures in the founding documents are **full-demand or full-growth ceilings**, not hard population caps or guaranteed survival carrying capacities. Populations may exceed them while growing slowly or not at all. A true long-run carrying capacity depends on maintenance demand, age structure, reproduction gates, migration, and recycling.

The sulfur first split moves from the isolated generated-light estimate of `263` hours to `272` in the paired field. Initial H2S above the adapted soft boundary adds a brief cost before biological demand lowers the stock, while shared gas transport and reserve throttling also alter the trajectory. The result remains inside the existing `250..275` generated-start band, so no reaction yield or tolerance change is warranted from this pass.

# Autonomous competitor expectation

The competitor uses the ordinary uncontrolled-species rules and earns mutation points identically to a controlled species. Reaching the `40 MP` price of `OrganicResourceUptake` is not sufficient reason to acquire that preparatory node: a proposal that does not yet enable useful work has zero raw material opportunity, and the complete fermentation path must be judged against actual labile-organic stock and recent flows.

The authoring program includes only a negative diagnostic for this rule while opening organic opportunity is absent; it deliberately does not substitute a simplified choice for the autonomous-evolution algorithm. The production fixture must run the exact scoring contract from [EVOLUTION.md](EVOLUTION.md) and [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md), record the considered candidates and rationale, and accept either continued saving or a different pressure-supported complete proposal. It must never grant the competitor a hidden discount, free trait, or privileged environmental knowledge.

# Provisional acceptance contract

The canonical production scenario should pass all of these checks across at least 64 named simulation keys and one pinned generated world:

- At least `95%` of each founder population survives until its first reproduction.
- Sulfur reproduces during hours `250..275`; hydrogen reproduces during hours `330..340` in the pinned reference, with a broader generated-world hydrogen allowance retained in world calibration.
- The sulfur/hydrogen first-reproduction ratio remains within `0.65..0.85`.
- Hydrogen reaches `40 MP` during hours `335..350`; the controlled sulfur root reaches `60 MP` during hours `480..520`.
- The player can commit `MetabolicRegulation` at the next command boundary without violating storage, quota, ancestry, or selection invariants.
- One-tile speciation moves exactly `floor(localPopulation × 0.50)` organisms, leaves at least one ancestor, copies the post-price balance and income remainder to both branches, and creates no matter or energy.
- Both sulfur branches retain at least `45` living members through the `168 h` refractory interval, and neither can speciate during it.
- Adapted founders incur no acute chemical deaths in the canonical environment. A separately unadapted control must still suffer the configured sulfur risk.
- By day 45, the scenario has entered a measurable fixed-nitrogen or founding-fuel growth constraint, created remnant matter through ordinary senescence, and retained at least one living root and the controlled descendant.
- Every founder and descendant remains in `BaselineActivity` unless its own local state and acquired DNA permit a transition. Species-wide aggregates affect presentation and autonomous evolution only.
- Repeating the same seed, setup command, player mutation command, speed changes, pause sequence, save/load boundary, and worker-count variants produces the same authoritative state hash.

The authoring fixture uses narrower day-45 bands of `700..850` total organisms and `20..50` senescence deaths to catch accidental parameter drift. Those are provisional regression landmarks, not player-facing promises and not yet catalogue-wide balance constraints.

# Recalibration triggers

Rerun this fixture after changing any of the following:

- Founder reaction yield, light coefficient, assembly ceiling or recipe, maintenance, sulfur tolerance, reserve floor/capacity, or initial endowment.
- Gas source, sink, exchange, accessibility, generated-field initialization, or paired-tile geometry.
- Health composition, age throughput, senescence, quota uptake, reproduction gates/allocation/cost, or cooldown rules.
- Mutation-income normalization, trait price, founder fraction, balance-copy rule, or refractory duration.
- Regulation upkeep, initial behavior, or a setup tolerance/efficiency package.
- Generated-world eligibility and repair bounds.

The next stronger version should replace the authored pair with a real generated seed, use the production fixed-point and keyed-RNG contracts, reconcile the full resource ledger, run exact autonomous scoring, and add persistence/replay and visibility assertions. Those steps refine the oracle; they do not reopen the basic Survival opening unless a band fails for a biological reason rather than an implementation discrepancy.

It must also run both possible player-controlled founding metabolisms. The mirrored hydrogen-player case may use different first-mutation timing and trait expectations, but it receives no environmental, point-income, founder-count, or autonomous-opponent advantage beyond the selected DNA and tile.

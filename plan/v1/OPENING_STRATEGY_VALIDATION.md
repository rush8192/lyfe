# Opening Strategy Validation

Status: first executable mirrored-choice matrix; four-seed authoring baseline and focused 30-day follow-ups complete

Sources: [player loop and narrative](PLAYER_LOOP_AND_NARRATIVE.md), [Survival opening](SURVIVAL_OPENING_VALIDATION.md), [evolution](EVOLUTION.md), [spatial calibration](SPATIAL_CALIBRATION.md), [energy storage](ENERGY_STORAGE.md), and [organic uptake and fermentation](ORGANIC_UPTAKE_AND_FERMENTATION.md).

# Purpose

Test whether the first affordable evolution options create distinguishable strategies for either player-controlled founder. This extends the viability-focused Survival fixture into a headless scenario matrix and establishes interfaces that a future engine-side balance runner will need.

The fixture is an executable design oracle, not production engine code. Its results decide which hypotheses deserve implementation and deeper simulation; they do not freeze final balance from four seeds.

# Executable

[opening_strategy_validation.py](calibration/opening_strategy_validation.py) reuses the paired gas-field, health, lifecycle, reproduction, and mutation calibration from [survival_opening_validation.py](calibration/survival_opening_validation.py). It adds:

- scenario-defined player metabolism and trait proposal;
- full-grid organism tile and local coordinates;
- trait-scaled Brownian movement, one-edge migration, and keyed admission;
- tile-local gas capture, growth resources, remains, labile dissolved organics, and reduced fermentation products;
- physical `ReserveCapacityI` storage construction, commissioning, health dilution, and fission provisioning;
- `OrganicResourceUptake` as a non-energy-yielding preparatory capability and the complete hydrogen fermentation bundle;
- deterministic one-tile founder selection and copied post-price balance;
- separate ancestor, descendant, and competitor counters;
- a configurable post-speciation observation window; and
- JSON summaries plus a deterministic result digest.

Run the first matrix from the repository root:

```text
python3 plan/v1/calibration/opening_strategy_validation.py --seeds 4
```

Machine-readable output is available with `--json`. A single case can use `--scenario`, and slower consequences can use `--review-hours` with a sufficient `--horizon-days`. `--verify-repeat` reruns the selected cases and compares their complete result records.

# Scenario matrix

Every proposal commits at the first completed boundary at which the controlled root can afford it. Choices of the same metabolism and price use the same founder-selection key, so their selected cohort does not change merely because the trait name changed.

| Player founder | Proposal | MP / complexity | Expected timing | Intended comparison |
| --- | --- | ---: | --- | --- |
| Hydrogen | `EnvironmentalAnchoring` | `40 / 1` | Immediate | Retain the productive founding niche |
| Hydrogen | `OrganicResourceUptake` | `40 / 1` | Preparatory | Pay toward fermentation; no energy without a complete reaction |
| Hydrogen | `ReserveCapacityI` | `40 / 1` | Maturing | Construct buffering capacity and bear dilution/provisioning cost |
| Hydrogen | Uptake + `Fermentation` | `120 / 3` | Conditional | Consume mortality-derived LDO if and when it appears |
| Sulfur | `EnvironmentalAnchoring` | `40 / 1` | Immediate | Remain near localized light and sulfide |
| Sulfur | `EnvironmentalDrifting` | `60 / 1` | Immediate | Trade niche retention for wider chance dispersal |
| Sulfur | `MetabolicRegulation` | `60 / 1` | Preparatory | Begin the longer route away from sulfide specialization |
| Sulfur | `ReserveCapacityI` | `40 / 1` | Maturing | Buffer the daily dark interval while paying construction cost |

`Maturing` is a distinct player-facing classification discovered by this pass. The DNA changes immediately, but its advertised physical capability must be constructed from conserved matter over subsequent ticks. Treating it as `Immediate`, `Conditional`, or merely `Preparatory` would obscure the actual consequence.

# Model order and comparison method

Each one-hour tick uses the established order at authoring-fixture fidelity:

1. Apply an accepted player speciation at the boundary.
2. Advance gas sources, sinks, exchange, dissolved decay/exchange, and geological phosphorus sources.
3. Evaluate intrinsic death risks.
4. Apply passive local movement and at most one edge-crossing attempt.
5. Resolve external founding-path capture and complete fermentation claims.
6. Pay maintenance and assign whole structural growth.
7. Acquire the opening micronutrient quota abstraction.
8. Reproduce deterministically when every gate passes.
9. Aggregate health and accrue mutation income.
10. Capture the consequence snapshot at the configured boundary.

The descendant is compared with its unchanged ancestor inside the same world. Raw populations are not directly comparable because the selected tile can already have migrants and founder rounding. The principal population measure is therefore:

```text
relativeGrowth = (descendantReviewPopulation / descendantFounderCount)
               / (ancestorReviewPopulation / ancestorPostSplitPopulation)
```

The fixture also records health and reserve-fraction deltas, occupied tiles, successful and failed migrations, births, deaths, capture, fermentation, storage construction, commissioned capacity, trait-active organism-hours, and useful trait events. No single number is declared “fitness”; the relevant evidence depends on the proposal's stated intent.

# First 168-hour results

The following values are medians across four deterministic seeds. Deltas compare the descendant with its unchanged ancestor at the review boundary.

| Scenario | Commit hour | Relative growth | Health delta | Reserve-fraction delta | Descendant migrations | Fermentation extents | Storage constructed |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Hydrogen anchoring | `343` | `1.000` | `+0.001` | `+0.001` | `0` | `0` | `0` |
| Hydrogen organic uptake | `343` | `1.000` | `+0.001` | `+0.001` | `2` | `0` | `0` |
| Hydrogen reserve I | `343` | `1.000` | `-0.079` | `-0.080` | `2` | `0` | `7,993.5` |
| Hydrogen fermentation bundle | `761.5` | `1.034` | approximately `0` | `+0.001` | `2.5` | `3,897` | `0` |
| Sulfur anchoring | `387.5` | `0.985` | `+0.008` | `+0.008` | `0` | `0` | `0` |
| Sulfur drifting | `497.5` | `0.954` | `+0.017` | `+0.018` | `1` | `0` | `0` |
| Sulfur regulation | `497.5` | `0.954` | `+0.016` | `+0.017` | `2` | `0` | `0` |
| Sulfur reserve I | `387.5` | `0.985` | `+0.079` | `+0.080` | `3` | `0` | `9,232.5` |

All descendants survive their first comparison window. Equal or near-equal population growth is not evidence of no effect: no branch reproduces during some early 168-hour windows, while movement, storage construction, and reserve condition already diverge.

The 40-MP sulfur options become affordable near hour `387.5`, more than 100 hours before the former 60-MP regulation-only script. This improves the first-decision cadence from roughly `4.1` to `3.2` real minutes at the provisional two ticks/second without changing mutation income.

# Focused 30-day results

Movement and storage strategies have slower consequences, so four-seed cases were rerun for 720 hours after speciation:

| Scenario | Relative growth | Health delta | Reserve delta | Descendant migrations | Interpretation |
| --- | ---: | ---: | ---: | ---: | --- |
| Hydrogen anchoring | `0.990` | `+0.002` | `+0.003` | `0.5` | Strongly retains the founding niche; almost eliminates passive migration |
| Hydrogen organic uptake | `0.999` | `+0.002` | approximately `0` | `12.5` | Uptake remains biologically unused; baseline spread continues |
| Hydrogen reserve I | `0.756` | `+0.001` | `+0.007` | `9.5` | Capacity matures, but reproduction provisioning is costly and continuous hydrogen capture supplies little current need for it |
| Sulfur anchoring | `0.999` | `+0.016` | `+0.019` | `2.5` | A viable resident specialist with much lower spread |
| Sulfur reserve I | `0.994` | `+0.024` | `+0.029` | `12.0` | Buffers daily dark periods while retaining baseline dispersal and near-neutral growth |
| Sulfur drifting | `0.979` | `+0.010` | `+0.012` | `18.5` | Produces substantially more migration, with a modest current-niche cost |
| Sulfur regulation | `0.979` | `+0.013` | `+0.022` | `10.5` | Pays a preparatory cost and retains baseline spread; no new useful reaction yet |

The first useful movement signal is clearer over 30 days than 168 hours. Consequence review should therefore keep the universal seven-day branch summary but retain proposal-specific follow-up landmarks. The player should not have to stare at a branch continuously; the chronicle can surface the later comparison when its authored window closes.

# Gameplay conclusions

## Keep the existing families for the next pass

This fixture does not justify adding another opening subsystem. It supports a coherent initial menu:

- Sulfur can choose an anchored resident specialist, a dark-period storage buffer, early risky dispersal, or preparatory regulation.
- Hydrogen can anchor to the stable volcanic source or invest toward mortality-supported organic metabolism. Storage remains available in the complete tree but should not be promoted as a favorable current-state suggestion without evidence of surplus followed by shortage.

The server's non-dominated frontier must therefore be contextual. A trait can remain legal while being absent from suggestions because its opportunity evidence is poor. The manual tree still permits speculative choices.

## Preserve honest preparation

`OrganicResourceUptake` alone performs zero useful uptake events and zero fermentation in every run. Its five-unit upkeep is often masked by abundant founding capture, which makes clear labeling more important, not less. It is an investment in a route, not an alternative energy source.

`MetabolicRegulation` similarly adds no useful suppression while the sulfur founding pathway remains continuously selected. Its current cost can be small enough to hide in reserve averages. Proposal preview must state the zero current capability gain and the complete path it opens.

## Fermentation has a promising emergent opening

At the approximately 120-MP hydrogen decision, world LDO stock is still zero in the reference case. Ordinary senescence then creates remains, structural decay creates LDO, and the new branch completes roughly `3,897` fermentation extents during the next seven days. Median relative growth reaches `1.034` despite pathway upkeep.

This is desirable causal structure: evolution becomes useful because the ecosystem has begun producing a niche, not because the purchase spawns its substrate. The UI can show the proposal as conditional at commit and record its later first activation. Autonomous opportunity must continue using observed stock and trailing flow rather than hidden knowledge of future deaths.

## Use intent-specific evidence

A seven-day population result alone would incorrectly call several choices identical. Initial consequence views should prioritize:

- migrations and occupied tiles for spread traits;
- commissioned capacity, storage construction, reserve condition, and reproduction suppression for storage;
- actual claims/reactions and resource provenance for metabolism;
- suppressed cost and selected pathways for regulation; and
- population, health, births, and deaths as common outcomes.

# Engine-facing guidance

The authoring executable suggests these concrete engine seams:

```text
ScenarioDefinition
    -> immutable RulePack + generated WorldSnapshot + ordered ActorCommands

SimulationRunner
    -> phase systems operating on WorldState
    -> ResourceClaim/Transaction resolver
    -> keyed RandomOracle(domain, tick, stable IDs, ordinal)
    -> EventSink + MetricsSink

ScenarioBatchRunner
    -> restore shared boundary checkpoint
    -> apply alternate proposal command
    -> run to authored observation landmarks
    -> emit stable JSON summary + state/event hashes
```

Recommended implementation consequences are:

- Treat headless scenario execution as a first-class engine entry point, not a test that drives the browser.
- Separate immutable generated initialization from mutable world state so many seeded branches can share setup work safely.
- Support restoring the same completed-boundary checkpoint and applying alternate commands for developer counterfactuals. This is testing infrastructure, not Survival backtracking.
- Compile trait effects into typed fields consumed by owning systems; scenario code should not grow a permanent chain of trait-name conditionals.
- Give every random decision a stable domain and key. Scenario enumeration, metrics, subscriptions, and worker count must not perturb it.
- Publish completed-tick observations to side-effect-free metrics and event sinks. Adding a balance measurement must not change simulation order or state.
- Allow authored observation landmarks by elapsed time, first activation, threshold crossing, or lineage event. Different strategies become legible over different horizons.
- Emit both common outcomes and intent-specific counters. A universal scalar fitness score is insufficient for player explanation or balance work.
- Batch scenarios with bounded parallelism and deterministic reduction. The Python fixture demonstrates why repeatedly advancing near-identical worlds serially will become expensive.
- Keep golden fixture summaries separate from production save schemas; both should consume the same authoritative state and event definitions.

# Known authoring limitations

The extension is deliberately stronger than the first opening script but still omits or simplifies:

- production fixed-point arithmetic, remainders, state hashing, dense storage, and parallel resolution;
- a generated map's depth, climate, tile compatibility, micronutrient composition, and exploration projection;
- active movement, behavior selection, predation, scavenging, and direct remnant consumption;
- exact CHNOPS transfers for every remnant and structural transaction;
- exact autonomous-evolution proposal scoring and commitment;
- atmospheric-access depth effects and localized terrestrial conditions;
- joint proportional resolution when hydrogen and sulfur migrants request different coupled reactions from the same tile; the current authoring pass resolves the two founding reactions in stable metabolism order; and
- enough seeds to freeze win rates or declare a catalogue-wide dominant strategy.

These omissions must remain visible in conclusions. In particular, four-seed migration medians establish a useful signal, not final probability calibration.

# Acceptance and next extensions

The current executable passes its first contract:

1. Every scenario reaches an affordable decision and complete review within the configured horizon.
2. Every descendant survives the first 168-hour window.
3. Same-price alternatives reuse founder selection rather than introducing choice-key cohort noise.
4. Reserve mutation constructs matter and capacity rather than granting either.
5. Uptake alone produces no energy; fermentation consumes only decay-created LDO.
6. Passive movement crosses at most one edge in a tick and records migrations separately.
7. Repeating the reference fermentation scenario produces an identical complete result record and digest.

Before freezing opening balance, extend this fixture to at least 64 keys and one real generated map, replace ordered cross-metabolism capture with the production coupled-claim resolver, and replay alternate commands from a shared serialized checkpoint. The next immediate calibration task is the bounded founder efficiency-versus-tolerance packages, evaluated across this same mirrored matrix rather than in isolated one-tile tests.

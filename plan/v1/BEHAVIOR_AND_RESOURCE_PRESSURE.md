# Behavior and Resource-Pressure Regulation

Status: first v1 general behavior, resource-pressure, persistence, reproduction-readiness, and DNA-extension contract; numerical population calibration pending

Sources: [organism mechanics](ORGANISMS.md), [simulation loop](SIMULATION_LOOP.md), [organism state and health](ORGANISM_STATE_AND_HEALTH.md), [spatial organisms and behavior](SPATIAL_ORGANISMS_AND_BEHAVIOR.md), [lifecycle and recycling](LIFECYCLE_AND_RECYCLING.md), [predation](PREDATION.md), [terrestrial adaptation](TERRESTRIAL_ADAPTATION.md), and [trait catalogue](TRAIT_CATALOGUE.md).

# Purpose

Define how an organism reacts to its own resource experience and capability-limited observations without giving primitive life global knowledge, future knowledge, or human-like planning. The controller should turn resource contention into individual pressure, preserve deterministic replay, keep decisions explainable, and give evolved regulation a real survival benefit without replacing the underlying resource economy with an artificial population cap.

# Core model

V1 uses a **local, reactive, homeostatic controller**.

- Resource scarcity remains a consequence of finite stocks, sources, sinks, biological consumption, and contention. Behavior does not impose a carrying capacity or population-wide reproduction multiplier.
- An organism may react only to its own concrete state, its recent completed experience, and information exposed by its compiled senses.
- Mandatory physiology is not optional behavior. Intrinsic death, mandatory upkeep, admitted metabolic obligations, aging, and hard lifecycle restrictions always resolve in their owning phases.
- Basic organisms receive hard affordability and viability protections. Evolved behavioral traits provide earlier, more selective, and more persistent responses rather than creating the first ability to avoid an invalid transaction.
- Behavior selected at phase 9 of tick `T` controls optional movement, targeting, growth permission, and reproduction permission during tick `T + 1`.
- Behavior may prevent optional expenditure, but it cannot undo an earlier death, movement, claim, reaction, lifecycle transition, or reproduction transaction.

The foundation trait is named `BaselineActivity`. Stochasticity belongs in probabilistic biological outcomes, target sampling, and near-equivalent behavior choices; a founder does not randomly decline necessary physiology. Persistent slow-growth or quiescent phenotype switching remains an evolved future strategy.

# Behavior state and directives

Every organism retains one dominant behavior and an optional typed target. The behavior compiles into next-tick directives rather than directly mutating resources:

```text
BehaviorState:
    behavior_id                 # Baseline | Conserving | Foraging | Dispersing | Fleeing
    optional_target             # EntityId | EdgeId | LocalPoint; type must match behavior
    selected_at_tick
    minimum_dwell_until_tick

BehaviorDirectives:             # derived from state plus compiled DNA
    optional_growth_permitted
    reproduction_permitted
    active_movement_policy
    targeted_action_policy
    metabolic_coordination_profile
```

`Dormant` remains a lifecycle phase, not a behavior value. A behavior policy may request an eligible transition into or out of dormancy; lifecycle resolution owns the transaction, cost, phase state, and hard admission checks.

The first dominant behaviors are:

| Behavior | First v1 role |
| --- | --- |
| `Baseline` | Ordinary capture, metabolism, DNA-permitted optional growth, and reproduction under the existing physiological gates |
| `Conserving` | Suppress avoidable expenditure while useful passive acquisition and profitable metabolism continue |
| `Foraging(target)` | Pursue a sensed remnant, prey organism, or later localized resource with positive estimated return |
| `Dispersing(edge)` | Move toward a selected neighboring tile when the organism can sense or otherwise select that edge |
| `Fleeing(threat)` | Use the existing threat-response policy to move away from an observed predator |

`Hunting` and `Scavenging` are typed foraging policies. Their target eligibility, observable inputs, and action rules remain owned by [PREDATION.md](PREDATION.md) and [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md). A well-mixed tile resource is never a within-tile spatial target in v1.

# Baseline safeguards

All organisms receive these safeguards without an evolved behavior trait:

- Active movement is capped by affordable realized distance.
- Optional biomass assembly obeys the compiled growth-protection floor.
- Reproduction must prove both resulting organisms satisfy the reproduction profile's hard minima before committing.
- Claims cannot exceed useful demand, destination capacity, or enabled processing opportunity.
- A pathway already selected as suppressed by `MetabolicRegulation` pays its configured suppressed upkeep. Lack of admitted work does not suppress a pathway by itself: without regulation, acquired pathways retain active upkeep, and an emitted intent remains active for the tick even if contention grants nothing.
- Lifecycle and habitat hard gates cannot be overridden by behavior.

`Baseline` otherwise remains opportunistic. If reproduction is eligible and no evolved behavior suppresses it, it commits deterministically. This leaves a meaningful benefit for evolved regulation: an unregulated population may exploit a boom rapidly and then experience a deeper shortage, while a regulated lineage may sacrifice short-term growth for a greater probability of surviving a bust.

# Observable pressure inputs

Resource-pressure regulation uses concrete channels rather than health alone. Health may be low because of age or environmental stress that conserving food cannot repair, so each cause remains separately visible.

```text
PressureObservation:
    reserve_fraction_q
    recent_energy_coverage_q
    limiting_material_deficit_q
    recent_acquisition_coverage_q
    environmental_severity_by_channel_q
```

The channels mean:

- `reserve_fraction_q`: stored usable energy divided by currently commissioned capacity.
- `recent_energy_coverage_q`: recent usable energy credited before maintenance divided by recent mandatory energy cost. It uses a coverage encoding from `0` through `2.0`; `1.0` means recent income covered mandatory cost and `2.0` is a display and arithmetic clamp, not a production bonus.
- `limiting_material_deficit_q`: the greatest normalized shortage among resources currently needed for the next DNA-permitted growth, maturation, or reproduction transaction.
- `recent_acquisition_coverage_q`: actual useful grants divided by useful demand calculated before tile availability and contention. A required but absent resource therefore produces demand with zero coverage rather than disappearing from the signal. A tick with no useful demand contributes neutral coverage `1.0`.
- `environmental_severity_by_channel_q`: one minus each already-derived current temperature, moisture, and chemical condition factor. These do not enter the resource composite because the appropriate response may be tolerance, escape, dormancy, or death rather than conservation.

Useful demand respects enabled capabilities, current capacity, desired inventory, and process need, but ignores current tile stock and competing organisms when establishing the denominator. It must not request or infer a resource that the DNA cannot use.

# Recent-experience memory

Any compiled behavior that uses history owns only the compact channels it needs. The first resource controller uses one fixed-point exponential moving average for energy coverage and one for acquisition coverage:

```text
UpdateOneDayEma(previousQ, currentSampleQ):
    return RoundHalfUp((23 * previousQ + currentSampleQ) / 24)
```

At the one-hour tick this is a gameplay-scale, approximately day-long memory. It is not a claim that a specific biochemical signal has a literal 24-hour response time. A different configured tick duration must compile coefficients that preserve the same simulated-time response and requires behavior-fixture reruns.

New founders and newborns initialize both coverage values to neutral `1.0`; their concrete reserve and inventory state still affect the first decision. An organism retains its memory when it changes species during speciation because it is the same organism. When a mutation first activates a history-using behavior, missing channels initialize to the current completed-tick sample rather than receiving invented pre-trait knowledge.

The composite value used for UI summaries and general conservation scoring is:

```text
reservePressureQ = 1 - reserveFractionQ
energyShortfallQ = max(0, 1 - recentEnergyCoverageQ)
acquisitionFailureQ = 1 - recentAcquisitionCoverageQ

resourcePressureQ = max(
    reservePressureQ,
    energyShortfallQ,
    limitingMaterialDeficitQ * acquisitionFailureQ)
```

Products use the shared fixed-point multiply and named rounding. Behavior retains the component values; the maximum does not erase the explanation of which shortage dominated.

# Conservation policy

`StateGatedActivity` can gate optional actions from current reserve, concrete inventory deficits, and current derived condition. Its child `ResourceConservation` adds recent-experience memory, hysteresis, and the persistent `Conserving` behavior.

The first `ResourceConservation` thresholds are:

```text
enter Conserving when:
    reserveFractionQ <= 0.50
    OR (
        reserveFractionQ < 0.80
        AND recentEnergyCoverageQ < 0.85
    )

critical conservation when:
    reserveFractionQ <= 0.20

leave Conserving when:
    reserveFractionQ >= 0.65
    AND (
        recentEnergyCoverageQ >= 1.00
        OR reserveFractionQ >= 0.85
    )
```

All comparisons are inclusive where written. Entry and exit use different thresholds so one marginal tick cannot cause oscillation.

In ordinary conservation:

- Optional structural growth and reproduction are disabled.
- Undirected paid movement and discretionary dispersal are disabled.
- Passive uptake, environmental energy capture, already-free Brownian movement, and useful profitable internal metabolism remain enabled.
- Fleeing remains enabled.
- A sensed scavenging or predation opportunity may be admitted when its estimated return is positive and its worst-case immediate cost leaves reserve above the critical `0.20` floor.
- Costly active transport, targeted pursuit, or suppressible pathway activation with no admitted useful work is disabled.

Critical conservation applies the same rules but admits no optional paid action except immediate fleeing, an affordable lifecycle transition, or a positive-return feeding action that is the organism's compiled acquisition route. Behavior never suppresses mandatory maintenance, and conservation cannot promise survival when no viable resource route remains.

The initial general behavior dwell is four simulated hours. A behavior selected at tick `T` is normally retained until `minimum_dwell_until_tick`; immediate threat response, hard target invalidation, and an admitted emergency dormancy policy may interrupt it. The moisture-specific dormancy policy retains its independently calibrated 24-hour lifecycle dwell.

# Reproduction readiness

Reproduction remains deterministic when every active gate passes. `ReproductionReadiness` adds a behavior gate; it does not add a success roll, reduce reproductive work, or change cooldown jitter.

In addition to the reproduction profile's existing hard gates, the first readiness policy requires:

```text
recentEnergyCoverageQ >= 1.05

for projected continuing parent and offspring:
    projectedReserve >= max(
        reproductionProfile.absoluteResultReserveMinimum,
        48 h * projectedCurrentEnvironmentMandatoryCostPerHour)
```

The projection uses current completed-tick conditions, each result's projected lifecycle phase and compiled DNA, and no weather or resource forecast. If the gate fails, reproduction simply remains pending; no energy is spent, no cooldown is rerolled, and the organism may become eligible on a later tick. The ordinary profile minima still dominate when they exceed the 48-hour runway.

This trait is useful only when avoiding fragile births repays its regulatory upkeep. It can reduce short-term population and mutation income, so it is not a universal upgrade.

# Cross-behavior selection

Phase 9 evaluates candidate behaviors in fixed priority bands:

```text
Emergency:
    Fleeing(valid observed threat)
    eligible protective lifecycle-transition request

Acquisition:
    Foraging(valid target with positive estimated return)

Conservation:
    Conserving(pressure policy requests entry or retention)

Opportunity:
    Dispersing(eligible selected edge)

Fallback:
    Baseline
```

An emergency candidate may interrupt ordinary dwell. Otherwise the controller evaluates only the highest nonempty band containing an eligible candidate. Within that band:

1. Calculate candidate utility from capability-exposed inputs using fixed-point arithmetic.
2. Apply a `1.20` persistence multiplier to the current behavior when it remains valid.
3. Retain candidates whose utility is at least `0.80` of the best utility.
4. Select among those candidates with one keyed utility-weighted draw in canonical candidate order.

If exactly one candidate remains, no draw is needed. The bounded near-equivalent sample creates individual variation without allowing a poor low-utility choice to override a clear survival response. Hunting retains its more specific `1.25` current-target multiplier inside the selected foraging behavior; the two multipliers affect different decisions and do not compound on the same comparison.

```text
EvaluateBehaviorForNextTick(organism, completedView, tick):
    observation = BuildCapabilityFilteredObservation(organism, completedView)
    pressure = UpdateAndDerivePressure(organism, observation)

    candidates = BuildEligibleBehaviorCandidates(
        organism,
        observation,
        pressure,
        compiledDna)

    if current behavior has not completed ordinary dwell
       and no emergency candidate exists
       and current behavior remains valid:
        return RetainCurrentBehaviorWithUpdatedDirectives()

    band = highest priority nonempty candidate band
    scored = ScoreCandidatesInCanonicalOrder(band)
    selected = KeyedNearEquivalentChoice(
        scored,
        address = RandomAddress(BehaviorChoice,
                                tick, organism.id, 0,
                                sampleIndex = 0))

    target = SelectTypedTargetIfRequired(selected, observation)
    return BehaviorUpdate(selected, target, tick, tick + 4 h)
```

Behavior selection cannot depend on dense-array order, spatial-bin iteration order, worker scheduling, client visibility, or whether a human is watching the organism.

# Movement, sensing, and dispersal

- Founders without active locomotion continue to spread only through reproduction and DNA-scaled Brownian movement.
- Active movement requires a locomotion capability. Direction toward an entity requires a matching entity sense and behavior; direction toward an edge requires an exposed neighboring-tile or dispersal policy.
- A tile-wide resource shortage may increase conservation or dispersal utility, but cannot create an invented direction within a well-mixed tile.
- Without directional environmental sensing, a disperser may retain a keyed heading or edge choice, but cannot rank hidden neighboring conditions.
- With an eligible sense, edge utility may use only the coarse or exact signals that sense exposes. It cannot read actor discovery state, server-only future conditions, or another simulation trajectory.
- Target invalidation and next-tick pursuit retain the spatial plan's stable typed-ID semantics.

Resource pressure by itself never grants organism-density knowledge. Population pressure normally appears as lower individual acquisition coverage. A future `QuorumResponse` trait may expose a costly density-related signal and use it to regulate reproduction or dispersal; no founder receives exact tile population as a free behavior input.

# Trait-family contract

The first tree is:

```text
BaselineActivity                                        Foundation
├── StateGatedActivity                                  Early
│   ├── ResourceConservation                            Early/Middle
│   │   └── StochasticQuiescence                        Future placeholder
│   ├── ReproductionReadiness                           Middle
│   └── StressAvoidance                                 Middle
├── DirectedForaging                                    Middle
│   ├── ScavengingBehavior                              Middle
│   └── HuntingBehavior                                 Late
├── MetabolicBehaviorCoordination                       Middle/Late
└── QuorumResponse                                      Future placeholder
```

- `BaselineActivity` supplies ordinary unregulated activity and the behavior interface, not a random survival handicap.
- `StateGatedActivity` reads current internal state and may gate optional actions, but does not receive history, density, or unsensed environment data for free.
- `ResourceConservation` owns the first recent-pressure memory and conservation thresholds.
- `ReproductionReadiness` is a sibling of `ResourceConservation` under `StateGatedActivity`. Either trait may compile the energy-coverage memory it needs, so reproductive regulation does not force the full conservation policy.
- `StressAvoidance` reads named current stress channels and only those neighboring conditions exposed by compatible senses.
- `DirectedForaging` requires a matching target sense and active locomotion before it can pursue; opportunistic contact actions remain possible without pursuit.
- `MetabolicBehaviorCoordination` may change internal process priority or holdback profiles in response to exposed need. It does not grant substrate, throughput, storage, or reaction yield and does not replace `MetabolicRegulation` or `ResourceAllocationControl`.
- `StochasticQuiescence` and `QuorumResponse` are non-selectable v1 placeholders. They reserve future bet-hedging and density-sensitive strategies without silently granting them to current species.

Growth-control ownership remains separate: `RegulatedMaturation` chooses a fixed growth-protection floor, `ResourceConditionalGrowth` selects its existing reserve-dependent floor profile, and `BiomassAssemblyControl` selects allocation bias. General behavior may permit or suppress growth but does not introduce a second competing growth-floor value.

# Persistence and derived state

The selected behavior, typed target, `selected_at_tick`, dwell boundary, and any active behavior-memory EMA values are authoritative. They persist through save/load and participate in state hashing. Utility scores, composite pressure, directives, observations, and candidate lists are derived phase-local data unless retained explicitly as diagnostics.

Coverage samples are finalized from the completed tick before phase-9 selection:

- Energy income counts usable energy credited before mandatory maintenance, including same-tick external capture and internal catabolism, but not storage capacity or energy that could not be admitted.
- Mandatory cost counts actual current-tick baseline, passive trait, environmental-stress, and lifecycle upkeep. Optional movement, attacks, growth, and reproduction remain separately observable costs and do not alter the denominator after the behavior choice they resulted from.
- Useful acquisition demand and grants retain enough per-resource explanation to identify the limiting deficit; the organism need not persist a per-resource history ring.

Newborns receive `Baseline`, no target, neutral `1.0` coverage memory when their DNA uses it, and a dwell boundary that permits their first ordinary phase-9 evaluation on the following tick. They cannot act or select another behavior during their birth tick. Speciation preserves valid behavior state and pressure memory. If descendant DNA removes or invalidates a behavior, the speciation transaction replaces it with `Baseline`, clears the target, and preserves only memory channels still used by the compiled descendant. A newly activated history channel initializes from the most recent completed-tick sample as specified above.
# Observability

For a live organism the server should be able to explain:

- Current behavior, target, selection tick, remaining dwell, and next-tick directives.
- Reserve fraction, recent energy coverage, limiting material deficit, acquisition coverage, and composite resource pressure.
- The entry, retention, or exit rule that selected conservation.
- Candidate behaviors, capability or sensing gates, priority band, utilities, persistence modifier, keyed choice when one occurred, and rejection reasons.
- Whether optional growth or reproduction is suppressed by behavior versus a physiological transaction gate.
- For reproduction readiness, recent coverage and the projected reserve/runway of both results.

The completed phase-9 organism states also produce behavior distributions:

```text
BehaviorDistribution:
    observed_at_tick
    species_id
    optional_tile_id          # present for a per-tile distribution
    total_observed_organisms
    count_by_behavior_id[]
    fraction_by_behavior_id[] # derived from the counts
```

- Phase 10 derives an exact per-species/per-tile distribution by counting living organisms in canonical tile, species, and behavior order. Exact species-wide distributions are sums of the tile counts, not independently sampled estimates.
- Counts are canonical; fractions are presentation values derived with named rounding and must sum to `1.0` after a deterministic largest-remainder correction when the population is nonzero.
- These aggregates are one-way observations. They never enter `PressureObservation`, phase-9 candidate scoring, or any later organism decision. A future `QuorumResponse` must use its explicitly modeled organism-observable signal, not this server aggregate.
- A live tile exposes its current per-species distributions. The controlled species may expose its exact current world-wide distribution because every tile it occupies is live to that controller.
- For another species, a multi-tile summary includes only organisms visible in the actor's current live tiles and is labeled **observed distribution**, never an authoritative global percentage. A reduced tile may retain its last observed distribution and timestamp; it does not continue updating. An unknown tile exposes none.

Client knowledge limits still apply. Reduced or unknown tiles do not reveal live organism behavior or current pressures, and explanation or aggregation requests never consume randomness or alter a decision.

# Validation

The first executable fixtures must prove:

- An abundant stable founder population behaves like the existing unregulated fixtures and does not enter conservation merely because its reserve is full and capture is capacity-limited.
- A declining resource supply produces lower acquisition coverage, then lower reserve or energy coverage, and eventually conservation without consulting global population.
- The `0.50/0.65` hysteresis and four-hour dwell prevent hour-to-hour flapping around one threshold.
- Conservation never disables free useful passive acquisition, mandatory metabolism, or maintenance.
- Critical conservation admits only the declared emergency and positive-return acquisition exceptions and never spends through its `0.20` reserve floor.
- `ReproductionReadiness` reduces births during a sustained deficit, resumes them after recovery, and never changes deterministic reproduction, zero-sum allocation, or cooldown draws.
- Under at least one feast/bust fixture, regulation improves lineage survival; under a stable rich fixture, its upkeep and deferred reproduction make it slower than the unregulated control.
- Without a resource gradient or neighboring-tile sense, scarcity cannot create directed within-tile movement or knowledge of a favorable neighbor.
- Near-equivalent behavior selection varies among organisms but reproduces exactly across dense iteration order, worker count, save/load, and client observation.
- Pressure channels, behavior state, target, and memory round-trip through save/load and preserve the next decision and state hash.
- Per-tile counts sum exactly to the tile/species population; authorized tile counts sum to the displayed species distribution; fractions use deterministic rounding and expose no hidden organisms.
- Enabling, disabling, or changing behavior aggregation, client subscriptions, or species-panel visibility never changes any organism behavior or world hash.

# Biological interpretation

The controller abstracts several observed biological patterns without claiming to reproduce their molecular machinery. Bacterial growth and gene expression are constrained by allocation of cellular resources, supporting a real cost for additional regulation and a tradeoff between growth machinery and other functions: [Scott et al.](https://pubmed.ncbi.nlm.nih.gov/21097934/). Experiments on nutrient downshift show that the stringent response reallocates cellular investment and changes growth behavior under scarcity, supporting explicit growth suppression and metabolic reprioritization rather than a global carrying-capacity rule: [Zhu and Dai](https://www.nature.com/articles/s41467-023-36254-0).

Observed bacterial chemotaxis biases an otherwise stochastic movement process rather than performing global pathfinding, supporting capability-limited observations, persistent local targets, and keyed exploration: [Berg and Brown](https://www.nature.com/articles/239500a0). Genetically identical bacteria can also switch into slow-growing persister states, while density-conditioned bacterial expression demonstrates a plausible basis for future quiescence and quorum-response branches: [Balaban et al.](https://pubmed.ncbi.nlm.nih.gov/15308767/) and [Nealson, Platt, and Hastings](https://journals.asm.org/doi/10.1128/jb.104.1.313-322.1970).

The one-day memory, pressure formula, thresholds, dwell, utilities, and trait effects remain LYFE balance values rather than measured biochemical constants.

# Deferred directions

- Aquatic, food, temperature, and toxin-triggered dormancy policies beyond the calibrated terrestrial moisture profile.
- Quorum-like signaling and density-sensitive reproduction or dispersal.
- Stochastic quiescence or persistent phenotype switching as an explicit bet-hedging strategy.
- Learning, prediction, neural decision systems, social coordination, communication, and shared species memory.
- Within-tile resource-gradient sensing and resource-point targets.
- Calendar-aware or forecast-aware behavior. Even later policies must receive an explicit evolved information source rather than read the future.

# Decisions fixed by this pass

- [x] Local reactive homeostasis rather than global carrying-capacity or omniscient optimization.
- [x] `Baseline`, `Conserving`, `Foraging`, `Dispersing`, and `Fleeing` as the first dominant behavior set; dormancy remains lifecycle state.
- [x] Separate behavior permissions for optional growth, reproduction, movement, targeting, and metabolic coordination.
- [x] Concrete reserve, energy-coverage, material-deficit, acquisition-coverage, and environmental-stress inputs.
- [x] Compact approximately one-day fixed-point EMA rather than per-organism history rings.
- [x] First conservation entry, critical, exit, hysteresis, four-hour dwell, and action-permission rules.
- [x] First `ReproductionReadiness` energy-coverage and projected 48-hour-runway rules.
- [x] Priority-banded selection, bounded near-equivalent keyed choice, and general behavior persistence.
- [x] No free density knowledge; quorum response and stochastic quiescence remain future placeholders.
- [x] Foundation renamed from `StochasticActivity` to `BaselineActivity`.
- [x] Current behavior distributions available per live tile and, where actor knowledge permits, as exact or explicitly observed species aggregates without feeding back into organism decisions.

# Next calibration work

1. Assign mutation prices, change complexities, and upkeep to `StateGatedActivity`, `ResourceConservation`, `ReproductionReadiness`, `StressAvoidance`, and `MetabolicBehaviorCoordination` using population fixtures.
2. Test the `0.85/1.00/1.05` coverage and `0.20/0.50/0.65/0.80/0.85` reserve thresholds across founder, storage, complex-cell, predator, and terrestrial cohorts.
3. Calibrate generic scavenging target value and non-sensing dispersal persistence without changing the already-fixed hunting policy.
4. Run clustered 100,000-organism behavior and spatial-index performance fixtures.

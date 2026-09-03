# DNA, Mutation, Speciation, and Lineage

Status: first v1 mutation-income, pricing-scale, speciation, autonomous-evolution, material-opportunity scoring, and lineage rule pack; catalogue-wide per-node pricing and broader population validation remain

Sources: [ORGANISMS vision](../../vision/ORGANISMS.md), [GAMEPLAY vision](../../vision/GAMEPLAY.md), [INTERFACE vision](../../vision/INTERFACE.md), and [behavior and resource pressure](BEHAVIOR_AND_RESOURCE_PRESSURE.md).

# Purpose

Define species-level DNA, trait relationships, mutation-point generation, human and autonomous speciation, and the persistent tree of life.

# Species DNA

Every v1 organism references one immutable DNA definition owned by its species. Define the compiled representation of:

- Trait families and levels or nodes.
- Prerequisites and incompatibilities.
- Direct capabilities and numeric modifiers.
- Cross-family effects.
- Ongoing metabolic and reproductive costs.
- Starting traits and future-accessible paths.
- Visual descriptors that clients may use without affecting mechanics.

A proposed DNA change must be validated and fully compiled before a speciation command can be accepted.

The detailed graph, typed-effect, stacking, activation, cost-channel, compilation, and candidate-frontier proposal is defined in [TRAIT_SYSTEM.md](TRAIT_SYSTEM.md). The first concrete family forests, milestone paths, and ecological tradeoffs are proposed in [TRAIT_CATALOGUE.md](TRAIT_CATALOGUE.md). This document owns mutation income, speciation transactions, autonomous choice, and lineage; the trait plans own the structure and meaning of the DNA being changed.

## Canonical trait families

The first compiled schema recognizes the following stable family IDs:

| Family ID | Compiled effects owned by the family |
| --- | --- |
| `CellularOrganization` | Viable/mature structure, scale, membranes/walls, compartments, eukaryotic organization |
| `EnvironmentalTolerance` | Moisture, temperature, and gas/chemical tolerance curves and stress costs |
| `ResourceAcquisition` | Uptake tags, transfer throughput, active-transport costs, ingestion and digestive access |
| `ExternalEnergyCapture` | Environmental reaction IDs, capture throughput, opportunity gates, and capture efficiency |
| `InternalMetabolism` | Internal reaction IDs, reserve mobilization, maintenance, assembly throughput, regulation, and waste routing |
| `EnergyStorage` | Reserve-resource eligibility and maximum energy-storage capacity |
| `NutrientStorage` | Available-store capacity by resource/tag and stockpiling constraints |
| `GrowthLifecycle` | Lifecycle phases, growth gates, dormancy, maturity, and senescence |
| `Reproduction` | Allocation model, health/resource gates, cooldown and jitter, overhead, and offspring requirements |
| `Locomotion` | Movement modes, velocity, terrain compatibility, and energy costs |
| `Sensing` | Observable signal types, ranges, precision, and update cost |
| `BehavioralRegulation` | Available behaviors, selection parameters, and condition-to-goal mappings |
| `PredationScavenging` | Target rules, interaction range, kill/capture and consumption parameters |
| `Defense` | Capture/kill resistance, structural protection, deterrence, and declared costs |
| `EvolutionaryMachinery` | Mutation-income modifier, affordable change breadth, DNA exchange, and abstract sexual reproduction |

Stable family IDs organize validation and presentation. Traits may emit effects owned by other families only through declared cross-family effects, and the compiler records the source trait for explanation. Future family additions must not change the meaning of existing IDs in a saved rule pack.

## Metabolic boundaries

The compiler treats acquisition, energy capture, internal metabolism, and storage as distinct effect domains:

```text
NutrientStorage
    --defines capacity of--> AvailableStore

external matter
    --ResourceAcquisition--> AvailableStore

environmental substrates + energy opportunity
    --ExternalEnergyCapture--> EnergyReserve(ReserveOrganic) + products

AvailableStore + internal substrates
    --InternalMetabolism--> EnergyReserve, Structure, internal stores, or waste

EnergyReserve
    --InternalMetabolism / SpendStoredEnergy--> useful work + spent matter + heat
```

No compiled trait may directly credit stored energy or matter. It enables or modifies a balanced reaction, capacity, or cost. `EnergyStorage` changes what can be held; it neither creates reserve nor increases a resource's energy density. A new reserve chemistry requires a new resource and balanced reactions.

`NutrientStorage` independently caps non-reserve matter in `AvailableStore`. Acquisition fills it and internal reactions consume or route it. It must not be inferred from energy capacity because species may be good at surviving energetic shortages while being poor at stockpiling a required micronutrient, or vice versa.

Every founding DNA contains `BasalReserveMobilization`, `BasalMaintenance`, and `BasalBiomassAssembly` internal capabilities plus `PrimitiveOrganicReserve` with a capacity of 10,000 energy units. Incremental reserve-capacity traits and a later cellular-organization-gated `CompartmentalizedReserve` are the initial storage path. Mutation preserves current reserve and structure; evolved capacity is commissioned gradually from an aggregate storage-structure assignment, and each newly constructed empty increment lowers reserve-relative health until it is filled. Exact values are defined in [ENERGY_STORAGE.md](ENERGY_STORAGE.md).

Fermentation and respiration compile as internal catabolic reactions. Hydrogen acetogenesis, sulfide anoxygenic phototrophy, and later oxygenic photosynthesis compile as external energy-capture reactions. Organic uptake is a resource-acquisition capability. These placements do not prevent cross-family prerequisites or combined UI explanations.

The first metabolism graph must implement the asymmetric paths in [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md): hydrogen acetogenesis reaches organic uptake and fermentation through the cheaper bridge; sulfide anoxygenic phototrophy receives stronger opening specialization traits but requires metabolic regulation and generalized catabolism before fermentation, or the substantially more expensive complex-photosystem, manganese/calcium water-oxidation, and oxygen-tolerance route to oxygenic photosynthesis. Suppression through metabolic regulation reduces ongoing cost but never removes an acquired trait.

# Mutation-point economy

Income is a function of total living population, average relative health, and DNA modifiers. Relative health is derived from concrete organism state, and the calculation consumes the completed phase-10 species aggregate defined in [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md). Controlled, uncontrolled, and sandbox-locked species use exactly the same calculation; control and locking affect decision authority, not income.

The first v1 formula is:

```text
effectivePopulation = 100 * log2(1 + livingPopulation / 100)
incomePerTick = effectivePopulation
              * averageRelativeHealth
              * dnaMutationModifier
              * tickDurationHours
              / 750
```

`referencePopulation = 100` and the `750` healthy-effective-organism-hours per point are versioned balance values. The logarithm provides diminishing returns without a discontinuous gameplay cap. Population zero produces zero income; extinction freezes the final balance and remainder for history.

At full health and the founder `1.0×` DNA modifier:

| Living population | Effective population | MP per simulated day | Hours to 40 MP |
| ---: | ---: | ---: | ---: |
| `10` | `13.75` | `0.440` | `2,182` |
| `25` | `32.19` | `1.030` | `932` |
| `100` | `100.00` | `3.200` | `300` |
| `500` | `258.50` | `8.272` | `116` |
| `1,000` | `345.94` | `11.070` | `87` |

Average health and the DNA modifier scale these rates linearly. A population of 100 at `0.75` average health therefore takes approximately 400 hours rather than 300 to earn 40 MP. This directly rewards physiological success while retaining strong diminishing returns from raw population.

## Authoritative arithmetic and state

One mutation point is stored as `1,000,000 MutationQ`. Population is an integer; average health, DNA modifier, and tick hours use the shared one-million fixed-point scale. The rule pack contains an immutable `effectivePopulationQ[0..maximumWorldOrganisms]` table generated by a pinned high-precision authoring tool using round-to-nearest, ties-to-even. The table and its generator version participate in the rules hash. Runtime simulation never evaluates a platform floating-point logarithm, and configuration rejects an organism ceiling beyond the table.

```text
SpeciesEvolutionState:
    mutation_balance_q: Int64
    mutation_income_remainder: UInt128
    speciation_not_before_tick: Tick
    speciation_ordinal: UInt32
    evolution_revision: UInt64
    next_autonomous_evaluation_tick: Tick
    autonomous_evaluation_ordinal: UInt32
    autonomous_intent: AutonomousEvolutionIntent?
    pressure_state: PressureState
```

```text
AccumulateMutationIncome(species, completedAggregate, tickDurationQ):
    population = completedAggregate.livingPopulation
    if population == 0:
        return

    effectiveQ = rules.effectivePopulationQ[population]
    averageHealthQ = RoundTiesToEven(
        completedAggregate.sumRelativeHealthQ / population)
    modifierQ = species.compiledDna.mutationIncomeModifierQ

    numerator = checkedUInt128(effectiveQ)
              * averageHealthQ
              * modifierQ
              * tickDurationQ
              + species.mutation_income_remainder
    denominator = 750 * RATIO_SCALE^3

    creditQ = numerator / denominator
    species.mutation_income_remainder = numerator % denominator
    species.mutation_balance_q = checkedAdd(species.mutation_balance_q, creditQ)
```

The founder modifier is `1.0×`. Compiled mutation-income modifiers are clamped to the rule-pack range `0.25×..3.0×`; reaching a clamp is a configuration error for required catalogue genomes rather than a silent balance mechanism. There is no lower gameplay rounding, minimum income, or bank cap. Numeric overflow or the configured species-account ceiling rejects the tick instead of saturating.

`evolution_revision` changes when genome, control/lock authority, the stored cooldown schedule, or another speciation-relevant structural field changes; ordinary income, pressure updates, and the mere passage of time do not increment it. Every command still rechecks the current balance, founder counts, and all invariants at application. This lets a preview survive harmless point accumulation without accepting stale DNA or authority.

Income runs once per completed tick after births, deaths, migrations, and health are finalized. The client receives current balance, population, average health, DNA modifier with trait provenance, current MP/day, and forecasts explicitly labeled as estimates at the current rate.

Using reserve fraction as the temporary isolated-fixture proxy for the full derived-health formula, this curve gave the hydrogen founder 40 MP near hour 338 and the sulfur founder 60 MP near hour 511. The first coupled health, lifecycle, contention, reproduction, and speciation authoring run now reaches those landmarks at hours `343` and `496`, with accepted bands of `335..350` and `480..520`; see [SURVIVAL_OPENING_VALIDATION.md](SURVIVAL_OPENING_VALIDATION.md). The common denominator remains suitable for initial implementation, while production fixed-point and generated-world validation still precede freezing it as final balance.

# Mutation pricing and change breadth

Mutation price and change complexity remain independent. A proposal pays the simple sum of every explicitly acquired node and consumes the simple sum of their `changeComplexity`; there is no bundle discount, environmental price, distance surcharge, refund, or separate speciation fee.

The first catalogue uses these authoring bands:

| Change class | Typical per-node price | Typical complexity | Intended role |
| --- | ---: | ---: | --- |
| Incremental adaptation | `20..40 MP` | `1` | Improve an existing capability or tolerance |
| New early capability | `40..80 MP` | `1..2` | Open one modest action, reaction, or regulatory option |
| Significant specialization | `80..150 MP` | `2..3` | Change niche access or add a substantial system |
| Major or late capability | `150..300 MP` | `3..5` | Keystone organization, metabolism, or ecological strategy |

These are lint bands, not automatic price formulas. A node outside its band's usual range requires an authored rationale. Prices are calibrated against cumulative milestone paths and persistent biological liabilities, not visual depth in a tree. The current anchors remain normative first fixtures:

- `OrganicResourceUptake = 40 MP`, complexity `1`.
- `Fermentation = 80 MP`, complexity `2`.
- `MetabolicRegulation = 60 MP`, complexity `1`.
- `GeneralizedOrganicCatabolism = 80 MP`, complexity `2`.
- `ComplexPhotosystem = 100 MP`, complexity `2`.
- `ManganeseCalciumWaterOxidation = 150 MP`, complexity `3`.
- `OxygenToleranceI = 80 MP`, complexity `1`.

Together the last three anchors activate the `OxygenicPhotosynthesis` reaction at cumulative price `330 MP`; the reaction name is not a fourth unpriced trait node.

The founder `AsexualInheritance` profile permits at most `3` change-complexity units per event. `ExpandedChangeCapacityI` raises the limit to `5`; `ExpandedChangeCapacityII` raises it to `8`. Thus the hydrogen `40 + 80 MP` path can be deliberately saved for and acquired in one founder-capacity event, while the sulfur `60 + 80 + 80 MP` route normally requires multiple events unless change capacity evolves first.

## First evolutionary-machinery economy

| Trait | MP | Complexity | Mutation-income effect | Other first liability/effect |
| --- | ---: | ---: | ---: | --- |
| `AsexualInheritance` | setup | `0` | `1.00×` | Maximum event complexity `3` |
| `IncreasedMutationSupplyI` | `60` | `1` | `1.15×` | `5` energy/hour passive upkeep |
| `IncreasedMutationSupplyII` | `120` | `2` | additional `1.20×` | additional `10` energy/hour upkeep |
| `DNAExchange` | `120` | `2` | `1.20×` | `5` energy/hour upkeep and `+100` reproductive-work energy |
| `AbstractSexualReproduction` | `180` | `3` | additional `1.25×` | additional `10` energy/hour upkeep, `+250` reproductive-work energy, and `+3 h` base reproductive cooldown |
| `ExpandedChangeCapacityI` | `100` | `2` | none | Set maximum event complexity to `5`; `5` energy/hour upkeep |
| `ExpandedChangeCapacityII` | `180` | `3` | none | Set maximum event complexity to `8`; additional `10` energy/hour upkeep |

`ProtoEukaryoticOrganization` provisionally contributes a cross-family `1.10×` mutation-income modifier in addition to its much larger structural, maintenance, reproduction, and micronutrient liabilities. With all first income modifiers combined, the resulting `2.277×` remains below the `3.0×` compiler bound. Modifiers multiply in canonical trait-ID order; upkeep and reproductive costs add through their named channels. Exact prices for other catalogue nodes remain versioned content calibration within the bands above.

At full health, holding population fixed and ignoring every biological liability, the simple mutation-income payback diagnostics are:

| Investment | Population 100 | Population 500 |
| --- | ---: | ---: |
| `IncreasedMutationSupplyI` | `125` days | `48` days |
| `IncreasedMutationSupplyII` after I | `163` days | `63` days |
| `DNAExchange` from baseline | `188` days | `73` days |
| `AbstractSexualReproduction` after DNA exchange | `188` days | `73` days |

Actual payback is longer because upkeep and reproductive overhead can reduce health or population growth. These values make evolutionary machinery a long-horizon investment that becomes more credible for large successful species rather than an automatic opening purchase. Scenario tests must compare remaining world time and indirect change-capacity value, not only this static arithmetic.

# Speciation

V1 allows one to four occupied founding tiles with per-tile fractions of 50%, 20%, 8%, and 3% respectively. The fraction applies independently to the phase-boundary ancestor population in each selected tile:

```text
fractionBySelectedTileCount = { 1: 0.50, 2: 0.20, 3: 0.08, 4: 0.03 }
founderCount(tile) = max(1, floor(localAncestorPopulation * fraction))
```

A descendant may begin with one organism because founders are asexual and v1 has no within-species genetic diversity. The transaction must leave at least one living ancestor organism across the world so speciation remains a fork rather than an in-place genome rewrite. Selected tiles must be distinct, occupied by the ancestor, and need not be adjacent. The preview shows exact counts and warns when a branch is very small or its current state poorly activates the proposed DNA.

Every accepted event gives both ancestor and descendant an absolute 168-hour refractory interval. `speciation_not_before_tick` is the first tick boundary at least 168 simulated hours after commit. Both branches continue earning MP during the interval. This limits bursts made possible by intentionally duplicated unused balances without adding another point cost. Roots start immediately eligible; a cooldown cannot be rerolled, shortened by save/load, or reset through control transfer.

## Command and validation

```text
SpeciationCommand:
    ancestor_species_id
    expected_evolution_revision
    expected_genome_hash
    new_trait_ids[]                 # canonical unique set
    selected_tile_ids[]             # 1..4 canonical unique IDs
    follow_descendant_if_permitted
```

The server returns typed validation failures including `WrongController`, `SpeciesLocked`, `SpeciesExtinct`, `StaleEvolutionRevision`, `GenomeChanged`, `SpeciationCooldown`, `UnknownTrait`, `MissingPrerequisite`, `IncompatibleTrait`, `ChangeComplexityExceeded`, `InsufficientMutationPoints`, `InvalidTileCount`, `TileNotOccupied`, `AncestorWouldBeDepleted`, `FounderStateInvariant`, and `NumericOverflow`. Activation warnings and likely poor founder health do not reject a genetically valid proposal. V1 does reject a selected-founder transition that would violate an authoritative storage/account invariant for which no explicit overflow route exists.

## Deterministic founder selection

After the exact per-tile counts are known, allocate a stable `SpeciationEventId`. Rank every eligible organism in a tile by:

```text
Raw64(RandomAddress(
    SpeciationFounderSelection,
    speciationEventId,
    tileId,
    organismId,
    sampleIndex = 0))
```

Select the lowest `founderCount(tile)` ranks, breaking an equal raw rank by stable organism ID. Selection uses the accepted phase-0 snapshot and never depends on dense order, spatial-bin order, worker assignment, or a mutable random stream. The event stores the RNG-schema/domain version and a digest of selected IDs; ordinary history stores counts rather than a potentially large ID list. `Raw64` use here is confined to the reviewed stable-rank operation defined in [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md).

## Atomic transaction

```text
Speciate(command, completedBoundaryState):
    validate authority, lock, revision, genome, cooldown, and living ancestor
    compile proposed descendant DNA from ancestor DNA + explicit new traits
    validate prerequisites, incompatibilities, price, and change complexity
    calculate per-tile founder counts and prove at least one ancestor remains
    validate selected-founder account/capacity transition invariants

    eventId = allocate deterministic lineage event ID
    founders = select keyed subsets from the stable snapshot
    postPriceBalanceQ = ancestor.balanceQ - proposal.priceQ
    descendantId = allocate deterministic species ID

    atomically:
        ancestor.balanceQ = postPriceBalanceQ
        descendant.balanceQ = postPriceBalanceQ
        copy ancestor income remainder and pressure state to descendant
        clear autonomous intent on both branches
        move founders to descendant without changing physical state
        set both speciation_not_before_tick to tick + 168 hours
        increment ancestor speciation ordinal and evolution revision
        initialize descendant speciation/evaluation ordinals and revision
        append immutable genome/speciation/lineage records
        transfer control as required by mode
        schedule the next autonomous evaluation for each branch left uncontrolled
        emit one accepted result
```

Unused MP and the fractional income remainder are intentionally copied after price payment, not divided. Pressure history is also copied because both branches experienced it before the fork; subsequent updates diverge. Organism positions, age, reserves, matter, structural assignments, bindings, lifecycle state, reproduction/scavenging cooldowns, and behavior persist. Invalid behavior or target capabilities are cleared through the normal species-transition rule. A larger compiled storage target begins unconstructed and supplies no capacity until ordinary biomass is assigned to it. Speciation never creates resources, repairs health, advances a lifecycle phase, or grants construction matter.

In survival, control must follow the descendant. In sandbox, `follow_descendant_if_permitted` may transfer control atomically; otherwise control remains on the ancestor and the player may switch normally. The ancestor becomes autonomous whenever it is no longer controlled. A sandbox lock blocks speciation but not income or pressure accumulation.

# Autonomous evolution

Autonomous species accumulate identically but maintain a persistent evolutionary goal rather than buying the cheapest available node. `next_autonomous_evaluation_tick` is the absolute first tick boundary at least 24 simulated hours after scheduling. When that boundary is reached, the species evaluates once, increments `autonomous_evaluation_ordinal`, and schedules the next boundary at least 24 hours later. A newly created, newly uncontrolled, or newly unlocked species schedules its first evaluation at least 24 hours later; a controlled or locked species has no due evaluation. Any resulting speciation command is queued for the next phase-0 boundary and is revalidated there; an autonomous descendant cannot act in its creation tick.

## Pressure state

Each pressure tag has a fixed-point accumulator with a 14-day exponential half-life. Evidence older than 60 days need not remain as individual events once folded into the accumulator. At every species-system pass:

- Each positive death-risk assessment contributes its probability, normalized per capita.
- The cause that actually triggered contributes an additional `1.0` before per-capita normalization.
- Mean nonfatal environmental or physiological severity contributes at `0.25×`.
- A failed required metabolic/acquisition process contributes at `0.50×` of its normalized failure rate.

Cause, stress, resource, and failed-process definitions map to versioned pressure tags. A triggered starvation death therefore weighs more than an untriggered 10% starvation risk, while both remain evidence. Aggregation uses complete server history and current state only; player visibility, client subscriptions, future simulation, and alternative-outcome probing are forbidden inputs.

## Goal selection

Candidate proposals are the same server-derived `Available` nodes and prerequisite-closed `ReachableInProposal` paths shown to a player. A candidate must satisfy incompatibilities and complexity, and its cost must be affordable from current balance plus forecast income over at most 60 simulated days. Forecasting uses an exponentially smoothed past income rate, not predicted population or environment.

Each candidate carries an authored positive `baseEvolutionWeightQ`. Let `pressureMatchQ` be the clamped dot product between normalized current pressure tags and the proposal's compiled pressure-response tags. Let `activationFractionQ` be the population-weighted fraction of living organisms whose current tile/state satisfies the proposal's non-material activation environment; traits without such a requirement use `1.0`.

A complete proposal that enables a finite-material-dependent process also compiles a `MaterialOpportunityProfile`. Its `materialOpportunityQ` is calculated from the exact required resource's current stock, trailing 168-hour production/exchange/uptake flows, proposed recurring costs, replacement capital, the real one-to-four-tile founder fractions, and the opportunity-adjusted distribution over those plans. It is the weighted expectation across eligible founding plans, not the best-case score. Resource-independent proposals use `1.0`; a preparatory proposal that does not yet enable useful work receives zero raw material opportunity. The complete deterministic calculation and organic fixture are defined in [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md).

Candidate weight is:

```text
pressureMultiplierQ = 1.0 + 3.0 * pressureMatchQ
feasibilityQ = 0.25 + 0.75 * activationFractionQ
materialOpportunityMultiplierQ = 0.10 + 0.90 * materialOpportunityQ
complexityQ = {1: 1.00, 2: 0.80, 3: 0.60, 4+: 0.40}
noveltyQ = keyed value in 0.90..1.10

candidateWeightQ = MultiplyRatiosRoundTiesToEven(
    baseEvolutionWeightQ,
    pressureMultiplierQ,
    feasibilityQ,
    materialOpportunityMultiplierQ,
    complexityQ,
    noveltyQ)
```

`noveltyQ` is keyed by world seed, species ID, goal-selection ordinal, and canonical proposal hash; it is not stored mutable randomness. The material-opportunity multiplier's `0.10` floor permits rare experimentation without treating an absent substrate as a useful niche. Candidate weights are positive integers after fixed-point composition or the candidate is rejected as invalid configuration.

On each goal-selection ordinal, a keyed 10% exploration draw samples by base, feasibility, material opportunity, complexity, and novelty without the pressure multiplier; the other 90% samples by the full weight. Weighted selection operates over candidates sorted by canonical trait-set hash. Exploration can therefore defy recent pressure, but it does not ignore the complete absence of a required material resource.

```text
AutonomousEvolutionIntent:
    target_trait_ids[]
    target_cost_q
    target_complexity
    created_tick
    expires_tick                         # created + 60 days
    selected_at_evaluation_ordinal
    unaffordable_evaluation_count
    initial_pressure_fit_q
    initial_material_opportunity_q
    material_opportunity_plan_breakdown[]
    candidate_score_breakdown
```

The intent persists while valid. It is reconsidered when prerequisites or control change, it expires, the 60-day affordability forecast fails for seven consecutive daily evaluations, or either its pressure fit or material-opportunity fit falls below half the corresponding initial value while another candidate scores at least twice as highly. Traits without a material-opportunity profile ignore the latter condition. Taking human control clears an intent. Applying a sandbox mutation lock also clears it; unlocking waits for the next ordinary evaluation.

## Commit timing and tile choice

Once the goal is affordable and cooldown permits, each daily evaluation receives a keyed commit draw:

```text
surplusRatio = min(1, (balance - cost) / max(cost, 1 MP))
commitProbability = 0.25 + 0.75 * surplusRatio
```

The probability is 25% at exact affordability and reaches 100% at twice the price, satisfying the requirement that a larger accumulated balance makes autonomous speciation more likely without abandoning persistent goals.

Autonomous v1 base tile-count weights are `80%`, `15%`, `4%`, and `1%` for one through four tiles. A resource-independent proposal samples those weights unchanged. For a material-dependent proposal, score the feasible founding plan for each `k` using the real `50%`, `20%`, `8%`, and `3%` per-tile fractions, then sample from:

```text
adjustedTileCountWeight[k] = baseTileCountWeight[k]
                           * max(0.10, planMaterialOpportunityQ[k])^2

materialOpportunityQ =
    sum(adjustedTileCountWeight[k] * planMaterialOpportunityQ[k])
    / sum(adjustedTileCountWeight[k])
```

Invalid tile counts receive zero weight. The weighted mean is the value used during candidate selection and intent reconsideration; the same adjusted distribution later supplies the tile-count draw. This preserves the prior when all plans are equally well supported while favoring a broader, lower-fraction branch when that is the only way the current resource budget can support the founders. Eligible occupied tiles use:

```text
tileWeight = localPopulation
           * max(0.10, localMeanHealth)
           * (0.50 + 0.50 * localPressureMatch)
           * (0.25 + 0.75 * proposedDnaActivationFraction)
           * (0.10 + 0.90 * localMaterialOpportunity)
```

For resource-independent proposals, `localMaterialOpportunity = 1.0`. Tiles are sampled without replacement with keyed draws and canonical tile ordering. Invalid combinations that would deplete the ancestor are removed before sampling. The ordinary per-tile founder fractions and atomic command path then apply unchanged.

Every autonomous event records the pressure snapshot, material-opportunity inputs and per-plan breakdown, candidates considered, selected score, exploration/commit/tile-count draws, goal age, selected tiles, and rule version. The player may inspect this explanation only to the extent allowed by species and tile visibility, but replay diagnostics retain it authoritatively. Client knowledge never constrains or changes the server calculation.

# Lineage

Setup creates one non-species `AbiogenesisOriginEvent` with one sandbox root or two independent survival roots. A root has no parent species; every non-root has exactly one parent. Inter-species breeding remains excluded, so the graph is a forest below one presentation origin rather than a reticulate network.

Identical acquired-trait sets under the same rules hash share one immutable `GenomeRecord`, but species identity never deduplicates: two branches may independently reach the same genome and remain different species.

```text
GenomeRecord:
    genome_id
    rules_hash
    canonical_acquired_trait_ids[]
    genome_hash
    compiled_dna_hash

SpeciesLineageRecord:
    species_id
    origin_event_id
    parent_species_id?
    founding_event_id
    genome_id
    created_tick
    extinct_tick?
    founder_counts_by_tile[]
    acquired_trait_delta[]

SpeciationEventRecord:
    event_id
    tick
    ancestor_species_id
    descendant_species_id
    actor_id?
    actor_kind: Player | Autonomous | System
    selected_tiles[]
    fraction_rule_id
    founder_counts_by_tile[]
    founder_selection_digest
    trait_delta[]
    mutation_price_q
    balance_before_q
    duplicated_balance_after_q
    change_complexity
    autonomous_explanation?
```

Extinction sets `extinct_tick` when the completed phase-10 population first reaches zero. Records, final mutation balance, genome, and ancestry remain queryable. Exact tile IDs and operational species state are authoritative but filtered by actor knowledge in client projections. A future recovery or multiplayer ownership record can reference existing species IDs without changing ancestry or event history; v1 survival does not backtrack.

# Closure and downstream handoff

The evolution economy has no remaining semantic blocker for implementation planning. The fixed contracts are income, arithmetic, pricing composition, event-complexity limits, founder selection, balance duplication, refractory timing, pressure and material-opportunity scoring, autonomous choice, lineage, authority, and failure behavior.

Player-facing proposal discovery does not change those contracts. [PLAYER_LOOP_AND_NARRATIVE.md](PLAYER_LOOP_AND_NARRATIVE.md) defines prerequisite-closed milestone presentation, the non-dominated proposal frontier, immediate/conditional/preparatory benefit timing, saved goals, and post-speciation consequence review. These are explanation and planning layers over the same validated trait set and atomic speciation command; they add no discount, refund, automatic mutation, or hidden survival forecast.

Remaining work divides into two downstream categories:

1. **Implementation evidence:** generate and hash the effective-population table; implement property, determinism, persistence, and population fixtures.
2. **Biological content calibration:** assign final price, complexity, pressure tags, effects, activation requirements, and liabilities to each selectable node after its owning subsystem defines concrete mechanics.

The mid/late-game rule pack must therefore return, for every new selectable capability:

- Exact balanced reactions or non-reaction mechanics and throughput.
- Structural, micronutrient, passive-upkeep, use-energy, reproduction, storage, and environmental liabilities.
- Stable prerequisites, activation requirements, incompatibilities, and pressure-response tags.
- A proposed mutation-price band and change complexity, validated against at least one favorable and one unfavorable niche fixture.

Catalogue-wide pricing can then be completed without reopening the economy formula or speciation semantics. The unresolved `MiniaturizedCell` product choice belongs to the complex-cell/size fixture: it remains a non-selectable placeholder until that pass determines whether staying at founder scale already supplies the intended oligotrophic niche.

The first returned fixture is organic uptake and fermentation. It retains the established `40 + 80 MP` prices and total change complexity `3`, adds respective 5 and 10 energy/hour passive upkeeps, and defines an exact low-yield reaction and mortality-derived substrate niche in [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md). Its first population calibration selects a 120-opportunity ceiling and the exact material-opportunity score in [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md); generic spent organic pools never count as fuel or opportunity.

`DissolvedOrganicSpecialization` is the first competitive-acquisition fixture: `100 MP / complexity 2`, 12 suppressible upkeep/hour, `Zn 5`, a `240/hour` LDO acquisition ceiling, and contention weight `2` inside the ordinary class. Its autonomous opportunity is positive only for recent otherwise-valid LDO contention or a complete higher-throughput direct-respiration proposal; stock alone is insufficient. Equal specialists do not create carrying capacity, and split claims cannot multiply weight. See [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md).

The next returned fixture is aerobic respiration. `OxygenRespiration` is `150 MP / complexity 3`, while its separately useful prerequisites retain their own prices: `OxygenToleranceI 80/1`, `MicronutrientRetention 60/1`, and `CatalyticCarrierRetention 60/1`; optional `ReducedProductUptake` is `60/1`. The pathway adds explicit O2 and fuel opportunity profiles, iron/copper quotas, carrier retention/recharge, coupled claims, and substantial upkeep in [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md). Autonomous scoring must see both required inputs rather than treating oxygen exposure or organic stock alone as a viable respiratory niche.

The paired oxygenic fixture retains the established `ComplexPhotosystem 100/2 + ManganeseCalciumWaterOxidation 150/3 + OxygenToleranceI 80/1 = 330 MP / complexity 6` milestone. It adds a one-CO2/one-water-to-one-reserve/one-O2 reaction, a `Mn 4 + Ca 1` quota, 30 energy/hour of photosystem/water-oxidation upkeep beyond tolerance, shared light with sulfide phototrophy, and exact light/material opportunity in [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md). Autonomous scoring treats exact accessible CO2 as recurring finite material, light and current committed quota as activation conditions, and micronutrient stocks, renewal, carried inventory, and uptake throughput as one-time provisioning opportunity. Oxygen toxicity is pressure against—not in favor of—oxygen production.

The first predation proposal adds a `BiologicalOpportunityProfile` because its finite opportunity consists of other living organisms rather than a tile resource pool. Contact capture begins with `ContactDetection 40/1 + ContactPredation 80/1 + CaptureMechanism 100/2 = 220 MP / complexity 4`, plus a useful acquisition/catabolic route. Distinct species are eligible regardless of ancestry or controller by default; optional `KinDiscrimination 80/1` excludes lineages within two tree edges at a `3/hour` recognition cost. Autonomous scoring combines eligible encounter, capture, observable food, processing, and cost coverage; energy shortage alone cannot favor a kill-only proposal. Recent predation mortality instead pressures defense, threat sensing, or escape. See [PREDATION.md](PREDATION.md).

The complex-predator calibration adds exact anchors for its remaining selectable nodes. `IncreasedCellScaleI` is `160/3` and Scale II is `260/4`; `ParticulateOrganicIngestion` is `120/2` and `ParticulateDigestion` is `140/2`; the directed-hunting sensing/behavior layer totals `360 MP / complexity 6`; and locomotion ranges from `ActiveMotility 60/1` to `AdvancedPropulsion 180/3`. The first passive-spread alternatives are `EnvironmentalAnchoring 40/1` and `EnvironmentalDrifting 60/1`; their respective 2- and 3-unit hourly upkeep and spatial tradeoffs are normative in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md). Each node retains the separate upkeep, quota, activation, and opportunity liabilities in [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md), [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md), and [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md). Autonomous scoring must evaluate complete reachable milestones and their prey-size/encounter economics rather than favoring an isolated scale, hunting, digestion, or dispersal node as though it supplied food by itself.

The energy-storage ladder adds `ReserveCapacityI 40/1`, `ReserveCapacityII 80/2`, and `CompartmentalizedReserve 160/3`, producing commissioned maxima of `25,000`, `50,000`, and `150,000`. The final node additionally requires `CompartmentalizedCell 160/3`; capacity is constructed from conserved storage structure rather than granted at speciation. Autonomous scoring should require evidence of both fill opportunity and a later deficit—capacity overflow plus drawdown, recurring unfavorable intervals, failed dormancy, or resource-poor migration—because starvation without prior surplus cannot make a larger empty compartment useful. Exact costs, commissioning, and calibration are in [ENERGY_STORAGE.md](ENERGY_STORAGE.md).

Behavioral-regulation proposals respond to named pressure evidence rather than raw population. Repeated low reserve, energy income below mandatory cost, unmet useful acquisition demand, growth suppression, and maintenance failure can favor `StateGatedActivity` or `ResourceConservation`; a recurrent birth-then-shortage pattern can favor `ReproductionReadiness`; environmental stress and hostile migration outcomes can favor `StressAvoidance`. These mappings use species aggregates produced from completed organism histories and never expose one organism to species-wide knowledge during behavior selection. `DirectedForaging` still requires a positive material or biological opportunity, and `MetabolicBehaviorCoordination` requires at least two useful enabled pathways whose availability or priority has actually varied. Exact scoring weights wait for the population fixtures in [BEHAVIOR_AND_RESOURCE_PRESSURE.md](BEHAVIOR_AND_RESOURCE_PRESSURE.md).

The terrestrial package adds `WetSurfaceColonization 120/2` and `IntermittentDesiccationTolerance 180/3`. The first is a prerequisite-closed landfall proposal only when it also includes or inherits `SelectiveMembrane`; the second extends an existing land-capable phenotype rather than independently authorizing land. Autonomous opportunity must combine observed neighboring land availability, trailing moisture and light conditions, coarse known lithology/resource fit, usable atmospheric reactions, and current pressure. A terrestrial tag alone has zero opportunity value, hidden exact neighboring stocks are unavailable, and the proposal must retain the exact structural, maintenance, throughput, and passive-acquisition liabilities in [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md).

# Required decisions and tests

- [x] First mutation-price bands, milestone anchors, change-complexity limits, and evolutionary-machinery economy.
- [x] Mutation-income formula, fixed-point accumulation, deterministic effective-population table, and modifier bounds.
- [x] Founder rounding, minimum one descendant, and at least one surviving ancestor.
- [x] Seven-day branch refractory interval and duplicated post-price balance/remainder.
- [x] Autonomous pressure accumulation, named material-opportunity scoring, persistent goals, exploration, commit timing, and candidate-aware tile selection.
- [x] Lineage event/state representation and typed proposal-validation errors.
- [ ] Catalogue-wide exact per-node prices/effects; later placeholders remain non-selectable.
- [ ] Population validation of income cadence, branching rate, autonomous diversity, and species-count growth.
- [ ] Deterministic effective-population table generator and golden hash.
- [ ] Executable deterministic founder-selection and autonomous-scoring fixtures; the organic authoring landmarks are fixed.
- [ ] Trait prerequisite/incompatibility property tests.
- [ ] Family/effect ownership validation and cross-family effect attribution tests.
- [ ] Capacity-only trait tests proving that energy and nutrient balances are unchanged at speciation.
- [ ] Reaction classification tests preventing external capture, internal catabolism, and direct capacity effects from bypassing ledger validation.
- [ ] Controlled/uncontrolled income equivalence tests.
- [ ] Save/load and worker-count equivalence for balances, remainders, pressure state, intent, cooldown, lineage events, and selection digests.

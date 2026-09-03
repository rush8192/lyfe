# Organism State and Derived Health

Status: first architectural pass; authoritative state ownership and the shape of the health calculation are decided, with first numeric candidates in [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md).

This document defines the state that belongs to an individual organism and the common physiological-condition value exposed to gameplay systems and the client. The surrounding tick order and contention rules are defined in [ORGANISMS.md](ORGANISMS.md); resource accounts and transfers are defined in [RESOURCE_MODEL.md](RESOURCE_MODEL.md).

# Core decision

**Health is a derived assessment, not an authoritative resource or spendable pool.**

An organism owns concrete state such as structure, stored resources, age, position, and behavior. At defined tick boundaries, the simulation deterministically evaluates that state against the species' compiled DNA and the local environment to produce normalized relative health.

Health therefore:

- Is never independently damaged, healed, transferred, or consumed.
- Is not an additional store of mass or energy.
- Cannot disagree with the concrete conditions meant to explain it.
- Is materialized and stored once at each named health barrier by the sole health builder.
- Is the numerical value consumed by downstream gameplay, aggregates, events, saves, and client projections; none of those consumers recomputes it.

This keeps the model biology-inspired and internally legible. Predation reduces concrete structure or reserves, starvation depletes stored energy, and a future toxin mechanic would increase a named toxic burden. Health reflects those changes without introducing unexplained damage or healing.

If a future system needs persistent injury, infection, poisoning, or tissue degradation, it should add the smallest concrete state needed to describe that condition. It should not turn health into an all-purpose hit-point account.

# Health semantics

Relative health answers:

> How close is this organism's current condition to the physiological ideal defined by its DNA and lifecycle state?

It is a unitless value on the closed interval `[0, 1]`:

- `1` means full reserves and no current condition penalties.
- Values near `0` mean severely depleted or stressed.
- `0` does not itself mean dead, although underlying states that produce `0` may independently produce a certain or nearly certain death risk.

Health is not synonymous with evolutionary fitness, reproductive output, combat power, or activity. Those systems may use health as one input alongside DNA capabilities, body structure, behavior, lifecycle phase, and local conditions.

# Authoritative organism state

The following is the proposed logical state for one living organism. Physical storage may use dense, columnar arrays rather than one object per organism, but the ownership and semantics remain the same.

```text
OrganismState
    organism_id
    species_id

    location
        tile_id
        position_x
        position_y
        velocity_x
        velocity_y

    lifecycle
        birth_tick
        biological_age
        phase
        phase_entered_tick
        reproduction_not_before_tick
        successful_reproduction_count

    resources
        structure[resource_id]
        structure_assignment[structure_role, resource_id]
        energy_reserve[resource_id]
        spent_energy_carrier[resource_id]
        available_store[resource_id]

    behavior
        current_behavior
        optional_target
        selected_at_tick
        minimum_dwell_until_tick
        optional_pressure_memory
            recent_energy_coverage_q
            recent_acquisition_coverage_q

    capability_state
        cooldowns[capability_id]
            scavenge_not_before_tick
        metabolic_binding_cohorts[]
        other small persistent state required by enabled traits
```

## Identity and DNA

- `organism_id` is stable for the organism's lifetime and is never reused within a world.
- `species_id` resolves to the species' authoritative DNA and compiled phenotype.
- DNA, capacities, tolerance curves, available behaviors, and baseline rates belong to the species and are not copied into every organism.
- Speciation changes the `species_id` of selected founders; it does not copy a mutable health value.

## Location and movement

- `tile_id` identifies the well-mixed compartment whose environmental state applies to the organism.
- Position and velocity are authoritative fixed-point values in tile-local coordinates.
- Crossing a permitted tile edge updates both tile and position through the deterministic movement phase.
- `LocalCoordQ` uses the normalized half-open tile square and velocity is a signed count of local-coordinate units per simulated hour. Direct v1 interactions are same-tile organism/remnant queries; exact geometry and movement semantics are defined in [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md).

## Chronological and biological age

- Chronological age is derived from `current_tick - birth_tick`; it does not require an age write on every tick.
- `biological_age` is authoritative because lifecycle phase, dormancy, or future traits may alter senescence rate relative to simulation time.
- `reproduction_not_before_tick` is the authoritative cooldown boundary. It incorporates the one keyed bounded jitter draw made when that cooldown was scheduled; reproduction itself has no per-tick success roll.
- `successful_reproduction_count` supplies a stable cooldown-jitter key and is incremented only by a committed reproduction transaction.
- In the simplest active lifecycle state, biological age advances by exactly one configured tick duration per tick.
- Senescence risk and age-related health effects use biological age. User-facing history may display both ages when they differ.

## Internal resource accounts

- `structure` is committed viable body material. Its assignment map partitions the same balance into geometric, organization, and storage-structure roles without changing resource identity or matter totals; micronutrient quotas remain separate committed resources. `geometricStructure + organizationStructure + storageStructure` supplies ordinary viable structure, while only the geometric assignment affects derived radius. The first role rules are defined in [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md) and [ENERGY_STORAGE.md](ENERGY_STORAGE.md).
- `energy_reserve` holds energy-bearing organic resources that can be metabolically consumed.
- `spent_energy_carrier` holds the composition-identical, zero-energy state of retained carrier matter when the DNA enables it. Charged and spent quantities share the compiled energy-carrier capacity, but only the charged balance is spendable or contributes stored energy.
- `available_store` holds internally available material that has not been committed to structure or a specialized reserve.
- Available-store balances remain one resource map but contribute to the separately limited dissolved-macronutrient, free-micronutrient, and ingested-matter capacity groups defined in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- Energy is not stored as a separate scalar. Total stored energy is derived from reserve quantities and configured energy densities.
- Maximum capacities and structural targets are compiled from DNA. Current energy capacity is derived from the organism's conserved storage-structure assignment and any organization activation gate rather than stored as an independently mutable scalar.
- Reproduction and predation transfer concrete resources between accounts and entities according to resource-ledger rules.

The v1 starting configuration may use only a single generic reserve-organic resource, but the account shape supports later storage chemistries without changing the health contract.

## Behavior and capability state

- The selected behavior and its ordinary dwell boundary are authoritative because they influence future movement, action selection, optional growth, and reproduction permission.
- An optional target may reference an organism, remains entity, position, tile edge, or other type permitted by that behavior.
- Organisms whose compiled traits use recent resource experience retain fixed-point energy-coverage and acquisition-coverage moving averages. Organisms without a history-using behavior do not require those optional columns.
- Composite resource pressure, behavior utilities, next-tick directives, observations, and candidate lists are derived from concrete state, pressure memory, compiled DNA, and capability-filtered observation; they are not independently writable organism attributes.
- Cooldowns or counters exist only for capabilities whose effects persist across ticks.
- Metabolic binding cohorts authoritatively encumber stored resources until their release tick as defined in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- Transient intents, claim weights, death risks, and action reservations are phase-local scratch data, not organism state.

The full behavior-state, initialization, speciation-invalidation, pressure-memory, and next-tick directive contract is defined in [BEHAVIOR_AND_RESOURCE_PRESSURE.md](BEHAVIOR_AND_RESOURCE_PRESSURE.md).

# Derived condition record

Systems that need health receive a derived condition record rather than reading an independently stored health field.

```text
DerivedCondition
    evaluated_tick
    evaluated_phase

    stored_energy
    energy_capacity                         # current commissioned value
    compiled_maximum_energy_capacity
    reserve_fraction

    structure_factor
    nutrient_factor
    age_factor
    lifecycle_factor
    environmental_factor
    environmental_factor_by_channel[stress_channel]

    relative_health
```

This breakdown is part of the contract. It makes displayed health explainable and gives balance and telemetry tools the factors that produced it.

Environmental stress is derived from species DNA, lifecycle state, and current tile conditions. It is not durable per-organism state merely because the vision describes an organism as having current stress. Implementations may cache a species-tile stress evaluation and apply small organism-specific modifiers.

Persistent exposure history is a separate future mechanic. If adopted, it must name the accumulated condition—for example toxic burden—rather than retaining an unexplained generic stress scalar.

# First health formula

All factors are clamped to `[0, 1]`. No factor acts as a bonus above full condition.

```text
stored_energy = sum(
    energy_reserve[resource] * energy_density[resource]
)

energy_capacity = commissioned_energy_capacity(
    species_dna,
    storage_structure,
    organization_activation)
reserve_fraction = clamp01(stored_energy / energy_capacity)

relative_health = clamp01(
    reserve_fraction
    * structure_factor
    * nutrient_factor
    * age_factor
    * lifecycle_factor
    * environmental_factor
)
```

The formula is deliberately multiplicative:

- Energy reserves remain the primary signal and place an upper bound on health.
- A severe deficiency cannot be hidden by excellence in an unrelated dimension.
- Each penalty has a named, inspectable cause.
- Traits improve the responsible capacity, target, or response curve instead of directly granting health.

The authoritative implementation must use the shared deterministic fixed-point ratio type selected in [DATA_MODEL.md](DATA_MODEL.md). It must define one canonical multiplication and rounding procedure and may not use platform-dependent floating-point math in the authoritative path.

## Reserve fraction

`reserve_fraction` compares current stored energy with the organism's currently commissioned DNA-defined capacity.

Zero-energy `SpentReserveCarrier` occupies physical carrier capacity but contributes zero to `stored_energy`. Consequently, spending a retained charged carrier lowers health until metabolism recharges it; retaining the carrier matter is not itself physiological energy.

This intentionally means that a storage mutation creates no energy or finished compartment. As ordinary biomass is assigned to the larger storage target, capacity rises while reserve quantity may not; relative health then falls because the same reserve fills a smaller fraction. The organism can restore that condition by gathering and storing additional resources. Exact commissioning and tier values are defined in [ENERGY_STORAGE.md](ENERGY_STORAGE.md).

A compiled maximum energy capacity below the founding `10,000` minimum is invalid for a living organism and must be rejected during DNA compilation. Current commissioned capacity remains at least that foundation value.

## Structure factor

`structure_factor` represents the fraction of expected viable structure that is present and functional.

```text
structure_factor = clamp01(
    viable_structure / current_phase_structure_target
)
```

- Reproduction must allocate at least the minimum viable structure required by the offspring's lifecycle phase.
- A newly budded offspring may be viable but below its mature structural target.
- An organism that acquires a higher body-scale, organization, or storage target enters the corresponding combined maturation phase: it retains the previous phase's viability floor, cannot reproduce, and uses the new mature total structure as its condition target until every required geometric, organization, and storage assignment is complete. No structure, reassignment, capacity, or stored matter is granted by the trait transition.
- Structural material lost to partial predation or a future injury mechanic lowers this factor until concrete resources are restored and rebuilt.
- Cosmetic size variation does not change authoritative structure.

If v1 does not permit living organisms to lose structure without dying, this factor will usually be `1`, but retaining it keeps predation, offspring growth, and future injury semantics coherent.

## Nutrient factor

Only constitutive, health-critical internal quotas contribute to whole-organism health. A micronutrient needed solely for an optional capability disables or weakens that capability when absent; it does not automatically make the whole organism unhealthy.

```text
quota_fraction(nutrient) = clamp01(
    internally_available(nutrient) / target_quota(nutrient)
)

nutrient_factor = limiting_quota_function(
    quota_fraction for each constitutive health-critical nutrient
)
```

The recommended v1 limiting-quota function is the minimum quota fraction. This follows the idea that the most limiting essential resource constrains current condition and is simpler to explain than an averaged score.

- If the compiled organism has no constitutive health-critical quota, `nutrient_factor = 1`.
- Empty general-purpose storage capacity is not itself a health penalty.
- Which starting resources are constitutive quotas, and their target quantities, remain fixture-level balance decisions.

## Age factor

`age_factor` is a monotonic DNA-defined curve over biological age.

- It is `1` through the normal pre-senescence interval.
- It declines gradually after the configured onset of senescence.
- It never reverses solely because the organism survives another tick.
- Senescence death probability remains a separate risk curve; the age factor does not replace it.

The first lifecycle rule also derives `ageMetabolicThroughput = 0.5 + 0.5 × age_factor`. This is a named consumer of the age factor, not another health component: it caps metabolic extents while leaving balanced reaction recipes and mandatory maintenance unchanged. The exact phase use and first values are defined in [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).

Keeping condition decline and death risk separate allows old organisms to become less capable before facing a high natural-death probability without turning health itself into a hidden death roll.

## Lifecycle factor

`lifecycle_factor` captures genuine phase-specific condition limits, not merely inactivity.

- The default is `1` for an organism in the expected condition for its phase.
- Immaturity should normally be represented by structural targets and action gates, not an arbitrary health penalty.
- Dormancy should normally preserve health while altering metabolism, movement, and reproduction.
- A lifecycle phase may define a factor below `1` only when it represents a physiological condition that should affect all consumers of health.

This avoids calling a well-adapted dormant organism unhealthy merely because it is inactive.

## Environmental factor

Each compiled phenotype supplies response curves for relevant environmental channels such as temperature, surface moisture or water state, acidity where modeled, and harmful volcanic compounds.

```text
channel_factor = response_curve(
    current_environment_value,
    preferred_range,
    soft_tolerance,
    hard_tolerance
)

environmental_factor = product(channel_factor)
```

- Values inside the preferred range produce `1`.
- Soft-limit excursions lower the factor and may also increase maintenance cost.
- Hard-limit excursions lower the factor further and independently create a direct death probability.
- Each channel remains separately visible in the condition record.

Environmental maintenance cost, direct health penalty, and death probability are three distinct configured effects. They must not be inferred from one another in a way that accidentally charges the same penalty twice. A channel may legitimately apply more than one effect, but each effect must be explicit in response-curve configuration and telemetry.

The exact slopes, floors, combination rule, and cross-stress interactions are balance parameters. Product composition is the initial rule; representative fixtures must test whether multiple moderate stresses compound too harshly.

# Health snapshots within a tick

Concrete state changes during a tick, so there is not one timeless health value for the entire tick. The simulation exposes named deterministic snapshots:

| Snapshot | Evaluated after | Primary consumers |
| --- | --- | --- |
| `intrinsic_start_condition` | Environment/resource update, before intrinsic death | Diagnostic death-risk explanations only; intrinsic probabilities read concrete causes directly |
| `interaction_health` | Movement and movement/action costs, before external grants and predation resolution | Predation attack/defense calculations and other external interactions |
| `reproduction_health` | External interaction plus internal metabolism and maintenance | Reproduction eligibility and allocation |
| `end_health` | Reproduction resolution | Behavior update, species average health, mutation income, snapshots, and client display |

The health builder updates only organisms whose declared dependencies changed between snapshots, stores the result and dependency generation, then seals it for consumers. The named phase determines which materialized value is visible.

The default, unqualified meaning of `health` in completed world/publication snapshots, telemetry, and gameplay documentation is `end_health`.

# Materialization and performance

Derived health does not require repeated expensive object-graph evaluation.

- Compile DNA into dense capacities, targets, and response-curve parameters once per species revision.
- Materialize shared environmental response inputs once per tile, or per occupied tile/species cohort only when multiple phases reuse the combined response.
- Compute organism-local factors in dense batches at the named health barriers and store the operational result.
- Tag materialized condition state with tick, phase, organism-state generation, compiled-phenotype revision, and tile-effective-state generation.
- Treat a tag mismatch or dirty read as an invariant failure; authoritative consumers never lazily recalculate.
- Include completed materialized health in authoritative hashes. A save may include a compatible health cache, or the sole health owner rebuilds the complete table before load validation/publication; debug oracles never silently replace a value during normal execution.

The server includes the stored `end_health` and its compact materialized factor breakdown in a client projection. Explanation reads the same value used by reproduction, behavior, species averaging, and mutation income.

This provides the practical advantage of a stored health field without creating another mutable simulation resource: only the health builder can write it, and it can do so only from declared primary/compiled/materialized dependencies. See [MATERIALIZED_DERIVED_STATE.md](MATERIALIZED_DERIVED_STATE.md).

# Species aggregation

At the end-of-tick barrier:

```text
species_average_health =
    sum(end_health for each living member) / living_population
```

- Members that died during the tick are excluded from living population and average.
- Newborns that survive reproduction resolution are included.
- Deterministic reduction order and rounding must be specified by the numeric-representation pass.
- Empty species have no average health and generate no mutation income.

The mutation economy uses this average as its physiological-condition input. Under the current three-input economy, healthy dormant organisms therefore still contribute normally through population and health. If later balance work shows that activity, dormancy, age structure, or effective breeding population must change income, that requires an explicit mutation-economy decision rather than silently redefining health.

# Death and health remain separate

Health is not rolled as a generic death chance.

At intrinsic death evaluation, each cause computes its own probability from concrete state and DNA response curves. Energy exhaustion, senescence, environmental hard limits, and other causes remain independently inspectable. The death record captures every cause with non-zero probability at the tick of death plus the selected cause, as defined in [ORGANISMS.md](ORGANISMS.md).

A low health value can correlate with several high death risks because they share underlying causes, but the engine may not add a generic `low_health` death probability unless a future mechanic explicitly defines and configures one.

# Presentation contract

The client should present health as a summary with an explanation, not as unexplained hit points.

At minimum, an observed organism may expose:

- Normalized health.
- Stored energy and energy capacity.
- Strongest current limiting factor.
- Significant environmental stress channels.
- Age and lifecycle phase.
- Whether a value is live, last-observed, or inferred under visibility rules.

The UI may render a familiar bar, but labels and tooltips should make clear that it represents physiological condition. Exact internal state remains subject to the player's tile visibility.

# Invariants and tests

The first implementation should enforce:

- Relative health is always within `[0, 1]`.
- The same authoritative inputs always produce the same condition record.
- Health is not present as an independently writable organism field.
- With all other factors at `1`, health equals reserve fraction.
- Increasing stored energy while capacity is fixed cannot lower health.
- Increasing energy capacity while stored energy is fixed cannot raise health.
- Increasing a constitutive nutrient toward its target cannot lower health.
- An absent capability-specific nutrient does not lower whole-organism health unless it is also a constitutive quota.
- Worsening one environmental channel while all other inputs are fixed cannot improve health.
- Neutral lifecycle and environmental factors do not alter health.
- Cache hits produce the same bit-exact record as full recomputation.
- Save/load recomputation produces the same end health and species average.
- Speciation or DNA changes immediately use the descendant species' capacities and curves.
- Species aggregation includes each living organism exactly once in stable reduction order.

# Remaining calibration decisions

First values for these areas are proposed in [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md). They do not block implementation, but fixtures must validate them before balance is stable:

1. The selected `RatioQ` scale and rounding procedure under a concrete stable species-reduction algorithm.
2. Whether partial structural loss is enabled in the first playable slice; founder viable and mature targets are already selected provisionally.
3. Advanced-trait constitutive quotas; both founder quota sets are already selected.
4. Alternative senescence and age-factor curves for evolved lifecycle strategies; the primitive curve has a first calibration.
5. Environmental response curves and severity of compounding simultaneous stresses.
6. Compact client and telemetry encodings for the factor breakdown.

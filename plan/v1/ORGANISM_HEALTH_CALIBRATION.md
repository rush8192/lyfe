# Organism Health Calibration

Status: provisional v1 ruleset exercised by paired-opening and terrestrial population fixtures; broader ecosystems and production fixed-point runs remain before the first rule pack is frozen.

Sources: [organism state and health](ORGANISM_STATE_AND_HEALTH.md), [resource calibration](RESOURCE_CALIBRATION.md), [hydrogen founder fixture](ONE_TILE_STARTING_CONFIGURATION.md), [sulfur founder fixture](SULFUR_TILE_STARTING_CONFIGURATION.md), and [trait catalogue](TRAIT_CATALOGUE.md).

# Purpose

Assign a coherent first set of numbers to the derived-health model without invalidating the established founder resource budgets or 10–14 day first-reproduction targets.

These values are calibration candidates, not engine constants. Every value belongs to a versioned rule pack or protocol schema, and representative simulation results may justify changing them.

# Calibration principles

1. Full reserves and ideal conditions produce full health.
2. Energy remains the dominant opening-game signal.
3. Founders begin moderately provisioned rather than nearly dead or fully buffered.
4. Direct environmental penalties are initially mild inside soft limits because stress already increases maintenance expenditure.
5. Missing pathway-specific catalysts disable that pathway rather than directly damaging unrelated physiology.
6. Senescence begins late enough for several founder reproduction opportunities but matters within the intended day-to-month organism timescale.
7. Values remain exactly reproducible in integer arithmetic.

# Decision summary

| Parameter | Provisional v1 value |
| --- | ---: |
| Ratio scale | `1,000,000` units per `1.0` |
| Founder mature structure target | `1,000` structural units |
| Primitive hard viable-structure floor | `500` structural units |
| Founder reserve capacity | `10,000` energy units |
| Founder initial reserve | `5,000`, or `0.5` reserve fraction |
| Primitive terminal reserve threshold | `0` |
| Founder growth-protection floor | `4,000`, or `0.4` reserve fraction |
| Founder reproduction-health gate | `0.60` |
| Primitive reproductive-work cost | `500` energy units |
| Minimum reserve per primitive-fission result | `4,000` energy units |
| Primitive senescence onset | `720 h`, or 30 days of biological age |
| Age-condition decline span | `720 h` after onset |
| Minimum age factor | `0.5` |
| Minimum age-metabolic-throughput multiplier | `0.75` |
| Senescence risk at onset | `100` per million per hour, or `0.01%` |
| Senescence risk escalation interval | `168 h`, or 7 days |
| Senescence risk cap | `500,000` per million per hour |
| Default environmental factor floor | `100,000`, or `0.1` |

# Fixed-point health representation

V1 uses a shared nonnegative fixed-point `RatioQ` representation with:

```text
RATIO_SCALE = 1_000_000
0.0         = 0
1.0         = 1_000_000
```

Health, probabilities, normalized severities, and multiplicative factors use an unsigned logical domain `0..1_000_000`. The C# storage type may be `int` or `uint`; calculations use checked `Int128` where a product or aggregate could exceed the storage type.

Canonical nonnegative division rounds to nearest, with an exact half rounded upward:

```text
RoundRatio(numerator, denominator):
    require numerator >= 0
    require denominator > 0
    return (numerator * RATIO_SCALE + denominator / 2) / denominator

MultiplyRatio(left, right):
    require 0 <= left, right <= RATIO_SCALE
    return (left * right + RATIO_SCALE / 2) / RATIO_SCALE
```

The implementation must not rely on C# decimal or floating-point operations in authoritative health evaluation. Configuration tools may display decimal source values, but the rule compiler converts them to `RatioQ` and rejects values that cannot be represented within the allowed rounding rule.

## Stable composition order

Named factors are multiplied in this order:

1. Reserve fraction.
2. Structure factor.
3. Nutrient factor.
4. Age factor.
5. Lifecycle factor.
6. Environmental factor.

Environmental channels are composed in stable configured `StressChannelId` order. The initial rule pack assigns temperature, water/moisture, H₂S, SO₂, O₂, and later channels stable IDs rather than depending on dictionary iteration order.

Species average health uses an exact checked integer sum followed by one rounded division. It is therefore independent of organism iteration order:

```text
averageHealthQ =
    (sumHealthQ + livingPopulation / 2) / livingPopulation
```

# Structural calibration

## Primitive direct lifecycle

| Value | Structural units |
| --- | ---: |
| Mature condition target | `1,000` |
| Hard viability floor | `500` |
| Structure required per result of primitive fission | `1,000` |
| Parent structure required before primitive fission | `2,000` |

```text
if viableStructure < hardViabilityFloor:
    StructuralFailure probability = 1.0
else:
    structureFactor = clamp01(
        viableStructure / conditionStructureTarget
    )
```

Primitive fission still produces two structurally mature 1,000-unit organisms, so lowering the hard floor from the earlier placeholder does not change either founding fixture. It creates a meaningful condition range for later partial structural loss and for an asymmetric juvenile without allowing a half-built organism to reproduce through `DirectLifecycle`.

## Increased-scale direct lifecycle

The first spatial calibration makes mature structure scale with the cube of mature radius:

| Organization | Mature target | Hard floor | Symmetric-fission parent requirement |
| --- | ---: | ---: | ---: |
| `PrimitiveCell` | `1,000` | `500` | `2,000` |
| `IncreasedCellScaleI` | `3,375` | `1,688` | `6,750` |
| `IncreasedCellScaleII` | `11,391` | `5,696` | `22,782` |

The table's mature hard floor is `ceil(0.5 × mature target)`. An organism acquiring a higher scale enters an explicit `ScaleMaturation` lifecycle phase: it retains its ancestor phase's hard floor, cannot reproduce, and receives higher-scale benefits only in proportion to built structure or after their declared activation threshold. Its condition target is the new mature structure target, so the transition is costly without becoming an immediate deterministic death. Reaching the target enters the ordinary mature phase and activates the new mature hard floor.

The condition formula remains `viableStructure / currentPhaseStructureTarget`; only the DNA-compiled target and lifecycle gates change. Speciation grants no structure: an organism retains its concrete matter and initially remains physically near its prior radius. Growth, ingestion, maintenance, and reproductive throughput for larger body tiers still require their dedicated fixtures; the separate energy-storage ladder now has its first commissioned-capacity calibration in [ENERGY_STORAGE.md](ENERGY_STORAGE.md). Radius derivation and encounter implications are defined in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).

V1 predation remains all-or-nothing and does not inflict persistent wounds. Until asymmetric budding or another structure-changing mechanic is enabled, a living primitive founder will therefore normally have `structureFactor = 1`.

## First asymmetric-budding candidate

The first balance fixture for `AsymmetricBudding` should use:

| Value | Structural units |
| --- | ---: |
| Continuing-parent target after allocation | `1,000` |
| New juvenile allocation | `600` |
| Juvenile hard viability floor | `500` |
| Juvenile maturation target | `1,000` |
| Minimum combined parent structure before budding | `1,600` plus any declared reproductive structure overhead |

The juvenile begins with `structureFactor = 0.6`, cannot reproduce, and becomes mature at 1,000 structure. This is deliberately only a starting candidate. The reproduction deep dive still owns reserve allocation, micronutrient allocation, attempt cost, maturation rate, and whether the continuing parent must retain a safety margin above 1,000.

# Nutrient calibration

Health uses only constitutive cellular quotas. Metabolism-specific catalysts remain activation and reproduction requirements.

## Constitutive founder quotas

Both founders use these health-critical target quantities:

| Micronutrient | Target per organism | Health role |
| --- | ---: | --- |
| Magnesium | `10` | Constitutive cellular/cofactor abstraction |
| Potassium | `10` | Constitutive internal-ion abstraction |
| Sodium | `10` | Constitutive founding aquatic-ion abstraction |

```text
nutrientFactor = min(
    clamp01(committedMagnesium / 10),
    clamp01(committedPotassium / 10),
    clamp01(committedSodium / 10)
)
```

`committed*` reads the organism's structural/catalytic quota assignment, not surplus material in its free micronutrient store. Founders begin with all three quotas filled, so `nutrientFactor = 1`. Free stockpiles and committed values above the target confer no direct health bonus.

## Capability-specific quotas

These existing quotas do not directly enter whole-organism health:

| Founder | Capability-specific quota |
| --- | --- |
| Hydrogen acetogenesis | Iron `20`, nickel `5`, cobalt `2` |
| Sulfide phototrophy | Iron `25` |

Falling below a capability's committed activation quota disables or scales that capability according to its trait definition. Reproduction requires complete constitutive and active inherited-capability quota sets for both resulting organisms; the additional set must be acquired into the free micronutrient store before it is committed to the offspring. Thus catalyst loss can indirectly lower future health through failed energy capture without being counted twice as an immediate generic nutrient penalty.

Advanced traits may declare additional constitutive quotas only when the nutrient is genuinely required for general ongoing structure or regulation. Otherwise their calcium, manganese, molybdenum, copper, zinc, or other requirements remain capability-specific.

# Senescence and biological age

## Primitive direct-lifecycle curve

Before senescence onset:

```text
ageFactor = 1.0
senescenceDeathChance = 0
```

At and after onset:

```text
onsetHours = 720
declineSpanHours = 720
minimumAgeFactor = 0.5
riskBasePerMillion = 100
riskEscalationHours = 168
riskCapPerMillion = 500_000

excessAge = biologicalAgeHours - onsetHours
decline = clamp01(excessAge / declineSpanHours)

ageFactor = 1.0 - 0.5 * decline^2

senescenceDeathChancePerMillion = min(
    riskCapPerMillion,
    round(riskBasePerMillion
          * (1 + excessAge / riskEscalationHours)^2)
)
```

Representative points are:

| Biological age | Excess after onset | Age factor | Hourly senescence chance |
| ---: | ---: | ---: | ---: |
| 30 days | `0 h` | `1.0000` | `0.0100%` |
| 37 days | `168 h` | `0.9728` | `0.0400%` |
| 44 days | `336 h` | `0.8911` | `0.0900%` |
| 51 days | `504 h` | `0.7550` | `0.1600%` |
| 58 days | `672 h` | `0.5644` | `0.2500%` |
| 60 days | `720 h` | `0.5000` | approximately `0.2794%` |

The first lifecycle rule derives a separate metabolic-throughput multiplier as `0.5 + 0.5 × ageFactor`. It is `1.0` through senescence onset and declines to `0.75` at the age-factor floor. It caps completed reaction extents without changing their mass/energy recipes or increasing baseline maintenance; see [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).

In an otherwise safe environment, this hourly risk curve gives approximate survival landmarks from birth:

| Fraction still alive | Approximate age |
| ---: | ---: |
| `75%` | `49.2 days` |
| `50%` | `58.0 days` |
| `25%` | `67.0 days` |
| `10%` | `75.1 days` |

The median organism therefore has time for several 10–14 day founder growth cycles, while old continuing-parent lineages face a meaningful late-life cost.

## Lifecycle aging-rate candidates

Biological age advances by simulated hours multiplied by a compiled lifecycle rate:

| State or trait | Biological-aging rate |
| --- | ---: |
| Active mature or juvenile | `1.00` |
| `DormantPhase` | `0.10` |
| `ResistantDormantPhase` | `0.02` |
| `RapidDivisionCycle` while active | `1.25` |

Dormancy does not directly lower health solely because the organism is inactive. It slows biological aging, changes tolerance and maintenance, and gates growth and reproduction. Entry and exit costs remain separate. `RapidDivisionCycle` accelerates biological aging rather than directly subtracting health.

# Environmental condition curves

Every stress channel declares a preferred/soft boundary, a hard boundary, maintenance weight, health penalty at the hard boundary, direct-death curve, and condition-factor floor.

For a value between the preferred/soft and hard boundaries:

```text
severity = clamp01(
    distanceBeyondPreferred / distanceFromPreferredToHard
)

extraMaintenance = ceil(
    baseMaintenance * maintenanceWeight * severity^2
)

channelHealthFactor =
    1.0 - healthPenaltyAtHard * severity^2
```

The first common parameters are:

| Channel class | Maintenance weight | Health penalty at hard | Factor at hard | Death chance just beyond hard | Death cap |
| --- | ---: | ---: | ---: | ---: | ---: |
| Temperature | `1.00` | `0.20` | `0.80` | `0.10%/h` | `25%/h` |
| Water/moisture | `1.00` | `0.25` | `0.75` | `0.10%/h` | `25%/h` |
| Acute chemical, including H₂S/SO₂/O₂ | `1.00` | `0.30` | `0.70` | `1.00%/h` | `50%/h` |

This means a soft-range stress both increases current energy expenditure and modestly lowers current function. The direct factor is intentionally much smaller than the potential cumulative reserve loss from prolonged maintenance stress.

Beyond a hard boundary:

```text
overage = distanceBeyondHard / configuredHardOverageScale

channelHealthFactor = max(
    0.10,
    factorAtHard / (1 + overage)^2
)

deathChance = min(
    deathCap,
    deathChanceAtHard * (1 + overage)^2
)
```

For one-sided chemical exposure, `configuredHardOverageScale` initially equals the hard threshold, preserving the existing H₂S/SO₂ death calculation. For two-sided temperature and moisture curves, it equals the distance from the nearest preferred boundary to that hard boundary.

Environmental factors multiply. Two moderate stresses can therefore matter together, but fixture tests must reject a rule pack in which ordinary seasonal variation collapses health through excessive compounding.

## Founder temperature candidate

The warm-water founder phenotype begins with:

| Range | Temperature |
| --- | ---: |
| Preferred | `40–50 °C` |
| Lower hard boundary | `25 °C` |
| Upper hard boundary | `65 °C` |

Both founder fixtures hold temperature at `45 °C`, so their temperature factor and stress maintenance are `1.0` and zero respectively. World generation may vary temperature; these bounds belong to the compiled founding tolerance rather than the tile definition.

## Water and surface-moisture candidates

Aquatic founders receive a neutral water-state factor while they remain in an aquatic tile. Water depth affects light and access to atmospheric gases through their own systems; it does not independently make an aquatic organism dehydrated. A founding organism cannot actively occupy a terrestrial tile.

For later terrestrial-capable traits, assume the world's normalized surface-moisture value uses `0` for dry and `1` for fully wet:

| Active phenotype | Preferred minimum | Hard minimum |
| --- | ---: | ---: |
| `WetSurfaceColonization` | `0.70` | `0.30` |
| `IntermittentDesiccationTolerance` | `0.40` | `0.10` |

Below the preferred minimum, the common water/moisture curve applies. Dormancy compiles separate phase thresholds rather than letting an active-cell tolerance imply dry survival:

| Dormant phase | Preferred minimum | Hard minimum |
| --- | ---: | ---: |
| `DormantPhase` | `0.20` | `0.05` |
| `ResistantDormantPhase` | `0.05` | `0.01` |

These thresholds apply only to organisms that already possess `WetSurfaceColonization`, the terrestrial-occupation capability. Dormancy does not by itself confer terrestrial access, change temperature or toxin curves, or permit survival at zero surface moisture. The landfall gate, trait liabilities, and migration interaction are defined in [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md).

## Founder sulfur example

Both founders have the already-defined `10×` SO₂ tolerance. At `5,000,000` exposure, the effective soft and hard thresholds are `2,500,000` and `10,000,000`:

```text
SO2 severity = (5,000,000 - 2,500,000)
               / (10,000,000 - 2,500,000)
             = 1/3

SO2 health factor = 1 - 0.30 * (1/3)^2
                  = 0.966667
```

Adapted H₂S, temperature, water state, structure, nutrients, age, and lifecycle are neutral at initialization. Exact fixed-point composition therefore gives:

```text
founder health = 0.500000 reserve fraction
                 * 0.966667 SO2 factor
               = 0.483334
```

The existing 6-energy SO₂ maintenance charge remains. This is intentional: one effect describes immediate condition and the other describes the ongoing energy cost of coping.

At an otherwise unchanged representative income rate, the SO₂ factor implies about `3.45%` more elapsed time than the reserve-only proxy: 338 hours scales to approximately 350 hours and 511 hours to approximately 529 hours. These are normalization estimates, not new milestone predictions; reproduction changes the population and income rate discontinuously, so exact timings require replaying the full trajectories.

## Compounding example

At half severity, temperature and acute-chemical factors are `0.95` and `0.925`. Their combined environmental factor is `0.87875`. An otherwise ideal organism at 75% reserves has:

```text
health = 0.75 * 0.87875 = 0.659063
```

This is a useful first regression case for verifying both composition and rounding.

# Lifecycle factor

The provisional factor is `1.0` for mature, juvenile, dormant, and resistant-dormant organisms when they are otherwise in the expected condition for their phase.

Lifecycle differences should first appear through:

- Phase-specific structure targets and action gates.
- Maintenance and biological-aging rates.
- Tolerance modifiers.
- Reproduction eligibility.
- Entry and exit costs.

A non-neutral lifecycle health factor requires a specifically described physiological impairment. This prevents health from becoming a disguised activity or reproduction score.

# Client and protocol encoding

Protocol Buffers should transmit `RatioQ` condition values as `uint32`; every value is bounded to `0..1_000_000` and is exactly representable in TypeScript's `number`.

A selected live organism may receive:

```text
OrganismConditionProjection
    evaluated_tick: uint64
    health_ppm: uint32
    reserve_fraction_ppm: uint32
    structure_factor_ppm: uint32
    nutrient_factor_ppm: uint32
    age_factor_ppm: uint32
    lifecycle_factor_ppm: uint32
    environmental_factor_ppm: uint32
    stress_factors[]
        stress_channel_id
        factor_ppm
        severity_ppm
    limiting_factor
```

Ordinary visible-organism deltas need only send `health_ppm` and a compact limiting-factor ID when either changes beyond the protocol's update threshold. Selecting an organism requests or subscribes to the full breakdown. The client displays whole percentages in dense views and one decimal place in the inspector; it retains the exact integer for tooltips and comparisons.

Optional presentation bands are:

| Health | Label |
| ---: | --- |
| `>= 0.75` | High condition |
| `>= 0.40` and `< 0.75` | Moderate condition |
| `>= 0.15` and `< 0.40` | Low condition |
| `< 0.15` | Critical condition |

These labels and colors are presentation-only. Simulation gates compare exact `RatioQ` values from rule data and do not depend on a label.

Last-known observations retain the condition projection and its observation tick. The client never recomputes a hidden organism's current health from partial or stale data.

# Validation matrix

The next executable fixtures should establish:

1. Both founders initialize at exactly `483,334` health units under their documented tile conditions.
2. Removing the SO₂ direct-health penalty restores exactly `500,000` initial health without changing resource-ledger flows.
3. Primitive fission retains two 1,000-structure results and its existing 250/334-tick growth targets.
4. A 600-structure asymmetric juvenile survives with `structureFactor = 600,000`, cannot reproduce, and matures at 1,000 structure without free matter.
5. Removing one half of a constitutive quota produces `nutrientFactor = 500,000`; removing a pathway-only catalyst disables the pathway but leaves the generic nutrient factor unchanged.
6. The senescence curve reproduces the approximate 49.2/58.0/67.0/75.1-day survival landmarks in a no-other-risk cohort.
7. The half-thermal plus half-chemical example produces exactly `659,063` health at 75% reserves.
8. Multiplication, species aggregation, save/load, replay, and different worker counts remain bit-identical.
9. The founder population fixtures are rerun through first speciation, and the mutation-income denominator is retuned only if decisions no longer occur within the intended fastest-speed cadence.
10. Representative seasonal worlds confirm that common simultaneous soft stresses do not make broad tolerance mandatory or universally optimal.
11. Fully commissioned storage tiers at a fixed `10,000` charged reserve produce reserve factors `1.0`, `0.4`, `0.2`, and `0.066667`; gradual commissioning is monotone and never credits reserve.

# Expected revision points

The values most likely to move after simulation are:

- The 500-unit viability floor and 600-unit budding allocation.
- Which cellular ions are genuinely constitutive health quotas.
- Senescence onset and escalation relative to realized reproduction cadence.
- Direct environmental penalty coefficients versus maintenance costs.
- Product composition if several mild simultaneous stresses become too punitive.
- Condition-delta thresholds and how much factor detail is affordable for large live tiles.
- Storage-capacity health dilution and recovery during real speciation/construction trajectories.

The fixed-point scale and derived-not-stored health decision should be substantially more stable than these balance values.

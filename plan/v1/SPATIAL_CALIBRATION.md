# Spatial Numerical Calibration

Status: provisional v1 founder-scale, passive-spread, sensing, interaction, placement, active-speed, turning, and movement-energy rule-pack candidate; synthetic clustered fixture complete, coupled lifecycle/ecosystem fixture required before freezing

Sources: [spatial organism contract](SPATIAL_ORGANISMS_AND_BEHAVIOR.md), [organism mechanics](ORGANISMS.md), [trait catalogue](TRAIT_CATALOGUE.md), [organism health calibration](ORGANISM_HEALTH_CALIBRATION.md), and [simulation loop](SIMULATION_LOOP.md).

# Purpose

Set one coherent spatial scale for v1. Organisms should occupy and interact within a small fraction of a tile, while a founder population undergoing zero-mean Brownian movement should still produce occasional chance encounters. Active movement and sensing must create meaningful improvements without turning every organism in a tile into a candidate on every tick.

These are gameplay-normalized values. A tile has no physical length, so the values do not claim a literal cellular diameter, diffusion coefficient, or swimming speed.

# Founder body radius

The mature primitive founder radius is:

```text
FOUNDER_RADIUS_Q = LOCAL_ONE / 1024 = 4,194,304
FOUNDER_RADIUS   = 0.0009765625 tile widths
FOUNDER_DIAMETER = 0.001953125 tile widths
```

A mature founder diameter is therefore about `0.195%` of a tile width. Exactly `512` founder diameters fit across a tile. This is small enough that local interactions remain local, while powers-of-two conversions make binning, range bounds, fixtures, and fixed-point diagnostics easy to inspect.

Client markers may enforce a presentation-only minimum pixel size when zoomed out. That does not alter authoritative radius, range, position, or collision-free overlap.

# Radius derivation

The simulation uses a three-dimensional biomass-inspired cube-root relationship even though positions are projected into a two-dimensional tile:

```text
bodyRadiusQ = compiledMatureRadiusQ
            * cbrt(currentGeometricStructure
                   / compiledGeometricStructureTarget)
            * authoredShapeMultiplier

Primitive compiledMatureRadiusQ = FounderRadiusQ
Primitive compiledGeometricStructureTarget = 1,000
```

The authoritative implementation uses a versioned monotone fixed-point lookup or integer cube-root routine, not runtime floating point. Radius is derived and rounded to nearest with ties to even. A nonempty living organism has a minimum radius of `0.25 × FounderRadiusQ`; configured body-scale traits impose the upper bound.

`compiledGeometricStructureTarget` is owned only by physical cell-scale traits. Total `compiledMatureStructure` may be higher because walls, internal membranes, compartments, organelles, or storage machinery add matter at the same external scale. The organism therefore assigns its one conserved `StructuralBiomass` resource among `geometricStructure`, `organizationStructure`, and `storageStructure`; their sum is viable structure, but only the geometric assignment affects radius. Speciation preserves every assignment and growth fills destination deficits explicitly, so internal machinery can never be reinterpreted as free body volume. The exact assignment, growth, reproduction, and remnant rules are defined in [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md) and [ENERGY_STORAGE.md](ENERGY_STORAGE.md).

The first physical-scale targets are:

| Cellular organization | Mature structure target | Mature-radius multiplier |
| --- | ---: | ---: |
| `PrimitiveCell` | `1,000` | `1.00×` |
| `IncreasedCellScaleI` | `3,375` | `1.50×` |
| `IncreasedCellScaleII` | `11,391` | `2.25×` |

The second structure target rounds `1,000 × 2.25³ = 11,390.625` upward. Because both numerator and geometric target change, an existing `1,000`-structure organism that speciates into `IncreasedCellScaleI` initially retains approximately its old physical radius and must actually assemble the additional matter to reach `1.50×`. These targets express the geometric cost of scale; metabolic throughput, storage, ingestion, maintenance, and reproduction must be recalibrated so larger cells gain useful ceilings rather than only a longer growth bar.

By contrast, acquiring `CompartmentalizedCell` while staying at primitive scale leaves `compiledGeometricStructureTarget = 1,000` and `compiledMatureRadiusQ = FounderRadiusQ` even though the organization multiplier raises total mature structure. A founder retains its 1,000-unit geometric assignment and physical radius while it builds organization structure. If scale and organization are acquired together, normalized assignment deficits deterministically divide new structure between body expansion and organization without either receiving free matter. See [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md).

A primitive organism at the `2,000` pre-fission structure threshold has radius `cbrt(2) = 1.2599×` its mature post-split radius. After a symmetric split, both `1,000`-structure organisms return to approximately `1.00×` radius.

Remnants begin with the terminal organism's radius and packing profile. Their radius subsequently scales by the cube root of remaining body-equivalent matter load divided by initial remnant matter load, with a `0.25 × FounderRadiusQ` floor while any directly consumable matter remains. Empty remnants are removed rather than displayed at the floor.

# Brownian displacement

The primitive one-hour Brownian target is:

```text
FounderBrownianRmsQ = 2 × FounderRadiusQ
                    = LOCAL_ONE / 512
                    = 8,388,608

FounderBrownianRms = 0.001953125 tile widths per sqrt-hour
```

Runtime sampling uses a versioned 256-direction antipodal table and an independent 256-entry magnitude-quantile table approximating a two-dimensional Gaussian random walk. The integer magnitude table is normalized so its discrete RMS equals the configured target; its maximum entry may not exceed `6 × FounderRadiusQ`. Runtime simulation performs no trigonometry, logarithm, or square root to draw the step.

Brownian RMS changes with current radius and medium:

```text
brownianRmsQ = FounderBrownianRmsQ
             * sqrt(FounderRadiusQ / currentBodyRadiusQ)
             * mediumBrownianMultiplier
             * lifecycleBrownianMultiplier
             * compiledEnvironmentalSpreadMultiplier
             * sqrt(tickDurationHours)
```

Authoritative square-root factors use checked integer square root or a versioned monotone lookup with declared rounding. Runtime platform floating point does not determine displacement.

The first aquatic active-lifecycle medium multiplier is `1.0`; the first terrestrial multiplier is `0.50` for an organism with the required occupation capability. Resistant dormancy may set the lifecycle multiplier to zero; ordinary dormancy provisionally uses `0.25`. The environmental-spread multiplier is a DNA effect and defaults to `1.0`; it is separate from medium and lifecycle state so later regulation can choose an attachment or dispersal state without pretending that the tile's physics changed. Exact terrestrial gates and the deliberately deferred weather coupling are in [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md).

For mature organization targets in the same aquatic state, Brownian RMS is approximately:

| Organization | RMS displacement/hour | Founder-radius units |
| --- | ---: | ---: |
| `PrimitiveCell` | `0.001953` | `2.000 R₀` |
| `IncreasedCellScaleI` | `0.001595` | `1.633 R₀` |
| `IncreasedCellScaleII` | `0.001302` | `1.333 R₀` |

Brownian movement remains phase-local and uncharged. It does not become persistent velocity or provide a sensed direction.

## Environmental-spread profiles

`EnvironmentalSpread` is the player-facing abstraction for how strongly an organism couples to unresolved, zero-mean environmental agitation. It modifies the calibrated Brownian-like component; it does not create a current, choose a direction, reveal neighboring conditions, or alter the probability of crossing an otherwise identical compatible edge a second time.

| Profile | MP / complexity | Passive RMS multiplier | Active-speed multiplier | Added upkeep/hour | Intended role |
| --- | ---: | ---: | ---: | ---: | --- |
| Free suspension | Inherent baseline | `1.00` | `1.00` | `0` | General default |
| `EnvironmentalAnchoring` | `40 / 1` | `0.25` | `0.50` | `2` | Retain local position and population clustering |
| `EnvironmentalDrifting` | `60 / 1` | `1.50` | `1.00` | `3` | Increase uncontrolled encounters and geographic spread |

The two acquired profiles are sibling alternatives and are mutually incompatible in v1. Anchoring is constitutive: if combined with locomotion, the active-speed penalty represents working against adhesion rather than cost-free attach/detach control. Drifting remains compatible with locomotion, but its random component is not cancelled by the desired active vector and therefore reduces fine positional control naturally. Added upkeep is constitutive and pays for adhesion, buoyancy, shape, or other maintained coupling machinery; passive distance itself remains uncharged.

The baseline one-hour `2 R₀` RMS therefore becomes `0.5 R₀` while anchored and `3 R₀` while drifting for a mature primitive aquatic organism. Mean squared displacement over a fixed period scales with the square of these multipliers: approximately `0.0625x` baseline for anchoring and `2.25x` for drifting. At equal scale, the calibrated random magnitude remains capped at `9 R₀` after the largest v1 multiplier.

`RegulatedAttachmentRelease` is a future node that may switch between resident and dispersive lifecycle states based on behavior or season. It must pay materially more than either constitutive profile and requires metabolic or behavioral regulation; v1 does not permit free per-tick switching.

# Interaction and sensing ranges

`R₀` below means the primitive founder radius. `Rₘ` means the acting species' compiled mature radius. Current body radii still participate where a formula explicitly sums entity sizes. Keeping sensing tiers in `R₀` units prevents large-cell traits from silently granting the longest sensing capability.

| Field or capability | First value | Primitive absolute range | Meaning |
| --- | ---: | ---: | --- |
| Contact margin | `0.25 × (RₘA + RₘB)` | `0.000488` for primitive peers | Small biochemical/contact reach beyond body surfaces |
| Equal-founder contact threshold | `2.5 R₀` | `0.002441` | `currentRadiusA + currentRadiusB + contactMargin` |
| `ContactPredation.captureReach` | `2.0 Rₘ` | `0.001953` for primitive predator | Equal-founder target is eligible within `3.0 R₀` center distance |
| `RemnantScavenging.feedingReach` | `2.0 Rₘ` | `0.001953` for primitive scavenger | Founder-sized remnant is eligible within `3.0 R₀` |
| `NearbyOrganismDetection` | `8 R₀` | `0.0078125` | Short-range organism detection |
| `RemnantDetection` | `16 R₀` | `0.015625` | Finds sparse cohesive remains |
| `PreyThreatDiscrimination` | `16 R₀` | `0.015625` | Extend relevant organism detection and classify prey/threat cues |
| `ExtendedRangeSensing` ceiling | `32 R₀` | `0.03125` | Maximum first-v1 within-tile entity-sensing radius |

For the primitive founder, the exact `LocalCoordQ` encodings are:

```text
body radius                 4,194,304   # 1 R₀
Brownian RMS                8,388,608   # 2 R₀
contact margin              2,097,152   # 0.5 R₀ between equal founders
equal-founder contact      10,485,760   # 2.5 R₀
capture/feeding reach       8,388,608   # 2 R₀ before adding target radius
nearby detection           33,554,432   # 8 R₀
remnant/threat detection   67,108,864   # 16 R₀
extended sensing ceiling  134,217,728   # 32 R₀
```

The `16 × 16` spatial-index bin width is `64 R₀`, so the longest v1 entity-sensing radius is half a bin width. Depending on its position inside a bin, a query normally touches at most four bins before exact filtering; larger-body contact/capture queries still use general bounding-box enumeration rather than relying on this convenience.

`ContactDetection` uses the applicable contact threshold rather than a separate circular sense. `ExtendedRangeSensing` doubles the inherited entity-sensing range but cannot exceed `32 R₀`. Chemical condition sensing remains tile-wide, and directional environmental sensing compares permitted neighboring-tile summaries; neither creates an entity interaction across a tile edge.

`PreyThreatDiscrimination` extends its inherited organism-detection query to `16 R₀` for organisms that can be classified through its declared visible cues. It does not reveal or classify unrelated hidden state at that distance.

Predation and scavenging use current target radius in their exact final threshold. Improving capture machinery may raise `captureReach` to `3 Rₘ` and then `4 Rₘ`, but it must pay corresponding structure, upkeep, or attempt-energy costs. Engulfment remains a contact action and does not inherit the longest capture reach.

# Reproduction placement

An offspring's center is placed at:

```text
spawnDistance = parentCurrentRadius
              + offspringInitialRadius
              + 0.25 × (parentMatureRadius + offspringMatureRadius)
```

For an equal founder split this is `2.5 R₀`, exactly the first contact threshold. The keyed direction makes the products adjacent but not forced to share a center. Boundary reflection keeps both in the same tile, and lack of rigid collision means other nearby entities cannot block reproduction.

# Active movement distance candidates

Active movement is intentionally faster and directional but remains local relative to a tile:

| Capability | MP / complexity | Speed (`R₀/hour`) | Acceleration (`R₀/hour²`) | Maximum heading change/hour | Energy per `R₀` before scale | Total locomotion upkeep/hour |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `ActiveMotility` | `60 / 1` | `4` | `2` | `90°` | `5` | `5` |
| `FlagellarPropulsion` | `100 / 2` | `8` | `4` | `90°` | `4` | `10` |
| `EfficientCruising` | `100 / 2` | `8` | `3` | `60°` | `2.5` | `15` |
| `SurfaceGliding` | `100 / 2` | `6` | `3` | `90°` | `2` | `10` |
| `BurstPropulsion` | `120 / 2` | `24` | `24` | `180°` | `10` | `15` |
| `AdvancedPropulsion` | `180 / 3` | `16` | `8` | `120°` | `3` | `20` |

The speed values preserve the earlier absolute displacements: `4`, `8`, `6`, `24`, and `16 R₀/hour` equal `1/256`, `1/128`, `3/512`, `3/128`, and `1/64` of a tile. They are ceilings, not default continuous speeds. Starting from rest may choose any heading but can add no more than the profile's acceleration. A moving organism first clamps heading change, then speed change, then affordable distance. `BurstPropulsion` can reverse and reach its ceiling in one tick; its high distance cost prevents continuous use.

`SurfaceGliding` requires both `ActiveMotility` and `WetSurfaceColonization`. Every terrestrial tile is an eligible surface in v1; lowland, highland, mountain, lithology, moisture band, and biological-mat presence do not add another hard tag. Current terrestrial activity still scales realized active movement. In an aquatic tile, the organism falls back to its inherited `ActiveMotility` speed, acceleration, turn, and distance cost rather than becoming immobile, while retaining the gliding profile's total `10/hour` locomotion upkeep. This makes it an amphibious specialization with a real carrying cost. Slope, roughness, sediment attachment, and biological surface classes remain future modifiers.

The active profile replaces its ancestor's speed, distance cost, and locomotion upkeep rather than adding all ancestor values. Other independently selected movement machinery remains additive only when its typed effect explicitly says so. `AdvancedPropulsion` requires `ProtoEukaryoticOrganization`; v1 accounts for its protein/membrane construction in the organism's organization structure rather than adding another micronutrient quota.

## Movement-energy scaling

Active movement pays for realized active displacement only:

```text
effectiveScaleRadiusQ = clamp(
    currentBodyRadius / FounderRadius,
    minimum = 1.0,
    maximum = compiledMatureRadius / FounderRadius)

bodyAreaMultiplierQ = square(effectiveScaleRadiusQ)

movementEnergy = ceil(
    realizedActiveDistanceInFounderRadii
    * activeProfile.energyPerFounderRadius
    * bodyAreaMultiplierQ
    * mediumCostMultiplierQ)
```

Brownian displacement remains free. The effective scale radius tracks actual construction during a scale transition but is clamped to the compiled phenotype's mature radius; transient pre-fission surplus therefore does not rewrite an inherited movement profile. The first open-water and ordinary terrestrial-medium movement-cost multiplier is `1.0`; `SurfaceGliding` uses `0.75` in a terrestrial tile and its inherited `ActiveMotility` profile with `1.0` in water. The ceiling is applied once to the complete tick cost, so splitting one movement vector into boundary segments cannot change the debit. An organism unable to afford the requested movement shortens its active vector to the greatest whole-coordinate distance whose complete rounded cost it can pay.

Using physical area rather than volume is a biology-inspired abstraction for drag and membrane propulsion. At mature Scale I and Scale II, the area multipliers are `2.25` and `5.0625`. Continuous maximum `AdvancedPropulsion` therefore costs:

| Body scale | Distance/hour | Movement energy/hour | Current radii/hour |
| --- | ---: | ---: | ---: |
| Primitive | `16 R₀` | `48` | `16.0` |
| Scale I (`1.5 R₀` radius) | `16 R₀` | `108` | `10.7` |
| Scale II (`2.25 R₀` radius) | `16 R₀` | `243` | `7.1` |

Larger cells retain the same absolute speed ceiling but travel fewer body lengths and pay more energy. `AdvancedPropulsion` is consequently useful rather than mandatory: a Scale-I `EfficientCruising` organism moving at `8 R₀/hour` pays `45/hour`, while the advanced profile pays `108/hour` only when it realizes the full doubled speed.

# Encounter-rate calculation

For `N` uniformly distributed point centers in a unit-area tile and a small center-distance threshold `d`, ignoring the less-than-one-percent first boundary correction:

```text
expected unordered pairs in range per snapshot
    = N × (N - 1) / 2 × π × d²
```

This measures pair opportunities in the post-movement snapshot, not guaranteed unique encounters or successful actions.

| Organisms in one tile | Contact pairs/tick at `2.5 R₀` | Contact pairs/day | Nearby-detection pairs/day at `8 R₀` |
| ---: | ---: | ---: | ---: |
| `100` | `0.0927` | `2.22` | `22.78` |
| `184` | `0.3153` | `7.57` | `77.48` |
| `1,000` | `9.3533` | `224.48` | `2,298.67` |

The `184` row is the average density of the 100,000-organism benchmark across 544 tiles. Real ecology will be clustered, so performance and encounter tests must include substantially denser local populations.

With 100 uniformly initialized founders, the versioned 256-direction/256-magnitude lookup described above, RMS Brownian step `2 R₀`, reflective boundaries, and post-movement contact at `2.5 R₀`, a 256-seed, 30-day exploratory calculation produced:

- Median `37` contact episodes, with seed tenth-to-ninetieth percentile `21..55`.
- Median `12` unique contacting pairs, with seed tenth-to-ninetieth percentile `9..16`.
- Median `66` contact-positive pair snapshots, or `2.20/day`, matching the analytic expectation.

For a uniformly mixed population of 100, the same Brownian scale produces approximately `5.29` total boundary intersections per day before habitat gates or passive-permeability probability. This is enough to make passive spread possible without making a successful cross-tile transition automatic. V1 selects `10%` success on a maximally compatible purely Brownian crossing, yielding approximately `0.529` passive migrations per 100-organism day across four compatible edges before population clustering and successful-migrant removal. Pure active control raises the base to `80%`; mixed movement interpolates by its actual outward-normal active share. Exact compatibility and transition composition are defined in [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md).

Boundary intersections are approximately linear in RMS displacement for a uniform population and small steps. The first environmental-spread hypotheses therefore produce:

| Profile | Raw boundary intersections per 100/day | Successful passive migrations at `10%` |
| --- | ---: | ---: |
| Anchored `0.25x` | `1.32` | `0.132` |
| Baseline `1.00x` | `5.29` | `0.529` |
| Drifting `1.50x` | `7.94` | `0.794` |

These are first-order uniform-density estimates, not golden simulation outputs. Anchoring and drifting will change clustering, encounter persistence, predation, scavenging, and reproduction neighborhoods, so the executable population fixture must measure those emergent effects rather than scaling every result linearly.

## Migration and terrestrial-movement authoring fixture

The reproducible [terrestrial_migration_calibration.py](calibration/terrestrial_migration_calibration.py) combines the selected boundary probabilities with the existing Brownian table. It is an authoring model rather than engine code: it does not remove successful migrants or simulate reproduction, survival, resource contention, or behavior selection.

Across 64 seeds, 100 uniformly placed organisms, and 30 days, its median passive results are:

| Profile | Aquatic raw intersections/day | Aquatic successes/day | Terrestrial raw intersections/day | Terrestrial successes/day | Successes/day through one selected wet shore |
| --- | ---: | ---: | ---: | ---: | ---: |
| Anchored | `1.250` | `0.125` | `0.650` | `0.065` | `0.023` |
| Baseline | `5.283` | `0.528` | `2.700` | `0.270` | `0.099` |
| Drifting | `7.950` | `0.795` | `4.100` | `0.410` | `0.149` |

The single-shore column assigns one quarter of aquatic intersections to the chosen edge and applies the maximally compatible `0.10 × 0.75` passive shoreline probability. It shows that population-scale passive landfall is possible but slow and uncontrolled. It is not the expected time for one individual, and a real population changes the rate through clustering, birth, death, edge occupancy, and successful-migrant removal.

For a directed organism beginning at tile center, the same fixture includes discrete acceleration and the expected additional failed attempts after first reaching an edge:

| Scenario | Effective speed | Probability/attempt | Expected center-to-success time |
| --- | ---: | ---: | ---: |
| Water to wet land, basal motility | `4 R₀/hour` | `60%` | `129.67 h` |
| Wet land to land, basal motility | `4 R₀/hour` | `80%` | `129.25 h` |
| Wet land to land, surface gliding | `6 R₀/hour` | `80%` | `86.25 h` |
| Moisture `0.40` land, basal wet-surface phenotype | `1.75 R₀/hour` | `35%` | `295.86 h` |
| Moisture `0.40` land, gliding wet-surface phenotype | `2.625 R₀/hour` | `35%` | `197.86 h` |
| Moisture `0.40` land, intermittent-tolerant glider | `6 R₀/hour` | `80%` | `86.25 h` |

These values justify keeping `SurfaceGliding` distinct from generic motility: it makes controlled land expansion materially faster, while physiological moisture tolerance—not locomotion—determines whether that speed and high crossing probability remain available as the surface dries.

## Environmental-spread exploratory simulation

The reproducible authoring model [environmental_spread_sim.py](calibration/environmental_spread_sim.py) ran 64 seeds with 100 fixed organisms for 30 simulated days. It uses a 256-direction antipodal table, a separately sampled 256-entry Rayleigh-quantile magnitude table normalized to exactly `2 R₀` baseline RMS, hourly movement, reflective boundaries, the `2.5 R₀` contact threshold, and a stationary center remnant with `3 R₀` access. Common random-number streams reduce comparison noise between profiles.

Two layouts isolate different questions. `uniform` uses the normal founder-placement rule and validates edge and chance-encounter behavior. `clustered` is a deliberate stress fixture placing organisms uniformly within `16 R₀` of tile center to approximate a reproduction-built colony and expose local persistence. The model omits active movement, reproduction during the run, death, consumption, predation resolution, resource effects, and removal after successful migration; it therefore measures spatial opportunity rather than fitness.

Uniform results are medians across seeds; parentheses show the seed 10th-to-90th percentile:

| Profile | Boundary intersections/day | Contact episodes/30 days | Unique contacted pairs | Snapshots/contacted pair |
| --- | ---: | ---: | ---: | ---: |
| Anchored `0.25x` | `1.25 (0.39..2.81)` | `5.5 (0.0..20.7)` | `1 (0..3.7)` | `11.75 (0.00..66.25)` |
| Anchored sensitivity `0.50x` | `2.70 (1.40..4.83)` | `16.0 (3.3..31.7)` | `4 (2..7)` | `12.47 (5.58..21.04)` |
| Baseline `1.00x` | `5.28 (3.51..8.16)` | `36.0 (19.0..59.7)` | `12 (8..17.7)` | `5.11 (3.43..7.45)` |
| Drifting `1.50x` | `7.95 (5.64..11.54)` | `50.0 (32.9..62.7)` | `21 (16..27.7)` | `3.02 (2.37..3.93)` |

The baseline reproduces the earlier 30-day medians of approximately 37 contact episodes and 12 unique pairs. Anchoring sharply reduces discovery of new partners but, when a contact exists, tends to preserve it for more snapshots. Drifting finds more unique partners while retaining each for less time.

Clustered results are likewise medians; the retained fraction measures organisms ending within the initial `16 R₀` colony radius:

| Profile | Final RMS radius | Retained fraction | Contact-pair snapshots/day | Unique contacted pairs | Snapshots/contacted pair | Center-remnant contacts/hour |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Anchored `0.25x` | `17.43 R₀` | `55%` | `1,644` | `1,206` | `41.06` | `3.14` |
| Anchored sensitivity `0.50x` | `29.11 R₀` | `26%` | `924` | `1,774` | `15.63` | `2.06` |
| Baseline `1.00x` | `54.98 R₀` | `8%` | `398` | `2,089` | `5.66` | `0.89` |
| Drifting `1.50x` | `81.01 R₀` | `3%` | `222` | `2,041` | `3.25` | `0.50` |

The selected `0.25x` anchor is intentionally strong: relative to baseline it retains about 6.9 times the fraction of the synthetic colony, produces about 4.1 times as many local contact snapshots, and supplies about 3.5 times as many opportunities to interact with a stationary central remnant. It is not a universal advantage. It encounters roughly 42% fewer unique partners, produces about one quarter as many uniform-layout edge intersections, pays constitutive upkeep, and halves any acquired active speed. Finite remnants, scavenging cooldown, same-species predation exclusion, hostile competitors, and tile-wide resource contention should prevent repeated proximity from becoming repeated free yield.

The `0.50x` sensitivity profile produces a milder but still visible resident niche. V1 retains `0.25x` provisionally because within-tile position affects only entity interactions and migration; a weak anchoring effect risks disappearing beneath reproduction, mortality, and resource variance. This is not yet a frozen balance decision. The next coupled fixture must add reproduction, finite remnant depletion, scavenging cooldown, predation, successful-migrant removal, and resource-limited population dynamics before confirming the `40 MP`, two-upkeep price or the multiplier.

As an order-of-magnitude comparison, an organism beginning near a tile center must cover roughly `512 R₀` to reach an edge. Equating that distance to random-walk RMS gives about 120 years for the anchored profile, 7.5 years at baseline, and 3.3 years for the drifting profile without reproduction. Basal active motility moving directly at `4 R₀/hour` covers the same distance in about 128 hours. These are explanatory scale estimates rather than first-passage guarantees; population growth supplies many simultaneous passive trials and is therefore central to the passive strategy.

# Acceptance targets

- A uniform 100-founder isolated tile produces `1..4` contact-positive pair snapshots per day averaged across at least 30 days.
- The 30-day median unique contacting-pair count remains in `8..20`; chance interaction exists but does not approximate global mixing.
- `NearbyOrganismDetection` produces approximately `15..35` pair opportunities per 100-organism day before capability and classification filters.
- The complete Brownian table has exact zero signed expectation per axis and configured RMS within one `LocalCoordQ` unit after integer normalization.
- A maximally compatible purely Brownian boundary attempt succeeds at exactly `10%`; a fully controlled active attempt succeeds at exactly `80%`, and mixed attempts interpolate without a threshold discontinuity.
- A maximally compatible aquatic-to-terrestrial attempt succeeds at exactly `7.5%` when purely Brownian and `60%` when fully controlled. At destination moisture `0.40`, a minimum wet-surface phenotype instead receives `3.28125%` and `26.25%`; intermittent tolerance restores the favorable values.
- Anchored, baseline, and drifting populations preserve zero signed displacement expectation while reproducing their configured `0.25`, `1.0`, and `1.5` RMS multipliers.
- The first 100-organism uniform fixture keeps anchored, baseline, and drifting raw boundary intersections near `1.32`, `5.29`, and `7.94` per day before crossing admission; active directed migration remains more reliable than every passive profile.
- No v1 entity-sensing or action range exceeds `1/32` of a tile without a new spatial-index and gameplay review.
- At the uniform 100,000-organism benchmark, exact-distance filtering examines far fewer candidates than a tile-wide scan; clustered 1,000- and 10,000-organism tiles receive separate worst-case measurements.

# Recalibration triggers

Rerun analytic, Monte Carlo, population, and performance fixtures after changing:

- Tile-local scale, tick duration, body radius, geometric or total structure targets, body-scale traits, organization traits, or radius derivation.
- Brownian direction/magnitude tables, size/medium/lifecycle/environmental-spread multipliers, or boundary response.
- Spawn distance, sensing ranges, capture/feeding reach, or spatial-bin dimensions.
- Active movement speed, movement energy, turning, migration permeability, or behavior selection.
- Predation, scavenging, reproduction, remnant decay, population density, or maximum organism count.
- Any localized resource field that makes position affect environmental acquisition.

# Remaining decisions

- Final capture-reach upgrades after testing the first size-compatibility, hunting, feeding, and engulfment profiles in [PREDATION.md](PREDATION.md).
- Dormant and later directional current-driven displacement fixtures; the first terrestrial, anchoring, drifting, migration, and surface-gliding values are now specified.
- Whether the `MiniaturizedCell` future branch is needed after founder encounter and scarcity playtests.

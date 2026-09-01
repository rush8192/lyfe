# Spatial Numerical Calibration

Status: provisional v1 founder-scale, Brownian, sensing, interaction, placement, and active-speed rule-pack candidate; clustered population fixtures required before freezing

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
            * cbrt(currentViableStructure / compiledMatureStructure)
            * authoredShapeMultiplier

Primitive compiledMatureRadiusQ = FounderRadiusQ
Primitive compiledMatureStructure = 1,000
```

The authoritative implementation uses a versioned monotone fixed-point lookup or integer cube-root routine, not runtime floating point. Radius is derived and rounded to nearest with ties to even. A nonempty living organism has a minimum radius of `0.25 × FounderRadiusQ`; configured body-scale traits impose the upper bound.

The first organization targets are:

| Cellular organization | Mature structure target | Mature-radius multiplier |
| --- | ---: | ---: |
| `PrimitiveCell` | `1,000` | `1.00×` |
| `IncreasedCellScaleI` | `3,375` | `1.50×` |
| `IncreasedCellScaleII` | `11,391` | `2.25×` |

The second structure target rounds `1,000 × 2.25³ = 11,390.625` upward. Because both numerator and compiled target change, an existing `1,000`-structure organism that speciates into `IncreasedCellScaleI` initially retains approximately its old physical radius and must actually assemble the additional matter to reach `1.50×`. These targets express the geometric cost of scale; metabolic throughput, storage, ingestion, maintenance, and reproduction must be recalibrated so larger cells gain useful ceilings rather than only a longer growth bar.

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
             * sqrt(tickDurationHours)
```

Authoritative square-root factors use checked integer square root or a versioned monotone lookup with declared rounding. Runtime platform floating point does not determine displacement.

The first aquatic active-lifecycle medium multiplier is `1.0`. Attachment and resistant dormancy may set the lifecycle multiplier to zero; ordinary dormancy provisionally uses `0.25`. Terrestrial and surface-associated multipliers remain tied to their future movement fixtures.

For mature organization targets in the same aquatic state, Brownian RMS is approximately:

| Organization | RMS displacement/hour | Founder-radius units |
| --- | ---: | ---: |
| `PrimitiveCell` | `0.001953` | `2.000 R₀` |
| `IncreasedCellScaleI` | `0.001595` | `1.633 R₀` |
| `IncreasedCellScaleII` | `0.001302` | `1.333 R₀` |

Brownian movement remains phase-local and uncharged. It does not become persistent velocity or provide a sensed direction.

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
| `PreyThreatDiscrimination` | `16 R₀` | `0.015625` | Classification range after organism detection |
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

| Capability | Maximum active displacement/hour | Founder-radius units | Directed time from tile center to an edge |
| --- | ---: | ---: | ---: |
| `ActiveMotility` | `1/256 = 0.003906` tile | `4 R₀` | `128 h` |
| `FlagellarPropulsion` | `1/128 = 0.007812` tile | `8 R₀` | `64 h` |
| `EfficientCruising` | `1/128 = 0.007812` tile | `8 R₀` | `64 h`, lower energy cost |
| `SurfaceGliding` | `3/512 = 0.005859` tile | `6 R₀` | `85.3 h` on eligible surfaces |
| `BurstPropulsion` | `3/128 = 0.023438` tile | `24 R₀` | `21.3 h`, high short-lived cost |
| `AdvancedPropulsion` | `1/64 = 0.015625` tile | `16 R₀` | `32 h` before body-scale modifiers |

These are displacement ceilings, not default continuous speeds. Behavior, acceleration/turning limits, health, environment, reserve affordability, and action intent determine realized movement. Energy costs and turning values remain the next calibration because they depend on the internal energy economy and behavior state machine.

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

For a uniformly mixed population of 100, the same Brownian scale produces approximately `5.29` total boundary intersections per day before habitat gates or passive-permeability probability. This is enough to make passive spread possible without making a successful cross-tile transition automatic. The migration pass should initially target approximately `5%..15%` success on a maximally compatible purely Brownian crossing, yielding roughly `0.26..0.79` passive migrations per 100-organism day before population clustering and edge effects.

# Acceptance targets

- A uniform 100-founder isolated tile produces `1..4` contact-positive pair snapshots per day averaged across at least 30 days.
- The 30-day median unique contacting-pair count remains in `8..20`; chance interaction exists but does not approximate global mixing.
- `NearbyOrganismDetection` produces approximately `15..35` pair opportunities per 100-organism day before capability and classification filters.
- The complete Brownian table has exact zero signed expectation per axis and configured RMS within one `LocalCoordQ` unit after integer normalization.
- A compatible purely Brownian boundary attempt succeeds rarely enough that active locomotion remains the reliable migration strategy.
- No v1 entity-sensing or action range exceeds `1/32` of a tile without a new spatial-index and gameplay review.
- At the uniform 100,000-organism benchmark, exact-distance filtering examines far fewer candidates than a tile-wide scan; clustered 1,000- and 10,000-organism tiles receive separate worst-case measurements.

# Recalibration triggers

Rerun analytic, Monte Carlo, population, and performance fixtures after changing:

- Tile-local scale, tick duration, body radius, structure targets, body-scale traits, or radius derivation.
- Brownian direction/magnitude tables, size/medium/lifecycle multipliers, or boundary response.
- Spawn distance, sensing ranges, capture/feeding reach, or spatial-bin dimensions.
- Active movement speed, movement energy, turning, migration permeability, or behavior selection.
- Predation, scavenging, reproduction, remnant decay, population density, or maximum organism count.
- Any localized resource field that makes position affect environmental acquisition.

# Remaining decisions

- Final active-movement energy per distance, acceleration, and turning limits.
- Exact active and passive migration probability curves and costs within the proposed `5%..15%` compatible Brownian range.
- Capture-reach upgrades, ingestion timing, and size-compatibility curves for predation/scavenging traits.
- Terrestrial, surface-attached, dormant, and later current-driven displacement fixtures.
- Whether the `MiniaturizedCell` future branch is needed after founder encounter and scarcity playtests.

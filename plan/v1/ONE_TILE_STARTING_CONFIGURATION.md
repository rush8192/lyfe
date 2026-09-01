# One-Tile Volcanic Starting Configuration

Status: first balance fixture; accounting values concrete, gameplay values provisional

Sources: [resource model](RESOURCE_MODEL.md), [resource calibration](RESOURCE_CALIBRATION.md), [world plan](WORLD_AND_CLIMATE.md), and [organism plan](ORGANISMS.md).

# Purpose

Define one deterministic volcanic-ocean configuration that can found and sustain primitive hydrogen-based life, exercise the resource ledger, and expose the pressures that should eventually drive migration and evolution. This is the hydrogen half of the opening balance work, not the complete Survival setup; [SULFUR_TILE_STARTING_CONFIGURATION.md](SULFUR_TILE_STARTING_CONFIGURATION.md) defines the complementary sulfur fixture and [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md) defines the paired starting region.

The tile is intentionally not a universal refuge. It combines plentiful primitive metabolic substrates with:

- Harmful hydrogen sulfide and sulfur dioxide exposure.
- Low or absent micronutrients needed by selected advanced pathways.
- A finite stock of easily assimilated nitrogen.
- Warm, volcanically unstable, dim aquatic conditions.

The values in this document form a test and early-balance fixture. They do not define every eligible starting tile produced by world generation.

# Design goals

The fixture should:

1. Keep 100 adapted founders healthy enough to grow.
2. Permit the first reproduction wave in roughly two simulated weeks under expected conditions.
3. Make hydrogen the first population-scale limiting substrate.
4. Let fixed nitrogen become a slower strategic pressure.
5. Make unadapted organisms lose substantial energy and face rapid death from sulfur exposure.
6. Prevent immediate use of canonical oxygenic photosynthesis, nitrogen fixation, and copper-dependent respiration.
7. Preserve exact CHNOPS and micronutrient accounting.
8. Settle toward comprehensible gas levels in the absence of biological demand.
9. Remain far below the numeric limits in [RESOURCE_CALIBRATION.md](RESOURCE_CALIBRATION.md).

# Fixture boundaries

- One aquatic tile; neighbor exchange and migration are disabled.
- One-hour ticks.
- One founding species and 100 organisms.
- Mutation, autonomous speciation, predation, and rare catastrophes are disabled for the first ledger fixture.
- Reproduction, starvation, senescence, death, remains, and decomposition are enabled.
- Climate is held at its baseline initially so resource and toxicity behavior can be isolated.
- All random outcomes still use deterministic keyed draws.

# Micronutrient catalogue extension

The original catalogue lacks several especially useful biological gates. Add four single-form bioavailable micronutrients in v1:

| Micronutrient | V1 role |
| --- | --- |
| Manganese | Required by the canonical oxygen-evolving complex of oxygenic photosynthesis |
| Molybdenum | Required by the canonical molybdenum nitrogenase path |
| Nickel | Required by the founding hydrogen/acetogenesis abstraction and later nickel-dependent gas-processing enzymes |
| Cobalt | Required by the founding acetogenesis abstraction and later cobalamin-associated pathways |

These join calcium, iron, potassium, sodium, magnesium, zinc, copper, iodine, fluoride, and selenium. All fourteen remain single-form tile resources in v1.

Alternative metal pathways exist in nature. V1 deliberately uses one canonical trait path for each major capability; alternative nitrogenases, hydrogenases, and photosystems can become later evolutionary branches.

# Fixed tile configuration

| Attribute | Fixture value | Purpose |
| --- | ---: | --- |
| Terrain | Aquatic volcanic ocean | Eligible starting terrain |
| Elevation | `-200 m` | Moderately deep water |
| Water depth | `200 m` | Derived from elevation |
| Mean temperature | `45 °C` | Warm enough to reward primitive heat tolerance |
| Temperature variation | Disabled in first fixture | Isolate resources before weather |
| Insolation at organism depth | `0.15` normalized | Makes later photosynthesis possible but inefficient |
| Volcanic activity | `0.80` normalized | Sustains local gas sources |
| Gas exposure multiplier | `1.00` | Makes fixture thresholds directly readable |

The exact physical interpretation of normalized insolation, volcanic activity, and gas exposure belongs in the world/climate deep dive.

# Atmospheric configuration

Sources apply first, then attrition/transformation applies to the post-source balance, then biological claims resolve.

Neighbor exchange is disabled only for this bounded fixture. Full worlds insert the gas exchange phase between attrition and biological claims using [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).

```text
postSource = startingAmount + sourcePerTick
attrition = floor(postSource * attritionRate + persistedRemainder)
availableToBiology = postSource - attrition
```

| Gas | Starting amount | Source/tick | Attrition/tick | First role |
| --- | ---: | ---: | ---: | --- |
| H₂ | 50,000,000 | 250,000 | 0.25% | Founding energy/carbon-fixation substrate |
| CO₂ | 100,000,000 | 150,000 | 0.02% | Founding carbon substrate |
| H₂S | 20,000,000 | 100,000 | 0.50% | Sulfur source, founding sulfide-phototrophy substrate in the paired fixture, and toxin |
| SO₂ | 5,000,000 | 50,000 | 1.00% | Short-lived volcanic toxin/sulfite precursor |
| CH₄ | 2,000,000 | 4,000 | 0.20% | Geological carbon/hydrogen reservoir |
| N₂ | 500,000,000 | 0 | 0 | Stable but initially unusable nitrogen reservoir |
| NH₃ | 10,000,000 | 5,000 | 0.05% | Accessible fixed-nitrogen source |
| O₂ | 0 | 0 | 0.02% when present | Future biological output; absent initially |

The N₂ and CO₂ starting amounts are the median global-background targets for generated worlds. Generated scenarios may vary N₂ by ±10% and CO₂ by ±25% at the world level; deterministic fixture calculations retain the exact medians. Because this tile's volcanic emission profile emits the founding gases, aquatic gas accessibility is `1.00` despite its `200 m` depth.

Approximate no-biology equilibria for sourced, attriting gases are `source × (1-rate) / rate` under this phase order:

| Gas | Approximate equilibrium |
| --- | ---: |
| H₂ | 99,750,000 |
| CO₂ | 749,850,000 |
| H₂S | 19,900,000 |
| SO₂ | 4,950,000 |
| CH₄ | 1,996,000 |
| NH₃ | 9,995,000 |

Biological demand lowers those equilibria. If demand exceeds the post-attrition source contribution, the gas eventually depletes.

These are isolated, exchange-disabled equilibria. In a generated world, neighbor leakage lowers a lone volcanic tile's local plateau and creates smaller surrounding stocks as calculated in [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).

# Generic macronutrient pools

## Before abiogenesis

| Resource | Organic | Inorganic |
| --- | ---: | ---: |
| Carbon | 20,000,000 | 50,000,000 |
| Hydrogen | 30,000,000 | 0 |
| Nitrogen | 5,000,000 | 20,000,000 |
| Oxygen | 10,000,000 | 20,000,000 |
| Phosphorus | 500,000 | 5,000,000 |
| Sulfur | 250,000 | 20,000,000 |

Water is the inexhaustible H/O boundary; the zero inorganic-hydrogen balance does not mean the tile lacks water.

## After founding-organism construction

The abiogenesis transaction described below consumes organic precursor matter. Remaining organic pools are:

| Resource | Remaining organic amount |
| --- | ---: |
| Carbon | 9,500,000 |
| Hydrogen | 12,000,000 |
| Nitrogen | 3,000,000 |
| Oxygen | 5,500,000 |
| Phosphorus | 300,000 |
| Sulfur | 150,000 |

Inorganic pools are unchanged by the founding event.

For the isolated fixture, the only ongoing generic-pool boundary input is 250 inorganic-phosphorus units per tick from weathering. Organic pools receive no abiotic replenishment after setup; death and decomposition are their first continuing biological inputs.

# Micronutrient configuration

| Micronutrient | Pre-abiogenesis amount | Ongoing source/tick | Ecological intent |
| --- | ---: | ---: | --- |
| Calcium | 50,000 | 5 | Scarce; advanced structural/photosynthetic demand matters |
| Iron | 2,000,000 | 100 | Volcanically abundant; supports primitive redox chemistry |
| Potassium | 3,000,000 | 25 | Available baseline cellular requirement |
| Sodium | 10,000,000 | 100 | Abundant aquatic ion |
| Magnesium | 5,000,000 | 50 | Available baseline cofactor |
| Zinc | 1,000 | 0 | Very scarce; constrains zinc-heavy advanced traits |
| Copper | 0 | 0 | Blocks canonical copper-dependent respiration |
| Iodine | 0 | 0 | Unavailable; no founding requirement |
| Fluoride | 100,000 | 0 | Present but not a universal requirement |
| Selenium | 500 | 0 | Rare specialized cofactor |
| Manganese | 0 | 0 | Blocks canonical oxygenic photosynthesis |
| Molybdenum | 0 | 0 | Blocks canonical nitrogen fixation |
| Nickel | 500,000 | 25 | Supports founding hydrogen processing |
| Cobalt | 100,000 | 5 | Supports founding acetogenesis abstraction |

An absent resource has a real zero balance. It is not silently supplied by water, volcanism, or a generic micronutrient pool.

Founder construction debits the required quotas, leaving:

| Micronutrient | Post-abiogenesis amount |
| --- | ---: |
| Iron | 1,998,000 |
| Nickel | 499,500 |
| Cobalt | 99,800 |
| Magnesium | 4,999,000 |
| Potassium | 2,999,000 |
| Sodium | 9,999,000 |

All other micronutrient balances are unchanged. Quotas are per organism, not proportional to every structural unit: a growing parent must acquire one additional complete quota set before it can create an offspring.

# Founding species configuration

## Initial population

| Value | Per organism | Population total |
| --- | ---: | ---: |
| Organisms | 1 | 100 |
| Structural biomass | 1,000 | 100,000 |
| Reserve organic/energy | 5,000 | 500,000 |
| Reserve capacity | 10,000 | 1,000,000 |
| Dissolved-macronutrient store | 0 free / 512 load capacity | 0 free / 51,200 aggregate capacity |
| Free-micronutrient store | 0 free / 64 load capacity | 0 free / 6,400 aggregate capacity |
| Ingested-matter buffer | 0 free / 0 capacity | 0 free / 0 aggregate capacity |
| Lifecycle phase | Mature | — |
| Initial health before stress adjustment | 50% | 50% average |

Positions are deterministic random samples within the tile, separated enough to avoid immediate overlap. Velocity begins at zero or a small deterministic random value according to the eventual locomotion baseline.

Under the first complete health calibration, the initial SO₂ exposure contributes a `0.966667` environmental factor while all other non-energy factors are neutral. Initial derived health is therefore exactly `0.483334`; see [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md). The 50% table value remains the pre-stress reserve fraction.

## Required capabilities

- Hydrogen acetogenesis reference metabolism.
- Assimilation of ammonia, inorganic phosphate, and sulfide into biomass.
- `PrimitiveNutrientStore`, using the founder capacities and needs-only retention policy.
- `VolcanicSulfurTolerance I` with a `10×` H₂S/SO₂ threshold multiplier.
- Warm-water tolerance centered near `45 °C`.
- True-split reproduction.
- No locomotion, sensing, predation, photosynthesis, nitrogen fixation, or respiration.

The founding hydrogen metabolism compiles an anabolic-throughput cap of three structural-assembly extents per tick. The sulfur founder's declared higher opening efficiency raises this to four while retaining the same biomass recipe, reproduction mode, reserve capacity, and founding endowment; see [SULFUR_TILE_STARTING_CONFIGURATION.md](SULFUR_TILE_STARTING_CONFIGURATION.md).

The sulfur-tolerance capability carries an ongoing efficiency cost. The fixture represents this by reducing expected metabolic reaction extent from the 250-per-tick maximum to 200 under otherwise favorable conditions.

## Structural micronutrient quotas

| Micronutrient | Minimum per organism |
| --- | ---: |
| Iron | 20 |
| Nickel | 5 |
| Cobalt | 2 |
| Magnesium | 10 |
| Potassium | 10 |
| Sodium | 10 |

All other founding quotas are zero. The complete committed set has a load of `57` micronutrient units. These quantities transfer with the organism and move to a remnant on death. Reproduction requires a second complete set; the primitive desired-inventory policy therefore attempts to accumulate at most one additional `57`-unit set in the free-micronutrient store, leaving seven units of headroom. Surplus free micronutrients do not raise health.

# Abiogenesis initialization transaction

Founding organisms do not appear without accounting. World setup runs one special atomic `AbiogenesisInitialization` transaction after tile selection and before tick zero.

For 100 founders it consumes generic organic precursor matter:

| Element | Founder structure | Founder reserves | Total debit |
| --- | ---: | ---: | ---: |
| C | 10,000,000 | 500,000 | 10,500,000 |
| H | 17,000,000 | 1,000,000 | 18,000,000 |
| N | 2,000,000 | 0 | 2,000,000 |
| O | 4,000,000 | 500,000 | 4,500,000 |
| P | 200,000 | 0 | 200,000 |
| S | 100,000 | 0 | 100,000 |

It also transfers the committed structural/catalytic micronutrient quotas from the tile and receives energy from the `LightningAbiogenesis` boundary. At least 500,000 energy units become founding reserves; configured assembly work dissipates separately. Abiogenesis does not prefill either available-store group: free macronutrients and the additional reproduction-quota set must be acquired during play.

```text
InitializeFounders(tile, speciesDNA, count):
    calculate complete structure, reserve, and micronutrient requirements
    validate tile accounts and founding eligibility
    debit generic organic precursors and tile micronutrients atomically
    create organisms with deterministic positions
    credit structure, reserves, and quotas
    record LightningAbiogenesis energy input and dissipated assembly work
    reconcile all matter before accepting tick zero
```

# Reference organism reactions

## Hydrogen acetogenesis

For one reaction extent:

```text
4 H2 + 2 CO2
    -> 2 ReserveOrganic + 2 H2O(boundary)

gross energy opportunity = 3
stored energy = 2
dissipated energy = 1
```

The organism has a maximum 250 extents per tick and an expected 200 under the fixture's favorable substrate, temperature, and tolerance conditions.

## Biomass assembly

One structural unit is assembled from:

```text
100 ReserveOrganic
+ 20 NH3
+ 2 InorganicPhosphorus
+ 1 H2S
    -> 1 StructuralBiomass
     + 46 H2O(boundary)
     + 14 OrganicOxygen
```

Element check:

```text
inputs:  C100 H262 N20 O100 P2 S1
outputs: StructuralBiomass(C100 H170 N20 O40 P2 S1)
       + water(H92 O46)
       + OrganicOxygen(O14)
```

All 100 stored-energy units carried by the consumed reserve are spent on assembly and dissipate. The founding DNA permits at most three structural assembly extents per tick.

## Maintenance

- Base maintenance: 50 energy per tick.
- Expected initial sulfur soft-stress cost: 6 energy per tick.
- Other environmental stress cost: zero under fixture baseline.

Spending reserve converts each CH₂O carrier into generic organic C/H/O products. Founder organisms release those products directly to the tile organic pool at the end of internal metabolism rather than retaining them in their dissolved-macronutrient store.

# Expected first-tick flows

Assume all 100 organisms receive 200 hydrogen-metabolism extents and three biomass-assembly extents.

## Gas balances

| Gas | After source/attrition | Biological debit | End of tick |
| --- | ---: | ---: | ---: |
| H₂ | 50,124,375 | 80,000 | 50,044,375 |
| CO₂ | 100,129,970 | 40,000 | 100,089,970 |
| H₂S | 19,999,500 | 300 | 19,999,200 |
| SO₂ | 4,999,500 | 0 | 4,999,500 |
| CH₄ | 1,999,992 | 0 | 1,999,992 |
| N₂ | 500,000,000 | 0 | 500,000,000 |
| NH₃ | 9,999,998 | 6,000 | 9,993,998 |
| O₂ | 0 | 0 | 0 |

Inorganic phosphorus changes from 5,000,000 to 4,999,650 after 250 units of weathering input and 600 units of biomass demand. The biomass inputs stage through each organism's dissolved-macronutrient store at a peak load of `255` and are fully consumed in the same tick.

Maintenance and stress spending release `5,600` organic carbon, `11,200` organic hydrogen, and `5,600` organic oxygen across the population. Biomass assembly releases another `4,200` organic oxygen and sends `13,800` water units to the ocean-water boundary. The end-of-phase tile organic credits are therefore `C = 5,600`, `H = 11,200`, and `O = 9,800`; founder macronutrient stores return to zero after this exact needs-only flow.

Primitive micronutrient uptake runs alongside these flows. Each organism with an incomplete extra quota set has one `0.5`-probability opportunity to claim one required micronutrient quantum, for `50` expected claims across the founding population on an abundant uncontested tick. The exact first-tick count and resource distribution are seed-keyed rather than forced to their expectation, and all granted quantities enter the free-micronutrient store.

One complete additional set costs `57` units per organism or `5,700` across the founding population. Under uninterrupted full grants, expected completion is `114` ticks, or 4 days 18 hours. The probability that keyed opportunity variance alone leaves a founder incomplete at the 334-tick structural reproduction deadline is approximately `8.1 × 10^-37`. Scarcity and ordinary contention can still delay it. No marginal uptake energy is added to the organism table because primitive passive-uptake cost is included in base maintenance.

Spread over 114 expected ticks, the population's quota demand averages approximately `17.54` iron, `4.39` nickel, `1.75` cobalt, and `8.77` each of magnesium, potassium, and sodium per tick. Their fixture sources are respectively `100`, `25`, `5`, `50`, `25`, and `100` per tick, in addition to large starting pools. Micronutrients therefore should not displace hydrogen or fixed nitrogen as the intended opening constraints on this eligible tile.

## Organism totals

| Flow | Per organism | Population |
| --- | ---: | ---: |
| Reserve produced | 400 | 40,000 |
| Metabolic energy dissipated | 200 | 20,000 |
| Base maintenance spent | 50 | 5,000 |
| Sulfur stress spent | 6 | 600 |
| Reserve spent on biomass | 300 | 30,000 |
| Net stored-energy change | +44 | +4,400 |
| Structural growth | +3 | +300 |

At this rate, an organism reaches 2,000 structural units in approximately 334 ticks, or 13.9 days. Reserve reaches its 10,000-unit capacity earlier, after which metabolism reduces extent to the amount needed for assembly, maintenance, and recovery. The first rule's 500-energy reproduction cost and 4,000-per-result reserve floor then permit a viable true split; exact gates and allocation are defined in [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).

The calculation is an expected-flow fixture, not a promise that every probabilistic tick has identical results.

# Sulfur exposure and health stress

H₂S can be useful at low or adapted exposures and harmful at high exposures. SO₂ and its aqueous sulfite/bisulfite products are modeled as a separate, generally harmful stress. V1 does not model pH or sulfur speciation directly.

## Base unadapted thresholds

| Stressor | Soft threshold | Hard threshold |
| --- | ---: | ---: |
| H₂S | 2,000,000 | 8,000,000 |
| SO₂ | 250,000 | 1,000,000 |

Effective thresholds are multiplied by DNA tolerance. `VolcanicSulfurTolerance I` uses `10×`, giving:

| Stressor | Adapted soft | Adapted hard | Fixture exposure |
| --- | ---: | ---: | ---: |
| H₂S | 20,000,000 | 80,000,000 | 20,000,000 |
| SO₂ | 2,500,000 | 10,000,000 | 5,000,000 |

## Soft stress

For an exposure between soft and hard thresholds:

```text
severity = clamp((exposure - soft) / (hard - soft), 0, 1)
extraMaintenance = ceil(baseMaintenance * severity^2)
```

At the initial adapted exposure, H₂S adds no cost and SO₂ severity is `1/3`, adding 6 energy after rounding. Soft costs from multiple stressors add.

## Hard stress and death

Above the hard threshold, each stressor receives an independent keyed death draw:

```text
overage = (exposure - hard) / hard
deathChancePerMillion =
    min(500_000, round(10_000 * (1 + overage)^2))
```

At the initial tile levels, an unadapted organism has approximately:

- 6.25% H₂S death chance per hourly tick.
- 25% SO₂ death chance per hourly tick.
- About 29.7% chance that at least one succeeds, assuming independent keyed draws.

Every successful trigger is recorded. An organism may therefore die with both `HydrogenSulfideToxicity` and `SulfurDioxideToxicity`, plus starvation or another simultaneous trigger.

The probability shape and thresholds are provisional balance data. The separate soft-cost and hard-death mechanisms are architectural decisions.

# Why advanced life should leave or adapt

| Capability | Required micronutrient gate | Fixture result |
| --- | --- | --- |
| Founding hydrogen acetogenesis | Fe, Ni, Co | Supported |
| Canonical oxygenic photosynthesis | Mn, Ca, Mg, Fe | Blocked by zero Mn; light is also weak |
| Canonical nitrogen fixation | Mo, Fe | Blocked by zero Mo |
| Canonical oxygen respiration | Cu, Fe, O₂ | Blocked by zero Cu and zero O₂ |
| Zinc-heavy enzyme/cellular traits | Zn | Strongly population-limited |
| Non-volcanic advanced organism | Sulfur tolerance | Severe soft/hard sulfur stress |

A migrant may carry enough missing micronutrient to function temporarily. Catalytic quotas are not consumed every tick, but growth and reproduction require each descendant to receive its own minimum quota. The tile therefore cannot sustain expansion of a manganese-, molybdenum-, or copper-dependent lineage without a new source, cross-tile import, predation on imported matter, or migration.

NH₃ is intentionally under-replenished at the expected founding-population demand: the first tick consumes 6,000 while the source contributes 5,000 before attrition. This creates a longer-term fixed-nitrogen pressure. Because molybdenum is absent, evolving canonical nitrogen fixation does not immediately solve that pressure in this tile.

# Expected population behavior

The fixture should exhibit four phases:

1. **Founding growth:** 100 adapted organisms gain structure and reserves.
2. **First reproduction:** structure reaches split requirements after roughly 334 favorable ticks.
3. **Hydrogen competition:** population growth raises H₂ claims until proportional allocation slows reserve and structural growth.
4. **Nitrogen pressure:** finite NH₃ declines, making migration, scavenging, decomposition, or a different environment increasingly valuable.

The approximate H₂ equilibrium with 100 organisms consuming 80,000 per tick is 67,750,000. At roughly 300 otherwise identical organisms, biological demand approaches the volcanic H₂ supply and the equilibrium falls close to depletion. This provides a first carrying-capacity target, not a guaranteed stable population.

# Validation scenarios

- Abiogenesis debits exactly the CHNOPS and micronutrients credited to founders.
- First-tick gas and organism totals match this document.
- Each founder's three-extent biomass input bundle reaches exactly `255` dissolved-macronutrient load, fits atomically under the `512` cap, and leaves the store empty after consumption.
- Founder waste credits the tile organic pool by exactly `C 5,600 / H 11,200 / O 9,800`; no spent carrier matter remains internally.
- Micronutrient stockpiling stops at one additional `57`-unit reproduction set and never treats free surplus as health-bearing committed quota.
- With abundant uncontested pools, primitive uptake averages `0.5` granted micronutrient quantum per organism-hour, consumes exactly `5,700` units when every founder fills its extra set, and finishes in 114 expected ticks.
- A pinned seed reproduces the exact per-tick micronutrient opportunity, target selection, claims, grants, and tile debits.
- The no-organism gas fixture approaches the stated equilibria.
- The 100-organism fixture grows under adapted sulfur tolerance.
- The same organisms without tolerance experience the expected stress and rapid mortality.
- H₂ contention remains proportional and deterministic as population approaches 300.
- NH₃ trends downward under expected demand.
- Zero Mn, Mo, and Cu prevent validation of their gated traits or reproduction of organisms requiring their quotas.
- Introducing one missing micronutrient through a boundary source enables only the capabilities gated by that nutrient.
- Death transfers all structural matter, reserve, stores, and micronutrient quotas to one remnant.
- Decomposition returns all remaining matter to the correct tile pools.
- Save/reload preserves gas attrition remainders, stress state, resource claims, and the next deterministic outcomes.

# Remaining balance questions

- [x] Use 100 founders and a roughly 14-day hydrogen first split as the slower side of the opening comparison; rerun after full health and action scheduling are implemented.
- [ ] Exact tolerance/efficiency choices exposed during initial DNA setup.
- [ ] Whether SO₂ stress should also modify a future acidity condition rather than remain entirely gas-specific.
- [ ] Exact micronutrient quotas for advanced traits.
- [x] Primitive founder micronutrient uptake throughput and targeting; recalibrate when the factors named in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md) change.
- [x] Exact first sulfur fixture and its 250-tick reproduction target; see [SULFUR_TILE_STARTING_CONFIGURATION.md](SULFUR_TILE_STARTING_CONFIGURATION.md).
- [x] First remnant decay rates and slow passive organic-to-inorganic nutrient return; see [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).
- [ ] Senescence distribution for the founding species.
- [ ] Mutation-point income under the resulting population curve.

# Scientific anchors

- The oxygen-evolving complex of oxygenic photosynthesis contains manganese and calcium: [Photoassembly of the Water-Oxidizing Complex in Photosystem II](https://pmc.ncbi.nlm.nih.gov/articles/PMC2597823/).
- Canonical molybdenum nitrogenase depends on Fe/Mo metalloclusters: [Biosynthesis of Nitrogenase Cofactors](https://pmc.ncbi.nlm.nih.gov/articles/PMC7318056/).
- Nickel enzymes include hydrogenases and the acetyl-CoA synthase chemistry used by the Wood–Ljungdahl pathway: [Structure, function, and biosynthesis of nickel-dependent enzymes](https://pmc.ncbi.nlm.nih.gov/articles/PMC7184782/).
- High H₂S concentrations can inhibit respiratory bioenergetics, while adapted organisms can also use sulfide: [Impact of Hydrogen Sulfide on Mitochondrial and Bacterial Bioenergetics](https://pmc.ncbi.nlm.nih.gov/articles/PMC8657789/).
- Sulfite is reactive, damages biological macromolecules, and induces bacterial detoxification responses: [Control of Bacterial Sulfite Detoxification](https://pmc.ncbi.nlm.nih.gov/articles/PMC6527743/).

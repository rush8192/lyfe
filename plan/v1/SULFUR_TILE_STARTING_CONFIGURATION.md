# One-Tile Sulfide-Phototrophy Starting Configuration

Status: first numerical calibration

Sources: [founding metabolism decisions](FOUNDING_METABOLISMS.md), [resource model](RESOURCE_MODEL.md), [resource calibration](RESOURCE_CALIBRATION.md), and [hydrogen fixture](ONE_TILE_STARTING_CONFIGURATION.md).

# Purpose

Define the sulfur half of the opening comparison as a deterministic, bounded fixture. It must demonstrate the intended fast-specialist identity without granting unexplained matter, mutation income, reproduction rules, or survival bonuses.

The fixture deliberately uses sulfide-powered anoxygenic phototrophy rather than a separate sulfur chemotrophy. It is shallow and illuminated, but remains volcanic, toxic to insufficiently adapted organisms, limited by fixed nitrogen, and devoid of several micronutrients required by advanced pathways.

# Decisions fixed by this pass

- The sulfur founder uses `SulfideAnoxygenicPhototrophy`.
- Its ideal-tile opening produces four structural-biomass units per organism-hour versus three in the hydrogen fixture.
- This produces first viable reproduction after 250 ticks, or 10 days 10 hours, compared with 334 ticks, or 13 days 22 hours, for hydrogen.
- The sulfur first split is therefore approximately 25.1% sooner and its initial structural growth rate is 33.3% higher.
- Useful phototrophy occurs for 12 fixture hours followed by 12 dark hours. Stored organic reserve funds maintenance and growth through darkness.
- The sulfur population is more tightly constrained by its founding substrate: an averaged source-limited ceiling near 208 organisms versus approximately 311 for hydrogen before other limits.
- Strong H₂S adaptation is intrinsic to the founding metabolism. SO₂ remains harmful and uses the same initial tolerance multiplier as the hydrogen founder.
- Founding reproduction mode, reserve capacity, organism structure, lifecycle phase, and mutation-income modifier remain identical between the two founders.

# Fixture boundaries

- One aquatic tile; neighbor exchange and migration are disabled.
- One-hour ticks.
- The fixture begins at local dawn with 12 illuminated ticks followed by 12 dark ticks.
- One founding species and 100 organisms.
- Mutation, autonomous speciation, predation, and rare catastrophes are disabled.
- Reproduction, starvation, senescence, death, remains, and decomposition are enabled.
- Temperature, turbidity, and volcanism are held at their baselines so the light cycle and resource effects can be isolated.
- All probabilistic outcomes use deterministic keyed draws. The expected-flow calculation below uses the configured ideal-path outcome.

The dawn start and square light cycle are fixture controls, not final world/climate rules. The climate deep dive must replace them with deterministic insolation at latitude, season, weather, water depth, and simulation time while preserving equivalent daily opportunity in an eligible sulfur start.

# Fixed tile configuration

| Attribute | Fixture value | Purpose |
| --- | ---: | --- |
| Terrain | Aquatic volcanic ocean | Eligible sulfur start |
| Elevation | `-20 m` | Shallow enough for useful light |
| Water depth | `20 m` | Derived from elevation |
| Mean temperature | `45 °C` | Matches the hydrogen comparison |
| Temperature variation | Disabled | Isolate metabolism and resources |
| Daytime insolation at organism depth | `0.80` normalized | Full founding light factor |
| Nighttime insolation | `0` | Forces reserve use |
| Illuminated/dark ticks | `12 / 12` | First diurnal fixture |
| Aquatic turbidity | Fixed inside the `0.80` value | Avoid duplicate attenuation |
| Volcanic activity | `0.80` normalized | Sustains local gas sources |
| Gas exposure multiplier | `1.00` | Keeps thresholds directly readable |

# Atmospheric configuration

The phase order matches the hydrogen fixture: source, attrition, then biological claims.

Neighbor exchange is disabled for this standalone calculation. Full worlds insert the calibrated exchange phase between attrition and biological claims; see [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).

```text
postSource = startingAmount + sourcePerTick
attrition = floor(postSource * attritionRate + persistedRemainder)
availableToBiology = postSource - attrition
```

| Gas | Starting amount | Source/tick | Attrition/tick | First role |
| --- | ---: | ---: | ---: | --- |
| H₂ | 10,000,000 | 50,000 | 0.25% | Secondary volcanic gas; not usable by founder |
| CO₂ | 100,000,000 | 150,000 | 0.02% | Founding carbon substrate |
| H₂S | 20,000,000 | 200,000 | 0.50% | Founding electron donor, structural sulfur, and toxin |
| SO₂ | 5,000,000 | 50,000 | 1.00% | Short-lived volcanic toxin/sulfite precursor |
| CH₄ | 2,000,000 | 4,000 | 0.20% | Geological reservoir; initially unusable |
| N₂ | 500,000,000 | 0 | 0 | Stable but initially unusable nitrogen reservoir |
| NH₃ | 10,000,000 | 5,000 | 0.05% | Accessible fixed-nitrogen source |
| O₂ | 0 | 0 | 0.02% when present | Future biological output; absent initially |

The N₂ and CO₂ starting amounts are the median global-background targets for generated worlds. Generated scenarios may vary N₂ by ±10% and CO₂ by ±25% at the world level; deterministic fixture calculations retain the exact medians. Because this tile's volcanic emission profile emits the founding gases, aquatic gas accessibility is `1.00` despite its `20 m` depth.

H₂S replenishment is twice the hydrogen fixture's H₂S source, but half of the sulfur founder's energy cycle is dark. At 100 organisms its light-period claims keep the gas near its starting range rather than allowing it to climb toward the no-biology equilibrium.

# Generic macronutrient pools

The pre-abiogenesis organic and inorganic CHNOPS pools, phosphorus-weathering input, founding structure, and founding reserve debits are identical to the hydrogen fixture. Keeping them identical ensures that the comparison is driven by metabolism, habitat, and micronutrient gates rather than a larger unexplained abiogenesis endowment.

Biomass assembly continues to use the common balanced recipe:

```text
100 ReserveOrganic
+ 20 NH3
+ 2 InorganicPhosphorus
+ 1 H2S
    -> 1 StructuralBiomass
     + 46 H2O(boundary)
     + 14 OrganicOxygen
```

# Micronutrient configuration

The sulfur tile uses the same general catalogue and abundant cellular ions as the hydrogen fixture, with these deliberate differences:

| Micronutrient | Pre-abiogenesis amount | Ongoing source/tick | Ecological intent |
| --- | ---: | ---: | --- |
| Calcium | 50,000 | 5 | Present but oxygenic photosynthesis remains blocked by manganese |
| Iron | 2,000,000 | 100 | Founding phototrophy/electron-transfer quota |
| Potassium | 3,000,000 | 25 | Baseline cellular requirement |
| Sodium | 10,000,000 | 100 | Abundant aquatic ion |
| Magnesium | 5,000,000 | 50 | Baseline cell and pigment abstraction quota |
| Zinc | 1,000 | 0 | Strongly constrains zinc-heavy traits |
| Copper | 0 | 0 | Blocks canonical copper-dependent respiration |
| Iodine | 0 | 0 | No founding requirement |
| Fluoride | 100,000 | 0 | Present but not universal |
| Selenium | 500 | 0 | Rare specialized cofactor |
| Manganese | 0 | 0 | Blocks canonical oxygenic photosynthesis |
| Molybdenum | 0 | 0 | Blocks canonical nitrogen fixation |
| Nickel | 0 | 0 | Prevents sustained reproduction through the canonical hydrogen path |
| Cobalt | 0 | 0 | Prevents sustained reproduction through the canonical acetogenesis path |

The founder's minimum structural/catalytic quotas are:

| Micronutrient | Minimum per organism | Population debit |
| --- | ---: | ---: |
| Iron | 25 | 2,500 |
| Magnesium | 10 | 1,000 |
| Potassium | 10 | 1,000 |
| Sodium | 10 | 1,000 |

After initialization, those four tile balances are `1,997,500`, `4,999,000`, `2,999,000`, and `9,999,000` respectively. All other balances are unchanged. A migrant may carry Ni or Co, but this tile cannot reproduce an expanding canonical hydrogen-acetogenesis population without importing those quotas.

# Founding species configuration

Initial organisms match the hydrogen fixture:

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
| Mutation points | 0 | 0 |
| Initial mutation-income DNA modifier | `1.00` | `1.00` |

Under the first complete health calibration, the initial SO₂ exposure contributes a `0.966667` environmental factor while intrinsic H₂S adaptation makes the other opening factors neutral. Initial derived health is exactly `0.483334`; see [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md). The 50% table value remains the pre-stress reserve fraction.

Required capabilities:

- `SulfideAnoxygenicPhototrophy` with a maximum 1,000 reaction extents per illuminated tick and `0.95` ideal-path success/yield factor.
- Intrinsic `25×` H₂S threshold multiplier without the hydrogen founder's separate tolerance-efficiency penalty.
- `10×` SO₂ threshold multiplier; SO₂ is not treated as a harmless substitute for H₂S.
- Assimilation of ammonia, inorganic phosphate, and sulfide into biomass.
- `PrimitiveNutrientStore`, using the founder capacities and needs-only retention policy.
- Warm-water tolerance centered near `45 °C`.
- True-split reproduction with the same provisional 500-energy additional cost.
- Founding anabolic throughput of four structural-assembly extents per tick when reserve requirements are satisfied.
- No locomotion, sensing, predation, fermentation, oxygenic photosynthesis, nitrogen fixation, or respiration.

The fourth assembly extent is the concrete expression of greater opening whole-organism efficiency. It is a declared cross-family effect of the metabolism, not free biomass: every extent still consumes 100 `ReserveOrganic`, nitrogen, phosphorus, and sulfur through the common reaction. The hydrogen founder remains capped at three assembly extents under its founding metabolism.

The committed sulfur-founder micronutrient quota has a total load of `55`. Its primitive desired-inventory policy attempts to acquire at most one additional complete reproduction set in the `64`-unit free-micronutrient store. It begins with zero free inventory; the abiogenesis transaction supplies only the committed founding quota.

# Sulfide-phototrophy reaction

For one illuminated reaction extent:

```text
2 H2S + CO2 + light
    -> 1 ReserveOrganic + H2O(boundary) + 2 InorganicSulfur

gross energy opportunity = 2
stored energy = 1
dissipated energy = 1
```

The ideal illuminated fixture resolves:

```text
expectedExtent = floor(1,000 * 0.95) = 950 per organism
```

Dark ticks resolve zero phototrophy extents. The final implementation may produce the `0.95` expectation probabilistically, but the deterministic balance fixture uses exactly 950 so its ledger values remain stable.

# Maintenance, stress, and reserve policy

- Base maintenance: 50 energy per organism-tick.
- H₂S soft-stress cost: zero at the initial exposure under the intrinsic `25×` multiplier.
- Expected SO₂ soft-stress cost: 6 energy per tick under the `10×` multiplier and existing sulfur-stress curve.
- Total expected maintenance and stress: 56 energy per tick.
- Structural assembly: four extents and 400 reserve per favorable tick.
- Growth reserve floor: an organism suppresses one or more assembly extents rather than spending below 4,000 reserve. Maintenance still applies.

The reserve floor is an internal homeostatic gate, not environmental sensing. It prevents newly split organisms from consuming themselves through a full dark period while allowing the initial 5,000-reserve founders to complete the calculated first growth cycle.

# Expected first illuminated-tick flows

At local dawn, all 100 organisms resolve 950 phototrophy extents and four assembly extents.

## Gas balances

| Gas | After source/attrition | Biological debit | End of tick |
| --- | ---: | ---: | ---: |
| H₂ | 10,024,875 | 0 | 10,024,875 |
| CO₂ | 100,129,970 | 95,000 | 100,034,970 |
| H₂S | 20,099,000 | 190,400 | 19,908,600 |
| SO₂ | 4,999,500 | 0 | 4,999,500 |
| CH₄ | 1,999,992 | 0 | 1,999,992 |
| N₂ | 500,000,000 | 0 | 500,000,000 |
| NH₃ | 9,999,998 | 8,000 | 9,991,998 |
| O₂ | 0 | 0 | 0 |

The H₂S debit contains 190,000 units for phototrophy and 400 for structural sulfur. Phototrophy credits 190,000 units to the tile's generic inorganic-sulfur pool. Inorganic phosphorus moves from 5,000,000 to 4,999,450 after 250 weathering units and 800 assembly units. The four-extent biomass input bundle stages at a peak dissolved-macronutrient load of `340` per organism and fits atomically under the shared `512` cap.

## Organism totals

| Flow | Per organism | Population |
| --- | ---: | ---: |
| Reserve produced | 950 | 95,000 |
| Metabolic energy dissipated | 950 | 95,000 |
| Base maintenance spent | 50 | 5,000 |
| SO₂ stress spent | 6 | 600 |
| Reserve spent on biomass | 400 | 40,000 |
| Net stored-energy change before capacity throttling | +494 | +49,400 |
| Structural growth | +4 | +400 |

Maintenance and stress spending release `5,600` organic carbon, `11,200` organic hydrogen, and `5,600` organic oxygen to the tile across the population. Biomass assembly releases another `5,600` organic oxygen and sends `18,400` water units to the ocean boundary. The end-of-phase tile organic credits are therefore `C = 5,600`, `H = 11,200`, and `O = 11,200`; founder macronutrient stores return to zero. Phototrophy sends another `95,000` water units to the boundary and credits `190,000` inorganic sulfur.

Primitive micronutrient uptake runs alongside these flows. Each organism with an incomplete extra quota set has one `0.5`-probability opportunity to claim one required micronutrient quantum, for `50` expected claims across the population on an abundant uncontested tick. The exact first-tick count and resource distribution are seed-keyed, and every grant enters the free-micronutrient store.

One complete additional set costs `55` units per organism or `5,500` across the founding population. Under uninterrupted full grants, expected completion is `110` ticks, or 4 days 14 hours. The probability that opportunity variance alone leaves a founder incomplete at the 250-tick structural reproduction deadline is approximately `2.1 × 10^-20`. Ecological scarcity and contention may still delay it. Primitive passive-uptake cost is already included in base maintenance.

Spread over 110 expected ticks, the population's quota demand averages approximately `22.73` iron and `9.09` each of magnesium, potassium, and sodium per tick. Their fixture sources are `100`, `50`, `25`, and `100`, again before drawing down large starting pools. Sulfide and fixed nitrogen therefore remain the intended opening ecological limits.

# First-reproduction calculation

A true split requires the parent to grow from 1,000 to 2,000 structural units:

```text
required growth = 1,000 structure
growth per tick = 4 structure
ticks = ceil(1,000 / 4) = 250
elapsed = 250 hours = 10 days 10 hours
```

The hydrogen fixture requires:

```text
ticks = ceil(1,000 / 3) = 334
elapsed = 334 hours = 13 days 22 hours
```

Therefore:

```text
time reduction = (334 - 250) / 334 = 25.15%
structural-rate increase = (4 - 3) / 3 = 33.33%
```

With the fixture starting at dawn, 950 daytime reserve production, a 10,000 capacity, and 456 energy of full-growth spending each hour, expected reserve immediately before the first reproduction is approximately 9,012. Paying the shared 500-energy reproduction cost leaves 8,512, or 4,256 for each result of a true split. The reserve floor then suppresses some post-split dark-period growth if needed; it does not create energy.

This result assumes adequate H₂S, CO₂, NH₃, phosphorus, and structural quotas. If claims are proportionally reduced, assembly falls below four and reproduction occurs later.

# Daily energy and matter budget

For one organism before capacity throttling:

```text
12 light ticks * 950 reserve                  = 11,400 produced/day
24 ticks * 400 reserve for assembly           =  9,600 spent/day
24 ticks * 56 maintenance and stress          =  1,344 spent/day
nominal reserve surplus                       =    456/day
structural growth                             =     96/day
```

Reserve capacity causes some daylight capture to be throttled once the organism is full. This is expected and must reduce gas claims rather than destroy produced reserve after the fact.

# Substrate ceiling and specialist pressure

Average per-organism H₂S demand while sustaining four assembly extents is:

```text
phototrophy = (12 * 2 * 950) / 24 = 950 H2S/tick
structure   = 4 H2S/tick
total       = 954 H2S/tick
```

For a source `S = 200,000` and attrition `r = 0.005`, the approximate averaged positive-equilibrium condition is:

```text
population ceiling < ((1 - r) * S) / demand
                   < 199,000 / 954
                   < 208.6 organisms
```

The corresponding averaged H₂S equilibria are approximately:

| Population | Approximate equilibrium |
| ---: | ---: |
| 100 | 20,720,000 |
| 200 | 1,640,000 |
| 209+ | No positive full-demand equilibrium; proportional contention reduces yield |

An explicit 250-tick calculation using the alternating light cycle, full pre-capacity claims, source, and attrition ends near 19,965,702 H₂S; the range during that run is approximately 18,932,870 to 20,903,579. This validates adequate substrate for the first split while making the doubled population immediately encounter strong H₂S competition.

For comparison, the hydrogen fixture consumes 800 H₂ per organism-tick and has `S = 250,000`, `r = 0.0025`, producing a source-limited ceiling near 311 organisms. The sulfur opening therefore grows faster but reaches its founding substrate constraint earlier.

Fixed nitrogen is also a deliberate pressure. At 100 sulfur organisms, four assembly extents consume 8,000 NH₃ per tick while the volcanic source contributes 5,000 before roughly 5,000 initial attrition. The first tick falls by 8,002 units. Iterating the source/attrition recurrence at a constant population reaches exhaustion after approximately 1,961 ticks, or 81.7 days; attrition falls with the declining pool, which is why dividing the initial stock by the first-tick loss would underestimate the lifetime. Population growth shortens that horizon. Zero molybdenum prevents canonical nitrogen fixation from immediately removing the pressure.

# Provisional mutation-income calibration

The opening fixtures expose enough population and reserve history to select a first mutation-income curve, although the result must be rerun when the final health function exists.

Use logarithmic effective population to reward growth without making late-game point income scale linearly into an unmanageable decision flood:

```text
referencePopulation = 100
effectivePopulation = 100 * log2(1 + livingPopulation / 100)

mutationIncomePerTick =
    effectivePopulation
    * averageRelativeHealth
    * dnaMutationModifier
    * tickDurationHours
    / 750
```

Initial founders use `dnaMutationModifier = 1.00`. Income accumulates fractionally in deterministic fixed-point state; only presentation rounds it. The same equation applies to controlled and uncontrolled species.

The curve produces:

| Living population | Effective population |
| ---: | ---: |
| 0 | 0.0 |
| 100 | 100.0 |
| 200 | 158.5 |
| 1,000 | 345.9 |
| 10,000 | 665.8 |
| 100,000 | 996.7 |

The denominator `750` is calibrated from the first two fixtures. For this calculation only, relative health equals reserve divided by reserve capacity; external health modifiers are neutral. Reproduction is synchronized at the calculated first-split tick, a true split pays 500 energy, and no speciation, senescence, resource contention, or deaths occur.

| Milestone | Simulated time | MP balance | At 2 ticks/second |
| --- | ---: | ---: | ---: |
| Hydrogen reaches its 40 MP `OrganicResourceUptake` option | 338 hours / 14.1 days | 40.0 | 2.8 minutes |
| Sulfur reaches its 60 MP `MetabolicRegulation` option | 511 hours / 21.3 days | 60.1 | 4.3 minutes |

At day 20, the same calculation produces approximately 63.3 MP for hydrogen and 55.7 MP for sulfur. Although sulfur reproduces earlier, its night-time reserve draw lowers average health enough that population alone does not give it faster income. This is an intended transparent consequence of the common health economy, not a metabolism-specific penalty.

These values support the “meaningful choice every few real-world minutes” target at a provisional fastest speed of two one-hour ticks per real second. They do not yet determine the time to complete either escape path: spending the first trait causes speciation, transfers control to a founding subset, and changes the descendant's population and future income. Hoarding several trait costs into one speciation would produce a misleading lower bound.

This curve is a calibration recommendation, not final balance, until the health calculation and speciation sequence are simulated. The implementation should make `referencePopulation`, the logarithmic curve, the `750` normalization, and DNA modifiers versioned rule data.

# Paired-region implications

The standalone hydrogen and sulfur fixtures disable neighbor exchange so each metabolism can be calibrated independently. The Survival pair then combines one instance of each profile in edge-sharing tiles.

The first coefficients and calculated paired plateaus are now fixed in [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md). They satisfy these balance requirements:

- Exchange must not provide enough Ni or Co to make the sulfur tile a sustainable hydrogen-acetogenesis start; these micronutrients are poorly mobile and remain zero without an explicit source or biological import.
- H₂S and SO₂ exchange must remain local enough that the sulfur substrate niche persists and volcanic toxicity matters in the hydrogen tile.
- CO₂ and other stable gases may mix quickly without erasing the founding distinction.
- With no founding locomotion, neither species crosses the tile boundary before evolving or otherwise acquiring a movement mechanism.

The paired fixture should run both with exchange disabled as an isolation control and with the selected coefficients, then compare first reproduction, reserve minimum, deaths by stressor, substrate trajectories, and mutation-point income for both species.

# Missing inputs exposed by calibration

These inputs remain necessary before the opening can be considered fully balanced:

1. **Final health function and post-speciation mutation trajectory.** The provisional logarithmic formula and two-tick-per-second target produce useful opening times, but must be rerun with environmental health adjustments, founder fractions, resource contention, senescence, and actual speciation choices.
2. **World insolation function.** The fixture fixes a 12/12 square cycle at `0.80`; eligible generated tiles need a seasonal, latitudinal, weather-, depth-, and turbidity-aware function that delivers a comparable daily opportunity.
3. **Senescence and death reserve threshold.** The first split remains viable under the 4,000 growth floor, but long-run survival and generation overlap require the actual starvation threshold and age distribution.
4. **Post-split assembly scheduling.** The internal reserve floor is numerically adequate, but organism action planning must specify how many assembly extents are suppressed as reserve approaches it.
5. **Organic-resource uptake and fermentation yields.** The hydrogen escape path has a mutation price but cannot be compared economically until its uptake, reaction, maintenance, and substrate values are defined.
6. **Environmental tolerance options.** The baseline comparison is fixed, but the exact player-selectable efficiency-versus-tolerance packages must be bounded and tested so none reverses the two identities.

Item 1 blocks final paired gameplay pacing. Items 2–4 block replacement of this deterministic fixture with generated-world runs. Items 5–6 block evaluation of later player evolution choices but do not block implementation of the two founding reactions, gas propagation, or the provisional mutation curve.

# Validation scenarios

- The first illuminated-tick ledger matches the stated gas, phosphorus, water, oxygen, reserve, and structure totals.
- Each founder's four-extent biomass input bundle reaches exactly `340` dissolved-macronutrient load, fits atomically under the `512` cap, and leaves the store empty after consumption.
- Founder waste credits the tile organic pool by exactly `C 5,600 / H 11,200 / O 11,200`; no spent carrier matter remains internally.
- Micronutrient stockpiling stops at one additional `55`-unit reproduction set and never treats free surplus as health-bearing committed quota.
- With abundant uncontested pools, primitive uptake averages `0.5` granted micronutrient quantum per organism-hour, consumes exactly `5,500` units when every founder fills its extra set, and finishes in 110 expected ticks.
- A pinned seed reproduces the exact per-tick micronutrient opportunity, target selection, claims, grants, and tile debits.
- With 100 founders and full substrate, the first viable split occurs at tick 250 and expected reserve remains positive through every dark period.
- Disabling light resolves zero phototrophy and causes stored reserve to decline through maintenance and any permitted assembly.
- Reducing H₂S claims proportionally delays assembly without violating mass balance.
- Removing intrinsic H₂S adaptation applies the existing hard-stress death curve.
- Removing the `10×` SO₂ multiplier produces the expected rapid sulfur-dioxide mortality.
- Zero Mn, Mo, Ni, Co, and Cu enforce their declared pathway and reproduction gates.
- At approximately 200 organisms, H₂S approaches its low positive equilibrium and constrains further growth.
- Reserve production is throttled before capacity overflow and unused substrate remains in the tile.
- Repeating the fixture produces identical ledgers, organisms, deaths, and state hashes.
- The provisional mutation curve reaches the hydrogen and sulfur opening choices within one tick of the stated reference milestones.

# Scientific anchors

- The resource equation uses the elemental-sulfur form of anoxygenic photosynthesis: [Low-Light Anoxygenic Photosynthesis and Fe-S-Biogeochemistry in a Microbial Mat](https://pmc.ncbi.nlm.nih.gov/articles/PMC5934491/).
- H₂S may serve as an energy substrate for adapted organisms while inhibiting bioenergetics at high exposure: [Impact of Hydrogen Sulfide on Mitochondrial and Bacterial Bioenergetics](https://pmc.ncbi.nlm.nih.gov/articles/PMC8657789/).
- The later oxygenic route's manganese/calcium gate is inspired by the oxygen-evolving complex: [Photoassembly of the Water-Oxidizing Complex in Photosystem II](https://pmc.ncbi.nlm.nih.gov/articles/PMC2597823/).

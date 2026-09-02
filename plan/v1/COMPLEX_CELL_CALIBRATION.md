# Complex-Cell Numerical Calibration

Status: first analytical processing-load, structure-assignment, assembly-throughput, body-scale, particulate-acquisition, and hunting-economy values selected; executable multi-seed population validation remains

Sources: [complex cellular organization](COMPLEX_CELLS.md), [founder fixture](ONE_TILE_STARTING_CONFIGURATION.md), [sulfur founder fixture](SULFUR_TILE_STARTING_CONFIGURATION.md), [organic uptake and fermentation](ORGANIC_UPTAKE_AND_FERMENTATION.md), [aerobic respiration](AEROBIC_RESPIRATION.md), [lifecycle and recycling](LIFECYCLE_AND_RECYCLING.md), [predation](PREDATION.md), and [spatial calibration](SPATIAL_CALIBRATION.md).

# Purpose

Choose the first integer processing loads and capacity values for the complex-cell rule pack, prove that they do not disturb settled opening fixtures, and establish numerical landmarks for compartmentalized generalists and proto-eukaryotic predators.

The calibration is deliberately constrained by existing values rather than selecting a free-standing complexity scale:

- Hydrogen and sulfur founders must retain three and four biomass-assembly extents per hour.
- Primitive fermentation must retain its full `120`-opportunity ceiling and existing 693-hour first-division fixture.
- Simple-cell respiration must retain its shared `80`-opportunity ceiling.
- Particulate digestion remains `1%` of mature structure per hour and recovers `32` reserve per structural quantum.
- Organization changes throughput ceilings but never changes any reaction recipe or yield.
- Scale must increase total per-organism capacity more slowly than structural mass, preserving the cost of a larger body.

# Structural assignments

Calibration of an organization-dense cell followed by a scale increase exposes information that cannot be derived from one undifferentiated structure total. A mature primitive-radius proto-eukaryotic cell contains `2,188` structure, but only `1,000` supports its external scale. If it later acquires Scale I, treating all `2,188` as geometric matter would enlarge it instantly despite mutation granting no matter.

V1 therefore keeps one `StructuralBiomass` resource but assigns every committed quantum to one of two roles:

```text
geometricStructure
    supports physical body scale and derived radius

organizationStructure
    supports walls, internal membranes, compartments, organelles,
    and other same-scale machinery

viableStructure = geometricStructure + organizationStructure
```

These are assignments within the organism's committed `Structure` reservoir, analogous to committed micronutrient quotas. They are not distinct chemical resources and do not change elemental accounting. Reproduction, death, digestion, and decay conserve the sum exactly.

The first mature targets are:

| Phenotype | Geometric target | Organization target | Total mature structure |
| --- | ---: | ---: | ---: |
| Primitive | `1,000` | `0` | `1,000` |
| `CompartmentalizedCell` | `1,000` | `250` | `1,250` |
| `ProtoEukaryoticOrganization` | `1,000` | `1,188` | `2,188` |
| Proto-eukaryotic + Scale I | `3,375` | `4,008` | `7,383` |
| Proto-eukaryotic + Scale II | `11,391` | `13,527` | `24,918` |

The totals preserve the already selected `1.25×` and additional `1.75×` organization multipliers with one final ceiling. Organization target is the exact remainder after the geometric target is removed; it is not independently rounded from a percentage.

On speciation, existing assignments remain unchanged. Growth toward a new phenotype selects the assignment with the greatest normalized deficit:

```text
geometricDeficitQ = max(0, targetGeometric - currentGeometric)
                  / targetGeometric

organizationDeficitQ = max(0, targetOrganization - currentOrganization)
                     / max(1, targetOrganization)

targetRole = role with greatest normalized deficit
tie order  = GeometricStructure, then OrganizationStructure
```

Each admitted assembly extent credits one assigned `StructuralBiomass` quantum. This keeps both dimensions near the destination phenotype's proportions during a combined scale-and-organization transition. Ordinary health continues to use total viable structure over total phase target. Radius uses only geometric structure.

Symmetric fission requires two complete geometric targets and two complete organization targets. The near-even split allocates each assignment independently, with the continuing parent receiving the stable-ID remainder. Asymmetric reproduction profiles must likewise name minimum assignments for both results. A remnant may retain the assignments for visual explanation, but every digestion or decay reaction accepts the common `StructuralBiomass` identity.

# Processing-work unit

`InternalProcessingWork` is an integer scheduling unit, not matter or energy. It is neither stored nor persisted as a balance. One tick derives a capacity from DNA, lifecycle, age, condition, and current scale, then consumes it while admitting internal work.

The first process loads are:

| Internal process | Work per admitted opportunity or extent | Rationale |
| --- | ---: | --- |
| Fermentation opportunity | `1` | Simple soluble-substrate catabolism |
| Respiratory opportunity, either fuel | `1` | Shared respiratory machinery already supplies a separate ceiling |
| Particulate digestion extent | `4` | Hydrolysis, handling, and combined catabolism are deliberately processing-heavy |
| Biomass-assembly extent | `8` | Constructing one 333-atom structural abstraction is the heaviest recurring founder-compatible task |
| Carrier recharge within respiration | `0` additional | Already included in the respiratory opportunity |
| Mandatory reserve spending and maintenance | `0` | An obligatory debit must not fail merely because optional work consumed capacity |
| Passive resource binding or quota promotion | `0` | Bookkeeping for an already admitted process, not separate work |

Probabilistic reactions reserve work for admitted **opportunities before their success draw**, not only for successful extents. A failed attempt still occupied machinery. This also prevents a lucky or unlucky binomial result from changing how much capacity remains for an unrelated process in the same tick.

The phase-5 plan therefore becomes:

```text
derive current processing capacity
derive each process's ordinary reaction-specific opportunity ceiling
allocate whole opportunities through DNA priority and holdback policy
debit processing work for admitted opportunities
perform keyed success draws only for admitted probabilistic opportunities
emit external claims for successful candidate extents
```

Deterministic digestion and growth consume work only for whole extents that can pass their ordinary input, output, reserve-floor, and assignment gates. Contention can still reduce a preplanned probabilistic reaction after its work was reserved; denied work is not reassigned with same-tick hindsight.

# Capacity compilation

The first mature primitive-scale organization capacities are:

| Active organization tier | Base work/hour | Relative to primitive |
| --- | ---: | ---: |
| `PrimitiveCell` | `160` | `1.00×` |
| `InternalMembraneScaffolding` | `184` | `1.15×` |
| `CompartmentalizedCell` | `240` | `1.50×` |
| `ProtoEukaryoticOrganization` | `448` | `2.80×` |

Physical scale multiplies total work linearly with current radius, capped at the DNA's mature radius:

```text
effectiveRadiusMultiplierQ = clamp(
    currentBodyRadius / FounderRadius,
    minimum = current active tier's inherited mature radius,
    maximum = compiled mature radius)

processingWorkPerHour = floor(
    organizationBaseWorkPerHour * effectiveRadiusMultiplierQ)
```

At mature Scale I and Scale II this gives `672` and `1,008` work/hour for a proto-eukaryotic cell. Structure grows with radius cubed while work grows only with radius, so work per mature structural quantum declines sharply:

| Proto-eukaryotic scale | Work/hour | Mature structure | Work per structure |
| --- | ---: | ---: | ---: |
| Primitive radius | `448` | `2,188` | `0.2048` |
| Scale I | `672` | `7,383` | `0.0910` |
| Scale II | `1,008` | `24,918` | `0.0405` |

Larger cells gain a greater total ceiling but become less processing-dense. This is the intended scale tradeoff.

During `OrganizationMaturation`, the prior completed organization's base work remains active until the new organization target and quotas are complete. During scale maturation, current geometric structure raises current radius and therefore work capacity continuously; mutation never grants the mature scale's full capacity immediately.

# Biomass-assembly ceiling

The shared processing budget cannot help a large or organization-dense phenotype if its separate biomass-assembly ceiling remains permanently fixed at three or four. The first ceiling retains the founder pathway identity, then scales with completed organization and current body surface:

```text
organizationAssemblyMultiplier:
    PrimitiveCell or InternalMembraneScaffolding = 1.00
    CompartmentalizedCell                        = 1.50
    ProtoEukaryoticOrganization                  = 3.00

currentScaleAreaMultiplierQ = min(
    square(currentBodyRadius / FounderRadius),
    square(compiledMatureRadius / FounderRadius))

assemblyCeilingPerHour = floor(
    founderPathAssemblyCeiling
    * organizationAssemblyMultiplier
    * currentScaleAreaMultiplierQ)
```

The inherited hydrogen and sulfur base ceilings remain `3` and `4`. The mature whole-extent results are:

| Organization | Hydrogen-derived ceiling | Sulfur-derived ceiling |
| --- | ---: | ---: |
| Primitive/scaffolding | `3` | `4` |
| Compartmentalized | `4` | `6` |
| Proto-eukaryotic, primitive radius | `9` | `12` |
| Proto-eukaryotic Scale I | `20` | `27` |
| Proto-eukaryotic Scale II | `45` | `60` |

These are construction ceilings, not resource grants. Each extent still needs `100 ReserveOrganic`, NH3, phosphorus, sulfur, eight processing-work units, output capacity, and reserve above the growth-protection floor. Organic carbon assimilation, energy, processing work, or nutrient access will often admit far fewer extents.

# Body-scale liabilities

Physical scale uses area for recurring surface/transport work and volume for matter. This makes a large cell somewhat more efficient per structural quantum without making its absolute costs smaller:

```text
effectiveScaleRadiusQ = clamp(
    currentBodyRadius / FounderRadius,
    minimum = 1.0,
    maximum = compiledMatureRadius / FounderRadius)

currentScaleAreaMultiplierQ = square(effectiveScaleRadiusQ)

scaledBaselineMaintenance = roundToNearestEven(
    organizationAdjustedBaselineMaintenance
    * currentScaleAreaMultiplierQ)

scaledReproductionWork = roundToNearestEven(
    organizationAdjustedReproductionWork
    * compiledMatureScaleAreaMultiplierQ)
```

Only base maintenance and the base reproductive transaction use these scale multipliers. Named machinery upkeep, environmental stress, attack costs, digestion overhead, and movement costs retain their own rules; active movement independently uses the same effective current-area factor once. Clamping at compiled mature radius prevents temporary pre-fission structure above the mature body target from changing the founding maintenance fixtures. Reproduction uses mature rather than current radius so an underbuilt organism cannot make division cheaper, though ordinary structure gates already prevent it from reproducing prematurely.

The first body-scale traits are:

| Trait | MP / complexity | Mature radius | Geometric target | Scale quota | Principal scale liability |
| --- | ---: | ---: | ---: | --- | --- |
| `IncreasedCellScaleI` | `160 / 3` | `1.50 R₀` | `3,375` | `Mg 5 + Zn 5` | `2.25×` base maintenance, reproduction work, and active-movement distance cost at maturity |
| `IncreasedCellScaleII` | `260 / 4` | `2.25 R₀` | `11,391` | additional `Mg 10 + Zn 5` | `5.0625×` those area-scaled costs at maturity |

The quotas represent control and construction machinery not already expressed by bulk CHNOPS structure. They are inherited, committed, and provisioned for reproduction like other constitutive quotas. With the particulate package below, the complete Scale-I hydrogen and sulfur predator sets require `127` and `125` units for an additional inherited copy, narrowly fitting the first `128`-unit retained store. Scale II raises those values to `142` and `140`, deliberately requiring a later storage/provisioning investment.

For the existing proto-eukaryotic organization factors, mature base maintenance and reproduction work become:

| Phenotype | Base maintenance/hour | Primitive-fission work |
| --- | ---: | ---: |
| Proto-eukaryotic, primitive radius | `69` | `688` |
| Proto-eukaryotic Scale I | `155` | `1,547` |
| Proto-eukaryotic Scale II | `348` | `3,480` |

The calculations use `50 × 1.10 × 1.25` for baseline maintenance and `500 × 1.10 × 1.25` for organization-adjusted reproduction work before scale. Additive scaffolding and pathway upkeep remain outside these rows.

# Integrated respiratory throughput

`ProtoEukaryoticOrganization` represents an integrated respiratory energy organelle and raises the existing shared respiratory opportunity ceiling:

| Active respiratory organization | Shared respiratory opportunities/hour |
| --- | ---: |
| Simple or compartmentalized cell | `80` |
| `ProtoEukaryoticOrganization` | `160` |

This is a typed modifier to `OxygenRespiration`, not a second reaction and not a yield bonus. Direct and reduced-product reactions still share the one ceiling. Base direct-labile uptake is `120/hour` and reduced-product uptake is `80/hour`, so one base fuel cannot automatically fill all 160 opportunities. `DissolvedOrganicSpecialization` raises the LDO acquisition ceiling to `240`, allowing direct respiration to fill the shared ceiling at its own additional upkeep and quota cost.

The 160-opportunity ceiling is why the proto-eukaryotic work budget selects `448` rather than the earlier analytical `400`: it keeps full respiration, digestion, and ordinary growth feasible at primitive radius; at Scale I it leaves only four work units of headroom for the more demanding sulfur-derived growth profile. Reaction yield, uptake, matter assimilation, and all catalyst requirements remain unchanged.

# Preservation of existing fixtures

The primitive `160` budget is the smallest convenient multiple-of-16 value that admits all settled maximum opening combinations:

| Existing phenotype and maximum concurrent internal work | Required work | Result |
| --- | ---: | --- |
| Hydrogen founder: three assembly extents | `3 × 8 = 24` | Fits; unchanged |
| Sulfur founder: four assembly extents | `4 × 8 = 32` | Fits; unchanged |
| Hydrogen fermenter: 120 opportunities + three assembly | `120 + 24 = 144` | Fits; unchanged |
| Sulfur fermenter: 120 opportunities + four assembly | `120 + 32 = 152` | Fits; unchanged |
| Hydrogen direct respirer: 80 opportunities + three assembly | `80 + 24 = 104` | Fits; unchanged |
| Sulfur direct respirer: 80 opportunities + four assembly | `80 + 32 = 112` | Fits; unchanged |

Because every existing maximum remains below `160`, this calibration does not reopen the hydrogen, sulfur, fermentation, or simple-cell respiration balance landmarks. The unused eight work units in the most demanding primitive case are deliberate integer headroom, not an invitation to raise an existing reaction ceiling.

# Complex-phenotype demand checks

## Compartmentalized consumer

At mature structure `1,250`, digestion can execute `12` whole extents/hour. A fully active compartmentalized consumer can admit:

| Path | Hydrogen-derived | Sulfur-derived | Budget |
| --- | ---: | ---: | ---: |
| Fermentation + digestion + maximum growth | `120 + 48 + 32 = 200` | `120 + 48 + 48 = 216` | `240` |
| Respiration + digestion + maximum growth | `80 + 48 + 32 = 160` | `80 + 48 + 48 = 176` | `240` |

Compartmentalization therefore supports one complete organic energy path, full digestion, and growth concurrently. Attempting maximum fermentation and maximum respiration together adds another 80 or 120 work and exceeds the tier; regulation must choose, split, or defer. This is the intended generalist value without double-dipping every pathway.

## Proto-eukaryotic consumer at primitive radius

At mature structure `2,188`, digestion admits `21` whole extents/hour. Maximum proto-eukaryotic respiration, digestion, and growth require `316` work for the hydrogen-derived ceiling or `340` for sulfur, below the `448` budget. Adding maximum fermentation raises these to `436` and `460`: the hydrogen-derived plan narrowly fits, while the sulfur-derived plan must defer twelve work units.

The spare work is not free production. Current uptake ceilings and shared respiratory selection make simultaneous maximum fuel grants uncommon, and reserve matter, nutrients, output capacity, or reaction ceilings may bind first. The headroom permits later sensing, organelle specialization, and internal reactions without immediately invalidating the keystone.

## Scale-I proto-eukaryotic consumer

At mature structure `7,383`, digestion admits `73` whole extents/hour:

| Concurrent plan | Hydrogen-derived work | Sulfur-derived work | Budget |
| --- | ---: | ---: | ---: |
| Respiration + full digestion + maximum growth | `160 + 292 + 160 = 612` | `160 + 292 + 216 = 668` | `672` |
| Add maximum fermentation too | `732` | `788` | `672` |

This is the strongest calibration landmark. A Scale-I proto-eukaryotic cell can respire at its complete organelle ceiling, digest at its complete structural ceiling, and grow at its complete pathway ceiling, but cannot also run maximum fermentation. The sulfur-derived profile has only four work units of headroom, so small age or condition reductions, other future processes, or allocation choices become meaningful.

## Scale-II pressure

At mature structure `24,918`, the raw digestion ceiling is `249` extents or `996` work, leaving only twelve of the `1,008` work budget before respiration or growth. Scale II therefore cannot maximize every body-proportional process and must specialize or evolve later processing improvements. This preserves a future complexity ceiling and prevents size from multiplying every useful action without bound.

# Matter and reproduction landmarks

Processing capacity does not remove the more important matter constraint:

- Direct labile respiration under proto-eukaryotic organization is limited by the current 120-unit uptake ceiling despite its 160 respiratory opportunities. It has at most 114 expected successful extents/hour and adds `342` new carrier-matter units/hour, sustainably supplying at most three whole biomass-assembly extents/hour before other carbon sources.
- With `DissolvedOrganicSpecialization`, abundant LDO can fill all 160 respiratory opportunities. The expected 152 successful extents add `456` carrier units/hour, sustainably supplying four whole assembly extents before another carbon source.
- Reduced-product respiration at its current 80-unit uptake ceiling has 76 expected successes and adds `152` new carrier units/hour. Alone it sustainably supplies at most one whole assembly extent/hour.
- With both fuels abundant, the direct-first policy can assign 120 opportunities to labile substrate and the remaining 40 to reduced product. At ideal success this adds approximately `342 + 76 = 418` new carrier units/hour, enough for four whole assembly extents before another source.
- The retained ancestral hydrogen fixture adds roughly `400` new reserve carriers/hour in favorable volcanic conditions; the sulfur fixture averages roughly `475/hour` across its day/night cycle. Carrier retention lets respiratory energy recharge spent matter rather than losing maintenance carrier matter to the tile.
- One digested structural quantum adds `32` new reserve carriers and can therefore finance `0.32` future structural quantum before other inputs and costs.

For a primitive-radius proto-eukaryotic target of `2,188`:

| Available carbon route | Sustainable whole growth ceiling | Ideal time to assemble one additional mature body |
| --- | ---: | ---: |
| Direct labile respiration only, current uptake | `3/hour` | `730 h / 30.4 d` |
| Specialized direct labile respiration | `4/hour` | `547 h / 22.8 d` |
| Favorable retained hydrogen or average sulfur founder capture | `4/hour` | `547 h / 22.8 d` |
| Both respiratory fuels at current uptake ceilings | `4/hour` | `547 h / 22.8 d` |
| Direct respiration plus another sufficient carbon source | At most organization ceiling `9` or `12` | `244 h` hydrogen-derived / `183 h` sulfur-derived lower bound |

The direct-respiration-only result intentionally lies near the 720-hour senescence onset. Complex organization is genetically reachable but not automatically robust merely because oxygen and one dissolved fuel exist. A richer second source, evolved organic acquisition, ingestion, or another trait is needed to realize its higher ceiling.

Transition from mature `CompartmentalizedCell` at `1,250` to the `2,188` proto-eukaryotic target requires `938` additional organization structure. Before the new tier activates, the compartmentalized assembly ceilings give resource-unlimited lower bounds of `235 h` for hydrogen-derived DNA and `157 h` for sulfur-derived DNA. Actual maturation remains matter-, energy-, quota-, and senescence-sensitive.

# Predatory biomass demand

A mature Scale-I proto-eukaryotic body contains `7,383` structure. With `32%` structural recovery, replacing that body entirely from mature primitive prey requires at least:

```text
7,383 / (1,000 prey structure * 0.32 recovery)
    = 23.072 primitive-prey equivalents
```

Replacing it by senescence onset requires an average `0.769` completely recovered primitive prey per day before maintenance, failed attacks, incomplete feeding, competition, decay, micronutrients, or reproduction work. Its `25%` ingested buffer holds approximately `1,845` structure-equivalent load, and its `73/hour` digestion ceiling can process a complete primitive prey in about `13.7` digestion-hours once acquired.

Scale II raises the lower bound to `77.869` primitive-prey equivalents, or `2.596/day` over 30 days. It is therefore a genuinely prey-dense specialization rather than a universal successor to Scale I.

These replacement demands are more informative than maintenance-only kill rates. Predation can keep an adult alive at a much lower encounter rate while still failing to replace senescent predators.

## Complete Scale-I hunter lower bound

The calibrated directed-engulfing loadout uses direct respiration, complete proto-eukaryotic organization, Scale I, contact capture, engulfment, particulate ingestion/digestion, prey sensing/behavior, and `AdvancedPropulsion`. Its ideal active costs are:

| Cost component | Energy/hour |
| --- | ---: |
| Scale-I organization-adjusted base maintenance | `155` |
| Internal membrane scaffolding | `5` |
| Direct-respiration stack above base | `60` |
| Contact detection/predation/capture | `17` |
| Engulfment | `20` |
| Particulate ingestion and digestion | `20` |
| Nearby/prey sensing and directed-hunting behavior | `23` |
| Advanced-propulsion upkeep | `20` |
| **Passive total while fully active** | **`320`** |
| Continuous maximum `16 R₀/hour` Scale-I movement | **`108`** |
| **Maximum-search total** | **`428`** |

Over the 720-hour senescence landmark, maximum-search maintenance consumes `308,160` reserve. One replacement body consumes `738,300`; the offspring floor adds `4,000`; and the scaled fission transaction costs `1,547`. At `32,000` recovered reserve per fully digested primitive prey, this is `32.875` prey equivalents before attack and handling costs.

With base size fit `0.50`, a qualifying pursuit factor of `1.15`, and equal attack/defense power, kill probability is `0.575 / 1.575 = 0.3651`. Expected 150-energy attempts add about `0.422` prey equivalent, and two 50-energy remnant-ingestion actions per kill add about `0.103`. The complete optimistic replacement threshold is therefore approximately:

```text
33.40 fully recovered primitive prey per 30-day generation
= 1.113 prey per predator-day
```

This remains optimistic: it excludes prey defense, target loss beyond the explicit encounter-realization factor, competition, decay before complete scavenging, quota acquisition, environmental stress, and non-structural nutrient replacement.

## Directed encounter calibration

A continuously searching advanced hunter has `16 R₀` prey-classification range and `16 R₀/hour` maximum speed. In a uniform tile, its fresh swept-strip opportunity is approximately:

```text
fresh detected prey/day
    = preyCount * (2 * 16 R₀ * 16 R₀/hour * 24 hours) / (1024 R₀)^2
    = preyCount * 0.01171875
```

Use `0.75` as the first fixture's path-realization factor for turns, stale target positions, already-searched overlap, and temporary handling. It is a calibration factor, not a hidden runtime probability: the executable simulation must produce it emergently, and acceptance is `0.65..0.85` across uniform multi-seed fixtures.

The resulting equilibrium landmarks are:

| Scale-I hunter capture profile | Kill chance after qualifying pursuit | Fresh-prey kill coefficient/day | Uniform primitive prey needed for `1.113` kills/day |
| --- | ---: | ---: | ---: |
| Base capture | `0.3651` | `0.003208 × preyCount` | approximately `347`, author as `350` |
| `ImprovedCaptureSuccessI` | `0.4631` | `0.004070 × preyCount` | approximately `274`, author as `275` |

These are one-predator, prey-replete thresholds. A population fixture should find a viable Scale-I predator niche around `275..400` uniformly distributed primitive prey per tile depending on capture investment, while predation remains unsustainable near `100` prey without clustering, reserve-rich prey, another food source, or materially better capture.

## Scale-II role

Scale II cannot capture a primitive prey under the current `0.10` improved-target minimum because `1,000 / 11,391 = 0.0878`. It must hunt larger prey, consume existing remains, or wait for a future small-prey mechanism. Against Scale-I prey, its geometric ratio is `3,375 / 11,391 = 0.2963`, inside the ordinary capture band.

A full Scale-II maximum-search loadout pays `513/hour` passive and `243/hour` movement, or `756/hour`. Replacing its `24,918` structure, maintenance, reserve floor, and `3,480` reproduction work requires about `12.92` fully recovered Scale-I prey per generation after expected base attempts and five post-kill particulate scavenging actions, or `0.431/day`. The same encounter assumptions require about `134` uniform Scale-I prey with base capture or `106` with `ImprovedCaptureSuccessI`.

Scale II remains selectable late-v1 content, but as a deliberately ecosystem-dependent apex/scavenger specialization. It is not a direct upgrade for consuming primitive cells, and its complete quota requires expanded nutrient storage.

# Autonomous-evolution calibration

The recent processing-saturation input uses denied **useful work**, not raw demand:

```text
usefulDeniedWork = denied opportunities or extents whose ordinary
    substrates, quotas, output destinations, lifecycle gates,
    reserve policy, and downstream capability were otherwise valid

trailingInternalBudgetSaturationQ =
    usefulDeniedWork / max(1, admittedWork + usefulDeniedWork)
```

Use a 168-hour ring aligned with other material-opportunity histories. `CompartmentalizedCell` receives no positive processing opportunity below `0.10` saturation. From `0.10..0.50`, map linearly to `0..1`; cap above `0.50`. The score still multiplies quota provision, replacement-energy margin, and remaining-horizon payback as defined in the parent rule pack.

Demand denied because a resource, catalyst, store, behavior, prey encounter, or reaction capability was missing does not count as processing saturation. This prevents autonomous evolution from choosing complexity to solve the wrong bottleneck.

# Required executable fixtures

1. Existing hydrogen, sulfur, fermentation, and simple-cell respiratory authoring outputs remain byte-for-byte unchanged when the work budget is enabled.
2. Opportunity work is reserved before success draws; changing the success outcome cannot give another process same-tick extra work.
3. Enumeration, priority, worker, and save/load changes preserve all admitted work, denied work, reaction draws, claims, and state hashes.
4. Structure assignments conserve exactly, preserve radius on organization or scale speciation, and sum to the existing viable-structure health input.
5. Combined organization-and-scale maturation fills assignments deterministically and never grants mature radius or processing capacity immediately.
6. The compartmentalized and Scale-I demand tables above reproduce exactly with whole extents.
7. Primitive-radius proto-eukaryotic direct respiration remains near the senescence boundary with current organic uptake; a supplied second carbon path makes it materially faster.
8. A complete maximum-search Scale-I predator below approximately `1.11` fully recovered primitive prey/day cannot replace itself indefinitely; a base hunter reaches that landmark near `350` uniform primitive prey per tile under the analytical encounter fixture.
9. Scale II encounters internal-processing contention even with unlimited prey and environmental substrates.
10. Client and autonomy diagnostics attribute every denied extent to processing work or the true non-processing limiter, never both.

Additional coupled fixtures must reproduce the `320` passive and `428` maximum-search Scale-I costs, the `33.40` primitive-prey replacement demand, and the Scale-II small-prey incompatibility.

# Remaining calibration inputs

The analytical complex-cell package is now closed enough for executable fixtures. One input remains outside this pass:

1. **Population acceptance bands.** Multi-seed fixtures must replace the analytical `0.75` encounter-realization assumption with observed pursuit, handling, clustering, competition, prey growth, and depletion outcomes, and must test specialized LDO competition against the new weighted-access landmarks.

This remaining work does not reopen the selected organization budgets, process loads, scale costs, particulate quotas, movement profiles, specialized LDO profile, or respiratory ceiling unless executable results miss their stated acceptance regions materially.

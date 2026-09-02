# Oxygenic Photosynthesis

Status: first v1 reaction, trait, light, quota, upkeep, oxygen-source, and authoring-balance parameters fixed; executable producer-consumer ecosystem validation remains

Sources: [aerobic respiration](AEROBIC_RESPIRATION.md), [founding metabolisms](FOUNDING_METABOLISMS.md), [world/climate calibration](WORLD_CLIMATE_CALIBRATION.md), [gas transport](GAS_TRANSPORT_AND_ATTRITION.md), [resource model](RESOURCE_MODEL.md), [trait catalogue](TRAIT_CATALOGUE.md), and [population ecosystem validation](POPULATION_ECOSYSTEM_VALIDATION.md).

# Purpose

Define the first metabolism that escapes dependence on a scarce geological electron donor by using water and light to fix carbon while releasing oxygen. It must be a major ecological transition without becoming universal free growth: it remains restricted by daylight, water depth, cloud, turbidity, carbon dioxide, manganese/calcium catalysts, oxygen tolerance, photosystem upkeep, nutrient supply, reserve capacity, and ordinary reproduction costs.

The rule pack also closes the first source side of the oxygen economy. Oxygen release must use the same tile-local `O2` reservoir, exchange, accessibility, stress, respiration, attrition, histories, and conservation rules already defined elsewhere.

# Fixed v1 decisions

1. Oxygenic photosynthesis is an external energy-capture reaction and a late extension of the sulfide-phototrophy photosystem lineage.
2. It fixes one `CO2` into one new `ReserveOrganic(CH2O)` and releases one `O2` per whole extent. Water and light are boundary/opportunity inputs, not finite tile stocks.
3. It does not recharge `SpentReserveCarrier`; that remains a separately evolved internal-metabolism capability. Without carrier retention, maintenance spending follows the ordinary tile-waste route.
4. Oxygenic and sulfide phototrophy share the organism's photosynthetic light-capture budget. Acquiring the new path adds donor flexibility, not a second independent sunlight allowance.
5. The complex photosystem includes a narrow intrinsic donor-selection policy. General `MetabolicRegulation` is not a prerequisite, but more sophisticated hedging remains an evolved option.
6. V1 uses a saturating light response but no explicit photoinhibition damage. Excess light above the base saturation point is unused.
7. The first oxygenic phenotype is aquatic. Terrestrial use requires the later terrestrial-adaptation rule pack and is not implied by water being a boundary resource.
8. Environmental oxygen attrition remains the existing linear `0.02%/hour` sink for v1. Finite crustal/oceanic reductant reservoirs and methane-coupled oxygen loss are future world-chemistry systems.

# Exact reaction

```text
OxygenicPhotosynthesis:
    1 CO2
  + 1 H2O(boundary)
  + light opportunity
    -> 1 ReserveOrganic(CH2O)
     + 1 O2

gross energy opportunity = 2
stored energy            = 1 in the new ReserveOrganic
dissipated energy        = 1
```

Matter balances exactly:

```text
inputs  = C1 H2 O3
outputs = CH2O + O2 = C1 H2 O3
```

The organism may execute an extent only when its new `ReserveOrganic` can enter free carrier capacity or be consumed by an already admitted same-tick biomass/mandatory-work bundle. V1 does not perform oxygen evolution merely to discard the fixed carbon. If neither destination is available, the extent is not requested. This makes oxygen output an auditable consequence of useful primary production rather than a free atmospheric source attached to DNA ownership.

`CO2` is debited from the tile's named gas account. Water crosses the inexhaustible aquatic boundary and is recorded as an input flow. `O2` is credited to the tile's named atmospheric account in the phase-7 deterministic output merge. It first participates in exchange, attrition, exposure, or respiratory claims on the next tick.

# Trait path, price, and complexity

The existing minimum conceptual path remains:

```text
SulfideAnoxygenicPhototrophy
└── ComplexPhotosystem                    100 MP / complexity 2
    └── ManganeseCalciumWaterOxidation    150 MP / complexity 3

cross-family requirement:
    OxygenToleranceI                       80 MP / complexity 1

complete cumulative milestone            330 MP / complexity 6
```

`OxygenicPhotosynthesis` is the reaction activated by the complete prerequisite set, not an unpriced fourth trait. Founder change capacity `3` cannot acquire the whole path in one event. A lineage may acquire `OxygenToleranceI` separately and then use `ExpandedChangeCapacityI` to add the two photosystem nodes together, or reach the milestone across multiple speciation events.

This path is directly adjacent only to the sulfide-phototrophy lineage in v1. A hydrogen descendant must first acquire the expensive compatible photosystem ancestry rather than purchasing water oxidation as an isolated shortcut. A future independent reaction-center route may provide another entry, but it requires its own price and machinery rather than a graph alias.

That adjacency is a gameplay abstraction, not a claim that science has established a direct sulfide-phototroph-to-oxygenic lineage. The ordering and early history of photosynthetic reaction centers and water oxidation remain actively debated. LYFE uses the sulfide branch because it already owns light capture and carbon fixation, producing a legible expensive upgrade while preserving the founding-metabolism tradeoff.

## Costs and persistent liabilities

| Trait | Upkeep | Suppression | Principal liability |
| --- | ---: | --- | --- |
| `ComplexPhotosystem` | `15 energy/hour` | Non-suppressible | Larger pigment/reaction-center system and repair burden |
| `ManganeseCalciumWaterOxidation` | `15 energy/hour` | Non-suppressible | Water-oxidation machinery and catalytic provisioning |
| `OxygenToleranceI` | `10 energy/hour` | Non-suppressible | Detoxification/protection even before oxygen becomes useful |

The canonical non-volcanic active producer therefore pays:

```text
base maintenance                         50
ComplexPhotosystem                       15
ManganeseCalciumWaterOxidation           15
OxygenToleranceI                         10
                                          --
total                                    90 energy/hour
```

Inherited sulfide-photo machinery is structurally extended rather than duplicated by `ComplexPhotosystem`; its opening maintenance remains represented inside the baseline founder profile. Other acquired traits and environmental stress add their ordinary costs. The 40-unit increase above base persists through darkness, making storage and the day/night cycle meaningful.

# Catalytic quotas and storage fit

The oxygen-evolving complex adds the following committed quotas:

| Micronutrient | Added quantity | Interpretation |
| --- | ---: | --- |
| Manganese | `4` | Four-manganese water-oxidation cluster abstraction |
| Calcium | `1` | Calcium member of the canonical cluster |

The existing sulfide-phototrophy profile already commits `25 Fe` and `10 Mg`, representing iron-bearing electron transfer and magnesium-bearing pigments, plus `10 K` and `10 Na`. `ComplexPhotosystem` raises the machinery cost but reuses those v1 catalyst identities rather than demanding duplicate Fe/Mg quotas.

The complete oxygenic producer quota is therefore:

```text
Fe 25 + Mg 10 + K 10 + Na 10 + Mn 4 + Ca 1 = 60
```

One additional reproduction set fits inside the primitive free-micronutrient capacity of `64`, leaving four units of headroom. `MicronutrientRetention` is therefore useful but not a hidden prerequisite, preserving the established 330-MP milestone. Any later oxygenic modifier that adds more than four quota units must require a storage upgrade in the same speciation event or fail trait compilation.

Mutation grants no catalyst matter. At the next phase-5 pre-external barrier, matching free Mn/Ca is deterministically promoted into the selected organism's new committed-quota deficit. Matter acquired during phase 6 waits until the following tick. An organism may therefore carry the complete quota into speciation, accumulate it later, import it through migration or predation, or remain oxygenically inactive. The manganese-free volcanic starting tiles still block activation and population expansion despite having the DNA and some light. Exact general promotion semantics are defined in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).

# Light response and throughput

The reaction reads `aquaticLightQ` already derived from solar geometry, cloud, depth, and turbidity in [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md). It does not independently reapply any attenuation.

```text
baseSaturationLightQ = 0.75
effectiveLightQ = min(aquaticLightQ, baseSaturationLightQ)

requestedOpportunities = floor(1,500 * effectiveLightQ)
successfulExtents = KeyedBinomial(
    requestedOpportunities,
    idealSuccessQ = 0.90)
```

The hourly parameter compiles proportionally if tick duration changes, while the one-hour fixture remains normative. At zero light, the reaction requests nothing but its non-suppressible machinery upkeep remains. Values above `0.75` do not increase base throughput and do not cause damage in v1.

`LowLightPhotosystem` and `HighFluxPhotosystem` are not prerequisites. Their exact changes to slope, saturation, photoprotection, quota, and upkeep remain a separate trait-balance pass. Until that pass, neither node silently changes the oxygenic coefficients above.

## Shared photosynthetic budget

An organism with both sulfide and oxygenic reactions receives one current `aquaticLightQ` budget:

```text
BuildPhotosyntheticPlan(organism, tileView, lightQ):
    calculate whole sulfide extents supportable by lightQ, H2S, outputs, and quota
    calculate whole oxygenic extents supportable by lightQ, CO2, outputs, and quota

    rank enabled reactions by expected admitted ReserveOrganic per light unit
    break ties by canonical reaction ID
    allocate light to the higher-ranked complete reaction
    allocate any remaining light to the other reaction

    emit substrate claims against the fixed plan
```

The current sulfur reaction wins in its ideal H2S-rich volcanic tile because it has the higher `2,083 × 0.95` reserve rate per unit light. Oxygenic photosynthesis wins where sulfide cannot support the light-backed plan. Contention can invalidate the forecast after claims are emitted; the organism does not receive same-tick hindsight or a second light budget. `MetabolicRegulation` may later enable conservative splitting or grant-history-aware selection.

# Reference-light calibration

The existing equatorial-equinox reference tile has `20 m` depth, cloud `0.10`, zero extra turbidity, and daily aquatic-light opportunity `5.7612957768`. Its twelve positive hourly oxygenic extents are:

```text
[132, 388, 618, 805, 937, 1006,
 1006, 937, 805, 618, 388, 132,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
```

This gives:

```text
daily successful extents       = 7,772
daily ReserveOrganic produced  = 7,772
daily CO2 consumed              = 7,772
daily O2 released               = 7,772
mean active source              = 323.833 per organism-hour
daily canonical upkeep         = 90 * 24 = 2,160
net replacement capital        = 5,612 ReserveOrganic/day
```

Using the standard `104,500` replacement-capital budget gives the expected-value authoring landmark:

```text
first division = 104,500 / (5,612 / 24)
               = 446.9 hours
               = 18.62 days
```

An executable fixture should initially accept `440..500 hours` because reserve floors, whole extents, day/night phase, capacity, age, nutrient acquisition, and cooldown timing can delay the expected-value result. This is intentionally slower than sulfide phototrophy in its ideal volcanic tile but substantially less substrate-bound and faster than the first fermentation-only niche.

The light thresholds are informative:

```text
maintenance-only daily light = 2,160 / (1,500 * 0.90)
                             = 1.600 opportunity-hours

division-by-30-days light = (2,160 + 104,500 / 30)
                            / (1,500 * 0.90)
                          = 4.180 opportunity-hours/day
```

Thus faint tiles may support persistence without reproduction, while robust expansion requires shallow, reasonably clear water. The existing `200 m` founder reference supplies only roughly one daily light-opportunity hour and is not a robust oxygenic habitat.

# CO2 access and replacement demand

Oxygenic photosynthesis uses the current `MixedOrigin` CO2 accessibility rule:

- Terrestrial access is full, once terrestrial photosynthesis exists.
- A volcanic aquatic tile whose emission profile includes CO2 has full access.
- A non-volcanic aquatic tile receives `floor(tileCO2 * 100 / (100 + depthMeters))` accessible CO2.

At shallow oxygenic depths, the `100,000,000` global background leaves CO2 abundant initially, but claims remain finite and contested. A complete replacement by hour 720 requires:

```text
replacement CO2 demand = canonical upkeep
                       + replacement capital / horizon
                       = 90 + 104,500 / 720
                       = 235.139 CO2 per founder-hour
```

This is below the reference-light ceiling of `323.833/hour`. One tile's `20,004/hour` diffuse geological CO2 source supports about `85` replacement-rate producers without net neighbor import, recycling, or respiratory CO2. Larger populations must draw down stock, import through rapid exchange, receive biological CO2, or slow their growth.

# Oxygen source, transport, and tolerance calibration

Oxygenic output uses the existing O2 transport class:

```text
environmental attrition = 0.02% per hour
exchange                 = 10% per compatible edge-hour
aquatic accessibility    = 100 / (100 + depthMeters)
```

The paired source calculation changes the provisional tolerance bands to:

| Profile | O2 soft threshold | O2 hard threshold | Upkeep |
| --- | ---: | ---: | ---: |
| Basal anaerobic | `100,000` | `1,000,000` | Included in basal maintenance |
| `OxygenToleranceI` | `30,000,000` | `100,000,000` | `10/hour` |
| `OxygenToleranceII` | `100,000,000` | `300,000,000` | Additional `15/hour` |

This creates three visible ecological regimes: trace/local oxygen harms basal anaerobes; first-tier tolerance enables producers and respirers during early oxygenation; second-tier tolerance becomes useful only after broad sustained oxygenation. Respiration grants no tolerance implicitly.

## World-scale authoring landmarks

On the default `32 × 17 = 544` world, ignoring biological consumption, the linear sink gives:

```text
worldMeanEquilibriumO2
    = totalMeanBiologicalSource * (1 - 0.0002)
      / (544 * 0.0002)
```

At the reference active source of `323.833/producer-hour`:

| Producers | Approximate world-mean O2 equilibrium |
| ---: | ---: |
| `100` | `297,581` |
| `1,000` | `2,975,814` |
| `10,081` | `30,000,000` / first-tier soft threshold |
| `33,604` | `100,000,000` / first-tier hard and second-tier soft threshold |

These are raw atmospheric-stock landmarks. Tolerance reads accessible O2. At the `20 m` reference depth, access is `100 / 120 = 0.833333`, so an otherwise uniform field needs approximately `12,098` reference producers to reach the first-tier soft exposure and `40,325` to reach its hard exposure for those aquatic organisms. A terrestrial organism receives the full raw balance. The no-consumption 95% approach time from the global uniform mode is approximately `14,977 hours`, or `624 days`. Exchange smooths spatial gradients much sooner but cannot accelerate total oxygen accumulation.

A constant-average 100-producer source on one central aquatic tile yields the following no-consumption steady field under default topology and fully compatible edges:

| Location | O2 stock |
| --- | ---: |
| Source tile | `448,579` |
| Orthogonal neighbor | `400,095` |
| Two tiles away | `363,587` |
| World mean | `297,581` |

For a full-access organism, approximately `224` such producers concentrated on one tile cross the basal `1,000,000` hard threshold in raw local stock, and about `6,688` reach the first-tier `30,000,000` soft boundary. At the producers' own `20 m` reference depth, the corresponding accessible-exposure counts are approximately `268` and `8,026`; first-tier hard exposure is approximately `26,752`. Charts and autonomous activation must not compare a raw tile stock directly with an aquatic threshold without applying accessibility.

These are upper-source landmarks, not promised plateaus. Full reserves, darkness, CO2/nutrient limitation, stress, death, and substrate contention suppress production. Conversely, population growth and favorable surface light can increase it. A replacement-rate producer releases `235.139 O2/hour`; one such producer can cover approximately `1.62` direct-respirer replacement O2 demands of `145.139/hour`, while one ideal reference producer can cover about `2.23`. The executable paired fixture must determine the realized producer/consumer ratio.

# Tick order and deterministic transactions

- Phase 2 applies prior biological O2 outputs already in the tile stock, environmental attrition, and exchange.
- Phase 3 evaluates oxygen exposure from the post-exchange accessible stock.
- Phase 5 evaluates light, photosynthetic donor selection, output capacity, and substrate requests.
- Phase 6 resolves CO2 or H2S claims. Water and light are validated boundary/opportunity inputs and cannot be contested as tile matter.
- Phase 7 executes only admitted whole extents, credits reserve, buffers CO2/H2S products and O2 outputs, and then resolves maintenance and biomass assembly.
- Phase 10 histories and aggregates record exact production, consumption, limiting factors, and species contributions.

Tile gas outputs from organisms are accumulated in deterministic per-tile buffers and merged in canonical resource/species/organism order or an equivalent order-independent checked reduction. Organism iteration or worker assignment cannot change O2 totals.

# Autonomous opportunity scoring

A complete oxygenic proposal declares:

```text
required material resource = CO2
non-material activation     = aquatic light, water habitat,
                              Mn/Ca quota availability,
                              OxygenToleranceI
output energy per extent    = 1
output ecological flow      = 1 O2
```

`materialOpportunityQ` uses exact accessible CO2 stock and trailing source/exchange/uptake history. Its optional quota profile declares the added activation quota `{Mn: 4, Ca: 1}`, the complete 60-unit reproduction set, the primitive shared uptake rate, and a `720 h` acquisition horizon. `quotaProvisionQ` estimates whether the proposed founders' carried inventory plus exact tile micronutrient stocks and recent renewal can commission the new machinery and provision one replacement. The oxygenic local opportunity is `CO2OpportunityQ * quotaProvisionQ`; Mn and Ca are never counted as per-extent fuel.

`activationFractionQ` then incorporates actual current committed quota, aquatic habitat, and the founders' daily light distribution. The replacement calculation integrates the tile's trailing or deterministic forecast daily light rather than treating noon light as a 24-hour constant. The advisory score uses the population-mean inventory before the random founder subset is drawn; the committed lineage may therefore activate somewhat sooner or later, which is reported rather than rerolled.

Recent H2S shortage, poor volcanic access, or energy-starvation deaths may favor the complete oxygenic proposal when suitable light and catalysts exist. Oxygen toxicity pressure must not favor oxygen production; it favors oxygen tolerance or an enabled consuming pathway. A preparatory photosystem node that does not complete useful oxygenic work receives zero material opportunity under the existing rule.

# Observability

A live tile/species view should expose:

- Current aquatic light and its solar, cloud, depth, turbidity, and saturation components.
- Potential, requested, granted, and executed oxygenic extents.
- CO2 accessibility, requests, grants, and limiting pressure.
- Reserve output, water-boundary input, and O2 production by species.
- Shared sulfide/oxygenic light-budget allocation and failed donor forecasts.
- Mn/Ca quota status and photosystem/tolerance upkeep.
- O2 stock, biological production, respiration, environmental loss, exchange, and projected net trend.
- Whether oxygenic expansion is maintenance-, light-, carbon-, quota-, reserve-capacity-, or reproduction-material-limited.

Reduced tiles retain only authorized coarse or last-observed values under the ordinary exploration rules.

# Required validation

- Every extent debits `1 CO2 + 1 H2O`, credits `1 CH2O + 1 O2`, stores one energy, and conserves CHO exactly.
- No O2 is produced when fixed carbon lacks a reserve, same-tick use, or other declared destination.
- Zero light, inaccessible CO2, missing Mn/Ca, or missing oxygen tolerance produces zero active extents.
- Quota commissioning promotes existing Mn/Ca without duplication, cannot use same-tick acquisition, and leaves a newly oxygenic sulfur organism targeting the complete 60-unit free reproduction set.
- The base light curve saturates at `0.75`, never becomes negative, and reproduces `7,772` daily reference extents.
- Oxygenic and sulfide phototrophy cannot each consume the full light budget in one tick.
- The canonical active profile pays exactly `90/hour` before other traits and stress, including darkness.
- The complete quota totals 60 and fits—but nearly fills—the primitive 64-unit reproduction store.
- The reference expected-value division is `446.9 hours`; executable results initially remain in `440..500 hours` under the defined fixture.
- The maintenance and 30-day replacement light thresholds reproduce `1.600` and `4.180` daily opportunity-hours.
- CO2 and O2 flows enter their exact named gas accounts and remain invariant under organism order, worker count, save/load, and client visibility.
- The no-consumption world and central-source fixtures reproduce their stated raw O2 equilibria and approach time within integer-remainder tolerance, while `20 m` exposure is exactly five-sixths of raw stock after named rounding.
- Producer-only worlds become oxygen-stressed as thresholds are crossed; adding compatible respirers lowers O2 without changing its transport coefficient.
- Autonomous scoring does not reward oxygenic production from O2 stress alone or from light without CO2/catalyst opportunity.
- Autonomous scoring treats CO2 as recurring fuel and micronutrients as one-time activation/reproduction provisioning, including the shared `0.5`-quantum/hour primitive uptake ceiling over 720 hours.

# Remaining decisions and recalibration triggers

The baseline is sufficiently specified for implementation and coupled simulation. These items remain deliberately open:

1. Exact `LowLightPhotosystem` and `HighFluxPhotosystem` modifiers, including whether the latter introduces explicit photoinhibition/repair costs.
2. Whether later world chemistry replaces the linear O2 sink with finite reducing reservoirs, methane oxidation, or threshold behavior. V1 retains the linear sink.
3. The terrestrial moisture gate and light response once terrestrial adaptation is specified. V1 oxygenic photosynthesis is aquatic.
4. Whether a later independent photosystem ancestry should let hydrogen descendants approach water oxidation without first acquiring the sulfide-photo lineage. The v1 adjacency is game-native because scientific reconstructions of the earliest reaction-center and water-oxidation path remain uncertain.
5. Executable multi-seed producer/anaerobe/respirer validation, which may tune the `1,500`, `0.90`, `0.75`, upkeep, or revised oxygen thresholds together rather than changing one coefficient in isolation.

Rerun this rule pack after changing solar/depth/turbidity calibration, CO2 background/source/access, reserve capacity, biomass cost, photosystem prices/upkeep/quotas, O2 exchange/attrition/access, respiration demand, tolerance curves, or the simulation tick duration.

# Biological anchors

The canonical catalyst quota follows the experimentally resolved `Mn4CaO5` oxygen-evolving cluster in photosystem II: [Umena et al.](https://pubmed.ncbi.nlm.nih.gov/21499260/). The saturating base light response and deferred high-light damage are guided by measured cyanobacterial transitions from light-limited growth to saturation or photoinhibition, while LYFE's normalized threshold remains game-native: [Ungerer et al.](https://pmc.ncbi.nlm.nih.gov/articles/PMC4981716/) and [Synechococcus photoprotection experiments](https://pmc.ncbi.nlm.nih.gov/articles/PMC3097228/). Geological evidence and Earth-system models indicate that oxygen production can precede broad atmospheric accumulation because environmental reductant sinks matter; this supports retaining an explicit sink and treating richer redox reservoirs as future work rather than equating first production with instant global oxygenation: [Planavsky et al.](https://www.nature.com/articles/ngeo2122) and [Horne et al.](https://doi.org/10.1029/2023GC011252). A modern review likewise treats the origin and ordering of reaction centers and water oxidation as unresolved, so the sulfide-lineage prerequisite should be understood as a game graph rather than settled phylogeny: [Sánchez-Baracaldo and Cardona](https://nph.onlinelibrary.wiley.com/doi/10.1111/nph.16249).

These sources motivate the reaction, dependencies, light shape, and ecological direction. Mutation prices, quotas, normalized light coefficients, upkeep, gas thresholds, and population landmarks are versioned LYFE balance values rather than literal biochemical measurements.

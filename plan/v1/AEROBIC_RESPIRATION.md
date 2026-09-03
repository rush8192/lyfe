# Aerobic Respiration

Status: first v1 reaction, trait, tolerance, quota, regulation, contention, and authoring-balance parameters fixed; paired oxygenic source is specified and executable population validation remains

Sources: [resource model](RESOURCE_MODEL.md), [organic uptake and fermentation](ORGANIC_UPTAKE_AND_FERMENTATION.md), [population ecosystem validation](POPULATION_ECOSYSTEM_VALIDATION.md), [gas transport](GAS_TRANSPORT_AND_ATTRITION.md), [internal allocation](INTERNAL_STORAGE_AND_ALLOCATION.md), [simulation loop](SIMULATION_LOOP.md), [trait catalogue](TRAIT_CATALOGUE.md), and [organism health calibration](ORGANISM_HEALTH_CALIBRATION.md).

# Purpose

Define the first oxygen-consuming metabolism as a materially balanced internal catabolic system. Respiration must create a substantial incentive to enter oxygenated habitats without becoming free energy: it requires a suitable organic electron donor, accessible atmospheric oxygen, oxygen tolerance, iron/copper catalysts, regulatory and respiratory upkeep, and the ordinary costs of building replacement biomass.

V1 supports both direct aerobic use of the named labile substrate and cross-feeding on the reduced product left by fermentation. It does not treat generic organic elemental pools, structural residue, or arbitrary remains as respiratory fuel.

# Fixed v1 decisions

1. Oxygen respiration is an internal metabolism, but its environmental fuel and O2 inputs are acquired through coupled phase-6 claims and consumed atomically in phase 7.
2. Direct labile respiration and reduced-product respiration share one respiratory electron-transport throughput ceiling; acquiring both does not duplicate machinery capacity.
3. Direct labile respiration stores more energy than fermentation followed by reduced-product respiration. The latter remains valuable because it consumes another species' waste or a prior mortality pulse.
4. Fermentation remains legal in oxygen. Regulation selects or combines enabled pathways; oxygen presence does not switch fermentation off globally.
5. Oxygen exposure, tolerance, and respiratory consumption remain separate. Phase-3 stress uses the accessible pre-consumption O2 field, while phase-7 respiration can lower future exposure.
6. Oxygen output or consumption changes the same tile-local atmospheric `O2` account that already exchanges rapidly and undergoes environmental attrition.
7. Respiration requires no complex-cell or eukaryotic prerequisite. Basal membranes can support the abstraction, but the pathway pays substantial regulatory, catalytic, and ongoing costs.

# Exact reactions

## Direct labile respiration

```text
AerobicLabileRespiration:
    1 LabileDissolvedOrganic(C6 H12 O6)
  + 3 O2
    -> 3 AssimilableCarrierMatter(CH2O)
     + 3 CO2
     + 3 H2O(boundary)

gross recoverable energy = 18
maximum stored energy     = 3 new ReserveOrganic
                          + 13 carrier recharges
                          = 16
dissipated energy         = 2
```

Matter balances exactly:

```text
inputs  = C6 H12 O12
outputs = 3 CH2O + 3 CO2 + 3 H2O = C6 H12 O12
```

The reaction assimilates half of the substrate carbon into new carrier matter and oxidizes the rest. This is the minimum explicit growth bridge that keeps an aerobic heterotroph from depending forever on an inherited volcanic carbon-fixation path. It is not a claim that every real organism uses a fixed one-half assimilation ratio.

The shorthand also separates new matter from energy capture. The compiler must not credit sixteen newly created matter-bearing reserve carriers from one six-carbon input. Its canonical execution is:

```text
matter transaction:
    1 LabileDissolvedOrganic(C6 H12 O6) + 3 O2
        -> 3 AssimilableCarrierMatter(CH2O)
         + 3 CO2 + 3 H2O(boundary)

energy transaction:
    gross opportunity = 18
    store 1 energy in each admitted AssimilableCarrierMatter quantum
        by crediting it as ReserveOrganic(CH2O)
    recharge up to 13 SpentReserveCarrier(CH2O)
        -> 13 ReserveOrganic(CH2O)
    dissipate 2 plus any unadmitted storage opportunity
```

`AssimilableCarrierMatter` is a reaction-output role, not a persistent resource ID. Each whole output quantum enters `ReserveOrganic` when shared carrier capacity is available; otherwise it is routed immediately to matching generic organic C/H/O tile waste with no stored energy. Energy opportunity is not a storable resource account. The remainder authorizes `RechargeReserveOrganic` using already-spent carrier matter; if fewer than 13 spent carriers are available, unused opportunity dissipates rather than blocking the balanced matter reaction. This preserves matter, provides bounded organic growth feedstock, and prevents respiration from manufacturing sixteen carrier molecules from six substrate carbons.

## Reduced-product respiration

```text
AerobicReducedProductRespiration:
    1 ReducedFermentationProducts(C4 H8 O4)
  + 2 O2
    -> 2 AssimilableCarrierMatter(CH2O)
     + 2 CO2
     + 2 H2O(boundary)

gross recoverable energy = 14
maximum stored energy     = 2 new ReserveOrganic
                          + 10 carrier recharges
                          = 12
dissipated energy         = 2
```

Matter balances as `C4 H8 O8` on both sides. The same output-routing and carrier-recharge rules apply.

The two reactions deliberately preserve this energy hierarchy per original `LabileDissolvedOrganic` quantum:

| Path | Stored reserve energy | Dissipated opportunity | Additional requirement |
| --- | ---: | ---: | --- |
| Direct aerobic labile respiration | `16` maximum | `2` minimum | `3 O2` |
| Fermentation then aerobic reduced-product respiration | `2 + 12 = 14` maximum | `1 + 2 = 3` minimum | `2 O2` after fermentation |
| Fermentation only | `2` | `1`, with reduced product retained | None |

One decayed structural quantum yields four labile quanta, so direct aerobic dissolved recovery stores at most `64` energy when adequate spent carriers exist after `100` was spent to assemble that structure. Fermentation followed by reduced-product respiration stores at most `56`; direct particulate digestion stores `32`; fermentation alone stores `8`. No route restores the construction energy, and every biological route also pays maintenance and machinery costs.

# Reserve-carrier clarification

`ReserveOrganic(CH2O)` is both matter and stored energy in the existing model. A reaction cannot oxidize external carbon to CO2 and also create additional matter-bearing reserve carriers from that same carbon. The reactions above explicitly partition fuel carbon between assimilation and oxidation, then use the oxidation opportunity to recharge the explicit zero-energy state of existing carrier matter:

```text
SpentReserveCarrier(C H2 O):
    usable energy = 0
    allowed reservoir = organism CarrierPool or remnant
    tile decay destination = matching generic organic C/H/O
```

The charged and spent forms have identical composition. `SpendStoredEnergy` converts retained `ReserveOrganic` to `SpentReserveCarrier` instead of emitting generic organic matter when `CatalyticCarrierRetention` is active. `RechargeReserveOrganic` reverses that material state only when a declared reaction supplies sufficient energy opportunity. Neither conversion creates, deletes, or changes carrier matter.

V1 should compile every energy-producing reaction into:

```text
balanced matter transformation
+ bounded energy opportunity
+ SpentReserveCarrier -> ReserveOrganic recharge extent
```

Founders without retention continue to release spent carrier products through the existing tile-waste route. Acquiring respiration also requires `CatalyticCarrierRetention`, defined below, to retain a bounded carrier pool. This avoids an otherwise hidden mass-balance contradiction and gives later advanced metabolisms a reusable energy-carrier contract.

# Trait and prerequisite package

## New and fixed nodes

| Trait | MP | Complexity | First effect and liability |
| --- | ---: | ---: | --- |
| `OxygenToleranceI` | `80` | `1` | O2 soft/hard thresholds `30,000,000 / 100,000,000`; `10` energy/hour non-suppressible upkeep |
| `OxygenToleranceII` | `120` | `2` | Thresholds `100,000,000 / 300,000,000`; additional `15` energy/hour upkeep |
| `ReducedProductUptake` | `60` | `1` | Accept `ReducedFermentationProducts`; `80/hour` uptake ceiling; `5` energy/hour suppressible upkeep |
| `MicronutrientRetention` | `60` | `1` | Free-micronutrient capacity `64 -> 128`; `5` energy/hour upkeep |
| `CatalyticCarrierRetention` | `60` | `1` | Add `SpentReserveCarrier` and retain/recharge a shared charged/spent carrier pool; `5` energy/hour upkeep |
| `OxygenRespiration` | `150` | `3` | Enable the shared respiratory machinery and the compatible reactions below; `20` energy/hour suppressible upkeep |

`CatalyticCarrierRetention` is placed under `InternalMetabolism` near `WasteRouting`/`CatalyticRecycling`, not under energy capacity. It does not add carrier matter, reserve capacity, or energy. It changes the founder waste route so future spending moves carrier matter into a dedicated internal spent state available for recharge. Acquisition starts with zero spent carriers and never reconstructs matter emitted before speciation. The assimilated reaction outputs above can add at most three or two new carrier quanta per extent, so no artificial priming transaction or initialization flag is required.

Primitive fission and offspring allocation split the charged and spent carrier balances with the rest of the reserve account. Speciation changes future routing but does not alter either balance. An organism with no spent carriers may still execute a balanced respiratory reaction and admit its small assimilated output; most of the energy opportunity initially dissipates. Ordinary movement, upkeep, maintenance, growth, and reproduction spending then creates spent carriers that later extents can recharge.

Charged `ReserveOrganic + SpentReserveCarrier` share one physical `EnergyCarrierPool` capacity equal to the organism's currently commissioned reserve capacity. The DNA compiles the maximum, while conserved storage structure and organization activation determine the current value as defined in [ENERGY_STORAGE.md](ENERGY_STORAGE.md). This one-to-one matter/energy capacity equivalence is valid for v1 because `ReserveOrganic` has energy density `1`; a future storage chemistry with another density must define a separate carrier-load conversion. Only charged reserve contributes to stored energy, reserve fraction, work affordability, or health. Recharging moves matter between states and cannot exceed the spent balance. Assimilated carrier output increases total pool matter only when capacity is free; otherwise its matter follows the declared tile-waste route.

`OxygenRespiration` requires:

```text
MetabolicRegulation
+ GeneralizedOrganicCatabolism
+ OxygenToleranceI
+ MicronutrientRetention
+ CatalyticCarrierRetention
+ at least one compatible acquisition path
```

`AerobicLabileRespiration` additionally requires `OrganicResourceUptake`. `AerobicReducedProductRespiration` additionally requires `ReducedProductUptake`. The latter is a child of `OrganicResourceUptake`, so every first reduced-product respirer can also use labile substrate when it is available.

The high total path cost is intentional. Respiration is a late ecological transition that becomes powerful only after oxygenic producers exist; it is not the automatic next purchase after fermentation.

# Catalytic quotas

The first respiratory machinery requires a distinct committed catalytic quota:

| Micronutrient | Quantity | Interpretation |
| --- | ---: | --- |
| Iron | `20` | Heme/iron redox-center abstraction |
| Copper | `5` | Canonical heme-copper terminal oxidase abstraction |

These quotas are not consumed per extent. Losing them scales or disables respiration through the ordinary capability-quota rule. Growth and reproduction must provision a complete additional set.

The added 25-unit set makes the extra reproduction quota `82` for a hydrogen lineage and `80` for a sulfur lineage before other advanced traits, exceeding the primitive 64-unit free-micronutrient store. `MicronutrientRetention` therefore raises capacity to 128 and is a hard prerequisite rather than an optional convenience. The volcanic founder tile's zero copper remains a deliberate ecological block: acquiring DNA alone does not fill the quota.

# Oxygen exposure and access

The base anaerobic and evolved thresholds are:

| Profile | O2 soft threshold | O2 hard threshold | Upkeep |
| --- | ---: | ---: | ---: |
| Basal anaerobic | `100,000` | `1,000,000` | Included in basal maintenance |
| `OxygenToleranceI` | `30,000,000` | `100,000,000` | `10/hour` |
| `OxygenToleranceII` | `100,000,000` | `300,000,000` | Additional `15/hour` |

Between soft and hard boundaries, O2 uses the existing acute-chemical quadratic stress curve. Beyond hard, it uses the same direct-death curve and cap. Tolerance is not a resource grant and respiration does not increase these thresholds implicitly.

For terrestrial organisms, exposure and claim access use the full tile O2 balance. For aquatic organisms, both use the existing `AtmosphericDepthLimited` accessibility:

```text
accessibleO2 = floor(tileO2 * 100 / (100 + waterDepthMeters))
```

Using the same accessible field means deep aquatic organisms are neither allowed to respire inaccessible atmospheric oxygen nor harmed as if that oxygen were fully dissolved locally. A future atmosphere/dissolved-gas split may replace this abstraction.

These paired-system thresholds place first-tier soft stress near the no-consumption raw world mean generated by roughly ten thousand reference oxygenic producers and its hard boundary near roughly thirty-four thousand for full-access organisms. The `20 m` aquatic reference instead needs roughly twelve thousand and forty thousand because it experiences five-sixths of raw stock. This creates a visible transition at v1-scale populations without making `OxygenToleranceI` immune to sustained oxygenation. Exact source, access, and plateau calculations are defined in [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md).

# Throughput and upkeep

Both reactions share:

| Parameter | V1 value |
| --- | ---: |
| Base shared respiratory opportunities | `80` extents per simulated hour |
| With `ProtoEukaryoticOrganization` | `160` extents per simulated hour |
| Ideal per-extent success factor | `0.95` |
| Respiratory machinery upkeep | `20` energy/hour while active |
| Minimum executable extent | `1` whole reaction |
| Oxygen concentration response | Linear claim/grant; no separate half-saturation curve |

The compiled shared ceiling is applied before reaction-specific fuel selection. Acquiring both reactions in a simple or compartmentalized cell therefore permits flexibility, not `160` extents/hour. `ProtoEukaryoticOrganization` raises the one shared ceiling to `160`; it does not grant `160` opportunities to each reaction. Base direct-labile and reduced-product uptake ceilings remain `120` and `80`. `DissolvedOrganicSpecialization` raises only the LDO acquisition ceiling to `240`, allowing a proto-eukaryotic cell to fill all 160 opportunities from that fuel at additional upkeep and quota cost; without it, mixed fuel is required. See [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md) and [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md).

The canonical active direct-labile profile, before inherited founding-path upkeep or environmental stress, pays:

```text
base maintenance                 50
MetabolicRegulation               5
GeneralizedOrganicCatabolism     10
OxygenToleranceI                 10
MicronutrientRetention            5
CatalyticCarrierRetention         5
OrganicResourceUptake             5
OxygenRespiration                20
                                  --
total                           110 energy/hour
```

The reduced-product profile substitutes its 5-unit uptake cost but may retain `1.25/hour` from a suppressed labile-uptake pathway when both are acquired, giving `111.25/hour`. Marked respiration and uptake machinery retain the ordinary 25% suppressed cost. Tolerance, retention, carrier handling, and regulation remain non-suppressible.

At ideal age and environmental conditions, the base `80`-opportunity ceiling produces an expected `76` successful extents/hour:

| Reaction | New carrier matter/hour | Maximum recharge capacity/hour | Steady growth-capital gain/hour |
| --- | ---: | ---: | ---: |
| Direct labile | `228` | `988` | approximately `228` after active costs are recharged |
| Reduced product | `152` | `760` | approximately `152` after active costs are recharged |

The distinction matters. Maintenance and other ordinary energy spending convert charged carriers to spent carriers, so abundant oxidation opportunity can restore that energy without adding matter. Structural assembly consumes carrier matter into biomass, so long-term reproduction is limited by the assimilated `228` or `152` new carriers/hour. These support at most `2.28` or `1.52` structure/hour before reserve provisioning, below the primitive three-structure/hour assembly ceiling. If the shared carrier pool fills, assimilated output is routed to waste and growth pauses until matter capacity is freed; respiration may still recharge spent carriers.

# Regulation and fuel selection

At phase 5, the default `FacultativeRespiration` policy uses the stable current tile/organism view:

1. Reject respiration unless tolerance, carrier capacity, catalytic quotas, lifecycle, and at least one complete reaction are active.
2. Calculate each reaction's current storable energy from its assimilated output, free carrier capacity, spent-carrier balance, and recharge ceiling. Derive the minimum maintenance-supporting batch from that state rather than assuming the maximum yield. Growth/replacement projections separately use assimilated carrier matter, because recharging spent matter cannot build a new body.
3. Treat a reaction as materially plausible only when current accessible fuel and O2 can support that batch before other organisms' unknown claims.
4. Allocate the organism's compiled shared respiratory-opportunity ceiling to direct labile respiration first because it stores more energy per original substrate. Use reduced products for remaining capacity.
5. If planned respiratory output is below the current non-growth obligation and fermentation is enabled, activate fermentation against remaining labile substrate as a fallback. Otherwise suppress fermentation for that tick.
6. Once claims are emitted, active costs remain. Contention failure cannot retroactively enable fermentation or suppress respiration.

The policy knows current stocks and the organism's prior state, not future grants. It can therefore make a bad choice under sudden contention; regulation improves adaptability without guaranteeing optimal foresight. `ReducedProductPreference`, mixed hedging, oxygen-affinity improvements, and learned grant-ratio policies are future regulation traits if population fixtures show a useful niche.

# Coupled external claims

Respiration needs fuel and O2 from different tile accounts but must debit neither unless a whole reaction executes. V1 generalizes ordinary proportional contention to bounded stoichiometric bundles:

```text
CoupledReactionClaim:
    claimant_id
    reaction_id
    maximum_candidate_extents
    required_inputs[(source_account, resource_id, units_per_extent)]
    input_contention_weight[(source_account, resource_id)] # default 1
    destination_and_output_proof
    priority_class                    # ordinary for respiration
```

```text
ResolveCoupledReactionClaims(availableByResource, claims, tickKey):
    discard invalid, zero, or output-unaccounted claims
    remainingExtent[i] = maximumCandidateExtents[i]
    admittedExtent[i] = 0

    while any remaining extent can fit as a complete bundle:
        for each resource r:
            actualDemand[r] = sum(remainingExtent[i] * unitsPerExtent[i,r])
        scarceResources = resources where actualDemand[r] > available[r]

        if scarceResources is empty:
            atomically reserve every remaining complete bundle
            break

        for each scarce resource r:
            weightedDemand[r] = sum(remainingExtent[i]
                                  * unitsPerExtent[i,r]
                                  * inputContentionWeight[i,r])
            pressureQ[r] = available[r] / weightedDemand[r]

        for each claim i:
            commonScaleQ[i] = min(1,
                min(pressureQ[r] * inputContentionWeight[i,r]
                    for each scarce required resource r))
            exactEntitlement[i] = remainingExtent[i] * commonScaleQ[i]
            baseExtent[i] = floor(exactEntitlement[i])

        atomically reserve every base extent and subtract its complete bundle
        residualClaims = feasible unmet claims sorted by
            descending fractionalPart(exactEntitlement[i]), then
            stable Hash(tickKey, claimant_id, reaction_id)
        give at most one additional complete extent to each feasible claim
            in that order, then repeat with the remaining stock and demand

    return whole admitted extents in canonical claimant order
```

Only resources that are actually scarce constrain the weighted scale. Consequently, sufficient physical stock always grants every valid request regardless of weights. For a scarce resource, the base cannot overdraw it because every claim using `r` is scaled by at most `available[r] * weight[i,r] / weightedDemand[r]`. The bounded residual rounds allocate only feasible whole bundles, change exact-tie rank by tick/domain, and terminate when all demand is met or no complete bundle fits. A claim receives every required input for an extent or none. Runtime never transfers unusable partial fuel into organism storage, and coupled throughput can bypass retained-store capacity only for matter consumed by the same committed reaction.

All coupled input weights default to `1`. `DissolvedOrganicSpecialization` sets only the `LabileDissolvedOrganic` leg to `2`; its O2 leg remains `1`. Thus two otherwise equal direct respirers contesting 120 LDO with abundant O2 receive 40 and 80 candidate extents when only one is specialized, while 240 LDO supplies both full 120-extent requests. If O2 alone is scarce, the acquisition trait conveys no O2 priority. Claims from the same organism for the same resource are normalized before weighting, so reaction splitting cannot multiply access.

Before emitting a bundle, the organism admits its age/environment/catalyst-limited share of its compiled respiratory-opportunity ceiling through the shared internal-processing budget at one work unit per opportunity, then samples successful candidate extents with the keyed binomial. The ceiling is `80` for simple and compartmentalized cells and `160` with `ProtoEukaryoticOrganization`. Phase-5 fuel selection assigns the shared spent-carrier balance across the plan in reaction-priority order: up to `13` recharges per direct extent and `10` per reduced-product extent. The two reactions cannot independently reserve the same spent matter. Assimilated outputs reserve free carrier capacity where possible and otherwise carry a proved tile-waste destination, so lack of storage reduces energy capture rather than invalidating the matter reaction. Contention-denied candidates do not return reserved work to another same-tick process.

# First energy and population landmarks

Using a newborn-equivalent mature organism at `1,000` structure and `4,000` reserve, the primitive `104,500` replacement-capital budget, and no inherited-path or stress costs:

| Profile | Canonical active cost | Assimilated carrier/hour | Capital-budget first division |
| --- | ---: | ---: | ---: |
| Direct labile respiration | `110` | `228` | approximately `459 h / 19.1 d` |
| Reduced-product respiration | `111.25` | `152` | approximately `688 h / 28.6 d` |

These expected-value authoring landmarks treat oxidation opportunity as sufficient to recharge recurring active costs and use new assimilated carrier matter for the body-plus-reserve capital requirement. They precede whole-extent variance, the one-tick spent-carrier startup, nutrient acquisition, contention, age, and cooldown jitter. Direct respiration is slower than the 10–14 day founding metabolisms but materially faster than the approximately 29-day fermentation path. Reduced-product respiration is deliberately near the fermentation cadence and more resource-specific, rewarding a cross-feeding niche rather than replacing direct respiration.

The conservative replacement demands are:

```text
direct energy demand = 110 / 13
                     = 8.462 extents/hour
direct matter demand = 104,500 / 720 / 3
                     = 48.380 extents/hour
direct replacement demand = max(energy, matter) = 48.380 LDO/hour

reduced energy demand = 111.25 / 10
                      = 11.125 extents/hour
reduced matter demand = 104,500 / 720 / 2
                      = 72.569 extents/hour
reduced replacement demand = max(energy, matter) = 72.569 RFP/hour
```

Both replacement boundaries consume approximately `145.14 O2/hour`: direct uses three oxygen per extent and reduced-product respiration uses two. At the ideal 76 extents/hour, one organism consumes `228 O2/hour` on labile substrate or `152/hour` on reduced product.

The 100-producer mortality fixture's `285.816` labile/hour could therefore support at most about `5.91` direct-respirer replacement equivalents before O2, exchange, environmental stress, competing fermenters, or other costs. This is larger than the approximately three-fermenter niche but remains subordinate to the primary population. If all of that labile flux is fermented first, its equal reduced-product flux supports about `3.94` reduced-product respirers while the fermenters retain their own lower energy yield.

# Oxygen feedback and tick order

- Phase 2 applies environmental O2 sources, attrition, and exchange.
- Phase 3 evaluates O2 exposure and death risk from the accessible post-exchange stock.
- Phase 5 first promotes existing free micronutrients into compiled committed-quota deficits, then selects respiratory/fermentative activity, allocates internal-processing work, and builds successful candidate bundles from admitted opportunities.
- Phase 6 resolves and reserves coupled fuel/O2 claims.
- Phase 7 executes respiration, recharges retained reserve carriers, credits CO2 to the tile atmosphere, routes water to the aquatic/surface-moisture boundary, and then pays maintenance/growth.
- Newly produced CO2 and newly depleted O2 first affect gas exchange and exposure on the next tick.

Respiration is a biological O2 sink. It does not change O2's `10%` exchange rate, `0.02%` environmental attrition, or depth-access class. The oxygenic source magnitude, tolerance revision, and no-consumption plateaus are paired with these demands in [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md).

# Autonomous opportunity scoring

Each reaction compiles a multi-input `MaterialOpportunityProfile` over its exact organic fuel and `O2`. For each resource, calculate stock bridge and potential renewal against the reaction's stoichiometric replacement demand. The local reaction opportunity is the minimum across required resources after process coverage:

```text
reactionMaterialOpportunityQ = processCoverageQ
                             * min(resourceOpportunityQ[fuel],
                                   resourceOpportunityQ[O2])
```

When a proposal enables both respiratory reactions, its local respiratory opportunity is the maximum of the two reaction scores because the shared machinery may choose either complete path, never their sum. The ordinary founder-fraction, plan, tile-count, candidate-weight, persistence, and explanation rules then apply unchanged.

Oxygen stress pressure can favor `OxygenToleranceI`; energy shortage can favor respiration only when exact fuel and O2 opportunity are nonzero. CO2, generic organic matter, inaccessible deep-water O2, or reduced product without the matching uptake/reaction must not inflate the score.

# Observability

A live tile and species view should expose:

- O2 stock, accessible fraction, environmental loss, exchange, biological production, and respiratory consumption.
- Labile and reduced-product requested/granted/consumed flows by reaction.
- Assimilated carrier matter admitted to reserve versus routed to organic waste, and oxidation opportunity stored versus dissipated.
- Coupled-claim limiting input and returned/unused candidate extents.
- CO2 output and water-boundary flow.
- Active/suppressed respiration, fermentation fallback, and their upkeep.
- Respiratory iron/copper quota status and retained carrier capacity.
- Replacement-demand and material-opportunity breakdown for proposed respiration traits.

Reduced-visibility tiles remain subject to the ordinary knowledge filter; server autonomy always uses authoritative state.

# Required validation

- Both reactions conserve every CHO atom, route every assimilated quantum to reserve or explicit waste, and never credit matter-bearing reserve twice.
- Reserve recharge cannot exceed retained spent-carrier matter or energy opportunity; assimilated carrier matter cannot exceed free physical pool capacity.
- Direct respiration stores at most 16 of 18 opportunity units and consumes `1 LDO + 3 O2`; reduced-product respiration stores at most 12 of 14 and consumes `1 RFP + 2 O2`.
- Fermentation plus later product respiration stores 14 per original labile quantum, less than direct respiration's 16.
- Four structural-decay labile quanta can store no more than 64 reserve energy; no build/decay/respire cycle is energy-positive.
- The shared reaction ceiling is `80` total opportunities/hour for simple and compartmentalized cells and `160` with `ProtoEukaryoticOrganization`, never a separate ceiling per enabled fuel.
- At age multiplier `1.0`, ideal single-fuel base fixtures converge on `76` successful extents/hour. A fully supplied proto-eukaryotic mixed-fuel fixture converges on `152` successes across its `160` opportunities, subject to its separate uptake ceilings.
- Coupled claims are whole, conservative, capped weighted-proportional before integer residuals, invariant under enumeration/worker order, and replay-identical.
- With abundant O2 and exactly 120 LDO, equal 120-extent direct claims at LDO weights 1 and 2 receive 40 and 80 extents; with 240 LDO both receive 120. Scarce O2 is divided without an LDO-only weight advantage.
- Zero accessible O2 or missing fuel produces zero extents and zero material opportunity.
- A deep-water organism cannot claim or receive stress from more O2 than the configured depth-accessible fraction.
- Missing copper or iron disables/scales respiration; primitive 64-unit micronutrient storage cannot provision the complete advanced reproduction quota.
- Suppressed respiratory/uptake machinery retains 25% cost; oxygen tolerance and retention costs remain fully active.
- A resource-rich but anoxic population loses to fermentation; an otherwise equal oxygenated population with full quotas makes respiration advantageous.
- A producer-free fuel/carrier loop eventually exhausts usable energy; assimilated carbon does not make build/decay/respire energy-positive.
- Save/load and worker-count changes preserve carrier pools, O2/fuel bundles, reaction successes, gas outputs, score breakdowns, and state hashes.

# Recalibration triggers and remaining paired work

Rerun this rule pack after changing reserve-carrier representation, carbon-assimilation fraction, fermentation yield/product identity, organic decay, O2 access/exchange/attrition, tolerance curves, micronutrient quotas/capacity, growth cost/throughput, reproduction capital, regulation, or coupled-claim allocation.

The paired oxygenic rule pack now defines the exact reaction, Mn/Ca quota, photosystem upkeep, light/depth throughput, shared photosynthetic machinery, local/world no-consumption O2 landmarks, and revised `30m/100m` and `100m/300m` tolerance bands. Remaining work is an executable oxygenic-producer, anaerobe, fermenter, direct-respirer, and reduced-product-respirer ecosystem fixture; see [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md).

# Biological anchors

The v1 abstraction is guided by experimental evidence that aerobic growth can yield substantially more biomass than fermentation on comparable organic carbon, while exact yields remain organism- and substrate-dependent: [Denger et al. co-culture experiments](https://www.frontiersin.org/journals/microbiology/articles/10.3389/fmicb.2018.02792/full). The canonical terminal oxidase quota uses iron and copper because experimental work identifies a heme-iron/copper oxygen-activating center in cytochrome oxidases: [Buse et al.](https://pmc.ncbi.nlm.nih.gov/articles/PMC2144334/). Oxygen tolerance remains a distinct trait because measured anaerobe survival correlates with oxygen-reduction and antioxidant capabilities rather than following one universal threshold: [Tally et al.](https://journals.asm.org/doi/10.1128/aem.36.2.306-313.1978).

These sources motivate direction and dependencies, not LYFE's numeric coefficients. The yields, rates, thresholds, costs, and quotas above are game-native balance values constrained by mass conservation and population fixtures.

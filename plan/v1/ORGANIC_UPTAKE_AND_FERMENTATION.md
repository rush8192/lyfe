# Organic Uptake and Fermentation

Status: first v1 semantic, regulation, base/specialized uptake, reaction, decay, dissolved-transport, and population authoring fixture complete; executable multi-seed validation remains

Sources: [resource model](RESOURCE_MODEL.md), [internal storage](INTERNAL_STORAGE_AND_ALLOCATION.md), [simulation loop](SIMULATION_LOOP.md), [lifecycle and recycling](LIFECYCLE_AND_RECYCLING.md), [trait catalogue](TRAIT_CATALOGUE.md), [evolution economy](EVOLUTION.md), and [population ecosystem validation](POPULATION_ECOSYSTEM_VALIDATION.md).

# Purpose

Define the first heterotrophic energy path without allowing generic organic matter to become an unlimited energy source. The rule separates access to small dissolved organic matter from its internal catabolism, gives decaying biomass a lower-value tile-wide recycling path, and preserves reduced fermentation products for the respiratory path in [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md) or future syntrophy.

The semantic rules in this document are fixed for the first v1 rule pack. Numeric throughput, upkeep, decay, and yield values are the first balance fixture and remain versioned calibration data.

# Design outcome

Organic uptake and fermentation are separate but coupled capabilities:

1. `OrganicResourceUptake` moves compatible low-molecular-weight organic substrate from the tile into an organism's metabolic plan. Acquisition alone creates no energy.
2. `Fermentation` transforms acquired substrate into `ReserveOrganic` without requiring oxygen.
3. Fermentation leaves a named reduced product containing matter and residual chemical opportunity. That product cannot be fermented again by the same rule.
4. Intact structural biomass remains inaccessible to this path. It requires direct particulate ingestion and digestion, or passive decay before its labile fraction becomes tile-wide.
5. Generic `OrganicCarbon`, `OrganicHydrogen`, and `OrganicOxygen` are not fermentable fuel. They may include already-spent carrier matter, so accepting them would permit repeated extraction of the same energy.

This makes fermentation an escape from volcanic dependence only where biological production and turnover supply organic substrate. It is not primary production and cannot sustain a sterile world by itself.

# Resource identities

V1 adds two biological compounds.

```text
LabileDissolvedOrganic:
    composition: C6 H12 O6
    phase: DissolvedOrMobile
    biologicalForm: Organic
    tileReservoir: OrganicPool
    organismReservoir: AvailableStore.DissolvedMacronutrientStore
    storageLoadPerQuantum: 24
    usableEnergyPerResourceQuantum: 0
    uptakeTags: [LabileDissolvedOrganic, FermentableOrganic]
```

`LabileDissolvedOrganic` is a glucose-equivalent accounting unit for small, readily transported biological organics. It is not a claim that the tile pool contains only glucose or that all organic compounds share one molecular pathway.

```text
ReducedFermentationProducts:
    composition: C4 H8 O4
    phase: DissolvedOrMobile
    biologicalForm: Organic
    tileReservoir: OrganicPool
    organismReservoir: AvailableStore.DissolvedMacronutrientStore
    storageLoadPerQuantum: 16
    usableEnergyPerResourceQuantum: 0
    uptakeTags: [ReducedOrganicProduct, RespirableOrganicFuture]
```

`ReducedFermentationProducts` is an aggregate of organic-acid-, alcohol-, and other reduced-product-like matter rather than one literal molecule. It has no directly spendable energy and is not a valid input to the v1 fermentation reaction. Its resource identity supplies the separately gated aerobic cross-feeding reaction in [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md) and preserves room for later syntrophy without allowing the fermentation pathway to run in a cycle.

For both compounds, `usableEnergyPerResourceQuantum = 0` means that an organism cannot debit the compound directly as stored reserve. It does not mean the compound is chemically inert. A compatible reaction declares its own bounded energy opportunity, substrates, acceptors, and products; the compiler validates the complete directed reaction graph against energy-restoring cycles.

Rule compilation must reject either compound as a substitute for generic elemental organic pools or as a general-purpose reserve. `ReserveOrganic` remains the only v1 internally spendable energy carrier.

# Source from structural decay

Ordinary remnant structural decay no longer sends all released structural matter directly to generic organic elemental pools. Each whole structural quantum released by the existing 30-day reference cohort executes:

```text
1 StructuralBiomass(C100 H170 O40 N20 P2 S1)
    -> 4 LabileDissolvedOrganic(C6 H12 O6)
     + 76 OrganicCarbon
     + 122 OrganicHydrogen
     + 16 OrganicOxygen
     + 20 OrganicNitrogen
     + 2 OrganicPhosphorus
     + 1 OrganicSulfur
```

The reaction balances exactly:

```text
4 LabileDissolvedOrganic = C24 H48 O24
generic remainder         = C76 H122 O16 N20 P2 S1
total                     = C100 H170 O40 N20 P2 S1
```

Committed micronutrient quotas remain separate and release through their existing proportional rule. Structural material consumed by particulate digestion never also participates in this decay reaction.

`ReserveOrganic` that passively decays continues to lose its remaining useful energy and release constituent generic organic pools. It does not become `LabileDissolvedOrganic`. Spent reserve emitted by maintenance and work likewise remains generic organic matter. These distinctions prevent a spend-decay-ferment energy loop.

V1 provides no large prebiotic stock of labile organic substrate in the default survival scenario. Scenario-specific finite inventories remain possible, but they must be explicit boundary or setup transactions and are not a default source.

# Tile persistence and passive loss

At the `40 degrees C` aquatic reference:

| Resource | Reference half-life | Hourly decay per million | Destination | Energy disposition |
| --- | ---: | ---: | --- | --- |
| `LabileDissolvedOrganic` | `336 h` / 14 days | `2,061` | Exact constituent generic organic elemental pools | Remaining catabolic opportunity dissipates |
| `ReducedFermentationProducts` | `2,160 h` / 90 days | `321` | Exact constituent generic organic elemental pools | Remaining catabolic opportunity dissipates |

Both use the lifecycle decay temperature/moisture multiplier, fixed-point coefficient, and persisted-remainder algorithm. After entering generic organic pools, their matter remains subject to the separate 90-day organic-to-inorganic mineralization rule.

These are passive environmental transformations, not biological fermentation or respiration. They produce no `ReserveOrganic`. The longer reduced-product lifetime leaves a later ecological niche, while the shorter labile-substrate lifetime ties fermenter success to relatively recent biological production and death.

Both compounds use the first `DissolvedMobile` profile:

| Parameter | V1 value |
| --- | ---: |
| Base exchange across one fully compatible edge | `1,500` per million per hour, or `0.15%` |
| Aquatic-to-aquatic compatibility | `1.00` |
| Aquatic-to-terrestrial compatibility | `0.10 × terrestrialSurfaceMoisture` |
| Terrestrial-to-terrestrial compatibility | `0.25 × min(surfaceMoistureA, surfaceMoistureB)` |
| Dry terrestrial compatibility | `0` |
| Directionality | Symmetric; no v1 runoff or current |

Exchange uses a stable post-source/post-passive-loss view, one persisted fixed-point remainder per undirected edge and resource, and exact signed transfers from the higher-stock tile to the lower-stock tile. `x` wraps and `y` remains bounded. The coefficient is far below the four-neighbor explicit-diffusion stability ceiling: a fully aquatic tile proposes at most `0.6%` total gross outbound exchange per hour before opposing inflows.

Only `LabileDissolvedOrganic` and `ReducedFermentationProducts` are assigned to `DissolvedMobile` in the first executable rule pack. Generic elemental organic/inorganic pools and micronutrients remain `TileBound` until their own ecology justifies reclassification. This prevents the organic-path decision from silently homogenizing every nutrient.

# Organic uptake

`OrganicResourceUptake` has the existing price of `40 MP` and change complexity `1`. Its first compiled effects are:

| Effect | V1 value |
| --- | ---: |
| Accepted tile substrate | `LabileDissolvedOrganic` only |
| Maximum planned uptake | `120` substrate quanta per simulated hour |
| Availability response | Linear claim and grant; no separate concentration-response curve |
| Passive upkeep while enabled | `5` reserve-energy units per simulated hour |
| Spatial requirement | None inside a tile; the tile pool is well mixed |
| Basal inventory policy | Current admitted reaction needs only |

The trait does not accept intact `StructuralBiomass`, `SpentStructuralResidue`, generic organic elements, or `ReducedFermentationProducts`. Other reactions may add accepted resources later through explicit typed effects.

V1 does not apply a Monod-like or other saturating concentration multiplier. A qualifying organism plans up to its compiled whole-extent ceiling, and the ordinary capped weighted-proportional claim resolver supplies `0..requested` substrate from the actual tile stock. In an uncontested tile, a stock below the request grants only that stock; under contention, grants divide by compiled weight. Tile quantity and competing demand therefore create scarcity without inventing a physical tile volume or a second arbitrary half-saturation scale. `DissolvedOrganicSpecialization` raises admissible uptake and weighted access inside this same ordinary class; it cannot request matter beyond a committed consumption or retention destination and does not receive an absolute priority over other claimants.

One substrate quantum has storage load `24`, so the primitive `512`-load dissolved store can retain only `21` whole substrate quanta. The `120`-quantum hourly uptake ceiling therefore does not grant a hidden capacity increase. Higher same-tick throughput is legal only through the atomic uptake-and-fermentation bundle below; any substrate retained after the bundle must fit the ordinary store.

## Dissolved-organic specialization

The first `DissolvedOrganicSpecialization` hypothesis is a competitive acquisition trait rather than a second organic reaction:

| Effect | Initial v1 value |
| --- | ---: |
| Prerequisite | `OrganicResourceUptake` |
| Mutation price / change complexity | `100 MP / 2` |
| Maximum planned `LabileDissolvedOrganic` uptake | `240/hour`, replacing `120/hour` |
| Ordinary contention weight | `2`, replacing `1`, for this resource only |
| Passive upkeep while acquisition is active | `12` energy/hour |
| Committed catalytic quota | `Zn 5` |
| Fermentation extent and success | Unchanged at `120/hour` and `0.90` |
| Reaction yield and accepted resources | Unchanged |

These numbers deliberately make the node situational rather than compulsory. Weight `2` creates an immediately legible mixed-population advantage while remaining inside the configured `1..4` weight range. The added 12-unit upkeep raises primitive active cost by roughly 15–18%, so equal-specialist populations lose carrying capacity instead of obtaining free throughput. `100 MP / complexity 2` makes the switch a meaningful evolutionary commitment, while `Zn 5` gives the additional transport/catalytic machinery a small reproducible material dependency that remains provisionable with the existing storage model. These are calibration hypotheses, not claims that one literal transporter has these costs.

The higher request ceiling supplies headroom for a compatible process that can actually use it. It does not let a fermenter over-request material it cannot consume: fermentation still requests at most 120 whole substrate extents/hour. A proto-eukaryotic direct respirer may request up to its shared 160-opportunity respiratory ceiling from labile substrate instead of being acquisition-limited to 120. Future compatible LDO reactions remain capped by both their own process ceiling and the specialized 240-unit acquisition ceiling.

The weight models higher-affinity or denser transport machinery without adding a concentration curve or privileged claim class. Before resolution, merge all ordinary LDO claims from one organism into one capped request, then use:

```text
effectiveDemand[i] = requestedAmount[i] * compiledLdoContentionWeight[i]

allocate available stock proportionally to effective demand
cap every grant at its requested amount
redistribute cap leftovers by the same weighted largest-remainder pass
```

Stable claimant ID breaks exact remainder ties. Splitting one organism's request across reactions cannot multiply its weight. If every claimant has the specialization, all weights cancel and the tile supports fewer—not more—organisms because the trait adds upkeep. With one base and one specialized fermenter each requesting 120 from a 120-unit pool, the first weighted allocation is 40 and 80. At 240 available, both receive their complete 120; specialization creates no matter.

The same resource-specific weight applies when LDO is one leg of an atomic respiration bundle. The coupled resolver weights only the scarce LDO input while retaining weight `1` for oxygen and grants complete stoichiometric bundles or none; see [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md). This prevents the specialization from accidentally becoming an oxygen-priority trait.

The upkeep is tagged `suppressiblePathwayUpkeep`: it is paid in full whenever the organism emits or retains an LDO acquisition plan and at 25% when metabolic regulation suppresses that acquisition. The zinc quota must be physically provisioned and is copied for reproduction through the ordinary committed-quota rules. The trait does not accept `ReducedFermentationProducts`, generic organic pools, or particles; it does not change fermentation or respiration stoichiometry, success probability, or yield.

This produces two intended niches:

- A specialist fermenter wins a larger share during mixed competition but is worse in an uncontested field because its reaction ceiling is unchanged and its recurring cost is higher.
- A proto-eukaryotic direct respirer can fill all 160 shared respiratory opportunities from abundant LDO. At ideal `0.95` success it completes 152 extents/hour, adds 456 new carrier units, supports four whole assembly extents/hour, and lowers the ideal `2,188`-structure assembly time from 730 to 547 hours.

For the primitive conservative replacement estimate, active hydrogen and sulfur specialist costs become `77` and `92` energy/hour. Their required LDO grants become `111.069/hour` and `118.569/hour`, compared with `105.069` and `112.569` for the base profiles. The 100-producer `285.816/hour` field therefore supports only about `2.57` hydrogen or `2.41` sulfur specialists if every consumer has the trait. The specialization redistributes scarcity and unlocks higher compatible throughput; it does not increase ecosystem carrying capacity by itself.

# Fermentation reaction

`Fermentation` retains its existing price of `80 MP` and change complexity `2`. It requires `OrganicResourceUptake` plus either `HydrogenAcetogenesis` or `GeneralizedOrganicCatabolism`.

The first exact reaction is:

```text
1 LabileDissolvedOrganic(C6 H12 O6)
    -> 2 ReserveOrganic(CH2O)
     + 1 ReducedFermentationProducts(C4 H8 O4)

gross recoverable energy = 3
stored energy             = 2
dissipated energy         = 1
```

Matter balances exactly:

```text
inputs:  C6 H12 O6
outputs: 2 ReserveOrganic = C2 H4 O2
         reduced product = C4 H8 O4
         total           = C6 H12 O6
```

The gross and stored energy values are game-native balance quantities anchored to the low useful yield of substrate-level phosphorylation, not a literal ATP conversion. The reduced product retains matter and future reaction opportunity but cannot be spent from `EnergyReserve`.

The first compiled effects are:

| Effect | V1 value |
| --- | ---: |
| Maximum reaction extent | `120` per simulated hour |
| Ideal per-extent success factor | `0.90` |
| Passive upkeep while enabled | `10` reserve-energy units per simulated hour |
| Oxygen requirement | None |
| Oxygen prohibition | None |
| Substrate per extent | `1 LabileDissolvedOrganic` |
| Reserve output per successful extent | `2` |
| Reduced-product output per successful extent | `1` |

Oxygen does not disable fermentation. Once respiration exists, `MetabolicRegulation` may prefer the higher-value reaction when its electron acceptor, tolerances, and substrates are available. Without suitable regulation, every enabled pathway retains its declared constitutive cost even when it cannot run.

Temperature, moisture, age throughput, catalytic availability, output capacity, and other compiled process modifiers apply without changing integer reaction coefficients. A successful extent either executes completely or not at all.

# Regulation profile

The first general metabolic-regulation profile is:

| Capability or state | V1 upkeep |
| --- | ---: |
| `MetabolicRegulation` control machinery | `5` energy/hour, constitutive and not suppressible |
| `GeneralizedOrganicCatabolism` active | `10` energy/hour |
| Any upkeep contribution explicitly marked `suppressiblePathwayUpkeep` | `100%` while active; `25%` while suppressed |
| Suppression transition | No separate energy cost, cooldown, or delay in v1 |

Structural targets, reproductive requirements, catalytic quotas, and costs not explicitly tagged as suppressible remain at `100%`. Suppression never removes DNA, matter, or prerequisites.

At phase 5, an organism with `MetabolicRegulation` deterministically selects the active/suppressed state of each eligible pathway for the complete tick using its current behavior, current tile view, available internal substrates, activation requirements, and compiled process priorities. A pathway selected to emit a claim or execute an internal reaction is active. A pathway not selected may be suppressed. The choice cannot be revised after contention results are known, so regulation cannot avoid active cost merely because its claim later lost contention. The next tick reevaluates from its new stable view.

An organism without `MetabolicRegulation` treats every acquired pathway as active for upkeep even when environmental opportunity is absent. `MetabolicRegulation` itself always pays its five-unit control cost. Fixed-point cost remainders represent fractional suppressed costs, so the organic pair's `5 + 10` active upkeep becomes exactly `1.25 + 2.50 = 3.75` energy/hour while both are suppressed.

For the sulfur bridge, `GeneralizedOrganicCatabolism` adds ten active energy/hour before organic uptake and fermentation costs. With all three organic capabilities active, the first sulfur heterotrophy accounting load is therefore:

```text
base maintenance                 50
MetabolicRegulation               5
GeneralizedOrganicCatabolism     10
OrganicResourceUptake             5
Fermentation                     10
                                  --
total                            80 energy/hour
```

Inherited founding-path upkeep and environmental stress remain additional. When the generalized and fermentation machinery are both suppressed but regulation remains enabled, their suppressible contributions retain 25% while the five-unit control cost remains.

# Atomic intent and execution

The organism must not claim substrate speculatively and then overflow its store when a later success roll fails. Whole opportunities first reserve shared internal-processing work from the stable phase input; candidate success is then determined before the external claim is emitted. A failed opportunity does not return work to another same-tick process.

```text
PlanFermentation(organism, tileView, tick, tickDuration):
    reject unless OrganicResourceUptake and Fermentation are active

    opportunityCount = compileRate(120 per hour, tickDuration,
                                   organism.fermentation_rate_remainder)
    opportunityCount = capByAgeEnvironmentAndCatalysts(opportunityCount)
    opportunityCount = admitThroughInternalProcessingBudget(
        opportunityCount, workPerOpportunity = 1)

    successfulCandidates = KeyedBinomial(
        RandomAddress(MetabolicOpportunity,
                      tick, organism.id, FermentationReactionId,
                      sampleIndex = 0),
        trials = opportunityCount,
        probabilityQ = compiledSuccessProbability(base = 0.90))

    extentCapacity = capByReactionThroughputAndAccountedOutputs(
        successfulCandidates,
        freeReserveCapacity,
        samePhaseCommittedReserveDebits,
        reducedProductWasteDestination)

    internalExtent = min(extentCapacity,
                         AvailableStore.LabileDissolvedOrganic)
    externalNeed = extentCapacity - internalExtent

    emit proportional ordinary-priority claim for externalNeed
    persist a plan that can execute no more than
        internalExtent + eventualExternalGrant
```

After ordinary tile contention resolves, phase 7 executes the admitted whole extents before mandatory maintenance. External substrate enters and leaves the atomic bundle without needing to fit simultaneously in retained storage. Substrate not included in a committed extent is never debited from the tile. Any pre-existing internal substrate not consumed remains subject to ordinary capacity and death-transfer rules.

The bundle may account for reserve that will be spent on already-known phase-7 mandatory maintenance, but it may not assume optional future growth to make output fit. If reserve output and the reduced-product tile destination cannot both be accounted, the extent is reduced before execution. No excess output disappears.

# First balance fixture

At ideal environmental conditions and abundant uncontested substrate:

```text
expected successful extents/hour = 120 * 0.90 = 108.0
expected reserve production/hour = 108.0 * 2 = 216.0

base maintenance                  = 50
OrganicResourceUptake upkeep      =  5
Fermentation upkeep               = 10
total recurring cost              = 65

expected ideal surplus/hour       = 151.0 ReserveOrganic
```

Including the primitive reserve floor, `8,500` pre-work reproduction gate, `500` work cost, age-throughput curve, and senescence schedule, the expected-value authoring fixture reaches its first division at hour `693`, or `28.88 days`. It begins senescence at hour `720`, and its idealized survival-weighted doubling time is approximately `79.7 days`. This is intentionally much slower than either founding metabolism in its ideal volcanic niche. The former `64`-extent ceiling could not replace one organism within its expected lifetime even with unlimited substrate; the population calculation and selected correction are defined in [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md).

One baseline mature corpse eventually releases at most:

```text
1,000 StructuralBiomass
    -> 4,000 LabileDissolvedOrganic
    -> 8,000 ReserveOrganic through fermentation
```

That is `8%` of the `100,000` reserve originally required to assemble the structure and one quarter of the `32,000` reserve available through direct particulate digestion. The intended recovery hierarchy is:

| Path | Reserve recovered per structural quantum | Access constraint |
| --- | ---: | --- |
| Direct particulate digestion | `32` | Local remnant contact, ingestion, digestion, and buffer capacity |
| Passive decay plus dissolved fermentation | `8` | Wait for decay, then compete for a tile-wide transient substrate |
| Passive mineralization | `0` | Returns matter to inorganic pools only |

The hierarchy makes reproduction and primary production necessary at ecosystem scale, rewards spatial scavenging, and still gives non-predatory heterotrophs a meaningful niche.

# Trait and lineage consequences

- The hydrogen path reaches `OrganicResourceUptake + Fermentation` for the established cumulative `120 MP` and change complexity `3`.
- The sulfur path still requires `MetabolicRegulation + GeneralizedOrganicCatabolism` before the same two nodes, for the established cumulative `220 MP` bridge beyond its opening loadout.
- In an ideal volcanic tile, a founding-path specialist should outgrow an otherwise comparable fermenter.
- In a recently productive or mortality-rich non-volcanic tile, fermentation may sustain and slowly grow a population that cannot access sufficient H2 or H2S.
- `DissolvedOrganicSpecialization` improves a lineage's share against base LDO claimants and unlocks the proto-eukaryotic direct-respiration ceiling, but decreases carrying capacity when every consumer bears its upkeep.
- In a sterile tile or a tile whose labile substrate has decayed, fermentation supplies no energy while its constitutive machinery remains costly.
- `ReducedFermentationProducts` feeds the separately defined oxygen-respiration reaction and preserves a later opportunity for syntrophy; neither path may recreate `LabileDissolvedOrganic` without an external energy source.

The autonomous-evolution pressure heuristic may score this path from recent `MaintenanceFailure` or energy-depletion deaths only in combination with the named material-opportunity score in [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md). That score uses exact `LabileDissolvedOrganic` stock and recent production/exchange/uptake flows, not generic organic abundance. Resource opportunity is not a mutation-price discount or a speciation-validity gate.

For `DissolvedOrganicSpecialization`, useful opportunity additionally requires either (a) recent ordinary-priority LDO contention denied an otherwise executable claim while competing non-specialist demand was present, or (b) a complete proposed direct-respiration phenotype has more than 120 usable respiratory opportunities and sufficient LDO/O2 supply. Raw LDO stock alone cannot make the acquisition-only node appear productive, and autonomous scoring must include its 12-unit upkeep and zinc provisioning.

# pH and product-toxicity deferral

V1 does not model tile pH, buffering capacity, acid dissociation, or fermentation-product toxicity. It also does not add a direct health penalty from `ReducedFermentationProducts` concentration.

Transport, enzyme operation, and primitive homeostasis are abstracted into the two pathway upkeeps and the one-unit per-extent dissipated loss. The server still records reduced-product stocks and flows so a future rule version can add product inhibition, acid stress, buffering geology, tolerance traits, or waste-removal symbioses without reconstructing historical matter.

Adding pH later requires a coherent aqueous-chemistry contract and is not permitted as an isolated scalar penalty hidden inside this reaction.

# Observability

A live tile exposes:

- Current `LabileDissolvedOrganic` and `ReducedFermentationProducts` quantities.
- Structural-decay production, passive loss, neighbor exchange, biological uptake, and fermentation-product release over the selected history window.
- Species aggregate requested, granted, consumed, successful, reserve-produced, and substrate-limited fermentation extents.
- Base versus weighted effective LDO demand, compiled contention weight, capped grant, and any redistributed remainder.

An inspected organism exposes organic-uptake and fermentation throughput, current limiting factor, passive upkeep, most recent reaction extent, and waste output. Reduced-visibility tiles retain only the ordinary coarse or last-observed resource information.

# Required validation

- Each structural-decay extent conserves `C100 H170 O40 N20 P2 S1` exactly and emits four labile-substrate quanta plus the declared generic remainder.
- Each fermentation extent consumes exactly `C6 H12 O6`, stores exactly two energy quanta in two `ReserveOrganic`, emits exactly one `C4 H8 O4` reduced product, and dissipates exactly one declared gross-energy unit.
- Generic organic elements, spent reserve products, `SpentStructuralResidue`, and `ReducedFermentationProducts` are rejected as fermentation inputs.
- A spend-decay-ferment cycle cannot increase or restore reserve energy.
- At the reference condition, labile substrate and reduced products reproduce their 14- and 90-day half-lives within fixed-point tolerance.
- Atomic same-tick uptake may exceed the retained `512`-load capacity only for substrate consumed by the same committed bundle; retained contents never exceed capacity.
- Claim grants are proportional and invariant under organism iteration and worker count.
- Specialized LDO weighting operates only inside the ordinary claim class, merges claims per organism before weighting, caps grants at requested usable matter, and conserves the tile stock exactly.
- One base and one specialized 120-unit request split a scarce 120-unit pool `40/80`, while a 240-unit pool grants `120/120`; equal weights produce the original proportional result.
- Keyed candidate success is identical across save/load, worker count, and claim enumeration order.
- At ideal conditions with the age-throughput multiplier fixed at `1.0`, a long-run uncontested reaction fixture converges on `108.0` expected extents and `216.0` reserve output per organism-hour within statistical tolerance.
- Specialization does not change that fermentation result. A fully supplied specialized proto-eukaryotic direct respirer instead converges on 152 successes and 456 new carrier units/hour across its 160 respiratory opportunities.
- One thousand fully decayed structural quanta can yield no more than `8,000` fermentation-derived reserve; direct digestion and decay cannot consume the same structural matter.
- No v1 health or reaction calculation reads an undeclared pH or acidity field.

# Calibration still required

1. Implement the mortality-balanced one-tile and multi-seed population fixtures defined in [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md); the authoring calculation selects a persistent but subordinate niche.
2. Validate the `120`, `0.90`, `5`, and `10` fixture against scarce-substrate contention and age/stress throughput.
3. Validate specialist invasion and loss: it should gain share against base uptake under scarcity, lose in an uncontested fermentation-only niche through upkeep, and improve a fully supplied proto-eukaryotic direct respirer without changing reaction yield.
3. Validate the fixed `0.15%` dissolved-organic exchange rate and moisture compatibility against generated aquatic regions and coastlines.
4. Revisit substrate and product persistence when respiration and biological mineralization reactions are defined.
5. Implement the named-stock/flow autonomous-opportunity fixtures and verify that generic organic waste alone produces zero material opportunity.
6. Validate the fixed 5-unit regulation cost, 10-unit generalized-catabolism cost, and 25% suppressed-upkeep fraction in variable-resource populations. The ideal hydrogen fixture above assumes both organic-pathway costs are fully active.

These are balance and integration tasks rather than missing semantic decisions.

# Biological anchors

The abstraction is guided by experimental evidence that low-molecular-weight dissolved organics are broadly accessible while degradation of higher-molecular-weight matter is more trait-dependent, and that fermentation product patterns and usable energy yields vary with organism and conditions:

- [Experimental aquatic-community degradation of low- and high-molecular-weight dissolved organic matter](https://www.nature.com/articles/ismej2015131)
- [Aquatic bacterial use of dissolved low-molecular-weight compounds](https://www.nature.com/articles/ismej2009120)
- [Fermentation products, acid tolerance, and energy use in Lactococcus cultures](https://journals.asm.org/doi/10.1128/AEM.65.6.2287-2293.1999)
- [Thermodynamics and observed microbial growth yields across fermentative and respiratory metabolisms](https://journals.asm.org/doi/10.1128/AEM.02425-10)

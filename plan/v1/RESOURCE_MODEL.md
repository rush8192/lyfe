# Resource and Energy Model

Status: first deep-dive draft; accounting architecture decided, balance values provisional

Sources: [NUTRIENTS vision](../../vision/NUTRIENTS.md), [WORLD vision](../../vision/WORLD.md), [ORGANISMS vision](../../vision/ORGANISMS.md), and [authoritative data model](DATA_MODEL.md).

# Purpose

Define the authoritative v1 model for matter, chemical energy, reservoirs, reactions, competition, and resource history. The model should be biology-inspired, internally mass-balanced, deterministic, efficient enough for the simulation hot loop, and explainable to a player.

This document fixes the accounting architecture needed by the other plans. Exact rates, capacities, efficiencies, and most reaction coefficients remain versioned balance data.

## Implementation checkpoint

`LEDGER-100` implements the first complete external-capture transaction against the
minimal official rules: `4 H₂ + 2 CO₂ -> 2 ReserveOrganic + 2 boundary H₂O`, with
three gross energy units divided into two stored and one dissipated. Typed account
keys distinguish tile atmosphere, organism energy reserve, matter boundary, energy
opportunity, and dissipated heat. Every transaction must exactly match its compiled
reaction, conserve each CHNOPS element and energy through checked `Int128` totals,
fit all finite balances, and produce the exact authoritative post-state promised by
the journal. Production history aggregation, all other reservoirs/reactions, and
the mass-balanced abiogenesis transaction remain later vertical slices.

# V1 scope and non-goals

V1 will model:

- The CHNOPS macronutrients as elemental quantities in organic and inorganic forms.
- Fourteen named, bioavailable micronutrients in one form each.
- A small catalogue of named atmospheric compounds with exact elemental composition.
- Abstract structural biomass and energy-bearing organic reserves.
- Matter transfers among tiles, organisms, and dead remnants.
- Explicit external sources and sinks.
- Chemical-energy capture, storage, use, and dissipation.
- Deterministic competition for well-mixed tile resources.
- Aggregated resource levels and flows for tests, diagnostics, and players.

V1 will not model:

- Complete molecular chemistry, pH, reaction kinetics, enzymes, or cellular compartments.
- Localized concentration fields within a tile.
- Depletion of ocean water or terrestrial surface moisture.
- A separate physical atmosphere and dissolved-gas layer within each tile.
- Thermodynamic simulation beyond declared reaction inputs, yields, and environmental requirements.
- Continuous quantities implemented with unconstrained floating-point mutation of authoritative reservoirs.

Post-v1 photoferrotrophy is the leading reason to add bulk mineral redox resources. That feature must distinguish ferrous substrate, ferric mineral products, and bioavailable catalytic iron rather than consuming the single v1 micronutrient pool as metabolic fuel. The candidate and its prerequisites are documented in [ALTERNATIVE_FOUNDING_METABOLISMS.md](ALTERNATIVE_FOUNDING_METABOLISMS.md).

# Accounting principles

The following are normative invariants:

1. Every tracked unit of matter is owned by exactly one ledger account.
2. Named compounds and generic elemental pools never represent the same matter simultaneously.
3. An internal transfer changes ownership but not resource identity or elemental totals.
4. An internal reaction may change resource identity but must conserve every tracked element and micronutrient.
5. Matter enters or leaves the represented world only through a declared boundary source or sink.
6. No authoritative reservoir may become negative.
7. Reproduction, predation, death, scavenging, and decomposition cannot create matter.
8. Stored chemical energy exists only on energy-bearing resources; it is not a freely assignable organism scalar.
9. Energy capture names its external or chemical opportunity, and energy use eventually becomes dissipated heat.
10. All contention and rounding outcomes are independent of collection order and worker scheduling.

For each tracked element or micronutrient over a tick range:

```text
ending amount
    = starting amount
    + declared boundary inputs
    - declared boundary outputs
```

Transfers and internally balanced reactions cancel when accounts are summed. This is an open, resource-constrained system with auditable boundaries, not a perfectly closed world.

# Resource ontology

## Elements and composition vectors

Every resource definition has an elemental composition vector over the six macronutrients:

```text
ElementVector = [C, H, N, O, P, S]
```

The vector states how many elemental matter quanta are present in one resource quantum. Micronutrients are tracked independently because each is already a single element in one bioavailable form.

Examples:

| Resource | Composition vector `[C,H,N,O,P,S]` |
| --- | --- |
| Generic inorganic carbon | `[1,0,0,0,0,0]` |
| Generic organic sulfur | `[0,0,0,0,0,1]` |
| H₂ | `[0,2,0,0,0,0]` |
| CO₂ | `[1,0,0,2,0,0]` |
| CH₄ | `[1,4,0,0,0,0]` |
| H₂S | `[0,2,0,0,0,1]` |
| Reserve organic, abstracted as CH₂O | `[1,2,0,1,0,0]` |

Composition vectors are accounting metadata, not a promise to simulate detailed chemistry.

## Generic macronutrient pools

Each CHNOPS element has two generic resources:

- `Organic<Element>`: biologically processed matter that is generally easier to incorporate.
- `Inorganic<Element>`: matter requiring a suitable fixation or transformation capability.

These twelve resource IDs hold elemental matter not currently bound in a separately modeled compound. A transformation from a named compound to a generic pool must debit the compound and credit its constituent elements atomically.

## Named atmospheric compounds

The initial v1 catalogue is:

| ID | Formula | Primary role |
| --- | --- | --- |
| `HydrogenGas` | H₂ | Volcanic input and founding metabolism substrate |
| `CarbonDioxide` | CO₂ | Carbon source and photosynthetic substrate |
| `Methane` | CH₄ | Geological or biological carbon and hydrogen reservoir |
| `HydrogenSulfide` | H₂S | Volcanic sulfur-metabolism substrate |
| `SulfurDioxide` | SO₂ | Volcanic sulfur reservoir with comparatively high attrition or transformation |
| `OxygenGas` | O₂ | Photosynthetic output and respiratory substrate |
| `NitrogenGas` | N₂ | Abundant but difficult-to-fix nitrogen reservoir |
| `Ammonia` | NH₃ | More accessible inorganic nitrogen source |

Named gases are authoritative resources, not displays derived from duplicated generic pools. Each remains tile-local state even when rapid neighbor exchange makes its distribution approximately global.

For v1, a tile gas reservoir abstracts both the local atmosphere and the fraction accessible to aquatic organisms. Terrestrial life receives full access. Aquatic gases are data-classified as fully vent-accessible, atmospheric depth-limited, or mixed-origin; depth-limited access follows `100 / (100 + depthMeters)`, while a volcanic tile grants full access to a mixed-origin gas in its emission profile. The exact classes, claim ceiling, and future phase-partition direction are defined in [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md); no separate dissolved-gas inventory is maintained.

## Micronutrients

The initial micronutrient catalogue is calcium, iron, potassium, sodium, magnesium, zinc, copper, iodine, fluoride, selenium, manganese, molybdenum, nickel, and cobalt. Each has one tile pool and may also be held by organisms and remnants. No organic/inorganic distinction or named compounds are modeled for micronutrients in v1.

## Abstract biological compounds

Five compiled resource definitions make biological accounting practical without modeling every molecule:

- `StructuralBiomass`: living cellular material with the v1 composition `C100 H170 O40 N20 P2 S1`.
- `ReserveOrganic`: a generic energy-bearing reserve abstracted initially as CH₂O, with one usable energy quantum per resource quantum.
- `SpentReserveCarrier`: the zero-energy `CH₂O` state of internally retained reserve-carrier matter. It exists only with `CatalyticCarrierRetention`, can be recharged only by a declared energy opportunity, and otherwise decays to matching generic organic matter.
- `SpentStructuralResidue`: zero-energy digestive waste with composition `C68 H106 O8 N20 P2 S1`. It cannot fuel v1 catabolism and mineralizes directly into matching inorganic elemental pools on the ordinary 90-day tile half-life.
- `LabileDissolvedOrganic`: a glucose-equivalent, readily transported substrate with composition `C6 H12 O6`. It has no directly spendable energy and enters only declared organic-catabolism reactions.
- `ReducedFermentationProducts`: a non-fermentable reduced-product aggregate with composition `C4 H8 O4`. It has no directly spendable energy but remains distinct for the defined aerobic reduced-product reaction or future syntrophy.

Micronutrients use separate DNA-defined quotas rather than being embedded in every structural unit. The initial rule pack uses 1,000 structural units as the baseline mature target, a 500-unit hard viability floor, and 10,000 reserve units as baseline energy capacity. See [RESOURCE_CALIBRATION.md](RESOURCE_CALIBRATION.md) and [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md) for the range proof and health calibration.

Any resource definition may eventually carry chemical-energy metadata, but `ReserveOrganic` is the only general-purpose internal energy carrier required by the initial implementation.

The exact structural-decay source, fermentation reaction, persistence, throughput, and anti-cycle rules for the two dissolved compounds are defined in [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md). Respiration's explicit carbon assimilation/oxidation split, energy opportunity, charged/spent carrier conversion, and oxygen accounting are defined in [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md). Generic organic elemental pools are not substitutes for energy-bearing substrates or spent carriers.

# Authoritative units and arithmetic

## Matter

Authoritative matter quantities use nonnegative signed 64-bit integers. One stored unit is a game-native stoichiometric quantum with no required conversion to atoms, grams, or moles.

- Generic elemental resources count elemental quanta.
- Compounds count compound quanta.
- A compound quantum expands to its integer composition vector for reconciliation.
- Checked 128-bit intermediate arithmetic is used for multiplication, allocation, and reconciliation.
- State mutation rejects overflow rather than wrapping or saturating silently.

This is the logical ledger, arithmetic, save, protocol, and reconciliation width. A dense organism column may use a narrower unsigned physical encoding only under the complete per-group range proof and checked accessor contract in [RESOURCE_STORAGE_AND_EVALUATION.md](RESOURCE_STORAGE_AND_EVALUATION.md). Runtime kernels still observe/promote `long` quantities, and an out-of-range rule pack is rejected rather than clamped. Tile/world accounts remain signed 64-bit storage.

The client initially presents LYFE resource units, normalized quantities, or rates. Any future physical-scale overlay is presentation metadata and does not change authoritative accounting.

## Energy

Authoritative energy also uses nonnegative signed 64-bit integer quanta with checked 128-bit intermediates. Each energy-bearing resource defines an integer `usableEnergyPerResourceQuantum`.

```text
StoredEnergy(organism) =
    sum(amount(resource) * usableEnergyPerResourceQuantum(resource))
```

DNA maximum-energy capacity is expressed in the same energy unit. Adding capacity never adds reserve matter.

## Rates, ratios, and probabilities

- Exact recipe coefficients are integers.
- Balance ratios and rates are stored as validated rational or fixed-point values.
- Multiplication uses checked wide intermediates and a named rounding rule.
- Fractional per-tick rates use persisted remainder accumulators when systematic loss would matter.
- Probabilities compile to integer thresholds compared against deterministic unsigned random draws.
- Floating point may be used for offline authoring tools or presentation, but authoritative conversions must be explicit and deterministic.

The [range proof](RESOURCE_CALIBRATION.md) defines the resource scales and account limits. The detailed data-model pass must still select fixed-point encodings for rates, ratios, and probabilities.

# Resource definitions

A rule pack compiles human-authored definitions into immutable runtime records:

```text
ResourceDefinition:
    id
    kind: GenericMacronutrient | NamedCompound | Micronutrient | BiologicalCompound
    composition: ElementVector
    micronutrientComposition: map<MicronutrientId, integer>
    biologicalForm: Organic | Inorganic | NotApplicable
    phase: Gas | DissolvedOrMobile | SolidOrBound | Internal
    usableEnergyPerResourceQuantum
    allowedReservoirKinds
    uptakeTags
    optionalAvailableStoreCapacityGroup
    storageLoadPerQuantum        # compiled from tracked matter composition
    displayMetadata
```

Compilation must reject negative coefficients, a zero total tracked composition across CHNOPS and micronutrients for a matter-bearing resource, invalid reservoir combinations, duplicate IDs, and biological compounds whose declared recipe cannot be reconciled.

# Ledger accounts and reservoirs

An authoritative account is identified by owner, compartment, and resource:

```text
LedgerAccountKey:
    owner: WorldId | TileId | OrganismId | RemnantId | BoundaryId
    compartment
    resourceId
```

## Tile accounts

- `Atmosphere`: named gases.
- `OrganicPool`: generic organic CHNOPS resources.
- `InorganicPool`: generic inorganic CHNOPS resources.
- `MicronutrientPool`: bioavailable micronutrients.

Ocean water and terrestrial surface moisture are boundary reservoirs, not finite tile accounts. Geological material that is not currently bioavailable is likewise outside the tracked tile inventory until introduced by a declared source. V1 has no runtime geological-material account: terrestrial lithology redistributes initial tracked pools, and moisture changes claim access to those pools without changing their balance or biological form.

## Organism accounts

- `Structure`: structural biomass and any explicitly structural micronutrients.
- `AvailableStore`: organic/inorganic nutrients and micronutrients held for future processes. Its balances are limited by the separately accounted dissolved-macronutrient, free-micronutrient, and ingested-matter capacity groups defined in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- `EnergyReserve`: energy-bearing biological compounds.

Health reads the energy implied by `EnergyReserve`; it does not own another resource balance. Reproductive allocation, predation, and death operate over all organism compartments.

An admitted metabolic process may temporarily encumber matter already held in an organism account without moving or consuming it. Authoritative binding cohorts reduce the quantity free to other processes until a deterministic release tick, while the physical balance remains in its existing account. DNA holdbacks protect unbound quantities from lower-priority processes without creating a physical account or capacity. See [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).

## Remnant accounts

A remnant owns the transferred contents of a dead organism. It may retain compartment labels for explanation, but consumption and decay can operate over a consolidated immutable resource listing plus mutable remaining quantities.

## Boundary accounts

Boundary IDs make open-system changes explicit:

- `Sunlight`, `GeothermalGradient`, and named chemical opportunities for energy input.
- `OceanWater` and `SurfaceMoisture` for inexhaustible water-derived matter.
- `VolcanicSource`, `WeatheringSource`, and other geological inputs.
- `AtmosphericEscape`, `GeologicalBurial`, and other matter sinks.
- `DissipatedHeat` for energy leaving useful storage.

Boundary accounts do not need finite balances, but every transaction using them records its category and amount.

# Ledger operations

## Runtime layout

The ledger-account model is conceptual; it does not require a heap object or dictionary entry for every balance. Rule compilation assigns dense integer slots to resources permitted in each reservoir kind.

- Tile balances use dense resource-major matrices indexed by compiled reservoir slot and `TileId`; source, sink, exchange, claim, and projection phases receive precompiled slot lists by transport/behavior class.
- Common organism and remnant balances use resource-major columns inside their tile-owned entity chunks. High-frequency structure/reserve accounts receive dedicated columns, while bounded group-specific vectors use compiled dense handles.
- Rare future mutually exclusive internal compound families use typed optional component stores rather than a dictionary per organism or permanently widening every core row.
- Account keys in diagnostics are reconstructed from the owner, compartment, and compiled slot.
- DNA compilation resolves reaction inputs/outputs, quotas, process priority, holdbacks, and costs to immutable numerical process plans and dense resource handles. Ticks do not traverse trait definitions or perform `ResourceId` map lookups in inner loops.
- Evaluation emits compact claims and reaction executions into flat per-tile or per-worker buffers; ordinary internal tick reservations remain scratch rather than authoritative state.
- Production applies resolved batches directly and updates aggregate flow counters.
- Debug mode may additionally materialize individual ledger entries for a bounded window.

The selected layouts, option comparison, tile effective views, and benchmark matrix are defined in [RESOURCE_STORAGE_AND_EVALUATION.md](RESOURCE_STORAGE_AND_EVALUATION.md). Dense core slots remain the default because the v1 catalogue is small and predictable, but organism memory is tracked per group because each additional `int64` slot costs approximately `0.8 MB` per `100,000` organisms per populated world version.

## Transfers

A transfer preserves resource identity and changes ownership:

```text
MatterTransfer:
    sourceAccount
    destinationAccount
    resourceId
    amount
    cause
    actorId?
```

Examples include tile uptake, parent-to-offspring allocation, organism-to-remnant death transfer, scavenging, predation, gas exchange, and neighbor nutrient movement.

## Reactions

A reaction atomically changes resource identities:

```text
ReactionDefinition:
    id
    requiredCapabilities
    inputs: list<ResourceAmount>
    catalystsOrRequiredMicronutrients
    outputs: list<ResourceAmount>
    matterBoundaryInputs
    matterBoundaryOutputs
    energyOpportunity
    grossEnergyYield
    storedEnergyOutputs
    environmentalRequirements
    baseAttemptProbability
    maximumExtentPerTick
    explanationMetadata
```

```text
ReactionExecution:
    reactionId
    actorId
    extent
    accountDebits
    accountCredits
    boundaryMatterFlows
    energyCaptured
    energyStored
    energyDissipated
    cause
```

Internal debits and credits plus declared matter boundaries must balance for every element and micronutrient. Energy stored cannot exceed the declared yield plus any energy-bearing inputs consumed. Lower efficiency changes stored-versus-dissipated energy or waste outputs; it never deletes matter.

## Atomic application

```text
ApplyReaction(execution):
    validate every amount is positive and every account permits the resource
    validate all source balances cover their debits
    expand all resource amounts into elemental and micronutrient vectors
    assert credits + boundaryOutputs == debits + boundaryInputs
    assert storedEnergy <= availableEnergy
    apply all debits and credits atomically with checked arithmetic
    record production ledger aggregates
```

Failed validation changes no state. An invariant failure during development aborts the tick and emits diagnostics rather than clamping a balance.

# Energy capture, storage, and use

DNA exposes this accounting through separate trait domains: external energy-capture reactions create energy-bearing reserve; energy-storage and nutrient-storage traits cap what their respective compartments may hold; internal-metabolism traits transform or spend internal matter; and resource acquisition transfers external matter without itself creating energy. The canonical family boundaries are defined in [EVOLUTION.md](EVOLUTION.md).

Energy opportunities are not finite nutrient pools:

- Sunlight is calculated from tile insolation and accessibility.
- Geothermal opportunities follow volcanic and geological conditions.
- Chemical opportunities require the declared chemical substrates.

A successful metabolism produces `ReserveOrganic` or another energy-bearing product. The product's amount determines stored chemical energy. If a reaction yields more usable energy than its products can hold, the remainder dissipates. If storage capacity is nearly full, reaction extent must be reduced or excess products must follow an explicit non-energy-bearing output path.

The v1 storage mechanism is `PrimitiveOrganicReserve` in the organism's `EnergyReserve` account. Incremental capacity traits and a later compartmentalized-storage trait still hold the same `ReserveOrganic` resource. Maximum capacity changes only as a DNA target; current capacity is commissioned from conserved `StorageStructure`, never changes current balances or energy density, and is shared with retained spent carriers. Adding a chemically denser store later requires a new resource definition and balanced synthesis/mobilization reactions. Exact tier and commissioning rules are defined in [ENERGY_STORAGE.md](ENERGY_STORAGE.md).

Maintenance and actions consume energy-bearing resources through balanced reactions:

```text
SpendStoredEnergy(organism, requestedEnergy, cause):
    choose permitted reserve resources in canonical order
    determine whole resource quanta required
    convert consumed carrier matter into declared spent products
    credit useful work for diagnostics only
    send released energy to DissipatedHeat
    persist any sub-quantum cost remainder
```

For the initial `ReserveOrganic`, spent carrier matter becomes the corresponding generic organic elemental products unless `CatalyticCarrierRetention` is active. Founder organisms do not retain those products: one explicit waste transaction credits the tile's organic pool at the end of internal metabolism. With carrier retention, the same spend instead converts charged `ReserveOrganic` to internal zero-energy `SpentReserveCarrier`; it must not also emit the carrier matter to the tile. Biomass-assembly `OrganicOxygen` follows the founder waste route. Later `WasteRouting` or `CatalyticRecycling` traits may retain other declared products subject to ordinary store capacity and a useful balanced reaction. This preserves matter after its useful chemical energy has dissipated without letting a primitive organism fill its small nutrient store with unavoidable waste.

# Metabolic reference recipes

The following are accounting examples, not final claims of biochemical fidelity or balance.

## Hydrogen-oriented founding metabolism

A combined anaerobic hydrogen-acetogenesis abstraction may use:

```text
4 H2 + 2 CO2
    -> 2 ReserveOrganic(CH2O) + 2 H2O(boundary)
```

The two `ReserveOrganic` units are an accounting abstraction of one acetate-like C₂H₄O₂ product, not a claim that the organism directly manufactures carbohydrate.

Element check:

```text
inputs:  C2 H8 O4
outputs: 2 ReserveOrganic = C2 H4 O2; 2 H2O = H4 O2
```

This H₂/CO₂-to-acetate pattern provides a defensible anaerobic biological anchor while remaining intentionally abstract. DNA and environment govern attempt probability, maximum extent, gas accessibility, storage capacity, and energy yield.

## Sulfur-oriented accounting fixture

A first sulfur fixture may use a combined sulfur-autotrophy abstraction:

```text
2 H2S + 1 CO2
    -> 1 ReserveOrganic(CH2O) + 1 H2O(boundary) + 2 InorganicSulfur
```

Element check:

```text
inputs:  C1 H4 O2 S2
outputs: ReserveOrganic = C1 H2 O1; H2O = H2 O1; inorganic sulfur = S2
```

This particular recipe uses sunlight as its energy opportunity and represents anoxygenic photosynthesis. A sulfur-based founding chemotrophy would require a separate balanced recipe with an appropriate electron acceptor. The ledger supports both without changing its accounting model.

# Uptake and deterministic contention

Organisms evaluate against a stable tile view and emit claims rather than directly decrementing tile pools:

```text
ResourceClaim:
    claimantId
    sourceAccount
    destinationAccount
    resourceId
    requestedAmount
    priorityClass
    contentionWeight             # default 1; bounded typed trait effect
    cause
```

V1 uses capped weighted-proportional allocation within each `(sourceAccount, resourceId, priorityClass)` group. Nearly every ordinary claim has weight `1`; `DissolvedOrganicSpecialization` supplies the first resource-specific weight `2` for LDO without creating another priority class:

```text
ResolveClaims(available, claims, tick, tileId, resourceId):
    discard invalid and zero claims
    merge claims by claimant/contention identity before applying weight
    sort only for canonical output, not preferential access
    remaining = available
    active = merged claims with unmet usable request
    while remaining > 0 and active is not empty:
        effectiveDemand[i] = unmetRequest[i] * contentionWeight[i]
        allocate floor(remaining * effectiveDemand[i]
                       / sum(effectiveDemand)) to each active claim,
            capped by its unmet requested amount
        distribute this round's leftover quanta among unmet claims
            by descending fractional remainder,
            then KeyedRank(ResourceRemainderRank,
                           tick, claimantId, Pack32(tileId, resourceId))
        remove satisfied claims and repeat if a cap left stock unallocated
    distribute each merged claimant grant to its subclaims in canonical
        process-priority order without changing its total
    return grants in canonical claimant order
```

Wide intermediates prevent overflow. The stable remainder rank prevents permanent low-ID preference and consumes no mutable random stream. Priority classes are permitted only for explicit mechanics; ordinary organisms competing for the same pool share one class. A weight changes proportional share only during scarcity, never eligibility, requested usable amount, or priority. Claims from one organism are merged before weighting so splitting demand across reactions cannot multiply influence. Configuration bounds ordinary contention weights to `1..4`; only a typed resource-specific trait may change the default.

Resource absorption transfers granted matter into organism storage. A metabolic reaction may then use only the organism's resulting internal inventory unless its definition explicitly couples uptake and reaction into one atomic claim bundle.

Atomic multi-input bundles use the same bounded, resource-specific weights without splitting their stoichiometry. Only inputs that are physically scarce constrain allocation, and a weight on one resource cannot grant priority to another independently scarce resource. The first exact coupled algorithm and fixtures are defined for respiration in [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md).

The founder `PassiveSmallMoleculeUptake` rule emits at most one micronutrient claim after a deterministic keyed `0.5` success check each one-hour tick. It selects among inherited reproduction-quota deficits by greatest normalized deficit and shares the ordinary contention class. Under abundant uncontested conditions this is `0.5` expected micronutrient quantum per organism-hour. It has no marginal energy debit because its background cost is included in founder maintenance; see [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md) for the exact policy and recalibration triggers.

# Reproduction, predation, death, and decay

## Reproduction

Reproduction is one atomic bundle:

1. Validate health, lifecycle, attempt outcome, energy, and minimum material requirements.
2. Spend the additional reproductive-work energy through a balanced carrier reaction.
3. Allocate structure, available stores, and reserves according to the DNA reproduction model.
4. Require both resulting organisms to satisfy their minimum viable structural composition.
5. Create the offspring and transfer the allocated accounts.

True split aims for an even division after reproductive work. Budding uses a smaller DNA-defined offspring fraction. Rounding assigns indivisible residual quanta using a documented deterministic rule. If either result would be nonviable, the entire reproduction bundle fails without cost unless the later organism plan explicitly defines a failed-attempt cost.

## Predation and scavenging

Predation first resolves whether the target dies. Consumption is then a transfer from the target or newly created remnant to the predator. Scavenging transfers a deterministically bounded fraction from a remnant. Neither mechanic converts matter automatically; digestion and metabolism are separate reactions with explicit waste products and energy yield.

Basic remnant scavenging may directly transfer only compatible simple reserve compounds, dissolved stores, and micronutrients. Its first profile costs 25 energy, schedules a two-hour organism-local handling cooldown, and caps one action at 200 compatible reserve quanta, 256 dissolved-matter load, and eight micronutrient units. Structural biomass requires particulate ingestion and digestion. Full cooldown, admission, contention, and destination-compartment semantics are defined in [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).

The first structural-food rule is one combined digestion-and-catabolism reaction. One `StructuralBiomass(C100 H170 O40 N20 P2 S1)` yields `32 ReserveOrganic(CH2O)` plus one zero-energy `SpentStructuralResidue(C68 H106 O8 N20 P2 S1)`; it declares 40 gross recoverable energy units, dissipates eight as processing overhead, and stores 32 in the reserve output. The equation conserves every CHNOPS element. Keeping the residue distinct prevents it from being treated as fresh fuel and yielding energy twice. Buffer, throughput, output routing, upkeep, action-cost, and mineralization rules are normative in [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).

The first dissolved-organic rule is separate: structural decay releases an exactly balanced fraction as `LabileDissolvedOrganic`, and fermentation converts each `C6 H12 O6` substrate into two `ReserveOrganic` plus one `ReducedFermentationProducts(C4 H8 O4)`. It recovers at most eight reserve units per decayed structural quantum, compared with 32 through direct particulate digestion. [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md) is normative for this path. [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md) partitions each named dissolved fuel between mass-balanced carrier assimilation and oxidation, then uses oxidation opportunity to recharge existing retained carrier matter rather than crediting matter-bearing reserve twice.

The first oxygenic primary-production rule consumes one named `CO2` plus boundary water and light opportunity to create one `ReserveOrganic(CH2O)` plus one named `O2`. Its fixed-carbon output must be stored or used by the same admitted transaction; oxygen is never credited independently. Light sharing, catalyst activation, throughput, and world-source calibration are normative in [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md).

When multiple ordinary scavengers target the same contents, the action resolver uses the same deterministic proportional-plus-stable-remainder mechanics as tile uptake, with every scavenging weight fixed at `1` in v1. When multiple predators successfully kill the same prey, each eligible claim from a predator that survives the complete predation pass is capped by ingestion and storage limits and weighted by its compiled `feedingPriorityWeight`:

```text
ResolveWeightedPredationClaims(available, successfulClaims,
                                tick, tileId, resourceId):
    remaining = available
    active = valid positive claims

    while remaining > 0 and active is not empty:
        weightTotal = sum(claim.feedingPriorityWeight for active)
        allocate floor(remaining * weight / weightTotal) to each active claim,
            capped by its unmet requested amount
        distribute this round's leftover quanta among unmet claims
            by descending fractional remainder,
            then KeyedRank(PredationFeedingRemainder,
                           tick, claimantId, Pack32(tileId, resourceId))
        remaining -= total grants made in this round
        remove fully satisfied claims
        stop if the round grants zero

    return grants in canonical claimant order
```

This special weight applies only to contents of prey killed by the participating predators. It never changes ordinary tile-resource priority, attack success, total available matter, or a predator's ingestion/storage cap.

## Death

Death atomically moves every positive organism account to one new remnant before removing the organism. Its event may attach multiple positive-probability risk assessments and triggered causes, but these do not repeat the transfer.

```text
FinalizeDeath(organism, riskAssessments, triggeredCauses):
    create empty remnant at organism position
    for each positive organism ledger account in canonical order:
        transfer entire balance to remnant
    assert organism total is zero
    remove organism
    record one death event with all positive-probability assessments
        and every triggered cause
```

## Decomposition

Decay operates on remaining remnant contents:

- Structural biomass and energy carriers are converted into their component generic organic pools.
- Remaining usable chemical energy dissipates unless a decomposer captures it through an explicit reaction first.
- Already generic organic matter transfers directly to tile organic pools.
- Inorganic matter and micronutrients transfer to their matching tile pools.
- Later rules may transform some outputs into gases or inorganic forms through separate balanced reactions.

No resource may remain simultaneously on the remnant and in the tile after a decay transaction.

The first decay cohorts, hourly coefficients, environmental multiplier, and slow 90-day organic-to-inorganic mineralization rule are fixed in [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md). Generic organic-to-inorganic mineralization is a one-to-one elemental-form transformation and never implicitly creates a named gas.

# Environmental sources, sinks, and exchange

Each source or sink has a rule-pack definition, owning subsystem, rate, eligibility conditions, and persisted fractional remainder where needed.

- Volcanism may introduce H₂, H₂S, SO₂, CO₂, methane, and mineral nutrients.
- A future explicit weathering rule may introduce inorganic phosphorus and micronutrients from a finite geological boundary or reservoir; v1 has no ongoing weathering source.
- Water contributes H or O only through a reaction that explicitly crosses the water boundary.
- Oxygenic photosynthesis may consume boundary water and produce O₂.
- Atmospheric escape, chemical attrition, and geological burial are explicit sinks or transformations.
- Sunlight and geothermal activity are energy opportunities rather than matter credits.

Neighbor exchange is calculated from a stable pre-exchange view. Each edge proposes paired outbound flows; all proposals are scaled if their combined demand exceeds a source tile balance, then applied after a barrier. Opposite directions are netted only for performance after debug accounting can reconstruct the gross flows.

Atmospheric gases use the concrete ordering and coefficients in [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md). Volcanic emissions scale with tile activity, environmental attrition declares either an elemental transformation destination or an explicit external boundary, and signed per-edge fixed-point remainders preserve sub-quantum exchange. Gas exchange itself is a transfer and conserves the named compound globally.

Terrestrial organism claims against tile `InorganicPool` and `MicronutrientPool` accounts multiply their ordinary request ceiling by the current physiological activity factor and `0.20 + 0.80 * surfaceMoisture`, as fixed in [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md). This is an accessibility constraint at claim construction, not a source, transformation, reservation, or distinct micronutrient form. Contention still resolves against the full concrete requests and debits only granted quantities.

# History and player explanation

Production does not retain every low-level transfer indefinitely. It maintains deterministic aggregates keyed by:

```text
time bucket
tile
resource or element
source and destination reservoir kind
cause/reaction category
species when applicable
```

Required views include current quantity, net change, gross inputs and outputs, transformations, top consumers/producers, and limiting failed claims. Debug mode may retain exact transactions for a bounded tick window. Aggregation must derive from applied transactions, never estimate flows from before/after snapshots.

The simulation may retain these aggregates for every tile, but player access is filtered by authoritative exploration state. Exact charts include only buckets observed while the tile was live for that actor. A reduced tile may show its timestamped last observed values and coarse fixed composition, but the server must not use hidden aggregates to fill or interpolate unobserved intervals.

# Bounded one-tile reference ecosystem

The first executable fixture is an aquatic volcanic tile with no cross-tile exchange.

## Initial boundary and tile state

- `VolcanicSource` introduces H₂ and CO₂ at fixed per-tick rates.
- `AtmosphericEscape` removes a configured fraction of H₂.
- `OceanWater` is an inexhaustible boundary account.
- The atmosphere begins with finite H₂ and CO₂ balances.
- One species uses only the hydrogen-oriented reference metabolism.
- Organisms contain structural biomass, small available stores, and a limited reserve capacity.
- No mutation, migration, predation, or environmental catastrophe is enabled in the first fixture.

## Minimal lifecycle

Each tick:

1. Apply volcanic inputs and atmospheric attrition.
2. Decay remnants that existed at tick start and transfer their released contents to tile pools.
3. Advance organism age and resolve reserve-exhaustion, senescence, and environmental deaths.
4. Evaluate surviving organisms and collect gas claims; this one-tile fixture has no movement.
5. Allocate gas claims proportionally.
6. Transfer granted gases and apply successful hydrogen-metabolism reactions.
7. Resolve internal energy-producing reactions, then spend maintenance energy; kill organisms that cannot pay the full obligation.
8. Attempt matter-conserving reproduction for post-maintenance survivors whose thresholds are satisfied.
9. Update behavior state if enabled by the fixture.
10. Reconcile matter, nonnegative balances, stored-energy capacity, and population aggregates.

## Worked reaction transaction

For one reaction extent:

| Entry | Account | Resource | Delta |
| --- | --- | --- | ---: |
| Debit | Tile atmosphere | H₂ | -4 |
| Debit | Tile atmosphere | CO₂ | -2 |
| Credit | Organism energy reserve | ReserveOrganic | +2 |
| Credit | Ocean-water boundary | H₂O | +2 |

The ledger expands both sides to `C2 H8 O4`; the internal-plus-boundary matter difference is zero. If the recipe declares 3 energy quanta available, the two `ReserveOrganic` outputs store 2 and the execution records 1 dissipated. The exact metabolic yield remains balance data; the one-energy-per-reserve unit is fixed.

## Fixture success criteria

- Every tick reconciles exactly with integer arithmetic.
- No account becomes negative even under oversubscribed uptake.
- Increasing volcanic input can increase carrying capacity.
- Increasing maintenance or attrition can produce scarcity and extinction.
- Reproduction preserves total matter after its declared energy-spending reaction.
- Death transfers all remaining contents exactly once.
- Unconsumed remnants ultimately reach tile pools without duplication.
- A fixed seed and rule pack produce identical tick hashes across save/reload and supported worker counts.
- Flow history can explain every change to H₂, CO₂, reserve organic, and tile organic pools.

# Implementation-facing pseudocode

```text
CompileResourceDefinitions(rulePack)
ValidateReactionBalance(reaction, definitions)
ExpandToElementVector(resourceId, amount)

CollectTileClaims(tileReadView, organismReadView)
ResolveClaims(available, claims, deterministicKey)
BuildReactionExecution(actor, recipe, grantedInputs)
ApplyTransfer(transfer)
ApplyReaction(execution)

SpendStoredEnergy(organism, requestedEnergy, cause)
ResolveReproduction(parent, allocationModel)
ConsumeRemnant(consumer, remnant, requestedAmount)
FinalizeDeath(organism, causes)
DecayRemnant(remnant, environment, elapsedTicks)

ApplyBoundarySourcesAndSinks(world, tick)
ResolveNeighborExchange(worldReadView)
AggregateAppliedFlows(appliedLedgerEntries)
ReconcileResourceLedger(world, tickRange)
```

# Decisions fixed by this deep dive

- Named gases are authoritative compounds with exact CHNOPS composition vectors.
- Generic pools never duplicate matter held by named compounds.
- The initial gas catalogue is H₂, CO₂, CH₄, H₂S, SO₂, O₂, N₂, and NH₃.
- `ReserveOrganic` is initially an abstract CH₂O energy carrier.
- Stored energy is derived from energy-bearing matter.
- Matter and energy quantities use fixed-point integer storage with checked wide intermediates.
- Ordinary well-mixed resource contention uses capped weighted-proportional allocation with deterministically ranked remainders; weight defaults to `1`, and the first specialized LDO trait uses `2` without changing priority class.
- Contested prey contents use a separate capped weighted allocation among successful predators; only explicit `feedingPriorityWeight` traits affect that predation-specific weight.
- The hydrogen-oriented metabolism is the first reference fixture; sulfide anoxygenic phototrophy is the second. Their intended gameplay asymmetry and trait paths are specified in [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md).
- The v1 catalogue and fixtures contain two founders, but resource and reaction IDs remain data-defined so later bulk-mineral metabolisms do not require a ledger redesign.
- Water is inexhaustible but any tracked matter crossing its boundary is recorded.
- Matter quanta are game-native rather than literal physical units.
- V1 structural biomass is `C100 H170 O40 N20 P2 S1`; micronutrients use separate DNA quotas.
- Structural-food catabolism emits explicit zero-energy `SpentStructuralResidue(C68 H106 O8 N20 P2 S1)` so residual matter cannot be mistaken for fresh fuel.
- Structural decay emits four `LabileDissolvedOrganic(C6 H12 O6)` per structural quantum plus an exact generic organic remainder; spent reserve never enters that labile pool.
- Fermentation emits two `ReserveOrganic` plus one non-fermentable `ReducedFermentationProducts(C4 H8 O4)` per labile substrate, preventing repeated energy extraction from generic organic waste.
- The two dissolved biological compounds exchange at `0.15%` per fully compatible edge-hour with moisture-dependent symmetric compatibility. Other non-gas resources remain tile-bound until explicitly classified.
- Baseline structure and reserve capacity are 1,000 and 10,000 units respectively.
- One `ReserveOrganic` quantum stores one energy quantum.
- Founder available-store capacities are 512 expanded-matter units for dissolved macronutrients, 64 units for free micronutrients, and zero for ingested matter; initial free contents are zero.
- Primitive micronutrient uptake has one `0.5`-probability, one-quantum opportunity per organism-hour and no separate marginal energy cost.
- Founder spent reserve products and biomass-assembly `OrganicOxygen` are released to the tile organic pool at the end of internal metabolism.

# Remaining decisions

These do not prevent implementing the ledger and first fixture, but must be resolved before representative gameplay balancing:

- [x] Gas accessibility equations for aquatic and terrestrial organisms; see [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- [ ] Remaining non-gas source, sink, exchange, decay, and attrition rates for resources not in the organic-path fixture. Organic-substrate production, 14-/90-day passive loss, and dissolved biological-compound exchange are fixed in [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md) and [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md). Terrestrial initial endowment and moisture-access rules are fixed in [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md); ongoing geological weathering is deferred. First atmospheric-gas rates are fixed in [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- [x] The v1 sulfur founder uses the worked H₂S anoxygenic-phototrophy recipe; a separate sulfur chemotrophy is not required for the founding choice.
- [x] Temporary metabolic binding, default sharing, and DNA-defined priority/holdback semantics; see [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- [x] Founder waste release timing and retention policy; advanced retention and recycling trait values remain to be calibrated.
- [x] No additional ordinary resource priority classes in v1; predation priority is isolated to contested prey contents.
- [ ] Production history bucket sizes and retention.
- [x] Ordinary failed reproduction transactions cost nothing and do not reschedule cooldown; explicitly risky future profiles may declare a cost.

# Required validation artifacts

- Machine-readable resource and reaction schemas.
- A compiler that produces immutable runtime definitions.
- Static elemental and micronutrient balance validation for every recipe.
- Capacity-group compilation and load tests, including both founder staging bundles and reproduction quota sets.
- Primitive micronutrient-uptake fixtures covering keyed opportunities, normalized-deficit targeting, ordinary contention, capacity stopping, missing resources, and deterministic replay.
- Property tests for transfers, reactions, contention, reproduction, death, and decay.
- The deterministic one-tile hydrogen fixture and a second sulfur fixture.
- A debug reconciliation report identifying imbalance by tick, account, resource, element, and cause.
- Representative charts of resource levels, gross flows, population, and stored energy from the fixture.

# Scientific anchors for the reference abstractions

- Reductive acetogenesis uses the overall H₂/CO₂-to-acetate relationship represented by the hydrogen fixture: [Elucidating acetogenic H₂ consumption in dark fermentation using flux balance analysis](https://pubmed.ncbi.nlm.nih.gov/23958339/).
- The sulfur fixture follows the commonly used elemental-sulfur form of the anoxygenic photosynthesis equation: [Low-Light Anoxygenic Photosynthesis and Fe-S-Biogeochemistry in a Microbial Mat](https://pmc.ncbi.nlm.nih.gov/articles/PMC5934491/).

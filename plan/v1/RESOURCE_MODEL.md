# Resource and Energy Model

Status: first deep-dive draft; accounting architecture decided, balance values provisional

Sources: [NUTRIENTS vision](../../vision/NUTRIENTS.md), [WORLD vision](../../vision/WORLD.md), [ORGANISMS vision](../../vision/ORGANISMS.md), and [authoritative data model](DATA_MODEL.md).

# Purpose

Define the authoritative v1 model for matter, chemical energy, reservoirs, reactions, competition, and resource history. The model should be biology-inspired, internally mass-balanced, deterministic, efficient enough for the simulation hot loop, and explainable to a player.

This document fixes the accounting architecture needed by the other plans. Exact rates, capacities, efficiencies, and most reaction coefficients remain versioned balance data.

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

Two compiled resource definitions make biological accounting practical without modeling every molecule:

- `StructuralBiomass`: living cellular material with the v1 composition `C100 H170 O40 N20 P2 S1`.
- `ReserveOrganic`: a generic energy-bearing reserve abstracted initially as CH₂O, with one usable energy quantum per resource quantum.

Micronutrients use separate DNA-defined quotas rather than being embedded in every structural unit. The initial rule pack uses 1,000 structural units as the baseline mature and minimum viable target and 10,000 reserve units as baseline energy capacity. See [RESOURCE_CALIBRATION.md](RESOURCE_CALIBRATION.md) for the range proof.

Any resource definition may eventually carry chemical-energy metadata, but `ReserveOrganic` is the only general-purpose internal energy carrier required by the initial implementation.

# Authoritative units and arithmetic

## Matter

Authoritative matter quantities use nonnegative signed 64-bit integers. One stored unit is a game-native stoichiometric quantum with no required conversion to atoms, grams, or moles.

- Generic elemental resources count elemental quanta.
- Compounds count compound quanta.
- A compound quantum expands to its integer composition vector for reconciliation.
- Checked 128-bit intermediate arithmetic is used for multiplication, allocation, and reconciliation.
- State mutation rejects overflow rather than wrapping or saturating silently.

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
    displayMetadata
```

Compilation must reject negative coefficients, a zero composition for matter-bearing resources, invalid reservoir combinations, duplicate IDs, and biological compounds whose declared recipe cannot be reconciled.

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

Ocean water and terrestrial surface moisture are boundary reservoirs, not finite tile accounts. Geological material that is not currently bioavailable is likewise outside the tracked tile inventory until introduced by a declared source.

## Organism accounts

- `Structure`: structural biomass and any explicitly structural micronutrients.
- `AvailableStore`: organic/inorganic nutrients and micronutrients held for future processes.
- `EnergyReserve`: energy-bearing biological compounds.

Health reads the energy implied by `EnergyReserve`; it does not own another resource balance. Reproductive allocation, predation, and death operate over all organism compartments.

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

- Tile balances use dense arrays indexed by compiled resource slot.
- Organism and remnant balances use dense columns or fixed-size resource vectors associated with their entity slots.
- Account keys in diagnostics are reconstructed from the owner, compartment, and compiled slot.
- Evaluation emits compact claims and reaction executions into per-tile or per-worker buffers.
- Production applies resolved batches directly and updates aggregate flow counters.
- Debug mode may additionally materialize individual ledger entries for a bounded window.

The detailed data-layout benchmark should compare dense vectors with a hybrid sparse layout, but v1 should begin dense because the catalogue is small and predictable.

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

The v1 storage mechanism is `PrimitiveOrganicReserve` in the organism's `EnergyReserve` account. Incremental capacity traits and a later compartmentalized-storage trait still hold the same `ReserveOrganic` resource. Changing capacity never changes current balances or energy density; adding a chemically denser store later requires a new resource definition and balanced synthesis/mobilization reactions.

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

For the initial `ReserveOrganic`, spent carrier matter becomes the corresponding generic organic elemental stores; later metabolism, waste, death, or decomposition determines where it moves next. This retains the nutrients after their useful chemical energy has dissipated.

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
    cause
```

V1 uses proportional allocation within each `(sourceAccount, resourceId, priorityClass)` group:

```text
ResolveClaims(available, claims, deterministicKey):
    discard invalid and zero claims
    sort only for canonical output, not preferential access
    if sum(requested) <= available:
        grant every request in full
    else:
        for each claim:
            baseGrant = floor(available * requested / totalRequested)
        distribute leftover quanta among claims with unmet demand
            by stable rank Hash(deterministicKey, claimantId)
    return grants in canonical claimant order
```

Wide intermediates prevent overflow. The stable remainder rank prevents permanent low-ID preference and consumes no mutable random stream. Priority classes are permitted only for explicit mechanics; ordinary organisms competing for the same pool share one class.

Resource absorption transfers granted matter into organism storage. A metabolic reaction may then use only the organism's resulting internal inventory unless its definition explicitly couples uptake and reaction into one atomic claim bundle.

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

When multiple consumers target the same contents, the action resolver first establishes eligible successful consumers, then uses the same proportional-plus-stable-remainder allocation principle.

## Death

Death atomically moves every positive organism account to one new remnant before removing the organism. Multiple death causes attach to the event but do not repeat the transfer.

```text
FinalizeDeath(organism, causes):
    create empty remnant at organism position
    for each positive organism ledger account in canonical order:
        transfer entire balance to remnant
    assert organism total is zero
    remove organism
    record one death event with all causes
```

## Decomposition

Decay operates on remaining remnant contents:

- Structural biomass and energy carriers are converted into their component generic organic pools.
- Remaining usable chemical energy dissipates unless a decomposer captures it through an explicit reaction first.
- Already generic organic matter transfers directly to tile organic pools.
- Inorganic matter and micronutrients transfer to their matching tile pools.
- Later rules may transform some outputs into gases or inorganic forms through separate balanced reactions.

No resource may remain simultaneously on the remnant and in the tile after a decay transaction.

# Environmental sources, sinks, and exchange

Each source or sink has a rule-pack definition, owning subsystem, rate, eligibility conditions, and persisted fractional remainder where needed.

- Volcanism may introduce H₂, H₂S, SO₂, CO₂, methane, and mineral nutrients.
- Weathering may introduce inorganic phosphorus and micronutrients.
- Water contributes H or O only through a reaction that explicitly crosses the water boundary.
- Oxygenic photosynthesis may consume boundary water and produce O₂.
- Atmospheric escape, chemical attrition, and geological burial are explicit sinks or transformations.
- Sunlight and geothermal activity are energy opportunities rather than matter credits.

Neighbor exchange is calculated from a stable pre-exchange view. Each edge proposes paired outbound flows; all proposals are scaled if their combined demand exceeds a source tile balance, then applied after a barrier. Opposite directions are netted only for performance after debug accounting can reconstruct the gross flows.

Atmospheric gases use the concrete ordering and coefficients in [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md). Volcanic emissions scale with tile activity, environmental attrition declares either an elemental transformation destination or an explicit external boundary, and signed per-edge fixed-point remainders preserve sub-quantum exchange. Gas exchange itself is a transfer and conserves the named compound globally.

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
2. Evaluate organisms and collect gas claims.
3. Allocate gas claims proportionally.
4. Transfer granted gases and apply successful hydrogen-metabolism reactions.
5. Spend maintenance energy.
6. Attempt matter-conserving reproduction when thresholds are satisfied.
7. Resolve starvation or senescence deaths.
8. Decay remnants and transfer their contents to tile pools.
9. Reconcile matter, nonnegative balances, and stored-energy capacity.
10. Record resource and population aggregates.

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
- Ordinary well-mixed resource contention uses proportional allocation with deterministically ranked remainders.
- The hydrogen-oriented metabolism is the first reference fixture; sulfide anoxygenic phototrophy is the second. Their intended gameplay asymmetry and trait paths are specified in [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md).
- The v1 catalogue and fixtures contain two founders, but resource and reaction IDs remain data-defined so later bulk-mineral metabolisms do not require a ledger redesign.
- Water is inexhaustible but any tracked matter crossing its boundary is recorded.
- Matter quanta are game-native rather than literal physical units.
- V1 structural biomass is `C100 H170 O40 N20 P2 S1`; micronutrients use separate DNA quotas.
- Baseline structure and reserve capacity are 1,000 and 10,000 units respectively.
- One `ReserveOrganic` quantum stores one energy quantum.

# Remaining decisions

These do not prevent implementing the ledger and first fixture, but must be resolved before representative gameplay balancing:

- [x] Gas accessibility equations for aquatic and terrestrial organisms; see [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- [ ] Non-gas source, sink, exchange, decay, and attrition rates. First atmospheric-gas rates are fixed in [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- [x] The v1 sulfur founder uses the worked H₂S anoxygenic-phototrophy recipe; a separate sulfur chemotrophy is not required for the founding choice.
- [ ] Waste release timing and whether organisms retain spent organic constituents.
- [ ] Priority classes, if any, beyond ordinary proportional competition.
- [ ] Production history bucket sizes and retention.
- [ ] Failed reproduction-attempt costs.

# Required validation artifacts

- Machine-readable resource and reaction schemas.
- A compiler that produces immutable runtime definitions.
- Static elemental and micronutrient balance validation for every recipe.
- Property tests for transfers, reactions, contention, reproduction, death, and decay.
- The deterministic one-tile hydrogen fixture and a second sulfur fixture.
- A debug reconciliation report identifying imbalance by tick, account, resource, element, and cause.
- Representative charts of resource levels, gross flows, population, and stored energy from the fixture.

# Scientific anchors for the reference abstractions

- Reductive acetogenesis uses the overall H₂/CO₂-to-acetate relationship represented by the hydrogen fixture: [Elucidating acetogenic H₂ consumption in dark fermentation using flux balance analysis](https://pubmed.ncbi.nlm.nih.gov/23958339/).
- The sulfur fixture follows the commonly used elemental-sulfur form of the anoxygenic photosynthesis equation: [Low-Light Anoxygenic Photosynthesis and Fe-S-Biogeochemistry in a Microbial Mat](https://pmc.ncbi.nlm.nih.gov/articles/PMC5934491/).

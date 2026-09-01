# DNA, Mutation, Speciation, and Lineage

Status: trait-family graph policies decided; mutation economy/speciation details pending

Sources: [ORGANISMS vision](../../vision/ORGANISMS.md), [GAMEPLAY vision](../../vision/GAMEPLAY.md), and [INTERFACE vision](../../vision/INTERFACE.md).

# Purpose

Define species-level DNA, trait relationships, mutation-point generation, human and autonomous speciation, and the persistent tree of life.

# Species DNA

Every v1 organism references one immutable DNA definition owned by its species. Define the compiled representation of:

- Trait families and levels or nodes.
- Prerequisites and incompatibilities.
- Direct capabilities and numeric modifiers.
- Cross-family effects.
- Ongoing metabolic and reproductive costs.
- Starting traits and future-accessible paths.
- Visual descriptors that clients may use without affecting mechanics.

A proposed DNA change must be validated and fully compiled before a speciation command can be accepted.

The detailed graph, typed-effect, stacking, activation, cost-channel, compilation, and candidate-frontier proposal is defined in [TRAIT_SYSTEM.md](TRAIT_SYSTEM.md). The first concrete family forests, milestone paths, and ecological tradeoffs are proposed in [TRAIT_CATALOGUE.md](TRAIT_CATALOGUE.md). This document owns mutation income, speciation transactions, autonomous choice, and lineage; the trait plans own the structure and meaning of the DNA being changed.

## Canonical trait families

The first compiled schema recognizes the following stable family IDs:

| Family ID | Compiled effects owned by the family |
| --- | --- |
| `CellularOrganization` | Viable/mature structure, scale, membranes/walls, compartments, eukaryotic organization |
| `EnvironmentalTolerance` | Moisture, temperature, and gas/chemical tolerance curves and stress costs |
| `ResourceAcquisition` | Uptake tags, transfer throughput, active-transport costs, ingestion and digestive access |
| `ExternalEnergyCapture` | Environmental reaction IDs, capture throughput, opportunity gates, and capture efficiency |
| `InternalMetabolism` | Internal reaction IDs, reserve mobilization, maintenance, assembly throughput, regulation, and waste routing |
| `EnergyStorage` | Reserve-resource eligibility and maximum energy-storage capacity |
| `NutrientStorage` | Available-store capacity by resource/tag and stockpiling constraints |
| `GrowthLifecycle` | Lifecycle phases, growth gates, dormancy, maturity, and senescence |
| `Reproduction` | Allocation model, health/resource gates, cooldown and jitter, overhead, and offspring requirements |
| `Locomotion` | Movement modes, velocity, terrain compatibility, and energy costs |
| `Sensing` | Observable signal types, ranges, precision, and update cost |
| `BehavioralRegulation` | Available behaviors, selection parameters, and condition-to-goal mappings |
| `PredationScavenging` | Target rules, interaction range, kill/capture and consumption parameters |
| `Defense` | Capture/kill resistance, structural protection, deterrence, and declared costs |
| `EvolutionaryMachinery` | Mutation-income modifier, affordable change breadth, DNA exchange, and abstract sexual reproduction |

Stable family IDs organize validation and presentation. Traits may emit effects owned by other families only through declared cross-family effects, and the compiler records the source trait for explanation. Future family additions must not change the meaning of existing IDs in a saved rule pack.

## Metabolic boundaries

The compiler treats acquisition, energy capture, internal metabolism, and storage as distinct effect domains:

```text
NutrientStorage
    --defines capacity of--> AvailableStore

external matter
    --ResourceAcquisition--> AvailableStore

environmental substrates + energy opportunity
    --ExternalEnergyCapture--> EnergyReserve(ReserveOrganic) + products

AvailableStore + internal substrates
    --InternalMetabolism--> EnergyReserve, Structure, internal stores, or waste

EnergyReserve
    --InternalMetabolism / SpendStoredEnergy--> useful work + spent matter + heat
```

No compiled trait may directly credit stored energy or matter. It enables or modifies a balanced reaction, capacity, or cost. `EnergyStorage` changes what can be held; it neither creates reserve nor increases a resource's energy density. A new reserve chemistry requires a new resource and balanced reactions.

`NutrientStorage` independently caps non-reserve matter in `AvailableStore`. Acquisition fills it and internal reactions consume or route it. It must not be inferred from energy capacity because species may be good at surviving energetic shortages while being poor at stockpiling a required micronutrient, or vice versa.

Every founding DNA contains `BasalReserveMobilization`, `BasalMaintenance`, and `BasalBiomassAssembly` internal capabilities plus `PrimitiveOrganicReserve` with a capacity of 10,000 energy units. Incremental reserve-capacity traits and a later cellular-organization-gated `CompartmentalizedReserve` are the initial storage path. Increasing capacity preserves current reserve amounts and can immediately lower reserve-relative health until the additional capacity is filled.

Fermentation and respiration compile as internal catabolic reactions. Hydrogen acetogenesis, sulfide anoxygenic phototrophy, and later oxygenic photosynthesis compile as external energy-capture reactions. Organic uptake is a resource-acquisition capability. These placements do not prevent cross-family prerequisites or combined UI explanations.

The first metabolism graph must implement the asymmetric paths in [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md): hydrogen acetogenesis reaches organic uptake and fermentation through the cheaper bridge; sulfide anoxygenic phototrophy receives stronger opening specialization traits but requires metabolic regulation and generalized catabolism before fermentation, or the substantially more expensive complex-photosystem, manganese/calcium water-oxidation, and oxygen-tolerance route to oxygenic photosynthesis. Suppression through metabolic regulation reduces ongoing cost but never removes an acquired trait.

# Mutation-point economy

Income is a function of total living population, average relative health, and DNA modifiers. Relative health is derived from concrete organism state, and the mutation calculation consumes the end-of-tick species average defined in [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md). The detailed plan must choose:

- Formula and units.
- Scaling and diminishing returns.
- Update frequency and fractional accumulation.
- Caps or anti-runaway behavior.
- Treatment of zero population and extinction.
- UI breakdown and forecast inputs.

Controlled and uncontrolled species use exactly the same income calculation.

The first calibration candidate is:

```text
effectivePopulation = 100 * log2(1 + livingPopulation / 100)
incomePerTick = effectivePopulation
              * averageRelativeHealth
              * dnaMutationModifier
              * tickDurationHours
              / 750
```

`referencePopulation = 100` and the `750` healthy-effective-organism-hours per point are versioned balance values. Fractional income accumulates deterministically. The logarithm provides diminishing returns without a discontinuous cap, and population zero produces zero income.

Using reserve fraction as the temporary fixture proxy for the full derived-health formula, this curve gives the hydrogen founder 40 MP near hour 338 and the sulfur founder 60 MP near hour 511. At a provisional fastest speed of two ticks per real second, those are approximately 2.8 and 4.3 minutes. See [SULFUR_TILE_STARTING_CONFIGURATION.md](SULFUR_TILE_STARTING_CONFIGURATION.md) for inputs and sensitivity limits. Those timings must be rerun with the structural, nutrient, age, lifecycle, and environmental factors enabled and after post-speciation scenario tests.

# Speciation

V1 allows one to four occupied founding tiles with per-tile fractions of 50%, 20%, 8%, and 3% respectively. Define:

- Minimum founder count and percentage rounding.
- Mutation-price calculation for multiple trait changes.
- Command validation and atomicity.
- Random founder selection without thread-order dependence.
- DNA replacement and new species creation.
- The duplicated unused mutation-point balance on ancestor and descendant.
- Control transfer to the descendant in survival.
- Event and client data emitted by the operation.

```text
Speciate(ancestor, proposedDNA, selectedTiles):
    validate control, cost, traits, and occupied tiles
    deduct price from ancestor balance
    select deterministic random founder subsets
    create descendant with compiled DNA and unused balance
    move selected organisms to descendant species
    append lineage event
    transfer control when required by mode
    publish speciation result
```

# Autonomous evolution

Autonomous species accumulate points identically but choose when and how to speciate. The heuristic should remain stochastic while weighting adaptations toward recent environmental pressures.

Define:

- Observation window for death-risk profiles, realized triggers, and contributing stresses.
- Mapping from pressures to relevant trait candidates.
- Affordability and prerequisite filtering.
- Weighting between adjacent and large DNA changes.
- Founding-tile selection based on distribution and health.
- Exploration probability so evolution is not perfectly optimizing.
- Explainability record describing why candidates were weighted.
- Protection against future knowledge or client-observation effects.

# Lineage

- Setup creates one non-species `AbiogenesisOriginEvent` with one sandbox root or two independent survival roots.
- A root species has no parent species; each non-root species has exactly one parent.
- Living and extinct species remain in the lineage store.
- Inter-species breeding is excluded.
- V1 survival has no backtracking.
- The lineage representation must retain enough information for future recovery and multiplayer ownership.

# Required decisions and tests

- [ ] Concrete trait-definition and compiled-DNA record schemas using the fixed family and effect-domain boundaries above.
- [ ] Mutation cost and income formulas.
- [ ] Deterministic fixed-point or lookup-table evaluation of the provisional logarithmic effective-population curve.
- [ ] Founder rounding and minimum viable subset.
- [ ] Autonomous-evolution scoring and timing.
- [ ] Lineage event/state representation.
- [ ] Validation error model for proposed DNA.
- [ ] Deterministic founder-selection tests.
- [ ] Trait prerequisite/incompatibility property tests.
- [ ] Family/effect ownership validation and cross-family effect attribution tests.
- [ ] Capacity-only trait tests proving that energy and nutrient balances are unchanged at speciation.
- [ ] Reaction classification tests preventing external capture, internal catabolism, and direct capacity effects from bypassing ledger validation.
- [ ] Controlled/uncontrolled income equivalence tests.

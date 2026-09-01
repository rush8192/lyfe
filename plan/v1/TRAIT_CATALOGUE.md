# Initial V1 DNA Trait Catalogue

Status: initial topology and design proposal; first policy review incorporated

Sources: [DNA trait system](TRAIT_SYSTEM.md), [evolution plan](EVOLUTION.md), [organism plan](ORGANISMS.md), [founding metabolisms](FOUNDING_METABOLISMS.md), and the [organism vision](../../vision/ORGANISMS.md).

# Purpose

Define the first concrete shape of LYFE's trait-family forests, their important cross-family prerequisites, and the ecological roles and liabilities they are intended to create. This is a catalogue plan, not yet an authoritative rule pack: stable final IDs, numeric effects, mutation prices, and exact balance curves will follow deterministic fixture testing.

The catalogue must make evolution attractive without turning it into a universal sequence of upgrades. A lineage should be able to succeed as a lean specialist, a regulated generalist, or a more structurally complex organism. Each strategy should gain access to different opportunities and fail under different pressures.

# Evolutionary design rules

## Complexity is not a universal level

`changeComplexity` measures how much can change in one speciation event; it is not organism complexity and must not impose a global debuff. Organism complexity emerges from acquired structures, reactions, sensing, regulation, and lifecycle machinery. Their costs are compiled from explicit effects.

Every non-baseline trait must state:

- **Capability or advantage:** what the organism can now do, or do better.
- **Liability:** an ongoing, use-dependent, reproductive, structural, material, environmental, or opportunity cost.
- **Niche:** the conditions under which the trait can repay that liability.
- **Escape or counterpressure:** the conditions that make a different branch attractive.

Mutation-point price alone is not an adequate liability because it is paid once while a trait's effects persist through the lineage.

## Permitted liability forms

At least one meaningful liability should accompany every substantial capability:

| Liability | Typical use |
| --- | --- |
| Higher `BaselineMaintenance` | Larger cells, compartments, constitutive machinery |
| Higher mature or viable biomass | Walls, size, organelles, storage structures |
| Higher reproduction threshold or overhead | Large cells, complex organization, defensive structures |
| Micronutrient or macronutrient quota | Catalytic machinery, photosystems, compartments |
| Passive upkeep while enabled | Sensing, regulation, active transport, defense |
| Use-dependent energy cost | Movement, ingestion, active acquisition, predation |
| Reduced peak reaction efficiency | Broad tolerance or generalized metabolism |
| Narrower opportunity envelope | Highly efficient specialists |
| Greater stress cost outside a preferred envelope | Fragile complex machinery or specialization |
| Permanent branch exclusion | Only where two adaptations are genuinely incompatible |

The rule pack should favor costs that follow from the capability. Arbitrary penalties added only to equalize mutation-point value will be hard to explain and balance.

## Complexity and environmental sensitivity

Greater complexity should usually increase dependency before it directly reduces temperature or chemical hard limits. More machinery requires more reserve, structural matter, and catalytic micronutrients; starvation and incomplete offspring provisioning therefore become more dangerous. Complex organization may also modestly amplify stress costs outside preferred conditions, but there is no blanket rule that advanced life has universally narrower tolerances. The first balance fixtures use these provisional effects relative to the phenotype immediately before each node:

| Node | Mature biomass | Baseline maintenance | Reproduction overhead | Soft stress cost | Additional dependency |
| --- | ---: | ---: | ---: | ---: | --- |
| `CompartmentalizedCell` | ×1.25 | ×1.10 | ×1.10 | unchanged | Higher phosphorus requirement and at least one additional catalytic micronutrient quota |
| `ProtoEukaryoticOrganization` | ×1.75 | ×1.25 | ×1.25 | ×1.15 | Additional catalytic quotas and greater minimum provisioning |

These are balance starting points, not claims that real cellular complexity follows these exact ratios. Neither node changes a hard temperature or toxin-death limit by default. Its benefits must include sufficient increases in reaction organization, storage ceilings, ingestion, or action throughput to repay these burdens in a rich niche.

Environmental-tolerance traits can offset specific sensitivities at their own ongoing cost. This creates an evolutionary choice between paying to protect complex machinery, remaining in a favorable niche, or retaining a simpler body plan.

## Incentives to evolve

Evolution must offer capabilities that cannot be reproduced by simply increasing a founding reaction's efficiency:

- Access to organic remains, scarce substrates, light, oxygen, or new tile types.
- Survival across temporal variability through storage, dormancy, or regulation.
- Directed migration and resource discovery through sensing and movement.
- Scavenging and predation on biological resource concentrations.
- Greater peak throughput through larger or compartmentalized cells.
- Reduced dependence on volcanic sources through heterotrophy or photosynthesis.
- Better defense against ecological pressures created by other species.
- Faster mutation income or broader speciation proposals through evolutionary machinery.

A complex lineage should generally have a higher potential ceiling but a higher break-even resource level. A simple specialist should remain competitive in the stable niche for which it is optimized.

# Catalogue notation and scope

- Indentation shows `displayParentTraitId` within a family.
- `requires` names hard genetic prerequisites, including cross-family prerequisites.
- `replaces` means exact effect supersession; the ancestor remains acquired.
- `incompatible` is used sparingly and permanently closes that branch for the descendant lineage.
- **Foundation** traits are present in every founder.
- **Opening** traits define one of the two founding loadouts.
- **Early**, **middle**, and **late** describe intended reach, not hard dates.
- **Future** nodes preserve an extension direction and are not required in the v1 rule pack.

The names below are proposed canonical-style IDs. They may be renamed before a rule pack or save refers to them.

The v1 content ceiling includes `ProtoEukaryoticOrganization`, engulfing predation, and oxygenic photosynthesis as achievable late-game keystones. A healthy survival run should initially be calibrated for approximately 15–25 player-directed evolutionary decisions. That cadence is a provisional order-of-magnitude target, not yet an acceptance threshold.

Late-game branches may remain broad catalogue placeholders until their prerequisite mechanics and balance fixtures exist. A placeholder is visible in planning but is not included in the authoritative rule pack, mutation frontier, or client tree. The initial implementation should complete coherent vertical paths before adding shallow numeric upgrades across every family.

# Founding DNA

Every founder starts with the following common traits or equivalent compiled capabilities:

| Family | Founding trait | Purpose and opening liability |
| --- | --- | --- |
| `CellularOrganization` | `PrimitiveCell` | Membrane-bound viable structure with low throughput and low maintenance |
| `EnvironmentalTolerance` | `BasalAquaticTolerance` | Viable only within a modest warm-aquatic envelope |
| `ResourceAcquisition` | `PassiveSmallMoleculeUptake` | Low-cost access to simple dissolved matter; poor selectivity and throughput |
| `InternalMetabolism` | `BasalInternalMetabolism` | Reserve mobilization, maintenance, and biomass assembly at primitive rates |
| `EnergyStorage` | `PrimitiveOrganicReserve` | Capacity of 10,000 energy units; limited starvation buffer |
| `NutrientStorage` | `PrimitiveNutrientStore` | Needs-only macronutrient staging plus one extra inherited micronutrient-quota set |
| `GrowthLifecycle` | `DirectLifecycle` | Growth directly to reproductive maturity; no dormant or specialized phase |
| `Reproduction` | `PrimitiveFission` | Near-even zero-sum division with a conservative health gate |
| `BehavioralRegulation` | `StochasticActivity` | Random or fixed activity without sensed goal selection |
| `EvolutionaryMachinery` | `AsexualInheritance` | Baseline mutation income and change-complexity limit |

Passive environmental displacement is world physics, not a locomotion trait. Founders have no active locomotion, sensing, predation, or defense capability unless a scenario later adds it explicitly.

The scenario then adds exactly one opening capture trait:

- `HydrogenAcetogenesis`, plus enough `VolcanicSulfurToleranceI` to survive its selected volcanic tile.
- `SulfideAnoxygenicPhototrophy`, whose declared cross-family effects provide the stronger native sulfide tolerance already priced into that specialist.

The metabolism choice is a scenario restriction, not a permanent incompatibility in the biological graph. A descendant may eventually acquire the other founding reaction, but opening capture traits have a high normal mutation price and change-complexity value even though setup grants one directly. The first pricing fixture should start at no less than roughly `160 MP` for a cross-pathway root—comparable to a major escape path—and include the costs of any tolerance and regulation needed to operate both effectively. Carrying two constitutive pathways without `MetabolicRegulation` retains both declared upkeep profiles.

# Proposed family forests

## `CellularOrganization`

```text
PrimitiveCell                                            Foundation
├── SelectiveMembrane                                   Early
│   ├── ReinforcedMembrane                              Middle
│   └── InternalMembraneScaffolding                     Middle
│       └── CompartmentalizedCell                       Middle
│           └── ProtoEukaryoticOrganization             Late
├── RigidCellWall                                       Early
│   └── LayeredCellEnvelope                             Middle
└── IncreasedCellScaleI                                 Middle
    └── IncreasedCellScaleII                            Late
```

- `SelectiveMembrane` improves retention and selective transport but adds membrane maintenance and synthesis quotas.
- `ReinforcedMembrane` increases chemical and physical resilience while increasing reproduction matter and reducing passive uptake.
- `RigidCellWall` provides capture resistance and chemical protection but raises biomass requirements and movement costs; it can coexist with membrane specialization.
- `InternalMembraneScaffolding` enables more reaction and storage organization but adds phosphorus-rich structure and maintenance.
- `CompartmentalizedCell` unlocks compartmentalized storage, higher metabolic throughput, and more precise regulation. It raises mature biomass, reproduction overhead, and micronutrient quotas.
- `ProtoEukaryoticOrganization` is the proposed late-v1 complexity keystone. It unlocks engulfment, advanced locomotion, larger scale, and high-throughput organelle-like effects, while substantially increasing reserve break-even, structural quotas, generation time, and stress outside preferred conditions.
- Increased scale improves ingestion, storage, predation, and defense ceilings but costs matter, maintenance, and reproduction time. It should not automatically improve every reaction per unit biomass.
- The first spatial calibration maps `PrimitiveCell`, `IncreasedCellScaleI`, and `IncreasedCellScaleII` to mature-radius multipliers `1.00×`, `1.50×`, and `2.25×`, with cube-scaled structure targets and lower Brownian displacement for larger bodies; see [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).
- A lineage already remains primitively small and efficient by declining the increased-scale nodes. An explicit miniaturization branch would mean evolving below the founding scale, which is a separate unresolved specialization described near the end of this document.

## `EnvironmentalTolerance`

This family is intentionally a forest of specific adaptations rather than one universal tolerance ladder.

```text
BasalAquaticTolerance                                   Foundation
├── ThermalBreadthI                                     Early
│   └── ThermalBreadthII                                Middle
├── HeatSpecialization                                  Early
│   └── ExtremeHeatSpecialization                       Middle
├── ColdSpecialization                                  Middle
├── ShallowWaterTolerance                               Middle
│   └── IntermittentDesiccationTolerance                Late
├── VolcanicSulfurToleranceI                            Opening/Early
│   ├── VolcanicSulfurToleranceII                       Middle
│   └── SulfurDioxideTolerance                          Middle
└── OxygenToleranceI                                    Middle
    └── OxygenToleranceII                               Late
```

- Breadth lowers environmental stress across a wider band but raises baseline maintenance or reduces peak metabolic efficiency.
- Heat and cold resilience improve independently and are not incompatible. Their total burden is convex and includes a coupling term, making simultaneous extreme heat and extreme cold resilience substantially more expensive than either specialization alone.
- Heat and cold specialization shift the preferred envelope and increase performance near an extreme while worsening performance toward the opposite extreme.
- Shallow-water and desiccation traits enable occupation of seasonally moist terrestrial tiles while surface moisture is present, but impose protective structure and reduced exchange throughput. Survival through a dry interval requires dormancy, migration, or another later adaptation; continuously dry land remains outside v1.
- Sulfur tolerance modifies H₂S and SO₂ independently. Substrate use may contribute a small relevant tolerance but never replaces explicit toxin handling.
- Oxygen tolerance pays detoxification upkeep before oxygen respiration or oxygenic photosynthesis can exploit oxygen-rich niches.

The first deterministic cost model derives upkeep from the final compiled envelope rather than merely summing trait costs:

```text
heatExtension = max(0, compiledHardMaximum - baselineHardMaximum)
coldExtension = max(0, baselineHardMinimum - compiledHardMinimum)

thermalToleranceUpkeep = heatCostCurve(heatExtension)
                         + coldCostCurve(coldExtension)
                         + breadthCoupling
                           * normalized(heatExtension)
                           * normalized(coldExtension)
```

Both individual curves are convex. `breadthCoupling` makes a lineage capable of both extremes pay for broad biochemical robustness without blocking either axis. The curves and coefficient are versioned balance data, and the server exposes their contribution in compiled-effect provenance.

## `ResourceAcquisition`

```text
PassiveSmallMoleculeUptake                              Foundation
├── SelectiveTransport                                  Early
│   ├── ActiveTransport                                 Middle
│   └── HighAffinityMicronutrientUptake                 Middle
├── OrganicResourceUptake                               Early (40 MP candidate)
│   ├── DissolvedOrganicSpecialization                  Middle
│   └── ParticulateOrganicIngestion                     Late
└── MineralSurfaceExtraction                            Middle
```

- Selectivity reduces loss and toxin uptake but adds machinery and can lower raw throughput.
- `PassiveSmallMoleculeUptake` recognizes the organism's inherited quota deficits but provides only one shared `0.5`-probability, one-quantum opportunity per organism-hour. It cannot build arbitrary surplus, overcome an absent resource, or claim priority under contention; its background cost is included in base maintenance.
- Active transport accesses scarce resources against a gradient at a direct energy cost.
- High-affinity uptake supports micronutrient-poor niches but adds element-specific quotas and passive upkeep.
- `OrganicResourceUptake` transfers suitable environmental organic matter into the available store; it does not extract energy by itself.
- Particulate ingestion requires `CompartmentalizedCell` and appropriate scavenging or predation access. It unlocks nonzero `IngestedMatterBuffer` capacity and supports rich biological-resource niches, but carries high per-use and digestion costs. The first buffer, ingestion, digestion, action-cost, upkeep, and exactly balanced structural-food reaction are fixed in [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md); advanced digestive substrates remain later rule-pack work.
- Mineral extraction is a proposed route toward non-volcanic inorganic-resource niches and later photoferrotrophy, with slow throughput and potentially toxic byproducts.

## `ExternalEnergyCapture`

```text
HydrogenAcetogenesis                                    Opening
├── ImprovedHydrogenCapture                             Early
│   └── HighAffinityHydrogenCapture                     Middle
└── AcetogenicCarbonEfficiency                          Middle

SulfideAnoxygenicPhototrophy                            Opening
├── ImprovedSulfideCapture                              Early
├── LowLightPhotosystem                                 Middle
├── HighFluxPhotosystem                                 Middle
└── ComplexPhotosystem                                  Middle (100 MP candidate)
    └── ManganeseCalciumWaterOxidation                  Late (150 MP candidate)
        └── OxygenicPhotosynthesis                      Late

Photoferrotrophy                                        Future
Methanogenesis                                           Future candidate
```

- Hydrogen improvements extend useful range away from a source but cannot make a neighboring low-H₂ tile sustain a robust founder-equivalent population.
- Sulfide improvements increase peak volcanic performance while preserving light and H₂S dependence.
- Low-light and high-flux photosystems are proposed specializations: the first broadens depth/day opportunity at lower peak rate; the second increases peak shallow-water output while increasing photochemical stress and quota.
- Oxygenic photosynthesis requires `ComplexPhotosystem`, `ManganeseCalciumWaterOxidation`, and `OxygenToleranceI`. It opens a widespread water-and-light energy niche but brings larger structural/catalytic quotas and oxygen-related ecological consequences.
- A capture trait always enables or modifies a mass-balanced reaction; no trait directly credits reserve.

## `InternalMetabolism`

```text
BasalInternalMetabolism                                 Foundation
├── EfficientReserveMobilizationI                       Early
│   └── EfficientReserveMobilizationII                  Middle
├── BiomassAssemblyControl                              Early
├── MetabolicRegulation                                 Middle (60 MP candidate)
│   ├── GeneralizedOrganicCatabolism                    Middle (80 MP candidate)
│   │   └── OxygenRespiration                           Late
│   ├── WasteRouting                                    Middle
│   └── ResourceAllocationControl                       Middle/Late
├── Fermentation                                        Middle (80 MP candidate)
└── CatalyticRecycling                                  Late
```

- `Fermentation` requires `OrganicResourceUptake` and either `HydrogenAcetogenesis` or `GeneralizedOrganicCatabolism`. The hydrogen lineage can therefore reach it through its acetogenic biochemical foundation; the sulfide lineage requires `MetabolicRegulation` and `GeneralizedOrganicCatabolism`, preserving the 120 MP versus 220 MP escape-path targets. Its display parent remains `BasalInternalMetabolism` so the family tree does not accidentally make generalized catabolism mandatory for both routes.
- Regulation permits pathway suppression, conditional selection, and bounded relative priority among metabolic process classes. It reduces wasted operation in variable habitats but adds a smaller passive control cost even while pathways are suppressed.
- `ResourceAllocationControl` adds a bounded ordered set of per-resource holdbacks for favored internal processes. It improves reliability under contention but adds regulation upkeep and can strand protected material when the beneficiary cannot run. Temporary resource binding, default sharing, and holdback semantics are defined in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- Fermentation gives access to energy from acquired organic matter without oxygen, at lower yield than respiration and with explicit waste products.
- Oxygen respiration requires organic acquisition, `OxygenToleranceI`, oxygen access, and generalized catabolism. It offers high yield but makes performance depend on a still-variable oxygen supply and increases catalytic quotas.
- Catalytic recycling reduces micronutrient losses and waste at the cost of complex organization and maintenance.

## `EnergyStorage`

```text
PrimitiveOrganicReserve                                 Foundation
└── ReserveCapacityI                                    Early
    └── ReserveCapacityII                               Middle
        └── CompartmentalizedReserve                    Middle/Late
            └── DenseReserveHandling                    Future
```

- Incremental capacity lengthens survival through poor periods but is empty when acquired, increases reproduction provisioning needs, and may add modest structure.
- `CompartmentalizedReserve` requires `CompartmentalizedCell`, provides a step increase, and adds maintenance and material quota.
- Storage never increases energy density or creates reserve. A genuinely denser reserve would require a future resource and reaction set, not a scalar trait.

## `NutrientStorage`

```text
PrimitiveNutrientStore                                  Foundation
├── MacronutrientRetention                              Early
├── MicronutrientRetention                              Middle
│   └── SelectiveMicronutrientStockpiling               Middle
└── CompartmentalizedNutrientStore                       Late
```

- `PrimitiveNutrientStore` supplies `512` expanded-matter load units for dissolved macronutrients, `64` for free micronutrients, and no ingested-matter capacity. It begins empty, stages only current macronutrient process demand, and targets at most one additional complete inherited reproduction-quota set. These are capacity and policy effects, not resource grants.
- `MacronutrientRetention` adds a persistent macronutrient fill target and expands dissolved-store capacity. This supports migration and scarcity but costs uptake/control energy and can retain harmful excess; its exact increment and target are tuning values.
- `MicronutrientRetention` expands free-micronutrient capacity and permits more than the primitive single-set target. It does not decide which scarce nutrient is favored.
- `SelectiveMicronutrientStockpiling` requires `SelectiveTransport`, adds resource-specific targets or admission protection, and improves scarce catalyst security at a passive control cost.
- `CompartmentalizedNutrientStore` requires `CompartmentalizedCell`; it expands and separates eligible storage, supports complex pathways, and raises structural and reproductive quotas.
- `WasteRouting` and `CatalyticRecycling` remain internal-metabolism traits: they may retain useful spent products only through declared balanced reactions and ordinary storage capacity. Nutrient-storage traits alone do not turn waste into usable input.

## `GrowthLifecycle`

```text
DirectLifecycle                                         Foundation
├── RegulatedMaturation                                 Early
│   └── ResourceConditionalGrowth                       Middle
├── DormantPhase                                        Middle
│   └── ResistantDormantPhase                           Late
└── MotileDispersalPhase                                Late
```

- Regulated growth avoids committing matter under poor conditions but delays reproduction when its control signals are conservative.
- Dormancy sharply lowers maintenance and increases some tolerances while disabling growth, reproduction, and most active capabilities. Entry and exit consume energy.
- Resistant dormancy requires protective structure and enables passage through hostile seasons or tiles at a high construction cost.
- A motile dispersal phase requires locomotion, sensing, and regulation. It improves colonization but diverts early-life energy from growth.

## `Reproduction`

```text
PrimitiveFission                                        Foundation
├── DivisionTimingControl                               Early
│   ├── LowerDivisionThreshold                          Early
│   ├── ConservativeDivision                            Early
│   └── RapidDivisionCycle                              Middle
└── PolarizedGrowthControl                              Middle
    └── AsymmetricBudding                               Middle
        └── ProvisionedOffspring                        Late
```

- `PrimitiveFission` owns the founding near-even allocation choice. The name deliberately avoids claiming that founders possess a complete modern bacterial divisome.
- Division timing control improves coordination and permits cooldown/threshold specialization, but adds constitutive reproductive machinery.
- Lower thresholds increase reproductive opportunity but create frailer parents and offspring and a higher failed-lineage risk.
- Conservative division raises the health/resource gate and offspring provisioning, trading frequency for survival.
- Rapid cycling reduces base interval while increasing per-division overhead and senescence pressure.
- Polarized growth control is a prerequisite for a deliberately asymmetric allocation.
- `AsymmetricBudding` replaces the allocation choice so the continuing parent retains more resources and the offspring begins smaller. It lets a well-established parent remain productive but adds machinery, lengthens offspring maturation, and increases juvenile mortality risk.
- Provisioned offspring adds reserve/nutrient allocation and survivability at substantial parent cost.

The first exact gate, work-cost, cooldown, allocation, upkeep, and aging modifiers for this tree are defined in [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md). Their mutation-point prices remain part of evolution-economy calibration.

Threshold/cooldown specializations may coexist unless their composed values violate validation bounds. Reproduction remains deterministic after eligibility; timing traits modify the base cooldown, bounded jitter window, and gates rather than adding a flat per-tick success chance. Exact effect supersession, rather than acquisition order, resolves changes to allocation mode.

The primitive age curve also reduces metabolic throughput after senescence onset, making young offspring more productive than aging continuing parents. Future lifespan/repair branches may reshape this curve or add explicit maintenance and damage mechanics, but those effects must carry their own costs and may not silently double-charge the same age penalty.

## Reproduction evidence and v1 interpretation

The evidence supports a neutral game abstraction rather than a strict historical ladder:

- Model fatty-acid protocells can grow into filaments and divide into multiple daughters under modest shear without evolved division machinery; internal contents are distributed among those daughters. This makes a generic primitive fission capability more defensible than projecting modern binary fission onto the first organisms. See [Zhu and Szostak's model-protocell experiments](https://pmc.ncbi.nlm.nih.gov/articles/PMC2669828/).
- Modern bacterial fission can depend on coordinated machinery centered on FtsZ and the divisome. That supports making reliable timing/control an acquired capability rather than assuming it is free. See [experimental work on FtsZ assembly](https://pmc.ncbi.nlm.nih.gov/articles/PMC6050046/).
- Bacterial reproduction is diverse. Cultured planctomycetes include polar and lateral budding, binary fission, and a clade with members capable of both modes. Budding is therefore an alternative strategy, not a universally later or superior stage. See the [comparative cultivation and time-lapse study](https://pmc.ncbi.nlm.nih.gov/articles/PMC7286433/).
- Time-lapse observations of a budding planctomycete show a persistent mother producing successive buds, while the daughter follows a different maturation schedule. See the [Gemmata cell-cycle study](https://pmc.ncbi.nlm.nih.gov/articles/PMC2656463/).
- Even morphologically symmetric fission can segregate cellular damage asymmetrically, producing aging and rejuvenated lineages. V1 does not need a separate damage inventory, but it should avoid claiming that equal matter allocation makes both outcomes biologically identical. See the [E. coli aggregate-segregation experiment](https://pmc.ncbi.nlm.nih.gov/articles/PMC2268587/).

The confirmed, scientifically grounded v1 policy is:

1. Every founder uses `PrimitiveFission` with a near-even allocation because it is legible, mass-balanced, and gives the opening fixtures one reproduction baseline.
2. `AsymmetricBudding` is an acquired alternative allocation strategy, not an assertion that budding evolved after binary fission in real history.
3. The authoritative simulation retains one existing organism as the continuing, aging lineage and creates one age-zero offspring in both modes. True-split versus budding changes matter allocation, maturity, and cost rather than entity identity.
4. V1 does not track damaged-protein inheritance. Senescence and offspring maturity provide the initial abstraction; damage segregation can become a future lifecycle trait if it creates useful gameplay.

## `Locomotion`

```text
ActiveMotility                                          Early
├── FlagellarPropulsion                                 Early/Middle
│   ├── EfficientCruising                               Middle
│   └── BurstPropulsion                                 Middle
├── SurfaceGliding                                      Middle
└── AdvancedPropulsion                                  Late
```

- Active motility enables DNA-controlled velocity layered over the default Brownian random walk and carries energy per active distance.
- Flagellar propulsion improves speed and tile-edge access but adds construction quota and upkeep.
- Cruising lowers cost per distance at modest speed; burst propulsion raises acceleration and escape/capture performance at high peak cost.
- Surface gliding is efficient on suitable mineral or biological surfaces but weak in open water.
- Advanced propulsion requires complex organization and supports larger cells at correspondingly higher upkeep.
- The first active displacement ceilings—`4 R₀/hour` for basal active motility through `24 R₀/hour` for burst propulsion—are defined in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md); realized speed still depends on behavior, environment, and affordable energy.

## `Sensing`

```text
ContactDetection                                        Early
├── NearbyOrganismDetection                             Early/Middle
│   ├── RemnantDetection                                Middle
│   └── PreyThreatDiscrimination                        Late
├── ChemicalConditionSensing                            Middle
└── DirectionalEnvironmentalSensing                      Middle
    └── ExtendedRangeSensing                            Late
```

- Sensing exposes information; behavior is required to act on it.
- Greater range and discrimination improve decisions but add passive evaluation cost and catalytic machinery.
- Chemical sensing initially observes well-mixed tile state and neighboring-tile summaries, not nonexistent within-tile nutrient gradients.
- Future localized micronutrient fields can add gradient senses without changing the family boundary.
- The first entity-sensing tiers are `8 R₀` for nearby organisms, `16 R₀` for remnants/threat classification, and an absolute `32 R₀` extended-range ceiling; see [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).

## `BehavioralRegulation`

```text
StochasticActivity                                      Foundation
├── StateGatedActivity                                  Early
│   ├── ResourceConservation                            Early/Middle
│   ├── ReproductionReadiness                           Middle
│   └── StressAvoidance                                 Middle
├── DirectedForaging                                    Middle
│   ├── ScavengingBehavior                             Middle
│   └── HuntingBehavior                                 Late
└── MetabolicBehaviorCoordination                       Middle/Late
```

- State gating reduces waste when reserve or health is low but adds control upkeep and can miss brief opportunities.
- Directed behavior requires a matching sense and locomotion capability; without both it cannot activate.
- Conservation may suppress movement, acquisition, or reproduction while preserving baseline maintenance.
- Metabolic coordination complements, but does not replace, the internal `MetabolicRegulation` trait.

## `PredationScavenging`

```text
RemnantScavenging                                       Early/Middle
├── HighThroughputRemnantScavenging                     Middle
└── ParticulateDigestion                                Middle/Late

ContactPredation                                        Middle
├── CaptureMechanism                                    Middle
│   ├── ImprovedCaptureSuccessI                        Middle
│   │   └── ImprovedCaptureSuccessII                   Late
│   ├── ContestedFeedingPriorityI                      Middle
│   │   └── ContestedFeedingPriorityII                 Late
│   ├── PiercingExtraction                             Late
│   └── Engulfment                                     Late
└── ImprovedTargetCompatibility                         Late
```

- Scavenging grants target access but requires compatible organic acquisition and catabolism to realize value. Basic scavenging can extract already-simple reserve/dissolved contents through a costed two-hour handling cycle; its throughput child raises caps at higher action cost. Structural biomass requires particulate ingestion and digestion. The first action caps, economics, and structural-food reaction are defined in [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).
- Predation requires active proximity, a capture mechanism, suitable processing, and enough size or structural capability for the target.
- Improved capture success raises `predationAttackPower` and therefore the odds of a lethal attempt against the prey's `predationDefensePower`. It also raises attempt-energy cost and structural/catalytic requirements.
- Contested feeding priority raises `feedingPriorityWeight`, increasing the share of an already-killed prey allocated to this predator when several attackers succeeded. It does not improve the kill draw and carries handling/rapid-ingestion cost.
- Engulfment requires `ProtoEukaryoticOrganization`, `IncreasedCellScaleI`, and particulate ingestion. It opens a high-density food niche while imposing large movement, digestion, and reproduction costs.
- Predation should create density-dependent opportunity and counterpressure rather than dominate autotrophic production in every tile.
- First contact, capture, feeding, and chance-encounter distances are defined in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md); improving reach remains distinct from sensing, attack power, ingestion capacity, and feeding priority.

## `Defense`

```text
StructuralResistance                                    Early
├── CaptureResistance                                   Middle
└── ReinforcedDefense                                   Late

ChemicalDeterrence                                      Middle
└── TargetedDeterrence                                  Late

EmergencyEscapeResponse                                 Middle/Late
```

- Structural resistance can require `RigidCellWall` or `ReinforcedMembrane`; the cellular trait supplies structure while the defense trait specializes its interaction effects.
- `CaptureResistance` and `ReinforcedDefense` raise `predationDefensePower`, directly opposing capture-success traits. Their additional matter, upkeep, and possible movement penalties ensure that defense is valuable under predation pressure rather than universally optimal.
- Chemical deterrence raises situational defense and can increase an attacker's attempt cost. It consumes matter and energy and may impose self-toxicity without matching tolerance.
- Emergency escape requires threat sensing, behavioral regulation, and burst-capable locomotion. Its high use cost makes it valuable mainly under predation pressure.
- Larger size and ordinary locomotion may contribute defensive effects through their own families; those effects are not duplicated here.

## `EvolutionaryMachinery`

```text
AsexualInheritance                                      Foundation
├── IncreasedMutationSupplyI                            Early
│   └── IncreasedMutationSupplyII                       Middle
├── DNAExchange                                         Middle
│   └── AbstractSexualReproduction                      Late
└── ExpandedChangeCapacityI                             Middle
    └── ExpandedChangeCapacityII                        Late
```

- Mutation-supply traits increase mutation-point income but may add repair/replication cost or a modest organism-health liability; they never directly change individual DNA in v1.
- DNA exchange raises mutation income and/or proposal breadth while adding reproductive machinery cost. It remains within-species and requires no mate proximity.
- Abstract sexual reproduction further increases evolutionary capacity and reproduction overhead without individual recombination.
- Expanded change capacity increases `maxChangeComplexityPerSpeciation` but not mutation-point income, preserving two distinct axes.

# Cross-family milestone paths

These paths are the primary reason to evolve beyond opening efficiency. They also provide deterministic acceptance fixtures for the graph.

| Milestone | Minimum conceptual path | New niche | Principal liabilities |
| --- | --- | --- | --- |
| Hydrogen heterotroph | `OrganicResourceUptake` → `Fermentation` | Organic remains away from volcanism | Organic-resource dependence, uptake and catabolic costs |
| Sulfide heterotroph | `MetabolicRegulation` → `GeneralizedOrganicCatabolism` plus the path above | Variable or post-volcanic organic habitats | Higher 220 MP bridge and retained sulfide machinery upkeep |
| Oxygenic producer | `ComplexPhotosystem` → `ManganeseCalciumWaterOxidation` + `OxygenToleranceI` → `OxygenicPhotosynthesis` | Widespread shallow water with light | Mn/Ca quotas, oxygen stress, complex photosystem maintenance |
| Seasonal survivor | `ResourceConservation` + reserve/storage improvements + `DormantPhase` | Tiles with recurring poor seasons | Lost growth while dormant, entry/exit and storage costs |
| Directed colonizer | active locomotion + directional sensing + `DirectedForaging` or `StressAvoidance` | Patchy neighboring tiles | Continuous sensing and movement cost |
| Scavenger | remnant detection + scavenging access + organic uptake + catabolism | Dead-remain concentrations | Biological-resource dependence and processing cost |
| Active predator | sensing + locomotion + capture + ingestion + catabolism | Dense prey populations | High complexity, target dependence, counter-defense |
| Complex generalist | compartmentalization + regulation + storage + multiple reactions | Variable, resource-rich environments | High break-even energy, quotas, slower reproduction, stress sensitivity |

No milestone grants its complete capability from one family. The graph should make cross-family dependencies visible without drawing false parent-child relationships inside a family.

# Initial balance hypotheses

The first numerical pass should test the following directional expectations:

1. A specialist improving its founding capture path retains the best net growth in its ideal stable tile.
2. A regulated dual-path lineage survives more variable tiles but loses to the specialist when both remain in the specialist's optimum.
3. Storage and dormancy improve survival through seasonal shortages without increasing long-run energy production.
4. Larger and compartmentalized cells require a materially richer environment before their throughput advantage exceeds maintenance and reproduction cost.
5. Specific tolerance investments enable expansion but reduce peak performance enough that universal tolerance stacking is not automatically optimal.
6. Sensing without matching behavior, or behavior without sensing and locomotion, provides little or no benefit while retaining declared costs; the UI must warn about inactive combinations.
7. Predation becomes viable only above a prey-density/resource threshold and creates a niche for defense without making defense universally worthwhile.
8. Evolutionary machinery repays itself only across a sufficiently long remaining simulation horizon.

Each hypothesis needs a paired deterministic fixture comparing otherwise identical DNA in at least one favorable and one unfavorable environment.

# Decisions confirmed

1. `ProtoEukaryoticOrganization`, engulfing predation, and oxygenic photosynthesis are all in v1 scope as achievable late-game keystones.
2. Approximately 15–25 player-directed evolutionary decisions is the provisional order of magnitude for a healthy survival run; later pacing tests may revise it.
3. Descendants may acquire the other founding metabolism, but doing so has a major mutation price and change-complexity burden, retains both pathways' costs, and normally makes regulation and additional tolerance desirable.
4. Heat and cold resilience improve independently. Their convex individual costs and breadth-coupling cost make simultaneous extreme resilience expensive without declaring the traits incompatible.
5. Complex-cell sensitivity begins with higher structure, maintenance, reproduction, and micronutrient dependencies plus a modest soft-stress multiplier. The fixture values above are explicitly provisional and should move with biology-guided balance evidence.
6. Intermittent desiccation adaptation permits late-v1 occupation of seasonally moist terrestrial tiles. Continuously dry land remains future content.
7. Late branches may remain planning placeholders until their mechanics and balance are ready; placeholders are not selectable rule-pack nodes.

# Confirmed reproduction policy

Use near-even `PrimitiveFission` for every founder and make `AsymmetricBudding` a later alternative allocation strategy. This keeps setup narrow and the opening fixtures comparable without claiming that real-world budding historically followed binary fission. In both modes, retain the existing organism as an aging lineage and create one age-zero offspring; change allocation, maturity, and reproduction cost rather than identity semantics. The evidence and full rationale are recorded in [Reproduction evidence and v1 interpretation](#reproduction-evidence-and-v1-interpretation).

V1 uses a small number of discrete, authored allocation profiles so their costs and survival consequences can be explained and balanced. A future rules version may replace or augment these choices with a broader resource-allocation spectrum, allowing DNA to tune the fraction of reserve, nutrients, and structure committed to offspring. Such tuning must remain zero-sum, respect minimum viable parent and offspring states, and derive reproduction time, overhead, offspring maturity, and survival risk from the selected allocation rather than making the ratio a free benefit.

# Remaining cell-size decision

“Staying small” and “evolving miniaturization” are different choices:

- A lineage that never acquires `IncreasedCellScaleI` already retains the founding cell's low biomass requirement, low maintenance, short reproduction time, and limited storage/ingestion ceiling. No extra trait is needed to preserve that strategy.
- A proposed `MiniaturizedCell` node would make descendants smaller than the founder. It could further reduce maintenance, viable biomass, reproduction requirements, and resource demand while improving passive exchange per unit biomass. In return it would lower reserve and nutrient capacity, interaction radius, ingestion capability, defense against size-based capture, and maximum per-organism throughput.
- Because evolution is irreversible, `MiniaturizedCell` would be incompatible with `IncreasedCellScaleI`; taking it would permanently reject the large-cell, engulfment, and some complex-organization paths for that descendant lineage.

The unresolved product choice is therefore whether v1 needs an explicit oligotrophic miniaturization niche below the founding scale. The current recommendation is to retain `MiniaturizedCell` as a broad placeholder and decide after the founding-size, resource-scarcity, and predation fixtures show whether simply remaining at `PrimitiveCell` already produces the desired niche.

# Authoritative-catalogue completion criteria

Before this proposal becomes a rule pack:

- Every node has a final stable ID, family, display parent, full prerequisite predicate, mutation cost, change complexity, typed effects, activation requirements, pressure tags, and visual descriptors.
- Every substantial node has a documented advantage, liability, and target niche.
- Every exact replacement names stable supersedable effect keys.
- Every cross-family milestone compiles from a valid explicit proposal and fails when any hard prerequisite is omitted.
- Predation fixtures prove that attack-power traits oppose defense-power traits, while contested-feeding traits affect only post-kill allocation and carry their declared costs.
- Founding DNA compiles without inactive required capabilities and reproduces the existing hydrogen and sulfur fixtures.
- No trait credits matter or energy, and all new reactions pass ledger validation.
- Representative specialists, generalists, complex cells, dormant survivors, scavengers, predators, and defended prey each have at least one environment in which they outperform and one in which their liability matters.
- Autonomous-evolution pressure tags offer at least one reachable response to each modeled death/stress category without guaranteeing the optimal choice.
- The client can explain final attributes, inactive capabilities, permanent branch closures, and benefit/liability previews using server-provided provenance.

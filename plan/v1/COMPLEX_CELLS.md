# Complex Cellular Organization

Status: first v1 milestone, prerequisite, benefit, liability, activation, processing-load, scale, ingestion, and analytical calibration rules fixed; executable population validation remains

Sources: [trait catalogue](TRAIT_CATALOGUE.md), [trait system](TRAIT_SYSTEM.md), [organism health calibration](ORGANISM_HEALTH_CALIBRATION.md), [internal storage and allocation](INTERNAL_STORAGE_AND_ALLOCATION.md), [aerobic respiration](AEROBIC_RESPIRATION.md), [lifecycle and recycling](LIFECYCLE_AND_RECYCLING.md), [spatial calibration](SPATIAL_CALIBRATION.md), [predation](PREDATION.md), and [evolution](EVOLUTION.md).

# Purpose

Define what LYFE means by a “complex cell” and turn it into a costly, useful, and explainable late-v1 evolutionary milestone. Complex organization should create a higher ceiling in rich or variable environments without becoming a universal upgrade over a lean primitive specialist.

V1 separates three axes that are related in nature but mechanically distinct:

1. **Internal organization:** membranes or other bounded structures isolate and coordinate internal processes.
2. **Eukaryote-like organization:** integrated energy machinery, endomembrane trafficking, cytoskeletal control, and genome organization support substantially more cellular machinery.
3. **Cell scale:** a larger physical body increases storage, ingestion, interaction, and per-organism throughput ceilings while raising material, movement, and reproduction costs.

A lineage can become compartmentalized without becoming eukaryote-like, and can become compartmentalized without becoming larger. Engulfment requires all of the organization, scale, acquisition, and digestion capabilities separately.

# Biology-inspired abstraction

Internal compartmentalization is not exclusive to eukaryotes. Bacteria use protein- and lipid-bounded compartments to concentrate reactions, retain volatile intermediates, and isolate toxic chemistry. This supports retaining `InternalMembraneScaffolding` and `CompartmentalizedCell` as meaningful pre-eukaryotic adaptations rather than making every compartment a miniature eukaryotic organelle.

The last common ancestor of living eukaryotes already had mitochondria or mitochondrion-derived machinery, but the relative order of mitochondrial integration, endomembranes, cytoskeletal complexity, and phagocytosis remains debated. V1 should choose one playable order without claiming that it is the uniquely correct history:

```text
localized internal organization
    -> broad cellular compartmentalization
    -> integrated respiratory energy organelle and eukaryote-like control
    -> optional larger scale plus engulfment
```

The proposed `ProtoEukaryoticOrganization` node abstracts the long integration of an endosymbiont and host into inheritable cellular machinery. It does not claim that a few ordinary mutations literally create a mitochondrion.

Most anaerobic eukaryotes retain mitochondrion-related organelles, and the exceptional known mitochondrion-free lineage represents secondary loss rather than an ancestral mitochondrion-free eukaryote. V1 may therefore use aerobic respiration as the first attainable route into proto-eukaryotic organization while retaining anaerobic complex-cell strategies as a future derived branch.

# V1 trait path

```text
PrimitiveCell
└── SelectiveMembrane
    └── InternalMembraneScaffolding
        └── CompartmentalizedCell
            └── ProtoEukaryoticOrganization

ProtoEukaryoticOrganization additionally requires:
    OxygenRespiration
    CatalyticCarrierRetention
    MicronutrientRetention

IncreasedCellScaleI remains a separate sibling path.
Engulfment requires ProtoEukaryoticOrganization
                 + IncreasedCellScaleI
                 + ParticulateOrganicIngestion
                 + ParticulateDigestion
```

`OxygenRespiration` already requires `CatalyticCarrierRetention` and `MicronutrientRetention`; they are listed above to make the complete biological dependency visible. The trait compiler should store the nonredundant predicate while the client may display the expanded path.

V1 does not add a separate selectable `EndosymbioticEnergyOrganelle` node. Its prerequisites, costs, and narrative are part of `ProtoEukaryoticOrganization`. This avoids turning one milestone into several low-value purchases and stays closer to the intended 15–25 evolutionary decisions in a healthy run. A future rule pack may separate stable symbiosis, organelle integration, anaerobic organelles, and organelle loss.

# Functional model

## A shared internal-processing budget

Compartmentalization increases how much internal work can be organized at once; it does not multiply energy yield or make external resources appear. V1 should represent this with a shared processing budget:

```text
InternalProcessDefinition
    process_id
    processing_load_per_opportunity_or_extent
    ordinary_reaction_or_action_requirements
    optional_required_organization_tags

CompiledOrganism
    internal_processing_budget_per_hour
```

The budget applies to internal catabolism, digestion, carrier recharge, biomass assembly, and other explicitly tagged internal work. External uptake, photosynthetic or chemical capture, movement, predation attempts, and scavenging handling retain their existing separate ceilings and costs.

For each tick:

```text
availableInternalBudget =
    compiledBudgetPerHour
    * tickDurationHours
    * ageMetabolicThroughputQ
    * applicableConditionModifiersQ

for candidate internal processes in canonical allocation order:
    requestedLoad = requestedAdmissionUnits * processingLoadPerUnit
    admittedLoad = min(requestedLoad, remainingInternalBudget)
    admittedUnits = floor(admittedLoad / processingLoadPerUnit)
    remainingInternalBudget -= admittedUnits * processingLoadPerUnit
```

For a probabilistic reaction, the admission unit is an opportunity and work is reserved before its keyed success draw. For deterministic digestion or assembly, it is an executable whole extent. Resource availability, reaction-specific throughput, output capacity, catalyst quotas, success rolls, and internal holdbacks still apply. Raising the shared budget has no value when a reaction's own ceiling, external acquisition, storage, energy carriers, or substrate supply remains limiting.

The selected primitive-scale capacities are:

| Organization | Shared internal-processing budget | Relative to primitive |
| --- | ---: | ---: |
| `PrimitiveCell` | `160/hour` | `1.00×` |
| `InternalMembraneScaffolding` | `184/hour` | `1.15×` |
| `CompartmentalizedCell` | `240/hour` | `1.50×` |
| `ProtoEukaryoticOrganization` | `448/hour` | `2.80×` |

These values replace one another rather than multiply. They raise a ceiling, not per-extent efficiency or yield. Fermentation and respiration cost one work unit per admitted opportunity, particulate digestion costs four per extent, and biomass assembly costs eight per extent. Scale multiplies the active organization budget linearly with current radius, capped at mature radius. The proto-eukaryotic energy organelle separately raises the shared respiratory ceiling from 80 to 160 opportunities without changing either reaction's yield or acquisition limit. Exact admission order, structure assignments, assembly scaling, preservation proofs, and ecological landmarks are defined in [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md).

## Specialized capabilities remain separate traits

The organization path unlocks but does not automatically grant:

- `CompartmentalizedReserve` or `CompartmentalizedNutrientStore`.
- `ParticulateOrganicIngestion`, digestion, scavenging, or predation.
- Active locomotion, sensing, hunting, or fleeing.
- Additional metabolic reactions or better reaction yield.
- A larger body radius.
- Environmental tolerance.

This prevents `ProtoEukaryoticOrganization` from acting as one purchase that silently provides an entire ecological strategy. A complex lineage must select which opportunities justify its new overhead.

## Process isolation

Compartmentalization may later let a particular reaction isolate a toxic or volatile intermediate, but V1 should not grant a generic detoxification or success bonus. A process receives an isolation benefit only when its own rule definition names an organization tag and an exact effect. No current founding, fermentation, respiratory, oxygenic, or digestive reaction requires such an effect to function.

This preserves an extension point without adding a second, difficult-to-explain universal multiplier beside processing throughput.

# First trait values and liabilities

The existing complexity multipliers remain the starting point. Values are canonical rule-pack candidates, not scientific measurements:

| Node | MP / change complexity | Mature structure | Base maintenance | Reproduction work | Soft stress cost | Internal budget |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `InternalMembraneScaffolding` | `100 / 2` | unchanged | `+5/hour` | unchanged | unchanged | set `1.15×` |
| `CompartmentalizedCell` | `160 / 3` | `×1.25` | `×1.10` | `×1.10` | unchanged | set `1.50×` |
| `ProtoEukaryoticOrganization` | `240 / 4` | additional `×1.75` | additional `×1.25` | additional `×1.25` | `×1.15` | set `448/hour` (`2.80×`) |

The independent body-scale branch is:

| Node | MP / change complexity | Mature radius | Geometric structure | Added quota | Area-scaled liability |
| --- | ---: | ---: | ---: | --- | --- |
| `IncreasedCellScaleI` | `160 / 3` | `1.50 R₀` | `3,375` | `Mg 5 + Zn 5` | `2.25×` base maintenance, reproduction work, and active-movement distance cost |
| `IncreasedCellScaleII` | `260 / 4` | `2.25 R₀` | `11,391` | additional `Mg 10 + Zn 5` | `5.0625×` the same costs |

Scale multipliers use current radius for baseline maintenance and movement, but compiled mature radius for reproductive work. They do not multiply named pathway or machinery upkeep. Exact composition, rounding, and predator-demand landmarks are defined in [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md).

`ProtoEukaryoticOrganization` exceeds the founder change-complexity limit of `3`, so acquiring it also requires an already compiled capacity of at least `4`; the existing `ExpandedChangeCapacityI` supplies `5`. This is an ordinary proposal limit, not an extra biological prerequisite.

The mature-structure multipliers are organization density and machinery, not radius multipliers. `CellScale` alone controls mature radius and `compiledGeometricStructureTarget`; total `compiledMatureStructure` additionally includes organization overhead. The one conserved structural resource has explicit geometric and organization assignments so later scale mutations cannot reinterpret existing organelle matter as newly constructed body volume. A compartmentalized organism can therefore require more biomass and reproduction matter without becoming physically larger or receiving a larger contact range. Exact assignment and radius rules are defined in [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md) and [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).

With both organization nodes and no scale increase, a primitive mature target of `1,000` becomes:

```text
ceil(1,000 * 1.25 * 1.75) = 2,188 structure
```

With `IncreasedCellScaleI` as well, the candidate target becomes:

```text
ceil(3,375 * 1.25 * 1.75) = 7,383 structure
```

The canonical fixed-point compiler performs one final ceiling after combining factors in trait-effect order; the arithmetic above is explanatory. Spatial radius remains the `1.50×` Scale-I value rather than growing again from organization density.

## Micronutrient dependencies

The first organization quotas are:

| Node | Added committed constitutive quota | Interpretation |
| --- | --- | --- |
| `CompartmentalizedCell` | `Zn 5` | Membrane trafficking and regulatory-protein abstraction |
| `ProtoEukaryoticOrganization` | additional `Zn 5 + Mg 5` | Expanded regulatory, structural, and genome-handling machinery |

These are constitutive quotas because the organization as a whole depends on them. They participate in generic nutrient health, activation, death-remnant transfer, and reproduction provisioning. The increased structural target already raises CHNOPS demand through `StructuralBiomass`; no separate “phosphorus quota” should duplicate that matter.

The complete added complex-organization set is `Zn 10 + Mg 5 = 15`. A hydrogen-derived aerobic lineage requires `97` free micronutrient units for one additional complete inherited set; a sulfur-derived aerobic lineage requires `95`. Both fit in the respiration-required `MicronutrientRetention` capacity of `128`, but neither is guaranteed access to zinc. The volcanic founder tile's very scarce zinc therefore makes sustained local complex-cell reproduction difficult without migration, import, scavenging, or a richer source.

Mutation never grants the added quota. Existing free micronutrients promote at the ordinary phase-5 barrier; newly acquired material waits until the next tick.

## Energy and ecological break-even

The organization multipliers apply to the 50-energy founder baseline before additive pathway costs. An illustrative aerobic proto-eukaryotic cell therefore pays approximately:

```text
round(50 * 1.10 * 1.25) = 69 base maintenance/hour
+ 5 InternalMembraneScaffolding
+ ordinary tolerance, regulation, carrier, storage, reaction,
  sensing, movement, ingestion, or predation costs
```

The exact compiler rounding rule remains owned by configuration. The example demonstrates that organization costs are only one portion of the full loadout: the prerequisite aerobic stack and any chosen ecological machinery make the true break-even substantially higher.

The benefit is likewise conditional. A simple specialist should remain superior when one reaction, one substrate, or one low-throughput acquisition path is the binding constraint. A complex lineage should pull ahead only when multiple useful internal processes compete for primitive capacity or when specialized unlocked capabilities exploit rich biological resources.

# Acquisition and organizational maturation

Speciation changes DNA but grants no matter, stored energy, or finished organelles. An organism selected into a descendant with a higher organization target enters `OrganizationMaturation` when its current structure or committed constitutive quotas do not meet the new phenotype:

```text
OrganizationMaturation
    preserve concrete structure, reserve, stores, age, and position
    preserve the prior viable-structure hard floor
    use the new organization target for structureFactor
    prohibit reproduction
    retain previously active capabilities
    keep new organization-dependent capabilities inactive
    grow structure and promote quotas through ordinary transactions
```

On reaching the new mature structure target and complete new constitutive quota, the organism enters its ordinary mature lifecycle and atomically activates the new organization-dependent effects at the next phase-5 barrier. No same-tick activation and use is allowed.

The new baseline maintenance multiplier should scale with achieved organization during maturation rather than charging the full completed machinery cost immediately. The `InternalMembraneScaffolding` additive upkeep begins immediately because it represents the construction and control system required for the transition. This interpolation must use fixed-point values and be a function of authoritative structure/quota state, not persisted progress.

If scale and organization increase in the same speciation event, one combined maturation target and the stricter inherited hard-floor rule apply. The organism does not enter nested lifecycle states.

# Respiration, oxygen, and endosymbiosis

The v1 gate is:

```text
ProtoEukaryoticOrganization requires OxygenRespiration
```

This is a gameplay abstraction with three benefits:

1. The high-throughput cell has an energy system capable of paying for its additional machinery.
2. Oxygenic producers create an ecological transition before complex consumers can flourish.
3. The late milestone depends on resource distribution, oxygen tolerance, copper, zinc, and organic fuel rather than mutation points alone.

It does not imply that bacterial respiration requires complex cells; `OxygenRespiration` remains independently available to simpler cells. It also does not imply that all later eukaryote-like cells must remain obligately aerobic.

V1 abstracts the donor encounter and integration event. The player spends mutation points and meets genetic/material prerequisites; the simulation does not require a specific donor species to be co-located, merge two lineages, or transfer a second genome. The event narrative can describe a rare symbiosis becoming permanent across the founding subset. This is consistent with LYFE's existing abstraction of species-wide DNA and avoids introducing a one-off horizontal-transfer subsystem.

Future rule packs may add:

- A literal ecological symbiosis and endosymbiont-acquisition event.
- Hydrogenosome-like or other anaerobic energy organelles derived from the complex lineage.
- Secondary organelle reduction or loss.
- Plastid integration and complex-cell photosynthetic branches.
- Multiple organelle lineages with different metabolic consequences.

# Scale, engulfment, and cell walls

Cell scale remains independent because complexity and size create different tradeoffs. `ProtoEukaryoticOrganization` enables the machinery that can support engulfment and advanced locomotion, while `IncreasedCellScaleI` provides the physical prey-size relationship and particulate capacity. Neither substitutes for the other.

Rigid walls are compatible with complex organization: a complex lineage may remain a defended producer, absorber, or respirer. They are not compatible with v1 engulfment, which requires substantial membrane deformation:

```text
Engulfment activation requires no active RigidCellWall
                         and no active LayeredCellEnvelope
```

Because these structures are constitutive and evolution is irreversible, taking the wall branch before engulfment permanently closes engulfment for that descendant in v1. A future flexible-wall, wall-remodeling, or temporary-wall lifecycle trait may relax this restriction at a meaningful construction and upkeep cost.

# Reproduction and mutation economy

Complex organization does not select a reproduction allocation profile. Primitive fission, asymmetric budding, and future allocation choices remain in the `Reproduction` family and must provision the larger structural target, complete committed quota, minimum reserve, and reproductive work.

The organization multipliers make generation time longer chiefly through material assembly, not through an arbitrary cooldown. No additional proto-eukaryotic cooldown is proposed until population fixtures demonstrate that structure and work costs fail to create the intended delay.

`ProtoEukaryoticOrganization` retains the provisional `1.10×` mutation-income modifier. It represents improved genome organization and cellular machinery at species scale, but it is deliberately much smaller than the modifiers in the dedicated evolutionary-machinery family. Its structural and metabolic liabilities apply continuously; mutation income alone should not justify the trait in a short remaining game.

# Autonomous evolution

Complex organization should not score highly from generic starvation, low health, or elapsed time. Its material-opportunity profile requires evidence that its ceiling can be used:

```text
compartmentOpportunityQ = min(
    trailingInternalBudgetSaturationQ,
    trailingUsefulInternalDemandQ,
    quotaProvisionQ,
    replacementEnergyMarginQ
)

protoEukOpportunityQ = min(
    compartmentOpportunityQ,
    trailingRespirationActivationQ,
    accessibleOxygenOpportunityQ,
    zincAndCompleteQuotaProvisionQ,
    remainingHorizonPaybackQ
)
```

`trailingUsefulInternalDemandQ` counts only processes that had substrate, output capacity, catalysts, and ordinary reaction eligibility but lost extents specifically to the shared internal budget. It must not count hypothetical downstream traits the proposal does not complete.

Engulfment, advanced movement, and storage retain their own opportunity profiles. Dense prey may make a proposal containing the complete organization-plus-engulfment path attractive, but `ProtoEukaryoticOrganization` alone does not receive the prey opportunity as if it could already feed.

# Observability

The server and client should expose:

- Current organization tier and maturation state.
- Current and target organization structure, separately from physical scale/radius.
- Shared internal-processing demand, admitted load, denied load, and top limiting processes.
- Added maintenance, reproduction work, soft-stress multiplier, and constitutive quotas with provenance.
- Which organization-dependent capabilities are unlocked, active, inactive, or still missing prerequisites.
- Whether a complex lineage is energy-, substrate-, processing-, structure-, oxygen-, or micronutrient-limited.
- The tree-of-life transition and a concise endosymbiosis narrative when the proto-eukaryotic lineage is founded.

The mutation preview must not claim that a processing-budget increase raises actual production when recent demand would remain constrained elsewhere.

# Deterministic validation fixtures

Before these nodes become selectable, test at least:

1. **Lean specialist:** a primitive specialist beats an otherwise unnecessary compartmentalized lineage in a stable substrate-poor tile.
2. **Processing saturation:** with several fully supplied internal reactions, compartmentalization completes more useful extents than the primitive cell without changing any reaction yield.
3. **External bottleneck:** raising internal budget provides no phantom benefit when uptake, light, gas, carrier matter, or output capacity is limiting.
4. **Quota gate:** zinc-poor founders can acquire the DNA but cannot activate or reproduce the new organization until they obtain exact matter.
5. **Aerobic gate:** proto-eukaryotic organization is unreachable without the complete respiratory path and remains unable to activate without accessible oxygen and catalysts.
6. **Maturation:** speciation grants no structure or quota; transition health, old hard floor, blocked reproduction, cost interpolation, and next-tick activation replay exactly.
7. **Scale independence:** organization changes structural density and processing but not radius; Scale I changes radius and encounter geometry independently.
8. **Engulfment composition:** only the complete organization, scale, ingestion, digestion, and predation path can engulf a compatible prey.
9. **Wall tradeoff:** a rigid-walled complex cell remains viable but cannot activate engulfment.
10. **Rich-niche payoff:** a fully supplied complex respirer or engulfing predator eventually repays its higher structure, maintenance, and generation-time costs.
11. **Poor-niche failure:** the same lineage loses to a lean lineage in anoxic, zinc-poor, low-organic, or chronically low-throughput conditions.
12. **Conservation:** acquisition, maturation, reproduction, death, ingestion, and decay conserve all organization-associated structure and micronutrients.

# Decisions confirmed

1. **Aerobic energy-organelle gate:** `ProtoEukaryoticOrganization` requires `OxygenRespiration` in v1. Anaerobic complex cells remain a future derived branch.
2. **Abstract endosymbiosis:** energy-organelle integration is part of the proto-eukaryotic mutation milestone and requires no actual co-located donor species or lineage merger.
3. **One milestone, not two:** v1 has no separate selectable endosymbiosis node; the player-facing path remains compact.
4. **Rigid-wall tradeoff:** walls and complex organization can coexist, but a constitutive rigid wall permanently closes the v1 engulfment path for that descendant.
5. **Independent physical scale:** organization can increase structural density and processing capacity without automatically increasing body radius.
6. **Ceiling rather than yield:** organization raises shared internal-processing capacity but never changes reaction stoichiometry or energy yield by itself.
7. **Explicit specialization:** complex organization unlocks opportunities but grants no automatic storage, ingestion, behavior, locomotion, reaction, or environmental-tolerance capability.
8. **Soft complexity sensitivity:** complex organization increases dependencies and soft-stress cost without imposing a blanket reduction to hard environmental limits.

# Scientific grounding

The distinction between pre-eukaryotic compartments and eukaryotic organization is informed by reviews of [bacterial compartmentalization](https://pmc.ncbi.nlm.nih.gov/articles/PMC4318566/) and [bacterial organelles](https://www.nature.com/articles/s41579-020-0413-0). Bacterial microcompartments can concentrate pathways, retain volatile products, and isolate toxic intermediates without constituting a eukaryotic cell.

The last eukaryotic common ancestor possessed mitochondria, while the order of mitochondrial acquisition, endomembrane complexity, and phagocytosis remains actively debated; see [the physiology of phagocytosis in mitochondrial origin](https://pmc.ncbi.nlm.nih.gov/articles/PMC5584316/), [the origin of phagocytosis in Earth history](https://pmc.ncbi.nlm.nih.gov/articles/PMC7333901/), and [eukaryotic mitochondrial acquisition](https://pmc.ncbi.nlm.nih.gov/articles/PMC4292153/). The known eukaryote lacking any mitochondrial organelle is consistent with secondary loss and therefore does not demonstrate a primitively mitochondrion-free eukaryotic ancestry: [Karnkowska et al.](https://pmc.ncbi.nlm.nih.gov/articles/PMC6759080/).

These sources guide the staged topology and qualitative dependencies. LYFE's trait prices, multipliers, quota amounts, activation gates, and exact historical ordering are explicit game abstractions subject to deterministic balance tests.

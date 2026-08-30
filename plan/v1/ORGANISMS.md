# Organism Model and Mechanics

Status: scaffold

Sources: [ORGANISMS vision](../../vision/ORGANISMS.md), [NUTRIENTS vision](../../vision/NUTRIENTS.md), and [SIMULATION vision](../../vision/SIMULATION.md).

# Purpose

Translate organism state, derived health, behavior, movement, metabolism, reproduction, predation, lifecycle, and death into intent-producing algorithms.

# Authoritative organism state

Plan the concrete representation of:

- Stable organism and species IDs.
- Tile and within-tile position.
- Velocity or movement state.
- Age and lifecycle phase.
- Structural biomass, stored nutrients, and energy-bearing reserves.
- Current environmental stresses.
- Current behavior or behavioral goal.
- Any cooldowns or accumulated state required by DNA capabilities.

DNA, capacities, tolerances, available behaviors, and action parameters belong to the species and are referenced rather than copied into every organism.

# Derived health

Stored energy is derived from the organism's energy-bearing resource quantities and their compiled energy densities; it is not an independently mutable resource pool. Health is primarily that derived energy divided by DNA-defined capacity, adjusted by nutrient sufficiency, lifecycle, and environmental stress. The detailed plan must define:

- Output range and clamping.
- Whether health is computed once per tick or per action.
- Effect of increasing maximum capacity during speciation.
- Soft-stress energy costs versus direct health adjustment.
- Which health value reproduction and mutation aggregates observe.

# Behavior and sensing

Define a deterministic behavior-selection interface that can grow with DNA capabilities:

```text
SelectBehavior(organism, speciesDNA, localObservation, randomDraws)
EvaluateBehavior(organism, behavior, localObservation) -> intents
```

V1 tiles have no within-tile nutrient gradients. Sensing may still detect nearby organisms and remains, local tile conditions, and neighboring-tile suitability. Future micronutrient fields must fit without rewriting the behavior interface.

# Movement and space

Specify:

- Within-tile coordinate bounds and units.
- Velocity integration and energy cost.
- Random movement versus directed movement.
- Neighbor/prey/remnant spatial index.
- Interaction radii.
- Edge crossing, including wrapped `x` and bounded `y` behavior.
- Environmental compatibility and migration probability.
- Passive movement if included in v1.

# Acquisition, energy capture, internal metabolism, and storage

An organism may have one or more acquisition and reaction capabilities but cannot directly debit shared pools during independent evaluation. It emits requests that the resource system resolves using proportional allocation and deterministically ranked remainders.

The organism pipeline must preserve five boundaries:

1. `ResourceAcquisition` transfers permitted external matter into `AvailableStore`.
2. `ExternalEnergyCapture` uses environmental substrates and energy opportunities to create `ReserveOrganic` or another declared energy-bearing product.
3. `InternalMetabolism` transforms acquired or stored matter, assembles biomass, mobilizes reserve for maintenance/actions, and routes spent matter or waste.
4. `EnergyStorage` limits eligible reserve resources and capacity but never credits reserve.
5. `NutrientStorage` caps non-reserve matter held in `AvailableStore` but never supplies it.

V1 does not create a transient ATP-like authoritative resource. `ReserveOrganic` is directly debited by `SpendStoredEnergy`; useful work is diagnostic and energy ultimately dissipates. DNA may compile maximum reserve-spend throughput and cost multipliers, but cannot bypass a balanced debit.

```text
EvaluateOrganismMetabolism(organism, compiledDNA, environment):
    emit external resource claims permitted by acquisition effects
    emit external capture claims/reactions, capped by free reserve capacity
    select enabled internal catabolic and anabolic reactions
    reserve mandatory maintenance and stress expenditure
    emit optional action-energy budgets allowed by remaining mobilization
    emit biomass assembly and explicit waste/retention intents
```

The detailed pass must resolve fresh-read boundaries and whether maintenance precedes or follows newly captured reserve within the same tick. It must also define capacity behavior, mobilization throughput, internal reaction selection, nutrient-store limits, and waste timing.

# Reproduction

- Reproduction requires a DNA-defined minimum health and resources.
- Attempt frequency and additional energy cost are DNA-defined.
- True split allocates reserves relatively evenly.
- Budding gives the offspring a smaller share.
- All offspring matter, including its energy-bearing reserves, comes from the parent.
- Abstract sexual reproduction requires no mate or proximity in v1.

Define allocation order, rounding, minimum viable offspring state, parent identity, offspring position, lifecycle phase, and failure behavior when conditions change during resolution.

# Predation and scavenging

Plan target eligibility, range, success probability, damage or whole-organism kill semantics, partial consumption, transfer yields, competing predators, and remnant creation. V1 must decide whether cannibalism is permitted and whether predation always kills before consumption.

# Aging and death

Death triggers include senescence, insufficient energy, hard environmental exposure, and predation. The final tick records every satisfied trigger and contributing stress. Death atomically removes the organism and creates one remnant containing all untransferred resources.

Chemical exposure uses the same soft/hard pattern as temperature and moisture: soft excess raises maintenance cost, while hard excess receives a keyed per-tick death draw whose probability rises with overage. H₂S and SO₂ are evaluated separately so they can produce distinct simultaneous death triggers. DNA tolerance scales thresholds and may impose an ongoing efficiency cost.

# Evaluation pseudocode to develop

```text
ObserveLocalEnvironment(organism)
CalculateRelativeHealth(organism, speciesDNA, environment)
EvaluateMaintenanceAndStress(organism)
ChooseAndEmitActionIntents(organism)
ResolveReproduction(parent, allocationModel)
ResolvePredation(predator, target)
FinalizeDeath(organism, satisfiedTriggers)
```

# Required decisions and tests

- [ ] Concrete organism fields and dense layout.
- [ ] Health formula and lifecycle phases.
- [ ] Behavior-selection mechanism.
- [ ] Spatial index and interaction radii.
- [ ] Movement/migration equations.
- [ ] Acquisition/capture/internal-metabolism intent interfaces and energy-storage enforcement.
- [ ] Reproduction and predation resolution.
- [ ] Senescence and environmental-death curves.
- [ ] Tests for zero-sum reproduction and complete remnant transfer.
- [ ] Tests proving storage capacity changes never credit reserves or nutrients and that newly expanded capacity affects derived reserve fraction as specified.
- [ ] Tests for multi-cause death and deterministic target contention.

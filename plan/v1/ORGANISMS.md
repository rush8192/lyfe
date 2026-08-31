# Organism Model and Mechanics

Status: first tick-integration, predation-resolution, and internal-state pass; detailed behavior and reproduction formulas pending

Sources: [ORGANISMS vision](../../vision/ORGANISMS.md), [NUTRIENTS vision](../../vision/NUTRIENTS.md), and [SIMULATION vision](../../vision/SIMULATION.md).

# Purpose

Translate organism state, derived health, behavior, movement, metabolism, reproduction, predation, lifecycle, and death into intent-producing algorithms.

# Authoritative organism state

The logical schema and field ownership are defined in [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md). In particular, health and current environmental stress are derived condition outputs rather than independently mutable organism fields.

The authoritative state includes:

- Stable organism and species IDs.
- Tile and within-tile position.
- Velocity or movement state.
- Age and lifecycle phase.
- Structural biomass, stored nutrients, and energy-bearing reserves.
- Current behavior or behavioral goal.
- Any cooldowns or accumulated state required by DNA capabilities.

DNA, capacities, tolerances, available behaviors, and action parameters belong to the species and are referenced rather than copied into every organism.

# Derived health

Health is a normalized deterministic assessment of concrete organism state, compiled DNA, lifecycle state, and current environment. It is not an authoritative resource or independently writable hit-point pool.

The first formula, phase-specific snapshots, caching contract, species aggregation, client projection, and remaining calibration questions are defined in [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md).

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

Internal resources may be consumed, temporarily bound by an admitted metabolic process, or protected from lower-priority use by an evolved DNA holdback. These are distinct operations. Default DNA shares free material evenly among same-tier processes, while later regulation traits can add weights, priority tiers, and bounded holdbacks. The complete semantic contract is defined in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).

V1 does not create a transient ATP-like authoritative resource. `ReserveOrganic` is directly debited by `SpendStoredEnergy`; useful work is diagnostic and energy ultimately dissipates. DNA may compile maximum reserve-spend throughput and cost multipliers, but cannot bypass a balanced debit.

```text
EvaluateOrganismMetabolism(organism, compiledDNA, environment):
    begin from committed external grants and capture products
    select and resolve enabled internal energy-producing reactions
    calculate and pay mandatory maintenance, stress, and passive upkeep
    if the mandatory payment fails, append a probability-one
        MaintenanceFailure assessment and die
    otherwise resolve permitted biomass assembly, growth,
        and explicit waste/retention transactions
```

Newly captured reserve and internally produced reserve are available before mandatory maintenance. Movement and targeted external-action costs are admitted and spent in their own phases according to [SIMULATION_LOOP.md](SIMULATION_LOOP.md). Temporary binding, DNA allocation semantics, founder capacity groups and limits, needs-only staging, and founder waste timing are decided. The remaining detailed pass must define reserve-mobilization throughput, advanced capacity increments and retention targets, capacity-reduction overflow, and the bounded multi-resource resolver.

# Reproduction

- Every founder uses the `PrimitiveFission` capability defined in [TRAIT_CATALOGUE.md](TRAIT_CATALOGUE.md), with a near-even allocation. The name avoids projecting a complete modern bacterial divisome onto the first organisms.
- Reproduction requires a DNA-defined minimum health and resources.
- Attempt frequency and additional energy cost are DNA-defined.
- True split allocates reserves relatively evenly.
- Budding gives the offspring a smaller share.
- All offspring matter, including its energy-bearing reserves, comes from the parent.
- Abstract sexual reproduction requires no mate or proximity in v1.

Both allocation modes retain the existing organism as the continuing, aging lineage and create one age-zero offspring. `AsymmetricBudding` changes allocation, offspring maturity, and cost rather than entity identity. V1 selects among discrete authored allocation profiles. A future rules version may expose a broader DNA-defined allocation spectrum, provided every profile remains zero-sum and its parent/offspring consequences are derived consistently.

Define allocation order, rounding, minimum viable offspring state, parent identity, offspring position, lifecycle phase, and failure behavior when conditions change during resolution.

# Predation and scavenging

V1 permits one targeted external interaction per organism per tick: predation or scavenging. A predation intent targets one post-movement organism of another species within range. Same-species predation, persistent wounds, and nonlethal damage are deferred; an admitted attempt either fails or lethally captures the prey.

The kill probability compares the predator's compiled `predationAttackPower` with the prey's compiled `predationDefensePower`, modified by current health, compatible size/movement, active escape, and chemical deterrence. Each actual attack receives a keyed draw and a death-risk assessment. Traits may independently improve attack power or `feedingPriorityWeight`; the latter affects consumption only after a successful kill.

If multiple attempts against one prey succeed, the prey dies once. All attack draws resolve before consumption, so mutually successful predators may kill one another and a predator killed in the same pass cannot receive food. Successful surviving predators submit resource-specific claims capped by ingestion capability and available storage. Remaining prey contents form one cohesive remnant. Contested contents are divided by feeding-priority weight with deterministic largest remainders; failed or killed predators receive nothing. All predators pay their attempt energy cost regardless of outcome.

Ordinary environmental resource claims remain unweighted and proportional. Full formulas and pseudocode are defined in [SIMULATION_LOOP.md](SIMULATION_LOOP.md), while ledger transfer rules remain in [RESOURCE_MODEL.md](RESOURCE_MODEL.md).

# Aging and death

Death risks include senescence, insufficient energy, hard environmental exposure, and predation. Intrinsic causes are evaluated after the environment updates but before movement. When an organism dies, its record contains the exact per-tick probability and contributing inputs for every cause with positive probability that was actually evaluated before death, plus which draw or deterministic condition triggered. Death atomically removes the organism and creates one remnant containing all untransferred resources. An intrinsic-death remnant may be scavenged later in that tick, while a remnant created during predation enters the next tick's scavenging snapshot.

Chemical exposure uses the same soft/hard pattern as temperature and moisture: soft excess raises maintenance cost, while hard excess receives a keyed per-tick death draw whose probability rises with overage. H₂S and SO₂ are evaluated separately so they can produce distinct simultaneous death triggers. DNA tolerance scales thresholds and may impose an ongoing efficiency cost.

Energy acquired through external capture or produced through internal catabolism may pay maintenance in the same tick. Internal energy-producing reactions resolve before mandatory maintenance. If the organism still cannot pay the full obligation, it dies from `MaintenanceFailure` during metabolism; general environmental and senescence death probabilities are not evaluated a second time.

# Organism evaluation contract

The authoritative phase order and full pseudocode live in [SIMULATION_LOOP.md](SIMULATION_LOOP.md). From an organism's perspective:

```text
advance age and evaluate all intrinsic death triggers
if alive:
    integrate movement using prior behavior and velocity
    emit external acquisition, capture, scavenging, and predation intents
    receive deterministically resolved external transactions
    resolve internal energy-producing reactions
    pay maintenance or die from MaintenanceFailure
    resolve optional internal reactions and growth
    evaluate lifecycle transition and reproduction
    observe completed local state and select behavior for next tick
```

Interactions use post-movement coordinates. Newly born organisms begin at age zero and take no further action until the next tick. Behavior updates last and cannot retroactively change the current tick.

# Required decisions and tests

- [ ] Dense physical organism layout; logical fields are defined in [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md).
- [x] First health formula and numeric calibration; see [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md) and [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md).
- [ ] Behavior-selection mechanism.
- [ ] Spatial index and interaction radii.
- [ ] Movement/migration equations.
- [ ] Acquisition/capture/internal-metabolism intent interfaces and energy-storage enforcement.
- [x] Temporary metabolic-resource binding and DNA priority/holdback semantics; see [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- [ ] Detailed reproduction resolution.
- [x] First v1 predation success, defense, contention, and remnant policy.
- [ ] Senescence and environmental-death curves.
- [ ] Tests for zero-sum reproduction and complete remnant transfer.
- [ ] Tests proving storage capacity changes never credit reserves or nutrients and that newly expanded capacity affects derived reserve fraction as specified.
- [ ] Tests proving founder dissolved/micronutrient capacities, composition-derived load, needs-only staging, single-quota targeting, and direct founder waste routing.
- [ ] Tests for multi-cause death and deterministic target contention.

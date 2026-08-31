# Internal Storage, Binding, and Allocation

Status: first semantic, founder-capacity, and primitive-uptake pass; temporary process binding, DNA-defined allocation policy, capacity groups, founder values, and basal micronutrient acquisition are decided, while advanced trait prices and the multi-resource allocation algorithm remain to be calibrated.

Sources: [resource model](RESOURCE_MODEL.md), [organism model](ORGANISMS.md), [simulation loop](SIMULATION_LOOP.md), [trait system](TRAIT_SYSTEM.md), and [trait catalogue](TRAIT_CATALOGUE.md).

# Purpose

Define how an organism shares finite internal resources among competing metabolic processes without duplicating matter or making source-code evaluation order an accidental evolutionary strategy.

This document also owns the remaining capacity, retention, overflow, and storage-transition rules as they are developed. The first pass resolves temporary resource use and process priority.

# Core distinctions

Three different uses of an internal resource must not be collapsed into one operation:

1. **Consumption** permanently debits an input through a balanced reaction and credits products or waste.
2. **Binding**, informally “tapping,” temporarily makes a quantity unavailable to other processes without consuming it.
3. **Holdback** leaves a quantity unbound but protects it from lower-priority processes for a DNA-designated use.

Consumption changes resource balances. Binding and holdback change current availability only. None creates matter, increases storage capacity, or grants a metabolic output.

# Organism storage view

The physical resource accounts remain:

- `Structure`: committed structural matter and committed quotas.
- `AvailableStore`: uncommitted nutrients, metabolic substrates, catalysts, and retained products.
- `EnergyReserve`: energy-bearing reserve compounds.

Temporary binding does not introduce a fourth physical matter account. It adds authoritative encumbrances over resource quantities already present in `AvailableStore`. The schema may later permit a process to bind `EnergyReserve`, but the initial rule pack forbids it: reserve mobilization and spending already provide the v1 energy-access constraint, and a separate fixture is required before health can remain high while all usable energy is encumbered.

```text
freeQuantity(resource) =
    storedQuantity(resource)
    - sum(activeBindings(resource).quantity)
```

Every organism must maintain:

```text
0 <= totalBound(resource) <= storedQuantity(resource)
```

Bound material still occupies storage capacity and transfers with the organism. A holdback is policy evaluated against free quantity; it does not itself count as bound.

# Available-store capacity groups

`AvailableStore` uses a small number of separately limited capacity groups. This prevents a large quantity of one material class from consuming space intended for a mechanically distinct class without requiring one capacity field for every resource.

```text
StorageCapacityGroupDefinition
    group_id
    eligible_resource_tags
    excluded_resource_ids
    baseline_capacity_load
    optional_per_resource_load_cap
```

Every resource admitted to `AvailableStore` must resolve to exactly one active capacity group. Rule compilation rejects a resource that resolves to zero or multiple groups. Species DNA compiles the group's baseline plus trait modifiers into the effective capacity used by each member. A resource may still be used directly by an external-capture reaction without entering `AvailableStore`.

The first groups are:

| Group | Eligible contents | Founder capacity |
| --- | --- | ---: |
| `DissolvedMacronutrientStore` | Internalized organic/inorganic CHNOPS, NH₃, H₂S, and other small dissolved compounds permitted by acquisition DNA | `512` matter-load units |
| `FreeMicronutrientStore` | Uncommitted quantities of the fourteen v1 micronutrients | `64` matter-load units |
| `IngestedMatterBuffer` | Undigested prey/remnant biomass and other particulate biological compounds | `0`; unlocked by later ingestion capabilities |

`EnergyReserve` remains a separate compartment with its existing founder capacity of `10,000` energy units. `Structure` uses compiled viable and mature targets rather than an `AvailableStore` capacity group.

The initial groups are intentionally broad. Dissolved organic feedstock shares the macronutrient group with inorganic and simple named compounds after a capability makes it acquirable. Particulate material remains separate because it requires ingestion and digestion and may be orders of magnitude larger per resource quantum.

## Matter-load units

Shared capacity cannot sum raw resource quantities: one resource quantum may represent one element while another represents a multi-element compound. Available-store groups therefore use expanded tracked matter as their common load unit.

```text
storageLoadPerQuantum(resource) =
    sum(resource.CHNOPSComposition)
    + sum(resource.micronutrientComposition)

storageLoad(resource, quantity) =
    storageLoadPerQuantum(resource) * quantity

usedGroupLoad(group) =
    sum(storageLoad(resource, storedQuantity(resource))
        for resources assigned to group)
```

Examples:

| Resource | Load per quantum |
| --- | ---: |
| Inorganic phosphorus | `1` |
| H₂S | `3` |
| NH₃ | `4` |
| One micronutrient | `1` |
| `ReserveOrganic` | Not counted here; it uses `EnergyReserve` capacity |
| `StructuralBiomass` | `333` when held as undigested matter; committed structure is outside `AvailableStore` |

All products use checked wide arithmetic when calculating load. A zero-load matter resource, negative load, overflow, or capacity below current group load is invalid unless an explicit overflow transaction is being resolved.

## Founder capacity derivation

The maximum current-tick biomass-assembly inputs are:

| Founder | NH₃ load | Phosphorus load | H₂S load | Total staging load |
| --- | ---: | ---: | ---: | ---: |
| Hydrogen | `60 × 4 = 240` | `6` | `3 × 3 = 9` | `255` |
| Sulfur | `80 × 4 = 320` | `8` | `4 × 3 = 12` | `340` |

A `512`-load primitive macronutrient store admits either maximum one-tick bundle with headroom for small taps or intermediates. If filled exclusively with those bundles, it represents approximately `2.01` hydrogen ticks or `1.51` sulfur ticks. It is staging space, not a free multi-day stockpile.

The complete additional micronutrient set needed to reproduce is `57` units for the hydrogen founder and `55` for the sulfur founder. A shared capacity of `64` holds either set with modest headroom while keeping primitive micronutrient stockpiling limited.

Any new founding tap requirement must fit concurrently with the process's consumed-input staging. If it does not fit within `512`, the rule pack must reduce the tap, increase primitive capacity with a declared cost, or reduce simultaneous throughput rather than silently exceeding capacity.

## Initial contents and desired inventory

Capacity is not a command to fill storage. Founders begin with:

| Group | Initial free content | Basal desired inventory policy |
| --- | ---: | --- |
| `DissolvedMacronutrientStore` | `0` | Request current admitted process needs only; no discretionary buffer filling |
| `FreeMicronutrientStore` | `0` | Accumulate at most one additional complete set of the species' currently inherited reproduction quotas |
| `IngestedMatterBuffer` | `0` | No eligible acquisition |

Current-tick acquisition may enter and leave the macronutrient store in the same tick, so zero initial content does not prevent the founder fixtures from operating. Actual micronutrient accumulation remains subject to acquisition eligibility, uptake throughput, tile availability, and contention.

Desired inventory is a DNA/acquisition-policy value distinct from maximum capacity and from internal process holdbacks. `MacronutrientRetention` may later add a persistent fill target and capacity; `SelectiveMicronutrientStockpiling` may add resource-specific targets or protected admission. Neither trait creates the requested matter.

A reproductively viable compiled DNA loadout must be able to hold at least one complete additional inherited micronutrient quota set in its free-micronutrient capacity. If a new trait increases that quota beyond current capacity, the mutation requires a compatible storage upgrade in the same speciation event or is rejected by the trait compiler. This is a cross-family prerequisite with a real cost, not an automatic capacity increase.

## Primitive micronutrient uptake

`PassiveSmallMoleculeUptake` gives each organism one keyed uptake opportunity per one-hour tick with probability `0.5`. A successful opportunity may request at most one micronutrient quantum, so abundant uncontested conditions yield an expected shared throughput of `0.5` micronutrient load per organism-hour. This is one shared limit across all micronutrients, not `0.5` for every resource.

The rule-pack parameter is the hourly expected throughput, not an unscaled per-tick constant. The v1 one-hour tick compiles it to the single `0.5` opportunity below. If the simulation step changes, compile `expectedOpportunities = 0.5 * tickDurationHours`, take the whole-number opportunities, and add one keyed opportunity with probability equal to the fractional remainder. Each opportunity remains capped at one quantum. This preserves the mean rate, although a changed step still requires fixture reruns because batching can alter contention and phase timing.

```text
BuildPrimitiveMicronutrientClaim(organism, tileSnapshot, tick):
    target = one additional complete inherited reproduction-quota set
    eligible = target micronutrients whose free quantity is below target,
        whose tile balance is positive, and for which one unit fits in storage

    if eligible is empty:
        return no claim

    resource = eligible resource with greatest
        (targetQuantity - freeQuantity) / targetQuantity
    break equal deficit ratios by stable keyed rank

    if not KeyedBernoulli(
        p = 0.5,
        key = (worldSeed, tick, organismId, PrimitiveMicronutrientUptake)):
        return no claim

    emit ordinary ResourceClaim(resource, requestedAmount = 1)
```

The normalized-deficit rule causes large and small inherited quotas to approach completion together. It is baseline homeostatic recognition of required nutrients, not the broader control supplied by later selective-storage traits. The claim receives no priority: ordinary proportional contention may reduce it to zero, missing nutrients remain missing, and there is no catch-up credit for an unsuccessful opportunity or denied claim.

Passive uptake has no separate per-attempt energy debit in the founder rule pack; its low background cost is included in base maintenance. `ActiveTransport` and later high-affinity or selective uptake may add direct energy costs in exchange for better access, higher throughput, or more control.

At the calibrated rate, the hydrogen founder requires `57 / 0.5 = 114` expected ticks and the sulfur founder `55 / 0.5 = 110` expected ticks to acquire a complete extra set under abundant uncontested conditions. These remain well inside their structural first-reproduction times of `334` and `250` ticks. With independent keyed opportunities and guaranteed grants, the probabilities of still lacking the set at those deadlines are approximately `8.1 × 10^-37` and `2.1 × 10^-20`; practical delays should therefore arise from ecological scarcity or contention rather than opening-fixture randomness.

### Recalibration triggers

The `0.5` value is a rule-pack balance parameter and must be rerun when any of these change materially:

- Simulation tick duration. Compilation must preserve `0.5` expected quanta per organism-hour, but different batching can still change contention, variance, and phase timing.
- Founder or advanced-trait quota totals, free-micronutrient capacity, reproduction provisioning rules, or first-reproduction timing.
- Starting micronutrient stocks, ongoing sources, tile exchange, population density, or ordinary contention behavior.
- The representation of passive selectivity, harmful or unwanted uptake, active transport, or high-affinity uptake.
- Any separate energy cost moved out of base maintenance, or membrane traits that change passive permeability.
- Future within-tile micronutrient gradients, organism scale, interaction area, or localized depletion.
- A different acceptable probability that random uptake alone delays otherwise viable reproduction.

## Admission and full-store behavior

- External claims are capped by free load in the destination group after accounting for already committed same-phase grants.
- A multi-input process is admitted only when all consumed inputs and taps can coexist within their assigned groups.
- Binding material already present does not add load, but bound material remains part of used load.
- No baseline process automatically evicts one stored resource to admit another.
- If a reaction would produce a retained output that cannot fit, its extent is reduced or the output must name an explicit direct waste destination.
- Direct external-capture reactions such as the founding H₂/CO₂ and H₂S/CO₂ pathways may consume tile substrates atomically and credit `EnergyReserve` without staging those gases in `AvailableStore`.

# Founder waste-routing rule

Founders have no persistent waste-storage group. At the end of internal metabolism:

- Matter from `ReserveOrganic` spent on maintenance becomes generic organic C/H/O and is credited directly to the tile's organic pools.
- `OrganicOxygen` left over from biomass assembly is credited directly to the tile organic pool.
- Water continues to use the ocean-water boundary.
- Released products are not available for reacquisition until the next tick because external acquisition has already resolved.

At the current founder rates, retaining these products would add `266` matter-load units per hydrogen organism-tick and `280` per sulfur organism-tick. A `512` store would fill in approximately `1.92` or `1.83` ticks respectively, preventing normal metabolism for reasons unrelated to the intended opening balance.

`WasteRouting` and `CatalyticRecycling` may later retain eligible products in an appropriate store, but retained material consumes ordinary capacity and requires an explicit useful reaction. The initial rule pack has zero passive internal leakage or decay; release occurs only through a declared waste transaction.

# Consumed inputs versus tap requirements

A metabolic process definition separates its resource requirements:

```text
MetabolicProcessDefinition
    process_id
    consumed_inputs[resource_or_tag]
    tap_requirements[resource_or_tag]
        quantity_per_extent
        binding_duration_ticks
        binding_trigger
    outputs[resource]
    energy_terms
    maximum_extent
    process_class
```

- `consumed_inputs` use the ordinary resource ledger.
- `tap_requirements` must be free before an attempt can be admitted.
- An admitted tap is unavailable to every other process until its release tick.
- The tapped matter returns unchanged at release unless the process declares a separate balanced loss, conversion, or maintenance reaction.
- Static activation quotas remain distinct. A capability may require a committed catalyst quota to exist and may additionally tap a free macronutrient while it operates.

The binding schema supports any internal resource so a later process can tap a micronutrient or reserve carrier if biologically and mechanically appropriate. Initial use should focus on macronutrients and should not convert every existing catalytic activation quota into a tap automatically.

## Binding trigger

The v1 default is `OnAdmittedAttempt`:

- The resources become bound once the engine admits a process attempt with all required inputs and output capacity.
- A later probabilistic failure does not immediately release them; attempting the process occupied the relevant machinery or carrier.
- An explicitly passive check that never begins a process creates no binding.

Configuration may later support `OnSuccess`, but the initial rule pack should use one trigger consistently unless a fixture demonstrates a clear need.

# Binding duration and cohorts

Duration is a positive whole number of simulation ticks after DNA modifiers are applied.

```text
releaseTick = currentTick + bindingDurationTicks
```

A duration of `1` means the resource cannot be reused by another process during the current tick and becomes free during start-of-tick preparation for the next tick.

Repeated attempts can create overlapping cohorts:

```text
ProcessBindingCohort
    process_id
    resource_id
    quantity
    release_tick
```

Cohorts with the same process, resource, and release tick are aggregated. A compiled process definition must place a finite bound on duration and cohort count so one organism cannot accumulate an unbounded list. A small sorted vector or fixed circular release bucket is the expected dense implementation; the logical behavior does not depend on that layout.

During start-of-tick preparation, after accepted commands are applied and before organism actions:

```text
ReleaseExpiredBindings(organism, currentTick):
    for cohort in stable (releaseTick, processId, resourceId) order:
        if cohort.releaseTick <= currentTick:
            remove cohort encumbrance
```

Release is not a matter-ledger transaction because the resource never left the organism account. Binding events may still be aggregated for diagnostics.

# Default shared allocation

Founding DNA has:

- One shared priority tier for discretionary metabolic processes.
- Equal weight for every eligible process.
- No hard holdbacks.
- No ability to change priorities dynamically within an organism's lifetime.

When multiple eligible processes in the same allocation barrier request the same free resource, none wins merely because its code or ID was evaluated first. The resolver allocates proportionally to process demand within the tier and uses deterministic largest remainders. Indivisible ties use a key derived from world seed, tick, organism, resource, and process ID so a low process ID does not receive every recurring remainder.

Processes that require multiple resources request an atomic extent bundle. The final resolver must iteratively return unusable partial grants and reduce each process to whole feasible extents. It may not bind one required input when another required input makes the admitted extent zero.

The exact bounded multi-resource algorithm remains to be written, but it must satisfy:

- Symmetric processes with symmetric demand receive symmetric expected allocations.
- Stable IDs affect only deterministic indivisible remainders, not the bulk allocation.
- The sum of consumed, bound, and retained quantities never exceeds free inventory.
- No process receives output capacity it cannot use or route explicitly.

# Non-evolvable survival priority

Mandatory survival costs are not discretionary metabolic competitors.

- Movement and admitted external-action costs are reserved and paid in their existing earlier phases.
- Internal energy-producing processes that may fund current maintenance retain their existing pre-maintenance phase.
- Baseline maintenance, environmental stress cost, and passive trait upkeep are then paid before optional growth and storage work.
- Failure to pay mandatory maintenance remains `MaintenanceFailure`.

DNA allocation policy may choose among eligible energy-producing pathways and may protect resources for one, but it cannot demote already-due maintenance below optional growth, reproduction, or wasteful cycling. This preserves the tick contract and prevents a policy setting from bypassing organism-death rules.

# DNA-defined process allocation policy

An evolved species may compile an immutable policy into its phenotype:

```text
ProcessAllocationPolicy
    process_rules[process_or_process_class]
        priority_tier
        relative_weight

    holdbacks[]
        resource_or_tag
        quantity
        beneficiary_process_or_class
        priority_order
```

The policy belongs to species DNA. A player changes it only through a valid speciation proposal, and an uncontrolled species evolves it through the same mutation system. It is not a per-tick direct player command.

## Relative priority

- Lower-numbered tiers resolve before higher-numbered tiers.
- Processes in the same tier divide contested free quantities by positive relative weight and requested extent.
- An earlier tier may exhaust a resource and intentionally starve a later tier.
- Weights influence contention but do not bypass process throughput, activation, output-capacity, or mass-balance limits.
- Equal weights reproduce the default shared behavior.

## Ordered holdbacks

A holdback protects up to an absolute resource quantity for a beneficiary process or class:

```text
protectedFor(rule) = min(
    rule.quantity,
    freeQuantity remaining after earlier holdbacks
)
```

Holdbacks for the same resource are evaluated in explicit DNA priority order. Lower-priority processes calculate usable free inventory after subtracting protected amounts belonging to higher-priority beneficiaries.

Important consequences are deliberate:

- A beneficiary may later consume or tap its protected quantity.
- If it is ineligible or chooses not to run, the protected quantity can remain unused for that allocation barrier.
- A holdback therefore improves reliability for a favored process at the cost of potentially stranding useful material.
- Holdbacks do not reserve storage capacity for matter that has not yet been acquired.
- Newly acquired material may satisfy a holdback at a later allocation barrier in the same tick if it has entered `AvailableStore` by then.

The rule compiler rejects negative quantities, unknown resources/processes, beneficiaries that can never use the selected resource, duplicate priority positions, and total fixed holdbacks above the compiled capacity of their storage group. A policy can reserve less than its target when current inventory is insufficient.

# Allocation barriers and phase interaction

Metabolic processes occur on both sides of external resource resolution, so the engine uses named allocation barriers rather than pretending all eligibility is known at tick start:

1. **Pre-external barrier:** allocates existing free internal resources needed by external energy-capture attempts or other external intents.
2. **Post-external/internal barrier:** includes committed external grants and capture products, then allocates internal catabolism, biomass assembly, retention, and other internal processes.

DNA holdbacks apply at both barriers. They are the mechanism by which a later internal process can be protected from an earlier process. Default sharing applies among processes visible at the same barrier; phase order remains a real dependency where later inputs do not yet exist.

Bindings created at the pre-external barrier remain visible to the internal barrier. A failed external claim does not create a tap unless the organism's own admitted attempt actually began under the configured binding trigger.

# Worked allocation examples

## Temporary tapping

An organism has 12 free units of one macronutrient. Two admitted processes run:

```text
Process A taps 4 units for 2 ticks
Process B taps 4 units for 1 tick
```

At the end of the current barrier, 8 units are bound and 4 remain free. During the next tick's state preparation, Process B's cohort releases and free quantity becomes 8. One tick later, Process A's cohort releases and all 12 are free again. No resource balance changed.

## Default sharing

Two equal-tier, equal-weight processes each request 8 units from a free balance of 12. Before whole-extent reconciliation, the proportional allocation is 6 units each. Neither process gains a bulk advantage from evaluation order. If their per-extent requirements make part of either grant unusable, the bounded bundle resolver returns that part and redistributes it.

## Ordered holdback

DNA protects 5 of 12 free phosphorus units for biomass assembly. A lower-priority catabolic process can see at most 7 units. If biomass assembly is ineligible, the protected 5 may remain unused for that barrier. This is the opportunity cost of reliable provisioning, not a hidden second store.

# First trait progression

Process allocation is an `InternalMetabolism` responsibility. The first progression is:

| Trait | Allocation capability | Liability |
| --- | --- | --- |
| `BasalInternalMetabolism` | Equal shared tier; no holdbacks | Low control, no extra upkeep |
| `BiomassAssemblyControl` | Select one authored bias between reserve conservation and structural growth | Small passive regulation cost; a poor bias can strand opportunity |
| `MetabolicRegulation` | Assign bounded relative weights and tiers among enabled metabolic process classes | Passive control cost even when little contention exists |
| `ResourceAllocationControl` | Add a bounded ordered set of absolute per-resource holdbacks | Additional regulation machinery, maintenance, and possible quota requirement |

`ResourceAllocationControl` is a new child of `MetabolicRegulation`. Exact mutation price, number of tiers, weight bounds, holdback-slot count, and upkeep remain balance parameters. V1 should prefer small bounded choices over an arbitrary programmable policy language.

`SelectiveMicronutrientStockpiling` remains a `NutrientStorage` trait. It controls acquisition and retention of scarce material; `ResourceAllocationControl` controls which internal process gets first access after material is inside. A lineage may need both for reliable use of a scarce imported nutrient.

# Reproduction, death, and speciation

Bindings are authoritative state and must survive save/load, but they never duplicate their underlying matter.

## Reproduction

When reproduction divides a resource balance:

- The transaction first verifies that the parent's committed quotas plus its free-micronutrient inventory contain two complete inherited quota sets.
- One complete set is atomically promoted from free inventory into the new organism's committed `Structure` quota assignment; the parent's original committed set remains committed. No quota is copied.
- Active binding cohorts are divided by the same compartment allocation rule as their underlying resource.
- Bound quantity assigned to each result remains unavailable until the original release tick.
- Indivisible binding remainders follow the reproduction transaction's deterministic remainder rule.
- The transaction must prove `bound <= stored` for both results before committing.
- A process effect already resolved before reproduction is not repeated in either result.

This permits ordinary reproduction while macronutrients are tapped without releasing them early or forcing all continuously metabolizing organisms to stop before reproducing.

## Death

Death transfers the complete physical resource balance to the remnant exactly once. Binding and holdback metadata are discarded; a remnant does not continue the dead organism's metabolic encumbrances.

## Speciation

Founders retain active binding cohorts and their release ticks when their species reference changes. A descendant DNA that no longer enables the originating process still honors the existing binding until release. The old process cannot create a new cohort after the DNA change.

Allocation-policy changes apply at the next allocation barrier. They do not cancel existing bindings or retroactively redirect a process that already ran.

# Capacity interaction

- Bound resources count fully against storage capacity.
- Holdbacks do not change used or maximum capacity.
- External claims and reaction extents are capped by actual free capacity, not by unbound quantity alone.
- Binding an already stored resource requires no additional capacity.
- A capacity increase creates only empty room.
- A future capacity reduction must route physical overflow explicitly; it cannot resolve the conflict by deleting bindings or matter.

This pass fixes the three founder capacity groups and their baseline values. Remaining capacity work is limited to advanced trait increments, capacity reduction during transitions, and explicit overflow routing; no rule may silently delete stored matter.

# Presentation and explainability

For a selected live organism, the server should be able to explain:

- Total, bound, free, and capacity for an internal resource.
- Which processes currently bind it and when each cohort releases.
- Which DNA holdbacks protect it and for which beneficiaries.
- Which process lost an allocation contest and whether priority, weight, holdback, missing input, throughput, or capacity was limiting.
- The trait source and upkeep cost of the allocation policy.

Aggregate telemetry should record requested, granted, bound, released, stranded-by-holdback, and rejected-for-capacity quantities by species, tile, resource, and process class without retaining every binding event indefinitely.

# Pseudocode contract

```text
ResolveMetabolicAllocation(organism, barrier, processIntents, currentTick):
    derive free inventory from stored balances minus active bindings
    validate process intents against capability, lifecycle, and environment

    protect DNA holdbacks in explicit priority order

    for priorityTier in ascending order:
        eligible = unresolved intents in this tier
        allocate each contested resource by demand * relativeWeight
        reconcile multi-resource bundles to whole feasible extents
        return unusable partial grants and iterate to a fixed bound

    for each admitted process in stable ProcessId order:
        atomically debit consumed inputs
        append tap binding cohorts using admitted extents
        apply the balanced process outcome or attempt outcome
        route outputs subject to capacity

    assert every balance nonnegative
    assert bound quantity does not exceed stored quantity
    return updated organism state and allocation diagnostics
```

Stable application order is for deterministic mutation of already-decided outcomes; it must not replace the fair allocation calculation.

# Invariants and fixtures

The first implementation must prove:

- Tapping a resource never changes its physical balance or elemental totals.
- A bound quantum cannot be tapped or consumed by another process before release.
- A duration-one tap releases at the next tick's first allocation barrier, not later in the same tick.
- Overlapping cohorts release only their own quantities.
- Equal default processes share a scarce resource without persistent bulk bias from process ID.
- A higher tier can intentionally exhaust a resource before a lower tier.
- A holdback protects its amount from lower-priority use and can intentionally remain stranded.
- An ineligible beneficiary cannot consume its holdback through another process.
- A rejected admission or failed atomic transaction cannot leave partial consumed inputs, outputs, or bindings. An admitted attempt with an unsuccessful probabilistic outcome retains its configured `OnAdmittedAttempt` binding.
- Multi-resource processes execute only whole admitted extents.
- With the v1 one-hour step, primitive micronutrient uptake emits no more than one one-quantum claim per organism-tick, only for a positive inherited quota deficit that fits in storage.
- Failed keyed opportunities and denied micronutrient claims grant nothing and create no catch-up credit.
- Normalized-deficit selection completes both founder quota vectors without exceeding the `64`-unit free-micronutrient cap.
- A pinned seed reproduces every primitive uptake opportunity and target-resource tie-break.
- Reproduction partitions bindings without duplication and preserves release ticks.
- Death transfers physical matter once and discards binding policy state.
- Speciation and policy changes do not release active bindings early.
- Save/load and different worker counts reproduce binding cohorts, allocations, and state hashes exactly.
- Aggregate requested, granted, bound, and stranded quantities reconcile with organism-level changes.

# Remaining storage decisions

The next passes should resolve, in order:

1. Reserve-production and reserve-mobilization throughput.
2. Advanced retention targets and the exact useful reactions enabled by waste-retention/recycling traits.
3. Capacity reductions during speciation and lifecycle transitions, including explicit overflow destinations.
4. Exact capacity increments, structure, maintenance, quota, and reproduction costs for storage traits.
5. The bounded multi-resource allocation algorithm and its performance layout.
6. Protocol update thresholds and historical aggregation windows.

Founder capacity groups, baseline capacities, needs-only macronutrient staging, one-extra-set micronutrient targeting, primitive `0.5` expected micronutrient uptake, full-store admission, founder waste release timing, and zero passive leakage are fixed by this pass.

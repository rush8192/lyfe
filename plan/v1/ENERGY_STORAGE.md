# Energy Storage Ladder

Status: first v1 reserve-capacity semantics, structural commissioning, trait values, lifecycle interaction, analytical calibration, and executable dry-season cohort selected; server-exact generated-climate population validation remains

Sources: [internal storage and allocation](INTERNAL_STORAGE_AND_ALLOCATION.md), [organism state and health](ORGANISM_STATE_AND_HEALTH.md), [lifecycle and recycling](LIFECYCLE_AND_RECYCLING.md), [complex-cell calibration](COMPLEX_CELL_CALIBRATION.md), [trait catalogue](TRAIT_CATALOGUE.md), [terrestrial adaptation](TERRESTRIAL_ADAPTATION.md), and the [organism vision](../../vision/ORGANISMS.md).

# Purpose and boundary

Energy storage should let a lineage bridge darkness, scarcity, travel, and dormancy without becoming free energy or a universally beneficial capacity upgrade. V1 retains one energy-bearing chemistry, `ReserveOrganic(CH2O)`, and changes only how much charged or spent carrier matter an organism can physically hold.

The ladder does not improve external capture, reaction yield, mobilization efficiency, energy density, metabolic throughput, or action affordability. Those effects belong to their owning acquisition or internal-metabolism traits. V1 also has no passive internal-reserve leakage. Construction, recurring upkeep, relative-health dilution, reproduction provisioning, and the opportunity cost of filling a larger compartment are the storage liabilities.

`DenseReserveHandling` remains future-only. Adding a lipid-inspired or otherwise denser reserve requires a new conserved resource identity, composition, energy density, synthesis and mobilization reactions, and health/load conversion. It cannot be represented as another scalar capacity multiplier.

# Selected ladder

Each row is the complete compiled result at that tier. Mutation costs are paid per newly acquired node; storage upkeep is cumulative because every acquired layer remains maintained.

The highest acquired node supplies one exact `SetCommissionedCapacity` profile and supersedes only its ancestor's capacity/target profile. Capacity targets therefore do not add together. Passive storage-upkeep effects remain additive and produce the cumulative totals below with complete trait provenance.

| Trait | Total capacity | Total `StorageStructure` target | Total storage upkeep/hour | Node MP / complexity | Additional prerequisite | Intended role |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| `PrimitiveOrganicReserve` | `10,000` | `0` | `0` | Foundation | None | Opening day/night and short-scarcity buffer |
| `ReserveCapacityI` | `25,000` | `60` | `2` | `40 / 1` | Foundation parent | Multi-day scarcity and migration |
| `ReserveCapacityII` | `50,000` | `160` | `5` | `80 / 2` | `ReserveCapacityI` | Longer scarcity and short dormancy |
| `CompartmentalizedReserve` | `150,000` | `560` | `13` | `160 / 3` | Acquired `ReserveCapacityII` and `CompartmentalizedCell`; activation above `50,000` waits for materially active organization | Seasonal dormancy and long resource gaps |
| `DenseReserveHandling` | Future | Future | Future | Future | `CompartmentalizedReserve` | A distinct storage chemistry, if later justified |

The incremental upkeep contributions are `+2`, `+3`, and `+8` energy/hour. `ReserveCapacityII` therefore costs five total and `CompartmentalizedReserve` thirteen total. The first two nodes fit the established incremental/new-capability price bands. The final node is a major capability and additionally requires the separately priced `CompartmentalizedCell = 160 MP / complexity 3`; reaching it from a founder costs `440 MP` and multiple speciation events before any optional dormancy or land traits.

No storage node adds a micronutrient quota in v1. Early reserve inclusions do not justify an arbitrary catalyst tax, and `CompartmentalizedReserve` already depends on the `Zn 5` constitutive quota and organization structure of `CompartmentalizedCell`. Its own material liability is the explicit storage-structure target.

# Conserved carrier pool

The organism's one `EnergyCarrierPool` contains:

```text
charged ReserveOrganic
+ zero-energy SpentReserveCarrier
<= currentCommissionedEnergyCapacity
```

Each charged or spent quantum consumes one capacity unit because v1 `ReserveOrganic` has energy density one and the spent carrier is composition-identical. Only charged reserve contributes stored energy, work affordability, reserve fraction, and health. Spending, respiratory recharge, reproduction, death, scavenging, digestion, and decay conserve carrier matter through their already declared routes.

When a reaction can create new `ReserveOrganic` but lacks capacity, it follows that reaction's named conservative overflow route. Unused energy opportunity dissipates. Matter is never discarded, credited to a hidden account, or allowed to exceed capacity. A capacity change itself transfers no carrier matter.

# Structural commissioning

DNA compiles a maximum tier, but mutation does not construct the physical storage that supplies it. V1 adds one aggregate structural assignment:

```text
geometricStructure
organizationStructure
storageStructure

viableStructure = geometricStructure
                + organizationStructure
                + storageStructure
```

`StorageStructure` is assigned `StructuralBiomass`, not a new chemical resource and not per-trait progress. It is one physical role like geometric and organization structure. This preserves the trait-system rule that the organism does not store a progress record for every acquired node.

The storage target is an absolute additive role applied after geometric and non-storage organization targets are composed. Percentage liabilities belonging to walls, terrestrial protection, or cellular organization do not multiply storage capacity or storage structure unless their typed effect explicitly names that role. This prevents an unrelated structural multiplier from silently creating more reserve capacity.

The common commissioning slope is:

```text
CAPACITY_PER_STORAGE_STRUCTURE = 250

structurallyCommissionedCapacity = min(
    compiledMaximumCapacity,
    10,000 + 250 * min(storageStructure,
                       compiledStorageStructureTarget))

currentCommissionedEnergyCapacity =
    min(structurallyCommissionedCapacity, 50,000)
        if CompartmentalizedReserve is acquired
           and CompartmentalizedCell is not materially active
    | structurallyCommissionedCapacity otherwise
```

The targets therefore derive exactly:

```text
10,000 + 250 * 60  = 25,000
10,000 + 250 * 160 = 50,000
10,000 + 250 * 560 = 150,000
```

Capacity above `50,000` remains unavailable until the compartmentalized-cell organization target and committed quota are complete. Already constructed storage matter can wait behind that activation gate; completing the organization may then expose its capacity without creating matter. The health effect and activation provenance remain observable.

On speciation, existing structural assignments and carrier quantities remain unchanged. The descendant acquires a larger compiled target, not completed structure. Ordinary biomass assembly constructs the deficit for `100` reserve energy plus the declared CHNOPS inputs per structural quantum. The cumulative storage-only construction investments are therefore `6,000`, `16,000`, and `56,000` energy before nutrient and processing constraints.

An organism below its new storage target enters `StorageMaturation`, or the existing single combined maturation phase when scale or organization also changes in the same event. It retains its inherited hard viability floor and previously active capacity, cannot reproduce, and grows through ordinary transactions. Capacity increments below the compartmentalization gate commission continuously from completed storage structure; this is the intended benefit during maturation. No nested lifecycle phases or separate per-trait progress records are added.

Growth selection adds storage to the existing normalized-deficit rule:

```text
geometricDeficitQ = normalized geometric target deficit
organizationDeficitQ = normalized organization target deficit
storageDeficitQ = normalized storage target deficit

targetRole = greatest normalized deficit
tie order  = GeometricStructure,
             OrganizationStructure,
             StorageStructure
```

One admitted assembly extent credits one structural quantum to the selected role. Capacity for the next tick derives from the committed result. The current tick's growth-protection floor uses the pre-assembly commissioned capacity, so constructing one storage quantum cannot retroactively invalidate the transaction that built it.

Surplus structural matter above the compiled storage target does not add capacity. Evolution is irreversible in v1, so ordinary speciation never lowers the compiled target. A rule or corrupted save that produces carrier load above commissioned capacity is invalid rather than silently spilling living-organism contents.

# Health, growth, and mutation income

Health retains the established physical fill rule:

```text
reserveFraction = chargedStoredEnergy
                / currentCommissionedEnergyCapacity
```

Spent carriers occupy capacity but contribute no energy. There is no lower hidden target or species-selected desired-fill denominator. Consequently, constructing empty capacity can reduce current health, reproduction eligibility, and mutation income until capture refills it. Increasing capacity is therefore useful only when a lineage has both a surplus with which to fill it and a later deficit to bridge.

At a retained `10,000` charged reserve after full commissioning, reserve health is `1.00`, `0.40`, `0.20`, and approximately `0.0667` across the four tiers. Commissioning is gradual rather than an instantaneous mutation penalty, but construction itself consumes reserve while raising the denominator. This is an intended maturation challenge, not damage.

The ordinary growth-protection floor remains `40%` of **current commissioned capacity**, rounded upward, unless DNA selects another bounded policy. It gates optional biomass assembly only. Mandatory maintenance, lifecycle transitions, and other higher-priority work may spend below it.

Under the favorable wet-surface oxygenic reference, beginning each transition at the prior tier's full reserve and holding non-energy inputs non-limiting gives:

| Transition | Commissioning time | Minimum reserve health during transition |
| --- | ---: | ---: |
| Foundation to Capacity I | `16 h` | `0.384` |
| Capacity I to Capacity II | `25 h` | `0.426` |
| Capacity II to compartmentalized | `185 h` | `0.384` |

The first two use the inherited sulfur-derived four-extent assembly ceiling; the final uses the active compartmentalized six-extent ceiling. Storage upkeep scales with the physically commissioned fraction during these transitions. The low point can fall slightly below `0.40` after a tick because the current transaction protects `40%` of pre-assembly capacity and the committed structure expands next tick's denominator. Growth then pauses until capture restores the new floor. All three transitions complete without hidden energy or overflow in the isolation fixture, while the final roughly eight-day construction interval remains a meaningful vulnerability.

# Reproduction

Reproduction transfers concrete charged reserve, spent carriers, and all three structure assignments. It never copies capacity or fills the result. The capacity owner materializes each result's current capacity once from its inherited DNA, assigned `StorageStructure`, and organization activation before any capacity consumer runs.

Structural requirements follow the reproduction profile:

- `PrimitiveFission` produces two mature results and therefore requires two complete compiled storage-structure targets, just as it requires two complete geometric and organization targets.
- `AsymmetricBudding` retains one complete target in the continuing parent and gives the juvenile at least `60%` of the compiled storage target, matching its `600/1,000` structural maturity. Capacity derives from that partial assignment.
- `ProvisionedOffspring` retains one complete target in the parent and gives the juvenile at least `80%`, matching its `800/1,000` structural maturity.

The profile-specific post-transaction reserve minima remain absolute safety values: primitive fission requires `4,000` in each result; asymmetric budding requires `4,500/3,000`; provisioned offspring requires `4,000/4,000`. They do not become percentages of maximum capacity. The pre-reproduction `0.60`, `0.65`, or `0.70` health gate already scales funding pressure with commissioned capacity, while the percentage store allocation naturally leaves results at lower relative health after reproduction. Making both result minima equal to the `40%` growth floor would overconstrain or make some percentage allocations impossible after reproductive work.

The growth-protection floor applies normally on later ticks. A result below that floor may capture and pay mandatory costs but cannot commit optional structural growth until it recovers above the current floor.

# Lifecycle, dormancy, and mobilization

Storage upkeep is one passive, phase-scalable cost channel. During maturation it scales with the completed storage assignment; the lifecycle multiplier then applies in the same fixed-point product. The calculation rounds upward once so a positive commissioned liability never becomes free:

```text
commissionedStorageFraction = storageStructure
                            / max(1, compiledStorageStructureTarget)

phaseStorageUpkeep = ceil(totalStorageUpkeep
                         * commissionedStorageFraction
                         * lifecycleMaintenanceMultiplier)
```

This yields ordinary-dormancy upkeeps of `0`, `1`, `1`, and `3` per hour and resistant-dormancy upkeeps of `0`, `1`, `1`, and `2`. The physical structure remains present and must be provisioned during reproduction even while upkeep is reduced.

V1 applies no passive reserve leakage and no distinct reserve-mobilization throughput ceiling. Mandatory reserve spending remains zero processing work, and ordinary action/reaction costs provide their own ceilings. Capacity therefore changes volume only. A future mobilization trait or leakage rule requires an explicit fixture demonstrating that the simpler model creates implausible burst spending or consequence-free long storage.

Dormancy admission uses the selected information-bounded moisture policy in [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md). It reads current reserve plus current and trailing tile moisture, never a future condition or calendar phase. The former hard-threshold oracle remains only as a named counterfactual in the population fixture.

# Spatial and physical abstraction

Storage structure is non-geometric. It contributes to viable structure, health targets, construction, remnants, digestion, and reproduction but not body radius, interaction reach, predation size class, or the area-based active-movement multiplier. Current reserve fill likewise does not dynamically swell the organism or change movement cost in v1.

This is an explicit abstraction. Reserve and storage matter remain fully conserved even though their changing internal load is not rendered as changing radius. A future mass/buoyancy or visibly swollen-storage rule may read carrier load, but it must preserve the settled spatial scale and cannot reinterpret reserve as geometric structure.

# Numerical calibration

The reproducible authoring model [energy_storage_calibration.py](calibration/energy_storage_calibration.py) evaluates the selected ladder. It is an expected-value model, not authoritative simulation code. It holds resource access favorable, assumes a full reserve for endurance tests, and omits aging, contention, random reaction outcomes, and population feedback.

## Zero-income endurance

| Tier | Basal aquatic active | Wet oxygenic active | Wet oxygenic ordinary dormancy |
| --- | ---: | ---: | ---: |
| Foundation `10,000` | `8.33 d` | `4.17 d` | `13.89 d` |
| Capacity I `25,000` | `20.03 d` | `10.21 d` | `33.60 d` |
| Capacity II `50,000` | `37.88 d` | `19.84 d` | `67.20 d` |
| Compartmentalized `150,000` | `91.91 d` | `52.97 d` | `183.82 d` |

The aquatic column pays base plus storage upkeep. The wet oxygenic columns include oxygenic machinery, oxygen tolerance, land protection, and—at the final tier—the compartmentalized-cell baseline. These are starvation ceilings from full reserve, not promised survival times.

## Seasonal dormancy

Using the exact smooth `0.15..0.85` moisture isolation curve and selected observable dormancy policy in [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md):

| Tier | Active/dormant upkeep | Maximum reserve draw | Capacity/draw | Bridges from full reserve? |
| --- | ---: | ---: | ---: | --- |
| Foundation | `100 / 30` | `114,272` | `0.088` | No |
| Capacity I | `102 / 31` | `117,938` | `0.212` | No |
| Capacity II | `105 / 31` | `118,198` | `0.423` | No |
| Compartmentalized | `118 / 34` | `130,362` | `1.151` | Yes |

The final `150,000` capacity leaves `19,638`, or approximately `15.1%` of the modeled draw, as margin for discrete outcomes and imperfect timing. The initially proposed `125,000` now covers only `95.9%` after including storage and compartmentalized-cell upkeep, so calibration rejects it more clearly. Generated weather may still justify changing the final value.

The fixed-liability sensitivity is:

| Candidate capacity | Storage structure | Capacity/draw | Reserve margin |
| --- | ---: | ---: | ---: |
| `125,000` | `460` | `0.959` | `-5,362` |
| `150,000` | `560` | `1.151` | `19,638` |
| `175,000` | `660` | `1.342` | `44,638` |

`150,000` is the smallest clean tier with meaningful—not merely rounding-sized—margin. The extra `100` storage structure needed for `175,000` would further raise construction and reproduction costs without being demanded by the isolation fixture.

## Stable-habitat liability

The permanently wet oxygenic control repeats the `8,904/day` surface opportunity and includes each tier's structure, upkeep, and current reproductive work:

| Tier | Reference total structure | Net energy/day | Replacement-capital landmark | Nominal replacement time |
| --- | ---: | ---: | ---: | ---: |
| Foundation | `1,100` | `6,504` | `114,500` | `17.60 d` |
| Capacity I | `1,160` | `6,456` | `120,500` | `18.66 d` |
| Capacity II | `1,260` | `6,384` | `130,500` | `20.44 d` |
| Compartmentalized | `1,935` | `6,072` | `198,050` | `32.62 d` |

For the final row, `1,935 = ceil(1,250 × 1.10) + 560`: the compartmentalized-cell target receives the wet-surface multiplier and storage remains its separate additive role. Replacement capital uses `100` energy per structural quantum, the profile's `4,000` result reserve, and `500` or organization-adjusted `550` reproductive work. Finite capacity, second-result structural accumulation, cooldown, nutrients, and population contention are omitted, so this is a comparative landmark rather than a split prediction.

The ordering is intentional. Every larger tier is worse in a permanently favorable habitat but extends a distinct scarcity interval. The final tier is the only selected one that bridges the synthetic dry season, and doing so requires the separate organization, landfall, oxygenic, and dormancy investments.

## Mutation-price cadence

At 100 effective organisms, the established evolution formula produces `3.2 MP/day` at full health and `2.4 MP/day` at `0.75` average health:

| Node | MP | Full-health saving time | Saving time at `0.75` health |
| --- | ---: | ---: | ---: |
| `ReserveCapacityI` | `40` | `12.50 d` | `16.67 d` |
| `ReserveCapacityII` | `80` | `25.00 d` | `33.33 d` |
| `CompartmentalizedReserve` | `160` | `50.00 d` | `66.67 d` |

The complete founder-to-final storage and organization path costs `440 MP` before dormancy or habitat traits, equivalent to `137.5` full-health days at this fixed population. Population growth shortens that time through the normal logarithmic income curve; health dilution while commissioning can lengthen it. This makes the first tier an accessible response, the second a deliberate investment, and the final seasonal strategy a late-game commitment rather than an automatic upgrade.

# Autonomous evolution and observability

Storage should be favored by evidence that a species can fill and use it, not starvation alone. The first pressure inputs are:

- charged-reserve output denied or overflow-routed because capacity was full;
- subsequent reserve drawdown, maintenance failure, or starvation risk;
- recurring dark, dry, or substrate-poor intervals observed in occupied tiles;
- dormancy episodes that ended from insufficient reserve;
- migration through known resource-poor habitat.

A storage proposal receives little opportunity when a species never reaches current capacity or has no later deficit. Recent overflow without drawdown indicates unused capture but no demonstrated buffer need; drawdown without earlier surplus indicates that a larger empty compartment would not help. Autonomous scoring must use trailing authoritative history and known current state, not hidden future weather. The exact weighting may be calibrated with the general autonomous-evolution fixture.

The live organism view exposes charged reserve, spent carriers, current commissioned capacity, compiled maximum capacity, storage structure/current target, organization gate, reserve fraction, growth-protection floor, storage upkeep with phase scaling, and recent capacity-limited overflow. Species DNA and mutation previews show the next target, empty-capacity health effect at current balances, structure and upkeep deltas, prerequisites, and reference endurance changes. Reduced tile knowledge never reveals hidden future resource or climate inputs through the preview.

# Required validation

1. Acquiring any storage node credits no structure, reserve, spent carrier, nutrient, or energy.
2. The three complete storage targets derive exactly `25,000`, `50,000`, and `150,000` capacity from the common commissioning slope.
3. Capacity increases monotonically with committed storage structure, never exceeds the compiled maximum, and cannot exceed `50,000` for the final node until `CompartmentalizedCell` is materially active.
4. Growth uses pre-assembly capacity for the current floor; the next tick observes the newly commissioned amount.
5. Charged plus spent carrier quantity never exceeds current capacity, and only charged reserve contributes energy and health.
6. At fixed charged reserve, commissioning more capacity never raises health. At fixed capacity, adding charged reserve never lowers it.
7. Storage upkeep scales monotonically with commissioned structure and reproduces exact fully mature active, ordinary-dormant, and resistant-dormant totals with one declared rounding operation.
8. Fission and budding conserve every carrier and structural quantum, honor their storage-assignment minima, and derive each result's capacity independently.
9. Expanded capacity does not alter reaction yield, acquisition throughput, mobilization, energy density, body radius, interaction reach, or movement cost.
10. The authoring fixture reproduces the endurance, seasonal-draw, and stable-habitat tables above.
11. A prior-tier-full wet oxygenic organism commissions the three transitions in `16`, `25`, and `185` hours under the declared isolation assumptions, with no hidden reserve credit and no minimum reserve health below `0.35`.
12. In the first executable population fixture, the final tier has materially slower permanently wet growth than the foundation, while a full final-tier cohort survives the selected dry-season bridge and the smaller tiers do not.
13. A final-tier dormant terrestrial producer can survive at least one representative generated dry season when entering sufficiently charged, but does not receive guaranteed survival under every weather history.
14. Death, scavenging, digestion, and decay reconcile all additional stored carrier and storage structure into existing reservoirs.
15. Save/load, dense reordering, worker count, and client subscriptions do not change commissioning, overflow, upkeep, health, reproduction, or state hashes.

# Recalibration triggers and remaining work

Rerun the analytical and executable fixtures after changing reserve chemistry or density, biomass assembly cost, structure composition, capacity slope or targets, storage upkeep, lifecycle phase multipliers, health normalization, growth floors, reproduction allocation, compartmentalized-cell costs, terrestrial moisture, photosynthetic opportunity, or tick duration.

Remaining work is implementation-facing rather than a missing semantic choice:

- Replace the synthetic seasonal history with representative generated hourly climate histories and rerun the selected behavior policy.
- Extend the first executable fixture from expected oxygenic successes to server-exact keyed binomial capture, complete nutrient provisioning, remnants, spatial migration, and autonomous selection.
- Confirm MP prices against actual mutation cadence and competing ecological proposals.
- Add a future storage chemistry only if the single-resource ladder cannot produce sufficiently different later niches.

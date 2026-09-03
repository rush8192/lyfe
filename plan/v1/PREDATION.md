# Predation and Hunting Behavior

Status: first v1 behavior, eligibility, capture, defense, feeding, movement, and analytical ecosystem-balance proposal; executable coupled population validation remains

Sources: [simulation loop](SIMULATION_LOOP.md), [spatial organisms and behavior](SPATIAL_ORGANISMS_AND_BEHAVIOR.md), [general behavior and resource pressure](BEHAVIOR_AND_RESOURCE_PRESSURE.md), [spatial calibration](SPATIAL_CALIBRATION.md), [lifecycle and recycling](LIFECYCLE_AND_RECYCLING.md), [organism health](ORGANISM_STATE_AND_HEALTH.md), [trait catalogue](TRAIT_CATALOGUE.md), and [evolution](EVOLUTION.md).

# Purpose

Define predation as an evolved organism behavior and mass-balanced biological-resource transfer. Predation should reward access to a dense living food source, create pressure for sensing, movement, defense, and behavioral regulation, and recycle concentrated organic matter without making every consumer universally superior to a primary producer.

V1 deliberately supports two ecological tiers:

1. **Opportunistic contact predation:** a comparatively simple lineage can attack a compatible organism encountered by chance and extract compatible simple contents. This covers abstract bacterial-style attachment, penetration, or lysis without pretending that a primitive cell engulfs another cell.
2. **Directed and engulfing predation:** sensing, hunting behavior, active locomotion, larger scale, compartmentalization, particulate ingestion, and digestion permit reliable pursuit and much greater recovery from prey structure.

# Inherited fixed decisions

These rules are already settled and remain normative:

1. Predation is same-tile and post-movement. Cross-tile attacks and detection are impossible.
2. One organism may perform at most one targeted external interaction per tick: predation or scavenging.
3. Same-species predation is excluded from v1. All members share one DNA and recognition identity, so v1 treats self-protection and clonal recognition as part of the base capture system rather than charging for a trait.
4. V1 has no wounds or persistent combat damage. An admitted attempt either fails or lethally captures the prey.
5. Every admitted attempt pays its action-energy cost even when it fails. Later food cannot fund the earlier attempt.
6. Every attack receives an independent keyed draw and contributes its exact positive death probability to the prey's tick risk record.
7. Multiple successful attacks kill one prey once and create one cohesive remnant. All attack outcomes resolve before any successful predator consumes.
8. A predator killed anywhere in the same predation pass receives no food. Mutual predation can kill both organisms.
9. Feeding transfers concrete prey/remnant resources into compatible organism accounts. It never creates reserve, nutrients, or biomass.
10. Unconsumed contents remain in the remnant and follow ordinary scavenging and decay rules. A kill grants no persistent carcass ownership in the first proposal.
11. Every distinct species is eligible by default when the ordinary mechanical gates pass, including ancestors, descendants, sibling lineages, species controlled by the same player, and species controlled by another player. Player ownership never grants biological immunity.

# Capability tiers and milestone paths

## Opportunistic contact predator

The first useful contact predator requires:

```text
ContactDetection
ContactPredation
└── CaptureMechanism

and at least one useful processing route:
    compatible simple-organic acquisition/catabolism
    or ParticulateOrganicIngestion + ParticulateDigestion
```

`ContactPredation` authorizes target selection and the predation action. `CaptureMechanism` supplies the first lethal attack profile. Neither trait creates food-processing ability. A DNA proposal that can kill but cannot claim or process any target matter is genetically valid, but the evolution preview treats it as an incomplete milestone with no positive biological-opportunity score.

Contact predation does not require active locomotion. Brownian movement can create rare encounters, allowing a specialized ambush/contact lineage to exist before directed hunting. This is intentionally inefficient at low prey density.

The first candidate trait values are:

| Trait | MP / complexity | Passive upkeep | Principal effect |
| --- | ---: | ---: | --- |
| `ContactDetection` | `40 / 1` | `2/hour` | Observe entities inside ordinary contact or an active mechanism's capture reach |
| `ContactPredation` | `80 / 1` | `5/hour` | Authorize one predation intent per eligible tick |
| `CaptureMechanism` | `100 / 2` | `10/hour` | Base attack power `1.0`, capture reach `2 Rm`, attempt cost `50` |
| `ImprovedCaptureSuccessI` | `60 / 1` | `5/hour` | Multiply attack power by `1.5`; add `25` attempt energy |
| `ImprovedCaptureSuccessII` | `120 / 2` | `10/hour` | Multiply attack power by another `1.5`; add another `25` |
| `ContestedFeedingPriorityI` | `50 / 1` | `5/hour` | Feeding-priority weight `2` instead of `1` |
| `ContestedFeedingPriorityII` | `100 / 2` | `10/hour` | Feeding-priority weight `4` instead of `2` |

The three-node base capture milestone costs `220 MP / complexity 4`; it cannot be acquired in one founder-capacity event. Its passive cost is `17/hour` before acquisition, metabolism, movement, digestion, or existing-DNA costs. This makes predation a staged investment rather than an instant food toggle.

## Directed hunter

Directed hunting adds:

```text
NearbyOrganismDetection
└── PreyThreatDiscrimination

ActiveMotility
DirectedForaging
└── HuntingBehavior

ContactPredation + CaptureMechanism
compatible food processing
```

`NearbyOrganismDetection` exposes nearby entities, while `PreyThreatDiscrimination` exposes only coarse mechanically relevant cues: species identity, current radius/size class, movement class, visible capture/defense tags, distance, and whether the target is currently behaving as a threat. It does not reveal exact internal reserve, nutrient stores, health factors, or future intent.

`HuntingBehavior` turns those observations into a persistent prey target and directed movement. Without it, contact predators may attack an eligible organism already in range but cannot pursue one deliberately. Hunting participates in the general controller's `Foraging` priority band; the predation rules below continue to own prey utility and target persistence after that band is selected. The first sensing and behavioral costs are:

| Trait | MP / complexity | Passive upkeep | Principal effect |
| --- | ---: | ---: | --- |
| `NearbyOrganismDetection` | `60 / 1` | `4/hour` | Detect organisms through `8 R₀` |
| `PreyThreatDiscrimination` | `100 / 2` | `6/hour` | Classify compatible prey and visible threats through `16 R₀` |
| `DirectedForaging` | `80 / 1` | `5/hour` | Authorize persistent movement toward a sensed biological or environmental target |
| `HuntingBehavior` | `120 / 2` | `8/hour` | Score prey, retain one target, and pursue it under the hunting policy |

The complete directed-hunting sensing/behavior layer therefore costs `360 MP / complexity 6` across multiple speciation events and `23` energy/hour before locomotion or capture. Each of the four capabilities supplies a distinct prerequisite and its upkeep remains additive while hunting is enabled. Locomotion prices, speed, turning, acceleration, and distance costs remain owned by [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).

## Evolved kin discrimination

`KinDiscrimination` is an optional child of `ContactDetection`:

| Trait | MP / complexity | Passive upkeep | Effect | Tradeoff |
| --- | ---: | ---: | --- | --- |
| `KinDiscrimination` | `80 / 1` | `3/hour` | Exclude recognized close-lineage species from predation targets | Pays recognition cost and forgoes edible related prey |

V1 defines close lineage as an undirected tree-of-life distance of at most two speciation edges, excluding the already-protected same species at distance zero. It therefore covers parent/child, grandparent/grandchild, and sibling species sharing a parent. This lineage distance is a deterministic proxy for inherited recognition-marker similarity, not a claim that organisms understand genealogy.

At contact, the trait hard-excludes recognized close kin from attack eligibility. When paired with `PreyThreatDiscrimination`, it also classifies and removes those species during ranged hunting-target selection; without ranged discrimination, a hunter may waste effort pursuing a close relative but refuses the final attack after contact recognition. The trait affects only its bearer's choices. It supplies no defense against unrelated predators and does not protect an unrecognized or more distant lineage.

## Engulfing predator

The late-game engulfment milestone requires:

```text
ContactPredation + CaptureMechanism
ProtoEukaryoticOrganization
IncreasedCellScaleI
ParticulateOrganicIngestion
ParticulateDigestion
Engulfment                         180 MP / complexity 3
```

`Engulfment` adds `20 energy/hour` passive upkeep and uses a `150`-energy attempt. It remains a contact action: its range is current predator radius plus current prey radius plus the ordinary contact margin, not the longer capture reach. It enables the existing particulate ingestion and structural-food reaction against a successfully killed compatible prey. Its reward is high, but buffer capacity and digestion throughput prevent instant conversion of the whole prey into reserve.

Engulfment is a processing/capture route, not a generic attack-power multiplier. A predator may still need capture-success traits against defended prey. Larger body scale supplies target compatibility and ingestion ceiling while also raising mature structure, maintenance, movement cost, quotas, and reproduction capital.

An active `RigidCellWall` or `LayeredCellEnvelope` prevents engulfment in v1 because the action requires substantial membrane deformation. Walls remain compatible with `ProtoEukaryoticOrganization` and every non-engulfing complex-cell strategy. The first organization path, maturation rules, processing budget, respiratory gate, and wall tradeoff are defined in [COMPLEX_CELLS.md](COMPLEX_CELLS.md).

# Prey eligibility and size compatibility

An organism is an eligible target only when all hard gates pass:

```text
predator and prey are alive in the same post-movement tile
predatorId != preyId
predator.speciesId != prey.speciesId
prey species is not excluded by predator KinDiscrimination
predator lifecycle permits predation
predator has an active capture profile and required quotas
capture profile is not blocked by an active rigid-envelope trait
prey is within that profile's inclusive range
at least one prey resource could enter an authorized destination if killed
predator can pay the complete attempt cost from current reserve
capture profile accepts the current predator/prey size relation
```

The first contact-capture profile compares current **geometric** structure, the physical-volume proxy. Organization-assigned structure contributes to viability, reproduction capital, digestion capacity, and remnant matter, but does not make an organism physically larger or improve size fit:

| Prey geometric structure / predator geometric structure | Result |
| ---: | --- |
| Below `0.25` | Ineligible without `ImprovedTargetCompatibility`; too small for reliable base capture |
| `0.25..0.50` | Eligible; size-fit multiplier `0.50` |
| `0.50..2.00` | Eligible; size-fit multiplier `1.00` |
| `2.00..4.00` | Eligible; size-fit multiplier declines linearly from `1.00` to `0.50` |
| Above `4.00` | Ineligible without a specialized mechanism |

`ImprovedTargetCompatibility` is provisionally `120 MP / complexity 2`, costs `10/hour`, and broadens contact capture to `0.10..6.00` while retaining a `0.50` maximum edge multiplier. It increases breadth, not peak attack power.

Engulfment separately requires:

```text
prey.currentGeometricStructure <= 0.50 * predator.currentGeometricStructure
at least one structural transfer quantum fits in the remaining IngestedMatterBuffer
```

The buffer rule does not require the entire prey to fit. It limits the immediate structural grant; unconsumed structure remains in the remnant. A mature proto-eukaryotic `IncreasedCellScaleI` organism has `3,375` geometric structure and `7,383` total viable structure, so it can engulf a mature primitive organism with `1,000` geometric structure. Two equal-geometric-scale mature peers cannot engulf one another without a later capability, regardless of their organization density.

# Attack and defense calculation

The first probability curve keeps hard incompatibilities outside the probability calculation. Every admitted attack uses fixed-point factors:

```text
predatorConditionQ = 0.50 + 0.50 * predator.interactionHealthQ
preyConditionQ     = 0.50 + 0.50 * prey.interactionHealthQ

attackQ = compiledAttackPowerQ
        * predatorConditionQ
        * sizeFitQ
        * pursuitFactorQ

defenseQ = compiledDefensePowerQ
         * preyConditionQ
         * escapeFactorQ
         * deterrenceFactorQ

basePredationOddsQ = 1.0
minimumDefenseQ = 0.01
oddsQ = basePredationOddsQ
      * attackQ / max(defenseQ, minimumDefenseQ)
rawKillProbabilityQ = oddsQ / (1 + oddsQ)

killProbabilityQ = clamp(rawKillProbabilityQ, 0.02, 0.95)
```

Base compiled attack and defense power are both `1.0`, so equally healthy, equal-size, undefended organisms using the first capture mechanism have a `0.50` chance per admitted attempt. The `2%` floor and `95%` ceiling apply only after hard eligibility; an impossible capture remains impossible rather than receiving the floor.

The first movement factors are deliberately modest because final position already captures most pursuit and escape benefit:

- `pursuitFactorQ = 1.15` when a directed hunter's paid active movement reduced distance to its retained target during the current phase; otherwise `1.0`.
- `escapeFactorQ = 1.50` when `EmergencyEscapeResponse` caused positive paid movement away from this predator and the prey still remained in range; otherwise `1.0`.
- Ordinary Brownian displacement grants neither factor.

These factors must be rerun when active-movement costs and turning limits are frozen. Movement must create value mainly by changing encounter and range outcomes rather than multiplying combat power twice.

# First defense counterweights

The defense branch begins with symmetric but costly counters:

| Trait | MP / complexity | Passive upkeep | Predation effect | Other liability |
| --- | ---: | ---: | --- | --- |
| `StructuralResistance` | `60 / 1` | `5/hour` | Defense power `1.25` | Additional structural quota |
| `CaptureResistance` | `100 / 2` | `10/hour` | Additional `1.5×` defense | Requires reinforced membrane or rigid wall; movement-cost increase |
| `ReinforcedDefense` | `180 / 3` | `15/hour` | Additional `1.5×` defense | More mature structure and reproduction capital |
| `ChemicalDeterrence` | `100 / 2` | `10/hour` | Situational `1.5×` deterrence | Adds `50` attacker attempt cost; self-tolerance/material quota |
| `EmergencyEscapeResponse` | `120 / 2` | `5/hour` | Enables the `1.5×` active-escape factor | Requires threat sensing, regulation, and burst movement cost |

Defense factors multiply in canonical trait-effect order. Configuration validation rejects a required catalogue genome whose combined eligible equal-condition kill probabilities fall outside the intended curve or whose liabilities are missing. Defense does not make an organism healthier and does not reduce unrelated environmental or senescence risks.

# Opportunistic target selection

A contact predator without `HuntingBehavior` does not receive omniscient prey choice. At phase 5 it forms all hard-eligible in-range targets from the stable post-movement view, sorts by `OrganismId`, and selects the minimum keyed rank:

```text
target = argmin KeyedRank(
    PredationTargetChoice, tick, predatorId, candidatePreyId)
```

It attacks only when at least one authorized content claim could be positive and it can pay the complete attempt cost. The predator need not retain enough reserve for later maintenance: a hungry organism may gamble its remaining usable energy on an affordable attack, benefit if it captures processable food, or die from ordinary maintenance failure if the gamble fails.

This random-with-respect-to-hidden-state policy allows chance contact predation without silently granting classification, optimization, or persistent pursuit.

# Hunting behavior and target persistence

At phase 9, a capable hunter evaluates detected, classified, hard-size-compatible organisms of other species. It uses observable estimates rather than authoritative hidden prey internals:

```text
estimatedFoodValue = compatibleSimpleContentProxy(preySizeClass)
                   + digestibleStructureProxy(preyCurrentRadius,
                                              predatorProcessingProfile)

estimatedAttemptValue = estimatedFoodValue
                      * estimatedKillProbabilityFromVisibleTraits

travelDistance = max(0, currentDistance - captureRange)
travelHours = ceil(travelDistance / maximumAffordableHuntingSpeed)

estimatedCost = attackAttemptCost
              + movementEnergyPerDistance * travelDistance
              + huntingUpkeepPerHour * travelHours
              + expectedHandlingCost

utilityQ = foodNeedQ
         * estimatedAttemptValue
         / max(1, estimatedCost)
```

`foodNeedQ` is the greatest of reserve deficit, current structural-growth need, inherited quota deficit, and reproduction-provisioning deficit, each normalized to its own target. A hunter does not seek prey merely because another species exists.

Candidate ordering is canonical. A keyed weighted draw over positive utility supplies bounded exploration without depending on spatial-index order. The current target receives a `1.25×` persistence multiplier; a new target must overcome that hysteresis through its utility rather than causing constant nearest-target oscillation. The target is cleared when it dies, changes tile, leaves sensing range, becomes hard-ineligible, or no longer has a positive estimated return.

Movement during tick `T + 1` aims at the target's completed-tick-`T` position. Predator and prey movement resolve from the same start-of-phase snapshot; neither receives sub-tick pursuit or knowledge of the other's new position. After movement, the current position and tile determine whether an attack is actually in range.

# Threat response and fleeing

`PreyThreatDiscrimination` may classify a detected organism as a threat only when visible DNA/behavior cues indicate a compatible predation mechanism. `EmergencyEscapeResponse` can then select `Fleeing(threatId)` during phase 9.

On the next movement phase, the prey requests the maximum affordable burst displacement directly away from the threat's previous completed position. If multiple detected threats exist, it selects the one with highest visible estimated kill probability, then nearest distance, then keyed stable rank. The action cannot anticipate a new predator that first enters sensing range during the current movement phase.

An already-active fleeing state grants its escape factor only when paid movement increased distance from the attacking predator. Failed, unaffordable, perpendicular, or Brownian movement grants no factor. This keeps behavioral defense observable and prevents an inactive trait from supplying a passive bonus.

# Feeding, handling, and matter flow

After every attack draw resolves, each successful surviving predator may claim only resources supported by its active processing capabilities:

- Compatible `ReserveOrganic`: at most `200` quanta in the first simple-extraction profile.
- Compatible dissolved macronutrients: at most `256` matter-load units.
- Free or committed micronutrients released by death: at most `8` units into free storage; they never become committed automatically.
- `StructuralBiomass`: zero without `ParticulateOrganicIngestion`; otherwise at most `5%` of predator mature structure and available ingested-buffer capacity.

At phase 5, an admitted predation intent reserves the corresponding destination capacity through phase 6. Later passive/environmental acquisition cannot fill that reserved capacity before predation resolves. The reservation contains no matter and grants no claim on the prey. On failure or loss of the predator, it disappears at the phase barrier without backfilling environmental claims; on success, actual grants consume it. This cost of preparing to feed prevents a predator from filling its stores earlier in the phase and then killing prey whose contents it must discard.

Simple transfer caps intentionally match basic scavenging. Predation pays the separate attack cost but does not also pay the 25-energy scavenging-action cost for its immediate post-kill grant. Particulate digestion remains capped at `1%` of mature structure per hour and yields the existing mass-balanced `32 ReserveOrganic` per structural quantum after processing loss.

When multiple successful predators survive, their resource-specific capped claims use `feedingPriorityWeight` values `1`, `2`, or `4` and deterministic largest remainders. Feeding priority never changes attack success. After this one immediate allocation, every remaining account belongs to the ordinary remnant. V1 creates no kill lock, reservation, or ownership timer: consuming more requires a later ordinary scavenging action, competes with every eligible scavenger, and obeys its two-hour handling cooldown.

Full buffers and stores reduce or eliminate the claim before attack admission. A predator does not kill merely to discard food in v1. A future territorial or competitor-killing behavior would require an explicit non-food motivation and separate opportunity/cost model.

# Density dependence and first authoring landmarks

At equal-founder size, base capture range is `3 R0` center distance. For a uniform tile with `100` prey and `10` contact predators:

```text
expected cross-species contact pairs/hour
    = 100 * 10 * pi * (3 / 1024)^2
    = 0.02696

expected attempts/day at full eligibility = 0.647
expected kills/day at base 0.50 chance    = 0.324
expected kills/predator-day               = 0.0324
```

One mature primitive prey contains `1,000` structural quanta. Complete uncontested particulate processing can recover at most `32,000` reserve energy from that structure over `100` primitive-equivalent digestion-organism-hours at the `10/hour` reference ceiling, plus whatever compatible reserve/simple contents remain. A larger predator's compiled ceiling may shorten that elapsed time. The first primitive-equivalent structural bite is capped at `50` quanta and therefore at `1,600` eventual reserve energy before simple contents.

Ignoring reproduction and body replacement, an illustrative simple contact predator paying `75/hour` total maintenance needs approximately `56.25` fully recovered primitive prey kills per 1,000 predator-days, or `0.05625` kills/predator-day. Pure Brownian contact at 100 prey is below that maintenance-only boundary. This remains useful for the opportunistic branch, but it is not the complete engulfing-predator replacement target.

The coupled complex-cell calibration now sets that stronger target. A fully active Scale-I advanced hunter pays `320/hour` passive and up to `108/hour` for continuous maximum movement. Body construction, 30 days of maximum-search costs, reproduction, expected attempts, and post-kill handling require approximately `33.40` fully recovered primitive prey, or `1.113 prey/day`. With `16 R₀` classification, `16 R₀/hour` movement, and the initial `0.75` path-realization landmark, base capture reaches this near `350` uniform primitive prey per tile; `ImprovedCaptureSuccessI` lowers it to about `275`. Exact arithmetic and the Scale-II larger-prey niche are in [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md).

These are authoring landmarks, not acceptance results. They establish the intended direction:

- Contact predation is a marginal dense-population or opportunistic niche.
- Directed hunting materially lowers the prey-density threshold but pays continuous sensing and movement costs.
- Engulfment greatly improves access to structure but remains handling- and digestion-limited.
- Predators decline when prey becomes scarce rather than persisting on an implicit food source.

# Autonomous-evolution opportunity

Predation uses a `BiologicalOpportunityProfile`, not a tile-resource `MaterialOpportunityProfile`:

```text
BiologicalOpportunityProfile:
    eligible_target_predicate
    observation_range_profile
    capture_profile
    processing_profile
    attack_cost
    maintenance_delta
    history_window_hours = 168
```

For each candidate founding tile, score only other-species organisms satisfying the proposed hard size and processing gates. The score combines:

```text
encounterOpportunityQ    # recent in-range contacts plus movement/sense forecast
captureOpportunityQ      # visible attack versus defense and escape estimates
foodOpportunityQ         # compatible observable biomass proxy and processing cap
costCoverageQ            # expected return versus movement/attack/upkeep obligation

localBiologicalOpportunityQ = encounterOpportunityQ
                            * captureOpportunityQ
                            * foodOpportunityQ
                            * costCoverageQ
```

The ordinary founder fractions, one-to-four-tile plans, exploratory floor, intent persistence, and explanation records then apply. Energy-shortage pressure can favor predation only when biological opportunity is nonzero. Recent predation deaths favor defense, threat sensing, or escape—not stronger predation. A kill-only intermediate without a useful processing route receives zero biological opportunity.

Phase 10 retains a sparse per-tile, ordered species-pair ring for the 168-hour scoring window. Each one-hour bucket stores detected eligible encounters, in-range eligible encounters, attempts, successful triggers, prey deaths, immediate matter grants by resource class, and remnant matter left after feeding. Zero pairs are not materialized; extinct or departed pairs age out only after their final bucket leaves the ring. The ring, rolling totals, and cursor persist because future autonomous decisions must replay identically.

The server may use authoritative live state for uncontrolled evolution, but the explanation shown to a player remains filtered through ordinary tile/species knowledge. Controlled-species occupied tiles are live, so a player preview can normally show the exact currently observed organisms while labeling encounter and return projections as estimates.

# Observability

A live predation explanation should expose:

- Predator, prey, distance, capture range, hard eligibility, and size ratio.
- Attack, defense, health, size, pursuit, escape, and deterrence factors.
- Attempt cost, exact kill probability, keyed outcome, and death-risk record entry.
- Immediate resource claims, ingestion/storage caps, contested weights, grants, and remnant remainder.
- Current hunting or fleeing target, target age, observable utility inputs, and target-loss reason.
- Same-species or close-lineage exclusion, lineage distance, recognition range, and the `KinDiscrimination` provenance when applicable.
- Recent species-level attempts, successes, kills, deaths, biomass acquired, energy recovered, processing loss, and prey-density trend.
- Evolution-preview biological opportunity and the principal encounter, capture, food, or cost limiter.

Reduced tiles must not reveal current hidden organisms, attacks, targets, or food-web flows. Previously observed aggregates retain their timestamp and gaps.

# Required validation

- Same-tile inclusive range and size-boundary tests; cross-tile and same-species attacks always reject.
- By default, mechanically eligible distinct species remain targetable regardless of ancestry or controller. `KinDiscrimination` excludes exactly tree distances one and two, does not protect distance-three or unrelated species, and gains ranged filtering only with prey/threat discrimination.
- Equal full-health base profiles produce exactly `0.50` kill probability; attack improvements increase and defenses decrease it monotonically inside the clamps.
- Hard-ineligible pairs never receive the `2%` floor.
- Every admitted attempt pays exactly once; omitted and unaffordable attempts pay nothing.
- Attack results, target selection, mutual kills, and contested grants remain invariant under organism/index/worker order and save/load.
- Multiple successful attacks create one death and remnant; dead predators receive no food and no resource is duplicated.
- Simple and particulate grants never exceed prey contents, predator destinations, or per-action caps; every remainder stays in the remnant.
- Phase-5 destination reservations prevent intervening acquisition from consuming promised feeding capacity, create no matter, and disappear without backfill after failed attacks.
- Feeding priority affects only contested post-kill grants.
- Pursuit and escape multipliers require qualifying paid movement and never arise from Brownian motion alone.
- Opportunistic selection cannot inspect hidden reserve, health, or future intent; hunting uses only its declared observation fields.
- Contact predators fail below the calibrated prey-density/encounter boundary; directed hunters and engulfers gain a niche without erasing primary producers.
- Defended prey outperform otherwise identical undefended prey under sustained predation, while defense liabilities reduce performance without predators.
- A producer-prey-predator fixture conserves all matter, dissipates declared energy, exhibits prey depletion feedback, and retains at least one parameter region for coexistence.

# Major decisions still open

The branch is coherent enough for the next numerical fixture, but these choices remain material:

1. **Post-kill ownership.** This proposal deliberately provides no carcass lock. If simulations make successful predators lose nearly every kill to unrelated scavengers, prefer target persistence or a bounded handling claim before adding invisible ownership.
2. **Miniaturization as defense.** The base size curve makes very small prey harder for ordinary contact capture, but the explicit `MiniaturizedCell` branch remains deferred until predation fixtures show whether staying founder-sized already supplies enough of that niche.
3. **Final population bands.** Prey growth, calibrated movement cost, clustering, defense frequency, carcass competition, and predator maintenance must be simulated together before setting coexistence, oscillation, or extinction acceptance ranges. The analytical starting targets are `275..400` primitive prey for one Scale-I hunter and roughly `106..134` Scale-I prey for one Scale-II hunter, depending on capture investment.
4. **Defense construction costs.** Exact structural/micronutrient quotas and movement penalties for reinforced protection remain to be paired with the defense-family pass. They do not block the unarmored predator fixture.

The following decisions are now closed: distinct species are eligible regardless of lineage or controller unless the predator evolves `KinDiscrimination`; engulfment retains capped immediate particulate transfer and leaves a normal remnant; and `PiercingExtraction` remains a non-selectable late placeholder for v1 rather than a third implemented mechanism.

# Biological anchors

Microbial predation includes attachment, penetration, extracellular attack, and engulfment-like protistan grazing rather than one universal mechanism, supporting separate contact and engulfment tiers: [Pérez et al.](https://doi.org/10.1111/1462-2920.13171). Experimental work on predatory bacteria shows strong prey-density dependence and costly changes between active hunting and low-prey survival states, supporting an explicit density threshold and behavioral conservation rather than free continuous hunting: [Rotem et al.](https://pmc.ncbi.nlm.nih.gov/articles/PMC7852544/). Protistan grazing is size-selective, while observed predator/prey volume ratios span a broad range, supporting mechanism-specific hard compatibility rather than one universal size bonus: [Kinner et al.](https://pmc.ncbi.nlm.nih.gov/articles/PMC106092/). Documented microbial defenses include altered size, walls, motility, and chemical defenses, supporting costly independent defense branches: [Pernthaler](https://www.nature.com/articles/nrmicro1180/).

Predatory microbes also distinguish self, kin, and unsuitable prey. `Bdellovibrio bacteriovorus` does not prey upon itself and uses dedicated self-protection against its own prey-modifying enzymes: [Lerner et al.](https://pmc.ncbi.nlm.nih.gov/articles/PMC4686830/). Predatory myxobacteria use contact-dependent receptors and toxin/immunity systems to distinguish clonemates from nonkin, sometimes even discriminating among members assigned to the same species: [Kaimer et al.](https://pmc.ncbi.nlm.nih.gov/articles/PMC10433427/). These findings support free clonal/same-species exclusion plus an evolved close-lineage discriminator, but not universal immunity for every taxonomic relative.

These sources motivate capability separation, density dependence, size selectivity, and counter-defense. LYFE's prices, powers, costs, ranges, clamps, and population landmarks are game-native balance values.

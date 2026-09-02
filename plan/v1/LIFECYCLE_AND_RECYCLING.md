# Lifecycle and Recycling

Status: first complete v1 lifecycle rule pack and organic-niche population calibration; executable multi-seed validation and later ecological strategies remain pending

Sources: [organism mechanics](ORGANISMS.md), [simulation loop](SIMULATION_LOOP.md), [resource model](RESOURCE_MODEL.md), [organism state and health](ORGANISM_STATE_AND_HEALTH.md), [health calibration](ORGANISM_HEALTH_CALIBRATION.md), [internal storage](INTERNAL_STORAGE_AND_ALLOCATION.md), [spatial contract](SPATIAL_ORGANISMS_AND_BEHAVIOR.md), and [spatial calibration](SPATIAL_CALIBRATION.md).

# Purpose

Define the complete v1 path from organism growth through reproduction, aging, death, remains, digestion, and return of matter to tile reservoirs. The subsystem must make population turnover legible and interesting while preserving the resource ledger: reproduction moves existing matter, death moves every remaining organism account once, and digestion or decay transforms matter through explicit recipes rather than creating or deleting it implicitly.

# Scope and ownership

This plan owns:

- Lifecycle phase transitions and their action gates.
- Growth admission after maintenance, including reserve protection.
- Reproduction readiness, timing, cost, allocation, and newborn state.
- Biological aging and the application of DNA-defined senescence curves.
- Remnant lifetime, composition, shrinkage, and environmental decay.
- Ingestion-buffer semantics, digestion throughput, and digestive waste.
- Return of organic compounds, inorganic compounds, and micronutrients to tile pools.

It does not redefine:

- The tick phase order or death-risk record in [SIMULATION_LOOP.md](SIMULATION_LOOP.md).
- Health as an independently spendable resource; health remains derived.
- Same-tile reach, target detection, offspring geometry, or spatial indexing.
- Shared-resource contention and ledger transfer primitives.
- Concrete mid- and late-game metabolic reactions beyond the interfaces they must satisfy.

# Fixed handoff contracts

The following decisions are already settled and should not be reopened merely to tune lifecycle balance:

1. Old-remnant decay runs in environmental-ledger phase 2.
2. Biological age advances and intrinsic death is evaluated in phase 3.
3. Scavenging and predation resolve in phase 6; acquired matter is available to internal processing in phase 7.
4. Digestion, internal energy production, mandatory maintenance, and growth all resolve in phase 7, in that order. Newly digested products may feed current-tick catabolism and maintenance. Mandatory maintenance precedes optional growth.
5. Lifecycle transitions and reproduction resolve in phase 8 from the post-maintenance state.
6. Newborns and post-growth radii appear in the end-of-tick behavior observation, but cannot act or retroactively change phase-6 eligibility.
7. A continuing parent retains its stable identity and biological age. The offspring receives a new stable identity, biological age zero, and no birth-tick action.
8. Reproduction is zero-sum in matter and requires an additional explicit energy cost. Founders use near-even `PrimitiveFission`; `AsymmetricBudding` is an evolved alternative.
9. Abstract sexual reproduction does not require mate proximity in v1.
10. Death atomically transfers every positive organism account to one cohesive remnant at the organism's terminal position.
11. Passive decay cannot yield usable energy to an organism. A living decomposer must capture and process remnant matter through explicit ingestion and metabolic reactions.

# Lifecycle state

The existing organism state owns:

```text
LifecycleState:
    birth_tick
    biological_age_q
    phase_id
    phase_entered_tick
    reproduction_not_before_tick
    successful_reproduction_count
```

`phase_id` is a compiled lifecycle state, not a free-form behavior label. The first rules must support:

- Active mature direct lifecycle.
- Juvenile maturation after asymmetric budding.
- `ScaleMaturation` after acquisition of a larger cellular organization.
- Ordinary and resistant dormancy when the corresponding traits exist.
- A later motile dispersal phase without changing the core state shape.

Each phase declares its mature or condition structure target, hard viability floor, biological-aging multiplier, maintenance multiplier, allowed actions, growth eligibility, and reproduction eligibility. Behavior may choose among DNA-permitted transitions, but cannot bypass a phase's hard gates.

# Ordered organism lifecycle evaluation

```text
EvaluateLifecycleAndReproduction(organism, compiledDna, tick):
    require organism survived intrinsic death and internal metabolism
    condition = DeriveCondition(postMaintenanceState)

    transition = EvaluateLifecycleTransition(
        currentPhase,
        structure,
        reserves,
        environment,
        priorBehavior)

    if transition is admitted:
        apply transition atomically
        condition = DeriveCondition(stateAfterTransition)

    if currentPhase does not permit reproduction:
        return

    if tick.number < reproduction_not_before_tick:
        return

    readiness = EvaluateReproductionReadiness(
        condition,
        structure,
        reserves,
        storedNutrients,
        compiledReproductionProfile)

    if readiness fails:
        record compact reason for diagnostics
        return

    transaction = BuildZeroSumReproductionTransaction(organism)
    if transaction cannot prove both results viable:
        reject atomically according to the declared failure-cost rule
        return

    commit reproductive work and all parent/offspring transfers
    allocate deterministic offspring id
    place offspring using the spatial rule pack
    schedule the parent and offspring reproduction cooldowns
```

At most one reproduction transaction may commit for one organism in one tick. A lifecycle transition and reproduction may occur in the same phase only when the transition explicitly permits it; maturation should normally make reproduction first eligible on the following tick so a phase transition cannot create an order-sensitive instant division.

Reproduction has no per-tick attempt roll. Once the lifecycle, cooldown, health, structure, quota, reserve, and transaction-validity gates are all satisfied, reproduction commits deterministically. Randomness enters only through a small cooldown jitter sampled once when a cooldown is scheduled:

```text
ScheduleReproductionCooldown(organism, completedTick, compiledProfile):
    ordinal = organism.successful_reproduction_count
    jitter = KeyedUniformInteger(
        worldSeed,
        organism.id,
        ordinal,
        ReproductionCooldownJitter,
        0,
        compiledProfile.cooldownJitterMaxTicks)

    organism.reproduction_not_before_tick =
        completedTick
        + compiledProfile.baseCooldownTicks
        + jitter
```

The primitive first rule uses a `24 h` base cooldown and an inclusive `0..3 h` jitter at the one-hour tick. The base is therefore a hard minimum; the jitter adds at most `12.5%` and has a mean of `1.5 h`. Both values are rule-pack data. A newly created organism receives the same schedule with ordinal zero; after a successful reproduction, the continuing parent increments its count and schedules from the new ordinal. Only committed birth or reproduction schedules a draw. Failed eligibility and rejected transactions neither reroll nor extend the cooldown.

The absolute `reproduction_not_before_tick` is authoritative and persists through save/load and speciation. DNA changes do not retroactively shorten, lengthen, or reroll an already scheduled cooldown; their compiled timing values apply the next time that organism receives a schedule. The UI can therefore explain both a failed biological gate and an exact remaining cooldown. Given the same seed and decisions the jitter remains deterministic on replay.

# Reproduction decisions

## Already fixed

- Primitive fission requires two complete inherited micronutrient quota sets and sufficient geometric and organization structure assignments for two viable results. Founder organization structure is zero, preserving the existing arithmetic.
- The primitive structural target is `1,000` units per mature result, so symmetric fission requires at least `2,000` structure before reproductive overhead.
- The first founder rule uses a `500`-energy reproductive-work cost and targets first reproduction near tick `334` for hydrogen founders and tick `250` for sulfide founders.
- Primitive reproduction is deterministic once all eligibility gates pass. It uses a `24 h` base cooldown plus one keyed `0..3 h` jitter draw per scheduled cooldown, not an independent chance each eligible tick.
- True split divides transferable stores and reserves near evenly after reproductive work. Indivisible remainders use a deterministic keyed rule.
- The parent's committed catalytic quotas remain with the parent; a complete additional set is promoted from free storage into the offspring's committed quotas. Nothing is copied.
- The continuing parent stays at its position. The offspring uses the deterministic same-tile spawn distance in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md); reproduction cannot itself cause migration or fail because of crowding.
- An uncommitted transaction leaves no partial offspring, partial transfers, or duplicated resources.

## Founder eligibility and allocation

Primitive fission commits when all of these conditions hold in the post-maintenance phase-8 state:

```text
phase permits reproduction
currentTick >= reproductionNotBeforeTick
reproductionHealthQ >= 600,000                 # 0.60
structure >= 2,000
one complete additional inherited quota set is free to commit
reserve before reproductive work >= 8,500
the 500-energy reproductive-work reaction is admissible
each projected result receives >= 1,000 structure
each projected result receives >= 4,000 reserve
all other capacity and composition invariants pass
```

The health gate uses the ordinary post-maintenance derived health rather than a reproduction-only opaque score. The named reserve and structure gates remain separate because total health alone could hide a specific shortage behind strong values in other factors.

After the `500`-energy work reaction, `PrimitiveFission` allocates each structure assignment, energy reserve, and divisible available-store balance as close to `50/50` as integer quantities allow. The continuing parent receives the deterministic remainder for structure required to retain its stable identity; other indivisible remainders use the keyed allocation rule. Each result must meet its compiled geometric and organization minima. The parent's existing committed micronutrient set stays with it and the complete additional set becomes the offspring's committed set.

At a full `10,000` reserve, the work reaction leaves `9,500`, or `4,750` per result. The sulfur fixture reaches division near `9,012`; after work it leaves `4,256` per result. Both therefore begin above the common `4,000` growth-protection floor. Although each result has fewer reserves than the pre-split parent, the species gains a second mature resource-gathering organism and one age-zero lineage member without creating matter.

The first `AsymmetricBudding` profile requires at least `1,600` structure and `0.65` reproduction health. Its reproductive-work cost is `650`, its base cooldown is `18 h` with `0..2 h` jitter, and it allocates `1,000` structure to the continuing parent and `600` to an immature offspring. After work it allocates divisible reserve and stores `60/40` to parent/offspring. Reserve before work must be at least `8,150`, leaving at least `4,500` and `3,000` under the limiting allocation; the parent and offspring hard minima remain `4,000` and `3,000`. The offspring may restore reserve immediately but cannot reproduce and cannot commit structural growth until the ordinary growth gate admits it.

`ProvisionedOffspring` replaces that budding profile with a `1,800`-structure gate, `0.70` health gate, `800` work cost, `24 h + 0..2 h` cooldown, `1,000/800` structure allocation, and `55/45` divisible-store allocation. It requires at least `9,700` reserve before work and at least `4,000` in both results. At the founder `10,000` capacity, the transaction leaves `5,060` with the continuing parent and `4,140` with the offspring. This improves juvenile survival while making a birth slower and materially harder to fund. A future rules version may expose a broader allocation spectrum; v1 uses these discrete profiles.

An invalid transaction rejects atomically with no cost and does not change the cooldown. A future explicitly risky reproduction trait may declare an attempt cost, but ordinary fission and budding do not spend resources merely because a gate failed.

The cooldown jitter is intentionally bounded and sampled once. It desynchronizes cooldown-bound organisms without creating the geometric long tail or unexplained repeated failure of a flat per-tick reproduction probability. Material accumulation may remain the binding gate for primitive fission, in which case the expired cooldown adds no artificial delay.

# Growth and reserve protection

Growth is optional work after current-tick maintenance succeeds. It may consume internally available matter and energy only through an explicit biomass-assembly transaction.

The v1 rule distinguishes the terminal threshold from one common founder growth-protection value:

```text
terminalReserveFloor   = 0 reserve
growthProtectionFloor = 4,000 reserve = 40% founder capacity
```

```text
availableGrowthEnergy = max(
    0,
    reserveAfterMaintenance - growthProtectionFloor
)

admittedGrowth = min(
    pathwayThroughput,
    materialLimitedGrowth,
    energyAffordableGrowth(availableGrowthEnergy),
    remainingStructureToCurrentTargetOrDivisionThreshold)
```

The growth resolver reduces the admitted whole assembly extents until projected reserve remains at or above `4,000`; it does not merely switch all growth on or off. No separate resume threshold or persistent `growth_suppressed` flag is used in the first rule. If current reserve is below the floor, growth remains zero until energy capture or catabolism lifts reserve above it. This avoids hidden state and permits immediate partial recovery without spending through the buffer.

This floor reserves energy; it does not create or permanently lock a second energy account. The organism may still spend protected reserves on later mandatory maintenance, emergency lifecycle transitions, or another explicitly higher-priority process. DNA regulation may change the protection fraction within compiled bounds. A later hysteresis trait or rule is justified only if simulations reveal harmful one-tick oscillation that the whole-extent cap does not already absorb.

The `4,000` value preserves roughly 80 hours of founder base maintenance at `50` energy/hour, or about 71 hours under the sulfur fixture's `56`-energy maintenance-plus-stress load. It also matches the already validated sulfur and generated-climate calculations. For other reserve capacities, the default compiled floor is `40%` rounded upward unless a DNA trait explicitly supplies another bounded policy. Acquiring capacity never fills the new capacity, and acquiring a larger body scale never grants the additional structure needed for maturation.

# Biological age and senescence

The primitive first rule is already numerically specified in [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md):

- Senescence begins at `720` biological hours.
- Age condition declines quadratically over the following `720` hours to a floor of `0.5`.
- Hourly death risk begins at `0.01%`, escalates quadratically on a `168`-hour interval, and caps at `50%`.
- The no-other-risk median lifespan is approximately `58` days.
- Active mature and juvenile phases age at `1.0×`; ordinary and resistant dormancy provisionally age at `0.10×` and `0.02×`; `RapidDivisionCycle` uses `1.25×` while active.

Phase 3 advances biological age before evaluating the current tick's senescence risk. The continuing parent keeps accumulated age through reproduction; the offspring begins at zero. Senescence never consumes matter directly and does not replace the separate age contribution to derived health.

The primitive curve is frozen for the first executable rule pack. Remaining work is validation and trait composition: confirm it against multi-generation population fixtures, decide how later lifespan traits modify onset/escalation without invalid curves, and ensure transitions between aging multipliers cannot advance or reverse biological age discontinuously.

## Age-related metabolic throughput

Age also creates a modest productivity cost before death. V1 reduces maximum admitted metabolic extent rather than changing reaction stoichiometry or increasing mandatory maintenance:

```text
ageMetabolicThroughputQ =
    500,000 + floor((ageFactorQ + 1) / 2)

ageLimitedExtent = floor(
    otherwiseAdmissibleExtent * ageMetabolicThroughputQ / RATIO_SCALE
)
```

The multiplier remains `1.0` before senescence because `ageFactor = 1`. It then declines to a floor of `0.75` when the primitive age factor reaches its `0.5` floor:

| Biological age | Age factor | Metabolic-throughput multiplier |
| ---: | ---: | ---: |
| 30 days | `1.0000` | `1.0000` |
| 37 days | `0.9728` | `0.9864` |
| 44 days | `0.8911` | `0.9456` |
| 51 days | `0.7550` | `0.8775` |
| 58 days | `0.5644` | `0.7822` |
| 60+ days | `0.5000` | `0.7500` |

This multiplier caps external energy-capture extents, internal catabolism, digestion, and biomass assembly. It does not change integer reaction recipes, usable energy per completed extent, passive maintenance, storage capacity, or the availability of material already held by the organism. External claims must be built from the age-limited extent so an older organism does not reserve substrate it cannot process.

The result makes a young offspring a full-throughput collector while its aging parent gradually contributes less, strengthening the species-level benefit of reproduction without making an old organism suddenly nonfunctional. The existing age contribution to health and senescence death remain separate, visible effects.

Explicit age-driven maintenance escalation, accumulated molecular damage, damage repair, and asymmetric damage inheritance are future directions. They require additional balance and possibly concrete damage state; v1 must not infer them implicitly from `ageFactor` or charge both lower throughput and higher upkeep for the same curve.

# Remnant state and decay

A cohesive remnant owns concrete contents until those contents are scavenged or decayed:

```text
RemnantState:
    remnant_id
    source_organism_id
    source_species_id
    created_tick
    tile_id
    position
    initial_packing_profile
    initial_body_equivalent_load
    contents[resource_id]
    decay_remainders[decay_rule_id]
```

Bindings, holdbacks, behavior, and cooldowns do not survive death. Resource identities and quantities do. Radius begins from the terminal organism and shrinks with remaining body-equivalent load according to the spatial rule pack.

Only remnants that existed at the start of a tick participate in phase-2 decay. A remnant created later in that tick is first eligible for decay on the next tick. Scavenging in phase 6 consumes the post-decay remainder.

Each decay rule must name its source resource, destination tile resource, rate, environmental modifiers, energy disposition, and fixed-point remainder policy. Reserve and other spent organic compounds decay into generic organic tile pools; inorganic compounds and micronutrients return to their matching pools. Ordinary `StructuralBiomass` is the exception: its exact decay reaction releases a labile dissolved fraction plus a generic organic remainder as defined in [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md). The separate slow passive-mineralization rule below eventually converts generic organic macronutrients into their inorganic forms explicitly and element-for-element.

## First decay cohorts

At the reference condition of `40 °C` in water, v1 uses these exponential-style half-lives and `RatioQ` hourly coefficients:

| Remnant content | Half-life | Hourly decay per million | Decay destination |
| --- | ---: | ---: | --- |
| Dissolved available stores and free micronutrients | `168 h` / 7 days | `4,117` | Matching generic organic/inorganic or micronutrient tile pool |
| `ReserveOrganic`, `SpentReserveCarrier`, and other simple carriers | `336 h` / 14 days | `2,061` | Constituent generic organic pools; any remaining usable energy dissipates |
| Ordinary `StructuralBiomass` and committed quotas | `720 h` / 30 days | `962` | Four `LabileDissolvedOrganic` per released structural quantum plus its exact generic organic remainder; micronutrients release proportionally |
| Future resistant structure fraction | `2,160 h` / 90 days | `321` | Same products, released slowly |

After 14 reference-condition days, approximately `25%` of dissolved contents, `50%` of reserve compounds, and `72%` of ordinary structure remain available for direct scavenging. After 30 days, approximately `5%`, `23%`, and `50%` remain. Remnants therefore persist as valuable local entities for weeks rather than disappearing before a scavenger can encounter them.

Each cohort uses a persisted fractional remainder:

```text
scaledDecay = amount * effectiveHourlyDecayQ + priorRemainderQ
released    = floor(scaledDecay / RATIO_SCALE)
newRemainderQ = scaledDecay % RATIO_SCALE
```

Reference rates receive a temperature/moisture multiplier. Temperature uses a versioned `Q10 = 2` lookup relative to `40 °C`, and the combined multiplier is clamped to `0.25..4.0`. Aquatic tiles use moisture multiplier `1.0`; terrestrial tiles use `0.25 + 0.75 × surfaceMoisture`. `LabileDissolvedOrganic` and `ReducedFermentationProducts` use the same multiplier with respective 14- and 90-day reference half-lives before becoming generic organic pools. V1 does not add oxygen, acidity, pH, or volcanic-gas decay modifiers: those require coherent environmental feedback rather than an isolated penalty.

Ordinary founders have no resistant fraction. A future resistant-body or dormant-structure trait must explicitly assign a bounded fraction to the 90-day cohort and pay its construction cost. Non-founder packing changes radius but do not change decay chemistry unless a trait declares a cohort effect.

# Ingestion and digestion

Predation and scavenging transfer matter; they do not automatically turn arbitrary biomass into usable reserve. V1 distinguishes simple extraction from particulate ingestion:

- `RemnantScavenging` plus compatible organic uptake may transfer already-simple reserve compounds and dissolved stores directly into their matching internal compartments, subject to acquisition throughput, action cost, and capacity.
- Basic scavenging does not mistake `SpentReserveCarrier` for charged reserve. V1 leaves spent carriers in the remnant for passive decay; direct recovery into another organism's carrier pool is a future catalytic-scavenging extension.
- Structural biomass and other particulate contents require `ParticulateOrganicIngestion`, nonzero `IngestedMatterBuffer` capacity, and a compatible digestion process. The acquired material retains resource identity inside that buffer.

## Basic remnant-scavenging action

The first `RemnantScavenging` action targets one same-tile remnant in feeding range and uses this profile:

| Parameter | First value |
| --- | ---: |
| Action-energy cost | `25` reserve-energy units |
| Handling cooldown | `2` simulated hours |
| Compatible `ReserveOrganic` transfer cap | `200` quanta per action |
| Dissolved-macronutrient transfer cap | `256` matter-load units per action |
| Micronutrient transfer cap | `8` units per action |
| Structural-biomass transfer | `0` without particulate ingestion |

All three simple-content caps may participate in one atomic claim bundle, but each is independently limited by target contents, compatible acquisition DNA, destination capacity, and the organism's declared inventory needs. Charged reserve transfers retain their usable chemical energy only when the scavenger can store that same compatible carrier; zero-energy spent carriers are ineligible for this basic transfer. Dissolved matter enters the matching available-store group. Every remnant micronutrient enters free storage; scavenging never commits it directly to structure or a catalytic quota.

The two-hour cooldown preserves the initial average ceiling of `100` reserve quanta, `128` dissolved load, and `4` micronutrients per organism-hour while reducing repetitive claims. At maximum reserve grant, one action nets `175` reserve before ordinary maintenance and supplies 3.5 hours of the founder's `50`-energy base maintenance. A roughly 4,000-reserve remnant can support at most 20 full uncontested claims before decay, other contents, and capacity are considered.

```text
EvaluateSimpleScavengingIntent(organism, target, tick):
    reject unless organism has RemnantScavenging
    reject unless tick >= organism.scavenge_not_before_tick
    reject unless target is a same-tile remnant within feeding range
    reject unless at least one compatible capped transfer is positive
    reject unless organism can reserve the 25-energy action cost

    return atomic simple-content claims capped by profile and free capacity

CommitSimpleScavengingAttempt(organism, grants, tick):
    spend 25 reserve energy through the ordinary balanced work reaction
    apply every granted transfer
    organism.scavenge_not_before_tick =
        first tick boundary at least 2 simulated hours after tick
```

An intent admitted against a valid nonempty phase snapshot pays the cost and starts cooldown even if contention reduces every grant to zero. A pre-admission failure pays nothing and does not start cooldown. The cooldown is organism-local: it does not reserve the remnant, prevent other scavengers from acting, or block unrelated metabolism, movement, or a different targeted-action capability on a later tick. The existing one-targeted-external-action-per-tick rule still prevents scavenging and predation in the same tick.

`scavenge_not_before_tick` is authoritative capability state. It survives save/load and speciation and cannot be reset by losing or reacquiring the trait. A changed DNA profile affects the next cooldown scheduled, not one already in progress. The first rule has no cooldown jitter; spatial contact, contention, decay, and reproduction already provide variability, while a fixed duration remains easy to explain.

```text
DigestIngestedMatter(organism, digestionProfile):
    select eligible buffered inputs by compiled allocation policy
    cap extent by digestion throughput, catalysts, and affordable energy
    consume selected buffered inputs
    produce declared available organic compounds and micronutrients
    route unusable or excess outputs to explicit waste
    dissipate declared energy loss
```

Digestion occurs at the start of internal metabolism, before energy-producing catabolism, maintenance, and optional growth. Its products may therefore feed catabolism and mandatory maintenance in the same tick. This avoids an artificial starvation death when an organism has already captured digestible food, while retaining explicit throughput, cost, and mass-balance limits.

The first particulate scale targets are expressed relative to compiled mature structure:

| Parameter | First v1 value |
| --- | ---: |
| Ingested buffer capacity | Matter load of `25%` of mature structural biomass |
| Maximum particulate ingestion per tick | Matter load of `5%` of mature structure |
| Maximum digestion per hour | `1%` of mature structure |
| Matter recovery target | `100%` accounted: usable outputs plus explicit digestive waste |
| Processing-energy target | No more than `20%` of the ideal gross catabolic yield of recovered products |

At primitive scale these correspond to a 250-structure-equivalent buffer, at most 50 structure-equivalent ingestion per tick, and 10 structure-equivalent digestion per hour. They scale with organization size but remain subject to storage, reaction, and whole-extent limits. The first exact structural-food recipe and gross yield are fixed below; other substrates remain unavailable until their mid/late-game reactions exist rather than using an unbalanced placeholder.

An internal plan may consume digestion products immediately without first fitting the entire output into dissolved storage only when the digestion-plus-downstream reaction bundle is admitted atomically. Any unconsumed output must fit ordinary stores or be routed to declared tile waste. Undigested matter remains stored, occupies capacity, and transfers to the remnant if the consumer dies.

Founder organisms have no particulate-ingestion capacity. `ParticulateOrganicIngestion` provides the buffer and external transfer, while `ParticulateDigestion` provides the first exact structural-food reaction below. General dissolved-organic uptake and fermentation are fixed in [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md), the first respiratory reactions are fixed in [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md), and prey-size compatibility is fixed in [PREDATION.md](PREDATION.md). Additional digestive substrates remain later rule-pack work.

## First structural-food reaction

V1 uses one deliberately abstract combined digestion-and-catabolism reaction for ordinary `StructuralBiomass`. It represents extracellular or compartmental hydrolysis followed by partial catabolic recovery; digestion alone is not asserted to generate useful energy.

```text
ParticulateBiomassDigestionAndCatabolism:
    1 StructuralBiomass(C100 H170 O40 N20 P2 S1)
        -> 32 ReserveOrganic(CH2O)
         + 1 SpentStructuralResidue(C68 H106 O8 N20 P2 S1)
         + 8 useful-energy units dissipated as processing overhead
```

The ledger balances exactly:

```text
outputs =
    32 × CH2O
    + 1 × SpentStructuralResidue(C68 H106 O8 N20 P2 S1)
    = C100 H170 O40 N20 P2 S1
```

For energy accounting, the reaction declares a gross recoverable yield of `40` energy units per structural quantum. Eight units, or `20%`, pay the abstract digestive/catabolic processing overhead and dissipate; the remaining `32` are stored in the 32 `ReserveOrganic` outputs. The coefficient is gameplay balance data, not a biochemical claim about a universal biomass-energy yield. It recovers `32%` of the `100` energy used by the first biomass-assembly fixture, ensuring that recycling is valuable without making repeated construction and consumption an energy-positive loop.

The reaction is admitted only when all outputs can be accounted. `ReserveOrganic` may enter reserve or an atomic same-tick downstream energy-use bundle. `SpentStructuralResidue` has no usable-energy value, is not a valid input to v1 energy-yielding catabolism, and is emitted to the tile as digestive waste. This explicit identity prevents the undifferentiated residue from being treated as fresh fuel and yielding energy twice. It mineralizes directly into matching inorganic elemental pools on the ordinary 90-day tile half-life, with the same temperature/moisture multiplier and fixed-point remainder policy. No output is discarded because a store is full. Micronutrients are not embedded in `StructuralBiomass`; separately ingested micronutrients remain in the buffer or move to free storage under their ordinary rules.

At the reference mature structure of `1,000`, the `1%` hourly digestion ceiling permits ten reaction extents per hour: at most `320` reserve-energy units recovered, `80` dissipated as processing overhead, and ten residue units emitted. Age-related metabolic throughput, catalytic availability, buffer contents, output capacity, and process allocation can reduce this extent. The nominal rate is a primitive-equivalent diagnostic; the capability itself requires advanced compartmentalization.

Particulate organic matter is known to require extracellular hydrolysis before microbial uptake, and hydrolysis can be the rate-limiting step in anaerobic macromolecule degradation. Reported bacterial carbon-assimilation efficiencies also vary substantially by substrate and pathway. These findings support an explicit throughput bottleneck and partial recovery, but they do not determine LYFE's coefficients: see [Kleindienst et al. on anaerobic macromolecule degradation](https://pmc.ncbi.nlm.nih.gov/articles/PMC8027456/) and [Wiegmann et al. on substrate-dependent bacterial assimilation](https://pmc.ncbi.nlm.nih.gov/articles/PMC6881813/).

# First evolved lifecycle modifiers

These are provisional v1 rule-pack values. Mutation-point prices belong to the evolution-economy pass; constitutive upkeep, transaction costs, gates, and phase multipliers are lifecycle-owned and fixed here so trait proposals can be simulated.

## Growth and lifecycle phases

| Trait or phase | First compiled effect | Principal tradeoff |
| --- | --- | --- |
| `RegulatedMaturation` | Select a fixed growth-protection floor of `30%`, `40%`, or `50%` of reserve capacity at speciation | Adds `5` energy/hour upkeep; aggressive growth risks reserve depletion, conservative growth delays maturity |
| `ResourceConditionalGrowth` | Before optional growth, use a `50%` floor below `50%` reserve, `40%` at `50..70%`, and `30%` above `70%` | Adds another `5` energy/hour upkeep and can miss short growth opportunities |
| `DormantPhase` | Entry/exit costs `500/250`; maintenance `0.20×`; biological aging `0.10×`; terrestrial moisture preferred/hard minima `0.20/0.05` | Disables growth, reproduction, active movement, predation, and scavenging while dormant |
| `ResistantDormantPhase` | Requires structure at least `110%` of the active mature target; entry/exit costs `1,000/500`; maintenance `0.10×`; aging `0.02×`; terrestrial moisture preferred/hard minima `0.05/0.01`; on death, `50%` of structure enters the 90-day resistant decay cohort | Extra body construction, larger transition costs, and the same disabled actions |
| `MotileDispersalPhase` | Maintenance `1.25×`; aging `1.0×`; permits its compiled dispersal movement profile | Disables growth and reproduction; exact motion cost remains owned by the spatial rule pack |

`ResourceConditionalGrowth` replaces the selected fixed floor for the current tick rather than multiplying it. Threshold comparisons use the post-maintenance reserve fraction before optional growth and exact inclusive bounds from configuration. Phase entry and exit are atomic work transactions; inability to pay leaves the organism in its current phase. A behavior preparing for resistant dormancy may raise its current growth target to the phase's `110%` entry gate.

The phase maintenance multiplier applies to base maintenance and passive upkeeps explicitly tagged as phase-scalable. It does not discount transition work, environmental-stress costs, or structural liabilities. An upkeep belonging solely to an action disabled by the current phase is suppressible by default; all other upkeep is non-suppressible unless its trait definition says otherwise. This keeps dormancy cheap without silently erasing every cost of complex DNA.

## Reproduction timing specializations

These modifiers compose in stable trait-ID order, but compilation applies typed operations in this semantic order: multiply structural gates, raise minimum health/reserve gates, add work cost and base-cooldown deltas, then apply cooldown multipliers and clamps. Acquiring opposed specializations may cancel a benefit, but never refunds their mutation price or upkeep.

| Trait | First modifier |
| --- | --- |
| `DivisionTimingControl` | `+5` energy/hour upkeep; reduce future cooldown jitter maximum by `1 h`, to a floor of zero |
| `LowerDivisionThreshold` | Multiply total and per-result structure gates by `0.90`, rounding upward; add `0.05` to the health gate and `100` work energy. A sub-mature result is juvenile until it reaches the ordinary mature target |
| `ConservativeDivision` | Multiply structure gates by `1.10`; require health at least `0.75` and result reserve at least `110%` of the profile minimum; add `100` work energy and `6 h` base cooldown |
| `RapidDivisionCycle` | After additive cooldown changes, multiply base cooldown by `0.50`, rounding upward with a `6 h` minimum; add `250` work energy and `10` energy/hour upkeep; active biological aging becomes `1.25×` |

The alternative budding profiles above replace the primitive allocation mode and base profile. Compatible timing specializations then modify that selected base. Configuration validation rejects any composition that produces a result below the universal `500`-structure viability floor, a negative cooldown, an impossible reserve gate, or an invalid senescence curve.

## Scavenging and particulate processing

`RemnantScavenging` uses the basic `25`-energy, two-hour profile. Its child is renamed `HighThroughputRemnantScavenging` for clarity: basic scavenging already supports partial consumption. The child raises each simple-content cap to `1.5×` (`300` reserve, `384` dissolved matter-load, and `12` micronutrients), keeps the two-hour cooldown, and raises action cost to `35` energy.

`ParticulateOrganicIngestion` costs `120 MP / complexity 2`, adds `10` energy/hour constitutive upkeep and a committed `Mg 5 + Ca 5` quota, and replaces the simple action cost with `50` energy when a claim bundle includes structural matter. It unlocks the `25%` buffer and `5%`-of-mature-structure per-tick ingestion cap. `ParticulateDigestion` costs `140 MP / complexity 2`, adds `10` energy/hour constitutive upkeep and a committed `Zn 5 + Fe 5` quota, and unlocks the `1%`-per-hour combined reaction above. Reaction overhead remains extent-dependent and is separate from constitutive upkeep.

The ingestion quota abstracts membrane/cytoskeletal handling and calcium-mediated control; the digestion quota abstracts diverse metal-dependent hydrolytic and catabolic machinery. They are construction/provisioning requirements, not per-extent consumption. Mutation grants neither quota. A scavenger unable to digest more matter may still retain it in the bounded buffer; it cannot bypass capacity by spilling unclaimed input into a hidden account. An engulfing predation attempt's `150`-energy cost includes its immediate structural grant and does not add the separate `50`-energy scavenging action, but every later remnant-scavenging action that includes particulate matter pays `50` and starts the ordinary two-hour handling cooldown.

## Senescence modifiers

V1 has no general longevity/repair branch. Its only evolved age-rate modifiers are the explicit phase multipliers above and `RapidDivisionCycle`; the primitive senescence onset, curve, and `0.75` age-throughput floor otherwise remain unchanged. Longevity, molecular repair, maintenance escalation, resistant-body construction variants, and asymmetric damage inheritance remain future trait directions rather than implicit bonuses.

# Nutrient recycling paths

V1 needs three distinct return paths:

1. **Direct scavenging:** remnant matter moves into an organism's ingested buffer and may later become usable internal matter.
2. **Passive decay:** remnant organic compounds return to tile organic pools; free inorganic compounds and micronutrients return to their corresponding tile pools.
3. **Biological mineralization:** evolved decomposer reactions consume organic compounds and produce named inorganic compounds or gases, potentially extracting usable energy on the way.

Micronutrients remain in their single bioavailable form and therefore return directly to their matching tile pools when released by passive decay or digestive waste. Macronutrients retain the organic/inorganic distinction. No rule may silently convert one form to another merely because a remnant disappears.

Passive mineralization is an explicit slow phase-2 transformation from each tile's `Organic<Element>` pool to its matching `Inorganic<Element>` pool. At the same `40 °C` aquatic reference it uses a `2,160 h` / 90-day half-life and hourly coefficient `321` per million, with the same temperature/moisture multiplier and persisted remainder as remnant decay. The conversion is one elemental quantum to one elemental quantum; it changes biological form but not CHNOPS totals, creates no usable energy, and does not emit a named gas implicitly. `SpentStructuralResidue` uses the same coefficient to debit one proportional fraction of the named residue and credit its exact constituent inorganic pools atomically.

Because remnant breakdown and mineralization are sequential, ordinary structure has a 30-day remnant half-life, releases both a 14-day labile dissolved fraction and a generic remainder, and only gradually returns all unconsumed matter to fixation-demanding inorganic pools. `ReducedFermentationProducts` persist on a 90-day reference half-life before joining the generic pool. This leaves a long direct-scavenging window followed by lower-value dissolved and generic-organic niches. Biological decomposers compete with rather than duplicate each passive flow.

# Determinism, observability, and tests

- Reproduction eligibility and failure reasons are reproducible from the completed phase-7 state.
- Parent plus offspring plus declared waste exactly equals the parent's pre-transaction matter, and reproductive energy use is separately accounted.
- A newborn acts first on the tick after birth and receives no hidden resource grant.
- Different organism iteration orders produce identical births, IDs once the engineering allocator is fixed, allocations, and placements.
- Growth never spends through the configured protection floor, but protected reserve remains physically present and available to higher-priority mandatory work.
- Biological age never decreases and is invariant across save/load.
- Death moves every positive account exactly once; the removed organism owns zero afterward.
- Remnant consumption plus decay never exceeds its start-of-phase contents.
- Digestion conserves every modeled element and cannot extract more useful energy than its reaction declares.
- Removing an empty remnant occurs exactly once and does not leave a spatial-index entry.
- Tile resource-flow history distinguishes scavenging, digestive waste, passive decay, and biological mineralization.

# Validation work remaining

1. Replay the hydrogen and sulfur opening fixtures with the settled reproduction, growth, aging, and population-turnover rules.
2. Prove the four decay cohorts, 90-day mineralization path, and structural-food reaction conserve every element in isolated fixtures.
3. Validate scavenger access, handling saturation, and net population benefit against spatial encounter rates and the multi-week remnant window.
4. Validate the frozen primitive senescence curve and evolved phase/timing modifiers in multi-generation populations.
5. Recalibrate provisional coefficients when the fixed organic-uptake/fermentation and respiration fixtures, or the later predation and complex-cell fixtures, expose an actual imbalance.

None of these is a missing lifecycle semantic decision. The subsystem is ready to hand its fixed interfaces and first balance values to implementation and scenario validation. Mutation-point prices remain owned by the evolution economy; broader metabolic reactions beyond organic uptake, fermentation, and the first aerobic paths remain owned by the mid/late-game rule pack.

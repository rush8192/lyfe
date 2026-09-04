# Simulation Loop and Action Resolution

Status: phase order, causality, external-conflict policies, keyed randomness, and deterministic parallel reduction/commit decided; detailed typed intent payloads and wall scheduling limits remain

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [ORGANISMS vision](../../vision/ORGANISMS.md), [technology decisions](TECHNOLOGY.md), [keyed randomness](KEYED_RANDOMNESS.md), and [deterministic parallel execution](DETERMINISTIC_PARALLEL_EXECUTION.md).

# Purpose

Define the fixed-tick state machine, command boundary, deterministic action ordering, parallel work, and event publication for the authoritative world.

## Implementation checkpoint

`TICK-100` implements the scalar reference boundary and all twelve ordered phase
barriers. Each phase seals a generation-stamped view, emits total-keyed outcomes,
preflights before opening a mutable journal, commits only through typed state
mutators, validates, and seals a phase journal. Phase 3 is the first substantive
slice: it advances organisms in stable-ID order by the compiled one-hour duration.

`LEDGER-100` adds the first phase-5/6 vertical path. Each eligible founder emits a
one-whole-extent hydrogen-acetogenesis intent; coupled H₂/CO₂ scarcity admits only
complete bundles using the registered keyed residual rank. Canonical transaction
records preserve tile debits, organism `ReserveOrganic` credits, ocean-water
boundary output, chemical-energy opportunity, stored energy, and dissipated heat.
Preflight validates finite balances and capacity, the conservation oracle expands
all resources through checked `Int128` CHNOPS totals, and post-commit validation
proves the authoritative balances equal the ledger's promised result. The fixed
one-extent request and `10,000` reserve ceiling are deliberately narrow foundation
values pending compiled phenotype throughput and storage-capacity materialization.

The runner publishes the new tick/revision only after all phases and final
validation succeed. A preflight failure leaves that phase untouched; an unexpected
failure after mutation faults the private working world while queries continue to
see only the preceding published snapshot. The other biological behaviors below
remain authoritative targets rather than claims about the current executable
surface. `HASH-100` now seals each published boundary over canonical logical
state; `CHANGE-100` is the next implementation slice.

# Time model

- The default tick represents one simulated hour and is configurable per world.
- Speed changes alter real processing cadence, never simulated tick duration.
- Pause stops tick advancement at a safe boundary.
- No tick skipping or coarse fast-forward exists.
- Calendar state drives seasons and the configured final date.

The detailed plan must specify wall-clock scheduling, catch-up policy, whether the fastest mode runs unpaced, and how overload is surfaced without silently dropping ticks.

# V1 phase order

The first authoritative phase order is:

0. Admit commands at the tick boundary.
1. Advance calendar and current environmental conditions.
2. Apply non-biological resource sources, sinks, transport, and decay.
3. Advance age and evaluate intrinsic death causes.
4. Move surviving organisms and resolve tile crossings.
5. Evaluate external resource acquisition, environmental energy capture, scavenging, and predation intents from post-movement positions.
6. Resolve external contention and apply its transactions.
7. Resolve internal catabolism, reserve use, maintenance, biomass assembly, and other internal metabolism.
8. Evaluate lifecycle transitions and reproduction for surviving organisms.
9. Materialize observations and update behavior state for the next tick.
10. Update species aggregates, mutation income, and autonomous evolution.
11. Finalize history, actor knowledge, state hashes, and authorized publication.

Behavior selected at the end of tick `T` controls movement and action selection during tick `T + 1`. This gives behavior a consistent one-tick control horizon and prevents a behavior update from retroactively changing movement or interactions that already occurred.

## Phase dependency table

| Phase | Stable inputs | Authoritative writes | Important consequences |
| --- | --- | --- | --- |
| 0. Command admission and state preparation | Completed tick state, canonically ordered accepted commands | Control changes, speciation transactions, pause/speed state, due metabolic-binding releases | Accepted simulation commands apply atomically; expired bindings release before any organism action; query traffic consumes no randomness |
| 1. Calendar and conditions | Prior calendar, fixed tile baselines, root seed and registered weather addresses | Tick/calendar, temperature, precipitation, surface moisture, insolation, volcanic state | Organisms experience the newly current conditions during this tick's death and metabolism phases |
| 2. Environmental ledger | Updated conditions, prior completed tile reservoirs and old remnants | Boundary sources, environmental sinks, gas exchange, mobile-resource exchange, old-remnant decay | Gas order remains source → attrition → symmetric exchange; biological claims have not yet occurred |
| 3. Intrinsic death | Updated environment, organism age/state at tick start | Incremented age, per-tick death-risk assessments, `DeathRecord`, cohesive remnant, organism removal | Every applicable intrinsic cause is evaluated; dead organisms do not move or act; their remains may be found later in this tick |
| 4. Movement | Surviving organisms, prior behavior, prior active velocity, compiled environmental-spread profile, updated environment, keyed Brownian-like displacement, future directional transport field | Movement-energy spend for active displacement, component-attributed position change, active-velocity integration, tile membership | Passive displacement is DNA-scaled, zero-mean, and has no distance charge; directional environmental displacement is exactly zero in v1; active movement is capped by affordable energy; interactions use post-movement coordinates; edge migration uses wrapped `x` and bounded `y` |
| 5. External intent evaluation | Post-movement organisms, tile resources, existing remains, prior behavior, compiled allocation policy | Pre-external metabolic allocations, immutable acquisition/capture/scavenging/predation intents | Private allocation precedes the stable external snapshot; energy-capture claims use age-limited extents; intent evaluation does not mutate shared resources or targets |
| 6. External resolution | Canonically grouped external intents and phase snapshot | Action-energy spend, resource grants, external-capture products, consumed remains, scavenging cooldowns, predation deaths/transfers, new predation remnants | Ordinary claims resolve before scavenging and predation; an admitted scavenging attempt pays and schedules cooldown even if contention yields zero; granted reserve and acquired matter are available to phase 7 |
| 7. Internal metabolism | Post-external organism stores and reserve, compiled DNA, environment | Internal reaction products/waste, reserve expenditure, maintenance, structure/growth, metabolic-failure deaths | Catabolism, digestion, and growth use the current age-throughput cap; captured energy and newly acquired substrates may pay this tick's maintenance; optional growth occurs only after mandatory maintenance |
| 8. Lifecycle and reproduction | Post-metabolism survivors, derived health, and prior behavior directives | Lifecycle transition, zero-sum parent allocation, new offspring | Behavior may suppress an optional transition or reproduction but cannot bypass hard gates; a new offspring begins at age zero and cannot move, act, or reproduce until the next tick |
| 9. Behavior update | End-of-action internal state, completed-tick energy/acquisition samples, pressure memory, and end-of-action capability-filtered observation | Selected behavior/target/dwell and enabled pressure-memory EMA values | Only survivors update; choices affect tick `T + 1`; no global population, hidden client, or future-world input is available |
| 10. Species systems | Completed organism membership, health, and selected behavior | Population/health/behavior aggregates, mutation-point income/remainder, pressure accumulators, autonomous intent and queued proposal | Behavior distributions are one-way derived observations and never become organism inputs; mutation income observes births and deaths from this tick; an autonomous proposal is revalidated and committed at the next phase-0 boundary, so descendants cannot act in their decision tick |
| 11. Finalization | Completed authoritative world | Histories, knowledge state, canonical events, sealed tick-change journal, optional hash/checkpoint | Only a fully successful tick is visible or saveable; actor-authorized protocol projection begins after atomic commit |

When the world is paused, commands explicitly classified as safe-boundary transactions—such as confirmed speciation or sandbox control transfer—may commit and publish without advancing `CompletedTick`; they do increment `WorldRevision`. The next organism-action tick begins from that completed boundary, so new organisms still cannot act in their creation tick. Commands that require tick-phase inputs wait for an ordinary tick. Exact ownership and replay ordering are defined in [WORLD_EXECUTION_AND_OWNERSHIP.md](WORLD_EXECUTION_AND_OWNERSHIP.md).

New remains created by intrinsic death in phase 3 exist before external intent evaluation and may be scavenged during phase 6. Predation deaths occur while phase-6 intents are being resolved; their new remains are not added to the current resolution snapshot and therefore become scavenging targets on the next tick. This avoids order-dependent predation/scavenging cascades.

The post-movement `ExternalInteractionIndex` and end-of-lifecycle `BehaviorObservationIndex` are the two logical spatial snapshots behind these phases. Their contents, same-tile locality, and stable query ordering are defined in [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md); a physical implementation may rebuild or deterministically update the derived bins. Phase-9 pressure samples, persistent behavior, and priority-banded selector are defined in [BEHAVIOR_AND_RESOURCE_PRESSURE.md](BEHAVIOR_AND_RESOURCE_PRESSURE.md).

# Death causality

Phase 3 advances age, derives current stress from the newly updated environment, and evaluates all applicable intrinsic death triggers:

- Reserve at or below the configured terminal threshold.
- Viable structure below the lifecycle's hard floor.
- Senescence probability derived from age and DNA.
- Each hard-limit environmental exposure, including distinct chemical exposures.
- Any other intrinsic lethal condition defined by compiled DNA or lifecycle state.

Each applicable probabilistic trigger receives its own key-derived draw even if another cause has already succeeded. The tick keeps a `DeathRiskAssessment` for every evaluated cause whose probability is greater than zero. If one or more triggers succeed, the organism dies once and its `DeathRecord` contains the complete risk profile plus every successful trigger. This preserves both counterfactual pressure and realized causality without depending on predicate evaluation order.

Predation is a later external death cause and can kill only an organism that survived phase 3. An organism killed intrinsically cannot also be selected as prey in that tick. Each actual attack opportunity with positive death probability appends a risk assessment to the target's tick profile. Multiple successful attacks may therefore appear as triggered causes, but there is one death and one remnant/resource disposition.

Internal metabolism does not repeat general senescence or environmental death evaluation. If all permitted energy-producing reactions and available reserve still cannot pay mandatory maintenance, the metabolic transaction appends a probability-one `MaintenanceFailure` assessment and kills the organism during phase 7. Its death record also includes the non-triggered intrinsic risks carried forward from phase 3. This prevents a one-tick starvation grace period while still allowing energy captured in phase 6 to fund the current tick.

## Death-risk record

```text
DeathRiskAssessment:
    causeId
    evaluationPhase
    probabilityPerTick        // exact fixed-point value used by the draw
    randomDraw?               // absent for deterministic probability-one causes
    triggered                 // draw succeeded or deterministic condition occurred
    contributingInputs        // exposure, limit, age, reserve, attacker, modifiers

DeathRecord:
    organismId
    tick
    terminalPhase
    riskAssessments[]         // every evaluated assessment with probability > 0
    triggeredCauseIds[]       // one or more; never inferred from probability alone
    contributingStresses[]
    remnantId
```

Deterministic death conditions such as terminal reserve exhaustion or an unpaid maintenance obligation are recorded with probability `1`. A risk with probability greater than zero remains in the record even when its draw did not trigger. The UI and autonomous-evolution heuristic must distinguish these cases explicitly.

“All causes” means all death opportunities actually evaluated before the organism's terminal phase. It does not include hypothetical later-phase opportunities the organism could no longer reach. For example:

- An intrinsic death contains every positive intrinsic probability, but no hypothetical predation risks because the organism was removed before movement.
- A predation death contains the positive intrinsic probabilities evaluated earlier plus each actual attack opportunity evaluated against that organism.
- A metabolic-failure death contains its earlier intrinsic risks and the probability-one metabolic failure.

Only death events retain the full per-organism risk profile long term. Survivor assessments may feed bounded species/tile risk aggregates before their tick scratch data is discarded. This avoids turning normal tick evaluation into unbounded event storage while leaving room for exposure-rate analysis that is not conditioned only on death.

# Intent-based resolution

Organism evaluation produces immutable intents whenever multiple organisms can affect shared state. Private organism-only metabolic changes may be calculated independently, but they are still staged and committed at a phase barrier.

```text
AdvanceTick(world, commands):
    tick = begin atomic tick transaction from last completed world

    apply canonically ordered commands accepted for tick.number
    release expired metabolic binding cohorts in OrganismId order

    advance calendar
    update current weather, moisture, insolation, and volcanism
    apply environmental sources and sinks
    decay remnants that existed at tick start
    exchange gases and other mobile resources from stable post-sink views

    intrinsicView = snapshot updated environment and starting organisms
    intrinsicOutcomes = parallel map EvaluateIntrinsicDeath(intrinsicView)
    apply age increments and intrinsic deaths in OrganismId order
    create intrinsic-death remnants

    movementIntents = parallel map IntegrateMovement(survivors, priorBehavior)
    resolve edge crossings and apply movement in canonical tile/organism order

    externalView = snapshot post-movement organisms, resources, and remains
    preExternalAllocations = parallel map ResolvePreExternalAllocation(
        externalView, compiledDnaAllocationPolicy)
    externalIntents = parallel map EvaluateExternalActions(
        externalView, preExternalAllocations, priorBehavior)
    externalTransactions = ResolveExternalContention(externalView,
                                                     externalIntents)
    apply resource grants, capture reactions, scavenging, and predation

    metabolicView = snapshot post-external organism state
    metabolicOutcomes = parallel map EvaluateInternalMetabolism(metabolicView)
    apply internal reactions, maintenance, growth, waste, and failures
    create metabolic-failure remnants

    reproductionView = snapshot post-metabolism survivors
    reproductionOutcomes = parallel map EvaluateLifecycleAndReproduction(
        reproductionView)
    apply lifecycle transitions and valid zero-sum reproduction transactions

    behaviorView = snapshot completed actions, births, deaths, and environment
    pressureSamples = finalize useful demand/grants and usable-energy/
        mandatory-cost coverage for each survivor
    behaviorUpdates = parallel map EvaluateBehaviorForNextTick(
        behaviorView, pressureSamples)
    apply behavior updates to organisms that existed before this phase

    materialize species population, average-health, and per-tile/species
        behavior-count aggregates through the sole phase-10 reducer
    accrue mutation points
    evaluate autonomous evolution and queue any proposal for a later
        phase-0 boundary

    update historical aggregates
    update actor knowledge from completed controlled-species occupancy
    validate conservation, identity, range, and topology invariants
    compute optional state hash
    atomically commit tick and seal its stable-ID change journal
    publish the completed read view and journal to downstream sinks
```

If any phase fails, the tick transaction is discarded and the last completed state remains authoritative. The first physical implementation uses the pooled page-level transactional fork defined in [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md); it may be replaced only by another implementation that preserves the same atomicity and passes the representative benchmark.

Each deterministic phase commit must mutate stores and record logical dirty fields/structural operations through the same typed API. Workers, exact reduction, conflict resolution, canonical creation/ID assignment, preflight, owner-only mutation, and failure behavior follow [DETERMINISTIC_PARALLEL_EXECUTION.md](DETERMINISTIC_PARALLEL_EXECUTION.md). Every probabilistic operation uses the registered semantic address in [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md). Phase journals merge in canonical phase order into the sealed `TickChangeSet`; they are discarded with a failed tick. Actor filtering, subscription filtering, Protocol Buffer materialization, coalescing, and socket work occur only after commit as defined in [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md). They cannot delay the world owner or become a simulation input.

## Intrinsic-death pseudocode

```text
EvaluateIntrinsicDeath(organism, environment, tick):
    nextAge = organism.age + tickDuration
    riskAssessments = []
    contributingStress = derive soft and hard environmental stress

    if organism.viableStructure < dna.hardViableStructureFloor:
        riskAssessments += Assessment(StructuralFailure,
                                      probability = 1,
                                      triggered = true,
                                      inputs = structure and compiled floor)

    if organism.reserve <= dna.terminalReserveThreshold:
        riskAssessments += Assessment(ReserveExhaustion,
                                      probability = 1,
                                      triggered = true,
                                      inputs = reserve and threshold)

    for exposure in hardLimitExposures in canonical ExposureId order:
        probability = dna.deathProbability(exposure, tickDuration)
        if probability > 0:
            decision = Bernoulli(
                RandomAddress(IntrinsicExposureDeath,
                              tick, organism.id, exposure.id, sampleIndex = 0),
                probability)
            riskAssessments += Assessment(EnvironmentalExposure(exposure.id),
                                          probability,
                                          decision.rawWord,
                                          triggered = decision.triggered,
                                          inputs = exposure and compiled limits)

    senescenceProbability = dna.senescenceProbability(nextAge, tickDuration)
    if senescenceProbability > 0:
        decision = Bernoulli(
            RandomAddress(SenescenceDeath,
                          tick, organism.id, 0, sampleIndex = 0),
            senescenceProbability)
        riskAssessments += Assessment(Senescence,
                                      senescenceProbability,
                                      decision.rawWord,
                                      triggered = decision.triggered,
                                      inputs = nextAge and senescence parameters)

    evaluate other intrinsic lethal conditions with distinct draw keys

    triggered = all assessments where assessment.triggered
    if triggered is empty:
        return Survives(nextAge, riskAssessments, contributingStress)
    return Dies(nextAge, riskAssessments, triggered, contributingStress)
```

The primitive founder `terminalReserveThreshold` is zero. This is distinct from the `4,000` optional-growth protection floor: falling below the growth floor suppresses biomass work, while reaching zero at intrinsic-death evaluation is terminal. An organism that cannot pay mandatory maintenance later in the tick instead dies from `MaintenanceFailure` as already defined.

## Internal-metabolism pseudocode

Organic uptake and fermentation bridge phases 6 and 7 through an atomic plan. Against the stable phase-5 view, the organism first receives whole fermentation opportunities from its reaction ceiling and shared internal-processing budget, then uses keyed draws to determine which admitted opportunities become successful candidate extents. It consumes internally retained substrate first and emits an ordinary proportional claim only for the deficit. Phase 7 executes no more than the internal quantity plus actual grant. Claimed substrate committed to the reaction may pass through the bundle without fitting in retained storage; anything retained must fit the normal `DissolvedMacronutrientStore`. Respiration uses the same work-before-draw boundary with a whole coupled fuel/O2 bundle, routes its mass-balanced assimilated carbon to new reserve or explicit waste, and recharges only retained spent carrier matter. See [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md), [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md), and [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md).

At runtime these operations consume immutable typed phenotype process plans with pre-resolved dense resource handles. Organism-local tick reservations live only in worker scratch, while shared external claims use flat tile/resource ranges; authoritative balances change only during canonical commit. Tile-only light, moisture, depth/access, and medium transforms are built once per affected tile/phase and reused across its organisms. See [RESOURCE_STORAGE_AND_EVALUATION.md](RESOURCE_STORAGE_AND_EVALUATION.md).

Photosynthetic plans consume one shared current-light budget across sulfide and oxygenic reactions. Oxygenic execution couples a finite CO2 grant with boundary water and a proved reserve/same-tick output destination, then buffers O2 for the deterministic tile-output merge. It cannot evolve oxygen merely because light exists or reuse light already assigned to sulfide phototrophy; see [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md).

At the start of the phase-5 pre-external allocation barrier, existing free micronutrients are promoted into compiled committed-quota deficits before capability activation is evaluated. This matter-preserving step can activate newly evolved machinery, but nutrients acquired in phase 6 wait until the next tick; see [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).

```text
EvaluateInternalMetabolism(organism, environment, tick):
    state = organism post-external state
    internalBudget = processing work left by the phase-5 plan after
        probabilistic opportunities reserved work before their success draws

    commit preplanned organic-uptake grants to no more than their
        keyed successful fermentation extents
    resolve all enabled internal energy-producing reactions through the
        DNA allocation policy using free AvailableStore, committed substrates,
        and internalBudget
    credit their balanced ReserveOrganic outputs and route their waste

    requiredMaintenance = baselineMaintenance
                        + environmentalSoftStressCost
                        + passiveTraitUpkeep

    paid = spend reserve up to requiredMaintenance
    if paid < requiredMaintenance:
        return Dies(append Assessment(MaintenanceFailure,
                                      probability = 1,
                                      triggered = true,
                                      inputs = requiredMaintenance and paid),
                    remaining state transferred to one remnant)

    resolve optional biomass assembly, binding, internal retention/routing,
        and permitted growth through the DNA allocation policy and remaining
        internalBudget
    enforce every capacity and non-negative ledger invariant
    return Survives(updated state)
```

External energy-capture reactions are committed before this function, so their reserve output is included in `state`. Internal catabolism such as fermentation or respiration runs before maintenance for the same reason. Optional growth cannot consume reserve needed for this tick's already calculated mandatory payment.

# Remaining phase-local decisions

The phase graph and the first external-resolution policies are sufficiently defined for implementation planning. Reproduction health/reserve gates, zero-sum allocation, and cooldown jitter are now fixed in [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md) and exercised by [SURVIVAL_OPENING_VALIDATION.md](SURVIVAL_OPENING_VALIDATION.md); canonical production ID allocation remains engineering work. The first numerical predation, eligibility, feeding, hunting, and movement-economy proposal is defined in [PREDATION.md](PREDATION.md) and [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md); its coupled population fixture remains provisional. Offspring placement is defined in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md): it occurs only after an accepted zero-sum reproduction transaction, remains in the parent's tile, and cannot itself cause migration. Reproduction stays after metabolism, observes post-maintenance health, and gives the newborn no action in its birth tick.

# Action-cost admission

V1 does not permit an organism to borrow energy from a later phase to perform an earlier action:

- Before movement or an active external action commits, calculate its cost from the requested extent and current compiled DNA.
- Cap continuous movement to the distance affordable from the organism's reserve at that phase. If the minimum useful distance is unaffordable, the organism does not move actively; passive displacement remains world physics.
- Admit at most one targeted external interaction per organism per tick: predation or scavenging. Passive uptake and eligible environmental capture may still occur alongside it.
- An active external intent is omitted when its minimum attempt cost is unaffordable. A predation attempt pays its declared attempt cost whether it succeeds or fails.
- Commit the balanced `SpendStoredEnergy` transaction with the movement or action itself. The spent reserve cannot transfer to a predator or remnant if the actor dies later in the tick.
- Energy captured afterward cannot fund an action that already happened, but it is available for internal catabolism, maintenance, growth, and later reproduction in their normal phases.

This immediate debit is action-cost resolution, not a second general internal-metabolism pass. It prevents useful work from becoming free when an organism dies before phase 7 and removes the need for a persistent energy-debt account.

# Ordinary resource contention

Environmental resource and capture claims resolve before scavenging and predation. Newly granted matter is therefore part of a prey organism's contents if it is killed later in the phase. Targeted intents are admitted against the post-movement, pre-grant snapshot, so newly captured energy cannot make an otherwise unaffordable predation or scavenging attempt eligible.

Ordinary finite pools use the capped weighted-proportional-plus-stable-remainder algorithm in [RESOURCE_MODEL.md](RESOURCE_MODEL.md). All ordinary organisms share one priority class and default weight `1`; a typed resource-specific acquisition trait may provide a bounded contention weight, with `DissolvedOrganicSpecialization` supplying the first weight `2`. Claims from one organism are merged before weighting, grants never exceed usable request, and cap leftovers are redistributed deterministically. A weight improves share only under scarcity and never creates an invisible absolute priority.

When a weighted resource is one input of a coupled reaction, the coupled resolver applies that weight only to that scarce input and still admits complete reaction bundles atomically. A dissolved-organic acquisition weight therefore cannot confer priority over scarce oxygen.

Founder passive micronutrient uptake performs its keyed `0.5` opportunity check while building environmental claims and emits at most one one-quantum claim across all inherited quota deficits. It receives no catch-up credit when the opportunity fails or contention denies the claim. The exact target-selection rule is in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).

# V1 predation resolution

V1 predation is a single-tick failed-attempt-or-lethal-capture interaction. Persistent wounds and nonlethal damage are deferred. Same-species predation is excluded from v1.

Each predator may emit at most one `PredationIntent` against an eligible, post-movement target in interaction range:

```text
PredationIntent:
    predatorId
    preyId
    attackPower
    feedingPriorityWeight
    attemptEnergyCost
    maximumConsumableByResource
    destinationCapacityReservation
```

An admitted intent reserves only the destination capacity needed for its capped feeding plan, not prey matter. The reservation prevents earlier phase-6 acquisition from filling that capacity; it is consumed by actual grants or released without backfill after the predation pass. Exact reservation and feeding semantics are defined in [PREDATION.md](PREDATION.md).

The chance that one attempt kills its target is derived from opposing compiled attributes and current-state modifiers captured in the immutable post-movement external view:

```text
attack = predator.predationAttackPower
       * predatorHealthFactor
       * sizeAndMovementCompatibility

defense = prey.predationDefensePower
        * preyHealthFactor
        * activeEscapeOrDeterrenceFactor

odds = basePredationOdds * attack / max(defense, minimumDefense)
killProbability = clamp(odds / (1 + odds), minimumChance, maximumChance)
```

The fixed-point implementation uses checked wide intermediates and configured bounds. Ineligible size, structure, or ingestion combinations reject the intent rather than relying on an almost-zero probability.

Resolve all admitted predation intents in two passes so predators that target one another do not produce order-dependent consumption:

```text
ResolvePredation(allIntents, externalView, tick):
    sort all intents by (PreyId, PredatorId)
    debit every admitted predation attempt cost in PredatorId order
    // all intents were proven affordable against the same pre-grant snapshot

    for intent in allIntents:
        probability = CalculateKillProbability(intent, externalView)
        decision = Bernoulli(
            RandomAddress(PredationKill,
                          tick, intent.predatorId, intent.preyId,
                          sampleIndex = 0),
            probability)
        append assessment to intent.prey's tick risk profile
        mark intent successful when decision.triggered

    killedPrey = every prey with at least one successful intent
    kill every organism in killedPrey once and create one remnant for each
    attach all positive attack assessments and successful triggers

    for prey in killedPrey in PreyId order:
        survivingConsumers = successful predators for prey
            whose PredatorId is not in killedPrey

        for each remnant resource:
            form capped claims from survivingConsumers able to ingest it
            allocate by feedingPriorityWeight with deterministic remainders
            transfer grants to predator AvailableStore

        preserve every unconsumed balance in the remnant for later ticks
```

Mutually successful predators may therefore kill one another, but a predator killed anywhere in the same resolution pass cannot consume another target. `predationAttackPower` changes success probability. `feedingPriorityWeight` changes only the share received when multiple successful surviving predators contest the same prey; it cannot turn a failed attempt into a successful one. Predation defenses oppose attack power through the probability formula and may also limit which capture mechanisms are eligible. This keeps offensive success, defensive resistance, ingestion capacity, and contested feeding priority separately explainable.

The exact first attack/defense factors, size hard gates, pursuit and escape modifiers, attempt costs, immediate feeding caps, opportunistic target selection, hunting policy, and authoring landmarks are normative in [PREDATION.md](PREDATION.md). This document continues to own simultaneous conflict resolution and phase ordering.

# Deterministic randomness

The randomness design is fixed in [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md): a pinned `Philox4x64-10` counter-based function maps the world's 128-bit seed plus a permanent domain ID, three semantic coordinates, and explicit sample index to each result. There is no mutable stream cursor. World generation, weather, organism decisions, interactions, founder selection, remainder rank, and autonomous evolution must register distinct address schemas. New calls do not shift unrelated outcomes; changing an existing schema is an explicit replay-affecting RNG version change.

# Parallelism

The worker, reduction, and commit design is fixed in [DETERMINISTIC_PARALLEL_EXECUTION.md](DETERMINISTIC_PARALLEL_EXECUTION.md). The first vertical slice uses one worker through the final buffer/reducer interfaces. Measured phases may later evaluate tiles/entity ranges concurrently, but cross-tile movement, exchange, shared claims, creations, materializations, and journals pass through the same canonical owner reduction and commit. Load imbalance changes performance only.

# Commands and queries

- Commands carry actor, expected world, and desired application tick or server receipt order.
- The server validates mode and control authority before admission.
- Accepted commands receive a deterministic order and become replay inputs.
- Queries and client subscriptions cannot mutate simulation or consume simulation randomness.

# Failure policy

Define behavior for invalid commands, invariant failures, tick exceptions, excessive tick duration, client disconnects, save requests during a tick, and shutdown. A partially applied tick must never be published or saved as a valid completed state.

# Required decisions and artifacts

- [x] First authoritative phase dependency graph and ordinary-tick pseudocode.
- [ ] Intent and resolution data structures.
- [x] First v1 conflict rules for shared prey, remains, and ordinary resource remainders.
- [x] First birth/death/movement timing and behavior-control horizon.
- [ ] Concrete projection/event schemas and filtering implementation for the already-fixed completed-boundary unknown/reduced/live transitions.
- [x] RNG algorithm, semantic address, conversion, compatibility, and save/replay design; permanent domain numbers remain implementation-scaffold work. See [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md).
- [x] Scalar oracle, worker isolation, exact reduction, stable creation/ID assignment, owner commit, and parallel equivalence design. See [DETERMINISTIC_PARALLEL_EXECUTION.md](DETERMINISTIC_PARALLEL_EXECUTION.md).
- [ ] Tick scheduler and overload policy.
- [ ] Pseudocode for pause, speed change, deadline, and command admission.
- [ ] Sequence diagrams for an ordinary tick and a speciation tick.

# Required external-resolution tests

- Ordinary resource grants conserve the source amount, never exceed requests/capacities, and are invariant under claim and worker ordering.
- Stable remainder ranks reproduce exactly and do not permanently prefer low organism IDs.
- Unaffordable movement is capped and unaffordable targeted actions are omitted before producing effects.
- Failed predation pays its attempt cost and transfers no prey matter.
- Increasing attacker power monotonically increases kill probability; increasing defender power monotonically decreases it within configured clamps.
- Feeding priority does not change attack success and affects allocation only when multiple successful surviving predators contest finite prey contents.
- Multiple successful attacks create one prey death and one remnant without duplicating matter.
- Mutual predation can kill both organisms, neither dead predator consumes, and results are invariant under prey-group ordering.
- All predation assessments appear in a resulting death-risk record with their exact probability, draw, and triggered status.

# Simulation Loop and Action Resolution

Status: first phase-order and causality decision pass; detailed conflict policies pending

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [ORGANISMS vision](../../vision/ORGANISMS.md), and [technology decisions](TECHNOLOGY.md).

# Purpose

Define the fixed-tick state machine, command boundary, deterministic action ordering, parallel work, and event publication for the authoritative world.

# Time model

- The default tick represents one simulated hour and is configurable per world.
- Speed changes alter real processing cadence, never simulated tick duration.
- Pause stops tick advancement at a safe boundary.
- No tick skipping or coarse fast-forward exists.
- Calendar state drives seasons and the configured final date.

The detailed plan must specify wall-clock scheduling, catch-up policy, whether the fastest mode runs unpaced, and how overload is surfaced without silently dropping ticks.

# V1 phase order

The first authoritative phase order is:

1. Admit commands at the tick boundary.
2. Advance calendar and current environmental conditions.
3. Apply non-biological resource sources, sinks, transport, and decay.
4. Advance age and evaluate intrinsic death causes.
5. Move surviving organisms and resolve tile crossings.
6. Evaluate and resolve external resource acquisition, environmental energy capture, scavenging, and predation from post-movement positions.
7. Resolve internal catabolism, reserve use, maintenance, biomass assembly, and other internal metabolism.
8. Evaluate lifecycle transitions and reproduction for surviving organisms.
9. Recompute observations and update behavior state for the next tick.
10. Update species aggregates, mutation income, and autonomous evolution.
11. Finalize history, actor knowledge, state hashes, and authorized publication.

Behavior selected at the end of tick `T` controls movement and action selection during tick `T + 1`. This gives behavior a consistent one-tick control horizon and prevents a behavior update from retroactively changing movement or interactions that already occurred.

## Phase dependency table

| Phase | Stable inputs | Authoritative writes | Important consequences |
| --- | --- | --- | --- |
| 0. Command admission and state preparation | Completed tick state, canonically ordered accepted commands | Control changes, speciation transactions, pause/speed state, due metabolic-binding releases | Accepted simulation commands apply atomically; expired bindings release before any organism action; query traffic consumes no randomness |
| 1. Calendar and conditions | Prior calendar, fixed tile baselines, weather RNG state | Tick/calendar, temperature, precipitation, surface moisture, insolation, volcanic state | Organisms experience the newly current conditions during this tick's death and metabolism phases |
| 2. Environmental ledger | Updated conditions, prior completed tile reservoirs and old remnants | Boundary sources, environmental sinks, gas exchange, mobile-resource exchange, old-remnant decay | Gas order remains source → attrition → symmetric exchange; biological claims have not yet occurred |
| 3. Intrinsic death | Updated environment, organism age/state at tick start | Incremented age, per-tick death-risk assessments, `DeathRecord`, cohesive remnant, organism removal | Every applicable intrinsic cause is evaluated; dead organisms do not move or act; their remains may be found later in this tick |
| 4. Movement | Surviving organisms, prior behavior, prior velocity, updated environment | Movement-energy spend, position, velocity integration, tile membership | Movement is capped by energy affordable before movement; interactions use post-movement coordinates; edge migration uses wrapped `x` and bounded `y` |
| 5. External intent evaluation | Post-movement organisms, tile resources, existing remains, prior behavior, compiled allocation policy | Pre-external metabolic allocations, immutable acquisition/capture/scavenging/predation intents | Private allocation precedes the stable external snapshot; intent evaluation does not mutate shared resources or targets |
| 6. External resolution | Canonically grouped external intents and phase snapshot | Action-energy spend, resource grants, external-capture products, consumed remains, predation deaths/transfers, new predation remnants | Ordinary claims resolve before scavenging and predation; granted reserve and acquired matter are available to phase 7 |
| 7. Internal metabolism | Post-external organism stores and reserve, compiled DNA, environment | Internal reaction products/waste, reserve expenditure, maintenance, structure/growth, metabolic-failure deaths | Captured energy and newly acquired substrates may pay this tick's maintenance; optional growth occurs only after mandatory maintenance |
| 8. Lifecycle and reproduction | Post-metabolism survivors and derived health | Lifecycle transition, zero-sum parent allocation, new offspring | A new offspring begins at age zero and cannot move, act, or reproduce until the next tick |
| 9. Behavior update | End-of-action internal state and end-of-action local observation | Behavior/goal and next-tick movement or action parameters | Only survivors update; choices affect tick `T + 1` |
| 10. Species systems | Completed organism membership and health | Population/health aggregates, mutation-point income, autonomous speciation | Mutation income observes births and deaths from this tick; autonomous descendants begin acting next tick |
| 11. Finalization | Completed authoritative world | Histories, knowledge state, events/deltas, optional hash/checkpoint | Only a fully successful tick is visible or saveable |

New remains created by intrinsic death in phase 3 exist before external intent evaluation and may be scavenged during phase 6. Predation deaths occur while phase-6 intents are being resolved; their new remains are not added to the current resolution snapshot and therefore become scavenging targets on the next tick. This avoids order-dependent predation/scavenging cascades.

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
    behaviorUpdates = parallel map EvaluateBehaviorForNextTick(behaviorView)
    apply behavior updates to organisms that existed before this phase

    recompute species population and average-health aggregates
    accrue mutation points
    evaluate and apply autonomous speciation
    update lineage and historical aggregates

    update actor knowledge from completed controlled-species occupancy
    validate conservation, identity, range, and topology invariants
    compute optional state hash
    atomically commit tick
    publish actor-authorized events and deltas from committed state
```

If any phase fails, the tick transaction is discarded and the last completed state remains authoritative. This is a logical atomicity requirement; implementation may use staged buffers, inverse-free arenas, or another design rather than copying the entire world.

## Intrinsic-death pseudocode

```text
EvaluateIntrinsicDeath(organism, environment, tickKey):
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
            draw = KeyedDraw(tickKey, organism.id, exposure.id)
            riskAssessments += Assessment(EnvironmentalExposure(exposure.id),
                                          probability,
                                          draw,
                                          triggered = draw < probability,
                                          inputs = exposure and compiled limits)

    senescenceProbability = dna.senescenceProbability(nextAge, tickDuration)
    if senescenceProbability > 0:
        draw = KeyedDraw(tickKey, organism.id, Senescence)
        riskAssessments += Assessment(Senescence,
                                      senescenceProbability,
                                      draw,
                                      triggered = draw < senescenceProbability,
                                      inputs = nextAge and senescence parameters)

    evaluate other intrinsic lethal conditions with distinct draw keys

    triggered = all assessments where assessment.triggered
    if triggered is empty:
        return Survives(nextAge, riskAssessments, contributingStress)
    return Dies(nextAge, riskAssessments, triggered, contributingStress)
```

## Internal-metabolism pseudocode

```text
EvaluateInternalMetabolism(organism, environment, tickKey):
    state = organism post-external state

    resolve enabled internal energy-producing reactions through the
        DNA allocation policy using free AvailableStore and substrates
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
        and permitted growth through the DNA allocation policy
    enforce every capacity and non-negative ledger invariant
    return Survives(updated state)
```

External energy-capture reactions are committed before this function, so their reserve output is included in `state`. Internal catabolism such as fermentation or respiration runs before maintenance for the same reason. Optional growth cannot consume reserve needed for this tick's already calculated mandatory payment.

# Remaining phase-local decisions

The phase graph and the first external-resolution policies are sufficiently defined for implementation planning. Exact reproduction attempt probability, offspring placement, deterministic ID allocation, and the numerical predation curves remain balance/mechanics work. Reproduction stays after metabolism, observes post-maintenance health, and gives the newborn no action in its birth tick.

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

Ordinary finite pools use the proportional-plus-stable-remainder algorithm in [RESOURCE_MODEL.md](RESOURCE_MODEL.md): grant each request its integer proportional floor, then assign leftover quanta to unmet claims by a deterministic hashed rank. All ordinary organisms share one priority class. Acquisition traits increase eligible resources or requested throughput; they do not create an invisible contention priority.

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
```

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
ResolvePredation(allIntents, externalView, tickKey):
    sort all intents by (PreyId, PredatorId)
    debit every admitted predation attempt cost in PredatorId order
    // all intents were proven affordable against the same pre-grant snapshot

    for intent in allIntents:
        probability = CalculateKillProbability(intent, externalView)
        draw = KeyedDraw(tickKey, intent.preyId, intent.predatorId, Predation)
        append assessment to intent.prey's tick risk profile
        mark intent successful when draw < probability

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

# Deterministic randomness

Define a random-stream scheme that covers world generation, weather, organism decisions, interaction outcomes, founder selection, and autonomous evolution. It must:

- Reproduce with the same seed, rules, configuration, and ordered commands.
- Remain unchanged when presentation requests differ.
- Avoid dependence on worker completion order.
- Permit save/reload without changing the next draw.
- Support diagnostic attribution of important random outcomes.

Compare mutable named streams with counter-based/key-derived draws. Document how new random call sites affect compatibility with existing saves.

# Parallelism

The likely first parallel boundary is tile-level evaluation. Cross-tile movements and exchanges should be emitted into deterministic boundary buffers and applied after a barrier. The plan must cover load imbalance when a few tiles contain most organisms.

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
- [ ] Detailed live/reduced visibility transitions and event filtering within the same tick.
- [ ] RNG stream/key design.
- [ ] Parallel partition and deterministic merge.
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

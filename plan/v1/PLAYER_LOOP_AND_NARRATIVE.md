# Player Loop, Attention, and Emergent Narrative

Status: first v1 contract; interaction wireframes and opening-choice calibration remain

Sources: [GAMEPLAY vision](../../vision/GAMEPLAY.md), [INTERFACE vision](../../vision/INTERFACE.md), [gameplay flows](GAMEPLAY.md), [evolution](EVOLUTION.md), [behavior and resource pressure](BEHAVIOR_AND_RESOURCE_PRESSURE.md), and [Survival opening validation](SURVIVAL_OPENING_VALIDATION.md).

# Purpose

Turn LYFE's observable simulation and irreversible evolution system into a recurring player loop with legible choices, timely feedback, and memorable lineage stories. The player remains an evolutionary guide: none of these tools command organism movement, feeding, reproduction, or behavior.

This document owns player-facing decision rhythm, attention management, proposal framing, consequence review, and the factual chronicle. Biological rules, mutation prices, visibility, and simulation authority remain owned by their subsystem plans.

# Voice and copy contract

Player-facing prose uses two related registers rather than one uniform product voice:

- **World, narrative, and discovery copy** carries wonder, curiosity, and a little mystery.
  Abiogenesis, organism stories, lineage landmarks, habitat discovery, evolution milestones, and
  run summaries may be whimsical and evocative. They should make an unfamiliar living world feel
  worth watching without pretending that the simulation observed intentions, destiny, or causes it
  did not record.
- **Mechanics, decisions, and diagnostics** lead with plain meaning. Costs, quantities, time windows,
  prerequisites, blockers, scopes, warnings, uncertainty, and irreversible consequences use direct,
  concrete language. A light aside, cheeky label, or small flourish may add personality after the
  rule is clear; flavor may never replace the rule or make a risky state sound safe.

Copy should sound authored for LYFE, not assembled from generic interface prose. The polish pass
removes repetitive caveat paragraphs, interchangeable headings, inflated abstractions,
over-explanation, and conspicuously templated sentence rhythms. Concision alone is not the goal:
the narrative register may breathe, but every line should earn its place and use the project's own
biological imagery and vocabulary.

Factual integrity remains the hard boundary. Metaphor cannot imply hidden knowledge, unsupported
causality, organism consciousness, a survival prediction, or a success judgment. Mechanically
significant facts must remain available in literal text for accessibility and localization rather
than being encoded only in a joke, metaphor, icon, color, or tooltip. Canonical event facts and
simulation hashes never depend on presentation wording.

Before play-tester alpha, perform one coherent editorial pass over setup, empty/loading/error states,
map and organism inspection, resource diagnostics, evolution planning, alerts, consequence review,
chronicle, extinction, and end-of-run presentation. Maintain a short terminology and voice sheet so
later features do not drift back into generic copy. Playtest the result for both comprehension and
tone: players should be able to restate a consequential mechanic accurately while describing the
world as intriguing rather than clinical or procedural.

# Engagement contract

V1 should support four kinds of agency:

- **Interpretive agency:** identify what is happening and which evidence supports that conclusion.
- **Strategic agency:** choose which pressure, opportunity, or long-term capability a descendant should address.
- **Commitment agency:** choose DNA changes and the one-to-four-tile founding cohort for an irreversible speciation.
- **Attention agency:** decide what to watch, which changes deserve interruption, and when to pause or accelerate.

LYFE does not promise that every choice succeeds. It must make the known tradeoffs, uncertainty, and subsequent causal evidence visible enough that a loss feels attributable rather than arbitrary. A large trait catalogue is not sufficient: a decision is meaningful only when the player can distinguish plausible alternatives by intent, cost, risk, and expected context.

The engagement curve begins with life struggling against its environment. Early alerts, explanations, and evolution proposals should primarily expose resource limitation, energy shortfall, toxicity, seasonal change, tolerance mismatch, and population overshoot. As biological interactions emerge, the same diagnostic loop expands to scavenging, competition, predation, defense, and prey depletion. This should feel like an ecological history produced by the simulation, not a fixed sequence of scenario stages; the interface labels the evidence and transition only after the underlying events occur.

# Nested player loops

| Horizon | Trigger and player question | Principal actions | Required feedback |
| --- | --- | --- | --- |
| Moment-to-moment observation | A visual change, alert, or selected organism: “What is happening?” | Pan, zoom, follow, change lens, pause, inspect | Current behavior, health factors, resource flows, movement, and timestamps |
| Diagnosis | A pressure persists or an opportunity appears: “Why, where, and to whom?” | Compare tiles, cohorts, resources, risks, and time windows | Cause chain, trend/runway, limiting factors, observation scope, and uncertainty |
| Evolution decision | A useful proposal becomes affordable or a saved goal matures: “What lineage should I create?” | Compare proposals, edit a draft, select founding tiles, inspect founders, commit or bank | Immediate versus conditional benefit, liabilities, prerequisite path, founder viability, and exact transaction |
| Consequence review | Speciation or another milestone: “Did my hypothesis work?” | Watch ancestor and descendant, compare pre/post windows, inspect migrations and deaths | Attribute deltas, population/health/resource divergence, activation failures, and notable events |
| Run narrative | Discovery, ecological transition, extinction, or deadline: “What did this world become?” | Browse lineage and chronicle, revisit evidence, continue or end | Factual milestones, branch outcomes, map spread, replay links, and final summary |

The normal rhythm is:

```text
Notice -> Inspect -> Form a hypothesis -> Compare evolutionary responses
       -> Commit or deliberately bank -> Observe consequences -> Reassess
```

Watching and banking are valid decisions, but prolonged waiting must still provide changing ecological evidence, exploration, lineage events, or an honest indication that the current world is stable. The UI must not manufacture busywork to conceal a quiet simulation.

# Player verbs and boundaries

## Observe and focus

- Navigate the world and switch environmental, resource, population, behavior, and lineage lenses.
- Inspect authorized tiles, species, organisms, remains, resources, reactions, and historical intervals.
- Follow an actual organism or pin a species, tile, resource, lineage branch, or evolutionary goal.
- Watch configurable lifecycle activity pulses on the live map and open any authorized
  organism's chronological journey from its sprite, remnant, or linked event.
- Compare up to four compatible subjects or time intervals without converting hidden information into current data.
- Add private labels or hypothesis notes to pins and chronicle events.

## Plan and commit

- Build, save, duplicate, and compare evolution-proposal drafts without changing authoritative state.
- Pin one evolution goal per controlled species and see its prerequisite closure, estimated affordability at the current earned rate, event-complexity requirement, and activation conditions.
- Preview and commit a speciation through the existing authoritative transaction.
- Deliberately defer a decision and keep accumulating mutation points.
- In sandbox only, transfer control and lock or unlock autonomous evolution as already specified.

## Do not add in v1

- Direct movement, hunting, feeding, reproduction, or behavior orders.
- Resource grants, environmental painting, or trait refunds in Survival.
- Hidden optimal-choice hints or a server-selected “recommended” mutation.
- Quests that pay matter, energy, mutation points, or biological bonuses for interface actions.

# From evidence to an evolution decision

## Decision opportunities

The server derives an authorized `EvolutionDecisionSummary` after completed ticks and after relevant commands. It contains the current balance and rate, cooldown, valid proposal frontier, saved-goal readiness, and evidence inputs already authorized to the actor. It is read-only and cannot influence autonomous evolution or organism behavior.

A proposal belongs to one benefit-timing class:

| Class | Meaning |
| --- | --- |
| `Immediate` | The completed proposal changes an active capability or parameter for at least part of the selected cohort in its observed current context. |
| `Maturing` | The proposal changes a physical target immediately, but organisms must construct or provision the capability from conserved resources before it becomes complete. |
| `Conditional` | It enables a complete capability, but its required environment or finite resource is not currently present or sufficient in the selected cohort. |
| `Preparatory` | It unlocks or improves a later path but does not yet produce the advertised capability. |

`Maturing`, `Conditional`, and `Preparatory` are not synonyms for useless. They must, however, be labeled before commit and may never be presented as though the final milestone were already active. The classification is evaluated for each selected founding plan; a proposal can be immediate in one tile and conditional in another. A maturing proposal additionally exposes its material target, current assignments, construction rate and blockers, and the capability commissioned so far.

The client groups the non-dominated frontier by an explanatory strategic intent:

- Exploit the current niche.
- Endure environmental variation or toxicity.
- Expand or alter dispersal.
- Diversify resource and energy access.
- Compete, hunt, evade, or defend.
- Invest in regulation, storage, organization, or future evolvability.

These intents organize information only. They grant no bonus, impose no path, and do not replace the trait-family tree.

## Non-dominated proposal frontier

The server need not enumerate every trait combination. For each authored candidate goal and strategic intent, it produces prerequisite-closed proposals within the species' change-complexity limit, then removes a proposal only when another proposal has all of:

- no greater mutation price;
- no greater change complexity;
- no worse activation class for the selected tile plan;
- no greater recurring costs in every cost channel; and
- the same or a strict superset of compiled effects relevant to that intent.

Dominance is an information-reduction rule, not a balance judgment. The full valid trait tree and manual proposal builder remain available.

## Milestone framing

Prerequisite-closed multi-trait proposals may be displayed as one milestone card when they fit the current event-complexity limit. The card always expands to its underlying trait nodes, summed price, summed complexity, permanent incompatibilities, and individual effects. There is no bundle discount and no new mechanical milestone resource.

If a complete milestone cannot fit in one event, the planner shows an ordered route. Each intermediate event is explicitly marked preparatory and shows:

- what it changes now;
- which future proposal it unlocks;
- remaining MP and complexity;
- estimated time to the next step at the current income rate; and
- any upkeep or vulnerability paid before the final capability becomes useful.

This is particularly important for the founding paths: `OrganicResourceUptake` alone extracts no energy, and the sulfide lineage's regulation/generalized-catabolism bridge may require more than one event. Presentation cannot compensate if the opening offers only one rational route; that is a balance failure described below.

## Evolution-goal contract

An `EvolutionGoal` is player planning state, not an accepted biological command:

```text
EvolutionGoal:
    actor_id
    controlled_species_id
    canonical_target_trait_ids
    preferred_tile_ids[]          // optional; authorized and currently occupied only
    created_at_tick
    last_evaluated_tick
    private_label                 // optional, never interpreted by the simulation
```

The goal never reserves MP, locks traits, predicts population, or causes automatic purchase. Its ETA uses the recent earned-income rate and must be labeled as a current-rate estimate. Genome, authority, incompatibility, or tile changes can make it stale; the client preserves the draft but requires a new preview.

## Required proposal comparison

For each proposal and selected founding plan, show:

- strategic intent and benefit-timing class;
- new traits, completed capabilities, indirect attribute changes, and appearance cues;
- MP price, change complexity, balance afterward, duplicated ancestor/descendant balance, and cooldown;
- constitutive, suppressible, metabolic, reproductive, structural, storage, and micronutrient costs by channel;
- prerequisites, incompatibilities, inactive dependencies, and later path opened;
- exact founder counts and ancestor remainder per tile;
- current cohort health/reserve distributions and material-opportunity or biological-opportunity evidence;
- current-state risk flags, including small cohort, inactive environment, scarce resource flow, overshoot, tolerance mismatch, or uncommissioned storage;
- what observable metrics would support or contradict the player's hypothesis after commit.

The comparison may calculate consequences already defined by the rules. It must not simulate secret futures, roll future randomness, expose hidden competitors or tiles, or claim a probability of long-term survival that the engine has not defined.

# Consequence review

An accepted player speciation creates a `ConsequenceReview` anchored to its lineage event. For the next 168 simulated hours—the existing branch cooldown—the client pins ancestor and descendant comparison by default. This changes presentation only.

The review compares the pre-event baseline with current observed values for:

- population, births, deaths, and average health;
- reserve and acquisition coverage;
- behavior distribution and suppressed actions;
- occupied tiles and migration causes;
- relevant resource stocks, production, uptake, and loss;
- activation fraction of the acquired capability; and
- recurring trait costs and any dominant new stress.

It answers “what changed?” rather than declaring success. At the cooldown boundary it creates a summary event even if the result is inconclusive. A conditional or preparatory proposal is judged against its stated activation/path-opening claim, not against an immediate population gain.

The cooldown summary does not close observation permanently. Each proposal may author a longer side-effect-free follow-up landmark for its primary evidence. The first opening fixture uses 720 hours for passive-spread and storage strategies because their migration and reproduction consequences are too sparse for a stable seven-day comparison. The chronicle surfaces that later result without extending the mechanical cooldown or forcing the player to keep the panel open.

# Attention and interruption

## Watchlist

Each actor may pin a bounded watchlist of tiles, species, lineage nodes, resources, and proposal goals. The client uses pins for layout and subscriptions; the server still applies actor knowledge before projecting any state.

## Notification classes

| Severity | Examples | Default behavior |
| --- | --- | --- |
| Informational | First reproduction, new reduced tile, population landmark, notable resource reversal | Chronicle badge; no pause |
| Strategic | New live tile, new death-risk channel, completed milestone, branch cooldown complete, immediate evolution option available | Notification; no pause |
| Decision-ready | Pinned evolution goal is now affordable and valid; first meaningful evolution opportunity for a new controlled species | Pause at next completed boundary |
| Critical | Controlled population crosses a configured danger threshold or a new acute death mechanism is realized | Pause at next completed boundary |

Default automatic pauses apply only in single-player worlds and can be disabled per class. A threshold alert fires on entry, then rearms only after its configured recovery boundary; it cannot fire every tick. Informational and strategic events are coalesced by entity, event type, and time window.

The first balance values for critical controlled-lineage alerts are provisional configuration, not death prediction:

- living population falls by at least 25% over the trailing 24 simulated hours;
- average health remains below `0.25` for six consecutive hours; or
- living population reaches ten organisms or fewer after previously exceeding ten.

Recovery rearms the respective alert after the 24-hour decline is below 10%, health exceeds `0.35` for six hours, or population exceeds fifteen. These values require scenario and playtest calibration. The notification states the observed condition and evidence; it does not say extinction is certain.

The implemented attention pass routes canonical notable events and lineage-review boundaries into a
saved append-only inbox. Same-family events for one species at one completed boundary are coalesced
and retain every chronicle evidence ID. Saved hysteresis covers low population, trailing-24-hour
decline, sustained low health, and sustained composite resource pressure. A realized death cause is
reported once per species/cause and linked to its retained journey event. This pass reports critical
and strategic alerts but does not yet apply auto-pause.

An auto-pause policy that changes the world clock is authoritative actor/world state, evaluated after the completed tick and effective before the next tick. Visual-only pins, panel layout, private notes, and non-pausing local notification preferences may remain client profile state. Future multiplayer must replace unilateral pause with a shared-clock policy; v1 schemas cannot assume every alert may stop the world.

```text
AfterCompletedTick(world, actor):
    observation = BuildAuthorizedObservation(world, actor)
    summary = BuildEvolutionDecisionSummary(observation, actor.controlledSpecies)
    events = EvaluateNotableEvents(observation, priorActorEventState)
    alerts = EvaluateAttentionPolicy(summary, events, priorAlertState)
    persist actor event/alert state
    if singlePlayer and any alerts request pause:
        pause before next tick boundary
    publish authorized summary, events, and alerts
```

This pass uses no simulation randomness, and subscriptions cannot change its result.

# Factual chronicle and species stories

The chronicle is a compact history generated from actual simulation events and completed-tick aggregates. It turns emergent outcomes into a navigable story without adding authored causality that did not occur.

First v1 notable-event families are:

- abiogenesis and root founding;
- first reproduction and population landmarks;
- speciation, trait milestone, controller transition, and extinction;
- first occupation, first landfall, habitat expansion, and migration-mechanism landmark;
- first scavenging, predation, photosynthesis, respiration, or other completed reaction milestone;
- new realized death mechanism or sustained dominant pressure;
- significant resource depletion, recovery, source/sink reversal, or oxygenation-band transition;
- ancestor/descendant comparison at the branch-cooldown boundary; and
- final-date or extinction result.

Each `NotableEvent` stores canonical facts: event ID and type, completed tick, involved stable IDs, authorized evidence references, significance/rule version, and deduplication key. Human-readable title and prose are presentation derived from those facts and may be localized or restyled without changing replay hashes. Event thresholds use hysteresis, minimum duration where appropriate, and per-key cooldowns to prevent noise.

The first implemented significance-rule version covers speciation, first reproduction, fixed
population thresholds, first species/tile occupation, first true compiled-reaction execution,
extinction, first realized death cause, and sustained high composite resource pressure. These
records use their typed IDs and source-event/value fields as bounded evidence identity, share one
saved event-ID namespace with lineage-review landmarks, and deduplicate by a canonical semantic key.
Per-compound depletion/recovery, source/sink reversal, and oxygenation-band transitions remain.

Chronicle projection follows knowledge rules. It cannot disclose hidden current organisms, locations, resources, or competitor activity. A later discovery may generate “first observed” evidence, but does not retroactively reveal secret history. Omniscient post-run presentation, if later allowed, is a separate explicit mode.

Player labels and hypothesis notes are clearly distinguished from factual events. They never enter autonomous-evolution scoring, organism behavior, world randomness, or the canonical narrative text.

## Organism-scale stories and moment-to-moment feedback

The world chronicle records species/world landmarks; it does not replace the smaller story
of an individual life. Every authorized organism has a journey assembled from retained
facts: founding or birth, parent/offspring links, reproduction, migration, meaningful
feeding interactions, summarized acquisition, major health/stress or lifecycle transitions,
and death. Following an organism therefore provides a readable narrative between mutation
decisions without granting the player direct control over its actions.

The live map projects the same completed facts as short-lived, configurable activity icons.
Positive events use green/teal presentation, harmful events use orange/red, and neutral
transitions use intermediate gold/amber colors, always paired with distinct symbols. Icons are
feedback, not rewards: they grant no resource, do not influence behavior, and may be
filtered or coalesced without changing the organism journey. Detailed animation, filtering,
retention, accessibility, and visibility rules are normative in [CLIENT.md](CLIENT.md).

# Failure, uncertainty, and recovery of understanding

V1 Survival retains irreversible evolution and no backtracking. Its safeguards are informational:

- typed proposal warnings before commit;
- auto-pause before the next tick after observed critical thresholds;
- trend and runway displays that cite their historical window;
- the consequence review after speciation;
- death records containing the realized trigger plus every positive death probability at the death tick; and
- a loss postmortem that reconstructs the last material, energy, health, behavior, environment, and lineage changes without inventing one exclusive cause.

A player may still choose a risky founder cohort or speculative trait path. Warnings do not reject biologically valid proposals. The planner should preserve and visibly distinguish uncertainty: a current-rate MP ETA, current-stock opportunity estimate, and historical reserve runway are not forecasts of future weather or survival.

# Mode-specific expression

## Survival

The loop emphasizes one controlled lineage, irreversible descendant choice, restricted knowledge, critical alerts, and a final causal postmortem. The autonomous paired founder creates comparison and ecological pressure but receives no special narrative omniscience.

## Free sandbox

The same observation, planner, consequence-review, and chronicle systems apply. Control transfer and evolution locks allow comparative experiments. Pins survive control transfer, but their information becomes stale or reduced when the newly controlled species does not authorize a live view. Sandbox should eventually receive a full-run acceptance scenario; v1 does not require environmental editor powers.

# Opening-loop balance gate

The coupled opening fixture proves viability and mutation timing, but not yet meaningful choice. Its first modeled steps expose a specific risk:

- hydrogen can afford `OrganicResourceUptake` at 40 MP, but uptake alone yields no energy without fermentation and available dissolved organic matter;
- sulfur can afford `MetabolicRegulation` at 60 MP, but regulation may provide little immediate benefit while its founding pathway remains continuously useful.

Before freezing the v1 founder packages, each player-controlled metabolism needs an opening menu containing at least two non-dominated, different-intent proposals. At least one must have an `Immediate` benefit in the validated opening, while a preparatory escape-path investment may remain the other. Candidate alternatives may come from existing environmental tolerance, environmental spread, storage, regulation, or pathway nodes; do not invent a free temporary buff. Both choices must be run through the same coupled population fixture, including the mirrored hydrogen-player opening.

This gate is deliberately stronger than “a trait is affordable.” If only one option is rational, the choice is cosmetic; if every option is preparatory, the first promised feedback loop is absent.

## First calibration hypothesis

Use existing nodes before adding another biological system:

| Founder at first landmark | Proposal to test | Intended choice and honest timing |
| --- | --- | --- |
| Hydrogen at 40 MP | `EnvironmentalAnchoring` (`40/1`) | Immediate niche-retention strategy: reduce passive escape from the productive founding tile at a small upkeep and active-speed cost |
| Hydrogen at 40 MP | `OrganicResourceUptake` (`40/1`) | Preparatory diversification strategy: begin the cheaper fermentation route, with no energy claim until the reaction and substrate are available |
| Hydrogen at 40 MP | `ReserveCapacityI` (`40/1`) | Maturing buffering strategy: begin constructing larger reserve capacity, paying upkeep and health dilution before it fills |
| Sulfur at 40 MP | `EnvironmentalAnchoring` (`40/1`) | Immediate specialization strategy: remain near the locally replenished sulfide/light niche at the cost of future spread |
| Sulfur at 60 MP | `EnvironmentalDrifting` (`60/1`) | Immediate but risky expansion strategy: increase chance dispersal despite dependence on a localized substrate |
| Sulfur at 60 MP | `MetabolicRegulation` (`60/1`) | Preparatory flexibility strategy: pay the first cost on the longer route away from the founding pathway |
| Sulfur at 40 MP | `ReserveCapacityI` (`40/1`) | Maturing buffering strategy under the same construction and dilution liabilities |

The first executable extension and results are now in [OPENING_STRATEGY_VALIDATION.md](OPENING_STRATEGY_VALIDATION.md). Retain a proposal on the suggested frontier only if it is non-dominated in at least one plausible observed opening state and its intended consequence is legible. If anchoring becomes universally superior for a volcanic founder, adjust its costs/effects or opening geography rather than hiding its dominance with UI labels. If these options cease to produce a compelling immediate-versus-long-term tradeoff under the production rule pack, the next intervention should be a small existing-family tolerance or efficiency node, not a disconnected minigame.

# Validation and telemetry

## Deterministic acceptance

- Decision summaries and proposal frontiers are identical across save/load, worker count, subscription shape, and proposal enumeration order.
- Every displayed cost, effect, founder count, warning, and activation class is traceable to the accepted preview and completed authoritative state.
- Hidden tiles and species never influence actor-visible explanations except through authorized observations.
- Auto-pause occurs only after a completed tick, changes no completed outcome, and replay applies the same recorded policy/clock transition.
- Chronicle event IDs, fact payloads, thresholds, and ordering replay identically; narrative wording is excluded from authoritative hashes.
- Organism journey landmarks and deterministic routine summaries survive save/load and
  replay identically; map-pulse preferences and fade progress do not enter authoritative state.
- Alert hysteresis and coalescing prevent repeated tick-by-tick notifications for one sustained condition.

## Gameplay measurements

Instrument without turning metrics into simulation inputs:

- real and simulated time between meaningful decision opportunities;
- size and strategic-intent diversity of the non-dominated affordable frontier;
- share of committed events classified immediate, maturing, conditional, and preparatory;
- time from commitment to first relevant observable consequence;
- rate of proposal cancellation, banking, and stale-goal revision;
- which warnings preceded failed branches and whether players inspected them;
- alert rate, auto-pause rate, dismissals, and alert-to-action time;
- player ability in a playtest to identify a dominant pressure and cite supporting evidence;
- branch survival, ecological niche, habitat spread, and final lineage diversity by chosen proposals; and
- chronicle events opened, followed into evidence, or used for navigation.
- organisms followed, journey entries inspected, and map-pulse filters changed, including
  whether players can correctly explain a selected organism's current state.

Initial scenario gates are:

1. Each founding metabolism has at least two non-dominated opening proposals with different intents, including one immediate option.
2. Every immediate proposal in the opening fixture produces a relevant visible difference within its authored consequence window, with the universal 168-hour summary retained even when a slower follow-up is required.
3. Preparatory proposals never receive immediate-success language and show a complete next milestone with remaining price and complexity.
4. A player can move from a critical alert to its underlying health/resource/death evidence without searching unrelated screens.
5. The final postmortem accounts for the controlled lineage's terminal decline using only recorded facts and clearly labeled inferences.

Do not set global targets for choice frequency, strategy win rates, or chronicle density until instrumented human playtests establish a baseline. The “every few real-world minutes” mutation cadence remains a pacing target, not permission to add low-value interruptions.

# Deferred directions

- Mode-specific objectives, challenges, or scenario authorship beyond survival and open sandbox.
- A limited lineage-recovery mechanic for Survival.
- Shared-clock notification and pause governance for multiplayer.
- Player-authored experiments that compare alternate command branches from a checkpoint; these must never masquerade as the canonical run.
- Natural-language chronicle variation beyond deterministic fact templates.
- Omniscient post-run ecology playback.

# Remaining implementation artifacts

- [ ] World HUD, lifecycle-pulse overlay/filter, organism Journey tab, alert inbox,
  diagnostic path, proposal comparison, goal, consequence-review, chronicle, and postmortem wireframes.
- [ ] Stable protocol schemas for organism journey/activity facts, decision summaries,
  drafts/previews, attention policy, alerts, and notable events.
- [ ] Authored candidate-goal catalogue and deterministic non-dominance tests.
- [ ] Opening-menu balance variants for both founder choices.
- [ ] Chronicle significance thresholds and retention budget.
- [ ] Human playtest script and telemetry/privacy policy.

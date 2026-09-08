# Browser Client

Status: Stage-A generated snapshot transport, normalized cache, absolute batch applier, and truthful PixiJS organism view implemented; playable controls and explanatory surfaces remain

Sources: [INTERFACE vision](../../vision/INTERFACE.md), [GAMEPLAY vision](../../vision/GAMEPLAY.md), [player loop and narrative](PLAYER_LOOP_AND_NARRATIVE.md), [behavior and resource pressure](BEHAVIOR_AND_RESOURCE_PRESSURE.md), [state change and client synchronization](STATE_CHANGE_AND_CLIENT_SYNC.md), [moddability](MODDABILITY.md), and [technology decisions](TECHNOLOGY.md).

# Purpose

Define the first TypeScript, React, and PixiJS client as a replaceable thin client that renders actual simulation state and helps the player understand causality.

# Client boundaries

The client owns:

- Connection and protocol adaptation.
- Presentation state, camera, selections, filters, and open panels.
- Interpolation between authoritative coordinate updates.
- Display-only aggregation and chart preparation.
- Pending-command UX and error presentation.
- Presentation-only randomness isolated from the world seed.

The client does not own organism decisions, resource resolution, mutation income, game outcomes, or authoritative time.

The client also does not embed authoritative balance or world-profile values. It receives the final server-compiled definition metadata required for explanation and presentation, caches it by final mechanics/presentation/world-profile identity, and displays the world's base pack, canonical mod set, selected world pack/profile/options, and certification class. A modified world must be visibly distinguishable from an official canonical world. The browser never evaluates overlay or generator logic or executes mod-supplied code; protocol values and server outcomes remain authoritative.

Authoritative resource and energy quantities may exceed JavaScript's exact integer range. The protocol adapter must preserve them as `bigint`, strings, or generated long values and convert only presentation-scaled values to `number` for charts and labels.

# Proposed layers

```text
React application shell
  ├── setup and game-mode flows
  ├── inspectors and controls
  ├── evolution and lineage views
  └── resource/death/history visualizations

Client state/query layer
  ├── protocol adapter
  ├── normalized snapshot/delta cache
  ├── unknown, reduced, live, and stale observation state
  ├── selections and subscriptions
  └── command lifecycle

PixiJS world renderer
  ├── tile map and layers
  ├── organism and remnant instances
  ├── lifecycle activity-pulse overlay
  ├── camera/follow behavior
  └── density or aggregate rendering at distant zoom
```

# Primary screens and surfaces

- World creation and founding-species setup.
- Compatible world-profile selection, bounded option controls, profile premise/pressure summary, and actor-authorized generated start-site preview; setup must not reveal exact hidden-tile composition that ordinary exploration would conceal.
- Storybook abiogenesis introduction.
- World map with environmental/resource layers.
- Within-tile organism and remnant view.
- Tile inspector with current and historical conditions.
- Organism and species inspector.
- Resource-level and flow history.
- Tree of life and evolution planner.
- Watchlist, alert inbox, evolution-goal status, and ancestor/descendant consequence review.
- Factual world chronicle with evidence navigation and private hypothesis notes.
- Per-organism journey log and on-map activity-filter controls.
- Pause, speed, date, deadline, save, and run-status controls.
- End-of-run summary.
- World/rule identity and modification status in setup, load, and persistent run information.

Survival setup presents the two choices defined in [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md), including expected opening reproduction time, environmental dependencies, tolerance, and the first route away from volcanism. It makes the paired autonomous founder explicit without displaying its hidden organisms or live resource state.

# Exploration presentation

- Unknown tiles use a clearly unavailable treatment and expose no environmental tooltips or layers.
- Reduced tiles show fixed geography, baselines, coarse composition, and timestamped last-known observations. They never animate current weather, organisms, remains, or resources.
- Live tiles show exact current state and truthful organism/remnant entities.
- A live-to-reduced transition evicts organisms, remains, exact current values, and hidden chart points from the client cache.
- Previously observed charts show explicit gaps for hidden intervals and label the last observation tick/date.
- Sandbox control transfer retains discovered-map presentation; the authoritative knowledge owner updates the live-tile set from the new controlled species, and the client derives styling and subscriptions only from that projected set.

Client layers must distinguish `current`, `last known`, `coarse`, and `unknown` values visually and textually. Rendering interpolation stops when a tile ceases to be live.

# Rendering truthfulness

- A rendered organism maps to one stable simulation ID.
- Interpolation must not imply authoritative positions the server could not reconcile.
- Distant aggregates are visually distinct from individuals.
- Cosmetic variation uses presentation-only inputs and never changes mechanics.
- Dead remains retain identity and remaining-consumption state.

# On-map activity pulses and organism journeys

## Activity-pulse purpose and event families

The live within-tile view gives immediate visual acknowledgement when an actual organism
does something biologically meaningful. A small screen-space icon appears at the recorded
event position for these first families:

| Family | First icon concept | Subject-relative valence | Default |
| --- | --- | --- | --- |
| Birth or reproduction | dividing cell / paired circles | positive green | on |
| Feeding, scavenging, or successful predation | consumed morsel / open jaws | positive green-teal for the consumer; negative red-orange for harmed prey | on |
| Nutrient or resource absorption | droplet with inward arrow | positive green-teal | on |
| Acute stress, failed attack, or harmful exposure | warning burst | negative orange-red | off except for the followed organism |
| Death | broken cell / skull-like mark | negative red | on |
| Migration or notable movement transition | directional wave / arrow | neutral gold | off |
| Lifecycle or behavior-state transition | phase ring / state glyph | neutral amber | off |

The event type and participant role determine valence; “good” and “bad” are relative to
the organism represented by that icon, not a moral judgment or a hidden player-fitness
score. One predation fact may therefore produce a green feeding pulse for the predator and
a red harm/death pulse for the prey when both are authorized and visible. Icon shape is the
primary meaning and color is secondary, so red/green color-vision differences do not erase
the distinction. The first valence scale moves from green/teal through gold/amber to
orange/red using perceptually distinct, contrast-checked tokens rather than raw RGB
interpolation.

These pulses may only represent completed authoritative facts. The client never infers a
birth, successful feeding, uptake, or death from a changing quantity or disappearing sprite.
It may animate a failed or suppressed action only when the server publishes that outcome as
an authorized event. Presentation metadata maps stable event family/role IDs to icons,
colors, labels, and sound hooks; mods or themes may replace that metadata without changing
event meaning.

## Animation and clutter rules

The initial presentation values are deliberately client-configurable and require visual
testing:

- A pulse enters at full opacity and readable scale at the event's recorded tile-local
  coordinate, holds for `400 ms`, then drifts upward by at most `18 CSS px` while fading
  over `2.6 s`. Its total default wall-clock lifetime is `3.0 s`.
- Fade duration is wall-clock presentation time, independent of simulation speed, tick
  duration, pause state, replay determinism, and server publication cadence. Pausing the
  simulation does not resurrect an expired pulse; the journey log is the inspection path.
- Icons remain within a bounded `16..22 CSS px` screen-space size across normal zoom levels.
  At distant aggregate zoom they become tile-level activity counts or disappear according
  to the selected layer; the renderer never invents organism locations.
- `prefers-reduced-motion` removes drift and scale motion while retaining a clear hold and
  fade. A no-animation accessibility option may show a static marker for the same lifetime.
- Repeated same-family events for one organism within one publication interval coalesce into
  one pulse with a bounded `×N` badge and magnitude tier. They do not create an unbounded
  animation queue.
- When a viewport exceeds its configured pulse budget, deterministic priority is death and
  reproduction, then feeding/harm, then absorption, then neutral transitions. Lower-priority
  events coalesce into an honest tile count; they are never randomly dropped based on frame
  timing. A followed organism's eligible pulses receive their own small reserved budget.

The preferences panel exposes a checkbox for every family, a scope of `controlled species`
(default) or `all currently observed organisms`, master enable/disable, reduced-motion, and
opacity/lifetime within safe bounds. Preferences are local profile state. They do not enter
world saves, world hashes, keyed randomness, organism decisions, or actor knowledge. A
future subscription may avoid transporting disabled high-volume families, but changing it
must not change authoritative event creation and broadening cannot recover hidden events.

## Organism journey inspector

Every authorized organism inspector has a chronological `Journey` tab. Exact landmark
entries include founding/birth, parent-child reproduction roles, lifecycle transitions,
tile migration, feeding/scavenging/predation interactions, significant health or stress
transitions, and death. Each entry carries simulation tick/date, organism age, event type,
subject role, tile and recorded position, involved stable IDs, relevant exact quantities or
outcome, and links to authorized causal evidence. Nutrient uptake and other routine repeated
work are summarized rather than logging one row per quantum:

- keep one bounded per-organism activity bucket for each of the latest `168` simulated
  hours, with sparse nonzero acquisition totals by resource family;
- compact older routine summaries into deterministic simulated-day buckets; and
- retain landmark lifecycle and interaction events exactly for the world lifetime.

World-lifetime logical retention does not require every record to remain in hot simulation
memory. Sealed old landmark/daily segments may move to a cold, organism-indexed archive;
the inspector loads them on demand while the active world retains only the index, verified
high-water mark/digest, and configured recent window.

The log explains how the organism reached its present age, reserves, committed/free
nutrient state, location, behavior, reproduction count, and eventual death without claiming
that one event exclusively caused a later condition. Parent and offspring entries link to
one another. A dead organism remains reachable through its remnant, species/lineage history,
or stable-ID search after the visible death; disappearance due only to projection loss does
not create a death entry.

Visibility remains authoritative. A currently visible organism exposes only journey facts
the actor is permitted to know. When it leaves observation, the cached journey becomes
timestamped stale history and receives no hidden updates. Entering a newly live tile does
not replay past map pulses, although authorized retained journey facts may become available
under the normal discovery policy. Omniscient histories remain limited to an explicitly
authorized post-run mode if one is added later.

# Resource and causal visualization

Plan views that can answer:

- Which tile resource is limiting this species?
- Where did a resource enter, leave, or transform?
- Which organisms or species consumed and released it?
- Why did a metabolic, reproductive, or migration attempt fail?
- How did predator attack power, prey defense, and contested-feeding priority affect a predation outcome?
- Which death risks, realized triggers, and contributing stresses are increasing?
- Why did an autonomous species favor a particular adaptation?

Views must distinguish authoritative named-compound quantities from elemental totals calculated by expanding their composition vectors. They may show both, but must not add a compound and its constituent elements as if they were separate matter.

The first executable diagnostic uses per-organism, per-compound acquisition evidence from the
last completed tick. It shows exact requested and granted quantities and separately labels
tile-supply scarcity or claim contention. For an atomic coupled reaction, all inputs show the
same granted extent, but only the input or tied inputs whose available stoichiometric extent set
that grant may be called limiting; another reduced co-input is not independent scarcity
evidence. A zero-request process blocked by light, capability, reserve capacity, or another
upstream gate requires separate activation evidence and must not be misreported as material
abundance. The first gate-evidence slices cover recurring external energy capture and
opportunity-qualified scavenging. Capture can name a missing compiled pathway, zero accessible
light, another zero environmental opportunity, or inadequate reserve room for one reaction
extent. Scavenging can name a missing capability, active cooldown, inadequate action energy, or
full particulate-intake capacity, but only when consumable remains are currently inside the
organism's interaction range. The UI must not turn absence of a target into a warning or reveal
out-of-range remains. Quantity gates carry exact available-versus-needed values and cooldown
evidence carries its exact clearing tick. Conservation behavior is not a capture gate because v1
behavior cannot suppress useful passive acquisition; later optional active-transport rules may
add an explicit behavior gate only when the simulation actually evaluates one. Persisted tile
history does not identify an organism-level cause: after restore, the client labels these two
organism diagnostics unavailable until another tick completes rather than inventing evidence from
tile stocks.

The first durable resource chart receives current exact stock plus chronological sparse flow
intervals for at most 168 simulated hours. It reconstructs earlier stock boundaries backward by
subtracting each interval's signed net flow; every tile mutation in the window is represented by
the balanced ledger, so this is exact without transmitting dense duplicate stock snapshots. The
client plots stock and signed net flow for one selected named compound, retaining `bigint` until
bounded display normalization. Empty-flow intervals remain in the series. Longer seasonal views
require server-side compaction and are deliberately deferred.

The same selected-organism surface receives a separate bounded action-gate array with at most one
first blocker for biomass growth and one for reproduction. Growth explanations distinguish missing
capability, conservation, maintenance, reserve protection, staging capacity, exact tile-resource
supply, and final-extent contention. Reproduction explanations distinguish conservation, cooldown,
health, structure, reserve, lifecycle, and commissioned-versus-free offspring micronutrient quotas.
Thresholds and stable resource names accompany reasons where they exist. A successful or partially
successful process has no blocker, and the client must not reconstruct a competing explanation from
later end-of-tick state.

The first organic-resource view distinguishes intact remains, `LabileDissolvedOrganic`, generic spent organic pools, and `ReducedFermentationProducts`. It should make the 32-versus-8 direct-digestion/dissolved-fermentation recovery hierarchy visible and explain whether fermentation is limited by substrate, contention, throughput, environment, reserve capacity, or pathway upkeep. For respiration, it separately presents fuel carbon assimilated into new reserve, carbon routed to CO2 or overflow waste, accessible O2, respiratory consumption, catalytic-quota status, charged/spent carrier balance, shared throughput, and the limiting member of a coupled claim. It must not present generic organic elemental abundance or spent reserve carriers as directly usable energy.

The photosynthesis view explains the current solar, cloud, depth, turbidity, and saturation contributions once; shows how one shared light budget was assigned between sulfide and oxygenic reactions; and pairs every oxygen output with its fixed-carbon output and CO2 input. O2 history distinguishes biological production, respiratory consumption, environmental attrition, and exchange, while an oxygenation indicator marks the basal, first-tier, and second-tier tolerance bands without implying that a threshold is universally safe.

The predation view distinguishes opportunistic contact, directed hunting, and engulfment. It explains capture range and size eligibility; attack, defense, health, pursuit, escape, and deterrence factors; exact attempt cost and probability; immediate feeding claims and remaining remnant matter; and why a hunting or fleeing target was retained or discarded. A speciation preview warns that ancestors, descendants, sibling lineages, and other controlled species are prey by default, and explains any evolved kin-discrimination exclusion. It must not imply carcass ownership or expose prey internals that the organism's senses and actor knowledge do not authorize.

Migration histories distinguish active, Brownian-like passive, and future directional environmental displacement. Species summaries expose the compiled `EnvironmentalSpread` profile and should make it possible to see whether a lineage remains clustered through anchoring, spreads through population/reproduction plus enhanced chance drift, uses directed movement, or later exploits directional environmental stages. The presentation must not imply that passive arrival was player-commanded or that environmental transport copied an organism.

The organism inspector presents relative health as an explainable physiological-condition summary rather than unexplained hit points. For live observations it should show stored energy versus current commissioned and compiled maximum capacity, storage structure versus target, any organization gate, the strongest limiting health factor, significant stress channels, age, and lifecycle phase according to [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md) and [ENERGY_STORAGE.md](ENERGY_STORAGE.md). It also shows the selected behavior and target, dwell, next-tick permissions, reserve fraction, recent energy and acquisition coverage, limiting material deficit, dominant pressure cause, and whether behavior or a hard transaction gate suppressed growth or reproduction. Candidate utilities and keyed choice details are diagnostic expansion rather than permanent map labels. Reduced or historical observations must retain their observation timestamp and must not be recomputed from hidden current state.

The tile and species inspectors show current behavior distributions as counts and percentages by behavior state. A live tile can show exact distributions for every species currently visible there. The controlled species can show an exact world-wide distribution and a per-occupied-tile breakdown. Other species summaries combine only current live-tile observations and are labeled `observed`, including the observed organism count and coverage scope; they must not imply knowledge of hidden populations. Reduced tiles may show a timestamped last-observed distribution but never a continuously updating one.

The species/evolution view presents mutation balance and current rate as population × average-health × DNA-modifier contributions, with forecasts labeled as current-rate estimates. A proposal preview shows the exact per-tile founder counts, ancestor remainder, mutation price, change complexity, post-price duplicated balance, seven-day branch cooldown, activation risks, and permanent incompatibilities before commit. For a finite-resource capability, it also distinguishes current stock from renewable flow, compares estimated replacement demand with the selected cohort, and plainly warns when a branch is likely to overshoot the observed niche. It must label this as a current-state estimate rather than a survival guarantee. Autonomous lineage events expose their pressure, material-opportunity, tile-plan, and candidate-score rationale only through the actor's authorized species/tile projection.

The planner also implements the framing contract in [PLAYER_LOOP_AND_NARRATIVE.md](PLAYER_LOOP_AND_NARRATIVE.md): immediate, maturing, conditional, and preparatory benefit timing; non-dominated proposals grouped by explanatory strategic intent; expandable prerequisite-closed milestone cards; saved goals with current-rate ETA; and side-by-side proposal comparison. Strategic-intent labels and private notes are presentation state, not simulation inputs. A preparatory node must never inherit the success language or iconography of the capability it only helps unlock; a maturing node must expose physical construction and commissioning rather than displaying its genetic maximum as present capacity.

After a player speciation, the client pins a 168-hour ancestor/descendant consequence review using authorized completed-tick aggregates. The alert inbox links directly to the evidence that caused each threshold crossing. Chronicle entries navigate to their involved lineage, tile, species, resource interval, or causal record; generated prose remains visibly distinct from a player's private hypothesis note.

# Performance strategy

Define viewport interest, object pooling, sprite batching, level of detail, update cadence, interpolation buffers, and chart downsampling. React should not create one DOM component per organism; dense world entities belong in PixiJS-managed structures.

The normalized cache and batch applier follow [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md). A batch is fully decoded and validated, applied to a draft in dependency order, and committed at one exact stream revision before React selectors or PixiJS buffers are notified. State fields are absolute replacements, histories are keyed upserts, and entity removals distinguish destruction from loss of projection. React observes coarse changed scopes; PixiJS receives bulk changed-ID/column ranges and never renders an intermediate partial batch.

# Stage-A executable surface

The browser fetches `application/x-protobuf`, decodes the generated
`ProjectionSnapshot`, and preserves all authoritative 64-bit values as `bigint`.
The initial normalized cache is keyed by stable domain IDs rather than server
dense locations. Its pure batch applier stages absolute tile/species
replacements and removals, validates exact stream/world/rules/tick/revision
continuity, then returns a new committed cache. Duplicates are harmless; gaps,
out-of-order or ambiguous operations, wrong identity, and invalid replacement
state retain the prior cache and require a fresh authorized snapshot.

The first PixiJS view distinguishes unknown, reduced, and live tile shapes and
draws each organism in a live tile exactly once from its authoritative stable-ID
projection. A live-to-reduced whole-tile replacement removes the inaccessible
organism collection from cache. This intentionally small surface proves the
thin-client, hidden-state, generated-contract, and one-to-one rendering seams;
it is not yet the playable map/inspector UI.

# Required decisions and artifacts

- [x] Stage-A Vite/TypeScript/Vitest tooling and package versions pinned; release/CI upgrade policy remains.
- [x] Stage-A normalized immutable projection-cache API; richer selectors, subscriptions, and renderer notifications grow with the playable UI.
- [x] Initial React shell/PixiJS dense-world ownership boundary.
- [ ] Camera, zoom, tile, and within-tile coordinate mapping.
- [x] Logical and generated snapshot/delta application, atomicity, replacement merge, duplicate/gap recovery, and Stage-A cache API. See [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md).
- [ ] Complete exploration-state visual language and stale-data UX; Stage-A unknown/reduced/live shapes and live-to-reduced cache eviction are implemented.
- [x] Activity-pulse families, valence/color semantics, filtering, animation, accessibility,
  clutter handling, journey contents, visibility, and first retention tiers are defined;
  first rendering/protocol slices are implemented while aggregate clutter handling and
  cold historical segments remain `UI-200` work.
- [x] First controlled-species mutation builder with prerequisite-closing selection,
  occupied-tile selection, authoritative preview, affordability/founder/cooldown summary,
  and idempotent apply transport; strategic intent, activation warnings, comparison, and
  saved goals remain.
- [x] First resource chart uses dependency-free SVG over exact `bigint` reconstruction; choose a
  chart library only if later interaction/downsampling needs justify it. The lineage graph library
  remains undecided.
- [ ] Loading, disconnect, resync, and command-error UX.
- [x] Client balance/world-profile authority boundary, final-definition cache identity, world-profile setup consequences, and modified-world disclosure; exact metadata schemas remain part of the protocol pass. See [MODDABILITY.md](MODDABILITY.md).
- [ ] Accessibility and input baseline.
- [ ] Wireframes for all primary surfaces.
- [ ] Decision-frontier, proposal-comparison, attention, consequence-review, activity-pulse,
  organism-journey, chronicle, and loss-postmortem interaction tests.
- [ ] Rendering benchmark with tens of thousands of visible entities and aggregates.

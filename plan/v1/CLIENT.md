# Browser Client

Status: scaffold

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

# Required decisions and artifacts

- [ ] Client build/package tooling and pinned versions.
- [ ] State-store/query approach.
- [ ] React/Pixi ownership boundary.
- [ ] Camera, zoom, tile, and within-tile coordinate mapping.
- [x] Logical snapshot/delta application, atomicity, merge, duplicate/gap, and renderer-notification contract; concrete state-store APIs remain open. See [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md).
- [ ] Exploration-state visual language, stale-data UX, and live-to-reduced cache tests.
- [ ] Resource-chart and lineage-graph libraries.
- [ ] Loading, disconnect, resync, and command-error UX.
- [x] Client balance/world-profile authority boundary, final-definition cache identity, world-profile setup consequences, and modified-world disclosure; exact metadata schemas remain part of the protocol pass. See [MODDABILITY.md](MODDABILITY.md).
- [ ] Accessibility and input baseline.
- [ ] Wireframes for all primary surfaces.
- [ ] Decision-frontier, proposal-comparison, attention, consequence-review, chronicle, and loss-postmortem interaction tests.
- [ ] Rendering benchmark with tens of thousands of visible entities and aggregates.

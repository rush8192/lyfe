# Server API and Client Protocol

Status: Stage-A generated projection snapshot and absolute-batch subset implemented; commands, hosted stream transport, retention, cadence, and richer gameplay schemas remain

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [INTERFACE vision](../../vision/INTERFACE.md), [player loop and narrative](PLAYER_LOOP_AND_NARRATIVE.md), [behavior and resource pressure](BEHAVIOR_AND_RESOURCE_PRESSURE.md), [state change and client synchronization](STATE_CHANGE_AND_CLIENT_SYNC.md), [moddability](MODDABILITY.md), and [technology decisions](TECHNOLOGY.md).

# Purpose

Define the authoritative server surface, Protocol Buffer schemas, command semantics, subscriptions, synchronization, and future-compatible actor model.

# Server responsibilities

- Host and advance authoritative worlds.
- Validate and order commands.
- Own pause, speed, and clock state.
- Coordinate save/load/checkpoint operations.
- Publish snapshots, deltas, events, and aggregates.
- Own per-actor exploration knowledge and filter observations before serialization.
- Isolate slow or disconnected clients from the simulation loop.
- Expose diagnostics and health without mutating the world.
- Load installed base/mod rule sources and compatible world-generation packs before world creation, expose only validated mod-set/profile combinations to setup, and pin the selected final identity for the world's lifetime.

One v1 process loads at most one active world through a `WorldHost`, but all routes, envelopes, sessions, and handles remain explicitly world-scoped. A single `WorldRunner` owns mutation; request threads enqueue candidates and receive authoritative boundary results. See [WORLD_EXECUTION_AND_OWNERSHIP.md](WORLD_EXECUTION_AND_OWNERSHIP.md).

# HTTP surface to design

Candidate operation groups:

- Server capabilities and protocol versions.
- Installed validated base/mod-set and compatible world-pack/profile metadata, bounded setup options, warnings, and certification status; clients never submit arbitrary server filesystem paths.
- World creation, listing, metadata, and deletion policy.
- World generation preview and setup confirmation.
- Save, checkpoint, load, and export/import.
- Static web-client hosting in local deployment.
- Health and diagnostics.

The detailed plan should define routes, request/response schemas, idempotency, errors, and which operations are unavailable while a world is running.

# WebSocket protocol

Design a versioned envelope containing message type, request/correlation ID where applicable, world ID, protocol version, and payload. Message families should include:

- Connection hello and capability negotiation.
- Subscribe/unsubscribe and interest updates.
- Client commands and command acknowledgements.
- Full snapshots and resynchronization.
- Tick/state deltas.
- Simulation, death, resource, and lineage events.
- Authorized organism-journey events and ephemeral map-activity pulses.
- Authorized evolution-decision summaries, attention alerts, consequence-review aggregates, and factual notable events.
- Clock/status updates.
- Errors, backpressure warnings, and graceful shutdown.

Resource, energy, ID, tick, revision, and other authoritative 64-bit quantities must preserve exact values. Protocol Buffer bindings expose them as C# `long`/`ulong` and TypeScript `bigint`; JavaScript clients must not coerce them into an unsafe IEEE-754 `number`. JSON diagnostics or HTTP shapes encode unbounded 64-bit quantities as canonical decimal strings. Presentation code may convert a separately range-checked/scaled display value to `number`.

Connection/world metadata carries the base pack, ordered balance-mod package identities, mod-set hash, world-pack/profile/options identity, compiled-world-profile hash, final mechanics/presentation/world-rules hashes, certification status, and engine-vocabulary compatibility required by the client. Definition/explanation metadata is scoped by that final identity. A client never assumes that official balance or world-profile values remain valid merely because stable definition IDs match.

Health, normalized condition factors, and probabilities use the bounded `0..1,000,000` `RatioQ` encoding and fit in Protocol Buffer `uint32` fields. The selected-organism condition projection and compact delta policy are proposed in [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md).

A live selected-organism behavior projection may expose selected behavior, authorized typed target, selection/dwell ticks, next-tick permission flags, reserve fraction, recent energy/acquisition coverage, limiting material deficit, composite pressure, and the reason an optional action is suppressed. The general behavior contract uses energy coverage up to `2.0`, so the field remains an integer fixed-point value but is not constrained to the health `0..1.0` range. Candidate utilities and keyed selection evidence belong in an on-demand diagnostic projection rather than every organism delta. Reduced and unknown tile projections expose none of this current state.

Behavior-distribution projections carry observation tick, species ID, optional tile ID, observed population, and counts by stable behavior ID; percentages are derived client-side or supplied with the same deterministic rounding rule. Live tiles may carry exact per-species distributions, and the controlled species may receive an exact world-wide aggregate plus its per-tile breakdown. Another species receives only an explicitly scoped observed aggregate over authorized live tiles. A reduced tile may carry its retained last-observed distribution and timestamp, while an unknown tile carries none. Subscription and projection choices cannot affect aggregation or organism decisions.

Live organism projections for carrier-retaining species distinguish charged reserve from zero-energy spent-carrier quantity; summing them as stored energy is a client error. Storage-capable projections also distinguish current commissioned capacity, compiled maximum capacity, storage structure/target, and any organization activation gate. Live tile/resource flow projections distinguish atmospheric O2 environmental loss/exchange, biological production, and respiratory consumption, plus fuel-specific requested, granted, and consumed extents.

Movement and migration events expose active, Brownian-like passive, and directional environmental displacement contributions plus the admitted crossing cause. The passive contribution reflects the organism's compiled environmental-spread multiplier. Directional environmental contribution is always zero under the v1 rule pack, but the protocol field is versioned now so later current- or wind-driven dispersal does not masquerade as active locomotion or require a breaking event-shape change. These fields remain subject to ordinary tile/species visibility.

Organism activity uses one stable factual vocabulary across the journey inspector and map
overlay. A retained event carries event ID, completed tick/phase, family, subject and
participant stable IDs/roles, tile and occurrence coordinate, quantities/outcome, and typed
evidence references. It does not carry localized prose, color values, glyph names, fade
duration, or player-relative fitness. The presentation pack maps family plus role to those
visuals.

The server projects retained journey landmarks and routine acquisition summaries for an
explicit organism-history interest. Separately, it may emit a bounded `ActivityPulseBatch`
for newly completed authorized events in live subscribed tiles. Pulse batches preserve
event IDs for deduplication and occurrence ticks for staleness checks, but may coalesce
same-organism/same-family routine activity and may expire under the explicit presentation
TTL. Reconnect does not replay expired animations; the journey query remains authoritative.
Neither subscription choice nor pulse filtering changes event creation, simulation state,
or future randomness.

# Command semantics

Every gameplay command needs:

- Actor/controller identity even though v1 has one human.
- Mode and authority validation.
- Server-assigned deterministic order.
- Explicit accepted/rejected result.
- Application tick.
- Replay representation.
- Idempotency behavior for retries.

```text
ReceiveCommand(connection, message):
    decode and validate protocol
    resolve actor and world
    validate mode, control, and expected state
    assign command ID and deterministic order
    enqueue for a safe simulation boundary
    acknowledge queue admission, then publish applied or rejected boundary result
```

Queue admission is not authoritative application. An applied result identifies the command, application tick, resulting world revision, and current durability state. Under the v1 persistence contract it means the running world accepted the command, not that a write-ahead log or save already made it crash-durable. Retries use an actor-scoped client command ID; the exact retention window and duplicate-result cache are fixed with the first command schema.

The first speciation command carries ancestor species ID, expected evolution revision, expected genome hash, explicit new trait IDs, one to four selected tile IDs, and the permitted sandbox follow-descendant preference. Its preview and rejection payloads expose the typed reasons defined in [EVOLUTION.md](EVOLUTION.md), exact founder counts, price, complexity, cooldown boundary, activation warnings, and resulting attribute/cost provenance. For a material-dependent proposal, the preview also exposes the authorized named-resource stock/flow inputs, replacement demand, local opportunity, and whether the selected founding cohort materially overshoots the estimated niche; this remains a warning rather than a validity gate. Autonomous phase-10 decisions enqueue the same semantic command for next-boundary application rather than mutating species state through a private path.

Proposal-preview schemas additionally carry the deterministic benefit-timing class—immediate, maturing, conditional, or preparatory—per selected tile plan, strategic-intent tags, completed versus merely opened capabilities, remaining prerequisite route, recurring costs by named channel, and relevant observable consequence fields. Maturing proposals expose current assignments, construction targets, commissioned effects, and blockers rather than equating a genetic maximum with present capacity. Intent is explanatory metadata and never authorizes the server to choose for the player. The server may expose a bounded non-dominated frontier while retaining a manual trait-set preview endpoint.

Evolution goals and proposal drafts do not reserve points or mutate a species. If stored server-side so a goal can trigger an auto-pause while the client is disconnected, they are actor planning state with explicit create/update/delete commands and revisions. An attention policy that can pause a single-player world is also authoritative actor/world state: it evaluates after a completed tick, records the resulting pause transition, and cannot use client subscriptions or presentation randomness. Future shared-clock worlds use a different policy rather than granting unilateral pause authority.

# Snapshots, deltas, and interest

The normative change-capture, per-connection projection-stream revision, absolute-patch, merge, atomic-apply, visibility-replacement, acknowledgement, and recovery rules are defined in [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md). The simulation publishes a stable-ID `TickChangeSet`, not Protocol Buffer deltas. Server projectors read final completed state, enforce actor knowledge and connection interest, and materialize typed batches at a cadence independent of ticks.

Define:

- Initial unknown/reduced map summary versus detailed live-tile snapshots.
- Stable organism IDs and delta operations.
- Visibility subscriptions by tile, species, organism, lineage, and chart.
- Update rates independent of simulation tick rate.
- Periodic resynchronization or hash checks.
- Behavior when an entity disappears outside a subscription.
- Aggregates at distant zoom without invented organisms.

Subscriptions can narrow information an actor is already authorized to receive, but cannot broaden it. The server must not serialize hidden current conditions, resource quantities, organisms, remains, or exact history and rely on the browser to mask them.

Protocol tile projections should be distinct shapes rather than one shape populated with nullable secrets:

```text
UnknownTile { tileId, gridPosition? }
ReducedTile { tileId, fixedSummary, coarseComposition, lastKnown, observedAtTick }
LiveTile { tileId, fixedState, currentState, exactStocks, flows, organisms, remains }
```

The exact map geometry exposed for unknown tiles remains a client/world-plan decision; if coordinates are visible for navigation, no environmental composition accompanies them. Visibility transitions are server events applied at completed tick boundaries. A transition from live to reduced removes live entities and exact changing state from the client's cache; reconnect and resynchronization rebuild only the actor-authorized projection.

# Backpressure and reconnect

The simulation must not wait for a slow client. State updates coalesce as absolute replacements while retained ordered events and command results preserve identity and order. Each stream uses bounded accumulators, outbound queues, acknowledgement-based retention, and resumable revisions. A missing base, incompatible interest/authorization change, invalid chunk, or exhausted retention requires a fresh actor-authorized projection snapshot. Exact byte/time limits and disconnect thresholds remain to be benchmarked.

# Future multiplayer constraints

V1 need not implement accounts, matchmaking, or competitive rules. It must avoid global-singleton assumptions for actor identity, control assignments, command attribution, subscriptions, and shared clock policy.

A future multiplayer world pins one server-selected mod set and world profile. Clients acknowledge their identities and required presentation/engine vocabulary during connection, but do not upload packages or participate in authoritative rule compilation. Server discovery, mod distribution, and competitive eligibility remain future service policy.

# Stage-A generated contract

The checked-in `lyfe.v1` Protocol Buffer source owns world lifecycle, organism
lifecycle, population scope, exact resource stocks, organisms, species, explicit
unknown/reduced/live tile variants, the actor world projection, projection
snapshot, and absolute projection batch. `Google.Protobuf 3.36.1` and
`Grpc.Tools 2.83.0` generate C#; Buf CLI `1.72.0` and `protoc-gen-es 2.14.1`
generate TypeScript. Generated authoritative `uint64`/`sint64` fields are
`ulong`/`long` in C# and `bigint` in TypeScript.

The native server exposes the current actor-authorized full snapshot as
`application/x-protobuf` at `/api/v1/worlds/active/projection`. This is the
Stage-A read seam, not yet a retained subscription stream. `NET-400` owns live
transport cadence, acknowledgements, bounded retention, backpressure, and resume.

# Required decisions and artifacts

- [x] Versioned `lyfe.v1` package, exact C#/TypeScript generators, pins, generation commands, and native `bigint` mapping.
- [ ] HTTP operation catalogue.
- [ ] WebSocket envelope and message catalogue.
- [ ] Command ordering and idempotency.
- [ ] Subscription and interest model.
- [ ] Organism-journey/activity-pulse, decision-summary, evolution-goal, attention-policy,
  alert, notable-event, and consequence-review schemas and retention behavior.
- [x] Direct actor-authorized unknown/reduced/live projection oracle plus generated Stage-A field numbers and binary snapshot mapping; richer entity/history shapes remain with their authoritative stores. See [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md).
- [x] Stage-A absolute tile/species replacement schema, stream revisions, atomic TypeScript application, visibility eviction, and full-snapshot resynchronization; transport limits remain `NET-400`. See [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md).
- [ ] Queue, retention, batch, chunk, cadence, and backpressure limits; bounded/coalescing behavior is fixed.
- [ ] Local deployment/startup flow.
- [x] Server-selected installed mod-set and world-profile boundary with required world/handshake identity; exact HTTP/Protocol Buffer fields remain with the general schema pass. See [MODDABILITY.md](MODDABILITY.md).
- [ ] Sequence diagrams for connect, command, save, reconnect, and shutdown.

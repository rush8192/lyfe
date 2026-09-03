# Server API and Client Protocol

Status: scaffold

Sources: [SIMULATION vision](../../vision/SIMULATION.md), [INTERFACE vision](../../vision/INTERFACE.md), and [technology decisions](TECHNOLOGY.md).

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

# HTTP surface to design

Candidate operation groups:

- Server capabilities and protocol versions.
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
- Clock/status updates.
- Errors, backpressure warnings, and graceful shutdown.

Resource and energy quantities are authoritative signed 64-bit integers. Protocol schemas must preserve their exact values; JavaScript clients must not coerce them into an unsafe IEEE-754 `number`. The protocol/code-generation decision must establish whether these fields arrive as `bigint`, strings, or generated long wrappers.

Health, normalized condition factors, and probabilities use the bounded `0..1,000,000` `RatioQ` encoding and fit in Protocol Buffer `uint32` fields. The selected-organism condition projection and compact delta policy are proposed in [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md).

Live organism projections for carrier-retaining species distinguish charged reserve from zero-energy spent-carrier quantity; summing them as stored energy is a client error. Storage-capable projections also distinguish current commissioned capacity, compiled maximum capacity, storage structure/target, and any organization activation gate. Live tile/resource flow projections distinguish atmospheric O2 environmental loss/exchange, biological production, and respiratory consumption, plus fuel-specific requested, granted, and consumed extents.

Movement and migration events expose active, Brownian-like passive, and directional environmental displacement contributions plus the admitted crossing cause. The passive contribution reflects the organism's compiled environmental-spread multiplier. Directional environmental contribution is always zero under the v1 rule pack, but the protocol field is versioned now so later current- or wind-driven dispersal does not masquerade as active locomotion or require a breaking event-shape change. These fields remain subject to ordinary tile/species visibility.

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
    acknowledge accepted application tick or rejection
```

The first speciation command carries ancestor species ID, expected evolution revision, expected genome hash, explicit new trait IDs, one to four selected tile IDs, and the permitted sandbox follow-descendant preference. Its preview and rejection payloads expose the typed reasons defined in [EVOLUTION.md](EVOLUTION.md), exact founder counts, price, complexity, cooldown boundary, activation warnings, and resulting attribute/cost provenance. For a material-dependent proposal, the preview also exposes the authorized named-resource stock/flow inputs, replacement demand, local opportunity, and whether the selected founding cohort materially overshoots the estimated niche; this remains a warning rather than a validity gate. Autonomous phase-10 decisions enqueue the same semantic command for next-boundary application rather than mutating species state through a private path.

# Snapshots, deltas, and interest

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

The simulation must not wait for a slow client. Plan bounded outbound queues, coalescing or replacement of stale deltas, event-retention windows, disconnect thresholds, reconnect tokens, and mandatory full resynchronization cases.

# Future multiplayer constraints

V1 need not implement accounts, matchmaking, or competitive rules. It must avoid global-singleton assumptions for actor identity, control assignments, command attribution, subscriptions, and shared clock policy.

# Required decisions and artifacts

- [ ] Protocol package/version strategy and code generator.
- [ ] HTTP operation catalogue.
- [ ] WebSocket envelope and message catalogue.
- [ ] Command ordering and idempotency.
- [ ] Subscription and interest model.
- [ ] Actor-authorized unknown/reduced/live projection schemas and cache-eviction rules.
- [ ] Delta representation and resynchronization.
- [ ] Queue limits and backpressure.
- [ ] Local deployment/startup flow.
- [ ] Sequence diagrams for connect, command, save, reconnect, and shutdown.

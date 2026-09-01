# Spatial Organisms and Behavior

Status: first v1 spatial semantics, entity-placement, indexing, movement-boundary contract, and numerical scale; movement energy, migration odds, and behavior policy pending

Sources: [organism mechanics](ORGANISMS.md), [simulation loop](SIMULATION_LOOP.md), [organism state](ORGANISM_STATE_AND_HEALTH.md), [world and climate](WORLD_AND_CLIMATE.md), [trait catalogue](TRAIT_CATALOGUE.md), [spatial calibration](SPATIAL_CALIBRATION.md), and [WORLD vision](../../vision/WORLD.md).

# Purpose

Define what tile-local position means in v1, how organisms and remains become eligible for direct interaction, how movement crosses a tile boundary, and which spatial observations may inform next-tick behavior. The contract must remain deterministic and performant for tens of thousands of individually simulated and displayed organisms without pretending that well-mixed tile resources already form local gradients.

# V1 spatial boundary

Within-tile coordinates have three authoritative uses in v1:

1. Determine proximity among living organisms and cohesive dead remains for detection, predation, scavenging, and later direct-contact capabilities.
2. Determine whether movement reaches an edge and can attempt migration into the edge-sharing tile.
3. Give the thin client stable one-to-one positions for live organisms and remains.

Position does **not** affect access to abiotic gases, dissolved nutrients, temperature, light, moisture, or other tile-wide conditions in v1. Every organism in a tile experiences the same environmental state and submits ordinary environmental-resource claims against the same pools. Resource success may depend on DNA, health, lifecycle, behavior, and tile conditions, but not distance to an invented resource point.

The resource and sensing interfaces should nevertheless accept a tile-local position. The v1 implementation ignores that argument for tile-wide fields; a future localized micronutrient field can use it without changing organism action interfaces or authoritative entity coordinates.

# Spatial entities

Only entities that participate in direct local interaction require authoritative within-tile geometry:

```text
SpatialEntityRef:
    kind                    # Organism | Remnant in v1
    stable_id
    tile_id
    position_x_q
    position_y_q
    body_radius_q           # derived, not independent stored state
```

Living organisms store tile and position through their normal organism state. Remnants store their own tile and the exact position at which death occurred. Radius is derived from species body-scale effects plus current structure for an organism, or remaining accounted contents and its original packing profile for a remnant. The first numerical radius curve is defined in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md) and remains versioned balance data rather than writable organism state.

V1 does not model rigid-body collision, volume exclusion, pushing, line of sight, orientation, or occlusion. Multiple entities may occupy the same coordinate. A radius describes biological size and interaction reach; it is not a solid physics collider. This avoids turning a well-mixed ecological simulation into a microscopic fluid or crowd solver.

Tile-wide resource reservoirs, atmospheric gases, environmental sources, and climate values are not spatial entities. A later resource-gradient implementation should be a field sampled at coordinates, not millions of invented resource particles.

# Coordinate and distance contract

Each tile is the normalized half-open square `[0, 1) × [0, 1)`. Authoritative coordinates use the world plan's unsigned `LocalCoordQ` encoding:

```text
LOCAL_ONE = 2^32
position_x_q, position_y_q in [0, LOCAL_ONE)

LocalDeltaQPerHour:
    signed 64-bit count of LocalCoordQ units per simulated hour
```

One tile width has no promised kilometer or organism-length conversion. DNA and balance configuration express speed, body radius, sensing range, and interaction reach as fractions of a tile. The one-hour default tick converts velocity into a checked signed displacement; another configured tick duration uses an exact rational scale and named rounding.

Same-tile distance uses Euclidean squared distance from signed coordinate differences:

```text
dx = signedWide(a.position_x_q) - signedWide(b.position_x_q)
dy = signedWide(a.position_y_q) - signedWide(b.position_y_q)
distanceSquared = dx * dx + dy * dy

WithinRange(a, b, rangeQ):
    return distanceSquared <= squareWide(rangeQ)
```

The implementation uses checked 128-bit intermediates. It compares squared values and does not require a runtime square root. No local-coordinate wrapping occurs inside a tile.

The first founder radius, body-scale curve, Brownian RMS, contact/detection/action ranges, spawn distance, active-speed ceilings, and encounter-rate targets are fixed as provisional rule-pack values in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).

# Interaction locality

Direct interactions are same-tile only in v1. Two entities on opposite sides of a tile boundary cannot detect, prey upon, scavenge, exchange matter, or use any other entity-targeted capability across that boundary, regardless of their geometric distance. An organism must cross through movement, become a member of the destination compartment, and then participate in post-movement interactions there.

This treats tile edges as environmental-compartment boundaries and avoids ambiguous attacks whose participants experience different tile resources or conditions. It also makes “near an edge” necessary but not sufficient for migration: the organism's movement must actually reach that edge and pass the migration check.

Interaction eligibility separates body size, sensing, and action reach:

```text
contactRange(a, b) = a.bodyRadius + b.bodyRadius + contactMargin
predationRange(predator, prey) = predator.captureReach + prey.bodyRadius
scavengeRange(scavenger, remnant) = scavenger.feedingReach + remnant.radius
detectionRange(observer, target) = compiled sense-specific range
```

Every range is a compiled DNA/lifecycle value subject to configured bounds. Detection does not authorize consumption or attack; it only exposes an eligible observation to behavior. Predation or scavenging still requires the matching action capability, affordability, size/processing compatibility, a selected target, and the simulation loop's one-targeted-interaction limit.

V1 performs no line-of-sight or pathfinding test inside a tile. Exact distance is the final geometric filter after spatial-index candidate retrieval.

# Spatial index

The first v1 index is a uniform `16 × 16` bin grid per tile. A bin is a performance accelerator, never authoritative state:

```text
binX = min(15, floor(position_x_q * 16 / LOCAL_ONE))
binY = min(15, floor(position_y_q * 16 / LOCAL_ONE))
binIndex = binY * 16 + binX
```

At the 100,000-organism reference workload and 544 default tiles, this is about 184 organisms per tile and less than one organism per bin on average before ecological clustering. Queries visit every bin intersecting the search-radius bounding box, then apply the exact squared-distance and eligibility filters. Ranges larger than one bin remain valid; they simply inspect more bins.

Index construction uses canonical tile order, bin order, entity kind, and stable entity ID. It must produce the same candidate order regardless of dense-slot layout or worker scheduling. Query consumers must either retain that order or apply their own documented stable ordering before keyed target selection.

The logical tick exposes two immutable views:

1. `ExternalInteractionIndex`, built after intrinsic deaths and movement. It includes surviving post-movement organisms, old remains that survived decay, and intrinsic-death remains. Phase-6 predation remains are intentionally absent until the next external-resolution snapshot.
2. `BehaviorObservationIndex`, built or deterministically updated after lifecycle resolution. It represents the completed local entity state used when surviving organisms select behavior for the next tick. It may expose newborns and new predation/metabolic-death remains even though those entities cannot act retroactively.

Structure growth during internal metabolism and parent/offspring allocation during lifecycle resolution may change derived radii. Those changes do not retroactively alter the already-resolved external-interaction snapshot. They appear in `BehaviorObservationIndex` and become eligible for contact, capture, or scavenging queries during the next tick. Remnants created before phase-5 intent evaluation use their then-current radius in the current tick as usual.

An implementation may reuse storage or apply deterministic deltas instead of rebuilding twice, but it must reproduce these two logical snapshots. Index data is derived and is rebuilt after load; saves do not persist bins.

# Placement and lifetime transitions

## Abiogenesis and setup

Founders receive positions from keyed uniform tile-local draws in stable organism-ID order. Position draws use a named setup domain separate from biology outcomes and cosmetic variation. Founder active velocity is zero unless its compiled DNA explicitly grants locomotion. Eligible founders still receive the ordinary phase-local Brownian displacement defined below; Brownian motion is not stored as persistent velocity.

No start-only central cluster or edge exclusion is required for correctness. A scenario may configure an inset for balance experiments, but it must be visible rule data and cannot silently alter migration odds.

## Reproduction

The continuing parent retains its position. The offspring is placed by a keyed angle and distance inside a DNA/configured spawn radius around the parent. Placement reflects back inside the same half-open tile bounds rather than causing migration during reproduction. Reproduction may create overlapping entities and never fails solely because no empty geometric space exists.

The placement draw occurs only after the zero-sum reproduction transaction is accepted and the offspring ID is known. Failed reproduction consumes no placement randomness. A newborn is visible in the end-of-tick behavior observation but cannot move, interact, or select a new behavior until its next eligible tick.

## Speciation

Speciation changes selected organisms' species membership without moving them. Their exact tile, position, velocity, behavior, target validity, and concrete internal state remain unless the compiled descendant DNA makes an active state invalid; any such invalidation follows an explicit species-transition rule rather than spatial replacement.

## Death and remains

Every death creates one cohesive remnant at the organism's exact terminal position:

- Intrinsic death uses the pre-movement position because it occurs before movement.
- Predation, maintenance failure, and other later death use the organism's post-movement position.
- Partial consumption and decay reduce the remnant's accounted contents; its derived radius may shrink accordingly.
- Decay releases matter into the tile-wide organic pools of the remnant's current tile.

Remnants are stationary in v1. Passive remnant drift, sinking, attachment, and cross-tile transport are future mechanics. Removing all remaining contents removes the remnant entity atomically.

# Movement and migration

V1 distinguishes selected active velocity, passive Brownian displacement, and actual displacement. Behavior chosen at the end of tick `T` supplies the desired active movement state for tick `T + 1`. Phase 4 derives the tick's Brownian component, caps paid active movement by DNA speed, environment/lifecycle modifiers, and affordable energy, then combines the two components for boundary tracing.

```text
ResolveMovement(organism, desiredVelocity, tickDuration, environment):
    activeRequested = ScaleVelocity(desiredVelocity, tickDuration)
    activeCapped = ClampToCompiledSpeedAndAffordableDistance(activeRequested)
    brownian = SampleBrownianDisplacement(organism, tick, environment)
    requested = CheckedVectorAdd(activeCapped, brownian)
    trace = TraceToFirstTileBoundary(position, requested)

    if trace reaches no boundary:
        debit cost for the full admitted active component only
        commit trace.endpoint and active velocity
        return

    debit active cost in proportion to active displacement actually realized
    if migration is ineligible or its keyed attempt fails:
        resolve active failure or Brownian reflection as declared below
        return

    enter the edge-sharing tile at the corresponding opposite edge
    apply permitted remaining tangential displacement
    stop before any second boundary crossing
    debit any remaining realized active cost
    commit new tile, position, and active velocity
```

An organism migrates across at most one edge per tick in v1. Compiled v1 active and Brownian movement bounds must therefore keep their combined requested displacement below one tile width per tick; the trace still handles diagonal movement and exact corner ties. If `x` and `y` boundaries are reached at the same rational time, `x` is the stable tie-break. Crossing east/west uses the world's wrapped neighbor; crossing beyond the north/south boundary is always ineligible.

Migration has two layers:

1. Hard eligibility rejects absent neighbors and habitat transitions the organism cannot physically occupy, such as an aquatic founder entering dry land without a matching capability.
2. A rule-pack probability may combine edge compatibility, crossing class, movement capability, lifecycle, health, and environmental stress. A nonzero admitted active component uses the active-attempt curve; a purely Brownian crossing uses the passive-permeability curve. Each actual boundary attempt receives one keyed draw.

Failure leaves the organism in the source tile. A failed purely Brownian crossing reflects the unused normal component back into the source tile so unbiased random motion does not accumulate organisms against an impermeable boundary. An active failed attempt stops just inside the boundary and retains only permitted tangential movement. Exact migration probabilities, active attempt cost, and environmental compatibility curves remain the next numerical decisions. Direct interactions use the organism's final post-movement tile and position.

# Brownian and other passive movement

Eligible organisms receive a small Brownian-like displacement on every tick, including organisms without `ActiveMotility`. It represents directionless local agitation and diffusion, not a simulated current. Because tiles have no physical length scale, its magnitude is a gameplay-calibrated random-walk abstraction rather than a claim to reproduce a literal molecular diffusion coefficient:

```text
SampleBrownianDisplacement(organism, tick, environment):
    directionIndex = KeyedUniform(
        worldSeed, tick, organismId, BrownianDirection,
        fixedAntipodalDirectionTable.count)
    magnitudeDraw = KeyedDraw(
        worldSeed, tick, organismId, BrownianMagnitude)
    direction = fixedAntipodalDirectionTable[directionIndex]
    magnitude = BrownianMagnitudeCurve(magnitudeDraw,
                                       compiledBodyScale,
                                       mediumClass,
                                       lifecycle,
                                       tickDuration)
    return FixedPointVector(direction, magnitude)
```

The direction table contains exact antipodal pairs selected with equal probability, and the magnitude draw is independent of direction. Each axis therefore has exactly zero expected signed displacement under the discrete rule rather than merely approximating zero through runtime trigonometry. The keyed domains include organism ID and tick, so organisms do not move in lockstep and the result is independent of iteration order, save/load, client observation, or worker count.

The configured Brownian scale is expressed per square-root hour: displacement variance grows linearly with elapsed simulated time, so typical step magnitude scales with `sqrt(tickDurationHours)` rather than linearly like velocity. V1's one-hour tick makes that factor one. Other tick durations use a versioned fixed-point lookup or precompiled coefficient and require rerunning encounter and migration fixtures.

Brownian displacement:

- carries no direct active-movement energy charge; baseline passive maintenance already accounts for ordinary existence in the medium;
- does not create or modify the organism's persistent active velocity;
- normally decreases as compiled body scale increases and may be reduced to zero by attachment, resistant dormancy, or another declared anchoring effect;
- has no directional environmental bias and cannot be used as a resource-gradient signal;
- may reach a tile edge and attempt a passive crossing through the ordinary hard habitat gates and a configured passive-permeability probability;
- is bounded with active displacement so an organism crosses at most one edge per tick.

Evolved locomotion remains valuable because it adds controllable direction, greater speed, pursuit/escape effects, and reliable edge access. Brownian motion alone produces slow diffusive spread and cannot deliberately seek prey, remains, resources, or favorable neighboring tiles.

Remnants remain stationary in v1. Directed currents, sinking, buoyancy, surface attachment, and other coherent passive transport are future mechanics represented through a separate `EnvironmentalDisplacement` component. Any future component must use the same boundary trace, migration eligibility, conservation rules, and deterministic key domains; it cannot teleport an entity between tile centers.

# Spatial observation for behavior

Behavior does not receive raw access to every spatial entity in a tile. It receives a capability-filtered observation built from the completed state:

```text
LocalObservation:
    selfCondition
    tileConditionSignals       # only signals exposed by compiled senses
    neighboringTileSignals     # only with directional/environmental sensing
    detectedOrganisms[]        # range and classification limited
    detectedRemnants[]         # requires remnant-detection capability
    currentTargetStatus?
    reachableEdges[]
```

`ContactDetection` exposes contact-range entities without classifying distant targets. Later senses increase range or distinguish organism, prey, threat, remnant, and directional environmental signals. Tile-wide chemical sensing observes permitted tile-level values; it does not fabricate a direction toward a well-mixed resource. Directed environmental sensing may compare the current tile with permitted neighboring-tile summaries and choose an edge, but it does not reveal hidden exact conditions beyond the sensing capability.

A behavior target stores a stable typed entity ID, edge, or tile-local point plus the tick at which it was selected. At intent evaluation, the target is resolved against the current post-movement snapshot. Missing, consumed, out-of-range, newly ineligible, or different-tile targets cause the targeted intent to be omitted; they never redirect implicitly to a different entity. Behavior may select a new target at phase 9.

The exact behavior-state set, priority/utility selection, target sampling, conservation thresholds, and stochastic exploration policy remain the next deep-dive section. Whatever policy is chosen must use keyed draws and stable candidate ordering rather than spatial-index iteration accident.

# Future localized fields

Future micronutrient gradients should add a field provider behind the existing environment query:

```text
SampleResourceAvailability(tileId, resourceId, position) -> amount/concentration
SampleResourceGradient(tileId, resourceId, position) -> optional direction
ApplyLocalizedTransfer(tileId, resourceId, position, amount)
```

In v1, the first call resolves the tile-wide pool, the second returns no gradient, and the third applies to the tile-wide ledger. No v1 behavior may infer a direction from a uniform field. A later implementation must define diffusion, field resolution, conservation between cells, and client visibility before enabling gradient-aware traits.

# Determinism, save, and protocol implications

- Tile, position, velocity, current behavior, and typed target are authoritative and saved.
- Derived body radius and index bins are recomputed from saved state and the pinned rule pack.
- Stable IDs, not dense slots or bin offsets, cross save or protocol boundaries.
- A live tile projection may send exact entity positions. Reduced and unknown projections send no current organisms or remains.
- Server interest management may change transmission cost but cannot change index construction, sensing, target selection, or simulation outcomes.
- Cosmetic offsets or animation interpolation are presentation-only and must not feed back into authoritative coordinates.

# First validation set

- Coordinate encode/decode, exact-distance boundaries, and `16 × 16` bin coverage have property tests at zero, bin edges, tile edges, and maximum values.
- Indexed queries return exactly the same eligible set as an exhaustive same-tile scan for randomized entity layouts and radii.
- Different dense layouts, worker counts, and save/reload produce identical candidate ordering, targets, movement, and hashes.
- Same-tile entities interact at exactly the configured inclusive range; entities in different tiles never interact directly even when adjacent across an edge.
- Every migration begins with an actual active or Brownian boundary intersection, crosses only an edge-sharing neighbor, wraps only in `x`, and crosses at most one edge per tick.
- A failed or ineligible migration remains in the source tile and never occupies an invalid coordinate.
- Over the complete direction table, Brownian displacement has exactly zero signed expectation on each axis; pinned-seed populations show diffusion without systematic preferred direction.
- Brownian outcomes are identical across dense iteration orders, worker counts, save/load, and client observation, and consume no active-movement energy.
- Reproduction conserves matter, retains the parent position, and places the offspring deterministically inside the same tile.
- Every death creates one remnant at the correct phase position; partial consumption and decay conserve contents and remove an empty remnant exactly once.
- Tile-wide resource outcomes are invariant under arbitrary organism-position permutations when no direct organism/remnant interactions occur.
- Client visibility and camera-follow requests never alter spatial outcomes.

# Decisions fixed by this pass

- [x] Organism/remnant interaction, edge migration, and truthful rendering are the only v1 uses of within-tile position.
- [x] Abiotic conditions and resources remain tile-wide and well mixed.
- [x] Organisms and cohesive remnants are the only v1 indexed spatial entity kinds.
- [x] All entity-targeted interaction is same-tile only; crossing movement precedes interaction.
- [x] Normalized `LocalCoordQ` positions, signed per-hour local velocity, and squared-distance comparison.
- [x] No rigid collision, occlusion, or line-of-sight simulation.
- [x] Uniform `16 × 16` derived bins with exact post-filtering and stable ordering.
- [x] Separate post-movement interaction and end-of-tick behavior-observation views.
- [x] Deterministic setup, offspring, speciation, death, and remnant-placement semantics.
- [x] Stationary remnants and default zero-mean Brownian organism displacement in v1; no coherent passive drift or current.
- [x] At most one edge migration per organism-tick, with `x` corner tie-break and bounded `y`.
- [x] Position-aware resource interface retained for future localized fields without enabling gradients in v1.
- [x] First founder body radius, structure-to-radius curve, Brownian RMS, interaction/sensing ranges, reproduction placement, and active-distance ceilings; see [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).

# Next decisions

1. Active movement energy, acceleration, and turning values for the proposed speed ceilings.
2. Migration hard gates, active and passive compatibility curves, attempt cost, and final probability bounds.
3. Baseline behavior states and the deterministic/stochastic selection model.
4. Target selection within sensed candidates, including whether organisms prefer nearest, weakest, richest, or a weighted keyed sample.
5. Capture-reach upgrades, predation/scavenging size compatibility, and non-founder remnant packing profiles.
6. Spatial performance acceptance at clustered—not merely uniform—100,000-organism workloads.

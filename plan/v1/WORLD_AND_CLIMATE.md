# World, Generation, and Climate

Status: scaffold

Sources: [WORLD vision](../../vision/WORLD.md), [SIMULATION vision](../../vision/SIMULATION.md), and [GAMEPLAY vision](../../vision/GAMEPLAY.md).

# Purpose

Translate fixed geography, climate baselines, current conditions, world generation, and cross-tile exchange into deterministic algorithms and state.

# V1 world topology

- The world is a finite rectangular grid.
- The east-west `x` dimension wraps.
- The north-south `y` dimension is bounded.
- Only edge-sharing tiles are neighbors.
- `y = 0` is the equator; signed distance informs latitude and seasons.
- Positive elevation is terrestrial; negative elevation is aquatic and determines depth.
- Tiles are environmentally well mixed in v1.

The detailed plan must define coordinate ranges, neighbor lookup, behavior at bounded `y` edges, and within-tile coordinate transfer when crossing an `x` seam.

# Fixed and baseline state

Specify the generated representation for:

- Elevation, terrain classification, and water depth.
- Monthly temperature normals.
- Monthly precipitation normals.
- Terrestrial surface-moisture baseline.
- Volcanic activity baseline and variation.
- Nutrient and atmospheric starting reservoirs.
- Geological, atmospheric, and volcanic source/sink parameters.
- Latitude-derived insolation.

# Current conditions

Design temporally coherent updates for:

- Temperature.
- Precipitation.
- Surface moisture.
- Cloud cover or another explicit weather term used to attenuate sunlight.
- Aquatic turbidity if included in v1.
- Insolation at the surface and at aquatic depth.
- Volcanic activity and rare destructive events.
- Global annual deviations.

Independent random sampling every hour would produce implausible noise. The detailed plan should define correlated weather evolution, interpolation between monthly normals, bounds, and how seasonal state survives save/load.

# World generation pipeline

```text
GenerateWorld(seed, dimensions, rulePack):
    establish coordinate and latitude ranges
    generate elevation field with x wrapping and bounded y edges
    derive land, ocean, and water depth
    assign correlated terrain and volcanic structure
    derive climate baselines
    initialize nutrients and atmospheric gases
    validate sandbox founding tiles and paired survival regions
    repair or regenerate invalid worlds deterministically
    return world plus generation diagnostics
```

The detailed pass must replace each line with algorithms, ordering, random-stream ownership, validation, and performance expectations.

# Cross-tile environment exchange

The first gas-specific source, sink, and exchange rates are fixed in [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md). Gas processing uses source, environmental attrition, symmetric exchange from a stable post-sink view, and then biological claims. Local volcanic fuels use low exchange relative to attrition; stable gases use 10% per-edge exchange and spread broadly through the finite neighbor graph.

Generated worlds use one seeded global N₂ target and one seeded global CO₂ target rather than per-tile background noise. N₂ begins uniform and conserved. A diffuse, explicitly accounted carbon-cycle boundary source maintains the CO₂ target against its environmental sink, while volcanic sources create local excess. The authoritative initial field is produced with bounded deterministic iteration over the normal gas phases; exact linear solving is only an offline diagnostic.

Gas transport itself does not depend on water depth. For biological claims, terrestrial organisms and vent-class gases have full access; atmospheric aquatic access declines as `100 / (100 + depthMeters)`; and mixed-origin CO₂/CH₄ receive full access in a volcanic tile that emits them. This preserves one conserved tile reservoir while deferring explicit atmospheric/dissolved phase partitioning.

Further exchange work remains for:

- Optional weather-dependent or directional atmospheric transport after v1.
- Dissolved or mobile nutrient resources.
- Solid or poorly transported nutrient resources.
- Passive organism transport if present in v1.

Exchange must be symmetric or explicitly directional, mass-balanced, stable for the selected tick duration, and independent of worker ordering. Generated worlds initialize gas fields from the equilibrium of the complete source graph rather than assigning every volcanic tile its isolated plateau.

# Starting-world validation

World generation must guarantee at least one volcanic ocean tile viable for each permitted founder and at least one paired survival region. A pair consists of edge-sharing volcanic ocean tiles: one viable for hydrogen acetogenesis and one shallow and sufficiently illuminated for sulfide anoxygenic phototrophy. Pair orientation and which member the player selects may vary, but choosing either metabolism must reserve a deterministic eligible neighbor for the other. Decide whether to construct this guarantee, repair a generated world, or retry with deterministic sub-seeds.

Eligibility means viable for the selected founding DNA, not broadly favorable. The [hydrogen fixture](ONE_TILE_STARTING_CONFIGURATION.md) combines primitive gas substrates with sulfur toxicity, weak light, and missing advanced-pathway micronutrients. The [sulfur fixture](SULFUR_TILE_STARTING_CONFIGURATION.md) defines the complementary shallow, illuminated, H₂S-driven profile, while [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md) defines the paired-region rules. World generation should vary the exact pressures while preserving both openings and ensuring neither starting tile is a generally superior refuge.

# Exploration state

World simulation always resolves every tile. Observation is a separate authoritative projection and must not reduce hidden-tile fidelity.

At each completed tick, compute the live tile set for an actor from the occupied tiles of its currently controlled species. Promote newly occupied tiles to live, add their edge-sharing neighbors to the discovered set, update last-known records, and demote vacated tiles to reduced. The player's starting tile and neighbors receive the same transitions at tick zero.

```text
UpdateKnowledge(actor, completedWorldView):
    newLive = OccupiedTiles(actor.controlledSpecies)
    newlyDiscovered = newLive union EdgeNeighbors(newLive)
    actor.discoveredTiles union= newlyDiscovered

    for tile in newLive in canonical tile order:
        actor.lastObserved[tile] = BuildLiveObservation(tile, completedWorldView)

    actor.liveTiles = newLive
```

Reduced neighbor summaries should be derived from generated fixed attributes and deliberately coarse bands for baselines and resource composition. They must not be recalculated from exact current hidden state. Previously live tiles retain their last observation and `observedAtTick`; they do not silently update until live again.

# Required decisions and artifacts

- [ ] Coordinate bounds and grid-size defaults.
- [ ] Elevation/terrain generation algorithm with seam tests.
- [ ] Climate model and weather correlation.
- [ ] Surface-moisture response and decay model.
- [ ] Insolation/calendar equations and units.
- [ ] Volcanism and global-event model.
- [x] First atmospheric-gas exchange equations, rates, plateau targets, and stability constraints; see [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- [x] Initial N₂/CO₂ background targets, deterministic iterative initialization, and first gas-accessibility curve; see [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- [ ] Starting-tile eligibility and deterministic repair.
- [ ] Paired-region construction, eligibility, and fairness diagnostics.
- [ ] Reduced tile-summary schema and coarse-band thresholds.
- [ ] Player-knowledge update algorithm and visibility transition tests.
- [ ] Maps and plots demonstrating representative generated worlds.

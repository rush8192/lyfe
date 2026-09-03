# World, Generation, and Climate

Status: first topology, generation, calendar, climate algorithms, numerical world-profile candidate, and external world-pack boundary; representative-map validation remains provisional

Sources: [WORLD vision](../../vision/WORLD.md), [SIMULATION vision](../../vision/SIMULATION.md), [GAMEPLAY vision](../../vision/GAMEPLAY.md), [moddability](MODDABILITY.md), and [numerical calibration](WORLD_CLIMATE_CALIBRATION.md).

# Purpose

Translate fixed geography, climate baselines, current conditions, world generation, and cross-tile exchange into deterministic algorithms and state.

# Decisions fixed by this pass

- The default v1 world is `32 × 17 = 544` tiles. Width and odd height remain scenario data, but v1 validation requires at least `8 × 5` and an odd height so one row is exactly the equator.
- `x` wraps and `y` is bounded. Only north, south, east, and west neighbors interact directly.
- Tile-local positions use normalized fixed-point coordinates rather than a claimed physical kilometer scale.
- A v1 calendar has 24-hour days, 360-day years, twelve 30-day months, and an Earth-like provisional axial tilt of `23.5°`.
- A selected starting tile begins at local dawn on the day-zero equinox. This chooses a world time offset; it does not alter generated geography, climate baselines, or resource endowments.
- Elevation uses deterministic periodic layered fields and a target aquatic fraction rather than a hand-authored map.
- Current weather comes from spatially correlated, temporally interpolated keyed fields. Tiles never draw independent hourly weather noise.
- Generated worlds use a hybrid guarantee: generate naturally, deterministically repair the best near-valid starting regions within strict bounds, and retry with a derived sub-seed if bounded repair cannot satisfy the scenario.
- Fixed geography and climate normals are authoritative generated state. Most current conditions are deterministic functions of those baselines, seed, and time; terrestrial surface moisture remains authoritative state because it integrates prior precipitation and evaporation.
- The generator algorithms and hard safety contracts live in engine vocabulary, while concrete geography, climate, atmosphere, tile-endowment, and start-repair settings come from one selected, validated world-generation profile.
- The official primordial-Earth-like profile uses the same closed world-pack format available to local external packages. A world pins its pack, profile, normalized options, seed, and compiled profile hash before generation.

The world size, calendar length, axial tilt, aquatic fraction, climate coefficients, and repair-count target are first world-profile values rather than engine constants.

# World-profile ownership and selection

World generation is driven by three separate inputs with different owners:

```text
CompiledRuleSet                 // resources, exposures, biological rules, engine vocabulary
CompiledWorldGenerationProfile // physical opportunity landscape and continuing environment rules
WorldSetup                     // seed plus profile-permitted player choices
```

The profile is loaded from exactly one `WorldGenerationPack` as defined in [MODDABILITY.md](MODDABILITY.md). It is a complete record, not a chain of inherited presets or an ordered patch stack. It may configure existing algorithms and existing resource/exposure definitions, but cannot introduce code, a new resource kind, or a new topology semantic.

This separation lets the same biology run on multiple coherent worlds—such as wetter, colder, more volcanic, or nutrient-fragmented profiles—without copying biological rules. It also lets a given world profile be exercised against alternate balance sets. Compatibility is established by API/registry checks followed by whole-combination validation; a profile author's tested mechanics hash is informative rather than permission to skip validation.

The profile owns both one-time generation inputs and ongoing environmental coefficients where the two must remain coherent. For example, atmospheric starting backgrounds, volcanic province frequency, emission rates, attrition, and pulse distributions belong together; so do precipitation normals, initial moisture spin-up, evaporation, and seasonal variability. The compiled profile therefore remains part of immutable `CompiledWorldRules` after generation even though generated fixed tiles become authoritative state.

A profile declares a bounded setup surface using stable typed world-parameter IDs. Initial candidates include dimensions, target aquatic fraction, broad climate volatility, volcanic prevalence/activity, and initial resource richness. Each option has an authored default and allowed range or choice set; the engine applies no hidden fallback. Scenario rules may narrow those choices or require capabilities such as viable paired volcanic-ocean starts.

# Numeric and coordinate contract

| Domain | V1 representation | Notes |
| --- | --- | --- |
| Tile `x`, `y` | Signed 32-bit integers | Default `x = 0..31`, `y = -8..8` |
| Latitude | Derived signed microdegrees or deterministic fixed-point angle | `latitudeDegrees = 180 * y / height`; default extreme centers are approximately `±84.706°` |
| Elevation | Signed integer meters | Generated tile centers never use exactly zero: `> 0` land, `< 0` aquatic |
| Water depth | Derived nonnegative integer meters | `max(0, -elevationMeters)` |
| Tile-local position | Unsigned normalized `LocalCoordQ` in `[0, 2^32)` | No physical-distance claim in v1 |
| Tile-local velocity | Signed fixed-point tile fractions per hour | Exact encoding belongs to the movement pass |
| Temperature | Signed milli-degrees Celsius | Environmental curves convert explicitly to `RatioQ` |
| Precipitation | Nonnegative integer micrometers of water per hour | A rate, distinct from surface moisture |
| Moisture, cloud, turbidity, volcanism, insolation | `RatioQ`, normally `[0, 1]` | Any domain permitted above one must declare that separately |

Coordinate helpers are pure and shared by generation, simulation, protocol projection, and tests:

```text
East(x)  = (x + 1) mod width
West(x)  = floorMod(x - 1, width)
North(y) = y + 1 when y < maxY, otherwise none
South(y) = y - 1 when y > minY, otherwise none

latitudeDegrees(y) = 180 * y / height
```

Crossing the east or west edge of a tile subtracts or adds one normalized tile width and changes `x` through `East` or `West`. Crossing a permitted north/south edge does the analogous `y` transfer. At the bounded north or south world edge, active movement is clamped at the tile boundary and cannot migrate; a later movement trait cannot silently wrap `y`.

# Authoritative state split

```text
WorldFixedState
    seed
    world_generation_identity
    normalized_selected_world_options
    width
    height
    generation_algorithm_version
    calendar_definition
    start_hour_offset
    world_resource_targets

TileFixedState
    coordinate
    latitude
    elevation_meters
    terrain_tags
    lithology_profile_id
    ocean_distance
    volcanic_province_id?
    volcanic_activity_baseline_q
    volcanic_emission_profile_id?
    climate_baseline_id
    resource_initialization_profile_id

TileClimateBaseline
    monthly_temperature_milli_c[12]
    monthly_precipitation_micrometers_per_hour[12]
    surface_moisture_baseline_q
    cloud_baseline_q[12]
    aquatic_turbidity_baseline_q

TileDynamicEnvironment
    surface_moisture_q                 # terrestrial only
    active_volcanic_pulses[]           # only while a pulse spans ticks
    environmental_rate_remainders[]
    resource_accounts[]
```

Current temperature, precipitation, cloud, insolation, aquatic light, and ordinary slow volcanic modulation are derived once by phase 1 and stored in the completed `TileClimateState` with its generation. They are not independently writable fields, and later phases read the stored values rather than invoking climate formulas. Active volcanic pulses are primary stored state because their start, duration, and decay form an authoritative event history. Resource accounts and source/sink/exchange remainders follow the resource and gas plans.

# Deterministic world-generation pipeline

Every generation stage uses a named key domain and immutable inputs. Adding a random choice to climate generation must not shift elevation or resource results.

```text
GenerateWorld(seed, scenario, compiledRuleSet, compiledWorldProfile, selectedOptions):
    validate odd height, dimensions, numeric limits, and rule references

    for generationAttempt in 0 ..< scenario.maxGenerationAttempts:
        attemptSeed = Key(seed, WorldAttempt, generationAttempt)

        elevationPotential = GeneratePeriodicElevationField(attemptSeed)
        elevation = ClassifyAndScaleElevation(elevationPotential,
                                              scenario.targetAquaticFraction)
        terrain = DeriveTerrainTags(elevation, slopes, coastDistance)
        volcanism = GenerateVolcanicProvinces(attemptSeed, elevation, terrain)
        climate = GenerateClimateBaselines(attemptSeed, elevation,
                                           terrain, volcanism)
        lithology = GenerateCorrelatedLithology(attemptSeed, terrain)
        resources = GenerateNonGasResources(attemptSeed, terrain, lithology,
                                            volcanism, climate)
        worldCandidate = ComposeCandidate(elevation, terrain, lithology,
                                          volcanism, climate, resources)
        candidate = worldCandidate

        startReport = EvaluateStartingRegions(candidate, scenario)
        if startReport is insufficient:
            candidate = ApplyBoundedDeterministicStartRepair(candidate,
                                                             startReport)
            startReport = EvaluateStartingRegions(candidate, scenario)

        if startReport satisfies scenario:
            initialize atmospheric fields with bounded iteration
            reconcile every source, sink, and resource account
            return candidate preview plus generation diagnostics and repair record

    fail generation with a stable diagnostic; never return an invalid world

FinalizeWorldStart(generatedWorld, setupCommand):
    validate selected eligible tile and reserved competitor tile when required
    choose start_hour_offset so the selected tile begins at local dawn
    spin up terrestrial moisture over the configured prehistory ending at tick zero
    derive and cache tick-zero conditions
    run the selected one- or two-founder initialization transactions
    reconcile matter and return the authoritative tick-zero world
```

Generation and start finalization are separate because the player chooses a tile after previewing generated baselines. Moisture spin-up uses the selected solar-time offset and a keyed negative prehistory interval ending at tick `-1`; displayed simulation time still begins at tick zero. Preview projections may show generated moisture baselines, not a fabricated exact tick-zero moisture value before finalization.

## Elevation and terrain

`GeneratePeriodicElevationField` combines three to five rule-pack-defined octaves of smooth keyed lattice noise plus a small number of broad continental influence fields. Lattice sampling is periodic in `x`; no post-generation seam blending is allowed because it can conceal an incorrect generator. `y` samples use bounded coordinates and may include a polar-shape term, but are not mirrored into a false north/south wrap.

The first scenario targets `70%` aquatic tiles with a permitted seeded variation of `±5` percentage points. The generator selects the corresponding potential-field quantile as sea level, then maps aquatic ranks into `-1..-6,000 m` and terrestrial ranks into `1..4,000 m`. Exact zero is reserved as the conceptual shoreline between tile centers. Terrain tags such as shallow ocean, deep ocean, coast, lowland, highland, and mountain are derived from elevation and neighborhood; `volcanic` remains an independent overlay.

This is a gameplay-normalized planet, not an equal-area spherical grid. Latitude affects climate and insolation, but east-west tile adjacency and capacity do not shrink toward the poles in v1.

## Volcanic provinces

Volcanism combines elongated ridge-like fields, isolated hotspots, and a small elevation/slope correlation. Before the general seeds, up to two and no more than half of province anchors may be selected from separated low-latitude ocean edges with complementary hydrogen/sulfur depth and clear-light geometry. The anchor score uses only elevation and latitude, which are already available before volcanism and climate finalization. This declared origin bias makes paired geography plausible on a 544-tile map without itself granting valid resources or biology; exact bounds are in [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md). Each volcanic tile receives a baseline activity and an emission-profile ID; activity alone does not decide whether the tile is hydrogen- or sulfide-favored. Neighboring tiles may share a province while using distinct emission strengths.

Baseline activity is stable generated state. Current activity is:

```text
currentVolcanism = clamp01(
    baselineVolcanism * SlowVolcanicField(seed, provinceId, tick)
    + sum(activePulseContribution)
)
```

The slow field is deterministic and varies on multi-day or longer timescales. Optional pulses have keyed start decisions, bounded duration, declared gas/source multipliers, and exponential or table-driven decay. V1 does not require random world-destroying eruptions; the final deadline event is a gameplay result boundary, not ordinary volcanism.

# Calendar and solar opportunity

The first calendar uses:

```text
hoursPerDay = 24
daysPerMonth = 30
monthsPerYear = 12
daysPerYear = 360
axialTilt = 23.5 degrees
```

Day zero is an equinox. The setup transaction selects `startHourOffset` so the chosen player tile begins at local dawn; the adjacent survival competitor is within one tile's longitude and begins at nearly the same local time.

For tile longitude `x`, latitude `lat`, day-of-year `d`, and fractional universal day `t`:

```text
declination = axialTilt * sin(2π * d / daysPerYear)
localSolarFraction = frac(t + x / width + startHourOffset)
hourAngle = 2π * (localSolarFraction - 0.5)

cosZenith = sin(lat) * sin(declination)
          + cos(lat) * cos(declination) * cos(hourAngle)

rawSurfaceSolarOpportunity = max(0, cosZenith)
```

Authoritative trigonometry uses versioned lookup tables or another explicitly specified deterministic approximation generated with the rule pack. Runtime platform `sin`, `cos`, and floating-point reassociation may not decide simulation results.

Cloud attenuates current surface opportunity. Aquatic attenuation uses a monotone rule-pack curve with fixture anchors:

| Depth | Clear-water depth factor |
| ---: | ---: |
| `0 m` | `1.00` |
| `20 m` | `0.80` |
| `200 m` | `0.15` |
| `1,000 m` | approximately `0` |

Interpolation must be deterministic and monotone; turbidity further reduces the factor. The anchors preserve the relative shallow sulfur and deeper hydrogen founder light environments. [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md) fixes the first generated-world scalar and daily curve: the reference sulfur day produces `11,394` phototrophy extents and a first split at tick `263`, while the square fixture remains a reaction/accounting isolation control. The square fixture value must not be mistaken for a literal zenith-angle equation.

# Climate baselines

Climate baselines are twelve monthly normals, not one annual value. The first temperature candidate is:

```text
annualMeanC = 42
            - 38 * abs(sin(latitude))
            - 6.5 * max(elevationMeters, 0) / 1000
            + seededRegionalAnomalyC
            + volcanicGeothermalBaselineC

seededRegionalAnomalyC in approximately [-5, +5]
volcanicGeothermalBaselineC = 5 * baselineVolcanism, normally in [0, +5]

seasonAmplitudeC = (2 + 18 * abs(sin(latitude)))
                 * terrainSeasonalityFactor

terrainSeasonalityFactor = 0.45 aquatic, 1.0 terrestrial
```

Monthly normals sample the seasonal curve at month centers with opposite phase in the two hemispheres. These numbers place warm volcanic oceans in the equatorial and adjacent rows while retaining cold high-latitude and mountain niches; they require representative-map validation and are balance data.

Monthly precipitation normals derive from a separate periodic moisture-potential field, distance to ocean, latitude band, terrain height, and a bounded orographic modifier. V1 does not simulate fluid atmospheric circulation or prevailing wind. The generator must nevertheless produce contiguous wet/dry regions rather than per-tile noise and must permit terrestrial tiles whose surface moisture is seasonal.

Cloud normals correlate with precipitation potential but are not identical to current precipitation. Aquatic turbidity has a generated baseline influenced by depth, coastal proximity, and volcanism.

V1 temperature does not respond dynamically to evolving atmospheric CO₂ or CH₄ quantities. Those gases remain fully simulated resource reservoirs with sources, sinks, exchange, biological transformations, and histories, but greenhouse feedback is deferred until the resource and climate systems can be jointly recalibrated. Climate configuration and history schemas must leave room for a later globally or regionally aggregated forcing term without storing such a term in v1 state.

# Current weather and conditions

Weather is deterministic, spatially correlated, and temporally continuous:

1. For each weather channel, generate a coarse periodic-in-`x` spatial anchor field keyed by `(seed, channel, dayIndex)`.
2. Smoothly interpolate between the current and next daily anchor fields.
3. Bilinearly interpolate the coarse spatial field to tiles.
4. Apply bounded channel-specific transforms to monthly temperature, precipitation, and cloud normals.
5. Add a separately keyed, slowly interpolated global annual anomaly so years differ coherently.

```text
UpdateCurrentConditions(world, nextTick):
    calendar = DeriveCalendar(nextTick)

    for tile in canonical order, parallel-safe:
        monthly = InterpolateMonthlyNormals(tile.baseline, calendar)
        weather = SampleCorrelatedWeatherFields(tile.coordinate, calendar)

        temperature = ClampClimateTemperature(
            monthly.temperature
            + DiurnalTemperatureTerm(tile, calendar)
            + weather.temperatureAnomaly
            + GlobalAnnualAnomaly(calendar.year))

        cloud = clamp01(monthly.cloud + weather.cloudAnomaly)
        precipitation = DerivePrecipitation(monthly.precipitation,
                                            weather.stormPotential,
                                            temperature)
        surfaceSolar = DeriveSolarOpportunity(tile, calendar, cloud)
        aquaticLight = ApplyDepthAndTurbidity(surfaceSolar, tile)
        volcanism = DeriveCurrentVolcanism(tile, calendar)

    update terrestrial surface moisture from stable prior moisture
    publish one immutable current-condition view for phases 2 through 10
```

Current-condition derivation consumes no mutable global random stream and is independent of worker count. Completed saves retain the materialized current condition values and generation used by downstream phases, in addition to the seed, tick, baselines, rule version, moisture state, and active volcanic pulses. Load validates rather than silently replaces those values; see [MATERIALIZED_DERIVED_STATE.md](MATERIALIZED_DERIVED_STATE.md).

Current raw/stateful conditions are held in dense field-major tile columns. After climate/resource phases establish their inputs, the engine builds compact tile-only physiology, acquisition, and movement views once per affected tile and reuses them for all organisms there. DNA- or organism-specific stress, health, and opportunity remain organism-kernel calculations rather than a species-by-tile cache. The storage layout and invalidation contract are defined in [RESOURCE_STORAGE_AND_EVALUATION.md](RESOURCE_STORAGE_AND_EVALUATION.md).

## Surface moisture

Aquatic tiles have full water access and no terrestrial surface-moisture balance. Terrestrial moisture is an authoritative `RatioQ` state:

```text
nextMoisture = clamp01(
    priorMoisture
    + precipitationToMoisture(precipitation, absorptionClass)
    - evaporation(priorMoisture, temperature, insolation)
    - drainage(priorMoisture, terrainClass)
)
```

Precipitation may raise moisture quickly; evaporation and drainage return it toward a generated baseline more slowly. The response must allow a terrestrial tile to be wet for only part of a year. Initialization runs the recurrence for two no-biology calendar years, or another bounded configured spin-up, over `climateEpochHour = -17,280..-1` for the first calendar. Weather-key derivation accepts this signed prehistory domain while authoritative gameplay tick IDs begin at zero. The last 48 spin-up moisture samples initialize the two-window history used by `MoistureConservation`; gameplay does not fabricate a zero-filled trend. This prevents tick zero from giving every land tile an arbitrary identical moisture state and keeps the result consistent with the selected local-dawn offset.

# Environmental resources and cross-tile movement

Atmospheric initialization, source, sink, attrition, accessibility, and symmetric exchange are fixed in [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md). Generated worlds use one global N₂ target and one global CO₂ target, then boundedly iterate the normal gas phases after starting-region repair so volcanic excess and neighbor leakage begin near their intended fields.

Non-gas resources use transport classes rather than bespoke engine code:

| Transport class | First v1 behavior |
| --- | --- |
| `TileBound` | No passive neighbor exchange; moves only through declared geology, biology, migration, predation, remains, or decay |
| `DissolvedMobile` | Symmetric neighbor exchange from a stable view with a rule-pack coefficient and edge compatibility |
| `SurfaceRunoff` | Optional directional downhill transfer on terrestrial edges; defer from the first executable slice unless needed by moisture validation |
| `BoundaryAvailable` | Water/surface moisture opportunity, never a finite tile debit |

Micronutrients and generic elemental organic/inorganic macronutrient pools default to `TileBound` in the first slice. This preserves well-mixed access inside a tile without making scarce nutrients or spent organic matter rapidly homogenize globally. `LabileDissolvedOrganic` and `ReducedFermentationProducts` are the first resources assigned to `DissolvedMobile`.

Terrestrial tiles also receive a spatially correlated `lithology_profile_id`. It redistributes their initial inorganic macronutrient and micronutrient endowments according to [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md), after which each resource is reconciled to its configured whole-world target. Lithology does not create an ongoing source. Current surface moisture independently limits organism claims against tracked terrestrial inorganic and micronutrient pools; it does not transform their single v1 biological form or alter their ledger balances.

The first dissolved profile uses `1,500` per million, or `0.15%`, of the stable stock difference per fully compatible undirected edge per simulated hour. Edge compatibility is symmetric:

```text
DissolvedCompatibility(tileA, tileB):
    if both tiles are aquatic:
        return 1.00
    if exactly one tile is terrestrial:
        return 0.10 * terrestrialTile.surfaceMoisture
    return 0.25 * min(tileA.surfaceMoisture, tileB.surfaceMoisture)
```

A dry terrestrial endpoint closes dissolved exchange. V1 has no directional runoff or current. The ordinary world topology applies: each wrapped `x` edge exchanges once, bounded `y` edges do not leak, and diagonal tiles never exchange directly.

```text
ResolveDissolvedExchange(stablePostLossView):
    for edge in canonical undirected edge order:
        compatibilityQ = DissolvedCompatibility(edge.low, edge.high)

        for resource assigned to DissolvedMobile in canonical resource order:
            delta = stock(edge.low, resource) - stock(edge.high, resource)
            flow = SignedFixedPointFlow(
                delta,
                baseRateQ = 1_500 per million per hour,
                compatibilityQ,
                edgeResourceRemainder[edge, resource])
            emit one signed transfer from the higher-stock endpoint

    proportionally scale any invalid aggregate outbound proposal
    atomically apply transfers and persist remainders
```

Sources resolve first, including structural-remnant release of labile substrate. Passive compound loss resolves second. Dissolved exchange uses that stable post-loss view, and biological claims resolve later. A fermentation product created during phase 7 first participates in exchange on the next tick. The selected maximum rate proposes at most `4 × 0.15% = 0.6%` gross outbound exchange from a fully aquatic four-neighbor tile, comfortably inside explicit-diffusion stability bounds.

The numeric single-source field and adjacent-consumer fixture are fixed in [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md). Other resources require an explicit class assignment; the existence of a `DissolvedOrMobile` physical phase does not automatically make a resource passively mobile.

# Cross-tile environment exchange

The first gas-specific source, sink, and exchange rates are fixed in [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md). Gas processing uses source, environmental attrition, symmetric exchange from a stable post-sink view, and then biological claims. Local volcanic fuels use low exchange relative to attrition; stable gases use 10% per-edge exchange and spread broadly through the finite neighbor graph.

Generated worlds use one seeded global N₂ target and one seeded global CO₂ target rather than per-tile background noise. N₂ begins uniform and conserved. A diffuse, explicitly accounted carbon-cycle boundary source maintains the CO₂ target against its environmental sink, while volcanic sources create local excess. The authoritative initial field is produced with bounded deterministic iteration over the normal gas phases; exact linear solving is only an offline diagnostic.

Gas transport itself does not depend on water depth. For biological claims, terrestrial organisms and vent-class gases have full access; atmospheric aquatic access declines as `100 / (100 + depthMeters)`; and mixed-origin CO₂/CH₄ receive full access in a volcanic tile that emits them. This preserves one conserved tile reservoir while deferring explicit atmospheric/dissolved phase partitioning.

Further exchange work remains for:

- Optional weather-dependent or directional atmospheric transport after v1.
- Classification and coefficients for any additional dissolved or mobile nutrient resources.
- Solid or poorly transported nutrient resources.
- Directional wind, runoff, and storm transport after v1; v1 organism environmental spread is zero-mean and uses the fixed terrestrial medium multiplier in [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md).

Exchange must be symmetric or explicitly directional, mass-balanced, stable for the selected tick duration, and independent of worker ordering. Generated worlds initialize gas fields from the equilibrium of the complete source graph rather than assigning every volcanic tile its isolated plateau.

# Starting-world validation

World generation must guarantee at least one volcanic ocean tile viable for each permitted founder and at least one paired survival region. The default scenario provisionally targets at least four non-identical eligible pairs so tile selection remains a choice; smaller maps may lower this configured target but never below one.

A pair consists of edge-sharing volcanic ocean tiles: one hydrogen-oriented tile near the `-200 m`, dim, H₂-rich fixture and one sulfur-oriented tile near the `-20 m`, illuminated, H₂S-rich fixture. Both target warm conditions near `45 °C`, founding quota availability, and survivable adapted sulfur exposure. Eligibility is expressed as ranges and integrated opportunity tests, not equality with fixture constants.

Eligibility means viable for the selected founding DNA, not broadly favorable. The [hydrogen fixture](ONE_TILE_STARTING_CONFIGURATION.md) combines primitive gas substrates with sulfur toxicity, weak light, and missing advanced-pathway micronutrients. The [sulfur fixture](SULFUR_TILE_STARTING_CONFIGURATION.md) defines the complementary shallow, illuminated, H₂S-driven profile, while [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md) defines the paired-region rules. World generation should vary the exact pressures while preserving both openings and ensuring neither starting tile is a generally superior refuge.

The deterministic guarantee is generate, score, repair, then retry:

1. Enumerate every east/south undirected edge once and score both possible hydrogen/sulfur orientations.
2. Accept naturally valid pairs first and retain diagnostics for why other pairs failed.
3. If too few pairs exist, select highest-scoring non-overlapping near-valid pairs by score and stable coordinate tie-break.
4. Repair only allowed fields in order: shallow/deep elevation band, volcanic emission-profile strength, regional geothermal anomaly, founding non-gas resource profile, and fixture-blocking advanced micronutrient removal.
5. Never repair global N₂/CO₂ targets, world topology, latitude, seasonal phase, or biological rules.
6. Reject a repair whose total normalized change exceeds the scenario budget; retry generation with the next named attempt seed.
7. Record every repaired tile and delta in generation diagnostics and the save's world-origin metadata.

After repair, atmospheric iteration and a paired biological smoke test must prove both founders survive, acquire quotas, and approach their expected first reproduction within configured tolerance. Repair cannot merely make a static eligibility predicate return true.

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

- [x] First coordinate bounds, default `32 × 17` grid, normalized local-coordinate contract, and neighbor rules.
- [x] First periodic elevation/terrain-generation algorithm and `70% ± 5%` aquatic target.
- [x] First monthly-baseline and spatially/temporally correlated weather model.
- [x] First surface-moisture recurrence and deterministic initialization spin-up.
- [x] First calendar, latitude, solar-angle, cloud, depth, and turbidity contract.
- [x] First volcanic-province, slow-variation, bounded-pulse model, and numerical pulse-rate candidate; long-run ecological tuning remains open.
- [x] First atmospheric-gas exchange equations, rates, plateau targets, and stability constraints; see [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- [x] Initial N₂/CO₂ background targets, deterministic iterative initialization, and first gas-accessibility curve; see [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- [x] Generate-score-repair-retry starting-region policy and first paired-region criteria.
- [x] First exact eligibility ranges, repair weights/budget, four-pair target, fairness diagnostics, and bounded biological smoke test; see [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md).
- [x] First generated-world light calibration: `11,394` reference daily extents, tick-`263` reproduction, and `250..275` acceptance band; see [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md).
- [x] First temperature, precipitation, cloud, moisture, turbidity, and volcanic coefficient set; representative-map validation and revision remain open.
- [x] First dissolved non-gas exchange and terrestrial tile-bound access rules; micronutrients remain tile-bound and use moisture-dependent claim access on land. Remnant decay and passive mineralization rates are fixed in [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).
- [ ] Reduced tile-summary schema and coarse-band thresholds.
- [ ] Player-knowledge update algorithm and visibility transition tests.
- [ ] Maps and plots demonstrating representative generated worlds.

# Required validation

- Every generated `x` seam is continuous for elevation, climate fields, volcanism, and weather; wrapped edges occur exactly once.
- Default `y = -8..8` maps symmetrically to latitude and never creates north/south neighbors beyond the bounds.
- The same seed, rule pack, scenario, and selected start produce identical fixed state, repair records, tick-zero moisture, and current conditions.
- Changing worker count or client visibility does not alter weather, resources, or volcanic events.
- Monthly interpolation is continuous at month and year boundaries; hemispheres have opposite seasonal phase and equal equinox conditions before regional modifiers.
- Day/night and day-length behavior are correct at the equator, mid-latitudes, and near the bounded polar rows.
- The depth-light curve reproduces `0 m = 1.0`, `20 m = 0.80`, and `200 m = 0.15`, remains monotone, and never produces negative light.
- Generated sulfur starts match the fixture's daily energy/growth opportunity within a configured tolerance; generated hydrogen starts remain meaningfully dimmer.
- Temperature, precipitation, cloud, moisture, turbidity, and volcanism remain inside configured numeric bounds under long runs.
- Terrestrial moisture responds to precipitation with lag, can dry seasonally, and reproduces exactly after save/load.
- Correlated terrestrial lithology produces resource-specific strengths and shortages, preserves configured world totals, and exposes no universally rich land profile.
- Atmospheric initialization and every non-gas exchange conserve matter against declared boundaries.
- The default scenario produces the required number of eligible paired regions or a deterministic explicit generation failure—never a silently invalid world.
- Repaired starts pass actual founder smoke simulations, preserve missing advanced-pathway micronutrient gates, and expose their repair deltas in diagnostics.
- Unknown and reduced client projections cannot infer exact current weather, resources, organisms, remains, or repair-only hidden state.

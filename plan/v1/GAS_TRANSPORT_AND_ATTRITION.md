# Atmospheric Gas Sources, Transport, and Attrition

Status: first numerical calibration implemented for the official primordial-Earth-like world profile (`GAS-200`)

Sources: [WORLD vision](../../vision/WORLD.md), [NUTRIENTS vision](../../vision/NUTRIENTS.md), [resource model](RESOURCE_MODEL.md), [world generation and climate](WORLD_AND_CLIMATE.md), [moddability](MODDABILITY.md), [hydrogen fixture](ONE_TILE_STARTING_CONFIGURATION.md), and [sulfur fixture](SULFUR_TILE_STARTING_CONFIGURATION.md).

# Purpose

Define deterministic, mass-balanced gas emission, environmental attrition, and neighbor exchange so that:

- Volcanic activity produces a stable no-consumption plateau rather than unlimited accumulation.
- H₂, H₂S, and SO₂ remain concentrated in volcanic tiles.
- A non-volcanic neighbor contains a moderate trace of those gases, but receives too little continuing flux to sustain a robust founding population.
- Stable gases such as N₂, CO₂, and later O₂ spread across the tile-local atmosphere much more quickly.
- Every source, sink, transformation, and transfer remains auditable and deterministic.

The tile reservoir represents locally bioavailable atmospheric or dissolved gas under LYFE's abstraction. It is not a claim that the real atmosphere above each map tile behaves as an isolated box.

The gas catalogue, composition, ledger semantics, and exchange/sink algorithms belong to the biological/base rules and engine contract. Concrete atmospheric backgrounds, volcanic emission strengths/distributions, attrition and exchange coefficients, diffuse boundary sources, and tile-access initialization values below belong to the selected complete world profile. The numbers in this document calibrate the official primordial-Earth-like profile; external world packs may supply different validated values without changing the algorithms or adding gas/resource kinds.

# Initial global background

V1 uses an early-Archean-inspired, anoxic global background while treating the physical analogy as authoring guidance rather than an authoritative conversion between LYFE resource units and bars.

| Gas | Reference target per tile | Seeded generated range | Informal atmospheric analogue |
| --- | ---: | ---: | --- |
| N₂ | `500,000,000` | `450,000,000–550,000,000` | Approximately `0.5 bar`, with ±10% world variation |
| CO₂ | `100,000,000` | `75,000,000–125,000,000` | Approximately `0.1 bar`, with ±25% world variation |
| O₂ | `0` | Fixed | Anoxic opening |

The world seed selects one global N₂ target and one global CO₂ target from symmetric triangular distributions centered on the reference values. These are world-level differences, not independent tile noise: rapidly mixing gases should not begin with arbitrary checkerboard variation. Volcanic and other local sources create the spatial structure above the background.

The reference values deliberately preserve the existing one-tile fixtures and an N₂:CO₂ ratio of `5:1`. Geological evidence does not uniquely determine the atmosphere at the origin of life; this is a plausible game baseline, not a claim that early Earth had one known composition. Any later physical-scale presentation must label the bar analogy as approximate.

N₂ has no v1 sink or maintenance source, so its globally conserved initial total persists and exchange only restores uniformity. CO₂ does have a slow environmental sink. To keep its configured background from disappearing on a gameplay timescale, every tile receives a declared diffuse carbon-cycle boundary source whose equilibrium equals that world's target:

```text
co2BackgroundSource = co2BackgroundTarget * co2SinkRate
                      / (1 - co2SinkRate)
```

At the reference `100,000,000` target and `0.02%` hourly sink, the source averages approximately `20,004.0008` units per tile-hour. Fixed-point source remainders preserve this fractional average. The source represents unresolved exchange with geological and deep-ocean carbon reservoirs and is recorded separately from volcanic CO₂. Under the source-then-sink phase order, a non-volcanic tile with no biological consumption remains at the target, while volcanic emissions create an additional spatially varying excess.

# Tick phase and state equation

Gas processing occurs from a stable pre-phase view in this order:

1. Credit geological, volcanic, atmospheric, and biological gas sources.
2. Apply gas-specific environmental attrition to each post-source tile balance.
3. Calculate and atomically apply symmetric neighbor exchange from the post-attrition view.
4. Resolve biological gas claims against the resulting available balances.
5. Record source, sink, exchange, and biological flows as distinct ledger categories.

For gas `g` in tile `i`, ignoring integer rounding and biological consumption:

```text
postSource[i] = stock[i] + source[i]
postSink[i]   = postSource[i] * (1 - sinkRate[g])

exchange[i,j] = exchangeRate[g]
              * compatibility[i,j,g]
              * (postSink[i] - postSink[j])

next[i] = postSink[i] - sum(exchange[i,j])
                         + sum(exchange[j,i])
```

`exchange[i,j]` is represented once per undirected edge with a sign; positive flow travels from `i` to `j`. Only edge-sharing neighbors participate, including the wrapped `x` seam. Bounded `y` edges have no outside neighbor and therefore no exchange flux.

V1 assigns every equal-area tile a normalized gas-transport volume of `1`, so reservoir amount and transport concentration have the same numeric ordering. Water depth affects biological accessibility, not the volume used for atmospheric neighbor exchange. If later phase partitioning or unequal transport volumes are introduced, the gradient must use `stock / transportVolume` while the applied flow remains an amount transfer.

Biological claims occur after exchange and can lower the realized plateau. Presentation requests and tile visibility never affect these calculations.

# Biological accessibility

Terrestrial organisms can claim against the full post-exchange gas balance in their tile. Aquatic accessibility is selected by a data-defined gas class:

| Accessibility class | Initial gas IDs | Aquatic behavior |
| --- | --- | --- |
| `VentAccessible` | H₂, H₂S, SO₂, NH₃ | Full access at every depth |
| `AtmosphericDepthLimited` | N₂, O₂ | Inversely related to water depth |
| `MixedOrigin` | CO₂, CH₄ | Full in a volcanic tile whose profile emits the gas; otherwise depth-limited |

The `VentAccessible` class treats transported H₂, H₂S, SO₂, and NH₃ as dissolved or otherwise biologically available even after they leave their source tile. This preserves the existing first-ring carrying-power calculations. CO₂ and CH₄ cannot be classified solely by molecule because v1 gives them both geological and broader atmospheric roles. `MixedOrigin` is the practical compromise while the model has one well-mixed account and no source provenance.

Depth-limited classes use:

```text
AtmosphericDepthAccess(depthMeters) =
    100 / (100 + max(0, depthMeters))
```

This provisional half-access depth gives `83.33%` access at the `20 m` sulfur fixture, `50%` at `100 m`, `33.33%` at the `200 m` hydrogen fixture, and approximately `9.09%` at `1,000 m`.

A mixed-origin gas is fully accessible to aquatic organisms when the tile's configured volcanic emission profile emits that gas and the tile has nonzero baseline volcanic activity. This is a tile-level vent-access abstraction: it does not attempt to retain source provenance after identical gas molecules enter one well-mixed reservoir. Consequently, both founding fixtures retain full access to their locally emitted CO₂ and fuel gases. CO₂ or CH₄ that is present in a non-volcanic aquatic tile uses the atmospheric depth curve.

```text
GasAccessibility(tile, gas):
    if tile is terrestrial:
        return 1.0
    match gas.accessibilityClass:
        VentAccessible:
            return 1.0
        AtmosphericDepthLimited:
            return AtmosphericDepthAccess(tile.waterDepthMeters)
        MixedOrigin:
            if tile.baselineVolcanicActivity > 0
               and tile.emissionProfile.emits(gas):
                return 1.0
            return AtmosphericDepthAccess(tile.waterDepthMeters)

accessibleForBiologicalClaims =
    floor(postExchangeBalance * GasAccessibility(tile, gas))
```

The inaccessible fraction remains in the same tile account and continues to exchange and undergo attrition; accessibility is a claim ceiling, not a second reservoir or a loss of matter. Ordinary DNA uptake efficiency limits an organism's request inside this environmental ceiling. Later traits may alter gas capture, but do not change the tile's conserved gas stock.

This is intentionally a practical abstraction. A future phase-partition model may distinguish atmosphere, dissolved gas, and hydrothermal delivery explicitly, at which point accessibility should emerge from transfers among those reservoirs rather than from this multiplier.

# Numeric representation and determinism

Rates use integer parts per million per one-hour tick:

```text
RATE_SCALE = 1,000,000
```

Each tile/gas sink and each canonical tile-edge/gas exchange retains a signed fixed-point remainder. For an edge:

```text
scaled = concentrationDifference * exchangeRatePpm
       * compatibilityPpm / RATE_SCALE
       + signedRemainder

flow = truncateTowardZero(scaled / RATE_SCALE)
signedRemainder = scaled - flow * RATE_SCALE
```

The implementation should combine the two rate multiplications with checked wide intermediates and one named rounding rule rather than actually rounding between them. Edges are keyed by the sorted pair of stable tile IDs, so worker assignment and traversal direction cannot change results.

All exchange proposals use the same read view and apply after a barrier. The configured maximum exchange rate is 10% per edge; with at most four neighbors, a tile can propose at most 40% of its stock outward when every neighbor is empty. The general proportional-scaling guard remains in place for future configurations that could exceed the available balance.

# Environmental attrition destinations

“Gas destruction” removes matter from the named bioavailable gas reservoir, not from accounting. Each configured attrition process declares a destination:

| Gas | Default attrition interpretation | Ledger destination |
| --- | --- | --- |
| H₂ | Atmospheric escape | External atmospheric/space sink |
| H₂S | Chemical transformation or dissolution | Generic inorganic H and S tile pools |
| SO₂ | Chemical transformation or dissolution | Generic inorganic S and O tile pools |
| CH₄ | Photochemical transformation/escape abstraction | Configured generic inorganic pools or explicit boundary sink |
| NH₃ | Chemical transformation/dissolution | Generic inorganic N and H tile pools |
| CO₂ | Slow dissolution or geological transfer | Generic inorganic C and O tile pools |
| O₂ | Slow reaction with environmental material | Generic inorganic O pool |
| N₂ | No default v1 attrition | None |

Expanding a named compound into generic elemental pools uses its declared composition vector and cannot duplicate the original named balance. A rule pack may use a true external sink only when matter is explicitly leaving the modeled world.

The first executable `GAS-200` slice records every configured attrition flow into an explicit external atmospheric/geological boundary account. This preserves exact elemental accounting without prematurely inventing tile-pool chemistry. The later resource-catalogue slice may redirect H₂S, SO₂, NH₃, CO₂, and O₂ attrition to named inorganic tile products; doing so is a rules/content change, not a transport-algorithm change.

# Volcanic source scaling

Each tile stores volcanic activity as a fixed-point value from `0` to `1`. Realized hourly emission is:

```text
source[tile, gas] =
    fullActivityEmission[emissionProfile, gas]
    * volcanicActivity[tile]
    * temporaryActivityModifier[tile]
```

Rounding uses a persisted source remainder. `temporaryActivityModifier` defaults to `1` and supports deterministic eruptions or quiet periods without changing the baseline.

V1 starts with two canonical emission profiles. Their full-activity rates are selected so activity `0.80` reproduces the existing fixture sources:

| Gas | Hydrogen-rich full activity | Sulfur-rich full activity | Hydrogen fixture at 0.80 | Sulfur fixture at 0.80 |
| --- | ---: | ---: | ---: | ---: |
| H₂ | 312,500 | 62,500 | 250,000 | 50,000 |
| CO₂ | 187,500 | 187,500 | 150,000 | 150,000 |
| H₂S | 125,000 | 250,000 | 100,000 | 200,000 |
| SO₂ | 62,500 | 62,500 | 50,000 | 50,000 |
| CH₄ | 5,000 | 5,000 | 4,000 | 4,000 |
| NH₃ | 6,250 | 6,250 | 5,000 | 5,000 |

Non-volcanic tiles use activity zero unless another geological or biological source is explicitly configured. Generated volcanic tiles choose or blend a versioned emission profile; they do not infer gas ratios ad hoc during the simulation.

# Gas transport classes

## Local volcanic gases

| Gas | Sink per hour | Sink ppm | Exchange per edge/hour | Exchange ppm | Intended behavior |
| --- | ---: | ---: | ---: | ---: | --- |
| H₂ | 0.25% | 2,500 | 0.010% | 100 | Local fuel; slow leakage and atmospheric escape |
| H₂S | 0.50% | 5,000 | 0.025% | 250 | Local fuel/toxin; modest first-ring presence |
| SO₂ | 1.00% | 10,000 | 0.050% | 500 | Short-lived toxin; near-source exposure |
| CH₄ | 0.20% | 2,000 | 0.050% | 500 | Semi-mobile geological reservoir |
| NH₃ | 0.05% | 500 | 0.100% | 1,000 | Mobile but reactive fixed-nitrogen source |

These rates intentionally describe bioavailable local reservoirs, not literal molecular diffusion speeds. A gas may have rapid physical transport but still lose local biological accessibility through escape, dissolution, reaction, dilution, or phase partitioning.

## Stable abundant gases

| Gas | Sink per hour | Sink ppm | Exchange per edge/hour | Exchange ppm | Intended behavior |
| --- | ---: | ---: | ---: | ---: | --- |
| CO₂ | 0.02% | 200 | 10.0% | 100,000 | Rapid broad mixing with slow environmental transfer |
| O₂ | 0.02% | 200 | 10.0% | 100,000 | Biological production spreads across the world |
| N₂ | 0 | 0 | 10.0% | 100,000 | Stable, approximately global background |

At 10% per edge, local gradients smooth quickly while the finite neighbor graph still determines propagation. On representative maps, complete global mixing takes many ticks and remains visible rather than becoming an instantaneous global reservoir.

# Edge compatibility

Exchange compatibility is symmetric and fixed for the duration of a tick:

| Edge | Baseline compatibility |
| --- | ---: |
| Aquatic–aquatic | 1.00 |
| Terrestrial–terrestrial | 1.00 |
| Aquatic–terrestrial | 0.50 |
| Either tile is a major mountain barrier | Multiply by 0.50 |

The first gas fixture uses aquatic–aquatic compatibility `1.00`. Wind-driven directionality, atmospheric circulation cells, and gas-specific phase partitioning are future refinements. If introduced, directional transport must remain mass-balanced and deterministic rather than modifying only a destination.

The `0.50` aquatic–terrestrial compatibility and `0.50` mountain multiplier remain the v1 provisional defaults. Selecting more exact values before representative maps exist would imply unsupported precision. Once the generator produces representative coastlines and mountain chains, validation should confirm that these modifiers create visible rain-shadow/barrier and coastal effects without unintentionally isolating large atmospheric regions. Changing the coefficients is balance-data tuning and does not require a transport-algorithm change.

# Isolated volcanic-field calibration

The reference calculation uses a `31 × 31` uniform aquatic grid with one activity-`0.80` source tile at the center, no organisms, no other sources, compatibility `1.00`, and bounded edges far enough away to be negligible. It iterates the stated source, sink, and exchange phases to a change below `0.001` resource unit per tick.

## No-consumption steady state

| Gas/profile | Volcanic center | Orthogonal neighbor | Neighbor/center | Two tiles away | Approx. ticks to 95% center plateau |
| --- | ---: | ---: | ---: | ---: | ---: |
| H₂, hydrogen-rich | 86,399,438 | 2,999,305 | 3.47% | 104,066 | 1,045 / 43.5 days |
| H₂S, sulfur-rich | 33,394,510 | 1,406,119 | 4.21% | 59,058 | 508 / 21.2 days |
| SO₂, either profile | 4,152,571 | 175,150 | 4.22% | 7,325 | 253 / 10.5 days |
| CH₄, either profile | 1,070,083 | 146,311 | 13.67% | 20,596 | 903 / 37.6 days |

The first ring is therefore observable and potentially useful to a very small adapted population, while the second ring receives only a trace of the primary founding fuels.

## Adjacent carrying-power check

At steady state, gross flow across the center-to-neighbor edge is approximately:

| Fuel | Gross influx/hour | Founding demand/organism-hour | Maximum organisms from that one edge | Founding population |
| --- | ---: | ---: | ---: | ---: |
| H₂ | 8,344 | 800 | 10.4 | 100 |
| H₂S | 8,007 | 954 averaged | 8.4 | 100 |

Actual biological consumption lowers the neighbor stock and changes surrounding flows, but cannot turn these figures into a 100-organism steady supply. V1 defines a robust founding population as at least 25 organisms sustained at its ideal per-organism demand. A source-free first-ring tile must remain below 15 equivalent organisms for either founding fuel. These rates satisfy that constraint with margin.

Micronutrient quotas and environmental tolerance continue to apply. Gas influx alone never makes a tile eligible for a founding species.

# Dependence on volcanic activity

Because sources, sinks, and exchange are linear in this fixture, steady stocks scale linearly with volcanic activity when all surrounding sources remain unchanged. For a single source tile:

| Activity | H₂ center, hydrogen-rich | H₂S center, sulfur-rich | SO₂ center |
| ---: | ---: | ---: | ---: |
| 0.20 | 21,599,860 | 8,348,628 | 1,038,143 |
| 0.50 | 53,999,649 | 20,871,569 | 2,595,357 |
| 0.80 | 86,399,438 | 33,394,510 | 4,152,571 |
| 1.00 | 107,999,298 | 41,743,138 | 5,190,714 |

Temporary activity changes approach the new plateau over the gas-specific timescale rather than teleporting the reservoir. This gives eruptions and quiet periods ecological consequences.

# Paired Survival region

The paired start contains adjacent activity-`0.80` hydrogen-rich and sulfur-rich volcanic tiles. Therefore the “moderate non-volcanic neighbor” rule does not directly describe the other founder's tile: it has its own source profile.

With no organisms, the two-source field settles approximately to:

| Gas | Hydrogen tile | Sulfur tile | Non-volcanic neighbor of hydrogen tile |
| --- | ---: | ---: | ---: |
| H₂ | 86,999,300 | 20,279,193 | 3,040,734 |
| H₂S | 18,103,374 | 34,097,569 | 820,358 |
| SO₂ | 4,327,721 | 4,327,721 | 189,700 |

With 100 hydrogen founders consuming 80,000 H₂ per hour and 100 sulfur founders consuming an averaged 95,400 H₂S per hour, the corresponding fuel stocks settle approximately to:

| Gas | Hydrogen tile | Sulfur tile | Non-volcanic neighbor of hydrogen tile |
| --- | ---: | ---: | ---: |
| H₂ | 59,271,479 | 19,319,415 | 2,080,956 |
| H₂S | 17,432,655 | 18,072,988 | 764,406 |

The secondary gas is present in each volcanic tile, but does not erase the opening distinction. The sulfur tile has zero Ni and Co for sustained canonical hydrogen-acetogenesis reproduction. The hydrogen tile is deep and weakly illuminated for sulfide phototrophy. Toxicity, structural nutrients, and later migration continue to matter.

# Initialization

Generated worlds should begin gas reservoirs at or near the no-biology equilibrium implied by their entire generated source field, not by treating every volcanic tile as isolated. Otherwise the opening experiences a long arbitrary fill or drain transient unrelated to player action.

Recommended deterministic initialization:

```text
InitializeGasField(world, rulePack):
    draw world-level N2 and CO2 background targets from named seed streams
    configure the diffuse CO2 boundary source for the selected target
    initialize N2 and CO2 uniformly to their targets
    initialize every other gas to zero
    iterate source, sink, and exchange without biology
        until the residual is within tolerance for 24 consecutive iterations
        or 20,000 initialization iterations are reached
    if the cap is reached:
        accept only when maximum relative tile/gas residual <= 1 ppm
        and diagnostics show no instability or conservation failure
        otherwise reject the generated world
    store converged balances as tick-zero environmental state
    apply AbiogenesisInitialization after the field is accepted
```

The residual tolerance for ordinary convergence is the larger of one resource unit or one part per million of the affected tile/gas stock. Requiring 24 consecutive qualifying iterations avoids accepting a one-tick rounding oscillation. Generated-world initialization records the selected background targets, iteration count, termination reason, maximum absolute and relative residuals, source totals, sink totals, and exchange reconciliation.

Bounded iteration is the v1 implementation because it reuses the authoritative source/sink/exchange path, preserves its integer rounding, and is easy to inspect. An offline linear solver may be used to validate or accelerate balance experiments, but its output is not authoritative and need not exactly match fixed-point remainder state.

N₂ requires a conserved initial total because zero sink plus exchange has no source-defined unique plateau. Oxygen begins at zero in the initial world and later spreads from biological sources. The first source is exactly one O2 per successful oxygenic-photosynthesis extent. Its reference producer and world-scale plateau landmarks are defined in [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md); they do not alter O2's transport or attrition coefficients.

# Methane transport and future metabolism

The presence of a methane-producing or methane-consuming metabolism must not itself change CH₄'s exchange coefficient. Biology changes source and sink flows; it does not cause the same gas to acquire new physical transport behavior. A conditional coefficient would also create a discontinuous world-wide rule change when a trait appears or disappears.

The current semi-local CH₄ class preserves geological hotspots and, later, local producer-consumer ecology. Moving methane to the stable `10%`-per-edge class would instead make biological methane a rapid global commons: distant organisms could benefit, local cross-feeding signals would weaken, and any methane-climate coupling would respond more globally. Either can be a valid design, but transport, photochemical attrition, biological source magnitude, and climate forcing must be calibrated together.

Recommendation:

- Keep CH₄ at `0.050%` exchange per edge-hour and `0.20%` attrition for v1, when it is a semi-local geological resource without a founding consumer.
- Do not change those rates merely because a methanogenesis trait is unlocked.
- When methane ecology and climate are implemented, test a faster single-reservoir coefficient as a simple approximation.
- If both local methane food webs and global greenhouse effects matter, introduce separate dissolved/local and atmospheric methane reservoirs with explicit phase transfer rather than switching one reservoir's rate based on biological state.

# Pseudocode

```text
ResolveGasEnvironment(worldReadView):
    for tile in canonical tile order:
        for gas in canonical gas order:
            source = EvaluateSources(tile, gas)
            postSource[tile, gas] = checkedAdd(stock[tile, gas], source)
            sink = EvaluateSink(postSource[tile, gas], sinkRemainder[tile, gas])
            postSink[tile, gas] = postSource[tile, gas] - sink
            emit source and sink ledger intents

    for edge in canonical undirected edge order:
        for gas in canonical gas order:
            delta = postSink[edge.low, gas] - postSink[edge.high, gas]
            flow = EvaluateSignedExchange(delta,
                                          exchangeRate[gas],
                                          compatibility[edge, gas],
                                          edgeRemainder[edge, gas])
            emit one signed mass-transfer intent

    proportionally scale any invalid aggregate outbound proposal
    atomically apply source, sink, and exchange intents
    publish gas balances for biological claim resolution
```

# Required validation

- Sources scale linearly with activity and preserve persisted fractional remainders.
- Every environmental sink reaches its declared destination with exact elemental accounting.
- Exchange conserves each named gas globally and is independent of edge traversal and worker count.
- An edge pair cannot create a net flow when its post-sink balances are equal.
- The single-source field converges to the stated center, first-ring, and second-ring values within fixture tolerance.
- A first-ring no-source tile supports fewer than 15 equivalent founding organisms from continuing H₂ or H₂S influx.
- Activity `0`, `0.20`, `0.50`, `0.80`, and `1.00` produce the expected plateau scaling.
- The paired field reproduces the stated no-biology and 100-organism fuel plateaus.
- A non-volcanic world initialized at its selected N₂/CO₂ targets remains at those targets, subject only to fixed-point remainders.
- Global-background draws are deterministic, remain inside their configured ranges, and do not introduce independent per-tile noise.
- At the reference CO₂ target, the diffuse boundary source and `0.02%` sink reproduce the `100,000,000`-unit equilibrium and reconcile their boundary flows.
- Terrestrial and `VentAccessible` gas claims receive full accessibility; depth-limited and mixed-origin aquatic fixtures reproduce the stated `20 m`, `100 m`, `200 m`, and `1,000 m` factors, with the mixed-origin volcanic override tested separately.
- The maximum configured exchange rate satisfies explicit-diffusion stability on every topology degree.
- Representative generated maps retain positive gas connectivity across coasts and mountains, and coefficient changes affect only versioned balance data.
- Wrapped `x` edges exchange exactly once; bounded `y` edges do not leak matter.
- Save/load preserves tile sink remainders, edge exchange remainders, activity modifiers, and the next result.
- Hidden-tile simulation and gas history remain identical under different client visibility subscriptions.

# Remaining decisions

- [x] Initial N₂ and CO₂ global-background targets and seeded ranges.
- [x] Bounded deterministic iteration for authoritative initialization; a linear solver remains an offline diagnostic.
- [x] Retain the provisional coastal and mountain modifiers unless representative-map validation shows a balance problem.
- [x] Terrestrial, aquatic-depth, and volcanic-tile gas accessibility equations.
- [x] Methane remains semi-local in v1 and does not change class merely because methane metabolism exists; later methane ecology triggers joint transport/climate recalibration.
- Whether weather-dependent or directional transport is valuable after v1.

# Scientific anchors

- The Archean atmosphere was anoxic and likely rich in N₂ and CO₂, but constraints—especially near the origin of life—remain broad. Later Archean evidence permits N₂ near `0.5 bar` and below roughly `1.0–1.1 bar`, while reported CO₂ constraints span a wide range: [The Archean atmosphere](https://pmc.ncbi.nlm.nih.gov/articles/PMC7043912/) and [The origin and evolution of Earth's nitrogen](https://pmc.ncbi.nlm.nih.gov/articles/PMC11223583/).
- Early-Earth methane abundance depends on the interaction of biological or geological source flux, atmospheric chemistry, and loss rather than on metabolism changing molecular transport: [The case and context for atmospheric methane as an exoplanet biosignature](https://pmc.ncbi.nlm.nih.gov/articles/PMC9168929/) and [Co-evolution of primitive methane-cycling ecosystems and early Earth's atmosphere and climate](https://pmc.ncbi.nlm.nih.gov/articles/PMC7264298/).

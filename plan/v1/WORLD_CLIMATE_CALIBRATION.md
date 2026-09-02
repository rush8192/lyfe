# World and Climate Numerical Calibration

Status: provisional v1 numerical rule-pack candidate; representative-seed maps and population smoke tests required before freezing

Sources: [world and climate algorithms](WORLD_AND_CLIMATE.md), [gas transport](GAS_TRANSPORT_AND_ATTRITION.md), [hydrogen founder fixture](ONE_TILE_STARTING_CONFIGURATION.md), [sulfur founder fixture](SULFUR_TILE_STARTING_CONFIGURATION.md), and [organism health calibration](ORGANISM_HEALTH_CALIBRATION.md).

# Purpose

Assign a coherent first set of numerical values to world generation, climate, aquatic light, terrestrial moisture, volcanism, and paired-start eligibility. These values turn the algorithm contract into a reproducible rule-pack candidate; they remain balance data, not engine constants.

# Calibration priorities

1. Preserve the established hydrogen and sulfur opening identities in generated worlds.
2. Produce useful geographic and seasonal variation without simulating atmospheric or ocean fluid dynamics.
3. Keep climate deterministic, spatially correlated, temporally continuous, and inexpensive across 544 tiles.
4. Make starting-region repair bounded and visible rather than allowing arbitrary fixture injection.
5. Create enough wet, dry, shallow, deep, hot, cold, volcanic, and non-volcanic space for the proposed trait niches.

# Decision summary

| Parameter | First v1 value |
| --- | ---: |
| Default dimensions | `32 × 17 = 544` tiles |
| Minimum dimensions | `8 × 5`; height must be odd |
| Generation attempts | `8` named deterministic attempts |
| Target aquatic fraction | Seeded uniformly from `65%..75%`, centered on `70%` |
| Elevation range | Aquatic `-1..-6,000 m`; terrestrial `1..4,000 m` |
| Elevation octaves | Wavelengths `16, 8, 4, 2` tiles; relative amplitudes `1, 0.5, 0.25, 0.125` |
| Calendar | 24-hour day; 360-day year; twelve 30-day months |
| Axial tilt | `23.5°` |
| Start phase | Day-zero equinox; selected tile at local dawn |
| Moisture spin-up | Two calendar years, `17,280` hourly steps |
| Dynamic CO₂/CH₄ greenhouse feedback | Deferred from v1; gas histories retained for a later forcing model |
| Required paired starts | `4` non-overlapping pairs on the default map |
| Minimum pair separation | Toroidal Manhattan distance `4` between pair centers |
| Pair repair budget | Maximum weighted deficit `0.55` |
| Maximum active start repair | Eight tiles: four disjoint pairs |

The four-pair target provides actual setup choice while changing at most `1.47%` of a default map's tiles when all four pairs require repair. Smaller scenarios may configure fewer pairs but never fewer than one.

# Elevation and terrain calibration

The elevation potential combines the four periodic octaves above with broad continental influence fields at relative amplitude `0.75`. Each named field uses its own random-key domain. After summing and normalizing the potential, a seeded aquatic target in `[0.65, 0.75]` selects sea level by exact rank; stable coordinate order resolves a quantile tie.

Ranks below sea level map monotonically into aquatic depth with a curve biased toward moderate/deep ocean rather than distributing depth uniformly. Ranks above sea level use a separate curve biased toward lowlands. The first curve targets are:

| Generated category | Share of aquatic or terrestrial tiles | Range |
| --- | ---: | ---: |
| Shallow ocean | `15%` of aquatic | `-1..-50 m` |
| Shelf/moderate ocean | `35%` of aquatic | `-51..-500 m` |
| Deep ocean | `50%` of aquatic | `-501..-6,000 m` |
| Lowland | `60%` of terrestrial | `1..500 m` |
| Highland | `30%` of terrestrial | `501..2,000 m` |
| Mountain | `10%` of terrestrial | `2,001..4,000 m` |

These category shares are validation targets with a tolerance of `±5` percentage points over the 128-seed calibration corpus. Individual worlds may vary more when necessary to preserve spatially coherent landforms.

# Temperature calibration

## Monthly normal inputs

The first annual-mean equation remains:

```text
annualMeanC = 42
            - 38 * abs(sin(latitude))
            - 6.5 * max(elevationMeters, 0) / 1000
            + regionalAnomalyC
            + 5 * baselineVolcanism
```

| Term | First value |
| --- | ---: |
| Regional anomaly | Spatially correlated, world-mean-centered, and scaled within `-5..+5 °C` |
| Geothermal baseline | `5 °C × baselineVolcanism` |
| Aquatic seasonal multiplier | `0.45` |
| Terrestrial seasonal multiplier | `1.00` |
| Base seasonal amplitude | `2 + 18 × abs(sin(latitude)) °C` before terrain multiplier |
| World current-temperature clamp | `-80..+80 °C` |

Monthly normals sample the seasonal curve at month centers. Positive `y` is northern hemisphere; negative `y` uses the opposite phase. At an equinox, identical north/south tiles have equal normal temperatures before regional anomalies.

The first phase convention is explicit:

```text
solarDeclinationDegrees(day) = 23.5 * sin(2π * day / 360)
northernSeasonTerm(day)      = sin(2π * day / 360)
southernSeasonTerm(day)      = -northernSeasonTerm(day)
monthlyTemperature[m]        = annualMeanC
                             + seasonAmplitudeC
                             * seasonTerm(day = 15 + 30 * m)
```

Day zero is the northward equinox, day 90 the northern summer solstice, day 180 the southward equinox, and day 270 the northern winter solstice. V1 deliberately omits a separate seasonal thermal lag; adding one would break the first equinox-symmetry invariant unless the validation rule changed with it.

## Current temperature terms

| Term | Aquatic | Terrestrial |
| --- | ---: | ---: |
| Diurnal peak amplitude | `2 °C` | `8 °C` |
| Correlated weather anomaly | `-4..+4 °C` | `-4..+4 °C` |
| Global annual anomaly | `-2..+2 °C` | `-2..+2 °C` |

The diurnal maximum lags solar noon by three hours over land and four hours over water. Daily weather anchors interpolate smoothly, so the anomaly cannot jump at midnight.

## Representative-map targets

The earlier `30 - 38 × abs(sin(latitude)) + 15 × volcanism` candidate was internally inconsistent with its own `20..35 °C` median target: because each latitude row has equal tile count, its unmodified median row was only about `4.4 °C`. The revised base and smaller geothermal term produce this sea-level, non-volcanic sanity table before regional anomaly:

| Absolute default-row latitude | Annual mean |
| ---: | ---: |
| `0.0°` | `42.0 °C` |
| `10.6°` | `35.0 °C` |
| `21.2°` | `28.3 °C` |
| `42.4°` | `16.4 °C` |
| `63.5°` | `8.0 °C` |
| `84.7°` | `4.2 °C` |

Baseline activity `0.80` adds `4 °C`, placing equatorial and first-row volcanic oceans near the founder temperature band without making the geothermal term dominate the planet. Across the first 128 accepted default worlds:

- Median annual-mean temperature should lie in `15..25 °C`.
- The fifth percentile should lie in `-20..+5 °C` and the ninety-fifth in `40..55 °C`.
- At least `10%` of non-volcanic tiles should have annual means below `5 °C`.
- No more than `10%` of non-volcanic tiles should exceed `45 °C`.
- Warm start candidates must arise from latitude, regional anomaly, and volcanism together rather than a hidden start-only temperature override.

Failure of these distribution targets changes the coefficients before it changes organism tolerance curves.

# Precipitation, cloud, and terrestrial moisture

## Monthly precipitation normals

Generated monthly mean precipitation is clamped to `0..250 μm/hour`, equivalent to `0..6 mm/day` when averaged across every hour. A temporally correlated storm field converts that mean into wet and dry hours:

| Parameter | First value |
| --- | ---: |
| Wet-hour fraction | `2%..30%`, derived from the monthly normal |
| Instantaneous precipitation cap | `20,000 μm/hour` (`20 mm/hour`) |
| Cloud normal | `0.05 + 0.65 × normalizedMonthlyPrecipitation`, capped at `0.75` |
| Weather cloud anomaly | `-0.25..+0.25` |
| Solar cloud attenuation | `cloudFactor = 1 - 0.60 × cloudQ` |

Conditional storm intensity is calibrated so the long-run hourly mean reproduces the monthly normal after its cap and integer rounding. Storm potential comes from the same spatially correlated weather family as cloud, but separate keyed channels prevent precipitation and cloud from becoming identical.

## Moisture recurrence

All quantities below use `RatioQ = 1,000,000` per `1.0`:

```text
rainGainQ = min(250,000, precipitationMicrometers * 50)

heatFactorQ = clamp01((temperatureC + 10) / 50)
solarFactorQ = clamp01(surfaceSolarOpportunity)

evaporationRateQ = 500
                 + floor(1,500 * heatFactorQ / 1,000,000)
                 + floor(1,000 * solarFactorQ / 1,000,000)

drainageRateQ = 200 lowland
              | 600 highland
              | 1,000 mountain

nextMoistureQ = clamp01(
    priorMoistureQ
    + rainGainQ
    - priorMoistureQ * (evaporationRateQ + drainageRateQ) / 1,000,000
)
```

One millimeter of rain therefore raises moisture by `0.05` before the hourly loss, while an extreme hour cannot raise it by more than `0.25`. In warm bright lowlands with no rain, the maximum first-pass loss rate is approximately `0.0032/hour`, giving a moisture half-life near nine days; cool dim lowlands retain moisture for several weeks.

Representative terrestrial maps should contain, after spin-up:

- At least `10%` persistently moist tiles whose monthly median never falls below `0.60`.
- At least `10%` seasonally moist tiles whose monthly medians cross both `0.25` and `0.60` during a year.
- At least `10%` arid tiles whose monthly median never exceeds `0.20`.

If these three niches cannot coexist across the seed corpus, tune precipitation-field distribution and drainage before changing the moisture thresholds used by organisms.

# Solar and aquatic-light calibration

## Deterministic sampling

Solar opportunity is evaluated at each tick's midpoint. The clear equatorial equinox sum of positive hourly `cosZenith` values is:

```text
sum(max(0, cosZenith)) = 7.6612975755 per day
```

The rule pack stores the fixed-point lookup result used by the engine; the decimal above is a calibration explanation rather than runtime floating point.

Depth attenuation uses log-linear interpolation between these anchors:

| Depth | Factor |
| ---: | ---: |
| `0 m` | `1.000` |
| `20 m` | `0.800` |
| `200 m` | `0.150` |
| `1,000 m` | `0.001` |

For `depth >= 1,000 m`, founding photosynthetic opportunity is zero. Turbidity applies:

```text
turbidityFactor = 1 - 0.75 * turbidityQ
```

## Sulfur-reference daily opportunity

The generated reference uses equator, equinox, `20 m` depth, cloud `0.10`, and zero additional turbidity:

```text
cloudFactor = 1 - 0.60 * 0.10 = 0.94

referenceDailyAquaticLight
    = 7.6612975755 * 0.80 * 0.94
    = 5.7612957768 opportunity-hours
```

To preserve the square fixture's `12 × 950 = 11,400` daily phototrophy extents, generated-world sulfide phototrophy uses:

```text
requestedExtentRatePerUnitAquaticLight = 2,083
idealPathFactor = 0.95

sum(floor(2,083 * 0.95 * hourlyAquaticLight))
    = 11,394 extents on the reference day
```

The generated rule has no separate `1,000`-extent hourly cap: peak reference hours may realize `1,475` extents before reserve-cap and substrate throttling. The standalone square fixture's `1,000` requested-extents allowance is a fixture-specific light control, not an intrinsic reaction maximum. Without this distinction, any smooth twelve-hour curve capped at `1,000 × 0.95` could equal `11,400` only by behaving as another square wave.

The six-extent difference is `0.053%` and is accepted as deterministic integer rounding. The hourly reference extents from dawn are:

```text
[194, 569, 905, 1180, 1374, 1475,
 1475, 1374, 1180, 905, 569, 194,
 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
```

Oxygenic photosynthesis reads this same already-attenuated aquatic-light value, applies its own `0.75` saturation cap once, and never reapplies cloud, depth, or turbidity. Its reference coefficient yields 7,772 daily extents and is defined in [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md).

With the existing 5,000 reserve, 4,000 growth floor, maintenance, reserve cap, and four-extent assembly limit, the realistic curve reaches 2,000 structure at tick `263` rather than square-fixture tick `250`. It remains `21.3%` faster than hydrogen's tick `334`. The square fixture remains a reaction/accounting isolation test; `250..275` ticks is the generated sulfur-start acceptance band.

## Light eligibility

Using a median-weather daily opportunity over the first 30 days:

| Start role | Required daily aquatic light |
| --- | ---: |
| Sulfur | `5.185..6.337`, or `90%..110%` of reference |
| Hydrogen | At most `2.016`, or `35%` of sulfur reference |

A sulfur candidate outside the range may still score as near-valid, but repair may alter depth, cloud baseline, or turbidity only within their declared bounds. It may not modify latitude or axial tilt.

# Volcanism calibration

| Parameter | First value |
| --- | ---: |
| Fraction of volcanic tiles | `8%..18%` target across accepted worlds |
| Minimum volcanic provinces | `4` |
| Origin-biased province anchors | Up to `2`, never more than `50%` of province seeds |
| Origin-anchor latitude | `abs(latitude) <= 12°` (default rows `y = -1..1`) |
| Volcanic tag threshold | Baseline activity `>= 0.40` |
| Founder activity range | `0.65..0.95` |
| Slow activity multiplier | `0.90..1.10` |
| Slow-field anchor interval | `30 days` |
| Pulse start probability | `0.001` per province-day |
| Pulse duration | `24..120 hours` |
| Pulse activity addition | `0.10..0.30` |
| Tick-zero active pulses | None |

Before placing general ridge and hotspot seeds, the generator enumerates low-latitude ocean edges with hydrogen/sulfur depth and clear-reference-light geometry. This uses only elevation and latitude already available at that pipeline stage, avoiding a climate/volcanism dependency cycle. In stable score order it may anchor up to two separated submarine volcanic provinces on those edges, but never more than half of all province seeds. The remaining province seeds follow only the geology field. This is a declared world-generation bias toward plausible origin geography, not a completed starting-pair repair: emissions, cloud, turbidity, temperature, nutrients, toxicity, and the biological smoke test can still reject the result. Without this bounded correlation, the joint probability of adjacent shallow/deeper water, strong volcanism, warmth, and light is too low for the four-pair setup target on only 544 tiles.

Pulses begin only through gameplay-time keyed evaluations at or after tick zero; moisture prehistory and setup previews do not create a hidden active eruption on a starting pair. Gas emissions multiply by current activity through the existing emission profiles. Pulse rates require long-run ecological validation and may be reduced if chance dominates early Survival outcomes.

# Paired-start numerical eligibility

Static eligibility creates candidates; a biological smoke test makes the final decision.

## Shared requirements

- Both tiles are aquatic, edge-sharing, in volcanic provinces, and have baseline activity `0.65..0.95`.
- Annual mean temperature is `38..50 °C`; tick-zero current temperature is `40..50 °C`.
- The first 30-day no-biology climate projection keeps the fifth-to-ninety-fifth temperature range within `35..55 °C`.
- Founding quota pools contain at least twenty complete population quota sets after abiogenesis.
- Ongoing required-micronutrient sources cover at least twice the 100-founder filling demand calculated in the uptake fixtures.
- Canonical manganese, molybdenum, and copper-dependent advanced paths remain unavailable from local starting stocks.
- Expected sulfur exposure remains survivable for the appropriate founder and materially stressful without its declared adaptation.

## Hydrogen-oriented tile

| Parameter | Eligible range |
| --- | ---: |
| Water depth | `120..300 m` |
| Daily aquatic light | `0..2.016` |
| H₂ source at baseline activity | `200,000..300,000/tick` |
| H₂S source | `60,000..140,000/tick` |
| SO₂ source | `30,000..70,000/tick` |
| NH₃ source | At least `4,000/tick` |

## Sulfur-oriented tile

| Parameter | Eligible range |
| --- | ---: |
| Water depth | `5..35 m` |
| Median 30-day daily aquatic light | `5.185..6.337` |
| H₂S source at baseline activity | `160,000..240,000/tick` |
| H₂ source | `0..100,000/tick` |
| SO₂ source | `30,000..70,000/tick` |
| NH₃ source | At least `4,000/tick` |

# Pair scoring and bounded repair

Pairs failing a non-repairable condition—land, nonadjacency, latitude/light impossibility, missing topology, numeric invalidity, or biological hard death—are discarded rather than repaired.

Repairable deficits use this weighted score:

| Dimension | Weight | Maximum repair |
| --- | ---: | --- |
| Depth bands | `0.15` | Hydrogen `150 m`; sulfur `40 m`; never cross land/water boundary |
| Temperature | `0.15` | Regional anomaly shift up to `8 °C` |
| Volcanic activity | `0.10` | Increase/decrease up to `0.25` |
| Emission profile | `0.20` | Source multiplier up to `1.5×` or down to `0.67×` |
| Founding resources/quotas | `0.20` | Starting pool up to `2×`; ongoing source up to `1.5×` |
| Advanced micronutrient gates | `0.10` | Reduce an existing amount by at most `50%`; cannot turn a rich tile into zero |
| Light via depth/cloud/turbidity | `0.10` | Only within the individual bounds above; never alter latitude/time |

Each dimension reports a normalized `0..1` deficit before weighting. Total repair cost must not exceed `0.55`. Repaired pairs must be disjoint and pair centers must be at least four toroidal Manhattan steps apart. Stable ordering is total score, then hydrogen coordinate, sulfur coordinate, and orientation.

The generator first retains natural eligible pairs, then repairs the best near-valid candidates until it reaches four or exhausts candidates. Failure to reach four triggers the next named generation attempt; the eighth failure returns a stable setup error.

# Biological smoke-test acceptance

Each candidate pair runs a deterministic, isolated 450-hour simulation from its own local-dawn/equinox offset with the real generated climate, sources, contention, health, uptake, and square-fixture-independent light curve.

| Outcome | Acceptance range |
| --- | ---: |
| Survival through first reproduction | At least `95%` of each founder population |
| Minimum average health before first reproduction | `>= 0.30` |
| Hydrogen first reproduction | Tick `300..420` |
| Sulfur first reproduction | Tick `250..275` |
| Sulfur/Hydrogen reproduction-time ratio | `0.65..0.85` |
| Extra micronutrient set complete before reproduction | At least `99%` of surviving organisms |
| Source-free first-ring carrying ability | Fewer than 15 equivalent founders, as required by the gas fixture |
| Advanced gated capability activation | Zero |

The smoke test is a setup validator, not a hidden balance bonus. It uses the same compiled DNA and simulation systems as play. A failure either consumes repair budget and reruns or rejects the pair.

# Representative-seed acceptance suite

Before freezing these values, run at least 128 named seeds and retain maps, distributions, repair records, and smoke-test outcomes. The candidate passes when:

- Every accepted world meets its exact aquatic target within one tile of quantile rounding.
- Every accepted world has at least four disjoint eligible pairs after bounded repair.
- At least half of retained pairs across the corpus are naturally valid; repair cannot be the dominant source of all playable geography.
- Median repaired-pair count is at most two, and 95% of accepted worlds succeed within the first three generation attempts.
- No repair exceeds `0.55`, changes more than eight tiles, or erases a rich advanced micronutrient deposit into an artificial zero.
- Temperature, precipitation, moisture, terrain, and volcanism meet their distribution targets.
- Hydrogen and sulfur smoke-test timings remain in range, with sulfur faster in every accepted pair.
- The same suite is bit-identical under save/reload and every supported worker count.

# Dissolved-organic exchange calibration

The first dissolved field uses the actual `32 × 17` world topology with wrapped `x`, bounded `y`, one central aquatic source, uniform aquatic compatibility, no biology, and the fixed `LabileDissolvedOrganic` passive-loss coefficient of `2,061` per million per hour. Source magnitude is arbitrary for the ratio comparison because the system is linear.

For each candidate, the field was iterated through source, passive loss, and stable-view symmetric exchange until the maximum per-tile change was below `10^-7` resource quantum. Ring distance is Manhattan distance on the wrapped grid.

| Base exchange per edge-hour | First ring / source | Second ring / source | Third ring / source | Stock-weighted RMS distance |
| ---: | ---: | ---: | ---: | ---: |
| `0.025%` | `8.47%` | `1.07%` | `0.14%` | `0.70` tiles |
| `0.050%` | `13.43%` | `2.66%` | `0.55%` | `0.99` tiles |
| `0.075%` | `16.92%` | `4.20%` | `1.09%` | `1.21` tiles |
| `0.100%` | `19.60%` | `5.60%` | `1.67%` | `1.39` tiles |
| **`0.150%`** | **`23.58%`** | **`8.02%`** | **`2.85%`** | **`1.71` tiles** |
| `0.200%` | `26.48%` | `10.03%` | `3.98%` | `1.97` tiles |
| `0.300%` | `30.63%` | `13.23%` | `5.99%` | `2.40` tiles |

`0.15%` is the highest tested value that keeps the labile first ring below `25%` and the second ring below `10%`. The selected no-biology field continues to `1.05%`, `0.39%`, `0.15%`, and approximately `0.02%` at rings four, five, six, and eight. It is locally leaky rather than globally mixed.

Using the same molecular-mobility coefficient with the longer-lived `ReducedFermentationProducts` pool deliberately creates a wider future respiratory/syntrophic niche. Its 90-day passive-loss coefficient produces `41.45%`, `23.53%`, `14.06%`, and `8.70%` at rings one through four and a stock-weighted RMS distance of `4.14` tiles. The field falls to `1.59%` by ring eight, `0.81%` by ring ten, and `0.09%` by ring sixteen, so it remains non-global on the default world.

## Biological interpretation

A continuous source equivalent to one fully decayed mature corpse per day supplies:

```text
4,000 LabileDissolvedOrganic / 24 hours
    = 166.667 substrate/hour
```

At the selected exchange and with no biological consumption, this produces approximately `24,910` substrate in the source tile, `5,874` as the first-ring mean, and `1,998` as the second-ring mean at steady state. When an uncapped diagnostic drain continuously consumes one tile, the field's maximum sustainable deliveries are:

| Consumer location | Maximum continuing grant | Fermenter maintenance equivalents at `32.5` substrate/hour |
| --- | ---: | ---: |
| Source tile | `165.56/hour` | `5.09` |
| Axial first-ring tile | `39.03/hour` | `1.20` |
| Axial second-ring tile | `9.89/hour` | `0.30` |
| Diagonal second-ring tile with two source paths | `16.66/hour` | `0.51` |
| Axial third-ring tile | `2.67/hour` | `0.08` |

The selected v1 fermenter plans `120` attempts and therefore requests `108` successful extents/hour on average under ideal conditions; the source field's `165.56/hour` hydraulic value is not a higher organism cap. One corpse-equivalent per day can barely maintain one first-ring fermenter and cannot maintain one in any second-ring position, before growth, stress, age, or competition. The population replacement boundary is much higher than maintenance—approximately `94.01/hour` for the hydrogen-derived profile—so the adjacent tile remains a marginal extension rather than a robust independent population source. See [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md).

For a fully wet coast with an aquatic source adjacent to terrestrial land, the `0.10` coast modifier produces approximately `5.00%` of source stock in the first terrestrial tile and `0.59%` in the next land tile. Same-row aquatic neighbors remain near `24–27%`. Lower surface moisture reduces coastal and terrestrial transfer linearly and dry land closes it.

## Fixed values and acceptance

- `DissolvedMobile` base rate is `1,500` per million per fully compatible edge-hour.
- Aquatic/aquatic compatibility is `1.00`.
- Aquatic/terrestrial compatibility is `0.10 × terrestrialSurfaceMoisture`.
- Terrestrial/terrestrial compatibility is `0.25 × min(surfaceMoistureA, surfaceMoistureB)`.
- `LabileDissolvedOrganic` and `ReducedFermentationProducts` use this profile; all other non-gas resources remain `TileBound` until explicitly reassigned.
- No-biology labile-field tests accept first-ring `23.0..24.2%`, second-ring `7.6..8.4%`, third-ring `2.6..3.1%`, and RMS distance `1.65..1.77` tiles under exact fixed-point execution.
- The one-corpse/day axial first-ring drain must grant `37..41` substrate/hour; every second-ring drain must remain below the `32.5` maintenance threshold.
- A fully wet coastal first land tile must remain below `6%` of source stock and its next land tile below `1%`.
- Exchange remains mass-conserving, iteration-order-independent, stable at maximum compatibility, and identical across save/load and worker counts.

# Recalibration triggers

Rerun the complete seed and biological suite after changing:

- Grid dimensions, latitude mapping, axial tilt, calendar, start season, or starting local time.
- Elevation/depth distributions, aquatic fraction, terrain curves, or volcanic geography.
- Temperature, weather, precipitation, cloud, turbidity, or moisture coefficients.
- The solar lookup, depth-light curve, phototrophy throughput, or sulfur assembly/reserve policy.
- Founder tolerances, health/stress curves, metabolism yields, quotas, uptake, reproduction, or mutation pacing.
- Atmospheric sources, sinks, exchange, accessibility, or volcanic emission profiles.
- Starting-pair eligibility, repair weights, repair budget, or required pair count.
- Dissolved-organic transport or remnant recycling coefficients.

# Remaining decisions

- Validate or revise every distribution target against actual 128-seed generated maps.
- Decide exact coarse bands exposed in reduced exploration views.
- Decide whether any additional non-gas resources need passive mobility. Dissolved-organic exchange is fixed; directional runoff remains deferred. Remnant decay and passive mineralization are owned by [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).
- Decide whether volcanic pulses need an opening grace rule after real Survival playtests; the first candidate has no artificial grace but starts with no active pulse.

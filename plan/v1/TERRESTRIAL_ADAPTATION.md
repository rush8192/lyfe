# Terrestrial Adaptation

Status: first v1 landfall gate, moisture-survival stages, observable dormancy policy, costs, opportunity model, succession arc, migration/surface-movement values, and executable cohort calibration selected provisionally; generated-climate and full coupled ecosystem validation remain

Sources: [world and climate](WORLD_AND_CLIMATE.md), [health calibration](ORGANISM_HEALTH_CALIBRATION.md), [spatial calibration](SPATIAL_CALIBRATION.md), [trait catalogue](TRAIT_CATALOGUE.md), [lifecycle and recycling](LIFECYCLE_AND_RECYCLING.md), [resource model](RESOURCE_MODEL.md), [oxygenic photosynthesis](OXYGENIC_PHOTOSYNTHESIS.md), and [organism vision](../../vision/ORGANISMS.md).

# Purpose and v1 boundary

Terrestrial occupation should be an evolutionary commitment rather than something an aquatic organism gains merely because a neighboring land tile happens to be wet. V1 models microbial-scale landfall onto seasonally moist surfaces, not vascular plants, macroscopic support against gravity, soil horizons, roots, or permanently active life on completely dry land.

The design separates four questions:

1. **Can the organism physically occupy a terrestrial tile?** One explicit DNA capability is a hard migration prerequisite.
2. **How well can it function at the current moisture?** A continuous preferred-to-hard response imposes energy, health, throughput, and death pressure.
3. **Can it wait through a dry interval?** Optional dormancy trades active growth and reproduction for survival.
4. **Why accept the cost?** Land exposes light, atmospheric gases, unoccupied space, and patchy geological resources without granting a habitat-wide yield bonus.

This avoids both extremes: land is not a free new resource field, but every degree of dryness does not require a separate binary mutation.

# Biology-inspired abstraction

Microbial terrestrial survival does not depend on one universal invention. Surface-associated microbes can retain microscopic water around aggregates, and extracellular polymeric material can form hydrated local environments. Compatible-solute systems can protect proteins and membranes under osmotic or drying stress, while repair systems become important because drying can damage DNA. Terrestrial phototrophs may also invest in extracellular matrices and photoprotective pigments.

V1 collapses those mechanisms into two player-facing packages:

- `WetSurfaceColonization` represents membrane/envelope stability, osmotic control, and enough hydration retention to function on a wet surface.
- `IntermittentDesiccationTolerance` represents stronger compatible-solute, stabilization, and repair machinery for repeated wet/dry cycles.

The abstraction is supported by experimental evidence that bacterial aggregates retain microscopic droplets associated with higher survival ([Grinberg et al.](https://pubmed.ncbi.nlm.nih.gov/31610846/)); compatible solutes can contribute to osmotic and desiccation protection, with strongly context-dependent effects ([Zeidler and Müller](https://pubmed.ncbi.nlm.nih.gov/30277310/)); and disruption of several DNA-repair pathways reduces desiccation tolerance ([Matthews et al.](https://pubmed.ncbi.nlm.nih.gov/34031124/)). Fossil microbial mats in a fluvial setting show that terrestrial microbial ecosystems existed by approximately 3.22 billion years ago ([Homann et al.](https://www.nature.com/articles/s41561-018-0190-9)). Modern subaerial mats demonstrate photosynthesis, rapid carbon turnover, mineral retention, and cycling of elements released by rock weathering ([Havig et al.](https://www.nature.com/articles/s41467-019-11541-x)); experiments also show cyanobacteria accelerating release of several elements from basalt and rhyolite at different rates ([Olsson-Francis et al.](https://pubmed.ncbi.nlm.nih.gov/22694082/)). UV-induced extracellular pigments and glycans in terrestrial cyanobacteria support a future radiation-protection branch if LYFE adds explicit surface UV exposure ([Ehling-Schulz et al.](https://pubmed.ncbi.nlm.nih.gov/9068639/)). These findings motivate the categories and terrestrial opportunities, not LYFE's numerical costs.

# Trait path and the landfall tax

```text
BasalAquaticTolerance                                  Foundation
└── WetSurfaceColonization                             Middle
    └── IntermittentDesiccationTolerance               Late

WetSurfaceColonization additionally requires SelectiveMembrane.
DormantPhase and ResistantDormantPhase are optional cross-family paths.
EnvironmentalAnchoring, EnvironmentalDrifting, SurfaceGliding,
and active locomotion are optional spatial strategies.
```

The initial trait candidates are:

| Trait | MP / complexity | Moisture preferred / hard | Structure | Maintenance | Passive-acquisition ceiling | Principal effect |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `WetSurfaceColonization` | `120 / 2` | `0.70 / 0.30` | `×1.10` | `+10/hour` | `×0.90` | Grants terrestrial occupation |
| `IntermittentDesiccationTolerance` | `180 / 3` | `0.40 / 0.10` | additional `×1.15` | additional `+15/hour` | replaces with `×0.80` | Extends active function through moderate dry periods |

`PassiveSmallMoleculeUptake` and `OrganicResourceUptake` use the passive-acquisition ceiling multiplier. Explicit active transport, ingestion, predation, and metabolic reaction yields are not silently changed. A protected envelope may reduce what crosses passively, but it cannot change reaction stoichiometry or make internal energy use more efficient.

The structure multipliers increase the compiled non-storage mature target and assign the added protective matter to `OrganizationStructure`; they do not change geometric radius or multiply a separately compiled `StorageStructure` target. All required matter must be physically grown after speciation. Mutation grants no protective matter, compatible solutes, reserve, or micronutrients. Reproduction must provision the larger body through the ordinary zero-sum transaction. The maintenance additions are constitutive while active; dormant-phase multipliers apply only where the lifecycle rule explicitly marks them phase-scalable.

These prices and liabilities are first calibration hypotheses. They make landfall comparable to other middle-game niche changes and make active desiccation tolerance a substantial late commitment without requiring complex-cell organization.

# State and tick ownership

Terrestrial adaptation adds no per-organism habitat flag beyond compiled DNA and the organism's existing tile membership. The authoritative inputs are:

- Fixed tile elevation, terrain tags, lithology profile, climate baselines, and initial resource profile.
- Dynamic tile surface moisture and ordinary resource accounts.
- Compiled species fields for terrestrial admission, active moisture thresholds, structural target, maintenance, passive-acquisition ceiling, and environmental-spread profile.
- Existing lifecycle phase, behavior, position, stores, quotas, and health inputs.

Tick ownership follows the common pipeline:

1. Phase 1 updates surface moisture and derives one immutable condition view.
2. Phase 3 records moisture stress and death probabilities before land organisms can move or gather.
3. Phase 4 applies the current activity factor to active movement, the terrestrial medium multiplier to Brownian displacement, and the land-admission gate at every crossed edge.
4. Phase 5 constructs light, gas, inorganic, micronutrient, organic, scavenging, and predation opportunities using the same condition view.
5. Phase 6 resolves finite claims and transfers; phase 7 runs internal processes and pays the full active maintenance liabilities.
6. Phase 8 applies ordinary lifecycle and zero-sum reproduction rules; phase 11 records resource, condition, movement, and knowledge histories.

Derived activity and access factors may be cached by `(speciesId, tileId, environmentVersion)` but are never independent save state. Crossing into a hostile tile in phase 4 first exposes the organism to that tile's acquisition limits during the current tick and to its intrinsic death draw in the next phase-3 evaluation, as already required by the migration rule.

# Occupation and migration gates

Any transition from an aquatic tile to a terrestrial tile, or between terrestrial tiles, uses the ordinary edge trace and migration transaction. Terrestrial destination admission adds exactly one hard capability gate:

```text
if destination.isTerrestrial and
   not organism.DNA.has(WetSurfaceColonization):
    reject migration as physically ineligible
```

The trait must be acquired while the lineage still occupies an eligible existing tile. This is the intended landfall tax: the player cannot inspect a favorable land tile and move ordinary aquatic founders there first, nor can speciation create organisms in an unoccupied tile.

Current moisture is a **soft compatibility gate**, not another capability flag. The destination's moisture condition factor contributes to migration success for active and passive crossings alike. Once admitted, the organism experiences the ordinary moisture maintenance, health, metabolic-throughput, reproduction-health, and death rules. Locomotion can reach a dry tile more reliably; it cannot make the organism physiologically able to survive there.

V1 does not impose an absolute moisture value below which a land-capable organism is forbidden to enter. Below its hard threshold, it instead faces the configured direct death probability and severe condition loss beginning on its next intrinsic-death evaluation. This permits risky dispersal and short-lived arrivals without letting a DNA-incompatible aquatic organism land at all. The selected migration rule floors compatibility at `0.02` only after the hard DNA gate, giving a fully directed shoreline attempt at zero moisture just `1.2%` success; zero activity then permits no gathering before the next death evaluation. A small nonzero activity can produce only its proportionally limited claim opportunity, so hostile land cannot become a useful one-tick resource exploit.

# Moisture response and dormancy

The common water/moisture curve remains normative:

- At or above the preferred minimum, the moisture channel contributes no stress.
- Between preferred and hard minima, convex severity raises maintenance and lowers health.
- Below the hard minimum, the channel additionally creates an increasing per-hour death chance.

The active and dormant profiles are:

| State or capability | Preferred minimum | Hard minimum | Meaning |
| --- | ---: | ---: | --- |
| `WetSurfaceColonization`, active | `0.70` | `0.30` | Wet-season or persistently damp surface life |
| `IntermittentDesiccationTolerance`, active | `0.40` | `0.10` | Active through moderate drying |
| `DormantPhase` | `0.20` | `0.05` | Inactive survival through a dry interval |
| `ResistantDormantPhase` | `0.05` | `0.01` | Costly resistant state for severe but nonzero moisture |

Dormancy does not grant terrestrial occupation and cannot bypass the `WetSurfaceColonization` gate. It disables growth, reproduction, active movement, predation, and scavenging as already specified. An organism that fails to enter dormancy before a sudden dry tick still experiences death evaluation in normal phase order; no trait retroactively avoids an earlier draw.

Surface moisture is an inexhaustible boundary condition, not stored water that organisms can deplete. Low moisture limits physiological opportunity and survival; it does not create a hidden finite water account.

## Observable dormancy policy

`DormantPhase` supplies the physiological state; the first `MoistureConservation` behavior policy decides when to request it. In v1 the phase has `ResourceConservation` as a cross-family hard prerequisite, and the pair compiles this policy; there is no player-triggered or policy-free dormancy mode. The decision is inherited DNA behavior, not a direct player action. It reads only the organism's post-maintenance reserve, current phase, current tile moisture, and two completed trailing 24-hour moisture windows ending at the current tick. It does not read future weather, day-of-year, the tile's climate normals, hidden client information, or another simulation trajectory.

The thresholds compile relative to the organism's active moisture response:

```text
emergencyEntryMoisture = activeHardMinimum
entryWarningMoisture = min(activePreferredMinimum,
                           activeHardMinimum + 0.05)
exitRecoveryMoisture = min(activePreferredMinimum,
                           entryWarningMoisture + 0.10)
trendWindow = 24 h
minimumPhaseDwell = 24 h
entryReserveHorizon = 24 h
```

For the `WetSurfaceColonization` profile, these become `0.30`, `0.35`, and `0.45`. For `IntermittentDesiccationTolerance`, they become `0.10`, `0.15`, and `0.25`; acquiring stronger active tolerance therefore does not make the organism sleep through conditions in which that investment lets it function.

```text
EvaluateMoistureConservation(organism, conditionHistory, compiledDna):
    if organism lacks DormantPhase:
        return StayInCurrentPhase

    if hoursSincePhaseEntry < 24:
        return StayInCurrentPhase

    priorTotal = sum(moisture from hours T-47 through T-24)
    recentTotal = sum(moisture from hours T-23 through T)

    if organism is active:
        earlyWarning = currentMoisture <= entryWarningMoisture
                    and recentTotal < priorTotal
        emergency = currentMoisture <= emergencyEntryMoisture
        entryCostFloor = entryWork
                       + 24 * (compiledDormantRecurringUpkeep
                               + currentDormantEnvironmentalStressCost)

        if (earlyWarning or emergency)
           and reserve >= entryCostFloor:
            request DormantPhase

    if organism is dormant:
        recovered = currentMoisture >= exitRecoveryMoisture
                 and recentTotal > priorTotal
        exitCostFloor = exitWork
                      + currentActiveMandatoryCostForOneTick

        if recovered and reserve >= exitCostFloor:
            request ActiveMature
```

Because both windows have 24 samples, the runtime compares their checked integer totals and performs no division. Comparisons are inclusive at the three moisture thresholds and strict for trend direction. Equal totals do not cause a transition. The existing `phase_entered_tick` supplies dwell state; no per-organism trend counter or forecast is added. Entry affordability prevents spending the last reserve on a state change that cannot fund even one day of baseline dormancy. Exit affordability prevents an immediately unpaid active tick. These are admission checks, not survival promises: current stress, a longer dry interval, other liabilities, and sudden weather can still kill the organism.

The request is evaluated in phase 8 after this tick's death and metabolism. An early-warning transition therefore affects tick `T+1`; emergency entry at the hard threshold can protect later ticks but cannot erase a phase-3 death draw already evaluated on tick `T`. The `0.05` warning lead, `0.10` exit hysteresis, opposing trend tests, and 24-hour dwell jointly prevent one noisy observation from paying repeated `500/250` transition costs.

This first selector is moisture-driven and activates only for a terrestrial organism with a surface-moisture condition. It does not infer impending food, gas, light, predation, temperature, or toxin shortages from one low current value. Aquatic and non-moisture dormancy policies remain part of the general behavior pass because they need explicit observable trend inputs and wake conditions; `ResourceConservation` must not turn into a hidden future-resource oracle.

Every terrestrial tile retains a 48-hour authoritative `RatioQ` surface-moisture ring and two checked rolling 24-hour totals. World spin-up supplies the initial 48 gameplay-prehistory samples; save/load preserves the ring position and totals. The data belongs to world history rather than player knowledge, and observing or hiding a tile cannot change an organism's transition. Aquatic tiles do not allocate the ring, and organisms without the behavior profile do not read it for dormancy decisions.

# Terrestrial passive and active movement

The first v1 medium multiplier is:

```text
terrestrialMediumBrownianMultiplier = 0.50

effectivePassiveRms = founderBrownianRms
                    * bodyScaleMultiplier
                    * lifecycleMultiplier
                    * terrestrialMediumBrownianMultiplier
                    * environmentalSpreadMultiplier
                    * sqrt(tickDurationHours)
```

For a primitive-scale active organism, the resulting passive profiles are:

| Profile | Effective RMS relative to aquatic baseline | Uniform edge intersections per 100/day |
| --- | ---: | ---: |
| `EnvironmentalAnchoring` | `0.125×` | approximately `0.66` |
| Free suspension | `0.50×` | approximately `2.65` |
| `EnvironmentalDrifting` | `0.75×` | approximately `3.97` |

This makes terrestrial populations more spatially persistent while retaining a reason to evolve drifting. Drifting remains nondirectional and does not receive a second bonus to migration probability. Active movement uses its separately configured surface eligibility, speed, and energy-cost rules. `SurfaceGliding` is the first efficient terrestrial movement specialization: it requires `WetSurfaceColonization`, works on every v1 terrestrial tile without another terrain tag, and falls back to basal active motility in water while paying its higher upkeep.

The selected boundary-composition rule makes a maximally compatible water-to-land attempt succeed at `7.5%` when purely Brownian and `60%` when fully directed. For one selected wet shore, 100 uniformly distributed aquatic organisms produce median successful passive landfalls of approximately `0.023`, `0.099`, and `0.149` per day under anchored, baseline, and drifting profiles. Directed basal movement from tile center reaches a successful wet landfall in approximately `129.67 h`; after landfall, the comparable land-to-land times are `129.25 h` with basal movement and `86.25 h` with surface gliding. These are isolated opportunity values, not population-growth forecasts. Full assumptions and the reproducible authoring fixture are in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md).

# Resources, gases, light, and reactions

- Terrestrial organisms receive the existing full atmospheric-gas accessibility. Possessing the occupation trait does not grant a gas metabolism or tolerance.
- Surface moisture supplies boundary water to a declared reaction without being depleted, but the reaction still requires an adequate moisture condition factor and all other substrates, catalysts, output capacity, and energy opportunity.
- Terrestrial sunlight has no aquatic depth or turbidity attenuation. Cloud and seasonal insolation still apply. Photoprotective UV is not inferred from visible-light opportunity in v1.
- Dissolved-organic edge exchange already scales with terrestrial moisture compatibility. A land organism consumes only the destination tile's actual accessible stocks.
- Solid and poorly transported nutrients remain tile-local. Landfall creates no default soil resource bonus.
- Temperature, volcanic toxicity, oxygen toxicity, micronutrient quotas, predation, and senescence remain independent stress or capability systems.

The terrestrial pass must not add a generic metabolism multiplier merely for being on land. Each external reaction uses its named gas, light, moisture, and resource gates; internal reaction recipes remain unchanged.

## Moisture-dependent activity

Surface moisture affects active physiology separately from its maintenance, health, and death effects. For any active terrestrial phenotype, derive one explicit throughput factor from that phenotype's current active preferred and hard minima:

```text
TerrestrialActivityFactor(moisture, preferred, hard):
    if moisture >= preferred:
        return 1.00
    if moisture >= hard:
        progress = (moisture - hard) / (preferred - hard)
        return 0.25 + 0.75 * progress
    if hard > 0:
        return 0.25 * moisture / hard
    return 0
```

The factor caps requested external energy capture, passive or active environmental-resource uptake, and active movement after each capability's ordinary limit is calculated. It does not reduce mandatory maintenance, internal reserve catabolism, death probabilities, or reaction stoichiometry. Dormancy uses its lifecycle rules and requests none of those active processes. Computing the factor once from the phase-1 condition view prevents rain arriving midway through a tick from changing only some actions.

This is an intentional physiological consequence, not a second health calculation. Telemetry reports health effects, stress energy, death risk, and throughput loss separately so balance work can identify over-penalization.

# Why land can be profitable

Land is a specialization with different opportunities, not a higher habitat tier. V1 creates its upside only through ordinary systems:

- Terrestrial light uses current surface solar opportunity with cloud and seasonality, but without aquatic depth or turbidity attenuation.
- Atmospheric gases have full terrestrial accessibility. The organism must still own the reaction and tolerance that use them, and all gas stocks remain finite and contested.
- Terrestrial resource initialization is patchy by lithology. A tile can be rich in particular bioavailable micronutrients or inorganic macronutrients while poor in others; no land tag enriches every resource.
- Newly reached land normally has little biological competition because organisms must arrive through real migration. There is no explicit low-competition multiplier or protection from later colonists.
- Biomass, remains, dissolved organics, scavengers, and predators appear through the existing mass-balanced lifecycle. Landfall creates none of them automatically.

The hydrogen and sulfide founding metabolisms therefore receive no general reason to leave their volcanic habitat. Ordinary non-volcanic land lacks a continuing H2 or H2S source. The first broadly attractive terrestrial primary-producer route is expected to be oxygenic photosynthesis or another future metabolism that can exploit surface light, atmospheric input, and boundary water without a scarce volcanic electron donor.

## Patchy geological endowments

`TileFixedState.lithology_profile_id` drives the terrestrial portion of `resource_initialization_profile_id`. Rank classification targets `30%` mafic, `30%` silicic, and `40%` mixed terrestrial tiles. The first profiles use `1.75x` for enriched, `1.25x` for secondary enrichment, `1.00x` for ordinary, and `0.50x` for depleted starting endowments before world-total reconciliation:

| Profile | `1.75x` enriched | `1.25x` secondary | `0.50x` depleted | Intended niche signal |
| --- | --- | --- | --- | --- |
| `MaficExposedRock` | Ca, Mg, Fe, Ni, Co | Inorganic sulfur | K | Metal-rich catalytic opportunities with an important shortage |
| `SilicicExposedRock` | K, Na | Inorganic phosphorus | Mg, Ni, Co | A contrasting storage/osmotic resource profile |
| `MixedWeatheredSurface` | Two keyed distinct entries from `Zn, Cu, Mn, Mo, Se` | None | Two different keyed entries from the same list | Broad but non-optimal stepping-stone habitat |

All unlisted inorganic resources and micronutrients use `1.00x`. The mixed profile selects enriched entries first in stable keyed order, then depleted entries without replacement. Profiles and keyed trace choices vary in spatially correlated regions rather than independently per tile. After applying them, generation rescales each resource across the whole world to its configured target with deterministic largest-remainder allocation. Enrichment therefore redistributes initial finite matter rather than creating a larger world inventory. Every generated land region must expose at least one meaningful relative strength and one relative shortage; world generation rejects an accidental universally rich land profile.

Micronutrients remain a single bioavailable form. V1 does not simulate mineral compounds, soil horizons, or a finite bedrock ledger. Instead, current surface moisture controls the opportunity to contact the tile's already tracked `InorganicPool` and `MicronutrientPool`:

```text
terrestrialGeologicalAccessFactor = 0.20 + 0.80 * surfaceMoisture

geologicalTileClaimLimit = ordinaryTileClaimLimit
                         * TerrestrialActivityFactor(...)
                         * terrestrialGeologicalAccessFactor
```

This factor applies only to environmental uptake from those two tile pools. It does not multiply gas access, light, boundary water, organic uptake, remnant consumption, predation, or internal reactions. It changes how much of an existing stock may be claimed in the tick; it never credits, converts, or transports matter. Current moisture already integrates recent precipitation, so no second rainfall-history state is needed.

Ongoing biological rock weathering is deferred. A later `MineralWeathering` or extracellular-matrix capability may transfer matter from an explicit finite geological reservoir into tracked pools, but it cannot be implemented as a free source. V1 represents pre-game weathering through initial stocks and represents rainfall through the access factor above.

# Terrestrial succession

The first land ecosystem should emerge in a legible order without scripted species or free resources:

1. A light- and atmosphere-using producer pays the landfall tax and occupies a sufficiently wet surface.
2. Reproduction and death create local biomass and remains; ordinary decay creates organic tile resources.
3. Scavenging, organic uptake, and fermentation become viable only when measured biological stocks and flows support them.
4. Predation becomes viable only after real prey density and encounter rates pass its normal opportunity thresholds.
5. Drying selects among retreat, seasonal population loss, active desiccation tolerance, and dormancy; movement traits determine whether descendants anchor or spread.

If a heterotroph or predator can maintain indefinitely in an initially sterile land fixture, the resource or accounting model is wrong. If later succession requires a hard-coded land bonus rather than producer-created material, the fixture is also wrong.

# Landfall calibration fixtures

The first paired fixture isolates the benefit before adding competition:

| Field | Shallow coastal water | Wet coastal land |
| --- | ---: | ---: |
| Elevation/depth | `-20 m` | `+50 m` |
| Latitude/time | Same | Same |
| Cloud | `0.10` | `0.10` |
| Turbidity | `0.10` | Not applicable |
| Surface moisture | Not applicable | held at `0.85` |
| Non-gas stocks | Non-limiting matched test stocks | Non-limiting matched test stocks |
| Competitors/predators | None | None |

Run the same oxygenic producer phenotype in both, adding only the minimum `WetSurfaceColonization` structure and upkeep to the land organism. Give both sides non-limiting output capacity and CO2 so this first comparison isolates light rather than reserve saturation or contention. The land organism reads unattenuated surface light; it receives no fixture-only production bonus. Across 64 named reaction-randomness keys, compare the median successful gross extents for the same reference day and target:

```text
land gross external energy capture / aquatic gross external energy capture
    = 1.20 .. 1.35
```

The ratio is an authoring target, not a promise that land always wins. If the ordinary light and gas equations put it outside the band, calibrate their declared saturation and accessibility interaction before adding a land scalar. The wet-land lineage should at least replace itself after paying the landfall liabilities, while the aquatic phenotype remains competitive in water.

A second control sets aquatic turbidity to zero. Under the current `20 m`, `0.80` depth factor, `0.10` cloud, `0.75` photosynthetic saturation, and non-limiting CO2, terrestrial light alone is expected to provide only approximately `1.15x` gross capture. The representative `1.20..1.35` target includes ordinary `0.10` coastal turbidity and should land near `1.24x`; it must not be achieved by silently changing a reaction yield. Full atmospheric CO2 access becomes an additional advantage only when aquatic accessibility or contention is actually limiting.

A companion gas-isolation control gives both habitats the same synthetic light opportunity and raw CO2 stock, removes volcanic full-access overrides, and supplies enough output capacity to emit every supported claim. At `20 m`, ordinary aquatic access is `100 / (100 + 20) = 0.833333`, while terrestrial access is `1.0`; the land organism must therefore see exactly `1.20x` accessible CO2 before contention. A population claim load between those accessible amounts must constrain the aquatic side while leaving the land side unconstrained. This proves the gas advantage exists without incorrectly multiplying every terrestrial photosynthetic extent when carbon is already abundant.

Then run three coupled fixtures:

1. **Permanent-wet tradeoff:** the minimum land phenotype persists and reproduces; adding intermittent-desiccation machinery makes it less fit than the lean phenotype when dryness never occurs.
2. **Seasonal interior:** monthly moisture crosses the wet-surface bands. The minimum phenotype grows faster during wet windows but cannot maintain a stable multi-year interior population; tolerance or well-timed dormancy can persist without eliminating visible seasonal boom/bust dynamics.
3. **Succession:** sterile land begins with no organic stock or prey. A producer establishes first, organic consumers become replacement-viable only after producer-derived flows exist, and predators only after adequate prey density. Every new stock reconciles to atmospheric, tile, organism, remnant, or boundary flows.

Primitive H2- and H2S-dependent controls on equivalent non-volcanic land must remain below replacement. A separate volcanic-land scenario may support them through its named emissions, but that is volcanism's benefit rather than a terrestrial bonus.

## First seasonal-interior hypothesis

The reproducible expected-value models [terrestrial_opportunity_calibration.py](calibration/terrestrial_opportunity_calibration.py) and [energy_storage_calibration.py](calibration/energy_storage_calibration.py) make the seasonal fixture concrete enough to constrain later engine tests. They hold temperature, CO2, nutrients, competition, and predation favorable, repeat the equatorial/equinox surface-light reference with existing `0.10` cloud on every test day, and apply this smooth 360-day isolation curve:

```text
surfaceMoisture(day) = 0.50 + 0.35 * cos(2π * day / 360)
```

The `0.15..0.85` range deliberately crosses the minimum land phenotype's preferred and hard bounds without crossing the intermittent-tolerance or ordinary-dormancy hard bounds. This is not a replacement for generated precipitation and hourly moisture recurrence. It is a deterministic stress fixture for separating trait effects.

The dormancy comparison uses the observable policy above. It enters the wet-surface phenotype at `0.35` while the trailing mean is falling and exits at `0.45` while it is rising, remaining dormant for approximately `146.4` days. Dormant upkeep is `30/hour`: `10` phase-scaled base maintenance plus the non-suppressed `10` oxygen-tolerance and `10` land-protection liabilities; disabled oxygenic machinery is suppressed. The ordinary `500/250` transition costs apply. If the rule compiler assigns different phase-scaling tags, this fixture must be regenerated.

| Phenotype/policy | Active days | Days below active hard | Annual net energy | Maximum dry-season reserve draw | Moisture-only annual survival | Nominal replacement capitals/year |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Wet-surface, always active | `360.0` | `110.3` | `867,480` | `250,896` | `1.58%` | `7.58` |
| Intermittent tolerance, active | `360.0` | `0` | `1,618,366` | `6,702` | `100%` | `12.35` |
| Wet-surface plus ordinary dormancy | `213.6` | `110.3` | `989,816` | `114,272` | `100%` | `8.64` |
| Wet-surface, compartmentalized reserve, and ordinary dormancy | `213.6` | `110.3` | `883,492` | `130,362` | `100%` | `4.46` |

Net energy and drawdown use an uncapped diagnostic ledger and assume the organism survives; they reveal energy opportunity and required buffer size rather than simulating a hidden infinite store. The survival column includes only moisture's direct death draws and intentionally excludes senescence and every other death cause.

Annual net production does not make the always-active wet phenotype seasonally viable: its individual probability of surviving all moisture-death draws is only about `1.58%`, and its dry-season reserve requirement exceeds primitive storage. Conversely, intermittent tolerance remains active and needs little bridging reserve in this curve, but pays more MP, structure, passive-acquisition loss, and maintenance.

Dormancy supplies a cheaper physiological alternative but is not self-sufficient. Its unmodified approximately `114,272` reserve draw is more than eleven times the primitive `10,000` capacity, so a lineage must combine dormancy with evolved energy storage, shorten its dormant interval through migration, or accept seasonal population loss. The fully costed `CompartmentalizedReserve` phenotype raises the draw to `130,362` through its storage and cellular-organization upkeep, but its selected `150,000` capacity leaves `19,638`, or `15.1%` of the modeled draw, when entering fully charged. Exact construction, health, reproduction, and tier calibration are defined in [ENERGY_STORAGE.md](ENERGY_STORAGE.md).

The first executable individual-based fixture is [terrestrial_population_validation.py](calibration/terrestrial_population_validation.py). Its one-dry-season cohort starts 128 age-zero, fully charged organisms on day 100, disables reproduction and optional growth so it measures the bridge rather than body construction, holds CO2 abundant, and ends on day 270 after recovery. Across eight keyed seeds, the selected observable policy retains a median `121/128` organisms (`119..126`); the no-storage policy loses every organism to unpaid dormant maintenance. A current-hard policy and a deliberately invalid next-tick-hindsight comparator each retain only a median `16/128` (`12..21`). They save approximately `5,000` reserve relative to the selected policy, but their extra active biological aging exposes the cohort to far more senescence. The old hard-threshold "oracle" was therefore only an energy-timing reference, not a population-survival upper bound.

The same fixture retains an intentionally harsh two-year source-limited population stress case: 12 full mature founders, only one source-day of initial CO2, and the ordinary `20,004/hour` source. Every seasonal profile eventually goes extinct across its first eight seeds, while both permanently wet controls persist. This does not invalidate the dry-season bridge—the one-day initial stock is not the world's historical CO2 background—but it exposes two integration risks for later behavior work: synchronized optional growth can overshoot a renewable resource, and the compartmentalized oxygenic/storage phenotype is only marginally replacement-capable under the primitive senescence curve. The next coupled fixture must vary realistic initial gas stock, growth/reproduction regulation, complementary metabolisms, and lineage size rather than silently disabling age or granting resources.

The permanently wet control retains the intended cost of unnecessary complexity. At moisture `0.85`, the minimum wet-surface producer nets `6,504/day` against a `114,500` replacement-capital landmark, or `17.60` nominal days. The intermittent-tolerant producer nets `6,144/day` against `131,000`, or `21.32` nominal days. A compartmentalized-storage wet-surface producer nets `6,072/day` against `198,050`, or `32.62` nominal days. These capital estimates include compiled structure at the existing 100-energy assembly cost per structural unit, plus profile result reserve and reproductive work. They omit finite capacity, second-result structural accumulation, growth floors, cooldown, and contention and therefore remain expected-value comparisons rather than promised split times.

# Weather and future environmental transport

V1 holds the terrestrial Brownian-like medium multiplier at `0.50`. Surface moisture, precipitation, and other weather do not modify passive organism displacement yet. This keeps the first land fixture separable from climate noise and avoids applying the same wet-season effect through survival, dissolved-resource exchange, and movement before each contribution is calibrated.

The extension points are explicit:

- Current surface moisture or recent precipitation may later apply a bounded zero-mean `weatherAgitationMultiplier` to passive displacement, representing splash, temporary water films, and small unresolved runoff.
- Wind and coherent runoff belong in the separately attributed directional `EnvironmentalDisplacement` vector, not in Brownian magnitude.
- Storms may temporarily raise both random agitation and a directional field, but each contribution must be bounded and observable separately.
- `EnvironmentalAnchoring` should reduce coupling to both random weather agitation and directional transport; `EnvironmentalDrifting`, buoyant stages, and resistant propagules may increase it.
- Moisture and weather may change dispersal without granting land compatibility or desiccation tolerance. A widely transported organism can still land in a lethal environment.

Any weather-driven movement remains deterministically keyed by world seed, tile, tick, event identity, and organism coupling. It must preserve zero signed expectation when configured as agitation, conserve organism identity, trace real tile edges, and produce the same state under worker-order and save/load changes.

# Autonomous evolution and observability

Land adaptations should respond to opportunity and pressure without reading hidden future outcomes:

- `WetSurfaceColonization` requires an occupied aquatic tile with at least one edge-sharing terrestrial destination and trailing destination conditions that sometimes exceed its hard moisture minimum.
- Its opportunity score may use only known coarse lithology, observed seasonal light/moisture bands, accessible atmospheric reactions, and the proposing species' actual resource requirements. It receives no value merely because a tile is terrestrial and cannot read hidden exact stocks in a reduced-visibility tile.
- `IntermittentDesiccationTolerance` may respond to repeated moisture stress, moisture-related death risks, failed reproduction during drying, and observed seasonal time below the wet-surface preferred minimum.
- Dormancy remains favored by predictable recurring poor intervals and sufficient reserve to pay transition costs.
- Drifting may score from crowding or repeated resource contention plus compatible neighboring habitat; anchoring may score from post-migration deaths, loss from a favorable tile, or persistent local biological-resource opportunity. These are pressure signals, not guarantees.

The live interface exposes the terrestrial-occupation tag, current moisture, preferred/hard thresholds, moisture severity, activity and geological-access factors, stress energy, death probability, light/gas accessibility, exact resource stocks and flows, passive-medium/profile multiplier breakdown, and the reason a migration was admitted or rejected. Reduced neighboring or previously inhabited tiles reveal stable lithology and coarse resource-strength bands plus already permitted coarse or last-observed climate information, never exact current stocks, organisms, remains, or weather.

# Required validation

1. An organism without `WetSurfaceColonization` can never enter or be founded in a terrestrial tile.
2. Acquiring the trait grants no structure, reserve, water, nutrients, or current land occupation; descendants must migrate normally.
3. Moisture response is continuous at preferred and hard thresholds, and lower moisture monotonically increases stress and then death risk. The activity curve returns `1.00` at the preferred minimum, `0.25` at the hard minimum, and `0` at zero moisture.
4. The two active tolerance stages and two dormant states reproduce their exact `0.70/0.30`, `0.40/0.10`, `0.20/0.05`, and `0.05/0.01` bands.
5. Dormancy without the landfall capability does not authorize terrestrial entry.
6. Terrestrial passive RMS equals `0.50×` the equivalent aquatic value before DNA profile effects; anchoring, baseline, and drifting reproduce effective `0.125×`, `0.50×`, and `0.75×` values.
7. A moisture-poor destination reduces both active and passive migration compatibility without receiving a duplicated DNA or movement bonus.
8. Terrestrial gas access, surface-water opportunity, geological access, dissolved-resource stocks, light, and reactions remain individually accounted and mass-balanced; neither access factor credits matter.
9. The wet-surface phenotype can maintain and reproduce in at least one seasonally wet terrestrial fixture but loses or retreats during an unadapted dry interval.
10. Intermittent tolerance or dormancy improves multi-season survival while losing to the leaner wet-surface phenotype in a permanently wet, otherwise equal tile.
11. Weather is movement-neutral in the v1 rule pack; enabling a future weather rule changes only the declared keyed displacement channels.
12. Save/load, worker count, client visibility, and dense iteration order do not change land admission, moisture risks, displacement, or state hashes.
13. Terrestrial lithology profiles redistribute fixed world resource targets, produce correlated relative strengths and shortages, and never generate a universally enriched land class. Geological access returns exactly `0.88` at moisture `0.85`, `0.52` at `0.40`, and `0.20` at zero before physiological activity is applied.
14. The paired wet-coast fixture obtains a `1.20..1.35` gross-capture ratio from ordinary light rules without a land yield scalar, while the clear-water control remains near `1.15`; the gas-isolation control produces exactly `1.20x` accessible CO2 at `20 m` without changing capture when CO2 is non-limiting. Land liabilities remain visible in net growth.
15. Initially sterile land cannot support a consumer or predator indefinitely; producer-created matter enables consumers and prey density later enables predators without breaking reconciliation.
16. The synthetic seasonal-interior fixture reproduces the profile rows above. In particular, active wet-surface annual moisture survival remains below `2%`, intermittent tolerance requires less than `10,000` reserve to bridge the worst interval, ordinary dormancy without storage requires `110,000..120,000`, and the fully costed compartmentalized-storage dormant phenotype draws `130,000..135,000` against its `150,000` capacity.
17. Baseline passive landfall through one selected favorable shore remains near `0.099` successes per 100 organisms/day; directed basal wet landfall remains near `129.67 h`, and surface gliding remains materially faster than basal land movement without changing physiological compatibility.
18. `MoistureConservation` uses no future weather or calendar input, produces one entry and one exit on the smooth seasonal fixture, does not chatter under the selected hysteresis/dwell rule, and reproduces the dry-season cohort ranges above.

# Remaining calibration work

- Run generated-map fixtures to measure how many land tiles and seasons fall into each active and dormant moisture band; replace the smooth seasonal isolation curve with representative generated hourly histories.
- Replace the smooth history with representative generated hourly histories and verify that the selected trend/hysteresis policy does not chatter or systematically mistime abrupt storms.
- Extend the first executable fixture with realistic initial gas backgrounds, exact keyed photosynthetic binomials, complete nutrients, resource-aware growth/reproduction regulation, migration, and multiple complementary metabolisms; use it to resolve the source-limited overshoot and advanced-phenotype replacement risks.
- Run the exact commissioned storage ladder through those generated-climate fixtures; its first values and isolated seasonal/stable-habitat tradeoff are fixed in [ENERGY_STORAGE.md](ENERGY_STORAGE.md).
- Revisit explicit UV exposure, extracellular hydration matrices, biofilm-scale shared protection, wind, runoff, and propagules after their environmental fields exist.

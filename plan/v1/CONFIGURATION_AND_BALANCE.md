# Configuration and Balance

Status: first configuration inventory and validation pass; authoring format, schemas, compiler layout, and rule hashing pending

Sources: all vision documents and [technology decisions](TECHNOLOGY.md).

# Purpose

Separate simulation rules and balance data from engine mechanics so that biology can be tuned without rewriting the hot loop, while ensuring every saved world remains tied to a reproducible rules version.

# Configuration layers

Plan a validated layering model for:

1. Engine constants that cannot change without a simulation-version change.
2. Versioned rules data for resources, traits, reactions, environments, and actions.
3. Scenario defaults such as grid size, final date, starting population, and speed presets.
4. Player-selected world options such as seed and permitted setup choices.
5. Development-only overrides used for tests and balance experiments.

The detailed plan must define precedence, validation, canonical serialization, hashing, and which layers are stored in a save.

# Data-driven definitions

Evaluate data schemas for:

- Resources with kind, CHNOPS composition vector, micronutrient composition, biological form, phase, energy density, permitted reservoirs, and uptake tags.
- Available-store capacity groups with eligible resource kinds, composition-derived load per quantum, DNA-compiled capacity, desired-inventory policy, and explicit overflow destinations. The founder groups and values are defined in [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- Energy-storage maximums, storage-structure targets, the capacity-per-structure slope, organization gate, cumulative commissioned/phase-scaled upkeep, reproduction assignment fractions, carrier-pool invariant, overflow behavior, trait prices, and calibration fixtures from [ENERGY_STORAGE.md](ENERGY_STORAGE.md).
- Fourteen single-form micronutrients and capability-specific structural/catalytic quotas.
- Passive micronutrient-uptake opportunity probability, per-opportunity quantum cap, target-selection policy, marginal energy cost, and trait modifiers.
- The fixed initial atmospheric catalogue: H₂, CO₂, CH₄, H₂S, SO₂, O₂, N₂, and NH₃.
- Gas emission profiles, full-activity source rates, environmental-sink rates and destinations, exchange rates, and symmetric edge-compatibility multipliers from [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- Global atmospheric-background targets and seeded ranges, diffuse background-maintenance sources, gas-accessibility classes, and the aquatic depth curve from [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- Boundary sources, sinks, and energy opportunities.
- Atomic metabolic, energy-use, biomass, and decomposition reactions.
- Metabolic consumed-input and tap requirements, binding durations/triggers, process classes, priority bounds, and holdback limits from [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- Linear dissolved-organic claims, merged per-organism weighted contention, the base and specialized uptake ceilings/weights, the 5-energy/hour regulation control cost, 10-energy/hour generalized-catabolism cost, 25% suppressed pathway-upkeep multiplier, and phase-5 suppression timing from [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md).
- Dissolved-resource class assignment, `1,500`-per-million base edge exchange, aquatic/coastal/terrestrial moisture compatibility, edge remainder policy, and field-acceptance bands from [WORLD_AND_CLIMATE.md](WORLD_AND_CLIMATE.md) and [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md).
- Trait families, levels, prerequisites, incompatibilities, costs, and effects.
- Stable canonical family IDs and typed effect domains for resource acquisition, external energy capture, internal metabolism, energy storage, cellular organization, behavior, defense, and the other families fixed in [EVOLUTION.md](EVOLUTION.md).
- Trait-family forests, global prerequisite predicates, incompatibilities, exact ancestor-effect supersession, mutation costs, change complexity, typed effects, activation requirements, pressure tags, and deterministic effect-composition metadata from [TRAIT_SYSTEM.md](TRAIT_SYSTEM.md).
- The initial founding loadouts, family-tree topology, cross-family milestones, evolutionary niches, and benefit/liability requirements from [TRAIT_CATALOGUE.md](TRAIT_CATALOGUE.md).
- Reproductive modes, deterministic eligibility gates, base cooldowns, bounded jitter windows, allocation profiles, growth-protection policies, lifecycle-phase multipliers and transition costs, evolved lifecycle modifiers, decay cohorts, environmental decay multipliers, mineralization rates, and lifecycle parameters from [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).
- Particulate buffer/ingestion/digestion limits and the exactly balanced combined structural-biomass digestion/catabolism reaction from [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).
- `LabileDissolvedOrganic` and `ReducedFermentationProducts` definitions; structural-decay yield; passive half-lives; organic-uptake throughput/upkeep; fermentation probability, throughput, upkeep, balanced reaction, and anti-cycle input tags from [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md).
- Material-opportunity profiles, required exact resource IDs, 168-hour history window, replacement-capital/horizon rules, stock/renewal weights, multiplier floor, tile-count-plan exponent, and explanation fields from [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md).
- `SpentReserveCarrier`, carrier-pool capacity/recharge rules, respiratory carbon-assimilation fractions, traits/prices/quotas/upkeeps, O2 tolerance bands, two balanced assimilation/oxidation reactions, shared throughput, regulation priorities, and coupled-claim parameters from [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md).
- Structural-assignment roles and targets, internal-process work loads, organization work budgets, radius scaling, biomass-assembly multipliers, the proto-eukaryotic respiratory-ceiling modifier, and processing-saturation scoring bands from [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md).
- Oxygenic reaction stoichiometry, photosystem prices/complexities/upkeeps, Mn/Ca quotas and commissioning, 720-hour quota-provisioning score, light slope/saturation/success, shared-photo budget, CO2 opportunity, O2 source fixtures, and paired tolerance bands from [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md).
- Environmental tolerances and stress curves.
- Terrestrial-occupation tags and prerequisites, moisture bands and activity curve, structural/upkeep/acquisition liabilities, lithology endowment profiles, moisture-dependent geological access, the `0.50` terrestrial passive-medium multiplier, migration compatibility inputs, seasonal-interior profile, `MoistureConservation` entry/exit offsets, trend window, dwell and reserve gates, succession fixtures, wet-coast advantage target, and weather-neutral v1 rule from [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md).
- Health-factor targets, structural targets, constitutive nutrient quotas, senescence curves, age-throughput curves, and lifecycle condition modifiers from [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md).
- Convex hot/cold tolerance-cost curves and their cross-axis breadth-coupling coefficient from [TRAIT_CATALOGUE.md](TRAIT_CATALOGUE.md).
- Gas-specific soft/hard chemical-exposure thresholds and DNA tolerance multipliers.
- Behavior and sensing capabilities.
- Spatial bin count, body/radius curves, Brownian direction table and magnitude curve, environmental-spread profiles and multiplier bounds, sensing and action reaches, active speed/turning limits, movement-energy costs, zero-valued v1 directional environmental displacement, reproduction spawn radius, migration hard gates, `0.10/0.80` active-control endpoints, `0.75` medium-transition factor, `0.02` compatibility floor, failure response, and probability composition from [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md).
- Predation trait prices/complexities/upkeeps, base odds, attack/defense attributes, eligibility and size bounds, attempt costs, pursuit/escape factors, ingestion caps, contested-feeding weights, hunting utility/hysteresis, kin-discrimination lineage distance, biological-opportunity profiles, and minimum/maximum kill probabilities from [PREDATION.md](PREDATION.md).
- Simple-scavenging action cost, handling cooldown, per-compartment transfer caps, compatibility rules, and trait modifiers from [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md).
- The exact hydrogen-acetogenesis and sulfide-anoxygenic-phototrophy founding definitions, trait paths, opening targets, and paired setup constraints from [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md).
- Scenario-defined allowed founding-metabolism IDs, tile-eligibility rules, setup-card metadata, and deterministic competitor-pairing policy. The v1 scenario contains exactly two; the engine must not encode that count.
- Mutation-price lint bands, exact per-node costs/complexity, milestone anchors, base and expanded per-event complexity limits, evolutionary-machinery modifiers/liabilities, and the speciation refractory interval from [EVOLUTION.md](EVOLUTION.md).
- Mutation-income effective-population table, normalization, DNA modifier bounds, `MutationQ` scale, fractional accumulation denominator, and numeric ceiling from [EVOLUTION.md](EVOLUTION.md).
- Autonomous pressure mappings/weights/decay, material-opportunity composition, evaluation cadence, savings horizon, exploration probability, goal invalidation rules, commit curve, base and opportunity-adjusted tile-count weights, and explanation retention from [EVOLUTION.md](EVOLUTION.md).
- Speciation tile fractions, founder rounding and selection-key domain, typed validation errors, and lineage-event fields from [EVOLUTION.md](EVOLUTION.md).
- World-generation distributions and climate parameters.
- Scenario world dimensions, odd-height/equator validation, calendar definition, axial tilt, aquatic-fraction target, elevation ranges, terrain thresholds, weather-field scales, moisture coefficients, depth-light curve, phototrophy throughput, volcanic-pulse parameters, starting-region eligibility, repair weights/budget, required pair count, generation-attempt limit, and seed-suite thresholds from [WORLD_AND_CLIMATE.md](WORLD_AND_CLIMATE.md) and [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md).

Data-driven does not mean arbitrary scripting. Frequently executed behavior should compile or resolve into efficient runtime structures during world initialization.

# Versioning rules

- An active world uses one immutable simulation-rules version.
- Hot-reloading rules into an active authoritative world is not a v1 requirement.
- Saves record the rules version and enough configuration to reject incompatible loading.
- Balance-only changes still require a version decision because they affect deterministic replay.
- Development fixtures should pin exact rule hashes.

# Validation

Configuration loading should reject:

- Missing referenced resources or traits.
- Cyclic prerequisites where not explicitly supported.
- Incompatible traits that are simultaneously required.
- Reactions that violate declared matter accounting.
- Energy-bearing outputs whose declared stored energy exceeds available reaction energy.
- Organic-catabolism graphs that accept generic spent-organic pools as fuel, permit `ReducedFermentationProducts` to feed the same fermentation reaction, or recreate an upstream higher-opportunity substrate without a declared external energy source.
- Resource definitions with zero/negative compositions or invalid reservoir placement.
- Available-store resources that resolve to zero or multiple capacity groups, have invalid/overflowing composition-derived load, or place current group load above capacity outside an atomic overflow transaction.
- Retained process products without both a compatible capacity group and an explicit useful reaction or waste destination.
- An energy-storage profile with a maximum below the foundation, a non-positive commissioning slope, a target that does not derive its declared maximum exactly, an unknown/non-structural assignment role, a descendant maximum or target below its ancestor, a non-positive acquired-tier upkeep, an activation cap above the gated maximum, or reproduction fractions outside `0..1`.
- A living-organism state whose charged plus spent carrier matter exceeds currently commissioned capacity, except inside an uncommitted atomic transaction that must restore the invariant before commit.
- Reproductively viable DNA whose inherited micronutrient quota set cannot fit in its compiled free-micronutrient capacity, unless a declared non-v1 reproductive mode supplies an alternative zero-sum provisioning rule.
- Negative capacities, costs, rates, or probabilities outside their domains.
- World setups with no eligible founding tile.
- Survival setups without a valid hydrogen/sulfide paired starting region.
- Founding metabolism or competitor mappings not permitted by the selected scenario.
- Gas exchange configurations whose maximum degree-weighted outbound rate is unstable, whose sink lacks a valid ledger destination, or whose source-field initialization cannot converge within its configured residual and iteration limits.
- World dimensions below scenario minima, even heights that cannot represent the equator row, non-periodic `x` generation fields, invalid elevation/aquatic targets, non-monotone depth-light curves, or climate bounds that overflow their fixed-point domains.
- Generated worlds that exhaust bounded attempts without the configured number of biologically validated starting pairs; repair deltas above budget must trigger retry rather than silent acceptance.
- Values that exceed numeric or collection limits.
- Tolerance-cost curves that are non-monotonic, non-convex, overflow-prone, or produce a negative derived upkeep contribution.
- An effective-population table shorter than the configured organism ceiling, non-monotone table entries, a nonzero population-zero entry, invalid mutation modifier bounds, or a required genome outside the bounds.
- A selectable non-foundation trait with zero/negative mutation price, an invalid change-complexity ceiling, an autonomous candidate with no positive weight, or pressure decay/commit probabilities outside their domains.
- An environmental-spread profile with a non-positive or out-of-bounds passive RMS multiplier, an active-speed multiplier outside `0..1`, an unaccounted upkeep, both mutually exclusive constitutive profiles, or any v1 effect that introduces directional bias into the antipodal passive-movement table.
- A moisture-conservation policy with non-positive trend/dwell/reserve horizons, entry warning below the compiled active hard threshold, exit recovery below entry warning or above the active preferred threshold, a missing condition-history window, or any future/calendar/visibility-dependent input.
- A material-opportunity profile with missing resources, zero/negative yield or horizon, a history window outside retained authoritative flow data, weights outside `0..1`, weights that do not sum to `1.0`, or a proposal that claims useful material opportunity without enabling a complete consuming process.
- A weighted resource claim with a non-positive/out-of-bounds contention weight, a weight applied across priority classes, unmerged same-organism claims that multiply influence, a grant above usable requested matter, or non-conservative cap redistribution.
- A respiration definition that credits matter-bearing reserve without either matched assimilated fuel carbon or matched spent-carrier matter, consumes a partial fuel/O2 bundle, exceeds the shared ceiling across fuels, lacks an O2 tolerance/quota dependency, or permits generic organic resources to satisfy a named fuel requirement.
- A complex-cell definition whose structure assignments do not sum exactly to conserved viable structure, lets organization matter affect radius or prey-size fit, grants mature scale/organization capacity before construction completes, defines non-positive processing loads, applies a respiratory-ceiling modifier separately to each fuel, applies a scale-area cost more than once, or provisions an inherited quota without matching matter.
- An oxygenic definition that fails `CO2 + H2O -> CH2O + O2` balance, produces O2 without admitting its fixed-carbon output, double-counts light across photo reactions, omits Mn/Ca/tolerance activation, or applies depth/cloud/turbidity more than once.
- A predation definition with cross-tile or same-species targets, player-ownership immunity, invalid kin-distance bounds, a selectable v1 `PiercingExtraction` without a complete mechanism, a probability floor applied to hard-ineligible prey, food transfer without matching acquisition/storage, non-positive attack/defense denominators, feeding weights outside declared bounds, hidden-state fields in an organism observation, or a biological-opportunity profile without a useful processing route.

# Tools and artifacts to plan

- Human-authored configuration format and schema.
- Loader and compiler pseudocode.
- Validation-report format with source locations.
- Rule-pack hash/version algorithm.
- Balance-diff tool between two rule sets.
- Minimal rule pack for deterministic tests.
- Representative v1 rule pack for the performance benchmark.

# Open decisions

- [ ] JSON, YAML, TOML, or another authoring format.
- [ ] Whether schemas are hand-validated or generated.
- [ ] How trait effects compile into hot-loop data.
- [ ] General curve and probability-distribution representation; health uses the first fixed-point curve shapes in [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md).
- [x] Game-native resource scale, structural-biomass composition, and reserve-energy density; see [RESOURCE_CALIBRATION.md](RESOURCE_CALIBRATION.md).
- [x] Founder available-store capacity groups, composition-derived load, initial values, desired inventories, and founder waste routing; see [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- [x] Primitive micronutrient uptake at `0.5` expected quantum per organism-hour with normalized-deficit targeting and ordinary contention; advanced trait modifiers remain open.
- [ ] Default final date and speed presets.
- [x] First mutation-income formula, deterministic arithmetic, economy scales, and provisional fastest opening cadence; see [EVOLUTION.md](EVOLUTION.md) and [SULFUR_TILE_STARTING_CONFIGURATION.md](SULFUR_TILE_STARTING_CONFIGURATION.md). Final population and post-speciation validation remain open.
- [x] Two exact founding-metabolism identities, path asymmetry, and provisional opening targets; see [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md). Exact numeric yields and prices remain tuning work.

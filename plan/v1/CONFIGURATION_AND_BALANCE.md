# Configuration and Balance

Status: scaffold

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
- Fourteen single-form micronutrients and capability-specific structural/catalytic quotas.
- Passive micronutrient-uptake opportunity probability, per-opportunity quantum cap, target-selection policy, marginal energy cost, and trait modifiers.
- The fixed initial atmospheric catalogue: H₂, CO₂, CH₄, H₂S, SO₂, O₂, N₂, and NH₃.
- Gas emission profiles, full-activity source rates, environmental-sink rates and destinations, exchange rates, and symmetric edge-compatibility multipliers from [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- Global atmospheric-background targets and seeded ranges, diffuse background-maintenance sources, gas-accessibility classes, and the aquatic depth curve from [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- Boundary sources, sinks, and energy opportunities.
- Atomic metabolic, energy-use, biomass, and decomposition reactions.
- Metabolic consumed-input and tap requirements, binding durations/triggers, process classes, priority bounds, and holdback limits from [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md).
- Trait families, levels, prerequisites, incompatibilities, costs, and effects.
- Stable canonical family IDs and typed effect domains for resource acquisition, external energy capture, internal metabolism, energy storage, cellular organization, behavior, defense, and the other families fixed in [EVOLUTION.md](EVOLUTION.md).
- Trait-family forests, global prerequisite predicates, incompatibilities, exact ancestor-effect supersession, mutation costs, change complexity, typed effects, activation requirements, pressure tags, and deterministic effect-composition metadata from [TRAIT_SYSTEM.md](TRAIT_SYSTEM.md).
- The initial founding loadouts, family-tree topology, cross-family milestones, evolutionary niches, and benefit/liability requirements from [TRAIT_CATALOGUE.md](TRAIT_CATALOGUE.md).
- Reproductive modes and lifecycle parameters.
- Environmental tolerances and stress curves.
- Health-factor targets, structural targets, constitutive nutrient quotas, senescence curves, and lifecycle condition modifiers from [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md).
- Convex hot/cold tolerance-cost curves and their cross-axis breadth-coupling coefficient from [TRAIT_CATALOGUE.md](TRAIT_CATALOGUE.md).
- Gas-specific soft/hard chemical-exposure thresholds and DNA tolerance multipliers.
- Behavior and sensing capabilities.
- Predation base odds, attack/defense attributes, eligibility bounds, attempt costs, ingestion caps, contested-feeding weights, and minimum/maximum kill probabilities.
- The exact hydrogen-acetogenesis and sulfide-anoxygenic-phototrophy founding definitions, trait paths, opening targets, and paired setup constraints from [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md).
- Scenario-defined allowed founding-metabolism IDs, tile-eligibility rules, setup-card metadata, and deterministic competitor-pairing policy. The v1 scenario contains exactly two; the engine must not encode that count.
- Mutation costs and autonomous-evolution weights.
- Mutation-income effective-population curve, normalization, DNA modifiers, and fractional accumulation scale.
- World-generation distributions and climate parameters.

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
- Resource definitions with zero/negative compositions or invalid reservoir placement.
- Available-store resources that resolve to zero or multiple capacity groups, have invalid/overflowing composition-derived load, or place current group load above capacity outside an atomic overflow transaction.
- Retained process products without both a compatible capacity group and an explicit useful reaction or waste destination.
- Reproductively viable DNA whose inherited micronutrient quota set cannot fit in its compiled free-micronutrient capacity, unless a declared non-v1 reproductive mode supplies an alternative zero-sum provisioning rule.
- Negative capacities, costs, rates, or probabilities outside their domains.
- World setups with no eligible founding tile.
- Survival setups without a valid hydrogen/sulfide paired starting region.
- Founding metabolism or competitor mappings not permitted by the selected scenario.
- Gas exchange configurations whose maximum degree-weighted outbound rate is unstable, whose sink lacks a valid ledger destination, or whose source-field initialization cannot converge within its configured residual and iteration limits.
- Values that exceed numeric or collection limits.
- Tolerance-cost curves that are non-monotonic, non-convex, overflow-prone, or produce a negative derived upkeep contribution.

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
- [x] First mutation-income calibration candidate and provisional fastest opening cadence; see [SULFUR_TILE_STARTING_CONFIGURATION.md](SULFUR_TILE_STARTING_CONFIGURATION.md). Final health and post-speciation validation remain open.
- [x] Two exact founding-metabolism identities, path asymmetry, and provisional opening targets; see [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md). Exact numeric yields and prices remain tuning work.

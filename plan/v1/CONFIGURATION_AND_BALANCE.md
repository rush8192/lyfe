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
- Fourteen single-form micronutrients and capability-specific structural/catalytic quotas.
- The fixed initial atmospheric catalogue: H₂, CO₂, CH₄, H₂S, SO₂, O₂, N₂, and NH₃.
- Gas emission profiles, full-activity source rates, environmental-sink rates and destinations, exchange rates, and symmetric edge-compatibility multipliers from [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- Global atmospheric-background targets and seeded ranges, diffuse background-maintenance sources, gas-accessibility classes, and the aquatic depth curve from [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md).
- Boundary sources, sinks, and energy opportunities.
- Atomic metabolic, energy-use, biomass, and decomposition reactions.
- Trait families, levels, prerequisites, incompatibilities, costs, and effects.
- Stable canonical family IDs and typed effect domains for resource acquisition, external energy capture, internal metabolism, energy storage, cellular organization, behavior, defense, and the other families fixed in [EVOLUTION.md](EVOLUTION.md).
- Trait-family forests, global prerequisite predicates, incompatibilities, mutation costs, change complexity, typed effects, activation requirements, pressure tags, and deterministic effect-composition metadata from [TRAIT_SYSTEM.md](TRAIT_SYSTEM.md).
- Reproductive modes and lifecycle parameters.
- Environmental tolerances and stress curves.
- Gas-specific soft/hard chemical-exposure thresholds and DNA tolerance multipliers.
- Behavior and sensing capabilities.
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
- Negative capacities, costs, rates, or probabilities outside their domains.
- World setups with no eligible founding tile.
- Survival setups without a valid hydrogen/sulfide paired starting region.
- Founding metabolism or competitor mappings not permitted by the selected scenario.
- Gas exchange configurations whose maximum degree-weighted outbound rate is unstable, whose sink lacks a valid ledger destination, or whose source-field initialization cannot converge within its configured residual and iteration limits.
- Values that exceed numeric or collection limits.

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
- [ ] How curves and probability distributions are represented.
- [x] Game-native resource scale, structural-biomass composition, and reserve-energy density; see [RESOURCE_CALIBRATION.md](RESOURCE_CALIBRATION.md).
- [ ] Default final date and speed presets.
- [x] First mutation-income calibration candidate and provisional fastest opening cadence; see [SULFUR_TILE_STARTING_CONFIGURATION.md](SULFUR_TILE_STARTING_CONFIGURATION.md). Final health and post-speciation validation remain open.
- [x] Two exact founding-metabolism identities, path asymmetry, and provisional opening targets; see [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md). Exact numeric yields and prices remain tuning work.

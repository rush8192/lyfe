# LYFE V1 Implementation Plan

This directory translates the vision into concrete, implementation-oriented decisions for v1. Planning documents should describe system boundaries, data ownership, algorithms, invariants, failure behavior, and pseudocode at enough depth that implementation does not have to rediscover major design choices.

The plans remain downstream from the vision. If a plan intentionally contradicts a vision requirement, the discrepancy must be called out in both places rather than silently changing the product.

# Planning map

| Document | Primary question | Important dependencies |
| --- | --- | --- |
| [TECHNOLOGY.md](TECHNOLOGY.md) | Which languages, runtimes, transports, and architectural constraints do we use? | Vision |
| [ARCHITECTURE.md](ARCHITECTURE.md) | How are processes, projects, dependencies, and runtime responsibilities divided? | Technology |
| [DATA_MODEL.md](DATA_MODEL.md) | How is authoritative state represented and identified? | Architecture |
| [CONFIGURATION_AND_BALANCE.md](CONFIGURATION_AND_BALANCE.md) | Which rules are data-driven, versioned, and tunable? | Data model |
| [WORLD_AND_CLIMATE.md](WORLD_AND_CLIMATE.md) | How is the wrapping world generated and how do conditions evolve? | Data model, configuration |
| [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md) | Which first numerical values make generated worlds varied, playable, and faithful to the founding fixtures? | World and climate, gas transport, founding fixtures, health calibration |
| [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md) | How do volcanic gases plateau, leak into neighbors, transform, and remain locally useful without sustaining a neighboring founder? | World, resource model, founding fixtures |
| [RESOURCE_MODEL.md](RESOURCE_MODEL.md) | How do matter and energy move through reservoirs without violating conservation? First deep-dive draft complete. | Data model, world |
| [RESOURCE_CALIBRATION.md](RESOURCE_CALIBRATION.md) | Which numeric scales keep organism, tile, world, and lifetime accounting precise and safe? | Resource model, data model |
| [ONE_TILE_STARTING_CONFIGURATION.md](ONE_TILE_STARTING_CONFIGURATION.md) | Can one harsh volcanic tile support primitive life while creating pressure to adapt or leave? | Resource calibration, organisms, world |
| [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md) | How do the hydrogen and sulfide openings create a fair specialist-versus-platform choice and seed Survival? | Resource model, starting fixture, evolution, gameplay |
| [ALTERNATIVE_FOUNDING_METABOLISMS.md](ALTERNATIVE_FOUNDING_METABOLISMS.md) | Is there a compelling third founder, which candidates should be deferred, and how do we preserve extension points? | Founding metabolisms, resource model, configuration |
| [SULFUR_TILE_STARTING_CONFIGURATION.md](SULFUR_TILE_STARTING_CONFIGURATION.md) | Does the fast sulfide specialist reach its opening target while remaining light-, substrate-, and micronutrient-constrained? | Founding metabolisms, resource calibration, hydrogen fixture |
| [SIMULATION_LOOP.md](SIMULATION_LOOP.md) | In what deterministic order does a tick resolve? | World, resources, organisms |
| [ORGANISMS.md](ORGANISMS.md) | How do individual organisms evaluate behavior, actions, lifecycle, and interactions? | Resources, simulation loop |
| [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md) | How do organisms and remains occupy local space, find targets, move, migrate, and expose observations to behavior? | Organisms, simulation loop, world, data model |
| [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md) | Which body, Brownian, sensing, interaction, placement, and active-distance scales create occasional local encounters without tile-wide reach? | Spatial organisms, health calibration, traits, simulation loop |
| [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md) | Which organism fields are authoritative, and how is explainable health derived and cached? | Organisms, resources, data model |
| [ORGANISM_HEALTH_CALIBRATION.md](ORGANISM_HEALTH_CALIBRATION.md) | What first numeric values govern health, structure, nutrient sufficiency, senescence, environmental condition, and presentation? | Organism state and health, founder fixtures, configuration |
| [LIFECYCLE_AND_RECYCLING.md](LIFECYCLE_AND_RECYCLING.md) | How do growth, reproduction, aging, death, remains, digestion, and nutrient return form one deterministic mass-balanced lifecycle? | Organisms, health, resources, storage, simulation loop, spatial rules |
| [ENERGY_STORAGE.md](ENERGY_STORAGE.md) | How do constructed reserve compartments, capacity tiers, health dilution, upkeep, reproduction, and dormancy create useful but costly buffering strategies? | Internal storage, health, lifecycle, complex cells, traits, terrestrial adaptation |
| [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md) | How does decayed biomass create a constrained dissolved-organic niche, and how do uptake and fermentation recover energy without double-counting it? | Resources, lifecycle, storage, simulation loop, traits, evolution |
| [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md) | Do mortality, decay, fermentation, and speciation fractions produce a viable but subordinate heterotroph niche, and how should autonomous evolution score it? | Organic uptake, lifecycle, health, evolution, world calibration |
| [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md) | How do organic fuel, oxygen, carrier recycling, tolerance, catalysts, regulation, and coupled contention create the first aerobic consumer niche? | Organic economy, gas transport, storage/allocation, traits, evolution |
| [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md) | How do light, water oxidation, carbon fixation, catalytic quotas, upkeep, and oxygen release create the first widespread primary-producer niche and oxygenation transition? | Respiration, world light, gas transport, founding photosystems, traits, evolution |
| [PREDATION.md](PREDATION.md) | How do opportunistic capture, directed hunting, defense, engulfment, and mass-balanced feeding create a density-dependent biological-resource niche? | Spatial behavior, simulation loop, lifecycle/digestion, health, traits, evolution |
| [COMPLEX_CELLS.md](COMPLEX_CELLS.md) | How do compartmentalization, eukaryote-like organization, energy machinery, and cell scale create a costly late-game complexity milestone? | Respiration, storage/allocation, health, lifecycle, spatial rules, predation, traits, evolution |
| [COMPLEX_CELL_CALIBRATION.md](COMPLEX_CELL_CALIBRATION.md) | Which processing loads, work capacities, structural assignments, and ecological demand landmarks make complex cells useful without invalidating simpler niches? | Complex cells, founder fixtures, respiration, lifecycle, scale, predation |
| [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md) | Which adaptations gate microbial landfall, what makes land worth colonizing, how does succession emerge, and how do drying and terrestrial movement shape survival? | World/climate, health, spatial rules, lifecycle, traits, resources, photosynthesis |
| [INTERNAL_STORAGE_AND_ALLOCATION.md](INTERNAL_STORAGE_AND_ALLOCATION.md) | How are stored resources acquired, capacity-limited, consumed, temporarily bound by active processes, or protected through evolved priority and holdback policies? | Organisms, resource model, simulation loop, traits |
| [TRAIT_SYSTEM.md](TRAIT_SYSTEM.md) | How are linear and branching trait families authored, validated, compiled, activated, priced, and explained? | Evolution, organisms, resources, configuration |
| [TRAIT_CATALOGUE.md](TRAIT_CATALOGUE.md) | What are the initial v1 trait trees, cross-family milestones, niches, and complexity tradeoffs? | Trait system, founding metabolisms, organisms, balance |
| [EVOLUTION.md](EVOLUTION.md) | How do DNA, mutation points, speciation, autonomy, and lineage work? | Organisms, configuration |
| [GAMEPLAY.md](GAMEPLAY.md) | How do setup, sandbox, survival, pacing, and endings become playable flows? | Evolution, simulation loop |
| [SERVER_AND_PROTOCOL.md](SERVER_AND_PROTOCOL.md) | How do clients command and observe the authoritative server? | Architecture, data model, gameplay |
| [CLIENT.md](CLIENT.md) | How does the first browser client render and explain the simulation? | Protocol, gameplay |
| [PERSISTENCE_AND_REPLAY.md](PERSISTENCE_AND_REPLAY.md) | How are worlds saved, resumed, versioned, and replayed deterministically? | Data model, simulation loop |
| [VALIDATION_AND_PERFORMANCE.md](VALIDATION_AND_PERFORMANCE.md) | How do we prove correctness, determinism, conservation, and useful speed? | All plans |

# Recommended planning sequence

The documents have dependencies but should be developed iteratively rather than treated as a strict waterfall.

1. Finalize architecture, identifiers, numeric representations, and configuration ownership.
2. Deeply design the world, climate, and resource ledger together.
3. Define the tick pipeline and organism action/intention model.
4. Define DNA, mutation, autonomous evolution, and gameplay state machines.
5. Map authoritative state into save, replay, protocol, and client-facing representations.
6. Set acceptance thresholds and build the representative simulation benchmark.

The resource model is the highest-risk domain. Its first detailed pass should precede final organism and metabolism algorithms because those systems depend on its reservoirs, units, and transaction rules.

# Definition of a complete planning document

A planning document is ready for implementation when it includes, as applicable:

- Decisions inherited from the vision and technology plan.
- Explicit v1 scope and future exclusions.
- Canonical terms and data ownership.
- Proposed data structures and units.
- Ordered algorithms or pseudocode.
- Determinism and mass-balance implications.
- Concurrency and performance implications.
- Errors, invalid states, and failure behavior.
- Save, replay, protocol, and observability needs.
- Testable invariants and acceptance criteria.
- Remaining questions that do not block implementation.

# Cross-cutting conventions

- Authoritative identifiers are stable and never inferred from array positions sent to a client.
- Simulation state changes only at defined tick phases.
- Commands are admitted at explicit ticks and recorded for replay.
- Randomness is simulation state and must not depend on thread scheduling or presentation.
- Matter transfers use an auditable resource ledger; energy dissipation is recorded separately from matter.
- Derived values should not be persisted or transmitted unless recomputation is expensive or version-sensitive.
- Plans should distinguish normative rules from examples and provisional balance values.
- Future multiplayer must influence boundaries, not expand v1 gameplay scope.

# Major decisions still to make

- Representative-map and biological smoke-test validation of the provisional world/climate rule pack. First climate coefficients, generated-start eligibility and repair thresholds, sulfur daily-light calibration, lifecycle-owned decay rates, and dissolved-organic mobility are now specified; other non-gas resources remain tile-bound pending an explicit need.
- Baseline behavior selection and coupled clustered spatial-performance validation. First body, Brownian, environmental-spread, sensing, interaction, placement, active-distance, acceleration, turning, movement-energy, migration probability/failure, and terrestrial surface-gliding values are specified in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md) and [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md); the terrestrial movement and land-admission rules are in [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md).
- Executable multi-seed validation of the complete first lifecycle rule pack: the organic mortality/decay niche and autonomous material-opportunity score have authoring landmarks, and terrestrial dormancy now has an executable individual-based dry-season cohort in [POPULATION_ECOSYSTEM_VALIDATION.md](POPULATION_ECOSYSTEM_VALIDATION.md). Scavenging, digestion, generated climate, and later ecological strategies still need coupled population fixtures.
- Population validation and catalogue-wide pricing of the first evolution economy. Income arithmetic, price bands and anchors, founder rounding, branch cooldown, autonomous pressure/material-opportunity/goal rules, and lineage schemas are fixed in [EVOLUTION.md](EVOLUTION.md); many biological trait effects remain tied to their subsystem passes.
- Execute the first complete complex-organization, dissolved-organic-specialist, predator, and terrestrial ecosystem fixtures. The storage ladder now has commissioned `10k/25k/50k/150k` capacities, an observable non-oracular dormancy policy, stable-habitat/seasonal landmarks, and an initial executable cohort result. Generated-world climate, realistic starting gas stocks, resource-aware growth/reproduction regulation, migration, and coupled multi-metabolism validation remain.
- Canonical identifier allocation, dense state layout, configuration encoding/compiler, random-key derivation, deterministic parallel reduction, and one-world/server lifecycle ownership.
- Final date, speed presets, complete command/state-machine catalogues, save/checkpoint format, protocol code generation, and client projection schemas.
- A representative benchmark rule pack, reference hardware, fastest-speed threshold, state-hash definition, and performance-regression policy.

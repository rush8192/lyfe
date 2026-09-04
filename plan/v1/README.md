# LYFE V1 Implementation Plan

This directory translates the vision into concrete, implementation-oriented decisions for v1. Planning documents should describe system boundaries, data ownership, algorithms, invariants, failure behavior, and pseudocode at enough depth that implementation does not have to rediscover major design choices.

The plans remain downstream from the vision. If a plan intentionally contradicts a vision requirement, the discrepancy must be called out in both places rather than silently changing the product.

# Planning map

| Document | Primary question | Important dependencies |
| --- | --- | --- |
| [TECHNOLOGY.md](TECHNOLOGY.md) | Which languages, runtimes, transports, and architectural constraints do we use? | Vision |
| [ARCHITECTURE.md](ARCHITECTURE.md) | How are processes, projects, dependencies, and runtime responsibilities divided? | Technology |
| [DEPLOYMENT_AND_PORTABILITY.md](DEPLOYMENT_AND_PORTABILITY.md) | How is the server packaged and run portably without coupling simulation code to deployment infrastructure? | Technology, architecture, world ownership, persistence |
| [ENGINEERING_READINESS_REVIEW.md](ENGINEERING_READINESS_REVIEW.md) | Which foundation decisions are sound, which premature mechanisms were simplified, and what exact walking skeleton should implementation build first? | Technology, architecture, ownership, storage, persistence, synchronization, validation |
| [WORLD_EXECUTION_AND_OWNERSHIP.md](WORLD_EXECUTION_AND_OWNERSHIP.md) | Who owns a world, how does it advance, and where do commands, parallelism, saves, faults, and shutdown cross its boundary? | Architecture, technology, simulation loop |
| [KEYED_RANDOMNESS.md](KEYED_RANDOMNESS.md) | How does every random outcome receive a stable semantic address independent of call order, workers, storage, and observation? | Technology, identity, simulation loop, persistence |
| [DETERMINISTIC_PARALLEL_EXECUTION.md](DETERMINISTIC_PARALLEL_EXECUTION.md) | How do workers evaluate sealed state and produce one canonical reduction, commit, ID sequence, materialization, and change journal? | World ownership, keyed randomness, storage, simulation loop |
| [DATA_MODEL.md](DATA_MODEL.md) | How is authoritative state represented and identified? | Architecture |
| [MATERIALIZED_DERIVED_STATE.md](MATERIALIZED_DERIVED_STATE.md) | How does each derived gameplay number get one owning formula, stored result, dependency contract, and shared use across simulation, save, and client explanation? | Data model, storage, resources, traits, climate |
| [ENTITY_IDENTITY_AND_STORAGE.md](ENTITY_IDENTITY_AND_STORAGE.md) | How do stable IDs, owner-mutable tile-partitioned dense stores, locators, detached snapshots, and derived indexes represent the authoritative world? | World ownership, data model, synchronization |
| [CONFIGURATION_AND_BALANCE.md](CONFIGURATION_AND_BALANCE.md) | Which rules are data-driven, versioned, and tunable? | Data model |
| [RULE_PACK_AUTHORING_AND_COMPILATION.md](RULE_PACK_AUTHORING_AND_COMPILATION.md) | How do strict authored rules, permanent identities, validation, hashing, scenarios, and DNA compilation become immutable runtime artifacts? | Configuration, traits, resources, storage, randomness |
| [MODDABILITY.md](MODDABILITY.md) | Which rule values may mods replace, how do selectable world-generation packs work, and how are both validated, identified, saved, and exposed without creating another runtime truth? | Rule-pack compilation, world/climate, persistence, protocol, validation |
| [WORLD_AND_CLIMATE.md](WORLD_AND_CLIMATE.md) | How is the wrapping world generated and how do conditions evolve? | Data model, configuration |
| [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md) | Which first numerical values make generated worlds varied, playable, and faithful to the founding fixtures? | World and climate, gas transport, founding fixtures, health calibration |
| [GAS_TRANSPORT_AND_ATTRITION.md](GAS_TRANSPORT_AND_ATTRITION.md) | How do volcanic gases plateau, leak into neighbors, transform, and remain locally useful without sustaining a neighboring founder? | World, resource model, founding fixtures |
| [RESOURCE_MODEL.md](RESOURCE_MODEL.md) | How do matter and energy move through reservoirs without violating conservation? First deep-dive draft complete. | Data model, world |
| [RESOURCE_STORAGE_AND_EVALUATION.md](RESOURCE_STORAGE_AND_EVALUATION.md) | How are hot organism balances, internal allocations, tile stocks, tile parameters, and compiled phenotype values laid out and evaluated efficiently? | Resource model, entity storage, traits, simulation loop |
| [RESOURCE_CALIBRATION.md](RESOURCE_CALIBRATION.md) | Which numeric scales keep organism, tile, world, and lifetime accounting precise and safe? | Resource model, data model |
| [ONE_TILE_STARTING_CONFIGURATION.md](ONE_TILE_STARTING_CONFIGURATION.md) | Can one harsh volcanic tile support primitive life while creating pressure to adapt or leave? | Resource calibration, organisms, world |
| [FOUNDING_METABOLISMS.md](FOUNDING_METABOLISMS.md) | How do the hydrogen and sulfide openings create a fair specialist-versus-platform choice and seed Survival? | Resource model, starting fixture, evolution, gameplay |
| [SURVIVAL_OPENING_VALIDATION.md](SURVIVAL_OPENING_VALIDATION.md) | Do the paired founders, health, lifecycle, resources, mutation income, and first player speciation form one viable opening loop? | Founding metabolisms, gas transport, lifecycle, health, evolution, gameplay |
| [OPENING_STRATEGY_VALIDATION.md](OPENING_STRATEGY_VALIDATION.md) | Do mirrored player starts and their first affordable trait branches create legible, non-dominated strategies, and what does the engine need to measure them? | Survival opening, player loop, spatial rules, storage, organic metabolism |
| [ALTERNATIVE_FOUNDING_METABOLISMS.md](ALTERNATIVE_FOUNDING_METABOLISMS.md) | Is there a compelling third founder, which candidates should be deferred, and how do we preserve extension points? | Founding metabolisms, resource model, configuration |
| [SULFUR_TILE_STARTING_CONFIGURATION.md](SULFUR_TILE_STARTING_CONFIGURATION.md) | Does the fast sulfide specialist reach its opening target while remaining light-, substrate-, and micronutrient-constrained? | Founding metabolisms, resource calibration, hydrogen fixture |
| [SIMULATION_LOOP.md](SIMULATION_LOOP.md) | In what deterministic order does a tick resolve? | World, resources, organisms |
| [ORGANISMS.md](ORGANISMS.md) | How do individual organisms evaluate behavior, actions, lifecycle, and interactions? | Resources, simulation loop |
| [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md) | How do organisms and remains occupy local space, find targets, move, migrate, and expose observations to behavior? | Organisms, simulation loop, world, data model |
| [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md) | Which body, Brownian, sensing, interaction, placement, and active-distance scales create occasional local encounters without tile-wide reach? | Spatial organisms, health calibration, traits, simulation loop |
| [BEHAVIOR_AND_RESOURCE_PRESSURE.md](BEHAVIOR_AND_RESOURCE_PRESSURE.md) | How do local observations and recent resource experience select persistent behavior, conservation, dispersal, and reproduction readiness? | Organisms, simulation loop, spatial rules, health, lifecycle, traits |
| [ORGANISM_STATE_AND_HEALTH.md](ORGANISM_STATE_AND_HEALTH.md) | Which organism fields are primary, and how is explainable health derived once and stored as a gameplay materialization? | Organisms, resources, data model |
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
| [PLAYER_LOOP_AND_NARRATIVE.md](PLAYER_LOOP_AND_NARRATIVE.md) | How do observation, diagnosis, evolution choices, attention, consequence review, and the factual chronicle form an engaging loop? | Gameplay, evolution, client, protocol |
| [STATE_CHANGE_AND_CLIENT_SYNC.md](STATE_CHANGE_AND_CLIENT_SYNC.md) | How do authoritative phase writes become deterministic change journals and actor-authorized, mergeable client batches? | Architecture, data model, simulation loop, protocol, client |
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
6. Build the end-to-end walking skeleton, then grow staged `10,000` and `50,000..100,000` organism benchmarks before specializing storage or execution.

The resource model is the highest-risk domain. Its first detailed pass should precede final organism and metabolism algorithms because those systems depend on its reservoirs, units, and transaction rules.

Implementation status and sequencing are tracked only in the living [v1 implementation backlog](../../implementation/v1/BACKLOG.md). Planning checklists describe specification completeness; they must not be interpreted as evidence that the corresponding code exists.

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
- Gameplay-relevant derived values have one owning compiler/builder, are materialized once when dependencies change, and are read by every consumer rather than independently recalculated.
- Start with the simplest well-bounded representation that preserves correctness and the target scale. Bit packing, narrower physical fields, adaptive encodings, specialized layouts, and custom compression require representative end-to-end evidence of a material simulation or synchronization bottleneck; improving one isolated metric is insufficient.
- Authoritative store mutations and change capture are one typed operation; internal change journals remain separate from actor-authorized protocol batches.
- Authoritative content crosses one cold-path boundary: strict rule sources resolve to a normalized, hashed model and compile into immutable runtime artifacts. Ticks never read authoring records, stable-key maps, or unresolved references.
- Moddability enters through that same cold-path boundary: explicitly opted-in balance overlays flatten before ordinary validation, while exactly one complete closed-schema world profile joins during world-rule compilation. The compiled result is the sole runtime truth; load order, scripts, inherited hidden defaults, and live rule mutation are not v1 mechanics.
- Client state patches use absolute values and apply atomically against an exact projection-stream revision, allowing consecutive batches to coalesce safely.
- Commands are admitted at explicit completed boundaries, assigned an application tick/world revision, and recorded in authoritative replay order.
- Randomness is an explicitly versioned counter-based function of the root seed and stable semantic address; it has no mutable global/per-worker stream and cannot depend on thread scheduling or presentation.
- The scalar executor is the correctness oracle. Workers evaluate sealed views into isolated buffers; one canonical reducer/preflight/commit path owns conflicts, IDs, materializations, and change journals for every worker count.
- Matter transfers use an auditable resource ledger; energy dissipation is recorded separately from matter.
- Plans should distinguish normative rules from examples and provisional balance values.
- Future multiplayer must influence boundaries, not expand v1 gameplay scope.

# Major decisions still to make

- The first player-loop contract is defined in [PLAYER_LOOP_AND_NARRATIVE.md](PLAYER_LOOP_AND_NARRATIVE.md). [OPENING_STRATEGY_VALIDATION.md](OPENING_STRATEGY_VALIDATION.md) now supplies a four-seed mirrored authoring matrix: it finds distinct resident, buffering, dispersal, preparation, and organic-consumer signals without adding another opening subsystem. The result is not final balance; 64-key/generated-map validation, bounded founder packages, interaction wireframes, alert/chronicle thresholds, and human-playtest baselines remain open.
- Two remaining player-facing choices should be fixed before the first representative rule pack is frozen: the default simulation deadline plus final-tick loss/completion precedence, and the exact bounded efficiency-versus-tolerance packages offered beside each founding metabolism. The opening matrix must then validate both possible player-controlled metabolisms and ensure each has a legible first mutation menu rather than only a forced preparatory purchase. These do not block architecture or storage-layout work, but the scheduler, range proof, setup schema, and final gameplay benchmark must consume explicit values rather than infer them.
- Extend the implemented deterministic `32 × 17` generated-world slice into representative-map and biological smoke-test validation. `WORLD-200` now proves fixed-point terrain/static climate generation, x-only wrapping, configured aquatic/volcanic bands, four bounded repaired start pairs, and replay across its seed matrix. Recurring weather/moisture, lithology, gas equilibrium, founder biology, and other non-gas resource placement remain in their owning later slices.
- Numerical behavior calibration and coupled clustered spatial-performance validation. The first local pressure channels, conservation thresholds, reproduction-readiness rule, persistent behavior set, and cross-behavior selector are specified in [BEHAVIOR_AND_RESOURCE_PRESSURE.md](BEHAVIOR_AND_RESOURCE_PRESSURE.md). Body, movement, sensing, interaction, and migration values remain in [SPATIAL_CALIBRATION.md](SPATIAL_CALIBRATION.md) and [SPATIAL_ORGANISMS_AND_BEHAVIOR.md](SPATIAL_ORGANISMS_AND_BEHAVIOR.md); terrestrial movement and land admission remain in [TERRESTRIAL_ADAPTATION.md](TERRESTRIAL_ADAPTATION.md).
- Broader executable validation of the complete first lifecycle rule pack: the paired Survival opening now couples founder growth, deterministic fission, health, senescence, mutation income, speciation, and first remnant decay in [SURVIVAL_OPENING_VALIDATION.md](SURVIVAL_OPENING_VALIDATION.md); terrestrial dormancy and the organic mortality/decay niche have their own executable or authoring fixtures. Scavenging, digestion, a real generated seed, and later ecological strategies still need coupled population fixtures.
- Population validation and catalogue-wide pricing of the first evolution economy. Income arithmetic, price bands and anchors, founder rounding, branch cooldown, autonomous pressure/material-opportunity/goal rules, and lineage schemas are fixed in [EVOLUTION.md](EVOLUTION.md); many biological trait effects remain tied to their subsystem passes.
- Execute the first complete complex-organization, dissolved-organic-specialist, predator, and terrestrial ecosystem fixtures. The storage ladder now has commissioned `10k/25k/50k/150k` capacities, an observable non-oracular dormancy policy, stable-habitat/seasonal landmarks, and an initial executable cohort result. The founding-metabolism pair now has a coupled opening fixture; generated-world climate, resource-aware behavior, migration, and coupled advanced-metabolism validation remain.
- Encode a minimal representative rule/world bundle and assign permanent IDs only to definitions and semantic domains that cross durable compatibility boundaries. Strict JSON authoring, generated schemas, typed content registries and tombstones, normalization, hash identities, fail-closed validation, global compilation, and deterministic phenotype compilation are fixed in [RULE_PACK_AUTHORING_AND_COMPILATION.md](RULE_PACK_AUTHORING_AND_COMPILATION.md). The official biological pack is the ordinary zero-mod base and the primordial-Earth-like setup is an ordinary complete world pack; [MODDABILITY.md](MODDABILITY.md) defines local balance overlays, selectable external world profiles, conflict rejection, final identities, certification, and the boundary for future general content mods. The `Philox4x64-10` semantic-address contract, scalar oracle, worker isolation, exact reduction, canonical creation/ID assignment, owner-only commit, tile-partitioned dense storage, standard live-ID locators, detached snapshots, hot resource matrices, phase-specific tile views, derived indexes, and mutation/change capture are fixed at a first-pass level; concrete types and physical tuning remain prototype work.
- Final date, speed presets, complete command/state-machine catalogues, autosave/replay retention policy, and hosted snapshot/delta transport limits. The Stage-A logical save format, exact reload, generated C#/TypeScript projection contract, revision checks, absolute replacement, atomic apply, visibility eviction, and full-snapshot resynchronization are implemented; richer schemas grow with their authoritative Stage-B stores.
- A representative benchmark rule pack, reference hardware, fastest-speed threshold, and performance-regression policy. The logical `WorldStateHashV1` scope, ordering, algorithm, concrete current-state tags, golden vectors, and cadence are fixed in [PERSISTENCE_AND_REPLAY.md](PERSISTENCE_AND_REPLAY.md); save/load verification follows with persistence.

To bring the world to life, we want a core engine that can simulate each individual organism in each tile of the world, along with the systems of the world they interact with.

# Scope

The initial scale target is to simulate tens of thousands of individual organisms in one world at a useful gameplay speed. Millions of organisms and many thousands within one tile remain stretch goals rather than v1 acceptance requirements. We should preserve a path toward those goals without taking on distributed-simulation or other unneeded complexity before measurements justify it.

V1 ships the single-player free sandbox and survival modes. Competitive multiplayer is deferred, but v1 simulation state, commands, clock ownership, and the authoritative server boundary must be designed so multiplayer can be added without replacing the core engine.

To aid in parallelism, we can make sure that each tile is relatively self-contained, except for a few discrete cross-tile interactions (e.g. the migration of certain organisms or resources from one tile to an adjacent tile). Parallel execution must not change simulation outcomes or the order in which seeded random decisions are resolved.

# Spatial model

The world is a two-dimensional grid in which only edge-sharing tiles are neighbors. The east-west `x` dimension wraps by connecting its minimum and maximum edges; the north-south `y` dimension is bounded and does not wrap. Each tile therefore has up to four neighbors. Cross-tile simulation interactions are limited to those neighbors and filtered by the compatibility of their attributes and current conditions.

Tiles are initially well-mixed environmental compartments: their conditions and nutrient pools do not vary by position within the tile. Individual organisms and dead remains do have specific within-tile coordinates. Proximity between those coordinates governs direct interactions such as predation and scavenging, while proximity to a shared tile edge is required for migration to the neighbor across that edge.

Localized micronutrient concentration fields are a future extension. They may eventually add within-tile resource gradients without requiring all environmental parameters and macronutrients to use the same spatial model.

Organism displacement may combine separately accounted active locomotion, zero-mean Brownian motion, and future environment-driven transport such as currents or wind. Every component uses the same within-tile path and edge-crossing rules, and at most moves the existing organism rather than creating a representative copy. This lets actively mobile lineages and reproduction-driven or environmentally dispersed lineages pursue different strategies without introducing cross-tile biological interactions.

# Species and individual state

In the initial simulation, every organism references the single shared DNA of its species rather than storing an independently varying genome. Each individual captures at least:

- Its species and current tile.
- Its within-tile position and velocity.
- Its age and lifecycle phase.
- Its stored energy and stored nutrients.
- Its current behavior or behavioral goal.
- Any small capability state that must persist across ticks.

Maximum energy-storage capacity, current environmental stress, and available behaviors are derived from species DNA together with the organism's state and surroundings. Relative health is a derived score grounded primarily in stored energy relative to capacity, then adjusted by internal state and current environmental conditions. Cosmetic differences between members of a species do not affect simulation state. Heritable individual genetic variation and recombination are possible future extensions rather than initial requirements.

Behavior represents the organism's current response to its condition and surroundings rather than direct player instructions. More advanced DNA may provide additional behaviors or more effective selection among them. For example, an organism low on a required resource might seek a detectable gradient, hunt eligible prey, scavenge, conserve energy, or move toward a more favorable environment depending on its capabilities.

# Organism ticks and actions

Every living organism is evaluated on every simulation tick. Its update advances age and lifecycle state, applies recurring maintenance and metabolism, selects or updates behavior, attempts any eligible actions, updates velocity, and advances its position. Movement may keep it within the tile or cause a boundary crossing when migration requirements are satisfied.

An organism may perform one or more actions during a tick as allowed by its DNA, behavior, state, and surroundings. Initial action categories include resource absorption, metabolism, reproduction, predation, scavenging, changing movement, environmental response, and death. Some operations, such as aging, maintenance expenditure, metabolism attempts, and movement integration, occur every tick. Other actions are gated by conditions or attempted probabilistically: reproduction occurs deterministically after its health, resource, lifecycle, and scheduled-cooldown gates pass; predation requires an eligible nearby target; and environmental death requires exposure beyond the organism's limits. A successful reproduction transfers stored nutrients and chemical energy from the parent to the offspring according to the DNA's true-split or budding allocation model and applies an additional energy cost; it cannot create resources.

Almost all biological processes may have probabilistic outcomes, even when evaluated every tick. For example, a metabolism attempts to transform available external resources into stored usable organic compounds, but its chance and yield depend on the favorability of the environment, available substrates, DNA capabilities, and organism state. All such outcomes draw from the world's deterministic seeded randomness. Successful transfers and transformations remain internally mass-balanced; the exact resource and reaction model is deferred.

The implementation plan must define a stable action-resolution order so competing interactions remain deterministic. That order is not part of the current vision, but execution speed or parallel scheduling must never change it.

# State

We want to store the state of the world in a way where we can easily serialize the state, save it as a file, load it, and checkpoint it. World state includes the game mode, root seed, simulation configuration and rules version, tick duration, current tick, calendar time and final date, seeded random-state progression, each tile's fixed geography and climate baselines, its current environmental conditions, nutrient pools and atmospheric-gas reservoirs, each organism and dead remnant with its current tile, within-tile coordinates, and remaining contents, player-to-species control assignments, sandbox species locks, and each actor's discovered tiles and timestamped last observations.

Each species has its own mutation-point balance and a persistent lineage record. An abiogenesis event records the mode's independent root species and founding transactions. A speciation event records the ancestor, descendant, time, selected founding tiles, founding populations, and DNA changes. Extinct species and their lineage records remain available for the tree-of-life history. Because inter-species breeding is excluded from the initial simulation, every non-root species has exactly one parent species.

The simulation maintains or derives each species' total living population and average relative health across all tiles. Mutation-point generation is calculated from those two values and the mutation modifiers encoded in the species' DNA. Controlled and uncontrolled species use the same calculation.

The simulation also retains time-series data or derivable events for key tile-resource levels and flows. At minimum, it must be possible to explain important inflows, outflows, transformations, biological consumption, and biological release over a selected time window. This causal record supports both player-facing visualization and diagnosis of the simulation.

When an organism dies, the death event records the per-tick chance from every death cause with positive probability that was actually evaluated before death, together with the inputs and contributing stresses behind those chances. It separately identifies every draw or deterministic condition that triggered the death; if multiple triggers apply, none are discarded in favor of a single primary cause. Initial causes include senescence, insufficient energy reserves, lethal environmental exposure, maintenance failure, and predation. Environmental tolerances distinguish soft limits that increase energy expenditure from hard limits that create a per-turn death probability increasing with the severity of exposure.

# Initialization

V1 provides two founding metabolisms: sulfide anoxygenic phototrophy and hydrogen acetogenesis. Free sandbox initializes one player-chosen founding species in an eligible volcanic ocean tile by default. Survival initializes the player-selected founder in the chosen tile and an autonomous founder using the other metabolism in a deterministically reserved edge-sharing tile suited to it. Both begin with equivalent population-scale state and ordinary simulation rules, but their DNA-specific quotas, efficiencies, tolerances, and environmental dependencies differ.

The founders are independent root species grouped by a non-species abiogenesis origin event. Neither is recorded as the other's parent; every species created by later speciation has exactly one parent species.

Abiogenesis is narrative framing rather than a simulated system. The engine starts from the established founding population after the lightning introduction.

Future competitive initialization is provisionally turn-based: players sequentially select eligible starting tiles and make basic DNA choices for their founding species. The ordinary shared simulation begins only after setup is complete and is not turn-based. Turn order, tile reservation, and balancing rules remain to be defined when multiplayer is planned.

# Time model and controls

The simulation advances in discrete fixed-duration ticks. The initial default is one simulated hour per tick, but tick duration is a configurable world value that we expect to tune. A world's selected duration is stored as part of its simulation configuration so it remains stable across saves and deterministic replays.

The calendar derived from these ticks drives hourly, daily, monthly, and seasonal conditions. Many individual organisms may be born and die within a day, week, or month, while the distribution and resiliency of a species determine whether its lineage survives changes across seasons and years.

Every world configuration includes a final simulation date. Reaching that deadline concludes the run and evaluates the final state before any narrative epilogue occurs. Survival succeeds if the controlled species remains alive; free sandbox records the surviving tree of life. Presentation may frame the deadline as an unavoidable world-ending event or a whimsical external conclusion without changing the evaluated simulation result.

The simulation can be paused and supports multiple execution-speed settings. Speed changes alter how quickly the engine processes ticks in real-world time; every intervening tick and organism interaction is still simulated. LYFE does not support jumping to a future date, skipping ticks, or coarsening the time step as a fast-forward mechanism because doing so would lose simulation fidelity. At the fastest speed, the simulation and mutation economy should allow a thriving species to reach a meaningful mutation decision every few real-world minutes.

Single-player worlds give the player authority to pause and change speed. Future competitive worlds will use a shared server-controlled clock; the multiplayer policy for exercising pause and speed remains to be defined.

# Randomness and deterministic replay

Every world has a controllable root seed. That seed governs both initial world generation and all future simulation randomness, including environmental variation, organism behavior, random founder selection, and autonomous speciation choices.

We should strive for the same root seed, simulation rules version, configuration, initial setup, and ordered gameplay decisions applied at the same simulation ticks to produce the same sequence of states and events. Saving and checkpointing must preserve enough seeded random state and decision history to continue or replay a world deterministically. Differences in execution speed, pausing, client presentation, or parallel scheduling must not change the simulated outcome.

Presentation-only randomness, such as choosing an organism for the camera to follow, must be isolated from simulation randomness so observation cannot alter the world's future.

# Structure

The authoritative simulation runs on a centralized server. The server owns world state, tick processing, seeded randomness, gameplay-command validation, saving, replay, and player-knowledge state. Thin clients send player decisions and receive only the snapshots, events, and aggregates the actor is permitted to observe; they do not independently advance or resolve simulation state or receive hidden current tile state for client-side masking.

The server-client boundary is a v1 architectural requirement even when both processes run on the same machine. It allows different clients to share one simulation and lets larger worlds scale by allocating stronger server hardware. V1 does not require a distributed simulation cluster, but its thin-client protocol should avoid tying the authoritative engine to one presentation technology.

Server updates should preserve stable organism identities and coordinates so clients can render simulated organisms one-to-one. If a client uses aggregation at a distant zoom or under extreme density, that aggregate must be explicitly distinct from an organism entity rather than represented by invented or duplicated organisms.

# Actors

V1 free sandbox has one human actor who may transfer control between species. V1 survival has one human actor bound to a species and its descendant lineage. Future competitive mode supports different human actors, each bound to their own lineage and connecting from a separate client to a shared server.

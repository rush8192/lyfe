The player's primary role in LYFE is to observe the world, understand why species are succeeding or struggling, and guide evolution through mutation and speciation decisions. The simulation controls the moment-to-moment behavior of organisms. Apart from choosing which organism to follow, players do not directly command movement, feeding, predation, or other organism behavior.

# Starting a world

By default, a single-player world begins with exactly one species. The player chooses its initial DNA from a narrow pool of primitive capabilities and then selects its starting location from the generated world's eligible volcanic ocean tiles. The pool contains no more than one or two starting metabolisms, with likely alternatives emphasizing hydrogen gas or sulfur compounds, plus a small number of choices that trade metabolic efficiency against environmental tolerance. Restricting the location choice to eligible tiles keeps the setup understandable and ensures that the initial species has a plausible foothold. The world contains no other life at this point; the tree of life begins to branch when the player triggers the first speciation event.

Competitive multiplayer is planned after v1. Its provisional start uses a turn-based setup phase in which players select eligible starting tiles and make basic choices from the limited founding-DNA pool. Once every player has established a founding species, the normal shared simulation begins. Turn order, whether and how tiles are reserved, and balance between available starting positions remain future design questions.

## Abiogenesis introduction

The transition from an empty world to the founding species should be introduced through a short, whimsical storybook sequence centered on lightning striking the primitive volcanic ocean. The sequence represents repeated energy inputs, chemical reactions, immense time, and random chance gradually creating an opportunity for a self-sustaining, replicating system to gain a foothold as life.

The presentation should not claim that a single lightning strike literally created life or that science has established one definitive path to abiogenesis. Lightning is a vivid narrative symbol for one plausible source of energy in a much longer and still uncertain process. The simulation begins with established life rather than attempting to model abiogenesis itself.

# Biological abstraction

LYFE follows the principle of being biology-inspired and internally mass-balanced. It is a game rather than a complete reconstruction of evolutionary history or biochemistry, so it may simplify exact compounds, pathways, timescales, and evolutionary sequences. Those abstractions should preserve the important pressures of the early living world: limited resources, energy capture and storage, environmental tolerance, reproduction, competition, and tradeoffs between capability and cost.

Within the chosen abstraction, nutrient matter must remain accounted for as it moves among organisms and environmental reservoirs, except through explicit sources and sinks. Whimsical presentation may simplify or personify events, but it should not undermine the causal rules of the simulation.

# Lineage and ownership

Every species retains a permanent link to the ancestor from which it speciated, creating a browsable tree of life that includes both living and extinct species. The default single-player world begins with one root, while future competitive setup may create one root lineage per player. Game modes use this same lineage record in different ways: free sandbox permits the player to move control anywhere in the tree among living species, while survival and future competitive modes bind each player to one descendant lineage. V1 survival does not permit backtracking, but the lineage record preserves the information needed for a later recovery mechanic.

Evolutionary choices are irreversible: mutation points cannot be refunded, a species' established DNA cannot be edited, and later descendants cannot undo their lineage history. Free-sandbox control changes operate by moving control to an eligible species already represented in the tree of life. Any post-v1 survival-backtracking mechanic must likewise move control to an existing eligible branch rather than reverse a speciation event or alter the DNA and history of an existing species.

# Game modes

V1 includes free sandbox and survival. Competitive multiplayer is a future mode, although the v1 architecture must preserve a path to it.

## Free sandbox

Free sandbox is an open-ended mode for exploring the entire world and its evolutionary possibilities.

- The player may take control of any living species and move between species at will.
- A controlled species receives human-directed mutation and speciation decisions. Uncontrolled species evolve autonomously.
- The player may lock a species to prevent it from mutating or speciating until the lock is explicitly removed. A lock persists when the player moves control to another species.
- The sandbox is lost early only when all life in the world is extinct. Reaching the world's final date concludes the run and records the surviving tree of life without requiring a conventional victory condition.

## Survival

Survival is a single-player, player-versus-environment mode centered on one evolutionary lineage.

- The player begins by customizing some traits of a starting species from a limited pool of available choices.
- The player is locked to that species and its descendants. When the player triggers speciation, control follows the chosen descendant species; the ancestor and other branches continue autonomously.
- The player loses immediately when the currently controlled species becomes extinct. V1 has no lineage-recovery or backtracking exception.
- A later version may add limited recovery by transferring control to a surviving branch already present in the tree of life, without reversing evolution.
- The player successfully completes the run when the controlled species remains alive at the world's final date.

## Competitive

Competitive mode is a future multiplayer form of survival and is not included in v1. Each player controls a species and remains bound to that species and its descendants as they mutate. A player loses when their controlled species becomes extinct. The provisional setup has players take turns selecting eligible starting tiles and basic founding DNA before the continuous shared simulation begins. Detailed placement, turn-order, balancing, clock control, and victory rules remain future design questions.

# Time and pacing

The simulation begins with a configurable time step of one simulated hour. This is short enough to model organisms whose complete lives occur within a day, week, or month while still allowing the player to experience seasonal and yearly ecological change. Individual organisms are transient on these longer timescales; the size, distribution, and resiliency of the species determine long-term survival.

Each world also has a configured final simulation date that acts as the deadline for the run. Reaching that date evaluates the final state before presenting an epilogue. The deadline may be framed as an unavoidable world-ending event, such as an asteroid impact, or through a more whimsical conclusion, such as visiting aliens awarding recognition to the life that survived.

The game supports multiple simulation-speed settings and can be paused when a single player controls the simulation clock. Speed settings process the same configured ticks at different real-world rates. There is no fast-forward that jumps over ticks or advances directly to a later date. At the fastest setting, a thriving species should accumulate enough mutation capacity for the player to make a meaningful mutation decision every few real-world minutes. This is a gameplay pacing target rather than a guarantee for species that are small or struggling.

Future competitive mode will run on a shared simulation clock, so its speed and pause policy cannot be controlled unilaterally by one player. The rules for pausing or changing speed in a multiplayer world remain a future design question.

# Observation controls

The player may choose to follow a randomly selected member of their currently controlled species. Following changes the camera or presentation only; it does not give the player direct control over the organism. Together with pausing, changing speed, inspecting the world, selecting a species, locking a sandbox species, and making evolution decisions, this defines the initial set of player actions.

Inspection includes historical views of key tile-resource levels and flows. The player should be able to see how important resources entered, left, transformed within, were consumed from, and were returned to a tile over time so that ecological success and failure can be understood rather than merely observed.

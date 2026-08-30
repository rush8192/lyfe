The player's primary role in LYFE is to observe the world, understand why species are succeeding or struggling, and guide evolution through mutation and speciation decisions. The simulation controls the moment-to-moment behavior of organisms. Apart from choosing which organism to follow, players do not directly command movement, feeding, predation, or other organism behavior.

# Starting a world

V1 offers exactly two starting metabolisms. Sulfide-powered anoxygenic phototrophy is the fast specialist: it grows more efficiently in shallow, illuminated, sulfide-rich volcanic water and begins with strong sulfide tolerance, but it performs poorly without both light and sulfide and faces an expensive path to a non-volcanic metabolism. Hydrogen acetogenesis grows more slowly but functions without light and shares capabilities with a cheaper route through organic-resource uptake and fermentation. “Non-volcanic” means less dependent on local volcanic substrates, not universally adapted to every environment. A small number of additional choices may trade efficiency against environmental tolerance without removing this central distinction.

The engine should permit later scenarios to expose additional data-defined founding metabolisms, but v1 does not add a third card merely for symmetry. Iron-powered anoxygenic photosynthesis is the leading future candidate once bulk iron oxidation states and mineral deposits are modeled. Methanogenesis and dark sulfur metabolisms may become distinct when methane climate/ecology and sulfur redox cycles are deeper systems. Fermentation remains a secondary consumer of existing organic matter rather than a self-sustaining default origin. Alternate biochemistries such as silicon-based or non-water life would require separate world rules rather than a metabolism trait inside the same aqueous CHNOPS simulation.

Free sandbox begins with one player-chosen founder by default. In survival, the player chooses one metabolism and selects an eligible volcanic ocean tile; an autonomous competing founder receives the other metabolism in a reserved edge-sharing tile suited to it. Both begin with the same organism count, relative energy reserves, lifecycle phase, zero mutation points, and unmodified simulation rules. They do not share a tile resource pool at tick zero, and the autonomous species receives no hidden economic or probability bonuses. Restricting both locations to a generated paired volcanic region gives each metabolism a plausible foothold while keeping later exchange, migration, and competition relevant.

Competitive multiplayer is planned after v1. Its provisional start uses a turn-based setup phase in which players select eligible starting tiles and make basic choices from the limited founding-DNA pool. Once every player has established a founding species, the normal shared simulation begins. Turn order, whether and how tiles are reserved, and balance between available starting positions remain future design questions.

## Abiogenesis introduction

The transition from an empty world to its founding population or populations should be introduced through a short, whimsical storybook sequence centered on lightning striking the primitive volcanic ocean. The sequence represents repeated energy inputs, chemical reactions, immense time, and random chance gradually creating an opportunity for self-sustaining, replicating systems to gain a foothold as life.

The presentation should not claim that a single lightning strike literally created life or that science has established one definitive path to abiogenesis. Lightning is a vivid narrative symbol for one plausible source of energy in a much longer and still uncertain process. The simulation begins with established life rather than attempting to model abiogenesis itself.

# Biological abstraction

LYFE follows the principle of being biology-inspired and internally mass-balanced. It is a game rather than a complete reconstruction of evolutionary history or biochemistry, so it may simplify exact compounds, pathways, timescales, and evolutionary sequences. Those abstractions should preserve the important pressures of the early living world: limited resources, energy capture and storage, environmental tolerance, reproduction, competition, and tradeoffs between capability and cost.

Within the chosen abstraction, nutrient matter must remain accounted for as it moves among organisms and environmental reservoirs, except through explicit sources and sinks. Whimsical presentation may simplify or personify events, but it should not undermine the causal rules of the simulation.

# Lineage and ownership

Every species retains a permanent link to the ancestor from which it speciated, creating a browsable tree of life that includes both living and extinct species. A non-species abiogenesis origin groups the independent root species created during setup: one root in the default sandbox and two roots in survival. The two survival founders have no parent species and are not ancestors of one another. Every later species has exactly one parent. Game modes use this same lineage record in different ways: free sandbox permits the player to move control anywhere in the tree among living species, while survival and future competitive modes bind each player to one descendant lineage. V1 survival does not permit backtracking, but the lineage record preserves the information needed for a later recovery mechanic.

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

- The player chooses sulfide anoxygenic phototrophy or hydrogen acetogenesis and customizes a few permitted efficiency-versus-tolerance traits. An autonomous species begins with the other metabolism in the paired starting tile.
- The player is locked to that species and its descendants. When the player triggers speciation, control follows the chosen descendant species; the ancestor and other branches continue autonomously.
- The player loses immediately when the currently controlled species becomes extinct. V1 has no lineage-recovery or backtracking exception.
- The competitor follows the same mutation economy and autonomous-evolution rules as other uncontrolled species. Its extinction does not itself win or end the run.
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

Inspection is limited by exploration in every game mode. At tick zero, the player's starting tile is live and its edge-sharing neighbors are revealed with reduced information. Occupation by the currently controlled species grants a live view of a tile, including organisms, exact current conditions, and resource stocks and flows. It also reveals the basic composition of that tile's edge-sharing neighbors. When the controlled species leaves a tile, the tile becomes remembered: its fixed attributes and last observed information remain visible with an observation timestamp, but its organisms and changing conditions no longer update. Tiles that have never been discovered remain hidden.

Player knowledge persists across control transfers in free sandbox, but live information always follows the currently controlled species. Switching to another species can therefore reveal the tiles it currently occupies; tiles revealed under earlier control remain only as remembered knowledge when no currently controlled organisms inhabit them. Historical charts show exact observations only through periods in which the tile was live and must not imply knowledge of hidden intervals.

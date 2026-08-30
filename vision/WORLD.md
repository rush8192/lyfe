The World of LYFE is a finite two-dimensional grid of connected tiles. Its east-west `x` dimension wraps, while its north-south `y` dimension is bounded. Each grid cell has unique attributes that contribute to its suitability for the life forms that inhabit it. We distinguish fixed geography, long-term climate and geological baselines, and current conditions that change with the season, random variation, and events.

# Fixed geography

Each tile has a unique integer `(x, y)` grid coordinate that locates it within the world. This is distinct from the local coordinates of organisms inside the tile. The minimum and maximum `x` edges connect, representing east-west continuity. The minimum and maximum `y` edges do not connect, so tiles along those boundaries have no neighbor farther north or south. Each tile therefore has up to four edge-sharing neighbors. The world's equator lies at `y = 0`; the sign of `y` identifies the northern or southern hemisphere, while its distance from zero acts as a latitude and constrains the climates that can be generated for the tile.

Elevation is measured relative to sea level, with zero marking the boundary between land and water. A positive elevation produces a terrestrial tile, while a negative elevation produces an aquatic tile whose water depth is the magnitude of its negative elevation. Aquatic vs terrestrial is therefore derived from elevation rather than generated as an independent attribute. Elevation and water depth affect which terrain types are plausible and how much sunlight can reach aquatic organisms.

# Climate and geological baselines

- Temperature normals, defined as a normal distribution for each month of the year. Plausible ranges correlate with latitude, elevation, and whether the tile is terrestrial or aquatic.
- Precipitation normals, defined as a normal distribution for each month of the year.
- A surface-moisture baseline for terrestrial tiles. Surface moisture is distinct from precipitation and represents the water currently available at the surface to organisms.
- Volcanic activity, defined by a mean and standard deviation by year.
- Nutrient baselines, with organic and inorganic quantities for each macronutrient and a single bioavailable quantity for each micronutrient.
- Tile-local atmospheric-gas reservoirs. A small set of named gases may be tracked where their distinct stability, transport, or metabolic role matters.
- Nutrient sources and sinks for resources introduced or removed by geological, volcanic, atmospheric, or other inorganic processes. These rates may depend on parameters such as volcanic activity.

Insolation is derived more directly from a tile's latitude and the time of year. Weather conditions such as cloud cover may attenuate the amount of sunlight that reaches the surface, and greater water depth or turbidity reduces the sunlight that reaches aquatic organisms.

# Current conditions

At a given point in simulation time, each tile has current conditions produced by its fixed geography, baselines, time of year, global variation, and recent events. These conditions are evaluated on the simulation's configurable fixed-duration ticks, initially one simulated hour:

- Current temperature, sampled from or otherwise guided by the temperature normal for the current time of year.
- Current precipitation, guided by the precipitation normal for the current time of year.
- Current surface moisture on terrestrial tiles. It varies around the tile's baseline in response to recent precipitation patterns, allowing some terrestrial tiles to be wet enough for moisture-dependent organisms during only part of the year.
- Current insolation, derived from latitude and time of year and attenuated by current weather, or by water depth and turbidity for aquatic organisms.
- Current volcanic activity.
- Current nutrient levels, with organic and inorganic quantities for each macronutrient and a single bioavailable quantity for each micronutrient.
- Current quantities of tracked atmospheric gases.
- Current living organisms and dead remains.

Cross-tile interactions are limited to edge-sharing neighbors, including neighbors connected across the wrapped `x` boundary. No interaction crosses a bounded `y` edge. Resources may move between neighbors at a rate that depends on the exchange compatibility of the two tiles. For example, resources can move relatively easily from an ocean tile to a shallow sea but have more difficulty moving onto a terrestrial tile; gases are more likely to migrate than solid nutrients. Compatibility is derived from relevant fixed attributes and current conditions and also influences whether organisms can cross the boundary.

Atmospheric gases remain tile-local state even when they mix quickly. Stable and abundant gases use high neighbor-exchange rates and can become approximately global in distribution; oxygen produced predominantly in a few tiles should therefore spread widely over time. Reactive or short-lived volcanic gases use lower effective reach and may undergo high rates of transformation or attrition through an explicit sink, keeping useful concentrations close to their sources. No separate instantly mixed global atmosphere overrides these tile reservoirs.

# Within-tile space

To start, each tile is a well-mixed compartment. Environmental conditions and nutrient pools are uniform within the tile, so consuming or releasing a resource changes the tile-wide pool rather than a localized concentration. Each living organism and dead remnant nevertheless has a specific coordinate within the tile.

Ocean water and terrestrial surface moisture are effectively inexhaustible environmental resources at the scale of the simulation. Organism use does not measurably reduce water depth or surface moisture; those conditions are governed by geography, climate, precipitation, and other environmental processes. Accessing hydrogen or oxygen bound in water may still require an appropriate metabolism and energy expenditure.

Within-tile positions determine which organisms and remnants are close enough for direct interactions such as predation and scavenging. An organism must be sufficiently near an edge shared with a neighboring tile before it can migrate into that tile. Its capabilities and the compatibility of the two environments determine whether and how readily it crosses. Passive environmental movement may still carry organisms, but it is subject to the same boundary requirement.

Dead remains are cohesive entities with their own identity, position, and remaining contents. Nearby organisms may consume them wholly or partially. Whatever remains eventually decays, transferring its accounted nutrients into the tile's organic resource pools.

As a future extension, micronutrients may be represented as localized concentration fields rather than tile-wide pools. This would allow depletion, release, diffusion, and sensing to create meaningful micronutrient gradients within a tile. The initial simulation will treat micronutrients as well-mixed.

# Global variance over time

To model the inherent randomness of the living world, each year may have some additional random global deviation from the 'normal' parameters of its tiles. Some years may be hotter than average; some may be wetter. In some years, volcanic activity might wipe out almost all life in a particular tile. These deviations and events are generated deterministically from the world's root seed and current simulation state.

# Exploration and tile knowledge

The world is not fully revealed at the start. In every game mode, the player's starting tile and its edge-sharing neighbors are visible initially. Visibility has three meaningful states:

- **Live:** any tile currently inhabited by the player's controlled species. The player can inspect living organisms and remains, exact current environmental conditions, current resource stocks and flows, and the full recorded history from periods when the tile was live.
- **Reduced:** an edge-sharing neighbor of a live tile, or a discovered tile the controlled species previously inhabited. The player retains fixed geography, climate and geological baselines, basic or coarse resource composition, and timestamped last-known information. Living organisms and remains are hidden, and variable state such as exact current temperature, weather, gas quantities, and resource stocks is neither shown nor updated.
- **Unknown:** a tile that has never been the starting tile, adjacent to a live tile, or occupied by the controlled species. It exposes no inspectable composition or current state.

Moving into a tile promotes it to live and reveals its neighbors at reduced detail. Leaving a tile demotes it to reduced unless another organism of the controlled species remains. Discoveries persist as player knowledge, but stale information must be labeled with when it was observed. In free sandbox, accumulated player knowledge persists when control changes species, while live visibility is recalculated from the newly controlled species' occupied tiles.

Visibility affects information only and must never change simulation behavior, randomness, or environmental resolution. The authoritative server enforces it; hidden state is not sent to a client merely to be covered by presentation fog.

# Generating the world

The world is generated randomly, but in a manner that roughly resembles the structure of our own world. Every generated tile receives a world `(x, y)` coordinate and an elevation. Elevation determines whether it is terrestrial or aquatic and, for aquatic tiles, its water depth. The tile's `y` coordinate directly informs its insolation pattern and constrains plausible temperature ranges, while elevation and neighboring geography provide additional climate correlations.

For example, most shallow ocean tiles border beach tiles; beach tiles often border flatlands; flatlands often border uplands; and uplands often border mountains. Mountains may occasionally border the ocean directly, but this is much rarer. We will define how likely certain neighboring combinations are to occur, start from an initial tile such as a volcanically active shallow ocean, and then programmatically generate the rest of the world from the distribution of likely neighboring attributes.

The player can supply the world's root seed or allow the game to create one. The root seed controls both deterministic world generation and all future seeded random decisions in that world. Given the same simulation configuration and the same ordered player decisions, the seed should reproduce the same history rather than only the same starting map.

We should support top-level grid dimensions that determine the number of tiles in a generated world.

# Starting conditions

World generation must produce volcanic ocean tiles capable of supporting both permitted founding-species configurations. Free sandbox requires at least one eligible tile for either player-selected metabolism. Survival requires paired starting regions: when the player selects an eligible tile for one metabolism, an edge-sharing volcanic ocean tile must be reserved and eligible for the other. The precise volcanism, depth, temperature, light, gas, and nutrient thresholds belong in the balance plan, but setup must always have a viable deterministic choice.

Starting volcanic tiles are productive but deliberately harsh environments rather than universally desirable habitats. Their gases and geological processes provide substrates for primitive metabolisms, but high hydrogen-sulfide and sulfur-dioxide exposure imposes additional energy costs and may kill organisms without suitable chemical tolerance. A starting tile may also lack micronutrients required by later capabilities, encouraging descendants to migrate, import or scavenge scarce matter, or specialize for other environments rather than remaining indefinitely in the original refuge.

Free sandbox begins with one living species in the selected tile by default. Survival begins with the player-controlled species in the selected tile and an autonomous competing species using the other founding metabolism in its paired tile. The two are independent roots rather than ancestor and descendant.

Future competitive setup begins with an empty world and lets players take turns selecting eligible starting tiles and making basic DNA choices for their founding species. Each player therefore creates a root lineage before the shared simulation begins. Turn order, tile-reservation rules, and competitive balancing remain future design questions.

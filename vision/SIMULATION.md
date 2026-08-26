To bring the world to life, we want a core engine that can simulate each individual organism in each tile of the world, along with the systems of the world they interact with.

# Scope

The scope of the simulate will be large: we aspire to handle simulating millions of organisms in a particular world, and many thousands in any given tile. We should architect the simulation in a way that will scale gracefully by default, instead of going back to re-architect and optimize later

To aid in parallelism, we can make sure that each tile is relatively self-contained, except for a few discrete cross-tile interactions (e.g. the migration of certain organisms or resources from one tile to an adjacent tile).

# State

We want to store the state of the world in a way where we can easily serialize the state, save it as a file, load it, and checkpoint it.

# Structure

We will assume the core simulation runs on a centralized server, and want to support a variety of different 'thin' clients that can display the current state of the simulation.

# Actors

To start there will be a single human actor guiding a single species from a single client, but we want to support different human actors each guiding their own species from a different client connecting to a shared server over the network. 
Vision for LYFE, the game of evolution and competition

# World

In LYFE, we create a living and evolving world. The world is composed of connected tiles which represent slices of our world (see WORLD.md). Each tile of the world has different factors that influence the ability of organisms to live in that environment: temperature, moisture, terrestrial vs aquatic, volanic activity, elevation and more. Importantly, the world is closed loop - anything that an organism takes from their environment are not available for other organisms to use, until the organism dies and releases the resources back to the environment.

# Organisms and Evolution

In LYFE, we will start with the simplest and most basic organisms, with access to a very limited set of tools: a primitive metabolism that requires external heat sources and inorganic compounds such Hydrogen Sulfide (H2S) to function; no locomotion or ability to sense the outside environment; no way to interact with other organisms directly; limited ability to store energy or withstand environmental changes; only the simplest asexual reproduction.

A species in LYFE is defined by a shared DNA; this shared DNA codifies all the physical attributes and behavior of each member of that species.

As a species manages to survive and grow in number, the species will earn abstract mutation 'points', which can be spent on modifying the DNA of the species. When a species mutates, we modify the DNA of a co-located subset of the population, and 'fork' that subset into a new species, sharing the new DNA. Members of the old species will be left behind unchanged, so a new species will also be competing with its ancestor specie(s) for resources.

Some species are directed by a human controller, who chooses when to make changes to a particular aspect of the species DNA. Uncontrolled species will also accumulate mutation points, but will generate more species at random; the more mutation points that have been accumulated, the more likely that one of these random speciation events will occur.

No progress comes without a cost: more complex organisms require more efficient metabolisms, which may also include depedency on extracting additional resources from their environment to survive and thrive (see NUTRIENTS.md). A metabolism that functions efficiently within a certain set of environmental parameters may fail completely when the environment changes.

# Nutrients

To model the interdependency of organisms and the environment, we try to capture the key cycles that define the struggle of life on earth for the key macro elements: carbon, nitrogen, hydrogen, sulfur, phosphorus, and oxygen. We model both these macronutrients, and also track the presence and abundance level of micronutrients that are required by more specialized forms of life.

# Simulation

We want to simulate individual organisms within the world, and want to support a large number of these organisms efficiently

The initial game will only have a single human player, but we want to support multiple players guiding different species in the future

We want the core simulation to run on a server, and to support a number of different 'thin' and varied clients that can render the current state of the simulation.


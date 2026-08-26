The World of LYFE is a series of connected tiles. Each tile has unique attributes that contribute to that tile's suitability for the life forms that inhabit it. The tile has some fixed parameters, as well as some parameters that vary over time, e.g. due to the yearly cycle of the seasons, or due to changes in global climate

# Initial Parameters

- Elevation
- Temperature (defined as a normal distribution for each month of the year, with mean and std deviation)
- Aquatic vs Terrestrial
- Volcanic activity (Mean and std deviation by year)
- Nutrient baseline (for all relevant macro and micro nutrients)
- Nutrient production (for certain resources like methane or ammonia that are produced by geological/volcanic processes, and which may be dependent on the volcanic activity parameter)
- Precipitation (defined as a normal distribution for each month, with mean and std deviation)
- Insolation: amount of sunlight, varies by latitude and season in a fairly deterministic fashion. Higher moisture may attenuate the amount of sun that reaches the surface, and deep sea environments get little sunlight

# Current parameters

In addition to the fixed parameters, some parameters will vary over time, based on the rate of production from organism and inorganic sources:

- Current nutrient levels
- Current living and dead organisms

In addition, some resources (and organisms) may diffuse from one tile to all neighboring tiles, at a rate that depends on the exchange-compatibility of the two tiles (e.g. resources have a relatively easy time migrating from an ocean tile to a shallow sea, but a harder time migrating onto a terrestrial tile; gasses are more likely to migrate than are solid nutrients, etc)

# Global variance over time

To model the inherent randomness of the living world, each year may have some additional random global deviation from the 'normal' parameters of its tiles. Some years may be hotter than average; some may be wetter. In some years, volcanic activity might wipe out almost all life in a particular tile.

# Generating the world

The world is generated randomly, but in a manner that roughly resembles the structure of our own world. For example, most shallow ocean tiles border beach tiles; beach tiles often border flatlands; flatlands often border uplands; uplands often border mountains. Mountains may occassionally border the ocean directly, but this is much more rare. We will define how likely certain neighboring combinations are to exist; start from some initial 'seed' tile (a volcanically active shallow ocean), and then programatically generate the rest of the world from the distribution of how likely neighboring tiles are to have similar attributes. We want a deterministic way of generating a world for a particular seed, but should support an infinite variety of random seeds.

We should support a top-level parameter which indicates how many tiles are in a particular world we are generating.
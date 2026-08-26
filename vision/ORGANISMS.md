Organisms and their DNA are an essential driver of the processes at work in LYFE. 

# DNA

The core blueprint of an organisms capabilities, metabolism, and limitations is contained within its DNA. 

We abstract the DNA into a few different areas that define the organism's behavior.

In general, greater capabilities result in greater overhead when reproducing.

# Mutation and Speciation

As a species expands and thrives, it will accumulate mutation points that will eventually be used to modify the organisms DNA to gain new capabilities.

For human-directed species, the human will decide when to spend the accumulate mutation points and trigger speciation. Speciation is location-dependent: some fraction of organisms within a given tile will gain the new species DNA, while all other organisms will be left behind (becoming competition, and being allowed to continue to evolve on their own). For non-human-directed species, the accumulate of mutation points will trigger random speciation events; the more mutation points accumulated, the more likely speciation will trigger. Certain adaptations will allow more mutation points to be accrued before speciation triggers (e.g. sexual reproduction), allowing larger leaps to be made at once.

# Capabilities

Organisms DNA encodes a variety of different capability categories that can be independently modified.

## Locomotion

Organisms can evolve ways to move around their environment. By default this locomotion is somewhat random in nature, but this alone can still allow the organism to penetrate neighboring environments at a higher frequency from the baseline. Locomotion takes energy!

### Locomotion forms

Flagellar Propulsion: Organisms use one or more long, whip-like tails called flagella that rotate or lash back and forth to swim through liquid. Fast but energy hungry

Ciliary Beating: Cells are covered in short, hair-like structures called cilia that beat in a coordinated, rhythmic wave to propel the organism through water. Moderate speed and energy consumption

Amoeboid Motility (Pseudopodia): Cells push out temporary cytoplasmic extensions known as pseudopodia (false feet), anchor them to a solid surface, and pull the cell body forward. Slower velocity, but fairly efficient, and able to move in non-liquid environments

## Detecting gradients / sensing

Organisms can detect the presence of absence of nutrients or prey in a given environment. When combined with locomotion, this can make them more efficient at gathering the resources they need.

## Moisture tolerance

Some organisms must live in water, and thus require either an aquatic tile, or a high-moisture terrestrial tile

## Temperature tolerance

Organisms can evolve the ability to withstand hotter and/or colder temperatures (at the cost of less metabolic efficiency)

## Eukaryotic cells

Evolving separate compartments for storing DNA vs other organelles is needed for more complex metabolic pathways, predation, and advanced capabilities

## Predation and scavenging

Organisms can evolve the ability to scavenge off the remnants of dead organisms, or to actively kill and consume other organisms

## DNA exchange

Simple organisms only reproduce through asexual reproduction, but even single-celled organisms may evolve ways to exchange DNA with nearby organisms of the same species while reproducing, i.e. via conjugation. This should increase that specicies capacity to accumulate mutation points without triggering speciation.

## Metabolism

The metabolism of an organism is one of its most essential and defining traits. Metabolisms consume different substrates, and can evolve to be more or less efficient, with tradeoffs in resiliency or reliance on rarer nutrients.

Basic metabolisms are anaerobic and rely on specific conditions, i.e. thermal gradients and compounds released by volcanic activity, allowing for chemotrophy as a viable pathway.

Fermentation emerged next as an option for breaking down organic molecules without oxygen (only possible when organic compounds were available in greater abundance)

Anoxygenic Photosynthesis first emerged as a limited option for using sunlight to help feed on sulfur compounds, but later opened dramatic new opportunities for life via Oxygenic Photosynthesis, allowing cells to harness the sun's energy to transform carbon dioxide into usable organic compounds (producing free oxygen as a waste product).

Once free oxygen became abundant, respiration emerged as a new mechanism for generating energy to power cellular processes.

# Lifecycle

We attempt to model the full lifecycle of an organism. In the simplest form, organisms begin as fully mature organisms after breaking off from their parent, and then may die, leaving behind a remnant that can serve as fuel for other organisms, and which will eventually naturally break down and release any latent nutrients back into the environment.

More advanced organisms may have additional lifecycle phases, like a dormant or 'seed' phase that is metabolically relatively inert, but which has increased tolerance for environmental parameters outside the norm. An organism might also have a `adolescent` phase more geared on mobility and finding an optimal environment before settling down for reproduction.

# Scope and Future Direction

We currently only plan to model single-celled organisms, but want to leave open the possibility of tracking the evolution of multicellular life.
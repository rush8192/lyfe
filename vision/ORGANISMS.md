Organisms and their DNA are essential drivers of the processes at work in LYFE.

# DNA

The core blueprint of an organism's capabilities, metabolism, and limitations is contained within its DNA.

We organize DNA into separately displayed trait families that define different aspects of the organism's behavior, such as locomotion, sensing, environmental tolerance, cellular structure, predation, reproduction, and metabolism. Trait families make the DNA understandable and allow each area to have a linear or tree-like progression, but they are not mechanically isolated from one another.

A trait may require traits or minimum capability levels from its own family or another family. Traits may also be mutually incompatible, and selecting a trait in one family may alter the effectiveness, metabolic requirements, or reproductive overhead of traits in other families. These cross-family relationships are part of the DNA model rather than exceptional cases.

For the initial simulation, DNA belongs to the species rather than to each organism independently. Every organism in a species has exactly the same gameplay-relevant DNA. Organisms differ through their position and lifecycle state, including attributes such as age, health, and stored energy, but these differences are not heritable. Minor visual differences between organisms of the same species are cosmetic and have no effect on simulation behavior.

In general, greater capabilities result in ongoing metabolic or reproductive costs. These continuing costs are distinct from any mutation points spent to acquire the capability during speciation.

# Initial species

The default single-player world begins with exactly one species. The player selects its DNA from a narrow set of permitted primitive capabilities and places it in an eligible volcanic ocean tile. The founding pool contains no more than one or two metabolism choices, with likely alternatives emphasizing hydrogen gas or sulfur compounds released by volcanic activity. A few other choices trade metabolic efficiency against environmental tolerance. All founding organisms share the resulting DNA, and there are no autonomous or competing species until the first speciation event creates a descendant and leaves the ancestor branch behind.

# Mutation and Speciation

As a species expands and thrives, it will accumulate mutation points that can be used to modify its DNA and create a descendant species with new capabilities. Controlled and uncontrolled species earn mutation points through the same rules. Human control changes who chooses when and how to spend the points, not how they are earned. A sandbox mutation lock prevents a species from mutating or speciating but does not prevent it from continuing to accumulate points.

The rate at which a species generates mutation points is a function of three inputs:

- The total number of living organisms in the species across the world.
- The average relative health of those organisms.
- Evolutionary modifiers provided by the species' DNA, including capabilities such as DNA exchange, sexual reproduction, and eukaryotic cellular organization.

Population and health provide the biological activity being rewarded, while DNA modifiers represent a species' capacity to generate and preserve useful variation. The exact curve, relative weight, and scaling limits for these inputs belong in the implementation and balance plan. Regardless of the final formula, larger and healthier populations should generally generate points faster, and controlled and uncontrolled species must use the same calculation.

For a human-directed species, the player decides when to spend accumulated mutation points, which DNA changes to make, and where to found the descendant species. For an uncontrolled species, accumulated mutation points drive autonomous speciation events; the more points accumulated, the more likely speciation becomes. The autonomous system chooses the DNA changes and founding location stochastically, but weights its choices toward adaptations relevant to current environmental pressures. Recent death triggers and their contributing stresses provide an important signal: repeated starvation, thermal stress, moisture stress, or predation should make relevant valid adaptations more likely. The heuristic may use the species' recorded past and current state but must not inspect future outcomes. Certain adaptations, including DNA exchange, sexual reproduction, and eukaryotic cellular organization, modify mutation-point generation and speciation capacity, enabling larger leaps to be made at once. DNA exchange and sexual reproduction are abstract species-level modifiers in the initial simulation and do not create or recombine distinct individual genomes.

The mutation-point price of the selected DNA changes is deducted when speciation occurs. Both the ancestor and the new descendant species receive the full unused balance that remains after this price is paid. The balance is therefore intentionally preserved on both branches rather than divided between them.

Speciation is location-dependent. A speciation event may select any one to four tiles occupied by the ancestor species in which to found one new descendant species; the selected tiles do not need to be adjacent. The player selects the tiles for a controlled species, while the autonomous system selects them for an uncontrolled species. The initial simulation uses a fixed founding fraction based on the number of selected tiles:

| Selected tiles | Randomly selected organisms converted in each tile |
| --- | --- |
| 1 | 50% |
| 2 | 20% |
| 3 | 8% |
| 4 | 3% |

The fraction applies independently to the ancestor population in every selected tile. The chosen organisms form random subsets and all receive the same descendant-species DNA; organisms not selected remain members of the ancestor species. In a future version, the player may be able to choose a smaller founding fraction by spending additional mutation points.

Which species are human-directed depends on the game mode. In free sandbox, the player may transfer control between living species at will and may lock a species against all mutation or speciation until it is explicitly unlocked. In survival and future competitive modes, a player remains bound to one lineage; when speciation occurs, control follows the chosen descendant while the ancestor and other branches evolve autonomously.

When DNA for a new species is created, its complete set of traits must satisfy all prerequisites and incompatibilities. Its resulting capabilities and ongoing costs are calculated from the interactions among all of its trait families, not by evaluating each family independently.

Evolution is irreversible. Once a descendant species is founded, its DNA and the speciation event cannot be edited, refunded, or changed back into its ancestor. Future descendants can only follow valid forward paths from the DNA they inherit. The continuing ancestor branch preserves the earlier DNA. V1 survival does not allow backtracking, but a later game mode or recovery mechanic may allow a player to resume from an eligible existing branch without reversing an evolutionary change or rewriting the tree of life.

# Lineage

Every speciation event records a parent-child relationship between the ancestor and descendant species. Together, these relationships form the world's tree of life. The lineage record persists after a species becomes extinct and should retain enough information to understand when and where speciation occurred and which DNA changes separated the descendant from its ancestor.

In the initial simulation, every species has a single ancestor and is reproductively isolated from all other species as soon as it is founded. Organisms cannot reproduce or exchange DNA with members of an ancestor, descendant, sibling, or unrelated species. The absence of inter-species breeding keeps lineage as a strict branching tree rather than a network.

# Health and energy storage

Every organism stores a limited reserve of chemical energy in organic compounds. Its DNA defines a maximum energy-storage capacity, which starts with a relatively low ceiling. Mutations may increase this ceiling through incremental improvements, while major metabolic or cellular capabilities may produce larger step-function increases.

An organism's relative health score is grounded in its current stored energy as a proportion of its maximum storage capacity, then adjusted by other internal state and current environmental conditions. Relevant factors may include stored nutrients, age, lifecycle phase, and environmental stress. This normalized score allows health to be compared across organisms and species with different capacities. A species' average health is the mean relative health of all its living organisms across the world and is one of the inputs to its mutation-point generation rate.

Stored energy is consumed by baseline maintenance and by capabilities such as movement and reproduction. Environmental stress may increase this expenditure. A larger storage capacity provides a longer buffer against poor conditions, but it does not create energy and may carry its own metabolic or reproductive costs.

# Individual state and behavior

Each organism stores its age, lifecycle phase, stored energy, stored nutrients, current tile, within-tile position, velocity, environmental stresses, and current behavior. Health is derived from this internal state and the surrounding environment rather than stored as an unrelated pool.

Behavior captures what the organism is currently trying to do. Primitive organisms may move or feed with little direction, while more advanced organisms may switch behaviors based on age, health, resource reserves, sensed gradients, nearby organisms, and environmental conditions. Available behaviors and the rules for selecting among them are encoded by DNA. Examples include seeking a scarce resource, following a resource gradient, hunting prey, scavenging, conserving energy, seeking a reproductive opportunity, or moving toward a more favorable tile boundary.

# Tick actions

Every organism is evaluated once per simulation tick and may perform one or more actions. Aging, baseline energy expenditure, metabolism attempts, and position updates occur on every tick. Other actions, including resource absorption, reproduction, predation, scavenging, behavioral changes, and death, are probabilistic and/or gated by the organism's DNA, health, reserves, lifecycle phase, environment, and proximity to valid targets.

Each tick updates position from velocity, and behavior may change that velocity for subsequent movement. Metabolism is also probabilistic: favorable conditions and sufficient external substrates increase the chance or yield of converting resources into stored usable organic compounds and energy. These processes use the world's seeded randomness so identical inputs can replay deterministically.

# Capabilities

An organism's DNA encodes a variety of trait families that can be explored and modified separately, subject to their cross-family prerequisites, incompatibilities, effects, and costs.

## Locomotion

Every organism occupies a specific coordinate within its current tile. Organisms can evolve ways to move through this local space. By default this locomotion is somewhat random in nature, but it can still help an organism find other organisms or dead remnants and reach the boundary of a neighboring environment more frequently than passive movement alone. An organism can migrate to a neighboring tile only when it is sufficiently near the shared edge. Locomotion takes energy!

### Locomotion forms

Flagellar Propulsion: Organisms use one or more long, whip-like tails called flagella that rotate or lash back and forth to swim through liquid. This form is fast but energy-hungry.

Ciliary Beating: Cells are covered in short, hair-like structures called cilia that beat in a coordinated, rhythmic wave to propel the organism through water. This form has moderate speed and energy consumption.

Amoeboid Motility (Pseudopodia): Cells push out temporary cytoplasmic extensions known as pseudopodia (false feet), anchor them to a solid surface, and pull the cell body forward. This form is slower but fairly efficient and can operate in non-liquid environments.

## Detecting gradients / sensing

Organisms can evolve the ability to detect nearby prey, dead remnants, or other organisms with which they can interact. When combined with locomotion, sensing allows movement to respond to local information instead of remaining entirely random.

To start, environmental conditions and nutrients are well-mixed within each tile, so they do not create within-tile spatial gradients. A future localized model for micronutrients may allow organisms to sense and follow micronutrient concentration gradients within a tile.

## Moisture tolerance

Some organisms must live in water and therefore require either an aquatic tile or a high-moisture terrestrial tile.

## Temperature tolerance

Organisms can evolve the ability to withstand hotter and/or colder temperatures at the cost of lower metabolic efficiency.

## Eukaryotic cells

Evolving separate compartments for storing DNA and other organelles supports more complex metabolic pathways and many advanced capabilities. Particular advanced traits may use eukaryotic organization as a prerequisite where that relationship serves the biological abstraction and gameplay.

## Predation and scavenging

Organisms can evolve the ability to scavenge off the remnants of dead organisms, or to actively kill and consume other organisms. The predator or scavenger must be within the appropriate interaction range of its target.

## DNA exchange

Simple organisms only reproduce through asexual reproduction, but even single-celled organisms may evolve abstract DNA-exchange capabilities inspired by processes such as conjugation. In the initial simulation, we do not simulate the transfer or recombination of individual genomes. Instead, DNA exchange increases the species' mutation-point generation or speciation capacity and enables larger changes during a later speciation event. Sexual reproduction is treated through the same species-level abstraction.

DNA exchange and sexual reproduction operate only within a species. A newly founded species is immediately incompatible with its ancestor and every other species. Because sexual reproduction is abstract in the initial simulation, it does not require two organisms to be near one another.

## Metabolism

The metabolism of an organism is one of its most essential and defining traits. Metabolisms consume different substrates, and can evolve to be more or less efficient, with tradeoffs in resiliency or reliance on rarer nutrients. On each tick, an organism attempts its available metabolic processes. The success and yield of an attempt may be probabilistic based on environmental favorability, substrate availability, DNA, and organism state. Successful metabolism converts external energy sources and nutrients into internally stored chemical energy and usable organic compounds. Energy dissipates when used, while the nutrients contained in the compounds remain part of the world's resource cycles.

Basic metabolisms are anaerobic and rely on specific conditions, such as thermal gradients and compounds released by volcanic activity, allowing chemotrophy to be a viable pathway.

Fermentation provides an option for breaking down organic molecules without oxygen. It becomes a viable evolutionary choice when sufficient organic compounds are available and the species has accumulated the mutation points needed to acquire it; it does not occupy a predetermined place in the world's evolutionary sequence.

Anoxygenic photosynthesis provides a limited option for using sunlight to help feed on sulfur compounds. Oxygenic photosynthesis opens dramatic new opportunities by allowing cells to harness the sun's energy to transform carbon dioxide and water into usable organic compounds, producing free oxygen as a waste product.

When free oxygen becomes abundant, respiration provides a powerful mechanism for generating energy to power cellular processes.

# Lifecycle

We attempt to model the full lifecycle of an organism. In the simplest form, organisms begin as fully mature organisms after splitting or budding from their parent. They may later die, leaving behind a cohesive remnant at their final location that can be consumed wholly or partially by nearby organisms and that eventually breaks down into organic tile resources.

## Reproduction

Reproduction is a net-zero transfer of material resources from a parent into a new organism. The offspring's stored nutrients and initial chemical energy must come from the parent; reproduction cannot create either. Performing the split also consumes additional stored energy, while the nutrients associated with that expended energy remain accounted for.

DNA defines the reproductive allocation model. In a true split, the original and new organism divide the parent's remaining reserves relatively evenly. In a budding or offspring model, the new organism receives a smaller share and the parent retains most of its reserves. DNA may further modify the minimum relative health required to attempt reproduction, its base attempt frequency, allocation ratios, and its additional energy cost. The exact values and probability curves belong in the balance plan.

Sexual reproduction remains an abstract species-level evolutionary capability in the initial simulation. It does not require a nearby mate, mate-seeking behavior, or recombination of individual genomes in order for an organism to reproduce.

More advanced organisms may have additional lifecycle phases, such as a dormant or "seed" phase that is relatively metabolically inert but has increased tolerance for environmental parameters outside the norm. An organism might also have an adolescent phase geared toward mobility and finding an optimal environment before settling down for reproduction.

Lifecycle state is initially evaluated once per one-hour simulation tick, with the tick duration remaining configurable for tuning. Many organisms may complete their entire lifecycle within a day, week, or month. Seasonal survival is therefore primarily an emergent property of the species population rather than the longevity of a particular organism.

## Death conditions

An organism may die through several distinct mechanisms:

- Senescence as it reaches or exceeds the lifespan allowed by its DNA and lifecycle.
- Starvation when its stored energy falls below the minimum reserve required to sustain life.
- Environmental stress. Conditions outside the organism's preferred range but inside its hard tolerance limits impose additional energy costs. Conditions beyond a hard limit create a chance of death each simulation turn, with the probability increasing as the condition moves farther beyond the limit.
- Predation when another organism successfully kills and consumes it.

The simulation should record every death trigger satisfied during the organism's final tick, along with relevant contributing stresses, so population health and mortality can be explained to the player. A death may therefore be attributed to multiple simultaneous causes rather than forced into one primary category.

# Scope and Future Direction

We currently only plan to model single-celled organisms, but want to leave open the possibility of tracking the evolution of multicellular life.

The biological model is intentionally biology-inspired rather than a claim of exact historical reconstruction. Simplified trait progressions and metabolisms should still preserve internal mass balance and reflect meaningful biological pressures and tradeoffs.

A future version may introduce heritable variation between individuals of the same species. This would allow organisms or subpopulations to experiment with new traits before full speciation, with those variants potentially succeeding, disappearing, recombining, or becoming the basis of a new species. This is outside the initial simulation; adding it would require explicit models for individual genomes, inheritance, selection, and the boundary between variation and speciation.

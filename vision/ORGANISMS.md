Organisms and their DNA are essential drivers of the processes at work in LYFE.

# DNA

The core blueprint of an organism's capabilities, metabolism, and limitations is contained within its DNA.

We organize DNA into separately displayed trait families that define different aspects of the organism's biology and behavior. Trait families make the DNA understandable and allow each area to have a linear or tree-like progression, but they are not mechanically isolated from one another.

A trait may require traits or minimum capability levels from its own family or another family. Traits may also be mutually incompatible, and selecting a trait in one family may alter the effectiveness, metabolic requirements, or reproductive overhead of traits in other families. These cross-family relationships are part of the DNA model rather than exceptional cases.

For the initial simulation, DNA belongs to the species rather than to each organism independently. Every organism in a species has exactly the same gameplay-relevant DNA. Organisms differ through their position and lifecycle state, including attributes such as age, health, and stored energy, but these differences are not heritable. Minor visual differences between organisms of the same species are cosmetic and have no effect on simulation behavior.

In general, greater capabilities result in ongoing metabolic or reproductive costs. These continuing costs are distinct from any mutation points spent to acquire the capability during speciation.

# Initial species

V1 has two exact founding-DNA baselines. Sulfide anoxygenic phototrophy is initially more productive in shallow, illuminated, sulfide-rich volcanic water and begins with stronger sulfur tolerance, but depends on its specialized niche. Hydrogen acetogenesis is initially less productive but operates without light and has a shorter trait path through organic uptake and fermentation toward a less volcanism-dependent existence. Additional founding choices may adjust efficiency and environmental tolerance within narrow bounds without reversing these identities.

Free sandbox begins with one player-chosen founding species by default. Survival begins with two independent root species: the player's selected metabolism in the chosen eligible tile and an autonomous species using the other metabolism in an eligible edge-sharing tile. Both use the same mutation-point and simulation rules and begin from equivalent population-scale state. All organisms within either founder share their species' DNA.

# Mutation and Speciation

As a species expands and thrives, it will accumulate mutation points that can be used to modify its DNA and create a descendant species with new capabilities. Controlled and uncontrolled species earn mutation points through the same rules. Human control changes who chooses when and how to spend the points, not how they are earned. A sandbox mutation lock prevents a species from mutating or speciating but does not prevent it from continuing to accumulate points.

The rate at which a species generates mutation points is a function of three inputs:

- The total number of living organisms in the species across the world.
- The average relative health of those organisms.
- Evolutionary modifiers provided by the species' DNA, including capabilities such as DNA exchange, sexual reproduction, and eukaryotic cellular organization.

Population and health provide the biological activity being rewarded, while DNA modifiers represent a species' capacity to generate and preserve useful variation. The exact curve, relative weight, and scaling limits for these inputs belong in the implementation and balance plan. Regardless of the final formula, larger and healthier populations should generally generate points faster, and controlled and uncontrolled species must use the same calculation.

For a human-directed species, the player decides when to spend accumulated mutation points, which DNA changes to make, and where to found the descendant species. For an uncontrolled species, accumulated mutation points drive autonomous speciation events; the more points accumulated, the more likely speciation becomes. The autonomous system chooses the DNA changes and founding location stochastically, but weights its choices toward adaptations relevant to current environmental pressures. Recent death-risk profiles, realized triggers, and contributing stresses provide an important signal: repeated starvation, thermal stress, moisture stress, or predation should make relevant valid adaptations more likely. A positive risk that did not trigger remains evidence of pressure but must be distinguished from the realized cause. The heuristic may use the species' recorded past and current state but must not inspect future outcomes. Certain adaptations, including DNA exchange, sexual reproduction, and eukaryotic cellular organization, modify mutation-point generation and speciation capacity, enabling larger leaps to be made at once. DNA exchange and sexual reproduction are abstract species-level modifiers in the initial simulation and do not create or recombine distinct individual genomes.

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

An organism's relative health score is grounded in its current stored energy as a proportion of its maximum storage capacity, then adjusted by other internal state and current environmental conditions. Relevant factors may include stored nutrients, viable structure, age, lifecycle phase, and environmental stress. This normalized score allows health to be compared across organisms and species with different capacities. A species' average health is the mean relative health of all its living organisms across the world and is one of the inputs to its mutation-point generation rate.

Stored energy is consumed by baseline maintenance and by capabilities such as movement and reproduction. Environmental stress may increase this expenditure. A larger storage capacity provides a longer buffer against poor conditions, but it does not create energy and may carry its own metabolic or reproductive costs.

# Individual state and behavior

Each organism stores its age and lifecycle state, concrete internal resource accounts, current tile, within-tile position, velocity, current behavior, and any small capability state that must persist across ticks. Current environmental stress is evaluated from the organism's DNA and surroundings. Health is derived from this concrete state and the surrounding environment rather than stored as an unrelated pool. Implementations may cache health for performance, but concrete state remains authoritative and health must be reproducible from it.

Behavior captures what the organism is currently trying to do. Primitive organisms may move or feed with little direction, while more advanced organisms may switch behaviors based on age, health, resource reserves, sensed gradients, nearby organisms, and environmental conditions. Available behaviors and the rules for selecting among them are encoded by DNA. Examples include seeking a scarce resource, following a resource gradient, hunting prey, scavenging, conserving energy, seeking a reproductive opportunity, or moving toward a more favorable tile boundary.

# Tick actions

Every organism is evaluated once per simulation tick and may perform one or more actions. Aging, baseline energy expenditure, metabolism attempts, and position updates occur on every tick. Other actions, including resource absorption, reproduction, predation, scavenging, behavioral changes, and death, are probabilistic and/or gated by the organism's DNA, health, reserves, lifecycle phase, environment, and proximity to valid targets.

Each tick updates position from velocity, and behavior may change that velocity for subsequent movement. Metabolism is also probabilistic: favorable conditions and sufficient external substrates increase the chance or yield of converting resources into stored usable organic compounds and energy. These processes use the world's seeded randomness so identical inputs can replay deterministically.

# Capabilities

An organism's DNA encodes a variety of trait families that can be explored and modified separately, subject to their cross-family prerequisites, incompatibilities, effects, and costs.

The canonical displayed families are:

| Trait family | Primary responsibility |
| --- | --- |
| Cellular organization and structure | Cell organization, structural scale, membranes or walls, compartments, and eukaryotic organization |
| Environmental tolerance | Moisture, temperature, and chemical soft/hard limits |
| Resource acquisition | Absorption, active uptake, ingestion, and transfer of external matter into internal stores |
| External energy capture | Using light, geothermal conditions, or environmental chemical substrates to create energy-bearing organic reserve |
| Internal metabolism | Mobilizing stored energy, catabolism, respiration, fermentation, biomass assembly, metabolic regulation, and waste routing |
| Energy storage | Maximum reserve capacity and the structures used to hold chemical-energy-bearing organic matter |
| Nutrient storage | Available-store capacities, retention, and stockpiling of non-reserve nutrients and micronutrients |
| Growth and lifecycle | Mature structure targets, lifecycle phases, dormancy, senescence, and growth scheduling |
| Reproduction | Split/budding model, allocation, health gate, cooldown/jitter, and overhead |
| Locomotion | Movement mechanisms, speed, environmental compatibility, and energy cost |
| Sensing | Which local organisms, remains, tile conditions, and future resource gradients can be detected |
| Behavioral regulation | How sensed information and internal state select goals and actions |
| Predation and scavenging | Target eligibility, capture or kill mechanisms, consumption, and remnant access |
| Defense | Resistance, avoidance, or structural protection against predation and biological attack |
| Genetic and evolutionary machinery | DNA exchange, abstract sexual reproduction, mutation-income modifiers, and speciation capacity |

Families are presentation and validation boundaries, not isolated subsystems. For example, predation may require locomotion, sensing, a capture mechanism, suitable ingestion, and internal catabolism; oxygen respiration may require oxygen uptake and tolerance; and compartmentalized energy storage may require advanced cellular organization.

Waste handling, nutrient retention, digestion, and morphology do not require separate top-level families in v1. They are compiled effects of resource acquisition, internal metabolism, cellular structure, predation, or lifecycle traits. Communication, symbiosis, colonial organization, parasitism, disease, and multicellularity are credible future families or branches, but are not required for the initial single-celled simulation.

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

## Chemical tolerance

Environmental compounds may be useful substrates for one metabolism while remaining stressful or toxic to organisms without the appropriate tolerance. Volcanic hydrogen sulfide and sulfur dioxide are important early examples. Exposure above a soft threshold increases energy expenditure; exposure beyond a hard threshold creates an increasingly high chance of death on each tick. Metabolic access to a sulfur compound may provide some tolerance where appropriate, but substrate use and toxin resistance remain separately configurable DNA effects.

## Cellular organization and structure

This family owns the organism's structural organization, including membranes or walls, its DNA-defined viable and mature structural-biomass targets, and any compartments that enable advanced traits. It is also the natural home for organism scale: larger or more elaborate cells can support greater storage, ingestion, defense, and metabolic throughput, but require more matter to reproduce and more energy to maintain.

Evolving separate compartments for storing DNA and other organelles supports more complex metabolic pathways and many advanced capabilities. Particular advanced traits may use eukaryotic organization as a prerequisite where that relationship serves the biological abstraction and gameplay. Eukaryotic organization is therefore an important node in this broader family rather than a complete family by itself.

## Predation and scavenging

Organisms can evolve the ability to scavenge off the remnants of dead organisms, or to actively kill and consume other organisms. The predator or scavenger must be within the appropriate interaction range of its target.

Predation traits may independently improve the chance of a successful lethal capture or the organism's relative feeding priority when multiple predators successfully contest the same prey. Capture success is opposed by the prey's defensive adaptations and current condition. Feeding priority affects only the division of an already-killed prey's eligible contents; it does not bypass a failed attack, ingestion limits, storage capacity, or matter conservation. Both offensive improvements carry corresponding energy, structural, or metabolic costs.

## Defense

Predation requires an opposing evolutionary pressure. Defense traits may reduce capture probability, increase the energy or capability needed to kill an organism, or provide structural or chemical deterrence. Evasion through sensing, behavior, or locomotion remains an interaction among those families rather than being duplicated as a defense trait. Defensive structures require matter and commonly increase maintenance or reproductive cost.

## Genetic and evolutionary machinery

Simple organisms only reproduce through asexual reproduction, but even single-celled organisms may evolve abstract DNA-exchange capabilities inspired by processes such as conjugation. In the initial simulation, we do not simulate the transfer or recombination of individual genomes. Instead, DNA exchange increases the species' mutation-point generation or speciation capacity and enables larger changes during a later speciation event. Sexual reproduction is treated through the same species-level abstraction.

DNA exchange and sexual reproduction operate only within a species. A newly founded species is immediately incompatible with its ancestor and every other species. Because sexual reproduction is abstract in the initial simulation, it does not require two organisms to be near one another.

## Resource acquisition

Resource acquisition controls how matter crosses from the environment, a remnant, or consumed prey into the organism's available stores. Primitive passive absorption may accept only a narrow set of bioavailable resources. Active transport, broader organic uptake, ingestion, and digestion can expand accessible substrates but impose maintenance, handling, or micronutrient costs.

Acquisition does not itself create energy. It transfers matter into an internal account. An organism also needs an external energy-capture or internal catabolic pathway that can transform the acquired material into `ReserveOrganic`, structural biomass, or another usable form. Predation and scavenging determine access to a target; resource-acquisition and internal-metabolism capabilities determine what can actually be taken and processed.

## External energy capture

External energy capture controls how an organism uses light, geothermal opportunities, or environmental chemical substrates to create internally stored chemical energy and usable organic compounds. Pathways can evolve different reaction inputs, efficiencies, environmental envelopes, throughput, regulation, and micronutrient requirements. On each tick, an organism may attempt its available capture reactions. Success and yield may be probabilistic based on environmental favorability, substrate availability, DNA, organism state, and remaining storage capacity.

Basic capture pathways are anaerobic and rely on specific conditions, such as thermal gradients and compounds released by volcanic activity, allowing chemotrophy to be viable. The founding hydrogen-acetogenesis and sulfide-anoxygenic-phototrophy choices are external energy-capture pathways.

Anoxygenic photosynthesis provides a limited option for using sunlight with sulfur compounds. Oxygenic photosynthesis opens dramatic new opportunities by allowing cells to harness sunlight using carbon dioxide and water, producing free oxygen as a waste product.

## Internal metabolism

Internal metabolism controls transformations after matter or chemical energy is inside the organism. It includes reserve mobilization, baseline maintenance, biomass assembly, internal nutrient transformations, regulation among available pathways, and the routing or retention of spent products and waste.

Every founder has a basal internal metabolism capable of consuming `ReserveOrganic` to pay maintenance and reproduction overhead and of combining reserve and required nutrients into structural biomass. Stored reserve is matter with chemical energy, not a second abstract energy-point pool. When the energy is spent, it dissipates while the carrier's constituent matter remains in internal stores or follows an explicit waste reaction.

Fermentation is an internal catabolic path that extracts chemical energy from acquired organic matter without oxygen. It becomes useful only when the organism can acquire suitable organic substrates and the environment supplies them. Respiration is another internal catabolic path: once free oxygen is available and the organism has appropriate uptake and tolerance, it can extract substantially more useful energy from organic matter. Neither pathway occupies a predetermined place in the world's chronology; prerequisites and environmental opportunity determine when it becomes viable.

Internal-metabolism traits may change enabled reactions, reaction throughput, mobilization rate, maintenance efficiency, anabolic throughput, regulation, and waste products. Lower energy expenditure never deletes matter: it changes how much reserve is consumed for useful work or how much input energy dissipates.

Some metabolic processes temporarily bind or “tap” internal macronutrients without consuming them, making that material unavailable to other processes for a defined time. By default, free internal resources are shared among eligible metabolic processes. Evolved metabolic regulation may instead give processes different relative priorities or protect a bounded quantity for favored processes in priority order. These choices are encoded in species DNA, carry corresponding regulatory costs, and never duplicate the protected or bound matter.

## Energy storage

All v1 organisms store chemical energy as `ReserveOrganic` in an `EnergyReserve` compartment. The founding `PrimitiveOrganicReserve` mechanism has the existing baseline capacity of 10,000 reserve units. This is an abstract representation of intracellular organic reserves rather than a claim that every organism stores one exact compound.

Energy-storage evolution has three conceptual axes:

- **Capacity:** incremental `ReserveCapacity` traits raise the maximum quantity that may be stored.
- **Organization:** a later `CompartmentalizedReserve` trait provides a larger step increase and may serve as a prerequisite for still larger storage, but requires suitable cellular organization.
- **Handling:** synthesis, retention, and mobilization efficiency may be improved through cross-family internal-metabolism traits rather than creating free stored energy.

Greater capacity does not add reserve matter when a species mutates. Founding members of the descendant retain their existing reserve quantity, so their reserve fraction—and therefore their initial health contribution from energy—may fall until the new capacity is filled. Storage structures may also increase mature structural requirements, maintenance cost, or the resources that must be transferred during reproduction.

V1 does not require multiple energy-bearing reserve compounds. A future dense reserve chemistry, such as a lipid-inspired store, would require its own composition, energy density, synthesis and mobilization reactions, and resource-ledger validation rather than being represented as a capacity multiplier on `ReserveOrganic`.

## Nutrient storage

Nutrient storage remains distinct from energy capacity even though energy-bearing reserves contain matter. This family controls capacities in the organism's `AvailableStore`, including stockpiling of structural macronutrients and micronutrients for later growth, reproduction, metabolic quotas, or migration through a resource-poor environment.

Storage may be defined by resource or by tags such as macronutrient, micronutrient, or digestible organic matter. Retention and waste-routing behavior can depend on internal-metabolism and behavioral-regulation traits, but the capacity belongs here. As with energy storage, greater capacity is initially empty and may require additional structure or maintenance.

## Behavioral regulation

Sensing determines what information an organism can observe; behavioral regulation determines what it does with that information. Regulation traits can add behaviors, improve switching among goals, set resource-conservation or escape responses, and coordinate locomotion, feeding, reproduction, dormancy, and metabolic suppression. Metabolic pathway switching remains an internal-metabolism effect even when behavioral state helps select it.

# Lifecycle

We attempt to model the full lifecycle of an organism. In the simplest form, organisms begin as fully mature organisms after splitting or budding from their parent. They may later die, leaving behind a cohesive remnant at their final location that can be consumed wholly or partially by nearby organisms and that eventually breaks down into organic tile resources.

Scavenging is limited by handling and processing time as well as proximity and storage. Even simple scavengers cannot repeatedly strip the same remains without delay; more complex particulate matter additionally requires ingestion capacity and digestion.

## Reproduction

Reproduction is a net-zero transfer of material resources from a parent into a new organism. The offspring's stored nutrients and initial chemical energy must come from the parent; reproduction cannot create either. Performing the split also consumes additional stored energy, while the nutrients associated with that expended energy remain accounted for.

DNA defines the reproductive allocation model. In a true split, the original and new organism divide the parent's remaining reserves relatively evenly. In a budding or offspring model, the new organism receives a smaller share and the parent retains most of its reserves. DNA may further modify the minimum relative health required for reproduction, its base cooldown and bounded cooldown-jitter window, allocation profile, and its additional energy cost. Once the gates and scheduled cooldown are satisfied, reproduction occurs deterministically; there is no independent per-tick reproduction-success roll. V1 uses a small number of discrete allocation profiles. A future version may support a broader spectrum of DNA-defined allocation between parent and offspring, with corresponding effects on reproduction cost, maturity, and survival. The exact gates, cooldowns, jitter windows, and costs belong in the balance plan.

Sexual reproduction remains an abstract species-level evolutionary capability in the initial simulation. It does not require a nearby mate, mate-seeking behavior, or recombination of individual genomes in order for an organism to reproduce.

More advanced organisms may have additional lifecycle phases, such as a dormant or "seed" phase that is relatively metabolically inert but has increased tolerance for environmental parameters outside the norm. An organism might also have an adolescent phase geared toward mobility and finding an optimal environment before settling down for reproduction.

Age may gradually reduce an organism's metabolic throughput before senescence kills it. The initial simulation models this as fewer completed balanced metabolic processes rather than silently changing reaction yields. Young offspring therefore restore full per-organism productivity, providing another species-level benefit to successful reproduction. Explicit age-driven maintenance escalation, accumulated cellular damage, repair, and damage inheritance are possible future extensions.

Lifecycle state is initially evaluated once per one-hour simulation tick, with the tick duration remaining configurable for tuning. Many organisms may complete their entire lifecycle within a day, week, or month. Seasonal survival is therefore primarily an emergent property of the species population rather than the longevity of a particular organism.

## Death conditions

An organism may die through several distinct mechanisms:

- Senescence as it reaches or exceeds the lifespan allowed by its DNA and lifecycle.
- Starvation when its stored energy falls below the minimum reserve required to sustain life.
- Environmental stress. Conditions outside the organism's preferred range but inside its hard tolerance limits impose additional energy costs. Conditions beyond a hard limit create a chance of death each simulation turn, with the probability increasing as the condition moves farther beyond the limit.
- Predation when another organism successfully kills and consumes it.

The simulation should record the per-tick chance from every positive-probability death cause actually evaluated before the organism died, along with the underlying inputs and relevant contributing stresses. It separately records every triggered draw or deterministic cause. A death can therefore show both multiple simultaneous triggers and substantial risks that did not realize, without misrepresenting the latter as causes.

# Scope and Future Direction

We currently only plan to model single-celled organisms, but want to leave open the possibility of tracking the evolution of multicellular life.

The biological model is intentionally biology-inspired rather than a claim of exact historical reconstruction. Simplified trait progressions and metabolisms should still preserve internal mass balance and reflect meaningful biological pressures and tradeoffs.

A future version may introduce heritable variation between individuals of the same species. This would allow organisms or subpopulations to experiment with new traits before full speciation, with those variants potentially succeeding, disappearing, recombining, or becoming the basis of a new species. This is outside the initial simulation; adding it would require explicit models for individual genomes, inheritance, selection, and the boundary between variation and speciation.

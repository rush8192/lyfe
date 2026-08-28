The main interface has two components: the World and its Tiles, and the Species with its Organisms.

# World and Tiles

In the default view, we want a tile's appearance to correspond to its underlying attributes, giving obvious clues about key properties such as elevation or water depth, aquatic/terrestrial status, surface moisture, temperature, and volcanism. We also want to support additional layers that present these attributes in isolation. Precipitation and surface moisture should be separate layers because current moisture reflects the effects of precipitation over time rather than the amount of precipitation currently falling.

You should be able to zoom in to within a single tile, or zoom out far enough to see the entire World. When viewing a tile closely, organisms and dead remains should appear at their actual within-tile coordinates so that nearby interactions and movement toward tile boundaries are understandable. We should strive to render organisms one-to-one: every displayed organism corresponds to one stable simulation entity, and simulated organisms should be shown individually whenever the current scale permits it. At distant zoom levels or extreme densities, explicitly labeled aggregates or density views are acceptable, but decorative organisms must not be invented, duplicated, or mistaken for simulated individuals. A tile should be selectable, allowing you to distinguish its fixed geography, climate and geological baselines, and current conditions. For example, the interface should show both the current temperature and the normal temperature range for the current time of year.

# Starting a world

The default single-player setup should guide the player through choosing from a narrow pool of primitive DNA capabilities and selecting one of the volcanic ocean tiles eligible for the resulting DNA. The pool offers no more than one or two primitive metabolisms, likely emphasizing either hydrogen gas or sulfur compounds, along with a few efficiency-versus-environmental-tolerance choices. The player should also be able to supply a world seed or use a generated seed. The tile-selection view should hide or disable ineligible locations and provide enough environmental information to compare the eligible choices without requiring expert biological knowledge.

After setup, a short storybook introduction should use repeated lightning over the primitive ocean to convey the role of energy, chemistry, time, and chance in life's uncertain beginnings. The transition ends with the single founding species appearing in the selected tile. The tone may be whimsical, but the presentation should not imply that one literal lightning strike is a settled explanation for abiogenesis.

Competitive multiplayer is planned after v1. Its provisional setup should let players take turns selecting eligible starting tiles and making basic choices from the permitted founding-DNA pool. Earlier selections should be visible to later players. This setup phase is turn-based; the simulation that follows is shared and continuous. Turn-order, reservation, and balancing details remain future design work.

# Game controls

The interface should display the current simulation date and hour, the world's final date and remaining time, and pause and simulation-speed controls for single-player worlds. Speed controls change how quickly complete simulation ticks are processed; they must not imply that the player can jump to a future date or skip intervening activity. At the fastest speed, a thriving species should reach meaningful mutation decisions every few real-world minutes. Future competitive pause and speed controls depend on a shared-clock policy that remains to be defined.

The player should be able to follow a randomly selected member of their controlled species. Following is a camera action and does not allow the player to direct the organism. In free sandbox, the interface should also allow the player to take control of any living species and lock or unlock a species against mutation and speciation. Controller and lock status should be visible wherever a species is selected.

# Species and Organism

You should be able to select an organism and see information about its current status, including its relative health, stored energy, maximum energy-storage capacity, stored nutrients, age, lifecycle phase, current behavior, velocity, position, tile, environmental stresses, and recent actions. The interface should explain that energy reserves are the primary input to health while internal state and external conditions may modify the final score. The organism view should also include a detailed panel about its species, highlighting the species' DNA and resulting capabilities and attributes.

We also want to display the total living population and average relative health of the species, both in the currently selected tile and in the world as a whole. It should be fairly intuitive to understand which environments the species is succeeding in and where it is struggling. The interface should show the frequency and co-occurrence of all recorded death triggers and contributing environmental stresses, distinguishing senescence, starvation, environmental exposure, and predation without forcing a multi-cause death into one category. We should also attempt to provide an estimate of the species' local reproductive constant—that is, whether its population is growing or shrinking in the current tile and overall.

Tile inspection must include historical views of key resource quantities and flows. The player should be able to select a time window and understand important sources, sinks, neighbor exchanges, transformations, biological consumption, and biological release rather than seeing only the current total. Atmospheric-gas history should use the same model, making local production, rapid mixing, and attrition visible where relevant.

The species panel should explain mutation-point income as a breakdown of the three governing inputs: total population, average relative health, and modifiers from DNA capabilities. It should show the current generation rate and available balance without requiring the player to infer them from population changes.

# Tree of Life

The interface should visualize the world's complete tree of life, including living and extinct species. Each speciation edge should make the ancestor-descendant relationship clear and allow the player to inspect when and where the new species was founded and which DNA changes distinguish it from its ancestor. Species nodes should show their living or extinct state, current controller, sandbox mutation lock, and available mutation-point balance.

The tree is also a navigation surface. Free sandbox allows the player to take control of any living species selected from it. Survival and future competitive modes visually identify the lineage to which the player is bound. V1 survival offers no backtracking controls; any later recovery mechanic should use the tree to show eligible existing branches.

The default view should be a read-only display that highlights the useful information, but we also want an 'evolution' view that lets you spend mutation points to alter one or more facets of the DNA to achieve new species capabilities. DNA should be presented as separate trait families, with the evolution of each family following either a linear or tree-like structure. It should be easier to move to adjacent points than to make a large jump along an attribute such as temperature tolerance.

The evolution view must also make relationships between trait families visible. It should show prerequisites, incompatibilities, and both the direct and cross-family consequences of a proposed change. Before the player commits a speciation event, the interface should explain its mutation-point price, ongoing metabolic and reproductive costs, and any important changes it causes to capabilities in other trait families.

The evolution view should communicate that a committed speciation event is irreversible. If a mode-specific backtracking option is added after v1, it should be presented as movement through the existing tree of life, not as a refund or reversal of DNA changes.

When preparing a speciation event, the evolution view should allow the player to select one to four occupied tiles and preview the founding fraction applied in each tile: 50% for one tile, 20% for two, 8% for three, or 3% for four. It should also show the mutation points spent and the unused balance that both the ancestor and descendant species will retain.

Reproduction traits should explain whether the organism uses true splitting or budding, how reserves are allocated between parent and offspring, the minimum health requirement, base attempt frequency, and additional energy cost. The interface should make clear that offspring resources come from the parent rather than being created by reproduction.

## Artistic style

We want our tiles and organisms to use a storybook style: somewhat whimsical and abstract rather than hyper-realistic and detailed. Organisms of a species should share a similar appearance but have mild cosmetic variation. In the initial simulation, this variation does not represent genetic or gameplay differences between members of the species.

Changes to DNA should manifest in an organism's appearance. Some capabilities will be standard and obvious—for example, whether it has a flagellum or cilia for locomotion, or whether a photosynthetic organism is some shade of green—but other changes can be more abstract. Different species should generally be colorful and visually distinct.

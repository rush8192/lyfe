The main interface has two components: the World and its Tiles, and the Species with its Organisms

# World and Tiles

In the default view, we want the display of the tile to correspond to the underlying attributes of the tile, i.e. the tile should give some obvious clues as to the key attributes (elevation, aquatic/terrestrial, moisture level, temperature, volcanism, etc). We also want to support additional 'layers' to the view, which present the different key attributes in isolation for each tile

You should be able to zoom in to within a single tile, or zoom out far enough to see the entire World. A tile should be selectable, allowing you to view all of its current attributes (as well as the underlying parameters that inform those attributes, like temperature range)

# Species and Organism

You should be able to select an organism, and see information about the status of that particular organism (i.e. its health, age, etc), as well as a detailed panel about the status of that organism's species. This panel should highlight the DNA of the species and its resulting capabilities and attributes.

We also want to display information about the overall size and health of the species, both in the currently selected tile, and in the world as a whole. It should be fairly intuitive to understand which environments the species is succeeding in, and where it is struggling; it should also make clear the cause of the struggle, i.e. the most common causes of death for an organism. We should also attempt to provide an estimate of the local reproductive constant of the organism (i.e. is it growing in number or shrinking in the current tile and overall)

The default view should be a read-only display that highlights the useful information, but we also want an 'evolution' view that lets you spend mutation points to alter one or more facets of the DNA to achieve new species capabilities. The evolution of each facet or capability of DNA should follow either a linear or tree-like structure - it should be easier to move to adjacent points, versus making a large jump along an attribute (like temperature tolerance)

## Artistic style

We want our tiles and organisms to use a 'storybook' style, i.e. somewhat whimsical and a bit abstract, instead of trying to be hyper-realistic and detailed. Organisms of a species should share a similar appearance, but should have some mild variation.

Changes to the DNA should manifest in changes to the appearance of an organism. Some capabilities will be fairly standard and obvious (e.g. does it have a flagella or cillia for locomotion; photosynthetic organisms should be some shade of green) but other changes can be more abstract. The different species should generally be colorful and differentiated in appearance. 
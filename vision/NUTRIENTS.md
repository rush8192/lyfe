Nutrients are the material resources that organisms need to gather from their environment to sustain life, store chemical energy, move, reproduce, and evolve. They include the macronutrients used widely throughout living matter—carbon, hydrogen, nitrogen, oxygen, phosphorus, and sulfur—as well as micronutrients required in smaller quantities.

LYFE models a resource-constrained world rather than a perfectly closed system. Our biological abstraction is biology-inspired and internally mass-balanced: nutrients move between environmental reservoirs, living organisms, and dead organic matter, and remain accounted for except through explicit sources and sinks. Nutrients held by living organisms are generally unavailable to competitors until they are released through death, predation, waste, or another defined process. Geological, atmospheric, and other processes may act as explicit sources or sinks for the portion of the world represented by the simulation.

Ocean water and terrestrial surface moisture are effectively inexhaustible background resources at the scale of the simulation. Organisms do not measurably deplete them, although extracting useful elements or energy from water may require specialized metabolism. This exception does not permit other tracked nutrient matter to appear without an accounted source.

# Resource forms

Each macronutrient can exist in an organic or inorganic form. Organic forms are generally easier for organisms to consume and process. Inorganic forms require the appropriate metabolic capabilities, such as carbon or nitrogen fixation, before organisms can incorporate them into organic matter. We do not need to model the exact compounds involved in every process; these two forms capture the biologically important distinction while keeping the nutrient model tractable.

Metabolism, decomposition, and inorganic processes may transform macronutrients between their organic and inorganic forms. Unless an explicit source or sink is involved, these transformations preserve the total quantity of the underlying nutrient.

# Energy

Energy is distinct from nutrient matter and does not cycle through the world in the same way. Energy enters the living system from sources such as sunlight, geothermal activity, and usable chemical gradients, and ultimately dissipates. Organisms convert available energy into internally stored chemical energy, generally in the form of organic compounds. Those compounds can later be consumed through metabolism to power maintenance, movement, reproduction, and other capabilities. When their stored energy is used, the nutrients contained in the compounds remain accounted for and return to an appropriate nutrient reservoir.

Every organism has a DNA-defined maximum energy-storage capacity. The capacity begins with a relatively low ceiling and can increase incrementally through storage traits or in larger steps through new metabolic and cellular capabilities. Stored energy relative to this capacity is the primary input to organism health, while stored nutrients, lifecycle state, and environmental stress may adjust the final relative score. Additional capacity therefore provides resilience but does not itself supply energy.

DNA distinguishes resource acquisition, external energy capture, energy storage, nutrient storage, and internal metabolism. Acquisition moves matter into the organism. External capture uses environmental substrates or opportunities to create energy-bearing organic reserve. Energy and nutrient storage independently determine how much each internal compartment can hold. Internal metabolism converts acquired matter, mobilizes reserve to power maintenance and actions, assembles biomass, and routes spent products. Increasing capacity does not fill it, and improving any stage cannot bypass the mass and energy accounting of the reactions it enables.

Reproduction redistributes the parent's stored nutrients and chemical energy into the resulting organisms and never creates matter or stored energy. Any additional reproductive work consumes stored energy, which ultimately dissipates, while the nutrients associated with that energy remain accounted for.

Organisms attempt resource absorption and metabolic transformations on simulation ticks. Success and yield may be probabilistic based on environmental conditions, substrate availability, organism state, and DNA capabilities. The exact resource pools, reactions, and competition rules remain deferred, but every successful transfer or transformation must preserve the internal mass balance described above.

# Macronutrients

The "big six," CHNOPS, make up 97 percent of living matter:

## Carbon

Found in the air as CO2, carbon must be fixed into a usable form by an organism's metabolism. Dying organisms may release fixed carbon back into the environment in organic compounds that organisms can consume directly; predatory organisms may take a more direct route to acquiring these resources. Carbon may also enter the biologically modeled reservoirs in methane gas (CH4) from geological or biological sources.

## Hydrogen

Hydrogen is released from volcanic sources as H2 and H2S, which can fuel early metabolisms. It also occurs in ammonia (NH3), is plentiful but difficult to access directly in water (H2O), and appears in methane (CH4) from biological or geological sources.

One v1 founding path uses hydrogen acetogenesis as an abstraction for converting H2 and CO2 into stored organic matter and energy. It is deliberately less productive in the opening than the sulfide alternative, but it functions without light and has a shorter evolutionary bridge through organic uptake and fermentation toward a less volcanism-dependent metabolism.

## Oxygen

Although initially absent as free oxygen (O2), oxygenic photosynthesis can release it as a waste product. This process uses carbon dioxide as a carbon source but derives the released oxygen principally from water.

When free oxygen is present, it can be used by organisms that use respiration for their metabolism.

## Nitrogen

Atmospheric nitrogen (N2) is plentiful but inert, so other sources were required for early life. Some non-biological sources of available nitrogen exist as ammonia (NH3) or nitrates (NO3), but mass production of fixed nitrogen is only possible once organisms evolve that capability.

## Phosphorus

Bioavailable phosphorus was fairly rare in the early Earth environment but was gradually released or mobilized through the weathering of volcanic igneous rocks and more sporadically by lightning strikes. Bioavailable forms of phosphorus are commonly found as phosphates (PO4).

## Sulfur

Volcanic vents producing hydrogen sulfide (H2S) and sulfur dioxide (SO2) provided an important fuel for early metabolisms. Once oxygen became more plentiful, many sulfur-based metabolisms became increasingly confined to specialized ecological niches.

The other v1 founding path uses H2S, CO2, and light in an anoxygenic-phototrophy abstraction. It receives greater opening productivity and stronger sulfide tolerance in its ideal shallow volcanic niche, but depends on the coincidence of useful light and sulfide. Reaching fermentation requires additional regulatory and generalized-catabolism traits, while the distinct oxygenic-photosynthesis path is a substantially larger leap gated by further traits and micronutrients.

# Micronutrients

While not as plentiful as the macronutrients, various other elements are used by different metabolisms and are required for organisms to live and thrive. Each micronutrient is modeled as a single bioavailable resource rather than being divided into organic and inorganic forms:

- Calcium
- Iron
- Potassium
- Sodium
- Magnesium
- Zinc
- Copper
- Iodine
- Fluoride
- Selenium
- Manganese
- Molybdenum
- Nickel
- Cobalt

Micronutrient requirements may gate particular metabolic and cellular capabilities. For the initial abstraction, manganese and calcium can support the canonical oxygenic-photosynthesis path, molybdenum and iron can support canonical nitrogen fixation, and nickel, cobalt, and iron can support primitive hydrogen-processing and acetogenesis-inspired pathways. These are gameplay-relevant canonical paths rather than a claim that biology has no alternative cofactors.

To start, micronutrient quantities are well-mixed within each tile. As a future extension, individual micronutrients may be represented as localized concentration fields within a tile. Organisms would consume and release them at specific coordinates, allowing diffusion and local activity to form gradients that organisms could evolve to sense and follow.

# Transformations, sources, and sinks

Processes described as nutrient decay must either transform a nutrient from one modeled form into another or remove it through an explicit sink. For example, radiation-induced chemical breakdown may change an organic compound into an inorganic form, while atmospheric loss into space removes matter from the simulated world. Geological activity and weathering may serve as explicit sources that introduce nutrients from outside the biologically modeled reservoirs.

A small number of named atmospheric gases may be represented as tile-local reservoirs when their distinct transport, stability, or metabolic role matters. Stable gases mix rapidly through neighbor exchange, while reactive or short-lived gases may undergo rapid transformation or attrition through an explicit sink and remain locally concentrated. These gas reservoirs must reconcile with the underlying macronutrient accounting rather than duplicating matter.

Dead remains are separate entities rather than immediately becoming a tile-wide resource pool. They retain their remaining nutrients and stored chemical energy, may be consumed wholly or partially by nearby organisms, and transfer their unconsumed matter into tile-wide organic resource pools as they decay.

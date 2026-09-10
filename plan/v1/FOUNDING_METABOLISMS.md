# Founding Metabolisms and Survival Origins

Status: founding identities, asymmetry, reactions, explicit light control, generated-world light/depth integration, and H₂S/SO₂ health/death effects are executable; optional-package tuning remains

Sources: [GAMEPLAY vision](../../vision/GAMEPLAY.md), [ORGANISMS vision](../../vision/ORGANISMS.md), [WORLD vision](../../vision/WORLD.md), [resource model](RESOURCE_MODEL.md), and [one-tile hydrogen fixture](ONE_TILE_STARTING_CONFIGURATION.md).

# Purpose

Turn the two founding metabolism choices into a consequential specialist-versus-platform decision and define how they seed a v1 survival world. This document fixes the shape of the tradeoff and the initial trait paths. The named path prices are first evolution-economy anchors; exact biological rates and catalogue-wide prices remain balance data to validate with deterministic fixtures.

# Founding choice

V1 uses exactly two founding metabolisms:

| Property | Sulfide anoxygenic phototrophy | Hydrogen acetogenesis |
| --- | --- | --- |
| Simplified inputs | H₂S, CO₂, and light | H₂ and CO₂ |
| Opening identity | Fast volcanic specialist | Slower metabolic platform |
| Ideal habitat | Shallow, illuminated, sulfide-rich volcanic ocean | Warm, hydrogen-rich volcanic ocean |
| Opening advantage | Greater expected net reserve and biomass growth; inherent sulfide tolerance | Operates through day and night; cheaper route to consuming organic matter |
| Opening liability | Requires coincident light and sulfide and performs poorly at depth, at night, or away from volcanic sulfide | Lower expected output and must pay for enough sulfur tolerance to survive its starting environment |
| Route away from volcanism | Expensive regulatory and catabolic bridge, or a much larger oxygenic-photosynthesis leap | Organic uptake followed by fermentation and facultative heterotrophy |

“More universally suitable” means less dependent on a local volcanic input. It does not mean that any metabolism is effective in every temperature, chemistry, moisture, or nutrient regime.

The sulfur path's higher opening efficiency is an explicit game-balance choice, not a claim that one real metabolism has a universal efficiency advantage. Environment, substrate contention, maintenance, stress, and resource quotas determine realized performance.

An explicit candidate audit found photoferrotrophy to be the strongest future third founder, but not a v1 addition because it requires bulk iron redox and mineral-deposition systems. Methanogenesis is historically plausible but currently overlaps the H2-and-CO2 niche of acetogenesis too closely to provide an equally distinct third setup card. The comparison and future candidates are recorded in [ALTERNATIVE_FOUNDING_METABOLISMS.md](ALTERNATIVE_FOUNDING_METABOLISMS.md).

Exactly two is a v1 scenario constraint rather than an engine constraint. Setup, world eligibility, protocol values, and compiled metabolism definitions must use stable data-defined IDs rather than assuming a permanent two-value list.

The two founding capture traits are not biologically incompatible. A descendant may eventually acquire the other reaction, but setup is the only context that grants one without paying its ordinary mutation price. Cross-pathway acquisition starts as a major-cost, high-change-complexity proposal, provisionally at least comparable to the 120–220 MP escape paths. Operating both constitutive pathways also retains both upkeep profiles unless `MetabolicRegulation` can suppress the inactive machinery. Exact root prices and any additional tolerance burden remain fixture-driven balance values in [TRAIT_CATALOGUE.md](TRAIT_CATALOGUE.md).

# Reference reactions

The resource ledger uses the already balanced abstractions:

```text
Sulfide anoxygenic phototrophy:
2 H2S + CO2 + light
    -> ReserveOrganic(CH2O) + H2O(boundary) + 2 InorganicSulfur

Hydrogen acetogenesis:
4 H2 + 2 CO2
    -> 2 ReserveOrganic(CH2O) + 2 H2O(boundary)
```

Light is an energy opportunity rather than matter. Reaction definitions must separately declare stored and dissipated energy. Neither recipe supplies structural nitrogen, phosphorus, or micronutrients; growth and reproduction still require complete biomass and catalytic quotas.

# Opening balance targets

Under each metabolism's own favorable starting conditions:

- Sulfide founders should reach their first viable reproduction after 250 ticks, or 10 days 10 hours, in the deterministic square fixture. The first generated-world reference reaches tick `263`, with `250..275` as the candidate acceptance band.
- Hydrogen founders should reach it in approximately 14 simulated days, matching the current bounded hydrogen fixture.
- Sulfide founders should reach that first split approximately 25% sooner through a 33% higher opening structural-growth rate, not through a larger founding endowment or different reproduction rule.
- Sulfide output should fall sharply when either useful light or H₂S is scarce. It should have a day/night and depth-sensitive profile.
- Hydrogen output should be lower but steadier because the founding reaction does not require light.
- Both populations must eventually encounter substrate, fixed-nitrogen, micronutrient, or habitat pressure rather than growing without bound.

The sulfur population's faster early growth can produce mutation points faster once it reproduces, but its lower substrate ceiling and more expensive escape path preserve a substantial long-term cost. Exact mutation timing remains dependent on the mutation-income formula and the population/health trajectories after speciation.

# Initial trait paths

The first trait graph should contain the following conceptual paths. Names and prices are stable planning identifiers until the configuration schema assigns canonical IDs.

```text
HydrogenAcetogenesis
└── OrganicResourceUptake                 40 MP
    └── Fermentation                       80 MP
        └── FacultativeHeterotrophy
            └── LaterRespirationPaths
```

The first volcanism-independent milestone for the hydrogen lineage therefore has a provisional cumulative price of `120 MP`. Organic uptake and fermentation become useful only where dead remains and other biological turnover produce the named labile substrate. Their exact decay source, reaction, cost, and first balance fixture are defined in [ORGANIC_UPTAKE_AND_FERMENTATION.md](ORGANIC_UPTAKE_AND_FERMENTATION.md).

This path intentionally crosses trait families: `OrganicResourceUptake` belongs to resource acquisition, while `Fermentation` belongs to internal metabolism. The founding `HydrogenAcetogenesis` reaction remains external energy capture. Their prerequisite chain is cross-family rather than evidence that all three should be displayed as one metabolism family.

```text
SulfideAnoxygenicPhototrophy
├── ImprovedSulfideCapture
├── SulfurStorage
├── ImprovedSulfideTolerance
│
├── MetabolicRegulation                  60 MP
│   └── GeneralizedOrganicCatabolism     80 MP
│       └── Fermentation                   80 MP
│
└── ComplexPhotosystem                  100 MP
    └── ManganeseCalciumWaterOxidation    150 MP
        └── OxygenToleranceI                   80 MP
            └── enables OxygenicPhotosynthesis reaction
```

The sulfur lineage's first fermentation route has a provisional cumulative price of `220 MP` and uses the same exact reaction after paying its additional regulatory/generalized-catabolism bridge. Its canonical oxygenic-photosynthesis route has a cumulative price of `330 MP` and remains environmentally gated by manganese and calcium availability as well as its trait prerequisites. `OxygenicPhotosynthesis` is the enabled reaction capability at the end of those three priced nodes, not a fourth unpriced trait. Its exact source rate, catalytic quotas, light sharing, upkeep, and oxygen-system calibration are fixed in [OXYGENIC_PHOTOSYNTHESIS.md](OXYGENIC_PHOTOSYNTHESIS.md), paired with the respiratory sink in [AEROBIC_RESPIRATION.md](AEROBIC_RESPIRATION.md).

Evolution remains irreversible. `MetabolicRegulation` does not remove sulfide-phototrophy DNA; it permits an organism to suppress that machinery when conditions do not support it, reducing but not necessarily eliminating its ongoing cost. The oxygenic route is a large step from one sulfur-powered photosystem to water oxidation and must not be represented as an automatic “next” metabolism.

Mutation prices above are normative first anchors for simulation experiments under [EVOLUTION.md](EVOLUTION.md). Only the complete versioned rule-pack values are authoritative for a saved world, and validation may revise the whole pinned rule pack rather than mutate an active save.

# Starting tolerance choices

Both metabolism cards may expose a small number of additional allocations between reaction efficiency and environmental tolerance. These choices must not erase the central identities:

- A sulfur founder always begins sufficiently sulfide-adapted to use its preferred tile and remains the stronger opening specialist there.
- A hydrogen founder always begins with enough volcanic-sulfur tolerance to survive its preferred tile, but that tolerance imposes an ongoing cost and is weaker than the sulfur founder's native adaptation.
- No selectable tolerance package may make an initial organism broadly suited to non-volcanic or advanced habitats.

The first bounded three-package candidate, exact multipliers, persistent representation,
competitor default, and 64-seed acceptance matrix are defined in
[FOUNDER_SETUP_PACKAGES.md](FOUNDER_SETUP_PACKAGES.md).

The setup UI must disclose expected first-reproduction time, light dependency, relevant toxic exposure, major micronutrient gates, and the first path away from volcanic dependence. It should describe the choices as `fast specialist` and `flexible foundation`, not as an unexplained difficulty selector.

# Survival initialization

Survival begins with two independently founded species:

1. The player selects one of the two founding metabolisms and its permitted tolerance/efficiency allocation.
2. The player selects an eligible volcanic ocean tile for that DNA.
3. Deterministic world generation reserves an edge-sharing eligible volcanic ocean tile for the other metabolism. Together they form a paired starting region.
4. The player-controlled species is initialized in the selected tile. An autonomous competitor with the other metabolism is initialized in the paired tile.
5. Both populations receive the same founder count, initial relative reserve, lifecycle phase, zero mutation points, and access to the same simulation rules. Their DNA-dependent resource quotas and environmental effects may differ.
6. Both initialization transactions are mass-balanced and recorded before tick zero.

The two populations do not begin in the same tile. Separate resource reservoirs prevent proportional contention in one well-mixed compartment from deciding the opening before either species can act. Edge adjacency keeps gas exchange, environmental spillover, later migration, scavenging, and competition relevant.

## Founder cohort desynchronization

A playable scenario must not initialize every founder as a coeval organism with the same
reproductive-readiness boundary. Before playable alpha, each scenario defines bounded founder
cohort distributions for biological age, initial reproduction schedule, and any other starting
state that can make the whole cohort cross a reproduction gate at once. This is initialization
spread, not per-tick reproduction randomness and not an undeclared fitness bonus.

Each independently meaningful parameter uses its own registered keyed-random domain, addressed by
the stable setup/origin identity and organism ID as defined by that domain. Parameter kinds must not
share a domain by hiding their meaning in a coordinate or sample index. Repeating a setup command
therefore produces the same organism values, hashes, saves, and future outcomes regardless of
iteration order or worker count. Player and autonomous founder cohorts use the same declared
distributions. The declared bounds must retain the configured mature lifecycle phase, avoid
starting beyond a senescence or viability boundary,
and preserve all resource and matter accounting; varying a material readiness input requires an
equal initialization debit rather than free structure, reserve, or nutrient inventory.

The opening acceptance fixture must show first births distributed across multiple completed ticks
instead of one cohort-wide event. It must also verify that the spread does not materially move the
authored first-reproduction and first-decision pacing bands. Small reaction, ledger, and deterministic
arithmetic fixtures may explicitly select a zero-spread setup profile, but the default playable
Sandbox and Survival scenarios may not. Exact ranges remain a balance decision until the coupled
opening is playtested.

The competitor uses the same mutation income and autonomous-evolution rules as every uncontrolled species and receives no hidden economic or probability bonuses. Its heuristic should tend to deepen specialization while its niche remains productive, and weight regulation, tolerance, or escape traits more heavily when recent deaths and failed metabolism indicate sulfide, light, substrate, or habitat pressure.

Survival control remains bound to the chosen species and the descendant selected at each player-directed speciation. Extinction of the competitor does not end the run. Extinction of the currently controlled species causes the existing v1 survival loss.

# Sandbox initialization

Free sandbox retains one player-chosen founding species by default so it remains a clean experimental start. Scenario configuration may support the paired origin for tests or later sandbox options, but this is not required for the default v1 sandbox flow.

# Origin and lineage representation

The two survival founders are independent roots, not ancestors of one another. The lineage store should represent:

```text
AbiogenesisOriginEvent
├── PlayerFoundingSpecies       parentSpecies = none
└── CompetitorFoundingSpecies   parentSpecies = none
```

`AbiogenesisOriginEvent` is not a species and cannot reproduce, mutate, become extinct, or hold mutation points. It groups the two founding transactions for history and presentation. Every species created after initialization still has exactly one parent species, preserving strict branching below each root. Free sandbox has one species child under the same event type.

# Deterministic setup sketch

```text
InitializeSurvival(command, generatedWorld):
    validate chosen metabolism, allocation, and player tile
    competitorMetabolism = ResolveCompetitorMetabolism(command.scenarioId,
                                                        command.metabolism)
    competitorTile = ResolveReservedPairedTile(command.playerTile,
                                               competitorMetabolism,
                                               setupSeed)
    validate both complete founder transactions
    create AbiogenesisOriginEvent
    initialize player founders atomically in player tile
    initialize competitor founders atomically in competitor tile
    attach both root species to origin event
    assign player control only to player root
    record setup command and resulting IDs for replay
```

The paired tile is resolved from generation output rather than searched nondeterministically at setup time. Repeating the seed and setup command must produce the same tiles, founder positions, species IDs, and future random outcomes.

The v1 scenario's competitor policy maps each founder to the other of its two allowed metabolisms. Keeping that mapping in scenario data permits later three-or-more-founder scenarios to define a deterministic pairing, weighted opponent, or multiple competitors without changing the setup engine.

# Required fixtures and acceptance criteria

- A bounded hydrogen tile reproduces the existing approximately 14-day first-split target.
- A bounded shallow sulfur tile produces an approximately 10–11-day first split under favorable light and H₂S.
- Removing light or H₂S suppresses the sulfur advantage without changing mass accounting.
- Running the sulfur tile with hydrogen DNA exposes the intended sulfur-tolerance cost or mortality.
- The hydrogen-to-fermentation path validates at its lower cumulative mutation price; the sulfur path cannot bypass regulation and generalized catabolism.
- Oxygenic photosynthesis fails trait validation without its prerequisites and population expansion fails where manganese/calcium quotas cannot be reproduced.
- A paired-region fixture initializes equal-sized populations without sharing a starting resource reservoir.
- Default playable setup uses deterministic bounded founder age/readiness spread, produces first births across multiple ticks, and gives both roots the same distribution without unbalanced matter or hidden advantage.
- The coupled authoring fixture reproduces the first-reproduction, mutation-income, first player-speciation, and seven-day branch-viability bands in [SURVIVAL_OPENING_VALIDATION.md](SURVIVAL_OPENING_VALIDATION.md).
- Neither founder receives an implicit matter, energy, mutation-point, or probability advantage outside its declared DNA and environment.
- Repeating the paired setup reproduces initialization and simulation hashes.
- The lineage store contains one origin event, two root species, and no false parent-child edge between the founders.

The calculated square-fixture sulfur values and ledger expectations are defined in [SULFUR_TILE_STARTING_CONFIGURATION.md](SULFUR_TILE_STARTING_CONFIGURATION.md). The corresponding generated-world solar, depth, cloud, and throughput calibration is defined in [WORLD_CLIMATE_CALIBRATION.md](WORLD_CLIMATE_CALIBRATION.md).

# Scientific anchors

- The sulfur accounting fixture uses the elemental-sulfur form of anoxygenic photosynthesis: [Low-Light Anoxygenic Photosynthesis and Fe-S-Biogeochemistry in a Microbial Mat](https://pmc.ncbi.nlm.nih.gov/articles/PMC5934491/).
- Acetogens can support autotrophic and heterotrophic lifestyles through the Wood–Ljungdahl pathway, providing the biological inspiration for the hydrogen path's metabolic flexibility: [Acetogenesis and the Wood-Ljungdahl Pathway of CO2 Fixation](https://pmc.ncbi.nlm.nih.gov/articles/PMC2646786/).
- The transition from anoxygenic to oxygenic photosynthesis is a major evolutionary transition rather than a mandatory immediate successor: [A Physiological Perspective on the Origin and Evolution of Photosynthesis](https://pmc.ncbi.nlm.nih.gov/articles/PMC5972617/).

# Remaining tuning questions

- Representative-seed and biological validation of the first generated-world light curve and its `250..275`-tick sulfur-start band.
- Population validation of the fixed 5-energy/hour regulation cost and 25% retained suppressible pathway upkeep.
- Coupled-world validation that the calibrated labile-organic stock and flow thresholds make the hydrogen fermentation escape path useful only after biological turnover creates its substrate.
- Generated-world population validation of the bounded player-facing efficiency-versus-tolerance packages before their values are frozen into the official rule pack.

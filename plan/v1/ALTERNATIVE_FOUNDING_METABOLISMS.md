# Alternative Founding Metabolism Audit

Status: v1 decision complete; future candidates ranked

Sources: [founding metabolism plan](FOUNDING_METABOLISMS.md), [resource model](RESOURCE_MODEL.md), [gameplay vision](../../vision/GAMEPLAY.md), and [nutrient vision](../../vision/NUTRIENTS.md).

# Decision

V1 retains exactly two founding choices: sulfide anoxygenic phototrophy and hydrogen acetogenesis. No third candidate is simultaneously as credible for the intended primitive world, as mechanically distinct, and as inexpensive to express with the v1 resource model.

This is a rule-pack and scenario decision, not an engine limitation. Metabolisms, eligibility rules, setup cards, and competitor pairings must remain data-driven so that a later scenario can add another founder without changing the authoritative simulation architecture.

The preferred first additional founder is **photoferrotrophy**, an iron-powered form of anoxygenic photosynthesis. It is scientifically plausible and offers a strong third gameplay identity, but it is deferred because representing it honestly requires bulk iron redox reservoirs and mineral products that v1 intentionally does not model.

# Evaluation gates

A founding metabolism is evaluated against four gates:

1. **Primitive-world plausibility:** it can sustain primary production under an anoxic, early-Earth-inspired environment without requiring products of a mature biosphere.
2. **Gameplay identity:** its habitat, resource pressure, environmental effect, and evolutionary route differ meaningfully from both existing choices.
3. **Accounting fit:** it can be internally mass-balanced without silently treating a catalytic micronutrient as an inexhaustible fuel or introducing untracked matter.
4. **Scope fit:** its required world generation, transport, climate, and ecology systems are proportionate to the value of one more setup card.

“Pseudo-historical” does not require LYFE to claim one settled chronology for early metabolism. It means a choice is compatible with credible hypotheses about primitive life and preserves the pressures those hypotheses imply.

# Candidate comparison

| Candidate | Primitive-world fit | Distinct gameplay | Required model expansion | Disposition |
| --- | --- | --- | --- | --- |
| Photoferrotrophy | Strong | Strong | High | Preferred first post-v1 founder |
| Hydrogenotrophic methanogenesis | Strong | Limited in the current rules | Medium | Future hydrogen branch or methane-world scenario |
| Dark sulfur reduction or respiration | Plausible | Moderate | Medium to high | Future deep-vent path or scenario |
| Fermentative heterotrophy | Secondary rather than self-sustaining | Strong | Low | Early evolved trait or finite-pantry challenge, not a default founder |
| Hydrogen-powered phototrophy | Plausible | Weak | Low | Trait branch, not a third setup identity |
| Methanotrophy, nitrification, and oxygen-dependent respiration | Requires a developed chemical ecology | Strong later | Medium | Later ecological niches, not abiogenesis founders |
| Silicon-based or alternate-solvent life | Highly speculative and orthogonal to metabolism | Potentially strong | Very high | Separate future world ruleset only |
| Radiation-driven primary metabolism | Speculative | Moderate | Medium | Exotic scenario research, not a planned founder |

# Preferred future candidate: photoferrotrophy

Photoferrotrophy uses light to fix carbon while oxidizing ferrous iron. It is a credible candidate for productivity in ferruginous Archean oceans and has been proposed as a contributor to banded iron formation. It is not established as the first photosynthesis, so LYFE should present it as a plausible path rather than a canonical historical step.

A future LYFE abstraction could begin from:

```text
FerrousIron + CO2 + light
    -> ReserveOrganic + FerricIronMineral + balanced boundary products
```

The final recipe must be compiled from integer-balanced coefficients; this sketch only names the intended reservoirs.

Its gameplay identity would be a **shallow-water mineral transformer**:

- It works without H2 or H2S but still requires useful light.
- It consumes a geographically distributed, exhaustible mineral substrate rather than a rapidly attriting volcanic gas.
- It progressively depletes local ferrous iron and leaves ferric mineral deposits, visibly changing the resource landscape.
- It could begin between the two v1 founders in opening productivity, with a moderate route toward more advanced photosystems but a persistent dependence on suitable iron chemistry.
- Geological weathering, upwelling, and reducing conditions could renew its substrate, creating different exploration pressures from volcanic gas plumes.

## Why it is not a v1 founder

V1 treats iron as one bioavailable micronutrient used in catalytic and structural quotas. Photoferrotrophy instead consumes iron at substrate scale and depends specifically on oxidation state. Adding it requires at least:

- Distinct bulk `FerrousIron` and `FerricIronMineral` resources that do not duplicate the bioavailable iron-micronutrient account.
- Explicit oxidation, precipitation or settling, weathering, and remobilization fates.
- World-generation distributions for ferruginous water and accessible iron.
- Transport and accessibility rules for dissolved/mobile versus particulate mineral forms.
- Resource visualizations that explain iron depletion and mineral deposition.
- New three-choice setup and competitor-pairing balance; the current paired Survival opening is intentionally symmetric around two complementary founders.

Adding only an `iron metabolism` trait while retaining one inexhaustibly bioavailable iron pool would erase the most interesting ecological consequence and violate the resource model's accounting spirit. The feature should wait for the bulk mineral-redox model.

# Plausible candidates that do not earn a third v1 card

## Hydrogenotrophic methanogenesis

Hydrogenotrophic methanogens use H2 to reduce CO2 and produce methane. This is an ancient and credible primitive metabolism, but its opening requirements substantially overlap hydrogen acetogenesis: both are dark, anaerobic H2-and-CO2 primary producers associated with similar reducing environments.

It becomes mechanically worthwhile when methane has consequences beyond being another ledger output:

- Methane accumulation affects climate or atmospheric chemistry.
- Methane loss competes with global mixing and environmental sinks.
- Later methanotrophs can consume the founder's waste and create a cross-feeding ecology.
- Methanogenesis trades lower biomass yield or different cofactor needs for climate engineering and a distinct evolutionary branch.

Until those systems exist, a methanogen card would mostly divide the hydrogen founder's identity into two recipes competing for the same habitat. The v1 hydrogen-acetogenesis choice should remain the broader H2 platform. Methanogenesis is a good future branch or the centerpiece of a methane-focused scenario.

## Dark sulfur reduction and respiration

Anaerobic sulfur reduction can use H2 as an electron donor and elemental sulfur, sulfite, or related sulfur species as acceptors. It offers a dark deep-vent identity and could form a useful ecological loop by producing sulfide for phototrophs.

It is deferred because a meaningful implementation needs multiple sulfur oxidation states, appropriate electron acceptors, and balanced transformation products. Otherwise it overlaps the hydrogen input of acetogenesis or the sulfur theme of the current phototroph without establishing a sufficiently different resource economy. It is best revisited as a deep-ocean founder, an evolutionary branch, or part of a multi-species sulfur-cycle scenario.

## Fermentation and obligate heterotrophy

Fermentation consumes energy already stored in organic substrates. It can make an interesting scavenger or decomposer, but it does not establish sustained primary production in an otherwise empty world. A founder supplied only by a finite abiogenesis endowment would consume its pantry and collapse unless geological or prebiotic organic inputs were made artificially generous.

Fermentation therefore remains an early evolved escape capability, as already planned for the hydrogen lineage. A future challenge scenario could deliberately begin with a finite organic inventory and ask the player to evolve a primary producer before it runs out, but that is a different setup promise from the default survival origin.

## Biosphere-dependent metabolisms

Methanotrophy, aerobic iron or sulfur oxidation, nitrification, and many respiratory chains become interesting only after other organisms or environmental transitions supply methane, oxygen, nitrate, sulfate, or comparable oxidants in useful quantities. These should enrich later food webs and biogeochemical cycles rather than appear as founding choices at tick zero.

# Speculative alternate biochemistry

Silicon-based life is not merely another metabolism. It changes the assumed material basis of structure and heredity, while a metabolism describes how an organism captures energy and transforms matter. In a water-rich Earthlike LYFE world, primarily silicon-based life is a poor fit: silicon chemistry tends toward stable silica, has much less versatile aqueous chemistry than carbon, and does not provide a credible drop-in replacement for the current organic resource and biomass ledger.

Silicon can still appear in conventional carbon life as a later **biomineralization** trait, such as protective silica structures. That would require a tracked silicon mineral resource but not a replacement biochemistry.

More radical possibilities belong to separate speculative world rule packs:

- Carbon-based life in a methane/ethane solvent.
- Ammonia-rich or other cryogenic solvent systems.
- Sulfuric-acid solvent environments.
- Primarily silicon-involving chemistry under a nonaqueous environment chosen specifically to make it less unfavorable.

Each would require its own solvent boundary, temperature regime, biomass composition, reaction catalogue, environmental tolerances, and likely elemental ledger. They must not be presented as one selectable DNA trait inside the aqueous CHNOPS world. If pursued, the world ruleset should define the chemistry first and then offer multiple metabolisms within that chemistry.

# Architecture guardrails

Deferring these candidates should not bake the number two into engine structure:

- Use stable `MetabolismId` values resolved from the immutable rule pack, not a two-value engine enum.
- Let a scenario declare `allowedFoundingMetabolismIds`, per-metabolism tile eligibility, founder definitions, and a competitor-pairing policy.
- Compile reaction inputs, outputs, energy opportunities, environmental gates, and trait links from validated data.
- Let setup UI cards render from authorized scenario definitions even though the v1 rule pack yields exactly two cards.
- Record metabolism and scenario IDs in saves, events, replays, and protocol messages without assuming one universal list.
- Keep resource storage extensible to additional named compounds and bulk minerals, while validating the exact v1 catalogue and range proof.
- Do not implement generic alternate solvents, iron redox chemistry, or unused reaction types merely to prove extensibility; stable identifiers and data-shaped boundaries are enough for v1.

# Re-evaluation triggers

Reconsider candidates when their distinguishing systems exist:

- Add photoferrotrophy after bulk mineral redox reservoirs, precipitation/settling, and iron world generation are designed.
- Add methanogenesis when methane climate feedback, atmospheric chemistry, or methanotrophy makes methane production strategically meaningful.
- Add dark sulfur metabolism when multiple sulfur oxidation states and a usable deep-ocean resource cycle exist.
- Add a heterotrophic founder only as a scenario with an intentional finite-organics premise and explicit success condition.
- Consider alternate biochemistry only through a dedicated world-ruleset design pass.

# Scientific anchors

- Photoferrotrophy is a proposed contributor to primary productivity and iron oxidation in ferruginous Archean oceans: [Photoferrotrophy: Remains of an Ancient Photosynthesis in Modern Environments](https://pmc.ncbi.nlm.nih.gov/articles/PMC5359306/) and [Photoferrotrophy and photoferrotrophic iron cycling in a stratified ferruginous lake](https://pmc.ncbi.nlm.nih.gov/articles/PMC6881150/).
- Hydrogenotrophic methanogenesis and acetogenesis both use H2 and CO2 and represent low-energy anaerobic strategies, supporting both its plausibility and its overlap with the current hydrogen start: [Hydrogenotrophic Methanogenesis and Acetogenesis in the Human Gut](https://pmc.ncbi.nlm.nih.gov/articles/PMC7146824/) and [Anaerobic Metabolisms in a Carbon-Rich Early Earth Ocean](https://pmc.ncbi.nlm.nih.gov/articles/PMC7264298/).
- Fermentation draws usable energy from pre-existing organic substrates rather than supplying primary production: [Anaerobic microbial metabolism can proceed close to thermodynamic limits](https://pmc.ncbi.nlm.nih.gov/articles/PMC2828274/).
- A broad review of silicon chemistry finds no examined environment in which silicon plausibly replaces carbon as life's primary chemical scaffold, although specialized organosilicon chemistry remains possible: [On the Potential of Silicon as a Building Block for Life](https://pmc.ncbi.nlm.nih.gov/articles/PMC7345352/).


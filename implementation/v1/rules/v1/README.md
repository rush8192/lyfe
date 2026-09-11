# Official v1 biological rules

This is the first strict authoring slice of LYFE's ordinary official rule pack. It is
intentionally a foundation fixture, not the complete v1 biological catalogue. Thirty-one
mass-balanced resource identities include the eight opening gases, elemental sulfur,
inorganic phosphorus, the organic-oxygen assembly remainder, and the fourteen canonical
micronutrients. Five reactions support
hydrogen acetogenesis, sulfide anoxygenic phototrophy, exact founder maintenance and
biomass assembly, and particulate-biomass digestion. The scenario permits two founder genomes
and three bounded founder allocations: Balanced, High throughput, and Stress tolerant.
The metabolism remains a steady hydrogen platform or a faster, light-gated sulfide specialist. Particulate
digestion is catalogued for evolved/test phenotypes rather than granted at abiogenesis.

Each founder genome owns its strict physiology configuration: explicit-fixture capture throughput,
efficiency, light window, generated-light coefficient, executable maintenance/growth parameters,
structure and storage capacities, equal-shared allocation policy, senescence, temperature,
and H₂S/SO₂ responses, free-micronutrient capacity, passive uptake rate, and committed quota,
deterministic-fission gates and cooldown jitter, and remnant decay/scavenging
parameters. It also compiles the five-state behavior interface and conservation
thresholds, while leaving `resourceConservation` disabled for the founder so the
evolved trait is not granted at abiogenesis. These values compile into the immutable phenotype and participate in
mechanics and compiled-artifact hashes. Pack `0.10.0` uses rule-compiler and mechanics-hash
schema `8`; allocation identity, multipliers, scenario permissions, micronutrient quotas,
uptake, the Balanced competitor default, and the scenario's inclusive `0..168`-hour founder-age
and `240..360`-hour initial-reproduction-readiness ranges are part of those identities.

Founder biomass inputs use a needs-only transient internal-store boundary: the exact
NH₃/phosphorus/H₂S bundle is admitted under the compiled `512`-load capacity and consumed
atomically in the same phase. Persistent committed/free micronutrient inventories use a
canonical fourteen-slot runtime value. Founding debits committed matter from the tile;
needs-only keyed uptake fills the free reproduction target; fission transfers one complete
quota; and death/remnant recycling returns the exact elements without creating matter.

The initial trait graph provides the two non-selectable metabolic identities, the
`StateGatedActivity -> ResourceConservation` branch, and the first distinct escape-path
nodes. Traits have permanent IDs,
explicit costs/change complexity, prerequisites, pressure tags, and typed compiled
effects. Selectable traits also carry one or more closed-vocabulary strategic intents used only to
group and explain player proposals. They affect the presentation hash while leaving mechanics and
compiled-artifact identity unchanged. A selectable trait may additionally author one consequence
follow-up duration greater than the universal 168-hour summary and one typed evidence family. The
first pack uses 720 hours for resource-conservation condition/pressure; later activation, spread,
and storage nodes can use their matching typed families once their primary counters are executable.
This metadata schedules observation only and cannot extend cooldown or alter simulation
mechanics. A missing non-selectable prerequisite cannot be synthesized by autonomous
prerequisite closure; eventual cross-pathway acquisition therefore requires an explicit
priced bridge. This narrow graph proves the authoring and full-phenotype recompilation
path; later biological milestones remain owned by their planned rule slices rather
than being selectable placeholders.

`pack.json` explicitly names every authoritative source file. `generated/rules.lock.json`
commits the permanent numeric-ID/stable-key pairs; removed identities will become
tombstones rather than being reused. Runtime simulation code will consume only
validated, immutable compiled artifacts—not these authoring records directly.

The compiler assigns dense slots in permanent-ID order, resolves every authored
key, checks reaction matter balance, and generates separate mechanics,
presentation, registry, and logical-compiled hashes. Golden vectors live in the
simulation tests; file grouping, JSON property order, and set order do not enter
those identities.

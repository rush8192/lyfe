# DNA Trait Graph and Compilation

Status: first implementation proposal; policy questions identified

Sources: [ORGANISMS vision](../../vision/ORGANISMS.md), [evolution plan](EVOLUTION.md), [organism plan](ORGANISMS.md), [configuration plan](CONFIGURATION_AND_BALANCE.md), and [resource model](RESOURCE_MODEL.md).

# Purpose

Define how LYFE authors, validates, prices, unlocks, compiles, applies, and explains DNA traits. The trait system must support linear progressions, branching family trees, hard cross-family prerequisites, incompatibilities, direct and indirect attribute effects, and energy/resource costs without placing arbitrary scripts in the simulation hot loop.

This document fixes the proposed data and compilation shape. The policy choices in [Questions to confirm](#questions-to-confirm) should be decided before the concrete configuration schema and initial trait catalogue are finalized.

# Core model

Each family is presented as a rooted tree or forest. A linear family is simply a tree in which each node has at most one successor. Multiple independent roots are permitted when a family contains orthogonal capabilities.

The complete DNA graph is not a tree. Cross-family prerequisites turn the union of family trees into a directed acyclic graph:

```text
family-local parent/child edges
          +
cross-family prerequisite edges
          =
global prerequisite DAG
```

An organism species owns an immutable set of acquired `TraitId` values. A descendant begins with every trait acquired by its ancestor and may add valid nodes; it cannot remove a trait. The family layout is therefore presentation metadata over exact prerequisite rules, not a second source of unlock truth.

Sibling traits are compatible by default. A branch becomes exclusive only through an explicit incompatibility. Choosing one side of an exclusive branch permanently closes the other side for that descendant lineage, while the ancestor and sibling species may pursue different branches.

# Authored definitions

```text
TraitFamilyDefinition:
    id: TraitFamilyId
    displayName
    displayOrder
    rootTraitIds[]
    layoutHints

TraitDefinition:
    id: TraitId
    familyId: TraitFamilyId
    displayName
    description
    displayParentTraitId: TraitId?
    prerequisites: TraitPredicate
    incompatibleTraitIds[]
    mutationPointCost
    changeComplexity
    effects: TraitEffect[]
    activationRequirements: ActivationRequirement[]
    pressureTags[]
    visualEffects[]
    rulesVersionIntroduced

TraitPredicate:
    HasTrait(TraitId)
    AllOf(TraitPredicate[])
    AnyOf(TraitPredicate[])
```

The optional within-family display parent is also a hard prerequisite and is compiled into the exact predicate model when the rule pack loads. A node that conceptually reconverges after two branches chooses one display parent and names both branches in `prerequisites`; the family remains drawable as a tree while the global dependency structure remains a DAG. Linear levels such as `HeatToleranceI`, `HeatToleranceII`, and `HeatToleranceIII` are separate stable nodes rather than a mutable integer level. This keeps pricing, historical attribution, save compatibility, and UI paths explicit.

The first implementation should not support negated predicates, arbitrary expressions, world-state queries, or author-written code. Incompatibility has its own field, and material/environmental feasibility is evaluated separately from genetic prerequisites.

# Typed effects

Traits compile through a closed, versioned union of typed effects. The initial vocabulary should cover:

```text
TraitEffect:
    AddNumeric(attributeId, amount)
    MultiplyNumeric(attributeId, fixedPointFactor)
    SetExclusiveChoice(attributeId, choiceId)
    AddPassiveCost(costChannel, amountOrRate)
    ModifyActionCost(actionOrCostChannel, fixedPointFactor)
    EnableCapability(capabilityId)
    EnableReaction(reactionId)
    ModifyReaction(reactionIdOrTag, parameter, operation, value)
    ModifyCapacity(compartmentOrResourceTag, operation, value)
    AddMinimumQuota(resourceId, amount)
    AddActivationRequirement(capabilityId, requirement)
    EnableSense(signalId, rangeAndPrecision)
    EnableBehavior(behaviorId, parameters)
    AddPressureResponse(pressureTag, weight)
    AddVisualDescriptor(descriptor)
```

Arbitrary callbacks and embedded scripts are excluded. If a desired trait cannot be represented by a typed effect, the engine effect vocabulary is extended deliberately and versioned rather than smuggling code into rule data.

## Direct and indirect effects

A direct effect writes to the attribute most visibly owned by its family. For example, `HeatToleranceII` directly widens a preferred temperature range and hard survival limits.

An indirect or cross-family effect uses the same typed vocabulary but names another attribute or cost channel. For example:

- A wall trait can add structural defense, chemical tolerance, mature biomass, and maintenance cost.
- A flagellar trait can add a movement mode, velocity, construction quota, passive upkeep, and energy cost per distance.
- A compartment trait can unlock a storage node and add structural and maintenance overhead.
- An oxygen-respiration trait can enable an internal reaction while adding oxygen-uptake and tolerance prerequisites.

Every compiled contribution retains its source `TraitId` and whether it was direct or cross-family. The client can therefore explain a final value as a breakdown rather than showing an unexplained aggregate.

# Attributes and cost channels

Fundamental organism attributes are compiled once per species. The first catalogue should include typed groups for:

- Viable and mature structural biomass.
- Energy-reserve and nutrient-store capacities.
- Baseline maintenance and stress multipliers.
- Environmental preferred ranges, soft limits, hard limits, and death curves.
- Movement modes, velocity, acceleration, terrain compatibility, and cost.
- Sensing signals, ranges, precision, and recurring cost.
- Acquisition tags and throughput.
- External capture and internal reaction availability, throughput, probability, and yield.
- Reserve-mobilization and biomass-assembly throughput.
- Lifecycle, maturity, dormancy, senescence, and reproduction parameters.
- Predation, scavenging, defense, and interaction radii.
- Behavior choices and selection weights.
- Mutation-income and per-speciation complexity modifiers.

Energy cost must be attributed to named channels rather than folded into one opaque metabolism multiplier:

| Cost channel | Example contributors |
| --- | --- |
| `BaselineMaintenance` | Cell scale, organization, constitutive machinery |
| `EnvironmentalStress` | Temperature, moisture, toxic exposure |
| `LocomotionUse` | Propulsion type, distance, terrain |
| `SensingUpkeep` | Active signals, range, precision |
| `DefenseUpkeep` | Walls, deterrents, active defenses |
| `AcquisitionUse` | Active transport, ingestion, digestion |
| `ReactionOverhead` | Capture or catabolic pathway operation |
| `ReproductionOverhead` | Split/bud machinery and allocation model |
| `TraitExpression` | Optional one-time or gated construction work |

Internal-metabolism traits may modify specific channels. A general efficiency trait must enumerate the channels it affects; it cannot silently discount all energy use. Passive costs apply every eligible tick, while use costs apply only when the action or reaction resolves.

# Numeric composition

Trait compilation must not depend on authoring order, hash-map order, or the order in which traits were acquired. For each numeric attribute:

```text
compiledValue = clamp(
    (baseValue + sum(all additive contributions))
    * product(all multiplicative contributions),
    configuredMinimum,
    configuredMaximum)
```

Fixed-point multiplication uses a canonical sort by `(attributeId, sourceTraitId, effectIndex)`, checked wide intermediates, and a named rounding rule. Multipliers apply once after the additive sum unless an attribute definition explicitly declares another operation model.

`SetExclusiveChoice` is permitted only for attributes such as reproductive allocation mode where exactly one choice must win. Configuration validation rejects two active providers unless the traits are explicitly ordered as replacements. Ordinary scalar traits must use typed additive or multiplicative effects instead of last-writer-wins assignment.

# Prerequisites, incompatibility, and feasibility

The implementation distinguishes three different gates:

1. **Genetic prerequisite:** another trait must already be present in the proposed descendant DNA. Missing prerequisites reject the DNA proposal.
2. **Incompatibility:** two acquired traits cannot coexist in one DNA. The proposed descendant is rejected, and irreversibility means an exclusive branch cannot later be undone.
3. **Phenotypic activation requirement:** the DNA is valid, but a capability operates only while an individual organism has sufficient structure, internal quota, lifecycle phase, or environmental opportunity.

Current tile conditions and resource stocks should not normally be genetic prerequisites. Oxygenic photosynthesis can be genetically valid after its trait prerequisites are present even if a selected tile is dark; it simply cannot capture energy there. A required manganese or calcium quota is an organism activation/reproduction requirement rather than a query against current tile inventory during graph validation.

Scenario rules may separately restrict which traits can appear in a mode or founding setup. Scenario permission is admission policy, not a prerequisite edge stored in the biological graph.

# Acquired DNA versus active phenotype

Speciation changes the selected founders' species/DNA reference but does not create matter, fill storage, or rewrite their authoritative resource balances. The recommended v1 policy is:

- Scalar DNA parameters apply beginning with the next organism-evaluation phase.
- Storage-capacity changes preserve current contents.
- A capability with activation requirements is present genetically but inactive for an organism until its current structure, resource quotas, lifecycle, and environment satisfy those requirements.
- Activation is derived from current state rather than stored as a second genetic flag.
- Reproduction must provide an offspring with the descendant DNA's minimum viable structure and quotas; otherwise the reproduction transaction is invalid.
- An organism below a new mature target may grow toward it. Falling below a hard minimum-viable target contributes stress or death according to organism rules rather than creating free structure.

This avoids per-trait expression state and prevents physical machinery from appearing without matter. It also permits members of one species to differ temporarily in active phenotype because their resources and lifecycle state differ, while their DNA remains identical.

If generic structural biomass and quota gates prove too abstract for important traits, a later rules version may add explicit one-time expression/construction transactions. V1 should not add per-trait physical inventories until a fixture demonstrates the need.

# Suppression and regulation

Acquired traits never disappear from DNA, but some capabilities may be conditionally inactive:

- Environmental or material activation requirements are derived automatically.
- Behavioral regulation may choose not to invoke an available action.
- Metabolic regulation may suppress an enabled pathway and reduce its declared suppressible upkeep.
- Constitutive costs and structural requirements remain even when a capability is not used unless the trait explicitly marks a cost as suppressible.

Suppression is runtime state or behavior, not reverse evolution. The compiled DNA records the full capability and both active/suppressed cost profiles.

# Mutation cost and change complexity

Mutation points and evolutionary change breadth solve different problems:

- `mutationPointCost` is the economic price of acquiring a node.
- `changeComplexity` measures how much genetic change one speciation event can express.
- `EvolutionaryMachinery` compiles `maxChangeComplexityPerSpeciation` and the mutation-income modifier independently.

The recommended v1 rule permits multiple nodes in one speciation when:

- The command explicitly names every new node.
- The final DNA satisfies every prerequisite and incompatibility.
- The sum of node mutation-point costs is affordable.
- The sum of node change complexity does not exceed the species' compiled limit.

Prerequisite nodes are never silently granted. The UI may construct and preview a complete path, but the command records the exact set and full price. V1 uses simple sums with no bundle discount, distance surcharge, or dynamic cost based on world conditions. Adjacency is rewarded naturally because nearby nodes require fewer prerequisite purchases and less change complexity.

# Compilation

```text
CompileDNA(baseGenome, acquiredTraitIds, compiledTraitGraph):
    reject unknown or duplicate trait IDs
    validate all predicates against final acquired set
    reject every acquired incompatibility pair
    group typed effects by target attribute/capability
    compose numeric attributes in canonical operation order
    resolve exclusive choices and enabled capabilities/reactions
    compile activation requirements and named cost channels
    validate ranges, quotas, reaction references, and capacity limits
    build pressure-response and visual-descriptor indexes
    retain effect provenance for every compiled result
    hash canonical genome and compiled rules
    return immutable CompiledDNA
```

Rule-pack loading expands display-parent prerequisites, validates the global DAG, and compiles effect metadata once. DNA compilation occurs for known genomes when that rule pack loads and when a new descendant DNA proposal is previewed or accepted. Organism ticks read dense immutable compiled structures; they do not traverse trait graphs or evaluate prerequisites.

Two identical acquired sets under one rule-pack hash must produce the same genome ID, compiled values, provenance, and canonical hash regardless of acquisition history or worker scheduling.

# Unlock frontier and UI

For a species, the server can derive four useful node states:

| State | Meaning |
| --- | --- |
| `Acquired` | Present in current DNA |
| `Available` | Not acquired; prerequisites met; no incompatibility; scenario permits it |
| `ReachableInProposal` | Can be included with additional explicit prerequisite nodes in the same proposal and complexity limit |
| `Blocked` | Missing prerequisites, incompatible branch, scenario restriction, or insufficient change complexity |

Affordability is shown separately from structural availability so a node can be valid but currently too expensive. Environmental usefulness and founder activation feasibility are also separate warnings rather than graph states.

A proposal preview returns:

- New nodes, full prerequisite path, mutation cost, and change complexity.
- Direct and cross-family attribute deltas with source attribution.
- Added passive and use-dependent costs by channel.
- Newly enabled reactions, actions, senses, and behaviors.
- New structure, micronutrient, storage, and activation requirements.
- Current selected-founder counts likely to satisfy activation gates.
- Capacity changes and the resulting reserve-relative-health impact without adding contents.
- Incompatibilities made permanent for the descendant lineage.

# Autonomous evolution

Autonomous evolution searches only the same server-derived `Available` and `ReachableInProposal` nodes exposed to a player. Trait definitions carry `pressureTags` such as `HeatStress`, `SulfideToxicity`, `Starvation`, `Predation`, or `EnergyShortage`. Recent species death/stress history changes candidate weights; it never bypasses prerequisites, incompatibilities, cost, complexity, or scenario rules.

Cross-family indirect effects also contribute pressure tags. A wall trait may therefore be considered under predation pressure even though it belongs to `CellularOrganization`, while a smaller-cell trait might respond to resource scarcity through lower maintenance.

# Configuration validation

Rule-pack loading rejects:

- Unknown family, trait, attribute, capability, reaction, resource, behavior, sense, or pressure IDs.
- Duplicate stable IDs.
- Cycles in family-parent or cross-family prerequisites.
- A trait incompatible with itself or a required ancestor.
- Unreachable nodes unless explicitly marked scenario/future-only.
- Invalid effect operations for an attribute's declared composition model.
- Multiple possible providers for an exclusive choice without explicit replacement rules.
- Negative mutation costs or complexity, invalid fixed-point values, or overflow risk.
- Activation requirements that reference unavailable organism state.
- Trait combinations in required fixtures that compile outside configured numeric bounds.

# Required tests

- Linear, branching, independent-root, reconverging, and cross-family prerequisite fixtures.
- Cycle, missing reference, incompatible ancestor, and exclusive-choice rejection tests.
- Compilation invariant under trait-input ordering and acquisition history.
- Golden compiled values and provenance for representative cross-family traits.
- Property tests that capacity traits do not credit matter or energy.
- Speciation tests preserving founder structure, reserve, stores, and quotas.
- Activation tests for sufficient/insufficient structure, micronutrients, lifecycle, and environment.
- Cost-channel tests distinguishing passive upkeep from action use.
- Suppression tests preserving acquired DNA and constitutive costs.
- Mutation-cost and change-complexity boundary tests.
- Player and autonomous candidate-frontier equivalence.
- Save/load and rule-pack-hash stability tests.

# Questions to confirm

The proposal recommends defaults, but these product choices deserve explicit confirmation:

1. **Exclusive branches:** siblings are compatible unless explicitly incompatible. An exclusive choice is permanent for that descendant lineage. Should any family make exclusivity the default instead?
2. **Multi-node speciation:** one event may buy a complete multi-node path, constrained by both mutation-point price and a DNA-defined change-complexity limit. Should v1 instead require one node per speciation?
3. **Trait expression:** DNA applies immediately, but material-dependent capabilities activate only when each organism meets derived structure/quota gates; v1 tracks no per-trait construction progress. Is that abstraction sufficient?
4. **Environmental gating:** world conditions affect usefulness/activation, not genetic unlock validation. Should any environmental observation be a hard requirement to acquire a trait?
5. **Effect replacement:** acquired traits normally contribute forever. Should advanced nodes be allowed to explicitly replace an ancestor's numeric effect or upkeep while keeping the ancestor in lineage history?
6. **Energy efficiency scope:** all efficiency effects target named cost/reaction channels; there is no unrestricted global efficiency multiplier. Is that degree of explicitness desirable for balance and explanation?
7. **V1 catalogue depth:** should every canonical family have at least one selectable v1 branch, or may defense, advanced behavior, nutrient storage, and other families remain shallow until their corresponding organism mechanics are calibrated?

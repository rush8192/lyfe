# Rule-Pack Authoring, Identity, Validation, and Compilation

Status: first v1 engineering contract; concrete DTOs, permanent catalogue assignments, and the representative rule bundle remain implementation work

Sources: [configuration and balance](CONFIGURATION_AND_BALANCE.md), [moddability](MODDABILITY.md), [trait system](TRAIT_SYSTEM.md), [resource storage and evaluation](RESOURCE_STORAGE_AND_EVALUATION.md), [keyed randomness](KEYED_RANDOMNESS.md), [data model](DATA_MODEL.md), and [technology decisions](TECHNOLOGY.md).

# Purpose

Define the cold-path boundary that turns readable LYFE content into the immutable, validated numerical structures consumed by the authoritative simulation. This contract owns authoring format, definition identity, layer composition, normalization, rule hashing, global compilation, DNA compilation, diagnostics, packaging, and compatibility.

The compiler must make invalid worlds impossible to start. It must not move authoring flexibility into organism ticks, allow file order to affect outcomes, or create a second calculation path beside the stored compiled values.

# Decision summary

- V1 rules are authored as strict UTF-8 JSON split into small domain files beneath one explicit pack manifest.
- C# authoring records and their closed tagged unions are the structural contract. `System.Text.Json` metadata source generation performs deserialization; generated JSON Schema is committed for editors and external tooling.
- Runtime validation is owned by C# and includes structural, referential, graph, dimensional, conservation, range, scenario, and fixture checks. JSON Schema is helpful but never the only validator.
- All authoritative numbers are JSON integers in explicitly named game units. Floating JSON numbers, `NaN`, infinities, locale-dependent text, and implicit unit conversion are prohibited.
- Every content definition carries an explicit permanent typed `UInt32` ID and a permanent lowercase stable key. Zero is reserved, removed IDs become tombstones, and neither value is inferred from array, file, enum, or discovery order.
- Files inside one base pack contribute definitions without overriding one another. An optional balance-mod source set may replace only explicitly registered moddable fields before whole-pack validation and compilation.
- Compilation has three representations: authoring records, a canonical normalized rule model, and immutable runtime artifacts. A world and its ticks never retain or traverse authoring JSON.
- Rule hashes are calculated from normalized semantic data, not source bytes, whitespace, file layout, object-property order, or display prose.
- Global compilation resolves registries, reactions, dense resource handles, trait metadata, curves, scenario tables, and bounds once. DNA compilation then produces one immutable typed phenotype per unique canonical genome.
- DNA is always fully recompiled from its canonical acquired-trait set. V1 does not incrementally patch an ancestor phenotype, preventing acquisition history and accumulated rounding from changing the result.
- Active worlds never hot-reload a rule pack or mod set. Every balance overlay, development override, or full fork builds a distinct final identity and cannot silently continue a canonical save.
- V1 implements a local/developer balance-overlay seam, plus separately selectable closed-schema world-generation packs. General content additions, executable mods, online distribution, and polished mod-management UX remain future work. See [MODDABILITY.md](MODDABILITY.md).

# Authoring format

## Strict JSON

V1 selects standards-compliant JSON rather than YAML, TOML, JSON-with-comments, or an executable content language.

Reasons:

- the .NET runtime already provides a UTF-8 parser, typed deserialization, required-member handling, closed polymorphic discriminators, and source-generated metadata;
- JSON Schema supplies useful editor completion and first-line structural feedback;
- arrays and tagged records represent traits, reactions, curves, and nested predicates without TOML's awkward deeply nested tables;
- strict JSON avoids YAML's implicit scalar typing and indentation semantics;
- prohibiting comments and trailing commas keeps every accepted source valid for ordinary JSON tools and prevents separate development/release parsers.

Explanatory author notes use optional `notes` or `description` properties whose semantic status is declared by the owning record. Long design rationale remains in Markdown. Unknown properties, duplicate object properties, comments, trailing commas, invalid UTF-8, and case-mismatched property names are errors.

Authoritative numeric properties use descriptive unit suffixes:

```text
maintenanceEnergyQPerHour
temperatureMilliC
probabilityQ
durationHours
mutationPointCost
resourceQuantityQ
```

All are JSON integer tokens within the declared signed or unsigned range. A decimal such as `0.25` is invalid; the author writes `250000` for a `RatioQ` value. Scientific notation and quoted numeric strings are rejected except for a specifically declared value wider than signed 64-bit, for which the schema supplies a canonical decimal-string type and bounded parser. No initial rule field is expected to require that exception.

## C# structural contract and generated schema

Authoring records are immutable C# records under a dedicated cold-path namespace, with:

- `required` properties for required fields;
- string JSON names and explicit tagged-union discriminators;
- source-generated `System.Text.Json` metadata;
- unmapped-member rejection;
- exact-case property and discriminator matching; and
- no reflection-based plug-in discovery.

The build tool exports JSON Schema from those contracts, adds the pack-specific descriptions and declared JSON Schema Draft 2020-12 identity, and writes committed schema artifacts. CI regenerates them and fails on a diff. The schema is an editor and interchange artifact; the typed C# loader and semantic validators remain authoritative because cross-reference, graph, conservation, and whole-pack constraints exceed structural schema validation.

V1 uses metadata source generation, not a hand-written high-performance JSON reader. Pack loading is a startup/tooling operation, and clarity and diagnostics matter more than deserialization throughput.

# Pack layout and composition

The first source layout is:

```text
rules/v1/
  pack.json
  resources/
    resources.json
    capacity-groups.json
  reactions/
    reactions.json
    decomposition.json
  traits/
    families.json
    traits.json
    founder-genomes.json
  environment/
    gases.json
    climate-contracts.json
    exposures.json
    transport.json
  organisms/
    lifecycle.json
    behavior.json
    spatial.json
  evolution/
    mutation-economy.json
    autonomous-evolution.json
  scenarios/
    sandbox.json
    survival.json
  curves/
    curves.json
  fixtures/
    required-fixtures.json
  generated/
    rule-pack.schema.json
    rules.lock.json
    engine-vocabulary.lock.json

world-packs/official-primordial/
  world-pack.json
  profiles/
    primordial-earthlike.json
  fixtures/
    required-seeds.json
  generated/
    world-generation-pack.schema.json
```

This organization is for authors. Moving a definition between files cannot change its normalized meaning or hash. `pack.json` explicitly lists every included relative file or file group; the loader does not discover authoritative inputs from operating-system enumeration. Paths are normalized relative to the pack root, may not escape it, and may not appear twice.

```text
RulePackManifest:
    packId
    version
    engineRuleApiVersion
    ruleCompilerVersion
    mechanicsHashSchemaVersion
    registryLockFile
    includedFiles[]
    requiredFixtureIds[]
```

The initial base pack is self-contained. Files within it have no imports, inheritance, patches, conditional includes, macros, environment-variable substitution, or embedded scripts. A separate `RuleSourceSet` may apply the constrained balance replacements in [MODDABILITY.md](MODDABILITY.md) before resolution; duplicate field writers fail instead of using arbitrary overlay precedence. World profiles live in separately hashed world packs, are complete rather than inherited, and join only at world-rule compilation after the base/mod rule set has resolved.

Test packs may be separate complete manifests, while balance experiments may also be explicit balance-mod packages targeting an exact base hash. A small test builder may construct definitions programmatically inside tests, but serialized golden packs and mod sets use the same loader and compiler as production.

## Configuration layers

The existing configuration layers cross this boundary as follows:

| Layer | Representation and ownership | Compatibility effect |
| --- | --- | --- |
| Engine algorithms | Versioned C# implementation and engine-vocabulary registry | Changes require an engine/RNG/compiler compatibility decision |
| Biological/world rules | Immutable rule pack definitions | Included in `mechanicsHash` |
| Scenario | Rule-pack definition selecting gameplay content, founders, pacing, and required world capabilities | Included in the compiled world-rules hash |
| World-generation profile | One complete profile from an independently identified world pack, using closed registered generator/environment vocabulary | Included in the compiled world-profile and world-rules hashes |
| Player world options | Validated setup command values allowed by both scenario and world profile | Included in the compiled world-rules hash and save |
| Balance/presentation mods | Typed replacements of explicitly marked fields against an exact base hash | Flattened before validation; included in mod-set and final rule identities |
| Development override | A tool-generated complete experimental pack | Produces a distinct identity; never masquerades as the canonical pack |
| Presentation metadata | Names, descriptions, strategic-intent tags, layout hints, visual descriptors | Included in `presentationHash`, excluded from `mechanicsHash` unless a field affects simulation |

A scenario references founding genomes, available setup choices, deadline, tick duration, allowed trait/content IDs, and capabilities a selected world profile must guarantee. It cannot replace a reaction coefficient, trait effect, or world-profile value. The chosen world profile owns dimensions/default ranges, climate and geography parameters, tile initialization, and continuing environmental coefficients. A different balance or world-profile value produces a different final identity.

# Moddable source boundary

The official base pack is ordinary rule data, not a hard-coded exception. Authoring members opt into `BalanceOverride` or `PresentationOverride` through stable generated namespaced keys; unmarked fields default to full-pack-only. A `RuleSourceSet` flattens disjoint, exact-base-targeted replacements before semantic validation, normalization, hashing, global compilation, and DNA compilation. The final compiled value remains the single runtime truth, whether or not an overlay supplied it.

The complete overlay and world-pack schemas, conflict rejection, certification split, save/protocol identity, client requirements, and future content/code-mod boundary are defined in [MODDABILITY.md](MODDABILITY.md).

# Permanent identity and registries

## Definition identity

Every authoritative definition has both:

```text
DefinitionIdentity:
    numericId: UInt32       // typed by registry; zero invalid
    stableKey: string       // permanent lowercase ASCII dotted key
```

Examples of keys are `resource.reserve-organic`, `reaction.hydrogen-acetogenesis`, and `trait.environmental-anchoring`. Keys match `^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$`, use one canonical spelling, and are never localized.

`numericId` is used in authoritative state, hashing, sorted execution, saves, and hot compiled references. `stableKey` is used in authored cross-references, diagnostics, CLI commands, and human review. Active content definitions are the source of their own ID/key pair. On load, every reference resolves through the typed registry, and each active identity must agree with the committed historical lock manifest.

Registries are separate typed spaces. `ResourceId(17)` and `TraitId(17)` do not collide because their types and manifests differ. At minimum, v1 maintains explicit registries for:

- resources, reservoirs, capacity groups, and transport classes;
- reactions and process kinds;
- trait families, traits, capabilities, attributes, and exclusive choices;
- cost channels, behaviors, senses, pressures, exposures, and lifecycle profiles;
- scenarios, founding genomes, curves, fixtures, and flow/cause categories;
- RNG domains; and
- deterministic outcome, creation, event, journal, and protocol-semantic categories where numeric stability is required.

Engine-vocabulary registries describe the closed concepts implemented in code, such as typed trait-effect kinds, process kernels, curve evaluators, rounding modes, RNG domains, and outcome categories. Their explicit C# compatibility declarations are the source of truth and generate `engine-vocabulary.lock.json`; they are not duplicated as editable pack content. Content registries are built from pack-defined biological/world entries and checked against `rules.lock.json`. Authoring cannot invent a new engine-vocabulary key without corresponding compiler and kernel support.

## Assignment policy

- IDs are explicitly written and code-reviewed; source generators never renumber them.
- A CLI command may suggest the lowest never-used ID in one typed registry, but accepting and committing it is an author action.
- Deleting a definition converts its committed lock entry to a tombstone containing its former ID/key and removal version. Neither ID nor key can be reused.
- Renaming display text does not change identity. Renaming a stable key is a compatibility change and normally uses a new ID plus a tombstone.
- Sorting, compacting, or splitting files cannot alter IDs.
- Registry lock generation rejects duplicates, reuse, missing tombstones, an ID/key mismatch with prior `rules.lock.json`, or an unexplained removal. Updating a lock is an explicit reviewed operation, not an ordinary build side effect.
- Dense runtime slots are assigned separately during compilation and never persist as identity.

The first implementation task assigns concrete numbers to the current v1 catalogue and every RNG/outcome category already named in planning. Until that manifest exists, the rules compiler is not complete enough to create a durable save.

# Representation pipeline

```text
strict source JSON + source map
          │ deserialize
          ▼
AuthoringRulePack
          │ structural/local validation
          ▼
ResolvedRulePack
          │ typed registry and cross-reference resolution
          ▼
NormalizedRulePack
          │ canonical semantic hashing
          ├──────────────► RulePackIdentity
          │ global compilation
          ▼
CompiledRulePack
          │ scenario + selected complete world profile + player options
          ▼
CompiledWorldRules
          │ canonical acquired TraitId set
          ▼
CompiledPhenotype
```

## Authoring model

`AuthoringRulePack` preserves strings, authoring groupings, optional display metadata, and source locations. It contains no dense slots, world state, delegates, service references, or engine entity IDs.

Before typed deserialization, one `Utf8JsonReader` pass rejects duplicate properties within each object and records a `RuleSourceMap` from `(file, JSON pointer)` to byte offset, line, and column. The exact same bytes are then passed to the source-generated typed deserializer. Semantic errors can therefore identify both the bad reference and, where relevant, the referenced definition.

## Resolved and normalized model

Resolution converts stable-key references to typed IDs and rejects missing or wrong-kind references. Normalization then:

- sorts definition sets by typed numeric ID;
- sorts set-valued references canonically and rejects duplicates;
- preserves order only for explicitly order-semantic arrays such as piecewise-curve points or authored display layout;
- expands display-parent trait prerequisites into the global predicate DAG;
- makes defaults explicit;
- converts all values to typed integer units;
- normalizes Unicode semantic strings to NFC where free text is genuinely semantic;
- separates simulation fields from presentation-only fields; and
- retains provenance back to source locations.

No validator or compiler reads an unordered dictionary as semantic sequence.

## Immutable runtime artifacts

`CompiledRulePack` contains the pack-global data needed by world creation and phenotype compilation:

```text
CompiledRulePack:
    identity
    definitionRegistries
    denseResourceManifestsByReservoir
    denseCapacityGroupManifests
    compiledReactions
    compiledTraitGraph
    compiledCurvesAndTables
    environmentMechanicsAndLifecycleProfiles
    worldGenerationSchemaAndAlgorithmRegistry
    scenarioDefinitions
    founderGenomeDefinitions
    engineVocabularyManifest
    declaredBounds
    explanationMetadata
```

`CompiledWorldRules` binds a scenario, one validated world profile, and accepted setup options:

```text
CompiledWorldRules:
    rulePack
    scenarioId
    worldGenerationIdentity
    compiledWorldGenerationProfile
    tickDuration
    worldDimensions
    deadline
    permittedFoundersAndOptions
    perTickRateTablesAndRemainderRules
    worldRulesHash
```

Duration/rate conversions occur here because tick duration is a world option. A duration that cannot be represented under the rule's declared rounding/remainder policy is rejected before world creation. The tick loop reads these compiled values and does not repeatedly convert per-hour authoring units.

# Curves and tables

V1 has one general authored curve form:

```text
PiecewiseLinearCurveDefinition:
    id
    inputUnit
    outputUnit
    points[]: (integerX, integerY)
    belowRange: ClampToEndpoint
    aboveRange: ClampToEndpoint
    interpolationRounding
```

Points require strictly increasing `x`, declared `x/y` bounds, at least two points, checked `Int128` interpolation, and one named rounding rule. Monotonic or convex behavior is an additional constraint declared by the consuming field, not assumed for every curve.

Engine-owned nonlinear mechanics—such as exponential half-life decay, a particular senescence hazard, or spatial radius scaling—remain closed typed evaluators with integer parameters. The compiler may materialize lookup tables for them over a proved bounded input domain. Rule authors cannot enter arbitrary formulas, expression trees, callbacks, or code. Adding a new curve kind is an engine-rule API and compatibility decision.

Every lookup table records the source curve/evaluator ID, input interval, output unit, rounding rule, and table hash. The compiler's direct evaluator and table must match at every admitted integer input in exhaustive or boundary-plus-property tests as appropriate.

# Validation pipeline

Pack construction is deterministic and fail-closed:

```text
BuildRulePack(packRoot):
    manifest = parse strict pack.json with source map
    sources = load exactly manifest.includedFiles within bounded limits
    authored = deserialize with generated metadata and closed discriminators

    diagnostics += ValidateStructuralAndLocalFields(authored)
    registries = BuildTypedRegistries(authored, previousLockManifest)
    resolved = ResolveEveryReference(authored, registries)
    diagnostics += ValidateGraphsAndCrossDefinitionRules(resolved)
    diagnostics += ValidateUnitsRangesAndCollectionBounds(resolved)
    diagnostics += ValidateMatterEnergyAndReactionGraph(resolved)
    diagnostics += ValidateScenariosAndFounderClosure(resolved)
    diagnostics += ValidateStorageReachabilityAndMaximums(resolved)
    if any Error: return failure with canonical diagnostic list

    normalized = Normalize(resolved)
    identity = HashNormalizedRules(normalized)
    compiled = CompileGlobalArtifacts(normalized, identity)
    diagnostics += ValidateCompiledArtifacts(compiled)
    diagnostics += RunRequiredRuleFixtures(compiled)
    if any Error: return failure with canonical diagnostic list

    lock = BuildRegistryAndHashLock(compiled)
    return immutable compiled rule pack + lock + diagnostics
```

Validation stages aggregate independent errors up to a configured diagnostic ceiling. Cascading checks skip only inputs invalidated by an earlier error. Diagnostics sort by severity, typed definition kind/ID, file, line, column, rule code, and related ID; parallel validation cannot reorder output.

```text
RuleDiagnostic:
    severity: Error | Warning
    code
    message
    definitionKind?
    definitionId?
    sourceLocation
    jsonPointer?
    relatedLocations[]
    suggestedAction?
```

Warnings never repair or substitute authoritative values. Release bundles require zero errors and an explicit warning policy; required fixture failures are errors. The server does not start a world from a partially compiled pack or fall back to built-in defaults.

The comprehensive domain validation inventory remains in [CONFIGURATION_AND_BALANCE.md](CONFIGURATION_AND_BALANCE.md). This document fixes how those checks execute and report.

# Canonical hashing and compatibility

## Hashes

One pack exposes at least:

```text
RulePackIdentity:
    packId
    declaredVersion
    engineRuleApiVersion
    ruleCompilerVersion
    mechanicsHashSchemaVersion
    mechanicsHash: SHA-256
    presentationHash: SHA-256
    registryManifestHash: SHA-256
    compiledArtifactHash: SHA-256
```

`mechanicsHash` includes every normalized base-rule field that can change authoritative state, probability, timing, eligibility, ordering, balance, permitted world-generation semantics, player command validity, or automatic pause. Concrete selected world-profile values live in `compiledWorldProfileHash` and are incorporated into `worldRulesHash`. The mechanics identity also includes engine-vocabulary registry identities and the compiler/evaluator versions needed to interpret those fields.

`presentationHash` covers normalized display names, descriptions, layout hints, visual descriptors, and similar non-authoritative metadata. A presentation-only correction may retain simulation compatibility, although a packaged client can still detect that its content text differs.

`registryManifestHash` covers every active and tombstoned numeric ID/key pair. `compiledArtifactHash` covers the canonical logical compiled outputs—stable IDs and numerical values, never runtime addresses, object layout, or dense-slot accidents—and detects compiler drift.

## Canonical hash encoding

Hashes do not serialize source JSON. A versioned `CanonicalRuleHashWriter` emits:

- explicit numeric record and field tags;
- fixed-width little-endian integers and booleans;
- length-prefixed UTF-8 NFC strings;
- typed ID values;
- arrays in declared semantic order;
- sets in canonical typed-ID order; and
- explicit presence markers for optional values.

It emits no floating-point values, object addresses, runtime type names, reflection order, dictionary order, file paths, source locations, comments, or display fields into `mechanicsHash`. Adding, removing, or reinterpreting a semantic field changes `mechanicsHashSchemaVersion` or the relevant engine/compiler version.

The same normalized pack built on supported x64/ARM64 hosts must produce identical hashes and compiled logical artifacts.

## World compatibility identity

A world save/checkpoint pins:

```text
SimulationCompatibility:
    engineSimulationVersion
    rulePackId
    modSetHash
    mechanicsHash
    registryManifestHash
    ruleCompilerVersion
    scenarioId
    worldPackPackageHash
    worldProfileKey
    selectedWorldOptionsHash
    compiledWorldProfileHash
    worldRulesHash
    rngAlgorithmId
    rngSchemaVersion
    rngDomainManifestHash
```

`worldRulesHash` combines the final flattened mechanics identity with the normalized scenario, selected world-package/profile identity, compiled world-profile semantics, and player-selected options. A load also verifies the base/mod and world-package provenance. A matching display version string is insufficient. There is no silent recompile-and-continue under changed semantics. Explicit migration tools may be designed later and must create an auditable new compatibility boundary.

# Global rule compilation

Global compilation performs work shared by every genome and world:

1. freeze typed registries and stable lookup tables;
2. assign runtime-only dense resource slots per compatible reservoir/capacity group in ascending stable ID order;
3. resolve reaction inputs, outputs, catalysts, waste destinations, and ledger categories to typed dense handles;
4. prove integer stoichiometry, matter balance, energy bounds, extent ceilings, and intermediate ranges;
5. compile trait predicates into canonical DAG nodes and topological ranks;
6. resolve typed effect targets and supersession selectors;
7. compile curves and bounded lookup tables;
8. compile environmental mechanics, lifecycle, behavior, evolution, and the closed world-generation schema/evaluator registry;
9. validate each scenario's reachable definitions, founder closure, and setup option ranges;
10. compile declared founder/test genomes; and
11. emit bounds used to size worker scratch and storage manifests.

Dense slot order is deterministic for a given pack but is not durable identity. Saves, protocols, diagnostics, and logical hashes use stable typed IDs. A runtime array may store a manifest once and values by dense slot only while that exact manifest is attached.

# DNA compilation

## Inputs and cache identity

```text
CanonicalGenomeKey:
    mechanicsHash
    acquiredTraitIds[]       // sorted ascending, unique
```

Founder genomes and accepted descendants use this same representation. A preview canonicalizes and compiles the proposed final trait set without allocating a world `GenomeId`, `SpeciesId`, or mutation transaction. If accepted, canonical commit interns or creates the logical genome and assigns world identity in its normal creation order.

Compiled phenotypes are cached by the complete `CanonicalGenomeKey`. The cache may be shared by immutable rule-pack instances, but cache presence, eviction, and compilation timing cannot affect semantic output. A cache miss and hit must return logically identical artifacts.

## Full deterministic compilation

```text
CompilePhenotype(compiledRulePack, canonicalGenomeKey):
    traits = resolve key.acquiredTraitIds in ascending TraitId order
    validate prerequisite closure, incompatibilities, and scenario-independent DNA rules
    contributions = expand typed effects with source TraitId and effect ordinal
    remove only explicitly superseded contributions
    group contributions by typed target
    compose base, additive, and multiplicative values in canonical order
    resolve exclusive choices and closed capability flags
    evaluate typed derived liabilities
    select enabled reactions, actions, senses, behaviors, and lifecycle modes
    compose named passive/use/reproduction cost channels
    compile activation and committed-quota requirements
    build storage capacities, desired inventories, process plans, and dense handles
    build tolerance, movement, interaction, evolution, pressure, and explanation profiles
    prove every field and scratch count within pack/compiler bounds
    hash the canonical logical phenotype and provenance
    return deeply immutable CompiledPhenotype
```

Compilation always starts from pack base definitions plus the final acquired set. It never mutates or copies an ancestor's already-rounded numerical profile. This is inexpensive at species scale and guarantees that identical DNA under one pack has identical output regardless of lineage history.

Every final field has exactly one compiled owner. Frequently consumed values are stored directly; ticks do not repeat trait composition, curve compilation, resource resolution, prerequisite traversal, or derived-liability calculation. Explanation provenance is produced in the same compilation and cites the contributions to the stored result.

## Runtime shape

The existing typed `CompiledPhenotype` groups values by storage, metabolism, tolerance, movement, behavior/sensing, lifecycle, interaction/defense, and evolution. This pass adds these invariants:

- every variable-length list has a compiler-proved maximum and stable semantic order;
- all resource/reaction references are pre-resolved to a typed handle plus retained stable ID;
- process order is `(priorityBand, processId)` and never trait acquisition order;
- flags accelerate closed capability checks but do not replace the canonical genome;
- arrays and nested records are read-only after publication;
- runtime-only phenotype slots are excluded from saves, protocols, RNG addresses, and logical hashes; and
- `canonicalCompiledHash` covers logical fields/provenance using stable IDs rather than physical slot numbers.

# Ownership and lifecycle

The first implementation keeps authoring records, validation, and compilation in cold-path namespaces within `Lyfe.Simulation`. This avoids premature project fragmentation while keeping namespace/API boundaries testable. Add one small executable:

```text
implementation/v1/
  src/
    Lyfe.Simulation/
      Rules/Authoring/
      Rules/Validation/
      Rules/Compilation/
      Rules/Runtime/
  tools/
    Lyfe.RuleTool/
  tests/
    Lyfe.Simulation.Tests/Rules/
```

`Lyfe.RuleTool` supports at least:

```text
validate <pack-root>
build <pack-root> --output <bundle>
schema <output-directory>
lock <pack-root> --check | --update
diff <old-pack> <new-pack>
explain-genome <pack> <trait-key>...
run-fixtures <pack>
```

The server loads one validated immutable flattened rule set—an official base pack plus zero or more installed balance mods—and discovers compatible installed world packs before creating/loading a world. World creation binds a scenario, exactly one complete world profile, and normalized options. The `WorldRunner` receives only `CompiledWorldRules` and immutable phenotype handles; it cannot access source, overlay, or world-pack files or reload content.

V1 release bundles include the base source JSON, the official world pack/profile, generated schemas, `rules.lock.json`, engine-vocabulary locks, generated mod/world-parameter catalogues, and manifest hashes. Local balance-mod and world-pack bundles carry their own source and package identity. A compiled binary cache is unnecessary initially. If startup compilation later becomes material, a cache must be tagged with every base/mod/world-profile/final compatibility identity, validated against the logical compiled hash, and remain disposable.

# Rule-pack diff and author workflow

The diff tool compares normalized identities rather than lines alone. It reports:

- definitions added, tombstoned, or identity-changed;
- semantic versus presentation-only edits;
- changed references, graph edges, reactions, curves, bounds, and scenarios;
- affected founder genomes and all known compiled phenotype fields/provenance;
- mechanics, presentation, registry, and compiled hash changes;
- save/replay compatibility classification; and
- required fixture changes and results.

Author workflow:

1. edit a small domain JSON file using generated schema assistance;
2. run format and validation;
3. update the registry lock only for deliberate additions/removals;
4. inspect normalized semantic diff and affected phenotype explanations;
5. run required subsystem and opening fixtures;
6. commit source, generated schema/lock changes, and intentionally updated golden results together.

Formatting may reorder object properties consistently but cannot reorder semantic arrays. A formatter never assigns IDs or changes numbers.

# Errors and operational behavior

- Server startup exposes pack failure as a typed configuration fault and does not create a world.
- Loading a save with an absent or mismatched compatibility identity returns an explicit incompatibility result; it does not substitute the latest pack.
- An accepted active-world command cannot reference authoring keys unknown to its pinned pack.
- Preview compilation returns deterministic structured diagnostics and has no authoritative side effects.
- A compiler exception is a defect, not an invalid-content diagnostic. It prevents pack publication and includes bounded source/definition context.
- Packs are treated as trusted local application content in v1, but parsers still enforce maximum file bytes, nesting depth, definition counts, collection lengths, string lengths, and compiled scratch bounds.
- Balance-mod and world-pack files are treated as untrusted bounded data even when installed locally. They cannot execute code, address arbitrary paths, select runtime types by name, or bypass validation. Future general content/code mods and distribution require the additional trust, signature, sandbox, namespace, and compatibility work in [MODDABILITY.md](MODDABILITY.md).

# Required tests

## Parsing and schema

- Generated schema is up to date with the C# authoring contract.
- Valid minimal and representative packs deserialize identically on supported platforms.
- Unknown/mis-cased/duplicate fields, comments, trailing commas, floating numbers, invalid UTF-8, unknown discriminators, and excessive depth/count/length fail with source locations.

## Identity and hashing

- Duplicate or zero IDs, duplicate keys, wrong typed references, removed-ID reuse, unexplained removal, and lock mismatch fail.
- File order, object-property order, whitespace, file splitting, and presentation-only edits do not change `mechanicsHash`.
- Every semantic field mutation changes `mechanicsHash`; registry changes alter `registryManifestHash`.
- Canonical hashes and logical compiled artifacts match on supported x64 and ARM64 environments.
- Golden canonical-writer vectors cover signed boundaries, strings, optional values, arrays, sets, and every record tag.

## Semantic validation

- Trait DAG, prerequisites, incompatibilities, supersession, reachability, and scenario closure exercise positive and negative fixtures.
- Every reaction and remnant/resource route passes matter/energy accounting and storage reachability checks.
- Numeric limits, curves, per-tick conversion, coupled claims, capacities, quotas, world generation, and scratch maxima reject boundary violations.
- Every validation diagnostic is stable under input enumeration and validator worker count.

## Compilation

- Dense manifests are complete, compatible, stable by ID, and reversible to logical IDs.
- Direct curve evaluation equals compiled lookup output throughout admitted test domains.
- The same final acquired set reached through different ancestors/order compiles to the same logical phenotype and provenance.
- Full recompilation, cache miss, and cache hit produce identical compiled hashes.
- Representative founder and advanced genomes match golden fields, process ordering, activation requirements, costs, and explanations.
- No tick kernel accepts authoring records, stable-key lookups, or unresolved rule references.

## Compatibility and integration

- A save resumes only with its exact simulation compatibility identity and produces the same continuation.
- Presentation-only pack changes may render new text while preserving authoritative hashes.
- Development overrides cannot claim the canonical pack identity.
- Balance overlays can reach only explicitly registered fields, conflicting writers fail independent of package order, and the final mod-set/rule identities survive save/load.
- Complete world profiles validate against the required API/registry, expose only registered bounded options, produce stable profile/world-rule hashes, and never depend on implicit inheritance or package enumeration order.
- The same world profile/options/seed produces identical fixed tiles, tile initialization, generation diagnostics, and start repair under the scalar and parallel generation paths.
- Rule-pack diff correctly classifies known compatible presentation edits and incompatible semantic/registry changes.
- Required opening, ecology, conservation, determinism, and performance fixtures run through the same compiled pack used by the engine.

# Initial implementation sequence

1. Add typed definition IDs/stable keys for the minimum slice, the active/tombstoned definition lock model, and stable namespaced mod/world-parameter keys and policies.
2. Define strict serializers and the smallest base-pack/world-pack records needed for one resource set, one founding reaction, one founder genome, one tile, and one scenario.
3. Implement normalization, canonical diagnostics, definition-reference resolution, the versioned rule/world hash writers, and golden vectors for that slice.
4. Compile those records into real dense resource/reaction handles and one complete `CompiledPhenotype`; do not introduce temporary kernel constants that bypass compilation.
5. Build a scalar one-tile tick walking skeleton that loads those compiled artifacts, applies one mass-balanced reaction, advances one organism, emits a logical change set, and produces a canonical world-state hash.
6. Add detached save/load and a direct authorized projection snapshot to prove the persistence and server boundaries before either format is made elaborate.
7. Expand schemas, registries, validators, phenotype profiles, and kernels together one subsystem at a time, keeping each new vertical slice executable and deterministic.
8. Add balance-overlay and alternate-world-package composition through the already-defined source-selection seam after the official source path works; mod/world packages remain first-class inputs, but polished tooling is not a prerequisite for the first tick.
9. Grow CLI workflows as their underlying capability lands: begin with `validate` and `run-fixtures`, then add lock/diff/describe/mod/world-preview commands.
10. Reach both founders and the complete opening rule set before calling the biological foundation complete, then run the existing authoring fixtures entirely through compiled values.

# Remaining implementation inputs

- Concrete permanent numeric assignments for definitions and those RNG, creation, command/event, protocol, and hash vocabularies that cross durable compatibility boundaries. Internal-only evaluator outcomes and change-journal discriminators begin as ordinary code enums.
- Concrete stable namespaced keys and policies for the first mod-parameter catalogue.
- Concrete stable namespaced keys for the closed world-option catalogue, numeric IDs only for registered generator algorithms that enter persisted engine semantics, and the first official world-pack manifest/profile key.
- Exact C# authoring/runtime record shapes as the first catalogue slice is encoded.
- Pack parser byte/depth/count/string limits and the diagnostic ceiling.
- Canonical hash record/field tag assignments and initial golden vectors.
- Deterministic release-bundle container details; semantic identity does not depend on them.
- The complete declared maxima for processes, reaction inputs/outputs, holdbacks, binding cohorts, traits, and curve points used to bound scratch storage.
- A policy for intentionally retiring a definition that remains referenced by old saves; v1 may require retaining the old complete pack rather than migrating it.

# Platform basis

.NET's [`System.Text.Json` source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) supports generated metadata contracts, while its documented defaults reject [comments and trailing commas](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/invalid-json). .NET also supplies [`JsonSchemaExporter`](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.schema.jsonschemaexporter?view=net-10.0) for serializer contract metadata. These capabilities are suitable for LYFE's cold-path typed loader without adding a second general serializer. [JSON Schema Draft 2020-12](https://json-schema.org/draft/2020-12) supplies the declared external schema vocabulary; LYFE's cross-definition semantic validation remains its own responsibility.

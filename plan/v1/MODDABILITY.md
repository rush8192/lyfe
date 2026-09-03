# Moddability, Balance Overrides, and World Packs

Status: first v1 mod-ready contract; local balance overlays and closed-schema world-generation packs belong in the implementation scaffold, while general content/code mods and distribution UX remain future work

Sources: [rule-pack authoring and compilation](RULE_PACK_AUTHORING_AND_COMPILATION.md), [configuration and balance](CONFIGURATION_AND_BALANCE.md), [technology decisions](TECHNOLOGY.md), [persistence and replay](PERSISTENCE_AND_REPLAY.md), [server and protocol](SERVER_AND_PROTOCOL.md), and [client](CLIENT.md).

# Purpose

Make LYFE's core simulation parameters straightforward to substitute without turning authoritative execution into an unsafe or order-dependent scripting environment. This document marks the data-defined rule-pack layer as the moddable boundary and defines the minimum v1 seams needed to preserve a broader future mod ecosystem.

The shipped LYFE rules are not privileged hard-coded behavior. They are the official base rule pack passed through the same loader, validators, normalizer, compiler, hashes, and runtime contracts used by a modified rule set.

# Recommendation

V1 should implement local/developer-grade **balance mods** as declarative overlays over an exact base rule pack and **world-generation packs** as separately selectable, complete recipes using a closed engine-supported schema. The official client does not need browsing, downloading, dependency resolution, or an in-game mod manager. A server operator or local launch configuration selects an installed balance-mod set and one world profile before world creation.

This modest implementation requirement is valuable now because it forces four healthy boundaries:

- balance values live in authored rule data rather than kernel literals;
- the compiler can compose sources before producing its one immutable runtime truth;
- saves and clients identify the actual final rules rather than assuming the official defaults; and
- validators distinguish biological/numeric safety from the official game's intended balance.

World packs are a deliberate bounded exception to deferring general content extension: they may add named world profiles, but only by configuring existing generation/environment vocabulary and referencing resources already present in the compiled biological rule set. Future general content mods may add resources, traits, reactions, scenarios, and art. Future code mods may add engine vocabulary or algorithms. Those require harder identity, trust, distribution, compatibility, and client-extension decisions and are not part of the first supported surface.

# Mod capability tiers

| Tier | Capability | V1 disposition |
| --- | --- | --- |
| Balance overlay | Replace explicitly marked scalar, enum-choice, bounded list/table, or curve values on existing definitions | Implement as a local/developer feature |
| World-generation pack | Add one or more complete named world/environment profiles using the closed v1 generator schema | Implement as a local/developer feature and world-setup input |
| Presentation overlay | Replace explicitly marked names, descriptions, layout hints, or visual metadata on existing definitions | Same pipeline; optional in the first UI |
| Full rule-pack fork | Copy and edit a complete pack, including structural definitions, then build it under a distinct pack identity | Supported by author tooling, not advertised as plug-and-play compatibility |
| Content extension | Add new resources, traits, reactions, behaviors, scenarios, assets, or protocol-visible content | Future |
| Engine/code mod | Add process kernels, trait-effect kinds, RNG operations, storage behavior, native code, or arbitrary scripts | Future and outside the data-only trust boundary |

Balance overlays and closed-schema world packs are the stable v1 data-mod promise. A full fork remains possible for developers because the source format is open and validated, but it does not receive the same compatibility/composition guarantees.

# World-generation packs

World composition is not merely another global balance override. World generation chooses the physical opportunity landscape in which the biological rules operate, so it receives a separate first-class package and identity.

```text
WorldGenerationPackManifest:
    worldPackId                    // globally namespaced stable string
    version
    displayName
    author?
    description?
    worldGenerationApiVersion
    compatibleBasePackId
    requiredRegistryManifestHash   // referenced resources/exposures must match
    testedBaseMechanicsHash?       // provenance/warning, not the sole validity gate
    profileFiles[]
```

Each pack contains one or more complete `WorldGenerationProfile` records. A profile includes, within the supported schema:

- default/minimum/maximum dimensions and any player-selectable bounded world options;
- elevation/noise, sea-level, terrain, coast, and mountain distributions;
- latitude, axial tilt, calendar, insolation, climate-normal, weather-variance, precipitation, surface-moisture, depth-light, and turbidity parameters;
- volcanic-province, pulse, gas-emission, atmospheric-background, exchange, and environmental-sink parameters;
- lithology, inorganic/macronutrient/micronutrient endowments, organic-background policy, and other tile-stock initialization parameters;
- spatial correlations, global reconciliation targets, seeded variance, and boundary source/sink profiles;
- start-region requirements, deterministic repair budgets, generation attempts, and profile-specific validation horizons; and
- presentation metadata explaining the world premise and the ecological pressures it is likely—but not guaranteed—to create.

World options address a permanent typed `WorldParameterId` defined by the same generated authoring/schema contract used for balance parameters. A profile supplies a default plus the range or choices the player may select. Setup values outside that advertised and engine-validated domain fail before generation. The normalized selected options enter `worldRulesHash`.

A world pack cannot add a generator stage, arbitrary noise implementation, resource, exposure type, gas species, topology rule, script, or executable asset. It can select only registered algorithms and existing definition keys supported by its declared API/registry identity. Fixed maps, custom generator code, and new tile/resource kinds remain future content/code-mod work.

The official primordial-Earth-like setup is itself an ordinary world-generation pack. V1 may initially ship only that polished pack, but tooling and setup discovery must permit additional official or externally installed packs without copying or editing the biological base rule pack.

Profiles are complete and do not inherit from or patch one another in v1. Author tools may clone and diff a profile for convenient balancing, but compilation sees one explicit resolved record. This avoids hidden defaults and load-order-dependent worlds.

World creation selects exactly one profile:

```text
WorldSourceSelection:
    compiledRuleSetIdentity
    worldPackPackageHash
    worldProfileKey
    selectedWorldOptions
```

The loader validates the world pack against the engine's world-generation API and the compiled rule set's registry manifest, then runs all structural, numeric, resource, start-feasibility, and representative generation checks. `testedBaseMechanicsHash` can produce a compatibility warning when the author calibrated against different balance rules, but successful whole-combination validation—not that advisory value—decides whether the profile is usable. This lets world packs and disjoint balance mods coexist without pretending their emergent outcomes are unchanged.

The selected world pack/profile/options compile into `CompiledWorldRules`. Generation and every later climate, source, sink, or environmental update read only that final artifact. The generated tile state is authoritative thereafter; a world-pack parameter is never consulted as a second mutable runtime configuration.

# Explicit moddable surface

Every property in the C# authoring contract has one declared policy:

```text
ModPolicy:
    LockedIdentity
    FullPackOnly
    BalanceOverride
    PresentationOverride
```

The default is `FullPackOnly`. Moddability is opt-in rather than inferred merely because a property happens to be numeric.

## `LockedIdentity`

An overlay can never change:

- a definition's typed numeric ID or stable key;
- base pack ID, compiler/API version, registry locks, or tombstones;
- engine-vocabulary IDs, RNG domains/address schemas, outcome/event IDs, or canonical hash field tags;
- the type/discriminator of a definition or effect; or
- the target base-pack identity.

## `FullPackOnly`

Initial examples include:

- resource elemental composition and biological form;
- reaction input/output topology and stoichiometry;
- permitted reservoirs and storage-group membership;
- trait prerequisites, incompatibilities, effect kinds, capability grants, and supersession topology;
- process-kernel kind, ledger ownership, phase ownership, and rounding algorithm;
- stable world topology semantics; and
- fields whose change could require a different physical storage schema or protocol meaning.

These values remain data-defined and can be changed in a separately identified full rule-pack fork. They are excluded from simple overlays because changing them can invalidate broad invariants or require structural client/engine support.

## `BalanceOverride`

The initial target surface includes validated values such as:

- biological uptake, reaction-success, digestion, metabolic, and remnant-decay rates;
- energy yields/opportunities, maintenance/use costs, capacities, throughput ceilings, cooldowns, and binding durations;
- environmental preferred/soft/hard thresholds and stress/death curve parameters;
- mutation prices, change complexity, mutation-income coefficients, autonomous-evolution weights, and refractory periods;
- movement distances/costs, interaction radii, predation attack/defense/probability parameters, and behavior thresholds;
- lifecycle, reproduction, senescence, dormancy, digestion, and recycling parameters;
- scenario final date, speed presets, starting populations, permitted setup packages, and biological validation horizons; and
- points in curves explicitly exposed as balance curves.

Each field retains its unit, numeric/collection bounds, cross-field validators, and conservation constraints. Marking a field moddable permits replacement; it does not relax its validity rules.

Reaction stoichiometry is deliberately not in the first balance surface. Authors who want alternate chemistry use a full pack fork until content-extension identity and validation are mature.

Concrete geography, climate, atmospheric-background, abiotic source/sink/exchange, tile-endowment, and start-repair values belong to the selected complete world profile rather than a base-rule balance overlay. This keeps the environmental recipe coherent and prevents two layers from claiming the same final value.

## `PresentationOverride`

Names, descriptions, author credits, layout hints, visual descriptors, and other explicitly non-authoritative presentation fields may be replaced without changing simulation mechanics. Presentation overlays still receive their own package/mod-set identity and follow client visibility and asset-safety rules.

# Mod parameter registry

Overlays do not address arbitrary JSON Pointer paths. The authoring contract generates a stable registry of permitted parameters:

```text
ModParameterDefinition:
    id: ModParameterId              // explicit permanent UInt32; zero invalid
    stableKey                       // e.g. trait.mutation-point-cost
    targetDefinitionKind
    modPolicy
    valueShape                      // integer, choice, integer list, curve points, record
    unit
    minimum?
    maximum?
    maximumItemCount?
    introducedCompilerVersion
```

The C# property and this registry are one generated contract: an explicitly annotated authoring member declares its `ModParameterId`, stable key, policy, shape, and unit. Schema export includes `x-lyfe-mod-policy`, `x-lyfe-mod-parameter`, unit, and bounds. `Lyfe.RuleTool describe-mod-surface` emits the complete human-readable catalogue.

IDs and keys are locked and tombstoned like other engine vocabulary. Renaming a C# property does not change the mod parameter identity. Removing or reinterpreting a parameter is a compatibility decision.

# Balance-mod package

```text
mods/example-balance/
  mod.json
  overrides.json
```

```text
BalanceModManifest:
    modId                         // globally namespaced stable string
    version
    displayName
    author?
    description?
    targetBasePackId
    targetBaseMechanicsHash
    requiredModPackageHashes[]
    overrideFiles[]
```

```text
BalanceOverride:
    targetDefinitionKind
    targetStableKey
    modParameterKey
    expectedBaseValueHash
    replacementValue             // shape checked by ModParameterDefinition
```

`modId` should use a reverse-domain or similarly collision-resistant namespace controlled by its author. A mod package is identified by the SHA-256 hash of its canonical manifest and overrides, not merely its display version.

`expectedBaseValueHash` hashes the normalized unmodified target field through the canonical rule writer. It prevents an overlay authored for one value from silently landing on a different but coincidentally accepted base definition. The exact target base mechanics hash provides the broader compatibility gate.

V1 balance overlays support replacement only. They do not add/remove definitions or list members, edit identities, splice arrays, invoke arithmetic expressions, read environment variables, include files outside the package, fetch network resources, or execute code. A complete curve/list/record replacement is atomic and validated against its declared shape.

# Mod-set composition

World creation selects one base pack plus zero or more installed balance mods:

```text
RuleSourceSet:
    basePack
    mods[]
```

Composition occurs after the base pack is structurally parsed and identity-locked, but before cross-definition validation, normalization, hashing, global compilation, scenario binding, or phenotype compilation:

```text
ComposeRuleSourceSet(basePack, mods):
    validate every package hash and exact base target
    validate exact dependency package hashes
    sort mods by canonical modId, version, package hash
    collect overrides in canonical target/parameter order
    reject duplicate writes to the same target definition + parameter
    verify parameter policy, shape, unit, and expected base value hash
    apply replacements to an immutable authored copy
    return flattened authored pack + ModSetProvenance
```

Two mods that write the same `(targetDefinitionKind, targetStableKey, modParameterKey)` are incompatible in v1. World setup fails with a conflict report; load order never decides a winner. An author may instead publish one combined replacement mod containing the desired merged value, and users select it in place of the conflicting originals. Automatic masking and compatibility patches are deferred.

Because simultaneous mods must write disjoint fields, package sorting is diagnostic/canonical rather than a precedence rule. Dependencies name exact package hashes; semantic-version ranges and online dependency resolution are deferred.

The flattened pack then goes through every ordinary semantic validator. No overlay bypasses mass/energy accounting, range proof, graph validity, storage reachability, scratch limits, deterministic ordering, or scenario feasibility.

# One final source of runtime truth

The base source and overlay provenance remain available for tools and explanation, but the authoritative runtime reads only the final immutable `CompiledRulePack`, `CompiledWorldRules`, and `CompiledPhenotype` artifacts.

```text
official base JSON
      + validated balance overlays
      -> flattened authored pack
      -> resolved and normalized rule pack
      -> final mechanics/presentation/mod-set hashes
      -> immutable compiled artifacts
      -> world
```

A tick cannot ask whether a value came from a mod, branch on “modified mode,” consult an overlay, or fall back to an official default. The final compiled number is the sole operational value. Explanation provenance may show the base and replacement to an authorized user without creating a second calculation path.

An architecture test should reject gameplay/balance literals in tick kernels unless the literal is an algorithmic identity such as zero/one, a fixed numeric scale, a proved dimensional conversion, or a documented engine compatibility constant. Ordinary rates, thresholds, probabilities, costs, capacities, and scenario values must arrive through compiled rules.

# Identity, saves, and replay

```text
ModPackageIdentity:
    modId
    declaredVersion
    packageHash

ModSetIdentity:
    basePackId
    baseMechanicsHash
    mods[] in canonical order
    modSetHash
    finalMechanicsHash
    finalPresentationHash
    finalCompiledArtifactHash

WorldGenerationIdentity:
    worldPackId
    declaredVersion
    packageHash
    worldProfileKey
    normalizedSelectedOptionsHash
    compiledWorldProfileHash
```

`modSetHash` covers package identities and override provenance. `finalMechanicsHash` covers the flattened normalized semantics. Two differently packaged mod sets may theoretically produce the same mechanics hash, but their provenance remains distinguishable.

Every save/checkpoint/replay records the complete `ModSetIdentity` and `WorldGenerationIdentity` as part of simulation compatibility. Loading requires the exact final mechanics/compiler/RNG/world-rules compatibility plus the balance-mod and world-pack provenance required by policy. It never substitutes an updated package with the same name/version.

The first local save format may require the exact base/mod/world packages to remain installed. The save container must reserve a way to embed or accompany the small data-only rule sources and locks for portability; whether v1 embeds them by default belongs to the persistence format decision. A post-v1 workshop/distribution service cannot be required to reopen a local save.

Changing the mod set or world profile of an existing world is not v1 functionality. A future conversion tool would create a new auditable world compatibility boundary rather than hot-patching active state.

# Validation and certification

Rule checks have three classes:

| Class | Meaning for mods |
| --- | --- |
| Structural/safety invariant | Always mandatory; any failure rejects the selected combination |
| Determinism/conservation fixture | Always mandatory where applicable; any failure rejects the selected combination |
| Official balance/experience fixture | Run and report against the official combination, but divergence marks the result non-canonical rather than structurally invalid |

A successfully compiled modded pack or external world profile is `ModifiedValidated`, not “officially balanced.” It may additionally publish its own named fixture results in the future, but cannot claim the official certification identity. `OfficialCanonical` applies only to an explicitly registered official combination of base pack, empty/approved mod set, world pack/profile, and options—not merely to an official component used in a different combination.

```text
RuleSetCertification:
    OfficialCanonical
    ModifiedValidated
    FullForkValidated
    Invalid
```

The server and client show modified status in world metadata and result/replay screens. This is provenance, not a difficulty judgment. Achievement, leaderboard, and competitive eligibility policies are future gameplay/service decisions.

# Server and multiplayer boundary

- V1 balance-mod and world-generation packages are installed by the local user/server operator before world creation; browser clients cannot upload rule files or paths.
- World creation accepts only a server-known validated `ModSetId` and `WorldProfileRef`, never an arbitrary filesystem location supplied over HTTP.
- The server remains authoritative and sends the world/mod-set/world-profile identity plus actor-authorized rule/explanation metadata needed by the client.
- A client that cannot represent the required protocol/engine vocabulary refuses the world explicitly; it does not display a modified value as the official default.
- Future multiplayer servers select and pin one mod set and world profile for the world. Connecting clients acknowledge those exact identities, but do not need to execute the simulation or trust local copies for authority.
- Online mod discovery, downloading, signatures, malware scanning, licensing, moderation, dependency solving, and server policy are future service work.

Data-only does not mean parser input is trusted. File sizes, paths, nesting, strings, definitions, override counts, collection replacements, and compiled bounds retain the limits in the rule-pack contract.

# Client consequences

The thin client should already obtain final authored/compiled explanation metadata from the server. To preserve mod readiness:

- do not hard-code official mutation prices, reaction yields, tolerance bands, capacities, cooldowns, scenario defaults, or curve values in TypeScript;
- display final server-provided values and units, with optional base-versus-modified provenance;
- include `modified`, mod-set identity, world-pack/profile/options identity, and certification in setup/world/result metadata;
- list compatible installed world profiles, their bounded options, authorship, premise, certification, and validation warnings during setup;
- preview consequences as authored expectations and actor-authorized generated summaries, not promises of particular evolutionary outcomes or a way to reveal hidden tile state before exploration;
- cache definition metadata under the final presentation/mechanics/world-profile identity rather than one global official catalogue;
- provide a generic presentation fallback for known engine vocabulary even when a value combination differs from the official pack; and
- never download/execute JavaScript, shaders, HTML, or other code from a balance mod or world pack.

Presentation assets and genuinely new definition kinds belong to future content-mod planning.

# Author and user workflow

Minimum CLI flow:

```text
lyfe-rules describe-mod-surface <base-pack>
lyfe-rules new-balance-mod <base-pack> --id <namespaced-id>
lyfe-rules validate-mod <base-pack> <mod>...
lyfe-rules diff-modset <base-pack> <mod>...
lyfe-rules build-modset <base-pack> <mod>... --output <bundle>
lyfe-rules validate-world-pack <compiled-rule-set> <world-pack>
lyfe-rules diff-world-profile <world-pack-a>:<profile> <world-pack-b>:<profile>
lyfe-rules generate-preview <compiled-rule-set> <world-pack>:<profile> --seed <seed>
lyfe-rules run-fixtures <compiled-world-rules>
```

`diff-modset` reports every base/replacement value, unit, owning definition, downstream compiled phenotype/curve/scenario changes, hash change, conflict, validator result, and official balance-fixture divergence. It is the primary safeguard against a one-line override having opaque systemic effects.

The local server lists installed validated mod sets and compatible world profiles. World setup may expose a simple pre-launch selection or rely on launch configuration initially. Once created, the world shows both pinned identities and cannot change either.

# Required tests

- Every `BalanceOverride`/`PresentationOverride` member has one permanent mod-parameter identity, declared unit/shape/bounds, generated schema annotation, and lock entry.
- Unannotated fields default to `FullPackOnly`; an overlay cannot reach locked/full-pack-only fields through alternate JSON spelling or nested structure.
- Unknown targets/parameters, wrong types/units, stale base/value hashes, identity edits, duplicate writers, missing dependencies, path escapes, and excessive values fail deterministically.
- Disjoint mod package enumeration order produces the same flattened model, provenance order, hashes, diagnostics, and compiled artifacts.
- Every modded pack reruns structural, range, graph, matter/energy, deterministic, storage, and scratch-bound validation.
- Official balance divergence changes certification/reporting but does not bypass mandatory invariants or automatically reject an otherwise valid balance mod.
- A modded scalar/curve reaches every owning compiled profile and client explanation, while tick kernels contain no fallback official value.
- Save/load/replay succeeds with the exact mod-set identity, accepts installed package enumeration in any order after canonicalization, and fails clearly for missing, stale, or hash-mismatched packages or a noncanonical/tampered recorded identity.
- Client caches cannot reuse definition metadata across different final identities.
- Protocol input cannot select arbitrary local paths or upload executable content.
- Official zero-mod worlds compile to the same semantic artifacts whether represented as an empty mod set or the direct base-pack path.
- Every world profile is complete: omitting a required value fails instead of reading an inherited profile or engine balance default.
- Unknown generator algorithms, resource references, option IDs, or registry/API identities fail before generation; external packages cannot select code by type name or path.
- The same rule set, world-package hash, profile, normalized options, and seed generate identical fixed tiles, start repair, diagnostics, and compiled world identity across enumeration order, worker count, save/load, and supported hosts.
- Changing any effective world-generation or tile-initialization parameter changes the compiled world-profile/world-rules identity, even if a particular seed happens to generate the same visible map.
- Representative seed suites report geography, climate, resource, starting-region, and ecological-opportunity distributions per profile; mandatory safety/feasibility failures reject while official balance-target divergence changes certification.

# Implementation changes to make now

1. Add `ModPolicy`, permanent `ModParameterId`, and closed `WorldParameterId`/generator-algorithm metadata to authoring members as the records are created.
2. Generate the mod/world-parameter schema annotations, registry locks, `describe-mod-surface`, and world-pack schema alongside ordinary rule schemas.
3. Make the rule compiler accept `RuleSourceSet` rather than a single implicit directory, and make world compilation accept one explicit `WorldSourceSelection`.
4. Implement immutable replacement and conflict detection before normalization; do not mutate deserialized base records in place.
5. Split mandatory invariant/determinism fixtures from official balance/experience certification.
6. Add `ModSetIdentity` and `WorldGenerationIdentity` to compiled rules, world metadata, saves, state-hash context, replay checks, diagnostics, and protocol scaffolding.
7. Ensure client-facing definitions and explanations come from the server's final compiled identity rather than TypeScript constants.
8. Provide CLI-based local selection and validation; defer polished discovery/installation UI.
9. Move concrete geography, climate, atmospheric-background, tile-endowment, and start-repair settings into the official complete world profile; leave only generator algorithms, typed parameter contracts, and hard safety bounds in engine/base-rule ownership.

# Deferred decisions

- New-definition identity and namespace allocation across independently authored general content mods; closed-schema world pack/profile identities are the bounded v1 exception.
- Cross-mod references and dependency/version solving beyond exact package hashes.
- Deliberate same-field conflict resolution or user-authored load-order semantics.
- Asset packaging, localization merging, custom visuals, and presentation sandboxing.
- Code/plugin APIs, process-kernel extension, WASM or other executable sandboxing.
- Package signatures, trust prompts, distribution/workshop service, licensing, moderation, and automatic updates.
- Save embedding versus sidecar packaging defaults.
- Multiplayer server discovery/filtering and achievement/leaderboard policy.

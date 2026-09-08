# LYFE v1 Implementation Backlog

Status: active source of truth for implementation sequencing and completion

Plan authority: the documents in [`plan/v1`](../../plan/v1/README.md) define intended behavior and architecture. This backlog is the authoritative record of what implementation work is done, active, ready, or deferred.

# Working agreement

- Every implementation change must reference one backlog ID and update its status or acceptance checklist in the same change.
- `Done` means the code and required automated acceptance checks exist and pass. Documentation alone does not complete an implementation item unless the item is explicitly a decision artifact.
- `Blocked` requires a named missing decision, dependency, tool, or external capability. The note must state what removes the block.
- New work is added here before or with implementation; competing private TODO lists are not sources of truth.
- Product or architecture changes update the relevant plan first, then this backlog. Balance-only discoveries may update both in one change.
- Items should remain vertical and demonstrable. A subsystem is not marked done because its types exist while its real compile, tick, persistence, projection, or client path is absent.
- Completed items remain in this file as a lightweight project history.

Status values are `Done`, `In progress`, `Ready`, `Planned`, `Blocked`, and `Deferred`. Only one item should normally be `In progress` at a time.

# Current focus

Stage A and the complete executable opening through `OPENING-200` are complete.
`UI-200` is in progress: its first slice now carries save-stable authoritative organism
journey events through actor projection, protobuf snapshots/deltas, atomic client apply,
configurable map pulses, and organism inspection. Its retention slice keeps exact landmarks,
rolls routine uptake from a 168-hour window into sparse daily summaries, and publishes
worsening stress-band crossings plus actual behavior transitions without per-tick noise.
The mutation-control slice now exposes the controlled species' compiled
trait catalogue and occupied tiles, prerequisite-closing client selection, authoritative
preview, and idempotent apply with optimistic genome/evolution identity. The first resource
diagnostic publishes moddable compound names/forms plus sparse source, sink, neighbor-exchange,
organism-uptake, and organism-release totals directly from the balanced ledger. A save-stable
rolling history retains each exact interval for up to 168 simulated hours; only live tiles receive
exact stocks and current/history flows. The client reconstructs stock history from current stock
plus sparse net changes and shows gross in/out and net without
conflating compound and elemental quantities, ties organism resource pressure to the selected
live tile, and hides absent compounds by default. Per-organism last-tick acquisition evidence
now reports exact requested/granted quantities per named compound and distinguishes an actual
tile-supply constraint from scavenging claim contention; coupled co-inputs are reduced without
being falsely named as the limiting material. This per-organism evidence is intentionally transient
across save restoration. The same last-tick channel now
reports why recurring external energy capture emitted no resource request: no compiled pathway,
zero accessible light/environmental opportunity, or less reserve room than one reaction extent
requires. Conservation behavior deliberately remains absent from this list because it cannot
suppress useful passive capture. Rich activation warnings, proposal comparison/goals, compacted
long-horizon resource histories, species/reaction contributors, opportunistic scavenging and other action-gate
failures, richer event prose/linking, cold historical segments, and the hosted command queue remain.

# Foundation and developer experience

| ID | Status | Work | Depends on | Acceptance |
| --- | --- | --- | --- | --- |
| FND-001 | Done | Pin .NET/Node toolchains and establish server, simulation, protocol, client, tests, scenarios, benchmarks, and rule-tool boundaries | — | Full solution and client build; dependency-direction tests pass |
| FND-002 | Done | Add typed `WorldId`, exclusive foundation runner, immutable snapshot, revision, and deterministic scaffold hash | FND-001 | Repeated runs produce the same foundation hash; invalid IDs fail |
| FND-003 | Done | Add minimal ASP.NET server boundary | FND-002 | Health, capabilities, and active-world endpoints pass a live smoke test |
| FND-004 | Done | Add replaceable React/PixiJS client shell | FND-003 | Client test and production build pass; HTTP 64-bit identifiers remain strings/`bigint` safe |
| FND-005 | Done | Add headless scenario, benchmark, architecture tests, and developer commands | FND-001 | All hosts build and the scenario emits deterministic JSON |
| FND-010 | Done | Enforce foundation determinism rules and canonical primitives | FND-001 | Compiled-assembly tests reject wall-clock/global RNG/filesystem/network use in simulation; canonical integer, boolean, byte, and NFC UTF-8 writers have golden vectors; foundation hash is frozen |
| DX-020 | Planned | Add continuous integration for formatting, build, tests, protocol checks, deterministic scenario, and bounded benchmark smoke | FND-010 | Clean checkout passes on supported Linux architecture matrix and retains useful failure artifacts |
| DX-030 | Planned | Define test taxonomy, fixture naming, metric names, and failure-injection conventions | FND-005 | Each planned test layer maps to a project/command and first fixtures use the convention |

# Packaging and operations

| ID | Status | Work | Depends on | Acceptance |
| --- | --- | --- | --- | --- |
| OPS-001 | Done | Decide the server deployment and portability boundary | FND-001 | [`DEPLOYMENT_AND_PORTABILITY.md`](../../plan/v1/DEPLOYMENT_AND_PORTABILITY.md) records Docker/OCI decision, limits, and acceptance gates |
| OPS-002 | Done | Add reference multi-stage server Dockerfile, Compose service, data-volume contract, and ignore rules | OPS-001 | Files are syntactically reviewable and documented; runtime validation is tracked separately |
| OPS-003 | Done | Build and smoke-test the server image on `linux/amd64` and `linux/arm64` | OPS-002 | Compose resolution, native arm64 and emulated amd64 builds/runs, non-root/read-only inspection, three HTTP probes, identical foundation hashes, and graceful exit code `0` passed locally |
| OPS-010 | Planned | Add image provenance and release pinning | DX-020, OPS-003 | CI records source revision and base/final digests, generates an SBOM, and publishes immutable version tags |
| OPS-020 | Planned | Implement durable container save location and replacement test | SAVE-120, OPS-003 | Save under `/var/lib/lyfe` survives removal/recreation and resumes with the expected hash |

# Stage A — first real deterministic vertical slice

| ID | Status | Work | Depends on | Acceptance |
| --- | --- | --- | --- | --- |
| CONTENT-100 | Done | Define minimal strict rule/world authoring records and permanent IDs | FND-010 | Typed nonzero IDs, strict source-generated JSON, bounded explicit manifests, registry/tombstone validation, deterministic diagnostics, CLI validation, and tiny official mass-balanced rule/world bundles pass positive and negative tests |
| CONTENT-110 | Done | Compile one resource set, founding reaction, founder genome, tile profile, and scenario into immutable runtime artifacts | CONTENT-100 | Canonically normalized definitions compile to typed IDs, deterministic dense handles/vectors, a mass-balanced reaction, founder phenotype, scenario, and bound world rules; separate mechanics/presentation/registry/compiled/world hashes have golden vectors and semantic/order invariance tests |
| STATE-100 | Done | Implement first tile, species, organism, resource, and compiled-phenotype stores with stable IDs and typed mutators | CONTENT-110 | `100` and `1,000` founder fixtures load into tile-partitioned 256-row chunks; monotonic IDs, private dense locators, resource-major tile stocks, phenotype indirection, swap removal, relocation, canonical phase changes, foreign-builder rejection, and debug untracked-write detection pass |
| RNG-100 | Done | Implement versioned `Philox4x64-10` semantic keyed randomness | FND-010, STATE-100 | Official and independent-reference golden vectors, a frozen 24-domain manifest (including LIFE-200 offspring placement), exact integer conversions, misuse failures, and call/enumeration-order invariance pass in Debug and Release |
| TICK-100 | Done | Replace the foundation increment with the scalar phase/evaluate/preflight/commit pipeline | STATE-100, RNG-100 | Compiled one-hour rules and a keyed 100-founder state run all 12 phases; phase 3 advances canonical organism ages through sealed view/outcome/preflight/typed commit; preflight overflow applies nothing; injected post-mutation and final-validation faults publish nothing |
| LEDGER-100 | Done | Implement the first mass-balanced resource transaction and reconciliation oracle | TICK-100 | Phase 5 emits immutable one-extent founder capture intents; phase 6 resolves coupled H₂/CO₂ scarcity by stable keyed rank, commits tile/reserve/boundary/heat entries as exact reaction bundles, and verifies CHNOPS, energy, and promised post-state balances. Abundance, scarcity, insufficiency, capacity, malformed-bundle, repeatability, Debug, and Release fixtures pass |
| HASH-100 | Done | Implement canonical `WorldStateHashV1` field tags and logical ordering | STATE-100, LEDGER-100 | SHA-256 covers the versioned compatibility preamble, root seed, boundary/time state, next IDs, canonical tile resources, genomes, species, organisms, and completed-tick ledger. Golden creation/first-tick vectors, repeated runs, state and counter sensitivity, canonical ledger ordering, and round-trip migration row-permutation invariance pass |
| CHANGE-100 | Done | Produce a bounded typed `PhaseChangeSet` and merged `TickChangeSet` from authoritative mutators | TICK-100 | Phase journals merge into stable-ID store changes with explicit lifecycle/relocation precedence and canonical ledger references; provisional per-tick operation limits fail closed; real age/resource mutations have exact merged evidence; cancellation, invalid-sequence, limit, repeatability, and frozen `TickChangeInspectorV1` tests pass |
| PROJ-100 | Done | Implement a direct actor-authorized full projection oracle | CHANGE-100 | A detached stable-ID publication snapshot feeds a pure server projector; controlled-species occupancy derives live tiles, stored discovery input derives reduced tiles, and all others remain unknown. Canonical-order, source-permutation, detachment, invalid-knowledge, distinct-shape, hidden-field, and no-dense-location fixtures pass; authoritative world hashes are deliberately absent from actor output |
| SAVE-100 | Done | Define v1 save envelope, checksums, atomic replacement, and corruption errors | HASH-100 | Fixed binary preamble and canonical metadata, independent checksum domains, bounds, compatibility rejection before payload reads, frozen metadata checksum, typed damage fixtures, and injected pre-replace failure preserving the prior save pass; no placeholder save endpoint is exposed |
| SAVE-120 | Done | Capture detached boundary saves; reload and rebuild indexes/materializations | SAVE-100, PROJ-100 | Canonical bounded logical payload, exact compatibility-before-payload load, reconstructed stores/locators/ledger, recorded-hash verification, and identical next-tick continuation pass; detached snapshots retain no mutable world arrays |
| PROTO-100 | Done | Select/pin C# and TypeScript generators and implement the full-projection contract | PROJ-100 | `Google.Protobuf 3.36.1`, `Grpc.Tools 2.83.0`, Buf CLI `1.72.0`, and `protoc-gen-es 2.14.1` generate both languages from `lyfe.v1`; exact 64-bit fields remain C# integers and TypeScript `bigint` |
| CLIENT-100 | Done | Replace the placeholder world read with the generated full projection | PROTO-100 | Browser decodes the real binary snapshot and renders the first tile plus all `100` organisms without importing server implementation code or receiving the authoritative state hash |
| DELTA-100 | Done | Add one absolute delta, atomic client apply, revision checks, and full resynchronization | CHANGE-100, CLIENT-100 | Whole-tile/species replacements produce the same normalized cache as a fresh projection; duplicates are ignored and gaps, wrong identity/rules, out-of-order, ambiguous, or invalid batches preserve the prior cache and require resync |
| STAGE-A | Done | Demonstrate the complete first vertical slice | DELTA-100, SAVE-120, LEDGER-100 | One test composes save/reload, exact next-tick continuation, actor projection, generated binary snapshot, and absolute next-tick delta; full automated suites and a live browser/server smoke pass |

# Stage B — playable opening and representative 10,000-organism world

| ID | Status | Work | Depends on | Acceptance |
| --- | --- | --- | --- | --- |
| WORLD-200 | Done | Implement wrapped-x grid generation, climate/time-of-year state, volcanic start eligibility, and first world profile | STAGE-A | Strict generated-profile compilation and frozen identities; deterministic fixed-point terrain/climate across a 32-seed matrix; `32 × 17` x-wrapped/y-bounded maps stay within aquatic/volcanic bands; four disjoint adjacent start pairs pass recorded eight-tile repair, depth, temperature, activity, and daily-light checks; the frozen Stage-A profile hashes still pass |
| GAS-200 | Done | Implement tile atmospheric stocks, mixing, volcanic sources, and sinks | WORLD-200 | Strict world-profile gas rules compile eight permanent gas resources, two volcanic profiles, accessibility classes, modifiers, and diffuse sources; phase 2 performs source → attrition → simultaneous canonical-edge exchange with checked wide arithmetic and persisted tile/edge remainders; all flows are exact boundary/tile ledger transactions; x-only topology, aquatic access, save/load continuation, and a 12,000-hour 0.8-activity hydrogen field meet the calibrated center/neighbor bands |
| ORG-200 | Done | Implement calibrated foundation internal state, computed health, intrinsic death evidence, and compiled storage/allocation contract | STAGE-A | Strict rule content compiles physiology/capacities and changes mechanics identity; the sole fixed-point builder materializes named intrinsic/end health with factor explanations into dense rows, the then-current hash/save schema, publication, and wire projection; reserve admission/commit enforces compiled capacity; death assessment retains every nonzero cause and random evidence for LIFE-200's atomic death commit; calibrated age/temperature, multi-cause, capacity, persistence, projection, and generated-start-climate fixtures pass |
| SPACE-200 | Done | Implement coordinates, body radius, Brownian/drift movement, index, local interactions, and edge migration | WORLD-200, ORG-200 | Compiled spatial profiles drive cube-root body radius, a frozen zero-mean Brownian table, terrestrial scaling, paid capped active vectors, one-edge migration with x wrap/y bounds, radius-aware same-tile queries through rebuildable `16 × 16` bins, reflected offspring placement, and client-visible organism/remnant radii; exact-range, index-edge, control-probability, deterministic-address, protocol, and full-suite fixtures pass. Destination compatibility is intentionally neutral until climate/habitat responses and advanced locomotion traits land in later slices. |
| LIFE-200 | Done | Implement deterministic reproduction with cooldown jitter, senescence death commit, remains, local scavenging/digestion, decay, and recycling | ORG-200 | Rule-compiled lifecycle profiles drive tick-duration-aware cooldowns; fission conserves matter and defers newborn action; every positive death probability is recorded with one actual cause; death atomically creates one remnant; radius-aware same-tile range claims (indexed by `SPACE-200`), cooldowns, fixed-point decay, exact particulate digestion, persistence/hash schema 4, projection/protocol fields, and continuation fixtures pass |
| BEHAVIOR-200 | Done | Implement persistent local behavior and resource-pressure regulation | LIFE-200 | Strict moddable profiles compile baseline/conservation capability and calibrated thresholds while founders retain only `BaselineActivity`; phase 9 uses organism-local reserve plus completed acquisition coverage, a one-day fixed-point EMA, `0.50/0.65` hysteresis, critical `0.20` entry, and four-hour dwell; conservation suppresses next-tick optional locomotion and reproduction without suppressing Brownian drift or useful capture; typed target-compatible state, pressure memory, hash/save schema 5, individual wire fields, and authorized per-tile/exact-or-observed species distributions round-trip and pass deterministic fixtures |
| EVO-200 | Done | Implement mutation income, trait trees, compiled phenotype updates, speciation, lineage, and autonomous opportunity scoring | BEHAVIOR-200 | A compiler-owned and fingerprinted effective-population table drives fixed-point phase-10 income equally for controlled and autonomous species; strict prerequisite-bearing traits recompile immutable genomes/phenotypes; validated atomic speciation uses deterministic founders and exact 50/20/8/3% per-tile fractions, duplicates post-price balances/remainders, preserves organism matter/state, records lineage/events, survives save/load continuation, and exposes controlled evolution through absolute projections/deltas; prerequisite-closed pressure scoring passes frozen fixtures |
| GAME-200 | Done | Implement survival and sandbox setup/control/loss state machines | EVO-200, DELTA-100 | Typed setup creates one sandbox root or two independent adjacent survival roots; sandbox supports optimistic-revision control transfer and persistent mutation locks; survival follows every player descendant and loses on controlled-species extinction while sandbox loses only on total extinction; terminal state blocks further ticks/evolution and mode/root/control/lock/outcome state is covered by journals, `WorldStateHashV9`, save schema 9, authorized projections, protobuf snapshots/deltas, and atomic client apply. Multiplayer ownership remains out of the world runner. |
| OPENING-200 | Done | Implement hydrogen and sulfide founder paths plus bounded starting trade-off packages | GAME-200, GAS-200 | Two founder genomes and three allocations compile six distinct choices; survival fixes the opposite metabolism to Balanced. Exact capture, maintenance, needs-only CHNOPS assembly, reserve-floor protection, and failure death/remnants are executable. Fourteen tile/organism/remnant micronutrients use fixed-width persistent committed/free inventories; founders debit one committed quota, keyed needs-only uptake fills at most one quantum per organism-hour opportunity, reproduction requires and transfers one complete extra quota without creating matter, and death/terminal decay recycle every unit. Schema-9 save/hash/publication/protobuf/client paths preserve the state. The generated-world matrix passed all six choices across 64 seeds: `384/384` reproduced, retained at least `95/100` founders at first reproduction, and reached 40 MP within 720 hours. |
| UI-200 | In progress | Add knowledge-limited map, organism/species inspection, lifecycle activity pulses and journey logs, mutation decisions, behavior summaries, and resource-flow views | OPENING-200 | Player can observe, diagnose, choose, and review consequences without hidden-state leakage; completed birth/reproduction, feeding, uptake, stress, migration/state-transition, and death facts drive configurable accessible on-map icons with bounded wall-clock fading; exact landmarks plus compacted routine summaries provide a save-stable authorized journey; live tiles expose named exact compound stocks, gross last-tick ledger flow categories, and a save-stable 168-hour exact sparse-flow history used to reconstruct stock trends without leaking non-live tile values; selected live organisms expose exact last-tick compound demand/grant plus bounded engine-authored capture, scavenging, biomass-growth, and reproduction blockers with applicable resource, threshold, capacity, action-energy, behavior, and cooldown evidence |
| PERF-200 | Planned | Profile the full default-grid opening around 10,000 organisms | UI-200 | Stage-B workload records tick, memory, GC, save, projection, wire, apply, and frame metrics |
| STAGE-B | Planned | Demonstrate a playable survival opening and free sandbox | PERF-200 | Coupled opening/lifecycle/resource scenarios and the end-to-end player loop pass at representative scale |

# Mid/late-game biological rule slices

| ID | Status | Work | Depends on | Acceptance |
| --- | --- | --- | --- | --- |
| BIO-300 | Planned | Organic uptake, dissolved-organic specialization, fermentation, and regulation | LIFE-200, EVO-200 | Uptake creates no energy; fermentation and decay niches pass ecosystem fixtures |
| BIO-310 | Planned | Aerobic respiration | BIO-300, GAS-200 | Coupled fuel/oxygen/carrier contention and oxygen-tolerance fixtures pass |
| BIO-320 | Planned | Oxygenic photosynthesis and oxygenation | BIO-310, WORLD-200 | Light/water/carbon accounting and gas feedback pass producer/consumer fixtures |
| BIO-330 | Planned | Opportunistic capture, predation, defense, and prey selection | SPACE-200, LIFE-200, EVO-200 | Claims are deterministic and mass-balanced; traits influence priority/success against defense |
| BIO-340 | Planned | Complex-cell milestone, cell scale, energy machinery, and advanced storage | BIO-310, BIO-330 | Complexity pays defined upkeep/sensitivity costs and opens validated niches |
| BIO-350 | Planned | Terrestrial admission, drying pressure, land rewards, anchoring, and environmental spread | WORLD-200, BIO-320 | Soft gates and land trade-offs yield viable but hazardous colonization strategies |
| ENERGY-300 | Planned | Complete energy-storage ladder and dormancy modifiers | ORG-200, LIFE-200, BIO-340 | Storage remains constructed/costly; dormancy is observable, bounded, and non-oracular |
| RULES-390 | Planned | Freeze and validate the v1 trait/reaction catalogue and provisional prices | BIO-300, BIO-310, BIO-320, BIO-330, BIO-340, BIO-350, ENERGY-300 | All active nodes compile, prerequisites/costs are valid, and placeholders are explicitly non-selectable |

# Stage C — capacity, parallelism, and production hardening

| ID | Status | Work | Depends on | Acceptance |
| --- | --- | --- | --- | --- |
| PAR-400 | Planned | Add deterministic partition/evaluation/reduction to measured phases | PERF-200 | Worker counts produce byte/logical equivalent ledgers, hashes, events, IDs, and changes to scalar oracle |
| PERF-400 | Planned | Run uniform, clustered, migration-heavy `50,000` and `100,000` workloads | PAR-400, RULES-390 | Reference hardware and playable thresholds are recorded; bottlenecks have phase/allocation/wire evidence |
| OPT-410 | Deferred | Introduce specialized layouts, widths, SIMD, codecs, or native kernels | PERF-400 | Only admitted for a measured end-to-end bottleneck with oracle equivalence and maintenance-cost justification |
| NET-400 | Planned | Add publication cadence, acknowledgements, retention, reconnect, chunking, and backpressure | DELTA-100, PERF-200 | Bounded queues and slow-client fixtures preserve simulation independence and recover correctly |
| SAVE-400 | Planned | Add autosave cadence, progress indication, version policy, and soak/failure tests | SAVE-120, GAME-200 | Long runs survive restart/fault scenarios without partial boundaries or misleading durability claims |
| MOD-400 | Planned | Implement local balance overlays and selectable closed-schema world packs | CONTENT-110, WORLD-200, SAVE-120 | Composition, validation, identity, load, save, and trust diagnostics match the mod plan |
| RELEASE-400 | Planned | Define final deadline/precedence, speed presets, range proof, supported platform matrix, and release gates | STAGE-B, PERF-400 | No provisional product/runtime limit remains implicit; release checklist and reproducible artifacts pass |
| STAGE-C | Planned | Validate v1 capacity and release architecture | NET-400, SAVE-400, MOD-400, RELEASE-400 | Supported workloads meet recorded thresholds without violating determinism, mass balance, visibility, or persistence |

# Explicitly deferred beyond v1

- Competitive multiplayer gameplay, matchmaking, accounts, and fleet orchestration.
- General executable mods, hot-reloading active-world rules, and a mod marketplace.
- Dynamic CO₂/CH₄ greenhouse feedback and intra-tile resource-gradient fields.
- Heritable individual variation, sexual mate proximity, reversible evolution, and full historical backtracking.
- Kubernetes-specific deployment, multi-world process scheduling, and live world migration.
- Alternative native simulation core unless the technology reconsideration gate is met.

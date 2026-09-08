# LYFE v1 implementation

This directory contains the executable implementation of the [v1 plan](../../plan/v1/README.md). The Stage-A walking skeleton is complete, and Stage B has begun with the first deterministic generated-world slice. Biological content and runtime mechanics continue to arrive as vertical slices.

The living [implementation backlog](BACKLOG.md) is the source of truth for work status, dependency order, and acceptance criteria.

# Toolchain

- .NET SDK `10.0.400` (the `global.json` permits later patches in the same feature band)
- Node.js `24.20.0` LTS
- npm `11.19.0`

The server can be restored and run without the browser client. Client dependencies are independently owned under `client/`.

# Portable server

Docker is the reference portable server packaging path; native `dotnet` remains the preferred edit/debug loop. From this directory, a machine with Docker Engine or Docker Desktop and Compose can build and start the server without installing .NET:

```sh
docker compose up --build
```

Set `LYFE_SERVER_PORT` to override the default host port `5080`. The container listens on `8080`, runs non-root with a read-only root filesystem, and reserves the named `lyfe-data` volume at `/var/lib/lyfe` for hosted world-save wiring. The logical save/reload service exists; attaching it to hosted lifecycle operations and proving container replacement is `OPS-020`. See the [deployment decision](../../plan/v1/DEPLOYMENT_AND_PORTABILITY.md) for the security, versioning, validation, and portability contract.

# Layout

```text
src/Lyfe.Simulation/       authoritative, headless simulation library
src/Lyfe.Protocol/         shared .NET protocol-facing artifacts; no simulation dependency
src/Lyfe.Server/           ASP.NET Core host and transport adapters
tests/                     simulation, server-projection, and dependency-direction tests
scenarios/                 deterministic headless scenario runner
benchmarks/                executable representative benchmarks
tools/Lyfe.RuleTool/       cold-path rule/world validation and compilation host
client/                    replaceable React + PixiJS browser client
protocol/                  language-neutral Protocol Buffer source of truth
Dockerfile.server          multi-stage authoritative-server image
compose.yaml               one-command portable server deployment
rules/v1/                  official biological rule sources
world-packs/               complete world-generation profiles
mods/                      local declarative mod packages
packages/                  future generated/shared distributable artifacts
```

# Dependency direction

```text
Lyfe.Simulation   general-purpose .NET libraries only
Lyfe.Protocol     general-purpose .NET libraries only
Lyfe.Server       -> Lyfe.Simulation + Lyfe.Protocol
Lyfe.RuleTool     -> Lyfe.Simulation
Scenarios         -> Lyfe.Simulation
Benchmarks        -> Lyfe.Simulation
Browser client    -> generated protocol artifacts only
```

The simulation never references ASP.NET Core, generated protocol types, client code, wall-clock scheduling, or filesystem locations.

# Commands

From this directory:

```sh
dotnet restore Lyfe.sln
dotnet build Lyfe.sln --no-restore
dotnet test Lyfe.sln --no-build
dotnet run --project src/Lyfe.Server
```

In a second terminal:

```sh
cd client
npm install
npm run dev
```

The Vite development server listens on `http://localhost:5173` and proxies server requests to `http://localhost:5080`.

Run the deterministic foundation scenario or the non-representative owner/hash microbenchmark with:

```sh
dotnet run --project scenarios/Lyfe.Scenarios
dotnet run -c Release --project scenarios/Lyfe.Scenarios -- --founder-matrix --seeds 64 --horizon-hours 720
dotnet run --project benchmarks/Lyfe.Benchmarks -c Release
```

Validate the official foundation rule and world packages with the same strict
source loaders used by later compilation work:

```sh
dotnet run --project tools/Lyfe.RuleTool -- validate rules/v1
dotnet run --project tools/Lyfe.RuleTool -- validate-world world-packs/primordial-earth
dotnet run --project tools/Lyfe.RuleTool -- compile rules/v1 world-packs/primordial-earth scenario.foundation-sandbox world.primordial-foundation
dotnet run --project tools/Lyfe.RuleTool -- validate-world world-packs/primordial-earth-v1
dotnet run --project tools/Lyfe.RuleTool -- compile rules/v1 world-packs/primordial-earth-v1 scenario.foundation-sandbox world.primordial-earth-v1
```

# Current executable surface

- The simulation exposes stable `WorldId`, a single-owner `WorldRunner`, immutable boundary snapshots, and the versioned SHA-256 `WorldStateHashV9` over canonical logical state rather than physical storage.
- The content foundation exposes typed permanent IDs, strict manifest-driven JSON loaders, a registry/tombstone lock, mass-balanced opening reactions, the first trait branch, and an independent one-tile world profile. A deterministic compiler resolves these into immutable numeric resource/reaction/trait artifacts, founder phenotypes, dense tile stocks, and a bound scenario; tick code does not consume authoring records.
- A separate generated-world pack compiles the first `32 × 17` profile without changing the frozen one-tile fixture. Versioned integer-only generation creates spatially correlated elevation and climate normals, exact aquatic and bounded volcanic coverage, x-wrapped/y-bounded topology, deterministic calendar/current-climate materializations, and four separated adjacent hydrogen/sulfur starts. Every selected start records its bounded repair and passes depth, activity, temperature, and daily-light checks across the current seed matrix.
- The first owner-mutable state prototype adds monotonic world-local genome/species/organism IDs, fixed tile columns, a resource-major tile matrix, species-to-phenotype indirection, and tile-partitioned 256-row organism chunks. Private dense locations never serve as identity, and typed mutators seal canonically ordered phase changes with mutation-epoch and debug shadow-fingerprint coverage.
- Authoritative randomness is a stateless `Philox4x64-10` oracle addressed by a 128-bit world seed, permanent domain ID, three semantic coordinates, and explicit sample index. Its frozen 24-domain manifest permits only declared typed operations: exact fixed-point Bernoulli decisions, unbiased bounded integers, stable ranks, canonical weighted choices, and keyed binomials.
- `WorldRunner` now owns compiled world rules and a keyed 100-organism foundation state. `AdvanceOneTick` executes all 12 canonical phases through sealed views, stable-keyed scalar outcomes, preflight, typed commit, phase journals, final validation, and atomic boundary publication. Phase 3 advances biological age, evaluates every applicable intrinsic death risk, and materializes its health barrier; phase 10 materializes authoritative end-of-tick health and species aggregates before mutation income is credited.
- Founder physiology is strict, moddable rule content compiled into the phenotype: viable/mature structure, four capacity groups, default equal-shared process allocation, senescence, and temperature response. The sole integer-only health builder combines reserve, structure, constitutive-nutrient, age, lifecycle, and environmental factors in canonical order. Dense organism rows store the materialized result; persistence, hashing, publication, and Protocol Buffers carry the same value and factor breakdown rather than recomputing it.
- Intrinsic death assessment evaluates every applicable nonzero cause even when another cause certainly triggers, retaining probability, random word where sampled, observed value, boundary, all triggered causes, and the first canonical actual cause. LIFE-200 consumes that evidence, records all positive risks plus the canonical actual cause, and atomically transfers structure, reserve, and ingested matter into one stable-ID remnant before removing the organism.
- Deterministic primitive fission runs after internal metabolism when compiled lifecycle, health, structure, reserve, and cooldown gates pass. Reproductive work is returned as a spent carrier, the remaining matter is split between parent and age-zero offspring, the child receives a deterministic nearby coordinate and cannot act on its birth tick, and both schedules use one keyed `24 h + 0..3 h` cooldown draw converted to the configured tick duration.
- Remnants are authoritative persisted entities. Old remnants decay with carried fixed-point remainders into exact organic products; local same-tile scavenging is radius-, range-, and cooldown-bound through a rebuildable `16 × 16` per-tile index with an exhaustive scalar test oracle; an enabled particulate-digestion phenotype converts buffered structural biomass through a CHNOPS- and energy-balanced reaction.
- `SPACE-200` replaces the movement barrier with calibrated deterministic geometry. Rule-compiled cube-root body radii and an exact-antipodal 256-direction/256-magnitude table drive free zero-mean Brownian displacement; phase 4 separately clamps persistent active velocity and energy cost, traces the first crossed edge, wraps only x, rejects bounded y and unavailable terrestrial habitat, and admits at most one migration through the keyed passive/active probability rule. Offspring placement uses the same body scale and reflection contract. Published organism and remnant radii flow through the generated client protocol.
- `GAS-200` makes each hosted tile's atmosphere an authoritative resource stock. Strict world profiles configure eight gases, hydrogen-rich and sulfur-rich volcanic emissions, attrition, diffuse sources, terrain compatibility, and biological-access classes. Phase 2 applies source, then sink, then simultaneous canonical-edge exchange using checked integer arithmetic; persisted source/sink/edge remainders preserve sub-unit flows and every transfer is an exactly balanced tile/boundary ledger fact. The opening one-tile fixture now runs an active 0.8 hydrogen-rich vent, while executable field calibration reproduces the planned center and neighboring concentrations.
- `BEHAVIOR-200` replaces phase 9's barrier with persistent organism-local behavior. Strict rule content compiles the five-state interface and calibrated conservation profile, while the founder keeps `BaselineActivity` until evolution enables regulation. Enabled organisms update one-day fixed-point acquisition memory from completed claims, apply reserve/coverage entry and recovery hysteresis plus a four-hour dwell, and carry typed target-compatible state; conservation suppresses next-tick paid locomotion and reproduction but never free Brownian drift or useful passive capture. Live projections expose per-organism pressure evidence, exact per-live-tile distributions, exact controlled-species totals, and explicitly observed totals for other species without feeding aggregates back into decisions.
- `EVO-200` makes evolution an executable species transaction. The cold-path compiler generates and fingerprints the `0..100,000` effective-population lookup; phase 10 accumulates integer mutation income and an exact wide remainder from population, stored average health, and compiled DNA modifier. Strict trait nodes carry costs, complexity, prerequisites, incompatibilities, pressure tags, and typed phenotype effects; every accepted genome is fully recompiled and interned by canonical hash. Preview/apply validates optimistic revision and genome identity, authority, cooldown, affordability, trait closure, and one-to-four occupied tiles. Apply selects founders by stable keyed rank using the `50/20/8/3%` schedule, preserves their complete physical state, duplicates the post-price account, records an immutable lineage/event digest, and publishes atomically. A prerequisite-closed autonomous opportunity scorer exists; scheduling and committing autonomous choices remains opening content.
- `GAME-200` makes the two v1 modes authoritative world state. Typed setup creates one controlled sandbox root or two independent adjacent survival roots, and survival now requires the roots to use different opening metabolism kinds. Sandbox commands transfer control among living species and persist mutation locks with optimistic gameplay revisions. Survival always follows a player-created descendant and returns ancestors to autonomous authority. Phase 11 records survival loss when the controlled branch dies or sandbox loss when all life dies, then rejects further ticks and evolution. Roots, mode, control, locks, gameplay revision, and terminal evidence flow through changes, save/load, hashing, projections, protobuf deltas, and atomic client apply.
- `OPENING-200` provides its executable environmental base pair, setup-allocation identity,
  mandatory maintenance, and conservative biomass assembly. Balanced, High-throughput,
  and Stress-tolerant allocations compile bounded efficiency/tolerance multipliers into
  distinct cached phenotype and genome hashes; the chosen allocation is inherited through
  speciation and carried through save/load, state hashes, publication, protobuf, and the
  client. Assembly stages exact NH₃/phosphorus/H₂S bundles under the compiled primitive
  store limit, protects the reserve floor, and conserves matter/energy through authored
  outputs. Fourteen canonical micronutrients are persistent fixed-width organism and
  remnant state, split into committed catalytic/structural quota and free reproductive
  stock. Abiogenesis debits each founder's committed quota from its tile; keyed
  normalized-deficit uptake fills no more than one quantum per successful organism-hour
  opportunity; fission transfers a complete additional quota; death and terminal decay
  conserve it back to the environment. The 64-seed generated-world matrix passed all six
  choices: `384/384` reproduced, retained at least `95/100` original founders at first
  reproduction, and reached 40 MP within the 720-hour horizon.
- `WorldStateHashV9` adds committed/free organism and remnant micronutrient state to the complete current continuation surface while retaining stable numeric record/field tags and canonical logical-state ordering. Dense handles, row/chunk order, locators, mutation epochs, dirtiness, projections, and scratch remain excluded.
- Every successful tick now merges its canonical phase-local mutation journals into one bounded, stable-ID `MergedTickChanges` invalidation contract. Creates/removes obey explicit lifecycle precedence, multi-phase moves collapse to one source/destination relocation, dirty logical-field references deduplicate and sort, and resource-ledger facts are retained once and referenced by canonical keys. `TickChangeInspectorV1` provides deterministic diagnostic JSON without becoming a wire protocol or copying authoritative values.
- `WorldRunner` can capture a detached, stable-ID-only publication snapshot at a valid completed boundary. The server's direct projection oracle combines that trusted source with actor-knowledge input: controlled-species occupancy produces live tiles with exact stocks and organisms, discovered non-live tiles produce timestamped reduced summaries, and undiscovered tiles expose only identity and grid position. The oracle canonicalizes source ordering, exposes only observed populations for non-controlled species, and never emits dense locations or the hidden-state-sensitive authoritative world hash.
- The persistence boundary encodes bounded envelope/payload schema 12, including founder allocation, abiogenesis roots, persistent micronutrient inventories, exact journey landmarks, bounded routine-activity summaries, and rolling tile-resource flows. Load checks compatibility before the payload, reconstructs runtime stores and indexes, recompiles and verifies allocation-aware genomes, reconciles the ledger, verifies the recorded `WorldStateHashV9`, and resumes with an identical next tick and event identity continuation. The atomic writer preserves the prior save across an injected pre-replace failure.
- Successful tick commits derive canonical birth, reproduction, resource-absorption, feeding, migration, worsening stress-band, behavior-transition, and death facts from phase receipts, relocations, and the balanced resource ledger. Each fact receives a stable world-local ID only after the whole tick succeeds; death landmarks retain every nonzero cause probability plus the triggered cause. Exact landmarks remain append-only, while routine uptake remains hourly for 168 simulated hours and then merges into sparse daily organism buckets. The actor projector currently authorizes the controlled species' records.
- Versioned `lyfe.v1` Protocol Buffer sources generate C# and TypeScript bindings with exact 64-bit integer semantics. The server maps the actor-authorized unknown/reduced/live projection, controlled-species evolution, gameplay state, exact journey landmarks, routine summaries, and transient pulse facts to a binary `ProjectionSnapshot` at `/api/v1/worlds/active/projection`; deltas append landmark identities and replace the bounded summary/pulse collections atomically. Authoritative state hashes are not exposed.
- The React/PixiJS client decodes the generated snapshot, stores `bigint` identities and quantities, and renders every live projected organism one-for-one. Its normalized cache atomically applies absolute gameplay/whole-tile/species/summary/pulse batches plus ordered journey-event appends, ignores duplicates, and requests a full resync on gaps, ordering, identity, rules, or structural failures without changing the committed view. Configurable shape-and-color activity pulses fade on wall-clock time, and the Journey inspector switches among visible controlled organisms while showing exact landmarks separately from compacted uptake.
- The first mutation-decision surface exposes presentation-aware compiled trait definitions, acquisition state, costs, complexity, prerequisites/incompatibilities, mutation income inputs, cooldown, and exact controlled-species occupancy. The client closes selectable prerequisite chains as a convenience, but generated protobuf preview/apply requests always carry world/species/revision/genome identity and the server remains authoritative. Previews retain price, founders, and proposed genome even when unaffordable. Apply requests are serialized and retain 1,024 exact in-memory results by client command ID, making retries idempotent and conflicting reuse fail closed. The current HTTP path applies immediately to the paused local world; durable replay, actor identity, and safe-boundary hosted queuing remain later server work.
- The first resource-pressure surface carries rule-authored compound identity, display name, biological form, and environmental phase alongside live-tile exact stocks. `WorldRunner` derives sparse source, sink, neighbor-in/out, organism-uptake, and organism-release totals from applied balanced transactions and retains one exact interval per tick for up to 168 simulated hours. Save schema 12 restores that history; the actor projector exposes it only for live tiles. The client reconstructs exact historical stock boundaries backward from current stock plus sparse net flows and draws a dependency-free stock/net-flow chart for a selected compound. Each live organism also carries exact requested/granted quantities for every last-tick acquired compound, with evidence flags for actual tile-supply scarcity and scavenging claim contention. Engine-authored gates explain inactive external capture and opportunity-qualified scavenging, while a distinct bounded channel carries the first biomass-growth and reproduction blocker. These per-organism channels remain transient across restore; tile history is durable.
- Tests prove canonical byte, world-hash, tick-change-inspector, logical-save, and Protocol Buffer vectors; exact save/reload continuation; delta-to-fresh-projection equivalence and cache recovery; projection visibility and detachment; ledger/order invariance; mutation-to-evidence coverage; strict-content failures; prohibited ambient/host API enforcement; and dependency direction. A live local browser/server smoke renders the real tile and `100` organisms with no browser warnings or errors.

The generated map is both an immutable initialization/climate artifact and the source for a seed-realized hosted world. Runtime realization preserves the compiled content identity, creates ordinary dense rows for all generated tiles, assigns the sulfur founder tile the H₂S-dominant configured emission profile and other active volcanic tiles the H₂-dominant profile, and validates metabolism-specific starting roles. Temperature and founding phototrophy now read simulation-hour climate. Recurring moisture consumers, generated-world setup preview, and general configurable/blended volcanic-profile placement remain later work.

The tick pipeline is an execution foundation, not yet the complete biological simulation.
Target-driven foraging, fleeing, dispersal, energy-cost coverage, active locomotion,
destination climate compatibility, and the implemented lifecycle path are intentionally
limited to their first primitive profiles and recycling recipes. Founder
positions are keyed and the initial rows use the planned `1,000` structure and
`5,000` charged reserve, but the corresponding mass-balanced abiogenesis debit is
still deferred to the complete founding transaction.

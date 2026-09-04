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

- The simulation exposes stable `WorldId`, a single-owner `WorldRunner`, immutable boundary snapshots, and the versioned SHA-256 `WorldStateHashV1` over canonical logical state rather than physical storage.
- The content foundation exposes typed permanent IDs, strict manifest-driven JSON loaders, a registry/tombstone lock, one mass-balanced founder reaction, and an independent one-tile world profile. A deterministic compiler resolves these into immutable numeric resource/reaction handles, a founder phenotype, dense tile stocks, and a bound scenario; tick code does not consume authoring records.
- A separate generated-world pack compiles the first `32 × 17` profile without changing the frozen one-tile fixture. Versioned integer-only generation creates spatially correlated elevation and climate normals, exact aquatic and bounded volcanic coverage, x-wrapped/y-bounded topology, deterministic calendar/current-climate materializations, and four separated adjacent hydrogen/sulfur starts. Every selected start records its bounded repair and passes depth, activity, temperature, and daily-light checks across the current seed matrix.
- The first owner-mutable state prototype adds monotonic world-local genome/species/organism IDs, fixed tile columns, a resource-major tile matrix, species-to-phenotype indirection, and tile-partitioned 256-row organism chunks. Private dense locations never serve as identity, and typed mutators seal canonically ordered phase changes with mutation-epoch and debug shadow-fingerprint coverage.
- Authoritative randomness is a stateless `Philox4x64-10` oracle addressed by a 128-bit world seed, permanent domain ID, three semantic coordinates, and explicit sample index. Its frozen 23-domain manifest permits only declared typed operations: exact fixed-point Bernoulli decisions, unbiased bounded integers, stable ranks, canonical weighted choices, and keyed binomials.
- `WorldRunner` now owns compiled world rules and a keyed 100-organism foundation state. `AdvanceOneTick` executes all 12 canonical phases through sealed views, stable-keyed scalar outcomes, preflight, typed commit, phase journals, final validation, and atomic boundary publication. The first substantive phase-3 rule advances biological age by the compiled one-hour duration.
- The first resource-ledger slice executes one whole hydrogen-acetogenesis extent per eligible founder and hour. Phase 5 publishes immutable intents; phase 6 resolves coupled H₂/CO₂ scarcity with keyed residual rank, atomically debits tile gases, credits organism `ReserveOrganic`, records ocean-water and dissipated-heat boundaries, and checks exact compiled-reaction, CHNOPS, energy, capacity, and post-commit account reconciliation. The one-extent rate and fixed `10,000` reserve capacity are walking-skeleton values pending compiled phenotype throughput and capacity.
- `WorldStateHashV1` assigns stable numeric record/field tags and hashes the complete current continuation surface: simulation/rule/world/RNG identity, seed, boundary counters and time, next entity IDs, stable-ID-ordered tiles/resources, genomes, species, organisms, and the completed tick's canonical resource transactions. Dense handles, row/chunk order, locators, mutation epochs, dirtiness, and scratch remain excluded.
- Every successful tick now merges its canonical phase-local mutation journals into one bounded, stable-ID `MergedTickChanges` invalidation contract. Creates/removes obey explicit lifecycle precedence, multi-phase moves collapse to one source/destination relocation, dirty logical-field references deduplicate and sort, and resource-ledger facts are retained once and referenced by canonical keys. `TickChangeInspectorV1` provides deterministic diagnostic JSON without becoming a wire protocol or copying authoritative values.
- `WorldRunner` can capture a detached, stable-ID-only publication snapshot at a valid completed boundary. The server's direct projection oracle combines that trusted source with actor-knowledge input: controlled-species occupancy produces live tiles with exact stocks and organisms, discovered non-live tiles produce timestamped reduced summaries, and undiscovered tiles expose only identity and grid position. The oracle canonicalizes source ordering, exposes only observed populations for non-controlled species, and never emits dense locations or the hidden-state-sensitive authoritative world hash.
- The persistence boundary encodes a bounded v1 envelope and a canonical logical payload containing stable-ID state, next-ID continuation, resource stocks, and the completed ledger. Load checks compatibility before the payload, reconstructs runtime stores and indexes, reconciles the ledger, verifies the recorded `WorldStateHashV1`, and resumes with an identical next tick. The atomic writer preserves the prior save across an injected pre-replace failure.
- Versioned `lyfe.v1` Protocol Buffer sources generate C# and TypeScript bindings with exact 64-bit integer semantics. The server maps the actor-authorized unknown/reduced/live projection to a binary `ProjectionSnapshot` at `/api/v1/worlds/active/projection`; authoritative state hashes are not exposed.
- The React/PixiJS client decodes the generated snapshot, stores `bigint` identities and quantities, and renders every live projected organism one-for-one. Its normalized cache atomically applies absolute whole-tile/species batches, ignores duplicates, and requests a full resync on gaps, ordering, identity, rules, or structural failures without changing the committed view.
- Tests prove canonical byte, world-hash, tick-change-inspector, logical-save, and Protocol Buffer vectors; exact save/reload continuation; delta-to-fresh-projection equivalence and cache recovery; projection visibility and detachment; ledger/order invariance; mutation-to-evidence coverage; strict-content failures; prohibited ambient/host API enforcement; and dependency direction. A live local browser/server smoke renders the real tile and `100` organisms with no browser warnings or errors.

The generated map is an immutable initialization artifact and climate oracle, not yet the active hosted world. `ORG-200`, `GAS-200`, and later opening integration will add representative organisms, transported gases, recurring weather/moisture, and start-selection gameplay without changing the Stage-A fixture.

The tick pipeline is an execution foundation, not yet the biological simulation.
Phases other than age and the first external-capture path remain explicit empty
barriers until their vertical slices land. Founder
positions are keyed and the initial rows use the planned `1,000` structure and
`5,000` charged reserve, but the corresponding mass-balanced abiogenesis debit is
still deferred to the complete founding transaction.

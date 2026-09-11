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
`UI-200` is complete. Its final browser benchmark records exact and graphics-only tile-density
rendering at 10,000 and 50,000 authorized organisms; `PERF-200` will select the production switch
threshold inside the representative end-to-end workload. The implemented client carries save-stable authoritative organism
journey events through actor projection, protobuf snapshots/deltas, atomic client apply,
configurable map pulses, and organism inspection. Its retention slice keeps exact landmarks,
rolls routine uptake from a 168-hour window into sparse daily summaries, and publishes
worsening stress-band crossings plus actual behavior transitions without per-tick noise.
The mutation-control slice now exposes the controlled species' compiled
trait catalogue and occupied tiles, prerequisite-closing client selection, authoritative
preview, and idempotent apply with optimistic genome/evolution identity. Proposal previews now
compare nine current/descendant compiled phenotype attributes, classify benefit timing as
immediate, conditional, or preparatory, and carry typed warnings for pressure-gated behavior,
path-only DNA, and metabolic traits that do not yet install a reaction. The client validates that
warning claims agree with the comparison before displaying them. The first resource
diagnostic publishes moddable compound names/forms plus sparse source, sink, neighbor-exchange,
organism-uptake, and organism-release totals directly from the balanced ledger. A save-stable
rolling history retains each exact interval for up to 168 simulated hours; only live tiles receive
exact stocks and current/history flows. The latest completed interval also carries contributors
grouped by typed process, optional compiled reaction, and visibility-filtered species; their sums
must reconcile exactly to the aggregate flow categories. The client reconstructs stock history from
current stock plus sparse net changes and shows gross in/out and net without
conflating compound and elemental quantities, ties organism resource pressure to the selected
live tile, and hides absent compounds by default. Per-organism last-tick acquisition evidence
now reports exact requested/granted quantities per named compound and distinguishes an actual
tile-supply constraint from scavenging claim contention; coupled co-inputs are reduced without
being falsely named as the limiting material. This per-organism evidence is intentionally transient
across save restoration. The same last-tick channel now
reports why recurring external energy capture emitted no resource request: no compiled pathway,
zero accessible light/environmental opportunity, or less reserve room than one reaction extent
requires. Conservation behavior deliberately remains absent from this list because it cannot
suppress useful passive capture. Each selectable trait now carries one or more rule-authored
strategic intents; the client groups the manual tree by primary intent and previews the canonical
union without turning those labels into simulation input or a recommendation. Proposal previews
also add selected-tile cohort health, reserves,
environmental fit, resource pressure, current generated climate where available, and per-reaction
accessible-stock limits. A typed recurring-cost table exposes mandatory maintenance separately
from benefits. The browser now retains one versioned proposal goal per world/species, stores its
canonical prerequisite closure and founding tiles, reloads it through the current decision surface,
and labels an exact-integer affordability ETA as a current-rate estimate based only on the last
completed mutation-income interval. Goals reserve no points and never apply themselves. Server-side
goal synchronization/attention policy, additional recurring-cost channels, compacted long-horizon
resource histories, opportunistic scavenging and other action-gate
failures, richer event prose/linking, cold historical segments, and the hosted command queue remain.

Finite external-capture inputs now pair current stock with a bounded proposal forecast. The server
sums up to 168 recent simulated hours of environmental source/exchange inflow, environmental
sink/exchange outflow, and organism uptake on each selected live tile; compares net local renewal
with the proposed founder cohort's held-current capture-demand ceiling; and emits a typed within,
overshoot, no-renewal, or no-history status. The latest completed interval separately attributes
uptake to the controlled lineage versus other species observed on that tile. The client labels both
windows and the held-current assumption, and never presents the result as a survival guarantee.

When a valid current builder diverges from the browser-local pinned goal, the client now requests a
fresh authoritative preview for both at the same current species boundary. A side-by-side decision
workspace compares milestone and prerequisite closure, intent, timing, validity/affordability,
price, complexity, balance outcome, founding plan, ancestor remainder, warnings, flow/competition
evidence, descendant phenotype, and recurring cost. It highlights differences without ranking a
winner; only the current builder retains an apply action, and preview payloads are not persisted.

The consequence-review slice now anchors a versioned browser-local baseline to every accepted
player speciation event and pins it through the shared 168-hour branch cooldown. The client compares
branch-start and current ancestor/descendant population, condition, acquisition, pressure, behavior,
visible occupancy, retained descendant birth/death events, proposal recurring costs, and latest
new-reaction uptake evidence. Every current population is labeled world-exact or live-tile-observed;
ancestor event history and other unavailable evidence remain explicitly unavailable rather than
becoming zero. The panel reports what changed and preserves immediate/conditional/preparatory intent
without scoring branch success. Accepted player branches now also create save-stable authoritative
review schedules. At the exact 168-hour cooldown boundary the runner appends a factual chronicle
summary; rule-authored longer windows append proposal-specific follow-up landmarks without extending
the cooldown. The reviewed branch retains world-exact baseline/current facts while its comparison
retains only live-tile-observed facts; unavailable comparison activity is labeled unavailable rather
than zero. The client applies the events as an ordered append-only stream and shows population,
health, reserve, occupancy, birth/death, and migration evidence without a success score. Each saved
observation now also freezes a canonical behavior-state mix and average recent acquisition coverage
at its own exact or observed scope; the client compares boundary counts and percentages without
reconstructing either historical aggregate from the present viewport. The saved schedule now also
accumulates world-exact reviewed-branch activation evidence through every due boundary: entries into
resource conservation and executed transactions for every compiled reaction. Landmarks distinguish
proposal-introduced from inherited machinery, retain zero as a factual absence of recorded use, and
explicitly state when the proposal installed no new reaction. The first generic world-event stream
is now authoritative and save-stable: speciation, first reproduction, fixed population thresholds,
first species/tile occupation, first true compiled-reaction execution, and extinction carry
deterministic significance rule v1, canonical deduplication keys, and globally unique chronicle
IDs. Actor projection retains only events involving the controlled lineage, batches append them
idempotently, and the client derives factual copy while keeping private hypotheses separate.
The first attention-routing pass turns those events and lineage-review boundaries into saved,
actor-filtered, append-only alerts whose evidence links back to chronicle IDs. Same-family facts for
one species and completed boundary coalesce deterministically. A critical population-band rule fires
only on entry from above ten to ten or fewer and rearms only after recovery above fifteen. Two saved
window rules now also report a 25% trailing-24-hour population decline and six consecutive hours
below 25% average health; they rearm below 10% trailing decline and after six consecutive hours
above 35% health respectively. Their rolling evidence, latches, and episode ordinals survive
save/reload. The next transition slice records the first realized death cause per species as a
critical event linked to its journey evidence, plus a strategic alert after six hours at or above
75% average composite resource pressure; that pressure rule rearms after six hours below 55%.
Automatic pausing, per-compound depletion/recovery/reversal events, retention, and broader narrative
polish remain later slices.

`UI-205` completes the pre-alpha camera contract. Authoritative signed grid coordinates are
normalized into the zero-based presentation extent, so fit/focus center the complete generated
world without changing the signed coordinates shown to the player. Horizontal travel is periodic while vertical
travel remains bounded; one 32-column scene is recycled across the seam instead of duplicating
authorized entities. Explicit organism follow survives projection refresh and zoom, while manual
pan or tile focus releases it. Versioned per-world camera/tile-selection preferences stay browser-
local; organism inspection and follow state are transient, and loss of live authorization retains
only the selected tile's truthful last-known projection.
Accepted early playtest feedback also replaces the narrow fixed-width shell with responsive
near-edge gutters, gives the camera the flexible wide-screen column and a `560–820 CSS px` height,
bounds the evidence rail at `320–430 CSS px`, and moves that rail beneath the map at `1050 CSS px`
rather than compressing the world surface. The compact rail keeps simulation hour, a single
Pause/Resume state-and-action control, Step, Speed, and the map knowledge key above the fold;
decorative world copy and duplicate lifecycle status are removed, while projection diagnostics are
available only through an explicit presentation-only debug UI build flag.
The no-preference entry camera focuses the controlled species' starting tile. The default rail adds
exact controlled-species living count, average health, and occupied-tile count; directly clicking a
green controlled organism replaces it with individual state and journey evidence until Back is
used. Other organisms render red and direct selection exposes only visibility-scoped species totals,
average health, and occupied tiles, explicitly withholding hidden members and locations and expiring
when no member remains on a shared live tile.
Live tile projections also carry compiled baseline volcanism. The exact renderer turns it into one
to sixteen deterministic static wireframe vent clusters beneath entity layers, with a compact key that
labels greater mark density as stronger baseline activity. Unknown tiles receive no volcanic art;
reduced/remembered volcanic knowledge remains in `KNOW-200`.
Fast-refresh flicker is removed by retaining one Pixi application/canvas and atomically installing a
fully prepared replacement scene before retiring the prior authoritative boundary. Scene-local pulse
ticker callbacks are retired with their scene instead of accumulating across refreshes. Activity
pulse age is keyed to authoritative world/event identity, so selection, camera, and filter redraws do
not replay the current boundary's symbols after their original animation has elapsed.
Pointer, keyboard, and Chromium accessibility-tree smoke plus pure pinch/pan classification tests
pass. Physical trackpad/touch and broader assistive-technology calibration remain playtest work,
while `PERF-200` owns the measured density-aggregation threshold.

The next implementation gate is `PERF-200`, followed by the Stage-B developer-playable milestone
and structured `ALPHA-200` sessions. `COPY-200` and `UI-210` deliberately consume those findings;
they no longer block the first internal sessions. The canonical local launch, exercise route, known
limits, and 2026-09-11 readiness evidence live in [`ALPHA_PLAYTEST.md`](ALPHA_PLAYTEST.md).

Early exploratory playtesting also confirmed that the intended neighboring/remembered-tile
knowledge loop is not yet connected to the hosted game. `KNOW-200` records that gap and is deferred
while additional playtest feedback is gathered. It must resume before `PERF-200`, so representative
profiling includes the persistent knowledge state, expanded projection, and reduced-tile rendering
rather than measuring the current empty-discovery scaffold.

Exploration state now has one consistent visual and textual language. The map legend and tile marks
distinguish current live projection, retained last-known observation, and unavailable unknown state;
live tiles render authorized remnants as distinct entities. The selected-tile inspector exposes
exact current stocks only for live tiles, computes reduced observation age from authoritative tick
boundaries, lists only retained resource identities for reduced tiles, and withholds all unavailable
detail for unknown tiles. Explicit selection of a reduced or unknown tile no longer causes the
resource diagnostic to silently substitute another live tile.

The first client recovery contract now refuses to present partial initial state, preserves and
labels the last verified completed boundary after a disconnect, and locks clock, persistence, and
evolution commands until a fresh projection/evolution/control boundary and catalogue validate.
Running worlds retry automatically and every request has a provisional eight-second deadline;
manual retry remains available for paused worlds and initial failure. HTTP rejections retain the
server explanation, while transport/protocol failures label command delivery unconfirmed instead of
guessing whether it applied. Production reconnect backoff, jitter, stream retention, and
acknowledgement policy remain `NET-400` work.

The first authoritative clock bridge is now executable. A host-owned single-writer service exposes
optimistic pause, resume, one-tick step, and slow/normal/fast commands; every response names the
completed tick, world revision, simulated hour, run status, and independent control revision. The
client reads projection, evolution, and control in one locked boundary payload and replaces that
payload periodically while running. Evolution preview/apply is admitted only while paused. The
initial wall cadences are `2,000`, `1,000`, and `100 ms` per tick and never change simulated tick
duration. Fast was tightened from the original provisional `250 ms` after exploratory playtesting;
it remains bounded and accumulates no catch-up debt. This remains a single-process bridge: durable hosted world selection, command replay,
save/load controls, deadline policy, and the full bounded mailbox are subsequent slices.

The playable opening now begins in a real no-active-world host state. An authoritative generated
setup surface exposes the official `32 × 17` profile, a canonical seed, Sandbox or Survival,
the two permitted founding metabolisms, three compiled founder allocations, and four seed-realized
candidate regions. Region previews carry bounded depth, current temperature, volcanism, and repair
status without tile IDs, exact resource stocks, or map-wide composition. Creation revalidates every
choice, installs `100` player founders at hour zero, and in Survival installs the other metabolism
in its paired neighboring tile under autonomous authority. The first hosted persistence surface now
lists compatible atomic saves from the configured data directory, marks whether the active revision
is saved exactly, captures a detached completed boundary, and restores or unloads only against the
expected active world/revision. Unsaved replacement requires explicit confirmation, world IDs are
reserved through an atomically persisted monotonic counter, and load reuses the existing envelope,
compatibility, payload, and world-hash verification. Richer premise/dependency copy, save naming and
deletion, autosave/checkpoint policy, and setup-option expansion remain later.

Each authoritative lineage landmark now also has a bounded browser-local hypothesis field keyed by
world and event identity. The interface labels the note as the player's interpretation rather than
a simulation fact; malformed local state fails closed, blank text removes the note, and no note
enters world persistence, protocol messages, hashes, scoring, or organism behavior. Authoritative
landmarks now retain typed decision, branch-summary, journey-window, and event-time resource-tile
references. The client validates each target, navigates internal fact records directly, and opens
exact tile resources only while current actor knowledge still marks that tile live. Cross-device
annotation synchronization and broader world-event evidence families remain later slices.

Cooldown and longer follow-up observations now freeze average recent acquisition coverage and
canonical behavior counts alongside health, reserves, and resource pressure. The reviewed branch
uses its world-exact population while comparison aggregates are limited to organisms on the reviewed
branch's event-time live tiles. The client displays the bounded coverage ratio and boundary behavior
mix directly from the authoritative landmark; it neither infers them from currently visible
organisms nor presents the intake level as a future forecast.

`OPENING-210` is complete. The official playable scenario now samples each founder's biological age
uniformly from `0..168` hours and absolute initial reproduction-readiness boundary from
`240..360` hours through separate RNG-schema-v2 domains keyed by stable organism/species/tile
identity. Both roots use the same scenario profile. `CreateGame` selects this spread by default;
legacy foundation fixtures retain their former age-zero plus `24..27`-hour schedule, and narrow
tests can explicitly select a true age-zero, 24-hour zero-spread mode. The spread changes no material
inventory, round-trips through save/restore, reproduces identical hashes and continuation for equal
inputs, keeps the calibrated first-event bands, and distributes each generated-world root's first
births over multiple completed ticks. A bounded post-change matrix reran all six founder/allocation
choices across four generated seeds: `24/24` reproduced, retained at least `98/100` founders at the
first reproduction, and reached 40 MP within the 720-hour horizon.

`COPY-200` is a feedback-informed editorial track after the first structured `ALPHA-200` sessions.
It does not block performance work or the developer-playable `STAGE-B` milestone. It does block the
later `ALPHA-READY` play-tester build, where narrative copy must carry LYFE's wonder and mystery and
mechanical copy must be direct before restrained personality is layered on top.

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
| OPENING-210 | Done | Desynchronize playable founder cohorts with bounded deterministic initialization spread | OPENING-200, LIFE-200 | Scenario-authored age, initial reproduction schedule, and any required readiness-phase ranges use distinct permanent stable-ID-keyed random domains assigned through the RNG compatibility workflow; player and autonomous roots receive the same distributions; all varied material state remains exactly debited and save/hash/replay/worker-count invariant; default Sandbox and Survival first births span multiple completed ticks without materially leaving the authored first-reproduction or first-decision pacing bands; narrow fixtures may explicitly use a zero-spread profile |
| UI-200 | Done | Add knowledge-limited map, organism/species inspection, lifecycle activity pulses and journey logs, mutation decisions, behavior summaries, and resource-flow views | OPENING-200 | Player can observe, diagnose, choose, and review consequences without hidden-state leakage; the presentation-only camera supports bounded pointer/keyboard pan, anchored wheel/pinch zoom, fit and tile focus, and one shared tile/within-tile hit transform while preserving live/reduced/unknown semantics; persistent map marks and a selected-tile inspector distinguish current exact state, aged retained identities, and wholly unavailable state without substituting unrelated live evidence; initial loading exposes no partial world, disconnect preserves a labeled last-complete boundary, recovery locks authoritative commands, and rejection remains distinct from unconfirmed delivery until full resynchronization; completed birth/reproduction, feeding, uptake, stress, migration/state-transition, and death facts drive configurable accessible on-map icons with bounded wall-clock fading; exact landmarks plus compacted routine summaries provide a save-stable authorized journey; mutation choices are grouped by rule-authored strategic intent, while previews compare current and proposed compiled phenotypes and recurring maintenance, label benefit timing, warn honestly about pressure gates or preparatory DNA, and show current selected-tile cohort/climate/substrate evidence without claiming future survival or recommending an optimum; finite capture inputs distinguish stock from recent local renewal, compare a held-current founder demand ceiling with that renewal, flag likely overshoot, and isolate latest controlled-versus-other-observed-species uptake without hidden-state leakage; one browser-local goal per world/species preserves canonical prerequisite/tile selections and reports a clearly caveated last-rate affordability ETA without reserving points or auto-applying; accepted player speciation pins a browser-local 168-hour ancestor/descendant review and creates an authoritative save-stable schedule whose exact cooldown boundary and optional rule-authored longer window append clearly labeled chronicle landmarks with scoped branch evidence, no cooldown extension, and no success score; each landmark permits a bounded world/event-local private hypothesis that is visibly non-authoritative and excluded from saves, hashes, protocol, and mechanics, plus typed navigation to its immutable decision/branch facts and only currently authorized journey/resource destinations; live tiles expose named exact compound stocks, gross last-tick ledger flow categories, reconciling typed reaction/species contributors, and a save-stable 168-hour exact sparse-flow history used to reconstruct stock trends without leaking non-live tile values; selected live organisms expose exact last-tick compound demand/grant plus bounded engine-authored capture, scavenging, biomass-growth, and reproduction blockers with applicable resource, threshold, capacity, action-energy, behavior, and cooldown evidence; browser-DOM interaction suites cover the primary decisions and evidence routes, and a reproducible headless-Chrome/PixiJS benchmark records exact and tile-density rendering at 10,000 and 50,000 visible organisms without promoting a UI-only timing result into an end-to-end performance claim |
| UI-205 | Done | Finish the pre-alpha camera continuity and local-preference contract | UI-200 | Signed authoritative grid coordinates normalize into a centered presentation extent for fit, focus, drawing, and inverse hit-testing without changing factual labels; X-wrapped worlds pan seamlessly across the visual seam while Y remains bounded; a new world focuses the controlled starting tile while Fit remains explicit; direct map hits are the sole entry to transient organism inspection; green controlled organisms expose individual state and journey evidence with Back, while red other organisms expose only species-level observations that expire with shared-tile visibility; an explicit controlled-organism follow toggle survives projection refreshes, manual pan or tile focus releases it, and zoom alone preserves it; camera/tile-selection preferences persist browser-locally per saved world without entering protocol, saves, hashes, replay, or mechanics; responsive near-edge gutters, a flexible camera column, bounded evidence rail, and stacked narrower-desktop layout preserve a spacious usable viewport; the default compact rail adds exact controlled-species population, average health, and occupied tiles while keeping consolidated clock controls and map key above the fold and gating raw projection diagnostics behind an explicit debug UI flag; pointer, trackpad, pinch, and keyboard behavior receive an assistive-technology and playtest smoke pass |
| KNOW-200 | Deferred | Implement persistent neighboring and remembered-tile knowledge | WORLD-200, PROJ-100, SAVE-120, DELTA-100, UI-205 | At tick zero and each completed boundary, controlled-species occupied tiles are live, edge-sharing neighbors become reduced observations, and vacated live tiles retain a timestamped observation; authoritative actor knowledge persists through save/load, hashes, deterministic continuation, and sandbox control transfer; reduced tiles expose only fixed/coarse environment bands, timestamped broad life-presence bands, and named key-resource presence or abundance bands, never exact current stocks, organism positions, species identities, remnants, flows, or silently refreshed hidden conditions; deterministic thresholds and canonical ordering produce identical knowledge across worker counts and replay; snapshot/delta replacement evicts live-only detail atomically; the map, selected-tile inspector, and evolution evidence label observation time, scope, staleness, and uncertainty without presenting remembered conditions as current or predictive |
| PERF-200 | Planned | Profile the full default-grid opening around 10,000 organisms | UI-200, OPENING-210, KNOW-200 | Stage-B workload records tick, memory, GC, save, projection, wire, apply, and frame metrics, including persistent knowledge updates and reduced-tile payload/rendering costs |
| STAGE-B | Planned | Demonstrate a developer-playable survival opening and free sandbox | PERF-200, UI-205 | Coupled opening/lifecycle/resource scenarios and the end-to-end player loop pass at representative scale with the pre-alpha camera contract; known copy debt is labeled and does not block the first structured internal sessions |
| ALPHA-200 | Planned | Run the first structured internal alpha sessions and capture attributable findings | STAGE-B | The checked-in playtest recipe launches from a clean supported environment; facilitators exercise setup, clock, observation, evolution, save/reload, and recovery; notes distinguish observed comprehension, pacing, performance, input, and copy issues; no behavioral telemetry is collected without a documented privacy/consent policy |
| COPY-200 | Planned | Replace generic or synthetic-sounding interface prose with a coherent LYFE voice for play-tester alpha | ALPHA-200 | A complete player-facing string inventory covers setup, world/discovery, organism inspection, resources, evolution, alerts, consequence review, chronicle, empty/loading/error states, extinction, and run summary; narrative copy evokes the wonder and mystery of life without inventing causality or consciousness; mechanics copy states rules, numbers, scope, uncertainty, and recovery plainly before any restrained wit or whimsy; repetitive caveats, inflated abstractions, generic headings, and templated rhythms receive editorial revision; literal accessible names preserve every consequential fact; a terminology/voice sheet and first-alpha findings confirm both mechanical comprehension and a distinctive tone without changing authoritative facts or hashes |
| UI-210 | Planned | Apply post-alpha interface polish from observed playtesting feedback | ALPHA-200 | Work begins only after the first structured playable-alpha sessions produce attributable findings; each accepted change cites the observed usability problem, preserves authoritative knowledge and input/accessibility contracts, receives proportionate interaction or visual regression coverage, and is rechecked with playtesters rather than becoming speculative redesign before alpha |
| ALPHA-READY | Planned | Prepare the feedback-informed play-tester alpha build | COPY-200, UI-210 | Accepted first-alpha findings are resolved or explicitly deferred; player-facing copy and UI pass their targeted checks; the local playtest recipe, known-issues list, and supported input/browser scope are current |

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

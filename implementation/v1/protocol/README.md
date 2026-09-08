# LYFE protocol sources

This directory owns cross-language Protocol Buffer schemas. Schemas are not generated from C# runtime classes, and generated bindings must not become the authoritative simulation model.

The `lyfe.v1` package owns the Stage-A capabilities and actor-authorized full-projection contracts. C# generation is pinned through `Grpc.Tools` in `Lyfe.Protocol`; TypeScript generation is pinned through Buf and Protobuf-ES in the client. Protobuf-ES maps authoritative 64-bit fields to native `bigint`.

Live organism and remnant projections include normalized tile-local coordinates and
their current derived body radius. The client can therefore render one visible unit
per simulated entity and apply absolute tile replacements without reconstructing
simulation geometry from DNA or conserved-matter state.

Live organisms also expose exact committed and free micronutrient stocks, while remnants
expose their combined recyclable stock. These are absolute, canonically ordered resource
entries; clients display or replace them and never infer quota state from DNA or tile totals.

The projection snapshot carries presentation-only resource definitions sourced from the
compiled mod pack. A live tile pairs exact stocks with sparse last-completed-tick flow totals
for environmental source/sink, neighbor exchange in/out, and organism uptake/release. Every
amount derives from an applied tile ledger entry; clients may sum gross inputs, gross outputs,
and net change but must not infer a flow from stock differences. A live tile also carries a
chronological, save-stable rolling history of these sparse intervals, capped at 168 simulated
hours. Current exact stock plus interval net changes is the compact, lossless contract for
reconstructing earlier stock boundaries; duplicate stock snapshots are not transmitted. Reduced and unknown tile
variants have no current flow field. Resource definitions remain stable for a projection stream,
so delta application retains the snapshot catalogue while whole-live-tile replacements carry
the latest interval.

Each live organism may also carry its exact last-completed-tick acquisition request and grant
per named resource. Separate flags identify tile-supply scarcity and scavenging claim
contention. A coupled reaction can therefore show every co-input's proportional reduction while
naming only the input whose available stoichiometric extent actually constrained the reaction.
The array is canonical by `resource_id`, is absent when no external acquisition was requested,
and is validated atomically by both projector and client cache. This per-organism evidence channel
is transient runner state rather than save payload: immediately after restoration it is empty until
one tick completes. Tile-level interval history remains available across restoration.

When a supported acquisition process emits no request, `acquisition_gate_evidence` carries its
first authoritative blocker before claims. Recurring external capture can name missing compiled
capability, inaccessible light, another zero environmental opportunity, or insufficient reserve
room. Opportunity-qualified scavenging can name missing capability, active cooldown, insufficient
action energy, or full particulate-intake capacity, but only when consumable remains are inside the
organism's local interaction range. There is deliberately no `no target` gate and no evidence about
out-of-range remains. Quantity gates carry exact available-versus-required values; cooldown gates
carry `clears_at_tick`; other reasons carry no invented numeric payload. Records are canonical by
process/reason, live-organism-only, and transient on restore like per-resource acquisition evidence.
V1 conservation behavior cannot suppress useful passive capture, so the server does not
manufacture a behavior-gate record.

`action_gate_evidence` separately explains optional biomass growth and deterministic reproduction.
It contains at most one first blocker for each process per live organism. Growth can report missing
capability, conservation, maintenance shortfall, reserve protection, staging capacity, the exact
limiting tile resource, or a lost final contested extent. Reproduction can report conservation,
cooldown, health, structure, reserve, lifecycle, or the exact missing commissioned/offspring
micronutrient quota. Successful or partially successful growth and successful reproduction carry
no gate. Quantity reasons carry exact available/required values, resource reasons carry a stable
resource ID, and cooldown carries its clearing tick. Both projector and client reject mismatched
process/reason or payload combinations. Like acquisition evidence, this diagnostic describes only
the last completed tick and is intentionally absent immediately after restore until another tick.

Live organism projections also carry current behavior, typed target-compatible
fields, selection/dwell ticks, pressure-memory channels, and derived resource
pressure. Live tiles carry exact per-species behavior counts; species summaries are
exact for the controlled species and explicitly limited to live-tile observations
for other species.

Controlled-species summaries additionally carry the immutable genome identity,
founder genome/allocation IDs, trait IDs, mutation balance/rate inputs, optimistic evolution revision, branch
cooldown, and lineage parent/timestamps. The same fields participate in absolute
species replacements, while observed uncontrolled species intentionally omit this
private evolution payload.

Gameplay roots also expose both founder IDs. Clients can therefore display the
chosen metabolism/allocation without reverse-engineering a compiled genome hash.

Actor projections separate three server-authored activity channels. Full snapshots
carry authorized append-only landmark history; delta batches carry strictly ordered
landmark appends. Bounded routine-uptake summaries are absolute sparse replacements,
and transient pulse events replace the prior publication's display facts. Birth,
reproduction, absorption, feeding, migration, worsening stress-band crossings,
actual behavior transitions, and death remain semantic; glyphs, colors, animation
timing, and user filters are client presentation state. Death events retain every
nonzero cause probability and identify which cause actually triggered the death.

The first implementation sends the complete bounded routine-summary collection in
each batch. This deliberately favors simple idempotent apply semantics; a future
profiled optimization may replace it with absolute composite-key upserts/removals
without changing the projection model.

`evolution.proto` owns the first on-demand controlled-species decision surface and
speciation command shapes. The query exposes compiled trait IDs, server-provided display
names, families, costs, complexity, prerequisites/incompatibilities, current acquisition,
mutation-income inputs, cooldown, and exact occupied-tile populations. Preview and apply
requests carry the expected evolution revision and genome hash; an unaffordable preview
still includes its calculated cost, founder split, and resulting genome identity.

The local v1 HTTP host exposes protobuf GET decision, POST preview, and POST apply routes.
Apply also carries a bounded client command ID: the server serializes requests and retains
the latest 1,024 response bytes so an exact retry returns the original command order/result,
while conflicting reuse returns HTTP 409. This is a deliberately narrow bridge to the
future hosted command queue: actor/session identity, durable command replay, queue admission,
and asynchronous safe-boundary acknowledgements remain unimplemented.

From `implementation/v1/client`, regenerate TypeScript bindings with:

```sh
npm run generate:protocol
```

The .NET build regenerates C# bindings from the same sources. Generated artifacts never become the authoritative simulation model.

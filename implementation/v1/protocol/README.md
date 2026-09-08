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

Actor projections include stable, server-authored organism journey events. Full
snapshots carry authorized retained history, while delta batches carry strictly
ordered appends. Birth, reproduction, absorption, feeding, migration, and death
facts remain semantic; glyphs, colors, animation timing, and user filters are
client presentation state. Death events retain every nonzero cause probability
and identify which cause actually triggered the death.

From `implementation/v1/client`, regenerate TypeScript bindings with:

```sh
npm run generate:protocol
```

The .NET build regenerates C# bindings from the same sources. Generated artifacts never become the authoritative simulation model.

# Official v1 biological rules

This is the first strict authoring slice of LYFE's ordinary official rule pack. It is
intentionally a foundation fixture, not the complete v1 biological catalogue: four
mass-balanced resources support one hydrogen-acetogenesis reaction, one founder
genome, and one sandbox scenario.

`pack.json` explicitly names every authoritative source file. `generated/rules.lock.json`
commits the permanent numeric-ID/stable-key pairs; removed identities will become
tombstones rather than being reused. Runtime simulation code will consume only
validated, immutable compiled artifacts—not these authoring records directly.

The compiler assigns dense slots in permanent-ID order, resolves every authored
key, checks reaction matter balance, and generates separate mechanics,
presentation, registry, and logical-compiled hashes. Golden vectors live in the
simulation tests; file grouping, JSON property order, and set order do not enter
those identities.

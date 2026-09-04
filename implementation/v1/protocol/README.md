# LYFE protocol sources

This directory owns cross-language Protocol Buffer schemas. Schemas are not generated from C# runtime classes, and generated bindings must not become the authoritative simulation model.

The `lyfe.v1` package owns the Stage-A capabilities and actor-authorized full-projection contracts. C# generation is pinned through `Grpc.Tools` in `Lyfe.Protocol`; TypeScript generation is pinned through Buf and Protobuf-ES in the client. Protobuf-ES maps authoritative 64-bit fields to native `bigint`.

From `implementation/v1/client`, regenerate TypeScript bindings with:

```sh
npm run generate:protocol
```

The .NET build regenerates C# bindings from the same sources. Generated artifacts never become the authoritative simulation model.

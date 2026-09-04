# Server Deployment and Portability

Status: v1 container boundary selected and reference image validated on native `linux/arm64` and emulated `linux/amd64`; logical save/reload and atomic local writing exist, while hosted lifecycle wiring and container replacement remain

Sources: [technology decisions](TECHNOLOGY.md), [architecture](ARCHITECTURE.md), [world ownership](WORLD_EXECUTION_AND_OWNERSHIP.md), [persistence](PERSISTENCE_AND_REPLAY.md), and [implementation backlog](../../implementation/v1/BACKLOG.md).

# Decision

LYFE v1 will ship the authoritative server as an OCI-compatible Linux container image. A checked-in multi-stage `Dockerfile` is the canonical image recipe, and Docker Compose is the reference one-command local deployment interface. Native `dotnet` execution remains a fully supported and faster development loop.

Docker is the right default for this boundary because it packages the pinned .NET runtime and server output together, behaves consistently across supported container hosts, supports both `amd64` and `arm64` images, and maps naturally onto the existing server/client process separation. Official Microsoft SDK and ASP.NET runtime images make this a conventional deployment rather than a custom runtime distribution. Multi-stage construction keeps build tooling out of the runtime image.

This decision selects a packaging contract, not a production orchestrator or a simulation architecture. The simulation library remains host-independent. Docker, Compose, Kubernetes, accounts, matchmaking, service discovery, and cloud-vendor APIs must never enter `Lyfe.Simulation`.

# Intended workflows

## Native inner loop

Developers with the pinned .NET SDK run restore, build, tests, scenarios, and the server directly. This is the preferred edit/build/debug path because it has the least indirection and supports ordinary profilers and debuggers.

## Portable server loop

A developer, tester, or single-player host with Docker and Compose runs:

```sh
docker compose up --build
```

This must require neither a host .NET SDK nor a host Node.js installation. The browser client may run natively during development and connect through its existing proxy. A production client image or combined distribution can be added later without changing the server image.

## Release and hosted deployment

CI builds the same Dockerfile for the supported Linux architectures, tests the resulting image through its public health and capability endpoints, records the immutable image digest and source revision, and publishes only after the native and container test gates pass. Hosted environments may run the image with Docker, another OCI-compatible runtime, or a later orchestrator.

# Container contract

The server container must:

- listen on container port `8080` and expose health through `/health`;
- run as the non-root `app` user supplied by official .NET images;
- accept graceful termination and stop only at the safe world boundary defined by world ownership;
- write structured logs to standard output and standard error;
- treat the image filesystem as immutable;
- use `/var/lib/lyfe` as the only persistent-data root once saves are implemented;
- accept configuration through validated environment variables and mounted configuration artifacts;
- mount external rule, world, and mod packages read-only and compile them through the ordinary cold-path boundary;
- avoid host paths, Docker socket access, privileged mode, and runtime package installation; and
- produce identical authoritative results to native execution for the same engine, rules, world, commands, seed, and architecture-support contract.

The reference Compose service uses a named data volume, a read-only root filesystem, a temporary `/tmp`, no added Linux capabilities, and `no-new-privileges`. A bounded envelope, canonical logical world codec, exact reload, and atomic local-file writer now exist. The server does not yet attach save/load to its hosted world lifecycle, so the volume establishes the location contract without claiming container-replacement durability before `OPS-020`.

# Image construction and versioning

The development Dockerfile uses matching official `.NET 10` SDK and ASP.NET runtime image families. Restore inputs are copied before source files so ordinary dependency layers remain cacheable. Only published server output crosses into the final runtime stage.

During the early development loop, the `10.0` image tag is allowed to receive supported servicing updates. Release CI must resolve base images to immutable digests, record those digests in build provenance, and rebuild after .NET or base-OS security updates. The application version, source revision, rules compiler version, and protocol version must also be exposed through build metadata before public releases.

The image must not embed active saves, local mods, secrets, or machine-specific configuration. Official rule and world content may be embedded as versioned application assets when those loaders exist.

# Why not make containers mandatory for development?

Containers improve reproducibility and portability, but they do not improve the deterministic simulation model by themselves. Requiring a container for every unit test or profiling session would slow the inner loop, complicate debugger/profiler access, and can distort performance measurements on Docker Desktop virtual machines. Native and container paths therefore exercise the same published server boundary, with native runs used for iteration and container runs used for packaging and deployment validation.

# Alternatives and boundaries

- The .NET SDK can publish OCI images without a Dockerfile. That remains useful for CI or air-gapped image archives, but the explicit multi-stage Dockerfile is easier to inspect, run with Compose, and extend with image-level policy during the foundation stage.
- Podman and other OCI runtimes should be compatible with the built image, but Docker Compose is the supported v1 developer command until another runtime is exercised in CI.
- Kubernetes is deliberately deferred. A single-world server needs a correct save/shutdown contract and operational measurements before orchestration adds value.
- The browser client is not placed in the server container. Keeping its image and release cadence separate preserves the multi-client architecture and avoids turning the server into a presentation host by accident.

# Validation matrix

| Gate | Required result | Current state |
| --- | --- | --- |
| Dockerfile review | Multi-stage build, runtime-only final image, non-root user, fixed internal port | Implemented and built |
| Compose parse | `docker compose config` succeeds without local overrides | Passed with Docker Compose `5.5.0` |
| Image build | Builds on `linux/amd64` and `linux/arm64` | Passed locally: native arm64 and Docker Desktop-emulated amd64 |
| Runtime smoke | `/health`, `/api/v1/capabilities`, and active-world projection succeed | Passed on native arm64 and emulated amd64 with identical `WorldStateHashV1` creation hashes |
| Shutdown | `SIGTERM` reaches a safe boundary within the configured grace period | Foundation host exited cleanly with code `0`; revalidate after real world-host lifecycle lands |
| Persistence | Save survives container replacement through `/var/lib/lyfe` | Logical save/reload and atomic fault injection pass; hosted volume wiring and container replacement remain `OPS-020` |
| Determinism | Native and container scenario hashes match on every promised architecture | Native process, native arm64 container, and emulated amd64 container match the frozen `WorldStateHashV1` creation vector |
| Security | Image runs non-root, read-only, without added capabilities or embedded secrets | Runtime passed as UID `1654`/`app`, read-only root, `cap_drop: ALL`, and `no-new-privileges` |

Local validation on September 3, 2026 used Docker Engine `29.7.2` and Docker Compose `5.5.0`. After `HASH-100`, the native process, native arm64 image, and Docker Desktop-emulated amd64 image returned the same frozen `WorldStateHashV1` creation hash, `8efb01397a4a24b1956c7570dbeb15e70a1860b24975d1228e0a296ec0d03a8e`. This proves the current Stage-A state surface across the local architecture paths; continuous architecture CI and later full biological/save-reload fixtures remain required.

After the generated Stage-A protocol landed, the reference image was rebuilt
from a clean protocol-copy/publish layer and the container returned `200` with
`application/x-protobuf` and a `2,719`-byte actor-authorized foundation snapshot.
This verifies that Protocol Buffer sources are part of the portable build rather
than an undeclared native-worktree dependency.

# Acceptance criteria

- A new contributor with Docker Desktop or Docker Engine plus Compose can start the server from `implementation/v1` with one documented command.
- The container responds on the configured host port and reports the same version/capability data as a native server.
- The final image contains no SDK and runs as non-root with a read-only root filesystem.
- Container replacement preserves saves once persistence exists; image replacement never mutates rule compatibility silently.
- CI builds and smoke-tests both supported architectures before the server packaging path is called release-ready.
- Container-specific code remains outside the simulation library.

# Upstream basis

- [Docker multi-stage builds](https://docs.docker.com/build/building/multi-stage/)
- [Docker build best practices](https://docs.docker.com/build/building/best-practices/)
- [Docker Compose application model](https://docs.docker.com/compose/intro/compose-application-model/)
- [Official .NET container images](https://learn.microsoft.com/en-us/dotnet/core/docker/container-images)
- [ASP.NET Core Docker hosting](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/?view=aspnetcore-10.0)

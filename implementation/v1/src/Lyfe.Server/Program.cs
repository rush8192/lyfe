using System.Globalization;
using System.Collections.Immutable;
using Google.Protobuf;
using Lyfe.Protocol;
using Lyfe.Server;
using Lyfe.Server.Projection;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.World;

var builder = WebApplication.CreateBuilder(args);

var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
builder.Services.AddSingleton(WorldRunner.CreateFoundation(
    WorldId.From(1),
    rules,
    RootRandomSeed.Parse("00000000000000000000000000000001")));

var app = builder.Build();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("healthy")));

app.MapGet(
    "/api/v1/capabilities",
    () => TypedResults.Ok(
        new ServerCapabilitiesResponse(
            ProtocolVersions.Current,
            ProtocolVersions.MinimumSupported,
            "v1-scalar-tick",
            ["thin-client", "deterministic-tick", "string-safe-64-bit-identifiers"])));

app.MapGet(
    "/api/v1/worlds/active",
    (WorldRunner runner) =>
    {
        var snapshot = runner.CaptureSnapshot();
        return TypedResults.Ok(
            new ActiveWorldResponse(
                snapshot.WorldId.ToString(),
                snapshot.CompletedTick.ToString(CultureInfo.InvariantCulture),
                snapshot.WorldRevision.ToString(CultureInfo.InvariantCulture),
                snapshot.StateHash,
                runner.Status == WorldRunnerStatus.PausedReady ? "paused-ready" : "faulted"));
    });

app.MapGet(
    "/api/v1/worlds/active/projection",
    (WorldRunner runner) =>
    {
        var source = runner.CapturePublicationSnapshot();
        var controlledSpeciesId = source.Gameplay.ControlledSpeciesId;
        var projection = DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(
                controlledSpeciesId,
                ImmutableArray<DiscoveredTileKnowledge>.Empty));
        var stream = ProjectionDeltaBuilder.CreateSnapshot(1, 1, projection);
        var payload = ProjectionProtocolMapper.ToProtocol(stream).ToByteArray();
        return Results.Bytes(payload, "application/x-protobuf");
    });

app.Run();

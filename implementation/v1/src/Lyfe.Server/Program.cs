using System.Globalization;
using System.Collections.Immutable;
using Google.Protobuf;
using Lyfe.Protocol;
using Lyfe.Server;
using Lyfe.Server.Evolution;
using Lyfe.Server.Projection;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.World;
using Proto = Lyfe.Protocol.V1;

var builder = WebApplication.CreateBuilder(args);

var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
builder.Services.AddSingleton(WorldRunner.CreateFoundation(
    WorldId.From(1),
    rules,
    RootRandomSeed.Parse("00000000000000000000000000000001")));
builder.Services.AddSingleton<EvolutionProtocolService>();

var app = builder.Build();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("healthy")));

app.MapGet(
    "/api/v1/capabilities",
    () => TypedResults.Ok(
        new ServerCapabilitiesResponse(
            ProtocolVersions.Current,
            ProtocolVersions.MinimumSupported,
            "v1-scalar-tick",
            ["thin-client", "deterministic-tick", "string-safe-64-bit-identifiers",
                "evolution-preview-apply"])));

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

app.MapGet(
    "/api/v1/worlds/active/evolution",
    (WorldRunner runner, EvolutionProtocolService evolution) =>
        Protobuf(evolution.CaptureDecisionSurface(runner)));

app.MapPost(
    "/api/v1/worlds/active/evolution/preview",
    async (HttpRequest request, WorldRunner runner, EvolutionProtocolService evolution) =>
        await HandleEvolutionRequest(
            async () => evolution.Preview(
                runner,
                Proto.SpeciationProposalRequest.Parser.ParseFrom(
                    await ByteString.FromStreamAsync(
                        request.Body,
                        request.HttpContext.RequestAborted)))));

app.MapPost(
    "/api/v1/worlds/active/evolution/apply",
    async (HttpRequest request, WorldRunner runner, EvolutionProtocolService evolution) =>
        await HandleEvolutionRequest(
            async () => evolution.Apply(
                runner,
                Proto.ApplySpeciationRequest.Parser.ParseFrom(
                    await ByteString.FromStreamAsync(
                        request.Body,
                        request.HttpContext.RequestAborted)))));

app.Run();

static async Task<IResult> HandleEvolutionRequest(Func<Task<IMessage>> action)
{
    try
    {
        return Protobuf(await action());
    }
    catch (EvolutionCommandConflictException exception)
    {
        return Results.Problem(
            exception.Message,
            statusCode: StatusCodes.Status409Conflict);
    }
    catch (Exception exception) when (exception is InvalidProtocolBufferException or
        ArgumentException or InvalidOperationException)
    {
        return Results.Problem(
            exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
    }
}

static IResult Protobuf(IMessage message) =>
    Results.Bytes(message.ToByteArray(), "application/x-protobuf");

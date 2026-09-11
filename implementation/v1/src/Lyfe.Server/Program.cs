using System.Globalization;
using System.Collections.Immutable;
using Google.Protobuf;
using Lyfe.Protocol;
using Lyfe.Server;
using Lyfe.Server.Evolution;
using Lyfe.Server.Persistence;
using Lyfe.Server.Projection;
using Proto = Lyfe.Protocol.V1;

var builder = WebApplication.CreateBuilder(args);

var rules = FoundationWorldBootstrap.LoadGeneratedRules(AppContext.BaseDirectory);
var saveDirectory = builder.Configuration["LYFE_DATA_DIR"] ??
    (builder.Environment.IsProduction()
        ? "/var/lib/lyfe"
        : Path.Combine(AppContext.BaseDirectory, "data"));
builder.Services.AddSingleton(rules);
builder.Services.AddSingleton<EvolutionProtocolService>();
builder.Services.AddSingleton<WorldClockService>();
builder.Services.AddHostedService<WorldClockService>(provider =>
    provider.GetRequiredService<WorldClockService>());
builder.Services.AddSingleton(provider => new WorldCatalogueService(
    provider.GetRequiredService<Lyfe.Simulation.Rules.Runtime.CompiledWorldRules>(),
    provider.GetRequiredService<WorldClockService>(),
    saveDirectory));
builder.Services.AddSingleton<WorldSetupService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (WorldControlRejectedException exception)
        when (!context.RequestServices.GetRequiredService<WorldClockService>().HasActiveWorld)
    {
        await Results.Problem(
            exception.Message,
            statusCode: StatusCodes.Status404NotFound).ExecuteAsync(context);
    }
});

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("healthy")));

app.MapGet(
    "/api/v1/capabilities",
    () => TypedResults.Ok(
        new ServerCapabilitiesResponse(
            ProtocolVersions.Current,
            ProtocolVersions.MinimumSupported,
            "v1-scalar-tick",
            ["thin-client", "deterministic-tick", "string-safe-64-bit-identifiers",
                "evolution-preview-apply", "authoritative-world-control",
                "authoritative-world-setup", "authoritative-world-persistence"])));

app.MapGet(
    "/api/v1/worlds",
    (WorldCatalogueService catalogue) => Protobuf(catalogue.Capture()));

app.MapGet(
    "/api/v1/worlds/setup",
    (HttpRequest request, WorldSetupService setup) =>
    {
        try
        {
            return Protobuf(setup.Capture(request.Query["seed"].FirstOrDefault()));
        }
        catch (Exception exception) when (exception is ArgumentException or
            FormatException or InvalidOperationException)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    });

app.MapPost(
    "/api/v1/worlds",
    async (HttpRequest request, WorldSetupService setup) =>
    {
        try
        {
            var command = Proto.CreateWorldRequest.Parser.ParseFrom(
                await ByteString.FromStreamAsync(
                    request.Body,
                    request.HttpContext.RequestAborted));
            return Protobuf(setup.Create(command));
        }
        catch (Exception exception) when (exception is InvalidProtocolBufferException or
            ArgumentException or InvalidOperationException)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    });

app.MapPost(
    "/api/v1/worlds/active/save",
    async (HttpRequest request, WorldCatalogueService catalogue) =>
        await HandlePersistenceRequest(async () =>
        {
            var command = Proto.SaveActiveWorldRequest.Parser.ParseFrom(
                await ByteString.FromStreamAsync(
                    request.Body,
                    request.HttpContext.RequestAborted));
            return await catalogue.SaveActiveAsync(
                command,
                request.HttpContext.RequestAborted);
        }));

app.MapPost(
    "/api/v1/worlds/load",
    async (HttpRequest request, WorldCatalogueService catalogue) =>
        await HandlePersistenceRequest(async () =>
        {
            var command = Proto.LoadWorldRequest.Parser.ParseFrom(
                await ByteString.FromStreamAsync(
                    request.Body,
                    request.HttpContext.RequestAborted));
            return await catalogue.LoadAsync(
                command,
                request.HttpContext.RequestAborted);
        }));

app.MapPost(
    "/api/v1/worlds/active/unload",
    async (HttpRequest request, WorldCatalogueService catalogue) =>
        await HandlePersistenceRequest(async () =>
        {
            var command = Proto.UnloadWorldRequest.Parser.ParseFrom(
                await ByteString.FromStreamAsync(
                    request.Body,
                    request.HttpContext.RequestAborted));
            return await catalogue.UnloadAsync(
                command,
                request.HttpContext.RequestAborted);
        }));

app.MapGet(
    "/api/v1/worlds/active",
    (WorldClockService clock) =>
        clock.ReadBoundary((runner, publication, control) =>
            TypedResults.Ok(
                new ActiveWorldResponse(
                    publication.WorldId.ToString(),
                    publication.CompletedTick.ToString(CultureInfo.InvariantCulture),
                    publication.WorldRevision.ToString(CultureInfo.InvariantCulture),
                    runner.CaptureSnapshot().StateHash,
                    control.Lifecycle == Proto.WorldLifecycle.Running
                        ? "running"
                        : "paused-ready"))));

app.MapGet(
    "/api/v1/worlds/active/projection",
    (WorldClockService clock) =>
        clock.ReadBoundary((source, control) =>
        {
            var projection = Project(source, control);
            var stream = ProjectionDeltaBuilder.CreateSnapshot(1, 1, projection);
            return Protobuf(ProjectionProtocolMapper.ToProtocol(stream));
        }));

app.MapGet(
    "/api/v1/worlds/active/state",
    (WorldClockService clock, EvolutionProtocolService evolution) =>
        clock.ReadBoundary((runner, source, control) =>
        {
            var stream = ProjectionDeltaBuilder.CreateSnapshot(
                1,
                1,
                Project(source, control));
            return Protobuf(new Proto.ActiveWorldState
            {
                Projection = ProjectionProtocolMapper.ToProtocol(stream),
                Evolution = evolution.CaptureDecisionSurface(runner, source),
                Control = control,
            });
        }));

app.MapGet(
    "/api/v1/worlds/active/evolution",
    (WorldClockService clock, EvolutionProtocolService evolution) =>
        clock.ReadBoundary((runner, source, _) =>
            Protobuf(evolution.CaptureDecisionSurface(runner, source))));

app.MapPost(
    "/api/v1/worlds/active/control",
    async (HttpRequest request, WorldClockService clock) =>
    {
        try
        {
            var command = Proto.WorldControlCommand.Parser.ParseFrom(
                await ByteString.FromStreamAsync(
                    request.Body,
                    request.HttpContext.RequestAborted));
            return Protobuf(clock.Apply(command));
        }
        catch (WorldControlConflictException exception)
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
    });

app.MapPost(
    "/api/v1/worlds/active/evolution/preview",
    async (HttpRequest request, WorldClockService clock,
        EvolutionProtocolService evolution) =>
        await HandleEvolutionRequest(
            async () =>
            {
                var command = Proto.SpeciationProposalRequest.Parser.ParseFrom(
                    await ByteString.FromStreamAsync(
                        request.Body,
                        request.HttpContext.RequestAborted));
                return clock.ExecutePaused(runner => evolution.Preview(runner, command));
            }));

app.MapPost(
    "/api/v1/worlds/active/evolution/apply",
    async (HttpRequest request, WorldClockService clock,
        EvolutionProtocolService evolution) =>
        await HandleEvolutionRequest(
            async () =>
            {
                var command = Proto.ApplySpeciationRequest.Parser.ParseFrom(
                    await ByteString.FromStreamAsync(
                        request.Body,
                        request.HttpContext.RequestAborted));
                return clock.ExecutePaused(runner => evolution.Apply(runner, command));
            }));

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

static async Task<IResult> HandlePersistenceRequest(Func<Task<IMessage>> action)
{
    try
    {
        return Protobuf(await action());
    }
    catch (Exception exception) when (exception is WorldControlConflictException or
        WorldReplacementConfirmationException)
    {
        return Results.Problem(
            exception.Message,
            statusCode: StatusCodes.Status409Conflict);
    }
    catch (SavedWorldNotFoundException exception)
    {
        return Results.Problem(
            exception.Message,
            statusCode: StatusCodes.Status404NotFound);
    }
    catch (Exception exception) when (exception is SaveEnvelopeException or
        WorldPayloadException or Lyfe.Simulation.World.WorldRestoreException or
        InvalidDataException)
    {
        return Results.Problem(
            "The saved world is damaged or incompatible with this server.",
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    catch (Exception exception) when (exception is InvalidProtocolBufferException or
        ArgumentException or WorldControlRejectedException)
    {
        return Results.Problem(
            exception.Message,
            statusCode: StatusCodes.Status400BadRequest);
    }
}

static IResult Protobuf(IMessage message) =>
    Results.Bytes(message.ToByteArray(), "application/x-protobuf");

static ActorWorldProjection Project(
    Lyfe.Simulation.Publication.WorldPublicationSnapshot source,
    Proto.WorldControlState control)
{
    var controlledSpeciesId = source.Gameplay.ControlledSpeciesId;
    return DirectWorldProjector.Project(
        source,
        new ActorKnowledgeSnapshot(
            controlledSpeciesId,
            ImmutableArray<DiscoveredTileKnowledge>.Empty)) with
    {
        Lifecycle = control.Lifecycle == Proto.WorldLifecycle.Running
            ? Lyfe.Simulation.Publication.PublicationWorldLifecycle.Running
            : Lyfe.Simulation.Publication.PublicationWorldLifecycle.PausedReady,
    };
}

using System.Collections.Immutable;
using Lyfe.Server.Projection;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Server.Tests;

public sealed class DirectWorldProjectorTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("0123456789abcdeffedcba9876543210");

    [Fact]
    public void ProjectionUsesDistinctLiveReducedAndUnknownShapes()
    {
        var runner = CreateRunner();
        var source = runner.CapturePublicationSnapshot();
        var controlled = Assert.Single(source.Species).SpeciesId;
        var reducedSource = source.Tiles[1];
        var knownResources = reducedSource.ResourceStocks
            .Where(stock => stock.QuantityQ > 0)
            .Select(stock => stock.ResourceId)
            .Reverse()
            .ToImmutableArray();
        var knowledge = new ActorKnowledgeSnapshot(
            controlled,
            [new DiscoveredTileKnowledge(reducedSource.TileId, 0, knownResources)]);

        var projection = DirectWorldProjector.Project(source, knowledge);

        Assert.Equal(source.WorldId, projection.WorldId);
        Assert.Equal(source.CompletedTick, projection.CompletedTick);
        Assert.Equal(source.WorldRevision, projection.WorldRevision);
        Assert.Equal(source.WorldRulesHash, projection.WorldRulesHash);
        Assert.Equal(controlled, projection.ControlledSpeciesId);
        Assert.Equal([0U, 1U, 2U, 3U], projection.Tiles.Select(tile => tile.TileId.Value));

        var live = Assert.IsType<LiveTileProjection>(projection.Tiles[0]);
        Assert.Equal(source.CompletedTick, live.ObservedAtTick);
        Assert.Equal(source.Tiles[0].ResourceStocks.Length, live.ResourceStocks.Length);
        Assert.Equal(100, live.Organisms.Length);
        Assert.All(live.Organisms, organism => Assert.Equal(controlled, organism.SpeciesId));

        var reduced = Assert.IsType<ReducedTileProjection>(projection.Tiles[1]);
        Assert.Equal(0UL, reduced.ObservedAtTick);
        Assert.Equal(
            knownResources.OrderBy(id => id.Value),
            reduced.KnownPresentResourceIds);
        Assert.IsType<UnknownTileProjection>(projection.Tiles[2]);
        Assert.IsType<UnknownTileProjection>(projection.Tiles[3]);

        var species = Assert.Single(projection.Species);
        Assert.Equal(controlled, species.SpeciesId);
        Assert.Equal(SpeciesPopulationScope.WorldExact, species.PopulationScope);
        Assert.Equal(100UL, species.Population);
        Assert.DoesNotContain(
            typeof(ActorWorldProjection).GetProperties(),
            property => property.Name.Contains("StateHash", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(ReducedTileProjection).GetProperties(),
            property => property.Name is "ResourceStocks" or "Organisms");
        Assert.DoesNotContain(
            typeof(UnknownTileProjection).GetProperties(),
            property => property.Name is "ElevationMeters" or "ResourceStocks" or "Organisms");
    }

    [Fact]
    public void ProjectionIsCanonicalAcrossDetachedSourceOrdering()
    {
        var source = CreateRunner().CapturePublicationSnapshot();
        var controlled = Assert.Single(source.Species).SpeciesId;
        var knowledge = new ActorKnowledgeSnapshot(
            controlled,
            [
                new DiscoveredTileKnowledge(
                    source.Tiles[1].TileId,
                    0,
                    source.Tiles[1].ResourceStocks
                        .Where(stock => stock.QuantityQ > 0)
                        .Select(stock => stock.ResourceId)
                        .Reverse()
                        .ToImmutableArray()),
            ]);
        var permuted = source with
        {
            Tiles = source.Tiles
                .Reverse()
                .Select(tile => tile with
                {
                    ResourceStocks = tile.ResourceStocks.Reverse().ToImmutableArray(),
                })
                .ToImmutableArray(),
            Species = source.Species.Reverse().ToImmutableArray(),
            Organisms = source.Organisms.Reverse().ToImmutableArray(),
        };

        var first = DirectWorldProjector.Project(source, knowledge);
        var second = DirectWorldProjector.Project(permuted, knowledge);

        Assert.Equal(ProjectionSignature(first), ProjectionSignature(second));
    }

    [Fact]
    public void ProjectionIsReadOnlyAndPublicationSnapshotsAreDetached()
    {
        var runner = CreateRunner();
        var boundaryBefore = runner.CaptureSnapshot();
        var sourceBefore = runner.CapturePublicationSnapshot();
        var controlled = Assert.Single(sourceBefore.Species).SpeciesId;
        var knowledge = new ActorKnowledgeSnapshot(controlled, []);

        _ = DirectWorldProjector.Project(sourceBefore, knowledge);

        Assert.Equal(boundaryBefore, runner.CaptureSnapshot());
        Assert.All(sourceBefore.Organisms, organism => Assert.Equal(0UL, organism.BiologicalAgeHours));

        runner.AdvanceOneTick();
        var sourceAfter = runner.CapturePublicationSnapshot();

        Assert.All(sourceBefore.Organisms, organism => Assert.Equal(0UL, organism.BiologicalAgeHours));
        Assert.All(sourceAfter.Organisms, organism => Assert.Equal(1UL, organism.BiologicalAgeHours));
    }

    [Fact]
    public void ProjectionRejectsInvalidActorKnowledge()
    {
        var source = CreateRunner().CapturePublicationSnapshot();
        var controlled = Assert.Single(source.Species).SpeciesId;
        var tile = source.Tiles[1];
        var valid = new DiscoveredTileKnowledge(tile.TileId, 0, []);

        Assert.Throws<ArgumentException>(() => DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(controlled, [valid, valid])));
        Assert.Throws<ArgumentException>(() => DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(
                controlled,
                [valid with { ObservedAtTick = source.CompletedTick + 1 }])));
        Assert.Throws<ArgumentException>(() => DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(
                controlled,
                [valid with { KnownPresentResourceIds = [ResourceId.From(999)] }])));
    }

    [Fact]
    public void PublicPublicationAndProjectionShapesContainNoDenseLocations()
    {
        var types = new[]
        {
            typeof(WorldPublicationSnapshot),
            typeof(PublicationTile),
            typeof(PublicationSpecies),
            typeof(PublicationOrganism),
            typeof(ActorWorldProjection),
            typeof(UnknownTileProjection),
            typeof(ReducedTileProjection),
            typeof(LiveTileProjection),
            typeof(OrganismProjection),
            typeof(SpeciesProjection),
        };

        Assert.All(
            types,
            type => Assert.DoesNotContain(
                type.GetProperties(),
                property =>
                    property.Name.Contains("Dense", StringComparison.Ordinal) ||
                    property.Name.Contains("Slot", StringComparison.Ordinal) ||
                    property.Name.Contains("Chunk", StringComparison.Ordinal) ||
                    property.Name.Contains("RowIndex", StringComparison.Ordinal)));
    }

    private static WorldRunner CreateRunner() =>
        WorldRunner.CreateFoundation(WorldId.From(1), CompileWorld(), Seed);

    private static CompiledWorldRules CompileWorld()
    {
        var sourceRules = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(sourceRules.IsSuccess, FormatDiagnostics(sourceRules.Diagnostics));
        var compiledRules = RulePackCompiler.Compile(
            Assert.IsType<AuthoringRulePack>(sourceRules.Pack));
        Assert.True(compiledRules.IsSuccess, FormatDiagnostics(compiledRules.Diagnostics));

        var sourceWorld = WorldPackSourceLoader.Load(new CopiedPackageSource("OfficialWorld"));
        Assert.True(sourceWorld.IsSuccess, FormatDiagnostics(sourceWorld.Diagnostics));
        var authoringWorld = Assert.IsType<AuthoringWorldPack>(sourceWorld.Pack);
        var profile = Assert.Single(authoringWorld.Profiles);
        var firstTile = Assert.Single(profile.Tiles!);
        authoringWorld = authoringWorld with
        {
            Profiles =
            [
                profile with
                {
                    Width = 4,
                    Height = 1,
                    WrapX = true,
                    WrapY = false,
                    Tiles = Enumerable.Range(0, 4)
                        .Select(index => firstTile with
                        {
                            X = index,
                            ElevationMeters = firstTile.ElevationMeters - index,
                        })
                        .ToArray(),
                },
            ],
        };

        var compiledWorld = WorldRulesCompiler.Compile(
            Assert.IsType<CompiledRulePack>(compiledRules.RulePack),
            authoringWorld,
            "scenario.foundation-sandbox",
            "world.primordial-foundation");
        Assert.True(compiledWorld.IsSuccess, FormatDiagnostics(compiledWorld.Diagnostics));
        return Assert.IsType<CompiledWorldRules>(compiledWorld.WorldRules);
    }

    private static string ProjectionSignature(ActorWorldProjection projection) =>
        string.Join(
            ';',
            projection.Tiles.Select(TileSignature)
                .Concat(projection.Species.Select(species =>
                    $"S:{species.SpeciesId.Value}:{(byte)species.PopulationScope}:{species.Population}")));

    private static string TileSignature(TileProjection tile) =>
        tile switch
        {
            UnknownTileProjection unknown =>
                $"U:{unknown.TileId.Value}:{unknown.X}:{unknown.Y}",
            ReducedTileProjection reduced =>
                $"R:{reduced.TileId.Value}:{reduced.X}:{reduced.Y}:{reduced.ElevationMeters}:" +
                $"{reduced.ObservedAtTick}:" +
                string.Join(',', reduced.KnownPresentResourceIds.Select(id => id.Value)),
            LiveTileProjection live =>
                $"L:{live.TileId.Value}:{live.X}:{live.Y}:{live.ElevationMeters}:" +
                $"{live.ObservedAtTick}:" +
                string.Join(',', live.ResourceStocks.Select(stock =>
                    $"{stock.ResourceId.Value}={stock.QuantityQ}")) +
                ':' +
                string.Join(',', live.Organisms.Select(organism =>
                    $"{organism.OrganismId.Value}={organism.SpeciesId.Value}=" +
                    $"{organism.PositionXQ}={organism.PositionYQ}=" +
                    $"{organism.BiologicalAgeHours}={organism.ChargedReserveQ}")),
            _ => throw new InvalidOperationException("Unknown projection shape."),
        };

    private static string FormatDiagnostics(IEnumerable<RuleDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"{diagnostic.SourceFile}: {diagnostic.Code}: {diagnostic.Message}"));

    private sealed class CopiedPackageSource : IContentSource
    {
        private readonly string root;

        public CopiedPackageSource(string directoryName) =>
            root = Path.Combine(AppContext.BaseDirectory, directoryName);

        public bool TryRead(string normalizedRelativePath, out ReadOnlyMemory<byte> content)
        {
            var path = Path.Combine(
                root,
                normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                content = default;
                return false;
            }

            content = File.ReadAllBytes(path);
            return true;
        }
    }
}

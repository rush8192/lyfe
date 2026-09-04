using System.Collections.Immutable;
using Google.Protobuf;
using Lyfe.Server.Projection;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Server.Tests;

public sealed class ProjectionProtocolMapperTests
{
    [Fact]
    public void GeneratedProjectionRoundTripsAllVisibilityShapesAndExactIntegers()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var runner = WorldRunner.CreateFoundation(
            WorldId.From(1),
            rules,
            RootRandomSeed.Parse("00112233445566778899aabbccddeeff"));
        runner.AdvanceOneTick();
        var source = runner.CapturePublicationSnapshot();
        var controlled = source.Species[0].SpeciesId;
        var direct = DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(
                controlled,
                ImmutableArray<DiscoveredTileKnowledge>.Empty));
        var live = Assert.IsType<LiveTileProjection>(direct.Tiles[0]);
        var expanded = direct with
        {
            Width = 3,
            Tiles =
            [
                live,
                new ReducedTileProjection(
                    TileId.FromRowMajorIndex(1),
                    1,
                    0,
                    -100,
                    direct.CompletedTick,
                    [ResourceId.From(1)]),
                new UnknownTileProjection(TileId.FromRowMajorIndex(2), 2, 0),
            ],
        };

        var message = ProjectionProtocolMapper.ToProtocol(expanded);
        var bytes = message.ToByteArray();
        var decoded = Lyfe.Protocol.V1.ActorWorldProjection.Parser.ParseFrom(bytes);

        Assert.Equal(message, decoded);
        Assert.Equal(
            Lyfe.Protocol.V1.TileProjection.DetailOneofCase.Live,
            decoded.Tiles[0].DetailCase);
        Assert.Equal(
            Lyfe.Protocol.V1.TileProjection.DetailOneofCase.Reduced,
            decoded.Tiles[1].DetailCase);
        Assert.Equal(
            Lyfe.Protocol.V1.TileProjection.DetailOneofCase.Unknown,
            decoded.Tiles[2].DetailCase);
        Assert.Equal(
            typeof(ulong),
            typeof(Lyfe.Protocol.V1.ActorWorldProjection).GetProperty("WorldId")!.PropertyType);
        Assert.Equal(
            typeof(long),
            typeof(Lyfe.Protocol.V1.ExactResourceStock).GetProperty("QuantityQ")!.PropertyType);
        Assert.DoesNotContain(
            System.Text.Encoding.UTF8.GetString(bytes),
            runner.CaptureSnapshot().StateHash,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessiveAuthoritativeProjectionsProduceOneAbsoluteBatch()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var runner = WorldRunner.CreateFoundation(
            WorldId.From(5),
            rules,
            RootRandomSeed.Parse("00112233445566778899aabbccddeeff"));
        var source = runner.CapturePublicationSnapshot();
        var controlled = source.Species[0].SpeciesId;
        var knowledge = new ActorKnowledgeSnapshot(
            controlled,
            ImmutableArray<DiscoveredTileKnowledge>.Empty);
        var initial = DirectWorldProjector.Project(source, knowledge);
        var stream = ProjectionDeltaBuilder.CreateSnapshot(91, 1, initial);

        runner.AdvanceOneTick();
        var current = DirectWorldProjector.Project(
            runner.CapturePublicationSnapshot(),
            knowledge);
        var delta = ProjectionDeltaBuilder.CreateDelta(stream, current);
        var protocol = ProjectionProtocolMapper.ToProtocol(delta);
        var decoded = Lyfe.Protocol.V1.ProjectionBatch.Parser.ParseFrom(
            protocol.ToByteArray());

        Assert.Equal(1UL, delta.BaseStreamRevision);
        Assert.Equal(2UL, delta.TargetStreamRevision);
        Assert.Single(delta.TileReplacements);
        Assert.Empty(delta.RemovedTileIds);
        Assert.Empty(delta.SpeciesReplacements);
        Assert.Empty(delta.RemovedSpeciesIds);
        Assert.Equal(protocol, decoded);
        Assert.Equal(100, decoded.TileReplacements[0].Live.Organisms.Count);
        Assert.All(
            decoded.TileReplacements[0].Live.Organisms,
            organism => Assert.Equal(1UL, organism.BiologicalAgeHours));
    }
}

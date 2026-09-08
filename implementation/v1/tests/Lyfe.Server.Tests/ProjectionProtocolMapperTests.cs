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
        var decodedOrganism = decoded.Tiles[0].Live.Organisms[0];
        Assert.Equal(10_000, decodedOrganism.ChargedReserveCapacityQ);
        Assert.Equal(487_593U, decodedOrganism.RelativeHealthQ);
        Assert.Equal(504_400U, decodedOrganism.ReserveFactorQ);
        Assert.Equal(4_198_494U, decodedOrganism.BodyRadiusQ);
        Assert.Equal(Lyfe.Protocol.V1.OrganismBehavior.Baseline, decodedOrganism.Behavior);
        Assert.Equal(1_000_000U, decodedOrganism.RecentEnergyCoverageQ);
        Assert.Single(decoded.Tiles[0].Live.BehaviorDistributions);
        Assert.Equal(100UL,
            decoded.Tiles[0].Live.BehaviorDistributions[0].TotalObservedOrganisms);
        Assert.Equal(100UL, decoded.Species[0].BehaviorCounts[0].Count);
        Assert.Equal(Lyfe.Protocol.V1.GameMode.FreeSandbox, decoded.Gameplay.Mode);
        Assert.Equal(Lyfe.Protocol.V1.GameRunStatus.Active, decoded.Gameplay.RunStatus);
        Assert.Equal(controlled.Value, decoded.Gameplay.ControlledSpeciesId);
        Assert.Single(decoded.Gameplay.Roots);
        Assert.Equal(1U, decoded.Gameplay.Roots[0].FounderAllocationId);
        Assert.Equal(1U, decoded.Species[0].Evolution.FounderGenomeId);
        Assert.Equal(1U, decoded.Species[0].Evolution.FounderAllocationId);
        Assert.Empty(decoded.Tiles[0].Live.Remnants);
        Assert.Equal(100, decoded.JourneyEvents.Count(value =>
            value.Family == Lyfe.Protocol.V1.OrganismJourneyEventFamily.Birth));
        Assert.All(decoded.JourneyEvents.Where(value => value.Tick == 0), value => Assert.Equal(
            Lyfe.Protocol.V1.OrganismJourneyEventFamily.Birth,
            value.Family));
        Assert.DoesNotContain(
            System.Text.Encoding.UTF8.GetString(bytes),
            runner.CaptureSnapshot().StateHash,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedProjectionRoundTripsLiveRemnants()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var runner = WorldRunner.CreateFoundation(
            WorldId.From(2),
            rules,
            RootRandomSeed.Parse("00112233445566778899aabbccddeeff"));
        var source = runner.CapturePublicationSnapshot();
        do
        {
            runner.AdvanceOneTick();
            source = runner.CapturePublicationSnapshot();
        }
        while (source.Remnants.IsEmpty && source.CompletedTick < 2_000);

        Assert.NotEmpty(source.Remnants);
        Assert.NotEmpty(source.Organisms);
        var controlled = Assert.Single(source.Species).SpeciesId;
        var projection = DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(controlled, []));
        var decoded = Lyfe.Protocol.V1.ActorWorldProjection.Parser.ParseFrom(
            ProjectionProtocolMapper.ToProtocol(projection).ToByteArray());

        var live = decoded.Tiles.Single(tile => tile.DetailCase ==
            Lyfe.Protocol.V1.TileProjection.DetailOneofCase.Live).Live;
        Assert.Equal(source.Remnants.Length, live.Remnants.Count);
        Assert.Equal(source.Remnants[0].RemnantId.Value, live.Remnants[0].RemnantId);
        Assert.Equal(source.Remnants[0].StructuralMatterQ,
            live.Remnants[0].StructuralMatterQ);
        Assert.Equal(source.Remnants[0].BodyRadiusQ, live.Remnants[0].BodyRadiusQ);
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
        var speciesReplacement = Assert.Single(delta.SpeciesReplacements);
        Assert.Equal(controlled, speciesReplacement.SpeciesId);
        Assert.NotNull(speciesReplacement.Evolution);
        Assert.Equal(1UL, speciesReplacement.Evolution.EvolutionRevision);
        Assert.Empty(delta.RemovedSpeciesIds);
        Assert.Equal(controlled.Value, decoded.Gameplay.ControlledSpeciesId);
        Assert.Equal(1UL, decoded.Gameplay.GameplayRevision);
        Assert.Equal(protocol, decoded);
        Assert.Equal(100, decoded.TileReplacements[0].Live.Organisms.Count);
        Assert.NotEmpty(decoded.JourneyEventAppends);
        Assert.All(decoded.JourneyEventAppends, value => Assert.Equal(1UL, value.Tick));
        Assert.All(
            decoded.TileReplacements[0].Live.Organisms,
            organism => Assert.Equal(1UL, organism.BiologicalAgeHours));
    }
}

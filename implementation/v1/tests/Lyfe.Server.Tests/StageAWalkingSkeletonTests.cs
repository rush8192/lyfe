using System.Collections.Immutable;
using Google.Protobuf;
using Lyfe.Server.Persistence;
using Lyfe.Server.Projection;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Server.Tests;

public sealed class StageAWalkingSkeletonTests
{
    [Fact]
    public async Task SaveReloadProjectionAndNextDeltaComposeEndToEnd()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var original = WorldRunner.CreateFoundation(
            WorldId.From(101),
            rules,
            RootRandomSeed.Parse("00112233445566778899aabbccddeeff"));
        original.AdvanceOneTick();
        original.AdvanceOneTick();

        var directory = Path.Combine(
            Path.GetTempPath(),
            $"lyfe-stage-a-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var savePath = Path.Combine(directory, "world.lyfe");
        try
        {
            await WorldSaveService.SaveAsync(
                original,
                savePath,
                TestContext.Current.CancellationToken);
            var restored = WorldSaveService.Load(savePath, rules);
            Assert.Equal(original.CaptureSnapshot(), restored.CaptureSnapshot());

            var publication = restored.CapturePublicationSnapshot();
            var knowledge = new ActorKnowledgeSnapshot(
                publication.Species[0].SpeciesId,
                ImmutableArray<DiscoveredTileKnowledge>.Empty);
            var initialProjection = DirectWorldProjector.Project(publication, knowledge);
            var initialStream = ProjectionDeltaBuilder.CreateSnapshot(1, 1, initialProjection);
            var snapshotBytes = ProjectionProtocolMapper.ToProtocol(initialStream).ToByteArray();
            var decodedSnapshot = Lyfe.Protocol.V1.ProjectionSnapshot.Parser.ParseFrom(snapshotBytes);

            Assert.Equal(1UL, decodedSnapshot.StreamRevision);
            Assert.Equal(100, decodedSnapshot.Projection.Tiles[0].Live.Organisms.Count);
            Assert.NotEmpty(decodedSnapshot.Projection.JourneyEvents);

            var expectedNext = original.AdvanceOneTick();
            var restoredNext = restored.AdvanceOneTick();
            Assert.Equal(expectedNext.Snapshot, restoredNext.Snapshot);

            var nextProjection = DirectWorldProjector.Project(
                restored.CapturePublicationSnapshot(),
                knowledge);
            var delta = ProjectionDeltaBuilder.CreateDelta(initialStream, nextProjection);
            var batchBytes = ProjectionProtocolMapper.ToProtocol(delta).ToByteArray();
            var decodedBatch = Lyfe.Protocol.V1.ProjectionBatch.Parser.ParseFrom(batchBytes);

            Assert.Equal(1UL, decodedBatch.BaseStreamRevision);
            Assert.Equal(2UL, decodedBatch.TargetStreamRevision);
            Assert.Single(decodedBatch.TileReplacements);
            Assert.Empty(decodedBatch.JourneyEventAppends);
            Assert.NotEmpty(decodedBatch.RoutineActivitySummaries);
            Assert.NotEmpty(decodedBatch.ActivityPulseEvents);
            Assert.Equal(100, decodedBatch.TileReplacements[0].Live.Organisms.Count);
            Assert.All(
                decodedBatch.TileReplacements[0].Live.Organisms,
                organism => Assert.Equal(3UL, organism.BiologicalAgeHours));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

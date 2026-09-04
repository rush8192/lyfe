using Lyfe.Simulation.Core;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.World;

namespace Lyfe.Server.Persistence;

public static class WorldSaveService
{
    public static async Task<SaveEnvelopeDescriptor> SaveAsync(
        WorldRunner runner,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runner);
        var detached = runner.CapturePersistenceSnapshot();
        var metadata = SaveEnvelopeMetadataFactory.Create(detached.Metadata);
        var logicalPayload = WorldPayloadCodecV1.Encode(detached.State);
        var envelope = SaveEnvelopeCodec.Encode(metadata, logicalPayload);
        await AtomicSaveFile.WriteAsync(
            destinationPath,
            envelope,
            cancellationToken).ConfigureAwait(false);

        using var descriptorStream = new MemoryStream(envelope, writable: false);
        return SaveEnvelopeCodec.ReadDescriptor(descriptorStream, metadata.Compatibility);
    }

    public static WorldRunner Load(
        string sourcePath,
        CompiledWorldRules rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(rules);
        var expectedCompatibility = SaveEnvelopeMetadataFactory.CreateCompatibility(rules);

        using var stream = new FileStream(
            Path.GetFullPath(sourcePath),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        _ = SaveEnvelopeCodec.ReadDescriptor(stream, expectedCompatibility);
        stream.Position = 0;
        var decoded = SaveEnvelopeCodec.Decode(stream, expectedCompatibility);
        var state = WorldPayloadCodecV1.Decode(decoded.LogicalPayload);
        return WorldRunner.Restore(
            rules,
            new WorldPersistenceSnapshot(
                ToSimulationMetadata(decoded.Descriptor.Metadata, rules),
                state));
    }

    private static WorldPersistenceMetadata ToSimulationMetadata(
        SaveEnvelopeMetadata metadata,
        CompiledWorldRules rules)
    {
        var seed = RootRandomSeed.Parse(metadata.RootRandomSeed);
        return new WorldPersistenceMetadata(
            new WorldSnapshot(
                WorldId.From(metadata.WorldId),
                metadata.CompletedTick,
                metadata.WorldRevision,
                metadata.SimulatedHours,
                metadata.TickDurationHours,
                metadata.OrganismCount,
                metadata.WorldStateHash),
            (WorldRunnerStatus)metadata.WorldLifecycle,
            metadata.Compatibility.EngineSimulationVersion,
            metadata.Compatibility.WorldStateHashSchemaVersion,
            RandomCompatibility.ForSeed(seed),
            rules.RulePack.Identity,
            rules.Scenario.Id,
            rules.Identity);
    }
}

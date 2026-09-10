using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Lyfe.Server.Persistence;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Server.Tests;

public sealed class SaveEnvelopeTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("00112233445566778899aabbccddeeff");

    [Fact]
    public void MetadataCanBeReadAndCompatibilityCheckedWithoutReadingPayload()
    {
        var metadata = CreateMetadata();
        var payload = Enumerable.Range(0, 4096)
            .Select(index => checked((byte)(index % 251)))
            .ToArray();
        var encoded = SaveEnvelopeCodec.Encode(metadata, payload);

        Assert.Equal("LYFESV1\0", Encoding.ASCII.GetString(encoded, 0, 8));
        Assert.Equal(
            SaveEnvelopeCodec.ContainerVersion,
            BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(8, 4)));
        using var descriptorStream = new MemoryStream(encoded, writable: false);
        var descriptor = SaveEnvelopeCodec.ReadDescriptor(
            descriptorStream,
            metadata.Compatibility);

        Assert.Equal(metadata, descriptor.Metadata);
        Assert.Equal(SaveCompressionKind.Brotli, descriptor.Compression);
        Assert.Equal((ulong)payload.Length, descriptor.LogicalPayloadLength);
        Assert.Equal(
            "c935f09c8eea5f8338a7b2e7a51551da9ff20cb5f0992eb025ae39f9cc20ff84",
            descriptor.MetadataSha256);
        Assert.True(descriptorStream.Position < descriptorStream.Length);

        using var decodeStream = new MemoryStream(encoded, writable: false);
        var decoded = SaveEnvelopeCodec.Decode(decodeStream, metadata.Compatibility);
        Assert.Equal(payload, decoded.LogicalPayload);
        Assert.Equal(descriptor, decoded.Descriptor);
    }

    [Fact]
    public void CorruptionAndStructuralDamageHaveTypedFailures()
    {
        var metadata = CreateMetadata();
        var encoded = SaveEnvelopeCodec.Encode(metadata, "logical-world-state"u8);

        var invalidMagic = encoded.ToArray();
        invalidMagic[0] ^= 0x01;
        AssertFailure(SaveLoadFailureCode.InvalidMagic, invalidMagic);

        var corruptMetadata = encoded.ToArray();
        corruptMetadata[SaveEnvelopeCodec.PreambleSize] ^= 0x01;
        AssertFailure(SaveLoadFailureCode.MetadataChecksumMismatch, corruptMetadata);

        var corruptEncodedPayload = encoded.ToArray();
        corruptEncodedPayload[^1] ^= 0x01;
        AssertFailure(
            SaveLoadFailureCode.EncodedPayloadChecksumMismatch,
            corruptEncodedPayload);

        var corruptLogicalDigest = encoded.ToArray();
        corruptLogicalDigest[104] ^= 0x01;
        AssertFailure(
            SaveLoadFailureCode.LogicalPayloadChecksumMismatch,
            corruptLogicalDigest);

        AssertFailure(SaveLoadFailureCode.Truncated, encoded[..^1]);
        AssertFailure(SaveLoadFailureCode.TrailingData, [.. encoded, 0x00]);
    }

    [Fact]
    public void CompatibilityFailsBeforeACompromisedPayloadIsRead()
    {
        var metadata = CreateMetadata();
        var encoded = SaveEnvelopeCodec.Encode(metadata, "logical-world-state"u8);
        encoded[^1] ^= 0x01;
        var incompatible = metadata.Compatibility with
        {
            WorldRulesHash = new string('0', 64),
        };

        using var stream = new MemoryStream(encoded, writable: false);
        var exception = Assert.Throws<SaveEnvelopeException>(() =>
            SaveEnvelopeCodec.Decode(stream, incompatible));

        Assert.Equal(SaveLoadFailureCode.IncompatiblePayload, exception.Code);
        Assert.True(stream.Position < stream.Length);
    }

    [Fact]
    public void InvalidOrUnboundedMetadataCannotBeWritten()
    {
        var metadata = CreateMetadata();
        var invalidHash = metadata with { WorldStateHash = "not-a-hash" };
        var invalidLifecycle = metadata with { WorldLifecycle = byte.MaxValue };
        var excessiveText = metadata with
        {
            Compatibility = metadata.Compatibility with
            {
                CertificationClass = new string('a', SaveEnvelopeCodec.MaximumMetadataBytes),
            },
        };

        Assert.Throws<ArgumentException>(() =>
            SaveEnvelopeCodec.Encode(invalidHash, "payload"u8));
        Assert.Throws<ArgumentException>(() =>
            SaveEnvelopeCodec.Encode(invalidLifecycle, "payload"u8));
        Assert.Throws<ArgumentException>(() =>
            SaveEnvelopeCodec.Encode(excessiveText, "payload"u8));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaveEnvelopeCodec.Encode(metadata, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public async Task AtomicReplacementPreservesPriorSaveAcrossInjectedFailure()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"lyfe-save-envelope-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "world.lyfe");
        try
        {
            var metadata = CreateMetadata();
            var first = SaveEnvelopeCodec.Encode(metadata, "first-boundary"u8);
            var second = SaveEnvelopeCodec.Encode(
                metadata with { CompletedTick = metadata.CompletedTick + 1 },
                "second-boundary"u8);
            await AtomicSaveFile.WriteAsync(
                destination,
                first,
                TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AtomicSaveFile.WriteAsync(
                    destination,
                    second,
                    new ThrowBeforeReplace(),
                    TestContext.Current.CancellationToken));

            Assert.Equal("first-boundary", DecodeFile(destination));
            Assert.Empty(Directory.EnumerateFiles(directory, ".*.tmp-*"));

            await AtomicSaveFile.WriteAsync(
                destination,
                second,
                TestContext.Current.CancellationToken);
            Assert.Equal("second-boundary", DecodeFile(destination));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LogicalWorldPayloadIsCanonicalAndRoundTrips()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var runner = WorldRunner.CreateFoundation(WorldId.From(17), rules, Seed);
        for (var index = 0; index < 3; index++)
        {
            runner.AdvanceOneTick();
        }

        var detached = runner.CapturePersistenceSnapshot();
        var payload = WorldPayloadCodec.Encode(detached.State);
        var decoded = WorldPayloadCodec.Decode(payload);

        Assert.Equal(payload, WorldPayloadCodec.Encode(decoded));
        Assert.Equal(detached.State.Organisms.Length, decoded.Organisms.Length);
        Assert.Equal(detached.State.LastCompletedTransactions.Length, decoded.LastCompletedTransactions.Length);
        Assert.Equal(3, decoded.ResourceFlowHistory.Length);
        Assert.Equal(
            detached.State.ResourceFlowHistory.Select(value => (
                value.CompletedTick, value.EndSimulatedHour, value.PeriodHours)),
            decoded.ResourceFlowHistory.Select(value => (
                value.CompletedTick, value.EndSimulatedHour, value.PeriodHours)));
        Assert.Equal(
            detached.State.ResourceFlowHistory.SelectMany(value => value.ResourceFlows),
            decoded.ResourceFlowHistory.SelectMany(value => value.ResourceFlows));
        Assert.Equal(3UL, decoded.ResourceFlowHistory[^1].CompletedTick);
        Assert.Equal(3UL, decoded.ResourceFlowHistory[^1].EndSimulatedHour);
        Assert.Equal(detached.State.NextJourneyEventId, decoded.NextJourneyEventId);
        Assert.Equal(detached.State.JourneyEvents.Length, decoded.JourneyEvents.Length);
        Assert.Equal(detached.State.JourneyEvents.Select(value => value.EventId),
            decoded.JourneyEvents.Select(value => value.EventId));
        Assert.Equal(detached.State.JourneyEvents.SelectMany(value => value.DeathCauses),
            decoded.JourneyEvents.SelectMany(value => value.DeathCauses));
        Assert.Equal(
            detached.State.RoutineActivitySummaries.Select(value => (
                value.BucketStartHour,
                value.PeriodHours,
                value.SubjectOrganismId,
                value.SubjectSpeciesId,
                value.TileId)),
            decoded.RoutineActivitySummaries.Select(value => (
                value.BucketStartHour,
                value.PeriodHours,
                value.SubjectOrganismId,
                value.SubjectSpeciesId,
                value.TileId)));
        Assert.Equal(
            detached.State.RoutineActivitySummaries.SelectMany(value =>
                value.ResourceAcquisitions),
            decoded.RoutineActivitySummaries.SelectMany(value =>
                value.ResourceAcquisitions));
        var nonDefaultBehavior = detached.State with
        {
            Organisms = detached.State.Organisms
                .Select((organism, index) => index == 0
                    ? organism with
                    {
                        BehaviorId = 2,
                        BehaviorSelectedAtTick = 2,
                        BehaviorMinimumDwellUntilTick = 6,
                        RecentEnergyCoverageQ = 900_000,
                        RecentAcquisitionCoverageQ = 750_000,
                        LimitingMaterialDeficitQ = 250_000,
                    }
                    : organism)
                .ToImmutableArray(),
        };
        var decodedBehavior = WorldPayloadCodec.Decode(
            WorldPayloadCodec.Encode(nonDefaultBehavior));
        Assert.Equal(
            nonDefaultBehavior.Organisms[0] with
            {
                CommittedMicronutrientsQ = decodedBehavior.Organisms[0].CommittedMicronutrientsQ,
                FreeMicronutrientsQ = decodedBehavior.Organisms[0].FreeMicronutrientsQ,
            },
            decodedBehavior.Organisms[0]);
        Assert.Equal(
            nonDefaultBehavior.Organisms[0].CommittedMicronutrientsQ,
            decodedBehavior.Organisms[0].CommittedMicronutrientsQ);
        Assert.Equal(
            nonDefaultBehavior.Organisms[0].FreeMicronutrientsQ,
            decodedBehavior.Organisms[0].FreeMicronutrientsQ);
        Assert.Equal(
            "f0182a6c1539493e5462e621a24c0b667295d43d49c217f7cc1c7d13626ae793",
            Convert.ToHexStringLower(SHA256.HashData(payload)));

        runner.AdvanceOneTick();
        var restoredAtCapture = WorldRunner.Restore(
            rules,
            detached with { State = decoded });
        Assert.Equal(detached.Metadata.Boundary, restoredAtCapture.CaptureSnapshot());
    }

    [Fact]
    public async Task SaveLoadAndNextTickContinuationAreEquivalent()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var original = WorldRunner.CreateFoundation(WorldId.From(23), rules, Seed);
        for (var index = 0; index < 5; index++)
        {
            original.AdvanceOneTick();
        }

        var directory = CreateTemporaryDirectory();
        var destination = Path.Combine(directory, "continuation.lyfe");
        try
        {
            var descriptor = await WorldSaveService.SaveAsync(
                original,
                destination,
                TestContext.Current.CancellationToken);
            var restored = WorldSaveService.Load(destination, rules);

            Assert.Equal(original.CaptureSnapshot(), restored.CaptureSnapshot());
            Assert.Equal(
                WorldPayloadCodec.Encode(original.CapturePersistenceSnapshot().State),
                WorldPayloadCodec.Encode(restored.CapturePersistenceSnapshot().State));
            Assert.Equal(original.CaptureSnapshot().StateHash, descriptor.Metadata.WorldStateHash);

            var expectedNext = original.AdvanceOneTick();
            var actualNext = restored.AdvanceOneTick();
            Assert.Equal(expectedNext.Snapshot, actualNext.Snapshot);
            Assert.Equal(
                expectedNext.Changes.MergedChanges.ResourceTransactionReferences,
                actualNext.Changes.MergedChanges.ResourceTransactionReferences);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ValidEnvelopeWithAlteredLogicalStateFailsWorldHashVerification()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var runner = WorldRunner.CreateFoundation(WorldId.From(31), rules, Seed);
        runner.AdvanceOneTick();
        var detached = runner.CapturePersistenceSnapshot();
        var first = detached.State.Organisms[0];
        var altered = detached.State with
        {
            Organisms = detached.State.Organisms.SetItem(
                0,
                first with { ChargedReserveQ = first.ChargedReserveQ + 1 }),
        };
        var metadata = SaveEnvelopeMetadataFactory.Create(detached.Metadata);
        var envelope = SaveEnvelopeCodec.Encode(metadata, WorldPayloadCodec.Encode(altered));
        var directory = CreateTemporaryDirectory();
        var destination = Path.Combine(directory, "altered.lyfe");
        try
        {
            await AtomicSaveFile.WriteAsync(
                destination,
                envelope,
                TestContext.Current.CancellationToken);
            Assert.Throws<WorldRestoreException>(() =>
                WorldSaveService.Load(destination, rules));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LogicalPayloadDamageAndOrderingHaveTypedFailures()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var runner = WorldRunner.CreateFoundation(WorldId.From(37), rules, Seed);
        var state = runner.CapturePersistenceSnapshot().State;
        var payload = WorldPayloadCodec.Encode(state);

        var invalidMagic = payload.ToArray();
        invalidMagic[0] ^= 1;
        AssertPayloadFailure(WorldPayloadFailureCode.InvalidMagic, invalidMagic);
        AssertPayloadFailure(WorldPayloadFailureCode.Truncated, payload[..^1]);
        AssertPayloadFailure(WorldPayloadFailureCode.TrailingData, [.. payload, 0]);

        var reversed = state with { Organisms = state.Organisms.Reverse().ToImmutableArray() };
        var exception = Assert.Throws<WorldPayloadException>(() =>
            WorldPayloadCodec.Encode(reversed));
        Assert.Equal(WorldPayloadFailureCode.InvalidOrdering, exception.Code);
    }

    [Fact]
    public void LogicalPayloadRejectsEmptyOrMalformedRemnants()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var state = WorldRunner.CreateFoundation(WorldId.From(39), rules, Seed)
            .CapturePersistenceSnapshot().State;
        var invalid = state with
        {
            Remnants =
            [
                new PersistenceRemnant(
                    1,
                    1,
                    1,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    1_000_000,
                    0,
                    ImmutableArray.CreateRange(Enumerable.Repeat(0L, 14))),
            ],
        };

        Assert.Throws<ArgumentException>(() => WorldPayloadCodec.Encode(invalid));
    }

    private static SaveEnvelopeMetadata CreateMetadata()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var runner = WorldRunner.CreateFoundation(WorldId.From(1), rules, Seed);
        runner.AdvanceOneTick();
        return SaveEnvelopeMetadataFactory.Create(runner.CapturePersistenceMetadata());
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"lyfe-save-envelope-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void AssertFailure(SaveLoadFailureCode expected, byte[] contents)
    {
        using var stream = new MemoryStream(contents, writable: false);
        var exception = Assert.Throws<SaveEnvelopeException>(() =>
            SaveEnvelopeCodec.Decode(stream));
        Assert.Equal(expected, exception.Code);
    }

    private static string DecodeFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Encoding.UTF8.GetString(SaveEnvelopeCodec.Decode(stream).LogicalPayload);
    }

    private static void AssertPayloadFailure(
        WorldPayloadFailureCode expected,
        byte[] contents)
    {
        var exception = Assert.Throws<WorldPayloadException>(() =>
            WorldPayloadCodec.Decode(contents));
        Assert.Equal(expected, exception.Code);
    }



    private sealed class ThrowBeforeReplace : IAtomicSaveFaultInjector
    {
        public void ThrowIfRequested(AtomicSaveStage stage)
        {
            Assert.Equal(AtomicSaveStage.AfterFlushBeforeReplace, stage);
            throw new InvalidOperationException("Injected replacement failure.");
        }
    }
}

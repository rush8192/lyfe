using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Lyfe.Simulation.Publication;

namespace Lyfe.Server.Persistence;

public enum WorldPayloadFailureCode
{
    Truncated = 1,
    InvalidMagic = 2,
    UnsupportedSchemaVersion = 3,
    InvalidCount = 4,
    InvalidOrdering = 5,
    InvalidValue = 6,
    TrailingData = 7,
}

public sealed class WorldPayloadException : IOException
{
    internal WorldPayloadException(WorldPayloadFailureCode code, string message)
        : base(message) => Code = code;

    public WorldPayloadFailureCode Code { get; }
}

public static class WorldPayloadCodec
{
    public const uint SchemaVersion = 21;
    public const int MaximumTileResourceCount = 4_000_000;
    public const int MaximumGenomeCount = 1_000_000;
    public const int MaximumSpeciesCount = 1_000_000;
    public const int MaximumTraitsPerGenome = 1_024;
    public const int MaximumOrganismCount = 1_000_000;
    public const int MaximumRemnantCount = 1_000_000;
    public const int MaximumJourneyEventCount = 10_000_000;
    public const int MaximumRoutineActivitySummaryCount = 10_000_000;
    public const int MaximumLineageReviewCount = 1_000_000;
    public const int MaximumNotableEventCount = 10_000_000;
    public const int MaximumAttentionAlertCount = 10_000_000;
    public const int MaximumAlertEvidenceCount = 256;
    public const int MaximumPopulationAttentionSamples = 25;
    public const int MaximumLineageReviewEvidenceReferences = 4_096;
    public const int MaximumResourceFlowHistoryIntervalCount = 168;
    public const int MaximumResourceFlowsPerInterval = 4_000_000;
    public const int MaximumResourceFlowHistoryTotalFlows = 10_000_000;
    public const int MaximumTransactionCount = 1_000_000;
    public const int MaximumEntriesPerTransaction = 128;

    private static ReadOnlySpan<byte> Magic => "LYFEWLD1"u8;

    public static byte[] Encode(WorldPersistenceState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateCollections(state);
        ValidateCanonicalOrder(state);

        var writer = new PayloadWriter();
        writer.WriteBytes(Magic);
        writer.WriteUInt32(SchemaVersion);
        writer.WriteUInt64(state.NextGenomeId);
        writer.WriteUInt64(state.NextSpeciesId);
        writer.WriteUInt64(state.NextOrganismId);
        writer.WriteUInt64(state.NextRemnantId);
        writer.WriteUInt64(state.NextJourneyEventId);
        WriteCount(writer, state.TileResources.Length);
        WriteCount(writer, state.GasTileRemainders.Length);
        WriteCount(writer, state.GasEdgeRemainders.Length);
        WriteCount(writer, state.Genomes.Length);
        WriteCount(writer, state.Species.Length);
        WriteCount(writer, state.SpeciationEvents.Length);
        WriteCount(writer, state.Organisms.Length);
        WriteCount(writer, state.Remnants.Length);
        WriteCount(writer, state.JourneyEvents.Length);
        WriteCount(writer, state.LineageReviewSchedules.Length);
        WriteCount(writer, state.LineageReviewLandmarks.Length);
        WriteCount(writer, state.NotableEvents.Length);
        WriteCount(writer, state.AttentionAlerts.Length);
        WriteCount(writer, state.PopulationAttentionStates.Length);
        WriteCount(writer, state.AttentionWindowStates.Length);
        WriteCount(writer, state.RoutineActivitySummaries.Length);
        WriteCount(writer, state.ResourceFlowHistory.Length);
        WriteCount(writer, state.LastCompletedTransactions.Length);
        writer.WriteByte(state.Gameplay.Mode);
        writer.WriteByte(state.Gameplay.RunStatus);
        writer.WriteByte(state.Gameplay.LossReason);
        writer.WriteUInt64(state.Gameplay.ControlledSpeciesId);
        writer.WriteUInt64(state.Gameplay.GameplayRevision);
        writer.WriteByte(state.Gameplay.EndedTick.HasValue ? (byte)1 : (byte)0);
        if (state.Gameplay.EndedTick.HasValue) writer.WriteUInt64(state.Gameplay.EndedTick.Value);
        WriteCount(writer, state.Gameplay.Roots.Length);
        foreach (var root in state.Gameplay.Roots)
        {
            writer.WriteUInt64(root.SpeciesId);
            writer.WriteUInt32(root.FounderGenomeId);
            writer.WriteUInt32(root.FounderAllocationId);
            writer.WriteUInt32(root.StartingTileId);
            writer.WriteUInt32(root.InitialPopulation);
            writer.WriteByte(root.PlayerSelected ? (byte)1 : (byte)0);
        }
        WriteCount(writer, state.Gameplay.MutationLockedSpeciesIds.Length);
        foreach (var speciesId in state.Gameplay.MutationLockedSpeciesIds)
        {
            writer.WriteUInt64(speciesId);
        }

        foreach (var resource in state.TileResources)
        {
            writer.WriteUInt32(resource.TileId);
            writer.WriteUInt32(resource.ResourceId);
            writer.WriteInt64(resource.QuantityQ);
        }

        foreach (var remainder in state.GasTileRemainders)
        {
            writer.WriteUInt32(remainder.TileId);
            writer.WriteUInt32(remainder.ResourceId);
            writer.WriteInt64(remainder.SourceRemainderQ);
            writer.WriteInt64(remainder.SinkRemainderQ);
        }

        foreach (var remainder in state.GasEdgeRemainders)
        {
            writer.WriteUInt32(remainder.LowerTileId);
            writer.WriteUInt32(remainder.HigherTileId);
            writer.WriteUInt32(remainder.ResourceId);
            writer.WriteInt64(remainder.ExchangeRemainderQ);
        }

        foreach (var genome in state.Genomes)
        {
            writer.WriteUInt64(genome.GenomeId);
            writer.WriteUInt32(genome.FounderGenomeId);
            writer.WriteUInt32(genome.FounderAllocationId);
            WriteUInt32Array(writer, genome.AcquiredTraitIds);
            writer.WriteString(genome.GenomeHash);
        }

        foreach (var species in state.Species)
        {
            writer.WriteUInt64(species.SpeciesId);
            writer.WriteUInt64(species.GenomeId);
            writer.WriteUInt64(species.Population);
            writer.WriteByte(species.Authority);
            writer.WriteInt64(species.MutationBalanceQ);
            writer.WriteUInt64(species.MutationIncomeRemainderLow);
            writer.WriteUInt64(species.MutationIncomeRemainderHigh);
            writer.WriteUInt64(species.SpeciationNotBeforeTick);
            writer.WriteUInt32(species.SpeciationOrdinal);
            writer.WriteUInt64(species.EvolutionRevision);
            writer.WriteUInt32(species.AverageHealthQ);
            writer.WriteInt64(species.LastIncomeQ);
            writer.WriteUInt32(species.EnergyShortagePressureQ);
            writer.WriteUInt32(species.StarvationPressureQ);
            writer.WriteUInt64(species.ParentSpeciesId);
            writer.WriteUInt64(species.FoundingEventId);
            writer.WriteUInt64(species.CreatedTick);
            writer.WriteByte(species.ExtinctTick.HasValue ? (byte)1 : (byte)0);
            if (species.ExtinctTick.HasValue) writer.WriteUInt64(species.ExtinctTick.Value);
            WriteFounderCounts(writer, species.FounderCounts);
            WriteUInt32Array(writer, species.AcquiredTraitDelta);
        }

        foreach (var value in state.SpeciationEvents)
        {
            writer.WriteUInt64(value.EventId);
            writer.WriteUInt64(value.Tick);
            writer.WriteUInt64(value.AncestorSpeciesId);
            writer.WriteUInt64(value.DescendantSpeciesId);
            writer.WriteByte(value.ActorKind);
            WriteFounderCounts(writer, value.FounderCounts);
            writer.WriteString(value.FounderSelectionDigest);
            WriteUInt32Array(writer, value.TraitDelta);
            writer.WriteInt64(value.MutationPriceQ);
            writer.WriteInt64(value.BalanceBeforeQ);
            writer.WriteInt64(value.DuplicatedBalanceAfterQ);
            writer.WriteUInt32(value.ChangeComplexity);
        }

        foreach (var organism in state.Organisms)
        {
            writer.WriteUInt64(organism.OrganismId);
            writer.WriteUInt64(organism.SpeciesId);
            writer.WriteUInt32(organism.TileId);
            writer.WriteUInt32(organism.PositionXQ);
            writer.WriteUInt32(organism.PositionYQ);
            writer.WriteInt64(organism.VelocityXQPerHour);
            writer.WriteInt64(organism.VelocityYQPerHour);
            writer.WriteUInt64(organism.BirthTick);
            writer.WriteUInt64(organism.BiologicalAgeHours);
            writer.WriteByte(organism.LifecyclePhase);
            writer.WriteUInt64(organism.ReproductionNotBeforeTick);
            writer.WriteUInt64(organism.SuccessfulReproductionCount);
            writer.WriteUInt64(organism.ScavengeNotBeforeTick);
            writer.WriteInt64(organism.IngestedStructuralMatterQ);
            writer.WriteInt64(organism.StructuralMatterQ);
            writer.WriteInt64(organism.ChargedReserveQ);
            writer.WriteUInt64(organism.ConditionEvaluatedTick);
            writer.WriteByte(organism.ConditionSnapshotKind);
            writer.WriteUInt32(organism.ReserveFactorQ);
            writer.WriteUInt32(organism.StructureFactorQ);
            writer.WriteUInt32(organism.NutrientFactorQ);
            writer.WriteUInt32(organism.AgeFactorQ);
            writer.WriteUInt32(organism.LifecycleFactorQ);
            writer.WriteUInt32(organism.EnvironmentalFactorQ);
            writer.WriteUInt32(organism.RelativeHealthQ);
            writer.WriteInt32(organism.ConditionTemperatureMilliC);
            writer.WriteUInt32(organism.TemperatureSeverityQ);
            writer.WriteByte(organism.BehaviorId);
            writer.WriteByte(organism.BehaviorTargetKind);
            writer.WriteUInt64(organism.BehaviorTargetId);
            writer.WriteUInt32(organism.BehaviorTargetPositionXQ);
            writer.WriteUInt32(organism.BehaviorTargetPositionYQ);
            writer.WriteUInt64(organism.BehaviorSelectedAtTick);
            writer.WriteUInt64(organism.BehaviorMinimumDwellUntilTick);
            writer.WriteUInt32(organism.RecentEnergyCoverageQ);
            writer.WriteUInt32(organism.RecentAcquisitionCoverageQ);
            writer.WriteUInt32(organism.LimitingMaterialDeficitQ);
            WriteMicronutrients(writer, organism.CommittedMicronutrientsQ);
            WriteMicronutrients(writer, organism.FreeMicronutrientsQ);
        }

        foreach (var remnant in state.Remnants)
        {
            writer.WriteUInt64(remnant.RemnantId);
            writer.WriteUInt64(remnant.SourceOrganismId);
            writer.WriteUInt64(remnant.SourceSpeciesId);
            writer.WriteUInt64(remnant.CreatedTick);
            writer.WriteUInt32(remnant.TileId);
            writer.WriteUInt32(remnant.PositionXQ);
            writer.WriteUInt32(remnant.PositionYQ);
            writer.WriteInt64(remnant.StructuralMatterQ);
            writer.WriteInt64(remnant.ChargedReserveQ);
            writer.WriteUInt32(remnant.StructureDecayRemainderQ);
            writer.WriteUInt32(remnant.ReserveDecayRemainderQ);
            WriteMicronutrients(writer, remnant.MicronutrientsQ);
        }

        foreach (var value in state.JourneyEvents)
        {
            writer.WriteUInt64(value.EventId);
            writer.WriteUInt64(value.Tick);
            writer.WriteByte(value.Phase);
            writer.WriteByte(value.Family);
            writer.WriteUInt64(value.SubjectOrganismId);
            writer.WriteUInt64(value.SubjectSpeciesId);
            writer.WriteUInt32(value.TileId);
            writer.WriteUInt32(value.PositionXQ);
            writer.WriteUInt32(value.PositionYQ);
            writer.WriteUInt64(value.RelatedOrganismId);
            writer.WriteUInt64(value.RelatedRemnantId);
            writer.WriteUInt32(value.ResourceId);
            writer.WriteInt64(value.AmountQ);
            writer.WriteUInt32(value.DetailId);
            WriteCount(writer, value.DeathCauses.Length);
            foreach (var cause in value.DeathCauses)
            {
                writer.WriteByte(cause.Cause);
                writer.WriteUInt32(cause.ProbabilityQ);
                writer.WriteByte(cause.Triggered ? (byte)1 : (byte)0);
            }
        }

        foreach (var value in state.LineageReviewSchedules)
        {
            writer.WriteUInt64(value.SpeciationEventId);
            writer.WriteUInt64(value.AppliedTick);
            writer.WriteUInt64(value.AppliedSimulatedHours);
            writer.WriteUInt64(value.AncestorSpeciesId);
            writer.WriteUInt64(value.DescendantSpeciesId);
            writer.WriteUInt64(value.PerspectiveSpeciesId);
            WriteUInt32Array(writer, value.TraitDelta);
            writer.WriteUInt64(value.CooldownBoundaryTick);
            writer.WriteUInt64(value.FollowUpBoundaryTick);
            writer.WriteUInt64(value.FollowUpHours);
            writer.WriteByte(value.FollowUpEvidenceKind);
            WriteLineageReviewObservation(writer, value.PerspectiveBaseline);
            WriteLineageReviewObservation(writer, value.ComparisonBaseline);
            WriteLineageReviewActivationEvidence(
                writer,
                value.CapabilityActivations,
                value.ReactionActivations);
        }

        foreach (var value in state.LineageReviewLandmarks)
        {
            writer.WriteUInt64(value.EventId);
            writer.WriteByte(value.Kind);
            writer.WriteUInt64(value.CompletedTick);
            writer.WriteUInt64(value.SimulatedHours);
            writer.WriteUInt64(value.WindowHours);
            writer.WriteUInt64(value.SpeciationEventId);
            writer.WriteUInt64(value.AncestorSpeciesId);
            writer.WriteUInt64(value.DescendantSpeciesId);
            writer.WriteUInt64(value.PerspectiveSpeciesId);
            WriteUInt32Array(writer, value.TraitDelta);
            writer.WriteByte(value.EvidenceKind);
            WriteLineageReviewObservation(writer, value.PerspectiveBaseline);
            WriteLineageReviewObservation(writer, value.ComparisonBaseline);
            WriteLineageReviewObservation(writer, value.PerspectiveCurrent);
            WriteLineageReviewObservation(writer, value.ComparisonCurrent);
            WriteLineageReviewActivationEvidence(
                writer,
                value.CapabilityActivations,
                value.ReactionActivations);
            WriteCount(writer, value.EvidenceReferences.Length);
            foreach (var reference in value.EvidenceReferences)
            {
                writer.WriteByte(reference.Kind);
                writer.WriteUInt64(reference.SpeciesId);
                writer.WriteByte(reference.TileId.HasValue ? (byte)1 : (byte)0);
                if (reference.TileId.HasValue)
                {
                    writer.WriteUInt32(reference.TileId.Value);
                }
                writer.WriteUInt64(reference.FromExclusiveTick);
                writer.WriteUInt64(reference.ThroughCompletedTick);
            }
        }

        foreach (var value in state.NotableEvents)
        {
            writer.WriteUInt64(value.EventId);
            writer.WriteByte(value.Family);
            writer.WriteByte(value.Significance);
            writer.WriteUInt32(value.SignificanceRuleVersion);
            writer.WriteUInt64(value.CompletedTick);
            writer.WriteUInt64(value.SimulatedHours);
            writer.WriteUInt64(value.SpeciesId);
            writer.WriteUInt64(value.RelatedSpeciesId);
            writer.WriteByte(value.TileId.HasValue ? (byte)1 : (byte)0);
            if (value.TileId.HasValue) writer.WriteUInt32(value.TileId.Value);
            writer.WriteUInt32(value.ReactionId);
            writer.WriteUInt64(value.SourceEventId);
            writer.WriteUInt64(value.MilestoneValue);
            writer.WriteUInt64(value.BaselineValue);
            writer.WriteString(value.DeduplicationKey);
        }

        foreach (var value in state.AttentionAlerts)
        {
            writer.WriteUInt64(value.AlertId);
            writer.WriteByte(value.AlertClass);
            writer.WriteByte(value.Kind);
            writer.WriteByte(value.EventFamily);
            writer.WriteUInt64(value.CompletedTick);
            writer.WriteUInt64(value.SimulatedHours);
            writer.WriteUInt64(value.SpeciesId);
            WriteCount(writer, value.ChronicleEventIds.Length);
            foreach (var eventId in value.ChronicleEventIds) writer.WriteUInt64(eventId);
            writer.WriteString(value.DeduplicationKey);
        }

        foreach (var value in state.PopulationAttentionStates)
        {
            writer.WriteUInt64(value.SpeciesId);
            writer.WriteByte(value.HasExceededDangerThreshold ? (byte)1 : (byte)0);
            writer.WriteByte(value.LowPopulationArmed ? (byte)1 : (byte)0);
            writer.WriteUInt32(value.LowPopulationEpisodeOrdinal);
        }

        foreach (var value in state.AttentionWindowStates)
        {
            writer.WriteUInt64(value.SpeciesId);
            writer.WriteByte(value.PopulationDeclineArmed ? (byte)1 : (byte)0);
            writer.WriteUInt32(value.PopulationDeclineEpisodeOrdinal);
            writer.WriteUInt32(value.LowHealthConsecutiveHours);
            writer.WriteByte(value.LowHealthArmed ? (byte)1 : (byte)0);
            writer.WriteUInt32(value.HealthyRecoveryConsecutiveHours);
            writer.WriteUInt32(value.LowHealthEpisodeOrdinal);
            writer.WriteUInt32(value.ResourcePressureConsecutiveHours);
            writer.WriteByte(value.ResourcePressureArmed ? (byte)1 : (byte)0);
            writer.WriteUInt32(value.ResourcePressureRecoveryConsecutiveHours);
            writer.WriteUInt32(value.ResourcePressureEpisodeOrdinal);
            WriteCount(writer, value.PopulationSamples.Length);
            foreach (var sample in value.PopulationSamples)
            {
                writer.WriteUInt64(sample.SimulatedHours);
                writer.WriteUInt64(sample.Population);
            }
        }

        foreach (var value in state.RoutineActivitySummaries)
        {
            writer.WriteUInt64(value.BucketStartHour);
            writer.WriteUInt32(value.PeriodHours);
            writer.WriteUInt64(value.SubjectOrganismId);
            writer.WriteUInt64(value.SubjectSpeciesId);
            writer.WriteUInt32(value.TileId);
            WriteCount(writer, value.ResourceAcquisitions.Length);
            foreach (var resource in value.ResourceAcquisitions)
            {
                writer.WriteUInt32(resource.ResourceId);
                writer.WriteInt64(resource.AmountQ);
            }
        }

        foreach (var interval in state.ResourceFlowHistory)
        {
            writer.WriteUInt64(interval.CompletedTick);
            writer.WriteUInt64(interval.EndSimulatedHour);
            writer.WriteUInt32(interval.PeriodHours);
            WriteCount(writer, interval.ResourceFlows.Length);
            foreach (var flow in interval.ResourceFlows)
            {
                writer.WriteUInt32(flow.TileId);
                writer.WriteUInt32(flow.ResourceId);
                writer.WriteByte(flow.Kind);
                writer.WriteInt64(flow.AmountQ);
            }
        }

        foreach (var transaction in state.LastCompletedTransactions)
        {
            writer.WriteUInt64(transaction.Tick);
            writer.WriteByte(transaction.Phase);
            writer.WriteUInt64(transaction.ScopeId);
            writer.WriteUInt64(transaction.KeyActorId);
            writer.WriteUInt32(transaction.KeyReactionId);
            writer.WriteUInt32(transaction.LocalOrdinal);
            writer.WriteByte(transaction.Cause);
            writer.WriteUInt32(transaction.ReactionId);
            writer.WriteUInt64(transaction.ActorId);
            writer.WriteUInt32(transaction.TileId);
            writer.WriteInt64(transaction.Extent);
            WriteCount(writer, transaction.MatterEntries.Length);
            WriteCount(writer, transaction.EnergyEntries.Length);
            foreach (var entry in transaction.MatterEntries)
            {
                writer.WriteByte(entry.OwnerKind);
                writer.WriteUInt64(entry.OwnerId);
                writer.WriteByte(entry.Compartment);
                writer.WriteUInt32(entry.ResourceId);
                writer.WriteInt64(entry.DeltaQ);
            }

            foreach (var entry in transaction.EnergyEntries)
            {
                writer.WriteByte(entry.Kind);
                writer.WriteUInt64(entry.OwnerId);
                writer.WriteInt64(entry.DeltaQ);
            }
        }

        return writer.ToArray();
    }

    public static WorldPersistenceState Decode(ReadOnlySpan<byte> payload)
    {
        var reader = new PayloadReader(payload);
        if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
        {
            throw Failure(WorldPayloadFailureCode.InvalidMagic, "The logical world payload magic is invalid.");
        }

        var schemaVersion = reader.ReadUInt32();
        if (schemaVersion != SchemaVersion)
        {
            throw Failure(
                WorldPayloadFailureCode.UnsupportedSchemaVersion,
                $"Logical world payload schema {schemaVersion} is unsupported.");
        }

        var nextGenomeId = reader.ReadUInt64();
        var nextSpeciesId = reader.ReadUInt64();
        var nextOrganismId = reader.ReadUInt64();
        var nextRemnantId = reader.ReadUInt64();
        var nextJourneyEventId = reader.ReadUInt64();
        if (nextGenomeId == 0 || nextSpeciesId == 0 || nextOrganismId == 0 ||
            nextRemnantId == 0 || nextJourneyEventId == 0)
        {
            throw Failure(WorldPayloadFailureCode.InvalidValue, "Next entity IDs must be nonzero.");
        }

        var tileResourceCount = ReadCount(reader.ReadUInt32(), MaximumTileResourceCount);
        var gasTileRemainderCount = ReadCount(reader.ReadUInt32(), MaximumTileResourceCount);
        var gasEdgeRemainderCount = ReadCount(reader.ReadUInt32(), MaximumTileResourceCount);
        var genomeCount = ReadCount(reader.ReadUInt32(), MaximumGenomeCount);
        var speciesCount = ReadCount(reader.ReadUInt32(), MaximumSpeciesCount);
        var speciationEventCount = ReadCount(reader.ReadUInt32(), MaximumSpeciesCount);
        var organismCount = ReadCount(reader.ReadUInt32(), MaximumOrganismCount);
        var remnantCount = ReadCount(reader.ReadUInt32(), MaximumRemnantCount);
        var journeyEventCount = ReadCount(reader.ReadUInt32(), MaximumJourneyEventCount);
        var lineageReviewScheduleCount = ReadCount(
            reader.ReadUInt32(), MaximumLineageReviewCount);
        var lineageReviewLandmarkCount = ReadCount(
            reader.ReadUInt32(), MaximumLineageReviewCount);
        var notableEventCount = ReadCount(reader.ReadUInt32(), MaximumNotableEventCount);
        var attentionAlertCount = ReadCount(reader.ReadUInt32(), MaximumAttentionAlertCount);
        var populationAttentionStateCount = ReadCount(reader.ReadUInt32(), MaximumSpeciesCount);
        var attentionWindowStateCount = ReadCount(reader.ReadUInt32(), MaximumSpeciesCount);
        var routineSummaryCount = ReadCount(
            reader.ReadUInt32(),
            MaximumRoutineActivitySummaryCount);
        var resourceFlowHistoryCount = ReadCount(
            reader.ReadUInt32(),
            MaximumResourceFlowHistoryIntervalCount);
        var transactionCount = ReadCount(reader.ReadUInt32(), MaximumTransactionCount);
        var gameMode = reader.ReadByte();
        var gameRunStatus = reader.ReadByte();
        var gameLossReason = reader.ReadByte();
        var controlledSpeciesId = reader.ReadUInt64();
        var gameplayRevision = reader.ReadUInt64();
        ulong? endedTick = reader.ReadByte() == 0 ? null : reader.ReadUInt64();
        var rootCount = ReadCount(reader.ReadUInt32(), 2);
        var roots = ImmutableArray.CreateBuilder<PersistenceAbiogenesisRoot>(rootCount);
        for (var index = 0; index < rootCount; index++)
        {
            roots.Add(new PersistenceAbiogenesisRoot(
                reader.ReadUInt64(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadByte() != 0));
        }
        var lockCount = ReadCount(reader.ReadUInt32(), MaximumSpeciesCount);
        var lockedSpeciesIds = ImmutableArray.CreateBuilder<ulong>(lockCount);
        for (var index = 0; index < lockCount; index++)
        {
            lockedSpeciesIds.Add(reader.ReadUInt64());
        }
        var gameplay = new PersistenceGameState(
            gameMode,
            gameRunStatus,
            gameLossReason,
            controlledSpeciesId,
            gameplayRevision,
            endedTick,
            roots.MoveToImmutable(),
            lockedSpeciesIds.MoveToImmutable());

        var tileResources = ImmutableArray.CreateBuilder<PersistenceTileResource>(tileResourceCount);
        for (var index = 0; index < tileResourceCount; index++)
        {
            tileResources.Add(new PersistenceTileResource(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt64()));
        }

        var gasTileRemainders = ImmutableArray.CreateBuilder<PersistenceGasTileRemainder>(gasTileRemainderCount);
        for (var index = 0; index < gasTileRemainderCount; index++)
        {
            gasTileRemainders.Add(new PersistenceGasTileRemainder(
                reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadInt64(), reader.ReadInt64()));
        }

        var gasEdgeRemainders = ImmutableArray.CreateBuilder<PersistenceGasEdgeRemainder>(gasEdgeRemainderCount);
        for (var index = 0; index < gasEdgeRemainderCount; index++)
        {
            gasEdgeRemainders.Add(new PersistenceGasEdgeRemainder(
                reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadInt64()));
        }

        var genomes = ImmutableArray.CreateBuilder<PersistenceGenome>(genomeCount);
        for (var index = 0; index < genomeCount; index++)
        {
            genomes.Add(new PersistenceGenome(
                reader.ReadUInt64(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                ReadUInt32Array(ref reader),
                reader.ReadString()));
        }

        var species = ImmutableArray.CreateBuilder<PersistenceSpecies>(speciesCount);
        for (var index = 0; index < speciesCount; index++)
        {
            species.Add(new PersistenceSpecies(
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadByte(),
                reader.ReadInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt32(),
                reader.ReadUInt64(),
                reader.ReadUInt32(),
                reader.ReadInt64(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadByte() == 0 ? null : reader.ReadUInt64(),
                ReadFounderCounts(ref reader),
                ReadUInt32Array(ref reader)));
        }

        var speciationEvents = ImmutableArray.CreateBuilder<PersistenceSpeciationEvent>(speciationEventCount);
        for (var index = 0; index < speciationEventCount; index++)
        {
            speciationEvents.Add(new PersistenceSpeciationEvent(
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadByte(),
                ReadFounderCounts(ref reader),
                reader.ReadString(),
                ReadUInt32Array(ref reader),
                reader.ReadInt64(),
                reader.ReadInt64(),
                reader.ReadInt64(),
                reader.ReadUInt32()));
        }

        var organisms = ImmutableArray.CreateBuilder<PersistenceOrganism>(organismCount);
        for (var index = 0; index < organismCount; index++)
        {
            organisms.Add(new PersistenceOrganism(
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt64(),
                reader.ReadInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadByte(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadInt64(),
                reader.ReadInt64(),
                reader.ReadInt64(),
                reader.ReadUInt64(),
                reader.ReadByte(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt32(),
                reader.ReadUInt32(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadUInt64(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                ReadMicronutrients(ref reader),
                ReadMicronutrients(ref reader)));
        }

        var remnants = ImmutableArray.CreateBuilder<PersistenceRemnant>(remnantCount);
        for (var index = 0; index < remnantCount; index++)
        {
            remnants.Add(new PersistenceRemnant(
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt64(),
                reader.ReadInt64(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                ReadMicronutrients(ref reader)));
        }

        var journeyEvents = ImmutableArray.CreateBuilder<PersistenceOrganismJourneyEvent>(
            journeyEventCount);
        for (var index = 0; index < journeyEventCount; index++)
        {
            var eventId = reader.ReadUInt64();
            var tick = reader.ReadUInt64();
            var phase = reader.ReadByte();
            var family = reader.ReadByte();
            var subjectOrganismId = reader.ReadUInt64();
            var subjectSpeciesId = reader.ReadUInt64();
            var tileId = reader.ReadUInt32();
            var positionX = reader.ReadUInt32();
            var positionY = reader.ReadUInt32();
            var relatedOrganismId = reader.ReadUInt64();
            var relatedRemnantId = reader.ReadUInt64();
            var resourceId = reader.ReadUInt32();
            var amountQ = reader.ReadInt64();
            var detailId = reader.ReadUInt32();
            var causeCount = ReadCount(reader.ReadUInt32(), 16);
            var causes = ImmutableArray.CreateBuilder<PersistenceJourneyDeathCause>(causeCount);
            for (var causeIndex = 0; causeIndex < causeCount; causeIndex++)
            {
                causes.Add(new PersistenceJourneyDeathCause(
                    reader.ReadByte(),
                    reader.ReadUInt32(),
                    reader.ReadByte() != 0));
            }
            journeyEvents.Add(new PersistenceOrganismJourneyEvent(
                eventId,
                tick,
                phase,
                family,
                subjectOrganismId,
                subjectSpeciesId,
                tileId,
                positionX,
                positionY,
                relatedOrganismId,
                relatedRemnantId,
                resourceId,
                amountQ,
                detailId,
                causes.MoveToImmutable()));
        }

        var lineageReviewSchedules =
            ImmutableArray.CreateBuilder<PersistenceLineageReviewSchedule>(
                lineageReviewScheduleCount);
        for (var index = 0; index < lineageReviewScheduleCount; index++)
        {
            lineageReviewSchedules.Add(new PersistenceLineageReviewSchedule(
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                ReadUInt32Array(ref reader),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadByte(),
                ReadLineageReviewObservation(ref reader),
                ReadLineageReviewObservation(ref reader),
                ReadLineageReviewCapabilityActivations(ref reader),
                ReadLineageReviewReactionActivations(ref reader)));
        }

        var lineageReviewLandmarks =
            ImmutableArray.CreateBuilder<PersistenceLineageReviewLandmark>(
                lineageReviewLandmarkCount);
        for (var index = 0; index < lineageReviewLandmarkCount; index++)
        {
            lineageReviewLandmarks.Add(new PersistenceLineageReviewLandmark(
                reader.ReadUInt64(),
                reader.ReadByte(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                ReadUInt32Array(ref reader),
                reader.ReadByte(),
                ReadLineageReviewObservation(ref reader),
                ReadLineageReviewObservation(ref reader),
                ReadLineageReviewObservation(ref reader),
                ReadLineageReviewObservation(ref reader),
                ReadLineageReviewCapabilityActivations(ref reader),
                ReadLineageReviewReactionActivations(ref reader),
                ReadLineageReviewEvidenceReferences(ref reader)));
        }

        var notableEvents = ImmutableArray.CreateBuilder<PersistenceNotableEvent>(
            notableEventCount);
        for (var index = 0; index < notableEventCount; index++)
        {
            notableEvents.Add(new PersistenceNotableEvent(
                reader.ReadUInt64(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadUInt32(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadByte() == 0 ? null : reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadString()));
        }

        var attentionAlerts = ImmutableArray.CreateBuilder<PersistenceAttentionAlert>(
            attentionAlertCount);
        for (var index = 0; index < attentionAlertCount; index++)
        {
            attentionAlerts.Add(new PersistenceAttentionAlert(
                reader.ReadUInt64(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                ReadUInt64Array(ref reader, MaximumAlertEvidenceCount),
                reader.ReadString()));
        }

        var populationAttentionStates =
            ImmutableArray.CreateBuilder<PersistencePopulationAttentionState>(
                populationAttentionStateCount);
        for (var index = 0; index < populationAttentionStateCount; index++)
        {
            populationAttentionStates.Add(new PersistencePopulationAttentionState(
                reader.ReadUInt64(),
                reader.ReadByte() != 0,
                reader.ReadByte() != 0,
                reader.ReadUInt32()));
        }

        var attentionWindowStates =
            ImmutableArray.CreateBuilder<PersistenceAttentionWindowState>(
                attentionWindowStateCount);
        for (var index = 0; index < attentionWindowStateCount; index++)
        {
            var speciesId = reader.ReadUInt64();
            var declineArmed = reader.ReadByte() != 0;
            var declineEpisode = reader.ReadUInt32();
            var lowHealthHours = reader.ReadUInt32();
            var lowHealthArmed = reader.ReadByte() != 0;
            var recoveryHours = reader.ReadUInt32();
            var lowHealthEpisode = reader.ReadUInt32();
            var pressureHours = reader.ReadUInt32();
            var pressureArmed = reader.ReadByte() != 0;
            var pressureRecoveryHours = reader.ReadUInt32();
            var pressureEpisode = reader.ReadUInt32();
            var sampleCount = ReadCount(
                reader.ReadUInt32(), MaximumPopulationAttentionSamples);
            var samples = ImmutableArray.CreateBuilder<PersistencePopulationAttentionSample>(
                sampleCount);
            for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                samples.Add(new PersistencePopulationAttentionSample(
                    reader.ReadUInt64(),
                    reader.ReadUInt64()));
            }
            attentionWindowStates.Add(new PersistenceAttentionWindowState(
                speciesId,
                declineArmed,
                declineEpisode,
                lowHealthHours,
                lowHealthArmed,
                recoveryHours,
                lowHealthEpisode,
                pressureHours,
                pressureArmed,
                pressureRecoveryHours,
                pressureEpisode,
                samples.MoveToImmutable()));
        }

        var routineSummaries =
            ImmutableArray.CreateBuilder<PersistenceOrganismRoutineActivitySummary>(
                routineSummaryCount);
        for (var index = 0; index < routineSummaryCount; index++)
        {
            var bucketStartHour = reader.ReadUInt64();
            var periodHours = reader.ReadUInt32();
            var subjectOrganismId = reader.ReadUInt64();
            var subjectSpeciesId = reader.ReadUInt64();
            var tileId = reader.ReadUInt32();
            var resourceCount = ReadCount(reader.ReadUInt32(), 256);
            var resources =
                ImmutableArray.CreateBuilder<PersistenceRoutineResourceAcquisition>(
                    resourceCount);
            for (var resourceIndex = 0; resourceIndex < resourceCount; resourceIndex++)
            {
                resources.Add(new PersistenceRoutineResourceAcquisition(
                    reader.ReadUInt32(),
                    reader.ReadInt64()));
            }
            routineSummaries.Add(new PersistenceOrganismRoutineActivitySummary(
                bucketStartHour,
                periodHours,
                subjectOrganismId,
                subjectSpeciesId,
                tileId,
                resources.MoveToImmutable()));
        }

        var resourceFlowHistory =
            ImmutableArray.CreateBuilder<PersistenceResourceFlowHistoryInterval>(
                resourceFlowHistoryCount);
        var totalHistoricalFlows = 0;
        for (var index = 0; index < resourceFlowHistoryCount; index++)
        {
            var completedTick = reader.ReadUInt64();
            var endSimulatedHour = reader.ReadUInt64();
            var periodHours = reader.ReadUInt32();
            var flowCount = ReadCount(
                reader.ReadUInt32(),
                MaximumResourceFlowsPerInterval);
            totalHistoricalFlows = checked(totalHistoricalFlows + flowCount);
            if (totalHistoricalFlows > MaximumResourceFlowHistoryTotalFlows)
            {
                throw Failure(
                    WorldPayloadFailureCode.InvalidCount,
                    "Resource-flow history exceeds its aggregate limit.");
            }
            var flows = ImmutableArray.CreateBuilder<PersistenceTileResourceFlow>(flowCount);
            for (var flowIndex = 0; flowIndex < flowCount; flowIndex++)
            {
                flows.Add(new PersistenceTileResourceFlow(
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadByte(),
                    reader.ReadInt64()));
            }
            resourceFlowHistory.Add(new PersistenceResourceFlowHistoryInterval(
                completedTick,
                endSimulatedHour,
                periodHours,
                flows.MoveToImmutable()));
        }

        var transactions = ImmutableArray.CreateBuilder<PersistenceResourceTransaction>(transactionCount);
        for (var index = 0; index < transactionCount; index++)
        {
            var tick = reader.ReadUInt64();
            var phase = reader.ReadByte();
            var scopeId = reader.ReadUInt64();
            var keyActorId = reader.ReadUInt64();
            var keyReactionId = reader.ReadUInt32();
            var localOrdinal = reader.ReadUInt32();
            var cause = reader.ReadByte();
            var reactionId = reader.ReadUInt32();
            var actorId = reader.ReadUInt64();
            var tileId = reader.ReadUInt32();
            var extent = reader.ReadInt64();
            var matterCount = ReadCount(reader.ReadUInt32(), MaximumEntriesPerTransaction);
            var energyCount = ReadCount(reader.ReadUInt32(), MaximumEntriesPerTransaction);
            var matter = ImmutableArray.CreateBuilder<PersistenceMatterEntry>(matterCount);
            for (var matterIndex = 0; matterIndex < matterCount; matterIndex++)
            {
                matter.Add(new PersistenceMatterEntry(
                    reader.ReadByte(),
                    reader.ReadUInt64(),
                    reader.ReadByte(),
                    reader.ReadUInt32(),
                    reader.ReadInt64()));
            }

            var energy = ImmutableArray.CreateBuilder<PersistenceEnergyEntry>(energyCount);
            for (var energyIndex = 0; energyIndex < energyCount; energyIndex++)
            {
                energy.Add(new PersistenceEnergyEntry(
                    reader.ReadByte(),
                    reader.ReadUInt64(),
                    reader.ReadInt64()));
            }

            transactions.Add(new PersistenceResourceTransaction(
                tick,
                phase,
                scopeId,
                keyActorId,
                keyReactionId,
                localOrdinal,
                cause,
                reactionId,
                actorId,
                tileId,
                extent,
                matter.MoveToImmutable(),
                energy.MoveToImmutable()));
        }

        if (!reader.IsComplete)
        {
            throw Failure(WorldPayloadFailureCode.TrailingData, "The logical world payload has trailing bytes.");
        }

        var result = new WorldPersistenceState(
            nextGenomeId,
            nextSpeciesId,
            nextOrganismId,
            nextRemnantId,
            nextJourneyEventId,
            tileResources.MoveToImmutable(),
            gasTileRemainders.MoveToImmutable(),
            gasEdgeRemainders.MoveToImmutable(),
            genomes.MoveToImmutable(),
            species.MoveToImmutable(),
            speciationEvents.MoveToImmutable(),
            organisms.MoveToImmutable(),
            remnants.MoveToImmutable(),
            journeyEvents.MoveToImmutable(),
            routineSummaries.MoveToImmutable(),
            resourceFlowHistory.MoveToImmutable(),
            transactions.MoveToImmutable(),
            gameplay,
            lineageReviewSchedules.MoveToImmutable(),
            lineageReviewLandmarks.MoveToImmutable(),
            notableEvents.MoveToImmutable(),
            attentionAlerts.MoveToImmutable(),
            populationAttentionStates.MoveToImmutable(),
            attentionWindowStates.MoveToImmutable());
        ValidateDecodedStateValues(result);
        ValidateCanonicalOrder(result);
        return result;
    }

    private static void ValidateCollections(WorldPersistenceState state)
    {
        if (state.NextGenomeId == 0 || state.NextSpeciesId == 0 || state.NextOrganismId == 0 ||
            state.NextRemnantId == 0 || state.NextJourneyEventId == 0 ||
            state.TileResources.IsDefault || state.GasTileRemainders.IsDefault ||
            state.GasEdgeRemainders.IsDefault || state.Genomes.IsDefault || state.Species.IsDefault ||
            state.SpeciationEvents.IsDefault ||
            state.Organisms.IsDefault || state.Remnants.IsDefault || state.JourneyEvents.IsDefault ||
            state.LineageReviewSchedules.IsDefault || state.LineageReviewLandmarks.IsDefault ||
            state.NotableEvents.IsDefault ||
            state.AttentionAlerts.IsDefault || state.PopulationAttentionStates.IsDefault ||
            state.AttentionWindowStates.IsDefault ||
            state.RoutineActivitySummaries.IsDefault ||
            state.ResourceFlowHistory.IsDefault ||
            state.LastCompletedTransactions.IsDefault || state.Gameplay is null ||
            state.Gameplay.Roots.IsDefault || state.Gameplay.MutationLockedSpeciesIds.IsDefault)
        {
            throw new ArgumentException("Logical world state is incomplete.", nameof(state));
        }

        RequireCount(state.TileResources.Length, MaximumTileResourceCount, nameof(state.TileResources));
        RequireCount(state.GasTileRemainders.Length, MaximumTileResourceCount, nameof(state.GasTileRemainders));
        RequireCount(state.GasEdgeRemainders.Length, MaximumTileResourceCount, nameof(state.GasEdgeRemainders));
        if (state.GasTileRemainders.Any(remainder =>
                remainder.SourceRemainderQ is < 0 or >= 1_000_000 ||
                remainder.SinkRemainderQ is < 0 or >= 1_000_000) ||
            state.GasEdgeRemainders.Any(remainder =>
                remainder.LowerTileId >= remainder.HigherTileId ||
                remainder.ExchangeRemainderQ is <= -1_000_000_000_000L or >= 1_000_000_000_000L))
        {
            throw new ArgumentException("Gas fixed-point remainders are invalid.", nameof(state));
        }
        RequireCount(state.Genomes.Length, MaximumGenomeCount, nameof(state.Genomes));
        RequireCount(state.Species.Length, MaximumSpeciesCount, nameof(state.Species));
        RequireCount(state.SpeciationEvents.Length, MaximumSpeciesCount, nameof(state.SpeciationEvents));
        RequireCount(state.Organisms.Length, MaximumOrganismCount, nameof(state.Organisms));
        RequireCount(state.Remnants.Length, MaximumRemnantCount, nameof(state.Remnants));
        RequireCount(state.JourneyEvents.Length, MaximumJourneyEventCount, nameof(state.JourneyEvents));
        RequireCount(state.LineageReviewSchedules.Length, MaximumLineageReviewCount,
            nameof(state.LineageReviewSchedules));
        RequireCount(state.LineageReviewLandmarks.Length, MaximumLineageReviewCount,
            nameof(state.LineageReviewLandmarks));
        RequireCount(state.NotableEvents.Length, MaximumNotableEventCount,
            nameof(state.NotableEvents));
        RequireCount(state.AttentionAlerts.Length, MaximumAttentionAlertCount,
            nameof(state.AttentionAlerts));
        RequireCount(state.PopulationAttentionStates.Length, MaximumSpeciesCount,
            nameof(state.PopulationAttentionStates));
        RequireCount(state.AttentionWindowStates.Length, MaximumSpeciesCount,
            nameof(state.AttentionWindowStates));
        RequireCount(
            state.RoutineActivitySummaries.Length,
            MaximumRoutineActivitySummaryCount,
            nameof(state.RoutineActivitySummaries));
        RequireCount(
            state.ResourceFlowHistory.Length,
            MaximumResourceFlowHistoryIntervalCount,
            nameof(state.ResourceFlowHistory));
        if (state.ResourceFlowHistory.Sum(interval => (long)interval.ResourceFlows.Length) >
            MaximumResourceFlowHistoryTotalFlows)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Resource-flow history exceeds its aggregate limit.");
        }
        RequireCount(
            state.LastCompletedTransactions.Length,
            MaximumTransactionCount,
            nameof(state.LastCompletedTransactions));
        if (HasInvalidGameplay(state.Gameplay))
        {
            throw new ArgumentException("Gameplay state is invalid.", nameof(state));
        }
        foreach (var organism in state.Organisms)
        {
            if (HasInvalidOrganism(organism))
            {
                throw new ArgumentException(
                    "An organism has invalid persisted state.",
                    nameof(state));
            }
        }

        foreach (var genome in state.Genomes)
        {
            if (genome.GenomeId == 0 || genome.FounderGenomeId == 0 ||
                genome.FounderAllocationId == 0 ||
                genome.AcquiredTraitIds.IsDefault || genome.AcquiredTraitIds.Length > MaximumTraitsPerGenome ||
                string.IsNullOrWhiteSpace(genome.GenomeHash))
            {
                throw new ArgumentException("A genome has invalid evolution state.", nameof(state));
            }
        }

        foreach (var species in state.Species)
        {
            if (species.Authority is < 1 or > 3 || species.AverageHealthQ > 1_000_000 ||
                species.EnergyShortagePressureQ > 1_000_000 || species.StarvationPressureQ > 1_000_000 ||
                species.FounderCounts.IsDefault || species.AcquiredTraitDelta.IsDefault)
            {
                throw new ArgumentException("A species has invalid evolution state.", nameof(state));
            }
        }

        foreach (var remnant in state.Remnants)
        {
            if (HasInvalidRemnant(remnant))
            {
                throw new ArgumentException("A remnant has invalid persisted state.", nameof(state));
            }
        }

        foreach (var value in state.JourneyEvents)
        {
            if (HasInvalidJourneyEvent(value))
            {
                throw new ArgumentException("A journey event has invalid persisted state.", nameof(state));
            }
        }
        if (state.LineageReviewSchedules.Any(HasInvalidLineageReviewSchedule) ||
            state.LineageReviewLandmarks.Any(HasInvalidLineageReviewLandmark))
        {
            throw new ArgumentException("A lineage-review record has invalid persisted state.",
                nameof(state));
        }
        if (state.NotableEvents.Any(HasInvalidNotableEvent))
        {
            throw new ArgumentException("A notable event has invalid persisted state.",
                nameof(state));
        }
        if (state.AttentionAlerts.Any(HasInvalidAttentionAlert) ||
            HasInvalidAttentionReferences(state) ||
            HasInvalidPopulationAttentionStates(state) ||
            HasInvalidAttentionWindowStates(state))
        {
            throw new ArgumentException("Attention state is invalid.", nameof(state));
        }
        foreach (var value in state.RoutineActivitySummaries)
        {
            if (HasInvalidRoutineActivitySummary(value))
            {
                throw new ArgumentException(
                    "A routine activity summary has invalid persisted state.",
                    nameof(state));
            }
        }
        foreach (var interval in state.ResourceFlowHistory)
        {
            if (HasInvalidResourceFlowHistoryInterval(interval))
            {
                throw new ArgumentException(
                    "A resource-flow history interval has invalid state.",
                    nameof(state));
            }
        }

        foreach (var transaction in state.LastCompletedTransactions)
        {
            if (transaction.MatterEntries.IsDefault || transaction.EnergyEntries.IsDefault)
            {
                throw new ArgumentException("Ledger entry collections must be initialized.", nameof(state));
            }

            RequireCount(transaction.MatterEntries.Length, MaximumEntriesPerTransaction, "matter entries");
            RequireCount(transaction.EnergyEntries.Length, MaximumEntriesPerTransaction, "energy entries");
        }
    }

    private static void ValidateDecodedStateValues(WorldPersistenceState state)
    {
        if (HasInvalidGameplay(state.Gameplay))
        {
            throw Failure(WorldPayloadFailureCode.InvalidValue, "Gameplay state is invalid.");
        }
        if (state.GasTileRemainders.Any(remainder =>
                remainder.SourceRemainderQ is < 0 or >= 1_000_000 ||
                remainder.SinkRemainderQ is < 0 or >= 1_000_000) ||
            state.GasEdgeRemainders.Any(remainder =>
                remainder.LowerTileId >= remainder.HigherTileId ||
                remainder.ExchangeRemainderQ is <= -1_000_000_000_000L or >= 1_000_000_000_000L))
        {
            throw Failure(WorldPayloadFailureCode.InvalidValue, "A gas fixed-point remainder is invalid.");
        }

        if (state.Genomes.Any(genome => genome.GenomeId == 0 ||
                genome.FounderGenomeId == 0 || genome.FounderAllocationId == 0 ||
                string.IsNullOrWhiteSpace(genome.GenomeHash)))
        {
            throw Failure(WorldPayloadFailureCode.InvalidValue, "A genome has invalid persisted identity.");
        }

        foreach (var organism in state.Organisms)
        {
            if (HasInvalidOrganism(organism))
            {
                throw Failure(
                    WorldPayloadFailureCode.InvalidValue,
                    "An organism has invalid persisted state.");
            }
        }


        foreach (var remnant in state.Remnants)
        {
            if (HasInvalidRemnant(remnant))
            {
                throw Failure(
                    WorldPayloadFailureCode.InvalidValue,
                    "A remnant has invalid persisted state.");
            }
        }
        if (state.JourneyEvents.Any(HasInvalidJourneyEvent))
        {
            throw Failure(WorldPayloadFailureCode.InvalidValue, "A journey event is invalid.");
        }
        if (state.LineageReviewSchedules.Any(HasInvalidLineageReviewSchedule) ||
            state.LineageReviewLandmarks.Any(HasInvalidLineageReviewLandmark))
        {
            throw Failure(WorldPayloadFailureCode.InvalidValue,
                "A lineage-review record is invalid.");
        }
        if (state.NotableEvents.Any(HasInvalidNotableEvent))
        {
            throw Failure(WorldPayloadFailureCode.InvalidValue, "A notable event is invalid.");
        }
        if (state.AttentionAlerts.Any(HasInvalidAttentionAlert) ||
            HasInvalidAttentionReferences(state) ||
            HasInvalidPopulationAttentionStates(state) ||
            HasInvalidAttentionWindowStates(state))
        {
            throw Failure(WorldPayloadFailureCode.InvalidValue, "Attention state is invalid.");
        }
        if (state.RoutineActivitySummaries.Any(HasInvalidRoutineActivitySummary))
        {
            throw Failure(
                WorldPayloadFailureCode.InvalidValue,
                "A routine activity summary is invalid.");
        }
        if (state.ResourceFlowHistory.Any(HasInvalidResourceFlowHistoryInterval))
        {
            throw Failure(
                WorldPayloadFailureCode.InvalidValue,
                "A resource-flow history interval is invalid.");
        }
    }

    private static bool HasInvalidOrganism(PersistenceOrganism organism) =>
        organism.OrganismId == 0 ||
        organism.SpeciesId == 0 ||
        organism.LifecyclePhase == 0 ||
        organism.IngestedStructuralMatterQ < 0 ||
        organism.StructuralMatterQ < 0 ||
        organism.ChargedReserveQ < 0 ||
        organism.ConditionSnapshotKind is < 1 or > 4 ||
        organism.ReserveFactorQ > 1_000_000 ||
        organism.StructureFactorQ > 1_000_000 ||
        organism.NutrientFactorQ > 1_000_000 ||
        organism.AgeFactorQ > 1_000_000 ||
        organism.LifecycleFactorQ > 1_000_000 ||
        organism.EnvironmentalFactorQ > 1_000_000 ||
        organism.RelativeHealthQ > 1_000_000 ||
        organism.TemperatureSeverityQ > 1_000_000 ||
        organism.BehaviorId is < 1 or > 5 ||
        organism.BehaviorTargetKind > 4 ||
        (organism.BehaviorTargetKind == 0 && organism.BehaviorTargetId != 0) ||
        organism.RecentEnergyCoverageQ > 2_000_000 ||
        organism.RecentAcquisitionCoverageQ > 2_000_000 ||
        organism.LimitingMaterialDeficitQ > 1_000_000 ||
        organism.CommittedMicronutrientsQ.IsDefault ||
        organism.CommittedMicronutrientsQ.Length != 14 ||
        organism.CommittedMicronutrientsQ.Any(value => value < 0) ||
        organism.FreeMicronutrientsQ.IsDefault ||
        organism.FreeMicronutrientsQ.Length != 14 ||
        organism.FreeMicronutrientsQ.Any(value => value < 0);

    private static bool HasInvalidRemnant(PersistenceRemnant remnant) =>
        remnant.RemnantId == 0 ||
        remnant.SourceOrganismId == 0 ||
        remnant.SourceSpeciesId == 0 ||
        remnant.StructuralMatterQ < 0 ||
        remnant.ChargedReserveQ < 0 ||
        remnant.MicronutrientsQ.IsDefault ||
        remnant.MicronutrientsQ.Length != 14 ||
        remnant.MicronutrientsQ.Any(value => value < 0) ||
        (remnant.StructuralMatterQ == 0 && remnant.ChargedReserveQ == 0 &&
            remnant.MicronutrientsQ.All(value => value == 0)) ||
        remnant.StructureDecayRemainderQ >= 1_000_000 ||
        remnant.ReserveDecayRemainderQ >= 1_000_000;

    private static bool HasInvalidJourneyEvent(PersistenceOrganismJourneyEvent value) =>
        value.EventId == 0 ||
        value.Phase > 11 ||
        value.Family is < 1 or > 8 ||
        value.SubjectOrganismId == 0 ||
        value.SubjectSpeciesId == 0 ||
        value.AmountQ < 0 ||
        value.DeathCauses.IsDefault ||
        value.DeathCauses.Length > 16 ||
        value.DeathCauses.Any(cause => cause.Cause == 0 || cause.ProbabilityQ > 1_000_000) ||
        (value.Family == 6 && value.RelatedRemnantId == 0) ||
        (value.Family != 6 && !value.DeathCauses.IsEmpty);

    private static bool HasInvalidLineageReviewObservation(
        PersistenceLineageReviewObservation value) =>
        value.SpeciesId == 0 ||
        value.Scope is < 1 or > 2 ||
        value.AverageHealthQ > 1_000_000 ||
        value.AverageReserveQ > 1_000_000 ||
        value.AverageAcquisitionCoverageQ > 2_000_000 ||
        value.AverageResourcePressureQ > 1_000_000 ||
        value.BehaviorCounts.IsDefault ||
        value.BehaviorCounts.Length > 5 ||
        value.BehaviorCounts.Where((count, index) =>
            count.BehaviorId is < 1 or > 5 || count.Count == 0 ||
            (index > 0 && count.BehaviorId <=
                value.BehaviorCounts[index - 1].BehaviorId)).Any() ||
        value.BehaviorCounts.Aggregate(
            (UInt128)0,
            (sum, count) => sum + count.Count) != value.Population ||
        (!value.ActivityCountsAvailable &&
            (value.BirthCount != 0 || value.DeathCount != 0 || value.MigrationCount != 0));

    private static bool HasInvalidLineageReviewSchedule(
        PersistenceLineageReviewSchedule value) =>
        value.SpeciationEventId == 0 ||
        value.AncestorSpeciesId == 0 ||
        value.DescendantSpeciesId == 0 ||
        (value.PerspectiveSpeciesId != value.AncestorSpeciesId &&
            value.PerspectiveSpeciesId != value.DescendantSpeciesId) ||
        value.TraitDelta.IsDefault ||
        value.CooldownBoundaryTick <= value.AppliedTick ||
        (value.FollowUpHours == 0) != (value.FollowUpBoundaryTick == 0) ||
        (value.FollowUpHours != 0 && value.FollowUpBoundaryTick <= value.CooldownBoundaryTick) ||
        value.FollowUpEvidenceKind is < 1 or > 5 ||
        value.PerspectiveBaseline.SpeciesId != value.PerspectiveSpeciesId ||
        value.PerspectiveBaseline.Scope != 1 ||
        value.PerspectiveBaseline.ActivityCountsAvailable ||
        value.ComparisonBaseline.SpeciesId != (value.PerspectiveSpeciesId ==
            value.AncestorSpeciesId ? value.DescendantSpeciesId : value.AncestorSpeciesId) ||
        value.ComparisonBaseline.Scope != 2 ||
        value.ComparisonBaseline.ActivityCountsAvailable ||
        HasInvalidLineageReviewActivationEvidence(
            value.CapabilityActivations,
            value.ReactionActivations) ||
        HasInvalidLineageReviewObservation(value.PerspectiveBaseline) ||
        HasInvalidLineageReviewObservation(value.ComparisonBaseline);

    private static bool HasInvalidLineageReviewLandmark(
        PersistenceLineageReviewLandmark value) =>
        value.EventId == 0 ||
        value.Kind is < 1 or > 2 ||
        value.CompletedTick == 0 ||
        value.WindowHours == 0 ||
        value.SpeciationEventId == 0 ||
        value.AncestorSpeciesId == 0 ||
        value.DescendantSpeciesId == 0 ||
        (value.PerspectiveSpeciesId != value.AncestorSpeciesId &&
            value.PerspectiveSpeciesId != value.DescendantSpeciesId) ||
        value.TraitDelta.IsDefault ||
        value.EvidenceKind is < 1 or > 5 ||
        (value.Kind == 1 && (value.WindowHours != 168 || value.EvidenceKind != 1)) ||
        (value.Kind == 2 && value.WindowHours <= 168) ||
        value.PerspectiveBaseline.SpeciesId != value.PerspectiveSpeciesId ||
        value.PerspectiveCurrent.SpeciesId != value.PerspectiveSpeciesId ||
        value.PerspectiveBaseline.Scope != 1 ||
        value.PerspectiveCurrent.Scope != 1 ||
        value.PerspectiveBaseline.ActivityCountsAvailable ||
        !value.PerspectiveCurrent.ActivityCountsAvailable ||
        value.ComparisonBaseline.SpeciesId != (value.PerspectiveSpeciesId ==
            value.AncestorSpeciesId ? value.DescendantSpeciesId : value.AncestorSpeciesId) ||
        value.ComparisonCurrent.SpeciesId != value.ComparisonBaseline.SpeciesId ||
        value.ComparisonBaseline.Scope != 2 ||
        value.ComparisonCurrent.Scope != 2 ||
        value.ComparisonBaseline.ActivityCountsAvailable ||
        value.ComparisonCurrent.ActivityCountsAvailable ||
        HasInvalidLineageReviewActivationEvidence(
            value.CapabilityActivations,
            value.ReactionActivations) ||
        HasInvalidLineageReviewEvidenceReferences(value) ||
        HasInvalidLineageReviewObservation(value.PerspectiveBaseline) ||
        HasInvalidLineageReviewObservation(value.ComparisonBaseline) ||
        HasInvalidLineageReviewObservation(value.PerspectiveCurrent) ||
        HasInvalidLineageReviewObservation(value.ComparisonCurrent);

    private static bool HasInvalidNotableEvent(PersistenceNotableEvent value)
    {
        if (value.EventId == 0 || value.Family is < 1 or > 11 ||
            value.SignificanceRuleVersion != 1 || value.SpeciesId == 0 ||
            (value.Family is not (8 or 9 or 11) && value.BaselineValue != 0) ||
            string.IsNullOrWhiteSpace(value.DeduplicationKey) ||
            Encoding.UTF8.GetByteCount(value.DeduplicationKey) > 128)
        {
            return true;
        }
        var expectedSignificance = value.Family switch
        {
            1 or 4 or 5 or 11 => (byte)2,
            2 or 3 => (byte)1,
            6 or 7 or 8 or 9 or 10 => (byte)3,
            _ => (byte)0,
        };
        var expectedKey = value.Family switch
        {
            1 when value.SourceEventId > 0 && value.RelatedSpeciesId > 0 &&
                !value.TileId.HasValue && value.ReactionId == 0 && value.MilestoneValue > 0 =>
                $"speciation:{value.SourceEventId}",
            2 when value.SourceEventId > 0 && value.RelatedSpeciesId == 0 &&
                value.TileId.HasValue && value.ReactionId == 0 && value.MilestoneValue == 1 =>
                $"first-reproduction:species:{value.SpeciesId}",
            3 when value.SourceEventId == 0 && value.RelatedSpeciesId == 0 &&
                !value.TileId.HasValue && value.ReactionId == 0 &&
                value.MilestoneValue is 100 or 250 or 500 or 1_000 or 2_500 or 5_000 or 10_000 =>
                $"population:species:{value.SpeciesId}:threshold:{value.MilestoneValue}",
            4 when value.SourceEventId > 0 && value.RelatedSpeciesId == 0 &&
                value.TileId.HasValue && value.ReactionId == 0 && value.MilestoneValue > 0 =>
                $"first-occupation:species:{value.SpeciesId}:tile:{value.TileId.Value}",
            5 when value.SourceEventId == 0 && value.RelatedSpeciesId == 0 &&
                value.TileId.HasValue && value.ReactionId > 0 && value.MilestoneValue == 1 =>
                $"first-reaction:species:{value.SpeciesId}:reaction:{value.ReactionId}",
            6 when value.SourceEventId == 0 && value.RelatedSpeciesId == 0 &&
                !value.TileId.HasValue && value.ReactionId == 0 && value.MilestoneValue > 0 =>
                $"extinction:species:{value.SpeciesId}",
            7 when value.SourceEventId == 0 && value.RelatedSpeciesId == 0 &&
                !value.TileId.HasValue && value.ReactionId == 0 &&
                value.MilestoneValue <= 10 && value.BaselineValue == 0 &&
                IsCanonicalEpisodeKey(value, "population-danger") =>
                value.DeduplicationKey,
            8 when value.SourceEventId == 0 && value.RelatedSpeciesId == 0 &&
                !value.TileId.HasValue && value.ReactionId == 0 &&
                value.BaselineValue > value.MilestoneValue &&
                ((UInt128)(value.BaselineValue - value.MilestoneValue) * 1_000_000) /
                    value.BaselineValue >= 250_000 &&
                IsCanonicalEpisodeKey(value, "population-decline") =>
                value.DeduplicationKey,
            9 when value.SourceEventId == 0 && value.RelatedSpeciesId == 0 &&
                !value.TileId.HasValue && value.ReactionId == 0 &&
                value.MilestoneValue < 250_000 && value.BaselineValue >= 6 &&
                IsCanonicalEpisodeKey(value, "low-health") =>
                value.DeduplicationKey,
            10 when value.SourceEventId > 0 && value.RelatedSpeciesId == 0 &&
                value.TileId.HasValue && value.ReactionId == 0 &&
                value.MilestoneValue is >= 1 and <= 7 && value.BaselineValue == 0 =>
                $"death-mechanism:species:{value.SpeciesId}:cause:{value.MilestoneValue}",
            11 when value.SourceEventId == 0 && value.RelatedSpeciesId == 0 &&
                !value.TileId.HasValue && value.ReactionId == 0 &&
                value.MilestoneValue >= 750_000 && value.BaselineValue >= 6 &&
                IsCanonicalEpisodeKey(value, "resource-pressure") =>
                value.DeduplicationKey,
            _ => string.Empty,
        };
        return value.Significance != expectedSignificance ||
            value.DeduplicationKey != expectedKey;
    }

    private static bool IsCanonicalEpisodeKey(
        PersistenceNotableEvent value,
        string family)
    {
        var prefix = $"{family}:species:{value.SpeciesId}:episode:";
        return value.DeduplicationKey.StartsWith(prefix, StringComparison.Ordinal) &&
            uint.TryParse(
                value.DeduplicationKey.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var ordinal) && ordinal > 0;
    }

    private static bool HasInvalidAttentionAlert(PersistenceAttentionAlert value) =>
        value.AlertId == 0 || value.AlertClass is < 1 or > 3 || value.Kind is < 1 or > 2 ||
        (value.Kind == 1) != (value.EventFamily is >= 1 and <= 11) ||
        value.SpeciesId == 0 || value.ChronicleEventIds.IsDefaultOrEmpty ||
        value.ChronicleEventIds.Length > MaximumAlertEvidenceCount ||
        string.IsNullOrWhiteSpace(value.DeduplicationKey) ||
        Encoding.UTF8.GetByteCount(value.DeduplicationKey) > 128;

    private static bool HasInvalidAttentionReferences(WorldPersistenceState state)
    {
        foreach (var alert in state.AttentionAlerts)
        {
            if (alert.Kind == 1)
            {
                var expected = state.NotableEvents.Where(value =>
                        value.Family == alert.EventFamily &&
                        value.SpeciesId == alert.SpeciesId &&
                        value.CompletedTick == alert.CompletedTick)
                    .OrderBy(value => value.EventId)
                    .ToImmutableArray();
                var expectedClass = alert.EventFamily switch
                {
                    2 or 3 => (byte)1,
                    1 or 4 or 5 or 11 => (byte)2,
                    6 or 7 or 8 or 9 or 10 => (byte)3,
                    _ => (byte)0,
                };
                if (expected.IsEmpty || alert.AlertClass != expectedClass ||
                    alert.SimulatedHours != expected[0].SimulatedHours ||
                    !alert.ChronicleEventIds.SequenceEqual(expected.Select(value => value.EventId)) ||
                    alert.DeduplicationKey !=
                        $"notable:{alert.EventFamily}:species:{alert.SpeciesId}:tick:{alert.CompletedTick}")
                {
                    return true;
                }
            }
            else
            {
                if (alert.EventFamily != 0 || alert.AlertClass != 2 ||
                    alert.ChronicleEventIds.Length != 1)
                {
                    return true;
                }
                var landmark = state.LineageReviewLandmarks.SingleOrDefault(value =>
                    value.EventId == alert.ChronicleEventIds[0]);
                if (landmark is null || landmark.PerspectiveSpeciesId != alert.SpeciesId ||
                    landmark.CompletedTick != alert.CompletedTick ||
                    landmark.SimulatedHours != alert.SimulatedHours ||
                    alert.DeduplicationKey !=
                        $"lineage-review:event:{alert.ChronicleEventIds[0]}")
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool HasInvalidPopulationAttentionStates(WorldPersistenceState state)
    {
        if (state.PopulationAttentionStates.Length != state.Species.Length ||
            !state.PopulationAttentionStates.Select(value => value.SpeciesId)
                .SequenceEqual(state.Species.Select(value => value.SpeciesId)))
        {
            return true;
        }
        var populations = state.Species.ToDictionary(value => value.SpeciesId,
            value => value.Population);
        return state.PopulationAttentionStates.Any(value =>
            value.SpeciesId == 0 ||
            (!value.HasExceededDangerThreshold &&
                (value.LowPopulationArmed || value.LowPopulationEpisodeOrdinal != 0 ||
                    populations[value.SpeciesId] > 10)) ||
            (value.LowPopulationEpisodeOrdinal > 0 &&
                !value.HasExceededDangerThreshold) ||
            (populations[value.SpeciesId] > 15 && !value.LowPopulationArmed) ||
            (populations[value.SpeciesId] <= 10 && value.HasExceededDangerThreshold &&
                value.LowPopulationArmed));
    }

    private static bool HasInvalidAttentionWindowStates(WorldPersistenceState state)
    {
        if (state.AttentionWindowStates.Length != state.Species.Length ||
            !state.AttentionWindowStates.Select(value => value.SpeciesId)
                .SequenceEqual(state.Species.Select(value => value.SpeciesId)))
        {
            return true;
        }
        var populations = state.Species.ToDictionary(value => value.SpeciesId,
            value => value.Population);
        return state.AttentionWindowStates.Any(value =>
            value.SpeciesId == 0 || value.PopulationSamples.IsDefaultOrEmpty ||
            value.PopulationSamples.Length > MaximumPopulationAttentionSamples ||
            (!value.PopulationDeclineArmed && value.PopulationDeclineEpisodeOrdinal == 0) ||
            (!value.LowHealthArmed && value.LowHealthEpisodeOrdinal == 0) ||
            (value.LowHealthArmed && value.HealthyRecoveryConsecutiveHours != 0) ||
            (!value.ResourcePressureArmed &&
                value.ResourcePressureEpisodeOrdinal == 0) ||
            (value.ResourcePressureArmed &&
                value.ResourcePressureRecoveryConsecutiveHours != 0) ||
            value.PopulationSamples[^1].Population != populations[value.SpeciesId] ||
            value.PopulationSamples[^1].SimulatedHours -
                value.PopulationSamples[0].SimulatedHours > 24 ||
            value.PopulationSamples.Where((sample, index) =>
                index > 0 && sample.SimulatedHours <=
                    value.PopulationSamples[index - 1].SimulatedHours).Any());
    }

    private static bool HasInvalidLineageReviewActivationEvidence(
        ImmutableArray<PersistenceLineageReviewCapabilityActivation> capabilities,
        ImmutableArray<PersistenceLineageReviewReactionActivation> reactions) =>
        capabilities.IsDefault ||
        capabilities.Length > 16 ||
        capabilities.Where((value, index) =>
            value.Kind != 1 ||
            value.SourceTraitId == 0 ||
            (!value.Installed &&
                (value.IntroducedByProposal || value.ActivationCount != 0)) ||
            (index > 0 && value.Kind <= capabilities[index - 1].Kind)).Any() ||
        reactions.IsDefault ||
        reactions.Length > 256 ||
        reactions.Where((value, index) =>
            value.ReactionId == 0 ||
            (!value.Installed &&
                (value.IntroducedByProposal || value.ActivationCount != 0)) ||
            (index > 0 && value.ReactionId <= reactions[index - 1].ReactionId)).Any();

    private static bool HasInvalidLineageReviewEvidenceReferences(
        PersistenceLineageReviewLandmark value)
    {
        var references = value.EvidenceReferences;
        var comparisonSpeciesId = value.PerspectiveSpeciesId == value.AncestorSpeciesId
            ? value.DescendantSpeciesId
            : value.AncestorSpeciesId;
        if (references.IsDefaultOrEmpty ||
            references.Length < 4 ||
            references.Length > MaximumLineageReviewEvidenceReferences ||
            references[0].Kind != 1 || references[1].Kind != 2 ||
            references[2].Kind != 3 || references[3].Kind != 4 ||
            references.Skip(4).Any(reference => reference.Kind != 5) ||
            references.Any(reference =>
                reference.FromExclusiveTick != references[0].FromExclusiveTick ||
                reference.FromExclusiveTick >= reference.ThroughCompletedTick ||
                reference.ThroughCompletedTick != value.CompletedTick))
        {
            return true;
        }
        if (references.Count(reference => reference.Kind == 1 &&
                reference.SpeciesId == 0 && !reference.TileId.HasValue) != 1 ||
            references.Count(reference => reference.Kind == 2 &&
                reference.SpeciesId == value.PerspectiveSpeciesId &&
                !reference.TileId.HasValue) != 1 ||
            references.Count(reference => reference.Kind == 3 &&
                reference.SpeciesId == comparisonSpeciesId &&
                !reference.TileId.HasValue) != 1 ||
            references.Count(reference => reference.Kind == 4 &&
                reference.SpeciesId == value.PerspectiveSpeciesId &&
                !reference.TileId.HasValue) != 1)
        {
            return true;
        }
        var tiles = references.Where(reference => reference.Kind == 5).ToImmutableArray();
        return references.Any(reference => reference.Kind is < 1 or > 5) ||
            references.Any(reference => reference.Kind switch
            {
                1 => reference.SpeciesId != 0 || reference.TileId.HasValue,
                2 => reference.SpeciesId != value.PerspectiveSpeciesId ||
                    reference.TileId.HasValue,
                3 => reference.SpeciesId != comparisonSpeciesId || reference.TileId.HasValue,
                4 => reference.SpeciesId != value.PerspectiveSpeciesId ||
                    reference.TileId.HasValue,
                5 => reference.SpeciesId != value.PerspectiveSpeciesId ||
                    !reference.TileId.HasValue,
                _ => true,
            }) ||
            tiles.Select(reference => reference.TileId!.Value).Distinct().Count() != tiles.Length ||
            !tiles.Select(reference => reference.TileId!.Value)
                .SequenceEqual(tiles.Select(reference => reference.TileId!.Value).Order());
    }

    private static bool HasInvalidRoutineActivitySummary(
        PersistenceOrganismRoutineActivitySummary value) =>
        value.PeriodHours == 0 ||
        value.SubjectOrganismId == 0 ||
        value.SubjectSpeciesId == 0 ||
        value.ResourceAcquisitions.IsDefaultOrEmpty ||
        value.ResourceAcquisitions.Length > 256 ||
        value.ResourceAcquisitions.Any(resource =>
            resource.ResourceId == 0 || resource.AmountQ <= 0) ||
        value.ResourceAcquisitions.Select(resource => resource.ResourceId).Distinct().Count() !=
            value.ResourceAcquisitions.Length;

    private static bool HasInvalidResourceFlowHistoryInterval(
        PersistenceResourceFlowHistoryInterval value) =>
        value.CompletedTick == 0 ||
        value.EndSimulatedHour == 0 ||
        value.PeriodHours == 0 ||
        value.ResourceFlows.IsDefault ||
        value.ResourceFlows.Length > MaximumResourceFlowsPerInterval ||
        value.ResourceFlows.Any(flow =>
            flow.ResourceId == 0 ||
            flow.Kind is < 1 or > 6 ||
            flow.AmountQ <= 0);

    private static bool HasInvalidGameplay(PersistenceGameState game) =>
        game.Mode is < 1 or > 2 ||
        game.RunStatus is < 1 or > 2 ||
        game.LossReason > 2 ||
        game.ControlledSpeciesId == 0 ||
        game.GameplayRevision == 0 ||
        game.Roots.Length != (game.Mode == 1 ? 1 : 2) ||
        game.Roots.Any(root => root.SpeciesId == 0 || root.FounderGenomeId == 0 ||
            root.FounderAllocationId == 0 ||
            root.InitialPopulation == 0) ||
        game.Roots.Count(root => root.PlayerSelected) != 1 ||
        (game.RunStatus == 1 && (game.LossReason != 0 || game.EndedTick.HasValue)) ||
        (game.RunStatus == 2 && (game.LossReason == 0 || !game.EndedTick.HasValue));

    private static void ValidateCanonicalOrder(WorldPersistenceState state)
    {
        for (var index = 1; index < state.TileResources.Length; index++)
        {
            var prior = state.TileResources[index - 1];
            var current = state.TileResources[index];
            if (prior.TileId > current.TileId ||
                (prior.TileId == current.TileId && prior.ResourceId >= current.ResourceId))
            {
                throw Failure(WorldPayloadFailureCode.InvalidOrdering, "Tile resources are not canonical.");
            }
        }

        for (var index = 1; index < state.GasTileRemainders.Length; index++)
        {
            var prior = state.GasTileRemainders[index - 1];
            var current = state.GasTileRemainders[index];
            if (prior.TileId > current.TileId ||
                (prior.TileId == current.TileId && prior.ResourceId >= current.ResourceId))
            {
                throw Failure(WorldPayloadFailureCode.InvalidOrdering, "Gas tile remainders are not canonical.");
            }
        }
        for (var index = 1; index < state.GasEdgeRemainders.Length; index++)
        {
            var prior = state.GasEdgeRemainders[index - 1];
            var current = state.GasEdgeRemainders[index];
            if (prior.LowerTileId > current.LowerTileId ||
                (prior.LowerTileId == current.LowerTileId && prior.HigherTileId > current.HigherTileId) ||
                (prior.LowerTileId == current.LowerTileId && prior.HigherTileId == current.HigherTileId &&
                 prior.ResourceId >= current.ResourceId))
            {
                throw Failure(WorldPayloadFailureCode.InvalidOrdering, "Gas edge remainders are not canonical.");
            }
        }

        RequireAscending(state.Genomes.Select(value => value.GenomeId), "genomes");
        RequireAscending(state.Species.Select(value => value.SpeciesId), "species");
        RequireAscending(state.SpeciationEvents.Select(value => value.EventId), "speciation events");
        RequireAscending(state.Organisms.Select(value => value.OrganismId), "organisms");
        RequireAscending(state.Remnants.Select(value => value.RemnantId), "remnants");
        RequireAscending(state.JourneyEvents.Select(value => value.EventId), "journey events");
        RequireAscending(state.LineageReviewSchedules.Select(value => value.SpeciationEventId),
            "lineage review schedules");
        RequireAscending(state.LineageReviewLandmarks.Select(value => value.EventId),
            "lineage review landmarks");
        RequireAscending(state.NotableEvents.Select(value => value.EventId), "notable events");
        RequireAscending(state.AttentionAlerts.Select(value => value.AlertId), "attention alerts");
        RequireAscending(state.PopulationAttentionStates.Select(value => value.SpeciesId),
            "population attention states");
        RequireAscending(state.AttentionWindowStates.Select(value => value.SpeciesId),
            "attention window states");
        var chronicleEventIds = state.LineageReviewLandmarks.Select(value => value.EventId)
            .Concat(state.NotableEvents.Select(value => value.EventId))
            .ToArray();
        if (chronicleEventIds.Distinct().Count() != chronicleEventIds.Length ||
            state.NotableEvents.Select(value => value.DeduplicationKey).Distinct().Count() !=
                state.NotableEvents.Length ||
            state.AttentionAlerts.Select(value => value.DeduplicationKey).Distinct().Count() !=
                state.AttentionAlerts.Length)
        {
            throw Failure(WorldPayloadFailureCode.InvalidOrdering,
                "Chronicle event identities and deduplication keys must be unique.");
        }
        if (state.JourneyEvents.Length > 0 &&
            state.NextJourneyEventId <= state.JourneyEvents[^1].EventId)
        {
            throw Failure(
                WorldPayloadFailureCode.InvalidOrdering,
                "The next journey event ID does not follow retained events.");
        }
        for (var index = 0; index < state.RoutineActivitySummaries.Length; index++)
        {
            var current = state.RoutineActivitySummaries[index];
            RequireAscending(
                current.ResourceAcquisitions.Select(value => (ulong)value.ResourceId),
                "routine activity resources");
            if (index > 0 && CompareRoutineSummaries(
                    state.RoutineActivitySummaries[index - 1], current) >= 0)
            {
                throw Failure(
                    WorldPayloadFailureCode.InvalidOrdering,
                    "Routine activity summaries are not canonical.");
            }
        }
        for (var index = 0; index < state.ResourceFlowHistory.Length; index++)
        {
            var current = state.ResourceFlowHistory[index];
            if (index > 0)
            {
                var prior = state.ResourceFlowHistory[index - 1];
                if (current.CompletedTick != prior.CompletedTick + 1 ||
                    current.EndSimulatedHour != prior.EndSimulatedHour + current.PeriodHours)
                {
                    throw Failure(
                        WorldPayloadFailureCode.InvalidOrdering,
                        "Resource-flow history intervals are not contiguous.");
                }
            }
            for (var flowIndex = 1; flowIndex < current.ResourceFlows.Length; flowIndex++)
            {
                if (CompareResourceFlows(
                        current.ResourceFlows[flowIndex - 1],
                        current.ResourceFlows[flowIndex]) >= 0)
                {
                    throw Failure(
                        WorldPayloadFailureCode.InvalidOrdering,
                        "Resource flows within a history interval are not canonical.");
                }
            }
        }
        RequireAscending(state.Gameplay.Roots.Select(value => value.SpeciesId), "gameplay roots");
        RequireAscending(state.Gameplay.MutationLockedSpeciesIds, "gameplay locks");
        for (var index = 1; index < state.LastCompletedTransactions.Length; index++)
        {
            if (CompareTransactions(
                    state.LastCompletedTransactions[index - 1],
                    state.LastCompletedTransactions[index]) >= 0)
            {
                throw Failure(WorldPayloadFailureCode.InvalidOrdering, "Transactions are not canonical.");
            }
        }
    }

    private static int CompareTransactions(
        PersistenceResourceTransaction left,
        PersistenceResourceTransaction right)
    {
        var comparison = left.Tick.CompareTo(right.Tick);
        if (comparison != 0) return comparison;
        comparison = left.Phase.CompareTo(right.Phase);
        if (comparison != 0) return comparison;
        comparison = left.ScopeId.CompareTo(right.ScopeId);
        if (comparison != 0) return comparison;
        comparison = left.KeyActorId.CompareTo(right.KeyActorId);
        if (comparison != 0) return comparison;
        comparison = left.KeyReactionId.CompareTo(right.KeyReactionId);
        return comparison != 0 ? comparison : left.LocalOrdinal.CompareTo(right.LocalOrdinal);
    }

    private static int CompareResourceFlows(
        PersistenceTileResourceFlow left,
        PersistenceTileResourceFlow right)
    {
        var comparison = left.TileId.CompareTo(right.TileId);
        if (comparison != 0) return comparison;
        comparison = left.ResourceId.CompareTo(right.ResourceId);
        return comparison != 0 ? comparison : left.Kind.CompareTo(right.Kind);
    }

    private static int CompareRoutineSummaries(
        PersistenceOrganismRoutineActivitySummary left,
        PersistenceOrganismRoutineActivitySummary right)
    {
        var comparison = left.SubjectOrganismId.CompareTo(right.SubjectOrganismId);
        if (comparison != 0) return comparison;
        comparison = left.BucketStartHour.CompareTo(right.BucketStartHour);
        if (comparison != 0) return comparison;
        comparison = left.TileId.CompareTo(right.TileId);
        return comparison != 0 ? comparison : left.PeriodHours.CompareTo(right.PeriodHours);
    }

    private static void RequireAscending(IEnumerable<ulong> values, string label)
    {
        ulong prior = 0;
        foreach (var value in values)
        {
            if (value == 0 || value <= prior)
            {
                throw Failure(WorldPayloadFailureCode.InvalidOrdering, $"Persisted {label} are not canonical.");
            }

            prior = value;
        }
    }

    private static int ReadCount(uint value, int maximum)
    {
        if (value > maximum)
        {
            throw Failure(WorldPayloadFailureCode.InvalidCount, "A logical payload count exceeds its limit.");
        }

        return checked((int)value);
    }

    private static void RequireCount(int value, int maximum, string label)
    {
        if (value < 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(label, value, "Logical payload count exceeds its limit.");
        }
    }

    private static void WriteCount(PayloadWriter writer, int value) =>
        writer.WriteUInt32(checked((uint)value));

    private static void WriteUInt32Array(PayloadWriter writer, ImmutableArray<uint> values)
    {
        WriteCount(writer, values.Length);
        foreach (var value in values) writer.WriteUInt32(value);
    }

    private static void WriteMicronutrients(PayloadWriter writer, ImmutableArray<long> values)
    {
        if (values.Length != 14 || values.Any(value => value < 0))
        {
            throw new ArgumentException("Micronutrient vectors must contain fourteen nonnegative values.");
        }
        foreach (var value in values) writer.WriteInt64(value);
    }

    private static void WriteLineageReviewObservation(
        PayloadWriter writer,
        PersistenceLineageReviewObservation value)
    {
        writer.WriteUInt64(value.SpeciesId);
        writer.WriteByte(value.Scope);
        writer.WriteUInt64(value.Population);
        writer.WriteUInt32(value.AverageHealthQ);
        writer.WriteUInt32(value.AverageReserveQ);
        writer.WriteUInt32(value.AverageAcquisitionCoverageQ);
        writer.WriteUInt32(value.AverageResourcePressureQ);
        writer.WriteUInt32(value.OccupiedTileCount);
        WriteCount(writer, value.BehaviorCounts.Length);
        foreach (var count in value.BehaviorCounts)
        {
            writer.WriteByte(count.BehaviorId);
            writer.WriteUInt64(count.Count);
        }
        writer.WriteByte(value.ActivityCountsAvailable ? (byte)1 : (byte)0);
        writer.WriteUInt64(value.BirthCount);
        writer.WriteUInt64(value.DeathCount);
        writer.WriteUInt64(value.MigrationCount);
    }

    private static void WriteLineageReviewActivationEvidence(
        PayloadWriter writer,
        ImmutableArray<PersistenceLineageReviewCapabilityActivation> capabilities,
        ImmutableArray<PersistenceLineageReviewReactionActivation> reactions)
    {
        WriteCount(writer, capabilities.Length);
        foreach (var capability in capabilities)
        {
            writer.WriteByte(capability.Kind);
            writer.WriteUInt32(capability.SourceTraitId);
            writer.WriteByte(capability.IntroducedByProposal ? (byte)1 : (byte)0);
            writer.WriteByte(capability.Installed ? (byte)1 : (byte)0);
            writer.WriteUInt64(capability.ActivationCount);
        }
        WriteCount(writer, reactions.Length);
        foreach (var reaction in reactions)
        {
            writer.WriteUInt32(reaction.ReactionId);
            writer.WriteByte(reaction.IntroducedByProposal ? (byte)1 : (byte)0);
            writer.WriteByte(reaction.Installed ? (byte)1 : (byte)0);
            writer.WriteUInt64(reaction.ActivationCount);
        }
    }

    private static PersistenceLineageReviewObservation ReadLineageReviewObservation(
        ref PayloadReader reader) => new(
            reader.ReadUInt64(),
            reader.ReadByte(),
            reader.ReadUInt64(),
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            ReadLineageReviewBehaviorCounts(ref reader),
            reader.ReadByte() != 0,
            reader.ReadUInt64(),
            reader.ReadUInt64(),
            reader.ReadUInt64());

    private static ImmutableArray<PersistenceLineageReviewBehaviorCount>
        ReadLineageReviewBehaviorCounts(ref PayloadReader reader)
    {
        var count = ReadCount(reader.ReadUInt32(), 5);
        var values = ImmutableArray.CreateBuilder<PersistenceLineageReviewBehaviorCount>(count);
        for (var index = 0; index < count; index++)
        {
            values.Add(new PersistenceLineageReviewBehaviorCount(
                reader.ReadByte(),
                reader.ReadUInt64()));
        }
        return values.MoveToImmutable();
    }

    private static ImmutableArray<PersistenceLineageReviewCapabilityActivation>
        ReadLineageReviewCapabilityActivations(ref PayloadReader reader)
    {
        var count = ReadCount(reader.ReadUInt32(), 16);
        var values =
            ImmutableArray.CreateBuilder<PersistenceLineageReviewCapabilityActivation>(count);
        for (var index = 0; index < count; index++)
        {
            values.Add(new PersistenceLineageReviewCapabilityActivation(
                reader.ReadByte(),
                reader.ReadUInt32(),
                reader.ReadByte() != 0,
                reader.ReadByte() != 0,
                reader.ReadUInt64()));
        }
        return values.MoveToImmutable();
    }

    private static ImmutableArray<PersistenceLineageReviewReactionActivation>
        ReadLineageReviewReactionActivations(ref PayloadReader reader)
    {
        var count = ReadCount(reader.ReadUInt32(), 256);
        var values =
            ImmutableArray.CreateBuilder<PersistenceLineageReviewReactionActivation>(count);
        for (var index = 0; index < count; index++)
        {
            values.Add(new PersistenceLineageReviewReactionActivation(
                reader.ReadUInt32(),
                reader.ReadByte() != 0,
                reader.ReadByte() != 0,
                reader.ReadUInt64()));
        }
        return values.MoveToImmutable();
    }

    private static ImmutableArray<PersistenceLineageReviewEvidenceReference>
        ReadLineageReviewEvidenceReferences(ref PayloadReader reader)
    {
        var count = ReadCount(
            reader.ReadUInt32(),
            MaximumLineageReviewEvidenceReferences);
        var references = ImmutableArray.CreateBuilder<PersistenceLineageReviewEvidenceReference>(
            count);
        for (var index = 0; index < count; index++)
        {
            var kind = reader.ReadByte();
            var speciesId = reader.ReadUInt64();
            var hasTileId = reader.ReadByte();
            if (hasTileId > 1)
            {
                throw Failure(
                    WorldPayloadFailureCode.InvalidValue,
                    "A lineage-review evidence reference has an invalid optional tile marker.");
            }
            var tileId = hasTileId == 0 ? (uint?)null : reader.ReadUInt32();
            references.Add(new PersistenceLineageReviewEvidenceReference(
                kind,
                speciesId,
                tileId,
                reader.ReadUInt64(),
                reader.ReadUInt64()));
        }
        return references.MoveToImmutable();
    }

    private static ImmutableArray<long> ReadMicronutrients(ref PayloadReader reader)
    {
        var values = ImmutableArray.CreateBuilder<long>(14);
        for (var index = 0; index < 14; index++) values.Add(reader.ReadInt64());
        return values.MoveToImmutable();
    }

    private static ImmutableArray<uint> ReadUInt32Array(ref PayloadReader reader)
    {
        var count = ReadCount(reader.ReadUInt32(), MaximumTraitsPerGenome);
        var values = ImmutableArray.CreateBuilder<uint>(count);
        for (var index = 0; index < count; index++) values.Add(reader.ReadUInt32());
        return values.MoveToImmutable();
    }

    private static ImmutableArray<ulong> ReadUInt64Array(
        ref PayloadReader reader,
        int maximum)
    {
        var count = ReadCount(reader.ReadUInt32(), maximum);
        var values = ImmutableArray.CreateBuilder<ulong>(count);
        for (var index = 0; index < count; index++) values.Add(reader.ReadUInt64());
        return values.MoveToImmutable();
    }

    private static void WriteFounderCounts(
        PayloadWriter writer,
        ImmutableArray<PersistenceFounderCount> values)
    {
        WriteCount(writer, values.Length);
        foreach (var value in values)
        {
            writer.WriteUInt32(value.TileId);
            writer.WriteUInt32(value.Count);
        }
    }

    private static ImmutableArray<PersistenceFounderCount> ReadFounderCounts(ref PayloadReader reader)
    {
        var count = ReadCount(reader.ReadUInt32(), 4);
        var values = ImmutableArray.CreateBuilder<PersistenceFounderCount>(count);
        for (var index = 0; index < count; index++)
        {
            values.Add(new PersistenceFounderCount(reader.ReadUInt32(), reader.ReadUInt32()));
        }
        return values.MoveToImmutable();
    }

    private static WorldPayloadException Failure(WorldPayloadFailureCode code, string message) =>
        new(code, message);

    private sealed class PayloadWriter
    {
        private readonly ArrayBufferWriter<byte> buffer = new();

        public void WriteByte(byte value)
        {
            buffer.GetSpan(1)[0] = value;
            buffer.Advance(1);
        }

        public void WriteUInt32(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.GetSpan(sizeof(uint)), value);
            buffer.Advance(sizeof(uint));
        }

        public void WriteInt32(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer.GetSpan(sizeof(int)), value);
            buffer.Advance(sizeof(int));
        }

        public void WriteUInt64(ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.GetSpan(sizeof(ulong)), value);
            buffer.Advance(sizeof(ulong));
        }

        public void WriteInt64(long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(buffer.GetSpan(sizeof(long)), value);
            buffer.Advance(sizeof(long));
        }

        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            value.CopyTo(buffer.GetSpan(value.Length));
            buffer.Advance(value.Length);
        }

        public void WriteString(string value)
        {
            var byteCount = Encoding.UTF8.GetByteCount(value);
            WriteCount(this, byteCount);
            Encoding.UTF8.GetBytes(value, buffer.GetSpan(byteCount));
            buffer.Advance(byteCount);
        }

        public byte[] ToArray() => buffer.WrittenSpan.ToArray();
    }

    private ref struct PayloadReader(ReadOnlySpan<byte> source)
    {
        private readonly ReadOnlySpan<byte> source = source;
        private int offset;

        public readonly bool IsComplete => offset == source.Length;

        public byte ReadByte() => ReadBytes(1)[0];

        public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(sizeof(uint)));

        public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(sizeof(int)));

        public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(sizeof(ulong)));

        public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(long)));

        public string ReadString()
        {
            var length = ReadCount(ReadUInt32(), 4096);
            return Encoding.UTF8.GetString(ReadBytes(length));
        }

        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            if (length < 0 || source.Length - offset < length)
            {
                throw Failure(WorldPayloadFailureCode.Truncated, "The logical world payload is truncated.");
            }

            var result = source.Slice(offset, length);
            offset += length;
            return result;
        }
    }
}

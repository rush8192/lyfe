using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
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
    public const uint SchemaVersion = 11;
    public const int MaximumTileResourceCount = 4_000_000;
    public const int MaximumGenomeCount = 1_000_000;
    public const int MaximumSpeciesCount = 1_000_000;
    public const int MaximumTraitsPerGenome = 1_024;
    public const int MaximumOrganismCount = 1_000_000;
    public const int MaximumRemnantCount = 1_000_000;
    public const int MaximumJourneyEventCount = 10_000_000;
    public const int MaximumRoutineActivitySummaryCount = 10_000_000;
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
        WriteCount(writer, state.RoutineActivitySummaries.Length);
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
        var routineSummaryCount = ReadCount(
            reader.ReadUInt32(),
            MaximumRoutineActivitySummaryCount);
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
            transactions.MoveToImmutable(),
            gameplay);
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
            state.RoutineActivitySummaries.IsDefault ||
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
        RequireCount(
            state.RoutineActivitySummaries.Length,
            MaximumRoutineActivitySummaryCount,
            nameof(state.RoutineActivitySummaries));
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
        foreach (var value in state.RoutineActivitySummaries)
        {
            if (HasInvalidRoutineActivitySummary(value))
            {
                throw new ArgumentException(
                    "A routine activity summary has invalid persisted state.",
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
        if (state.RoutineActivitySummaries.Any(HasInvalidRoutineActivitySummary))
        {
            throw Failure(
                WorldPayloadFailureCode.InvalidValue,
                "A routine activity summary is invalid.");
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

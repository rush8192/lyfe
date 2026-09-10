using System.Collections.Immutable;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Publication;

public readonly record struct PersistenceTileResource(
    uint TileId,
    uint ResourceId,
    long QuantityQ);

public readonly record struct PersistenceGasTileRemainder(
    uint TileId,
    uint ResourceId,
    long SourceRemainderQ,
    long SinkRemainderQ);

public readonly record struct PersistenceGasEdgeRemainder(
    uint LowerTileId,
    uint HigherTileId,
    uint ResourceId,
    long ExchangeRemainderQ);

public readonly record struct PersistenceGenome(
    ulong GenomeId,
    uint FounderGenomeId,
    uint FounderAllocationId,
    ImmutableArray<uint> AcquiredTraitIds,
    string GenomeHash);

public readonly record struct PersistenceFounderCount(uint TileId, uint Count);

public readonly record struct PersistenceSpecies(
    ulong SpeciesId,
    ulong GenomeId,
    ulong Population,
    byte Authority,
    long MutationBalanceQ,
    ulong MutationIncomeRemainderLow,
    ulong MutationIncomeRemainderHigh,
    ulong SpeciationNotBeforeTick,
    uint SpeciationOrdinal,
    ulong EvolutionRevision,
    uint AverageHealthQ,
    long LastIncomeQ,
    uint EnergyShortagePressureQ,
    uint StarvationPressureQ,
    ulong ParentSpeciesId,
    ulong FoundingEventId,
    ulong CreatedTick,
    ulong? ExtinctTick,
    ImmutableArray<PersistenceFounderCount> FounderCounts,
    ImmutableArray<uint> AcquiredTraitDelta);

public sealed record PersistenceSpeciationEvent(
    ulong EventId,
    ulong Tick,
    ulong AncestorSpeciesId,
    ulong DescendantSpeciesId,
    byte ActorKind,
    ImmutableArray<PersistenceFounderCount> FounderCounts,
    string FounderSelectionDigest,
    ImmutableArray<uint> TraitDelta,
    long MutationPriceQ,
    long BalanceBeforeQ,
    long DuplicatedBalanceAfterQ,
    uint ChangeComplexity);

public readonly record struct PersistenceAbiogenesisRoot(
    ulong SpeciesId,
    uint FounderGenomeId,
    uint FounderAllocationId,
    uint StartingTileId,
    uint InitialPopulation,
    bool PlayerSelected);

public sealed record PersistenceGameState(
    byte Mode,
    byte RunStatus,
    byte LossReason,
    ulong ControlledSpeciesId,
    ulong GameplayRevision,
    ulong? EndedTick,
    ImmutableArray<PersistenceAbiogenesisRoot> Roots,
    ImmutableArray<ulong> MutationLockedSpeciesIds);

public readonly record struct PersistenceOrganism(
    ulong OrganismId,
    ulong SpeciesId,
    uint TileId,
    uint PositionXQ,
    uint PositionYQ,
    long VelocityXQPerHour,
    long VelocityYQPerHour,
    ulong BirthTick,
    ulong BiologicalAgeHours,
    byte LifecyclePhase,
    ulong ReproductionNotBeforeTick,
    ulong SuccessfulReproductionCount,
    ulong ScavengeNotBeforeTick,
    long IngestedStructuralMatterQ,
    long StructuralMatterQ,
    long ChargedReserveQ,
    ulong ConditionEvaluatedTick,
    byte ConditionSnapshotKind,
    uint ReserveFactorQ,
    uint StructureFactorQ,
    uint NutrientFactorQ,
    uint AgeFactorQ,
    uint LifecycleFactorQ,
    uint EnvironmentalFactorQ,
    uint RelativeHealthQ,
    int ConditionTemperatureMilliC,
    uint TemperatureSeverityQ,
    byte BehaviorId,
    byte BehaviorTargetKind,
    ulong BehaviorTargetId,
    uint BehaviorTargetPositionXQ,
    uint BehaviorTargetPositionYQ,
    ulong BehaviorSelectedAtTick,
    ulong BehaviorMinimumDwellUntilTick,
    uint RecentEnergyCoverageQ,
    uint RecentAcquisitionCoverageQ,
    uint LimitingMaterialDeficitQ,
    ImmutableArray<long> CommittedMicronutrientsQ,
    ImmutableArray<long> FreeMicronutrientsQ);

public readonly record struct PersistenceRemnant(
    ulong RemnantId,
    ulong SourceOrganismId,
    ulong SourceSpeciesId,
    ulong CreatedTick,
    uint TileId,
    uint PositionXQ,
    uint PositionYQ,
    long StructuralMatterQ,
    long ChargedReserveQ,
    uint StructureDecayRemainderQ,
    uint ReserveDecayRemainderQ,
    ImmutableArray<long> MicronutrientsQ);

public readonly record struct PersistenceMatterEntry(
    byte OwnerKind,
    ulong OwnerId,
    byte Compartment,
    uint ResourceId,
    long DeltaQ);

public readonly record struct PersistenceEnergyEntry(
    byte Kind,
    ulong OwnerId,
    long DeltaQ);

public readonly record struct PersistenceJourneyDeathCause(
    byte Cause,
    uint ProbabilityQ,
    bool Triggered);

public sealed record PersistenceOrganismJourneyEvent(
    ulong EventId,
    ulong Tick,
    byte Phase,
    byte Family,
    ulong SubjectOrganismId,
    ulong SubjectSpeciesId,
    uint TileId,
    uint PositionXQ,
    uint PositionYQ,
    ulong RelatedOrganismId,
    ulong RelatedRemnantId,
    uint ResourceId,
    long AmountQ,
    uint DetailId,
    ImmutableArray<PersistenceJourneyDeathCause> DeathCauses);

public readonly record struct PersistenceRoutineResourceAcquisition(
    uint ResourceId,
    long AmountQ);

public sealed record PersistenceOrganismRoutineActivitySummary(
    ulong BucketStartHour,
    uint PeriodHours,
    ulong SubjectOrganismId,
    ulong SubjectSpeciesId,
    uint TileId,
    ImmutableArray<PersistenceRoutineResourceAcquisition> ResourceAcquisitions);

public readonly record struct PersistenceLineageReviewBehaviorCount(
    byte BehaviorId,
    ulong Count);

public readonly record struct PersistenceLineageReviewCapabilityActivation(
    byte Kind,
    uint SourceTraitId,
    bool IntroducedByProposal,
    bool Installed,
    ulong ActivationCount);

public readonly record struct PersistenceLineageReviewReactionActivation(
    uint ReactionId,
    bool IntroducedByProposal,
    bool Installed,
    ulong ActivationCount);

public readonly record struct PersistenceLineageReviewObservation(
    ulong SpeciesId,
    byte Scope,
    ulong Population,
    uint AverageHealthQ,
    uint AverageReserveQ,
    uint AverageAcquisitionCoverageQ,
    uint AverageResourcePressureQ,
    uint OccupiedTileCount,
    ImmutableArray<PersistenceLineageReviewBehaviorCount> BehaviorCounts,
    bool ActivityCountsAvailable,
    ulong BirthCount,
    ulong DeathCount,
    ulong MigrationCount);

public sealed record PersistenceLineageReviewSchedule(
    ulong SpeciationEventId,
    ulong AppliedTick,
    ulong AppliedSimulatedHours,
    ulong AncestorSpeciesId,
    ulong DescendantSpeciesId,
    ulong PerspectiveSpeciesId,
    ImmutableArray<uint> TraitDelta,
    ulong CooldownBoundaryTick,
    ulong FollowUpBoundaryTick,
    ulong FollowUpHours,
    byte FollowUpEvidenceKind,
    PersistenceLineageReviewObservation PerspectiveBaseline,
    PersistenceLineageReviewObservation ComparisonBaseline,
    ImmutableArray<PersistenceLineageReviewCapabilityActivation> CapabilityActivations,
    ImmutableArray<PersistenceLineageReviewReactionActivation> ReactionActivations);

public readonly record struct PersistenceLineageReviewEvidenceReference(
    byte Kind,
    ulong SpeciesId,
    uint? TileId,
    ulong FromExclusiveTick,
    ulong ThroughCompletedTick);

public sealed record PersistenceLineageReviewLandmark(
    ulong EventId,
    byte Kind,
    ulong CompletedTick,
    ulong SimulatedHours,
    ulong WindowHours,
    ulong SpeciationEventId,
    ulong AncestorSpeciesId,
    ulong DescendantSpeciesId,
    ulong PerspectiveSpeciesId,
    ImmutableArray<uint> TraitDelta,
    byte EvidenceKind,
    PersistenceLineageReviewObservation PerspectiveBaseline,
    PersistenceLineageReviewObservation ComparisonBaseline,
    PersistenceLineageReviewObservation PerspectiveCurrent,
    PersistenceLineageReviewObservation ComparisonCurrent,
    ImmutableArray<PersistenceLineageReviewCapabilityActivation> CapabilityActivations,
    ImmutableArray<PersistenceLineageReviewReactionActivation> ReactionActivations,
    ImmutableArray<PersistenceLineageReviewEvidenceReference> EvidenceReferences);

public sealed record PersistenceNotableEvent(
    ulong EventId,
    byte Family,
    byte Significance,
    uint SignificanceRuleVersion,
    ulong CompletedTick,
    ulong SimulatedHours,
    ulong SpeciesId,
    ulong RelatedSpeciesId,
    uint? TileId,
    uint ReactionId,
    ulong SourceEventId,
    ulong MilestoneValue,
    ulong BaselineValue,
    string DeduplicationKey);

public sealed record PersistenceAttentionAlert(
    ulong AlertId,
    byte AlertClass,
    byte Kind,
    byte EventFamily,
    ulong CompletedTick,
    ulong SimulatedHours,
    ulong SpeciesId,
    ImmutableArray<ulong> ChronicleEventIds,
    string DeduplicationKey);

public readonly record struct PersistencePopulationAttentionState(
    ulong SpeciesId,
    bool HasExceededDangerThreshold,
    bool LowPopulationArmed,
    uint LowPopulationEpisodeOrdinal);

public readonly record struct PersistencePopulationAttentionSample(
    ulong SimulatedHours,
    ulong Population);

public sealed record PersistenceAttentionWindowState(
    ulong SpeciesId,
    bool PopulationDeclineArmed,
    uint PopulationDeclineEpisodeOrdinal,
    uint LowHealthConsecutiveHours,
    bool LowHealthArmed,
    uint HealthyRecoveryConsecutiveHours,
    uint LowHealthEpisodeOrdinal,
    uint ResourcePressureConsecutiveHours,
    bool ResourcePressureArmed,
    uint ResourcePressureRecoveryConsecutiveHours,
    uint ResourcePressureEpisodeOrdinal,
    ImmutableArray<PersistencePopulationAttentionSample> PopulationSamples);

public readonly record struct PersistenceTileResourceFlow(
    uint TileId,
    uint ResourceId,
    byte Kind,
    long AmountQ);

public sealed record PersistenceResourceFlowHistoryInterval(
    ulong CompletedTick,
    ulong EndSimulatedHour,
    uint PeriodHours,
    ImmutableArray<PersistenceTileResourceFlow> ResourceFlows);

public sealed record PersistenceResourceTransaction(
    ulong Tick,
    byte Phase,
    ulong ScopeId,
    ulong KeyActorId,
    uint KeyReactionId,
    uint LocalOrdinal,
    byte Cause,
    uint ReactionId,
    ulong ActorId,
    uint TileId,
    long Extent,
    ImmutableArray<PersistenceMatterEntry> MatterEntries,
    ImmutableArray<PersistenceEnergyEntry> EnergyEntries);

public sealed record WorldPersistenceState(
    ulong NextGenomeId,
    ulong NextSpeciesId,
    ulong NextOrganismId,
    ulong NextRemnantId,
    ulong NextJourneyEventId,
    ImmutableArray<PersistenceTileResource> TileResources,
    ImmutableArray<PersistenceGasTileRemainder> GasTileRemainders,
    ImmutableArray<PersistenceGasEdgeRemainder> GasEdgeRemainders,
    ImmutableArray<PersistenceGenome> Genomes,
    ImmutableArray<PersistenceSpecies> Species,
    ImmutableArray<PersistenceSpeciationEvent> SpeciationEvents,
    ImmutableArray<PersistenceOrganism> Organisms,
    ImmutableArray<PersistenceRemnant> Remnants,
    ImmutableArray<PersistenceOrganismJourneyEvent> JourneyEvents,
    ImmutableArray<PersistenceOrganismRoutineActivitySummary> RoutineActivitySummaries,
    ImmutableArray<PersistenceResourceFlowHistoryInterval> ResourceFlowHistory,
    ImmutableArray<PersistenceResourceTransaction> LastCompletedTransactions,
    PersistenceGameState Gameplay,
    ImmutableArray<PersistenceLineageReviewSchedule> LineageReviewSchedules = default,
    ImmutableArray<PersistenceLineageReviewLandmark> LineageReviewLandmarks = default,
    ImmutableArray<PersistenceNotableEvent> NotableEvents = default,
    ImmutableArray<PersistenceAttentionAlert> AttentionAlerts = default,
    ImmutableArray<PersistencePopulationAttentionState> PopulationAttentionStates = default,
    ImmutableArray<PersistenceAttentionWindowState> AttentionWindowStates = default);

public sealed record WorldPersistenceSnapshot(
    WorldPersistenceMetadata Metadata,
    WorldPersistenceState State);

internal static class PersistenceEvolutionConversion
{
    public static SpeciesEvolutionAccount ToEvolutionAccount(this PersistenceSpecies value) =>
        new(
            (EvolutionAuthorityKind)value.Authority,
            value.MutationBalanceQ,
            ((UInt128)value.MutationIncomeRemainderHigh << 64) | value.MutationIncomeRemainderLow,
            value.SpeciationNotBeforeTick,
            value.SpeciationOrdinal,
            value.EvolutionRevision,
            value.AverageHealthQ,
            value.LastIncomeQ,
            new EvolutionPressureState(value.EnergyShortagePressureQ, value.StarvationPressureQ));

    public static SpeciesLineageState ToLineageState(this PersistenceSpecies value) =>
        new(
            value.ParentSpeciesId == 0 ? null : SpeciesId.FromAllocatedValue(value.ParentSpeciesId),
            value.FoundingEventId,
            value.CreatedTick,
            value.ExtinctTick,
            value.FounderCounts.Select(count => new FounderCount(
                TileId.FromRowMajorIndex(count.TileId), count.Count)).ToImmutableArray(),
            value.AcquiredTraitDelta.Select(TraitId.From).ToImmutableArray());

    public static SpeciationEventRecord ToRecord(this PersistenceSpeciationEvent value) =>
        new(
            value.EventId,
            value.Tick,
            SpeciesId.FromAllocatedValue(value.AncestorSpeciesId),
            SpeciesId.FromAllocatedValue(value.DescendantSpeciesId),
            (SpeciationActorKind)value.ActorKind,
            value.FounderCounts.Select(count => new FounderCount(
                TileId.FromRowMajorIndex(count.TileId), count.Count)).ToImmutableArray(),
            value.FounderSelectionDigest,
            value.TraitDelta.Select(TraitId.From).ToImmutableArray(),
            value.MutationPriceQ,
            value.BalanceBeforeQ,
            value.DuplicatedBalanceAfterQ,
            value.ChangeComplexity);
}

internal static class PersistenceGameplayConversion
{
    public static GameStateSnapshot ToGameState(this PersistenceGameState value) =>
        new(
            (GameMode)value.Mode,
            (GameRunStatus)value.RunStatus,
            (GameLossReason)value.LossReason,
            SpeciesId.FromAllocatedValue(value.ControlledSpeciesId),
            value.GameplayRevision,
            value.EndedTick,
            value.Roots.Select(root => new AbiogenesisRoot(
                SpeciesId.FromAllocatedValue(root.SpeciesId),
                FounderGenomeId.From(root.FounderGenomeId),
                FounderAllocationId.From(root.FounderAllocationId),
                TileId.FromRowMajorIndex(root.StartingTileId),
                root.InitialPopulation,
                root.PlayerSelected)).ToImmutableArray(),
            value.MutationLockedSpeciesIds
                .Select(SpeciesId.FromAllocatedValue)
                .ToImmutableArray());
}

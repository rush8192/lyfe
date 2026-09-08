using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.Ticks;

namespace Lyfe.Server.Projection;

public enum TileVisibility : byte
{
    Unknown = 1,
    Reduced = 2,
    Live = 3,
}

public enum SpeciesPopulationScope : byte
{
    WorldExact = 1,
    LiveTilesObserved = 2,
}

public sealed record DiscoveredTileKnowledge(
    TileId TileId,
    ulong ObservedAtTick,
    ImmutableArray<ResourceId> KnownPresentResourceIds);

public sealed record ActorKnowledgeSnapshot(
    SpeciesId ControlledSpeciesId,
    ImmutableArray<DiscoveredTileKnowledge> DiscoveredTiles);

public readonly record struct ExactResourceStockProjection(
    ResourceId ResourceId,
    long QuantityQ);

public readonly record struct ResourceDefinitionProjection(
    ResourceId ResourceId,
    string StableKey,
    string DisplayName,
    BiologicalForm BiologicalForm,
    EnvironmentalPhase EnvironmentalPhase);

public readonly record struct ResourceFlowProjection(
    ResourceId ResourceId,
    PublicationResourceFlowKind Kind,
    long AmountQ);

public sealed record ResourceFlowHistoryIntervalProjection(
    ulong CompletedTick,
    ulong EndSimulatedHour,
    uint PeriodHours,
    ImmutableArray<ResourceFlowProjection> ResourceFlows);

public readonly record struct ResourceAcquisitionEvidenceProjection(
    ResourceId ResourceId,
    long RequestedQ,
    long GrantedQ,
    bool TileSupplyConstrained,
    bool ClaimContentionConstrained);

public readonly record struct AcquisitionGateEvidenceProjection(
    PublicationAcquisitionProcessKind Process,
    PublicationAcquisitionGateReason Reason,
    long AvailableQ,
    long RequiredQ,
    ulong ClearsAtTick);

public readonly record struct OrganismActionGateEvidenceProjection(
    PublicationOrganismActionProcessKind Process,
    PublicationOrganismActionGateReason Reason,
    long AvailableQ,
    long RequiredQ,
    ResourceId? ResourceId,
    ulong ClearsAtTick);

public sealed record OrganismProjection(
    OrganismId OrganismId,
    SpeciesId SpeciesId,
    uint PositionXQ,
    uint PositionYQ,
    uint BodyRadiusQ,
    long VelocityXQPerHour,
    long VelocityYQPerHour,
    ulong BirthTick,
    ulong BiologicalAgeHours,
    PublicationLifecyclePhase LifecyclePhase,
    ulong ReproductionNotBeforeTick,
    ulong SuccessfulReproductionCount,
    ulong ScavengeNotBeforeTick,
    long IngestedStructuralMatterQ,
    long StructuralMatterQ,
    long ChargedReserveQ,
    long ChargedReserveCapacityQ,
    uint RelativeHealthQ,
    uint ReserveFactorQ,
    uint StructureFactorQ,
    uint AgeFactorQ,
    uint EnvironmentalFactorQ,
    OrganismBehaviorId BehaviorId,
    BehaviorTargetKind BehaviorTargetKind,
    ulong BehaviorTargetId,
    uint BehaviorTargetPositionXQ,
    uint BehaviorTargetPositionYQ,
    ulong BehaviorSelectedAtTick,
    ulong BehaviorMinimumDwellUntilTick,
    uint RecentEnergyCoverageQ,
    uint RecentAcquisitionCoverageQ,
    uint LimitingMaterialDeficitQ,
    uint ResourcePressureQ,
    ImmutableArray<ExactResourceStockProjection> CommittedMicronutrients,
    ImmutableArray<ExactResourceStockProjection> FreeMicronutrients,
    ImmutableArray<ResourceAcquisitionEvidenceProjection> ResourceAcquisitionEvidence = default,
    ImmutableArray<AcquisitionGateEvidenceProjection> AcquisitionGateEvidence = default,
    ImmutableArray<OrganismActionGateEvidenceProjection> ActionGateEvidence = default);

public readonly record struct BehaviorCountProjection(
    OrganismBehaviorId BehaviorId,
    ulong Count);

public sealed record BehaviorDistributionProjection(
    SpeciesId SpeciesId,
    ulong ObservedAtTick,
    ulong TotalObservedOrganisms,
    ImmutableArray<BehaviorCountProjection> Counts);

public sealed record RemnantProjection(
    RemnantId RemnantId,
    OrganismId SourceOrganismId,
    SpeciesId SourceSpeciesId,
    uint PositionXQ,
    uint PositionYQ,
    uint BodyRadiusQ,
    ulong CreatedTick,
    long StructuralMatterQ,
    long ChargedReserveQ,
    ImmutableArray<ExactResourceStockProjection> Micronutrients);

public readonly record struct JourneyDeathCauseProjection(
    IntrinsicDeathCause Cause,
    uint ProbabilityQ,
    bool Triggered);

public sealed record OrganismJourneyEventProjection(
    ulong EventId,
    ulong Tick,
    TickPhase Phase,
    OrganismJourneyEventFamily Family,
    OrganismId SubjectOrganismId,
    SpeciesId SubjectSpeciesId,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    OrganismId? RelatedOrganismId,
    RemnantId? RelatedRemnantId,
    ResourceId? ResourceId,
    long AmountQ,
    uint DetailId,
    ImmutableArray<JourneyDeathCauseProjection> DeathCauseProbabilities);

public readonly record struct RoutineResourceAcquisitionProjection(
    ResourceId ResourceId,
    long AmountQ);

public sealed record OrganismRoutineActivitySummaryProjection(
    ulong BucketStartHour,
    uint PeriodHours,
    OrganismId SubjectOrganismId,
    SpeciesId SubjectSpeciesId,
    TileId TileId,
    ImmutableArray<RoutineResourceAcquisitionProjection> ResourceAcquisitions);

public abstract record TileProjection(
    TileId TileId,
    int X,
    int Y,
    TileVisibility Visibility);

public sealed record UnknownTileProjection(
    TileId TileId,
    int X,
    int Y) : TileProjection(TileId, X, Y, TileVisibility.Unknown);

public sealed record ReducedTileProjection(
    TileId TileId,
    int X,
    int Y,
    int ElevationMeters,
    ulong ObservedAtTick,
    ImmutableArray<ResourceId> KnownPresentResourceIds) :
    TileProjection(TileId, X, Y, TileVisibility.Reduced);

public sealed record LiveTileProjection(
    TileId TileId,
    int X,
    int Y,
    int ElevationMeters,
    ulong ObservedAtTick,
    ImmutableArray<ExactResourceStockProjection> ResourceStocks,
    ImmutableArray<OrganismProjection> Organisms,
    ImmutableArray<RemnantProjection> Remnants,
    ImmutableArray<BehaviorDistributionProjection> BehaviorDistributions,
    uint ResourceFlowPeriodHours = 0,
    ImmutableArray<ResourceFlowProjection> ResourceFlows = default,
    ImmutableArray<ResourceFlowHistoryIntervalProjection> ResourceFlowHistory = default) :
    TileProjection(TileId, X, Y, TileVisibility.Live);

public sealed record SpeciesProjection(
    SpeciesId SpeciesId,
    SpeciesPopulationScope PopulationScope,
    ulong Population,
    ImmutableArray<BehaviorCountProjection> BehaviorCounts,
    SpeciesEvolutionProjection? Evolution = null);

public sealed record SpeciesEvolutionProjection(
    GenomeId GenomeId,
    FounderGenomeId FounderGenomeId,
    FounderAllocationId FounderAllocationId,
    string GenomeHash,
    ImmutableArray<TraitId> AcquiredTraits,
    long MutationBalanceQ,
    ulong EvolutionRevision,
    ulong SpeciationNotBeforeTick,
    uint AverageHealthQ,
    long LastMutationIncomeQ,
    uint MutationIncomeModifierQ,
    SpeciesId? ParentSpeciesId,
    ulong CreatedTick,
    ulong? ExtinctTick);

public sealed record AbiogenesisRootProjection(
    SpeciesId SpeciesId,
    FounderGenomeId FounderGenomeId,
    FounderAllocationId FounderAllocationId,
    TileId StartingTileId,
    uint InitialPopulation,
    bool PlayerSelected);

public sealed record GameProjection(
    GameMode Mode,
    GameRunStatus RunStatus,
    GameLossReason LossReason,
    SpeciesId ControlledSpeciesId,
    ulong GameplayRevision,
    ulong? EndedTick,
    ImmutableArray<AbiogenesisRootProjection> Roots,
    ImmutableArray<SpeciesId> MutationLockedSpeciesIds);

public sealed record ActorWorldProjection(
    WorldId WorldId,
    ulong CompletedTick,
    ulong WorldRevision,
    ulong SimulatedHours,
    uint TickDurationHours,
    PublicationWorldLifecycle Lifecycle,
    string WorldRulesHash,
    uint Width,
    uint Height,
    bool WrapX,
    bool WrapY,
    SpeciesId ControlledSpeciesId,
    GameProjection Gameplay,
    ImmutableArray<TileProjection> Tiles,
    ImmutableArray<SpeciesProjection> Species,
    ImmutableArray<OrganismJourneyEventProjection> JourneyEvents,
    ImmutableArray<OrganismRoutineActivitySummaryProjection> RoutineActivitySummaries,
    ImmutableArray<OrganismJourneyEventProjection> ActivityPulseEvents,
    ImmutableArray<ResourceDefinitionProjection> ResourceDefinitions = default);

using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Publication;

public enum PublicationWorldLifecycle : byte
{
    PausedReady = 1,
    Running = 2,
}

public enum PublicationLifecyclePhase : byte
{
    Mature = 1,
}

public readonly record struct PublicationResourceStock(
    ResourceId ResourceId,
    long QuantityQ);

public readonly record struct PublicationResourceAcquisitionEvidence(
    ResourceId ResourceId,
    long RequestedQ,
    long GrantedQ,
    bool TileSupplyConstrained,
    bool ClaimContentionConstrained);

public enum PublicationAcquisitionProcessKind : byte
{
    ExternalEnergyCapture = 1,
    Scavenging = 2,
}

public enum PublicationAcquisitionGateReason : byte
{
    MissingCapability = 1,
    InaccessibleLight = 2,
    EnvironmentalOpportunity = 3,
    InternalCapacity = 4,
    CooldownActive = 5,
    InsufficientActionEnergy = 6,
}

public readonly record struct PublicationAcquisitionGateEvidence(
    PublicationAcquisitionProcessKind Process,
    PublicationAcquisitionGateReason Reason,
    long AvailableQ,
    long RequiredQ,
    ulong ClearsAtTick);

public enum PublicationOrganismActionProcessKind : byte
{
    BiomassGrowth = 1,
    Reproduction = 2,
}

public enum PublicationOrganismActionGateReason : byte
{
    MissingCapability = 1,
    BehaviorSuppressed = 2,
    CooldownActive = 3,
    HealthBelowMinimum = 4,
    StructureBelowMinimum = 5,
    ReserveBelowMinimum = 6,
    ConstitutiveMicronutrientQuotaMissing = 7,
    OffspringMicronutrientQuotaMissing = 8,
    MaintenanceShortfall = 9,
    ReserveProtectionFloor = 10,
    InternalCapacity = 11,
    ResourceSupply = 12,
    ClaimContention = 13,
    LifecycleIneligible = 14,
}

public readonly record struct PublicationOrganismActionGateEvidence(
    PublicationOrganismActionProcessKind Process,
    PublicationOrganismActionGateReason Reason,
    long AvailableQ,
    long RequiredQ,
    ResourceId? ResourceId,
    ulong ClearsAtTick);

public readonly record struct PublicationResourceDefinition(
    ResourceId ResourceId,
    string StableKey,
    string DisplayName,
    BiologicalForm BiologicalForm,
    EnvironmentalPhase EnvironmentalPhase);

public readonly record struct PublicationReactionDefinition(
    ReactionId ReactionId,
    string StableKey,
    string DisplayName);

public enum PublicationResourceFlowKind : byte
{
    EnvironmentalSource = 1,
    EnvironmentalSink = 2,
    NeighborExchangeIn = 3,
    NeighborExchangeOut = 4,
    OrganismUptake = 5,
    OrganismRelease = 6,
}

public readonly record struct PublicationTileResourceFlow(
    TileId TileId,
    ResourceId ResourceId,
    PublicationResourceFlowKind Kind,
    long AmountQ);

public enum PublicationResourceFlowProcessKind : byte
{
    ExternalEnergyCapture = 1,
    ParticulateDigestion = 2,
    EnvironmentalGasSource = 3,
    EnvironmentalGasSink = 4,
    EnvironmentalGasExchange = 5,
    MandatoryMaintenance = 6,
    BiomassAssembly = 7,
    MicronutrientUptake = 8,
}

public readonly record struct PublicationTileResourceFlowContributor(
    TileId TileId,
    ResourceId ResourceId,
    PublicationResourceFlowKind Kind,
    PublicationResourceFlowProcessKind Process,
    ReactionId? ReactionId,
    SpeciesId? SpeciesId,
    long AmountQ);

public sealed record PublicationResourceFlowHistoryInterval(
    ulong CompletedTick,
    ulong EndSimulatedHour,
    uint PeriodHours,
    ImmutableArray<PublicationTileResourceFlow> ResourceFlows);

public sealed record PublicationTile(
    TileId TileId,
    int X,
    int Y,
    int ElevationMeters,
    uint BaselineVolcanismQ,
    ImmutableArray<PublicationResourceStock> ResourceStocks);

public sealed record PublicationSpecies(
    SpeciesId SpeciesId,
    GenomeId GenomeId,
    FounderGenomeId FounderGenomeId,
    FounderAllocationId FounderAllocationId,
    ulong Population,
    string GenomeHash,
    ImmutableArray<TraitId> AcquiredTraits,
    EvolutionAuthorityKind EvolutionAuthority,
    long MutationBalanceQ,
    ulong EvolutionRevision,
    ulong SpeciationNotBeforeTick,
    uint AverageHealthQ,
    long LastMutationIncomeQ,
    uint MutationIncomeModifierQ,
    SpeciesId? ParentSpeciesId,
    ulong CreatedTick,
    ulong? ExtinctTick);

public sealed record PublicationAbiogenesisRoot(
    SpeciesId SpeciesId,
    FounderGenomeId FounderGenomeId,
    FounderAllocationId FounderAllocationId,
    TileId StartingTileId,
    uint InitialPopulation,
    bool PlayerSelected);

public sealed record PublicationGameState(
    GameMode Mode,
    GameRunStatus RunStatus,
    GameLossReason LossReason,
    SpeciesId ControlledSpeciesId,
    ulong GameplayRevision,
    ulong? EndedTick,
    ImmutableArray<PublicationAbiogenesisRoot> Roots,
    ImmutableArray<SpeciesId> MutationLockedSpeciesIds);

public sealed record PublicationOrganism(
    OrganismId OrganismId,
    SpeciesId SpeciesId,
    TileId TileId,
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
    ImmutableArray<PublicationResourceStock> CommittedMicronutrients,
    ImmutableArray<PublicationResourceStock> FreeMicronutrients,
    ImmutableArray<PublicationResourceAcquisitionEvidence> ResourceAcquisitionEvidence = default,
    ImmutableArray<PublicationAcquisitionGateEvidence> AcquisitionGateEvidence = default,
    ImmutableArray<PublicationOrganismActionGateEvidence> ActionGateEvidence = default);

public sealed record PublicationRemnant(
    RemnantId RemnantId,
    OrganismId SourceOrganismId,
    SpeciesId SourceSpeciesId,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    uint BodyRadiusQ,
    ulong CreatedTick,
    long StructuralMatterQ,
    long ChargedReserveQ,
    ImmutableArray<PublicationResourceStock> Micronutrients);

public sealed record WorldPublicationSnapshot(
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
    PublicationGameState Gameplay,
    ImmutableArray<PublicationTile> Tiles,
    ImmutableArray<PublicationSpecies> Species,
    ImmutableArray<PublicationOrganism> Organisms,
    ImmutableArray<PublicationRemnant> Remnants,
    ImmutableArray<OrganismJourneyEvent> JourneyEvents,
    ImmutableArray<OrganismRoutineActivitySummary> RoutineActivitySummaries,
    ImmutableArray<OrganismJourneyEvent> ActivityPulseEvents,
    ImmutableArray<PublicationResourceDefinition> ResourceDefinitions = default,
    ImmutableArray<PublicationTileResourceFlow> ResourceFlows = default,
    uint ResourceFlowPeriodHours = 0,
    ImmutableArray<PublicationResourceFlowHistoryInterval> ResourceFlowHistory = default,
    ImmutableArray<PublicationReactionDefinition> ReactionDefinitions = default,
    ImmutableArray<PublicationTileResourceFlowContributor> ResourceFlowContributors = default,
    ImmutableArray<LineageReviewLandmark> LineageReviewLandmarks = default,
    ImmutableArray<NotableEvent> NotableEvents = default,
    ImmutableArray<AttentionAlert> AttentionAlerts = default);

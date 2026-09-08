using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Publication;

public enum PublicationWorldLifecycle : byte
{
    PausedReady = 1,
}

public enum PublicationLifecyclePhase : byte
{
    Mature = 1,
}

public readonly record struct PublicationResourceStock(
    ResourceId ResourceId,
    long QuantityQ);

public sealed record PublicationTile(
    TileId TileId,
    int X,
    int Y,
    int ElevationMeters,
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
    ImmutableArray<PublicationResourceStock> FreeMicronutrients);

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
    ImmutableArray<OrganismJourneyEvent> ActivityPulseEvents);

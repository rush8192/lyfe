using System.Collections.Immutable;

namespace Lyfe.Simulation.Publication;

public readonly record struct PersistenceTileResource(
    uint TileId,
    uint ResourceId,
    long QuantityQ);

public readonly record struct PersistenceGenome(
    ulong GenomeId,
    uint FounderGenomeId);

public readonly record struct PersistenceSpecies(
    ulong SpeciesId,
    ulong GenomeId,
    ulong Population);

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
    long StructuralMatterQ,
    long ChargedReserveQ);

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
    ImmutableArray<PersistenceTileResource> TileResources,
    ImmutableArray<PersistenceGenome> Genomes,
    ImmutableArray<PersistenceSpecies> Species,
    ImmutableArray<PersistenceOrganism> Organisms,
    ImmutableArray<PersistenceResourceTransaction> LastCompletedTransactions);

public sealed record WorldPersistenceSnapshot(
    WorldPersistenceMetadata Metadata,
    WorldPersistenceState State);

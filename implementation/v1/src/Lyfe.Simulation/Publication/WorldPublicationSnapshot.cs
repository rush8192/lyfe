using System.Collections.Immutable;
using Lyfe.Simulation.Core;
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
    ulong Population);

public sealed record PublicationOrganism(
    OrganismId OrganismId,
    SpeciesId SpeciesId,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    long VelocityXQPerHour,
    long VelocityYQPerHour,
    ulong BirthTick,
    ulong BiologicalAgeHours,
    PublicationLifecyclePhase LifecyclePhase,
    long StructuralMatterQ,
    long ChargedReserveQ);

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
    ImmutableArray<PublicationTile> Tiles,
    ImmutableArray<PublicationSpecies> Species,
    ImmutableArray<PublicationOrganism> Organisms);

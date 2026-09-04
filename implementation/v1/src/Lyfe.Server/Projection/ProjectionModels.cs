using System.Collections.Immutable;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;

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

public sealed record OrganismProjection(
    OrganismId OrganismId,
    SpeciesId SpeciesId,
    uint PositionXQ,
    uint PositionYQ,
    long VelocityXQPerHour,
    long VelocityYQPerHour,
    ulong BirthTick,
    ulong BiologicalAgeHours,
    PublicationLifecyclePhase LifecyclePhase,
    long StructuralMatterQ,
    long ChargedReserveQ);

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
    ImmutableArray<OrganismProjection> Organisms) :
    TileProjection(TileId, X, Y, TileVisibility.Live);

public sealed record SpeciesProjection(
    SpeciesId SpeciesId,
    SpeciesPopulationScope PopulationScope,
    ulong Population);

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
    ImmutableArray<TileProjection> Tiles,
    ImmutableArray<SpeciesProjection> Species);

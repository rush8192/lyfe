using Domain = Lyfe.Server.Projection;
using Proto = Lyfe.Protocol.V1;

namespace Lyfe.Server.Projection;

public static class ProjectionProtocolMapper
{
    public static Proto.ProjectionSnapshot ToProtocol(ProjectionStreamSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new Proto.ProjectionSnapshot
        {
            ProjectionStreamId = source.ProjectionStreamId,
            StreamRevision = source.StreamRevision,
            Projection = ToProtocol(source.Projection),
        };
    }

    public static Proto.ProjectionBatch ToProtocol(ProjectionDelta source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var boundary = source.TargetBoundary;
        var result = new Proto.ProjectionBatch
        {
            ProjectionStreamId = source.ProjectionStreamId,
            WorldId = boundary.WorldId.Value,
            WorldRulesHash = boundary.WorldRulesHash,
            BaseStreamRevision = source.BaseStreamRevision,
            TargetStreamRevision = source.TargetStreamRevision,
            FromExclusiveTick = source.FromExclusiveTick,
            ThroughCompletedTick = boundary.CompletedTick,
            WorldRevision = boundary.WorldRevision,
            SimulatedHours = boundary.SimulatedHours,
            Lifecycle = boundary.Lifecycle switch
            {
                Simulation.Publication.PublicationWorldLifecycle.PausedReady =>
                    Proto.WorldLifecycle.PausedReady,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported world lifecycle."),
            },
        };
        result.TileReplacements.Add(source.TileReplacements.Select(ToProtocol));
        result.RemovedTileIds.Add(source.RemovedTileIds.Select(id => id.Value));
        result.SpeciesReplacements.Add(source.SpeciesReplacements.Select(ToProtocol));
        result.RemovedSpeciesIds.Add(source.RemovedSpeciesIds.Select(id => id.Value));
        return result;
    }

    public static Proto.ActorWorldProjection ToProtocol(Domain.ActorWorldProjection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new Proto.ActorWorldProjection
        {
            WorldId = source.WorldId.Value,
            CompletedTick = source.CompletedTick,
            WorldRevision = source.WorldRevision,
            SimulatedHours = source.SimulatedHours,
            TickDurationHours = source.TickDurationHours,
            Lifecycle = source.Lifecycle switch
            {
                Simulation.Publication.PublicationWorldLifecycle.PausedReady =>
                    Proto.WorldLifecycle.PausedReady,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported world lifecycle."),
            },
            WorldRulesHash = source.WorldRulesHash,
            Width = source.Width,
            Height = source.Height,
            WrapX = source.WrapX,
            WrapY = source.WrapY,
            ControlledSpeciesId = source.ControlledSpeciesId.Value,
        };
        result.Tiles.Add(source.Tiles.Select(ToProtocol));
        result.Species.Add(source.Species.Select(ToProtocol));
        return result;
    }

    private static Proto.TileProjection ToProtocol(Domain.TileProjection source)
    {
        var result = new Proto.TileProjection
        {
            TileId = source.TileId.Value,
            X = source.X,
            Y = source.Y,
        };
        switch (source)
        {
            case Domain.UnknownTileProjection:
                result.Unknown = new Proto.UnknownTile();
                break;
            case Domain.ReducedTileProjection reduced:
                result.Reduced = new Proto.ReducedTile
                {
                    ElevationMeters = reduced.ElevationMeters,
                    ObservedAtTick = reduced.ObservedAtTick,
                };
                result.Reduced.KnownPresentResourceIds.Add(
                    reduced.KnownPresentResourceIds.Select(id => id.Value));
                break;
            case Domain.LiveTileProjection live:
                result.Live = new Proto.LiveTile
                {
                    ElevationMeters = live.ElevationMeters,
                    ObservedAtTick = live.ObservedAtTick,
                };
                result.Live.ResourceStocks.Add(live.ResourceStocks.Select(stock =>
                    new Proto.ExactResourceStock
                    {
                        ResourceId = stock.ResourceId.Value,
                        QuantityQ = stock.QuantityQ,
                    }));
                result.Live.Organisms.Add(live.Organisms.Select(ToProtocol));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), "Unsupported tile projection.");
        }

        return result;
    }

    private static Proto.OrganismProjection ToProtocol(Domain.OrganismProjection source) =>
        new()
        {
            OrganismId = source.OrganismId.Value,
            SpeciesId = source.SpeciesId.Value,
            PositionXQ = source.PositionXQ,
            PositionYQ = source.PositionYQ,
            VelocityXQPerHour = source.VelocityXQPerHour,
            VelocityYQPerHour = source.VelocityYQPerHour,
            BirthTick = source.BirthTick,
            BiologicalAgeHours = source.BiologicalAgeHours,
            LifecyclePhase = source.LifecyclePhase switch
            {
                Simulation.Publication.PublicationLifecyclePhase.Mature =>
                    Proto.OrganismLifecyclePhase.Mature,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported organism lifecycle."),
            },
            StructuralMatterQ = source.StructuralMatterQ,
            ChargedReserveQ = source.ChargedReserveQ,
        };

    private static Proto.SpeciesProjection ToProtocol(Domain.SpeciesProjection source) =>
        new()
        {
            SpeciesId = source.SpeciesId.Value,
            PopulationScope = source.PopulationScope switch
            {
                Domain.SpeciesPopulationScope.WorldExact =>
                    Proto.SpeciesPopulationScope.WorldExact,
                Domain.SpeciesPopulationScope.LiveTilesObserved =>
                    Proto.SpeciesPopulationScope.LiveTilesObserved,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported population scope."),
            },
            Population = source.Population,
        };
}

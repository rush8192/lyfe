using System.Collections.Immutable;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Server.Projection;

public static class DirectWorldProjector
{
    public static ActorWorldProjection Project(
        WorldPublicationSnapshot source,
        ActorKnowledgeSnapshot knowledge)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(knowledge);

        var sourceIndex = ValidateAndIndex(source);
        var discovered = ValidateAndIndex(knowledge, source, sourceIndex);
        var liveTileIds = source.Organisms
            .Where(organism => organism.SpeciesId == knowledge.ControlledSpeciesId)
            .Select(organism => organism.TileId)
            .ToHashSet();
        var organismsByTile = source.Organisms
            .GroupBy(organism => organism.TileId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(organism => organism.OrganismId.Value)
                    .ToImmutableArray());
        var tiles = source.Tiles
            .OrderBy(tile => tile.TileId.Value)
            .Select(tile => ProjectTile(
                tile,
                source.CompletedTick,
                liveTileIds,
                discovered,
                organismsByTile))
            .ToImmutableArray();
        var species = ProjectSpecies(
            source,
            knowledge.ControlledSpeciesId,
            liveTileIds,
            sourceIndex.Species);

        return new ActorWorldProjection(
            source.WorldId,
            source.CompletedTick,
            source.WorldRevision,
            source.SimulatedHours,
            source.TickDurationHours,
            source.Lifecycle,
            source.WorldRulesHash,
            source.Width,
            source.Height,
            source.WrapX,
            source.WrapY,
            knowledge.ControlledSpeciesId,
            tiles,
            species);
    }

    private static TileProjection ProjectTile(
        PublicationTile tile,
        ulong completedTick,
        HashSet<TileId> liveTileIds,
        Dictionary<TileId, DiscoveredTileKnowledge> discovered,
        Dictionary<TileId, ImmutableArray<PublicationOrganism>> organismsByTile)
    {
        if (liveTileIds.Contains(tile.TileId))
        {
            var organisms = organismsByTile[tile.TileId];
            return new LiveTileProjection(
                tile.TileId,
                tile.X,
                tile.Y,
                tile.ElevationMeters,
                completedTick,
                tile.ResourceStocks
                    .OrderBy(stock => stock.ResourceId.Value)
                    .Select(stock => new ExactResourceStockProjection(
                        stock.ResourceId,
                        stock.QuantityQ))
                    .ToImmutableArray(),
                organisms.Select(ToProjection).ToImmutableArray());
        }

        if (discovered.TryGetValue(tile.TileId, out var memory))
        {
            return new ReducedTileProjection(
                tile.TileId,
                tile.X,
                tile.Y,
                tile.ElevationMeters,
                memory.ObservedAtTick,
                memory.KnownPresentResourceIds
                    .OrderBy(id => id.Value)
                    .ToImmutableArray());
        }

        return new UnknownTileProjection(tile.TileId, tile.X, tile.Y);
    }

    private static ImmutableArray<SpeciesProjection> ProjectSpecies(
        WorldPublicationSnapshot source,
        SpeciesId controlledSpeciesId,
        HashSet<TileId> liveTileIds,
        Dictionary<SpeciesId, PublicationSpecies> speciesById)
    {
        var observedPopulation = source.Organisms
            .Where(organism => liveTileIds.Contains(organism.TileId))
            .GroupBy(organism => organism.SpeciesId)
            .ToDictionary(
                group => group.Key,
                group => checked((ulong)group.LongCount()));
        var visibleSpecies = observedPopulation.Keys
            .Append(controlledSpeciesId)
            .Distinct()
            .OrderBy(id => id.Value);
        var result = ImmutableArray.CreateBuilder<SpeciesProjection>();
        foreach (var speciesId in visibleSpecies)
        {
            if (speciesId == controlledSpeciesId)
            {
                result.Add(new SpeciesProjection(
                    speciesId,
                    SpeciesPopulationScope.WorldExact,
                    speciesById[speciesId].Population));
            }
            else
            {
                result.Add(new SpeciesProjection(
                    speciesId,
                    SpeciesPopulationScope.LiveTilesObserved,
                    observedPopulation[speciesId]));
            }
        }

        return result.ToImmutable();
    }

    private static OrganismProjection ToProjection(PublicationOrganism organism) =>
        new(
            organism.OrganismId,
            organism.SpeciesId,
            organism.PositionXQ,
            organism.PositionYQ,
            organism.VelocityXQPerHour,
            organism.VelocityYQPerHour,
            organism.BirthTick,
            organism.BiologicalAgeHours,
            organism.LifecyclePhase,
            organism.StructuralMatterQ,
            organism.ChargedReserveQ);

    private static SourceIndex ValidateAndIndex(WorldPublicationSnapshot source)
    {
        if (!source.WorldId.IsValid ||
            source.TickDurationHours == 0 ||
            source.Width == 0 ||
            source.Height == 0 ||
            source.WrapY ||
            string.IsNullOrWhiteSpace(source.WorldRulesHash) ||
            source.Tiles.IsDefault ||
            source.Species.IsDefault ||
            source.Organisms.IsDefault)
        {
            throw new ArgumentException("The publication source is incomplete or invalid.", nameof(source));
        }

        var tileCount = checked(source.Width * source.Height);
        if (tileCount != (uint)source.Tiles.Length)
        {
            throw new ArgumentException(
                "The publication tile count must match the declared grid.",
                nameof(source));
        }

        var tiles = UniqueIndex(
            source.Tiles,
            tile => tile.TileId,
            "tile",
            nameof(source));
        var species = UniqueIndex(
            source.Species,
            item => item.SpeciesId,
            "species",
            nameof(source));
        var resourceIds = new HashSet<ResourceId>();
        foreach (var tile in source.Tiles)
        {
            if (tile.ResourceStocks.IsDefault)
            {
                throw new ArgumentException("A tile has an uninitialized resource vector.", nameof(source));
            }

            var tileResources = new HashSet<ResourceId>();
            foreach (var stock in tile.ResourceStocks)
            {
                if (stock.ResourceId == default ||
                    stock.QuantityQ < 0 ||
                    !tileResources.Add(stock.ResourceId))
                {
                    throw new ArgumentException(
                        "A tile has an invalid or duplicate resource stock.",
                        nameof(source));
                }

                resourceIds.Add(stock.ResourceId);
            }
        }

        var actualPopulations = new Dictionary<SpeciesId, ulong>();
        var organismIds = new HashSet<OrganismId>();
        foreach (var organism in source.Organisms)
        {
            if (!species.ContainsKey(organism.SpeciesId) ||
                !tiles.ContainsKey(organism.TileId) ||
                organism.OrganismId == default ||
                !organismIds.Add(organism.OrganismId) ||
                organism.LifecyclePhase == default ||
                organism.StructuralMatterQ < 0 ||
                organism.ChargedReserveQ < 0)
            {
                throw new ArgumentException(
                    "An organism has invalid state or a dangling reference.",
                    nameof(source));
            }

            actualPopulations.TryGetValue(organism.SpeciesId, out var count);
            actualPopulations[organism.SpeciesId] = checked(count + 1);
        }

        foreach (var item in source.Species)
        {
            if (item.SpeciesId == default || item.GenomeId == default)
            {
                throw new ArgumentException("A species has an invalid stable ID.", nameof(source));
            }

            actualPopulations.TryGetValue(item.SpeciesId, out var actual);
            if (actual != item.Population)
            {
                throw new ArgumentException(
                    "A species population does not match its organisms.",
                    nameof(source));
            }
        }

        return new SourceIndex(tiles, species, resourceIds);
    }

    private static Dictionary<TileId, DiscoveredTileKnowledge> ValidateAndIndex(
        ActorKnowledgeSnapshot knowledge,
        WorldPublicationSnapshot source,
        SourceIndex sourceIndex)
    {
        if (knowledge.ControlledSpeciesId == default ||
            !sourceIndex.Species.ContainsKey(knowledge.ControlledSpeciesId) ||
            knowledge.DiscoveredTiles.IsDefault)
        {
            throw new ArgumentException(
                "Actor knowledge must identify one existing controlled species.",
                nameof(knowledge));
        }

        var discovered = new Dictionary<TileId, DiscoveredTileKnowledge>();
        foreach (var tile in knowledge.DiscoveredTiles)
        {
            if (!sourceIndex.Tiles.ContainsKey(tile.TileId) ||
                tile.ObservedAtTick > source.CompletedTick ||
                tile.KnownPresentResourceIds.IsDefault ||
                !discovered.TryAdd(tile.TileId, tile))
            {
                throw new ArgumentException(
                    "Actor knowledge contains an invalid or duplicate discovered tile.",
                    nameof(knowledge));
            }

            var knownResources = new HashSet<ResourceId>();
            foreach (var resourceId in tile.KnownPresentResourceIds)
            {
                if (!sourceIndex.ResourceIds.Contains(resourceId) ||
                    !knownResources.Add(resourceId))
                {
                    throw new ArgumentException(
                        "Actor knowledge contains an invalid or duplicate resource ID.",
                        nameof(knowledge));
                }
            }
        }

        return discovered;
    }

    private static Dictionary<TKey, TItem> UniqueIndex<TKey, TItem>(
        IEnumerable<TItem> items,
        Func<TItem, TKey> keySelector,
        string label,
        string parameterName)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TItem>();
        foreach (var item in items)
        {
            if (!result.TryAdd(keySelector(item), item))
            {
                throw new ArgumentException(
                    $"The publication source contains a duplicate {label} ID.",
                    parameterName);
            }
        }

        return result;
    }

    private sealed record SourceIndex(
        Dictionary<TileId, PublicationTile> Tiles,
        Dictionary<SpeciesId, PublicationSpecies> Species,
        HashSet<ResourceId> ResourceIds);
}

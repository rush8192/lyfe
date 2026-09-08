using System.Collections.Immutable;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Spatial;

internal readonly record struct IndexedOrganism(
    OrganismId Id,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    uint BodyRadiusQ);

internal readonly record struct IndexedRemnant(
    RemnantId Id,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    uint BodyRadiusQ);

/// <summary>
/// A rebuildable acceleration structure. Entity state remains authoritative in
/// MutableWorldState; bins are deliberately absent from persistence and hashing.
/// </summary>
internal sealed class SpatialEntityIndex
{
    public const int BinsPerAxis = 16;
    private const int BinShift = 28;
    private readonly Dictionary<(TileId Tile, int Bin), List<IndexedOrganism>> organisms = [];
    private readonly Dictionary<(TileId Tile, int Bin), List<IndexedRemnant>> remnants = [];
    private readonly Dictionary<TileId, uint> maximumRemnantRadiusByTile = [];

    public SpatialEntityIndex(
        IEnumerable<IndexedOrganism> organismEntities,
        IEnumerable<IndexedRemnant> remnantEntities)
    {
        foreach (var entity in organismEntities
                     .OrderBy(entity => entity.TileId.Value)
                     .ThenBy(entity => Bin(entity.PositionXQ, entity.PositionYQ))
                     .ThenBy(entity => entity.Id.Value))
        {
            GetOrAdd(organisms, (entity.TileId, Bin(entity.PositionXQ, entity.PositionYQ)))
                .Add(entity);
        }

        foreach (var entity in remnantEntities
                     .OrderBy(entity => entity.TileId.Value)
                     .ThenBy(entity => Bin(entity.PositionXQ, entity.PositionYQ))
                     .ThenBy(entity => entity.Id.Value))
        {
            GetOrAdd(remnants, (entity.TileId, Bin(entity.PositionXQ, entity.PositionYQ)))
                .Add(entity);
            maximumRemnantRadiusByTile.TryGetValue(entity.TileId, out var maximum);
            maximumRemnantRadiusByTile[entity.TileId] = Math.Max(maximum, entity.BodyRadiusQ);
        }
    }

    public ImmutableArray<IndexedOrganism> QueryOrganisms(
        TileId tileId,
        uint positionXQ,
        uint positionYQ,
        uint centerRangeQ,
        OrganismId excludedId = default)
    {
        var result = ImmutableArray.CreateBuilder<IndexedOrganism>();
        VisitBins(tileId, positionXQ, positionYQ, centerRangeQ, (tile, bin) =>
        {
            if (!organisms.TryGetValue((tile, bin), out var values))
            {
                return;
            }

            foreach (var value in values)
            {
                if (value.Id != excludedId && SpatialMath.WithinRange(
                        positionXQ, positionYQ, value.PositionXQ, value.PositionYQ, centerRangeQ))
                {
                    result.Add(value);
                }
            }
        });
        return result.OrderBy(value => value.Id.Value).ToImmutableArray();
    }

    public ImmutableArray<IndexedRemnant> QueryRemnantsForFeeding(
        TileId tileId,
        uint positionXQ,
        uint positionYQ,
        uint feedingReachQ)
    {
        maximumRemnantRadiusByTile.TryGetValue(tileId, out var maximumRadius);
        var broadRange = Math.Min(uint.MaxValue, (ulong)feedingReachQ + maximumRadius);
        var result = ImmutableArray.CreateBuilder<IndexedRemnant>();
        VisitBins(tileId, positionXQ, positionYQ, broadRange, (tile, bin) =>
        {
            if (!remnants.TryGetValue((tile, bin), out var values))
            {
                return;
            }

            foreach (var value in values)
            {
                var exactRange = (ulong)feedingReachQ + value.BodyRadiusQ;
                if (SpatialMath.WithinRange(
                        positionXQ, positionYQ, value.PositionXQ, value.PositionYQ, exactRange))
                {
                    result.Add(value);
                }
            }
        });
        return result.OrderBy(value => value.Id.Value).ToImmutableArray();
    }

    internal static int Bin(uint x, uint y) =>
        checked(((int)(y >> BinShift) * BinsPerAxis) + (int)(x >> BinShift));

    private static void VisitBins(
        TileId tileId,
        uint x,
        uint y,
        ulong range,
        Action<TileId, int> visitor)
    {
        var minimumX = x > range ? x - range : 0;
        var maximumX = Math.Min(uint.MaxValue, (ulong)x + range);
        var minimumY = y > range ? y - range : 0;
        var maximumY = Math.Min(uint.MaxValue, (ulong)y + range);
        for (var binY = (int)(minimumY >> BinShift);
             binY <= (int)(maximumY >> BinShift);
             binY++)
        {
            for (var binX = (int)(minimumX >> BinShift);
                 binX <= (int)(maximumX >> BinShift);
                 binX++)
            {
                visitor(tileId, checked(binY * BinsPerAxis + binX));
            }
        }
    }

    private static List<T> GetOrAdd<T>(Dictionary<(TileId Tile, int Bin), List<T>> values,
        (TileId Tile, int Bin) key)
    {
        if (!values.TryGetValue(key, out var bucket))
        {
            bucket = [];
            values.Add(key, bucket);
        }

        return bucket;
    }
}

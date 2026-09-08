using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World.Generation;

namespace Lyfe.Simulation.State.Storage;

internal readonly record struct TileSnapshot(
    TileId Id,
    int X,
    int Y,
    int ElevationMeters);

internal readonly record struct GasEdge(TileId LowerTileId, TileId HigherTileId);

internal sealed class TileStore
{
    private readonly int[] x;
    private readonly int[] y;
    private readonly int[] elevationMeters;

    public TileStore(CompiledWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        x = new int[profile.Tiles.Length];
        y = new int[profile.Tiles.Length];
        elevationMeters = new int[profile.Tiles.Length];
        foreach (var tile in profile.Tiles)
        {
            if (tile.TileIndex >= (uint)profile.Tiles.Length)
            {
                throw new ArgumentException("Compiled tile indices must be contiguous.", nameof(profile));
            }

            var index = checked((int)tile.TileIndex);
            x[index] = tile.X;
            y[index] = tile.Y;
            elevationMeters[index] = tile.ElevationMeters;
        }
    }

    public int Count => x.Length;

    public bool Contains(TileId id) => id.Value < (uint)x.Length;

    public TileSnapshot Get(TileId id)
    {
        var index = GetIndex(id);
        return new TileSnapshot(id, x[index], y[index], elevationMeters[index]);
    }

    private int GetIndex(TileId id)
    {
        if (!Contains(id))
        {
            throw new KeyNotFoundException($"Tile {id} does not exist.");
        }

        return checked((int)id.Value);
    }
}

internal sealed class TileResourceStore
{
    private readonly ResourceId[] resourceIdsBySlot;
    private readonly long[] quantitiesByResourceThenTile;
    private readonly int tileCount;
    private readonly ResourceId[] gasResourceIdsBySlot;
    private readonly long[] gasSourceRemainders;
    private readonly long[] gasSinkRemainders;
    private readonly long[] gasExchangeRemainders;
    private readonly ImmutableArray<GasEdge> gasEdges;

    public TileResourceStore(CompiledRulePack rulePack, CompiledWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(rulePack);
        ArgumentNullException.ThrowIfNull(profile);

        tileCount = profile.Tiles.Length;
        resourceIdsBySlot = rulePack.Resources.Select(resource => resource.Id).ToArray();
        quantitiesByResourceThenTile = new long[checked(resourceIdsBySlot.Length * tileCount)];
        gasResourceIdsBySlot = profile.GasEnvironment.Gases.Select(gas => gas.Resource.Id).ToArray();
        gasEdges = BuildGasEdges(profile);
        gasSourceRemainders = new long[checked(gasResourceIdsBySlot.Length * tileCount)];
        gasSinkRemainders = new long[checked(gasResourceIdsBySlot.Length * tileCount)];
        gasExchangeRemainders = new long[checked(gasResourceIdsBySlot.Length * gasEdges.Length)];
        foreach (var tile in profile.Tiles)
        {
            if (tile.ResourceQuantitiesByDenseSlot.Length != resourceIdsBySlot.Length)
            {
                throw new ArgumentException(
                    "Compiled tile resource vectors must match the rule-pack resource manifest.",
                    nameof(profile));
            }

            for (var resourceSlot = 0; resourceSlot < resourceIdsBySlot.Length; resourceSlot++)
            {
                quantitiesByResourceThenTile[GetOffset(resourceSlot, tile.TileIndex)] =
                    tile.ResourceQuantitiesByDenseSlot[resourceSlot];
            }
        }
    }

    public ulong MutationEpoch { get; private set; }

    public ImmutableArray<GasEdge> GasEdges => gasEdges;

    public long Get(TileId tileId, ResourceHandle resource)
    {
        ValidateResource(resource);
        return quantitiesByResourceThenTile[GetOffset(resource.DenseSlot, tileId.Value)];
    }

    public void Restore(
        ImmutableArray<PersistenceTileResource> resources,
        ImmutableArray<PersistenceGasTileRemainder> tileRemainders,
        ImmutableArray<PersistenceGasEdgeRemainder> edgeRemainders)
    {
        if (resources.IsDefault ||
            resources.Length != checked(resourceIdsBySlot.Length * tileCount) ||
            tileRemainders.IsDefault ||
            edgeRemainders.IsDefault ||
            tileRemainders.Length != checked(gasResourceIdsBySlot.Length * tileCount) ||
            edgeRemainders.Length != checked(gasResourceIdsBySlot.Length * gasEdges.Length) ||
            MutationEpoch != 0)
        {
            throw new ArgumentException(
                "Persisted tile resources do not match this empty compiled store.",
                nameof(resources));
        }

        var inputIndex = 0;
        var canonicalResources = resourceIdsBySlot
            .Select((id, denseSlot) => (Id: id, DenseSlot: denseSlot))
            .OrderBy(resource => resource.Id.Value)
            .ToArray();
        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            foreach (var resource in canonicalResources)
            {
                var persisted = resources[inputIndex++];
                if (persisted.TileId != (uint)tileIndex ||
                    persisted.ResourceId != resource.Id.Value ||
                    persisted.QuantityQ < 0)
                {
                    throw new ArgumentException(
                        "Persisted tile resources are not canonical or compatible.",
                        nameof(resources));
                }

                quantitiesByResourceThenTile[GetOffset(resource.DenseSlot, (uint)tileIndex)] =
                    persisted.QuantityQ;
            }
        }


        inputIndex = 0;
        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            for (var gasSlot = 0; gasSlot < gasResourceIdsBySlot.Length; gasSlot++)
            {
                var persisted = tileRemainders[inputIndex++];
                if (persisted.TileId != (uint)tileIndex ||
                    persisted.ResourceId != gasResourceIdsBySlot[gasSlot].Value ||
                    persisted.SourceRemainderQ < 0 || persisted.SourceRemainderQ >= 1_000_000 ||
                    persisted.SinkRemainderQ < 0 || persisted.SinkRemainderQ >= 1_000_000)
                {
                    throw new ArgumentException("Persisted gas tile remainders are not canonical.", nameof(tileRemainders));
                }

                var offset = GetGasTileOffset(gasSlot, (uint)tileIndex);
                gasSourceRemainders[offset] = persisted.SourceRemainderQ;
                gasSinkRemainders[offset] = persisted.SinkRemainderQ;
            }
        }

        inputIndex = 0;
        for (var edgeSlot = 0; edgeSlot < gasEdges.Length; edgeSlot++)
        {
            for (var gasSlot = 0; gasSlot < gasResourceIdsBySlot.Length; gasSlot++)
            {
                var persisted = edgeRemainders[inputIndex++];
                var edge = gasEdges[edgeSlot];
                if (persisted.LowerTileId != edge.LowerTileId.Value ||
                    persisted.HigherTileId != edge.HigherTileId.Value ||
                    persisted.ResourceId != gasResourceIdsBySlot[gasSlot].Value ||
                    persisted.ExchangeRemainderQ <= -1_000_000_000_000L ||
                    persisted.ExchangeRemainderQ >= 1_000_000_000_000L)
                {
                    throw new ArgumentException("Persisted gas edge remainders are not canonical.", nameof(edgeRemainders));
                }

                gasExchangeRemainders[GetGasEdgeOffset(gasSlot, edgeSlot)] = persisted.ExchangeRemainderQ;
            }
        }
    }

    public (long SourceQ, long SinkQ) GetGasTileRemainders(int gasSlot, TileId tileId)
    {
        var offset = GetGasTileOffset(gasSlot, tileId.Value);
        return (gasSourceRemainders[offset], gasSinkRemainders[offset]);
    }

    public long GetGasExchangeRemainder(int gasSlot, int edgeSlot) =>
        gasExchangeRemainders[GetGasEdgeOffset(gasSlot, edgeSlot)];

    public void SetGasTileRemainders(
        int gasSlot, TileId tileId, long sourceQ, long sinkQ, PhaseChangeBuilder changes)
    {
        var offset = GetGasTileOffset(gasSlot, tileId.Value);
        if (gasSourceRemainders[offset] == sourceQ && gasSinkRemainders[offset] == sinkQ) return;
        gasSourceRemainders[offset] = sourceQ;
        gasSinkRemainders[offset] = sinkQ;
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.TileResources);
    }

    public void SetGasExchangeRemainder(
        int gasSlot, int edgeSlot, long remainderQ, PhaseChangeBuilder changes)
    {
        var offset = GetGasEdgeOffset(gasSlot, edgeSlot);
        if (gasExchangeRemainders[offset] == remainderQ) return;
        gasExchangeRemainders[offset] = remainderQ;
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.TileResources);
    }

    public void ApplyDelta(
        TileId tileId,
        ResourceHandle resource,
        long delta,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ValidateResource(resource);
        var offset = GetOffset(resource.DenseSlot, tileId.Value);
        var next = checked(quantitiesByResourceThenTile[offset] + delta);
        if (next < 0)
        {
            throw new InvalidOperationException("A tile resource balance cannot become negative.");
        }

        if (next == quantitiesByResourceThenTile[offset])
        {
            return;
        }

        quantitiesByResourceThenTile[offset] = next;
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.TileResources);
        changes.RecordDirty(StateEntityReference.From(tileId), LogicalFieldGroup.TileResources);
    }

    public ulong ComputeCoverageFingerprint()
    {
        var hash = new ShadowHashAccumulator();
        foreach (var quantity in quantitiesByResourceThenTile)
        {
            hash.Add(quantity);
        }

        foreach (var remainder in gasSourceRemainders) hash.Add(remainder);
        foreach (var remainder in gasSinkRemainders) hash.Add(remainder);
        foreach (var remainder in gasExchangeRemainders) hash.Add(remainder);

        return hash.Value;
    }

    private int GetGasTileOffset(int gasSlot, uint tileIndex) =>
        checked((gasSlot * tileCount) + (int)tileIndex);

    private int GetGasEdgeOffset(int gasSlot, int edgeSlot) =>
        checked((gasSlot * gasEdges.Length) + edgeSlot);

    private static ImmutableArray<GasEdge> BuildGasEdges(CompiledWorldProfile profile)
    {
        var pairs = new HashSet<(uint Lower, uint Higher)>();
        foreach (var tile in profile.Tiles)
        {
            Add(WorldGridTopology.Neighbor(
                profile.Width,
                profile.Height,
                tile.TileIndex,
                1,
                0));
            Add(WorldGridTopology.Neighbor(
                profile.Width,
                profile.Height,
                tile.TileIndex,
                0,
                1));

            void Add(uint? otherValue)
            {
                if (otherValue is null) return;
                var other = otherValue.Value;
                if (other == tile.TileIndex) return;
                pairs.Add((Math.Min(tile.TileIndex, other), Math.Max(tile.TileIndex, other)));
            }
        }

        return pairs.OrderBy(pair => pair.Lower).ThenBy(pair => pair.Higher)
            .Select(pair => new GasEdge(TileId.FromRowMajorIndex(pair.Lower), TileId.FromRowMajorIndex(pair.Higher)))
            .ToImmutableArray();
    }

    private int GetOffset(int resourceSlot, uint tileIndex)
    {
        if (tileIndex >= (uint)tileCount)
        {
            throw new KeyNotFoundException($"Tile {tileIndex} does not exist.");
        }

        return checked((resourceSlot * tileCount) + (int)tileIndex);
    }

    private void ValidateResource(ResourceHandle resource)
    {
        if (resource.DenseSlot < 0 ||
            resource.DenseSlot >= resourceIdsBySlot.Length ||
            resourceIdsBySlot[resource.DenseSlot] != resource.Id)
        {
            throw new ArgumentException(
                "The resource handle does not belong to this compiled rule manifest.",
                nameof(resource));
        }
    }
}

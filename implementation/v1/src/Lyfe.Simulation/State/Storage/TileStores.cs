using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.State.Storage;

internal readonly record struct TileSnapshot(
    TileId Id,
    int X,
    int Y,
    int ElevationMeters);

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

    public TileResourceStore(CompiledRulePack rulePack, CompiledWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(rulePack);
        ArgumentNullException.ThrowIfNull(profile);

        tileCount = profile.Tiles.Length;
        resourceIdsBySlot = rulePack.Resources.Select(resource => resource.Id).ToArray();
        quantitiesByResourceThenTile = new long[checked(resourceIdsBySlot.Length * tileCount)];
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

    public long Get(TileId tileId, ResourceHandle resource)
    {
        ValidateResource(resource);
        return quantitiesByResourceThenTile[GetOffset(resource.DenseSlot, tileId.Value)];
    }

    public void Restore(ImmutableArray<PersistenceTileResource> resources)
    {
        if (resources.IsDefault ||
            resources.Length != checked(resourceIdsBySlot.Length * tileCount) ||
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

        return hash.Value;
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

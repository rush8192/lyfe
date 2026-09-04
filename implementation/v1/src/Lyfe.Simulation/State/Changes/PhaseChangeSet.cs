using System.Collections.Immutable;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.State.Changes;

public enum StateEntityKind : byte
{
    Tile = 1,
    Genome = 2,
    Species = 3,
    Organism = 4,
}

public enum LogicalFieldGroup : byte
{
    TileResources = 1,
    SpeciesPopulation = 2,
    OrganismPosition = 3,
    OrganismLifecycle = 4,
    OrganismStructure = 5,
    OrganismReserve = 6,
}

internal enum WorldStoreKind : byte
{
    Identity = 1,
    TileResources = 2,
    Genomes = 3,
    Species = 4,
    Organisms = 5,
}

public readonly record struct StateEntityReference(StateEntityKind Kind, ulong Value)
{
    public static StateEntityReference From(TileId id) => new(StateEntityKind.Tile, id.Value);

    public static StateEntityReference From(GenomeId id) => new(StateEntityKind.Genome, id.Value);

    public static StateEntityReference From(SpeciesId id) => new(StateEntityKind.Species, id.Value);

    public static StateEntityReference From(OrganismId id) => new(StateEntityKind.Organism, id.Value);
}

public readonly record struct DirtyStateEntity(
    LogicalFieldGroup FieldGroup,
    StateEntityReference Entity);

public readonly record struct EntityRelocation(
    OrganismId OrganismId,
    TileId SourceTileId,
    TileId DestinationTileId);

public sealed record PhaseChangeSet(
    ImmutableArray<StateEntityReference> Creates,
    ImmutableArray<StateEntityReference> Removes,
    ImmutableArray<EntityRelocation> Relocations,
    ImmutableArray<DirtyStateEntity> DirtyEntities);

public sealed record LogicalFieldChangeGroup(
    LogicalFieldGroup FieldGroup,
    ImmutableArray<StateEntityReference> Entities);

public sealed record StoreChangeSet(
    StateEntityKind EntityKind,
    ImmutableArray<StateEntityReference> Creates,
    ImmutableArray<StateEntityReference> Removes,
    ImmutableArray<EntityRelocation> Relocations,
    ImmutableArray<LogicalFieldChangeGroup> DirtyFieldGroups);

public readonly record struct ResourceTransactionReference(LedgerTransactionKey Key);

public sealed record MergedTickChanges(
    ImmutableArray<StoreChangeSet> StoreChanges,
    ImmutableArray<ResourceTransactionReference> ResourceTransactionReferences)
{
    public int StructuralChangeCount => StoreChanges.Sum(store =>
        store.Creates.Length + store.Removes.Length + store.Relocations.Length);

    public int DirtyEntityReferenceCount => StoreChanges.Sum(store =>
        store.DirtyFieldGroups.Sum(group => group.Entities.Length));
}

internal readonly record struct StoreCoverage(ulong MutationEpoch, ulong Fingerprint);

internal sealed class PhaseChangeBuilder
{
    private readonly object ownerToken;
    private readonly IReadOnlyDictionary<WorldStoreKind, StoreCoverage> initialCoverage;
    private readonly Dictionary<WorldStoreKind, ulong> mutationCounts = [];
    private readonly HashSet<StateEntityReference> creates = [];
    private readonly HashSet<StateEntityReference> removes = [];
    private readonly HashSet<EntityRelocation> relocations = [];
    private readonly HashSet<DirtyStateEntity> dirtyEntities = [];
    private bool isSealed;

    public PhaseChangeBuilder(
        object ownerToken,
        IReadOnlyDictionary<WorldStoreKind, StoreCoverage> initialCoverage)
    {
        this.ownerToken = ownerToken;
        this.initialCoverage = initialCoverage;
    }

    public bool BelongsTo(object candidateOwnerToken) =>
        ReferenceEquals(ownerToken, candidateOwnerToken);

    public IReadOnlyDictionary<WorldStoreKind, StoreCoverage> InitialCoverage => initialCoverage;

    public IReadOnlyDictionary<WorldStoreKind, ulong> MutationCounts => mutationCounts;

    public void RecordMutation(WorldStoreKind storeKind)
    {
        RequireOpen();
        mutationCounts.TryGetValue(storeKind, out var count);
        mutationCounts[storeKind] = checked(count + 1);
    }

    public void RecordCreate(StateEntityReference entity)
    {
        RequireOpen();
        creates.Add(entity);
    }

    public void RecordRemove(StateEntityReference entity)
    {
        RequireOpen();
        removes.Add(entity);
    }

    public void RecordRelocation(EntityRelocation relocation)
    {
        RequireOpen();
        relocations.Add(relocation);
    }

    public void RecordDirty(StateEntityReference entity, LogicalFieldGroup fieldGroup)
    {
        RequireOpen();
        dirtyEntities.Add(new DirtyStateEntity(fieldGroup, entity));
    }

    public PhaseChangeSet Seal()
    {
        RequireOpen();
        isSealed = true;

        return new PhaseChangeSet(
            creates.OrderBy(entity => entity.Kind).ThenBy(entity => entity.Value).ToImmutableArray(),
            removes.OrderBy(entity => entity.Kind).ThenBy(entity => entity.Value).ToImmutableArray(),
            relocations
                .OrderBy(relocation => relocation.OrganismId.Value)
                .ThenBy(relocation => relocation.SourceTileId.Value)
                .ThenBy(relocation => relocation.DestinationTileId.Value)
                .ToImmutableArray(),
            dirtyEntities
                .OrderBy(entity => entity.FieldGroup)
                .ThenBy(entity => entity.Entity.Kind)
                .ThenBy(entity => entity.Entity.Value)
                .ToImmutableArray());
    }

    private void RequireOpen()
    {
        if (isSealed)
        {
            throw new InvalidOperationException("The phase change builder is already sealed.");
        }
    }
}

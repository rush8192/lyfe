using System.Collections.Immutable;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Ticks;

internal static class TickChangeLimits
{
    public const int MaximumEntityOperations = 200_000;
    public const int MaximumRelocations = 100_000;
    public const int MaximumDirtyEntityReferences = 1_000_000;
    public const int MaximumResourceTransactionReferences = 500_000;
}

internal static class TickChangeMerger
{
    public static MergedTickChanges Merge(
        ulong completedTick,
        ImmutableArray<TickPhaseJournal> phases)
    {
        ArgumentOutOfRangeException.ThrowIfZero(completedTick);
        ValidatePhaseSequence(phases);
        ValidateInputBounds(phases);

        var entities = new Dictionary<StateEntityReference, EntityChangeAccumulator>();
        var ledgerReferences = new HashSet<LedgerTransactionKey>();
        foreach (var phase in phases)
        {
            MergePhase(phase, completedTick, entities, ledgerReferences);
        }

        var storeChanges = BuildStoreChanges(entities);
        var references = ledgerReferences
            .Order()
            .Select(key => new ResourceTransactionReference(key))
            .ToImmutableArray();
        ValidateOutputBounds(storeChanges, references);
        return new MergedTickChanges(storeChanges, references);
    }

    private static void MergePhase(
        TickPhaseJournal phase,
        ulong completedTick,
        Dictionary<StateEntityReference, EntityChangeAccumulator> entities,
        HashSet<LedgerTransactionKey> ledgerReferences)
    {
        foreach (var entity in phase.Changes.Creates)
        {
            ValidateEntity(entity);
            var state = GetAccumulator(entities, entity);
            if (state.Created || state.Removed)
            {
                throw new InvalidOperationException(
                    $"Entity {entity} has an invalid duplicate or remove-then-create sequence.");
            }

            state.Created = true;
        }

        var relocatedThisPhase = new HashSet<OrganismId>();
        foreach (var relocation in phase.Changes.Relocations)
        {
            if (!relocatedThisPhase.Add(relocation.OrganismId))
            {
                throw new InvalidOperationException(
                    $"Organism {relocation.OrganismId} relocates more than once in phase {phase.Phase}.");
            }

            var entity = StateEntityReference.From(relocation.OrganismId);
            ValidateEntity(entity);
            var state = GetAccumulator(entities, entity);
            if (state.Removed || relocation.SourceTileId == relocation.DestinationTileId)
            {
                throw new InvalidOperationException("A relocation must move one live organism between tiles.");
            }

            if (state.RelocationSource is null)
            {
                state.RelocationSource = relocation.SourceTileId;
            }
            else if (state.RelocationDestination != relocation.SourceTileId)
            {
                throw new InvalidOperationException(
                    $"Organism {relocation.OrganismId} has a discontinuous relocation chain.");
            }

            state.RelocationDestination = relocation.DestinationTileId;
        }

        foreach (var dirty in phase.Changes.DirtyEntities)
        {
            ValidateDirty(dirty);
            var state = GetAccumulator(entities, dirty.Entity);
            if (state.Removed)
            {
                throw new InvalidOperationException(
                    $"Entity {dirty.Entity} is dirtied after its removal.");
            }

            state.DirtyFields.Add(dirty.FieldGroup);
        }

        foreach (var entity in phase.Changes.Removes)
        {
            ValidateEntity(entity);
            var state = GetAccumulator(entities, entity);
            if (state.Removed)
            {
                throw new InvalidOperationException($"Entity {entity} is removed more than once.");
            }

            state.Removed = true;
        }

        foreach (var transaction in phase.ResourceTransactions)
        {
            if (transaction.Key.Tick != completedTick || transaction.Key.Phase != phase.Phase)
            {
                throw new InvalidOperationException(
                    "A resource transaction reference does not belong to its completed tick and phase.");
            }

            if (!ledgerReferences.Add(transaction.Key))
            {
                throw new InvalidOperationException(
                    $"Duplicate resource transaction key {transaction.Key}.");
            }
        }
    }

    private static ImmutableArray<StoreChangeSet> BuildStoreChanges(
        Dictionary<StateEntityReference, EntityChangeAccumulator> entities)
    {
        var stores = ImmutableArray.CreateBuilder<StoreChangeSet>(
            Enum.GetValues<StateEntityKind>().Length);
        foreach (var entityKind in Enum.GetValues<StateEntityKind>())
        {
            var matching = entities
                .Where(pair => pair.Key.Kind == entityKind)
                .OrderBy(pair => pair.Key.Value)
                .ToArray();
            var creates = matching
                .Where(pair => pair.Value.Created && !pair.Value.Removed)
                .Select(pair => pair.Key)
                .ToImmutableArray();
            var removes = matching
                .Where(pair => !pair.Value.Created && pair.Value.Removed)
                .Select(pair => pair.Key)
                .ToImmutableArray();
            var relocations = matching
                .Where(pair =>
                    !pair.Value.Created &&
                    !pair.Value.Removed &&
                    pair.Value.RelocationSource is not null &&
                    pair.Value.RelocationSource != pair.Value.RelocationDestination)
                .Select(pair => new EntityRelocation(
                    OrganismId.FromAllocatedValue(pair.Key.Value),
                    pair.Value.RelocationSource!.Value,
                    pair.Value.RelocationDestination!.Value))
                .OrderBy(relocation => relocation.OrganismId.Value)
                .ToImmutableArray();
            var dirtyGroups = Enum.GetValues<LogicalFieldGroup>()
                .Select(fieldGroup => new LogicalFieldChangeGroup(
                    fieldGroup,
                    matching
                        .Where(pair =>
                            !pair.Value.Created &&
                            !pair.Value.Removed &&
                            pair.Value.DirtyFields.Contains(fieldGroup))
                        .Select(pair => pair.Key)
                        .ToImmutableArray()))
                .Where(group => !group.Entities.IsEmpty)
                .ToImmutableArray();

            if (!creates.IsEmpty ||
                !removes.IsEmpty ||
                !relocations.IsEmpty ||
                !dirtyGroups.IsEmpty)
            {
                stores.Add(new StoreChangeSet(
                    entityKind,
                    creates,
                    removes,
                    relocations,
                    dirtyGroups));
            }
        }

        return stores.ToImmutable();
    }

    private static EntityChangeAccumulator GetAccumulator(
        Dictionary<StateEntityReference, EntityChangeAccumulator> entities,
        StateEntityReference entity)
    {
        if (!entities.TryGetValue(entity, out var state))
        {
            state = new EntityChangeAccumulator();
            entities.Add(entity, state);
        }

        return state;
    }

    private static void ValidatePhaseSequence(ImmutableArray<TickPhaseJournal> phases)
    {
        var expected = Enum.GetValues<TickPhase>();
        if (phases.Length != expected.Length)
        {
            throw new InvalidOperationException(
                "A merged tick must contain every canonical phase exactly once.");
        }

        for (var index = 0; index < expected.Length; index++)
        {
            if (phases[index].Phase != expected[index])
            {
                throw new InvalidOperationException(
                    "Phase journals must be merged in canonical phase order.");
            }
        }
    }

    private static void ValidateInputBounds(ImmutableArray<TickPhaseJournal> phases)
    {
        var entityOperations = 0;
        var relocations = 0;
        var dirtyReferences = 0;
        var ledgerReferences = 0;
        foreach (var phase in phases)
        {
            entityOperations = checked(
                entityOperations + phase.Changes.Creates.Length + phase.Changes.Removes.Length);
            relocations = checked(relocations + phase.Changes.Relocations.Length);
            dirtyReferences = checked(dirtyReferences + phase.Changes.DirtyEntities.Length);
            ledgerReferences = checked(ledgerReferences + phase.ResourceTransactions.Length);
        }

        RequireAtMost(
            entityOperations,
            TickChangeLimits.MaximumEntityOperations,
            "entity operations");
        RequireAtMost(relocations, TickChangeLimits.MaximumRelocations, "relocations");
        RequireAtMost(
            dirtyReferences,
            TickChangeLimits.MaximumDirtyEntityReferences,
            "dirty entity references");
        RequireAtMost(
            ledgerReferences,
            TickChangeLimits.MaximumResourceTransactionReferences,
            "resource transaction references");
    }

    private static void ValidateOutputBounds(
        ImmutableArray<StoreChangeSet> stores,
        ImmutableArray<ResourceTransactionReference> ledgerReferences)
    {
        var entityOperations = stores.Sum(store =>
            store.Creates.Length + store.Removes.Length);
        var relocations = stores.Sum(store => store.Relocations.Length);
        var dirtyReferences = stores.Sum(store =>
            store.DirtyFieldGroups.Sum(group => group.Entities.Length));
        RequireAtMost(
            entityOperations,
            TickChangeLimits.MaximumEntityOperations,
            "merged entity operations");
        RequireAtMost(relocations, TickChangeLimits.MaximumRelocations, "merged relocations");
        RequireAtMost(
            dirtyReferences,
            TickChangeLimits.MaximumDirtyEntityReferences,
            "merged dirty entity references");
        RequireAtMost(
            ledgerReferences.Length,
            TickChangeLimits.MaximumResourceTransactionReferences,
            "merged resource transaction references");
    }

    private static void ValidateEntity(StateEntityReference entity)
    {
        if (entity.Kind == default ||
            (entity.Kind != StateEntityKind.Tile && entity.Value == 0))
        {
            throw new InvalidOperationException("A change references an invalid stable entity ID.");
        }
    }

    private static void ValidateDirty(DirtyStateEntity dirty)
    {
        ValidateEntity(dirty.Entity);
        var expectedKind = dirty.FieldGroup switch
        {
            LogicalFieldGroup.TileResources => StateEntityKind.Tile,
            LogicalFieldGroup.SpeciesPopulation or
            LogicalFieldGroup.SpeciesEvolution => StateEntityKind.Species,
            LogicalFieldGroup.OrganismPosition or
            LogicalFieldGroup.OrganismLifecycle or
            LogicalFieldGroup.OrganismStructure or
            LogicalFieldGroup.OrganismReserve or
            LogicalFieldGroup.OrganismCondition or
            LogicalFieldGroup.OrganismIngestedMatter or
            LogicalFieldGroup.OrganismBehavior or
            LogicalFieldGroup.OrganismMicronutrients or
            LogicalFieldGroup.OrganismSpecies => StateEntityKind.Organism,
            LogicalFieldGroup.RemnantContents => StateEntityKind.Remnant,
            LogicalFieldGroup.Gameplay => StateEntityKind.Gameplay,
            _ => throw new InvalidOperationException("A dirty mark has an unknown logical field group."),
        };
        if (dirty.Entity.Kind != expectedKind)
        {
            throw new InvalidOperationException(
                $"Logical field {dirty.FieldGroup} cannot dirty {dirty.Entity.Kind}.");
        }
    }

    private static void RequireAtMost(int actual, int maximum, string category)
    {
        if (actual > maximum)
        {
            throw new InvalidOperationException(
                $"Tick change {category} count {actual} exceeds the limit {maximum}.");
        }
    }

    private sealed class EntityChangeAccumulator
    {
        public bool Created { get; set; }

        public bool Removed { get; set; }

        public TileId? RelocationSource { get; set; }

        public TileId? RelocationDestination { get; set; }

        public HashSet<LogicalFieldGroup> DirtyFields { get; } = [];
    }
}

using System.Collections.Immutable;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.State.Storage;

internal readonly record struct RemnantInitialState(
    OrganismId SourceOrganismId,
    SpeciesId SourceSpeciesId,
    ulong CreatedTick,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    long StructuralMatterQ,
    long ChargedReserveQ,
    uint StructureDecayRemainderQ,
    uint ReserveDecayRemainderQ,
    MicronutrientInventory Micronutrients = default);

internal readonly record struct RemnantSnapshot(
    RemnantId Id,
    OrganismId SourceOrganismId,
    SpeciesId SourceSpeciesId,
    ulong CreatedTick,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    long StructuralMatterQ,
    long ChargedReserveQ,
    uint StructureDecayRemainderQ,
    uint ReserveDecayRemainderQ,
    MicronutrientInventory Micronutrients)
{
    public bool IsEmpty => StructuralMatterQ == 0 && ChargedReserveQ == 0 &&
        Micronutrients.TotalLoadQ == 0;
}

internal sealed class RemnantStore
{
    private readonly Dictionary<RemnantId, RemnantSnapshot> remnants = [];

    public int Count => remnants.Count;

    public ulong MutationEpoch { get; private set; }

    public void Restore(RemnantId id, RemnantInitialState state)
    {
        if (MutationEpoch != 0 || !remnants.TryAdd(id, ToSnapshot(id, state)))
        {
            throw new InvalidOperationException("Remnants can only be restored once with unique IDs.");
        }
    }

    public void Create(RemnantId id, RemnantInitialState state, PhaseChangeBuilder changes)
    {
        Validate(state);
        if (!remnants.TryAdd(id, ToSnapshot(id, state)))
        {
            throw new InvalidOperationException($"Remnant {id} already exists.");
        }

        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Remnants);
        changes.RecordCreate(StateEntityReference.From(id));
    }

    public RemnantSnapshot Get(RemnantId id) => remnants.TryGetValue(id, out var value)
        ? value
        : throw new KeyNotFoundException($"Remnant {id} does not exist.");

    public bool Contains(RemnantId id) => remnants.ContainsKey(id);

    public ImmutableArray<RemnantId> GetIdsInCanonicalOrder() =>
        remnants.Keys.OrderBy(id => id.Value).ToImmutableArray();

    public void SetContents(
        RemnantId id,
        long structuralMatterQ,
        long chargedReserveQ,
        uint structureDecayRemainderQ,
        uint reserveDecayRemainderQ,
        MicronutrientInventory? micronutrients,
        PhaseChangeBuilder changes)
    {
        var current = Get(id);
        var next = current with
        {
            StructuralMatterQ = structuralMatterQ,
            ChargedReserveQ = chargedReserveQ,
            StructureDecayRemainderQ = structureDecayRemainderQ,
            ReserveDecayRemainderQ = reserveDecayRemainderQ,
            Micronutrients = micronutrients ?? current.Micronutrients,
        };
        Validate(ToInitial(next));
        if (next == current)
        {
            return;
        }

        remnants[id] = next;
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Remnants);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.RemnantContents);
    }

    public RemnantSnapshot Remove(RemnantId id, PhaseChangeBuilder changes)
    {
        var current = Get(id);
        remnants.Remove(id);
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Remnants);
        changes.RecordRemove(StateEntityReference.From(id));
        return current;
    }

    public ulong ComputeCoverageFingerprint()
    {
        var hash = new ShadowHashAccumulator();
        hash.Add(Count);
        foreach (var id in GetIdsInCanonicalOrder())
        {
            var value = remnants[id];
            hash.Add(id.Value);
            hash.Add(value.SourceOrganismId.Value);
            hash.Add(value.SourceSpeciesId.Value);
            hash.Add(value.CreatedTick);
            hash.Add(value.TileId.Value);
            hash.Add(value.PositionXQ);
            hash.Add(value.PositionYQ);
            hash.Add(value.StructuralMatterQ);
            hash.Add(value.ChargedReserveQ);
            hash.Add(value.StructureDecayRemainderQ);
            hash.Add(value.ReserveDecayRemainderQ);
            for (var slot = 0; slot < MicronutrientInventory.Count; slot++)
            {
                hash.Add(value.Micronutrients[slot]);
            }
        }

        return hash.Value;
    }

    private static RemnantSnapshot ToSnapshot(RemnantId id, RemnantInitialState state)
    {
        Validate(state);
        return new RemnantSnapshot(
            id,
            state.SourceOrganismId,
            state.SourceSpeciesId,
            state.CreatedTick,
            state.TileId,
            state.PositionXQ,
            state.PositionYQ,
            state.StructuralMatterQ,
            state.ChargedReserveQ,
            state.StructureDecayRemainderQ,
            state.ReserveDecayRemainderQ,
            state.Micronutrients);
    }

    private static RemnantInitialState ToInitial(RemnantSnapshot value) => new(
        value.SourceOrganismId,
        value.SourceSpeciesId,
        value.CreatedTick,
        value.TileId,
        value.PositionXQ,
        value.PositionYQ,
        value.StructuralMatterQ,
        value.ChargedReserveQ,
        value.StructureDecayRemainderQ,
        value.ReserveDecayRemainderQ,
        value.Micronutrients);

    private static void Validate(RemnantInitialState state)
    {
        if (state.SourceOrganismId == default ||
            state.SourceSpeciesId == default ||
            state.StructuralMatterQ < 0 ||
            state.ChargedReserveQ < 0 ||
            state.StructureDecayRemainderQ >= 1_000_000 ||
            state.ReserveDecayRemainderQ >= 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Remnant state is invalid.");
        }
    }
}

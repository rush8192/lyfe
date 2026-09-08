using System.Collections.Immutable;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.State.Storage;

internal sealed class GameplayStateStore
{
    private GameStateSnapshot? state;

    public ulong MutationEpoch { get; private set; }

    public bool IsInitialized => state is not null;

    public GameStateSnapshot Get() => state ?? throw new InvalidOperationException(
        "Gameplay state has not been initialized.");

    public void Restore(GameStateSnapshot value)
    {
        if (MutationEpoch != 0 || state is not null || !IsStructurallyValid(value))
        {
            throw new ArgumentException("Persisted gameplay state is invalid.", nameof(value));
        }

        state = Canonicalize(value);
    }

    public void Initialize(GameStateSnapshot value, PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (state is not null || !IsStructurallyValid(value) || value.GameplayRevision != 1)
        {
            throw new ArgumentException("Initial gameplay state is invalid.", nameof(value));
        }

        state = Canonicalize(value);
        RecordMutation(changes);
        changes.RecordCreate(StateEntityReference.Gameplay());
    }

    public void Replace(GameStateSnapshot value, PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var current = Get();
        if (!IsStructurallyValid(value) || value.GameplayRevision <= current.GameplayRevision)
        {
            throw new ArgumentException("Replacement gameplay state is invalid or stale.", nameof(value));
        }

        state = Canonicalize(value);
        RecordMutation(changes);
        changes.RecordDirty(StateEntityReference.Gameplay(), LogicalFieldGroup.Gameplay);
    }

    public ulong ComputeCoverageFingerprint()
    {
        if (state is null)
        {
            return 0;
        }

        var hash = new ShadowHashAccumulator();
        hash.Add((uint)state.Mode);
        hash.Add((uint)state.RunStatus);
        hash.Add((uint)state.LossReason);
        hash.Add(state.ControlledSpeciesId.Value);
        hash.Add(state.GameplayRevision);
        hash.Add(state.EndedTick ?? 0);
        foreach (var root in state.Roots)
        {
            hash.Add(root.SpeciesId.Value);
            hash.Add(root.FounderGenomeId.Value);
            hash.Add(root.FounderAllocationId.Value);
            hash.Add(root.StartingTileId.Value);
            hash.Add(root.InitialPopulation);
            hash.Add(root.PlayerSelected ? 1U : 0U);
        }
        foreach (var id in state.MutationLockedSpeciesIds)
        {
            hash.Add(id.Value);
        }
        return hash.Value;
    }

    private static GameStateSnapshot Canonicalize(GameStateSnapshot value) => value with
    {
        Roots = value.Roots.OrderBy(root => root.SpeciesId.Value).ToImmutableArray(),
        MutationLockedSpeciesIds = value.MutationLockedSpeciesIds
            .OrderBy(id => id.Value)
            .ToImmutableArray(),
    };

    private static bool IsStructurallyValid(GameStateSnapshot value)
    {
        if (!Enum.IsDefined(value.Mode) || !Enum.IsDefined(value.RunStatus) ||
            !Enum.IsDefined(value.LossReason) || value.ControlledSpeciesId == default ||
            value.GameplayRevision == 0 || value.Roots.IsDefault ||
            value.MutationLockedSpeciesIds.IsDefault ||
            value.Roots.Length != (value.Mode == GameMode.FreeSandbox ? 1 : 2) ||
            value.Roots.Select(root => root.SpeciesId).Distinct().Count() != value.Roots.Length ||
            value.Roots.Count(root => root.PlayerSelected) != 1 ||
            value.Roots.Any(root => root.SpeciesId == default || root.FounderGenomeId == default ||
                root.FounderAllocationId == default ||
                root.InitialPopulation == 0) ||
            value.MutationLockedSpeciesIds.Any(id => id == default) ||
            value.MutationLockedSpeciesIds.Distinct().Count() != value.MutationLockedSpeciesIds.Length)
        {
            return false;
        }

        return value.RunStatus switch
        {
            GameRunStatus.Active => value.LossReason == GameLossReason.None && !value.EndedTick.HasValue,
            GameRunStatus.Lost => value.LossReason != GameLossReason.None && value.EndedTick.HasValue,
            _ => false,
        };
    }

    private void RecordMutation(PhaseChangeBuilder changes)
    {
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Gameplay);
    }
}

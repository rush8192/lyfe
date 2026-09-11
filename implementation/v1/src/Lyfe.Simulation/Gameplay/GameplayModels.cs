using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Gameplay;

public enum GameMode : byte
{
    FreeSandbox = 1,
    Survival = 2,
}

public enum GameRunStatus : byte
{
    Active = 1,
    Lost = 2,
}

public enum GameLossReason : byte
{
    None = 0,
    AllLifeExtinct = 1,
    ControlledSpeciesExtinct = 2,
}

public enum FounderInitializationMode : byte
{
    ScenarioDefault = 0,
    LegacyFixture = 1,
    ZeroSpreadFixture = 2,
}

public sealed record GameSetupCommand(
    GameMode Mode,
    FounderGenomeId PlayerFounderGenomeId,
    TileId PlayerStartingTileId,
    uint FounderPopulation,
    FounderGenomeId? CompetitorFounderGenomeId = null,
    TileId? CompetitorStartingTileId = null,
    FounderAllocationId? PlayerFounderAllocationId = null,
    FounderAllocationId? CompetitorFounderAllocationId = null,
    FounderInitializationMode FounderInitialization = FounderInitializationMode.ScenarioDefault);

public readonly record struct AbiogenesisRoot(
    SpeciesId SpeciesId,
    FounderGenomeId FounderGenomeId,
    FounderAllocationId FounderAllocationId,
    TileId StartingTileId,
    uint InitialPopulation,
    bool PlayerSelected);

public sealed record GameStateSnapshot(
    GameMode Mode,
    GameRunStatus RunStatus,
    GameLossReason LossReason,
    SpeciesId ControlledSpeciesId,
    ulong GameplayRevision,
    ulong? EndedTick,
    ImmutableArray<AbiogenesisRoot> Roots,
    ImmutableArray<SpeciesId> MutationLockedSpeciesIds);

public enum GameplayCommandFailure : byte
{
    None = 0,
    WrongMode = 1,
    RunEnded = 2,
    StaleGameplayRevision = 3,
    SpeciesNotFound = 4,
    SpeciesExtinct = 5,
    AlreadyControlled = 6,
    LockAlreadyMatches = 7,
}

public sealed record GameplayCommandResult(
    bool Accepted,
    GameplayCommandFailure Failure,
    ulong WorldRevision,
    ulong GameplayRevision,
    string StateHash);

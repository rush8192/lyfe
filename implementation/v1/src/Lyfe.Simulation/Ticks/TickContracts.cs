using System.Collections.Immutable;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Ticks;

public enum TickPhase : byte
{
    CommandAdmission = 0,
    CalendarAndConditions = 1,
    EnvironmentalLedger = 2,
    IntrinsicDeath = 3,
    Movement = 4,
    ExternalIntent = 5,
    ExternalResolution = 6,
    InternalMetabolism = 7,
    LifecycleAndReproduction = 8,
    BehaviorUpdate = 9,
    SpeciesSystems = 10,
    Finalization = 11,
}

public enum PhaseExecutionClass : byte
{
    OwnerOnly = 1,
    IndependentMap = 2,
    MapThenGroupedResolve = 3,
    ExactAggregateReduce = 4,
}

public readonly record struct WorldStateGenerationStamp(
    ulong Identity,
    ulong TileResources,
    ulong Genomes,
    ulong Species,
    ulong Organisms);

public sealed record PhaseViewStamp(
    ulong TargetWorldRevision,
    ulong Tick,
    TickPhase Phase,
    WorldStateGenerationStamp StoreGenerations,
    string RulesHash);

public readonly record struct OutcomeKey(
    TickPhase Phase,
    uint SemanticCategoryId,
    ulong ScopeId,
    ulong PrimaryStableId,
    ulong SecondaryStableIdOrDefinitionId,
    uint LocalOrdinal) : IComparable<OutcomeKey>
{
    public static bool operator <(OutcomeKey left, OutcomeKey right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(OutcomeKey left, OutcomeKey right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(OutcomeKey left, OutcomeKey right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(OutcomeKey left, OutcomeKey right) =>
        left.CompareTo(right) >= 0;

    public int CompareTo(OutcomeKey other)
    {
        var comparison = Phase.CompareTo(other.Phase);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = SemanticCategoryId.CompareTo(other.SemanticCategoryId);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = ScopeId.CompareTo(other.ScopeId);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = PrimaryStableId.CompareTo(other.PrimaryStableId);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = SecondaryStableIdOrDefinitionId.CompareTo(
            other.SecondaryStableIdOrDefinitionId);
        return comparison != 0 ? comparison : LocalOrdinal.CompareTo(other.LocalOrdinal);
    }
}

public sealed record TickPhaseJournal(
    TickPhase Phase,
    PhaseExecutionClass ExecutionClass,
    int EvaluatedWorkCount,
    PhaseChangeSet Changes,
    ImmutableArray<ResourceTransaction> ResourceTransactions);

public sealed record TickChangeSet(
    ulong CompletedTick,
    ulong WorldRevision,
    ImmutableArray<TickPhaseJournal> Phases,
    MergedTickChanges MergedChanges)
{
    public int EvaluatedWorkCount => Phases.Sum(phase => phase.EvaluatedWorkCount);
}

internal interface IPhaseReadView
{
    PhaseViewStamp Stamp { get; }

    int WorkCount { get; }
}

internal readonly record struct PhaseOutcome<TPayload>(
    PhaseViewStamp Stamp,
    OutcomeKey Key,
    TPayload Payload);

internal readonly record struct TickExecutionContext(
    ulong TargetWorldRevision,
    ulong Tick,
    uint TickDurationHours,
    string RulesHash,
    ITickFaultInjector FaultInjector,
    ISimulationRandom Random,
    TickScratch Scratch);

internal enum TickFailureStage : byte
{
    Evaluation = 1,
    Preflight = 2,
    Commit = 3,
    CommitAfterFirstMutation = 4,
    Materialization = 5,
    FinalValidation = 6,
}

internal interface ITickFaultInjector
{
    void ThrowIfRequested(TickPhase phase, TickFailureStage stage);
}

internal sealed class NoTickFaultInjector : ITickFaultInjector
{
    public static NoTickFaultInjector Instance { get; } = new();

    private NoTickFaultInjector()
    {
    }

    public void ThrowIfRequested(TickPhase phase, TickFailureStage stage)
    {
    }
}

internal sealed class TickPhaseExecutionException : InvalidOperationException
{
    public TickPhaseExecutionException(
        TickPhase phase,
        TickFailureStage stage,
        Exception innerException)
        : base($"Tick phase {phase} failed during {stage}.", innerException)
    {
        Phase = phase;
        Stage = stage;
    }

    public TickPhase Phase { get; }

    public TickFailureStage Stage { get; }
}

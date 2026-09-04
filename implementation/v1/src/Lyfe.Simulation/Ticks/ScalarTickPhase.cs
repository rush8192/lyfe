using System.Collections.Immutable;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;

namespace Lyfe.Simulation.Ticks;

internal interface IScalarTickPhase
{
    TickPhase Phase { get; }

    TickPhaseJournal Execute(MutableWorldState world, TickExecutionContext context);
}

internal abstract class ScalarTickPhase<TView, TPayload, TPlan> : IScalarTickPhase
    where TView : IPhaseReadView
{
    public abstract TickPhase Phase { get; }

    protected abstract PhaseExecutionClass ExecutionClass { get; }

    public TickPhaseJournal Execute(MutableWorldState world, TickExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(world);

        var view = SealView(world, context);
        ImmutableArray<PhaseOutcome<TPayload>> outcomes;
        try
        {
            context.FaultInjector.ThrowIfRequested(Phase, TickFailureStage.Evaluation);
            outcomes = Evaluate(view, context);
            outcomes = ValidateAndSortOutcomes(view, outcomes);
        }
        catch (Exception exception) when (exception is not TickPhaseExecutionException)
        {
            throw new TickPhaseExecutionException(
                Phase,
                TickFailureStage.Evaluation,
                exception);
        }

        TPlan plan;
        try
        {
            context.FaultInjector.ThrowIfRequested(Phase, TickFailureStage.Preflight);
            if (world.CaptureGenerationStamp() != view.Stamp.StoreGenerations)
            {
                throw new InvalidOperationException(
                    "The working world changed after the phase view was sealed.");
            }

            plan = Preflight(view, outcomes, context);
        }
        catch (Exception exception) when (exception is not TickPhaseExecutionException)
        {
            throw new TickPhaseExecutionException(
                Phase,
                TickFailureStage.Preflight,
                exception);
        }

        var changes = world.BeginChanges();
        var commitCompleted = false;
        try
        {
            Commit(world, plan, changes, context);
            commitCompleted = true;
            context.FaultInjector.ThrowIfRequested(Phase, TickFailureStage.Materialization);
            RunMaterializationBuilders(world, plan, changes, context);
            world.ValidateInvariants();
            var sealedChanges = world.SealChanges(changes);
            return new TickPhaseJournal(
                Phase,
                ExecutionClass,
                view.WorkCount,
                sealedChanges,
                GetResourceTransactions(plan));
        }
        catch (Exception exception) when (exception is not TickPhaseExecutionException)
        {
            world.MarkFaulted(changes);
            var stage = exception is InjectedCommitFailureException
                ? TickFailureStage.CommitAfterFirstMutation
                : commitCompleted
                    ? TickFailureStage.Materialization
                    : TickFailureStage.Commit;
            throw new TickPhaseExecutionException(Phase, stage, exception);
        }
    }

    protected abstract TView SealView(
        MutableWorldState world,
        TickExecutionContext context);

    protected abstract ImmutableArray<PhaseOutcome<TPayload>> Evaluate(
        TView view,
        TickExecutionContext context);

    protected abstract TPlan Preflight(
        TView view,
        ImmutableArray<PhaseOutcome<TPayload>> outcomes,
        TickExecutionContext context);

    protected abstract void Commit(
        MutableWorldState world,
        TPlan plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context);

    protected virtual void RunMaterializationBuilders(
        MutableWorldState world,
        TPlan plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
    }

    protected virtual ImmutableArray<ResourceTransaction> GetResourceTransactions(TPlan plan) => [];

    protected PhaseViewStamp CreateStamp(
        MutableWorldState world,
        TickExecutionContext context) =>
        new(
            context.TargetWorldRevision,
            context.Tick,
            Phase,
            world.CaptureGenerationStamp(),
            context.RulesHash);

    private ImmutableArray<PhaseOutcome<TPayload>> ValidateAndSortOutcomes(
        TView view,
        ImmutableArray<PhaseOutcome<TPayload>> outcomes)
    {
        if (outcomes.Length != view.WorkCount)
        {
            throw new InvalidOperationException(
                $"Phase {Phase} expected {view.WorkCount} outcomes but received {outcomes.Length}.");
        }

        var sorted = outcomes.ToArray();
        Array.Sort(sorted, static (left, right) => left.Key.CompareTo(right.Key));
        for (var index = 0; index < sorted.Length; index++)
        {
            var outcome = sorted[index];
            if (outcome.Stamp != view.Stamp || outcome.Key.Phase != Phase)
            {
                throw new InvalidOperationException(
                    $"Phase {Phase} received an outcome from an incompatible view or phase.");
            }

            if (index > 0 && sorted[index - 1].Key == outcome.Key)
            {
                throw new InvalidOperationException(
                    $"Phase {Phase} received duplicate outcome key {outcome.Key}.");
            }
        }

        return [.. sorted];
    }
}

internal sealed class InjectedCommitFailureException : InvalidOperationException
{
    public InjectedCommitFailureException(Exception innerException)
        : base("An injected failure occurred after the first phase mutation.", innerException)
    {
    }
}

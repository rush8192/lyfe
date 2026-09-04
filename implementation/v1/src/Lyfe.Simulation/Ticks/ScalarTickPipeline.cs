using System.Collections.Immutable;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;

namespace Lyfe.Simulation.Ticks;

internal sealed class ScalarTickPipeline
{
    private readonly ImmutableArray<IScalarTickPhase> phases;

    public ScalarTickPipeline()
    {
        phases =
        [
            new EmptyTickPhase(TickPhase.CommandAdmission, PhaseExecutionClass.OwnerOnly),
            new EmptyTickPhase(TickPhase.CalendarAndConditions, PhaseExecutionClass.IndependentMap),
            new EmptyTickPhase(TickPhase.EnvironmentalLedger, PhaseExecutionClass.MapThenGroupedResolve),
            new IntrinsicAgePhase(),
            new EmptyTickPhase(TickPhase.Movement, PhaseExecutionClass.IndependentMap),
            new ExternalCaptureIntentPhase(),
            new ExternalCaptureResolutionPhase(),
            new EmptyTickPhase(TickPhase.InternalMetabolism, PhaseExecutionClass.MapThenGroupedResolve),
            new EmptyTickPhase(TickPhase.LifecycleAndReproduction, PhaseExecutionClass.IndependentMap),
            new EmptyTickPhase(TickPhase.BehaviorUpdate, PhaseExecutionClass.IndependentMap),
            new EmptyTickPhase(TickPhase.SpeciesSystems, PhaseExecutionClass.ExactAggregateReduce),
            new EmptyTickPhase(TickPhase.Finalization, PhaseExecutionClass.OwnerOnly),
        ];

        var expected = Enum.GetValues<TickPhase>();
        if (phases.Length != expected.Length ||
            !phases.Select(phase => phase.Phase).SequenceEqual(expected))
        {
            throw new InvalidOperationException(
                "The scalar tick pipeline must contain every phase exactly once in canonical order.");
        }
    }

    public TickChangeSet Execute(MutableWorldState world, TickExecutionContext context)
    {
        var journals = ImmutableArray.CreateBuilder<TickPhaseJournal>(phases.Length);
        foreach (var phase in phases)
        {
            journals.Add(phase.Execute(world, context));
        }

        try
        {
            context.FaultInjector.ThrowIfRequested(
                TickPhase.Finalization,
                TickFailureStage.FinalValidation);
            world.ValidateInvariants();

            var phaseJournals = journals.MoveToImmutable();
            var mergedChanges = TickChangeMerger.Merge(context.Tick, phaseJournals);
            return new TickChangeSet(
                context.Tick,
                context.TargetWorldRevision,
                phaseJournals,
                mergedChanges);
        }
        catch (Exception exception)
        {
            world.MarkFaulted();
            throw new TickPhaseExecutionException(
                TickPhase.Finalization,
                TickFailureStage.FinalValidation,
                exception);
        }
    }
}

internal readonly record struct EmptyPhaseView(PhaseViewStamp Stamp) : IPhaseReadView
{
    public int WorkCount => 0;
}

internal sealed class EmptyTickPhase(
    TickPhase phase,
    PhaseExecutionClass executionClass) :
    ScalarTickPhase<EmptyPhaseView, byte, byte>
{
    public override TickPhase Phase { get; } = phase;

    protected override PhaseExecutionClass ExecutionClass { get; } = executionClass;

    protected override EmptyPhaseView SealView(
        MutableWorldState world,
        TickExecutionContext context) =>
        new(CreateStamp(world, context));

    protected override ImmutableArray<PhaseOutcome<byte>> Evaluate(
        EmptyPhaseView view,
        TickExecutionContext context) => [];

    protected override byte Preflight(
        EmptyPhaseView view,
        ImmutableArray<PhaseOutcome<byte>> outcomes,
        TickExecutionContext context) => 0;

    protected override void Commit(
        MutableWorldState world,
        byte plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
    }
}

internal sealed record IntrinsicAgeView(
    PhaseViewStamp Stamp,
    ImmutableArray<OrganismSnapshot> Organisms) : IPhaseReadView
{
    public int WorkCount => Organisms.Length;
}

internal readonly record struct AgeEvaluation(
    OrganismId OrganismId,
    ulong ExpectedAgeHours);

internal readonly record struct AgeMutation(
    OrganismId OrganismId,
    ulong ExpectedAgeHours,
    ulong NextAgeHours);

internal sealed class IntrinsicAgePhase :
    ScalarTickPhase<IntrinsicAgeView, AgeEvaluation, ImmutableArray<AgeMutation>>
{
    private const uint AgeOutcomeCategory = 1;

    public override TickPhase Phase => TickPhase.IntrinsicDeath;

    protected override PhaseExecutionClass ExecutionClass => PhaseExecutionClass.IndependentMap;

    protected override IntrinsicAgeView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var organismIds = world.GetOrganismIdsInCanonicalOrder();
        var organisms = ImmutableArray.CreateBuilder<OrganismSnapshot>(organismIds.Length);
        foreach (var organismId in organismIds)
        {
            organisms.Add(world.GetOrganism(organismId));
        }

        return new IntrinsicAgeView(CreateStamp(world, context), organisms.MoveToImmutable());
    }

    protected override ImmutableArray<PhaseOutcome<AgeEvaluation>> Evaluate(
        IntrinsicAgeView view,
        TickExecutionContext context)
    {
        var outcomes = ImmutableArray.CreateBuilder<PhaseOutcome<AgeEvaluation>>(
            view.Organisms.Length);
        foreach (var organism in view.Organisms)
        {
            outcomes.Add(new PhaseOutcome<AgeEvaluation>(
                view.Stamp,
                new OutcomeKey(Phase, AgeOutcomeCategory, 0, organism.Id.Value, 0, 0),
                new AgeEvaluation(organism.Id, organism.BiologicalAgeHours)));
        }

        return outcomes.MoveToImmutable();
    }

    protected override ImmutableArray<AgeMutation> Preflight(
        IntrinsicAgeView view,
        ImmutableArray<PhaseOutcome<AgeEvaluation>> outcomes,
        TickExecutionContext context)
    {
        var plan = ImmutableArray.CreateBuilder<AgeMutation>(outcomes.Length);
        foreach (var outcome in outcomes)
        {
            var nextAge = checked(
                outcome.Payload.ExpectedAgeHours + context.TickDurationHours);
            plan.Add(new AgeMutation(
                outcome.Payload.OrganismId,
                outcome.Payload.ExpectedAgeHours,
                nextAge));
        }

        return plan.MoveToImmutable();
    }

    protected override void Commit(
        MutableWorldState world,
        ImmutableArray<AgeMutation> plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        for (var index = 0; index < plan.Length; index++)
        {
            var mutation = plan[index];
            world.SetOrganismBiologicalAge(
                mutation.OrganismId,
                mutation.ExpectedAgeHours,
                mutation.NextAgeHours,
                changes);
            if (index == 0)
            {
                try
                {
                    context.FaultInjector.ThrowIfRequested(
                        Phase,
                        TickFailureStage.CommitAfterFirstMutation);
                }
                catch (Exception exception)
                {
                    throw new InjectedCommitFailureException(exception);
                }
            }
        }
    }
}

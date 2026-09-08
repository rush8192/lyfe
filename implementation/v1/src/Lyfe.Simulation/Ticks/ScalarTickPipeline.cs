using System.Collections.Immutable;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Rules.Runtime;
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
            new EnvironmentalLedgerPhase(),
            new IntrinsicAgePhase(),
            new SpatialMovementPhase(),
            new ExternalCaptureIntentPhase(),
            new ExternalCaptureResolutionPhase(),
            new InternalMetabolismPhase(),
            new LifecycleReproductionPhase(),
            new BehaviorUpdatePhase(),
            new SpeciesSystemsPhase(),
            new GameplayOutcomePhase(),
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
            world.ValidateGameplayOutcomeInvariants();

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
    ImmutableArray<IntrinsicOrganismCandidate> Organisms) : IPhaseReadView
{
    public int WorkCount => Organisms.Length;
}

internal readonly record struct IntrinsicOrganismCandidate(
    OrganismSnapshot Organism,
    CompiledOrganismPhysiology Physiology,
    OrganismEnvironmentInput Environment);

internal readonly record struct AgeEvaluation(
    OrganismId OrganismId,
    ulong ExpectedAgeHours,
    IntrinsicDeathEvidence DeathEvidence,
    MaterializedOrganismCondition Condition);

internal readonly record struct AgeMutation(
    OrganismId OrganismId,
    ulong ExpectedAgeHours,
    ulong NextAgeHours,
    MaterializedOrganismCondition Condition,
    IntrinsicDeathEvidence DeathEvidence);

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
        var organisms = ImmutableArray.CreateBuilder<IntrinsicOrganismCandidate>(organismIds.Length);
        foreach (var organismId in organismIds)
        {
            var organism = world.GetOrganism(organismId);
            organisms.Add(new IntrinsicOrganismCandidate(
                organism,
                world.GetCompiledPhenotype(organism.SpeciesId).Physiology,
                world.GetOrganismEnvironment(
                    organism.TileId,
                    checked((context.Tick - 1) * context.TickDurationHours +
                        context.TickDurationHours / 2))));
        }

        return new IntrinsicAgeView(CreateStamp(world, context), organisms.MoveToImmutable());
    }

    protected override ImmutableArray<PhaseOutcome<AgeEvaluation>> Evaluate(
        IntrinsicAgeView view,
        TickExecutionContext context)
    {
        var outcomes = ImmutableArray.CreateBuilder<PhaseOutcome<AgeEvaluation>>(
            view.Organisms.Length);
        foreach (var candidate in view.Organisms)
        {
            var organism = candidate.Organism;
            var nextAge = ulong.MaxValue - organism.BiologicalAgeHours < context.TickDurationHours
                ? ulong.MaxValue
                : organism.BiologicalAgeHours + context.TickDurationHours;
            var evidence = IntrinsicDeathEvaluator.Evaluate(
                organism.Id,
                context.Tick,
                candidate.Physiology,
                new OrganismConditionInput(
                    organism.StructuralMatterQ,
                    organism.ChargedReserveQ,
                    nextAge),
                candidate.Environment,
                context.Random);
            var condition = OrganismConditionBuilder.Build(
                candidate.Physiology,
                new OrganismConditionInput(
                    organism.StructuralMatterQ,
                    organism.ChargedReserveQ,
                    nextAge),
                candidate.Environment,
                context.Tick,
                ConditionSnapshotKind.IntrinsicStart);
            outcomes.Add(new PhaseOutcome<AgeEvaluation>(
                view.Stamp,
                new OutcomeKey(Phase, AgeOutcomeCategory, 0, organism.Id.Value, 0, 0),
                new AgeEvaluation(
                    organism.Id,
                    organism.BiologicalAgeHours,
                    evidence,
                    condition)));
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
            var nextAge = checked(outcome.Payload.ExpectedAgeHours + context.TickDurationHours);
            plan.Add(new AgeMutation(
                outcome.Payload.OrganismId,
                outcome.Payload.ExpectedAgeHours,
                nextAge,
                outcome.Payload.Condition,
                outcome.Payload.DeathEvidence));
        }


        context.Scratch.PublishIntrinsicDeathAssessments(
            context.Tick,
            outcomes
                .Select(outcome => outcome.Payload.DeathEvidence)
                .Where(evidence => !evidence.NonzeroCauses.IsEmpty)
                .ToImmutableArray());

        return plan.MoveToImmutable();
    }

    protected override void Commit(
        MutableWorldState world,
        ImmutableArray<AgeMutation> plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        var deathRecords = ImmutableArray.CreateBuilder<DeathRecord>();
        for (var index = 0; index < plan.Length; index++)
        {
            var mutation = plan[index];
            if (mutation.DeathEvidence.Died)
            {
                var organism = world.GetOrganism(mutation.OrganismId);
                var remnantId = world.CreateRemnant(
                    new RemnantInitialState(
                        organism.Id,
                        organism.SpeciesId,
                        context.Tick,
                        organism.TileId,
                        organism.PositionXQ,
                        organism.PositionYQ,
                        checked(organism.StructuralMatterQ +
                            organism.IngestedStructuralMatterQ),
                    organism.ChargedReserveQ,
                    0,
                    0,
                    organism.CommittedMicronutrients.Add(organism.FreeMicronutrients)),
                    changes);
                world.RemoveOrganism(mutation.OrganismId, changes);
                deathRecords.Add(new DeathRecord(
                    mutation.OrganismId,
                    organism.SpeciesId,
                    organism.TileId,
                    organism.PositionXQ,
                    organism.PositionYQ,
                    context.Tick,
                    TickPhase.IntrinsicDeath,
                    mutation.DeathEvidence.NonzeroCauses,
                    mutation.DeathEvidence.ActualCause!.Value,
                    remnantId));
            }
            else
            {
                var organism = world.GetOrganism(mutation.OrganismId);
                var priorStressBand = StressBand(organism.Condition.EnvironmentalFactorQ);
                var currentStressBand = StressBand(
                    mutation.Condition.EnvironmentalFactorQ);
                if (currentStressBand > priorStressBand)
                {
                    context.Scratch.AppendStressReceipt(
                        context.Tick,
                        new StressActivityReceipt(
                            organism.Id,
                            organism.SpeciesId,
                            organism.TileId,
                            organism.PositionXQ,
                            organism.PositionYQ,
                            priorStressBand,
                            currentStressBand,
                            mutation.Condition.EnvironmentalFactorQ));
                }
                world.SetOrganismBiologicalAge(
                    mutation.OrganismId,
                    mutation.ExpectedAgeHours,
                    mutation.NextAgeHours,
                    changes);
                world.SetMaterializedOrganismCondition(
                    mutation.OrganismId,
                    mutation.Condition,
                    changes);
            }
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


        context.Scratch.PublishDeathRecords(context.Tick, deathRecords.ToImmutable());
    }

    private static byte StressBand(uint environmentalFactorQ) =>
        environmentalFactorQ switch
        {
            >= 850_000 => 0,
            >= 600_000 => 1,
            >= 300_000 => 2,
            _ => 3,
        };
}

internal readonly record struct EndConditionView(
    PhaseViewStamp Stamp,
    ImmutableArray<OrganismId> OrganismIds) : IPhaseReadView
{
    public int WorkCount => OrganismIds.Length;
}

internal sealed class EndConditionMaterializationPhase :
    ScalarTickPhase<EndConditionView, OrganismId, ImmutableArray<OrganismId>>
{
    private const uint ConditionOutcomeCategory = 1;

    public override TickPhase Phase => TickPhase.Finalization;

    protected override PhaseExecutionClass ExecutionClass => PhaseExecutionClass.OwnerOnly;

    protected override EndConditionView SealView(
        MutableWorldState world,
        TickExecutionContext context) =>
        new(CreateStamp(world, context), world.GetOrganismIdsInCanonicalOrder());

    protected override ImmutableArray<PhaseOutcome<OrganismId>> Evaluate(
        EndConditionView view,
        TickExecutionContext context) =>
        view.OrganismIds
            .Select(id => new PhaseOutcome<OrganismId>(
                view.Stamp,
                new OutcomeKey(Phase, ConditionOutcomeCategory, 0, id.Value, 0, 0),
                id))
            .ToImmutableArray();

    protected override ImmutableArray<OrganismId> Preflight(
        EndConditionView view,
        ImmutableArray<PhaseOutcome<OrganismId>> outcomes,
        TickExecutionContext context) =>
        outcomes.Select(outcome => outcome.Payload).ToImmutableArray();

    protected override void Commit(
        MutableWorldState world,
        ImmutableArray<OrganismId> plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
    }

    protected override void RunMaterializationBuilders(
        MutableWorldState world,
        ImmutableArray<OrganismId> plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        foreach (var organismId in plan)
        {
            world.MaterializeOrganismCondition(
                organismId,
                context.Tick,
                ConditionSnapshotKind.End,
                world.GetOrganismEnvironment(
                    world.GetOrganism(organismId).TileId,
                    checked(context.Tick * context.TickDurationHours)),
                changes);
        }
    }
}

using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;

namespace Lyfe.Simulation.Ticks;

internal readonly record struct BehaviorCandidate(
    OrganismSnapshot Organism,
    CompiledBehaviorProfile Profile,
    long ReserveCapacityQ,
    uint AcquisitionCoverageSampleQ);

internal sealed record BehaviorView(
    PhaseViewStamp Stamp,
    ImmutableArray<BehaviorCandidate> Candidates) : IPhaseReadView
{
    public int WorkCount => Candidates.Length;
}

internal readonly record struct BehaviorMutation(
    OrganismId OrganismId,
    OrganismBehaviorState State);

internal sealed class BehaviorUpdatePhase :
    ScalarTickPhase<BehaviorView, BehaviorMutation, ImmutableArray<BehaviorMutation>>
{
    private const uint OutcomeCategory = 1;

    public override TickPhase Phase => TickPhase.BehaviorUpdate;

    protected override PhaseExecutionClass ExecutionClass => PhaseExecutionClass.IndependentMap;

    protected override BehaviorView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var coverage = context.Scratch.RequireAcquisitionSamples(context.Tick)
            .ToDictionary(
                sample => sample.OrganismId,
                sample => BehaviorMath.FractionQ(sample.GrantedQ, sample.UsefulDemandQ));
        var candidates = world.GetOrganismIdsInCanonicalOrder()
            .Select(world.GetOrganism)
            .Select(organism => new BehaviorCandidate(
                organism,
                world.GetCompiledPhenotype(organism.SpeciesId).Physiology.Behavior,
                world.GetCompiledPhenotype(organism.SpeciesId).Physiology.ChargedReserveCapacityQ,
                coverage.GetValueOrDefault(
                    organism.Id,
                    OrganismBehaviorState.NeutralCoverageQ)))
            .ToImmutableArray();
        return new BehaviorView(CreateStamp(world, context), candidates);
    }

    protected override ImmutableArray<PhaseOutcome<BehaviorMutation>> Evaluate(
        BehaviorView view,
        TickExecutionContext context)
    {
        var outcomes = ImmutableArray.CreateBuilder<PhaseOutcome<BehaviorMutation>>(
            view.Candidates.Length);
        foreach (var candidate in view.Candidates)
        {
            var next = Evaluate(candidate, context);
            outcomes.Add(new PhaseOutcome<BehaviorMutation>(
                view.Stamp,
                new OutcomeKey(
                    Phase,
                    OutcomeCategory,
                    candidate.Organism.TileId.Value,
                    candidate.Organism.Id.Value,
                    (ulong)next.BehaviorId,
                    0),
                new BehaviorMutation(candidate.Organism.Id, next)));
        }

        return outcomes.MoveToImmutable();
    }

    protected override ImmutableArray<BehaviorMutation> Preflight(
        BehaviorView view,
        ImmutableArray<PhaseOutcome<BehaviorMutation>> outcomes,
        TickExecutionContext context) =>
        outcomes.Select(outcome => outcome.Payload).ToImmutableArray();

    protected override void Commit(
        MutableWorldState world,
        ImmutableArray<BehaviorMutation> plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        foreach (var mutation in plan)
        {
            var organism = world.GetOrganism(mutation.OrganismId);
            if (organism.Behavior.BehaviorId != mutation.State.BehaviorId)
            {
                context.Scratch.AppendBehaviorTransitionReceipt(
                    context.Tick,
                    new BehaviorTransitionActivityReceipt(
                        organism.Id,
                        organism.SpeciesId,
                        organism.TileId,
                        organism.PositionXQ,
                        organism.PositionYQ,
                        organism.Behavior.BehaviorId,
                        mutation.State.BehaviorId));
            }
            world.SetOrganismBehavior(mutation.OrganismId, mutation.State, changes);
        }
    }

    internal static OrganismBehaviorState Evaluate(
        BehaviorCandidate candidate,
        TickExecutionContext context)
    {
        var organism = candidate.Organism;
        var current = organism.Behavior;
        if (organism.BirthTick == context.Tick)
        {
            return current;
        }

        if (!candidate.Profile.ResourceConservation)
        {
            return current;
        }

        // Mandatory maintenance does not yet exist in the executable rule pack, so a
        // neutral energy-coverage sample is the only truthful denominator for this slice.
        var energyCoverageQ = BehaviorMath.UpdateOneDayEma(
            current.RecentEnergyCoverageQ,
            OrganismBehaviorState.NeutralCoverageQ,
            context.TickDurationHours);
        var acquisitionCoverageQ = BehaviorMath.UpdateOneDayEma(
            current.RecentAcquisitionCoverageQ,
            candidate.AcquisitionCoverageSampleQ,
            context.TickDurationHours);
        var limitingDeficitQ = checked(
            BehaviorMath.OneQ - Math.Min(BehaviorMath.OneQ, candidate.AcquisitionCoverageSampleQ));
        var reserveFractionQ = BehaviorMath.FractionQ(
            organism.ChargedReserveQ,
            candidate.ReserveCapacityQ);
        var desired = OrganismBehaviorId.Baseline;
        var enter = reserveFractionQ <= candidate.Profile.ConservationEnterReserveQ ||
            reserveFractionQ < candidate.Profile.ConservationEnterConditionalReserveQ &&
            energyCoverageQ < candidate.Profile.ConservationEnterEnergyCoverageQ;
        var exit = reserveFractionQ >= candidate.Profile.ConservationExitReserveQ &&
            (energyCoverageQ >= candidate.Profile.ConservationExitEnergyCoverageQ ||
                reserveFractionQ >= candidate.Profile.ConservationExitHighReserveQ);
        desired = current.BehaviorId == OrganismBehaviorId.Conserving
            ? exit ? OrganismBehaviorId.Baseline : OrganismBehaviorId.Conserving
            : enter ? OrganismBehaviorId.Conserving : OrganismBehaviorId.Baseline;

        var criticalEntry = desired == OrganismBehaviorId.Conserving &&
            reserveFractionQ <= candidate.Profile.ConservationCriticalReserveQ;
        if (desired != current.BehaviorId &&
            context.Tick < current.MinimumDwellUntilTick &&
            !criticalEntry)
        {
            desired = current.BehaviorId;
        }

        var changed = desired != current.BehaviorId;
        var dwellTicks = checked((candidate.Profile.MinimumDwellHours +
            context.TickDurationHours - 1) / context.TickDurationHours);
        return new OrganismBehaviorState(
            desired,
            changed ? BehaviorTargetKind.None : current.TargetKind,
            changed ? 0 : current.TargetId,
            changed ? 0 : current.TargetPositionXQ,
            changed ? 0 : current.TargetPositionYQ,
            changed ? context.Tick : current.SelectedAtTick,
            changed ? checked(context.Tick + dwellTicks) : current.MinimumDwellUntilTick,
            energyCoverageQ,
            acquisitionCoverageQ,
            limitingDeficitQ);
    }
}

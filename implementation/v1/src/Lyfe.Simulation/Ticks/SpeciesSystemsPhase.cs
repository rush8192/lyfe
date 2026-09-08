using System.Collections.Immutable;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;

namespace Lyfe.Simulation.Ticks;

internal readonly record struct SpeciesOrganismCandidate(
    OrganismSnapshot Organism,
    MaterializedOrganismCondition EndCondition);

internal sealed record SpeciesSystemsView(
    PhaseViewStamp Stamp,
    ImmutableArray<SpeciesSnapshot> Species,
    ImmutableArray<SpeciesOrganismCandidate> Organisms) : IPhaseReadView
{
    public int WorkCount => Organisms.Length;
}

internal readonly record struct SpeciesOrganismOutcome(
    OrganismId OrganismId,
    SpeciesId SpeciesId,
    MaterializedOrganismCondition EndCondition,
    uint EnergyShortageSampleQ);

internal sealed record SpeciesAggregateMutation(
    ImmutableArray<SpeciesOrganismOutcome> Organisms,
    ImmutableArray<(SpeciesId SpeciesId, SpeciesEvolutionAccount Account)> Species);

internal sealed class SpeciesSystemsPhase :
    ScalarTickPhase<SpeciesSystemsView, SpeciesOrganismOutcome, SpeciesAggregateMutation>
{
    private const uint OutcomeCategory = 1;
    private const uint HourlyPressureRetentionQ = 997_939;

    public override TickPhase Phase => TickPhase.SpeciesSystems;

    protected override PhaseExecutionClass ExecutionClass => PhaseExecutionClass.ExactAggregateReduce;

    protected override SpeciesSystemsView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var organisms = world.GetOrganismIdsInCanonicalOrder()
            .Select(world.GetOrganism)
            .Select(organism => new SpeciesOrganismCandidate(
                organism,
                OrganismConditionBuilder.Build(
                    world.GetCompiledPhenotype(organism.SpeciesId).Physiology,
                    new OrganismConditionInput(
                        organism.StructuralMatterQ,
                        organism.ChargedReserveQ,
                        organism.BiologicalAgeHours),
                    world.GetOrganismEnvironment(
                        organism.TileId,
                        checked(context.Tick * context.TickDurationHours)),
                    context.Tick,
                    ConditionSnapshotKind.End)))
            .ToImmutableArray();
        return new SpeciesSystemsView(
            CreateStamp(world, context),
            world.GetSpeciesIdsInCanonicalOrder().Select(world.GetSpecies).ToImmutableArray(),
            organisms);
    }

    protected override ImmutableArray<PhaseOutcome<SpeciesOrganismOutcome>> Evaluate(
        SpeciesSystemsView view,
        TickExecutionContext context) =>
        view.Organisms.Select(candidate =>
        {
            var reserveDeficit = RatioQ.Scale - candidate.EndCondition.ReserveFactorQ;
            var shortage = Math.Max(reserveDeficit, candidate.Organism.Behavior.LimitingMaterialDeficitQ);
            return new PhaseOutcome<SpeciesOrganismOutcome>(
                view.Stamp,
                new OutcomeKey(
                    Phase,
                    OutcomeCategory,
                    candidate.Organism.SpeciesId.Value,
                    candidate.Organism.Id.Value,
                    0,
                    0),
                new SpeciesOrganismOutcome(
                    candidate.Organism.Id,
                    candidate.Organism.SpeciesId,
                    candidate.EndCondition,
                    shortage));
        }).ToImmutableArray();

    protected override SpeciesAggregateMutation Preflight(
        SpeciesSystemsView view,
        ImmutableArray<PhaseOutcome<SpeciesOrganismOutcome>> outcomes,
        TickExecutionContext context)
    {
        var bySpecies = outcomes.GroupBy(outcome => outcome.Payload.SpeciesId)
            .ToDictionary(group => group.Key, group => group.Select(value => value.Payload).ToArray());
        var mutations = ImmutableArray.CreateBuilder<(SpeciesId, SpeciesEvolutionAccount)>(view.Species.Length);
        foreach (var current in view.Species)
        {
            bySpecies.TryGetValue(current.Id, out var members);
            members ??= [];
            ulong healthSum = 0;
            ulong shortageSum = 0;
            foreach (var member in members)
            {
                healthSum = checked(healthSum + member.EndCondition.RelativeHealthQ);
                shortageSum = checked(shortageSum + member.EnergyShortageSampleQ);
            }

            var averageHealthQ = members.Length == 0
                ? 0U
                : DivideRoundTiesToEven(healthSum, checked((uint)members.Length));
            var shortageQ = members.Length == 0
                ? 0U
                : DivideRoundTiesToEven(shortageSum, checked((uint)members.Length));
            mutations.Add((current.Id, current.Evolution with
            {
                AverageHealthQ = averageHealthQ,
                LastIncomeQ = -1,
                Pressure = UpdatePressure(current.Evolution.Pressure, shortageQ, context.TickDurationHours),
            }));
        }

        return new SpeciesAggregateMutation(
            outcomes.Select(outcome => outcome.Payload).ToImmutableArray(),
            mutations.MoveToImmutable());
    }

    protected override void Commit(
        MutableWorldState world,
        SpeciesAggregateMutation plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        foreach (var organism in plan.Organisms)
        {
            world.SetMaterializedOrganismCondition(organism.OrganismId, organism.EndCondition, changes);
        }
        foreach (var (speciesId, provisional) in plan.Species)
        {
            var current = world.GetSpecies(speciesId);
            var phenotype = world.GetCompiledPhenotype(speciesId);
            var (creditQ, remainder) = MutationIncomeMath.Accumulate(
                world.Rules.RulePack.EffectivePopulationQ,
                current.Population,
                provisional.AverageHealthQ,
                phenotype.MutationIncomeModifierQ,
                context.TickDurationHours,
                current.Evolution.MutationIncomeRemainder);
            var next = provisional with
            {
                MutationBalanceQ = checked(current.Evolution.MutationBalanceQ + creditQ),
                MutationIncomeRemainder = remainder,
                LastIncomeQ = creditQ,
            };
            world.SetSpeciesEvolutionAggregate(speciesId, next, context.Tick, changes);
        }
    }

    private static EvolutionPressureState UpdatePressure(
        EvolutionPressureState prior,
        uint shortageSampleQ,
        uint elapsedHours)
    {
        var retention = RatioQ.Scale;
        for (var hour = 0U; hour < elapsedHours; hour++)
        {
            retention = RatioQ.Multiply(retention, HourlyPressureRetentionQ);
        }
        var sampleWeight = RatioQ.Scale - retention;
        var energy = checked(RatioQ.Multiply(prior.EnergyShortageQ, retention) +
            RatioQ.Multiply(shortageSampleQ, sampleWeight));
        var starvationSample = shortageSampleQ >= 900_000 ? shortageSampleQ : 0;
        var starvation = checked(RatioQ.Multiply(prior.StarvationQ, retention) +
            RatioQ.Multiply(starvationSample, sampleWeight));
        return new EvolutionPressureState(energy, starvation);
    }

    private static uint DivideRoundTiesToEven(ulong numerator, uint denominator)
    {
        var quotient = numerator / denominator;
        var remainder = numerator % denominator;
        var twice = remainder * 2;
        if (twice > denominator || (twice == denominator && (quotient & 1) != 0)) quotient++;
        return checked((uint)quotient);
    }
}

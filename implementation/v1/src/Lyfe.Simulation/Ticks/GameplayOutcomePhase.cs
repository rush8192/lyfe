using System.Collections.Immutable;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Ticks;

internal sealed record GameplayOutcomeView(
    PhaseViewStamp Stamp,
    GameStateSnapshot Game,
    ImmutableArray<(SpeciesId SpeciesId, ulong Population)> Species) : IPhaseReadView
{
    public int WorkCount => 0;
}

internal readonly record struct GameplayOutcomePlan(GameLossReason LossReason);

internal sealed class GameplayOutcomePhase :
    ScalarTickPhase<GameplayOutcomeView, byte, GameplayOutcomePlan>
{
    public override TickPhase Phase => TickPhase.Finalization;

    protected override PhaseExecutionClass ExecutionClass => PhaseExecutionClass.OwnerOnly;

    protected override GameplayOutcomeView SealView(
        MutableWorldState world,
        TickExecutionContext context) =>
        new(
            CreateStamp(world, context),
            world.GetGameplayState(),
            world.GetSpeciesIdsInCanonicalOrder()
                .Select(id => (id, world.GetSpecies(id).Population))
                .ToImmutableArray());

    protected override ImmutableArray<PhaseOutcome<byte>> Evaluate(
        GameplayOutcomeView view,
        TickExecutionContext context) => [];

    protected override GameplayOutcomePlan Preflight(
        GameplayOutcomeView view,
        ImmutableArray<PhaseOutcome<byte>> outcomes,
        TickExecutionContext context)
    {
        if (view.Game.RunStatus != GameRunStatus.Active)
        {
            return default;
        }

        var loss = view.Game.Mode switch
        {
            GameMode.FreeSandbox when view.Species.All(species => species.Population == 0) =>
                GameLossReason.AllLifeExtinct,
            GameMode.Survival when view.Species.Single(species =>
                species.SpeciesId == view.Game.ControlledSpeciesId).Population == 0 =>
                GameLossReason.ControlledSpeciesExtinct,
            _ => GameLossReason.None,
        };
        return new GameplayOutcomePlan(loss);
    }

    protected override void Commit(
        MutableWorldState world,
        GameplayOutcomePlan plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        if (plan.LossReason == GameLossReason.None)
        {
            return;
        }

        var current = world.GetGameplayState();
        world.ReplaceGameplayState(current with
        {
            RunStatus = GameRunStatus.Lost,
            LossReason = plan.LossReason,
            EndedTick = context.Tick,
            GameplayRevision = checked(current.GameplayRevision + 1),
        }, changes);
    }
}

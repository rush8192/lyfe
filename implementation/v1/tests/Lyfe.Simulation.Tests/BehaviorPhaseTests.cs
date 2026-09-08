using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.Ticks;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class BehaviorPhaseTests
{
    [Fact]
    public void OneHourCoverageMemoryUsesRoundHalfUpOneDayEma()
    {
        Assert.Equal(958_333U, BehaviorMath.UpdateOneDayEma(1_000_000, 0, 1));
        Assert.Equal(1_000_000U,
            BehaviorMath.UpdateOneDayEma(1_000_000, 1_000_000, 1));
    }

    [Fact]
    public void LowReserveSelectsConservationAndStartsFourHourDwell()
    {
        var candidate = Candidate(5_000, OrganismBehaviorState.Initial(0), 0);

        var result = BehaviorUpdatePhase.Evaluate(candidate, Context(tick: 10));

        Assert.Equal(OrganismBehaviorId.Conserving, result.BehaviorId);
        Assert.Equal(10UL, result.SelectedAtTick);
        Assert.Equal(14UL, result.MinimumDwellUntilTick);
        Assert.Equal(958_333U, result.RecentAcquisitionCoverageQ);
        Assert.Equal(1_000_000U, result.LimitingMaterialDeficitQ);
    }

    [Fact]
    public void HysteresisExitsOnlyAfterRecoveryAndDwell()
    {
        var conserving = OrganismBehaviorState.Initial(10) with
        {
            BehaviorId = OrganismBehaviorId.Conserving,
            MinimumDwellUntilTick = 14,
        };

        var retained = BehaviorUpdatePhase.Evaluate(
            Candidate(9_000, conserving, 1_000_000),
            Context(tick: 13));
        var exited = BehaviorUpdatePhase.Evaluate(
            Candidate(9_000, retained, 1_000_000),
            Context(tick: 14));

        Assert.Equal(OrganismBehaviorId.Conserving, retained.BehaviorId);
        Assert.Equal(OrganismBehaviorId.Baseline, exited.BehaviorId);
        Assert.Equal(18UL, exited.MinimumDwellUntilTick);
    }

    [Fact]
    public void CriticalReserveCanInterruptBaselineDwell()
    {
        var baseline = OrganismBehaviorState.Initial(10) with
        {
            MinimumDwellUntilTick = 14,
        };

        var result = BehaviorUpdatePhase.Evaluate(
            Candidate(2_000, baseline, 1_000_000),
            Context(tick: 11));

        Assert.Equal(OrganismBehaviorId.Conserving, result.BehaviorId);
        Assert.Equal(15UL, result.MinimumDwellUntilTick);
    }

    [Fact]
    public void FounderWithoutConservationTraitRetainsNeutralBaselineState()
    {
        var state = OrganismBehaviorState.Initial(0);
        var candidate = Candidate(1_000, state, 0) with
        {
            Profile = Profile with { ResourceConservation = false },
        };

        Assert.Equal(state, BehaviorUpdatePhase.Evaluate(candidate, Context(tick: 10)));
    }

    private static readonly CompiledBehaviorProfile Profile = new(
        true,
        4,
        500_000,
        800_000,
        850_000,
        200_000,
        650_000,
        1_000_000,
        850_000);

    private static BehaviorCandidate Candidate(
        long reserveQ,
        OrganismBehaviorState behavior,
        uint acquisitionCoverageQ) => new(
            new OrganismSnapshot(
                OrganismId.FromAllocatedValue(1),
                SpeciesId.FromAllocatedValue(1),
                TileId.FromRowMajorIndex(0),
                0,
                0,
                0,
                0,
                0,
                1,
                LifecyclePhase.Mature,
                100,
                0,
                0,
                0,
                1_000,
                reserveQ,
                new MaterializedOrganismCondition(
                    0,
                    ConditionSnapshotKind.End,
                    checked((uint)(reserveQ * 100)),
                    1_000_000,
                    1_000_000,
                    1_000_000,
                    1_000_000,
                    1_000_000,
                    checked((uint)(reserveQ * 100)),
                    45_000,
                    0),
                behavior),
            Profile,
            10_000,
            acquisitionCoverageQ);

    private static TickExecutionContext Context(ulong tick) => new(
        tick,
        tick,
        1,
        "rules",
        NoTickFaultInjector.Instance,
        new SemanticRandomOracle(RootRandomSeed.Parse(
            "00112233445566778899aabbccddeeff")),
        new TickScratch(tick));
}

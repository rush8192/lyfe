using Lyfe.Server.Evolution;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.World;
using Proto = Lyfe.Protocol.V1;
using Xunit;

namespace Lyfe.Server.Tests;

public sealed class EvolutionProtocolServiceTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("00112233445566778899aabbccddeeff");

    [Fact]
    public void DecisionSurfaceCarriesAuthoritativeTraitsAndOccupiedTiles()
    {
        var runner = CreateRunner();
        var service = new EvolutionProtocolService();

        var surface = service.CaptureDecisionSurface(runner);

        Assert.Equal(1UL, surface.WorldId);
        Assert.Equal(100UL, surface.ControlledPopulation);
        Assert.Equal(3U, surface.MaximumChangeComplexity);
        Assert.Equal(100UL, Assert.Single(surface.OccupiedTiles).Population);
        var stateGated = surface.Traits.Single(value => value.TraitId == 3);
        Assert.Equal("State-Gated Activity", stateGated.DisplayName);
        Assert.True(stateGated.Selectable);
        Assert.False(stateGated.Acquired);
        Assert.Equal(40_000_000L, stateGated.MutationPointCostQ);
        Assert.Equal([1U], stateGated.PrerequisiteTraitIds);
    }

    [Fact]
    public void UnaffordablePreviewStillExplainsCostFoundersAndResultingGenome()
    {
        var runner = CreateRunner();
        var service = new EvolutionProtocolService();
        var surface = service.CaptureDecisionSurface(runner);

        var preview = service.Preview(runner, Proposal(surface, 3));

        Assert.False(preview.Accepted);
        Assert.Equal(Proto.SpeciationFailure.InsufficientMutationPoints, preview.Failure);
        Assert.Equal(40_000_000L, preview.MutationPriceQ);
        Assert.Equal(1U, preview.ChangeComplexity);
        Assert.Equal(50U, Assert.Single(preview.FounderCounts).Count);
        Assert.Equal(50UL, preview.AncestorPopulationAfter);
        Assert.Equal(50UL, preview.DescendantPopulation);
        Assert.Equal(64, preview.ProposedGenomeHash.Length);
        Assert.Equal(168UL, preview.ResultingSpeciationNotBeforeTick);
    }

    [Fact]
    public void ApplyRetryReturnsOriginalResultAndConflictingReuseFailsClosed()
    {
        var runner = CreateRunner();
        var service = new EvolutionProtocolService();
        var surface = service.CaptureDecisionSurface(runner);
        var request = new Proto.ApplySpeciationRequest
        {
            ClientCommandId = "client-command-1",
            Proposal = Proposal(surface, 3),
        };

        var first = service.Apply(runner, request);
        var retry = service.Apply(runner, request.Clone());

        Assert.Equal(first, retry);
        Assert.False(first.Applied);
        Assert.Equal(1UL, first.CommandOrder);
        Assert.Equal(0UL, runner.CaptureSnapshot().WorldRevision);
        var conflict = request.Clone();
        conflict.Proposal.NewTraitIds.Clear();
        conflict.Proposal.NewTraitIds.Add(7);
        Assert.Throws<EvolutionCommandConflictException>(() =>
            service.Apply(runner, conflict));
    }

    [Fact]
    public void AffordableApplyBranchesOnceAndFollowsTheDescendant()
    {
        var runner = CreateRunner();
        var service = new EvolutionProtocolService();
        Proto.EvolutionDecisionSurface surface;
        do
        {
            runner.AdvanceOneTick();
            surface = service.CaptureDecisionSurface(runner);
        }
        while (surface.MutationBalanceQ < 40_000_000 && surface.CompletedTick < 1_000);
        var request = new Proto.ApplySpeciationRequest
        {
            ClientCommandId = "affordable-command",
            Proposal = Proposal(surface, 3),
        };

        var applied = service.Apply(runner, request);
        var retry = service.Apply(runner, request.Clone());
        var after = service.CaptureDecisionSurface(runner);

        Assert.True(applied.Applied);
        Assert.Equal(applied, retry);
        Assert.NotEqual(surface.ControlledSpeciesId, applied.DescendantSpeciesId);
        Assert.Equal(applied.DescendantSpeciesId, after.ControlledSpeciesId);
        Assert.Equal(surface.WorldRevision + 1, applied.WorldRevision);
        Assert.Equal(2, runner.CapturePublicationSnapshot().Species.Length);
    }

    private static Proto.SpeciationProposalRequest Proposal(
        Proto.EvolutionDecisionSurface surface,
        uint traitId)
    {
        var request = new Proto.SpeciationProposalRequest
        {
            WorldId = surface.WorldId,
            AncestorSpeciesId = surface.ControlledSpeciesId,
            ExpectedEvolutionRevision = surface.EvolutionRevision,
            ExpectedGenomeHash = surface.GenomeHash,
            FollowDescendantIfPermitted = true,
        };
        request.NewTraitIds.Add(traitId);
        request.SelectedTileIds.Add(surface.OccupiedTiles[0].TileId);
        return request;
    }

    private static WorldRunner CreateRunner() => WorldRunner.CreateFoundation(
        WorldId.From(1),
        FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory),
        Seed);
}

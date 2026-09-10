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
        Assert.Equal(
            [Proto.EvolutionStrategicIntent.InvestInComplexity],
            stateGated.StrategicIntents);
        var conservation = surface.Traits.Single(value => value.TraitId == 4);
        Assert.Equal(720UL, conservation.ConsequenceFollowUpHours);
        Assert.Equal(Proto.EvolutionFollowUpEvidenceKind.ConditionAndPressure,
            conservation.ConsequenceEvidenceKind);
        Assert.NotEmpty(surface.ResourceDefinitions);
        Assert.NotEmpty(surface.ReactionDefinitions);
        Assert.NotEmpty(Assert.Single(surface.OccupiedTiles).ResourceStocks);
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
        Assert.Equal(Proto.EvolutionBenefitTiming.Preparatory, preview.BenefitTiming);
        Assert.Equal(
            [Proto.EvolutionStrategicIntent.InvestInComplexity],
            preview.StrategicIntents);
        Assert.NotNull(preview.CurrentPhenotype);
        Assert.NotNull(preview.ProposedPhenotype);
        Assert.Equal(preview.CurrentPhenotype, preview.ProposedPhenotype);
        Assert.Contains(preview.ActivationWarnings, warning =>
            warning.Kind == Proto.EvolutionActivationWarningKind.NoCompiledChange &&
            warning.Severity == Proto.EvolutionActivationWarningSeverity.Caution &&
            warning.TraitIds.SequenceEqual([3U]));
        var cost = Assert.Single(preview.ProposedPhenotype.RecurringCosts);
        Assert.Equal(Proto.EvolutionRecurringCostChannel.MandatoryMaintenance, cost.Channel);
        Assert.Equal(preview.ProposedPhenotype.MaintenanceCostQPerHour,
            cost.AmountQPerOrganismHour);
        var tile = Assert.Single(preview.TileActivationEvidence);
        Assert.Equal(50U, tile.FounderCount);
        Assert.Equal(Assert.Single(surface.OccupiedTiles).HasCurrentClimate,
            tile.HasCurrentClimate);
        Assert.InRange(tile.AverageHealthQ, 1U, 1_000_000U);
        var opportunity = Assert.Single(tile.ReactionOpportunities);
        Assert.Equal(1U, opportunity.ReactionId);
        Assert.Equal(Proto.EvolutionReactionOpportunityStatus.Available, opportunity.Status);
        Assert.Equal(1U, opportunity.LimitingResourceId);
        Assert.Equal(12_500_000L, opportunity.StockSupportedExtents);
        var hydrogen = opportunity.Inputs.Single(input => input.ResourceId == 1);
        Assert.NotNull(hydrogen.FlowForecast);
        Assert.Equal(
            Proto.EvolutionResourceFlowForecastStatus.NoHistory,
            hydrogen.FlowForecast.Status);
        Assert.Equal(0U, hydrogen.FlowForecast.HistoryPeriodHours);
    }

    [Fact]
    public void PreviewComparesProposedDemandWithRecentFlowAndLatestObservedCompetition()
    {
        var runner = CreateRunner();
        var service = new EvolutionProtocolService();
        runner.AdvanceOneTick();
        var surface = service.CaptureDecisionSurface(runner);

        var preview = service.Preview(runner, Proposal(surface, 3));

        var opportunity = Assert.Single(Assert.Single(preview.TileActivationEvidence)
            .ReactionOpportunities);
        var hydrogen = opportunity.Inputs.Single(input => input.ResourceId == 1);
        var forecast = Assert.IsType<Proto.EvolutionResourceFlowForecast>(
            hydrogen.FlowForecast);
        Assert.Equal(runner.Rules.TickDurationHours, forecast.HistoryPeriodHours);
        Assert.Equal(40_000L, forecast.ProposedCohortDemandQ);
        Assert.Equal(runner.Rules.TickDurationHours, forecast.LatestObservationPeriodHours);
        Assert.InRange(
            forecast.LatestControlledSpeciesUptakeQ,
            0,
            forecast.RecentOrganismUptakeQ);
        Assert.Equal(0, forecast.LatestObservedCompetitorUptakeQ);
        var netRenewal = Math.Max(
            0,
            forecast.RecentEnvironmentalInflowQ - forecast.RecentEnvironmentalOutflowQ);
        var expectedStatus = netRenewal == 0
            ? Proto.EvolutionResourceFlowForecastStatus.NoRecentRenewal
            : forecast.ProposedCohortDemandQ > netRenewal
                ? Proto.EvolutionResourceFlowForecastStatus.ExceedsRecentRenewal
                : Proto.EvolutionResourceFlowForecastStatus.WithinRecentRenewal;
        Assert.Equal(expectedStatus, forecast.Status);
    }

    [Fact]
    public void PreviewDistinguishesConditionalBehaviorFromPreparatoryMetabolism()
    {
        var runner = CreateRunner();
        var service = new EvolutionProtocolService();
        var surface = service.CaptureDecisionSurface(runner);

        var conservation = service.Preview(runner, Proposal(surface, 3, 4));
        var organicUptake = service.Preview(runner, Proposal(surface, 7));

        Assert.Equal(Proto.EvolutionBenefitTiming.Conditional, conservation.BenefitTiming);
        Assert.False(conservation.CurrentPhenotype.ResourceConservation);
        Assert.True(conservation.ProposedPhenotype.ResourceConservation);
        Assert.Equal(720UL, conservation.ConsequenceFollowUpHours);
        Assert.Equal(Proto.EvolutionFollowUpEvidenceKind.ConditionAndPressure,
            conservation.ConsequenceEvidenceKind);
        Assert.Contains(conservation.ActivationWarnings, warning =>
            warning.Kind == Proto.EvolutionActivationWarningKind.ResourcePressureRequired &&
            warning.TraitIds.SequenceEqual([4U]));

        Assert.Equal(Proto.EvolutionBenefitTiming.Preparatory, organicUptake.BenefitTiming);
        Assert.Contains(organicUptake.ActivationWarnings, warning =>
            warning.Kind == Proto.EvolutionActivationWarningKind.NoNewActiveReaction &&
            warning.TraitIds.SequenceEqual([7U]));
        Assert.Equal(
            organicUptake.CurrentPhenotype.ActiveReactionIds,
            organicUptake.ProposedPhenotype.ActiveReactionIds);
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
        params uint[] traitIds)
    {
        var request = new Proto.SpeciationProposalRequest
        {
            WorldId = surface.WorldId,
            AncestorSpeciesId = surface.ControlledSpeciesId,
            ExpectedEvolutionRevision = surface.EvolutionRevision,
            ExpectedGenomeHash = surface.GenomeHash,
            FollowDescendantIfPermitted = true,
        };
        request.NewTraitIds.Add(traitIds);
        request.SelectedTileIds.Add(surface.OccupiedTiles[0].TileId);
        return request;
    }

    private static WorldRunner CreateRunner() => WorldRunner.CreateFoundation(
        WorldId.From(1),
        FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory),
        Seed);
}

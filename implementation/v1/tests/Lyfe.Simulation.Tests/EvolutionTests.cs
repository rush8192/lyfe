using System.Collections.Immutable;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class EvolutionTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("0123456789abcdeffedcba9876543210");

    [Fact]
    public void MutationIncomeAccruesInTheSameFixedPointUnitAsTraitPrices()
    {
        var (firstCredit, firstRemainder) = MutationIncomeMath.Accumulate(
            MutationIncomeMath.EffectivePopulationTable,
            100,
            1_000_000,
            1_000_000,
            1,
            0);
        Assert.Equal(133_333, firstCredit);
        Assert.Equal((UInt128)250_000_000_000_000, firstRemainder);

        long balance = 0;
        UInt128 remainder = 0;
        for (var hour = 0; hour < 300; hour++)
        {
            var next = MutationIncomeMath.Accumulate(
                MutationIncomeMath.EffectivePopulationTable,
                100,
                1_000_000,
                1_000_000,
                1,
                remainder);
            balance = checked(balance + next.CreditQ);
            remainder = next.Remainder;
        }
        Assert.Equal(40 * MutationIncomeMath.MutationPointScale, balance);
        Assert.Equal((UInt128)0, remainder);
    }

    [Fact]
    public void BothAuthoritiesAccumulateIncomeFromTheirOwnOpeningOutcomes()
    {
        var rules = CompileWorld(tileCount: 2);
        var founders = rules.RulePack.FounderPhenotypes
            .OrderBy(phenotype => phenotype.FounderGenomeId.Value)
            .Select(phenotype => phenotype.FounderGenomeId)
            .ToArray();
        var runner = WorldRunner.CreateGame(
            WorldId.From(1),
            rules,
            Seed,
            new GameSetupCommand(
                GameMode.Survival,
                founders[0],
                TileId.FromRowMajorIndex(0),
                100,
                founders[1],
                TileId.FromRowMajorIndex(1)));

        runner.AdvanceOneTick();

        var accounts = runner.CapturePersistenceSnapshot().State.Species;
        var first = accounts.Single(item =>
            item.Authority == (byte)EvolutionAuthorityKind.Controlled);
        var second = accounts.Single(item =>
            item.Authority == (byte)EvolutionAuthorityKind.Autonomous);
        Assert.True(first.AverageHealthQ > 0);
        Assert.True(second.AverageHealthQ > first.AverageHealthQ);
        Assert.True(first.MutationBalanceQ > 0);
        Assert.True(second.MutationBalanceQ > first.MutationBalanceQ);
    }

    [Fact]
    public void EffectivePopulationTableHasFrozenCompilerIdentityAndLandmarks()
    {
        var table = CompileWorld().RulePack.EffectivePopulationQ;

        Assert.Equal(100_001, table.Length);
        Assert.Equal(0, table[0]);
        Assert.Equal(1_435_529, table[1]);
        Assert.Equal(100_000_000, table[100]);
        Assert.Equal(345_943_162, table[1_000]);
        Assert.Equal(996_722_626, table[100_000]);
        Assert.Equal(
            "df4408bb4da12b2a5b4ea312c6bcc74258238732cb17ae49ef8b73dfb42bbf34",
            MutationIncomeMath.EffectivePopulationTableSha256);
    }

    [Fact]
    public void TraitGraphClosesPrerequisitesAndScoresCurrentResourcePressure()
    {
        var rules = CompileWorld().RulePack;
        var founder = rules.FounderPhenotypes.Single(phenotype =>
            phenotype.FounderGenomeId.Value == 1);

        var scores = AutonomousEvolutionScorer.ScoreReachable(
            rules,
            founder.AcquiredTraits,
            new EvolutionPressureState(800_000, 900_000),
            200 * MutationIncomeMath.MutationPointScale,
            founder.MaximumChangeComplexity);

        Assert.Equal(3, scores.Length);
        var conservation = scores.Single(score => score.TraitIds[^1] == TraitId.From(4));
        Assert.Equal([3U, 4U], conservation.TraitIds.Select(id => id.Value));
        Assert.Equal(100 * MutationIncomeMath.MutationPointScale, conservation.MutationPriceQ);
        Assert.Equal(2U, conservation.ChangeComplexity);
        Assert.Equal(900_000U, conservation.PressureMatchQ);
    }

    [Fact]
    public void SpeciationForksDeterministicFoundersAndPreservesPhysicalStateAndUnusedPoints()
    {
        var first = CreateRunnerWithAllocation(FounderAllocationId.From(2));
        var second = CreateRunnerWithAllocation(FounderAllocationId.From(2));
        var firstResult = Speciate(first);
        var secondResult = Speciate(second);

        Assert.True(firstResult.Preview.Accepted);
        Assert.Equal(50U, Assert.Single(firstResult.Preview.FounderCounts).Count);
        Assert.Equal(firstResult.Preview.Accepted, secondResult.Preview.Accepted);
        Assert.Equal(firstResult.Preview.Failure, secondResult.Preview.Failure);
        Assert.Equal(firstResult.Preview.MutationPriceQ, secondResult.Preview.MutationPriceQ);
        Assert.Equal(firstResult.Preview.ChangeComplexity, secondResult.Preview.ChangeComplexity);
        Assert.Equal(firstResult.Preview.FounderCounts, secondResult.Preview.FounderCounts);
        Assert.Equal(firstResult.Preview.ProposedGenomeHash, secondResult.Preview.ProposedGenomeHash);
        Assert.Equal(firstResult.StateHash, secondResult.StateHash);

        var publication = first.CapturePublicationSnapshot();
        Assert.Equal(2, publication.Species.Length);
        Assert.Equal([50UL, 50UL], publication.Species.Select(species => species.Population));
        Assert.All(publication.Species, species =>
            Assert.Equal(100 * MutationIncomeMath.MutationPointScale, species.MutationBalanceQ));
        var ancestor = publication.Species[0];
        var descendant = publication.Species[1];
        Assert.Equal(EvolutionAuthorityKind.Autonomous, ancestor.EvolutionAuthority);
        Assert.Equal(EvolutionAuthorityKind.Controlled, descendant.EvolutionAuthority);
        Assert.Equal(ancestor.SpeciesId, descendant.ParentSpeciesId);
        Assert.All(publication.Species, species =>
            Assert.Equal(FounderAllocationId.From(2), species.FounderAllocationId));
        Assert.Equal([1U, 2U, 3U, 4U, 5U], descendant.AcquiredTraits.Select(id => id.Value));
        Assert.True(first.MutableWorld.GetCompiledPhenotype(descendant.SpeciesId)
            .Physiology.Behavior.ResourceConservation);

        var persisted = first.CapturePersistenceSnapshot();
        Assert.All(persisted.State.Genomes, genome =>
            Assert.Equal(2U, genome.FounderAllocationId));
        var record = Assert.Single(persisted.State.SpeciationEvents);
        Assert.Equal(64, record.FounderSelectionDigest.Length);
        Assert.Equal(100 * MutationIncomeMath.MutationPointScale, record.DuplicatedBalanceAfterQ);
        Assert.Equal(100, persisted.State.Organisms.Length);
        Assert.Equal(100_000L, persisted.State.Organisms.Sum(value => value.StructuralMatterQ));
        Assert.Equal(500_000L, persisted.State.Organisms.Sum(value => value.ChargedReserveQ));

        var restored = WorldRunner.Restore(CompileWorld(), persisted);
        Assert.Equal(first.CaptureSnapshot(), restored.CaptureSnapshot());
        Assert.Equal(
            first.AdvanceOneTick().Snapshot,
            restored.AdvanceOneTick().Snapshot);
    }

    [Fact]
    public void SpeciationValidationRejectsMissingPrerequisitesAndImmediateRefork()
    {
        var runner = CreateRunner();
        var species = Fund(runner);
        var missing = runner.PreviewSpeciation(new SpeciationCommand(
            species.SpeciesId,
            species.EvolutionRevision,
            species.GenomeHash,
            [TraitId.From(4)],
            [TileId.FromRowMajorIndex(0)]));
        Assert.Equal(SpeciationFailure.MissingPrerequisite, missing.Failure);

        var accepted = Speciate(runner);
        var descendant = runner.CapturePublicationSnapshot().Species[1];
        var cooldown = runner.PreviewSpeciation(new SpeciationCommand(
            descendant.SpeciesId,
            descendant.EvolutionRevision,
            descendant.GenomeHash,
            [TraitId.From(3)],
            [TileId.FromRowMajorIndex(0)]));
        Assert.Equal(SpeciationFailure.SpeciationCooldown, cooldown.Failure);
        Assert.True(accepted.Preview.Accepted);
    }

    [Fact]
    public void PlayerSpeciationCreatesSavedCooldownAndAuthoredFollowUpLandmarks()
    {
        var runner = CreateRunner();
        var applied = Speciate(runner);

        Assert.True(applied.Preview.Accepted);
        var initialNotableEvents = runner.CapturePublicationSnapshot().NotableEvents;
        var speciationEvent = Assert.Single(initialNotableEvents.Where(value =>
            value.Family == NotableEventFamily.Speciation));
        Assert.Equal(applied.EventId, speciationEvent.SourceEventId);
        Assert.Equal(NotableEventSignificance.Strategic, speciationEvent.Significance);
        Assert.Equal(50UL, speciationEvent.MilestoneValue);
        Assert.Contains(initialNotableEvents, value =>
            value.Family == NotableEventFamily.FirstTileOccupation &&
            value.SpeciesId == applied.DescendantSpeciesId &&
            value.TileId == TileId.FromRowMajorIndex(0));
        Assert.Equal(initialNotableEvents.Length,
            initialNotableEvents.Select(value => value.DeduplicationKey).Distinct().Count());
        var initialAlerts = runner.CapturePublicationSnapshot().AttentionAlerts;
        Assert.Equal(2, initialAlerts.Length);
        Assert.All(initialAlerts, alert =>
            Assert.Equal(AttentionAlertClass.Strategic, alert.AlertClass));
        Assert.All(initialAlerts, alert => Assert.All(
            alert.ChronicleEventIds,
            eventId => Assert.Contains(initialNotableEvents, value => value.EventId == eventId)));
        var schedule = Assert.Single(runner.CapturePersistenceSnapshot().State.LineageReviewSchedules);
        Assert.Equal(168UL, schedule.CooldownBoundaryTick);
        Assert.Equal(720UL, schedule.FollowUpBoundaryTick);
        Assert.Equal((byte)LineageReviewEvidenceKind.ConditionAndPressure,
            schedule.FollowUpEvidenceKind);
        var scheduledCapability = Assert.Single(schedule.CapabilityActivations);
        Assert.Equal((byte)LineageReviewCapabilityKind.ResourceConservation,
            scheduledCapability.Kind);
        Assert.True(scheduledCapability.IntroducedByProposal);
        Assert.True(scheduledCapability.Installed);
        Assert.Equal(0UL, scheduledCapability.ActivationCount);
        Assert.Equal(3, schedule.ReactionActivations.Length);
        Assert.All(schedule.ReactionActivations, scheduledReaction =>
        {
            Assert.False(scheduledReaction.IntroducedByProposal);
            Assert.True(scheduledReaction.Installed);
            Assert.Equal(0UL, scheduledReaction.ActivationCount);
        });

        for (var tick = 0; tick < 168; tick++) runner.AdvanceOneTick();

        var cooldownSnapshot = runner.CapturePublicationSnapshot();
        var cooldown = Assert.Single(cooldownSnapshot.LineageReviewLandmarks);
        Assert.True(cooldown.EventId > initialNotableEvents.Max(value => value.EventId));
        Assert.Equal(LineageReviewLandmarkKind.CooldownBoundary, cooldown.Kind);
        Assert.Equal(168UL, cooldown.WindowHours);
        Assert.Equal(LineageReviewObservationScope.WorldExact,
            cooldown.PerspectiveCurrent.Scope);
        var reviewedOrganisms = cooldownSnapshot.Organisms
            .Where(organism => organism.SpeciesId == cooldown.PerspectiveSpeciesId)
            .ToArray();
        var expectedAcquisitionCoverageQ = checked((uint)(reviewedOrganisms.Aggregate(
            0UL,
            (sum, organism) => checked(sum + organism.RecentAcquisitionCoverageQ)) /
            (ulong)reviewedOrganisms.Length));
        Assert.Equal(expectedAcquisitionCoverageQ,
            cooldown.PerspectiveCurrent.AverageAcquisitionCoverageQ);
        Assert.Equal(
            reviewedOrganisms
                .GroupBy(organism => organism.BehaviorId)
                .OrderBy(group => group.Key)
                .Select(group => (Behavior: group.Key, Count: (ulong)group.LongCount())),
            cooldown.PerspectiveCurrent.BehaviorCounts
                .Select(count => (Behavior: count.BehaviorId, count.Count)));
        Assert.True(cooldown.PerspectiveCurrent.ActivityCountsAvailable);
        Assert.Equal(LineageReviewObservationScope.LiveTilesObserved,
            cooldown.ComparisonCurrent.Scope);
        Assert.False(cooldown.ComparisonCurrent.ActivityCountsAvailable);
        var capability = Assert.Single(cooldown.CapabilityActivations);
        Assert.Equal(LineageReviewCapabilityKind.ResourceConservation, capability.Kind);
        Assert.Equal(TraitId.From(4), capability.SourceTraitId);
        Assert.True(capability.IntroducedByProposal);
        Assert.True(capability.Installed);
        Assert.Equal(3, cooldown.ReactionActivations.Length);
        Assert.All(cooldown.ReactionActivations, reaction =>
        {
            Assert.False(reaction.IntroducedByProposal);
            Assert.True(reaction.Installed);
        });
        var cooldownReactionActivationCount = cooldown.ReactionActivations.Aggregate(
            0UL,
            (sum, reaction) => checked(sum + reaction.ActivationCount));
        Assert.True(cooldownReactionActivationCount > 0);
        Assert.True(cooldown.ReactionActivations.Single(reaction =>
            reaction.ReactionId == ReactionId.From(4)).ActivationCount > 0);
        Assert.Contains(cooldownSnapshot.NotableEvents, value =>
            value.Family == NotableEventFamily.FirstReactionExecution &&
            value.ReactionId == ReactionId.From(4));
        Assert.Equal(cooldownSnapshot.NotableEvents.Length,
            cooldownSnapshot.NotableEvents.Select(value => value.DeduplicationKey)
                .Distinct().Count());
        Assert.Contains(cooldownSnapshot.AttentionAlerts, alert =>
            alert.Kind == AttentionAlertKind.LineageReviewBoundary &&
            alert.ChronicleEventIds.SequenceEqual([cooldown.EventId]));
        Assert.Contains(cooldownSnapshot.AttentionAlerts, alert =>
            alert.Kind == AttentionAlertKind.NotableEventGroup &&
            alert.EventFamily == NotableEventFamily.FirstReactionExecution);
        Assert.Collection(
            cooldown.EvidenceReferences,
            reference => Assert.Equal(
                LineageReviewEvidenceReferenceKind.SpeciationDecision, reference.Kind),
            reference => Assert.Equal(
                LineageReviewEvidenceReferenceKind.ReviewedSpeciesSummary, reference.Kind),
            reference => Assert.Equal(
                LineageReviewEvidenceReferenceKind.ComparisonSpeciesSummary, reference.Kind),
            reference => Assert.Equal(
                LineageReviewEvidenceReferenceKind.ReviewedJourneyWindow, reference.Kind),
            reference =>
            {
                Assert.Equal(LineageReviewEvidenceReferenceKind.LiveTileResourceWindow,
                    reference.Kind);
                Assert.Equal(TileId.FromRowMajorIndex(0), reference.TileId);
            });

        var persisted = runner.CapturePersistenceSnapshot();
        var restored = WorldRunner.Restore(CompileWorld(), persisted);
        var restoredCooldown = Assert.Single(
            restored.CapturePublicationSnapshot().LineageReviewLandmarks);
        Assert.Equal(cooldown.EventId, restoredCooldown.EventId);
        Assert.Equal(cooldown.Kind, restoredCooldown.Kind);
        AssertLineageReviewObservationEqual(
            cooldown.PerspectiveBaseline,
            restoredCooldown.PerspectiveBaseline);
        AssertLineageReviewObservationEqual(
            cooldown.PerspectiveCurrent,
            restoredCooldown.PerspectiveCurrent);
        Assert.Equal(cooldown.EvidenceReferences, restoredCooldown.EvidenceReferences);
        Assert.Equal(
            cooldown.CapabilityActivations.ToArray(),
            restoredCooldown.CapabilityActivations.ToArray());
        Assert.Equal(
            cooldown.ReactionActivations.ToArray(),
            restoredCooldown.ReactionActivations.ToArray());
        Assert.Equal(cooldown.TraitDelta.Select(id => id.Value),
            restoredCooldown.TraitDelta.Select(id => id.Value));
        Assert.Equal(
            cooldownSnapshot.NotableEvents,
            restored.CapturePublicationSnapshot().NotableEvents);
        Assert.Equal(
            cooldownSnapshot.AttentionAlerts,
            restored.CapturePublicationSnapshot().AttentionAlerts);
        for (var tick = 168; tick < 720; tick++) restored.AdvanceOneTick();

        var landmarks = restored.CapturePublicationSnapshot().LineageReviewLandmarks;
        Assert.Equal(2, landmarks.Length);
        var followUp = landmarks[1];
        Assert.Equal(LineageReviewLandmarkKind.ProposalFollowUp, followUp.Kind);
        Assert.Equal(720UL, followUp.WindowHours);
        Assert.Equal(LineageReviewEvidenceKind.ConditionAndPressure, followUp.EvidenceKind);
        Assert.True(followUp.ReactionActivations.Aggregate(
            0UL,
            (sum, reaction) => checked(sum + reaction.ActivationCount)) >
            cooldownReactionActivationCount);
    }

    [Fact]
    public void LowPopulationAttentionUsesEntryAndRecoveryHysteresis()
    {
        var speciesId = SpeciesId.FromAllocatedValue(1);
        var state = new PopulationAttentionState(speciesId, true, true, 0);

        var first = PopulationAttentionRules.Evaluate(state, 11, 10);
        Assert.True(first.EnteredLowPopulationBand);
        Assert.False(first.State.LowPopulationArmed);
        Assert.Equal(1U, first.State.LowPopulationEpisodeOrdinal);

        var sustained = PopulationAttentionRules.Evaluate(first.State, 10, 8);
        Assert.False(sustained.EnteredLowPopulationBand);
        var partialRecovery = PopulationAttentionRules.Evaluate(sustained.State, 8, 15);
        Assert.False(partialRecovery.State.LowPopulationArmed);
        var recovered = PopulationAttentionRules.Evaluate(partialRecovery.State, 15, 16);
        Assert.True(recovered.State.LowPopulationArmed);

        var second = PopulationAttentionRules.Evaluate(recovered.State, 16, 10);
        Assert.True(second.EnteredLowPopulationBand);
        Assert.Equal(2U, second.State.LowPopulationEpisodeOrdinal);
    }

    [Fact]
    public void WindowAttentionUsesSavedDurationsAndRecoveryHysteresis()
    {
        var speciesId = SpeciesId.FromAllocatedValue(1);
        var populationState = new AttentionWindowState(
            speciesId,
            true,
            0,
            0,
            true,
            0,
            0,
            0,
            true,
            0,
            0,
            [new PopulationAttentionSample(0, 100)]);

        AttentionWindowEvaluation population = default!;
        for (ulong hour = 1; hour < 24; hour++)
        {
            population = AttentionWindowRules.Evaluate(
                populationState, hour, 1, 100, 500_000, 0);
            populationState = population.State;
            Assert.False(population.EnteredPopulationDecline);
        }
        population = AttentionWindowRules.Evaluate(
            populationState, 24, 1, 75, 500_000, 0);
        Assert.True(population.EnteredPopulationDecline);
        Assert.Equal(100UL, population.PopulationBaseline);
        Assert.Equal(250_000U, population.PopulationDeclineQ);
        Assert.Equal(1U, population.State.PopulationDeclineEpisodeOrdinal);

        population = AttentionWindowRules.Evaluate(
            population.State, 25, 1, 100, 500_000, 0);
        Assert.True(population.State.PopulationDeclineArmed);
        for (ulong hour = 26; hour <= 48; hour++)
        {
            population = AttentionWindowRules.Evaluate(
                population.State, hour, 1, 100, 500_000, 0);
        }
        population = AttentionWindowRules.Evaluate(
            population.State, 49, 1, 75, 500_000, 0);
        Assert.True(population.EnteredPopulationDecline);
        Assert.Equal(2U, population.State.PopulationDeclineEpisodeOrdinal);
        Assert.True(population.State.PopulationSamples.Length <= 25);
        Assert.True(population.State.PopulationSamples[^1].SimulatedHours -
            population.State.PopulationSamples[0].SimulatedHours <= 24);

        var healthState = new AttentionWindowState(
            speciesId,
            true,
            0,
            0,
            true,
            0,
            0,
            0,
            true,
            0,
            0,
            [new PopulationAttentionSample(0, 100)]);
        AttentionWindowEvaluation health = default!;
        for (ulong hour = 1; hour <= 5; hour++)
        {
            health = AttentionWindowRules.Evaluate(
                healthState, hour, 1, 100, 200_000, 0);
            healthState = health.State;
            Assert.False(health.EnteredSustainedLowHealth);
        }
        health = AttentionWindowRules.Evaluate(healthState, 6, 1, 100, 200_000, 0);
        Assert.True(health.EnteredSustainedLowHealth);
        Assert.Equal(6U, health.State.LowHealthConsecutiveHours);
        Assert.Equal(1U, health.State.LowHealthEpisodeOrdinal);

        for (ulong hour = 7; hour <= 12; hour++)
        {
            health = AttentionWindowRules.Evaluate(
                health.State, hour, 1, 100, 360_000, 0);
        }
        Assert.True(health.State.LowHealthArmed);
        for (ulong hour = 13; hour <= 18; hour++)
        {
            health = AttentionWindowRules.Evaluate(
                health.State, hour, 1, 100, 200_000, 0);
        }
        Assert.True(health.EnteredSustainedLowHealth);
        Assert.Equal(2U, health.State.LowHealthEpisodeOrdinal);

        var extinct = AttentionWindowRules.Evaluate(
            healthState with { LowHealthConsecutiveHours = 5 }, 6, 1, 0, 0, 0);
        Assert.False(extinct.EnteredSustainedLowHealth);
        Assert.Equal(0U, extinct.State.LowHealthConsecutiveHours);

        var pressureState = healthState;
        AttentionWindowEvaluation pressure = default!;
        for (ulong hour = 1; hour <= 6; hour++)
        {
            pressure = AttentionWindowRules.Evaluate(
                pressureState, hour, 1, 100, 500_000, 750_000);
            pressureState = pressure.State;
        }
        Assert.True(pressure.EnteredSustainedResourcePressure);
        Assert.Equal(1U, pressure.State.ResourcePressureEpisodeOrdinal);
        for (ulong hour = 7; hour <= 12; hour++)
        {
            pressure = AttentionWindowRules.Evaluate(
                pressure.State, hour, 1, 100, 500_000, 549_999);
        }
        Assert.True(pressure.State.ResourcePressureArmed);
        for (ulong hour = 13; hour <= 18; hour++)
        {
            pressure = AttentionWindowRules.Evaluate(
                pressure.State, hour, 1, 100, 500_000, 800_000);
        }
        Assert.True(pressure.EnteredSustainedResourcePressure);
        Assert.Equal(2U, pressure.State.ResourcePressureEpisodeOrdinal);
    }

    [Fact]
    public void InsufficientBalancePreviewRetainsACompletePlanningResult()
    {
        var runner = CreateRunner();
        var species = Assert.Single(runner.CapturePublicationSnapshot().Species);

        var preview = runner.PreviewSpeciation(new SpeciationCommand(
            species.SpeciesId,
            species.EvolutionRevision,
            species.GenomeHash,
            [TraitId.From(3)],
            [TileId.FromRowMajorIndex(0)]));

        Assert.False(preview.Accepted);
        Assert.Equal(SpeciationFailure.InsufficientMutationPoints, preview.Failure);
        Assert.Equal(40 * MutationIncomeMath.MutationPointScale, preview.MutationPriceQ);
        Assert.Equal(1U, preview.ChangeComplexity);
        Assert.Equal(50U, Assert.Single(preview.FounderCounts).Count);
        Assert.Equal(64, preview.ProposedGenomeHash.Length);
    }

    [Theory]
    [InlineData(1, 12)]
    [InlineData(2, 5)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    public void FounderFractionsApplyPerSelectedOccupiedTile(
        int selectedTileCount,
        uint expectedFoundersPerTile)
    {
        var runner = CreateRunner(tileCount: 4);
        var world = runner.MutableWorld;
        var organismIds = world.GetOrganismIdsInCanonicalOrder();
        var movement = world.BeginChanges();
        for (var index = 0; index < organismIds.Length; index++)
        {
            var tileIndex = checked((uint)(index / 25));
            var organism = world.GetOrganism(organismIds[index]);
            world.RelocateOrganism(
                organism.Id,
                TileId.FromRowMajorIndex(tileIndex),
                organism.PositionXQ,
                organism.PositionYQ,
                movement);
        }
        world.SealChanges(movement);

        var species = Fund(runner);
        var selectedTiles = Enumerable.Range(0, selectedTileCount)
            .Select(index => TileId.FromRowMajorIndex(checked((uint)index)))
            .ToImmutableArray();
        var preview = runner.PreviewSpeciation(new SpeciationCommand(
            species.SpeciesId,
            species.EvolutionRevision,
            species.GenomeHash,
            [TraitId.From(3)],
            selectedTiles));

        Assert.True(preview.Accepted);
        Assert.Equal(selectedTileCount, preview.FounderCounts.Length);
        Assert.All(preview.FounderCounts, count =>
            Assert.Equal(expectedFoundersPerTile, count.Count));
    }

    private static SpeciationResult Speciate(WorldRunner runner)
    {
        var species = Fund(runner);
        return runner.ApplySpeciation(new SpeciationCommand(
            species.SpeciesId,
            species.EvolutionRevision,
            species.GenomeHash,
            [TraitId.From(3), TraitId.From(4)],
            [TileId.FromRowMajorIndex(0)]));
    }

    private static Lyfe.Simulation.Publication.PublicationSpecies Fund(WorldRunner runner)
    {
        var species = Assert.Single(runner.CapturePublicationSnapshot().Species);
        runner.SetSpeciesEvolutionForTesting(
            species.SpeciesId,
            200 * MutationIncomeMath.MutationPointScale,
            EvolutionAuthorityKind.Controlled);
        return Assert.Single(runner.CapturePublicationSnapshot().Species);
    }

    private static WorldRunner CreateRunner(int tileCount = 1) => WorldRunner.CreateFoundation(
        WorldId.From(1), CompileWorld(tileCount), Seed);

    private static void AssertLineageReviewObservationEqual(
        LineageReviewObservation expected,
        LineageReviewObservation actual)
    {
        Assert.Equal(expected with { BehaviorCounts = actual.BehaviorCounts }, actual);
        Assert.Equal(expected.BehaviorCounts.ToArray(), actual.BehaviorCounts.ToArray());
    }

    private static WorldRunner CreateRunnerWithAllocation(FounderAllocationId allocation) =>
        WorldRunner.CreateGame(
            WorldId.From(1),
            CompileWorld(),
            Seed,
            new GameSetupCommand(
                GameMode.FreeSandbox,
                FounderGenomeId.From(1),
                TileId.FromRowMajorIndex(0),
                100,
                PlayerFounderAllocationId: allocation));

    private static CompiledWorldRules CompileWorld(int tileCount = 1)
    {
        var sourceRules = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(sourceRules.IsSuccess, FormatDiagnostics(sourceRules.Diagnostics));
        var compiledRules = RulePackCompiler.Compile(
            Assert.IsType<AuthoringRulePack>(sourceRules.Pack));
        Assert.True(compiledRules.IsSuccess, FormatDiagnostics(compiledRules.Diagnostics));
        var sourceWorld = WorldPackSourceLoader.Load(new CopiedPackageSource("OfficialWorld"));
        Assert.True(sourceWorld.IsSuccess, FormatDiagnostics(sourceWorld.Diagnostics));
        var authoringWorld = Assert.IsType<AuthoringWorldPack>(sourceWorld.Pack);
        if (tileCount > 1)
        {
            var profile = Assert.Single(authoringWorld.Profiles);
            var firstTile = Assert.Single(profile.Tiles!);
            authoringWorld = authoringWorld with
            {
                Profiles =
                [
                    profile with
                    {
                        Width = checked((uint)tileCount),
                        Tiles = Enumerable.Range(0, tileCount)
                            .Select(index => firstTile with { X = index })
                            .ToArray(),
                    },
                ],
            };
        }
        var compiledWorld = WorldRulesCompiler.Compile(
            Assert.IsType<CompiledRulePack>(compiledRules.RulePack),
            authoringWorld,
            "scenario.foundation-sandbox",
            "world.primordial-foundation");
        Assert.True(compiledWorld.IsSuccess, FormatDiagnostics(compiledWorld.Diagnostics));
        return Assert.IsType<CompiledWorldRules>(compiledWorld.WorldRules);
    }

    private static string FormatDiagnostics(IEnumerable<RuleDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"{diagnostic.SourceFile}: {diagnostic.Code}: {diagnostic.Message}"));

    private sealed class CopiedPackageSource : IContentSource
    {
        private readonly string root;

        public CopiedPackageSource(string directoryName) =>
            root = Path.Combine(AppContext.BaseDirectory, directoryName);

        public bool TryRead(string normalizedRelativePath, out ReadOnlyMemory<byte> content)
        {
            var path = Path.Combine(
                root,
                normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                content = default;
                return false;
            }

            content = File.ReadAllBytes(path);
            return true;
        }
    }
}

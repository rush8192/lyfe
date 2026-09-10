using System.Collections.Immutable;
using Google.Protobuf;
using Lyfe.Server.Projection;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Server.Tests;

public sealed class ProjectionProtocolMapperTests
{
    [Fact]
    public void GeneratedProjectionRoundTripsAllVisibilityShapesAndExactIntegers()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var runner = WorldRunner.CreateFoundation(
            WorldId.From(1),
            rules,
            RootRandomSeed.Parse("00112233445566778899aabbccddeeff"));
        runner.AdvanceOneTick();
        var source = runner.CapturePublicationSnapshot();
        var controlled = source.Species[0].SpeciesId;
        var direct = DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(
                controlled,
                ImmutableArray<DiscoveredTileKnowledge>.Empty));
        var live = Assert.IsType<LiveTileProjection>(direct.Tiles[0]);
        var gatedLive = live with
        {
            Organisms = live.Organisms.SetItem(
                0,
                live.Organisms[0] with
                {
                    AcquisitionGateEvidence =
                    [
                        new AcquisitionGateEvidenceProjection(
                            PublicationAcquisitionProcessKind.ExternalEnergyCapture,
                            PublicationAcquisitionGateReason.InternalCapacity,
                            0,
                            2,
                            0),
                        new AcquisitionGateEvidenceProjection(
                            PublicationAcquisitionProcessKind.Scavenging,
                            PublicationAcquisitionGateReason.CooldownActive,
                            0,
                            0,
                            direct.CompletedTick + 1),
                    ],
                    ActionGateEvidence =
                    [
                        new OrganismActionGateEvidenceProjection(
                            PublicationOrganismActionProcessKind.Reproduction,
                            PublicationOrganismActionGateReason.CooldownActive,
                            0,
                            0,
                            null,
                            direct.CompletedTick + 1),
                    ],
                }),
        };
        var expanded = direct with
        {
            Width = 3,
            Tiles =
            [
                gatedLive,
                new ReducedTileProjection(
                    TileId.FromRowMajorIndex(1),
                    1,
                    0,
                    -100,
                    direct.CompletedTick,
                    [ResourceId.From(1)]),
                new UnknownTileProjection(TileId.FromRowMajorIndex(2), 2, 0),
            ],
            NotableEvents = direct.NotableEvents.Add(new NotableEventProjection(
                10_000,
                NotableEventFamily.PopulationDeclineThreshold,
                NotableEventSignificance.Critical,
                1,
                direct.CompletedTick,
                direct.SimulatedHours,
                controlled,
                null,
                null,
                null,
                0,
                75,
                "population-decline:species:1:episode:1",
                100)).Add(new NotableEventProjection(
                    10_001,
                    NotableEventFamily.RealizedDeathMechanism,
                    NotableEventSignificance.Critical,
                    1,
                    direct.CompletedTick,
                    direct.SimulatedHours,
                    controlled,
                    null,
                    TileId.FromRowMajorIndex(0),
                    null,
                    44,
                    4,
                    "death-mechanism:species:1:cause:4",
                    0)).Add(new NotableEventProjection(
                        10_002,
                        NotableEventFamily.SustainedResourcePressure,
                        NotableEventSignificance.Strategic,
                        1,
                        direct.CompletedTick,
                        direct.SimulatedHours,
                        controlled,
                        null,
                        null,
                        null,
                        0,
                        800_000,
                        "resource-pressure:species:1:episode:1",
                        6)),
            LineageReviewLandmarks =
            [
                new LineageReviewLandmarkProjection(
                    1,
                    LineageReviewLandmarkKind.CooldownBoundary,
                    1,
                    168,
                    168,
                    1,
                    SpeciesId.From(2),
                    controlled,
                    controlled,
                    [TraitId.From(4)],
                    LineageReviewEvidenceKind.GeneralOutcomes,
                    new LineageReviewObservationProjection(
                        controlled, SpeciesPopulationScope.WorldExact,
                        50, 500_000, 500_000, 800_000, 500_000, 1,
                        [new BehaviorCountProjection(OrganismBehaviorId.Baseline, 50)],
                        false, 0, 0, 0),
                    new LineageReviewObservationProjection(
                        SpeciesId.From(2),
                        SpeciesPopulationScope.LiveTilesObserved,
                        50, 500_000, 500_000, 750_000, 500_000, 1,
                        [new BehaviorCountProjection(OrganismBehaviorId.Baseline, 50)],
                        false, 0, 0, 0),
                    new LineageReviewObservationProjection(
                        controlled, SpeciesPopulationScope.WorldExact,
                        55, 700_000, 600_000, 900_000, 400_000, 1,
                        [
                            new BehaviorCountProjection(OrganismBehaviorId.Baseline, 50),
                            new BehaviorCountProjection(OrganismBehaviorId.Foraging, 5),
                        ],
                        true, 6, 1, 2),
                    new LineageReviewObservationProjection(
                        SpeciesId.From(2),
                        SpeciesPopulationScope.LiveTilesObserved,
                        48, 650_000, 550_000, 850_000, 450_000, 1,
                        [new BehaviorCountProjection(OrganismBehaviorId.Baseline, 48)],
                        false, 0, 0, 0),
                    [
                        new LineageReviewCapabilityActivationProjection(
                            LineageReviewCapabilityKind.ResourceConservation,
                            TraitId.From(4),
                            true,
                            true,
                            3),
                    ],
                    [
                        new LineageReviewReactionActivationProjection(
                            ReactionId.From(1),
                            false,
                            true,
                            123),
                    ],
                    [
                        new LineageReviewEvidenceReferenceProjection(
                            LineageReviewEvidenceReferenceKind.SpeciationDecision,
                            null, null, 0, 1),
                        new LineageReviewEvidenceReferenceProjection(
                            LineageReviewEvidenceReferenceKind.ReviewedSpeciesSummary,
                            controlled, null, 0, 1),
                        new LineageReviewEvidenceReferenceProjection(
                            LineageReviewEvidenceReferenceKind.ComparisonSpeciesSummary,
                            SpeciesId.From(2), null, 0, 1),
                        new LineageReviewEvidenceReferenceProjection(
                            LineageReviewEvidenceReferenceKind.ReviewedJourneyWindow,
                            controlled, null, 0, 1),
                        new LineageReviewEvidenceReferenceProjection(
                            LineageReviewEvidenceReferenceKind.LiveTileResourceWindow,
                            controlled, TileId.FromRowMajorIndex(0), 0, 1),
                    ]),
            ],
        };

        var message = ProjectionProtocolMapper.ToProtocol(expanded);
        var bytes = message.ToByteArray();
        var decoded = Lyfe.Protocol.V1.ActorWorldProjection.Parser.ParseFrom(bytes);

        Assert.Equal(message, decoded);
        Assert.Equal(5, decoded.LineageReviewLandmarks[0].EvidenceReferences.Count);
        Assert.Equal(900_000U,
            decoded.LineageReviewLandmarks[0].PerspectiveCurrent
                .AverageAcquisitionCoverageQ);
        Assert.Equal(2,
            decoded.LineageReviewLandmarks[0].PerspectiveCurrent.BehaviorCounts.Count);
        Assert.Equal(Lyfe.Protocol.V1.OrganismBehavior.Foraging,
            decoded.LineageReviewLandmarks[0].PerspectiveCurrent.BehaviorCounts[1].Behavior);
        Assert.Equal(3UL,
            decoded.LineageReviewLandmarks[0].CapabilityActivations[0].ActivationCount);
        Assert.Equal(123UL,
            decoded.LineageReviewLandmarks[0].ReactionActivations[0].ActivationCount);
        Assert.NotEmpty(decoded.NotableEvents);
        Assert.All(decoded.NotableEvents, value =>
            Assert.Equal(1U, value.SignificanceRuleVersion));
        Assert.Contains(decoded.NotableEvents, value =>
            value.Family == Lyfe.Protocol.V1.NotableEventFamily.FirstReactionExecution &&
            value.HasTileId && value.HasReactionId);
        Assert.Contains(decoded.NotableEvents, value =>
            value.Family == Lyfe.Protocol.V1.NotableEventFamily.PopulationDeclineThreshold &&
            value.MilestoneValue == 75 && value.BaselineValue == 100);
        Assert.Contains(decoded.NotableEvents, value =>
            value.Family == Lyfe.Protocol.V1.NotableEventFamily.RealizedDeathMechanism &&
            value.SourceEventId == 44 && value.MilestoneValue == 4);
        Assert.Contains(decoded.NotableEvents, value =>
            value.Family == Lyfe.Protocol.V1.NotableEventFamily.SustainedResourcePressure &&
            value.MilestoneValue == 800_000 && value.BaselineValue == 6);
        Assert.NotEmpty(decoded.AttentionAlerts);
        Assert.Contains(decoded.AttentionAlerts, value =>
            value.Kind == Lyfe.Protocol.V1.AttentionAlertKind.NotableEventGroup &&
            value.HasEventFamily && value.ChronicleEventIds.Count > 0);
        Assert.True(decoded.LineageReviewLandmarks[0].EvidenceReferences[4].HasTileId);
        Assert.Equal(0U, decoded.LineageReviewLandmarks[0].EvidenceReferences[4].TileId);
        Assert.Equal(
            Lyfe.Protocol.V1.TileProjection.DetailOneofCase.Live,
            decoded.Tiles[0].DetailCase);
        Assert.Equal(
            Lyfe.Protocol.V1.TileProjection.DetailOneofCase.Reduced,
            decoded.Tiles[1].DetailCase);
        Assert.Equal(
            Lyfe.Protocol.V1.TileProjection.DetailOneofCase.Unknown,
            decoded.Tiles[2].DetailCase);
        Assert.Equal(
            typeof(ulong),
            typeof(Lyfe.Protocol.V1.ActorWorldProjection).GetProperty("WorldId")!.PropertyType);
        Assert.Equal(
            typeof(long),
            typeof(Lyfe.Protocol.V1.ExactResourceStock).GetProperty("QuantityQ")!.PropertyType);
        var decodedOrganism = decoded.Tiles[0].Live.Organisms[0];
        Assert.Equal(10_000, decodedOrganism.ChargedReserveCapacityQ);
        Assert.Equal(487_593U, decodedOrganism.RelativeHealthQ);
        Assert.Equal(504_400U, decodedOrganism.ReserveFactorQ);
        Assert.Equal(4_198_494U, decodedOrganism.BodyRadiusQ);
        Assert.Equal(Lyfe.Protocol.V1.OrganismBehavior.Baseline, decodedOrganism.Behavior);
        Assert.Equal(1_000_000U, decodedOrganism.RecentEnergyCoverageQ);
        Assert.NotEmpty(decodedOrganism.ResourceAcquisitionEvidence);
        var hydrogenEvidence = decodedOrganism.ResourceAcquisitionEvidence.Single(value =>
            value.ResourceId == 1);
        Assert.Equal(800, hydrogenEvidence.RequestedQ);
        Assert.Equal(800, hydrogenEvidence.GrantedQ);
        Assert.False(hydrogenEvidence.TileSupplyConstrained);
        Assert.False(hydrogenEvidence.ClaimContentionConstrained);
        Assert.Equal(2, decodedOrganism.AcquisitionGateEvidence.Count);
        var gateEvidence = decodedOrganism.AcquisitionGateEvidence[0];
        Assert.Equal(
            Lyfe.Protocol.V1.AcquisitionProcess.ExternalEnergyCapture,
            gateEvidence.Process);
        Assert.Equal(
            Lyfe.Protocol.V1.AcquisitionGateReason.InternalCapacity,
            gateEvidence.Reason);
        Assert.Equal(0, gateEvidence.AvailableQ);
        Assert.Equal(2, gateEvidence.RequiredQ);
        Assert.Equal(0UL, gateEvidence.ClearsAtTick);
        var cooldownGate = decodedOrganism.AcquisitionGateEvidence[1];
        Assert.Equal(Lyfe.Protocol.V1.AcquisitionProcess.Scavenging,
            cooldownGate.Process);
        Assert.Equal(Lyfe.Protocol.V1.AcquisitionGateReason.CooldownActive,
            cooldownGate.Reason);
        Assert.Equal(direct.CompletedTick + 1, cooldownGate.ClearsAtTick);
        var actionGate = Assert.Single(decodedOrganism.ActionGateEvidence);
        Assert.Equal(Lyfe.Protocol.V1.OrganismActionProcess.Reproduction,
            actionGate.Process);
        Assert.Equal(Lyfe.Protocol.V1.OrganismActionGateReason.CooldownActive,
            actionGate.Reason);
        Assert.Equal(direct.CompletedTick + 1, actionGate.ClearsAtTick);
        Assert.Single(decoded.Tiles[0].Live.BehaviorDistributions);
        Assert.Equal(source.ResourceFlowPeriodHours,
            decoded.Tiles[0].Live.ResourceFlowPeriodHours);
        Assert.NotEmpty(decoded.Tiles[0].Live.ResourceFlows);
        var resourceHistory = Assert.Single(decoded.Tiles[0].Live.ResourceFlowHistory);
        Assert.Equal(source.CompletedTick, resourceHistory.CompletedTick);
        Assert.Equal(source.SimulatedHours, resourceHistory.EndSimulatedHour);
        Assert.NotEmpty(resourceHistory.ResourceFlows);
        Assert.NotEmpty(decoded.ReactionDefinitions);
        Assert.NotEmpty(decoded.Tiles[0].Live.ResourceFlowContributors);
        Assert.Contains(decoded.Tiles[0].Live.ResourceFlowContributors,
            value => value.ReactionId != 0 && value.SpeciesId == direct.ControlledSpeciesId.Value);
        Assert.Equal(source.ResourceDefinitions.Length, decoded.ResourceDefinitions.Count);
        Assert.Equal("Hydrogen gas", decoded.ResourceDefinitions.Single(value =>
            value.ResourceId == 1).DisplayName);
        Assert.Equal(Lyfe.Protocol.V1.ResourceBiologicalForm.Inorganic,
            decoded.ResourceDefinitions.Single(value => value.ResourceId == 1).BiologicalForm);
        Assert.Equal(Lyfe.Protocol.V1.ResourceEnvironmentalPhase.Gas,
            decoded.ResourceDefinitions.Single(value => value.ResourceId == 1).EnvironmentalPhase);
        Assert.Equal(100UL,
            decoded.Tiles[0].Live.BehaviorDistributions[0].TotalObservedOrganisms);
        Assert.Equal(100UL, decoded.Species[0].BehaviorCounts[0].Count);
        Assert.Equal(Lyfe.Protocol.V1.GameMode.FreeSandbox, decoded.Gameplay.Mode);
        Assert.Equal(Lyfe.Protocol.V1.GameRunStatus.Active, decoded.Gameplay.RunStatus);
        Assert.Equal(controlled.Value, decoded.Gameplay.ControlledSpeciesId);
        Assert.Single(decoded.Gameplay.Roots);
        Assert.Equal(1U, decoded.Gameplay.Roots[0].FounderAllocationId);
        Assert.Equal(1U, decoded.Species[0].Evolution.FounderGenomeId);
        Assert.Equal(1U, decoded.Species[0].Evolution.FounderAllocationId);
        Assert.Empty(decoded.Tiles[0].Live.Remnants);
        Assert.Equal(100, decoded.JourneyEvents.Count(value =>
            value.Family == Lyfe.Protocol.V1.OrganismJourneyEventFamily.Birth));
        Assert.All(decoded.JourneyEvents.Where(value => value.Tick == 0), value => Assert.Equal(
            Lyfe.Protocol.V1.OrganismJourneyEventFamily.Birth,
            value.Family));
        var review = Assert.Single(decoded.LineageReviewLandmarks);
        Assert.Equal(Lyfe.Protocol.V1.LineageReviewLandmarkKind.CooldownBoundary,
            review.Kind);
        Assert.Equal(Lyfe.Protocol.V1.SpeciesPopulationScope.WorldExact,
            review.PerspectiveCurrent.PopulationScope);
        Assert.Equal(6UL, review.PerspectiveCurrent.BirthCount);
        Assert.False(review.ComparisonCurrent.ActivityCountsAvailable);
        Assert.DoesNotContain(
            System.Text.Encoding.UTF8.GetString(bytes),
            runner.CaptureSnapshot().StateHash,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedProjectionRoundTripsLiveRemnants()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var runner = WorldRunner.CreateFoundation(
            WorldId.From(2),
            rules,
            RootRandomSeed.Parse("00112233445566778899aabbccddeeff"));
        var source = runner.CapturePublicationSnapshot();
        do
        {
            runner.AdvanceOneTick();
            source = runner.CapturePublicationSnapshot();
        }
        while (source.Remnants.IsEmpty && source.CompletedTick < 2_000);

        Assert.NotEmpty(source.Remnants);
        Assert.NotEmpty(source.Organisms);
        var controlled = Assert.Single(source.Species).SpeciesId;
        var projection = DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(controlled, []));
        var decoded = Lyfe.Protocol.V1.ActorWorldProjection.Parser.ParseFrom(
            ProjectionProtocolMapper.ToProtocol(projection).ToByteArray());

        var live = decoded.Tiles.Single(tile => tile.DetailCase ==
            Lyfe.Protocol.V1.TileProjection.DetailOneofCase.Live).Live;
        Assert.Equal(source.Remnants.Length, live.Remnants.Count);
        Assert.Equal(source.Remnants[0].RemnantId.Value, live.Remnants[0].RemnantId);
        Assert.Equal(source.Remnants[0].StructuralMatterQ,
            live.Remnants[0].StructuralMatterQ);
        Assert.Equal(source.Remnants[0].BodyRadiusQ, live.Remnants[0].BodyRadiusQ);
    }

    [Fact]
    public void SuccessiveAuthoritativeProjectionsProduceOneAbsoluteBatch()
    {
        var rules = FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory);
        var runner = WorldRunner.CreateFoundation(
            WorldId.From(5),
            rules,
            RootRandomSeed.Parse("00112233445566778899aabbccddeeff"));
        var source = runner.CapturePublicationSnapshot();
        var controlled = source.Species[0].SpeciesId;
        var knowledge = new ActorKnowledgeSnapshot(
            controlled,
            ImmutableArray<DiscoveredTileKnowledge>.Empty);
        var initial = DirectWorldProjector.Project(source, knowledge);
        var stream = ProjectionDeltaBuilder.CreateSnapshot(91, 1, initial);

        runner.AdvanceOneTick();
        var current = DirectWorldProjector.Project(
            runner.CapturePublicationSnapshot(),
            knowledge);
        var delta = ProjectionDeltaBuilder.CreateDelta(stream, current);
        var protocol = ProjectionProtocolMapper.ToProtocol(delta);
        var decoded = Lyfe.Protocol.V1.ProjectionBatch.Parser.ParseFrom(
            protocol.ToByteArray());

        Assert.Equal(1UL, delta.BaseStreamRevision);
        Assert.Equal(2UL, delta.TargetStreamRevision);
        Assert.Single(delta.TileReplacements);
        Assert.Empty(delta.RemovedTileIds);
        var speciesReplacement = Assert.Single(delta.SpeciesReplacements);
        Assert.Equal(controlled, speciesReplacement.SpeciesId);
        Assert.NotNull(speciesReplacement.Evolution);
        Assert.Equal(1UL, speciesReplacement.Evolution.EvolutionRevision);
        Assert.Empty(delta.RemovedSpeciesIds);
        Assert.Equal(controlled.Value, decoded.Gameplay.ControlledSpeciesId);
        Assert.Equal(1UL, decoded.Gameplay.GameplayRevision);
        Assert.Equal(protocol, decoded);
        Assert.Equal(100, decoded.TileReplacements[0].Live.Organisms.Count);
        Assert.Empty(decoded.JourneyEventAppends);
        Assert.NotEmpty(decoded.RoutineActivitySummaries);
        Assert.NotEmpty(decoded.ActivityPulseEvents);
        Assert.NotEmpty(decoded.NotableEventAppends);
        Assert.NotEmpty(decoded.AttentionAlertAppends);
        Assert.All(decoded.AttentionAlertAppends, alert => Assert.All(
            alert.ChronicleEventIds,
            eventId => Assert.Contains(decoded.NotableEventAppends,
                notable => notable.EventId == eventId)));
        Assert.All(decoded.ActivityPulseEvents, value => Assert.Equal(1UL, value.Tick));
        Assert.All(
            decoded.TileReplacements[0].Live.Organisms,
            organism => Assert.Equal(1UL, organism.BiologicalAgeHours));
    }
}

using Domain = Lyfe.Server.Projection;
using Proto = Lyfe.Protocol.V1;

namespace Lyfe.Server.Projection;

public static class ProjectionProtocolMapper
{
    public static Proto.ProjectionSnapshot ToProtocol(ProjectionStreamSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new Proto.ProjectionSnapshot
        {
            ProjectionStreamId = source.ProjectionStreamId,
            StreamRevision = source.StreamRevision,
            Projection = ToProtocol(source.Projection),
        };
    }

    public static Proto.ProjectionBatch ToProtocol(ProjectionDelta source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var boundary = source.TargetBoundary;
        var result = new Proto.ProjectionBatch
        {
            ProjectionStreamId = source.ProjectionStreamId,
            WorldId = boundary.WorldId.Value,
            WorldRulesHash = boundary.WorldRulesHash,
            BaseStreamRevision = source.BaseStreamRevision,
            TargetStreamRevision = source.TargetStreamRevision,
            FromExclusiveTick = source.FromExclusiveTick,
            ThroughCompletedTick = boundary.CompletedTick,
            WorldRevision = boundary.WorldRevision,
            SimulatedHours = boundary.SimulatedHours,
            Lifecycle = boundary.Lifecycle switch
            {
                Simulation.Publication.PublicationWorldLifecycle.PausedReady =>
                    Proto.WorldLifecycle.PausedReady,
                Simulation.Publication.PublicationWorldLifecycle.Running =>
                    Proto.WorldLifecycle.Running,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported world lifecycle."),
            },
            Gameplay = ToProtocol(boundary.Gameplay),
        };
        result.TileReplacements.Add(source.TileReplacements.Select(ToProtocol));
        result.RemovedTileIds.Add(source.RemovedTileIds.Select(id => id.Value));
        result.SpeciesReplacements.Add(source.SpeciesReplacements.Select(ToProtocol));
        result.RemovedSpeciesIds.Add(source.RemovedSpeciesIds.Select(id => id.Value));
        result.JourneyEventAppends.Add(source.JourneyEventAppends.Select(ToProtocol));
        result.RoutineActivitySummaries.Add(
            source.RoutineActivitySummaries.Select(ToProtocol));
        result.ActivityPulseEvents.Add(source.ActivityPulseEvents.Select(ToProtocol));
        result.LineageReviewLandmarkAppends.Add(
            source.LineageReviewLandmarkAppends.Select(ToProtocol));
        result.NotableEventAppends.Add(source.NotableEventAppends.Select(ToProtocol));
        result.AttentionAlertAppends.Add(source.AttentionAlertAppends.Select(ToProtocol));
        return result;
    }

    private static Proto.ResourceDefinition ToProtocol(
        Domain.ResourceDefinitionProjection source) => new()
        {
            ResourceId = source.ResourceId.Value,
            StableKey = source.StableKey,
            DisplayName = source.DisplayName,
            BiologicalForm = source.BiologicalForm switch
            {
                Simulation.Rules.Runtime.BiologicalForm.Inorganic =>
                    Proto.ResourceBiologicalForm.Inorganic,
                Simulation.Rules.Runtime.BiologicalForm.Organic =>
                    Proto.ResourceBiologicalForm.Organic,
                Simulation.Rules.Runtime.BiologicalForm.Boundary =>
                    Proto.ResourceBiologicalForm.Boundary,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
            EnvironmentalPhase = source.EnvironmentalPhase switch
            {
                Simulation.Rules.Runtime.EnvironmentalPhase.Gas =>
                    Proto.ResourceEnvironmentalPhase.Gas,
                Simulation.Rules.Runtime.EnvironmentalPhase.Dissolved =>
                    Proto.ResourceEnvironmentalPhase.Dissolved,
                Simulation.Rules.Runtime.EnvironmentalPhase.Boundary =>
                    Proto.ResourceEnvironmentalPhase.Boundary,
                Simulation.Rules.Runtime.EnvironmentalPhase.Particulate =>
                    Proto.ResourceEnvironmentalPhase.Particulate,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
        };

    public static Proto.ActorWorldProjection ToProtocol(Domain.ActorWorldProjection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new Proto.ActorWorldProjection
        {
            WorldId = source.WorldId.Value,
            CompletedTick = source.CompletedTick,
            WorldRevision = source.WorldRevision,
            SimulatedHours = source.SimulatedHours,
            TickDurationHours = source.TickDurationHours,
            Lifecycle = source.Lifecycle switch
            {
                Simulation.Publication.PublicationWorldLifecycle.PausedReady =>
                    Proto.WorldLifecycle.PausedReady,
                Simulation.Publication.PublicationWorldLifecycle.Running =>
                    Proto.WorldLifecycle.Running,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported world lifecycle."),
            },
            WorldRulesHash = source.WorldRulesHash,
            Width = source.Width,
            Height = source.Height,
            WrapX = source.WrapX,
            WrapY = source.WrapY,
            ControlledSpeciesId = source.ControlledSpeciesId.Value,
            Gameplay = ToProtocol(source.Gameplay),
        };
        result.Tiles.Add(source.Tiles.Select(ToProtocol));
        result.Species.Add(source.Species.Select(ToProtocol));
        result.JourneyEvents.Add(source.JourneyEvents.Select(ToProtocol));
        result.RoutineActivitySummaries.Add(
            source.RoutineActivitySummaries.Select(ToProtocol));
        result.ActivityPulseEvents.Add(source.ActivityPulseEvents.Select(ToProtocol));
        result.ResourceDefinitions.Add(source.ResourceDefinitions.Select(ToProtocol));
        result.ReactionDefinitions.Add(source.ReactionDefinitions.Select(value =>
            new Proto.ReactionDefinition
            {
                ReactionId = value.ReactionId.Value,
                StableKey = value.StableKey,
                DisplayName = value.DisplayName,
            }));
        result.LineageReviewLandmarks.Add(source.LineageReviewLandmarks.Select(ToProtocol));
        result.NotableEvents.Add(source.NotableEvents.Select(ToProtocol));
        result.AttentionAlerts.Add(source.AttentionAlerts.Select(ToProtocol));
        return result;
    }

    private static Proto.AttentionAlert ToProtocol(Domain.AttentionAlertProjection source)
    {
        var result = new Proto.AttentionAlert
        {
            AlertId = source.AlertId,
            AlertClass = source.AlertClass switch
            {
                Simulation.Gameplay.AttentionAlertClass.Informational => Proto.AttentionAlertClass.Informational,
                Simulation.Gameplay.AttentionAlertClass.Strategic => Proto.AttentionAlertClass.Strategic,
                Simulation.Gameplay.AttentionAlertClass.Critical => Proto.AttentionAlertClass.Critical,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
            Kind = source.Kind switch
            {
                Simulation.Gameplay.AttentionAlertKind.NotableEventGroup => Proto.AttentionAlertKind.NotableEventGroup,
                Simulation.Gameplay.AttentionAlertKind.LineageReviewBoundary => Proto.AttentionAlertKind.LineageReviewBoundary,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
            CompletedTick = source.CompletedTick,
            SimulatedHours = source.SimulatedHours,
            SpeciesId = source.SpeciesId.Value,
            DeduplicationKey = source.DeduplicationKey,
        };
        if (source.EventFamily.HasValue)
        {
            result.EventFamily = ToProtocol(source.EventFamily.Value);
        }
        result.ChronicleEventIds.Add(source.ChronicleEventIds);
        return result;
    }

    private static Proto.NotableEventFamily ToProtocol(
        Simulation.Gameplay.NotableEventFamily value) => value switch
        {
            Simulation.Gameplay.NotableEventFamily.Speciation => Proto.NotableEventFamily.Speciation,
            Simulation.Gameplay.NotableEventFamily.FirstReproduction => Proto.NotableEventFamily.FirstReproduction,
            Simulation.Gameplay.NotableEventFamily.PopulationMilestone => Proto.NotableEventFamily.PopulationMilestone,
            Simulation.Gameplay.NotableEventFamily.FirstTileOccupation => Proto.NotableEventFamily.FirstTileOccupation,
            Simulation.Gameplay.NotableEventFamily.FirstReactionExecution => Proto.NotableEventFamily.FirstReactionExecution,
            Simulation.Gameplay.NotableEventFamily.SpeciesExtinction => Proto.NotableEventFamily.SpeciesExtinction,
            Simulation.Gameplay.NotableEventFamily.PopulationDangerThreshold => Proto.NotableEventFamily.PopulationDangerThreshold,
            Simulation.Gameplay.NotableEventFamily.PopulationDeclineThreshold => Proto.NotableEventFamily.PopulationDeclineThreshold,
            Simulation.Gameplay.NotableEventFamily.SustainedLowHealth => Proto.NotableEventFamily.SustainedLowHealth,
            Simulation.Gameplay.NotableEventFamily.RealizedDeathMechanism => Proto.NotableEventFamily.RealizedDeathMechanism,
            Simulation.Gameplay.NotableEventFamily.SustainedResourcePressure => Proto.NotableEventFamily.SustainedResourcePressure,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static Proto.NotableEvent ToProtocol(Domain.NotableEventProjection source)
    {
        var result = new Proto.NotableEvent
        {
            EventId = source.EventId,
            Family = ToProtocol(source.Family),
            Significance = source.Significance switch
            {
                Simulation.Gameplay.NotableEventSignificance.Informational => Proto.NotableEventSignificance.Informational,
                Simulation.Gameplay.NotableEventSignificance.Strategic => Proto.NotableEventSignificance.Strategic,
                Simulation.Gameplay.NotableEventSignificance.Critical => Proto.NotableEventSignificance.Critical,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
            SignificanceRuleVersion = source.SignificanceRuleVersion,
            CompletedTick = source.CompletedTick,
            SimulatedHours = source.SimulatedHours,
            SpeciesId = source.SpeciesId.Value,
            SourceEventId = source.SourceEventId,
            MilestoneValue = source.MilestoneValue,
            DeduplicationKey = source.DeduplicationKey,
            BaselineValue = source.BaselineValue,
        };
        if (source.RelatedSpeciesId.HasValue)
            result.RelatedSpeciesId = source.RelatedSpeciesId.Value.Value;
        if (source.TileId.HasValue) result.TileId = source.TileId.Value.Value;
        if (source.ReactionId.HasValue) result.ReactionId = source.ReactionId.Value.Value;
        return result;
    }

    private static Proto.LineageReviewLandmark ToProtocol(
        Domain.LineageReviewLandmarkProjection source)
    {
        var result = new Proto.LineageReviewLandmark
        {
            EventId = source.EventId,
            Kind = source.Kind switch
            {
                Simulation.Gameplay.LineageReviewLandmarkKind.CooldownBoundary =>
                    Proto.LineageReviewLandmarkKind.CooldownBoundary,
                Simulation.Gameplay.LineageReviewLandmarkKind.ProposalFollowUp =>
                    Proto.LineageReviewLandmarkKind.ProposalFollowUp,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
            CompletedTick = source.CompletedTick,
            SimulatedHours = source.SimulatedHours,
            WindowHours = source.WindowHours,
            SpeciationEventId = source.SpeciationEventId,
            AncestorSpeciesId = source.AncestorSpeciesId.Value,
            DescendantSpeciesId = source.DescendantSpeciesId.Value,
            PerspectiveSpeciesId = source.PerspectiveSpeciesId.Value,
            EvidenceKind = source.EvidenceKind switch
            {
                Simulation.Gameplay.LineageReviewEvidenceKind.GeneralOutcomes =>
                    Proto.LineageReviewEvidenceKind.GeneralOutcomes,
                Simulation.Gameplay.LineageReviewEvidenceKind.CapabilityActivation =>
                    Proto.LineageReviewEvidenceKind.CapabilityActivation,
                Simulation.Gameplay.LineageReviewEvidenceKind.ConditionAndPressure =>
                    Proto.LineageReviewEvidenceKind.ConditionAndPressure,
                Simulation.Gameplay.LineageReviewEvidenceKind.GeographicSpread =>
                    Proto.LineageReviewEvidenceKind.GeographicSpread,
                Simulation.Gameplay.LineageReviewEvidenceKind.ReserveStorage =>
                    Proto.LineageReviewEvidenceKind.ReserveStorage,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
            PerspectiveBaseline = ToProtocol(source.PerspectiveBaseline),
            ComparisonBaseline = ToProtocol(source.ComparisonBaseline),
            PerspectiveCurrent = ToProtocol(source.PerspectiveCurrent),
            ComparisonCurrent = ToProtocol(source.ComparisonCurrent),
        };
        result.TraitIds.Add(source.TraitDelta.Select(id => id.Value));
        result.CapabilityActivations.Add(source.CapabilityActivations.Select(ToProtocol));
        result.ReactionActivations.Add(source.ReactionActivations.Select(ToProtocol));
        result.EvidenceReferences.Add(source.EvidenceReferences.Select(ToProtocol));
        return result;
    }

    private static Proto.LineageReviewCapabilityActivation ToProtocol(
        Domain.LineageReviewCapabilityActivationProjection source) => new()
        {
            Kind = source.Kind switch
            {
                Simulation.Gameplay.LineageReviewCapabilityKind.ResourceConservation =>
                    Proto.LineageReviewCapabilityKind.ResourceConservation,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
            SourceTraitId = source.SourceTraitId.Value,
            IntroducedByProposal = source.IntroducedByProposal,
            Installed = source.Installed,
            ActivationCount = source.ActivationCount,
        };

    private static Proto.LineageReviewReactionActivation ToProtocol(
        Domain.LineageReviewReactionActivationProjection source) => new()
        {
            ReactionId = source.ReactionId.Value,
            IntroducedByProposal = source.IntroducedByProposal,
            Installed = source.Installed,
            ActivationCount = source.ActivationCount,
        };

    private static Proto.LineageReviewEvidenceReference ToProtocol(
        Domain.LineageReviewEvidenceReferenceProjection source)
    {
        var result = new Proto.LineageReviewEvidenceReference
        {
            Kind = source.Kind switch
            {
                Simulation.Gameplay.LineageReviewEvidenceReferenceKind.SpeciationDecision =>
                    Proto.LineageReviewEvidenceReferenceKind.SpeciationDecision,
                Simulation.Gameplay.LineageReviewEvidenceReferenceKind.ReviewedSpeciesSummary =>
                    Proto.LineageReviewEvidenceReferenceKind.ReviewedSpeciesSummary,
                Simulation.Gameplay.LineageReviewEvidenceReferenceKind.ComparisonSpeciesSummary =>
                    Proto.LineageReviewEvidenceReferenceKind.ComparisonSpeciesSummary,
                Simulation.Gameplay.LineageReviewEvidenceReferenceKind.ReviewedJourneyWindow =>
                    Proto.LineageReviewEvidenceReferenceKind.ReviewedJourneyWindow,
                Simulation.Gameplay.LineageReviewEvidenceReferenceKind.LiveTileResourceWindow =>
                    Proto.LineageReviewEvidenceReferenceKind.LiveTileResourceWindow,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
            FromExclusiveTick = source.FromExclusiveTick,
            ThroughCompletedTick = source.ThroughCompletedTick,
        };
        if (source.SpeciesId.HasValue)
        {
            result.SpeciesId = source.SpeciesId.Value.Value;
        }
        if (source.TileId.HasValue)
        {
            result.TileId = source.TileId.Value.Value;
        }
        return result;
    }

    private static Proto.LineageReviewObservation ToProtocol(
        Domain.LineageReviewObservationProjection source)
    {
        var result = new Proto.LineageReviewObservation
        {
            SpeciesId = source.SpeciesId.Value,
            PopulationScope = source.PopulationScope switch
            {
                Domain.SpeciesPopulationScope.WorldExact =>
                    Proto.SpeciesPopulationScope.WorldExact,
                Domain.SpeciesPopulationScope.LiveTilesObserved =>
                    Proto.SpeciesPopulationScope.LiveTilesObserved,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
            Population = source.Population,
            AverageHealthQ = source.AverageHealthQ,
            AverageReserveQ = source.AverageReserveQ,
            AverageAcquisitionCoverageQ = source.AverageAcquisitionCoverageQ,
            AverageResourcePressureQ = source.AverageResourcePressureQ,
            OccupiedTileCount = source.OccupiedTileCount,
            ActivityCountsAvailable = source.ActivityCountsAvailable,
            BirthCount = source.BirthCount,
            DeathCount = source.DeathCount,
            MigrationCount = source.MigrationCount,
        };
        result.BehaviorCounts.Add(source.BehaviorCounts.Select(ToProtocol));
        return result;
    }

    private static Proto.OrganismJourneyEvent ToProtocol(
        Domain.OrganismJourneyEventProjection source)
    {
        var result = new Proto.OrganismJourneyEvent
        {
            EventId = source.EventId,
            Tick = source.Tick,
            Phase = (uint)source.Phase,
            Family = source.Family switch
            {
                Simulation.Gameplay.OrganismJourneyEventFamily.Birth =>
                    Proto.OrganismJourneyEventFamily.Birth,
                Simulation.Gameplay.OrganismJourneyEventFamily.Reproduction =>
                    Proto.OrganismJourneyEventFamily.Reproduction,
                Simulation.Gameplay.OrganismJourneyEventFamily.ResourceAbsorption =>
                    Proto.OrganismJourneyEventFamily.ResourceAbsorption,
                Simulation.Gameplay.OrganismJourneyEventFamily.Feeding =>
                    Proto.OrganismJourneyEventFamily.Feeding,
                Simulation.Gameplay.OrganismJourneyEventFamily.Migration =>
                    Proto.OrganismJourneyEventFamily.Migration,
                Simulation.Gameplay.OrganismJourneyEventFamily.Death =>
                    Proto.OrganismJourneyEventFamily.Death,
                Simulation.Gameplay.OrganismJourneyEventFamily.Stress =>
                    Proto.OrganismJourneyEventFamily.Stress,
                Simulation.Gameplay.OrganismJourneyEventFamily.BehaviorTransition =>
                    Proto.OrganismJourneyEventFamily.BehaviorTransition,
                _ => throw new ArgumentOutOfRangeException(nameof(source)),
            },
            SubjectOrganismId = source.SubjectOrganismId.Value,
            SubjectSpeciesId = source.SubjectSpeciesId.Value,
            TileId = source.TileId.Value,
            PositionXQ = source.PositionXQ,
            PositionYQ = source.PositionYQ,
            RelatedOrganismId = source.RelatedOrganismId?.Value ?? 0,
            RelatedRemnantId = source.RelatedRemnantId?.Value ?? 0,
            ResourceId = source.ResourceId?.Value ?? 0,
            AmountQ = source.AmountQ,
            DetailId = source.DetailId,
        };
        result.DeathCauseProbabilities.Add(source.DeathCauseProbabilities.Select(cause =>
            new Proto.JourneyDeathCauseProbability
            {
                Cause = (uint)cause.Cause,
                ProbabilityQ = cause.ProbabilityQ,
                Triggered = cause.Triggered,
            }));
        return result;
    }

    private static Proto.OrganismRoutineActivitySummary ToProtocol(
        Domain.OrganismRoutineActivitySummaryProjection source)
    {
        var result = new Proto.OrganismRoutineActivitySummary
        {
            BucketStartHour = source.BucketStartHour,
            PeriodHours = source.PeriodHours,
            SubjectOrganismId = source.SubjectOrganismId.Value,
            SubjectSpeciesId = source.SubjectSpeciesId.Value,
            TileId = source.TileId.Value,
        };
        result.ResourceAcquisitions.Add(source.ResourceAcquisitions.Select(resource =>
            new Proto.RoutineResourceAcquisition
            {
                ResourceId = resource.ResourceId.Value,
                AmountQ = resource.AmountQ,
            }));
        return result;
    }

    private static Proto.GameProjection ToProtocol(Domain.GameProjection source)
    {
        var result = new Proto.GameProjection
        {
            Mode = source.Mode switch
            {
                Simulation.Gameplay.GameMode.FreeSandbox => Proto.GameMode.FreeSandbox,
                Simulation.Gameplay.GameMode.Survival => Proto.GameMode.Survival,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported game mode."),
            },
            RunStatus = source.RunStatus switch
            {
                Simulation.Gameplay.GameRunStatus.Active => Proto.GameRunStatus.Active,
                Simulation.Gameplay.GameRunStatus.Lost => Proto.GameRunStatus.Lost,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported game status."),
            },
            LossReason = source.LossReason switch
            {
                Simulation.Gameplay.GameLossReason.None => Proto.GameLossReason.Unspecified,
                Simulation.Gameplay.GameLossReason.AllLifeExtinct => Proto.GameLossReason.AllLifeExtinct,
                Simulation.Gameplay.GameLossReason.ControlledSpeciesExtinct =>
                    Proto.GameLossReason.ControlledSpeciesExtinct,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported game loss reason."),
            },
            ControlledSpeciesId = source.ControlledSpeciesId.Value,
            GameplayRevision = source.GameplayRevision,
            Ended = source.EndedTick.HasValue,
            EndedTick = source.EndedTick ?? 0,
        };
        result.Roots.Add(source.Roots.Select(root => new Proto.AbiogenesisRoot
        {
            SpeciesId = root.SpeciesId.Value,
            FounderGenomeId = root.FounderGenomeId.Value,
            FounderAllocationId = root.FounderAllocationId.Value,
            StartingTileId = root.StartingTileId.Value,
            InitialPopulation = root.InitialPopulation,
            PlayerSelected = root.PlayerSelected,
        }));
        result.MutationLockedSpeciesIds.Add(
            source.MutationLockedSpeciesIds.Select(id => id.Value));
        return result;
    }

    private static Proto.TileProjection ToProtocol(Domain.TileProjection source)
    {
        var result = new Proto.TileProjection
        {
            TileId = source.TileId.Value,
            X = source.X,
            Y = source.Y,
        };
        switch (source)
        {
            case Domain.UnknownTileProjection:
                result.Unknown = new Proto.UnknownTile();
                break;
            case Domain.ReducedTileProjection reduced:
                result.Reduced = new Proto.ReducedTile
                {
                    ElevationMeters = reduced.ElevationMeters,
                    ObservedAtTick = reduced.ObservedAtTick,
                };
                result.Reduced.KnownPresentResourceIds.Add(
                    reduced.KnownPresentResourceIds.Select(id => id.Value));
                break;
            case Domain.LiveTileProjection live:
                result.Live = new Proto.LiveTile
                {
                    ElevationMeters = live.ElevationMeters,
                    ObservedAtTick = live.ObservedAtTick,
                    ResourceFlowPeriodHours = live.ResourceFlowPeriodHours,
                    BaselineVolcanismQ = live.BaselineVolcanismQ,
                };
                result.Live.ResourceStocks.Add(live.ResourceStocks.Select(stock =>
                    new Proto.ExactResourceStock
                    {
                        ResourceId = stock.ResourceId.Value,
                        QuantityQ = stock.QuantityQ,
                    }));
                result.Live.Organisms.Add(live.Organisms.Select(ToProtocol));
                result.Live.Remnants.Add(live.Remnants.Select(ToProtocol));
                result.Live.BehaviorDistributions.Add(
                    live.BehaviorDistributions.Select(ToProtocol));
                result.Live.ResourceFlows.Add(live.ResourceFlows.Select(flow =>
                    new Proto.ResourceFlow
                    {
                        ResourceId = flow.ResourceId.Value,
                        Kind = flow.Kind switch
                        {
                            Simulation.Publication.PublicationResourceFlowKind.EnvironmentalSource =>
                                Proto.ResourceFlowKind.EnvironmentalSource,
                            Simulation.Publication.PublicationResourceFlowKind.EnvironmentalSink =>
                                Proto.ResourceFlowKind.EnvironmentalSink,
                            Simulation.Publication.PublicationResourceFlowKind.NeighborExchangeIn =>
                                Proto.ResourceFlowKind.NeighborExchangeIn,
                            Simulation.Publication.PublicationResourceFlowKind.NeighborExchangeOut =>
                                Proto.ResourceFlowKind.NeighborExchangeOut,
                            Simulation.Publication.PublicationResourceFlowKind.OrganismUptake =>
                                Proto.ResourceFlowKind.OrganismUptake,
                            Simulation.Publication.PublicationResourceFlowKind.OrganismRelease =>
                                Proto.ResourceFlowKind.OrganismRelease,
                            _ => throw new ArgumentOutOfRangeException(nameof(source)),
                        },
                        AmountQ = flow.AmountQ,
                    }));
                result.Live.ResourceFlowHistory.Add(live.ResourceFlowHistory.Select(interval =>
                {
                    var encoded = new Proto.ResourceFlowHistoryInterval
                    {
                        CompletedTick = interval.CompletedTick,
                        EndSimulatedHour = interval.EndSimulatedHour,
                        PeriodHours = interval.PeriodHours,
                    };
                    encoded.ResourceFlows.Add(interval.ResourceFlows.Select(flow =>
                        new Proto.ResourceFlow
                        {
                            ResourceId = flow.ResourceId.Value,
                            Kind = ToProtocol(flow.Kind),
                            AmountQ = flow.AmountQ,
                        }));
                    return encoded;
                }));
                result.Live.ResourceFlowContributors.Add(
                    live.ResourceFlowContributors.Select(value =>
                        new Proto.ResourceFlowContributor
                        {
                            ResourceId = value.ResourceId.Value,
                            Kind = ToProtocol(value.Kind),
                            Process = ToProtocol(value.Process),
                            ReactionId = value.ReactionId?.Value ?? 0,
                            SpeciesId = value.SpeciesId?.Value ?? 0,
                            AmountQ = value.AmountQ,
                        }));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), "Unsupported tile projection.");
        }

        return result;
    }

    private static Proto.OrganismProjection ToProtocol(Domain.OrganismProjection source)
    {
        var result = new Proto.OrganismProjection
        {
            OrganismId = source.OrganismId.Value,
            SpeciesId = source.SpeciesId.Value,
            PositionXQ = source.PositionXQ,
            PositionYQ = source.PositionYQ,
            BodyRadiusQ = source.BodyRadiusQ,
            VelocityXQPerHour = source.VelocityXQPerHour,
            VelocityYQPerHour = source.VelocityYQPerHour,
            BirthTick = source.BirthTick,
            BiologicalAgeHours = source.BiologicalAgeHours,
            LifecyclePhase = source.LifecyclePhase switch
            {
                Simulation.Publication.PublicationLifecyclePhase.Mature =>
                    Proto.OrganismLifecyclePhase.Mature,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported organism lifecycle."),
            },
            StructuralMatterQ = source.StructuralMatterQ,
            ChargedReserveQ = source.ChargedReserveQ,
            ChargedReserveCapacityQ = source.ChargedReserveCapacityQ,
            RelativeHealthQ = source.RelativeHealthQ,
            ReserveFactorQ = source.ReserveFactorQ,
            StructureFactorQ = source.StructureFactorQ,
            AgeFactorQ = source.AgeFactorQ,
            EnvironmentalFactorQ = source.EnvironmentalFactorQ,
            ReproductionNotBeforeTick = source.ReproductionNotBeforeTick,
            SuccessfulReproductionCount = source.SuccessfulReproductionCount,
            ScavengeNotBeforeTick = source.ScavengeNotBeforeTick,
            IngestedStructuralMatterQ = source.IngestedStructuralMatterQ,
            Behavior = ToProtocol(source.BehaviorId),
            BehaviorTargetKind = ToProtocol(source.BehaviorTargetKind),
            BehaviorTargetId = source.BehaviorTargetId,
            BehaviorTargetPositionXQ = source.BehaviorTargetPositionXQ,
            BehaviorTargetPositionYQ = source.BehaviorTargetPositionYQ,
            BehaviorSelectedAtTick = source.BehaviorSelectedAtTick,
            BehaviorMinimumDwellUntilTick = source.BehaviorMinimumDwellUntilTick,
            RecentEnergyCoverageQ = source.RecentEnergyCoverageQ,
            RecentAcquisitionCoverageQ = source.RecentAcquisitionCoverageQ,
            LimitingMaterialDeficitQ = source.LimitingMaterialDeficitQ,
            ResourcePressureQ = source.ResourcePressureQ,
        };

        result.CommittedMicronutrients.Add(source.CommittedMicronutrients.Select(
            ToProtocolResource));
        result.FreeMicronutrients.Add(source.FreeMicronutrients.Select(
            ToProtocolResource));
        result.ResourceAcquisitionEvidence.Add(source.ResourceAcquisitionEvidence.Select(value =>
            new Proto.ResourceAcquisitionEvidence
            {
                ResourceId = value.ResourceId.Value,
                RequestedQ = value.RequestedQ,
                GrantedQ = value.GrantedQ,
                TileSupplyConstrained = value.TileSupplyConstrained,
                ClaimContentionConstrained = value.ClaimContentionConstrained,
            }));
        result.AcquisitionGateEvidence.Add(source.AcquisitionGateEvidence.Select(value =>
            new Proto.AcquisitionGateEvidence
            {
                Process = value.Process switch
                {
                    Simulation.Publication.PublicationAcquisitionProcessKind.ExternalEnergyCapture =>
                        Proto.AcquisitionProcess.ExternalEnergyCapture,
                    Simulation.Publication.PublicationAcquisitionProcessKind.Scavenging =>
                        Proto.AcquisitionProcess.Scavenging,
                    _ => throw new ArgumentOutOfRangeException(nameof(source)),
                },
                Reason = value.Reason switch
                {
                    Simulation.Publication.PublicationAcquisitionGateReason.MissingCapability =>
                        Proto.AcquisitionGateReason.MissingCapability,
                    Simulation.Publication.PublicationAcquisitionGateReason.InaccessibleLight =>
                        Proto.AcquisitionGateReason.InaccessibleLight,
                    Simulation.Publication.PublicationAcquisitionGateReason.EnvironmentalOpportunity =>
                        Proto.AcquisitionGateReason.EnvironmentalOpportunity,
                    Simulation.Publication.PublicationAcquisitionGateReason.InternalCapacity =>
                        Proto.AcquisitionGateReason.InternalCapacity,
                    Simulation.Publication.PublicationAcquisitionGateReason.CooldownActive =>
                        Proto.AcquisitionGateReason.CooldownActive,
                    Simulation.Publication.PublicationAcquisitionGateReason.InsufficientActionEnergy =>
                        Proto.AcquisitionGateReason.InsufficientActionEnergy,
                    _ => throw new ArgumentOutOfRangeException(nameof(source)),
                },
                AvailableQ = value.AvailableQ,
                RequiredQ = value.RequiredQ,
                ClearsAtTick = value.ClearsAtTick,
            }));
        result.ActionGateEvidence.Add(source.ActionGateEvidence.Select(value =>
            new Proto.OrganismActionGateEvidence
            {
                Process = value.Process switch
                {
                    Simulation.Publication.PublicationOrganismActionProcessKind.BiomassGrowth =>
                        Proto.OrganismActionProcess.BiomassGrowth,
                    Simulation.Publication.PublicationOrganismActionProcessKind.Reproduction =>
                        Proto.OrganismActionProcess.Reproduction,
                    _ => throw new ArgumentOutOfRangeException(nameof(source)),
                },
                Reason = value.Reason switch
                {
                    Simulation.Publication.PublicationOrganismActionGateReason.MissingCapability =>
                        Proto.OrganismActionGateReason.MissingCapability,
                    Simulation.Publication.PublicationOrganismActionGateReason.BehaviorSuppressed =>
                        Proto.OrganismActionGateReason.BehaviorSuppressed,
                    Simulation.Publication.PublicationOrganismActionGateReason.CooldownActive =>
                        Proto.OrganismActionGateReason.CooldownActive,
                    Simulation.Publication.PublicationOrganismActionGateReason.HealthBelowMinimum =>
                        Proto.OrganismActionGateReason.HealthBelowMinimum,
                    Simulation.Publication.PublicationOrganismActionGateReason.StructureBelowMinimum =>
                        Proto.OrganismActionGateReason.StructureBelowMinimum,
                    Simulation.Publication.PublicationOrganismActionGateReason.ReserveBelowMinimum =>
                        Proto.OrganismActionGateReason.ReserveBelowMinimum,
                    Simulation.Publication.PublicationOrganismActionGateReason
                        .ConstitutiveMicronutrientQuotaMissing =>
                        Proto.OrganismActionGateReason.ConstitutiveMicronutrientQuotaMissing,
                    Simulation.Publication.PublicationOrganismActionGateReason
                        .OffspringMicronutrientQuotaMissing =>
                        Proto.OrganismActionGateReason.OffspringMicronutrientQuotaMissing,
                    Simulation.Publication.PublicationOrganismActionGateReason.MaintenanceShortfall =>
                        Proto.OrganismActionGateReason.MaintenanceShortfall,
                    Simulation.Publication.PublicationOrganismActionGateReason
                        .ReserveProtectionFloor =>
                        Proto.OrganismActionGateReason.ReserveProtectionFloor,
                    Simulation.Publication.PublicationOrganismActionGateReason.InternalCapacity =>
                        Proto.OrganismActionGateReason.InternalCapacity,
                    Simulation.Publication.PublicationOrganismActionGateReason.ResourceSupply =>
                        Proto.OrganismActionGateReason.ResourceSupply,
                    Simulation.Publication.PublicationOrganismActionGateReason.ClaimContention =>
                        Proto.OrganismActionGateReason.ClaimContention,
                    Simulation.Publication.PublicationOrganismActionGateReason.LifecycleIneligible =>
                        Proto.OrganismActionGateReason.LifecycleIneligible,
                    _ => throw new ArgumentOutOfRangeException(nameof(source)),
                },
                AvailableQ = value.AvailableQ,
                RequiredQ = value.RequiredQ,
                ResourceId = value.ResourceId?.Value ?? 0,
                ClearsAtTick = value.ClearsAtTick,
            }));
        return result;
    }

    private static Proto.RemnantProjection ToProtocol(Domain.RemnantProjection source)
    {
        var result = new Proto.RemnantProjection
        {
            RemnantId = source.RemnantId.Value,
            SourceOrganismId = source.SourceOrganismId.Value,
            SourceSpeciesId = source.SourceSpeciesId.Value,
            PositionXQ = source.PositionXQ,
            PositionYQ = source.PositionYQ,
            BodyRadiusQ = source.BodyRadiusQ,
            CreatedTick = source.CreatedTick,
            StructuralMatterQ = source.StructuralMatterQ,
            ChargedReserveQ = source.ChargedReserveQ,
        };
        result.Micronutrients.Add(source.Micronutrients.Select(ToProtocolResource));
        return result;
    }

    private static Proto.ExactResourceStock ToProtocolResource(
        Domain.ExactResourceStockProjection source) => new()
        {
            ResourceId = source.ResourceId.Value,
            QuantityQ = source.QuantityQ,
        };

    private static Proto.ResourceFlowKind ToProtocol(
        Simulation.Publication.PublicationResourceFlowKind kind) => kind switch
        {
            Simulation.Publication.PublicationResourceFlowKind.EnvironmentalSource =>
                Proto.ResourceFlowKind.EnvironmentalSource,
            Simulation.Publication.PublicationResourceFlowKind.EnvironmentalSink =>
                Proto.ResourceFlowKind.EnvironmentalSink,
            Simulation.Publication.PublicationResourceFlowKind.NeighborExchangeIn =>
                Proto.ResourceFlowKind.NeighborExchangeIn,
            Simulation.Publication.PublicationResourceFlowKind.NeighborExchangeOut =>
                Proto.ResourceFlowKind.NeighborExchangeOut,
            Simulation.Publication.PublicationResourceFlowKind.OrganismUptake =>
                Proto.ResourceFlowKind.OrganismUptake,
            Simulation.Publication.PublicationResourceFlowKind.OrganismRelease =>
                Proto.ResourceFlowKind.OrganismRelease,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static Proto.ResourceFlowProcess ToProtocol(
        Simulation.Publication.PublicationResourceFlowProcessKind process) => process switch
        {
            Simulation.Publication.PublicationResourceFlowProcessKind.ExternalEnergyCapture =>
                Proto.ResourceFlowProcess.ExternalEnergyCapture,
            Simulation.Publication.PublicationResourceFlowProcessKind.ParticulateDigestion =>
                Proto.ResourceFlowProcess.ParticulateDigestion,
            Simulation.Publication.PublicationResourceFlowProcessKind.EnvironmentalGasSource =>
                Proto.ResourceFlowProcess.EnvironmentalGasSource,
            Simulation.Publication.PublicationResourceFlowProcessKind.EnvironmentalGasSink =>
                Proto.ResourceFlowProcess.EnvironmentalGasSink,
            Simulation.Publication.PublicationResourceFlowProcessKind.EnvironmentalGasExchange =>
                Proto.ResourceFlowProcess.EnvironmentalGasExchange,
            Simulation.Publication.PublicationResourceFlowProcessKind.MandatoryMaintenance =>
                Proto.ResourceFlowProcess.MandatoryMaintenance,
            Simulation.Publication.PublicationResourceFlowProcessKind.BiomassAssembly =>
                Proto.ResourceFlowProcess.BiomassAssembly,
            Simulation.Publication.PublicationResourceFlowProcessKind.MicronutrientUptake =>
                Proto.ResourceFlowProcess.MicronutrientUptake,
            _ => throw new ArgumentOutOfRangeException(nameof(process)),
        };

    private static Proto.SpeciesProjection ToProtocol(Domain.SpeciesProjection source)
    {
        var result = new Proto.SpeciesProjection
        {
            SpeciesId = source.SpeciesId.Value,
            PopulationScope = source.PopulationScope switch
            {
                Domain.SpeciesPopulationScope.WorldExact =>
                    Proto.SpeciesPopulationScope.WorldExact,
                Domain.SpeciesPopulationScope.LiveTilesObserved =>
                    Proto.SpeciesPopulationScope.LiveTilesObserved,
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported population scope."),
            },
            Population = source.Population,
        };
        result.BehaviorCounts.Add(source.BehaviorCounts.Select(ToProtocol));
        if (source.Evolution is not null)
        {
            result.Evolution = new Proto.SpeciesEvolution
            {
                GenomeId = source.Evolution.GenomeId.Value,
                FounderGenomeId = source.Evolution.FounderGenomeId.Value,
                FounderAllocationId = source.Evolution.FounderAllocationId.Value,
                GenomeHash = source.Evolution.GenomeHash,
                MutationBalanceQ = checked((ulong)source.Evolution.MutationBalanceQ),
                EvolutionRevision = source.Evolution.EvolutionRevision,
                SpeciationNotBeforeTick = source.Evolution.SpeciationNotBeforeTick,
                AverageHealthQ = source.Evolution.AverageHealthQ,
                LastMutationIncomeQ = checked((ulong)source.Evolution.LastMutationIncomeQ),
                MutationIncomeModifierQ = source.Evolution.MutationIncomeModifierQ,
                ParentSpeciesId = source.Evolution.ParentSpeciesId?.Value ?? 0,
                CreatedTick = source.Evolution.CreatedTick,
                Extinct = source.Evolution.ExtinctTick.HasValue,
                ExtinctTick = source.Evolution.ExtinctTick ?? 0,
            };
            result.Evolution.AcquiredTraitIds.Add(
                source.Evolution.AcquiredTraits.Select(trait => trait.Value));
        }
        return result;
    }

    private static Proto.BehaviorDistribution ToProtocol(
        Domain.BehaviorDistributionProjection source)
    {
        var result = new Proto.BehaviorDistribution
        {
            SpeciesId = source.SpeciesId.Value,
            ObservedAtTick = source.ObservedAtTick,
            TotalObservedOrganisms = source.TotalObservedOrganisms,
        };
        result.Counts.Add(source.Counts.Select(ToProtocol));
        return result;
    }

    private static Proto.BehaviorCount ToProtocol(Domain.BehaviorCountProjection source) =>
        new()
        {
            Behavior = ToProtocol(source.BehaviorId),
            Count = source.Count,
        };

    private static Proto.OrganismBehavior ToProtocol(
        Simulation.Behavior.OrganismBehaviorId behavior) => behavior switch
        {
            Simulation.Behavior.OrganismBehaviorId.Baseline => Proto.OrganismBehavior.Baseline,
            Simulation.Behavior.OrganismBehaviorId.Conserving => Proto.OrganismBehavior.Conserving,
            Simulation.Behavior.OrganismBehaviorId.Foraging => Proto.OrganismBehavior.Foraging,
            Simulation.Behavior.OrganismBehaviorId.Dispersing => Proto.OrganismBehavior.Dispersing,
            Simulation.Behavior.OrganismBehaviorId.Fleeing => Proto.OrganismBehavior.Fleeing,
            _ => throw new ArgumentOutOfRangeException(nameof(behavior)),
        };

    private static Proto.BehaviorTargetKind ToProtocol(
        Simulation.Behavior.BehaviorTargetKind targetKind) => targetKind switch
        {
            Simulation.Behavior.BehaviorTargetKind.None => Proto.BehaviorTargetKind.Unspecified,
            Simulation.Behavior.BehaviorTargetKind.Organism => Proto.BehaviorTargetKind.Organism,
            Simulation.Behavior.BehaviorTargetKind.Remnant => Proto.BehaviorTargetKind.Remnant,
            Simulation.Behavior.BehaviorTargetKind.Edge => Proto.BehaviorTargetKind.Edge,
            Simulation.Behavior.BehaviorTargetKind.LocalPoint => Proto.BehaviorTargetKind.LocalPoint,
            _ => throw new ArgumentOutOfRangeException(nameof(targetKind)),
        };
}

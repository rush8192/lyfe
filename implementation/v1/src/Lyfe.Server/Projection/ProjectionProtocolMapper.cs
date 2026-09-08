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
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Unsupported world lifecycle."),
            },
            Gameplay = ToProtocol(boundary.Gameplay),
        };
        result.TileReplacements.Add(source.TileReplacements.Select(ToProtocol));
        result.RemovedTileIds.Add(source.RemovedTileIds.Select(id => id.Value));
        result.SpeciesReplacements.Add(source.SpeciesReplacements.Select(ToProtocol));
        result.RemovedSpeciesIds.Add(source.RemovedSpeciesIds.Select(id => id.Value));
        result.JourneyEventAppends.Add(source.JourneyEventAppends.Select(ToProtocol));
        return result;
    }

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

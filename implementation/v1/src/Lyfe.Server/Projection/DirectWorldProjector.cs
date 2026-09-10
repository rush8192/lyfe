using System.Collections.Immutable;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Server.Projection;

public static class DirectWorldProjector
{
    public static ActorWorldProjection Project(
        WorldPublicationSnapshot source,
        ActorKnowledgeSnapshot knowledge)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(knowledge);

        var sourceIndex = ValidateAndIndex(source);
        var discovered = ValidateAndIndex(knowledge, source, sourceIndex);
        var liveTileIds = source.Organisms
            .Where(organism => organism.SpeciesId == knowledge.ControlledSpeciesId)
            .Select(organism => organism.TileId)
            .ToHashSet();
        var organismsByTile = source.Organisms
            .GroupBy(organism => organism.TileId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(organism => organism.OrganismId.Value)
                    .ToImmutableArray());
        var remnantsByTile = source.Remnants
            .GroupBy(remnant => remnant.TileId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(remnant => remnant.RemnantId.Value).ToImmutableArray());
        var flowsByTile = source.ResourceFlows
            .GroupBy(flow => flow.TileId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(flow => flow.ResourceId.Value)
                    .ThenBy(flow => flow.Kind)
                    .ToImmutableArray());
        var contributorsByTile = source.ResourceFlowContributors
            .GroupBy(value => value.TileId)
            .ToDictionary(group => group.Key, group => group.ToImmutableArray());
        var tiles = source.Tiles
            .OrderBy(tile => tile.TileId.Value)
            .Select(tile => ProjectTile(
                tile,
                source.CompletedTick,
                knowledge.ControlledSpeciesId,
                liveTileIds,
                discovered,
                organismsByTile,
                remnantsByTile,
                flowsByTile,
                contributorsByTile,
                source.ResourceFlowPeriodHours,
                source.ResourceFlowHistory))
            .ToImmutableArray();
        var species = ProjectSpecies(
            source,
            knowledge.ControlledSpeciesId,
            liveTileIds,
            sourceIndex.Species);
        var gameplay = new GameProjection(
            source.Gameplay.Mode,
            source.Gameplay.RunStatus,
            source.Gameplay.LossReason,
            source.Gameplay.ControlledSpeciesId,
            source.Gameplay.GameplayRevision,
            source.Gameplay.EndedTick,
            source.Gameplay.Roots
                .OrderBy(root => root.SpeciesId.Value)
                .Select(root => new AbiogenesisRootProjection(
                    root.SpeciesId,
                    root.FounderGenomeId,
                    root.FounderAllocationId,
                    root.StartingTileId,
                    root.InitialPopulation,
                    root.PlayerSelected)).ToImmutableArray(),
            source.Gameplay.MutationLockedSpeciesIds
                .OrderBy(id => id.Value)
                .ToImmutableArray());
        var journeyEvents = source.JourneyEvents
            .Where(value => value.SubjectSpeciesId == knowledge.ControlledSpeciesId)
            .OrderBy(value => value.EventId)
            .Select(ToProjection)
            .ToImmutableArray();
        var routineActivitySummaries = source.RoutineActivitySummaries
            .Where(value => value.SubjectSpeciesId == knowledge.ControlledSpeciesId)
            .Select(value => new OrganismRoutineActivitySummaryProjection(
                value.BucketStartHour,
                value.PeriodHours,
                value.SubjectOrganismId,
                value.SubjectSpeciesId,
                value.TileId,
                value.ResourceAcquisitions.Select(resource =>
                    new RoutineResourceAcquisitionProjection(
                        resource.ResourceId,
                        resource.AmountQ)).ToImmutableArray()))
            .ToImmutableArray();
        var activityPulseEvents = source.ActivityPulseEvents
            .Where(value => value.SubjectSpeciesId == knowledge.ControlledSpeciesId)
            .OrderBy(value => value.EventId)
            .Select(ToProjection)
            .ToImmutableArray();

        return new ActorWorldProjection(
            source.WorldId,
            source.CompletedTick,
            source.WorldRevision,
            source.SimulatedHours,
            source.TickDurationHours,
            source.Lifecycle,
            source.WorldRulesHash,
            source.Width,
            source.Height,
            source.WrapX,
            source.WrapY,
            knowledge.ControlledSpeciesId,
            gameplay,
            tiles,
            species,
            journeyEvents,
            routineActivitySummaries,
            activityPulseEvents,
            source.ResourceDefinitions
                .OrderBy(resource => resource.ResourceId.Value)
                .Select(resource => new ResourceDefinitionProjection(
                    resource.ResourceId,
                    resource.StableKey,
                    resource.DisplayName,
                    resource.BiologicalForm,
                    resource.EnvironmentalPhase))
                .ToImmutableArray(),
            source.ReactionDefinitions
                .OrderBy(reaction => reaction.ReactionId.Value)
                .Select(reaction => new ReactionDefinitionProjection(
                    reaction.ReactionId,
                    reaction.StableKey,
                    reaction.DisplayName))
                .ToImmutableArray(),
            (source.LineageReviewLandmarks.IsDefault
                ? []
                : source.LineageReviewLandmarks)
                .OrderBy(value => value.EventId)
                .Select(ToProjection)
                .ToImmutableArray(),
            (source.NotableEvents.IsDefault ? [] : source.NotableEvents)
                .Where(value => value.SpeciesId == knowledge.ControlledSpeciesId ||
                    value.RelatedSpeciesId == knowledge.ControlledSpeciesId)
                .OrderBy(value => value.EventId)
                .Select(ToProjection)
                .ToImmutableArray(),
            (source.AttentionAlerts.IsDefault ? [] : source.AttentionAlerts)
                .Where(value => value.SpeciesId == knowledge.ControlledSpeciesId)
                .OrderBy(value => value.AlertId)
                .Select(ToProjection)
                .ToImmutableArray());
    }

    private static NotableEventProjection ToProjection(NotableEvent value) => new(
        value.EventId,
        value.Family,
        value.Significance,
        value.SignificanceRuleVersion,
        value.CompletedTick,
        value.SimulatedHours,
        value.SpeciesId,
        value.RelatedSpeciesId,
        value.TileId,
        value.ReactionId,
        value.SourceEventId,
        value.MilestoneValue,
        value.DeduplicationKey,
        value.BaselineValue);

    private static AttentionAlertProjection ToProjection(AttentionAlert value) => new(
        value.AlertId,
        value.AlertClass,
        value.Kind,
        value.EventFamily,
        value.CompletedTick,
        value.SimulatedHours,
        value.SpeciesId,
        value.ChronicleEventIds,
        value.DeduplicationKey);

    private static LineageReviewLandmarkProjection ToProjection(
        LineageReviewLandmark value) => new(
            value.EventId,
            value.Kind,
            value.CompletedTick,
            value.SimulatedHours,
            value.WindowHours,
            value.SpeciationEventId,
            value.AncestorSpeciesId,
            value.DescendantSpeciesId,
            value.PerspectiveSpeciesId,
            value.TraitDelta,
            value.EvidenceKind,
            ToProjection(value.PerspectiveBaseline),
            ToProjection(value.ComparisonBaseline),
            ToProjection(value.PerspectiveCurrent),
            ToProjection(value.ComparisonCurrent),
            value.CapabilityActivations.Select(activation =>
                new LineageReviewCapabilityActivationProjection(
                    activation.Kind,
                    activation.SourceTraitId,
                    activation.IntroducedByProposal,
                    activation.Installed,
                    activation.ActivationCount)).ToImmutableArray(),
            value.ReactionActivations.Select(activation =>
                new LineageReviewReactionActivationProjection(
                    activation.ReactionId,
                    activation.IntroducedByProposal,
                    activation.Installed,
                    activation.ActivationCount)).ToImmutableArray(),
            value.EvidenceReferences.Select(reference =>
                new LineageReviewEvidenceReferenceProjection(
                    reference.Kind,
                    reference.SpeciesId,
                    reference.TileId,
                    reference.FromExclusiveTick,
                    reference.ThroughCompletedTick)).ToImmutableArray());

    private static LineageReviewObservationProjection ToProjection(
        LineageReviewObservation value) => new(
            value.SpeciesId,
            value.Scope switch
            {
                LineageReviewObservationScope.WorldExact => SpeciesPopulationScope.WorldExact,
                LineageReviewObservationScope.LiveTilesObserved =>
                    SpeciesPopulationScope.LiveTilesObserved,
                _ => throw new ArgumentOutOfRangeException(nameof(value)),
            },
            value.Population,
            value.AverageHealthQ,
            value.AverageReserveQ,
            value.AverageAcquisitionCoverageQ,
            value.AverageResourcePressureQ,
            value.OccupiedTileCount,
            value.BehaviorCounts.Select(count => new BehaviorCountProjection(
                count.BehaviorId,
                count.Count)).ToImmutableArray(),
            value.ActivityCountsAvailable,
            value.BirthCount,
            value.DeathCount,
            value.MigrationCount);

    private static TileProjection ProjectTile(
        PublicationTile tile,
        ulong completedTick,
        SpeciesId controlledSpeciesId,
        HashSet<TileId> liveTileIds,
        Dictionary<TileId, DiscoveredTileKnowledge> discovered,
        Dictionary<TileId, ImmutableArray<PublicationOrganism>> organismsByTile,
        Dictionary<TileId, ImmutableArray<PublicationRemnant>> remnantsByTile,
        Dictionary<TileId, ImmutableArray<PublicationTileResourceFlow>> flowsByTile,
        Dictionary<TileId, ImmutableArray<PublicationTileResourceFlowContributor>> contributorsByTile,
        uint resourceFlowPeriodHours,
        ImmutableArray<PublicationResourceFlowHistoryInterval> resourceFlowHistory)
    {
        if (liveTileIds.Contains(tile.TileId))
        {
            var organisms = organismsByTile[tile.TileId];
            return new LiveTileProjection(
                tile.TileId,
                tile.X,
                tile.Y,
                tile.ElevationMeters,
                completedTick,
                tile.ResourceStocks
                    .OrderBy(stock => stock.ResourceId.Value)
                    .Select(stock => new ExactResourceStockProjection(
                        stock.ResourceId,
                        stock.QuantityQ))
                    .ToImmutableArray(),
                organisms.Select(ToProjection).ToImmutableArray(),
                remnantsByTile.GetValueOrDefault(tile.TileId, [])
                    .Select(ToProjection)
                    .ToImmutableArray(),
                BuildBehaviorDistributions(organisms, completedTick),
                resourceFlowPeriodHours,
                flowsByTile.GetValueOrDefault(tile.TileId, [])
                    .Select(flow => new ResourceFlowProjection(
                        flow.ResourceId,
                        flow.Kind,
                        flow.AmountQ))
                    .ToImmutableArray(),
                resourceFlowHistory.Select(interval =>
                    new ResourceFlowHistoryIntervalProjection(
                        interval.CompletedTick,
                        interval.EndSimulatedHour,
                        interval.PeriodHours,
                        interval.ResourceFlows
                            .Where(flow => flow.TileId == tile.TileId)
                            .Select(flow => new ResourceFlowProjection(
                                flow.ResourceId,
                                flow.Kind,
                                flow.AmountQ))
                            .ToImmutableArray()))
                    .ToImmutableArray(),
                ProjectResourceFlowContributors(
                    contributorsByTile.GetValueOrDefault(tile.TileId, []),
                    organisms,
                    controlledSpeciesId));
        }

        if (discovered.TryGetValue(tile.TileId, out var memory))
        {
            return new ReducedTileProjection(
                tile.TileId,
                tile.X,
                tile.Y,
                tile.ElevationMeters,
                memory.ObservedAtTick,
                memory.KnownPresentResourceIds
                    .OrderBy(id => id.Value)
                    .ToImmutableArray());
        }

        return new UnknownTileProjection(tile.TileId, tile.X, tile.Y);
    }

    private static ImmutableArray<ResourceFlowContributorProjection>
        ProjectResourceFlowContributors(
            ImmutableArray<PublicationTileResourceFlowContributor> contributors,
            ImmutableArray<PublicationOrganism> visibleOrganisms,
            SpeciesId controlledSpeciesId)
    {
        var visibleSpecies = visibleOrganisms
            .Select(organism => organism.SpeciesId)
            .Append(controlledSpeciesId)
            .ToHashSet();
        return contributors
            .Select(value => value with
            {
                SpeciesId = value.SpeciesId is { } speciesId && visibleSpecies.Contains(speciesId)
                    ? speciesId
                    : null,
            })
            .GroupBy(value => (
                value.ResourceId,
                value.Kind,
                value.Process,
                value.ReactionId,
                value.SpeciesId))
            .OrderBy(group => group.Key.ResourceId.Value)
            .ThenBy(group => group.Key.Kind)
            .ThenBy(group => group.Key.Process)
            .ThenBy(group => group.Key.ReactionId?.Value ?? 0)
            .ThenBy(group => group.Key.SpeciesId?.Value ?? 0)
            .Select(group => new ResourceFlowContributorProjection(
                group.Key.ResourceId,
                group.Key.Kind,
                group.Key.Process,
                group.Key.ReactionId,
                group.Key.SpeciesId,
                group.Sum(value => value.AmountQ)))
            .ToImmutableArray();
    }

    private static ImmutableArray<SpeciesProjection> ProjectSpecies(
        WorldPublicationSnapshot source,
        SpeciesId controlledSpeciesId,
        HashSet<TileId> liveTileIds,
        Dictionary<SpeciesId, PublicationSpecies> speciesById)
    {
        var observedPopulation = source.Organisms
            .Where(organism => liveTileIds.Contains(organism.TileId))
            .GroupBy(organism => organism.SpeciesId)
            .ToDictionary(
                group => group.Key,
                group => checked((ulong)group.LongCount()));
        var visibleSpecies = observedPopulation.Keys
            .Append(controlledSpeciesId)
            .Distinct()
            .OrderBy(id => id.Value);
        var result = ImmutableArray.CreateBuilder<SpeciesProjection>();
        foreach (var speciesId in visibleSpecies)
        {
            if (speciesId == controlledSpeciesId)
            {
                result.Add(new SpeciesProjection(
                    speciesId,
                    SpeciesPopulationScope.WorldExact,
                    speciesById[speciesId].Population,
                    BuildBehaviorCounts(source.Organisms.Where(organism =>
                        organism.SpeciesId == speciesId)),
                    ToEvolutionProjection(speciesById[speciesId])));
            }
            else
            {
                result.Add(new SpeciesProjection(
                    speciesId,
                    SpeciesPopulationScope.LiveTilesObserved,
                    observedPopulation[speciesId],
                    BuildBehaviorCounts(source.Organisms.Where(organism =>
                        organism.SpeciesId == speciesId && liveTileIds.Contains(organism.TileId)))));
            }
        }

        return result.ToImmutable();
    }

    private static SpeciesEvolutionProjection ToEvolutionProjection(PublicationSpecies value) =>
        new(
            value.GenomeId,
            value.FounderGenomeId,
            value.FounderAllocationId,
            value.GenomeHash,
            value.AcquiredTraits,
            value.MutationBalanceQ,
            value.EvolutionRevision,
            value.SpeciationNotBeforeTick,
            value.AverageHealthQ,
            value.LastMutationIncomeQ,
            value.MutationIncomeModifierQ,
            value.ParentSpeciesId,
            value.CreatedTick,
            value.ExtinctTick);

    private static OrganismProjection ToProjection(PublicationOrganism organism) =>
        new(
            organism.OrganismId,
            organism.SpeciesId,
            organism.PositionXQ,
            organism.PositionYQ,
            organism.BodyRadiusQ,
            organism.VelocityXQPerHour,
            organism.VelocityYQPerHour,
            organism.BirthTick,
            organism.BiologicalAgeHours,
            organism.LifecyclePhase,
            organism.ReproductionNotBeforeTick,
            organism.SuccessfulReproductionCount,
            organism.ScavengeNotBeforeTick,
            organism.IngestedStructuralMatterQ,
            organism.StructuralMatterQ,
            organism.ChargedReserveQ,
            organism.ChargedReserveCapacityQ,
            organism.RelativeHealthQ,
            organism.ReserveFactorQ,
            organism.StructureFactorQ,
            organism.AgeFactorQ,
            organism.EnvironmentalFactorQ,
            organism.BehaviorId,
            organism.BehaviorTargetKind,
            organism.BehaviorTargetId,
            organism.BehaviorTargetPositionXQ,
            organism.BehaviorTargetPositionYQ,
            organism.BehaviorSelectedAtTick,
            organism.BehaviorMinimumDwellUntilTick,
            organism.RecentEnergyCoverageQ,
            organism.RecentAcquisitionCoverageQ,
            organism.LimitingMaterialDeficitQ,
            organism.ResourcePressureQ,
            organism.CommittedMicronutrients.OrderBy(stock => stock.ResourceId.Value).Select(stock =>
                new ExactResourceStockProjection(stock.ResourceId, stock.QuantityQ)).ToImmutableArray(),
            organism.FreeMicronutrients.OrderBy(stock => stock.ResourceId.Value).Select(stock =>
                new ExactResourceStockProjection(stock.ResourceId, stock.QuantityQ)).ToImmutableArray(),
            organism.ResourceAcquisitionEvidence
                .OrderBy(value => value.ResourceId.Value)
                .Select(value => new ResourceAcquisitionEvidenceProjection(
                    value.ResourceId,
                    value.RequestedQ,
                    value.GrantedQ,
                    value.TileSupplyConstrained,
                    value.ClaimContentionConstrained))
                .ToImmutableArray(),
            organism.AcquisitionGateEvidence
                .OrderBy(value => value.Process)
                .ThenBy(value => value.Reason)
                .Select(value => new AcquisitionGateEvidenceProjection(
                    value.Process,
                    value.Reason,
                    value.AvailableQ,
                    value.RequiredQ,
                    value.ClearsAtTick))
                .ToImmutableArray(),
            organism.ActionGateEvidence
                .OrderBy(value => value.Process)
                .Select(value => new OrganismActionGateEvidenceProjection(
                    value.Process,
                    value.Reason,
                    value.AvailableQ,
                    value.RequiredQ,
                    value.ResourceId,
                    value.ClearsAtTick))
                .ToImmutableArray());

    private static ImmutableArray<BehaviorDistributionProjection> BuildBehaviorDistributions(
        ImmutableArray<PublicationOrganism> organisms,
        ulong observedAtTick) => organisms
        .GroupBy(organism => organism.SpeciesId)
        .OrderBy(group => group.Key.Value)
        .Select(group => new BehaviorDistributionProjection(
            group.Key,
            observedAtTick,
            checked((ulong)group.LongCount()),
            BuildBehaviorCounts(group)))
        .ToImmutableArray();

    private static ImmutableArray<BehaviorCountProjection> BuildBehaviorCounts(
        IEnumerable<PublicationOrganism> organisms) => organisms
        .GroupBy(organism => organism.BehaviorId)
        .OrderBy(group => group.Key)
        .Select(group => new BehaviorCountProjection(
            group.Key,
            checked((ulong)group.LongCount())))
        .ToImmutableArray();

    private static RemnantProjection ToProjection(PublicationRemnant remnant) => new(
        remnant.RemnantId,
        remnant.SourceOrganismId,
        remnant.SourceSpeciesId,
        remnant.PositionXQ,
        remnant.PositionYQ,
        remnant.BodyRadiusQ,
        remnant.CreatedTick,
        remnant.StructuralMatterQ,
        remnant.ChargedReserveQ,
        remnant.Micronutrients.OrderBy(stock => stock.ResourceId.Value).Select(stock =>
            new ExactResourceStockProjection(stock.ResourceId, stock.QuantityQ)).ToImmutableArray());

    private static SourceIndex ValidateAndIndex(WorldPublicationSnapshot source)
    {
        if (!source.WorldId.IsValid ||
            source.TickDurationHours == 0 ||
            source.Width == 0 ||
            source.Height == 0 ||
            source.WrapY ||
            string.IsNullOrWhiteSpace(source.WorldRulesHash) ||
            source.Tiles.IsDefault ||
            source.Species.IsDefault ||
            source.Organisms.IsDefault ||
            source.Remnants.IsDefault ||
            source.JourneyEvents.IsDefault ||
            source.RoutineActivitySummaries.IsDefault ||
            source.ActivityPulseEvents.IsDefault ||
            source.ResourceDefinitions.IsDefault ||
            source.ResourceFlows.IsDefault ||
            source.ResourceFlowHistory.IsDefault ||
            source.ReactionDefinitions.IsDefault ||
            source.ResourceFlowContributors.IsDefault ||
            source.ResourceFlowPeriodHours == 0 ||
            source.Gameplay is null ||
            !Enum.IsDefined(source.Gameplay.Mode) ||
            !Enum.IsDefined(source.Gameplay.RunStatus) ||
            !Enum.IsDefined(source.Gameplay.LossReason) ||
            source.Gameplay.ControlledSpeciesId == default ||
            source.Gameplay.GameplayRevision == 0 ||
            source.Gameplay.Roots.IsDefault ||
            source.Gameplay.MutationLockedSpeciesIds.IsDefault)
        {
            throw new ArgumentException("The publication source is incomplete or invalid.", nameof(source));
        }

        var tileCount = checked(source.Width * source.Height);
        if (tileCount != (uint)source.Tiles.Length)
        {
            throw new ArgumentException(
                "The publication tile count must match the declared grid.",
                nameof(source));
        }

        var tiles = UniqueIndex(
            source.Tiles,
            tile => tile.TileId,
            "tile",
            nameof(source));
        var species = UniqueIndex(
            source.Species,
            item => item.SpeciesId,
            "species",
            nameof(source));
        var resourceIds = new HashSet<ResourceId>();
        foreach (var tile in source.Tiles)
        {
            if (tile.ResourceStocks.IsDefault)
            {
                throw new ArgumentException("A tile has an uninitialized resource vector.", nameof(source));
            }

            var tileResources = new HashSet<ResourceId>();
            foreach (var stock in tile.ResourceStocks)
            {
                if (stock.ResourceId == default ||
                    stock.QuantityQ < 0 ||
                    !tileResources.Add(stock.ResourceId))
                {
                    throw new ArgumentException(
                        "A tile has an invalid or duplicate resource stock.",
                        nameof(source));
                }

                resourceIds.Add(stock.ResourceId);
            }
        }

        var definitionIds = new HashSet<ResourceId>();
        foreach (var definition in source.ResourceDefinitions)
        {
            if (definition.ResourceId == default ||
                string.IsNullOrWhiteSpace(definition.StableKey) ||
                string.IsNullOrWhiteSpace(definition.DisplayName) ||
                !Enum.IsDefined(definition.BiologicalForm) ||
                !Enum.IsDefined(definition.EnvironmentalPhase) ||
                !definitionIds.Add(definition.ResourceId))
            {
                throw new ArgumentException(
                    "The publication source has an invalid resource definition.",
                    nameof(source));
            }
        }
        if (!resourceIds.SetEquals(definitionIds))
        {
            throw new ArgumentException(
                "Published stocks and resource definitions must use the same resource set.",
                nameof(source));
        }

        var reactionIds = new HashSet<ReactionId>();
        foreach (var definition in source.ReactionDefinitions)
        {
            if (definition.ReactionId == default ||
                string.IsNullOrWhiteSpace(definition.StableKey) ||
                string.IsNullOrWhiteSpace(definition.DisplayName) ||
                !reactionIds.Add(definition.ReactionId))
            {
                throw new ArgumentException(
                    "The publication source has an invalid reaction definition.",
                    nameof(source));
            }
        }

        var flowKeys = new HashSet<(TileId, ResourceId, PublicationResourceFlowKind)>();
        foreach (var flow in source.ResourceFlows)
        {
            if (!tiles.ContainsKey(flow.TileId) ||
                !resourceIds.Contains(flow.ResourceId) ||
                !Enum.IsDefined(flow.Kind) ||
                flow.AmountQ <= 0 ||
                !flowKeys.Add((flow.TileId, flow.ResourceId, flow.Kind)))
            {
                throw new ArgumentException(
                    "The publication source has an invalid or duplicate resource flow.",
                    nameof(source));
            }
        }
        var contributorKeys = new HashSet<(
            TileId,
            ResourceId,
            PublicationResourceFlowKind,
            PublicationResourceFlowProcessKind,
            ReactionId?,
            SpeciesId?)>();
        foreach (var contributor in source.ResourceFlowContributors)
        {
            var usesReaction = contributor.Process is
                PublicationResourceFlowProcessKind.ExternalEnergyCapture or
                PublicationResourceFlowProcessKind.ParticulateDigestion or
                PublicationResourceFlowProcessKind.MandatoryMaintenance or
                PublicationResourceFlowProcessKind.BiomassAssembly;
            var biological = usesReaction ||
                contributor.Process == PublicationResourceFlowProcessKind.MicronutrientUptake;
            if (!tiles.ContainsKey(contributor.TileId) ||
                !resourceIds.Contains(contributor.ResourceId) ||
                !Enum.IsDefined(contributor.Kind) ||
                !Enum.IsDefined(contributor.Process) ||
                contributor.AmountQ <= 0 ||
                usesReaction != contributor.ReactionId.HasValue ||
                (contributor.ReactionId is { } reactionId && !reactionIds.Contains(reactionId)) ||
                biological != contributor.SpeciesId.HasValue ||
                (contributor.SpeciesId is { } speciesId && !species.ContainsKey(speciesId)) ||
                !IsValidContributorFlowKind(contributor.Process, contributor.Kind) ||
                !contributorKeys.Add((
                    contributor.TileId,
                    contributor.ResourceId,
                    contributor.Kind,
                    contributor.Process,
                    contributor.ReactionId,
                    contributor.SpeciesId)))
            {
                throw new ArgumentException(
                    "The publication source has an invalid resource-flow contributor.",
                    nameof(source));
            }
        }
        var contributorTotals = source.ResourceFlowContributors
            .GroupBy(value => (value.TileId, value.ResourceId, value.Kind))
            .ToDictionary(group => group.Key, group => group.Sum(value => value.AmountQ));
        if (source.ResourceFlows.Any(flow =>
                !contributorTotals.TryGetValue(
                    (flow.TileId, flow.ResourceId, flow.Kind), out var total) ||
                total != flow.AmountQ) ||
            contributorTotals.Count != source.ResourceFlows.Length)
        {
            throw new ArgumentException(
                "Resource-flow contributors do not reconcile to published flow totals.",
                nameof(source));
        }

        ValidateResourceFlowHistory(source, tiles, resourceIds);

        var actualPopulations = new Dictionary<SpeciesId, ulong>();
        var organismIds = new HashSet<OrganismId>();
        foreach (var organism in source.Organisms)
        {
            if (!species.ContainsKey(organism.SpeciesId) ||
                !tiles.ContainsKey(organism.TileId) ||
                organism.OrganismId == default ||
                !organismIds.Add(organism.OrganismId) ||
                organism.LifecyclePhase == default ||
                organism.IngestedStructuralMatterQ < 0 ||
                organism.StructuralMatterQ < 0 ||
                organism.ChargedReserveQ < 0 ||
                organism.ChargedReserveCapacityQ <= 0 ||
                organism.ChargedReserveQ > organism.ChargedReserveCapacityQ ||
                organism.RelativeHealthQ > 1_000_000 ||
                organism.ReserveFactorQ > 1_000_000 ||
                organism.StructureFactorQ > 1_000_000 ||
                organism.AgeFactorQ > 1_000_000 ||
                organism.EnvironmentalFactorQ > 1_000_000 ||
                !Enum.IsDefined(organism.BehaviorId) ||
                !Enum.IsDefined(organism.BehaviorTargetKind) ||
                organism.RecentEnergyCoverageQ > 2_000_000 ||
                organism.RecentAcquisitionCoverageQ > 2_000_000 ||
                organism.LimitingMaterialDeficitQ > 1_000_000 ||
                organism.ResourcePressureQ > 1_000_000 ||
                !ValidResourceAcquisitionEvidence(
                    organism.ResourceAcquisitionEvidence,
                    resourceIds) ||
                !ValidAcquisitionGateEvidence(
                    organism.AcquisitionGateEvidence,
                    source.CompletedTick) ||
                !ValidOrganismActionGateEvidence(
                    organism.ActionGateEvidence,
                    source.CompletedTick,
                    resourceIds) ||
                !ValidMicronutrientStocks(organism.CommittedMicronutrients, resourceIds) ||
                !ValidMicronutrientStocks(organism.FreeMicronutrients, resourceIds))
            {
                throw new ArgumentException(
                    "An organism has invalid state or a dangling reference.",
                    nameof(source));
            }

            actualPopulations.TryGetValue(organism.SpeciesId, out var count);
            actualPopulations[organism.SpeciesId] = checked(count + 1);
        }

        foreach (var item in source.Species)
        {
            if (item.SpeciesId == default || item.GenomeId == default ||
                string.IsNullOrWhiteSpace(item.GenomeHash) ||
                item.AcquiredTraits.IsDefault ||
                !Enum.IsDefined(item.EvolutionAuthority) ||
                item.MutationBalanceQ < 0 ||
                item.AverageHealthQ > 1_000_000 ||
                item.LastMutationIncomeQ < 0 ||
                item.MutationIncomeModifierQ is < 250_000 or > 3_000_000 ||
                item.ParentSpeciesId == item.SpeciesId ||
                item.ExtinctTick < item.CreatedTick ||
                (item.Population == 0) != item.ExtinctTick.HasValue)
            {
                throw new ArgumentException("A species has invalid identity or evolution state.", nameof(source));
            }

            actualPopulations.TryGetValue(item.SpeciesId, out var actual);
            if (actual != item.Population)
            {
                throw new ArgumentException(
                    "A species population does not match its organisms.",
                    nameof(source));
            }
        }

        if (!species.ContainsKey(source.Gameplay.ControlledSpeciesId) ||
            source.Gameplay.Roots.Length !=
                (source.Gameplay.Mode == Simulation.Gameplay.GameMode.FreeSandbox ? 1 : 2) ||
            source.Gameplay.Roots.Count(root => root.PlayerSelected) != 1 ||
            source.Gameplay.Roots.Any(root => root.InitialPopulation == 0) ||
            source.Gameplay.Roots.Any(root =>
                !species.ContainsKey(root.SpeciesId) ||
                !tiles.ContainsKey(root.StartingTileId)) ||
            source.Gameplay.MutationLockedSpeciesIds.Any(id => !species.ContainsKey(id)) ||
            source.Gameplay.Roots.Select(root => root.SpeciesId).Distinct().Count() !=
                source.Gameplay.Roots.Length ||
            source.Gameplay.MutationLockedSpeciesIds.Distinct().Count() !=
                source.Gameplay.MutationLockedSpeciesIds.Length)
        {
            throw new ArgumentException("The publication gameplay state is invalid.", nameof(source));
        }
        if ((source.Gameplay.RunStatus == Simulation.Gameplay.GameRunStatus.Active &&
                (source.Gameplay.LossReason != Simulation.Gameplay.GameLossReason.None ||
                    source.Gameplay.EndedTick.HasValue)) ||
            (source.Gameplay.RunStatus == Simulation.Gameplay.GameRunStatus.Lost &&
                (source.Gameplay.LossReason == Simulation.Gameplay.GameLossReason.None ||
                    !source.Gameplay.EndedTick.HasValue)))
        {
            throw new ArgumentException("The publication game outcome is invalid.", nameof(source));
        }


        var remnantIds = new HashSet<RemnantId>();
        foreach (var remnant in source.Remnants)
        {
            if (remnant.RemnantId == default ||
                !remnantIds.Add(remnant.RemnantId) ||
                !species.ContainsKey(remnant.SourceSpeciesId) ||
                !tiles.ContainsKey(remnant.TileId) ||
                remnant.StructuralMatterQ < 0 ||
                remnant.ChargedReserveQ < 0 ||
                !ValidMicronutrientStocks(remnant.Micronutrients, resourceIds) ||
                (remnant.StructuralMatterQ == 0 &&
                    remnant.ChargedReserveQ == 0 &&
                    remnant.Micronutrients.IsEmpty))
            {
                throw new ArgumentException(
                    "A remnant has invalid state or a dangling reference.",
                    nameof(source));
            }
        }

        ulong priorJourneyEventId = 0;
        foreach (var value in source.JourneyEvents)
        {
            if (value.EventId <= priorJourneyEventId ||
                value.Tick > source.CompletedTick ||
                !Enum.IsDefined(value.Phase) ||
                !Enum.IsDefined(value.Family) ||
                value.SubjectOrganismId == default ||
                !species.ContainsKey(value.SubjectSpeciesId) ||
                !tiles.ContainsKey(value.TileId) ||
                value.AmountQ < 0 ||
                value.DeathCauseProbabilities.IsDefault ||
                value.DeathCauseProbabilities.Any(cause =>
                    !Enum.IsDefined(cause.Cause) || cause.ProbabilityQ > 1_000_000))
            {
                throw new ArgumentException(
                    "A journey event has invalid state or a dangling reference.",
                    nameof(source));
            }
            priorJourneyEventId = value.EventId;
        }
        foreach (var value in source.ActivityPulseEvents)
        {
            ValidateJourneyEvent(value, source, tiles, species);
        }
        ulong priorLineageReviewEventId = 0;
        foreach (var landmark in source.LineageReviewLandmarks)
        {
            var comparisonSpeciesId = landmark.PerspectiveSpeciesId == landmark.AncestorSpeciesId
                ? landmark.DescendantSpeciesId
                : landmark.AncestorSpeciesId;
            if (landmark.EventId <= priorLineageReviewEventId ||
                landmark.CompletedTick > source.CompletedTick ||
                landmark.EvidenceReferences.IsDefaultOrEmpty ||
                landmark.EvidenceReferences.Length < 4 ||
                landmark.EvidenceReferences.Length > 4_096 ||
                landmark.EvidenceReferences[0].Kind !=
                    LineageReviewEvidenceReferenceKind.SpeciationDecision ||
                landmark.EvidenceReferences[1].Kind !=
                    LineageReviewEvidenceReferenceKind.ReviewedSpeciesSummary ||
                landmark.EvidenceReferences[2].Kind !=
                    LineageReviewEvidenceReferenceKind.ComparisonSpeciesSummary ||
                landmark.EvidenceReferences[3].Kind !=
                    LineageReviewEvidenceReferenceKind.ReviewedJourneyWindow ||
                landmark.EvidenceReferences.Skip(4).Any(reference =>
                    reference.Kind !=
                        LineageReviewEvidenceReferenceKind.LiveTileResourceWindow) ||
                landmark.EvidenceReferences.Any(reference =>
                    reference.FromExclusiveTick !=
                        landmark.EvidenceReferences[0].FromExclusiveTick) ||
                landmark.EvidenceReferences.Skip(4)
                    .Select(reference => reference.TileId)
                    .Distinct().Count() != landmark.EvidenceReferences.Length - 4 ||
                !landmark.EvidenceReferences.Skip(4)
                    .Select(reference => reference.TileId?.Value ?? uint.MaxValue)
                    .SequenceEqual(landmark.EvidenceReferences.Skip(4)
                        .Select(reference => reference.TileId?.Value ?? uint.MaxValue).Order()) ||
                landmark.EvidenceReferences.Count(reference =>
                    reference.Kind == LineageReviewEvidenceReferenceKind.SpeciationDecision) != 1 ||
                landmark.EvidenceReferences.Count(reference =>
                    reference.Kind == LineageReviewEvidenceReferenceKind.ReviewedSpeciesSummary) != 1 ||
                landmark.EvidenceReferences.Count(reference =>
                    reference.Kind == LineageReviewEvidenceReferenceKind.ComparisonSpeciesSummary) != 1 ||
                landmark.EvidenceReferences.Count(reference =>
                    reference.Kind == LineageReviewEvidenceReferenceKind.ReviewedJourneyWindow) != 1 ||
                landmark.EvidenceReferences.Any(reference =>
                    !ValidLineageReviewEvidenceReference(
                        reference,
                        landmark,
                        comparisonSpeciesId,
                        tiles,
                        species)))
            {
                throw new ArgumentException(
                    "A lineage-review landmark has invalid or unauthorized evidence references.",
                    nameof(source));
            }
            priorLineageReviewEventId = landmark.EventId;
        }
        ulong priorNotableEventId = 0;
        foreach (var notable in source.NotableEvents)
        {
            if (notable.EventId <= priorNotableEventId ||
                notable.CompletedTick > source.CompletedTick ||
                !Enum.IsDefined(notable.Family) ||
                !Enum.IsDefined(notable.Significance) ||
                notable.SignificanceRuleVersion != 1 ||
                !species.ContainsKey(notable.SpeciesId) ||
                (notable.RelatedSpeciesId.HasValue &&
                    !species.ContainsKey(notable.RelatedSpeciesId.Value)) ||
                (notable.TileId.HasValue && !tiles.ContainsKey(notable.TileId.Value)) ||
                (notable.ReactionId.HasValue &&
                    !reactionIds.Contains(notable.ReactionId.Value)) ||
                string.IsNullOrWhiteSpace(notable.DeduplicationKey))
            {
                throw new ArgumentException(
                    $"A notable event has invalid state or a dangling reference: {notable}.",
                    nameof(source));
            }
            priorNotableEventId = notable.EventId;
        }
        var chronicleIds = source.LineageReviewLandmarks.Select(value => value.EventId)
            .Concat(source.NotableEvents.Select(value => value.EventId))
            .ToArray();
        if (chronicleIds.Distinct().Count() != chronicleIds.Length ||
            source.NotableEvents.Select(value => value.DeduplicationKey).Distinct().Count() !=
                source.NotableEvents.Length)
        {
            throw new ArgumentException(
                "Chronicle event identities and deduplication keys must be unique.",
                nameof(source));
        }
        var chronicleIdSet = chronicleIds.ToHashSet();
        ulong priorAlertId = 0;
        var alertKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alert in source.AttentionAlerts)
        {
            if (alert.AlertId <= priorAlertId || !Enum.IsDefined(alert.AlertClass) ||
                !Enum.IsDefined(alert.Kind) ||
                (alert.Kind == AttentionAlertKind.NotableEventGroup) !=
                    alert.EventFamily.HasValue ||
                (alert.EventFamily.HasValue && !Enum.IsDefined(alert.EventFamily.Value)) ||
                alert.CompletedTick > source.CompletedTick ||
                !species.ContainsKey(alert.SpeciesId) ||
                alert.ChronicleEventIds.IsDefaultOrEmpty ||
                alert.ChronicleEventIds.Length > 256 ||
                alert.ChronicleEventIds.Any(value => !chronicleIdSet.Contains(value)) ||
                !alertKeys.Add(alert.DeduplicationKey))
            {
                throw new ArgumentException(
                    "An attention alert has invalid state or a dangling evidence reference.",
                    nameof(source));
            }
            priorAlertId = alert.AlertId;
        }
        foreach (var value in source.RoutineActivitySummaries)
        {
            if (value.PeriodHours == 0 ||
                value.SubjectOrganismId == default ||
                !species.ContainsKey(value.SubjectSpeciesId) ||
                !tiles.ContainsKey(value.TileId) ||
                value.ResourceAcquisitions.IsDefaultOrEmpty ||
                value.ResourceAcquisitions.Any(resource =>
                    resource.AmountQ <= 0 || !resourceIds.Contains(resource.ResourceId)))
            {
                throw new ArgumentException(
                    "A routine activity summary has invalid state or a dangling reference.",
                    nameof(source));
            }
        }

        return new SourceIndex(tiles, species, resourceIds);
    }

    private static bool ValidLineageReviewEvidenceReference(
        LineageReviewEvidenceReference reference,
        LineageReviewLandmark landmark,
        SpeciesId comparisonSpeciesId,
        Dictionary<TileId, PublicationTile> tiles,
        Dictionary<SpeciesId, PublicationSpecies> species)
    {
        if (reference.FromExclusiveTick >= reference.ThroughCompletedTick ||
            reference.ThroughCompletedTick != landmark.CompletedTick)
        {
            return false;
        }
        return reference.Kind switch
        {
            LineageReviewEvidenceReferenceKind.SpeciationDecision =>
                !reference.SpeciesId.HasValue && !reference.TileId.HasValue,
            LineageReviewEvidenceReferenceKind.ReviewedSpeciesSummary or
                LineageReviewEvidenceReferenceKind.ReviewedJourneyWindow =>
                reference.SpeciesId == landmark.PerspectiveSpeciesId &&
                species.ContainsKey(landmark.PerspectiveSpeciesId) &&
                !reference.TileId.HasValue,
            LineageReviewEvidenceReferenceKind.ComparisonSpeciesSummary =>
                reference.SpeciesId == comparisonSpeciesId &&
                species.ContainsKey(comparisonSpeciesId) &&
                !reference.TileId.HasValue,
            LineageReviewEvidenceReferenceKind.LiveTileResourceWindow =>
                reference.SpeciesId == landmark.PerspectiveSpeciesId &&
                reference.TileId.HasValue && tiles.ContainsKey(reference.TileId.Value),
            _ => false,
        };
    }

    private static bool IsValidContributorFlowKind(
        PublicationResourceFlowProcessKind process,
        PublicationResourceFlowKind kind) => process switch
        {
            PublicationResourceFlowProcessKind.EnvironmentalGasSource =>
                kind == PublicationResourceFlowKind.EnvironmentalSource,
            PublicationResourceFlowProcessKind.EnvironmentalGasSink =>
                kind == PublicationResourceFlowKind.EnvironmentalSink,
            PublicationResourceFlowProcessKind.EnvironmentalGasExchange =>
                kind is PublicationResourceFlowKind.NeighborExchangeIn or
                    PublicationResourceFlowKind.NeighborExchangeOut,
            _ => kind is PublicationResourceFlowKind.OrganismUptake or
                PublicationResourceFlowKind.OrganismRelease,
        };

    private static void ValidateResourceFlowHistory(
        WorldPublicationSnapshot source,
        Dictionary<TileId, PublicationTile> tiles,
        HashSet<ResourceId> resourceIds)
    {
        if ((source.CompletedTick == 0) != source.ResourceFlowHistory.IsEmpty ||
            source.ResourceFlowHistory.Length > 168)
        {
            throw new ArgumentException(
                "Resource-flow history is missing or exceeds its exact retention window.",
                nameof(source));
        }

        ulong priorTick = 0;
        ulong priorEndHour = 0;
        for (var index = 0; index < source.ResourceFlowHistory.Length; index++)
        {
            var interval = source.ResourceFlowHistory[index];
            if (interval.PeriodHours != source.TickDurationHours ||
                interval.CompletedTick == 0 ||
                interval.EndSimulatedHour != checked(
                    interval.CompletedTick * source.TickDurationHours) ||
                interval.ResourceFlows.IsDefault ||
                (index > 0 && (interval.CompletedTick != priorTick + 1 ||
                    interval.EndSimulatedHour != priorEndHour + source.TickDurationHours)))
            {
                throw new ArgumentException(
                    "Resource-flow history has an invalid interval boundary.",
                    nameof(source));
            }

            var keys = new HashSet<(TileId, ResourceId, PublicationResourceFlowKind)>();
            foreach (var flow in interval.ResourceFlows)
            {
                if (!tiles.ContainsKey(flow.TileId) ||
                    !resourceIds.Contains(flow.ResourceId) ||
                    !Enum.IsDefined(flow.Kind) ||
                    flow.AmountQ <= 0 ||
                    !keys.Add((flow.TileId, flow.ResourceId, flow.Kind)))
                {
                    throw new ArgumentException(
                        "Resource-flow history contains an invalid or duplicate flow.",
                        nameof(source));
                }
            }
            priorTick = interval.CompletedTick;
            priorEndHour = interval.EndSimulatedHour;
        }

        if (!source.ResourceFlowHistory.IsEmpty)
        {
            var first = source.ResourceFlowHistory[0];
            var last = source.ResourceFlowHistory[^1];
            if (last.CompletedTick != source.CompletedTick ||
                last.EndSimulatedHour != source.SimulatedHours ||
                source.SimulatedHours - (first.EndSimulatedHour - first.PeriodHours) > 168 ||
                !CanonicalFlows(last.ResourceFlows).SequenceEqual(
                    CanonicalFlows(source.ResourceFlows)))
            {
                throw new ArgumentException(
                    "Resource-flow history does not match the current publication boundary.",
                    nameof(source));
            }
        }
    }

    private static IEnumerable<PublicationTileResourceFlow> CanonicalFlows(
        IEnumerable<PublicationTileResourceFlow> flows) => flows
        .OrderBy(flow => flow.TileId.Value)
        .ThenBy(flow => flow.ResourceId.Value)
        .ThenBy(flow => flow.Kind);

    private static bool ValidResourceAcquisitionEvidence(
        ImmutableArray<PublicationResourceAcquisitionEvidence> values,
        HashSet<ResourceId> resourceIds) =>
        !values.IsDefault &&
        values.All(value =>
            resourceIds.Contains(value.ResourceId) &&
            value.RequestedQ > 0 &&
            value.GrantedQ >= 0 &&
            value.GrantedQ <= value.RequestedQ) &&
        values.Select(value => value.ResourceId).Distinct().Count() == values.Length;

    private static bool ValidAcquisitionGateEvidence(
        ImmutableArray<PublicationAcquisitionGateEvidence> values,
        ulong completedTick) =>
        !values.IsDefault &&
        values.All(value =>
            Enum.IsDefined(value.Process) &&
            Enum.IsDefined(value.Reason) &&
            ValidAcquisitionGatePair(value.Process, value.Reason) &&
            value.AvailableQ >= 0 &&
            value.RequiredQ >= 0 &&
            (value.Reason is PublicationAcquisitionGateReason.InternalCapacity or
                    PublicationAcquisitionGateReason.InsufficientActionEnergy
                ? value.RequiredQ > 0 && value.AvailableQ < value.RequiredQ &&
                    value.ClearsAtTick == 0
                : value.Reason == PublicationAcquisitionGateReason.CooldownActive
                    ? value.AvailableQ == 0 && value.RequiredQ == 0 &&
                        value.ClearsAtTick > completedTick
                    : value.AvailableQ == 0 && value.RequiredQ == 0 &&
                        value.ClearsAtTick == 0)) &&
        values.Select(value => (value.Process, value.Reason)).Distinct().Count() == values.Length;

    private static bool ValidAcquisitionGatePair(
        PublicationAcquisitionProcessKind process,
        PublicationAcquisitionGateReason reason) => process switch
        {
            PublicationAcquisitionProcessKind.ExternalEnergyCapture => reason is
                PublicationAcquisitionGateReason.MissingCapability or
                PublicationAcquisitionGateReason.InaccessibleLight or
                PublicationAcquisitionGateReason.EnvironmentalOpportunity or
                PublicationAcquisitionGateReason.InternalCapacity,
            PublicationAcquisitionProcessKind.Scavenging => reason is
                PublicationAcquisitionGateReason.MissingCapability or
                PublicationAcquisitionGateReason.InternalCapacity or
                PublicationAcquisitionGateReason.CooldownActive or
                PublicationAcquisitionGateReason.InsufficientActionEnergy,
            _ => false,
        };

    private static bool ValidOrganismActionGateEvidence(
        ImmutableArray<PublicationOrganismActionGateEvidence> values,
        ulong completedTick,
        HashSet<ResourceId> resourceIds) =>
        !values.IsDefault &&
        values.Length <= 2 &&
        values.Select(value => value.Process).Distinct().Count() == values.Length &&
        values.All(value =>
            Enum.IsDefined(value.Process) &&
            Enum.IsDefined(value.Reason) &&
            ValidOrganismActionGatePair(value.Process, value.Reason) &&
            value.AvailableQ >= 0 &&
            value.RequiredQ >= 0 &&
            (value.Reason == PublicationOrganismActionGateReason.CooldownActive
                ? value.AvailableQ == 0 && value.RequiredQ == 0 &&
                    value.ResourceId is null && value.ClearsAtTick > completedTick
                : value.Reason is PublicationOrganismActionGateReason.MissingCapability or
                        PublicationOrganismActionGateReason.BehaviorSuppressed or
                        PublicationOrganismActionGateReason.LifecycleIneligible
                    ? value.AvailableQ == 0 && value.RequiredQ == 0 &&
                        value.ResourceId is null && value.ClearsAtTick == 0
                    : value.RequiredQ > 0 && value.AvailableQ < value.RequiredQ &&
                        value.ClearsAtTick == 0 &&
                        (value.Reason is
                            PublicationOrganismActionGateReason
                                .ConstitutiveMicronutrientQuotaMissing or
                            PublicationOrganismActionGateReason
                                .OffspringMicronutrientQuotaMissing or
                            PublicationOrganismActionGateReason.ResourceSupply
                                ? value.ResourceId is not null &&
                                    resourceIds.Contains(value.ResourceId.Value)
                                : value.ResourceId is null)));

    private static bool ValidOrganismActionGatePair(
        PublicationOrganismActionProcessKind process,
        PublicationOrganismActionGateReason reason) => process switch
        {
            PublicationOrganismActionProcessKind.BiomassGrowth => reason is
                PublicationOrganismActionGateReason.MissingCapability or
                PublicationOrganismActionGateReason.BehaviorSuppressed or
                PublicationOrganismActionGateReason.MaintenanceShortfall or
                PublicationOrganismActionGateReason.ReserveProtectionFloor or
                PublicationOrganismActionGateReason.InternalCapacity or
                PublicationOrganismActionGateReason.ResourceSupply or
                PublicationOrganismActionGateReason.ClaimContention,
            PublicationOrganismActionProcessKind.Reproduction => reason is
                PublicationOrganismActionGateReason.BehaviorSuppressed or
                PublicationOrganismActionGateReason.CooldownActive or
                PublicationOrganismActionGateReason.HealthBelowMinimum or
                PublicationOrganismActionGateReason.StructureBelowMinimum or
                PublicationOrganismActionGateReason.ReserveBelowMinimum or
                PublicationOrganismActionGateReason.ConstitutiveMicronutrientQuotaMissing or
                PublicationOrganismActionGateReason.OffspringMicronutrientQuotaMissing or
                PublicationOrganismActionGateReason.LifecycleIneligible,
            _ => false,
        };

    private static OrganismJourneyEventProjection ToProjection(
        Simulation.Gameplay.OrganismJourneyEvent value) => new(
            value.EventId,
            value.Tick,
            value.Phase,
            value.Family,
            value.SubjectOrganismId,
            value.SubjectSpeciesId,
            value.TileId,
            value.PositionXQ,
            value.PositionYQ,
            value.RelatedOrganismId,
            value.RelatedRemnantId,
            value.ResourceId,
            value.AmountQ,
            value.DetailId,
            value.DeathCauseProbabilities.Select(cause =>
                new JourneyDeathCauseProjection(
                    cause.Cause,
                    cause.ProbabilityQ,
                    cause.Triggered)).ToImmutableArray());

    private static void ValidateJourneyEvent(
        Simulation.Gameplay.OrganismJourneyEvent value,
        WorldPublicationSnapshot source,
        Dictionary<TileId, PublicationTile> tiles,
        Dictionary<SpeciesId, PublicationSpecies> species)
    {
        if (value.EventId == 0 ||
            value.Tick > source.CompletedTick ||
            !Enum.IsDefined(value.Phase) ||
            !Enum.IsDefined(value.Family) ||
            value.SubjectOrganismId == default ||
            !species.ContainsKey(value.SubjectSpeciesId) ||
            !tiles.ContainsKey(value.TileId) ||
            value.AmountQ < 0 ||
            value.DeathCauseProbabilities.IsDefault ||
            value.DeathCauseProbabilities.Any(cause =>
                !Enum.IsDefined(cause.Cause) || cause.ProbabilityQ > 1_000_000))
        {
            throw new ArgumentException(
                "A journey event has invalid state or a dangling reference.",
                nameof(source));
        }
    }

    private static bool ValidMicronutrientStocks(
        ImmutableArray<PublicationResourceStock> stocks,
        HashSet<ResourceId> knownResourceIds)
    {
        if (stocks.IsDefault) return false;

        var resourceIds = new HashSet<ResourceId>();
        foreach (var stock in stocks)
        {
            if (stock.ResourceId.Value < MicronutrientInventory.FirstResourceId ||
                stock.ResourceId.Value >=
                    MicronutrientInventory.FirstResourceId + MicronutrientInventory.Count ||
                stock.QuantityQ <= 0 ||
                !knownResourceIds.Contains(stock.ResourceId) ||
                !resourceIds.Add(stock.ResourceId))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<TileId, DiscoveredTileKnowledge> ValidateAndIndex(
        ActorKnowledgeSnapshot knowledge,
        WorldPublicationSnapshot source,
        SourceIndex sourceIndex)
    {
        if (knowledge.ControlledSpeciesId == default ||
            knowledge.ControlledSpeciesId != source.Gameplay.ControlledSpeciesId ||
            !sourceIndex.Species.ContainsKey(knowledge.ControlledSpeciesId) ||
            knowledge.DiscoveredTiles.IsDefault)
        {
            throw new ArgumentException(
                "Actor knowledge must identify one existing controlled species.",
                nameof(knowledge));
        }

        var discovered = new Dictionary<TileId, DiscoveredTileKnowledge>();
        foreach (var tile in knowledge.DiscoveredTiles)
        {
            if (!sourceIndex.Tiles.ContainsKey(tile.TileId) ||
                tile.ObservedAtTick > source.CompletedTick ||
                tile.KnownPresentResourceIds.IsDefault ||
                !discovered.TryAdd(tile.TileId, tile))
            {
                throw new ArgumentException(
                    "Actor knowledge contains an invalid or duplicate discovered tile.",
                    nameof(knowledge));
            }

            var knownResources = new HashSet<ResourceId>();
            foreach (var resourceId in tile.KnownPresentResourceIds)
            {
                if (!sourceIndex.ResourceIds.Contains(resourceId) ||
                    !knownResources.Add(resourceId))
                {
                    throw new ArgumentException(
                        "Actor knowledge contains an invalid or duplicate resource ID.",
                        nameof(knowledge));
                }
            }
        }

        return discovered;
    }

    private static Dictionary<TKey, TItem> UniqueIndex<TKey, TItem>(
        IEnumerable<TItem> items,
        Func<TItem, TKey> keySelector,
        string label,
        string parameterName)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TItem>();
        foreach (var item in items)
        {
            if (!result.TryAdd(keySelector(item), item))
            {
                throw new ArgumentException(
                    $"The publication source contains a duplicate {label} ID.",
                    parameterName);
            }
        }

        return result;
    }

    private sealed record SourceIndex(
        Dictionary<TileId, PublicationTile> Tiles,
        Dictionary<SpeciesId, PublicationSpecies> Species,
        HashSet<ResourceId> ResourceIds);
}

using System.Collections.Immutable;
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
        var tiles = source.Tiles
            .OrderBy(tile => tile.TileId.Value)
            .Select(tile => ProjectTile(
                tile,
                source.CompletedTick,
                liveTileIds,
                discovered,
                organismsByTile,
                remnantsByTile))
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
            activityPulseEvents);
    }

    private static TileProjection ProjectTile(
        PublicationTile tile,
        ulong completedTick,
        HashSet<TileId> liveTileIds,
        Dictionary<TileId, DiscoveredTileKnowledge> discovered,
        Dictionary<TileId, ImmutableArray<PublicationOrganism>> organismsByTile,
        Dictionary<TileId, ImmutableArray<PublicationRemnant>> remnantsByTile)
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
                BuildBehaviorDistributions(organisms, completedTick));
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
                new ExactResourceStockProjection(stock.ResourceId, stock.QuantityQ)).ToImmutableArray());

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
        IReadOnlyDictionary<TileId, PublicationTile> tiles,
        IReadOnlyDictionary<SpeciesId, PublicationSpecies> species)
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

using System.Collections.Immutable;
using System.Security.Cryptography;
using Google.Protobuf;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Mechanics;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World;
using Lyfe.Simulation.World.Generation;
using Proto = Lyfe.Protocol.V1;

namespace Lyfe.Server.Evolution;

public sealed class EvolutionProtocolService
{
    private const int MaximumRememberedCommands = 1_024;
    private readonly object gate = new();
    private readonly Dictionary<string, CachedCommand> commands =
        new(StringComparer.Ordinal);
    private readonly Queue<string> commandOrder = new();
    private ulong nextCommandOrder = 1;

    public Proto.EvolutionDecisionSurface CaptureDecisionSurface(WorldRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        lock (gate)
        {
            var publication = runner.CapturePublicationSnapshot();
            return CaptureDecisionSurfaceCore(runner, publication);
        }
    }

    public Proto.EvolutionDecisionSurface CaptureDecisionSurface(
        WorldRunner runner,
        WorldPublicationSnapshot publication)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(publication);
        lock (gate)
        {
            return CaptureDecisionSurfaceCore(runner, publication);
        }
    }

    public Proto.SpeciationProposal Preview(
        WorldRunner runner,
        Proto.SpeciationProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            var publication = runner.CapturePublicationSnapshot();
            var before = CaptureDecisionSurfaceCore(runner, publication);
            var command = ToCommand(runner, request);
            return ToProtocol(
                runner,
                publication,
                runner.PreviewSpeciation(command),
                before,
                runner.CaptureSnapshot().CompletedTick,
                request.NewTraitIds);
        }
    }

    public Proto.ApplySpeciationResponse Apply(
        WorldRunner runner,
        Proto.ApplySpeciationRequest request)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ClientCommandId) ||
            request.ClientCommandId.Length > 128 ||
            request.Proposal is null)
        {
            throw new ArgumentException("A bounded client command ID and proposal are required.",
                nameof(request));
        }

        lock (gate)
        {
            var fingerprint = Convert.ToHexStringLower(
                SHA256.HashData(request.Proposal.ToByteArray()));
            if (commands.TryGetValue(request.ClientCommandId, out var cached))
            {
                if (!string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw new EvolutionCommandConflictException(request.ClientCommandId);
                }
                return Proto.ApplySpeciationResponse.Parser.ParseFrom(cached.ResponseBytes);
            }

            var publication = runner.CapturePublicationSnapshot();
            var before = CaptureDecisionSurfaceCore(runner, publication);
            var command = ToCommand(runner, request.Proposal);
            var result = runner.ApplySpeciation(command);
            var response = new Proto.ApplySpeciationResponse
            {
                ClientCommandId = request.ClientCommandId,
                CommandOrder = nextCommandOrder++,
                Proposal = ToProtocol(
                    runner,
                    publication,
                    result.Preview,
                    before,
                    runner.CaptureSnapshot().CompletedTick,
                    request.Proposal.NewTraitIds),
                Applied = result.Preview.Accepted,
                EventId = result.EventId,
                DescendantSpeciesId = result.DescendantSpeciesId.Value,
                DescendantGenomeId = result.DescendantGenomeId.Value,
                ApplicationTick = runner.CaptureSnapshot().CompletedTick,
                WorldRevision = result.WorldRevision,
            };
            Remember(
                request.ClientCommandId,
                new CachedCommand(fingerprint, response.ToByteArray()));
            return response;
        }
    }

    private static Proto.EvolutionDecisionSurface CaptureDecisionSurfaceCore(
        WorldRunner runner,
        WorldPublicationSnapshot publication)
    {
        var controlledId = publication.Gameplay.ControlledSpeciesId;
        var species = publication.Species.Single(value => value.SpeciesId == controlledId);
        var acquired = species.AcquiredTraits.ToHashSet();
        var maximumComplexity = runner.Rules.RulePack.Traits
            .Where(trait => acquired.Contains(trait.Id) &&
                trait.MaximumChangeComplexity.HasValue)
            .Select(trait => trait.MaximumChangeComplexity ?? 0)
            .Aggregate(0U, Math.Max);
        var result = new Proto.EvolutionDecisionSurface
        {
            WorldId = publication.WorldId.Value,
            CompletedTick = publication.CompletedTick,
            WorldRevision = publication.WorldRevision,
            TickDurationHours = publication.TickDurationHours,
            ControlledSpeciesId = controlledId.Value,
            ControlledPopulation = species.Population,
            EvolutionRevision = species.EvolutionRevision,
            GenomeHash = species.GenomeHash,
            MutationBalanceQ = species.MutationBalanceQ,
            LastMutationIncomeQ = species.LastMutationIncomeQ,
            AverageHealthQ = species.AverageHealthQ,
            MutationIncomeModifierQ = species.MutationIncomeModifierQ,
            SpeciationNotBeforeTick = species.SpeciationNotBeforeTick,
            MaximumChangeComplexity = maximumComplexity,
        };
        if (species.HabitatProfile is { } habitat)
        {
            result.HabitatProfile = new Proto.EvolutionHabitatProfile
            {
                PreferredTemperatureMinimumMilliC =
                    habitat.PreferredTemperatureMinimumMilliC,
                PreferredTemperatureMaximumMilliC =
                    habitat.PreferredTemperatureMaximumMilliC,
                HardTemperatureMinimumMilliC = habitat.HardTemperatureMinimumMilliC,
                HardTemperatureMaximumMilliC = habitat.HardTemperatureMaximumMilliC,
                CanOccupyTerrestrial = habitat.CanOccupyTerrestrial,
                RequiresLight = habitat.RequiresLight,
            };
            result.HabitatProfile.RelevantResources.Add(habitat.RelevantResources.Select(
                resource => new Proto.EvolutionRelevantResource
                {
                    ResourceId = resource.ResourceId.Value,
                    MetabolismInput = resource.MetabolismInput,
                    GrowthInput = resource.GrowthInput,
                    HealthRequirement = resource.HealthRequirement,
                    EnvironmentalHazard = resource.EnvironmentalHazard,
                    SoftHazardThresholdQ = resource.SoftHazardThresholdQ,
                    HardHazardThresholdQ = resource.HardHazardThresholdQ,
                }));
        }
        result.Traits.Add(runner.Rules.RulePack.Traits
            .OrderBy(trait => trait.Id.Value)
            .Select(trait =>
            {
                var option = new Proto.EvolutionTraitOption
                {
                    TraitId = trait.Id.Value,
                    StableKey = trait.StableKey,
                    DisplayName = trait.DisplayName,
                    Family = trait.Family,
                    Selectable = trait.Selectable,
                    Acquired = acquired.Contains(trait.Id),
                    MutationPointCostQ = trait.MutationPointCostQ,
                    ChangeComplexity = trait.ChangeComplexity,
                    EnablesResourceConservation = trait.EnablesResourceConservation,
                    ConsequenceFollowUpHours = trait.ConsequenceFollowUpHours ?? 0,
                    ConsequenceEvidenceKind = trait.ConsequenceEvidenceKind.HasValue
                        ? (Proto.EvolutionFollowUpEvidenceKind)(int)trait.ConsequenceEvidenceKind.Value
                        : Proto.EvolutionFollowUpEvidenceKind.Unspecified,
                };
                option.PrerequisiteTraitIds.Add(trait.Prerequisites.Select(id => id.Value));
                option.IncompatibleTraitIds.Add(
                    trait.Incompatibilities.Select(id => id.Value));
                option.StrategicIntents.Add(trait.StrategicIntents.Select(intent =>
                    (Proto.EvolutionStrategicIntent)(int)intent));
                return option;
            }));
        result.OccupiedTiles.Add(publication.Organisms
            .Where(organism => organism.SpeciesId == controlledId)
            .GroupBy(organism => organism.TileId)
            .OrderBy(group => group.Key.Value)
            .Select(group =>
            {
                var organisms = group.ToArray();
                var tile = publication.Tiles.Single(value => value.TileId == group.Key);
                var generatedTile = runner.Rules.GeneratedWorld?.GetTile(group.Key.Value);
                var currentClimate = generatedTile is null
                    ? (TileCurrentClimate?)null
                    : WorldClimateEvaluator.Evaluate(
                        runner.Rules.GeneratedWorld!,
                        generatedTile,
                        publication.SimulatedHours);
                var result = new Proto.EvolutionOccupiedTile
                {
                    TileId = group.Key.Value,
                    Population = checked((ulong)organisms.LongLength),
                    AverageHealthQ = Average(organisms, value => value.RelativeHealthQ),
                    AverageReserveQ = Average(organisms, value => value.ReserveFactorQ),
                    AverageEnvironmentalFactorQ = Average(
                        organisms,
                        value => value.EnvironmentalFactorQ),
                    AverageResourcePressureQ = Average(
                        organisms,
                        value => value.ResourcePressureQ),
                    ElevationMeters = tile.ElevationMeters,
                    HasCurrentClimate = currentClimate.HasValue,
                    CurrentTemperatureMilliC = currentClimate?.TemperatureMilliC ?? 0,
                    CurrentSurfaceMoistureQ = currentClimate?.SurfaceMoistureQ ?? 0,
                    CurrentAccessibleLightQ = currentClimate?.AccessibleLightQ ?? 0,
                    CurrentPrecipitationMicrometersPerHour =
                        currentClimate?.PrecipitationMicrometersPerHour ?? 0,
                    CurrentCloudQ = currentClimate?.CloudQ ?? 0,
                    CurrentSurfaceLightQ = currentClimate?.SurfaceLightQ ?? 0,
                    SeasonalTemperatureMinimumMilliC = generatedTile is null
                        ? currentClimate?.TemperatureMilliC ?? 0
                        : generatedTile.MonthlyTemperatureMilliC.Min(),
                    SeasonalTemperatureMaximumMilliC = generatedTile is null
                        ? currentClimate?.TemperatureMilliC ?? 0
                        : generatedTile.MonthlyTemperatureMilliC.Max(),
                    BaselineVolcanismQ = generatedTile?.BaselineVolcanismQ ??
                        runner.Rules.WorldProfile.Tiles
                            .Single(value => value.TileIndex == group.Key.Value)
                            .BaselineVolcanismQ,
                };
                result.ResourceStocks.Add(tile.ResourceStocks
                    .OrderBy(stock => stock.ResourceId.Value)
                    .Select(stock => new Proto.EvolutionResourceStock
                    {
                        ResourceId = stock.ResourceId.Value,
                        QuantityQ = stock.QuantityQ,
                    }));
                return result;
            }));
        result.ResourceDefinitions.Add(runner.Rules.RulePack.Resources
            .OrderBy(resource => resource.Id.Value)
            .Select(resource => new Proto.EvolutionResourceDefinition
            {
                ResourceId = resource.Id.Value,
                DisplayName = resource.DisplayName,
            }));
        result.ReactionDefinitions.Add(runner.Rules.RulePack.Reactions
            .OrderBy(reaction => reaction.Id.Value)
            .Select(reaction => new Proto.EvolutionReactionDefinition
            {
                ReactionId = reaction.Id.Value,
                DisplayName = reaction.DisplayName,
            }));
        return result;
    }

    private static uint Average(
        PublicationOrganism[] organisms,
        Func<PublicationOrganism, uint> selector)
    {
        var sum = organisms.Aggregate(0UL, (total, value) => checked(total + selector(value)));
        return checked((uint)(sum / (ulong)organisms.Length));
    }

    private static SpeciationCommand ToCommand(
        WorldRunner runner,
        Proto.SpeciationProposalRequest request)
    {
        if (request.WorldId != runner.WorldId.Value)
        {
            throw new ArgumentException("The proposal targets a different world.",
                nameof(request));
        }
        return new SpeciationCommand(
            SpeciesId.From(request.AncestorSpeciesId),
            request.ExpectedEvolutionRevision,
            request.ExpectedGenomeHash,
            request.NewTraitIds.Select(TraitId.From).ToImmutableArray(),
            request.SelectedTileIds.Select(TileId.FromRowMajorIndex).ToImmutableArray(),
            SpeciationActorKind.Player,
            request.FollowDescendantIfPermitted);
    }

    private static Proto.SpeciationProposal ToProtocol(
        WorldRunner runner,
        WorldPublicationSnapshot publication,
        SpeciationPreview preview,
        Proto.EvolutionDecisionSurface before,
        ulong applicationTick,
        IEnumerable<uint> requestedTraitIds)
    {
        var founders = preview.FounderCounts.Aggregate(
            0UL,
            (total, value) => checked(total + value.Count));
        var result = new Proto.SpeciationProposal
        {
            Accepted = preview.Accepted,
            Failure = (Proto.SpeciationFailure)(int)preview.Failure,
            MutationPriceQ = preview.MutationPriceQ,
            ChangeComplexity = preview.ChangeComplexity,
            ProposedGenomeHash = preview.ProposedGenomeHash,
            BalanceBeforeQ = before.MutationBalanceQ,
            DuplicatedBalanceAfterQ = preview.Accepted
                ? checked(before.MutationBalanceQ - preview.MutationPriceQ)
                : 0,
            AncestorPopulationBefore = before.ControlledPopulation,
            AncestorPopulationAfter = before.ControlledPopulation >= founders
                ? before.ControlledPopulation - founders
                : 0,
            DescendantPopulation = founders,
            ResultingSpeciationNotBeforeTick = checked(applicationTick +
                (SpeciationRules.RefractoryHours + before.TickDurationHours - 1) /
                before.TickDurationHours),
        };
        result.FounderCounts.Add(preview.FounderCounts.Select(value =>
            new Proto.SpeciationFounderCount
            {
                TileId = value.TileId.Value,
                Count = value.Count,
            }));
        if (preview.CurrentPhenotype is { } current &&
            preview.ProposedPhenotype is { } proposed)
        {
            result.CurrentPhenotype = ToProtocol(current);
            result.ProposedPhenotype = ToProtocol(proposed);
            AddActivationAssessment(
                result,
                current,
                proposed,
                requestedTraitIds,
                before.Traits);
            result.StrategicIntents.Add(before.Traits
                .Where(trait => requestedTraitIds.Contains(trait.TraitId))
                .SelectMany(trait => trait.StrategicIntents)
                .Distinct()
                .Order());
            var followUp = before.Traits
                .Where(trait => requestedTraitIds.Contains(trait.TraitId) &&
                    trait.ConsequenceFollowUpHours > 0)
                .OrderByDescending(trait => trait.ConsequenceFollowUpHours)
                .ThenBy(trait => trait.TraitId)
                .FirstOrDefault();
            if (followUp is not null)
            {
                result.ConsequenceFollowUpHours = followUp.ConsequenceFollowUpHours;
                result.ConsequenceEvidenceKind = followUp.ConsequenceEvidenceKind;
            }
            AddTileActivationEvidence(result, runner, publication, before, proposed);
        }
        return result;
    }

    private static Proto.EvolutionPhenotypeSummary ToProtocol(
        EvolutionPhenotypeSnapshot source)
    {
        var result = new Proto.EvolutionPhenotypeSummary
        {
            MutationIncomeModifierQ = source.MutationIncomeModifierQ,
            MaximumChangeComplexity = source.MaximumChangeComplexity,
            ResourceConservation = source.ResourceConservation,
            MaximumCaptureExtentsPerHour = source.MaximumCaptureExtentsPerHour,
            FavorableCaptureEfficiencyQ = source.FavorableCaptureEfficiencyQ,
            MaintenanceCostQPerHour = source.MaintenanceCostQPerHour,
            ChargedReserveCapacityQ = source.ChargedReserveCapacityQ,
            MinimumReproductionHealthQ = source.MinimumReproductionHealthQ,
            RequiresLight = source.RequiresLight,
        };
        result.ActiveReactionIds.Add(source.ActiveReactionIds.Select(id => id.Value));
        result.RecurringCosts.Add(new Proto.EvolutionRecurringCost
        {
            Channel = Proto.EvolutionRecurringCostChannel.MandatoryMaintenance,
            AmountQPerOrganismHour = source.MaintenanceCostQPerHour,
        });
        return result;
    }

    private static void AddTileActivationEvidence(
        Proto.SpeciationProposal result,
        WorldRunner runner,
        WorldPublicationSnapshot publication,
        Proto.EvolutionDecisionSurface before,
        EvolutionPhenotypeSnapshot proposed)
    {
        var tiles = before.OccupiedTiles.ToDictionary(tile => tile.TileId);
        var reactions = runner.Rules.RulePack.Reactions.ToDictionary(reaction => reaction.Id);
        foreach (var founder in result.FounderCounts.OrderBy(value => value.TileId))
        {
            var tile = tiles[founder.TileId];
            var evidence = new Proto.EvolutionTileActivationEvidence
            {
                TileId = tile.TileId,
                FounderCount = founder.Count,
                AverageHealthQ = tile.AverageHealthQ,
                AverageReserveQ = tile.AverageReserveQ,
                AverageEnvironmentalFactorQ = tile.AverageEnvironmentalFactorQ,
                AverageResourcePressureQ = tile.AverageResourcePressureQ,
                HasCurrentClimate = tile.HasCurrentClimate,
                CurrentTemperatureMilliC = tile.CurrentTemperatureMilliC,
                CurrentSurfaceMoistureQ = tile.CurrentSurfaceMoistureQ,
                CurrentAccessibleLightQ = tile.CurrentAccessibleLightQ,
            };
            evidence.ReactionOpportunities.Add(proposed.ActiveReactionIds
                .Select(id => reactions[id])
                .Where(reaction => reaction.ProcessKind == ProcessKind.ExternalEnergyCapture)
                .OrderBy(reaction => reaction.Id.Value)
                .Select(reaction => BuildReactionOpportunity(
                    runner,
                    publication,
                    tile,
                    founder.Count,
                    proposed,
                    reaction)));
            result.TileActivationEvidence.Add(evidence);
        }
    }

    private static Proto.EvolutionReactionOpportunity BuildReactionOpportunity(
        WorldRunner runner,
        WorldPublicationSnapshot publication,
        Proto.EvolutionOccupiedTile tile,
        uint founderCount,
        EvolutionPhenotypeSnapshot phenotype,
        CompiledReaction reaction)
    {
        var stocks = tile.ResourceStocks.ToDictionary(stock => stock.ResourceId);
        var inputs = reaction.Inputs
            .OrderBy(input => input.Resource.Id.Value)
            .Select(input =>
            {
                var resource = runner.Rules.RulePack.Resources[input.Resource.DenseSlot];
                var stock = stocks.GetValueOrDefault(resource.Id.Value)?.QuantityQ ?? 0;
                var inexhaustible = resource.BiologicalForm == BiologicalForm.Boundary;
                var accessible = inexhaustible
                    ? stock
                    : AccessibleStock(runner.Rules, tile, resource, stock);
                var result = new Proto.EvolutionReactionInputOpportunity
                {
                    ResourceId = resource.Id.Value,
                    TileStockQ = stock,
                    AccessibleStockQ = accessible,
                    RequiredPerExtentQ = input.Quantity,
                    Inexhaustible = inexhaustible,
                };
                if (!inexhaustible)
                {
                    result.FlowForecast = BuildResourceFlowForecast(
                        publication,
                        tile,
                        founderCount,
                        phenotype,
                        resource.Id,
                        input.Quantity);
                }
                return result;
            })
            .ToArray();
        var finite = inputs.Where(input => !input.Inexhaustible).ToArray();
        var limiting = finite
            .OrderBy(input => input.AccessibleStockQ / input.RequiredPerExtentQ)
            .ThenBy(input => input.ResourceId)
            .FirstOrDefault();
        var supported = limiting is null
            ? long.MaxValue
            : limiting.AccessibleStockQ / limiting.RequiredPerExtentQ;
        var inaccessibleLight = phenotype.RequiresLight &&
            runner.Rules.GeneratedWorld is not null &&
            tile.CurrentAccessibleLightQ == 0;
        var result = new Proto.EvolutionReactionOpportunity
        {
            ReactionId = reaction.Id.Value,
            Status = inaccessibleLight
                ? Proto.EvolutionReactionOpportunityStatus.InaccessibleLight
                : supported <= 0
                    ? Proto.EvolutionReactionOpportunityStatus.ResourceLimited
                    : Proto.EvolutionReactionOpportunityStatus.Available,
            StockSupportedExtents = supported,
            LimitingResourceId = limiting?.ResourceId ?? 0,
        };
        result.Inputs.Add(inputs);
        return result;
    }

    private static Proto.EvolutionResourceFlowForecast BuildResourceFlowForecast(
        WorldPublicationSnapshot publication,
        Proto.EvolutionOccupiedTile tile,
        uint founderCount,
        EvolutionPhenotypeSnapshot phenotype,
        ResourceId resourceId,
        long requiredPerExtentQ)
    {
        var historyHours = publication.ResourceFlowHistory.Aggregate(
            0U,
            (total, interval) => checked(total + interval.PeriodHours));
        var flows = publication.ResourceFlowHistory
            .SelectMany(interval => interval.ResourceFlows)
            .Where(flow => flow.TileId.Value == tile.TileId &&
                flow.ResourceId == resourceId)
            .ToArray();
        var inflow = SumFlows(
            flows,
            PublicationResourceFlowKind.EnvironmentalSource,
            PublicationResourceFlowKind.NeighborExchangeIn);
        var outflow = SumFlows(
            flows,
            PublicationResourceFlowKind.EnvironmentalSink,
            PublicationResourceFlowKind.NeighborExchangeOut);
        var uptake = SumFlows(flows, PublicationResourceFlowKind.OrganismUptake);
        var proposedDemand = historyHours == 0
            ? 0
            : ProposedCohortDemand(
                phenotype,
                tile,
                founderCount,
                historyHours,
                requiredPerExtentQ);
        var netRenewal = inflow >= outflow ? inflow - outflow : 0;
        var latest = publication.ResourceFlowContributors.Where(flow =>
            flow.TileId.Value == tile.TileId &&
            flow.ResourceId == resourceId &&
            flow.Kind == PublicationResourceFlowKind.OrganismUptake);
        var controlledId = publication.Gameplay.ControlledSpeciesId;
        var controlled = SaturatingSum(latest
            .Where(flow => flow.SpeciesId == controlledId)
            .Select(flow => flow.AmountQ));
        var competitors = SaturatingSum(latest
            .Where(flow => flow.SpeciesId.HasValue &&
                flow.SpeciesId.Value != controlledId)
            .Select(flow => flow.AmountQ));
        return new Proto.EvolutionResourceFlowForecast
        {
            Status = historyHours == 0
                ? Proto.EvolutionResourceFlowForecastStatus.NoHistory
                : netRenewal == 0 && proposedDemand > 0
                    ? Proto.EvolutionResourceFlowForecastStatus.NoRecentRenewal
                    : proposedDemand > netRenewal
                        ? Proto.EvolutionResourceFlowForecastStatus.ExceedsRecentRenewal
                        : Proto.EvolutionResourceFlowForecastStatus.WithinRecentRenewal,
            HistoryPeriodHours = historyHours,
            RecentEnvironmentalInflowQ = inflow,
            RecentEnvironmentalOutflowQ = outflow,
            RecentOrganismUptakeQ = uptake,
            ProposedCohortDemandQ = proposedDemand,
            LatestObservationPeriodHours = publication.CompletedTick == 0
                ? 0
                : publication.ResourceFlowPeriodHours,
            LatestControlledSpeciesUptakeQ = controlled,
            LatestObservedCompetitorUptakeQ = competitors,
        };
    }

    private static long ProposedCohortDemand(
        EvolutionPhenotypeSnapshot phenotype,
        Proto.EvolutionOccupiedTile tile,
        uint founderCount,
        uint periodHours,
        long requiredPerExtentQ)
    {
        UInt128 extentsPerOrganism;
        if (phenotype.RequiresLight)
        {
            var hourly = (UInt128)phenotype.GeneratedLightCaptureExtentsPerUnitHour *
                phenotype.FavorableCaptureEfficiencyQ * tile.CurrentAccessibleLightQ /
                ((UInt128)RatioQ.Scale * RatioQ.Scale);
            extentsPerOrganism = hourly * periodHours;
        }
        else
        {
            extentsPerOrganism = (UInt128)phenotype.MaximumCaptureExtentsPerHour *
                periodHours * phenotype.FavorableCaptureEfficiencyQ / RatioQ.Scale;
        }
        var demand = extentsPerOrganism * founderCount * (UInt128)requiredPerExtentQ;
        return demand > (UInt128)long.MaxValue ? long.MaxValue : (long)demand;
    }

    private static long SumFlows(
        IEnumerable<PublicationTileResourceFlow> flows,
        params PublicationResourceFlowKind[] kinds)
    {
        var included = kinds.ToHashSet();
        return SaturatingSum(flows
            .Where(flow => included.Contains(flow.Kind))
            .Select(flow => flow.AmountQ));
    }

    private static long SaturatingSum(IEnumerable<long> values)
    {
        var total = 0L;
        foreach (var value in values)
        {
            total = value > long.MaxValue - total ? long.MaxValue : total + value;
        }
        return total;
    }

    private static long AccessibleStock(
        CompiledWorldRules rules,
        Proto.EvolutionOccupiedTile tile,
        CompiledResource resource,
        long stock)
    {
        if (resource.EnvironmentalPhase != EnvironmentalPhase.Gas)
        {
            return stock;
        }
        var transport = rules.WorldProfile.GasEnvironment.Gases
            .Single(value => value.Resource.Id == resource.Id);
        return GasAccessibility.AccessibleQuantity(
            stock,
            tile.ElevationMeters,
            tile.BaselineVolcanismQ,
            transport.AccessibilityClass);
    }

    private static void AddActivationAssessment(
        Proto.SpeciationProposal result,
        EvolutionPhenotypeSnapshot current,
        EvolutionPhenotypeSnapshot proposed,
        IEnumerable<uint> requestedTraitIds,
        IEnumerable<Proto.EvolutionTraitOption> traitOptions)
    {
        var requested = requestedTraitIds.Distinct().Order().ToArray();
        var options = traitOptions
            .Where(option => requested.Contains(option.TraitId))
            .ToArray();
        var equivalent = Equivalent(current, proposed);
        var onlyConservationChanges = !equivalent &&
            current.ResourceConservation != proposed.ResourceConservation &&
            EquivalentExceptResourceConservation(current, proposed);
        result.BenefitTiming = equivalent
            ? Proto.EvolutionBenefitTiming.Preparatory
            : onlyConservationChanges
                ? Proto.EvolutionBenefitTiming.Conditional
                : Proto.EvolutionBenefitTiming.Immediate;

        if (equivalent)
        {
            AddWarning(
                result,
                Proto.EvolutionActivationWarningKind.NoCompiledChange,
                Proto.EvolutionActivationWarningSeverity.Caution,
                requested);
        }

        var conservationTraits = options
            .Where(option => option.EnablesResourceConservation)
            .Select(option => option.TraitId)
            .ToArray();
        if (!current.ResourceConservation && proposed.ResourceConservation)
        {
            AddWarning(
                result,
                Proto.EvolutionActivationWarningKind.ResourcePressureRequired,
                Proto.EvolutionActivationWarningSeverity.Information,
                conservationTraits);
        }

        var metabolismTraits = options
            .Where(option => option.Family.Contains("metabolism", StringComparison.Ordinal) ||
                string.Equals(option.Family, "resource-acquisition", StringComparison.Ordinal))
            .Select(option => option.TraitId)
            .ToArray();
        if (metabolismTraits.Length > 0 &&
            current.ActiveReactionIds.SequenceEqual(proposed.ActiveReactionIds))
        {
            AddWarning(
                result,
                Proto.EvolutionActivationWarningKind.NoNewActiveReaction,
                Proto.EvolutionActivationWarningSeverity.Caution,
                metabolismTraits);
        }
    }

    private static void AddWarning(
        Proto.SpeciationProposal result,
        Proto.EvolutionActivationWarningKind kind,
        Proto.EvolutionActivationWarningSeverity severity,
        IEnumerable<uint> traitIds)
    {
        var warning = new Proto.EvolutionActivationWarning
        {
            Kind = kind,
            Severity = severity,
        };
        warning.TraitIds.Add(traitIds);
        result.ActivationWarnings.Add(warning);
    }

    private static bool Equivalent(
        EvolutionPhenotypeSnapshot left,
        EvolutionPhenotypeSnapshot right) =>
        left.ResourceConservation == right.ResourceConservation &&
        EquivalentExceptResourceConservation(left, right);

    private static bool EquivalentExceptResourceConservation(
        EvolutionPhenotypeSnapshot left,
        EvolutionPhenotypeSnapshot right) =>
        left.MutationIncomeModifierQ == right.MutationIncomeModifierQ &&
        left.MaximumChangeComplexity == right.MaximumChangeComplexity &&
        left.MaximumCaptureExtentsPerHour == right.MaximumCaptureExtentsPerHour &&
        left.FavorableCaptureEfficiencyQ == right.FavorableCaptureEfficiencyQ &&
        left.GeneratedLightCaptureExtentsPerUnitHour ==
            right.GeneratedLightCaptureExtentsPerUnitHour &&
        left.RequiresLight == right.RequiresLight &&
        left.MaintenanceCostQPerHour == right.MaintenanceCostQPerHour &&
        left.ChargedReserveCapacityQ == right.ChargedReserveCapacityQ &&
        left.MinimumReproductionHealthQ == right.MinimumReproductionHealthQ &&
        left.ActiveReactionIds.SequenceEqual(right.ActiveReactionIds);

    private void Remember(string id, CachedCommand command)
    {
        commands.Add(id, command);
        commandOrder.Enqueue(id);
        if (commandOrder.Count <= MaximumRememberedCommands) return;
        commands.Remove(commandOrder.Dequeue());
    }

    private sealed record CachedCommand(string Fingerprint, byte[] ResponseBytes);
}

public sealed class EvolutionCommandConflictException(string commandId)
    : InvalidOperationException(
        $"Client command ID '{commandId}' was already used for a different proposal.");

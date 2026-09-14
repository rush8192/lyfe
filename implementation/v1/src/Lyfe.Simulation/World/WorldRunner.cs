using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.Spatial;
using Lyfe.Simulation.Ticks;
using Lyfe.Simulation.World.Generation;

namespace Lyfe.Simulation.World;

public enum WorldRunnerStatus : byte
{
    PausedReady = 1,
    Faulted = 2,
}

public sealed record WorldFault(
    ulong AttemptedTick,
    ulong LastPublishedWorldRevision,
    TickPhase Phase,
    string FailureStage);

public sealed record TickResult(
    WorldSnapshot Snapshot,
    TickChangeSet Changes,
    ImmutableArray<DeathRecord> DeathRecords);

public sealed class WorldTickException : InvalidOperationException
{
    internal WorldTickException(WorldFault fault, Exception innerException)
        : base(
            $"World tick {fault.AttemptedTick} failed in {fault.Phase} during {fault.FailureStage}.",
            innerException) => Fault = fault;

    public WorldFault Fault { get; }
}

public sealed class WorldRestoreException : InvalidOperationException
{
    internal WorldRestoreException(string message)
        : base(message)
    {
    }

    internal WorldRestoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class WorldRunner
{
    public const ulong ResourceFlowHistoryRetentionHours = 168;
    public const uint DefaultFoundationPopulation = 100;
    public const uint MaximumFoundationPopulation = 100_000;
    public const long FoundationStructuralMatterQ = 1_000;
    public const long FoundationChargedReserveQ = 5_000;

    private readonly object ownerGate = new();
    private readonly MutableWorldState world;
    private readonly ScalarTickPipeline tickPipeline = new();
    private readonly ITickFaultInjector faultInjector;
    private readonly RandomCompatibilityState randomCompatibility;
    private readonly SemanticRandomOracle randomOracle;
    private WorldSnapshot publishedSnapshot;
    private ImmutableArray<ResourceTransaction> lastCompletedTransactions = [];
    private ImmutableArray<PublicationResourceFlowHistoryInterval> resourceFlowHistory = [];
    private ImmutableArray<AcquisitionCoverageSample> lastCompletedAcquisitionSamples = [];
    private ImmutableArray<AcquisitionGateSample> lastCompletedAcquisitionGateSamples = [];
    private ImmutableArray<OrganismActionGateSample> lastCompletedActionGateSamples = [];
    private ImmutableArray<OrganismJourneyEvent> journeyEvents = [];
    private ImmutableArray<OrganismRoutineActivitySummary> routineActivitySummaries = [];
    private ImmutableArray<OrganismJourneyEvent> lastActivityPulseEvents = [];
    private ImmutableArray<LineageReviewSchedule> lineageReviewSchedules = [];
    private ImmutableArray<LineageReviewLandmark> lineageReviewLandmarks = [];
    private ImmutableArray<NotableEvent> notableEvents = [];
    private ImmutableArray<AttentionAlert> attentionAlerts = [];
    private ImmutableArray<PopulationAttentionState> populationAttentionStates = [];
    private ImmutableArray<AttentionWindowState> attentionWindowStates = [];
    private ulong nextJourneyEventId = 1;
    private ulong nextChronicleEventId = 1;
    private ulong nextAttentionAlertId = 1;

    private static readonly ulong[] PopulationMilestones =
        [100, 250, 500, 1_000, 2_500, 5_000, 10_000];

    private WorldRunner(
        WorldId worldId,
        CompiledWorldRules rules,
        RootRandomSeed rootSeed,
        GameSetupCommand setup,
        ITickFaultInjector faultInjector)
    {
        if (!worldId.IsValid)
        {
            throw new ArgumentException("A world ID must be nonzero.", nameof(worldId));
        }

        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(faultInjector);
        var runtimeRules = WorldRuntimeRealizer.Realize(rules, rootSeed);
        ValidateSetup(runtimeRules, setup);
        if (runtimeRules.TickDurationHours == 0)
        {
            throw new ArgumentException("A compiled world must have a positive tick duration.", nameof(rules));
        }

        WorldId = worldId;
        Rules = runtimeRules;
        this.faultInjector = faultInjector;
        randomCompatibility = RandomCompatibility.ForSeed(rootSeed);
        randomOracle = new SemanticRandomOracle(rootSeed);
        world = new MutableWorldState(runtimeRules);
        InitializeGamePopulation(rootSeed, setup);
        populationAttentionStates = world.GetSpeciesIdsInCanonicalOrder()
            .Select(id =>
            {
                var aboveThreshold = world.GetSpecies(id).Population >
                    PopulationAttentionRules.DangerThreshold;
                return new PopulationAttentionState(id, aboveThreshold, aboveThreshold, 0);
            })
            .ToImmutableArray();
        attentionWindowStates = world.GetSpeciesIdsInCanonicalOrder()
            .Select(id => new AttentionWindowState(
                id,
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
                [new PopulationAttentionSample(0, world.GetSpecies(id).Population)]))
            .ToImmutableArray();
        journeyEvents = world.GetOrganismIdsInCanonicalOrder()
            .Select(id =>
            {
                var organism = world.GetOrganism(id);
                return CreateJourneyEvent(
                    0,
                    TickPhase.LifecycleAndReproduction,
                    OrganismJourneyEventFamily.Birth,
                    organism,
                    null,
                    null,
                    null,
                    0,
                    0,
                    []);
            })
            .ToImmutableArray();
        lastActivityPulseEvents = journeyEvents;
        world.ValidateInvariants();
        world.ValidateGameplayOutcomeInvariants();
        publishedSnapshot = CaptureWorkingSnapshot(0, 0, 0);
    }

    private WorldRunner(
        CompiledWorldRules rules,
        WorldPersistenceSnapshot persisted,
        ITickFaultInjector faultInjector)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(persisted);
        ArgumentNullException.ThrowIfNull(faultInjector);

        try
        {
            var metadata = persisted.Metadata;
            var runtimeRules = WorldRuntimeRealizer.Realize(
                rules,
                metadata.RandomCompatibility.RootSeed);
            ValidateRestoreCompatibility(runtimeRules, persisted);
            WorldId = metadata.Boundary.WorldId;
            Rules = runtimeRules;
            this.faultInjector = faultInjector;
            randomCompatibility = metadata.RandomCompatibility;
            randomOracle = new SemanticRandomOracle(randomCompatibility.RootSeed);
            world = MutableWorldState.Restore(runtimeRules, persisted.State);
            world.ValidateGameplayOutcomeInvariants();
            lastCompletedTransactions = RestoreTransactions(
                persisted.State.LastCompletedTransactions,
                metadata.Boundary.CompletedTick,
                world);
            resourceFlowHistory = RestoreResourceFlowHistory(
                persisted.State.ResourceFlowHistory,
                metadata.Boundary.CompletedTick,
                metadata.Boundary.SimulatedHours,
                runtimeRules.TickDurationHours,
                world);
            if (!resourceFlowHistory.IsEmpty &&
                !resourceFlowHistory[^1].ResourceFlows.SequenceEqual(
                    BuildPublicationResourceFlows(lastCompletedTransactions)))
            {
                throw new WorldRestoreException(
                    "Persisted resource-flow history disagrees with the completed ledger boundary.");
            }
            journeyEvents = persisted.State.JourneyEvents
                .Select(ToJourneyEvent)
                .ToImmutableArray();
            routineActivitySummaries = persisted.State.RoutineActivitySummaries
                .Select(ToRoutineActivitySummary)
                .ToImmutableArray();
            lineageReviewSchedules = persisted.State.LineageReviewSchedules
                .Select(ToLineageReviewSchedule)
                .ToImmutableArray();
            lineageReviewLandmarks = persisted.State.LineageReviewLandmarks
                .Select(ToLineageReviewLandmark)
                .ToImmutableArray();
            notableEvents = persisted.State.NotableEvents
                .Select(ToNotableEvent)
                .ToImmutableArray();
            attentionAlerts = persisted.State.AttentionAlerts
                .Select(ToAttentionAlert)
                .ToImmutableArray();
            populationAttentionStates = persisted.State.PopulationAttentionStates
                .Select(ToPopulationAttentionState)
                .ToImmutableArray();
            attentionWindowStates = persisted.State.AttentionWindowStates
                .Select(ToAttentionWindowState)
                .ToImmutableArray();
            lastActivityPulseEvents = [];
            nextJourneyEventId = persisted.State.NextJourneyEventId;
            nextChronicleEventId = checked(lineageReviewLandmarks
                .Select(value => value.EventId)
                .Concat(notableEvents.Select(value => value.EventId))
                .DefaultIfEmpty(0UL)
                .Max() + 1);
            nextAttentionAlertId = checked(attentionAlerts
                .Select(value => value.AlertId)
                .DefaultIfEmpty(0UL)
                .Max() + 1);
            ResourceLedgerOracle.Reconcile(runtimeRules.RulePack, lastCompletedTransactions);
            publishedSnapshot = CaptureWorkingSnapshot(
                metadata.Boundary.CompletedTick,
                metadata.Boundary.WorldRevision,
                metadata.Boundary.SimulatedHours,
                lastCompletedTransactions);
            if (publishedSnapshot != metadata.Boundary)
            {
                throw new WorldRestoreException(
                    "The restored logical world does not match its recorded completed boundary.");
            }
        }
        catch (WorldRestoreException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            OverflowException or
            KeyNotFoundException)
        {
            throw new WorldRestoreException(
                "The persisted logical world is malformed or incompatible.",
                exception);
        }
    }

    public WorldId WorldId { get; }

    public CompiledWorldRules Rules { get; }

    public WorldRunnerStatus Status { get; private set; } = WorldRunnerStatus.PausedReady;

    public WorldFault? Fault { get; private set; }

    public GameStateSnapshot GameState
    {
        get
        {
            lock (ownerGate)
            {
                return world.GetGameplayState();
            }
        }
    }

    public TickChangeSet? LastCompletedTickChanges { get; private set; }

    public ImmutableArray<IntrinsicDeathEvidence> LastIntrinsicDeathAssessments { get; private set; }

    public ImmutableArray<DeathRecord> LastDeathRecords { get; private set; }

    internal MutableWorldState MutableWorld => world;

    internal string ComputeWorkingStateHashForTesting(
        ImmutableArray<ResourceTransaction>? completedTickTransactions = null) =>
        WorldStateHasher.Hash(
            new WorldSnapshotContent(
                WorldId,
                publishedSnapshot.CompletedTick,
                publishedSnapshot.WorldRevision,
                publishedSnapshot.SimulatedHours,
                Rules.TickDurationHours,
                Status,
                randomCompatibility),
            world,
            completedTickTransactions ?? GetLastCompletedTransactions());

    public static WorldRunner CreateFoundation(
        WorldId worldId,
        CompiledWorldRules rules,
        RootRandomSeed rootSeed,
        uint founderCount = DefaultFoundationPopulation,
        FounderInitializationMode founderInitialization = FounderInitializationMode.LegacyFixture) =>
        new(
            worldId,
            rules,
            rootSeed,
            DefaultSandboxSetup(rules, rootSeed, founderCount, founderInitialization),
            NoTickFaultInjector.Instance);

    public static WorldRunner CreateGame(
        WorldId worldId,
        CompiledWorldRules rules,
        RootRandomSeed rootSeed,
        GameSetupCommand setup) =>
        new(worldId, rules, rootSeed, setup, NoTickFaultInjector.Instance);

    public static WorldRunner Restore(
        CompiledWorldRules rules,
        WorldPersistenceSnapshot persisted) =>
        new(rules, persisted, NoTickFaultInjector.Instance);

    internal static WorldRunner CreateFoundationForTesting(
        WorldId worldId,
        CompiledWorldRules rules,
        RootRandomSeed rootSeed,
        uint founderCount,
        ITickFaultInjector faultInjector) =>
        new(
            worldId,
            rules,
            rootSeed,
            DefaultSandboxSetup(
                rules,
                rootSeed,
                founderCount,
                FounderInitializationMode.LegacyFixture),
            faultInjector);

    public WorldSnapshot CaptureSnapshot()
    {
        lock (ownerGate)
        {
            return publishedSnapshot;
        }
    }

    public WorldPublicationSnapshot CapturePublicationSnapshot()
    {
        lock (ownerGate)
        {
            if (Status != WorldRunnerStatus.PausedReady)
            {
                throw new InvalidOperationException(
                    "A faulted world cannot publish its partially mutated working state.");
            }

            var tiles = ImmutableArray.CreateBuilder<PublicationTile>(world.TileCount);
            var resourceHandles = world.GetResourceHandlesInCanonicalOrder();
            foreach (var tileId in world.GetTileIdsInCanonicalOrder())
            {
                var tile = world.GetTile(tileId);
                var stocks = ImmutableArray.CreateBuilder<PublicationResourceStock>(
                    resourceHandles.Length);
                foreach (var resource in resourceHandles)
                {
                    stocks.Add(new PublicationResourceStock(
                        resource.Id,
                        world.GetTileResource(tileId, resource)));
                }

                tiles.Add(new PublicationTile(
                    tile.Id,
                    tile.X,
                    tile.Y,
                    tile.ElevationMeters,
                    world.Rules.WorldProfile.Tiles[checked((int)tile.Id.Value)].BaselineVolcanismQ,
                    stocks.MoveToImmutable()));
            }

            var species = world.GetSpeciesIdsInCanonicalOrder()
                .Select(id =>
                {
                    var state = world.GetSpecies(id);
                    var genome = world.GetGenome(state.GenomeId);
                    var phenotype = world.GetCompiledPhenotype(id);
                    return new PublicationSpecies(
                        state.Id,
                        state.GenomeId,
                        genome.FounderGenomeId,
                        genome.FounderAllocationId,
                        state.Population,
                        genome.GenomeHash,
                        genome.AcquiredTraits,
                        state.Evolution.Authority,
                        state.Evolution.MutationBalanceQ,
                        state.Evolution.EvolutionRevision,
                        state.Evolution.SpeciationNotBeforeTick,
                        state.Evolution.AverageHealthQ,
                        state.Evolution.LastIncomeQ,
                        phenotype.MutationIncomeModifierQ,
                        state.Lineage.ParentSpeciesId,
                        state.Lineage.CreatedTick,
                        state.Lineage.ExtinctTick,
                        BuildPublicationHabitatProfile(phenotype));
                })
                .ToImmutableArray();
            var acquisitionByOrganism = lastCompletedAcquisitionSamples
                .ToDictionary(sample => sample.OrganismId);
            var acquisitionGatesByOrganism = lastCompletedAcquisitionGateSamples
                .GroupBy(sample => sample.OrganismId)
                .ToDictionary(group => group.Key, group => group.ToImmutableArray());
            var actionGatesByOrganism = lastCompletedActionGateSamples
                .GroupBy(sample => sample.OrganismId)
                .ToDictionary(group => group.Key, group => group.ToImmutableArray());
            var organisms = world.GetOrganismIdsInCanonicalOrder()
                .Select(id => ToPublicationOrganism(
                    world.GetOrganism(id),
                    acquisitionByOrganism.GetValueOrDefault(id).Resources,
                    acquisitionGatesByOrganism.GetValueOrDefault(id, []),
                    actionGatesByOrganism.GetValueOrDefault(id, [])))
                .ToImmutableArray();
            var remnants = world.GetRemnantIdsInCanonicalOrder()
                .Select(id => ToPublicationRemnant(world.GetRemnant(id)))
                .ToImmutableArray();
            var game = world.GetGameplayState();
            var gameplay = new PublicationGameState(
                game.Mode,
                game.RunStatus,
                game.LossReason,
                game.ControlledSpeciesId,
                game.GameplayRevision,
                game.EndedTick,
                game.Roots.Select(root => new PublicationAbiogenesisRoot(
                    root.SpeciesId,
                    root.FounderGenomeId,
                    root.FounderAllocationId,
                    root.StartingTileId,
                    root.InitialPopulation,
                    root.PlayerSelected)).ToImmutableArray(),
                game.MutationLockedSpeciesIds);
            var profile = Rules.WorldProfile;
            return new WorldPublicationSnapshot(
                publishedSnapshot.WorldId,
                publishedSnapshot.CompletedTick,
                publishedSnapshot.WorldRevision,
                publishedSnapshot.SimulatedHours,
                publishedSnapshot.TickDurationHours,
                PublicationWorldLifecycle.PausedReady,
                Rules.Identity.WorldRulesHash,
                profile.Width,
                profile.Height,
                profile.WrapX,
                profile.WrapY,
                gameplay,
                tiles.MoveToImmutable(),
                species,
                organisms,
                remnants,
                journeyEvents,
                routineActivitySummaries,
                lastActivityPulseEvents,
                Rules.RulePack.Resources
                    .OrderBy(resource => resource.Id.Value)
                    .Select(resource => new PublicationResourceDefinition(
                        resource.Id,
                        resource.StableKey,
                        resource.DisplayName,
                        resource.BiologicalForm,
                        resource.EnvironmentalPhase))
                    .ToImmutableArray(),
                BuildPublicationResourceFlows(lastCompletedTransactions),
                Rules.TickDurationHours,
                resourceFlowHistory,
                Rules.RulePack.Reactions
                    .OrderBy(reaction => reaction.Id.Value)
                    .Select(reaction => new PublicationReactionDefinition(
                        reaction.Id,
                        reaction.StableKey,
                        reaction.DisplayName))
                    .ToImmutableArray(),
                BuildPublicationResourceFlowContributors(lastCompletedTransactions),
                lineageReviewLandmarks,
                notableEvents,
                attentionAlerts);
        }
    }

    private PublicationHabitatProfile BuildPublicationHabitatProfile(
        CompiledPhenotype phenotype)
    {
        var metabolismInputs = phenotype.Processes
            .Select(process => Rules.RulePack.Reactions[process.Reaction.DenseSlot])
            .Where(reaction => reaction.ProcessKind == ProcessKind.ExternalEnergyCapture)
            .SelectMany(reaction => reaction.Inputs)
            .Where(input => Rules.RulePack.Resources[input.Resource.DenseSlot].BiologicalForm !=
                BiologicalForm.Boundary)
            .Select(input => input.Resource.Id)
            .ToHashSet();
        var growthInputs = phenotype.Processes
            .Select(process => Rules.RulePack.Reactions[process.Reaction.DenseSlot])
            .Where(reaction => reaction.ProcessKind == ProcessKind.BiomassAssembly)
            .SelectMany(reaction => reaction.Inputs)
            .Where(input => input.Resource.Id != ResourceId.From(3) &&
                Rules.RulePack.Resources[input.Resource.DenseSlot].BiologicalForm !=
                    BiologicalForm.Boundary)
            .Select(input => input.Resource.Id)
            .ToHashSet();
        var healthRequirements = phenotype.Physiology.CommittedMicronutrientQuotas
            .Select(quota => quota.Resource.Id)
            .ToHashSet();
        var chemical = phenotype.Physiology.ChemicalResponse;
        var hazards = new Dictionary<ResourceId, (long SoftQ, long HardQ)>
        {
            [ResourceId.From(10)] = (
                chemical.HydrogenSulfideSoftThresholdQ,
                chemical.HydrogenSulfideHardThresholdQ),
            [ResourceId.From(11)] = (
                chemical.SulfurDioxideSoftThresholdQ,
                chemical.SulfurDioxideHardThresholdQ),
        };
        var relevant = metabolismInputs
            .Concat(growthInputs)
            .Concat(healthRequirements)
            .Concat(hazards.Keys)
            .Distinct()
            .OrderBy(id => id.Value)
            .Select(id =>
            {
                var hazard = hazards.GetValueOrDefault(id);
                return new PublicationRelevantResource(
                    id,
                    metabolismInputs.Contains(id),
                    growthInputs.Contains(id),
                    healthRequirements.Contains(id),
                    hazards.ContainsKey(id),
                    hazard.SoftQ,
                    hazard.HardQ);
            })
            .ToImmutableArray();
        var temperature = phenotype.Physiology.TemperatureResponse;
        return new PublicationHabitatProfile(
            temperature.PreferredMinimumMilliC,
            temperature.PreferredMaximumMilliC,
            temperature.HardMinimumMilliC,
            temperature.HardMaximumMilliC,
            phenotype.Physiology.Spatial.CanOccupyTerrestrial,
            phenotype.Physiology.OpeningMetabolism.RequiresLight,
            relevant);
    }

    private static ImmutableArray<PublicationTileResourceFlow> BuildPublicationResourceFlows(
        ImmutableArray<ResourceTransaction> transactions) => transactions
        .SelectMany(transaction => transaction.MatterEntries
            .Where(entry => entry.Account.OwnerKind == MatterAccountOwnerKind.Tile &&
                entry.DeltaQ != 0)
            .Select(entry => new
            {
                TileId = TileId.FromRowMajorIndex(checked((uint)entry.Account.OwnerId)),
                entry.Account.ResourceId,
                Kind = ToPublicationFlowKind(transaction.Cause, entry.DeltaQ),
                AmountQ = checked(Math.Abs(entry.DeltaQ)),
            }))
        .GroupBy(value => (value.TileId, value.ResourceId, value.Kind))
        .OrderBy(group => group.Key.TileId.Value)
        .ThenBy(group => group.Key.ResourceId.Value)
        .ThenBy(group => group.Key.Kind)
        .Select(group => new PublicationTileResourceFlow(
            group.Key.TileId,
            group.Key.ResourceId,
            group.Key.Kind,
            group.Sum(value => value.AmountQ)))
        .ToImmutableArray();

    internal static ImmutableArray<PublicationResourceFlowHistoryInterval>
        AppendResourceFlowHistory(
            ImmutableArray<PublicationResourceFlowHistoryInterval> history,
            ulong completedTick,
            ulong endSimulatedHour,
            uint periodHours,
            ImmutableArray<ResourceTransaction> transactions)
    {
        var retainedAfterHour = endSimulatedHour > ResourceFlowHistoryRetentionHours
            ? endSimulatedHour - ResourceFlowHistoryRetentionHours
            : 0;
        return history
            .Add(new PublicationResourceFlowHistoryInterval(
                completedTick,
                endSimulatedHour,
                periodHours,
                BuildPublicationResourceFlows(transactions)))
            .Where(interval => interval.EndSimulatedHour > retainedAfterHour)
            .ToImmutableArray();
    }

    private ImmutableArray<PublicationTileResourceFlowContributor>
        BuildPublicationResourceFlowContributors(
            ImmutableArray<ResourceTransaction> transactions) => transactions
        .SelectMany(transaction => transaction.MatterEntries
            .Where(entry => entry.Account.OwnerKind == MatterAccountOwnerKind.Tile &&
                entry.DeltaQ != 0)
            .Select(entry => new
            {
                TileId = TileId.FromRowMajorIndex(checked((uint)entry.Account.OwnerId)),
                entry.Account.ResourceId,
                Kind = ToPublicationFlowKind(transaction.Cause, entry.DeltaQ),
                Process = ToPublicationFlowProcess(transaction.Cause),
                ReactionId = UsesCompiledReaction(transaction.Cause)
                    ? transaction.ReactionId
                    : (ReactionId?)null,
                SpeciesId = transaction.ActorId == default
                    ? (SpeciesId?)null
                    : world.GetOrganism(transaction.ActorId).SpeciesId,
                AmountQ = checked(Math.Abs(entry.DeltaQ)),
            }))
        .GroupBy(value => (
            value.TileId,
            value.ResourceId,
            value.Kind,
            value.Process,
            value.ReactionId,
            value.SpeciesId))
        .OrderBy(group => group.Key.TileId.Value)
        .ThenBy(group => group.Key.ResourceId.Value)
        .ThenBy(group => group.Key.Kind)
        .ThenBy(group => group.Key.Process)
        .ThenBy(group => group.Key.ReactionId?.Value ?? 0)
        .ThenBy(group => group.Key.SpeciesId?.Value ?? 0)
        .Select(group => new PublicationTileResourceFlowContributor(
            group.Key.TileId,
            group.Key.ResourceId,
            group.Key.Kind,
            group.Key.Process,
            group.Key.ReactionId,
            group.Key.SpeciesId,
            group.Sum(value => value.AmountQ)))
        .ToImmutableArray();

    private static PublicationResourceFlowProcessKind ToPublicationFlowProcess(
        LedgerCause cause) => cause switch
        {
            LedgerCause.ExternalEnergyCapture =>
                PublicationResourceFlowProcessKind.ExternalEnergyCapture,
            LedgerCause.ParticulateDigestion =>
                PublicationResourceFlowProcessKind.ParticulateDigestion,
            LedgerCause.EnvironmentalGasSource =>
                PublicationResourceFlowProcessKind.EnvironmentalGasSource,
            LedgerCause.EnvironmentalGasSink =>
                PublicationResourceFlowProcessKind.EnvironmentalGasSink,
            LedgerCause.EnvironmentalGasExchange =>
                PublicationResourceFlowProcessKind.EnvironmentalGasExchange,
            LedgerCause.MandatoryMaintenance =>
                PublicationResourceFlowProcessKind.MandatoryMaintenance,
            LedgerCause.BiomassAssembly =>
                PublicationResourceFlowProcessKind.BiomassAssembly,
            LedgerCause.MicronutrientUptake =>
                PublicationResourceFlowProcessKind.MicronutrientUptake,
            LedgerCause.EnvironmentalResourceSource =>
                PublicationResourceFlowProcessKind.EnvironmentalResourceSource,
            LedgerCause.RemnantDecay =>
                PublicationResourceFlowProcessKind.RemnantDecay,
            _ => throw new ArgumentOutOfRangeException(nameof(cause)),
        };

    private static bool UsesCompiledReaction(LedgerCause cause) => cause is
        LedgerCause.ExternalEnergyCapture or
        LedgerCause.ParticulateDigestion or
        LedgerCause.MandatoryMaintenance or
        LedgerCause.BiomassAssembly;

    private static PublicationResourceFlowKind ToPublicationFlowKind(
        LedgerCause cause,
        long deltaQ) => cause switch
    {
        LedgerCause.EnvironmentalGasSource when deltaQ > 0 =>
            PublicationResourceFlowKind.EnvironmentalSource,
        LedgerCause.EnvironmentalResourceSource when deltaQ > 0 =>
            PublicationResourceFlowKind.EnvironmentalSource,
        LedgerCause.EnvironmentalGasSink when deltaQ < 0 =>
            PublicationResourceFlowKind.EnvironmentalSink,
        LedgerCause.EnvironmentalGasExchange when deltaQ > 0 =>
            PublicationResourceFlowKind.NeighborExchangeIn,
        LedgerCause.EnvironmentalGasExchange when deltaQ < 0 =>
            PublicationResourceFlowKind.NeighborExchangeOut,
        LedgerCause.ExternalEnergyCapture or
        LedgerCause.ParticulateDigestion or
        LedgerCause.MandatoryMaintenance or
        LedgerCause.BiomassAssembly or
        LedgerCause.MicronutrientUptake or
        LedgerCause.RemnantDecay when deltaQ > 0 =>
            PublicationResourceFlowKind.OrganismRelease,
        LedgerCause.ExternalEnergyCapture or
        LedgerCause.ParticulateDigestion or
        LedgerCause.MandatoryMaintenance or
        LedgerCause.BiomassAssembly or
        LedgerCause.MicronutrientUptake when deltaQ < 0 =>
            PublicationResourceFlowKind.OrganismUptake,
        _ => throw new InvalidOperationException(
            $"Ledger cause {cause} produced an invalid tile delta {deltaQ}."),
    };

    public WorldPersistenceMetadata CapturePersistenceMetadata()
    {
        lock (ownerGate)
        {
            if (Status != WorldRunnerStatus.PausedReady)
            {
                throw new InvalidOperationException(
                    "A faulted world cannot expose persistence metadata for partial state.");
            }

            return CapturePersistenceMetadataCore();
        }
    }

    public WorldPersistenceSnapshot CapturePersistenceSnapshot()
    {
        lock (ownerGate)
        {
            if (Status != WorldRunnerStatus.PausedReady)
            {
                throw new InvalidOperationException(
                    "A faulted world cannot expose partial state for persistence.");
            }

            var counters = world.CaptureIdContinuationState();
            var resourceHandles = world.GetResourceHandlesInCanonicalOrder();
            var tileResources = ImmutableArray.CreateBuilder<PersistenceTileResource>(
                checked(world.TileCount * resourceHandles.Length));
            foreach (var tileId in world.GetTileIdsInCanonicalOrder())
            {
                foreach (var resource in resourceHandles)
                {
                    tileResources.Add(new PersistenceTileResource(
                        tileId.Value,
                        resource.Id.Value,
                        world.GetTileResource(tileId, resource)));
                }
            }

            var gasTileRemainders = ImmutableArray.CreateBuilder<PersistenceGasTileRemainder>();
            for (var tileIndex = 0; tileIndex < world.TileCount; tileIndex++)
            {
                var tileId = TileId.FromRowMajorIndex((uint)tileIndex);
                for (var gasSlot = 0; gasSlot < Rules.WorldProfile.GasEnvironment.Gases.Length; gasSlot++)
                {
                    var gas = Rules.WorldProfile.GasEnvironment.Gases[gasSlot];
                    var remainder = world.GetGasTileRemainders(gasSlot, tileId);
                    gasTileRemainders.Add(new PersistenceGasTileRemainder(
                        tileId.Value, gas.Resource.Id.Value, remainder.SourceQ, remainder.SinkQ));
                }
            }

            var edges = world.GetGasEdges();
            var gasEdgeRemainders = ImmutableArray.CreateBuilder<PersistenceGasEdgeRemainder>();
            for (var edgeSlot = 0; edgeSlot < edges.Length; edgeSlot++)
            {
                var edge = edges[edgeSlot];
                for (var gasSlot = 0; gasSlot < Rules.WorldProfile.GasEnvironment.Gases.Length; gasSlot++)
                {
                    var gas = Rules.WorldProfile.GasEnvironment.Gases[gasSlot];
                    gasEdgeRemainders.Add(new PersistenceGasEdgeRemainder(
                        edge.LowerTileId.Value,
                        edge.HigherTileId.Value,
                        gas.Resource.Id.Value,
                        world.GetGasExchangeRemainder(gasSlot, edgeSlot)));
                }
            }

            var genomes = world.GetGenomeIdsInCanonicalOrder()
                .Select(id =>
                {
                    var genome = world.GetGenome(id);
                    return new PersistenceGenome(
                        id.Value,
                        genome.FounderGenomeId.Value,
                        genome.FounderAllocationId.Value,
                        genome.AcquiredTraits.Select(trait => trait.Value).ToImmutableArray(),
                        genome.GenomeHash);
                })
                .ToImmutableArray();
            var species = world.GetSpeciesIdsInCanonicalOrder()
                .Select(id =>
                {
                    var current = world.GetSpecies(id);
                    var account = current.Evolution;
                    var lineage = current.Lineage;
                    return new PersistenceSpecies(
                        id.Value,
                        current.GenomeId.Value,
                        current.Population,
                        (byte)account.Authority,
                        account.MutationBalanceQ,
                        (ulong)account.MutationIncomeRemainder,
                        (ulong)(account.MutationIncomeRemainder >> 64),
                        account.SpeciationNotBeforeTick,
                        account.SpeciationOrdinal,
                        account.EvolutionRevision,
                        account.AverageHealthQ,
                        account.LastIncomeQ,
                        account.Pressure.EnergyShortageQ,
                        account.Pressure.StarvationQ,
                        lineage.ParentSpeciesId?.Value ?? 0,
                        lineage.FoundingEventId,
                        lineage.CreatedTick,
                        lineage.ExtinctTick,
                        lineage.FounderCounts.Select(count =>
                            new PersistenceFounderCount(count.TileId.Value, count.Count)).ToImmutableArray(),
                        lineage.AcquiredTraitDelta.Select(trait => trait.Value).ToImmutableArray());
                })
                .ToImmutableArray();
            var speciationEvents = world.GetSpeciationEvents()
                .Select(value => new PersistenceSpeciationEvent(
                    value.EventId,
                    value.Tick,
                    value.AncestorSpeciesId.Value,
                    value.DescendantSpeciesId.Value,
                    (byte)value.ActorKind,
                    value.FounderCounts.Select(count =>
                        new PersistenceFounderCount(count.TileId.Value, count.Count)).ToImmutableArray(),
                    value.FounderSelectionDigest,
                    value.TraitDelta.Select(trait => trait.Value).ToImmutableArray(),
                    value.MutationPriceQ,
                    value.BalanceBeforeQ,
                    value.DuplicatedBalanceAfterQ,
                    value.ChangeComplexity))
                .ToImmutableArray();
            var organisms = world.GetOrganismIdsInCanonicalOrder()
                .Select(id => ToPersistenceOrganism(world.GetOrganism(id)))
                .ToImmutableArray();
            var remnants = world.GetRemnantIdsInCanonicalOrder()
                .Select(id => ToPersistenceRemnant(world.GetRemnant(id)))
                .ToImmutableArray();
            var transactions = lastCompletedTransactions
                .Select(ToPersistenceTransaction)
                .ToImmutableArray();
            var persistedResourceFlowHistory = resourceFlowHistory
                .Select(value => new PersistenceResourceFlowHistoryInterval(
                    value.CompletedTick,
                    value.EndSimulatedHour,
                    value.PeriodHours,
                    value.ResourceFlows.Select(flow => new PersistenceTileResourceFlow(
                        flow.TileId.Value,
                        flow.ResourceId.Value,
                        (byte)flow.Kind,
                        flow.AmountQ)).ToImmutableArray()))
                .ToImmutableArray();
            var persistedJourneyEvents = journeyEvents
                .Select(ToPersistenceJourneyEvent)
                .ToImmutableArray();
            var persistedRoutineSummaries = routineActivitySummaries
                .Select(ToPersistenceRoutineActivitySummary)
                .ToImmutableArray();
            var persistedLineageReviewSchedules = lineageReviewSchedules
                .Select(ToPersistenceLineageReviewSchedule)
                .ToImmutableArray();
            var persistedLineageReviewLandmarks = lineageReviewLandmarks
                .Select(ToPersistenceLineageReviewLandmark)
                .ToImmutableArray();
            var persistedNotableEvents = notableEvents
                .Select(ToPersistenceNotableEvent)
                .ToImmutableArray();
            var persistedAttentionAlerts = attentionAlerts
                .Select(ToPersistenceAttentionAlert)
                .ToImmutableArray();
            var persistedPopulationAttentionStates = populationAttentionStates
                .Select(ToPersistencePopulationAttentionState)
                .ToImmutableArray();
            var persistedAttentionWindowStates = attentionWindowStates
                .Select(ToPersistenceAttentionWindowState)
                .ToImmutableArray();
            var game = world.GetGameplayState();
            var persistedGame = new PersistenceGameState(
                (byte)game.Mode,
                (byte)game.RunStatus,
                (byte)game.LossReason,
                game.ControlledSpeciesId.Value,
                game.GameplayRevision,
                game.EndedTick,
                game.Roots.Select(root => new PersistenceAbiogenesisRoot(
                    root.SpeciesId.Value,
                    root.FounderGenomeId.Value,
                    root.FounderAllocationId.Value,
                    root.StartingTileId.Value,
                    root.InitialPopulation,
                    root.PlayerSelected)).ToImmutableArray(),
                game.MutationLockedSpeciesIds.Select(id => id.Value).ToImmutableArray());

            return new WorldPersistenceSnapshot(
                CapturePersistenceMetadataCore(),
                new WorldPersistenceState(
                    counters.NextGenomeId,
                    counters.NextSpeciesId,
                    counters.NextOrganismId,
                    counters.NextRemnantId,
                    nextJourneyEventId,
                    tileResources.MoveToImmutable(),
                    gasTileRemainders.ToImmutable(),
                    gasEdgeRemainders.ToImmutable(),
                    genomes,
                    species,
                    speciationEvents,
                    organisms,
                    remnants,
                    persistedJourneyEvents,
                    persistedRoutineSummaries,
                    persistedResourceFlowHistory,
                    transactions,
                    persistedGame,
                    persistedLineageReviewSchedules,
                    persistedLineageReviewLandmarks,
                    persistedNotableEvents,
                    persistedAttentionAlerts,
                    persistedPopulationAttentionStates,
                    persistedAttentionWindowStates));
        }
    }

    public TickResult AdvanceOneTick()
    {
        lock (ownerGate)
        {
            if (Status == WorldRunnerStatus.Faulted)
            {
                throw new InvalidOperationException("A faulted world cannot advance.");
            }
            if (world.GetGameplayState().RunStatus != GameRunStatus.Active)
            {
                throw new InvalidOperationException("An ended game cannot advance.");
            }

            var nextTick = checked(publishedSnapshot.CompletedTick + 1);
            var nextRevision = checked(publishedSnapshot.WorldRevision + 1);
            var nextSimulatedHours = checked(
                publishedSnapshot.SimulatedHours + Rules.TickDurationHours);
            var scratch = new TickScratch(nextTick);
            var context = new TickExecutionContext(
                nextRevision,
                nextTick,
                Rules.TickDurationHours,
                Rules.Identity.WorldRulesHash,
                faultInjector,
                randomOracle,
                scratch);

            try
            {
                var priorPopulations = world.GetSpeciesIdsInCanonicalOrder()
                    .ToDictionary(id => id, id => world.GetSpecies(id).Population);
                var changes = tickPipeline.Execute(world, context);
                var completedTransactions = GetTransactions(changes);
                var nextResourceFlowHistory = AppendResourceFlowHistory(
                    resourceFlowHistory,
                    nextTick,
                    nextSimulatedHours,
                    Rules.TickDurationHours,
                    completedTransactions);
                var completedJourneyEvents = BuildJourneyEvents(
                    nextTick,
                    changes,
                    completedTransactions,
                    scratch);
                lastActivityPulseEvents = completedJourneyEvents;
                journeyEvents = journeyEvents.AddRange(completedJourneyEvents.Where(value =>
                    value.Family != OrganismJourneyEventFamily.ResourceAbsorption));
                routineActivitySummaries = UpdateRoutineActivitySummaries(
                    routineActivitySummaries,
                    completedJourneyEvents,
                    checked((nextTick - 1) * Rules.TickDurationHours),
                    nextSimulatedHours,
                    Rules.TickDurationHours);
                UpdateLineageReviewActivationEvidence(
                    completedJourneyEvents,
                    completedTransactions,
                    nextTick);
                AppendNotableTickEvents(
                    priorPopulations,
                    completedJourneyEvents,
                    completedTransactions,
                    nextTick,
                    nextSimulatedHours);
                AppendPopulationDangerEvents(
                    priorPopulations,
                    nextTick,
                    nextSimulatedHours);
                AppendWindowAttentionEvents(nextTick, nextSimulatedHours);
                AppendDueLineageReviewLandmarks(nextTick, nextSimulatedHours);
                AppendAttentionAlerts(nextTick, nextSimulatedHours);
                var nextSnapshot = CaptureWorkingSnapshot(
                    nextTick,
                    nextRevision,
                    nextSimulatedHours,
                    completedTransactions);

                LastCompletedTickChanges = changes;
                LastIntrinsicDeathAssessments =
                    scratch.RequireIntrinsicDeathAssessments(nextTick);
                LastDeathRecords = scratch.RequireDeathRecords(nextTick);
                lastCompletedAcquisitionSamples =
                    scratch.RequireAcquisitionSamples(nextTick);
                lastCompletedAcquisitionGateSamples =
                    scratch.RequireAcquisitionGateSamples(nextTick);
                lastCompletedActionGateSamples = scratch.GetOrganismActionGates(nextTick);
                lastCompletedTransactions = completedTransactions;
                resourceFlowHistory = nextResourceFlowHistory;
                publishedSnapshot = nextSnapshot;
                return new TickResult(nextSnapshot, changes, LastDeathRecords);
            }
            catch (Exception exception)
            {
                world.MarkFaulted();
                var executionFailure = exception as TickPhaseExecutionException;
                var fault = new WorldFault(
                    nextTick,
                    publishedSnapshot.WorldRevision,
                    executionFailure?.Phase ?? TickPhase.Finalization,
                    executionFailure?.Stage.ToString() ?? "Unknown");
                Fault = fault;
                Status = WorldRunnerStatus.Faulted;
                throw new WorldTickException(fault, exception);
            }
        }
    }

    private ImmutableArray<OrganismJourneyEvent> BuildJourneyEvents(
        ulong tick,
        TickChangeSet changes,
        ImmutableArray<ResourceTransaction> transactions,
        TickScratch scratch)
    {
        var pending = new List<PendingJourneyEvent>();
        var deaths = scratch.RequireDeathRecords(tick);
        var deathByOrganism = deaths.ToDictionary(value => value.OrganismId);

        OrganismSnapshot FindOrganism(OrganismId id) => world.GetOrganism(id);

        foreach (var receipt in scratch.GetReproductionReceipts(tick))
        {
            var parent = FindOrganism(receipt.ParentId);
            pending.Add(new PendingJourneyEvent(
                TickPhase.LifecycleAndReproduction,
                OrganismJourneyEventFamily.Reproduction,
                receipt.ParentId,
                receipt.SpeciesId,
                receipt.TileId,
                receipt.ParentPositionXQ,
                receipt.ParentPositionYQ,
                receipt.OffspringId,
                null,
                null,
                0,
                checked((uint)parent.SuccessfulReproductionCount),
                []));
            pending.Add(new PendingJourneyEvent(
                TickPhase.LifecycleAndReproduction,
                OrganismJourneyEventFamily.Birth,
                receipt.OffspringId,
                receipt.SpeciesId,
                receipt.TileId,
                receipt.OffspringPositionXQ,
                receipt.OffspringPositionYQ,
                receipt.ParentId,
                null,
                null,
                0,
                0,
                []));
        }

        foreach (var receipt in scratch.GetScavengeReceipts(tick))
        {
            var died = deathByOrganism.TryGetValue(receipt.OrganismId, out var death);
            var organism = died ? default : FindOrganism(receipt.OrganismId);
            pending.Add(new PendingJourneyEvent(
                TickPhase.ExternalResolution,
                OrganismJourneyEventFamily.Feeding,
                receipt.OrganismId,
                died ? death.SpeciesId : organism.SpeciesId,
                receipt.TileId,
                died ? death.PositionXQ : organism.PositionXQ,
                died ? death.PositionYQ : organism.PositionYQ,
                null,
                receipt.RemnantId,
                null,
                checked(receipt.GrantedReserveQ + receipt.GrantedStructureQ),
                0,
                []));
        }

        foreach (var transaction in transactions.Where(value =>
                     value.Cause is LedgerCause.ExternalEnergyCapture or
                         LedgerCause.MicronutrientUptake))
        {
            var died = deathByOrganism.TryGetValue(transaction.ActorId, out var death);
            var organism = died ? default : FindOrganism(transaction.ActorId);
            var speciesId = died ? death.SpeciesId : organism.SpeciesId;
            var positionX = died ? death.PositionXQ : organism.PositionXQ;
            var positionY = died ? death.PositionYQ : organism.PositionYQ;
            var resourceId = transaction.MatterEntries
                .Where(entry => entry.Account.OwnerKind == MatterAccountOwnerKind.Tile &&
                    entry.DeltaQ < 0)
                .Select(entry => (ResourceId?)entry.Account.ResourceId)
                .FirstOrDefault();
            var amount = transaction.MatterEntries
                .Where(entry => entry.Account.OwnerKind == MatterAccountOwnerKind.Organism &&
                    entry.DeltaQ > 0)
                .Sum(entry => entry.DeltaQ);
            pending.Add(new PendingJourneyEvent(
                transaction.Key.Phase,
                OrganismJourneyEventFamily.ResourceAbsorption,
                transaction.ActorId,
                speciesId,
                transaction.TileId,
                positionX,
                positionY,
                null,
                null,
                resourceId,
                amount,
                transaction.ReactionId.Value,
                []));
        }

        foreach (var relocation in changes.MergedChanges.StoreChanges
                     .SelectMany(store => store.Relocations))
        {
            var died = deathByOrganism.TryGetValue(relocation.OrganismId, out var death);
            var organism = died ? default : FindOrganism(relocation.OrganismId);
            pending.Add(new PendingJourneyEvent(
                TickPhase.Movement,
                OrganismJourneyEventFamily.Migration,
                relocation.OrganismId,
                died ? death.SpeciesId : organism.SpeciesId,
                relocation.DestinationTileId,
                died ? death.PositionXQ : organism.PositionXQ,
                died ? death.PositionYQ : organism.PositionYQ,
                null,
                null,
                null,
                0,
                relocation.SourceTileId.Value,
                []));
        }

        foreach (var stress in scratch.GetStressReceipts(tick))
        {
            pending.Add(new PendingJourneyEvent(
                TickPhase.IntrinsicDeath,
                OrganismJourneyEventFamily.Stress,
                stress.OrganismId,
                stress.SpeciesId,
                stress.TileId,
                stress.PositionXQ,
                stress.PositionYQ,
                null,
                null,
                null,
                stress.EnvironmentalFactorQ,
                checked((uint)(stress.PriorBand << 8 | stress.CurrentBand)),
                []));
        }

        foreach (var transition in scratch.GetBehaviorTransitionReceipts(tick))
        {
            pending.Add(new PendingJourneyEvent(
                TickPhase.BehaviorUpdate,
                OrganismJourneyEventFamily.BehaviorTransition,
                transition.OrganismId,
                transition.SpeciesId,
                transition.TileId,
                transition.PositionXQ,
                transition.PositionYQ,
                null,
                null,
                null,
                0,
                checked((uint)((byte)transition.PriorBehavior << 8 |
                    (byte)transition.CurrentBehavior)),
                []));
        }

        foreach (var death in deaths)
        {
            pending.Add(new PendingJourneyEvent(
                death.TerminalPhase,
                OrganismJourneyEventFamily.Death,
                death.OrganismId,
                death.SpeciesId,
                death.TileId,
                death.PositionXQ,
                death.PositionYQ,
                null,
                death.RemnantId,
                null,
                0,
                (uint)death.ActualCause,
                death.NonzeroCauseProbabilities.Select(value =>
                    new JourneyDeathCauseProbability(
                        value.Cause,
                        value.ProbabilityQ,
                        value.Triggered)).ToImmutableArray()));
        }

        return pending
            .OrderBy(value => value.Phase)
            .ThenBy(value => value.SubjectOrganismId.Value)
            .ThenBy(value => value.Family)
            .ThenBy(value => value.RelatedOrganismId?.Value ?? 0)
            .ThenBy(value => value.RelatedRemnantId?.Value ?? 0)
            .ThenBy(value => value.ResourceId?.Value ?? 0)
            .Select(value => new OrganismJourneyEvent(
                nextJourneyEventId++,
                tick,
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
                value.DeathCauseProbabilities))
            .ToImmutableArray();
    }

    internal static ImmutableArray<OrganismRoutineActivitySummary>
        UpdateRoutineActivitySummaries(
            ImmutableArray<OrganismRoutineActivitySummary> prior,
            ImmutableArray<OrganismJourneyEvent> completedEvents,
            ulong bucketStartHour,
            ulong completedSimulatedHours,
            uint periodHours)
    {
        const ulong recentHours = 168;
        var additions = completedEvents
            .Where(value =>
                value.Family == OrganismJourneyEventFamily.ResourceAbsorption &&
                value.ResourceId.HasValue &&
                value.AmountQ > 0)
            .GroupBy(value => new
            {
                value.SubjectOrganismId,
                value.SubjectSpeciesId,
                value.TileId,
            })
            .Select(group => new OrganismRoutineActivitySummary(
                bucketStartHour,
                periodHours,
                group.Key.SubjectOrganismId,
                group.Key.SubjectSpeciesId,
                group.Key.TileId,
                group.GroupBy(value => value.ResourceId!.Value)
                    .OrderBy(resources => resources.Key.Value)
                    .Select(resources => new RoutineResourceAcquisition(
                        resources.Key,
                        resources.Sum(value => value.AmountQ)))
                    .ToImmutableArray()))
            .ToImmutableArray();
        var all = prior.AddRange(additions);
        var cutoffHour = completedSimulatedHours > recentHours
            ? completedSimulatedHours - recentHours
            : 0;
        var expired = all.Where(value =>
            value.PeriodHours != 24 &&
            checked(value.BucketStartHour + value.PeriodHours) <= cutoffHour);
        var recent = all.Where(value =>
            value.PeriodHours != 24 &&
            checked(value.BucketStartHour + value.PeriodHours) > cutoffHour);
        var dailyInputs = all.Where(value => value.PeriodHours == 24)
            .Concat(expired.Select(value => value with
            {
                BucketStartHour = value.BucketStartHour / 24 * 24,
                PeriodHours = 24,
            }));
        var daily = dailyInputs
            .GroupBy(value => new
            {
                value.BucketStartHour,
                value.SubjectOrganismId,
                value.SubjectSpeciesId,
                value.TileId,
            })
            .Select(group => new OrganismRoutineActivitySummary(
                group.Key.BucketStartHour,
                24,
                group.Key.SubjectOrganismId,
                group.Key.SubjectSpeciesId,
                group.Key.TileId,
                group.SelectMany(value => value.ResourceAcquisitions)
                    .GroupBy(value => value.ResourceId)
                    .OrderBy(resources => resources.Key.Value)
                    .Select(resources => new RoutineResourceAcquisition(
                        resources.Key,
                        resources.Sum(value => value.AmountQ)))
                    .ToImmutableArray()));
        return daily.Concat(recent)
            .OrderBy(value => value.SubjectOrganismId.Value)
            .ThenBy(value => value.BucketStartHour)
            .ThenBy(value => value.TileId.Value)
            .ThenBy(value => value.PeriodHours)
            .ToImmutableArray();
    }

    private OrganismJourneyEvent CreateJourneyEvent(
        ulong tick,
        TickPhase phase,
        OrganismJourneyEventFamily family,
        OrganismSnapshot organism,
        OrganismId? relatedOrganismId,
        RemnantId? relatedRemnantId,
        ResourceId? resourceId,
        long amountQ,
        uint detailId,
        ImmutableArray<JourneyDeathCauseProbability> deathCauses) =>
        new(
            nextJourneyEventId++,
            tick,
            phase,
            family,
            organism.Id,
            organism.SpeciesId,
            organism.TileId,
            organism.PositionXQ,
            organism.PositionYQ,
            relatedOrganismId,
            relatedRemnantId,
            resourceId,
            amountQ,
            detailId,
            deathCauses);

    private readonly record struct PendingJourneyEvent(
        TickPhase Phase,
        OrganismJourneyEventFamily Family,
        OrganismId SubjectOrganismId,
        SpeciesId SubjectSpeciesId,
        TileId TileId,
        uint PositionXQ,
        uint PositionYQ,
        OrganismId? RelatedOrganismId,
        RemnantId? RelatedRemnantId,
        ResourceId? ResourceId,
        long AmountQ,
        uint DetailId,
        ImmutableArray<JourneyDeathCauseProbability> DeathCauseProbabilities);

    public SpeciationPreview PreviewSpeciation(SpeciationCommand command)
    {
        lock (ownerGate)
        {
            if (Status != WorldRunnerStatus.PausedReady)
            {
                throw new InvalidOperationException("A faulted world cannot preview speciation.");
            }
            if (world.GetGameplayState().RunStatus != GameRunStatus.Active)
            {
                throw new InvalidOperationException("An ended game cannot preview speciation.");
            }
            return SpeciationEngine.Preview(world, command, publishedSnapshot.CompletedTick);
        }
    }

    public SpeciationResult ApplySpeciation(SpeciationCommand command)
    {
        lock (ownerGate)
        {
            if (Status != WorldRunnerStatus.PausedReady)
            {
                throw new InvalidOperationException("A faulted world cannot apply speciation.");
            }
            var game = world.GetGameplayState();
            if (game.RunStatus != GameRunStatus.Active)
            {
                throw new InvalidOperationException("An ended game cannot apply speciation.");
            }
            var preview = SpeciationEngine.Preview(world, command, publishedSnapshot.CompletedTick);
            if (!preview.Accepted)
            {
                return new SpeciationResult(
                    preview,
                    0,
                    default,
                    default,
                    publishedSnapshot.WorldRevision,
                    publishedSnapshot.StateHash);
            }

            var changes = world.BeginChanges();
            try
            {
                var followPlayerControl = command.ActorKind == SpeciationActorKind.Player &&
                    (game.Mode == GameMode.Survival || command.FollowDescendantIfPermitted);
                var applied = SpeciationEngine.Apply(
                    world,
                    command,
                    publishedSnapshot.CompletedTick,
                    randomOracle,
                    followPlayerControl,
                    changes);
                if (followPlayerControl)
                {
                    world.ReplaceGameplayState(game with
                    {
                        ControlledSpeciesId = applied.DescendantId,
                        GameplayRevision = checked(game.GameplayRevision + 1),
                    }, changes);
                }
                if (command.ActorKind == SpeciationActorKind.Player)
                {
                    lineageReviewSchedules = lineageReviewSchedules.Add(
                        CreateLineageReviewSchedule(
                            command,
                            applied.EventId,
                            applied.DescendantId,
                            followPlayerControl));
                }
                AppendNotableEvent(new PendingNotableEvent(
                    NotableEventFamily.Speciation,
                    NotableEventSignificance.Strategic,
                    applied.DescendantId,
                    command.AncestorSpeciesId,
                    null,
                    null,
                    applied.EventId,
                    applied.Preview.FounderCounts.Aggregate(
                        0UL,
                        (sum, value) => checked(sum + value.Count)),
                    $"speciation:{applied.EventId}"),
                    publishedSnapshot.CompletedTick,
                    publishedSnapshot.SimulatedHours);
                foreach (var founder in applied.Preview.FounderCounts
                    .OrderBy(value => value.TileId.Value))
                {
                    AppendNotableEvent(new PendingNotableEvent(
                        NotableEventFamily.FirstTileOccupation,
                        NotableEventSignificance.Strategic,
                        applied.DescendantId,
                        null,
                        founder.TileId,
                        null,
                        applied.EventId,
                        founder.Count,
                        $"first-occupation:species:{applied.DescendantId.Value}:tile:{founder.TileId.Value}"),
                        publishedSnapshot.CompletedTick,
                        publishedSnapshot.SimulatedHours);
                }
                var descendantPopulation = world.GetSpecies(applied.DescendantId).Population;
                var aboveDangerThreshold = descendantPopulation >
                    PopulationAttentionRules.DangerThreshold;
                populationAttentionStates = populationAttentionStates.Add(
                    new PopulationAttentionState(
                        applied.DescendantId,
                        aboveDangerThreshold,
                        aboveDangerThreshold,
                        0));
                attentionWindowStates = attentionWindowStates.Add(new AttentionWindowState(
                    applied.DescendantId,
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
                    [new PopulationAttentionSample(
                        publishedSnapshot.SimulatedHours,
                        descendantPopulation)]));
                AppendAttentionAlerts(
                    publishedSnapshot.CompletedTick,
                    publishedSnapshot.SimulatedHours);
                world.ValidateInvariants();
                world.ValidateGameplayOutcomeInvariants();
                world.SealChanges(changes);
                var revision = checked(publishedSnapshot.WorldRevision + 1);
                publishedSnapshot = CaptureWorkingSnapshot(
                    publishedSnapshot.CompletedTick,
                    revision,
                    publishedSnapshot.SimulatedHours,
                    lastCompletedTransactions);
                return new SpeciationResult(
                    applied.Preview,
                    applied.EventId,
                    applied.DescendantId,
                    applied.GenomeId,
                    revision,
                    publishedSnapshot.StateHash);
            }
            catch
            {
                world.MarkFaulted(changes);
                Status = WorldRunnerStatus.Faulted;
                throw;
            }
        }
    }

    public GameplayCommandResult TransferSandboxControl(
        SpeciesId targetSpeciesId,
        ulong expectedGameplayRevision)
    {
        lock (ownerGate)
        {
            var game = world.GetGameplayState();
            var failure = ValidateGameplayCommand(game, expectedGameplayRevision, targetSpeciesId);
            if (failure == GameplayCommandFailure.None && game.Mode != GameMode.FreeSandbox)
            {
                failure = GameplayCommandFailure.WrongMode;
            }
            if (failure == GameplayCommandFailure.None && targetSpeciesId == game.ControlledSpeciesId)
            {
                failure = GameplayCommandFailure.AlreadyControlled;
            }
            if (failure != GameplayCommandFailure.None)
            {
                return RejectGameplayCommand(failure, game);
            }

            var changes = world.BeginChanges();
            try
            {
                var locked = game.MutationLockedSpeciesIds.ToHashSet();
                SetSpeciesAuthorityForGameplay(
                    game.ControlledSpeciesId,
                    locked.Contains(game.ControlledSpeciesId)
                        ? EvolutionAuthorityKind.Locked
                        : EvolutionAuthorityKind.Autonomous,
                    changes);
                SetSpeciesAuthorityForGameplay(
                    targetSpeciesId,
                    locked.Contains(targetSpeciesId)
                        ? EvolutionAuthorityKind.Locked
                        : EvolutionAuthorityKind.Controlled,
                    changes);
                var next = game with
                {
                    ControlledSpeciesId = targetSpeciesId,
                    GameplayRevision = checked(game.GameplayRevision + 1),
                };
                world.ReplaceGameplayState(next, changes);
                return CommitGameplayCommand(next, changes);
            }
            catch
            {
                world.MarkFaulted(changes);
                Status = WorldRunnerStatus.Faulted;
                throw;
            }
        }
    }

    public GameplayCommandResult SetSandboxMutationLock(
        SpeciesId speciesId,
        bool locked,
        ulong expectedGameplayRevision)
    {
        lock (ownerGate)
        {
            var game = world.GetGameplayState();
            var failure = ValidateGameplayCommand(game, expectedGameplayRevision, speciesId);
            if (failure == GameplayCommandFailure.None && game.Mode != GameMode.FreeSandbox)
            {
                failure = GameplayCommandFailure.WrongMode;
            }
            var locks = game.MutationLockedSpeciesIds.ToHashSet();
            if (failure == GameplayCommandFailure.None && locks.Contains(speciesId) == locked)
            {
                failure = GameplayCommandFailure.LockAlreadyMatches;
            }
            if (failure != GameplayCommandFailure.None)
            {
                return RejectGameplayCommand(failure, game);
            }

            var changes = world.BeginChanges();
            try
            {
                if (locked)
                {
                    locks.Add(speciesId);
                }
                else
                {
                    locks.Remove(speciesId);
                }
                SetSpeciesAuthorityForGameplay(
                    speciesId,
                    locked
                        ? EvolutionAuthorityKind.Locked
                        : speciesId == game.ControlledSpeciesId
                            ? EvolutionAuthorityKind.Controlled
                            : EvolutionAuthorityKind.Autonomous,
                    changes);
                var next = game with
                {
                    GameplayRevision = checked(game.GameplayRevision + 1),
                    MutationLockedSpeciesIds = locks.OrderBy(id => id.Value).ToImmutableArray(),
                };
                world.ReplaceGameplayState(next, changes);
                return CommitGameplayCommand(next, changes);
            }
            catch
            {
                world.MarkFaulted(changes);
                Status = WorldRunnerStatus.Faulted;
                throw;
            }
        }
    }

    private GameplayCommandFailure ValidateGameplayCommand(
        GameStateSnapshot game,
        ulong expectedGameplayRevision,
        SpeciesId speciesId)
    {
        if (Status != WorldRunnerStatus.PausedReady || game.RunStatus != GameRunStatus.Active)
        {
            return GameplayCommandFailure.RunEnded;
        }
        if (game.GameplayRevision != expectedGameplayRevision)
        {
            return GameplayCommandFailure.StaleGameplayRevision;
        }
        if (!world.GetSpeciesIdsInCanonicalOrder().Contains(speciesId))
        {
            return GameplayCommandFailure.SpeciesNotFound;
        }
        return world.GetSpecies(speciesId).Population == 0
            ? GameplayCommandFailure.SpeciesExtinct
            : GameplayCommandFailure.None;
    }

    private void SetSpeciesAuthorityForGameplay(
        SpeciesId speciesId,
        EvolutionAuthorityKind authority,
        PhaseChangeBuilder changes)
    {
        var species = world.GetSpecies(speciesId);
        if (species.Evolution.Authority == authority)
        {
            return;
        }
        world.SetSpeciesSpeciationState(speciesId, species.Evolution with
        {
            Authority = authority,
            EvolutionRevision = checked(species.Evolution.EvolutionRevision + 1),
        }, changes);
    }

    private GameplayCommandResult CommitGameplayCommand(
        GameStateSnapshot game,
        PhaseChangeBuilder changes)
    {
        world.ValidateInvariants();
        world.ValidateGameplayOutcomeInvariants();
        world.SealChanges(changes);
        var revision = checked(publishedSnapshot.WorldRevision + 1);
        publishedSnapshot = CaptureWorkingSnapshot(
            publishedSnapshot.CompletedTick,
            revision,
            publishedSnapshot.SimulatedHours,
            lastCompletedTransactions);
        return new GameplayCommandResult(
            true,
            GameplayCommandFailure.None,
            revision,
            game.GameplayRevision,
            publishedSnapshot.StateHash);
    }

    private GameplayCommandResult RejectGameplayCommand(
        GameplayCommandFailure failure,
        GameStateSnapshot game) =>
        new(
            false,
            failure,
            publishedSnapshot.WorldRevision,
            game.GameplayRevision,
            publishedSnapshot.StateHash);

    internal void SetSpeciesEvolutionForTesting(
        SpeciesId speciesId,
        long mutationBalanceQ,
        EvolutionAuthorityKind authority)
    {
        lock (ownerGate)
        {
            var current = world.GetSpecies(speciesId);
            var changes = world.BeginChanges();
            world.SetSpeciesSpeciationState(
                speciesId,
                current.Evolution with
                {
                    Authority = authority,
                    MutationBalanceQ = mutationBalanceQ,
                    EvolutionRevision = checked(current.Evolution.EvolutionRevision + 1),
                },
                changes);
            world.SealChanges(changes);
            publishedSnapshot = CaptureWorkingSnapshot(
                publishedSnapshot.CompletedTick,
                checked(publishedSnapshot.WorldRevision + 1),
                publishedSnapshot.SimulatedHours,
                lastCompletedTransactions);
        }
    }

    private void InitializeGamePopulation(RootRandomSeed rootSeed, GameSetupCommand setup)
    {
        var oracle = new SemanticRandomOracle(rootSeed);
        var changes = world.BeginChanges();
        var scenario = Rules.RulePack.Scenarios[Rules.Scenario.DenseSlot];
        var playerAllocation = setup.PlayerFounderAllocationId ??
            Rules.RulePack.FounderAllocations.Single(value => value.IsBaseline).Id;
        var competitorAllocation = setup.CompetitorFounderAllocationId ??
            scenario.DefaultCompetitorFounderAllocation.Id;
        var genomes = new Dictionary<(FounderGenomeId Founder, FounderAllocationId Allocation), GenomeId>();
        GenomeId ResolveGenome(FounderGenomeId founder, FounderAllocationId allocation)
        {
            if (!genomes.TryGetValue((founder, allocation), out var genomeId))
            {
                genomeId = world.CreateGenome(founder, allocation, changes);
                genomes.Add((founder, allocation), genomeId);
            }
            return genomeId;
        }

        var playerSpeciesId = world.CreateRootSpecies(
            ResolveGenome(setup.PlayerFounderGenomeId, playerAllocation),
            EvolutionAuthorityKind.Controlled,
            changes);
        CreateFounderPopulation(
            playerSpeciesId,
            setup.PlayerStartingTileId,
            setup.FounderPopulation,
            setup.FounderInitialization,
            oracle,
            changes);
        var roots = ImmutableArray.CreateBuilder<AbiogenesisRoot>(
            setup.Mode == GameMode.FreeSandbox ? 1 : 2);
        roots.Add(new AbiogenesisRoot(
            playerSpeciesId,
            setup.PlayerFounderGenomeId,
            playerAllocation,
            setup.PlayerStartingTileId,
            setup.FounderPopulation,
            true));

        if (setup.Mode == GameMode.Survival)
        {
            var competitorFounder = setup.CompetitorFounderGenomeId!.Value;
            var competitorTile = setup.CompetitorStartingTileId!.Value;
            var competitorSpeciesId = world.CreateRootSpecies(
                ResolveGenome(competitorFounder, competitorAllocation),
                EvolutionAuthorityKind.Autonomous,
                changes);
            CreateFounderPopulation(
                competitorSpeciesId,
                competitorTile,
                setup.FounderPopulation,
                setup.FounderInitialization,
                oracle,
                changes);
            roots.Add(new AbiogenesisRoot(
                competitorSpeciesId,
                competitorFounder,
                competitorAllocation,
                competitorTile,
                setup.FounderPopulation,
                false));
        }

        world.InitializeGameplay(new GameStateSnapshot(
            setup.Mode,
            GameRunStatus.Active,
            GameLossReason.None,
            playerSpeciesId,
            1,
            null,
            roots.MoveToImmutable(),
            []), changes);
        world.SealChanges(changes);
    }

    private void CreateFounderPopulation(
        SpeciesId speciesId,
        TileId tileId,
        uint founderCount,
        FounderInitializationMode initializationMode,
        SemanticRandomOracle oracle,
        PhaseChangeBuilder changes)
    {
        var phenotype = world.GetCompiledPhenotype(speciesId);
        var initialization = Rules.RulePack.Scenarios[Rules.Scenario.DenseSlot]
            .FounderInitialization;
        var committedMicronutrients = MicronutrientInventory.Compile(
            phenotype.Physiology.CommittedMicronutrientQuotas);
        foreach (var quota in phenotype.Physiology.CommittedMicronutrientQuotas)
        {
            var required = checked(quota.Quantity * founderCount);
            if (world.GetTileResource(tileId, quota.Resource) < required)
            {
                throw new InvalidOperationException(
                    $"Starting tile {tileId} cannot supply the founder micronutrient quota.");
            }
            world.ApplyTileResourceDelta(tileId, quota.Resource, -required, changes);
        }

        for (var ordinal = 0U; ordinal < founderCount; ordinal++)
        {
            var organismId = world.CreateOrganism(
                new OrganismInitialState(
                    speciesId,
                    tileId,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    LifecyclePhase.Mature,
                    0,
                    0,
                    0,
                    0,
                    FoundationStructuralMatterQ,
                    FoundationChargedReserveQ,
                    OrganismBehaviorState.Initial(0),
                    committedMicronutrients,
                    MicronutrientInventory.Empty),
                changes);
            var biologicalAgeHours = FounderBiologicalAge(
                organismId,
                speciesId,
                tileId,
                initialization,
                initializationMode,
                oracle);
            if (biologicalAgeHours > 0)
            {
                world.SetOrganismBiologicalAge(
                    organismId,
                    0,
                    biologicalAgeHours,
                    changes);
                world.MaterializeOrganismCondition(
                    organismId,
                    0,
                    ConditionSnapshotKind.End,
                    world.GetOrganismEnvironment(tileId),
                    changes);
            }
            var positionX = checked((uint)oracle.UniformInclusive(
                RandomAddress.Create(
                    RandomDomains.FounderPositionX,
                    ordinal,
                    organismId.Value,
                    tileId.Value),
                uint.MaxValue));
            var positionY = checked((uint)oracle.UniformInclusive(
                RandomAddress.Create(
                    RandomDomains.FounderPositionY,
                    ordinal,
                    organismId.Value,
                    tileId.Value),
                uint.MaxValue));
            world.SetOrganismPosition(organismId, positionX, positionY, 0, 0, changes);
            var reproduction = world.GetCompiledPhenotype(speciesId).Physiology.Reproduction;
            var readinessHours = FounderReproductionReadiness(
                organismId,
                speciesId,
                tileId,
                initialization,
                reproduction,
                initializationMode,
                oracle);
            world.SetOrganismLifecycleSchedule(
                organismId,
                checked((readinessHours +
                    Rules.TickDurationHours - 1) / Rules.TickDurationHours),
                0,
                0,
                changes);
        }
    }

    private static ulong FounderBiologicalAge(
        OrganismId organismId,
        SpeciesId speciesId,
        TileId tileId,
        CompiledFounderInitializationProfile profile,
        FounderInitializationMode mode,
        SemanticRandomOracle random) => mode == FounderInitializationMode.ScenarioDefault
            ? UniformFounderRange(
                profile.BiologicalAgeMinimumHours,
                profile.BiologicalAgeMaximumHours,
                RandomDomains.FounderBiologicalAge,
                organismId,
                speciesId,
                tileId,
                random)
            : 0;

    private static ulong FounderReproductionReadiness(
        OrganismId organismId,
        SpeciesId speciesId,
        TileId tileId,
        CompiledFounderInitializationProfile initialization,
        CompiledReproductionProfile reproduction,
        FounderInitializationMode mode,
        SemanticRandomOracle random)
    {
        if (mode == FounderInitializationMode.ScenarioDefault)
        {
            return UniformFounderRange(
                initialization.ReproductionReadinessMinimumHours,
                initialization.ReproductionReadinessMaximumHours,
                RandomDomains.FounderReproductionReadiness,
                organismId,
                speciesId,
                tileId,
                random);
        }
        if (mode == FounderInitializationMode.ZeroSpreadFixture)
        {
            return reproduction.BaseCooldownHours;
        }
        var legacyJitter = random.UniformInclusive(
            RandomAddress.Create(
                RandomDomains.ReproductionCooldownJitter,
                organismId.Value,
                0,
                0),
            reproduction.CooldownJitterMaximumHours);
        return checked(reproduction.BaseCooldownHours + legacyJitter);
    }

    private static ulong UniformFounderRange(
        ulong minimum,
        ulong maximum,
        RandomDomainId domain,
        OrganismId organismId,
        SpeciesId speciesId,
        TileId tileId,
        SemanticRandomOracle random)
    {
        if (minimum == maximum)
        {
            return minimum;
        }
        var offset = random.UniformInclusive(
            RandomAddress.Create(domain, organismId.Value, speciesId.Value, tileId.Value),
            maximum - minimum);
        return checked(minimum + offset);
    }

    private static GameSetupCommand DefaultSandboxSetup(
        CompiledWorldRules rules,
        RootRandomSeed rootSeed,
        uint founderCount,
        FounderInitializationMode founderInitialization)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (founderCount is 0 or > MaximumFoundationPopulation)
        {
            throw new ArgumentOutOfRangeException(
                nameof(founderCount),
                founderCount,
                $"The foundation population must be between 1 and {MaximumFoundationPopulation}.");
        }
        var scenario = rules.RulePack.Scenarios[rules.Scenario.DenseSlot];
        if (scenario.PermittedFounders.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException(
                "The selected scenario must permit at least one founder genome.");
        }
        var tileId = rules.WorldProfile.Generator is null
            ? TileId.FromRowMajorIndex(0)
            : TileId.FromRowMajorIndex(
                DeterministicWorldGenerator.Generate(rules.WorldProfile, rootSeed)
                    .StartingPairs[0].HydrogenTileIndex);
        return new GameSetupCommand(
            GameMode.FreeSandbox,
            scenario.PermittedFounders[0].Id,
            tileId,
            founderCount,
            PlayerFounderAllocationId: rules.RulePack.FounderAllocations
                .Single(value => value.IsBaseline).Id,
            FounderInitialization: founderInitialization);
    }

    private static void ValidateSetup(CompiledWorldRules rules, GameSetupCommand setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (!Enum.IsDefined(setup.Mode) ||
            !Enum.IsDefined(setup.FounderInitialization) ||
            setup.FounderPopulation is 0 or > MaximumFoundationPopulation)
        {
            throw new ArgumentException("The gameplay mode or founder population is invalid.", nameof(setup));
        }

        var scenario = rules.RulePack.Scenarios[rules.Scenario.DenseSlot];
        var permitted = scenario.PermittedFounders.Select(founder => founder.Id).ToHashSet();
        var permittedAllocations = scenario.PermittedFounderAllocations
            .Select(allocation => allocation.Id)
            .ToHashSet();
        var playerAllocation = setup.PlayerFounderAllocationId ??
            rules.RulePack.FounderAllocations.Single(value => value.IsBaseline).Id;
        var tileCount = checked(rules.WorldProfile.Width * rules.WorldProfile.Height);
        if (!permitted.Contains(setup.PlayerFounderGenomeId) ||
            !permittedAllocations.Contains(playerAllocation) ||
            setup.PlayerStartingTileId.Value >= tileCount)
        {
            throw new ArgumentException("The player founder or starting tile is not permitted.", nameof(setup));
        }

        var playerMetabolism = rules.RulePack.FounderPhenotypes.Single(phenotype =>
            phenotype.FounderGenomeId == setup.PlayerFounderGenomeId)
            .Physiology.OpeningMetabolism.Kind;
        if (rules.GeneratedWorld is not null)
        {
            ValidateGeneratedStartingTile(
                rules.GeneratedWorld,
                setup.PlayerStartingTileId,
                playerMetabolism,
                nameof(setup));
        }

        if (setup.Mode == GameMode.FreeSandbox)
        {
            if (setup.CompetitorFounderGenomeId.HasValue ||
                setup.CompetitorStartingTileId.HasValue ||
                setup.CompetitorFounderAllocationId.HasValue)
            {
                throw new ArgumentException("Free sandbox setup cannot contain a competitor root.", nameof(setup));
            }
            return;
        }

        if (!setup.CompetitorFounderGenomeId.HasValue ||
            !setup.CompetitorStartingTileId.HasValue ||
            !permitted.Contains(setup.CompetitorFounderGenomeId.Value) ||
            setup.CompetitorStartingTileId.Value.Value >= tileCount ||
            setup.CompetitorStartingTileId.Value == setup.PlayerStartingTileId ||
            WorldGridTopology.CylindricalManhattanDistance(
                rules.WorldProfile.Width,
                rules.WorldProfile.Height,
                setup.PlayerStartingTileId.Value,
                setup.CompetitorStartingTileId.Value.Value) != 1)
        {
            throw new ArgumentException(
                "Survival setup requires one permitted competitor in a distinct neighboring tile.",
                nameof(setup));
        }

        var competitorAllocation = setup.CompetitorFounderAllocationId ??
            scenario.DefaultCompetitorFounderAllocation.Id;
        if (competitorAllocation != scenario.DefaultCompetitorFounderAllocation.Id)
        {
            throw new ArgumentException(
                "The v1 Survival competitor must use the scenario's balanced founder allocation.",
                nameof(setup));
        }

        var competitorMetabolism = rules.RulePack.FounderPhenotypes.Single(phenotype =>
            phenotype.FounderGenomeId == setup.CompetitorFounderGenomeId.Value)
            .Physiology.OpeningMetabolism.Kind;
        if (rules.GeneratedWorld is not null)
        {
            ValidateGeneratedStartingTile(
                rules.GeneratedWorld,
                setup.CompetitorStartingTileId!.Value,
                competitorMetabolism,
                nameof(setup));
        }
        if (playerMetabolism == competitorMetabolism)
        {
            throw new ArgumentException(
                "Survival setup requires the two abiogenesis roots to use different opening metabolisms.",
                nameof(setup));
        }
    }

    private static void ValidateGeneratedStartingTile(
        GeneratedWorldMap generated,
        TileId tileId,
        FoundingMetabolismKind metabolism,
        string parameterName)
    {
        var expected = metabolism == FoundingMetabolismKind.HydrogenAcetogenesis
            ? StartingTileRole.Hydrogen
            : StartingTileRole.Sulfur;
        if (generated.GetTile(tileId.Value).StartingRole != expected)
        {
            throw new ArgumentException(
                $"Generated-world {metabolism} founders require a selected {expected} starting tile.",
                parameterName);
        }
    }

    private WorldSnapshot CaptureWorkingSnapshot(
        ulong completedTick,
        ulong worldRevision,
        ulong simulatedHours,
        ImmutableArray<ResourceTransaction> completedTransactions = default)
    {
        var state = new WorldSnapshotContent(
            WorldId,
            completedTick,
            worldRevision,
            simulatedHours,
            Rules.TickDurationHours,
            Status,
            randomCompatibility);
        return new WorldSnapshot(
            state.WorldId,
            state.CompletedTick,
            state.WorldRevision,
            state.SimulatedHours,
            state.TickDurationHours,
            world.OrganismCount,
            WorldStateHasher.Hash(
                state,
                world,
                completedTransactions.IsDefault ? [] : completedTransactions));
    }

    private ImmutableArray<ResourceTransaction> GetLastCompletedTransactions() =>
        lastCompletedTransactions;

    private LineageReviewSchedule CreateLineageReviewSchedule(
        SpeciationCommand command,
        ulong speciationEventId,
        SpeciesId descendantSpeciesId,
        bool followPlayerControl)
    {
        var perspectiveSpeciesId = followPlayerControl
            ? descendantSpeciesId
            : command.AncestorSpeciesId;
        var comparisonSpeciesId = perspectiveSpeciesId == descendantSpeciesId
            ? command.AncestorSpeciesId
            : descendantSpeciesId;
        var followUpTraits = Rules.RulePack.Traits
            .Where(trait => command.NewTraitIds.Contains(trait.Id) &&
                trait.ConsequenceFollowUpHours.HasValue)
            .OrderByDescending(trait => trait.ConsequenceFollowUpHours)
            .ThenBy(trait => trait.Id.Value)
            .ToImmutableArray();
        var followUpHours = followUpTraits.IsEmpty
            ? 0
            : followUpTraits[0].ConsequenceFollowUpHours!.Value;
        var evidenceKinds = followUpTraits
            .Where(trait => trait.ConsequenceFollowUpHours == followUpHours)
            .Select(trait => trait.ConsequenceEvidenceKind)
            .Distinct()
            .ToImmutableArray();
        var followUpEvidenceKind = evidenceKinds.Length == 1
            ? ToLineageEvidenceKind(evidenceKinds[0]!.Value)
            : LineageReviewEvidenceKind.GeneralOutcomes;
        var cooldownTicks = checked((SpeciationRules.RefractoryHours +
            Rules.TickDurationHours - 1) / Rules.TickDurationHours);
        var followUpTicks = followUpHours == 0
            ? 0
            : checked((followUpHours + Rules.TickDurationHours - 1) /
                Rules.TickDurationHours);
        var liveTiles = GetOccupiedTiles(perspectiveSpeciesId);
        var perspectivePhenotype = world.GetCompiledPhenotype(perspectiveSpeciesId);
        var comparisonPhenotype = world.GetCompiledPhenotype(comparisonSpeciesId);
        var perspectiveTraits = world.GetGenome(
            world.GetSpecies(perspectiveSpeciesId).GenomeId).AcquiredTraits;
        var capabilityActivations = perspectivePhenotype.Physiology.Behavior.ResourceConservation
            ? ImmutableArray.Create(new LineageReviewCapabilityActivation(
                LineageReviewCapabilityKind.ResourceConservation,
                Rules.RulePack.Traits
                    .Where(trait => trait.EnablesResourceConservation &&
                        perspectiveTraits.Contains(trait.Id))
                    .OrderBy(trait => trait.Id.Value)
                    .Select(trait => trait.Id)
                    .First(),
                !comparisonPhenotype.Physiology.Behavior.ResourceConservation,
                true,
                0))
            : [];
        var comparisonReactionIds = comparisonPhenotype.Processes
            .Select(process => process.Reaction.Id)
            .ToHashSet();
        var reactionActivations = perspectivePhenotype.Processes
            .OrderBy(process => process.Reaction.Id.Value)
            .Select(process => new LineageReviewReactionActivation(
                process.Reaction.Id,
                !comparisonReactionIds.Contains(process.Reaction.Id),
                true,
                0))
            .ToImmutableArray();
        return new LineageReviewSchedule(
            speciationEventId,
            publishedSnapshot.CompletedTick,
            publishedSnapshot.SimulatedHours,
            command.AncestorSpeciesId,
            descendantSpeciesId,
            perspectiveSpeciesId,
            command.NewTraitIds.OrderBy(id => id.Value).ToImmutableArray(),
            checked(publishedSnapshot.CompletedTick + cooldownTicks),
            followUpTicks == 0 ? 0 : checked(publishedSnapshot.CompletedTick + followUpTicks),
            followUpHours,
            followUpEvidenceKind,
            BuildLineageObservation(
                perspectiveSpeciesId,
                LineageReviewObservationScope.WorldExact,
                null,
                publishedSnapshot.CompletedTick,
                false),
            BuildLineageObservation(
                comparisonSpeciesId,
                LineageReviewObservationScope.LiveTilesObserved,
                liveTiles,
                publishedSnapshot.CompletedTick,
                false),
            capabilityActivations,
            reactionActivations);
    }

    private void UpdateLineageReviewActivationEvidence(
        ImmutableArray<OrganismJourneyEvent> completedEvents,
        ImmutableArray<ResourceTransaction> completedTransactions,
        ulong completedTick)
    {
        var speciesByOrganism = world.GetOrganismIdsInCanonicalOrder()
            .Select(world.GetOrganism)
            .ToDictionary(organism => organism.Id, organism => organism.SpeciesId);
        foreach (var completedEvent in completedEvents)
        {
            speciesByOrganism.TryAdd(
                completedEvent.SubjectOrganismId,
                completedEvent.SubjectSpeciesId);
        }
        lineageReviewSchedules = lineageReviewSchedules.Select(schedule =>
        {
            var trackingThroughTick = schedule.FollowUpBoundaryTick == 0
                ? schedule.CooldownBoundaryTick
                : Math.Max(schedule.CooldownBoundaryTick, schedule.FollowUpBoundaryTick);
            if (completedTick <= schedule.AppliedTick || completedTick > trackingThroughTick)
            {
                return schedule;
            }

            var capabilityActivations = schedule.CapabilityActivations
                .Select(activation => activation with
                {
                    ActivationCount = checked(activation.ActivationCount +
                        (ulong)completedEvents.LongCount(value =>
                            value.SubjectSpeciesId == schedule.PerspectiveSpeciesId &&
                            value.Family == OrganismJourneyEventFamily.BehaviorTransition &&
                            activation.Kind == LineageReviewCapabilityKind.ResourceConservation &&
                            (OrganismBehaviorId)(value.DetailId & 0xff) ==
                                OrganismBehaviorId.Conserving)),
                })
                .ToImmutableArray();
            var reactionActivations = schedule.ReactionActivations
                .Select(activation => activation with
                {
                    ActivationCount = checked(activation.ActivationCount +
                        (ulong)completedTransactions.LongCount(transaction =>
                            transaction.ActorId != default &&
                            transaction.ReactionId == activation.ReactionId &&
                            speciesByOrganism.GetValueOrDefault(transaction.ActorId) ==
                                schedule.PerspectiveSpeciesId)),
                })
                .ToImmutableArray();
            return schedule with
            {
                CapabilityActivations = capabilityActivations,
                ReactionActivations = reactionActivations,
            };
        }).ToImmutableArray();
    }

    private void AppendDueLineageReviewLandmarks(ulong completedTick, ulong simulatedHours)
    {
        foreach (var schedule in lineageReviewSchedules.OrderBy(value => value.SpeciationEventId))
        {
            if (schedule.CooldownBoundaryTick == completedTick &&
                !HasLineageReviewLandmark(
                    schedule.SpeciationEventId,
                    LineageReviewLandmarkKind.CooldownBoundary))
            {
                AppendLineageReviewLandmark(
                    schedule,
                    LineageReviewLandmarkKind.CooldownBoundary,
                    LineageReviewEvidenceKind.GeneralOutcomes,
                    SpeciationRules.RefractoryHours,
                    completedTick,
                    simulatedHours);
            }
            if (schedule.FollowUpBoundaryTick == completedTick &&
                !HasLineageReviewLandmark(
                    schedule.SpeciationEventId,
                    LineageReviewLandmarkKind.ProposalFollowUp))
            {
                AppendLineageReviewLandmark(
                    schedule,
                    LineageReviewLandmarkKind.ProposalFollowUp,
                    schedule.FollowUpEvidenceKind,
                    schedule.FollowUpHours,
                    completedTick,
                    simulatedHours);
            }
        }
    }

    private void AppendNotableTickEvents(
        IReadOnlyDictionary<SpeciesId, ulong> priorPopulations,
        ImmutableArray<OrganismJourneyEvent> completedEvents,
        ImmutableArray<ResourceTransaction> completedTransactions,
        ulong completedTick,
        ulong simulatedHours)
    {
        var candidates = ImmutableArray.CreateBuilder<PendingNotableEvent>();
        foreach (var reproduction in completedEvents
            .Where(value => value.Family == OrganismJourneyEventFamily.Reproduction)
            .GroupBy(value => value.SubjectSpeciesId)
            .Select(group => group.OrderBy(value => value.EventId).First()))
        {
            candidates.Add(new PendingNotableEvent(
                NotableEventFamily.FirstReproduction,
                NotableEventSignificance.Informational,
                reproduction.SubjectSpeciesId,
                null,
                reproduction.TileId,
                null,
                reproduction.EventId,
                1,
                $"first-reproduction:species:{reproduction.SubjectSpeciesId.Value}"));
        }

        foreach (var migration in completedEvents
            .Where(value => value.Family == OrganismJourneyEventFamily.Migration)
            .GroupBy(value => (value.SubjectSpeciesId, value.TileId))
            .Select(group => group.OrderBy(value => value.EventId).First()))
        {
            candidates.Add(new PendingNotableEvent(
                NotableEventFamily.FirstTileOccupation,
                NotableEventSignificance.Strategic,
                migration.SubjectSpeciesId,
                null,
                migration.TileId,
                null,
                migration.EventId,
                1,
                $"first-occupation:species:{migration.SubjectSpeciesId.Value}:tile:{migration.TileId.Value}"));
        }

        var speciesByOrganism = world.GetOrganismIdsInCanonicalOrder()
            .Select(world.GetOrganism)
            .ToDictionary(organism => organism.Id, organism => organism.SpeciesId);
        foreach (var completedEvent in completedEvents)
        {
            speciesByOrganism.TryAdd(
                completedEvent.SubjectOrganismId,
                completedEvent.SubjectSpeciesId);
        }
        foreach (var reaction in completedTransactions
            .Where(value => IsCompiledReactionExecution(value.Cause) &&
                value.ActorId != default && value.ReactionId != default &&
                speciesByOrganism.ContainsKey(value.ActorId))
            .GroupBy(value => (SpeciesId: speciesByOrganism[value.ActorId], value.ReactionId))
            .Select(group => group.OrderBy(value => value.Key).First()))
        {
            candidates.Add(new PendingNotableEvent(
                NotableEventFamily.FirstReactionExecution,
                NotableEventSignificance.Strategic,
                speciesByOrganism[reaction.ActorId],
                null,
                reaction.TileId,
                reaction.ReactionId,
                0,
                1,
                $"first-reaction:species:{speciesByOrganism[reaction.ActorId].Value}:reaction:{reaction.ReactionId.Value}"));
        }

        foreach (var death in completedEvents
            .Where(value => value.Family == OrganismJourneyEventFamily.Death)
            .GroupBy(value => (value.SubjectSpeciesId, Cause: value.DetailId))
            .Select(group => group.OrderBy(value => value.EventId).First()))
        {
            candidates.Add(new PendingNotableEvent(
                NotableEventFamily.RealizedDeathMechanism,
                NotableEventSignificance.Critical,
                death.SubjectSpeciesId,
                null,
                death.TileId,
                null,
                death.EventId,
                death.DetailId,
                $"death-mechanism:species:{death.SubjectSpeciesId.Value}:cause:{death.DetailId}"));
        }

        foreach (var speciesId in world.GetSpeciesIdsInCanonicalOrder())
        {
            var currentPopulation = world.GetSpecies(speciesId).Population;
            var priorPopulation = priorPopulations.GetValueOrDefault(speciesId);
            foreach (var milestone in PopulationMilestones.Where(value =>
                priorPopulation < value && currentPopulation >= value))
            {
                candidates.Add(new PendingNotableEvent(
                    NotableEventFamily.PopulationMilestone,
                    NotableEventSignificance.Informational,
                    speciesId,
                    null,
                    null,
                    null,
                    0,
                    milestone,
                    $"population:species:{speciesId.Value}:threshold:{milestone}"));
            }
            if (priorPopulation > 0 && currentPopulation == 0)
            {
                candidates.Add(new PendingNotableEvent(
                    NotableEventFamily.SpeciesExtinction,
                    NotableEventSignificance.Critical,
                    speciesId,
                    null,
                    null,
                    null,
                    0,
                    priorPopulation,
                    $"extinction:species:{speciesId.Value}"));
            }
        }

        foreach (var candidate in candidates
            .OrderBy(value => value.Family)
            .ThenBy(value => value.SpeciesId.Value)
            .ThenBy(value => value.RelatedSpeciesId?.Value ?? 0)
            .ThenBy(value => value.TileId?.Value ?? 0)
            .ThenBy(value => value.ReactionId?.Value ?? 0)
            .ThenBy(value => value.MilestoneValue)
            .ThenBy(value => value.SourceEventId))
        {
            AppendNotableEvent(candidate, completedTick, simulatedHours);
        }
    }

    private static bool IsCompiledReactionExecution(LedgerCause cause) => cause is
        LedgerCause.ExternalEnergyCapture or
        LedgerCause.ParticulateDigestion or
        LedgerCause.MandatoryMaintenance or
        LedgerCause.BiomassAssembly;

    private void AppendPopulationDangerEvents(
        IReadOnlyDictionary<SpeciesId, ulong> priorPopulations,
        ulong completedTick,
        ulong simulatedHours)
    {
        var states = populationAttentionStates.ToDictionary(value => value.SpeciesId);
        foreach (var speciesId in world.GetSpeciesIdsInCanonicalOrder())
        {
            var priorPopulation = priorPopulations.GetValueOrDefault(speciesId);
            var currentPopulation = world.GetSpecies(speciesId).Population;
            if (!states.TryGetValue(speciesId, out var state))
            {
                var wasAbove = priorPopulation > PopulationAttentionRules.DangerThreshold;
                state = new PopulationAttentionState(speciesId, wasAbove, wasAbove, 0);
            }
            var evaluation = PopulationAttentionRules.Evaluate(
                state,
                priorPopulation,
                currentPopulation);
            if (evaluation.EnteredLowPopulationBand)
            {
                AppendNotableEvent(new PendingNotableEvent(
                    NotableEventFamily.PopulationDangerThreshold,
                    NotableEventSignificance.Critical,
                    speciesId,
                    null,
                    null,
                    null,
                    0,
                    currentPopulation,
                    $"population-danger:species:{speciesId.Value}:episode:{evaluation.State.LowPopulationEpisodeOrdinal}"),
                    completedTick,
                    simulatedHours);
            }
            states[speciesId] = evaluation.State;
        }
        populationAttentionStates = states.Values
            .OrderBy(value => value.SpeciesId.Value)
            .ToImmutableArray();
    }

    private void AppendAttentionAlerts(ulong completedTick, ulong simulatedHours)
    {
        var notableGroups = notableEvents
            .Where(value => value.CompletedTick == completedTick)
            .GroupBy(value => (value.Family, value.SpeciesId))
            .OrderBy(group => group.Key.Family)
            .ThenBy(group => group.Key.SpeciesId.Value);
        foreach (var group in notableGroups)
        {
            var key = $"notable:{(byte)group.Key.Family}:species:{group.Key.SpeciesId.Value}:tick:{completedTick}";
            if (attentionAlerts.Any(value => value.DeduplicationKey == key)) continue;
            var events = group.OrderBy(value => value.EventId).ToImmutableArray();
            attentionAlerts = attentionAlerts.Add(new AttentionAlert(
                checked(nextAttentionAlertId++),
                (AttentionAlertClass)events.Max(value => (byte)value.Significance),
                AttentionAlertKind.NotableEventGroup,
                group.Key.Family,
                completedTick,
                simulatedHours,
                group.Key.SpeciesId,
                events.Select(value => value.EventId).ToImmutableArray(),
                key));
        }
        foreach (var landmark in lineageReviewLandmarks
            .Where(value => value.CompletedTick == completedTick)
            .OrderBy(value => value.EventId))
        {
            var key = $"lineage-review:event:{landmark.EventId}";
            if (attentionAlerts.Any(value => value.DeduplicationKey == key)) continue;
            attentionAlerts = attentionAlerts.Add(new AttentionAlert(
                checked(nextAttentionAlertId++),
                AttentionAlertClass.Strategic,
                AttentionAlertKind.LineageReviewBoundary,
                null,
                completedTick,
                simulatedHours,
                landmark.PerspectiveSpeciesId,
                [landmark.EventId],
                key));
        }
    }

    private void AppendWindowAttentionEvents(ulong completedTick, ulong simulatedHours)
    {
        var states = attentionWindowStates.ToDictionary(value => value.SpeciesId);
        var resourcePressureBySpecies = ComputeAverageResourcePressureBySpecies();
        foreach (var speciesId in world.GetSpeciesIdsInCanonicalOrder())
        {
            var species = world.GetSpecies(speciesId);
            if (!states.TryGetValue(speciesId, out var state))
            {
                state = new AttentionWindowState(
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
                    []);
            }
            var averageResourcePressureQ =
                resourcePressureBySpecies.GetValueOrDefault(speciesId);
            var evaluation = AttentionWindowRules.Evaluate(
                state,
                simulatedHours,
                Rules.TickDurationHours,
                species.Population,
                species.Evolution.AverageHealthQ,
                averageResourcePressureQ);
            if (evaluation.EnteredPopulationDecline)
            {
                AppendNotableEvent(new PendingNotableEvent(
                    NotableEventFamily.PopulationDeclineThreshold,
                    NotableEventSignificance.Critical,
                    speciesId,
                    null,
                    null,
                    null,
                    0,
                    species.Population,
                    $"population-decline:species:{speciesId.Value}:episode:{evaluation.State.PopulationDeclineEpisodeOrdinal}",
                    evaluation.PopulationBaseline),
                    completedTick,
                    simulatedHours);
            }
            if (evaluation.EnteredSustainedLowHealth)
            {
                AppendNotableEvent(new PendingNotableEvent(
                    NotableEventFamily.SustainedLowHealth,
                    NotableEventSignificance.Critical,
                    speciesId,
                    null,
                    null,
                    null,
                    0,
                    species.Evolution.AverageHealthQ,
                    $"low-health:species:{speciesId.Value}:episode:{evaluation.State.LowHealthEpisodeOrdinal}",
                    evaluation.State.LowHealthConsecutiveHours),
                    completedTick,
                    simulatedHours);
            }
            if (evaluation.EnteredSustainedResourcePressure)
            {
                AppendNotableEvent(new PendingNotableEvent(
                    NotableEventFamily.SustainedResourcePressure,
                    NotableEventSignificance.Strategic,
                    speciesId,
                    null,
                    null,
                    null,
                    0,
                    averageResourcePressureQ,
                    $"resource-pressure:species:{speciesId.Value}:episode:{evaluation.State.ResourcePressureEpisodeOrdinal}",
                    evaluation.State.ResourcePressureConsecutiveHours),
                    completedTick,
                    simulatedHours);
            }
            states[speciesId] = evaluation.State;
        }
        attentionWindowStates = states.Values
            .OrderBy(value => value.SpeciesId.Value)
            .ToImmutableArray();
    }

    private Dictionary<SpeciesId, uint> ComputeAverageResourcePressureBySpecies() =>
        world.GetOrganismIdsInCanonicalOrder()
            .Select(world.GetOrganism)
            .GroupBy(value => value.SpeciesId)
            .ToDictionary(
                group => group.Key,
                group => checked((uint)(group.Aggregate(
                    0UL,
                    (sum, organism) => checked(sum + ComputeResourcePressureQ(organism))) /
                    (ulong)group.LongCount())));

    private void AppendNotableEvent(
        PendingNotableEvent candidate,
        ulong completedTick,
        ulong simulatedHours)
    {
        if (notableEvents.Any(value => value.DeduplicationKey == candidate.DeduplicationKey))
        {
            return;
        }
        notableEvents = notableEvents.Add(new NotableEvent(
            checked(nextChronicleEventId++),
            candidate.Family,
            candidate.Significance,
            1,
            completedTick,
            simulatedHours,
            candidate.SpeciesId,
            candidate.RelatedSpeciesId,
            candidate.TileId,
            candidate.ReactionId,
            candidate.SourceEventId,
            candidate.MilestoneValue,
            candidate.DeduplicationKey,
            candidate.BaselineValue));
    }

    private readonly record struct PendingNotableEvent(
        NotableEventFamily Family,
        NotableEventSignificance Significance,
        SpeciesId SpeciesId,
        SpeciesId? RelatedSpeciesId,
        TileId? TileId,
        ReactionId? ReactionId,
        ulong SourceEventId,
        ulong MilestoneValue,
        string DeduplicationKey,
        ulong BaselineValue = 0);

    private bool HasLineageReviewLandmark(
        ulong speciationEventId,
        LineageReviewLandmarkKind kind) => lineageReviewLandmarks.Any(value =>
            value.SpeciationEventId == speciationEventId && value.Kind == kind);

    private void AppendLineageReviewLandmark(
        LineageReviewSchedule schedule,
        LineageReviewLandmarkKind kind,
        LineageReviewEvidenceKind evidenceKind,
        ulong windowHours,
        ulong completedTick,
        ulong simulatedHours)
    {
        var comparisonSpeciesId = schedule.PerspectiveSpeciesId == schedule.DescendantSpeciesId
            ? schedule.AncestorSpeciesId
            : schedule.DescendantSpeciesId;
        var liveTiles = GetOccupiedTiles(schedule.PerspectiveSpeciesId);
        var phenotype = world.GetCompiledPhenotype(schedule.PerspectiveSpeciesId);
        var installedReactionIds = phenotype.Processes
            .Select(process => process.Reaction.Id)
            .ToHashSet();
        var evidenceReferences = ImmutableArray.Create(
                new LineageReviewEvidenceReference(
                    LineageReviewEvidenceReferenceKind.SpeciationDecision,
                    null,
                    null,
                    schedule.AppliedTick,
                    completedTick),
                new LineageReviewEvidenceReference(
                    LineageReviewEvidenceReferenceKind.ReviewedSpeciesSummary,
                    schedule.PerspectiveSpeciesId,
                    null,
                    schedule.AppliedTick,
                    completedTick),
                new LineageReviewEvidenceReference(
                    LineageReviewEvidenceReferenceKind.ComparisonSpeciesSummary,
                    comparisonSpeciesId,
                    null,
                    schedule.AppliedTick,
                    completedTick),
                new LineageReviewEvidenceReference(
                    LineageReviewEvidenceReferenceKind.ReviewedJourneyWindow,
                    schedule.PerspectiveSpeciesId,
                    null,
                    schedule.AppliedTick,
                    completedTick))
            .AddRange(liveTiles
                .OrderBy(tileId => tileId.Value)
                .Select(tileId => new LineageReviewEvidenceReference(
                    LineageReviewEvidenceReferenceKind.LiveTileResourceWindow,
                    schedule.PerspectiveSpeciesId,
                    tileId,
                    schedule.AppliedTick,
                    completedTick)));
        lineageReviewLandmarks = lineageReviewLandmarks.Add(new LineageReviewLandmark(
            checked(nextChronicleEventId++),
            kind,
            completedTick,
            simulatedHours,
            windowHours,
            schedule.SpeciationEventId,
            schedule.AncestorSpeciesId,
            schedule.DescendantSpeciesId,
            schedule.PerspectiveSpeciesId,
            schedule.TraitDelta,
            evidenceKind,
            schedule.PerspectiveBaseline,
            schedule.ComparisonBaseline,
            BuildLineageObservation(
                schedule.PerspectiveSpeciesId,
                LineageReviewObservationScope.WorldExact,
                null,
                schedule.AppliedTick,
                true),
            BuildLineageObservation(
                comparisonSpeciesId,
                LineageReviewObservationScope.LiveTilesObserved,
                liveTiles,
                schedule.AppliedTick,
                false),
            schedule.CapabilityActivations.Select(activation => activation with
            {
                Installed = activation.Kind switch
                {
                    LineageReviewCapabilityKind.ResourceConservation =>
                        phenotype.Physiology.Behavior.ResourceConservation,
                    _ => throw new ArgumentOutOfRangeException(nameof(activation)),
                },
            }).ToImmutableArray(),
            schedule.ReactionActivations.Select(activation => activation with
            {
                Installed = installedReactionIds.Contains(activation.ReactionId),
            }).ToImmutableArray(),
            evidenceReferences));
    }

    private HashSet<TileId> GetOccupiedTiles(SpeciesId speciesId) => world
        .GetOrganismIdsInCanonicalOrder()
        .Select(world.GetOrganism)
        .Where(organism => organism.SpeciesId == speciesId)
        .Select(organism => organism.TileId)
        .ToHashSet();

    private LineageReviewObservation BuildLineageObservation(
        SpeciesId speciesId,
        LineageReviewObservationScope scope,
        HashSet<TileId>? visibleTiles,
        ulong afterTick,
        bool includeActivityCounts)
    {
        var organisms = world.GetOrganismIdsInCanonicalOrder()
            .Select(world.GetOrganism)
            .Where(organism => organism.SpeciesId == speciesId &&
                (visibleTiles is null || visibleTiles.Contains(organism.TileId)))
            .ToImmutableArray();
        var population = checked((ulong)organisms.Length);
        var averageHealthQ = population == 0
            ? 0
            : checked((uint)(organisms.Aggregate(0UL, (sum, organism) =>
                checked(sum + organism.Condition.RelativeHealthQ)) / population));
        var averageReserveQ = population == 0
            ? 0
            : checked((uint)(organisms.Aggregate(0UL, (sum, organism) =>
                checked(sum + organism.Condition.ReserveFactorQ)) / population));
        var averageAcquisitionCoverageQ = population == 0
            ? 0
            : checked((uint)(organisms.Aggregate(0UL, (sum, organism) =>
                checked(sum + organism.Behavior.RecentAcquisitionCoverageQ)) / population));
        var averageResourcePressureQ = population == 0
            ? 0
            : checked((uint)(organisms.Aggregate(0UL, (sum, organism) =>
                checked(sum + ComputeResourcePressureQ(organism))) / population));
        var activity = includeActivityCounts
            ? journeyEvents.Where(value =>
                value.SubjectSpeciesId == speciesId && value.Tick > afterTick)
                .ToImmutableArray()
            : [];
        var behaviorCounts = organisms
            .GroupBy(organism => organism.Behavior.BehaviorId)
            .OrderBy(group => group.Key)
            .Select(group => new LineageReviewBehaviorCount(
                group.Key,
                checked((ulong)group.LongCount())))
            .ToImmutableArray();
        return new LineageReviewObservation(
            speciesId,
            scope,
            population,
            averageHealthQ,
            averageReserveQ,
            averageAcquisitionCoverageQ,
            averageResourcePressureQ,
            checked((uint)organisms.Select(organism => organism.TileId).Distinct().Count()),
            behaviorCounts,
            includeActivityCounts,
            checked((ulong)activity.LongCount(value =>
                value.Family == OrganismJourneyEventFamily.Birth)),
            checked((ulong)activity.LongCount(value =>
                value.Family == OrganismJourneyEventFamily.Death)),
            checked((ulong)activity.LongCount(value =>
                value.Family == OrganismJourneyEventFamily.Migration)));
    }

    private static LineageReviewEvidenceKind ToLineageEvidenceKind(
        EvolutionFollowUpEvidenceKind value) => value switch
        {
            EvolutionFollowUpEvidenceKind.CapabilityActivation =>
                LineageReviewEvidenceKind.CapabilityActivation,
            EvolutionFollowUpEvidenceKind.ConditionAndPressure =>
                LineageReviewEvidenceKind.ConditionAndPressure,
            EvolutionFollowUpEvidenceKind.GeographicSpread =>
                LineageReviewEvidenceKind.GeographicSpread,
            EvolutionFollowUpEvidenceKind.ReserveStorage =>
                LineageReviewEvidenceKind.ReserveStorage,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static ImmutableArray<ResourceTransaction> GetTransactions(TickChangeSet changes) =>
        changes.Phases
            .SelectMany(phase => phase.ResourceTransactions)
            .OrderBy(transaction => transaction.Key)
            .ToImmutableArray();

    private PublicationOrganism ToPublicationOrganism(
        OrganismSnapshot organism,
        ImmutableArray<ResourceAcquisitionSample> acquisitionEvidence,
        ImmutableArray<AcquisitionGateSample> acquisitionGateEvidence,
        ImmutableArray<OrganismActionGateSample> actionGateEvidence)
    {
        var physiology = world.GetCompiledPhenotype(organism.SpeciesId).Physiology;
        var capacity = physiology.ChargedReserveCapacityQ;
        return new(
            organism.Id,
            organism.SpeciesId,
            organism.TileId,
            organism.PositionXQ,
            organism.PositionYQ,
            SpatialMath.BodyRadiusQ(physiology.Spatial, organism.StructuralMatterQ),
            organism.VelocityXQPerHour,
            organism.VelocityYQPerHour,
            organism.BirthTick,
            organism.BiologicalAgeHours,
            organism.LifecyclePhase switch
            {
                LifecyclePhase.Mature => PublicationLifecyclePhase.Mature,
                _ => throw new InvalidOperationException(
                    $"Organism {organism.Id} has an unsupported lifecycle phase."),
            },
            organism.ReproductionNotBeforeTick,
            organism.SuccessfulReproductionCount,
            organism.ScavengeNotBeforeTick,
            organism.IngestedStructuralMatterQ,
            organism.StructuralMatterQ,
            organism.ChargedReserveQ,
            capacity,
            organism.Condition.RelativeHealthQ,
            organism.Condition.ReserveFactorQ,
            organism.Condition.StructureFactorQ,
            organism.Condition.AgeFactorQ,
            organism.Condition.EnvironmentalFactorQ,
            organism.Behavior.BehaviorId,
            organism.Behavior.TargetKind,
            organism.Behavior.TargetId,
            organism.Behavior.TargetPositionXQ,
            organism.Behavior.TargetPositionYQ,
            organism.Behavior.SelectedAtTick,
            organism.Behavior.MinimumDwellUntilTick,
            organism.Behavior.RecentEnergyCoverageQ,
            organism.Behavior.RecentAcquisitionCoverageQ,
            organism.Behavior.LimitingMaterialDeficitQ,
            ComputeResourcePressureQ(organism),
            ToPublicationMicronutrients(organism.CommittedMicronutrients),
            ToPublicationMicronutrients(organism.FreeMicronutrients),
            (acquisitionEvidence.IsDefault ? [] : acquisitionEvidence)
                .OrderBy(value => value.ResourceId.Value)
                .Select(value => new PublicationResourceAcquisitionEvidence(
                    value.ResourceId,
                    value.RequestedQ,
                    value.GrantedQ,
                    value.ConstraintFlags.HasFlag(AcquisitionConstraintFlags.TileSupply),
                    value.ConstraintFlags.HasFlag(AcquisitionConstraintFlags.ClaimContention)))
                .ToImmutableArray(),
            (acquisitionGateEvidence.IsDefault ? [] : acquisitionGateEvidence)
                .OrderBy(value => value.Process)
                .ThenBy(value => value.Reason)
                .Select(value => new PublicationAcquisitionGateEvidence(
                    value.Process switch
                    {
                        AcquisitionProcessKind.ExternalEnergyCapture =>
                            PublicationAcquisitionProcessKind.ExternalEnergyCapture,
                        AcquisitionProcessKind.Scavenging =>
                            PublicationAcquisitionProcessKind.Scavenging,
                        _ => throw new InvalidOperationException(
                            $"Unsupported acquisition process {value.Process}."),
                    },
                    value.Reason switch
                    {
                        AcquisitionGateReason.MissingCapability =>
                            PublicationAcquisitionGateReason.MissingCapability,
                        AcquisitionGateReason.InaccessibleLight =>
                            PublicationAcquisitionGateReason.InaccessibleLight,
                        AcquisitionGateReason.EnvironmentalOpportunity =>
                            PublicationAcquisitionGateReason.EnvironmentalOpportunity,
                        AcquisitionGateReason.InternalCapacity =>
                            PublicationAcquisitionGateReason.InternalCapacity,
                        AcquisitionGateReason.CooldownActive =>
                            PublicationAcquisitionGateReason.CooldownActive,
                        AcquisitionGateReason.InsufficientActionEnergy =>
                            PublicationAcquisitionGateReason.InsufficientActionEnergy,
                        _ => throw new InvalidOperationException(
                            $"Unsupported acquisition gate reason {value.Reason}."),
                    },
                    value.AvailableQ,
                    value.RequiredQ,
                    value.ClearsAtTick))
                .ToImmutableArray(),
            (actionGateEvidence.IsDefault ? [] : actionGateEvidence)
                .OrderBy(value => value.Process)
                .Select(value => new PublicationOrganismActionGateEvidence(
                    value.Process switch
                    {
                        OrganismActionProcessKind.BiomassGrowth =>
                            PublicationOrganismActionProcessKind.BiomassGrowth,
                        OrganismActionProcessKind.Reproduction =>
                            PublicationOrganismActionProcessKind.Reproduction,
                        _ => throw new InvalidOperationException(
                            $"Unsupported organism action process {value.Process}."),
                    },
                    value.Reason switch
                    {
                        OrganismActionGateReason.MissingCapability =>
                            PublicationOrganismActionGateReason.MissingCapability,
                        OrganismActionGateReason.BehaviorSuppressed =>
                            PublicationOrganismActionGateReason.BehaviorSuppressed,
                        OrganismActionGateReason.CooldownActive =>
                            PublicationOrganismActionGateReason.CooldownActive,
                        OrganismActionGateReason.HealthBelowMinimum =>
                            PublicationOrganismActionGateReason.HealthBelowMinimum,
                        OrganismActionGateReason.StructureBelowMinimum =>
                            PublicationOrganismActionGateReason.StructureBelowMinimum,
                        OrganismActionGateReason.ReserveBelowMinimum =>
                            PublicationOrganismActionGateReason.ReserveBelowMinimum,
                        OrganismActionGateReason.ConstitutiveMicronutrientQuotaMissing =>
                            PublicationOrganismActionGateReason
                                .ConstitutiveMicronutrientQuotaMissing,
                        OrganismActionGateReason.OffspringMicronutrientQuotaMissing =>
                            PublicationOrganismActionGateReason
                                .OffspringMicronutrientQuotaMissing,
                        OrganismActionGateReason.MaintenanceShortfall =>
                            PublicationOrganismActionGateReason.MaintenanceShortfall,
                        OrganismActionGateReason.ReserveProtectionFloor =>
                            PublicationOrganismActionGateReason.ReserveProtectionFloor,
                        OrganismActionGateReason.InternalCapacity =>
                            PublicationOrganismActionGateReason.InternalCapacity,
                        OrganismActionGateReason.ResourceSupply =>
                            PublicationOrganismActionGateReason.ResourceSupply,
                        OrganismActionGateReason.ClaimContention =>
                            PublicationOrganismActionGateReason.ClaimContention,
                        OrganismActionGateReason.LifecycleIneligible =>
                            PublicationOrganismActionGateReason.LifecycleIneligible,
                        _ => throw new InvalidOperationException(
                            $"Unsupported organism action gate reason {value.Reason}."),
                    },
                    value.AvailableQ,
                    value.RequiredQ,
                    value.ResourceId,
                    value.ClearsAtTick))
                .ToImmutableArray());
    }

    private static uint ComputeResourcePressureQ(OrganismSnapshot organism)
    {
        var reservePressure = BehaviorMath.OneQ - organism.Condition.ReserveFactorQ;
        var energyShortfall = BehaviorMath.OneQ - Math.Min(
            BehaviorMath.OneQ,
            organism.Behavior.RecentEnergyCoverageQ);
        var acquisitionFailure = BehaviorMath.OneQ - Math.Min(
            BehaviorMath.OneQ,
            organism.Behavior.RecentAcquisitionCoverageQ);
        var materialPressure = checked((uint)((ulong)organism.Behavior.LimitingMaterialDeficitQ *
            acquisitionFailure / BehaviorMath.OneQ));
        return Math.Max(reservePressure, Math.Max(energyShortfall, materialPressure));
    }

    private PublicationRemnant ToPublicationRemnant(RemnantSnapshot remnant)
    {
        var spatial = world.GetCompiledPhenotype(remnant.SourceSpeciesId).Physiology.Spatial;
        return new(
        remnant.Id,
        remnant.SourceOrganismId,
        remnant.SourceSpeciesId,
        remnant.TileId,
        remnant.PositionXQ,
        remnant.PositionYQ,
        SpatialMath.BodyRadiusQ(spatial, remnant.StructuralMatterQ),
        remnant.CreatedTick,
        remnant.StructuralMatterQ,
        remnant.ChargedReserveQ,
        ToPublicationMicronutrients(remnant.Micronutrients));
    }

    private static ImmutableArray<PublicationResourceStock> ToPublicationMicronutrients(
        MicronutrientInventory inventory) => Enumerable.Range(0, MicronutrientInventory.Count)
        .Where(slot => inventory[slot] > 0)
        .Select(slot => new PublicationResourceStock(
            MicronutrientInventory.ResourceIdAt(slot), inventory[slot]))
        .ToImmutableArray();

    private WorldPersistenceMetadata CapturePersistenceMetadataCore() =>
        new(
            publishedSnapshot,
            Status,
            WorldStateHasher.SimulationVersion,
            WorldStateHasher.SchemaVersion,
            randomCompatibility,
            Rules.RulePack.Identity,
            Rules.Scenario.Id,
            Rules.Identity);

    private static PersistenceOrganism ToPersistenceOrganism(OrganismSnapshot organism) =>
        new(
            organism.Id.Value,
            organism.SpeciesId.Value,
            organism.TileId.Value,
            organism.PositionXQ,
            organism.PositionYQ,
            organism.VelocityXQPerHour,
            organism.VelocityYQPerHour,
            organism.BirthTick,
            organism.BiologicalAgeHours,
            (byte)organism.LifecyclePhase,
            organism.ReproductionNotBeforeTick,
            organism.SuccessfulReproductionCount,
            organism.ScavengeNotBeforeTick,
            organism.IngestedStructuralMatterQ,
            organism.StructuralMatterQ,
            organism.ChargedReserveQ,
            organism.Condition.EvaluatedTick,
            (byte)organism.Condition.SnapshotKind,
            organism.Condition.ReserveFactorQ,
            organism.Condition.StructureFactorQ,
            organism.Condition.NutrientFactorQ,
            organism.Condition.AgeFactorQ,
            organism.Condition.LifecycleFactorQ,
            organism.Condition.EnvironmentalFactorQ,
            organism.Condition.RelativeHealthQ,
            organism.Condition.TemperatureMilliC,
            organism.Condition.TemperatureSeverityQ,
            (byte)organism.Behavior.BehaviorId,
            (byte)organism.Behavior.TargetKind,
            organism.Behavior.TargetId,
            organism.Behavior.TargetPositionXQ,
            organism.Behavior.TargetPositionYQ,
            organism.Behavior.SelectedAtTick,
            organism.Behavior.MinimumDwellUntilTick,
            organism.Behavior.RecentEnergyCoverageQ,
            organism.Behavior.RecentAcquisitionCoverageQ,
            organism.Behavior.LimitingMaterialDeficitQ,
            organism.CommittedMicronutrients.ToImmutableArray(),
            organism.FreeMicronutrients.ToImmutableArray());

    private static PersistenceRemnant ToPersistenceRemnant(RemnantSnapshot remnant) => new(
        remnant.Id.Value,
        remnant.SourceOrganismId.Value,
        remnant.SourceSpeciesId.Value,
        remnant.CreatedTick,
        remnant.TileId.Value,
        remnant.PositionXQ,
        remnant.PositionYQ,
        remnant.StructuralMatterQ,
        remnant.ChargedReserveQ,
        remnant.StructureDecayRemainderQ,
        remnant.ReserveDecayRemainderQ,
        remnant.Micronutrients.ToImmutableArray());

    private static PersistenceResourceTransaction ToPersistenceTransaction(
        ResourceTransaction transaction) =>
        new(
            transaction.Key.Tick,
            (byte)transaction.Key.Phase,
            transaction.Key.ScopeId,
            transaction.Key.ActorId,
            transaction.Key.ReactionId,
            transaction.Key.LocalOrdinal,
            (byte)transaction.Cause,
            transaction.ReactionId.Value,
            transaction.ActorId.Value,
            transaction.TileId.Value,
            transaction.Extent,
            transaction.MatterEntries
                .Select(entry => new PersistenceMatterEntry(
                    (byte)entry.Account.OwnerKind,
                    entry.Account.OwnerId,
                    (byte)entry.Account.Compartment,
                    entry.Account.ResourceId.Value,
                    entry.DeltaQ))
                .ToImmutableArray(),
            transaction.EnergyEntries
                .Select(entry => new PersistenceEnergyEntry(
                    (byte)entry.Account.Kind,
                    entry.Account.OwnerId,
                    entry.DeltaQ))
                .ToImmutableArray());

    private static PersistenceOrganismJourneyEvent ToPersistenceJourneyEvent(
        OrganismJourneyEvent value) => new(
            value.EventId,
            value.Tick,
            (byte)value.Phase,
            (byte)value.Family,
            value.SubjectOrganismId.Value,
            value.SubjectSpeciesId.Value,
            value.TileId.Value,
            value.PositionXQ,
            value.PositionYQ,
            value.RelatedOrganismId?.Value ?? 0,
            value.RelatedRemnantId?.Value ?? 0,
            value.ResourceId?.Value ?? 0,
            value.AmountQ,
            value.DetailId,
            value.DeathCauseProbabilities.Select(cause =>
                new PersistenceJourneyDeathCause(
                    (byte)cause.Cause,
                    cause.ProbabilityQ,
                    cause.Triggered)).ToImmutableArray());

    private static OrganismJourneyEvent ToJourneyEvent(
        PersistenceOrganismJourneyEvent value) => new(
            value.EventId,
            value.Tick,
            (TickPhase)value.Phase,
            (OrganismJourneyEventFamily)value.Family,
            OrganismId.FromAllocatedValue(value.SubjectOrganismId),
            SpeciesId.FromAllocatedValue(value.SubjectSpeciesId),
            TileId.FromRowMajorIndex(value.TileId),
            value.PositionXQ,
            value.PositionYQ,
            value.RelatedOrganismId == 0
                ? null
                : OrganismId.FromAllocatedValue(value.RelatedOrganismId),
            value.RelatedRemnantId == 0
                ? null
                : RemnantId.FromAllocatedValue(value.RelatedRemnantId),
            value.ResourceId == 0 ? null : ResourceId.From(value.ResourceId),
            value.AmountQ,
            value.DetailId,
            value.DeathCauses.Select(cause => new JourneyDeathCauseProbability(
                (IntrinsicDeathCause)cause.Cause,
                cause.ProbabilityQ,
                cause.Triggered)).ToImmutableArray());

    private static PersistenceLineageReviewObservation ToPersistenceLineageReviewObservation(
        LineageReviewObservation value) => new(
            value.SpeciesId.Value,
            (byte)value.Scope,
            value.Population,
            value.AverageHealthQ,
            value.AverageReserveQ,
            value.AverageAcquisitionCoverageQ,
            value.AverageResourcePressureQ,
            value.OccupiedTileCount,
            value.BehaviorCounts.Select(count =>
                new PersistenceLineageReviewBehaviorCount(
                    (byte)count.BehaviorId,
                    count.Count)).ToImmutableArray(),
            value.ActivityCountsAvailable,
            value.BirthCount,
            value.DeathCount,
            value.MigrationCount);

    private static LineageReviewObservation ToLineageReviewObservation(
        PersistenceLineageReviewObservation value) => new(
            SpeciesId.FromAllocatedValue(value.SpeciesId),
            (LineageReviewObservationScope)value.Scope,
            value.Population,
            value.AverageHealthQ,
            value.AverageReserveQ,
            value.AverageAcquisitionCoverageQ,
            value.AverageResourcePressureQ,
            value.OccupiedTileCount,
            value.BehaviorCounts.Select(count => new LineageReviewBehaviorCount(
                (OrganismBehaviorId)count.BehaviorId,
                count.Count)).ToImmutableArray(),
            value.ActivityCountsAvailable,
            value.BirthCount,
            value.DeathCount,
            value.MigrationCount);

    private static PersistenceLineageReviewSchedule ToPersistenceLineageReviewSchedule(
        LineageReviewSchedule value) => new(
            value.SpeciationEventId,
            value.AppliedTick,
            value.AppliedSimulatedHours,
            value.AncestorSpeciesId.Value,
            value.DescendantSpeciesId.Value,
            value.PerspectiveSpeciesId.Value,
            value.TraitDelta.Select(id => id.Value).ToImmutableArray(),
            value.CooldownBoundaryTick,
            value.FollowUpBoundaryTick,
            value.FollowUpHours,
            (byte)value.FollowUpEvidenceKind,
            ToPersistenceLineageReviewObservation(value.PerspectiveBaseline),
            ToPersistenceLineageReviewObservation(value.ComparisonBaseline),
            value.CapabilityActivations.Select(ToPersistenceCapabilityActivation)
                .ToImmutableArray(),
            value.ReactionActivations.Select(ToPersistenceReactionActivation)
                .ToImmutableArray());

    private static LineageReviewSchedule ToLineageReviewSchedule(
        PersistenceLineageReviewSchedule value) => new(
            value.SpeciationEventId,
            value.AppliedTick,
            value.AppliedSimulatedHours,
            SpeciesId.FromAllocatedValue(value.AncestorSpeciesId),
            SpeciesId.FromAllocatedValue(value.DescendantSpeciesId),
            SpeciesId.FromAllocatedValue(value.PerspectiveSpeciesId),
            value.TraitDelta.Select(TraitId.From).ToImmutableArray(),
            value.CooldownBoundaryTick,
            value.FollowUpBoundaryTick,
            value.FollowUpHours,
            (LineageReviewEvidenceKind)value.FollowUpEvidenceKind,
            ToLineageReviewObservation(value.PerspectiveBaseline),
            ToLineageReviewObservation(value.ComparisonBaseline),
            value.CapabilityActivations.Select(ToCapabilityActivation).ToImmutableArray(),
            value.ReactionActivations.Select(ToReactionActivation).ToImmutableArray());

    private static PersistenceLineageReviewLandmark ToPersistenceLineageReviewLandmark(
        LineageReviewLandmark value) => new(
            value.EventId,
            (byte)value.Kind,
            value.CompletedTick,
            value.SimulatedHours,
            value.WindowHours,
            value.SpeciationEventId,
            value.AncestorSpeciesId.Value,
            value.DescendantSpeciesId.Value,
            value.PerspectiveSpeciesId.Value,
            value.TraitDelta.Select(id => id.Value).ToImmutableArray(),
            (byte)value.EvidenceKind,
            ToPersistenceLineageReviewObservation(value.PerspectiveBaseline),
            ToPersistenceLineageReviewObservation(value.ComparisonBaseline),
            ToPersistenceLineageReviewObservation(value.PerspectiveCurrent),
            ToPersistenceLineageReviewObservation(value.ComparisonCurrent),
            value.CapabilityActivations.Select(ToPersistenceCapabilityActivation)
                .ToImmutableArray(),
            value.ReactionActivations.Select(ToPersistenceReactionActivation)
                .ToImmutableArray(),
            value.EvidenceReferences.Select(reference =>
                new PersistenceLineageReviewEvidenceReference(
                    (byte)reference.Kind,
                    reference.SpeciesId?.Value ?? 0,
                    reference.TileId?.Value,
                    reference.FromExclusiveTick,
                    reference.ThroughCompletedTick)).ToImmutableArray());

    private static LineageReviewLandmark ToLineageReviewLandmark(
        PersistenceLineageReviewLandmark value) => new(
            value.EventId,
            (LineageReviewLandmarkKind)value.Kind,
            value.CompletedTick,
            value.SimulatedHours,
            value.WindowHours,
            value.SpeciationEventId,
            SpeciesId.FromAllocatedValue(value.AncestorSpeciesId),
            SpeciesId.FromAllocatedValue(value.DescendantSpeciesId),
            SpeciesId.FromAllocatedValue(value.PerspectiveSpeciesId),
            value.TraitDelta.Select(TraitId.From).ToImmutableArray(),
            (LineageReviewEvidenceKind)value.EvidenceKind,
            ToLineageReviewObservation(value.PerspectiveBaseline),
            ToLineageReviewObservation(value.ComparisonBaseline),
            ToLineageReviewObservation(value.PerspectiveCurrent),
            ToLineageReviewObservation(value.ComparisonCurrent),
            value.CapabilityActivations.Select(ToCapabilityActivation).ToImmutableArray(),
            value.ReactionActivations.Select(ToReactionActivation).ToImmutableArray(),
            value.EvidenceReferences.Select(reference =>
                new LineageReviewEvidenceReference(
                    (LineageReviewEvidenceReferenceKind)reference.Kind,
                    reference.SpeciesId == 0
                        ? null
                        : SpeciesId.FromAllocatedValue(reference.SpeciesId),
                    reference.TileId.HasValue
                        ? TileId.FromRowMajorIndex(reference.TileId.Value)
                        : null,
                    reference.FromExclusiveTick,
                    reference.ThroughCompletedTick)).ToImmutableArray());

    private static PersistenceNotableEvent ToPersistenceNotableEvent(NotableEvent value) => new(
        value.EventId,
        (byte)value.Family,
        (byte)value.Significance,
        value.SignificanceRuleVersion,
        value.CompletedTick,
        value.SimulatedHours,
        value.SpeciesId.Value,
        value.RelatedSpeciesId?.Value ?? 0,
        value.TileId?.Value,
        value.ReactionId?.Value ?? 0,
        value.SourceEventId,
        value.MilestoneValue,
        value.BaselineValue,
        value.DeduplicationKey);

    private static NotableEvent ToNotableEvent(PersistenceNotableEvent value) => new(
        value.EventId,
        (NotableEventFamily)value.Family,
        (NotableEventSignificance)value.Significance,
        value.SignificanceRuleVersion,
        value.CompletedTick,
        value.SimulatedHours,
        SpeciesId.FromAllocatedValue(value.SpeciesId),
        value.RelatedSpeciesId == 0
            ? null
            : SpeciesId.FromAllocatedValue(value.RelatedSpeciesId),
        value.TileId.HasValue ? TileId.FromRowMajorIndex(value.TileId.Value) : null,
        value.ReactionId == 0 ? null : ReactionId.From(value.ReactionId),
        value.SourceEventId,
        value.MilestoneValue,
        value.DeduplicationKey,
        value.BaselineValue);

    private static PersistenceAttentionAlert ToPersistenceAttentionAlert(
        AttentionAlert value) => new(
            value.AlertId,
            (byte)value.AlertClass,
            (byte)value.Kind,
            (byte)(value.EventFamily ?? 0),
            value.CompletedTick,
            value.SimulatedHours,
            value.SpeciesId.Value,
            value.ChronicleEventIds,
            value.DeduplicationKey);

    private static AttentionAlert ToAttentionAlert(PersistenceAttentionAlert value) => new(
        value.AlertId,
        (AttentionAlertClass)value.AlertClass,
        (AttentionAlertKind)value.Kind,
        value.EventFamily == 0 ? null : (NotableEventFamily)value.EventFamily,
        value.CompletedTick,
        value.SimulatedHours,
        SpeciesId.FromAllocatedValue(value.SpeciesId),
        value.ChronicleEventIds,
        value.DeduplicationKey);

    private static PersistencePopulationAttentionState ToPersistencePopulationAttentionState(
        PopulationAttentionState value) => new(
            value.SpeciesId.Value,
            value.HasExceededDangerThreshold,
            value.LowPopulationArmed,
            value.LowPopulationEpisodeOrdinal);

    private static PopulationAttentionState ToPopulationAttentionState(
        PersistencePopulationAttentionState value) => new(
            SpeciesId.FromAllocatedValue(value.SpeciesId),
            value.HasExceededDangerThreshold,
            value.LowPopulationArmed,
            value.LowPopulationEpisodeOrdinal);

    private static PersistenceAttentionWindowState ToPersistenceAttentionWindowState(
        AttentionWindowState value) => new(
            value.SpeciesId.Value,
            value.PopulationDeclineArmed,
            value.PopulationDeclineEpisodeOrdinal,
            value.LowHealthConsecutiveHours,
            value.LowHealthArmed,
            value.HealthyRecoveryConsecutiveHours,
            value.LowHealthEpisodeOrdinal,
            value.ResourcePressureConsecutiveHours,
            value.ResourcePressureArmed,
            value.ResourcePressureRecoveryConsecutiveHours,
            value.ResourcePressureEpisodeOrdinal,
            value.PopulationSamples.Select(sample =>
                new PersistencePopulationAttentionSample(
                    sample.SimulatedHours,
                    sample.Population)).ToImmutableArray());

    private static AttentionWindowState ToAttentionWindowState(
        PersistenceAttentionWindowState value) => new(
            SpeciesId.FromAllocatedValue(value.SpeciesId),
            value.PopulationDeclineArmed,
            value.PopulationDeclineEpisodeOrdinal,
            value.LowHealthConsecutiveHours,
            value.LowHealthArmed,
            value.HealthyRecoveryConsecutiveHours,
            value.LowHealthEpisodeOrdinal,
            value.ResourcePressureConsecutiveHours,
            value.ResourcePressureArmed,
            value.ResourcePressureRecoveryConsecutiveHours,
            value.ResourcePressureEpisodeOrdinal,
            value.PopulationSamples.Select(sample => new PopulationAttentionSample(
                sample.SimulatedHours,
                sample.Population)).ToImmutableArray());

    private static PersistenceLineageReviewCapabilityActivation
        ToPersistenceCapabilityActivation(LineageReviewCapabilityActivation value) => new(
            (byte)value.Kind,
            value.SourceTraitId.Value,
            value.IntroducedByProposal,
            value.Installed,
            value.ActivationCount);

    private static LineageReviewCapabilityActivation ToCapabilityActivation(
        PersistenceLineageReviewCapabilityActivation value) => new(
            (LineageReviewCapabilityKind)value.Kind,
            TraitId.From(value.SourceTraitId),
            value.IntroducedByProposal,
            value.Installed,
            value.ActivationCount);

    private static PersistenceLineageReviewReactionActivation
        ToPersistenceReactionActivation(LineageReviewReactionActivation value) => new(
            value.ReactionId.Value,
            value.IntroducedByProposal,
            value.Installed,
            value.ActivationCount);

    private static LineageReviewReactionActivation ToReactionActivation(
        PersistenceLineageReviewReactionActivation value) => new(
            ReactionId.From(value.ReactionId),
            value.IntroducedByProposal,
            value.Installed,
            value.ActivationCount);

    private static PersistenceOrganismRoutineActivitySummary
        ToPersistenceRoutineActivitySummary(OrganismRoutineActivitySummary value) => new(
            value.BucketStartHour,
            value.PeriodHours,
            value.SubjectOrganismId.Value,
            value.SubjectSpeciesId.Value,
            value.TileId.Value,
            value.ResourceAcquisitions.Select(resource =>
                new PersistenceRoutineResourceAcquisition(
                    resource.ResourceId.Value,
                    resource.AmountQ)).ToImmutableArray());

    private static OrganismRoutineActivitySummary ToRoutineActivitySummary(
        PersistenceOrganismRoutineActivitySummary value) => new(
            value.BucketStartHour,
            value.PeriodHours,
            OrganismId.FromAllocatedValue(value.SubjectOrganismId),
            SpeciesId.FromAllocatedValue(value.SubjectSpeciesId),
            TileId.FromRowMajorIndex(value.TileId),
            value.ResourceAcquisitions.Select(resource => new RoutineResourceAcquisition(
                ResourceId.From(resource.ResourceId),
                resource.AmountQ)).ToImmutableArray());

    private static ImmutableArray<ResourceTransaction> RestoreTransactions(
        ImmutableArray<PersistenceResourceTransaction> transactions,
        ulong completedTick,
        MutableWorldState world)
    {
        var restored = ImmutableArray.CreateBuilder<ResourceTransaction>(transactions.Length);
        foreach (var transaction in transactions)
        {
            if (transaction.Tick != completedTick ||
                !Enum.IsDefined((TickPhase)transaction.Phase) ||
                !Enum.IsDefined((LedgerCause)transaction.Cause) ||
                transaction.MatterEntries.IsDefault ||
                transaction.EnergyEntries.IsDefault)
            {
                throw new ArgumentException("A persisted ledger transaction is invalid.");
            }

            var cause = (LedgerCause)transaction.Cause;
            var environmental = cause is LedgerCause.EnvironmentalGasSource or
                LedgerCause.EnvironmentalGasSink or LedgerCause.EnvironmentalGasExchange or
                LedgerCause.EnvironmentalResourceSource or LedgerCause.RemnantDecay;
            var actorId = environmental
                ? default
                : OrganismId.FromAllocatedValue(transaction.ActorId);
            var tileId = TileId.FromRowMajorIndex(transaction.TileId);
            if (!environmental)
            {
                _ = world.GetOrganism(actorId);
            }
            _ = world.GetTile(tileId);
            restored.Add(new ResourceTransaction(
                new LedgerTransactionKey(
                    transaction.Tick,
                    (TickPhase)transaction.Phase,
                    transaction.ScopeId,
                    transaction.KeyActorId,
                    transaction.KeyReactionId,
                    transaction.LocalOrdinal),
                cause,
                ReactionId.From(transaction.ReactionId),
                actorId,
                tileId,
                transaction.Extent,
                transaction.MatterEntries
                    .Select(entry => new MatterLedgerEntry(
                        new MatterAccountKey(
                            (MatterAccountOwnerKind)entry.OwnerKind,
                            entry.OwnerId,
                            (MatterCompartment)entry.Compartment,
                            ResourceId.From(entry.ResourceId)),
                        entry.DeltaQ))
                    .ToImmutableArray(),
                transaction.EnergyEntries
                    .Select(entry => new EnergyLedgerEntry(
                        new EnergyAccountKey(
                            (EnergyAccountKind)entry.Kind,
                            entry.OwnerId),
                        entry.DeltaQ))
                    .ToImmutableArray()));
        }

        return restored.MoveToImmutable();
    }

    private static ImmutableArray<PublicationResourceFlowHistoryInterval>
        RestoreResourceFlowHistory(
            ImmutableArray<PersistenceResourceFlowHistoryInterval> persisted,
            ulong completedTick,
            ulong simulatedHours,
            uint tickDurationHours,
            MutableWorldState world)
    {
        if (persisted.IsDefault ||
            (completedTick == 0) != persisted.IsEmpty ||
            persisted.Length > checked((int)ResourceFlowHistoryRetentionHours))
        {
            throw new ArgumentException("Persisted resource-flow history is incomplete or oversized.");
        }

        var result = ImmutableArray.CreateBuilder<PublicationResourceFlowHistoryInterval>(
            persisted.Length);
        ulong priorTick = 0;
        ulong priorEndHour = 0;
        for (var index = 0; index < persisted.Length; index++)
        {
            var interval = persisted[index];
            if (interval.PeriodHours != tickDurationHours ||
                interval.CompletedTick == 0 ||
                interval.EndSimulatedHour != checked(interval.CompletedTick * tickDurationHours) ||
                (index > 0 && (interval.CompletedTick != priorTick + 1 ||
                    interval.EndSimulatedHour != priorEndHour + tickDurationHours)) ||
                interval.ResourceFlows.IsDefault)
            {
                throw new ArgumentException("A persisted resource-flow interval is invalid.");
            }

            var flows = ImmutableArray.CreateBuilder<PublicationTileResourceFlow>(
                interval.ResourceFlows.Length);
            (uint TileId, uint ResourceId, byte Kind)? priorKey = null;
            foreach (var flow in interval.ResourceFlows)
            {
                if (!Enum.IsDefined((PublicationResourceFlowKind)flow.Kind) || flow.AmountQ <= 0)
                {
                    throw new ArgumentException("A persisted resource flow is invalid.");
                }
                var key = (flow.TileId, flow.ResourceId, flow.Kind);
                if (priorKey is { } previous && previous.CompareTo(key) >= 0)
                {
                    throw new ArgumentException("Persisted resource flows are not canonical.");
                }
                var tileId = TileId.FromRowMajorIndex(flow.TileId);
                var resourceId = ResourceId.From(flow.ResourceId);
                _ = world.GetTile(tileId);
                _ = world.GetResourceHandle(resourceId);
                flows.Add(new PublicationTileResourceFlow(
                    tileId,
                    resourceId,
                    (PublicationResourceFlowKind)flow.Kind,
                    flow.AmountQ));
                priorKey = key;
            }

            result.Add(new PublicationResourceFlowHistoryInterval(
                interval.CompletedTick,
                interval.EndSimulatedHour,
                interval.PeriodHours,
                flows.MoveToImmutable()));
            priorTick = interval.CompletedTick;
            priorEndHour = interval.EndSimulatedHour;
        }

        if (result.Count > 0 &&
            (result[^1].CompletedTick != completedTick ||
                result[^1].EndSimulatedHour != simulatedHours ||
                simulatedHours - checked(result[0].EndSimulatedHour - tickDurationHours) >
                    ResourceFlowHistoryRetentionHours))
        {
            throw new ArgumentException("Persisted resource-flow history does not end at the save boundary.");
        }

        return result.MoveToImmutable();
    }

    private static void ValidateRestoreCompatibility(
        CompiledWorldRules rules,
        WorldPersistenceSnapshot persisted)
    {
        var metadata = persisted.Metadata ??
            throw new ArgumentException("Persistence metadata is required.", nameof(persisted));
        var boundary = metadata.Boundary ??
            throw new ArgumentException("A completed persistence boundary is required.", nameof(persisted));
        if (!boundary.WorldId.IsValid ||
            metadata.Lifecycle != WorldRunnerStatus.PausedReady ||
            metadata.EngineSimulationVersion != WorldStateHasher.SimulationVersion ||
            metadata.WorldStateHashSchemaVersion != WorldStateHasher.SchemaVersion ||
            metadata.RulePackIdentity != rules.RulePack.Identity ||
            metadata.ScenarioId != rules.Scenario.Id ||
            metadata.WorldRulesIdentity != rules.Identity ||
            metadata.RandomCompatibility !=
                RandomCompatibility.ForSeed(metadata.RandomCompatibility.RootSeed) ||
            boundary.TickDurationHours != rules.TickDurationHours ||
            boundary.OrganismCount != persisted.State.Organisms.Length ||
            boundary.SimulatedHours != checked(boundary.CompletedTick * rules.TickDurationHours))
        {
            throw new WorldRestoreException(
                "The persisted boundary is incompatible with the supplied compiled world rules.");
        }
    }
}

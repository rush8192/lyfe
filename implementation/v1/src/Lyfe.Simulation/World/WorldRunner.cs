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
    private ImmutableArray<OrganismJourneyEvent> journeyEvents = [];
    private ImmutableArray<OrganismRoutineActivitySummary> routineActivitySummaries = [];
    private ImmutableArray<OrganismJourneyEvent> lastActivityPulseEvents = [];
    private ulong nextJourneyEventId = 1;

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
            journeyEvents = persisted.State.JourneyEvents
                .Select(ToJourneyEvent)
                .ToImmutableArray();
            routineActivitySummaries = persisted.State.RoutineActivitySummaries
                .Select(ToRoutineActivitySummary)
                .ToImmutableArray();
            lastActivityPulseEvents = [];
            nextJourneyEventId = persisted.State.NextJourneyEventId;
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
        uint founderCount = DefaultFoundationPopulation) =>
        new(
            worldId,
            rules,
            rootSeed,
            DefaultSandboxSetup(rules, rootSeed, founderCount),
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
            DefaultSandboxSetup(rules, rootSeed, founderCount),
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
                        state.Lineage.ExtinctTick);
                })
                .ToImmutableArray();
            var organisms = world.GetOrganismIdsInCanonicalOrder()
                .Select(id => ToPublicationOrganism(world.GetOrganism(id)))
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
                lastActivityPulseEvents);
        }
    }

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
            var persistedJourneyEvents = journeyEvents
                .Select(ToPersistenceJourneyEvent)
                .ToImmutableArray();
            var persistedRoutineSummaries = routineActivitySummaries
                .Select(ToPersistenceRoutineActivitySummary)
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
                    transactions,
                    persistedGame));
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
                var changes = tickPipeline.Execute(world, context);
                var completedTransactions = GetTransactions(changes);
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
                var nextSnapshot = CaptureWorkingSnapshot(
                    nextTick,
                    nextRevision,
                    nextSimulatedHours,
                    completedTransactions);

                LastCompletedTickChanges = changes;
                LastIntrinsicDeathAssessments =
                    scratch.RequireIntrinsicDeathAssessments(nextTick);
                LastDeathRecords = scratch.RequireDeathRecords(nextTick);
                lastCompletedTransactions = completedTransactions;
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

    private static ImmutableArray<OrganismRoutineActivitySummary>
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
        SemanticRandomOracle oracle,
        PhaseChangeBuilder changes)
    {
        var phenotype = world.GetCompiledPhenotype(speciesId);
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
            var jitter = oracle.UniformInclusive(
                RandomAddress.Create(
                    RandomDomains.ReproductionCooldownJitter,
                    organismId.Value,
                    0,
                    0),
                reproduction.CooldownJitterMaximumHours);
            world.SetOrganismLifecycleSchedule(
                organismId,
                checked((reproduction.BaseCooldownHours + jitter +
                    Rules.TickDurationHours - 1) / Rules.TickDurationHours),
                0,
                0,
                changes);
        }
    }

    private static GameSetupCommand DefaultSandboxSetup(
        CompiledWorldRules rules,
        RootRandomSeed rootSeed,
        uint founderCount)
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
                .Single(value => value.IsBaseline).Id);
    }

    private static void ValidateSetup(CompiledWorldRules rules, GameSetupCommand setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (!Enum.IsDefined(setup.Mode) ||
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

    private static ImmutableArray<ResourceTransaction> GetTransactions(TickChangeSet changes) =>
        changes.Phases
            .SelectMany(phase => phase.ResourceTransactions)
            .OrderBy(transaction => transaction.Key)
            .ToImmutableArray();

    private PublicationOrganism ToPublicationOrganism(OrganismSnapshot organism)
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
            ToPublicationMicronutrients(organism.FreeMicronutrients));
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
                LedgerCause.EnvironmentalGasSink or LedgerCause.EnvironmentalGasExchange;
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

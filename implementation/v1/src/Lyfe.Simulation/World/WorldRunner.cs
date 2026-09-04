using System.Collections.Immutable;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.Ticks;

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

public sealed record TickResult(WorldSnapshot Snapshot, TickChangeSet Changes);

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

    private WorldRunner(
        WorldId worldId,
        CompiledWorldRules rules,
        RootRandomSeed rootSeed,
        uint founderCount,
        ITickFaultInjector faultInjector)
    {
        if (!worldId.IsValid)
        {
            throw new ArgumentException("A world ID must be nonzero.", nameof(worldId));
        }

        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(faultInjector);
        if (founderCount is 0 or > MaximumFoundationPopulation)
        {
            throw new ArgumentOutOfRangeException(
                nameof(founderCount),
                founderCount,
                $"The foundation population must be between 1 and {MaximumFoundationPopulation}.");
        }
        if (rules.TickDurationHours == 0)
        {
            throw new ArgumentException("A compiled world must have a positive tick duration.", nameof(rules));
        }

        WorldId = worldId;
        Rules = rules;
        this.faultInjector = faultInjector;
        randomCompatibility = RandomCompatibility.ForSeed(rootSeed);
        randomOracle = new SemanticRandomOracle(rootSeed);
        world = new MutableWorldState(rules);
        InitializeFoundationPopulation(rootSeed, founderCount);
        world.ValidateInvariants();
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
            ValidateRestoreCompatibility(rules, persisted);
            var metadata = persisted.Metadata;
            WorldId = metadata.Boundary.WorldId;
            Rules = rules;
            this.faultInjector = faultInjector;
            randomCompatibility = metadata.RandomCompatibility;
            randomOracle = new SemanticRandomOracle(randomCompatibility.RootSeed);
            world = MutableWorldState.Restore(rules, persisted.State);
            lastCompletedTransactions = RestoreTransactions(
                persisted.State.LastCompletedTransactions,
                metadata.Boundary.CompletedTick,
                world);
            ResourceLedgerOracle.Reconcile(rules.RulePack, lastCompletedTransactions);
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

    public TickChangeSet? LastCompletedTickChanges { get; private set; }

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
            founderCount,
            NoTickFaultInjector.Instance);

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
        new(worldId, rules, rootSeed, founderCount, faultInjector);

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
                    return new PublicationSpecies(
                        state.Id,
                        state.GenomeId,
                        state.Population);
                })
                .ToImmutableArray();
            var organisms = world.GetOrganismIdsInCanonicalOrder()
                .Select(id => ToPublicationOrganism(world.GetOrganism(id)))
                .ToImmutableArray();
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
                tiles.MoveToImmutable(),
                species,
                organisms);
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

            var genomes = world.GetGenomeIdsInCanonicalOrder()
                .Select(id =>
                {
                    var genome = world.GetGenome(id);
                    return new PersistenceGenome(id.Value, genome.FounderGenomeId.Value);
                })
                .ToImmutableArray();
            var species = world.GetSpeciesIdsInCanonicalOrder()
                .Select(id =>
                {
                    var current = world.GetSpecies(id);
                    return new PersistenceSpecies(
                        id.Value,
                        current.GenomeId.Value,
                        current.Population);
                })
                .ToImmutableArray();
            var organisms = world.GetOrganismIdsInCanonicalOrder()
                .Select(id => ToPersistenceOrganism(world.GetOrganism(id)))
                .ToImmutableArray();
            var transactions = lastCompletedTransactions
                .Select(ToPersistenceTransaction)
                .ToImmutableArray();

            return new WorldPersistenceSnapshot(
                CapturePersistenceMetadataCore(),
                new WorldPersistenceState(
                    counters.NextGenomeId,
                    counters.NextSpeciesId,
                    counters.NextOrganismId,
                    tileResources.MoveToImmutable(),
                    genomes,
                    species,
                    organisms,
                    transactions));
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

            var nextTick = checked(publishedSnapshot.CompletedTick + 1);
            var nextRevision = checked(publishedSnapshot.WorldRevision + 1);
            var nextSimulatedHours = checked(
                publishedSnapshot.SimulatedHours + Rules.TickDurationHours);
            var context = new TickExecutionContext(
                nextRevision,
                nextTick,
                Rules.TickDurationHours,
                Rules.Identity.WorldRulesHash,
                faultInjector,
                randomOracle,
                new TickScratch(nextTick));

            try
            {
                var changes = tickPipeline.Execute(world, context);
                var nextSnapshot = CaptureWorkingSnapshot(
                    nextTick,
                    nextRevision,
                    nextSimulatedHours,
                    GetTransactions(changes));

                LastCompletedTickChanges = changes;
                lastCompletedTransactions = GetTransactions(changes);
                publishedSnapshot = nextSnapshot;
                return new TickResult(nextSnapshot, changes);
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

    private void InitializeFoundationPopulation(RootRandomSeed rootSeed, uint founderCount)
    {
        var scenario = Rules.RulePack.Scenarios[Rules.Scenario.DenseSlot];
        if (scenario.PermittedFounders.IsDefaultOrEmpty)
        {
            throw new InvalidOperationException(
                "The selected scenario must permit at least one founder genome.");
        }

        var founderGenome = scenario.PermittedFounders[0].Id;
        var tileId = TileId.FromRowMajorIndex(0);
        var oracle = new SemanticRandomOracle(rootSeed);
        var changes = world.BeginChanges();
        var genomeId = world.CreateGenome(founderGenome, changes);
        var speciesId = world.CreateSpecies(genomeId, changes);
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
                    FoundationStructuralMatterQ,
                    FoundationChargedReserveQ),
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
        }

        world.SealChanges(changes);
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

    private static PublicationOrganism ToPublicationOrganism(OrganismSnapshot organism) =>
        new(
            organism.Id,
            organism.SpeciesId,
            organism.TileId,
            organism.PositionXQ,
            organism.PositionYQ,
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
            organism.StructuralMatterQ,
            organism.ChargedReserveQ);

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
            organism.StructuralMatterQ,
            organism.ChargedReserveQ);

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

            var actorId = OrganismId.FromAllocatedValue(transaction.ActorId);
            var tileId = TileId.FromRowMajorIndex(transaction.TileId);
            _ = world.GetOrganism(actorId);
            _ = world.GetTile(tileId);
            restored.Add(new ResourceTransaction(
                new LedgerTransactionKey(
                    transaction.Tick,
                    (TickPhase)transaction.Phase,
                    transaction.ScopeId,
                    transaction.KeyActorId,
                    transaction.KeyReactionId,
                    transaction.LocalOrdinal),
                (LedgerCause)transaction.Cause,
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

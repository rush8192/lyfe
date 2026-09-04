using System.Collections.Immutable;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.Ticks;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class WorldRunnerTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("fedcba98765432100123456789abcdef");

    [Fact]
    public void NewWorldStartsAtAConfiguredPublishedBoundary()
    {
        var runner = CreateRunner(WorldId.From(42));

        var snapshot = runner.CaptureSnapshot();

        Assert.Equal(42UL, snapshot.WorldId.Value);
        Assert.Equal(0UL, snapshot.CompletedTick);
        Assert.Equal(0UL, snapshot.WorldRevision);
        Assert.Equal(0UL, snapshot.SimulatedHours);
        Assert.Equal(1U, snapshot.TickDurationHours);
        Assert.Equal(100, snapshot.OrganismCount);
        Assert.Equal(WorldRunnerStatus.PausedReady, runner.Status);
        Assert.Null(runner.Fault);
        Assert.Null(runner.LastCompletedTickChanges);
        Assert.Equal(64, snapshot.StateHash.Length);
    }

    [Fact]
    public void OneRealTickRunsEveryPhaseAndAdvancesOrganismAge()
    {
        var runner = CreateRunner(WorldId.From(1), founderCount: 3);
        var organismIds = runner.MutableWorld.GetOrganismIdsInCanonicalOrder();

        var result = runner.AdvanceOneTick();

        Assert.Equal(1UL, result.Snapshot.CompletedTick);
        Assert.Equal(1UL, result.Snapshot.WorldRevision);
        Assert.Equal(1UL, result.Snapshot.SimulatedHours);
        Assert.Equal(9, result.Changes.EvaluatedWorkCount);
        Assert.Equal(Enum.GetValues<TickPhase>(), result.Changes.Phases.Select(phase => phase.Phase));
        Assert.Equal(
            Enum.GetValues<TickPhase>().Length,
            result.Changes.Phases.Select(phase => phase.Phase).Distinct().Count());
        var intrinsic = Assert.Single(
            result.Changes.Phases,
            phase => phase.Phase == TickPhase.IntrinsicDeath);
        Assert.Equal(PhaseExecutionClass.IndependentMap, intrinsic.ExecutionClass);
        Assert.Equal(3, intrinsic.EvaluatedWorkCount);
        Assert.Equal(3, intrinsic.Changes.DirtyEntities.Length);
        Assert.All(
            intrinsic.Changes.DirtyEntities,
            dirty => Assert.Equal(LogicalFieldGroup.OrganismLifecycle, dirty.FieldGroup));
        Assert.All(
            organismIds,
            organismId => Assert.Equal(
                1UL,
                runner.MutableWorld.GetOrganism(organismId).BiologicalAgeHours));
        Assert.Equal(0, result.Changes.MergedChanges.StructuralChangeCount);
        Assert.Equal(7, result.Changes.MergedChanges.DirtyEntityReferenceCount);
        var tileChanges = Assert.Single(
            result.Changes.MergedChanges.StoreChanges,
            store => store.EntityKind == StateEntityKind.Tile);
        var tileResources = Assert.Single(tileChanges.DirtyFieldGroups);
        Assert.Equal(LogicalFieldGroup.TileResources, tileResources.FieldGroup);
        Assert.Equal([0UL], tileResources.Entities.Select(entity => entity.Value));
        var organismChanges = Assert.Single(
            result.Changes.MergedChanges.StoreChanges,
            store => store.EntityKind == StateEntityKind.Organism);
        Assert.Equal(
            [LogicalFieldGroup.OrganismLifecycle, LogicalFieldGroup.OrganismReserve],
            organismChanges.DirtyFieldGroups.Select(group => group.FieldGroup));
        Assert.All(
            organismChanges.DirtyFieldGroups,
            group => Assert.Equal(
                organismIds.Select(id => id.Value),
                group.Entities.Select(entity => entity.Value)));
        Assert.Equal(3, result.Changes.MergedChanges.ResourceTransactionReferences.Length);
        Assert.Equal(result.Changes, runner.LastCompletedTickChanges);
    }

    [Fact]
    public void EqualSeedsAndInputsProduceEqualBoundariesAndJournals()
    {
        var first = CreateRunner(WorldId.From(7));
        var second = CreateRunner(WorldId.From(7));

        Assert.Equal(first.CaptureSnapshot(), second.CaptureSnapshot());
        for (var tick = 0; tick < 10; tick++)
        {
            var firstResult = first.AdvanceOneTick();
            var secondResult = second.AdvanceOneTick();
            Assert.Equal(firstResult.Snapshot, secondResult.Snapshot);
            Assert.Equal(firstResult.Changes.CompletedTick, secondResult.Changes.CompletedTick);
            Assert.Equal(firstResult.Changes.WorldRevision, secondResult.Changes.WorldRevision);
            AssertJournalsEqual(firstResult.Changes.Phases, secondResult.Changes.Phases);
            Assert.Equal(
                TickChangeInspector.ToCanonicalJson(firstResult.Changes),
                TickChangeInspector.ToCanonicalJson(secondResult.Changes));
        }
    }

    [Fact]
    public void TickChangeInspectorHasAFrozenCanonicalVector()
    {
        var changes = CreateRunner(WorldId.From(1), founderCount: 3)
            .AdvanceOneTick()
            .Changes;

        var json = TickChangeInspector.ToCanonicalJson(changes);

        Assert.Equal(
            "{\"format\":\"TickChangeInspectorV1\",\"completedTick\":\"1\",\"worldRevision\":\"1\",\"evaluatedWorkCount\":9,\"stores\":[{\"entityKind\":1,\"creates\":[],\"removes\":[],\"relocations\":[],\"dirty\":[{\"fieldGroup\":1,\"entityIds\":[\"0\"]}]},{\"entityKind\":4,\"creates\":[],\"removes\":[],\"relocations\":[],\"dirty\":[{\"fieldGroup\":4,\"entityIds\":[\"1\",\"2\",\"3\"]},{\"fieldGroup\":6,\"entityIds\":[\"1\",\"2\",\"3\"]}]}],\"resourceTransactions\":[{\"tick\":\"1\",\"phase\":6,\"scopeId\":\"0\",\"actorId\":\"1\",\"reactionId\":1,\"localOrdinal\":0},{\"tick\":\"1\",\"phase\":6,\"scopeId\":\"0\",\"actorId\":\"2\",\"reactionId\":1,\"localOrdinal\":0},{\"tick\":\"1\",\"phase\":6,\"scopeId\":\"0\",\"actorId\":\"3\",\"reactionId\":1,\"localOrdinal\":0}]}",
            json);
    }

    [Fact]
    public void DifferentSeedsProduceDifferentFounderPositionsAndBoundaryHashes()
    {
        var first = CreateRunner(WorldId.From(7), founderCount: 2);
        var second = WorldRunner.CreateFoundation(
            WorldId.From(7),
            CompileWorld(),
            RootRandomSeed.Parse("00000000000000000000000000000001"),
            2);
        var firstIds = first.MutableWorld.GetOrganismIdsInCanonicalOrder();
        var secondIds = second.MutableWorld.GetOrganismIdsInCanonicalOrder();

        Assert.NotEqual(first.CaptureSnapshot().StateHash, second.CaptureSnapshot().StateHash);
        Assert.NotEqual(
            first.MutableWorld.GetOrganism(firstIds[0]).PositionXQ,
            second.MutableWorld.GetOrganism(secondIds[0]).PositionXQ);
    }

    [Fact]
    public void WorldHashIncludesResourcesButExcludesMutationTrackingHistory()
    {
        var runner = CreateRunner(WorldId.From(1), founderCount: 1);
        var world = runner.MutableWorld;
        var tileId = TileId.FromRowMajorIndex(0);
        var hydrogen = world.GetResourceHandle(ResourceId.From(1));
        var original = runner.ComputeWorkingStateHashForTesting();

        var debit = world.BeginChanges();
        world.ApplyTileResourceDelta(tileId, hydrogen, -1, debit);
        world.SealChanges(debit);
        var changed = runner.ComputeWorkingStateHashForTesting();

        var restore = world.BeginChanges();
        world.ApplyTileResourceDelta(tileId, hydrogen, 1, restore);
        world.SealChanges(restore);

        Assert.NotEqual(original, changed);
        Assert.Equal(original, runner.ComputeWorkingStateHashForTesting());
    }

    [Fact]
    public void PhysicalRowPermutationDoesNotChangeLogicalWorldHash()
    {
        var runner = WorldRunner.CreateFoundation(
            WorldId.From(1),
            CompileWorld(twoTiles: true),
            Seed,
            3);
        var world = runner.MutableWorld;
        var organismId = world.GetOrganismIdsInCanonicalOrder()[0];
        var organism = world.GetOrganism(organismId);
        var before = runner.ComputeWorkingStateHashForTesting();

        var migrate = world.BeginChanges();
        world.RelocateOrganism(
            organismId,
            TileId.FromRowMajorIndex(1),
            7,
            9,
            migrate);
        world.SealChanges(migrate);
        var returnHome = world.BeginChanges();
        world.RelocateOrganism(
            organismId,
            organism.TileId,
            organism.PositionXQ,
            organism.PositionYQ,
            returnHome);
        world.SealChanges(returnHome);

        Assert.Equal(before, runner.ComputeWorkingStateHashForTesting());
    }

    [Fact]
    public void CompletedLedgerIsHashedInCanonicalOrder()
    {
        var runner = CreateRunner(WorldId.From(1), founderCount: 3);
        var result = runner.AdvanceOneTick();
        var transactions = result.Changes.Phases
            .SelectMany(phase => phase.ResourceTransactions)
            .ToImmutableArray();

        var reversed = transactions
            .Reverse()
            .Select(transaction => transaction with
            {
                MatterEntries = transaction.MatterEntries.Reverse().ToImmutableArray(),
                EnergyEntries = transaction.EnergyEntries.Reverse().ToImmutableArray(),
            })
            .ToImmutableArray();

        Assert.Equal(
            result.Snapshot.StateHash,
            runner.ComputeWorkingStateHashForTesting(reversed));
        Assert.NotEqual(
            result.Snapshot.StateHash,
            runner.ComputeWorkingStateHashForTesting(ImmutableArray<ResourceTransaction>.Empty));
    }

    [Fact]
    public void NextEntityIdCountersAreContinuationState()
    {
        var runner = CreateRunner(WorldId.From(1), founderCount: 1);
        var world = runner.MutableWorld;
        var existing = world.GetOrganism(
            Assert.Single(world.GetOrganismIdsInCanonicalOrder()));
        var before = runner.ComputeWorkingStateHashForTesting();
        var changes = world.BeginChanges();
        var temporary = world.CreateOrganism(
            new OrganismInitialState(
                existing.SpeciesId,
                existing.TileId,
                1,
                2,
                0,
                0,
                0,
                0,
                LifecyclePhase.Mature,
                1_000,
                5_000),
            changes);
        world.RemoveOrganism(temporary, changes);
        world.SealChanges(changes);

        Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        Assert.NotEqual(before, runner.ComputeWorkingStateHashForTesting());
    }

    [Fact]
    public void WorldStateHashV1HasFrozenCreationAndFirstTickVectors()
    {
        var runner = WorldRunner.CreateFoundation(
            WorldId.From(1),
            CompileWorld(),
            RootRandomSeed.Parse("00000000000000000000000000000001"));

        var creationHash = runner.CaptureSnapshot().StateHash;
        var firstTickHash = runner.AdvanceOneTick().Snapshot.StateHash;

        Assert.Equal(
            "8efb01397a4a24b1956c7570dbeb15e70a1860b24975d1228e0a296ec0d03a8e",
            creationHash);
        Assert.Equal(
            "344e6aba5ede1918acc174f6b625db6239a9c15ffe34fffdddfcb3914c0946fc",
            firstTickHash);
    }

    [Fact]
    public void FailedPreflightAppliesNothingAndPublishesNothing()
    {
        var injector = new OneShotFaultInjector(
            TickPhase.IntrinsicDeath,
            TickFailureStage.Preflight);
        var runner = CreateRunnerForTesting(injector, founderCount: 2);
        var before = runner.CaptureSnapshot();
        var organismIds = runner.MutableWorld.GetOrganismIdsInCanonicalOrder();

        var exception = Assert.Throws<WorldTickException>(runner.AdvanceOneTick);

        Assert.Equal(TickPhase.IntrinsicDeath, exception.Fault.Phase);
        Assert.Equal(nameof(TickFailureStage.Preflight), exception.Fault.FailureStage);
        Assert.Equal(before, runner.CaptureSnapshot());
        Assert.Null(runner.LastCompletedTickChanges);
        Assert.Equal(WorldRunnerStatus.Faulted, runner.Status);
        Assert.True(runner.MutableWorld.IsFaulted);
        Assert.All(
            organismIds,
            organismId => Assert.Equal(
                0UL,
                runner.MutableWorld.GetOrganism(organismId).BiologicalAgeHours));
        Assert.Throws<InvalidOperationException>(runner.AdvanceOneTick);
    }

    [Fact]
    public void AgeOverflowIsRejectedByRealPreflightBeforePhaseMutation()
    {
        var runner = CreateRunner(WorldId.From(1), founderCount: 1);
        var world = runner.MutableWorld;
        var organismId = Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        var setupChanges = world.BeginChanges();
        world.AdvanceOrganismBiologicalAge(organismId, ulong.MaxValue, setupChanges);
        world.SealChanges(setupChanges);
        var generationBefore = world.CaptureGenerationStamp();
        var pipeline = new ScalarTickPipeline();
        var context = new TickExecutionContext(
            1,
            1,
            1,
            runner.Rules.Identity.WorldRulesHash,
            NoTickFaultInjector.Instance,
            new SemanticRandomOracle(Seed),
            new TickScratch(1));

        var exception = Assert.Throws<TickPhaseExecutionException>(() =>
            pipeline.Execute(world, context));

        Assert.Equal(TickPhase.IntrinsicDeath, exception.Phase);
        Assert.Equal(TickFailureStage.Preflight, exception.Stage);
        Assert.Equal(generationBefore, world.CaptureGenerationStamp());
        Assert.Equal(ulong.MaxValue, world.GetOrganism(organismId).BiologicalAgeHours);
    }

    [Fact]
    public void PostMutationFailureFaultsWorkingWorldButPublishesNothing()
    {
        var injector = new OneShotFaultInjector(
            TickPhase.IntrinsicDeath,
            TickFailureStage.CommitAfterFirstMutation);
        var runner = CreateRunnerForTesting(injector, founderCount: 2);
        var before = runner.CaptureSnapshot();
        var organismIds = runner.MutableWorld.GetOrganismIdsInCanonicalOrder();

        var exception = Assert.Throws<WorldTickException>(runner.AdvanceOneTick);

        Assert.Equal(
            nameof(TickFailureStage.CommitAfterFirstMutation),
            exception.Fault.FailureStage);
        Assert.Equal(before, runner.CaptureSnapshot());
        Assert.Null(runner.LastCompletedTickChanges);
        Assert.Equal(1UL, runner.MutableWorld.GetOrganism(organismIds[0]).BiologicalAgeHours);
        Assert.Equal(0UL, runner.MutableWorld.GetOrganism(organismIds[1]).BiologicalAgeHours);
        Assert.True(runner.MutableWorld.IsFaulted);
        Assert.Throws<InvalidOperationException>(runner.CapturePublicationSnapshot);
        Assert.Throws<InvalidOperationException>(runner.CapturePersistenceMetadata);
    }

    [Fact]
    public void FinalValidationFailureDoesNotPublishCommittedPhases()
    {
        var injector = new OneShotFaultInjector(
            TickPhase.Finalization,
            TickFailureStage.FinalValidation);
        var runner = CreateRunnerForTesting(injector, founderCount: 2);
        var before = runner.CaptureSnapshot();
        var organismIds = runner.MutableWorld.GetOrganismIdsInCanonicalOrder();

        var exception = Assert.Throws<WorldTickException>(runner.AdvanceOneTick);

        Assert.Equal(nameof(TickFailureStage.FinalValidation), exception.Fault.FailureStage);
        Assert.Equal(before, runner.CaptureSnapshot());
        Assert.Null(runner.LastCompletedTickChanges);
        Assert.All(
            organismIds,
            organismId => Assert.Equal(
                1UL,
                runner.MutableWorld.GetOrganism(organismId).BiologicalAgeHours));
    }

    [Fact]
    public void ZeroIsNotAValidWorldIdOrFounderCount()
    {
        var rules = CompileWorld();

        Assert.Throws<ArgumentOutOfRangeException>(() => WorldId.From(0));
        Assert.Throws<ArgumentException>(() =>
            WorldRunner.CreateFoundation(default, rules, Seed));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldRunner.CreateFoundation(WorldId.From(1), rules, Seed, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldRunner.CreateFoundation(
                WorldId.From(1),
                rules,
                Seed,
                WorldRunner.MaximumFoundationPopulation + 1));
    }

    private static WorldRunner CreateRunner(WorldId worldId, uint founderCount = 100) =>
        WorldRunner.CreateFoundation(worldId, CompileWorld(), Seed, founderCount);

    private static void AssertJournalsEqual(
        IEnumerable<TickPhaseJournal> expected,
        IEnumerable<TickPhaseJournal> actual)
    {
        var expectedArray = expected.ToArray();
        var actualArray = actual.ToArray();
        Assert.Equal(expectedArray.Length, actualArray.Length);
        for (var index = 0; index < expectedArray.Length; index++)
        {
            var expectedJournal = expectedArray[index];
            var actualJournal = actualArray[index];
            Assert.Equal(expectedJournal.Phase, actualJournal.Phase);
            Assert.Equal(expectedJournal.ExecutionClass, actualJournal.ExecutionClass);
            Assert.Equal(expectedJournal.EvaluatedWorkCount, actualJournal.EvaluatedWorkCount);
            Assert.Equal(
                expectedJournal.Changes.Creates.AsEnumerable(),
                actualJournal.Changes.Creates.AsEnumerable());
            Assert.Equal(
                expectedJournal.Changes.Removes.AsEnumerable(),
                actualJournal.Changes.Removes.AsEnumerable());
            Assert.Equal(
                expectedJournal.Changes.Relocations.AsEnumerable(),
                actualJournal.Changes.Relocations.AsEnumerable());
            Assert.Equal(
                expectedJournal.Changes.DirtyEntities.AsEnumerable(),
                actualJournal.Changes.DirtyEntities.AsEnumerable());
            AssertLedgerEqual(
                expectedJournal.ResourceTransactions,
                actualJournal.ResourceTransactions);
        }
    }

    private static void AssertLedgerEqual(
        IReadOnlyList<ResourceTransaction> expected,
        IReadOnlyList<ResourceTransaction> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].Key, actual[index].Key);
            Assert.Equal(expected[index].Cause, actual[index].Cause);
            Assert.Equal(expected[index].ReactionId, actual[index].ReactionId);
            Assert.Equal(expected[index].ActorId, actual[index].ActorId);
            Assert.Equal(expected[index].TileId, actual[index].TileId);
            Assert.Equal(expected[index].Extent, actual[index].Extent);
            Assert.Equal(
                expected[index].MatterEntries.AsEnumerable(),
                actual[index].MatterEntries.AsEnumerable());
            Assert.Equal(
                expected[index].EnergyEntries.AsEnumerable(),
                actual[index].EnergyEntries.AsEnumerable());
        }
    }

    private static WorldRunner CreateRunnerForTesting(
        ITickFaultInjector faultInjector,
        uint founderCount) =>
        WorldRunner.CreateFoundationForTesting(
            WorldId.From(1),
            CompileWorld(),
            Seed,
            founderCount,
            faultInjector);

    private static CompiledWorldRules CompileWorld(bool twoTiles = false)
    {
        var sourceRules = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(sourceRules.IsSuccess, FormatDiagnostics(sourceRules.Diagnostics));
        var compiledRules = RulePackCompiler.Compile(
            Assert.IsType<AuthoringRulePack>(sourceRules.Pack));
        Assert.True(compiledRules.IsSuccess, FormatDiagnostics(compiledRules.Diagnostics));

        var sourceWorld = WorldPackSourceLoader.Load(new CopiedPackageSource("OfficialWorld"));
        Assert.True(sourceWorld.IsSuccess, FormatDiagnostics(sourceWorld.Diagnostics));
        var authoringWorld = Assert.IsType<AuthoringWorldPack>(sourceWorld.Pack);
        if (twoTiles)
        {
            var profile = Assert.Single(authoringWorld.Profiles);
            var firstTile = Assert.Single(profile.Tiles!);
            authoringWorld = authoringWorld with
            {
                Profiles =
                [
                    profile with
                    {
                        Width = 2,
                        Tiles = [firstTile, firstTile with { X = 1 }],
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

    private sealed class OneShotFaultInjector(
        TickPhase requestedPhase,
        TickFailureStage requestedStage) : ITickFaultInjector
    {
        private bool hasThrown;

        public void ThrowIfRequested(TickPhase phase, TickFailureStage stage)
        {
            if (!hasThrown && phase == requestedPhase && stage == requestedStage)
            {
                hasThrown = true;
                throw new InvalidOperationException("Injected test failure.");
            }
        }
    }
}

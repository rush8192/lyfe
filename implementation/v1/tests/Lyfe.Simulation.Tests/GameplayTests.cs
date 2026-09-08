using Lyfe.Simulation.Core;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class GameplayTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("89abcdef0123456776543210fedcba98");

    [Fact]
    public void SurvivalSetupCreatesTwoIndependentAdjacentRoots()
    {
        var runner = CreateSurvivalRunner();
        var publication = runner.CapturePublicationSnapshot();
        var game = publication.Gameplay;

        Assert.Equal(GameMode.Survival, game.Mode);
        Assert.Equal(GameRunStatus.Active, game.RunStatus);
        Assert.Equal(2, game.Roots.Length);
        Assert.Equal([0U, 1U], game.Roots.Select(root => root.StartingTileId.Value));
        Assert.Equal([100U, 100U], game.Roots.Select(root => root.InitialPopulation));
        Assert.Single(game.Roots, root => root.PlayerSelected);
        Assert.Equal(200, publication.Organisms.Length);
        Assert.Equal(2, publication.Species.Length);
        Assert.Equal([1U, 2U], game.Roots.Select(root => root.FounderGenomeId.Value));
        Assert.Equal([1U, 1U], game.Roots.Select(root => root.FounderAllocationId.Value));
        Assert.All(publication.Species, species => Assert.Null(species.ParentSpeciesId));
        Assert.Equal(
            EvolutionAuthorityKind.Controlled,
            publication.Species.Single(species => species.SpeciesId == game.ControlledSpeciesId)
                .EvolutionAuthority);
        Assert.Equal(
            EvolutionAuthorityKind.Autonomous,
            publication.Species.Single(species => species.SpeciesId != game.ControlledSpeciesId)
                .EvolutionAuthority);
    }

    [Fact]
    public void SurvivalPersistsThePlayerAllocationAndDefaultsTheCompetitorToBalanced()
    {
        var rules = CompileWorld(2);
        var runner = WorldRunner.CreateGame(
            WorldId.From(12),
            rules,
            Seed,
            new GameSetupCommand(
                GameMode.Survival,
                FounderGenomeId.From(1),
                TileId.FromRowMajorIndex(0),
                100,
                FounderGenomeId.From(2),
                TileId.FromRowMajorIndex(1),
                PlayerFounderAllocationId: FounderAllocationId.From(2)));

        var snapshot = runner.CapturePublicationSnapshot();
        Assert.Equal([2U, 1U], snapshot.Gameplay.Roots
            .Select(root => root.FounderAllocationId.Value));
        Assert.Equal(2U, snapshot.Species.Single(species =>
            species.SpeciesId == snapshot.Gameplay.ControlledSpeciesId)
            .FounderAllocationId.Value);
        Assert.Equal(1U, snapshot.Species.Single(species =>
            species.SpeciesId != snapshot.Gameplay.ControlledSpeciesId)
            .FounderAllocationId.Value);
        Assert.Equal([2U, 1U], runner.CapturePersistenceSnapshot().State.Gameplay.Roots
            .Select(root => root.FounderAllocationId));
    }

    [Fact]
    public void SurvivalRejectsTwoRootsWithTheSameOpeningMetabolism()
    {
        var rules = CompileWorld(2);
        var hydrogen = rules.RulePack.FounderPhenotypes.Single(phenotype =>
            phenotype.FounderGenomeId.Value == 1).FounderGenomeId;

        var error = Assert.Throws<ArgumentException>(() => WorldRunner.CreateGame(
            WorldId.From(3),
            rules,
            Seed,
            new GameSetupCommand(
                GameMode.Survival,
                hydrogen,
                TileId.FromRowMajorIndex(0),
                100,
                hydrogen,
                TileId.FromRowMajorIndex(1))));

        Assert.Contains("different opening metabolisms", error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SurvivalRejectsANonBalancedCompetitorAllocation()
    {
        var rules = CompileWorld(2);
        var error = Assert.Throws<ArgumentException>(() => WorldRunner.CreateGame(
            WorldId.From(13),
            rules,
            Seed,
            new GameSetupCommand(
                GameMode.Survival,
                FounderGenomeId.From(1),
                TileId.FromRowMajorIndex(0),
                100,
                FounderGenomeId.From(2),
                TileId.FromRowMajorIndex(1),
                PlayerFounderAllocationId: FounderAllocationId.From(3),
                CompetitorFounderAllocationId: FounderAllocationId.From(2))));

        Assert.Contains("balanced founder allocation", error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SandboxCanLockMutationsAndTransferControlAcrossLivingSpecies()
    {
        var runner = CreateSandboxRunner();
        var ancestor = FundControlledSpecies(runner);
        var result = runner.ApplySpeciation(new SpeciationCommand(
            ancestor.SpeciesId,
            ancestor.EvolutionRevision,
            ancestor.GenomeHash,
            [TraitId.From(3)],
            [TileId.FromRowMajorIndex(0)],
            FollowDescendantIfPermitted: false));
        Assert.True(result.Preview.Accepted);
        var descendant = result.DescendantSpeciesId;
        Assert.Equal(ancestor.SpeciesId, runner.GameState.ControlledSpeciesId);

        var locked = runner.SetSandboxMutationLock(
            descendant,
            true,
            runner.GameState.GameplayRevision);
        Assert.True(locked.Accepted);
        Assert.Contains(descendant, runner.GameState.MutationLockedSpeciesIds);
        Assert.Equal(
            EvolutionAuthorityKind.Locked,
            runner.CapturePublicationSnapshot().Species.Single(species =>
                species.SpeciesId == descendant).EvolutionAuthority);
        var beforeIncome = runner.CapturePersistenceSnapshot().State.Species.Single(species =>
            species.SpeciesId == descendant.Value);
        runner.AdvanceOneTick();
        var afterIncome = runner.CapturePersistenceSnapshot().State.Species.Single(species =>
            species.SpeciesId == descendant.Value);
        Assert.True(
            afterIncome.MutationBalanceQ > beforeIncome.MutationBalanceQ ||
            afterIncome.MutationIncomeRemainderLow != beforeIncome.MutationIncomeRemainderLow ||
            afterIncome.MutationIncomeRemainderHigh != beforeIncome.MutationIncomeRemainderHigh);

        var transferred = runner.TransferSandboxControl(
            descendant,
            runner.GameState.GameplayRevision);
        Assert.True(transferred.Accepted);
        Assert.Equal(descendant, runner.GameState.ControlledSpeciesId);
        Assert.Equal(
            SpeciationFailure.SpeciesLocked,
            runner.PreviewSpeciation(new SpeciationCommand(
                descendant,
                runner.CapturePublicationSnapshot().Species.Single(species =>
                    species.SpeciesId == descendant).EvolutionRevision,
                runner.CapturePublicationSnapshot().Species.Single(species =>
                    species.SpeciesId == descendant).GenomeHash,
                [TraitId.From(4)],
                [TileId.FromRowMajorIndex(0)])).Failure);

        var stale = runner.SetSandboxMutationLock(descendant, false, locked.GameplayRevision);
        Assert.False(stale.Accepted);
        Assert.Equal(GameplayCommandFailure.StaleGameplayRevision, stale.Failure);
        var unlocked = runner.SetSandboxMutationLock(
            descendant,
            false,
            runner.GameState.GameplayRevision);
        Assert.True(unlocked.Accepted);
        Assert.Equal(
            EvolutionAuthorityKind.Controlled,
            runner.CapturePublicationSnapshot().Species.Single(species =>
                species.SpeciesId == descendant).EvolutionAuthority);

        var restored = WorldRunner.Restore(runner.Rules, runner.CapturePersistenceSnapshot());
        AssertGameEquivalent(runner.GameState, restored.GameState);
        Assert.Equal(runner.CaptureSnapshot(), restored.CaptureSnapshot());
    }

    [Fact]
    public void SurvivalAlwaysFollowsThePlayerDescendant()
    {
        var runner = CreateSurvivalRunner();
        var ancestor = FundControlledSpecies(runner);
        var result = runner.ApplySpeciation(new SpeciationCommand(
            ancestor.SpeciesId,
            ancestor.EvolutionRevision,
            ancestor.GenomeHash,
            [TraitId.From(3)],
            [TileId.FromRowMajorIndex(0)],
            FollowDescendantIfPermitted: false));

        Assert.True(result.Preview.Accepted);
        Assert.Equal(result.DescendantSpeciesId, runner.GameState.ControlledSpeciesId);
        var species = runner.CapturePublicationSnapshot().Species;
        Assert.Equal(EvolutionAuthorityKind.Controlled, species.Single(item =>
            item.SpeciesId == result.DescendantSpeciesId).EvolutionAuthority);
        Assert.All(species.Where(item => item.SpeciesId != result.DescendantSpeciesId), item =>
            Assert.Equal(EvolutionAuthorityKind.Autonomous, item.EvolutionAuthority));
    }

    [Fact]
    public void SurvivalLosesWhenControlledBranchDiesEvenIfCompetitorLives()
    {
        var runner = CreateSurvivalRunner();
        RemoveSpeciesPopulation(runner, runner.GameState.ControlledSpeciesId);

        var boundary = runner.AdvanceOneTick().Snapshot;

        Assert.Equal(GameRunStatus.Lost, runner.GameState.RunStatus);
        Assert.Equal(GameLossReason.ControlledSpeciesExtinct, runner.GameState.LossReason);
        Assert.Equal(boundary.CompletedTick, runner.GameState.EndedTick);
        Assert.Contains(runner.CapturePublicationSnapshot().Species, species =>
            species.SpeciesId != runner.GameState.ControlledSpeciesId && species.Population > 0);
        Assert.Throws<InvalidOperationException>(runner.AdvanceOneTick);
        var restored = WorldRunner.Restore(runner.Rules, runner.CapturePersistenceSnapshot());
        AssertGameEquivalent(runner.GameState, restored.GameState);
    }

    [Fact]
    public void SandboxLosesOnlyAfterAllLifeIsExtinct()
    {
        var runner = CreateSandboxRunner();
        var ancestor = FundControlledSpecies(runner);
        var split = runner.ApplySpeciation(new SpeciationCommand(
            ancestor.SpeciesId,
            ancestor.EvolutionRevision,
            ancestor.GenomeHash,
            [TraitId.From(3)],
            [TileId.FromRowMajorIndex(0)],
            FollowDescendantIfPermitted: false));
        Assert.True(split.Preview.Accepted);

        RemoveSpeciesPopulation(runner, ancestor.SpeciesId);
        runner.AdvanceOneTick();
        Assert.Equal(GameRunStatus.Active, runner.GameState.RunStatus);

        RemoveSpeciesPopulation(runner, split.DescendantSpeciesId);
        runner.AdvanceOneTick();
        Assert.Equal(GameRunStatus.Lost, runner.GameState.RunStatus);
        Assert.Equal(GameLossReason.AllLifeExtinct, runner.GameState.LossReason);
    }

    private static Lyfe.Simulation.Publication.PublicationSpecies FundControlledSpecies(
        WorldRunner runner)
    {
        var species = runner.CapturePublicationSnapshot().Species.Single(item =>
            item.SpeciesId == runner.GameState.ControlledSpeciesId);
        runner.SetSpeciesEvolutionForTesting(
            species.SpeciesId,
            200 * MutationIncomeMath.MutationPointScale,
            EvolutionAuthorityKind.Controlled);
        return runner.CapturePublicationSnapshot().Species.Single(item =>
            item.SpeciesId == species.SpeciesId);
    }

    private static void RemoveSpeciesPopulation(WorldRunner runner, SpeciesId speciesId)
    {
        var world = runner.MutableWorld;
        var changes = world.BeginChanges();
        foreach (var organismId in world.GetOrganismIdsInCanonicalOrder()
            .Where(id => world.GetOrganism(id).SpeciesId == speciesId))
        {
            world.RemoveOrganism(organismId, changes);
        }
        world.SealChanges(changes);
    }

    private static void AssertGameEquivalent(GameStateSnapshot expected, GameStateSnapshot actual)
    {
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.RunStatus, actual.RunStatus);
        Assert.Equal(expected.LossReason, actual.LossReason);
        Assert.Equal(expected.ControlledSpeciesId, actual.ControlledSpeciesId);
        Assert.Equal(expected.GameplayRevision, actual.GameplayRevision);
        Assert.Equal(expected.EndedTick, actual.EndedTick);
        Assert.True(expected.Roots.SequenceEqual(actual.Roots));
        Assert.True(expected.MutationLockedSpeciesIds.SequenceEqual(
            actual.MutationLockedSpeciesIds));
    }

    private static WorldRunner CreateSandboxRunner()
    {
        var rules = CompileWorld(1);
        return WorldRunner.CreateFoundation(WorldId.From(1), rules, Seed);
    }

    private static WorldRunner CreateSurvivalRunner()
    {
        var rules = CompileWorld(2);
        var founders = rules.RulePack.FounderPhenotypes
            .OrderBy(phenotype => phenotype.FounderGenomeId.Value)
            .Select(phenotype => phenotype.FounderGenomeId)
            .ToArray();
        return WorldRunner.CreateGame(
            WorldId.From(2),
            rules,
            Seed,
            new GameSetupCommand(
                GameMode.Survival,
                founders[0],
                TileId.FromRowMajorIndex(0),
                100,
                founders[1],
                TileId.FromRowMajorIndex(1)));
    }

    private static CompiledWorldRules CompileWorld(int tileCount)
    {
        var sourceRules = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(sourceRules.IsSuccess, FormatDiagnostics(sourceRules.Diagnostics));
        var compiledRules = RulePackCompiler.Compile(
            Assert.IsType<AuthoringRulePack>(sourceRules.Pack));
        Assert.True(compiledRules.IsSuccess, FormatDiagnostics(compiledRules.Diagnostics));
        var sourceWorld = WorldPackSourceLoader.Load(new CopiedPackageSource("OfficialWorld"));
        Assert.True(sourceWorld.IsSuccess, FormatDiagnostics(sourceWorld.Diagnostics));
        var authoringWorld = Assert.IsType<AuthoringWorldPack>(sourceWorld.Pack);
        if (tileCount > 1)
        {
            var profile = Assert.Single(authoringWorld.Profiles);
            var firstTile = Assert.Single(profile.Tiles!);
            authoringWorld = authoringWorld with
            {
                Profiles =
                [
                    profile with
                    {
                        Width = checked((uint)tileCount),
                        Tiles = Enumerable.Range(0, tileCount)
                            .Select(index => firstTile with { X = index })
                            .ToArray(),
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

    private sealed class CopiedPackageSource(string directoryName) : IContentSource
    {
        private readonly string root = Path.Combine(AppContext.BaseDirectory, directoryName);

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
}

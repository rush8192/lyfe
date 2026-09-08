using System.Collections.Immutable;
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

public sealed class EvolutionTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("0123456789abcdeffedcba9876543210");

    [Fact]
    public void MutationIncomeAccruesInTheSameFixedPointUnitAsTraitPrices()
    {
        var (firstCredit, firstRemainder) = MutationIncomeMath.Accumulate(
            MutationIncomeMath.EffectivePopulationTable,
            100,
            1_000_000,
            1_000_000,
            1,
            0);
        Assert.Equal(133_333, firstCredit);
        Assert.Equal((UInt128)250_000_000_000_000, firstRemainder);

        long balance = 0;
        UInt128 remainder = 0;
        for (var hour = 0; hour < 300; hour++)
        {
            var next = MutationIncomeMath.Accumulate(
                MutationIncomeMath.EffectivePopulationTable,
                100,
                1_000_000,
                1_000_000,
                1,
                remainder);
            balance = checked(balance + next.CreditQ);
            remainder = next.Remainder;
        }
        Assert.Equal(40 * MutationIncomeMath.MutationPointScale, balance);
        Assert.Equal((UInt128)0, remainder);
    }

    [Fact]
    public void BothAuthoritiesAccumulateIncomeFromTheirOwnOpeningOutcomes()
    {
        var rules = CompileWorld(tileCount: 2);
        var founders = rules.RulePack.FounderPhenotypes
            .OrderBy(phenotype => phenotype.FounderGenomeId.Value)
            .Select(phenotype => phenotype.FounderGenomeId)
            .ToArray();
        var runner = WorldRunner.CreateGame(
            WorldId.From(1),
            rules,
            Seed,
            new GameSetupCommand(
                GameMode.Survival,
                founders[0],
                TileId.FromRowMajorIndex(0),
                100,
                founders[1],
                TileId.FromRowMajorIndex(1)));

        runner.AdvanceOneTick();

        var accounts = runner.CapturePersistenceSnapshot().State.Species;
        var first = accounts.Single(item =>
            item.Authority == (byte)EvolutionAuthorityKind.Controlled);
        var second = accounts.Single(item =>
            item.Authority == (byte)EvolutionAuthorityKind.Autonomous);
        Assert.True(first.AverageHealthQ > 0);
        Assert.True(second.AverageHealthQ > first.AverageHealthQ);
        Assert.True(first.MutationBalanceQ > 0);
        Assert.True(second.MutationBalanceQ > first.MutationBalanceQ);
    }

    [Fact]
    public void EffectivePopulationTableHasFrozenCompilerIdentityAndLandmarks()
    {
        var table = CompileWorld().RulePack.EffectivePopulationQ;

        Assert.Equal(100_001, table.Length);
        Assert.Equal(0, table[0]);
        Assert.Equal(1_435_529, table[1]);
        Assert.Equal(100_000_000, table[100]);
        Assert.Equal(345_943_162, table[1_000]);
        Assert.Equal(996_722_626, table[100_000]);
        Assert.Equal(
            "df4408bb4da12b2a5b4ea312c6bcc74258238732cb17ae49ef8b73dfb42bbf34",
            MutationIncomeMath.EffectivePopulationTableSha256);
    }

    [Fact]
    public void TraitGraphClosesPrerequisitesAndScoresCurrentResourcePressure()
    {
        var rules = CompileWorld().RulePack;
        var founder = rules.FounderPhenotypes.Single(phenotype =>
            phenotype.FounderGenomeId.Value == 1);

        var scores = AutonomousEvolutionScorer.ScoreReachable(
            rules,
            founder.AcquiredTraits,
            new EvolutionPressureState(800_000, 900_000),
            200 * MutationIncomeMath.MutationPointScale,
            founder.MaximumChangeComplexity);

        Assert.Equal(3, scores.Length);
        var conservation = scores.Single(score => score.TraitIds[^1] == TraitId.From(4));
        Assert.Equal([3U, 4U], conservation.TraitIds.Select(id => id.Value));
        Assert.Equal(100 * MutationIncomeMath.MutationPointScale, conservation.MutationPriceQ);
        Assert.Equal(2U, conservation.ChangeComplexity);
        Assert.Equal(900_000U, conservation.PressureMatchQ);
    }

    [Fact]
    public void SpeciationForksDeterministicFoundersAndPreservesPhysicalStateAndUnusedPoints()
    {
        var first = CreateRunnerWithAllocation(FounderAllocationId.From(2));
        var second = CreateRunnerWithAllocation(FounderAllocationId.From(2));
        var firstResult = Speciate(first);
        var secondResult = Speciate(second);

        Assert.True(firstResult.Preview.Accepted);
        Assert.Equal(50U, Assert.Single(firstResult.Preview.FounderCounts).Count);
        Assert.Equal(firstResult.Preview.Accepted, secondResult.Preview.Accepted);
        Assert.Equal(firstResult.Preview.Failure, secondResult.Preview.Failure);
        Assert.Equal(firstResult.Preview.MutationPriceQ, secondResult.Preview.MutationPriceQ);
        Assert.Equal(firstResult.Preview.ChangeComplexity, secondResult.Preview.ChangeComplexity);
        Assert.Equal(firstResult.Preview.FounderCounts, secondResult.Preview.FounderCounts);
        Assert.Equal(firstResult.Preview.ProposedGenomeHash, secondResult.Preview.ProposedGenomeHash);
        Assert.Equal(firstResult.StateHash, secondResult.StateHash);

        var publication = first.CapturePublicationSnapshot();
        Assert.Equal(2, publication.Species.Length);
        Assert.Equal([50UL, 50UL], publication.Species.Select(species => species.Population));
        Assert.All(publication.Species, species =>
            Assert.Equal(100 * MutationIncomeMath.MutationPointScale, species.MutationBalanceQ));
        var ancestor = publication.Species[0];
        var descendant = publication.Species[1];
        Assert.Equal(EvolutionAuthorityKind.Autonomous, ancestor.EvolutionAuthority);
        Assert.Equal(EvolutionAuthorityKind.Controlled, descendant.EvolutionAuthority);
        Assert.Equal(ancestor.SpeciesId, descendant.ParentSpeciesId);
        Assert.All(publication.Species, species =>
            Assert.Equal(FounderAllocationId.From(2), species.FounderAllocationId));
        Assert.Equal([1U, 2U, 3U, 4U, 5U], descendant.AcquiredTraits.Select(id => id.Value));
        Assert.True(first.MutableWorld.GetCompiledPhenotype(descendant.SpeciesId)
            .Physiology.Behavior.ResourceConservation);

        var persisted = first.CapturePersistenceSnapshot();
        Assert.All(persisted.State.Genomes, genome =>
            Assert.Equal(2U, genome.FounderAllocationId));
        var record = Assert.Single(persisted.State.SpeciationEvents);
        Assert.Equal(64, record.FounderSelectionDigest.Length);
        Assert.Equal(100 * MutationIncomeMath.MutationPointScale, record.DuplicatedBalanceAfterQ);
        Assert.Equal(100, persisted.State.Organisms.Length);
        Assert.Equal(100_000L, persisted.State.Organisms.Sum(value => value.StructuralMatterQ));
        Assert.Equal(500_000L, persisted.State.Organisms.Sum(value => value.ChargedReserveQ));

        var restored = WorldRunner.Restore(CompileWorld(), persisted);
        Assert.Equal(first.CaptureSnapshot(), restored.CaptureSnapshot());
        Assert.Equal(
            first.AdvanceOneTick().Snapshot,
            restored.AdvanceOneTick().Snapshot);
    }

    [Fact]
    public void SpeciationValidationRejectsMissingPrerequisitesAndImmediateRefork()
    {
        var runner = CreateRunner();
        var species = Fund(runner);
        var missing = runner.PreviewSpeciation(new SpeciationCommand(
            species.SpeciesId,
            species.EvolutionRevision,
            species.GenomeHash,
            [TraitId.From(4)],
            [TileId.FromRowMajorIndex(0)]));
        Assert.Equal(SpeciationFailure.MissingPrerequisite, missing.Failure);

        var accepted = Speciate(runner);
        var descendant = runner.CapturePublicationSnapshot().Species[1];
        var cooldown = runner.PreviewSpeciation(new SpeciationCommand(
            descendant.SpeciesId,
            descendant.EvolutionRevision,
            descendant.GenomeHash,
            [TraitId.From(3)],
            [TileId.FromRowMajorIndex(0)]));
        Assert.Equal(SpeciationFailure.SpeciationCooldown, cooldown.Failure);
        Assert.True(accepted.Preview.Accepted);
    }

    [Fact]
    public void InsufficientBalancePreviewRetainsACompletePlanningResult()
    {
        var runner = CreateRunner();
        var species = Assert.Single(runner.CapturePublicationSnapshot().Species);

        var preview = runner.PreviewSpeciation(new SpeciationCommand(
            species.SpeciesId,
            species.EvolutionRevision,
            species.GenomeHash,
            [TraitId.From(3)],
            [TileId.FromRowMajorIndex(0)]));

        Assert.False(preview.Accepted);
        Assert.Equal(SpeciationFailure.InsufficientMutationPoints, preview.Failure);
        Assert.Equal(40 * MutationIncomeMath.MutationPointScale, preview.MutationPriceQ);
        Assert.Equal(1U, preview.ChangeComplexity);
        Assert.Equal(50U, Assert.Single(preview.FounderCounts).Count);
        Assert.Equal(64, preview.ProposedGenomeHash.Length);
    }

    [Theory]
    [InlineData(1, 12)]
    [InlineData(2, 5)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    public void FounderFractionsApplyPerSelectedOccupiedTile(
        int selectedTileCount,
        uint expectedFoundersPerTile)
    {
        var runner = CreateRunner(tileCount: 4);
        var world = runner.MutableWorld;
        var organismIds = world.GetOrganismIdsInCanonicalOrder();
        var movement = world.BeginChanges();
        for (var index = 0; index < organismIds.Length; index++)
        {
            var tileIndex = checked((uint)(index / 25));
            var organism = world.GetOrganism(organismIds[index]);
            world.RelocateOrganism(
                organism.Id,
                TileId.FromRowMajorIndex(tileIndex),
                organism.PositionXQ,
                organism.PositionYQ,
                movement);
        }
        world.SealChanges(movement);

        var species = Fund(runner);
        var selectedTiles = Enumerable.Range(0, selectedTileCount)
            .Select(index => TileId.FromRowMajorIndex(checked((uint)index)))
            .ToImmutableArray();
        var preview = runner.PreviewSpeciation(new SpeciationCommand(
            species.SpeciesId,
            species.EvolutionRevision,
            species.GenomeHash,
            [TraitId.From(3)],
            selectedTiles));

        Assert.True(preview.Accepted);
        Assert.Equal(selectedTileCount, preview.FounderCounts.Length);
        Assert.All(preview.FounderCounts, count =>
            Assert.Equal(expectedFoundersPerTile, count.Count));
    }

    private static SpeciationResult Speciate(WorldRunner runner)
    {
        var species = Fund(runner);
        return runner.ApplySpeciation(new SpeciationCommand(
            species.SpeciesId,
            species.EvolutionRevision,
            species.GenomeHash,
            [TraitId.From(3), TraitId.From(4)],
            [TileId.FromRowMajorIndex(0)]));
    }

    private static Lyfe.Simulation.Publication.PublicationSpecies Fund(WorldRunner runner)
    {
        var species = Assert.Single(runner.CapturePublicationSnapshot().Species);
        runner.SetSpeciesEvolutionForTesting(
            species.SpeciesId,
            200 * MutationIncomeMath.MutationPointScale,
            EvolutionAuthorityKind.Controlled);
        return Assert.Single(runner.CapturePublicationSnapshot().Species);
    }

    private static WorldRunner CreateRunner(int tileCount = 1) => WorldRunner.CreateFoundation(
        WorldId.From(1), CompileWorld(tileCount), Seed);

    private static WorldRunner CreateRunnerWithAllocation(FounderAllocationId allocation) =>
        WorldRunner.CreateGame(
            WorldId.From(1),
            CompileWorld(),
            Seed,
            new GameSetupCommand(
                GameMode.FreeSandbox,
                FounderGenomeId.From(1),
                TileId.FromRowMajorIndex(0),
                100,
                PlayerFounderAllocationId: allocation));

    private static CompiledWorldRules CompileWorld(int tileCount = 1)
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
}

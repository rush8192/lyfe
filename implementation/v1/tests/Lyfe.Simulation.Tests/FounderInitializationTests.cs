using Lyfe.Simulation.Core;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World;
using Lyfe.Simulation.World.Generation;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class FounderInitializationTests
{
    private const uint FounderPopulation = 100;

    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("0123456789abcdeffedcba9876543210");

    [Fact]
    public void PlayableSetupAppliesDeterministicScenarioSpreadToBothRoots()
    {
        var rules = CompileGeneratedWorld();
        var setup = CreateSurvivalSetup(rules, FounderInitializationMode.ScenarioDefault);
        var first = WorldRunner.CreateGame(WorldId.From(41), rules, Seed, setup);
        var second = WorldRunner.CreateGame(WorldId.From(41), rules, Seed, setup);

        Assert.Equal(first.CaptureSnapshot(), second.CaptureSnapshot());
        Assert.Equal(
            FounderSignatures(first.CapturePublicationSnapshot()),
            FounderSignatures(second.CapturePublicationSnapshot()));

        var publication = first.CapturePublicationSnapshot();
        var profile = rules.RulePack.Scenarios[rules.Scenario.DenseSlot].FounderInitialization;
        foreach (var root in publication.Gameplay.Roots)
        {
            var cohort = publication.Organisms
                .Where(organism => organism.SpeciesId == root.SpeciesId)
                .ToArray();
            Assert.Equal(checked((int)FounderPopulation), cohort.Length);
            Assert.All(cohort, organism =>
            {
                Assert.InRange(
                    organism.BiologicalAgeHours,
                    profile.BiologicalAgeMinimumHours,
                    profile.BiologicalAgeMaximumHours);
                Assert.InRange(
                    organism.ReproductionNotBeforeTick,
                    profile.ReproductionReadinessMinimumHours,
                    profile.ReproductionReadinessMaximumHours);
            });
            Assert.True(cohort.Select(value => value.BiologicalAgeHours).Distinct().Count() > 50);
            Assert.True(cohort.Select(value => value.ReproductionNotBeforeTick).Distinct().Count() > 50);
        }
    }

    [Fact]
    public void FixtureModesPreserveLegacyAndTrueZeroSpreadWithoutChangingMatter()
    {
        var rules = CompileGeneratedWorld();
        var legacy = WorldRunner.CreateFoundation(
            WorldId.From(42),
            rules,
            Seed,
            32);
        var zero = WorldRunner.CreateFoundation(
            WorldId.From(42),
            rules,
            Seed,
            32,
            FounderInitializationMode.ZeroSpreadFixture);
        var scenario = WorldRunner.CreateFoundation(
            WorldId.From(42),
            rules,
            Seed,
            32,
            FounderInitializationMode.ScenarioDefault);

        Assert.All(legacy.CapturePublicationSnapshot().Organisms, organism =>
        {
            Assert.Equal(0UL, organism.BiologicalAgeHours);
            Assert.InRange(organism.ReproductionNotBeforeTick, 24UL, 27UL);
        });
        Assert.All(zero.CapturePublicationSnapshot().Organisms, organism =>
        {
            Assert.Equal(0UL, organism.BiologicalAgeHours);
            Assert.Equal(24UL, organism.ReproductionNotBeforeTick);
        });
        Assert.Contains(scenario.CapturePublicationSnapshot().Organisms,
            organism => organism.BiologicalAgeHours > 0);

        Assert.Equal(
            zero.CapturePersistenceSnapshot().State.TileResources.AsEnumerable(),
            scenario.CapturePersistenceSnapshot().State.TileResources.AsEnumerable());
        var zeroOrganisms = zero.CapturePublicationSnapshot().Organisms;
        var scenarioOrganisms = scenario.CapturePublicationSnapshot().Organisms;
        Assert.Equal(zeroOrganisms.Length, scenarioOrganisms.Length);
        for (var index = 0; index < zeroOrganisms.Length; index++)
        {
            Assert.Equal(zeroOrganisms[index].OrganismId, scenarioOrganisms[index].OrganismId);
            Assert.Equal(zeroOrganisms[index].StructuralMatterQ,
                scenarioOrganisms[index].StructuralMatterQ);
            Assert.Equal(zeroOrganisms[index].ChargedReserveQ,
                scenarioOrganisms[index].ChargedReserveQ);
            Assert.Equal(
                zeroOrganisms[index].CommittedMicronutrients.AsEnumerable(),
                scenarioOrganisms[index].CommittedMicronutrients.AsEnumerable());
            Assert.Equal(
                zeroOrganisms[index].FreeMicronutrients.AsEnumerable(),
                scenarioOrganisms[index].FreeMicronutrients.AsEnumerable());
        }
    }

    [Fact]
    public void ScenarioSpreadSurvivesSaveAndContinuesExactly()
    {
        var rules = CompileGeneratedWorld();
        var runner = WorldRunner.CreateGame(
            WorldId.From(43),
            rules,
            Seed,
            CreateSurvivalSetup(rules, FounderInitializationMode.ScenarioDefault));

        var restored = WorldRunner.Restore(rules, runner.CapturePersistenceSnapshot());

        Assert.Equal(runner.CaptureSnapshot(), restored.CaptureSnapshot());
        Assert.Equal(
            FounderSignatures(runner.CapturePublicationSnapshot()),
            FounderSignatures(restored.CapturePublicationSnapshot()));
        Assert.Equal(runner.AdvanceOneTick().Snapshot, restored.AdvanceOneTick().Snapshot);
    }

    [Fact]
    public void PlayableFounderCohortsProduceFirstBirthsAcrossMultipleTicks()
    {
        var rules = CompileGeneratedWorld();
        var runner = WorldRunner.CreateGame(
            WorldId.From(44),
            rules,
            Seed,
            CreateSurvivalSetup(rules, FounderInitializationMode.ScenarioDefault));

        for (var hour = 0; hour < 420; hour++)
        {
            runner.AdvanceOneTick();
        }

        var publication = runner.CapturePublicationSnapshot();
        var reproductionEvents = publication.JourneyEvents
            .Where(value => value.Family == OrganismJourneyEventFamily.Reproduction)
            .ToArray();
        foreach (var root in publication.Gameplay.Roots)
        {
            var ticks = reproductionEvents
                .Where(value => value.SubjectSpeciesId == root.SpeciesId &&
                    value.SubjectOrganismId.Value <= FounderPopulation * 2UL)
                .Select(value => value.Tick)
                .Distinct()
                .Order()
                .ToArray();
            Assert.True(ticks.Length >= 10,
                $"Founder genome {root.FounderGenomeId.Value} reproduced across only {ticks.Length} ticks.");
            if (root.FounderGenomeId.Value == 1)
            {
                Assert.InRange(ticks[0], 300UL, 420UL);
            }
            else
            {
                Assert.InRange(ticks[0], 250UL, 275UL);
            }
        }
    }

    private static (ulong Id, ulong Species, ulong Age, ulong ReadyAt, uint X, uint Y)[]
        FounderSignatures(WorldPublicationSnapshot publication) => publication.Organisms
            .Select(organism => (
                organism.OrganismId.Value,
                organism.SpeciesId.Value,
                organism.BiologicalAgeHours,
                organism.ReproductionNotBeforeTick,
                organism.PositionXQ,
                organism.PositionYQ))
            .ToArray();

    private static GameSetupCommand CreateSurvivalSetup(
        CompiledWorldRules rules,
        FounderInitializationMode mode)
    {
        var generated = DeterministicWorldGenerator.Generate(rules.WorldProfile, Seed);
        var pair = generated.StartingPairs[0];
        return new GameSetupCommand(
            GameMode.Survival,
            FounderGenomeId.From(1),
            TileId.FromRowMajorIndex(pair.HydrogenTileIndex),
            FounderPopulation,
            FounderGenomeId.From(2),
            TileId.FromRowMajorIndex(pair.SulfurTileIndex),
            FounderInitialization: mode);
    }

    private static CompiledWorldRules CompileGeneratedWorld()
    {
        var result = WorldRulesPipeline.Compile(
            new CopiedPackageSource("OfficialRules"),
            new CopiedPackageSource("OfficialGeneratedWorld"),
            "scenario.foundation-sandbox",
            "world.primordial-earth-v1");
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine,
            result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.IsType<CompiledWorldRules>(result.WorldRules);
    }

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

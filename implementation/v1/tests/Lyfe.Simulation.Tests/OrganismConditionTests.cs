using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World.Generation;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class OrganismConditionTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("0123456789abcdeffedcba9876543210");

    [Fact]
    public void FounderConditionHasOneExactFixedPointExplanation()
    {
        var physiology = CompileRules().FounderPhenotypes[0].Physiology;

        var condition = OrganismConditionBuilder.Build(
            physiology,
            new OrganismConditionInput(1_000, 5_000, 0),
            new OrganismEnvironmentInput(45_000),
            0,
            ConditionSnapshotKind.End);

        Assert.Equal(500_000U, condition.ReserveFactorQ);
        Assert.Equal(RatioQ.Scale, condition.StructureFactorQ);
        Assert.Equal(RatioQ.Scale, condition.NutrientFactorQ);
        Assert.Equal(RatioQ.Scale, condition.AgeFactorQ);
        Assert.Equal(RatioQ.Scale, condition.LifecycleFactorQ);
        Assert.Equal(RatioQ.Scale, condition.EnvironmentalFactorQ);
        Assert.Equal(500_000U, condition.RelativeHealthQ);
        Assert.Equal(0U, condition.TemperatureSeverityQ);
    }

    [Fact]
    public void AgeAndTemperatureCurvesMatchCalibratedLandmarks()
    {
        var physiology = CompileRules().FounderPhenotypes[0].Physiology;

        var aged = OrganismConditionBuilder.Build(
            physiology,
            new OrganismConditionInput(1_000, 10_000, 888),
            new OrganismEnvironmentInput(45_000),
            1,
            ConditionSnapshotKind.End);
        var atUpperHard = OrganismConditionBuilder.Build(
            physiology,
            new OrganismConditionInput(1_000, 10_000, 0),
            new OrganismEnvironmentInput(65_000),
            1,
            ConditionSnapshotKind.End);
        var beyondUpperHard = OrganismConditionBuilder.Build(
            physiology,
            new OrganismConditionInput(1_000, 10_000, 0),
            new OrganismEnvironmentInput(80_000),
            1,
            ConditionSnapshotKind.End);

        Assert.Equal(972_778U, aged.AgeFactorQ);
        Assert.Equal(800_000U, atUpperHard.EnvironmentalFactorQ);
        Assert.Equal(200_000U, beyondUpperHard.EnvironmentalFactorQ);
    }

    [Fact]
    public void FounderChemicalToleranceCreatesDistinctSulfurTradeoff()
    {
        var rules = CompileRules();
        var hydrogen = rules.FounderPhenotypes.Single(value =>
            value.FounderGenomeId.Value == 1).Physiology;
        var sulfur = rules.FounderPhenotypes.Single(value =>
            value.FounderGenomeId.Value == 2).Physiology;
        var environment = new OrganismEnvironmentInput(45_000, 20_000_000, 5_000_000);

        var hydrogenCondition = OrganismConditionBuilder.Build(
            hydrogen,
            new OrganismConditionInput(1_000, 5_000, 0),
            environment,
            0,
            ConditionSnapshotKind.End);
        var sulfurCondition = OrganismConditionBuilder.Build(
            sulfur,
            new OrganismConditionInput(1_000, 5_000, 0),
            environment,
            0,
            ConditionSnapshotKind.End);

        Assert.Equal(966_667U, hydrogenCondition.EnvironmentalFactorQ);
        Assert.Equal(483_334U, hydrogenCondition.RelativeHealthQ);
        Assert.Equal(966_667U, sulfurCondition.EnvironmentalFactorQ);
        Assert.Equal(483_334U, sulfurCondition.RelativeHealthQ);
        Assert.Equal(20_000_000, hydrogen.ChemicalResponse.HydrogenSulfideSoftThresholdQ);
        Assert.Equal(50_000_000, sulfur.ChemicalResponse.HydrogenSulfideSoftThresholdQ);
    }

    [Fact]
    public void LethalChemicalAssessmentRecordsEveryNonzeroIndependentCause()
    {
        var physiology = CompileRules().FounderPhenotypes[0].Physiology;

        var evidence = IntrinsicDeathEvaluator.Evaluate(
            OrganismId.FromAllocatedValue(17),
            9,
            physiology,
            new OrganismConditionInput(1_000, 5_000, 0),
            new OrganismEnvironmentInput(45_000, 200_000_000, 50_000_000),
            new SemanticRandomOracle(Seed));

        Assert.Equal(
            [
                IntrinsicDeathCause.HydrogenSulfideExposure,
                IntrinsicDeathCause.SulfurDioxideExposure,
            ],
            evidence.NonzeroCauses.Select(value => value.Cause));
        Assert.Equal(62_500U, evidence.NonzeroCauses[0].ProbabilityQ);
        Assert.Equal(250_000U, evidence.NonzeroCauses[1].ProbabilityQ);
        Assert.All(evidence.NonzeroCauses, value => Assert.NotNull(value.RandomWord));
    }

    [Fact]
    public void DeathEvidenceRetainsEveryNonzeroCauseAndOneCanonicalActualCause()
    {
        var physiology = CompileRules().FounderPhenotypes[0].Physiology;

        var evidence = IntrinsicDeathEvaluator.Evaluate(
            OrganismId.FromAllocatedValue(7),
            31,
            physiology,
            new OrganismConditionInput(499, 0, 720),
            new OrganismEnvironmentInput(80_000),
            new SemanticRandomOracle(Seed));

        Assert.Equal(
            [
                IntrinsicDeathCause.ReserveExhaustion,
                IntrinsicDeathCause.StructuralFailure,
                IntrinsicDeathCause.Senescence,
                IntrinsicDeathCause.TemperatureExposure,
            ],
            evidence.NonzeroCauses.Select(cause => cause.Cause));
        Assert.Equal(IntrinsicDeathCause.ReserveExhaustion, evidence.ActualCause);
        Assert.Equal(RatioQ.Scale, evidence.NonzeroCauses[0].ProbabilityQ);
        Assert.Null(evidence.NonzeroCauses[0].RandomWord);
        Assert.Equal(100U, evidence.NonzeroCauses[2].ProbabilityQ);
        Assert.NotNull(evidence.NonzeroCauses[2].RandomWord);
        Assert.Equal(4_000U, evidence.NonzeroCauses[3].ProbabilityQ);
        Assert.NotNull(evidence.NonzeroCauses[3].RandomWord);
    }

    [Fact]
    public void StorageCapacityIsCompiledAndCreatesNoStoredMatter()
    {
        var physiology = CompileRules().FounderPhenotypes[0].Physiology;

        var capacity = OrganismConditionBuilder.GetStorageCapacity(physiology);

        Assert.Equal(10_000, capacity.ChargedReserveCapacityQ);
        Assert.Equal(512, capacity.DissolvedMacronutrientCapacityLoadQ);
        Assert.Equal(64, capacity.FreeMicronutrientCapacityLoadQ);
        Assert.Equal(0, capacity.IngestedMatterCapacityLoadQ);
        Assert.Equal(ProcessAllocationPolicy.EqualShared, capacity.AllocationPolicy);
    }

    [Fact]
    public void GeneratedStartingTilesAreNeutralForFounderTemperatureHealth()
    {
        var rulePack = CompileRules();
        var worldSource = WorldPackSourceLoader.Load(
            new CopiedPackageSource("OfficialGeneratedWorld"));
        Assert.True(worldSource.IsSuccess, FormatDiagnostics(worldSource.Diagnostics));
        var compiledWorld = WorldRulesCompiler.Compile(
            rulePack,
            Assert.IsType<AuthoringWorldPack>(worldSource.Pack),
            "scenario.foundation-sandbox",
            "world.primordial-earth-v1");
        Assert.True(compiledWorld.IsSuccess, FormatDiagnostics(compiledWorld.Diagnostics));
        var worldRules = Assert.IsType<CompiledWorldRules>(compiledWorld.WorldRules);
        var generated = DeterministicWorldGenerator.Generate(worldRules.WorldProfile, Seed);
        var physiology = rulePack.FounderPhenotypes[0].Physiology;

        foreach (var tileIndex in generated.StartingPairs.SelectMany(pair =>
                     new[] { pair.HydrogenTileIndex, pair.SulfurTileIndex }))
        {
            var tile = generated.GetTile(tileIndex);
            var climate = WorldClimateEvaluator.Evaluate(generated, tile, 0);
            var condition = OrganismConditionBuilder.Build(
                physiology,
                new OrganismConditionInput(1_000, 5_000, 0),
                new OrganismEnvironmentInput(climate.TemperatureMilliC),
                0,
                ConditionSnapshotKind.End);

            Assert.InRange(climate.TemperatureMilliC, 40_000, 50_000);
            Assert.Equal(RatioQ.Scale, condition.EnvironmentalFactorQ);
            Assert.Equal(500_000U, condition.RelativeHealthQ);
        }
    }

    private static CompiledRulePack CompileRules()
    {
        var source = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(source.IsSuccess, FormatDiagnostics(source.Diagnostics));
        var compiled = RulePackCompiler.Compile(Assert.IsType<AuthoringRulePack>(source.Pack));
        Assert.True(compiled.IsSuccess, FormatDiagnostics(compiled.Diagnostics));
        return Assert.IsType<CompiledRulePack>(compiled.RulePack);
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

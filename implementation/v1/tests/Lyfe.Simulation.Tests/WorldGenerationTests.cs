using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.World.Generation;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class WorldGenerationTests
{
    [Fact]
    public void OfficialGeneratedProfileLoadsCompilesAndGeneratesDeterministically()
    {
        var rules = CompileGeneratedWorldRules();
        var profile = rules.WorldProfile;
        var seed = RootRandomSeed.Parse("00112233445566778899aabbccddeeff");

        var first = DeterministicWorldGenerator.Generate(profile, seed);
        var second = DeterministicWorldGenerator.Generate(profile, seed);

        Assert.Equal("world.primordial-earth-v1", first.WorldProfileKey);
        Assert.Equal(32U, first.Width);
        Assert.Equal(17U, first.Height);
        Assert.True(first.WrapX);
        Assert.False(first.WrapY);
        Assert.Equal(WorldSignature(first), WorldSignature(second));
        Assert.Equal(
            "b11ecae2733e1260c101c4e76dc04c9319f6601004524f14aa9ee9185230e4bd",
            rules.Identity.WorldPackageHash);
        Assert.Equal(
            "91d401c9422c5f75d305ca9ec3731f10ad33408d462e1bbce6e7669b768fb6e7",
            rules.Identity.CompiledWorldProfileHash);
        Assert.Equal(
            "7d25d3a6ead85f55a293d6f5667f3c2809a9cf335ec16f5782cbdefa55e28293",
            rules.Identity.WorldRulesHash);
    }

    [Theory]
    [InlineData("00000000000000000000000000000001")]
    [InlineData("00000000000000000000000000000002")]
    [InlineData("0123456789abcdeffedcba9876543210")]
    [InlineData("fedcba98765432100123456789abcdef")]
    public void FixedSeedsProduceValidMapsAndFourComplementaryStartPairs(string seedText)
    {
        var profile = CompileGeneratedProfile();
        var world = DeterministicWorldGenerator.Generate(profile, RootRandomSeed.Parse(seedText));
        var generator = Assert.IsType<CompiledWorldGenerator>(profile.Generator);

        Assert.Equal(544, world.Tiles.Length);
        Assert.Equal(4, world.StartingPairs.Length);
        Assert.Equal(8, world.Repairs.Length);
        Assert.Equal(world.Tiles.Length, world.Tiles.Select(tile => tile.TileIndex).Distinct().Count());
        Assert.All(world.Tiles, tile =>
        {
            var coordinates = WorldGridTopology.ToCoordinates(world.Width, world.Height, tile.TileIndex);
            Assert.Equal((tile.X, tile.Y), coordinates);
            Assert.NotEqual(0, tile.ElevationMeters);
            Assert.InRange(tile.ElevationMeters, -6_000, 4_000);
            Assert.Equal(12, tile.MonthlyTemperatureMilliC.Length);
            Assert.Equal(12, tile.MonthlyPrecipitationMicrometersPerHour.Length);
            Assert.Equal(12, tile.MonthlyCloudQ.Length);
        });

        var aquaticCount = world.Tiles.Count(tile => tile.IsAquatic);
        var aquaticFractionQ = checked((uint)((long)aquaticCount * 1_000_000 / world.Tiles.Length));
        Assert.InRange(
            aquaticFractionQ,
            generator.TargetAquaticFractionMinimumQ - 1_000,
            generator.TargetAquaticFractionMaximumQ + 1_000);
        var volcanicFractionQ = checked((uint)((long)world.Tiles.Count(tile =>
            tile.BaselineVolcanismQ >= generator.Volcanism.TileThresholdQ) *
            1_000_000 / world.Tiles.Length));
        Assert.InRange(
            volcanicFractionQ,
            generator.Volcanism.TileFractionMinimumQ,
            generator.Volcanism.TileFractionMaximumQ);

        var used = new HashSet<uint>();
        foreach (var pair in world.StartingPairs)
        {
            Assert.InRange(pair.RepairCostQ, 0U, generator.StartingRegions.MaximumTotalRepairCostQ);
            Assert.True(used.Add(pair.HydrogenTileIndex));
            Assert.True(used.Add(pair.SulfurTileIndex));
            Assert.Equal(
                1,
                WorldGridTopology.CylindricalManhattanDistance(
                    world.Width,
                    world.Height,
                    pair.HydrogenTileIndex,
                    pair.SulfurTileIndex));
            var hydrogen = world.GetTile(pair.HydrogenTileIndex);
            var sulfur = world.GetTile(pair.SulfurTileIndex);
            Assert.Equal(StartingTileRole.Hydrogen, hydrogen.StartingRole);
            Assert.Equal(StartingTileRole.Sulfur, sulfur.StartingRole);
            Assert.Equal(200, hydrogen.WaterDepthMeters);
            Assert.Equal(20, sulfur.WaterDepthMeters);
            Assert.Equal(800_000U, hydrogen.BaselineVolcanismQ);
            Assert.Equal(800_000U, sulfur.BaselineVolcanismQ);
            Assert.InRange(hydrogen.AnnualMeanTemperatureMilliC, 38_000, 50_000);
            Assert.InRange(sulfur.AnnualMeanTemperatureMilliC, 38_000, 50_000);
            Assert.InRange(
                WorldClimateEvaluator.Evaluate(world, hydrogen, 0).TemperatureMilliC,
                40_000,
                50_000);
            Assert.InRange(
                WorldClimateEvaluator.Evaluate(world, sulfur, 0).TemperatureMilliC,
                40_000,
                50_000);
            Assert.InRange(
                WorldClimateEvaluator.EvaluateDailyAccessibleLightQ(world, hydrogen, 0),
                0,
                2_016_000);
            Assert.InRange(
                WorldClimateEvaluator.EvaluateDailyAccessibleLightQ(world, sulfur, 0),
                5_185_000,
                6_337_000);

            var hydrogenRepair = Assert.Single(world.Repairs.Where(repair =>
                repair.TileIndex == pair.HydrogenTileIndex));
            var sulfurRepair = Assert.Single(world.Repairs.Where(repair =>
                repair.TileIndex == pair.SulfurTileIndex));
            Assert.InRange(
                Math.Abs(hydrogenRepair.FinalElevationMeters - hydrogenRepair.OriginalElevationMeters),
                0,
                generator.StartingRegions.HydrogenMaximumDepthRepairMeters);
            Assert.InRange(
                Math.Abs(sulfurRepair.FinalElevationMeters - sulfurRepair.OriginalElevationMeters),
                0,
                generator.StartingRegions.SulfurMaximumDepthRepairMeters);
            Assert.InRange(
                AbsoluteDifference(hydrogenRepair.FinalVolcanismQ, hydrogenRepair.OriginalVolcanismQ),
                0U,
                generator.StartingRegions.MaximumVolcanismRepairQ);
            Assert.InRange(
                AbsoluteDifference(sulfurRepair.FinalVolcanismQ, sulfurRepair.OriginalVolcanismQ),
                0U,
                generator.StartingRegions.MaximumVolcanismRepairQ);
        }

        foreach (var first in world.StartingPairs)
        {
            foreach (var second in world.StartingPairs)
            {
                if (first == second)
                {
                    continue;
                }

                Assert.True(
                    WorldGridTopology.CylindricalManhattanDistance(
                        world.Width,
                        world.Height,
                        first.HydrogenTileIndex,
                        second.HydrogenTileIndex) >= 4);
            }
        }
    }

    [Fact]
    public void DifferentSeedsChangeGeneratedWorld()
    {
        var profile = CompileGeneratedProfile();
        var first = DeterministicWorldGenerator.Generate(
            profile,
            RootRandomSeed.Parse("00000000000000000000000000000001"));
        var second = DeterministicWorldGenerator.Generate(
            profile,
            RootRandomSeed.Parse("00000000000000000000000000000002"));

        Assert.NotEqual(WorldSignature(first), WorldSignature(second));
    }

    [Fact]
    public void RepresentativeSeedMatrixAlwaysFindsBoundedFoundingPairs()
    {
        var profile = CompileGeneratedProfile();
        for (var seedOrdinal = 0UL; seedOrdinal < 32; seedOrdinal++)
        {
            var world = DeterministicWorldGenerator.Generate(
                profile,
                new RootRandomSeed(seedOrdinal, 0x9e3779b97f4a7c15));
            Assert.Equal(4, world.StartingPairs.Length);
            Assert.InRange(world.GenerationAttempt, 0U, 7U);
            Assert.Equal(8, world.Repairs.Length);
        }
    }

    [Fact]
    public void TopologyWrapsOnlyEastWest()
    {
        var west = WorldGridTopology.ToTileIndex(32, 17, -1, 0);
        var east = WorldGridTopology.ToTileIndex(32, 17, 31, 0);

        Assert.Equal(east, west);
        Assert.Equal(
            west,
            WorldGridTopology.Neighbor(
                32,
                17,
                WorldGridTopology.ToTileIndex(32, 17, 0, 0),
                -1,
                0));
        Assert.Null(WorldGridTopology.Neighbor(
            32,
            17,
            WorldGridTopology.ToTileIndex(32, 17, 0, 8),
            0,
            1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldGridTopology.ToTileIndex(32, 17, 0, 9));
    }

    [Fact]
    public void CalendarAndLightExposeSeasonAndDayNightWithoutFloatingPoint()
    {
        var profile = CompileGeneratedProfile();
        var world = DeterministicWorldGenerator.Generate(
            profile,
            RootRandomSeed.Parse("00112233445566778899aabbccddeeff"));
        var tile = world.GetTile(world.StartingPairs[0].HydrogenTileIndex);

        Assert.Equal(new WorldCalendarState(0, 0, 0, 0, 0),
            WorldClimateEvaluator.GetCalendar(world.Climate, 0));
        Assert.Equal(new WorldCalendarState(1, 0, 0, 0, 0),
            WorldClimateEvaluator.GetCalendar(world.Climate, 8_640));
        Assert.Equal(1_000_000U, WorldClimateEvaluator.DepthLightFactorQ(0));
        Assert.Equal(800_000U, WorldClimateEvaluator.DepthLightFactorQ(20));
        Assert.Equal(150_000U, WorldClimateEvaluator.DepthLightFactorQ(200));
        Assert.Equal(0U, WorldClimateEvaluator.DepthLightFactorQ(1_000));

        var dawn = WorldClimateEvaluator.Evaluate(world, tile, 0);
        var sixHoursLater = WorldClimateEvaluator.Evaluate(world, tile, 6);
        Assert.Equal(0U, dawn.SurfaceLightQ);
        Assert.True(sixHoursLater.SurfaceLightQ > dawn.SurfaceLightQ);
        var state = WorldClimateEvaluator.Materialize(world, 6, 1);
        Assert.Equal(1UL, state.Generation);
        Assert.Equal(6UL, state.AbsoluteHour);
        Assert.Equal(world.Tiles.Length, state.Tiles.Length);
        Assert.Equal(sixHoursLater, state.GetTile(tile.TileIndex));
    }

    private static CompiledWorldProfile CompileGeneratedProfile() =>
        CompileGeneratedWorldRules().WorldProfile;

    private static CompiledWorldRules CompileGeneratedWorldRules()
    {
        var rulesSource = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(rulesSource.IsSuccess, FormatDiagnostics(rulesSource.Diagnostics));
        var rules = RulePackCompiler.Compile(Assert.IsType<AuthoringRulePack>(rulesSource.Pack));
        Assert.True(rules.IsSuccess, FormatDiagnostics(rules.Diagnostics));
        var compiledRules = Assert.IsType<CompiledRulePack>(rules.RulePack);
        var worldSource = WorldPackSourceLoader.Load(new CopiedPackageSource("OfficialGeneratedWorld"));
        Assert.True(worldSource.IsSuccess, FormatDiagnostics(worldSource.Diagnostics));
        var result = WorldRulesCompiler.Compile(
            compiledRules,
            Assert.IsType<AuthoringWorldPack>(worldSource.Pack),
            "scenario.foundation-sandbox",
            "world.primordial-earth-v1");
        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        return Assert.IsType<CompiledWorldRules>(result.WorldRules);
    }

    private static string WorldSignature(GeneratedWorldMap world) => string.Join(
        '|',
        world.Tiles.Select(tile => string.Join(
            ',',
            tile.TileIndex,
            tile.ElevationMeters,
            tile.BaselineVolcanismQ,
            tile.AnnualMeanTemperatureMilliC,
            (byte)tile.StartingRole,
            tile.MonthlyPrecipitationMicrometersPerHour[0])));

    private static uint AbsoluteDifference(uint left, uint right) =>
        left >= right ? left - right : right - left;

    private static string FormatDiagnostics(IEnumerable<RuleDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"{diagnostic.SourceFile}: {diagnostic.Code}: {diagnostic.Message}"));

    private sealed class CopiedPackageSource(string directoryName) : IContentSource
    {
        private readonly string root = Path.Combine(AppContext.BaseDirectory, directoryName);

        public bool TryRead(string normalizedRelativePath, out ReadOnlyMemory<byte> content)
        {
            var path = Path.Combine(root, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
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

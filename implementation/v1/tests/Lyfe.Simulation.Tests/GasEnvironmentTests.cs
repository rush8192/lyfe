using Lyfe.Simulation.Mechanics;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.Ticks;
using Lyfe.Simulation.World;
using Lyfe.Simulation.World.Generation;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class GasEnvironmentTests
{
    [Fact]
    public void GeneratedWorldWeatheringSuppliesPhosphorusAsAnExactEnvironmentalFlow()
    {
        var rules = CompileGeneratedWorld();
        var seed = RootRandomSeed.Parse("00112233445566778899aabbccddeeff");
        var generated = DeterministicWorldGenerator.Generate(rules.WorldProfile, seed);
        var founderTile = TileId.FromRowMajorIndex(generated.StartingPairs[0].HydrogenTileIndex);
        var observedTile = TileId.FromRowMajorIndex(
            founderTile.Value == 0 ? 1U : 0U);
        var runner = WorldRunner.CreateGame(
            WorldId.From(98),
            rules,
            seed,
            new GameSetupCommand(
                GameMode.FreeSandbox,
                FounderGenomeId.From(1),
                founderTile,
                1));
        var phosphorus = runner.MutableWorld.GetResourceHandle(ResourceId.From(16));
        var before = runner.MutableWorld.GetTileResource(observedTile, phosphorus);

        var result = runner.AdvanceOneTick();

        Assert.Equal(before + 250,
            runner.MutableWorld.GetTileResource(observedTile, phosphorus));
        var environmental = Assert.Single(result.Changes.Phases,
            value => value.Phase == TickPhase.EnvironmentalLedger);
        var sources = environmental.ResourceTransactions.Where(value =>
            value.Cause == LedgerCause.EnvironmentalResourceSource).ToArray();
        Assert.Equal(544, sources.Length);
        Assert.All(sources, value => Assert.Equal(250, value.Extent));
        Assert.True(ResourceLedgerOracle.Reconcile(
            runner.Rules.RulePack,
            environmental.ResourceTransactions).IsBalanced);
        var publication = runner.CapturePublicationSnapshot();
        Assert.Contains(publication.ResourceFlowHistory[^1].ResourceFlows, value =>
            value.TileId == observedTile &&
            value.ResourceId == phosphorus.Id &&
            value.Kind == PublicationResourceFlowKind.EnvironmentalSource &&
            value.AmountQ == 250);
    }

    [Fact]
    public void WeatheringSupportsReproductionAfterThePlayableTilePhosphatePoolIsDepleted()
    {
        var rules = CompileGeneratedWorld();
        var seed = RootRandomSeed.Parse("00112233445566778899aabbccddeeff");
        var generated = DeterministicWorldGenerator.Generate(rules.WorldProfile, seed);
        var founderTile = TileId.FromRowMajorIndex(generated.StartingPairs[0].HydrogenTileIndex);
        var runner = WorldRunner.CreateGame(
            WorldId.From(97),
            rules,
            seed,
            new GameSetupCommand(
                GameMode.FreeSandbox,
                FounderGenomeId.From(1),
                founderTile,
                1,
                FounderInitialization: FounderInitializationMode.ZeroSpreadFixture));
        var world = runner.MutableWorld;
        var phosphorus = world.GetResourceHandle(ResourceId.From(16));
        var setup = world.BeginChanges();
        world.ApplyTileResourceDelta(
            founderTile,
            phosphorus,
            -world.GetTileResource(founderTile, phosphorus),
            setup);
        world.SealChanges(setup);

        for (var tick = 0; tick < 400; tick++)
        {
            runner.AdvanceOneTick();
        }

        Assert.True(world.GetTileResource(founderTile, phosphorus) > 0);
        Assert.True(world.GetSpecies(runner.GameState.ControlledSpeciesId).Population > 1);
    }

    [Fact]
    public void OfficialVentAppliesSourcesBeforeSinksAndPersistsFractionalContinuation()
    {
        var rules = CompileOfficialWorld();
        var runner = WorldRunner.CreateFoundation(
            WorldId.From(99),
            rules,
            RootRandomSeed.Parse("00000000000000000000000000000001"),
            founderCount: 1);

        var result = runner.AdvanceOneTick();
        var phase = Assert.Single(result.Changes.Phases,
            journal => journal.Phase == TickPhase.EnvironmentalLedger);
        var hydrogen = phase.ResourceTransactions.Where(transaction =>
            transaction.ReactionId.Value == 1).ToArray();
        Assert.Equal([250_000L, 125_625L], hydrogen.Select(transaction => transaction.Extent));
        Assert.Equal(
            [LedgerCause.EnvironmentalGasSource, LedgerCause.EnvironmentalGasSink],
            hydrogen.Select(transaction => transaction.Cause));
        Assert.True(ResourceLedgerOracle.Reconcile(rules.RulePack, phase.ResourceTransactions).IsBalanced);

        var persisted = runner.CapturePersistenceSnapshot();
        var carbonRemainder = Assert.Single(persisted.State.GasTileRemainders,
            remainder => remainder.ResourceId == 2);
        Assert.Equal(800, carbonRemainder.SinkRemainderQ);
        var restored = WorldRunner.Restore(rules, persisted);
        Assert.Equal(runner.CaptureSnapshot(), restored.CaptureSnapshot());
        Assert.Equal(
            runner.AdvanceOneTick().Snapshot,
            restored.AdvanceOneTick().Snapshot);
    }

    [Fact]
    public void HydrogenFieldConvergesToCalibratedVolcanicAndNeighborBands()
    {
        var rules = CompileHydrogenField(width: 9, height: 9, wrapX: false);
        var world = new MutableWorldState(rules);
        var phase = new GasTransportPhase();
        var random = new SemanticRandomOracle(
            RootRandomSeed.Parse("0123456789abcdeffedcba9876543210"));
        for (ulong tick = 1; tick <= 12_000; tick++)
        {
            phase.Execute(world, new TickExecutionContext(
                tick, tick, 1, rules.Identity.WorldRulesHash,
                NoTickFaultInjector.Instance, random, new TickScratch(tick)));
        }

        var hydrogen = world.GetResourceHandle(ResourceId.From(1));
        var center = TileId.FromRowMajorIndex(40);
        var neighbor = TileId.FromRowMajorIndex(41);
        var twoAway = TileId.FromRowMajorIndex(42);
        Assert.InRange(world.GetTileResource(center, hydrogen), 84_000_000, 89_000_000);
        Assert.InRange(world.GetTileResource(neighbor, hydrogen), 2_500_000, 3_500_000);
        Assert.InRange(world.GetTileResource(twoAway, hydrogen), 75_000, 140_000);
    }

    [Fact]
    public void GasTopologyWrapsOnlyXAndEmitsEachUndirectedEdgeOnce()
    {
        var rules = CompileHydrogenField(width: 3, height: 3, wrapX: true);
        var world = new MutableWorldState(rules);
        var edges = world.GetGasEdges();

        Assert.Equal(15, edges.Length);
        Assert.Contains(edges, edge => edge.LowerTileId.Value == 0 && edge.HigherTileId.Value == 2);
        Assert.DoesNotContain(edges, edge => edge.LowerTileId.Value == 0 && edge.HigherTileId.Value == 6);
        Assert.Equal(edges.Length, edges.Distinct().Count());
    }

    [Theory]
    [InlineData(GasAccessibilityClass.VentAccessible, -900, 0, 1_000_000)]
    [InlineData(GasAccessibilityClass.AtmosphericDepthLimited, -900, 0, 100_000)]
    [InlineData(GasAccessibilityClass.MixedOrigin, -900, 0, 100_000)]
    [InlineData(GasAccessibilityClass.MixedOrigin, -900, 1, 1_000_000)]
    [InlineData(GasAccessibilityClass.AtmosphericDepthLimited, 50, 0, 1_000_000)]
    public void AquaticGasAccessibilityUsesTheDeclaredOriginClass(
        GasAccessibilityClass accessibility,
        int elevationMeters,
        uint volcanismQ,
        long expected)
    {
        Assert.Equal(expected, GasAccessibility.AccessibleQuantity(
            1_000_000, elevationMeters, volcanismQ, accessibility));
    }

    private static CompiledWorldRules CompileHydrogenField(uint width, uint height, bool wrapX)
    {
        var ruleSource = RulePackSourceLoader.Load(new DirectorySource("OfficialRules"));
        Assert.True(ruleSource.IsSuccess);
        var ruleResult = RulePackCompiler.Compile(Assert.IsType<AuthoringRulePack>(ruleSource.Pack));
        Assert.True(ruleResult.IsSuccess);
        var rules = Assert.IsType<CompiledRulePack>(ruleResult.RulePack);

        var worldSource = WorldPackSourceLoader.Load(new DirectorySource("OfficialWorld"));
        Assert.True(worldSource.IsSuccess);
        var authoring = Assert.IsType<AuthoringWorldPack>(worldSource.Pack);
        var original = Assert.Single(authoring.Profiles);
        var originalGas = original.GasEnvironment;
        var hydrogenGas = originalGas.Gases.Single(gas => gas.ResourceKey == "resource.hydrogen-gas");
        var hydrogenProfile = originalGas.EmissionProfiles.Single(profile =>
            profile.StableKey == "gas-emission.hydrogen-rich");
        hydrogenProfile = hydrogenProfile with
        {
            Emissions = hydrogenProfile.Emissions
                .Where(emission => emission.ResourceKey == "resource.hydrogen-gas")
                .ToArray(),
        };
        var centerX = checked((int)width / 2);
        var centerY = checked((int)height / 2);
        var tiles = Enumerable.Range(0, checked((int)(width * height)))
            .Select(index =>
            {
                var x = index % (int)width;
                var y = index / (int)width;
                var isCenter = x == centerX && y == centerY;
                return new TileProfileDefinition
                {
                    X = x,
                    Y = y,
                    ElevationMeters = -200,
                    BaselineVolcanismQ = isCenter ? 800_000U : 0,
                    GasEmissionProfileKey = isCenter ? hydrogenProfile.StableKey : null,
                    ResourceStocks = [],
                };
            })
            .ToArray();
        authoring = authoring with
        {
            Profiles = [original with
            {
                Width = width,
                Height = height,
                WrapX = wrapX,
                WrapY = false,
                GasEnvironment = originalGas with
                {
                    Gases = [hydrogenGas],
                    EmissionProfiles = [hydrogenProfile],
                },
                Tiles = tiles,
            }],
        };
        var result = WorldRulesCompiler.Compile(
            rules, authoring, "scenario.foundation-sandbox", "world.primordial-foundation");
        Assert.True(result.IsSuccess, string.Join('\n', result.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<CompiledWorldRules>(result.WorldRules);
    }

    private static CompiledWorldRules CompileOfficialWorld()
    {
        var rulesSource = RulePackSourceLoader.Load(new DirectorySource("OfficialRules"));
        var rules = RulePackCompiler.Compile(Assert.IsType<AuthoringRulePack>(rulesSource.Pack));
        var worldSource = WorldPackSourceLoader.Load(new DirectorySource("OfficialWorld"));
        var world = WorldRulesCompiler.Compile(
            Assert.IsType<CompiledRulePack>(rules.RulePack),
            Assert.IsType<AuthoringWorldPack>(worldSource.Pack),
            "scenario.foundation-sandbox",
            "world.primordial-foundation");
        Assert.True(world.IsSuccess, string.Join('\n', world.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<CompiledWorldRules>(world.WorldRules);
    }

    private static CompiledWorldRules CompileGeneratedWorld()
    {
        var result = WorldRulesPipeline.Compile(
            new DirectorySource("OfficialRules"),
            new DirectorySource("OfficialGeneratedWorld"),
            "scenario.foundation-sandbox",
            "world.primordial-earth-v1");
        Assert.True(result.IsSuccess, string.Join('\n',
            result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.IsType<CompiledWorldRules>(result.WorldRules);
    }

    private sealed class DirectorySource(string directoryName) : IContentSource
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

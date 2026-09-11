using Lyfe.Server.Persistence;
using Proto = Lyfe.Protocol.V1;
using Xunit;

namespace Lyfe.Server.Tests;

public sealed class WorldSetupServiceTests : IDisposable
{
    private readonly string saveDirectory = Path.Combine(
        Path.GetTempPath(),
        $"lyfe-world-setup-tests-{Guid.NewGuid():N}");

    [Fact]
    public void SurfacePublishesOnlyBoundedAuthoredChoicesAndCandidateConditions()
    {
        using var clock = new WorldClockService();
        var rules = FoundationWorldBootstrap.LoadGeneratedRules(AppContext.BaseDirectory);
        using var catalogue = new WorldCatalogueService(rules, clock, saveDirectory);
        var service = new WorldSetupService(rules, clock, catalogue);

        var surface = service.Capture(WorldSetupService.DefaultSeed);

        Assert.False(surface.HasActiveWorld);
        Assert.Equal("world.primordial-earth-v1", surface.WorldProfileKey);
        Assert.Equal("Primordial Earth 32 by 17", surface.WorldProfileDisplayName);
        Assert.Equal(32U, surface.Width);
        Assert.Equal(17U, surface.Height);
        Assert.Equal(100U, surface.FounderPopulation);
        Assert.Equal(2, surface.Founders.Count);
        Assert.Equal(3, surface.Allocations.Count);
        Assert.Equal(4, surface.StartingRegions.Count);
        Assert.All(surface.Founders, founder =>
        {
            Assert.NotEmpty(founder.StableKey);
            Assert.NotEmpty(founder.DisplayName);
            Assert.True(founder.BaseReproductionCooldownHours > 0);
        });
        Assert.Equal([0U, 1U, 2U, 3U],
            surface.StartingRegions.Select(value => value.StartingPairIndex));
    }

    [Fact]
    public void CreateInstallsSandboxThenSurvivalWithTheAuthoredRoots()
    {
        using var clock = new WorldClockService();
        var rules = FoundationWorldBootstrap.LoadGeneratedRules(AppContext.BaseDirectory);
        using var catalogue = new WorldCatalogueService(rules, clock, saveDirectory);
        var service = new WorldSetupService(rules, clock, catalogue);

        var sandbox = service.Create(new Proto.CreateWorldRequest
        {
            RootSeedHex = WorldSetupService.DefaultSeed,
            Mode = Proto.GameMode.FreeSandbox,
            FounderGenomeId = 1,
            FounderAllocationId = 2,
            StartingPairIndex = 0,
        });

        Assert.Equal(1UL, sandbox.WorldId);
        clock.ReadBoundary((runner, publication, control) =>
        {
            Assert.Equal(544, publication.Tiles.Length);
            Assert.Equal(Proto.WorldLifecycle.PausedReady, control.Lifecycle);
            var root = Assert.Single(runner.GameState.Roots);
            Assert.Equal(1U, root.FounderGenomeId.Value);
            Assert.Equal(2U, root.FounderAllocationId.Value);
            Assert.True(root.PlayerSelected);
            return true;
        });

        Assert.Throws<WorldControlRejectedException>(() => service.Create(
            new Proto.CreateWorldRequest
            {
                RootSeedHex = WorldSetupService.DefaultSeed,
                Mode = Proto.GameMode.Survival,
                FounderGenomeId = 2,
                FounderAllocationId = 3,
                StartingPairIndex = 1,
            }));

        var survival = service.Create(new Proto.CreateWorldRequest
        {
            RootSeedHex = WorldSetupService.DefaultSeed,
            Mode = Proto.GameMode.Survival,
            FounderGenomeId = 2,
            FounderAllocationId = 3,
            StartingPairIndex = 1,
            ConfirmReplaceActive = true,
        });

        Assert.Equal(2UL, survival.WorldId);
        clock.ReadBoundary((runner, _, control) =>
        {
            Assert.Equal(2UL, control.ControlRevision);
            Assert.Equal(2, runner.GameState.Roots.Length);
            var player = runner.GameState.Roots.Single(value => value.PlayerSelected);
            var competitor = runner.GameState.Roots.Single(value => !value.PlayerSelected);
            Assert.Equal(2U, player.FounderGenomeId.Value);
            Assert.Equal(3U, player.FounderAllocationId.Value);
            Assert.Equal(1U, competitor.FounderGenomeId.Value);
            Assert.Equal(1U, competitor.FounderAllocationId.Value);
            Assert.NotEqual(player.StartingTileId, competitor.StartingTileId);
            return true;
        });
        Assert.True(service.Capture(WorldSetupService.DefaultSeed).HasActiveWorld);
    }

    [Fact]
    public void InvalidSeedAndUnpermittedChoicesFailClosed()
    {
        using var clock = new WorldClockService();
        var rules = FoundationWorldBootstrap.LoadGeneratedRules(AppContext.BaseDirectory);
        using var catalogue = new WorldCatalogueService(rules, clock, saveDirectory);
        var service = new WorldSetupService(rules, clock, catalogue);

        Assert.Throws<FormatException>(() => service.Capture("not-a-seed"));
        Assert.Throws<FormatException>(() => service.Create(new Proto.CreateWorldRequest
        {
            Mode = Proto.GameMode.FreeSandbox,
            FounderGenomeId = 1,
            FounderAllocationId = 1,
        }));
        Assert.Throws<ArgumentException>(() => service.Create(new Proto.CreateWorldRequest
        {
            RootSeedHex = WorldSetupService.DefaultSeed,
            Mode = Proto.GameMode.FreeSandbox,
            FounderGenomeId = 99,
            FounderAllocationId = 1,
        }));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
    }
}

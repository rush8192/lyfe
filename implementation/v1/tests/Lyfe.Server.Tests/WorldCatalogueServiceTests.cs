using Lyfe.Server.Persistence;
using Proto = Lyfe.Protocol.V1;
using Xunit;

namespace Lyfe.Server.Tests;

public sealed class WorldCatalogueServiceTests : IDisposable
{
    private readonly string saveDirectory = Path.Combine(
        Path.GetTempPath(),
        $"lyfe-world-catalogue-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveUnloadAndLoadPreserveAnExactAuthoritativeBoundary()
    {
        var rules = FoundationWorldBootstrap.LoadGeneratedRules(AppContext.BaseDirectory);
        using var clock = new WorldClockService();
        using var catalogue = new WorldCatalogueService(rules, clock, saveDirectory);
        var setup = new WorldSetupService(rules, clock, catalogue);
        var created = setup.Create(CreateRequest());
        var initial = Assert.IsType<Lyfe.Simulation.World.WorldSnapshot>(
            clock.CaptureActiveSnapshot());

        var beforeSave = catalogue.Capture();
        Assert.True(beforeSave.ActiveHasUnsavedChanges);
        Assert.Empty(beforeSave.SavedWorlds);

        var saved = await catalogue.SaveActiveAsync(new Proto.SaveActiveWorldRequest
        {
            ExpectedWorldId = created.WorldId,
            ExpectedWorldRevision = initial.WorldRevision,
        }, TestContext.Current.CancellationToken);

        var summary = Assert.Single(saved.SavedWorlds);
        Assert.Equal(created.WorldId, summary.WorldId);
        Assert.True(summary.IsActive);
        Assert.True(summary.IsActiveSavedBoundary);
        Assert.False(saved.ActiveHasUnsavedChanges);

        _ = clock.Apply(new Proto.WorldControlCommand
        {
            ExpectedControlRevision = 1,
            Action = Proto.WorldControlAction.Step,
        });
        var advanced = Assert.IsType<Lyfe.Simulation.World.WorldSnapshot>(
            clock.CaptureActiveSnapshot());
        Assert.True(catalogue.Capture().ActiveHasUnsavedChanges);
        await Assert.ThrowsAsync<WorldReplacementConfirmationException>(() =>
            catalogue.UnloadAsync(new Proto.UnloadWorldRequest
            {
                ExpectedWorldId = created.WorldId,
                ExpectedWorldRevision = advanced.WorldRevision,
            }, TestContext.Current.CancellationToken));

        var unloaded = await catalogue.UnloadAsync(new Proto.UnloadWorldRequest
        {
            ExpectedWorldId = created.WorldId,
            ExpectedWorldRevision = advanced.WorldRevision,
            ConfirmDiscardUnsaved = true,
        }, TestContext.Current.CancellationToken);
        Assert.False(unloaded.HasActiveWorld);

        var loaded = await catalogue.LoadAsync(new Proto.LoadWorldRequest
        {
            WorldId = created.WorldId,
        }, TestContext.Current.CancellationToken);
        var restored = Assert.IsType<Lyfe.Simulation.World.WorldSnapshot>(
            clock.CaptureActiveSnapshot());
        Assert.Equal(initial, restored);
        Assert.False(loaded.ActiveHasUnsavedChanges);
        Assert.True(Assert.Single(loaded.SavedWorlds).IsActiveSavedBoundary);
    }

    [Fact]
    public void WorldIdReservationsSurviveServiceRestart()
    {
        var rules = FoundationWorldBootstrap.LoadGeneratedRules(AppContext.BaseDirectory);
        using var clock = new WorldClockService();
        ulong first;
        using (var catalogue = new WorldCatalogueService(rules, clock, saveDirectory))
        {
            first = catalogue.ReserveWorldId().Value;
        }
        using var restarted = new WorldCatalogueService(rules, clock, saveDirectory);

        Assert.Equal(1UL, first);
        Assert.Equal(2UL, restarted.ReserveWorldId().Value);
    }

    [Fact]
    public async Task StaleBoundaryAndMissingWorldRequestsFailClosed()
    {
        var rules = FoundationWorldBootstrap.LoadGeneratedRules(AppContext.BaseDirectory);
        using var clock = new WorldClockService();
        using var catalogue = new WorldCatalogueService(rules, clock, saveDirectory);
        var setup = new WorldSetupService(rules, clock, catalogue);
        var created = setup.Create(CreateRequest());

        await Assert.ThrowsAsync<WorldControlConflictException>(() =>
            catalogue.SaveActiveAsync(new Proto.SaveActiveWorldRequest
            {
                ExpectedWorldId = created.WorldId,
                ExpectedWorldRevision = ulong.MaxValue,
            }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<SavedWorldNotFoundException>(() =>
            catalogue.LoadAsync(new Proto.LoadWorldRequest
            {
                WorldId = 999,
                ExpectedActiveWorldId = created.WorldId,
            }, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
    }

    private static Proto.CreateWorldRequest CreateRequest() => new()
    {
        RootSeedHex = WorldSetupService.DefaultSeed,
        Mode = Proto.GameMode.FreeSandbox,
        FounderGenomeId = 1,
        FounderAllocationId = 1,
        StartingPairIndex = 0,
    };
}

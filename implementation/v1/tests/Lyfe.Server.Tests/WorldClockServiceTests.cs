using Lyfe.Simulation.Core;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.World;
using Proto = Lyfe.Protocol.V1;
using Xunit;

namespace Lyfe.Server.Tests;

public sealed class WorldClockServiceTests
{
    [Fact]
    public async Task RunningClockAdvancesAndPauseHoldsTheLastCompletedBoundary()
    {
        var runner = CreateRunner();
        using var clock = new WorldClockService(runner);
        await clock.StartAsync(CancellationToken.None);
        var faster = clock.Apply(new Proto.WorldControlCommand
        {
            ExpectedControlRevision = 1,
            Action = Proto.WorldControlAction.SetSpeed,
            Speed = Proto.SimulationSpeedPreset.Fast,
        });
        var running = clock.Apply(new Proto.WorldControlCommand
        {
            ExpectedControlRevision = faster.ControlRevision,
            Action = Proto.WorldControlAction.Resume,
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (clock.Capture().CompletedTick == 0)
        {
            await Task.Delay(25, timeout.Token);
        }

        var paused = clock.Apply(new Proto.WorldControlCommand
        {
            ExpectedControlRevision = running.ControlRevision,
            Action = Proto.WorldControlAction.Pause,
        });
        await Task.Delay(350, TestContext.Current.CancellationToken);

        Assert.Equal(paused.CompletedTick, clock.Capture().CompletedTick);
        await clock.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void CommandsAdvanceOnlyCompletedBoundariesAndRejectStaleRevisions()
    {
        var runner = CreateRunner();
        using var clock = new WorldClockService(runner);

        var initial = clock.Capture();
        Assert.Equal(1UL, initial.ControlRevision);
        Assert.Equal(Proto.WorldLifecycle.PausedReady, initial.Lifecycle);
        Assert.Equal(Proto.SimulationSpeedPreset.Normal, initial.Speed);
        Assert.Equal(1_000U, initial.TargetIntervalMs);
        Assert.Equal(0UL, initial.CompletedTick);

        var stepped = clock.Apply(new Proto.WorldControlCommand
        {
            ExpectedControlRevision = initial.ControlRevision,
            Action = Proto.WorldControlAction.Step,
        });
        Assert.Equal(2UL, stepped.ControlRevision);
        Assert.Equal(1UL, stepped.CompletedTick);
        Assert.Equal(Proto.WorldLifecycle.PausedReady, stepped.Lifecycle);

        Assert.Throws<WorldControlConflictException>(() => clock.Apply(
            new Proto.WorldControlCommand
            {
                ExpectedControlRevision = initial.ControlRevision,
                Action = Proto.WorldControlAction.Step,
            }));

        var faster = clock.Apply(new Proto.WorldControlCommand
        {
            ExpectedControlRevision = stepped.ControlRevision,
            Action = Proto.WorldControlAction.SetSpeed,
            Speed = Proto.SimulationSpeedPreset.Fast,
        });
        Assert.Equal(100U, faster.TargetIntervalMs);

        var running = clock.Apply(new Proto.WorldControlCommand
        {
            ExpectedControlRevision = faster.ControlRevision,
            Action = Proto.WorldControlAction.Resume,
        });
        Assert.Equal(Proto.WorldLifecycle.Running, running.Lifecycle);
        Assert.Throws<WorldControlRejectedException>(() =>
            clock.ExecutePaused(() => 1));

        var paused = clock.Apply(new Proto.WorldControlCommand
        {
            ExpectedControlRevision = running.ControlRevision,
            Action = Proto.WorldControlAction.Pause,
        });
        Assert.Equal(Proto.WorldLifecycle.PausedReady, paused.Lifecycle);
        Assert.Equal(1UL, paused.CompletedTick);

        clock.ReadBoundary((publication, control) =>
        {
            Assert.Equal(publication.CompletedTick, control.CompletedTick);
            Assert.Equal(publication.WorldRevision, control.WorldRevision);
            Assert.Equal(publication.SimulatedHours, control.SimulatedHours);
            return true;
        });
    }

    private static WorldRunner CreateRunner() => WorldRunner.CreateFoundation(
        WorldId.From(1),
        FoundationWorldBootstrap.LoadRules(AppContext.BaseDirectory),
        RootRandomSeed.Parse("00112233445566778899aabbccddeeff"));
}

using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.World;
using Proto = Lyfe.Protocol.V1;

namespace Lyfe.Server;

public sealed class WorldControlConflictException(ulong expected, ulong actual) :
    InvalidOperationException(
        $"World-control revision {expected} is stale; current revision is {actual}.");

public sealed class WorldControlRejectedException(string message) :
    InvalidOperationException(message);

public sealed class WorldClockService : BackgroundService
{
    private const int SlowIntervalMs = 2_000;
    private const int NormalIntervalMs = 1_000;
    private const int FastIntervalMs = 100;

    private readonly object gate = new();
    private readonly SemaphoreSlim wakeSignal = new(0, 1);
    private WorldRunner? runner;
    private ulong controlRevision;
    private bool running;
    private Proto.SimulationSpeedPreset speed = Proto.SimulationSpeedPreset.Normal;

    public WorldClockService()
    {
    }

    public WorldClockService(WorldRunner runner)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        controlRevision = 1;
    }

    public bool HasActiveWorld
    {
        get { lock (gate) return runner is not null; }
    }

    public Proto.WorldControlState ReplacePaused(
        WorldRunner replacement,
        bool confirmReplaceActive = true)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        lock (gate)
        {
            if (running)
            {
                throw new WorldControlRejectedException(
                    "Pause the active world before replacing it.");
            }
            if (runner is not null && !confirmReplaceActive)
            {
                throw new WorldControlRejectedException(
                    "Replacing the active world requires explicit confirmation.");
            }
            runner = replacement;
            speed = Proto.SimulationSpeedPreset.Normal;
            controlRevision = controlRevision == 0
                ? 1
                : checked(controlRevision + 1);
            Signal();
            return CaptureCore();
        }
    }

    public WorldSnapshot? CaptureActiveSnapshot()
    {
        lock (gate) return runner?.CaptureSnapshot();
    }

    public WorldPersistenceSnapshot CapturePersistenceSnapshot(
        ulong expectedWorldId,
        ulong expectedWorldRevision)
    {
        lock (gate)
        {
            var active = RequireRunner();
            var boundary = active.CaptureSnapshot();
            RequireExpectedBoundary(boundary, expectedWorldId, expectedWorldRevision);
            return active.CapturePersistenceSnapshot();
        }
    }

    public void UnloadPaused(ulong expectedWorldId, ulong expectedWorldRevision)
    {
        lock (gate)
        {
            if (running)
            {
                throw new WorldControlRejectedException(
                    "Pause the active world before unloading it.");
            }
            var active = RequireRunner();
            RequireExpectedBoundary(
                active.CaptureSnapshot(),
                expectedWorldId,
                expectedWorldRevision);
            runner = null;
            speed = Proto.SimulationSpeedPreset.Normal;
            controlRevision = checked(controlRevision + 1);
            Signal();
        }
    }

    public Proto.WorldControlState Capture()
    {
        lock (gate) return CaptureCore();
    }

    public T ReadBoundary<T>(
        Func<WorldPublicationSnapshot, Proto.WorldControlState, T> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        lock (gate)
        {
            var active = RequireRunner();
            return reader(active.CapturePublicationSnapshot(), CaptureCore());
        }
    }

    public T ReadBoundary<T>(
        Func<WorldRunner, WorldPublicationSnapshot, Proto.WorldControlState, T> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        lock (gate)
        {
            var active = RequireRunner();
            return reader(active, active.CapturePublicationSnapshot(), CaptureCore());
        }
    }

    public T ExecutePaused<T>(Func<T> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (gate)
        {
            if (running)
            {
                throw new WorldControlRejectedException(
                    "This command requires the world to be paused.");
            }
            _ = RequireRunner();
            return command();
        }
    }

    public T ExecutePaused<T>(Func<WorldRunner, T> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (gate)
        {
            if (running)
            {
                throw new WorldControlRejectedException(
                    "This command requires the world to be paused.");
            }
            return command(RequireRunner());
        }
    }

    public Proto.WorldControlState Apply(Proto.WorldControlCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (gate)
        {
            var active = RequireRunner();
            if (command.ExpectedControlRevision != controlRevision)
            {
                throw new WorldControlConflictException(
                    command.ExpectedControlRevision,
                    controlRevision);
            }
            switch (command.Action)
            {
                case Proto.WorldControlAction.Pause:
                    RequireUnspecifiedSpeed(command);
                    running = false;
                    Signal();
                    break;
                case Proto.WorldControlAction.Resume:
                    RequireActiveGame();
                    if (command.Speed != Proto.SimulationSpeedPreset.Unspecified)
                    {
                        RequireSpeed(command.Speed);
                        speed = command.Speed;
                    }
                    running = true;
                    Signal();
                    break;
                case Proto.WorldControlAction.Step:
                    RequireUnspecifiedSpeed(command);
                    RequireActiveGame();
                    if (running)
                    {
                        throw new WorldControlRejectedException(
                            "Pause the world before advancing one tick.");
                    }
                    active.AdvanceOneTick();
                    break;
                case Proto.WorldControlAction.SetSpeed:
                    RequireSpeed(command.Speed);
                    speed = command.Speed;
                    Signal();
                    break;
                default:
                    throw new WorldControlRejectedException(
                        "A supported world-control action is required.");
            }
            controlRevision = checked(controlRevision + 1);
            return CaptureCore();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int intervalMs;
            lock (gate)
            {
                if (!running)
                {
                    intervalMs = 0;
                }
                else
                {
                    intervalMs = IntervalMs(speed);
                }
            }

            if (intervalMs == 0)
            {
                await wakeSignal.WaitAsync(stoppingToken);
                continue;
            }

            if (await wakeSignal.WaitAsync(intervalMs, stoppingToken))
            {
                continue;
            }
            lock (gate)
            {
                if (!running) continue;
                try
                {
                    var active = RequireRunner();
                    active.AdvanceOneTick();
                    if (active.GameState.RunStatus != GameRunStatus.Active)
                    {
                        running = false;
                        controlRevision = checked(controlRevision + 1);
                    }
                }
                catch (WorldTickException)
                {
                    running = false;
                    controlRevision = checked(controlRevision + 1);
                }
            }
        }
    }

    private Proto.WorldControlState CaptureCore()
    {
        var active = RequireRunner();
        var boundary = active.CaptureSnapshot();
        var game = active.GameState;
        return new Proto.WorldControlState
        {
            ControlRevision = controlRevision,
            Lifecycle = running
                ? Proto.WorldLifecycle.Running
                : Proto.WorldLifecycle.PausedReady,
            Speed = speed,
            TargetIntervalMs = checked((uint)IntervalMs(speed)),
            CompletedTick = boundary.CompletedTick,
            WorldRevision = boundary.WorldRevision,
            SimulatedHours = boundary.SimulatedHours,
            GameRunStatus = game.RunStatus switch
            {
                GameRunStatus.Active => Proto.GameRunStatus.Active,
                GameRunStatus.Lost => Proto.GameRunStatus.Lost,
                _ => throw new InvalidOperationException("Unsupported game run status."),
            },
        };
    }

    private static int IntervalMs(Proto.SimulationSpeedPreset value) => value switch
    {
        Proto.SimulationSpeedPreset.Slow => SlowIntervalMs,
        Proto.SimulationSpeedPreset.Normal => NormalIntervalMs,
        Proto.SimulationSpeedPreset.Fast => FastIntervalMs,
        _ => throw new WorldControlRejectedException("A supported speed preset is required."),
    };

    private static void RequireSpeed(Proto.SimulationSpeedPreset value) =>
        _ = IntervalMs(value);

    private static void RequireUnspecifiedSpeed(Proto.WorldControlCommand command)
    {
        if (command.Speed != Proto.SimulationSpeedPreset.Unspecified)
        {
            throw new WorldControlRejectedException(
                "This world-control action does not accept a speed preset.");
        }
    }

    private void RequireActiveGame()
    {
        if (RequireRunner().GameState.RunStatus != GameRunStatus.Active)
        {
            throw new WorldControlRejectedException(
                "An ended game cannot advance.");
        }
    }

    private WorldRunner RequireRunner() => runner ??
        throw new WorldControlRejectedException("There is no active world.");

    private static void RequireExpectedBoundary(
        WorldSnapshot boundary,
        ulong expectedWorldId,
        ulong expectedWorldRevision)
    {
        if (boundary.WorldId.Value != expectedWorldId ||
            boundary.WorldRevision != expectedWorldRevision)
        {
            throw new WorldControlConflictException(
                expectedWorldRevision,
                boundary.WorldRevision);
        }
    }

    private void Signal()
    {
        try
        {
            wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // One pending wake is sufficient.
        }
    }
}

using System.Globalization;
using System.Text;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.World;
using Proto = Lyfe.Protocol.V1;

namespace Lyfe.Server.Persistence;

public sealed class WorldReplacementConfirmationException(string message) :
    InvalidOperationException(message);

public sealed class SavedWorldNotFoundException(ulong worldId) :
    FileNotFoundException($"Saved world {worldId} does not exist.");

public sealed class WorldCatalogueService : IDisposable
{
    private const string SaveExtension = ".lyfe";
    private const string NextWorldIdFileName = "next-world-id";

    private readonly CompiledWorldRules rules;
    private readonly WorldClockService clock;
    private readonly string saveDirectory;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly object idGate = new();
    private ulong nextWorldId;
    private bool disposed;

    public WorldCatalogueService(
        CompiledWorldRules rules,
        WorldClockService clock,
        string saveDirectory)
    {
        this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);
        this.saveDirectory = Path.GetFullPath(saveDirectory);
        Directory.CreateDirectory(this.saveDirectory);
        nextWorldId = ReadNextWorldId();
    }

    public WorldId ReserveWorldId()
    {
        lock (idGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var reserved = WorldId.From(nextWorldId);
            nextWorldId = checked(nextWorldId + 1);
            var contents = Encoding.ASCII.GetBytes(
                nextWorldId.ToString(CultureInfo.InvariantCulture));
            AtomicSaveFile.WriteAsync(
                    Path.Combine(saveDirectory, NextWorldIdFileName),
                    contents)
                .GetAwaiter()
                .GetResult();
            return reserved;
        }
    }

    public Proto.WorldCatalogue Capture()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var active = clock.CaptureActiveSnapshot();
        var saved = ReadSavedWorlds(active);
        var result = new Proto.WorldCatalogue
        {
            HasActiveWorld = active is not null,
            ActiveWorldId = active?.WorldId.Value ?? 0,
            ActiveWorldRevision = active?.WorldRevision ?? 0,
            ActiveHasUnsavedChanges = active is not null &&
                !saved.Any(value => value.IsActiveSavedBoundary),
        };
        result.SavedWorlds.Add(saved);
        return result;
    }

    public async Task<Proto.WorldCatalogue> SaveActiveAsync(
        Proto.SaveActiveWorldRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var detached = clock.CapturePersistenceSnapshot(
                request.ExpectedWorldId,
                request.ExpectedWorldRevision);
            await WorldSaveService.SaveAsync(
                detached,
                SavePath(detached.Metadata.Boundary.WorldId.Value),
                cancellationToken).ConfigureAwait(false);
            return Capture();
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<Proto.WorldCatalogue> LoadAsync(
        Proto.LoadWorldRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.WorldId == 0)
        {
            throw new ArgumentException("A saved world ID is required.", nameof(request));
        }

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sourcePath = SavePath(request.WorldId);
            if (!File.Exists(sourcePath)) throw new SavedWorldNotFoundException(request.WorldId);
            var active = RequireExpectedActiveBoundary(
                request.ExpectedActiveWorldId,
                request.ExpectedActiveWorldRevision);
            RequireDiscardConfirmation(active, request.ConfirmDiscardUnsaved);
            var replacement = WorldSaveService.Load(sourcePath, rules);
            if (replacement.CaptureSnapshot().WorldId.Value != request.WorldId)
            {
                throw new InvalidDataException(
                    "The saved world identity does not match its catalogue entry.");
            }
            clock.ReplacePaused(replacement, confirmReplaceActive: active is not null);
            return Capture();
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<Proto.WorldCatalogue> UnloadAsync(
        Proto.UnloadWorldRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var active = RequireExpectedActiveBoundary(
                request.ExpectedWorldId,
                request.ExpectedWorldRevision) ??
                throw new WorldControlRejectedException("There is no active world.");
            RequireDiscardConfirmation(active, request.ConfirmDiscardUnsaved);
            clock.UnloadPaused(request.ExpectedWorldId, request.ExpectedWorldRevision);
            return Capture();
        }
        finally
        {
            operationGate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        operationGate.Dispose();
        disposed = true;
    }

    private WorldSnapshot? RequireExpectedActiveBoundary(
        ulong expectedWorldId,
        ulong expectedWorldRevision)
    {
        var active = clock.CaptureActiveSnapshot();
        if (active is null)
        {
            if (expectedWorldId != 0 || expectedWorldRevision != 0)
            {
                throw new WorldControlConflictException(expectedWorldRevision, 0);
            }
            return null;
        }
        if (active.WorldId.Value != expectedWorldId ||
            active.WorldRevision != expectedWorldRevision)
        {
            throw new WorldControlConflictException(
                expectedWorldRevision,
                active.WorldRevision);
        }
        return active;
    }

    private void RequireDiscardConfirmation(
        WorldSnapshot? active,
        bool confirmed)
    {
        if (active is not null && !HasSavedBoundary(active) && !confirmed)
        {
            throw new WorldReplacementConfirmationException(
                "The active world has unsaved changes. Confirm discarding them before continuing.");
        }
    }

    private bool HasSavedBoundary(WorldSnapshot active) =>
        TryReadDescriptor(SavePath(active.WorldId.Value), out var descriptor) &&
        descriptor.Metadata.WorldRevision == active.WorldRevision &&
        descriptor.Metadata.WorldStateHash == active.StateHash;

    private Proto.SavedWorldSummary[] ReadSavedWorlds(WorldSnapshot? active)
    {
        var result = new List<Proto.SavedWorldSummary>();
        foreach (var path in Directory.EnumerateFiles(saveDirectory, $"*{SaveExtension}"))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            if (!ulong.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ||
                id == 0 ||
                !TryReadDescriptor(path, out var descriptor) ||
                descriptor.Metadata.WorldId != id)
            {
                continue;
            }
            var metadata = descriptor.Metadata;
            var isActive = active?.WorldId.Value == id;
            result.Add(new Proto.SavedWorldSummary
            {
                WorldId = id,
                CompletedTick = metadata.CompletedTick,
                WorldRevision = metadata.WorldRevision,
                SimulatedHours = metadata.SimulatedHours,
                TickDurationHours = metadata.TickDurationHours,
                OrganismCount = metadata.OrganismCount,
                RootSeedHex = metadata.RootRandomSeed,
                WorldProfileKey = metadata.Compatibility.WorldProfileKey,
                WorldStateHash = metadata.WorldStateHash,
                SavedAtUnixMilliseconds = new DateTimeOffset(
                    File.GetLastWriteTimeUtc(path)).ToUnixTimeMilliseconds(),
                IsActive = isActive,
                IsActiveSavedBoundary = isActive &&
                    metadata.WorldRevision == active!.WorldRevision &&
                    metadata.WorldStateHash == active.StateHash,
            });
        }
        return result
            .OrderByDescending(value => value.SavedAtUnixMilliseconds)
            .ThenByDescending(value => value.WorldId)
            .ToArray();
    }

    private bool TryReadDescriptor(
        string path,
        out SaveEnvelopeDescriptor descriptor)
    {
        try
        {
            using var stream = File.OpenRead(path);
            descriptor = SaveEnvelopeCodec.ReadDescriptor(
                stream,
                SaveEnvelopeMetadataFactory.CreateCompatibility(rules));
            return true;
        }
        catch (Exception exception) when (exception is IOException or
            UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            descriptor = null!;
            return false;
        }
    }

    private ulong ReadNextWorldId()
    {
        ulong nextFromCounter = 1;
        var counterPath = Path.Combine(saveDirectory, NextWorldIdFileName);
        if (File.Exists(counterPath) &&
            ulong.TryParse(
                File.ReadAllText(counterPath),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsed) &&
            parsed > 0)
        {
            nextFromCounter = parsed;
        }

        var nextFromSaves = Directory.EnumerateFiles(saveDirectory, $"*{SaveExtension}")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(value => ulong.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var id) ? id : 0)
            .DefaultIfEmpty(0UL)
            .Max();
        nextFromSaves = checked(nextFromSaves + 1);
        return Math.Max(nextFromCounter, nextFromSaves);
    }

    private string SavePath(ulong worldId)
    {
        ArgumentOutOfRangeException.ThrowIfZero(worldId);
        return Path.Combine(
            saveDirectory,
            $"{worldId.ToString("D20", CultureInfo.InvariantCulture)}{SaveExtension}");
    }
}

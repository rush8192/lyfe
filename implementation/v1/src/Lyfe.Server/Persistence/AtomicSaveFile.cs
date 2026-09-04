namespace Lyfe.Server.Persistence;

internal enum AtomicSaveStage : byte
{
    AfterFlushBeforeReplace = 1,
}

internal interface IAtomicSaveFaultInjector
{
    void ThrowIfRequested(AtomicSaveStage stage);
}

internal sealed class NoAtomicSaveFaultInjector : IAtomicSaveFaultInjector
{
    public static NoAtomicSaveFaultInjector Instance { get; } = new();

    private NoAtomicSaveFaultInjector()
    {
    }

    public void ThrowIfRequested(AtomicSaveStage stage)
    {
    }
}

public static class AtomicSaveFile
{
    public static Task WriteAsync(
        string destinationPath,
        ReadOnlyMemory<byte> contents,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            destinationPath,
            contents,
            NoAtomicSaveFaultInjector.Instance,
            cancellationToken);

    internal static async Task WriteAsync(
        string destinationPath,
        ReadOnlyMemory<byte> contents,
        IAtomicSaveFaultInjector faultInjector,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(faultInjector);
        if (contents.IsEmpty)
        {
            throw new ArgumentException("A save file cannot be empty.", nameof(contents));
        }

        var destination = Path.GetFullPath(destinationPath);
        var fileName = Path.GetFileName(destination);
        var directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException(
                "A save destination must identify a file in a directory.",
                nameof(destinationPath));
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{fileName}.tmp-{Guid.NewGuid():N}");
        var replaced = false;
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(contents, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            faultInjector.ThrowIfRequested(AtomicSaveStage.AfterFlushBeforeReplace);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destination, overwrite: true);
            replaced = true;
        }
        finally
        {
            if (!replaced && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

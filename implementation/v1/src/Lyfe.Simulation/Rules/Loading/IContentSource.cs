namespace Lyfe.Simulation.Rules.Loading;

public interface IContentSource
{
    bool TryRead(string normalizedRelativePath, out ReadOnlyMemory<byte> content);
}


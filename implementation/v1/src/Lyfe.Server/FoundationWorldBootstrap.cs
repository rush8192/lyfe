using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Server;

internal static class FoundationWorldBootstrap
{
    public static CompiledWorldRules LoadRules(string applicationBaseDirectory)
    {
        var compiledWorld = WorldRulesPipeline.Compile(
            new HostDirectoryContentSource(applicationBaseDirectory, "OfficialRules"),
            new HostDirectoryContentSource(applicationBaseDirectory, "OfficialWorld"),
            "scenario.foundation-sandbox",
            "world.primordial-foundation");
        if (!compiledWorld.IsSuccess || compiledWorld.WorldRules is not CompiledWorldRules worldRules)
        {
            throw CompilationFailure(compiledWorld.Diagnostics);
        }

        return worldRules;
    }

    private static InvalidOperationException CompilationFailure(
        IEnumerable<RuleDiagnostic> diagnostics)
    {
        var details = string.Join(
            "; ",
            diagnostics.Select(diagnostic =>
                $"{diagnostic.SourceFile}:{diagnostic.Code}:{diagnostic.Message}"));
        return new InvalidOperationException(
            $"Foundation world compilation failed. {details}");
    }
}

internal sealed class HostDirectoryContentSource : IContentSource
{
    private readonly string rootPath;

    public HostDirectoryContentSource(string applicationBaseDirectory, string directoryName) =>
        rootPath = Path.GetFullPath(Path.Combine(applicationBaseDirectory, directoryName));

    public bool TryRead(string normalizedRelativePath, out ReadOnlyMemory<byte> content)
    {
        content = default;
        try
        {
            var relativePath = normalizedRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(rootPath, relativePath));
            var rootPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.Ordinal) ||
                !File.Exists(candidate))
            {
                return false;
            }

            content = File.ReadAllBytes(candidate);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

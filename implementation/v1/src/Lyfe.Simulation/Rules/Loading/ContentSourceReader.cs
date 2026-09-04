namespace Lyfe.Simulation.Rules.Loading;

internal static class ContentSourceReader
{
    public const int MaximumManifestBytes = 1024 * 1024;
    public const int MaximumSourceBytes = 4 * 1024 * 1024;
    public const int MaximumIncludedFiles = 128;

    public static ReadOnlyMemory<byte>? Read(
        IContentSource source,
        string relativePath,
        int maximumBytes,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (!source.TryRead(relativePath, out var content))
        {
            diagnostics.Add(new RuleDiagnostic(
                RuleDiagnosticSeverity.Error,
                "LYFE-CONTENT-SOURCE-001",
                "Required content file is missing.",
                relativePath,
                1,
                1));
            return null;
        }

        if (content.Length > maximumBytes)
        {
            diagnostics.Add(new RuleDiagnostic(
                RuleDiagnosticSeverity.Error,
                "LYFE-CONTENT-SOURCE-002",
                $"Content file exceeds the {maximumBytes}-byte limit.",
                relativePath,
                1,
                1));
            return null;
        }

        return content;
    }

    public static bool ValidateIncludedPaths(
        string manifestFile,
        string[]? paths,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (paths is null || paths.Length == 0)
        {
            diagnostics.Add(new RuleDiagnostic(
                RuleDiagnosticSeverity.Error,
                "LYFE-CONTENT-MANIFEST-001",
                "A manifest must explicitly include at least one source file.",
                manifestFile,
                1,
                1));
            return false;
        }

        if (paths.Length > MaximumIncludedFiles)
        {
            diagnostics.Add(new RuleDiagnostic(
                RuleDiagnosticSeverity.Error,
                "LYFE-CONTENT-MANIFEST-002",
                $"A manifest may include at most {MaximumIncludedFiles} source files.",
                manifestFile,
                1,
                1));
        }

        var valid = paths.Length <= MaximumIncludedFiles;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (!ContentPath.IsValid(path))
            {
                diagnostics.Add(new RuleDiagnostic(
                    RuleDiagnosticSeverity.Error,
                    "LYFE-CONTENT-MANIFEST-003",
                    $"Included path '{path}' is not a normalized relative content path.",
                    manifestFile,
                    1,
                    1));
                valid = false;
            }
            else if (!seen.Add(path))
            {
                diagnostics.Add(new RuleDiagnostic(
                    RuleDiagnosticSeverity.Error,
                    "LYFE-CONTENT-MANIFEST-004",
                    $"Included path '{path}' appears more than once.",
                    manifestFile,
                    1,
                    1));
                valid = false;
            }
        }

        return valid;
    }
}


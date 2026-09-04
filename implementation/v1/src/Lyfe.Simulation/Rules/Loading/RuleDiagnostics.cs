namespace Lyfe.Simulation.Rules.Loading;

public enum RuleDiagnosticSeverity
{
    Error = 0,
    Warning = 1,
}

public sealed record RuleDiagnostic(
    RuleDiagnosticSeverity Severity,
    string Code,
    string Message,
    string SourceFile,
    int Line,
    int Column,
    string? JsonPath = null);

internal static class RuleDiagnosticOrdering
{
    public static RuleDiagnostic[] Sort(IEnumerable<RuleDiagnostic> diagnostics) =>
        diagnostics
            .OrderBy(diagnostic => diagnostic.Severity)
            .ThenBy(diagnostic => diagnostic.SourceFile, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Line)
            .ThenBy(diagnostic => diagnostic.Column)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray();
}


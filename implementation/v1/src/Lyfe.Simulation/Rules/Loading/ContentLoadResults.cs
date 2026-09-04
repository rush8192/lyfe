using Lyfe.Simulation.Rules.Authoring;

namespace Lyfe.Simulation.Rules.Loading;

public sealed record RulePackLoadResult(
    AuthoringRulePack? Pack,
    IReadOnlyList<RuleDiagnostic> Diagnostics)
{
    public bool IsSuccess => Pack is not null &&
        !Diagnostics.Any(diagnostic => diagnostic.Severity == RuleDiagnosticSeverity.Error);
}

public sealed record WorldPackLoadResult(
    AuthoringWorldPack? Pack,
    IReadOnlyList<RuleDiagnostic> Diagnostics)
{
    public bool IsSuccess => Pack is not null &&
        !Diagnostics.Any(diagnostic => diagnostic.Severity == RuleDiagnosticSeverity.Error);
}


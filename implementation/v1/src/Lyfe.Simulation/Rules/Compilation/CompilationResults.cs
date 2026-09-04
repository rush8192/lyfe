using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Simulation.Rules.Compilation;

public sealed record RuleCompilationResult(
    CompiledRulePack? RulePack,
    IReadOnlyList<RuleDiagnostic> Diagnostics)
{
    public bool IsSuccess => RulePack is not null &&
        !Diagnostics.Any(diagnostic => diagnostic.Severity == RuleDiagnosticSeverity.Error);
}

public sealed record WorldRulesCompilationResult(
    CompiledWorldRules? WorldRules,
    IReadOnlyList<RuleDiagnostic> Diagnostics)
{
    public bool IsSuccess => WorldRules is not null &&
        !Diagnostics.Any(diagnostic => diagnostic.Severity == RuleDiagnosticSeverity.Error);
}

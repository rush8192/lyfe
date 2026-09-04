using Lyfe.Simulation.Rules.Loading;

namespace Lyfe.Simulation.Rules.Compilation;

public static class WorldRulesPipeline
{
    public static WorldRulesCompilationResult Compile(
        IContentSource ruleSource,
        IContentSource worldSource,
        string scenarioKey,
        string worldProfileKey)
    {
        ArgumentNullException.ThrowIfNull(ruleSource);
        ArgumentNullException.ThrowIfNull(worldSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldProfileKey);

        var diagnostics = new List<RuleDiagnostic>();
        var loadedRules = RulePackSourceLoader.Load(ruleSource);
        diagnostics.AddRange(loadedRules.Diagnostics);
        if (!loadedRules.IsSuccess || loadedRules.Pack is null)
        {
            return new WorldRulesCompilationResult(null, diagnostics);
        }

        var compiledRules = RulePackCompiler.Compile(loadedRules.Pack);
        diagnostics.AddRange(compiledRules.Diagnostics);
        if (!compiledRules.IsSuccess || compiledRules.RulePack is null)
        {
            return new WorldRulesCompilationResult(null, diagnostics);
        }

        var loadedWorld = WorldPackSourceLoader.Load(worldSource);
        diagnostics.AddRange(loadedWorld.Diagnostics);
        if (!loadedWorld.IsSuccess || loadedWorld.Pack is null)
        {
            return new WorldRulesCompilationResult(null, diagnostics);
        }

        var compiledWorld = WorldRulesCompiler.Compile(
            compiledRules.RulePack,
            loadedWorld.Pack,
            scenarioKey,
            worldProfileKey);
        diagnostics.AddRange(compiledWorld.Diagnostics);
        return new WorldRulesCompilationResult(compiledWorld.WorldRules, diagnostics);
    }
}

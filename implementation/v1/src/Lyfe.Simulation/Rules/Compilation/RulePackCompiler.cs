using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Simulation.Rules.Compilation;

public static class RulePackCompiler
{
    private const string CompilationSource = "<rule-compilation>";

    public static RuleCompilationResult Compile(AuthoringRulePack source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var diagnostics = new List<RuleDiagnostic>();
        var resources = CompileResources(source.Resources, diagnostics, out var resourceByKey);
        var reactions = CompileReactions(
            source.Reactions,
            resources,
            resourceByKey,
            diagnostics,
            out var reactionByKey);
        var founderPhenotypes = CompileFounderPhenotypes(
            source.FounderGenomes,
            reactionByKey,
            diagnostics,
            out var founderByKey);
        var scenarios = CompileScenarios(source.Scenarios, founderByKey, diagnostics);
        var registry = CompileRegistry(source.RegistryLock, diagnostics);

        if (diagnostics.Any(IsError))
        {
            return Failure(diagnostics);
        }

        var mechanicsHash = CanonicalRuleHashWriter.HashMechanics(
            source,
            resources,
            reactions,
            founderPhenotypes,
            scenarios);
        founderPhenotypes = founderPhenotypes
            .Select(phenotype => phenotype with
            {
                CanonicalCompiledHash = CanonicalRuleHashWriter.HashPhenotype(
                    mechanicsHash,
                    phenotype),
            })
            .ToImmutableArray();

        var identity = new RulePackIdentity(
            source.Manifest.PackId,
            source.Manifest.Version,
            source.Manifest.EngineRuleApiVersion,
            source.Manifest.RuleCompilerVersion,
            source.Manifest.MechanicsHashSchemaVersion,
            mechanicsHash,
            CanonicalRuleHashWriter.HashPresentation(source),
            CanonicalRuleHashWriter.HashRegistry(registry),
            CanonicalRuleHashWriter.HashCompiledArtifacts(
                source.Manifest.RuleCompilerVersion,
                resources,
                reactions,
                founderPhenotypes,
                scenarios));

        return new RuleCompilationResult(
            new CompiledRulePack(
                identity,
                registry,
                resources,
                reactions,
                founderPhenotypes,
                scenarios),
            RuleDiagnosticOrdering.Sort(diagnostics));
    }

    private static ImmutableArray<CompiledResource> CompileResources(
        IReadOnlyList<ResourceDefinition> definitions,
        ICollection<RuleDiagnostic> diagnostics,
        out Dictionary<string, ResourceHandle> resourceByKey)
    {
        resourceByKey = new Dictionary<string, ResourceHandle>(StringComparer.Ordinal);
        var resources = ImmutableArray.CreateBuilder<CompiledResource>(definitions.Count);

        foreach (var definition in definitions.OrderBy(definition => definition.NumericId))
        {
            if (definition.NumericId == 0 ||
                !TryParseBiologicalForm(definition.BiologicalForm, out var biologicalForm) ||
                !TryParseEnvironmentalPhase(definition.EnvironmentalPhase, out var environmentalPhase))
            {
                AddError(
                    "LYFE-COMPILE-RESOURCE-001",
                    $"Resource '{definition.StableKey}' has an invalid identity, biological form, or environmental phase.",
                    diagnostics);
                continue;
            }

            var composition = ImmutableArray.CreateBuilder<CompiledElementQuantity>();
            var seenElements = new HashSet<ChemicalElement>();
            foreach (var component in definition.Composition ?? [])
            {
                if (component.Quantity == 0 ||
                    !TryParseElement(component.Element, out var element) ||
                    !seenElements.Add(element))
                {
                    AddError(
                        "LYFE-COMPILE-RESOURCE-002",
                        $"Resource '{definition.StableKey}' has an unknown, zero, or duplicated elemental component '{component.Element}'.",
                        diagnostics);
                    continue;
                }

                composition.Add(new CompiledElementQuantity(element, component.Quantity));
            }

            if (composition.Count == 0)
            {
                AddError(
                    "LYFE-COMPILE-RESOURCE-003",
                    $"Resource '{definition.StableKey}' has no compilable composition.",
                    diagnostics);
                continue;
            }

            var id = ResourceId.From(definition.NumericId);
            var handle = new ResourceHandle(id, resources.Count);
            if (!resourceByKey.TryAdd(definition.StableKey, handle))
            {
                AddError(
                    "LYFE-COMPILE-RESOURCE-004",
                    $"Resource key '{definition.StableKey}' is duplicated.",
                    diagnostics);
                continue;
            }

            resources.Add(new CompiledResource(
                id,
                biologicalForm,
                environmentalPhase,
                composition.OrderBy(component => component.Element).ToImmutableArray()));
        }

        return resources.ToImmutable();
    }

    private static ImmutableArray<CompiledReaction> CompileReactions(
        IReadOnlyList<ReactionDefinition> definitions,
        ImmutableArray<CompiledResource> resources,
        IReadOnlyDictionary<string, ResourceHandle> resourceByKey,
        ICollection<RuleDiagnostic> diagnostics,
        out Dictionary<string, ReactionHandle> reactionByKey)
    {
        reactionByKey = new Dictionary<string, ReactionHandle>(StringComparer.Ordinal);
        var reactions = ImmutableArray.CreateBuilder<CompiledReaction>(definitions.Count);

        foreach (var definition in definitions.OrderBy(definition => definition.NumericId))
        {
            if (definition.NumericId == 0 ||
                !TryParseProcessKind(definition.ProcessKind, out var processKind))
            {
                AddError(
                    "LYFE-COMPILE-REACTION-001",
                    $"Reaction '{definition.StableKey}' has an invalid identity or process kind.",
                    diagnostics);
                continue;
            }

            var inputs = ResolveTerms(
                definition.StableKey,
                "input",
                definition.Inputs,
                resourceByKey,
                diagnostics);
            var outputs = ResolveTerms(
                definition.StableKey,
                "output",
                definition.Outputs,
                resourceByKey,
                diagnostics);
            if (inputs.IsEmpty || outputs.IsEmpty)
            {
                AddError(
                    "LYFE-COMPILE-REACTION-002",
                    $"Reaction '{definition.StableKey}' requires compilable inputs and outputs.",
                    diagnostics);
                continue;
            }

            var id = ReactionId.From(definition.NumericId);
            var handle = new ReactionHandle(id, reactions.Count);
            if (!reactionByKey.TryAdd(definition.StableKey, handle))
            {
                AddError(
                    "LYFE-COMPILE-REACTION-003",
                    $"Reaction key '{definition.StableKey}' is duplicated.",
                    diagnostics);
                continue;
            }

            var reaction = new CompiledReaction(
                id,
                processKind,
                inputs,
                outputs,
                definition.GrossEnergyQ,
                definition.StoredEnergyQ,
                definition.DissipatedEnergyQ);
            ValidateReactionMatterBalance(definition.StableKey, reaction, resources, diagnostics);
            reactions.Add(reaction);
        }

        return reactions.ToImmutable();
    }

    private static ImmutableArray<CompiledPhenotype> CompileFounderPhenotypes(
        IReadOnlyList<FounderGenomeDefinition> definitions,
        Dictionary<string, ReactionHandle> reactionByKey,
        ICollection<RuleDiagnostic> diagnostics,
        out Dictionary<string, FounderGenomeHandle> founderByKey)
    {
        founderByKey = new Dictionary<string, FounderGenomeHandle>(StringComparer.Ordinal);
        var phenotypes = ImmutableArray.CreateBuilder<CompiledPhenotype>(definitions.Count);

        foreach (var definition in definitions.OrderBy(definition => definition.NumericId))
        {
            if (definition.NumericId == 0)
            {
                AddError(
                    "LYFE-COMPILE-GENOME-001",
                    $"Founder genome '{definition.StableKey}' uses invalid ID zero.",
                    diagnostics);
                continue;
            }

            var reactions = new List<ReactionHandle>();
            var seenReactions = new HashSet<ReactionId>();
            foreach (var reactionKey in definition.EnabledReactionKeys ?? [])
            {
                if (!reactionByKey.TryGetValue(reactionKey, out var reaction) ||
                    !seenReactions.Add(reaction.Id))
                {
                    AddError(
                        "LYFE-COMPILE-GENOME-002",
                        $"Founder genome '{definition.StableKey}' has an unresolved or duplicated reaction '{reactionKey}'.",
                        diagnostics);
                    continue;
                }

                reactions.Add(reaction);
            }

            if (reactions.Count == 0)
            {
                AddError(
                    "LYFE-COMPILE-GENOME-003",
                    $"Founder genome '{definition.StableKey}' has no compilable reactions.",
                    diagnostics);
                continue;
            }

            var id = FounderGenomeId.From(definition.NumericId);
            var handle = new FounderGenomeHandle(id, phenotypes.Count);
            if (!founderByKey.TryAdd(definition.StableKey, handle))
            {
                AddError(
                    "LYFE-COMPILE-GENOME-004",
                    $"Founder genome key '{definition.StableKey}' is duplicated.",
                    diagnostics);
                continue;
            }

            var processes = reactions
                .OrderBy(reaction => reaction.Id.Value)
                .Select(reaction => new CompiledProcessPlan(reaction))
                .ToImmutableArray();
            phenotypes.Add(new CompiledPhenotype(id, processes, string.Empty));
        }

        return phenotypes.ToImmutable();
    }

    private static ImmutableArray<CompiledScenario> CompileScenarios(
        IReadOnlyList<ScenarioDefinition> definitions,
        Dictionary<string, FounderGenomeHandle> founderByKey,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var scenarios = ImmutableArray.CreateBuilder<CompiledScenario>(definitions.Count);

        foreach (var definition in definitions.OrderBy(definition => definition.NumericId))
        {
            if (definition.NumericId == 0 || definition.TickDurationHours == 0)
            {
                AddError(
                    "LYFE-COMPILE-SCENARIO-001",
                    $"Scenario '{definition.StableKey}' has an invalid identity or tick duration.",
                    diagnostics);
                continue;
            }

            var founders = new List<FounderGenomeHandle>();
            var seenFounders = new HashSet<FounderGenomeId>();
            foreach (var founderKey in definition.FounderGenomeKeys ?? [])
            {
                if (!founderByKey.TryGetValue(founderKey, out var founder) ||
                    !seenFounders.Add(founder.Id))
                {
                    AddError(
                        "LYFE-COMPILE-SCENARIO-002",
                        $"Scenario '{definition.StableKey}' has an unresolved or duplicated founder '{founderKey}'.",
                        diagnostics);
                    continue;
                }

                founders.Add(founder);
            }

            if (founders.Count == 0)
            {
                AddError(
                    "LYFE-COMPILE-SCENARIO-003",
                    $"Scenario '{definition.StableKey}' has no compilable founders.",
                    diagnostics);
                continue;
            }

            scenarios.Add(new CompiledScenario(
                ScenarioId.From(definition.NumericId),
                definition.TickDurationHours,
                founders.OrderBy(founder => founder.Id.Value).ToImmutableArray()));
        }

        return scenarios.ToImmutable();
    }

    private static ImmutableArray<DefinitionRegistryEntry> CompileRegistry(
        RegistryLockFile registryLock,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var entries = ImmutableArray.CreateBuilder<DefinitionRegistryEntry>();
        foreach (var registry in registryLock.Registries ?? [])
        {
            if (!TryParseDefinitionKind(registry.Kind, out var kind))
            {
                AddError(
                    "LYFE-COMPILE-REGISTRY-001",
                    $"Registry kind '{registry.Kind}' is not recognized by this compiler.",
                    diagnostics);
                continue;
            }

            foreach (var entry in registry.Entries ?? [])
            {
                if (entry.NumericId == 0 || entry.Status is not ("active" or "tombstone"))
                {
                    AddError(
                        "LYFE-COMPILE-REGISTRY-002",
                        $"Registry entry '{registry.Kind}:{entry.NumericId}:{entry.StableKey}' is invalid.",
                        diagnostics);
                    continue;
                }

                entries.Add(new DefinitionRegistryEntry(
                    kind,
                    entry.NumericId,
                    entry.StableKey,
                    entry.Status == "active",
                    entry.RemovedInVersion));
            }
        }

        return entries
            .OrderBy(entry => entry.Kind)
            .ThenBy(entry => entry.NumericId)
            .ToImmutableArray();
    }

    private static ImmutableArray<CompiledResourceTerm> ResolveTerms(
        string reactionKey,
        string role,
        ResourceQuantityDefinition[]? definitions,
        IReadOnlyDictionary<string, ResourceHandle> resourceByKey,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var terms = new List<CompiledResourceTerm>();
        var seenResources = new HashSet<ResourceId>();
        foreach (var definition in definitions ?? [])
        {
            if (definition.Quantity <= 0 ||
                !resourceByKey.TryGetValue(definition.ResourceKey, out var resource) ||
                !seenResources.Add(resource.Id))
            {
                AddError(
                    "LYFE-COMPILE-REACTION-004",
                    $"Reaction '{reactionKey}' has an invalid, unresolved, or duplicated {role} '{definition.ResourceKey}'.",
                    diagnostics);
                continue;
            }

            terms.Add(new CompiledResourceTerm(resource, definition.Quantity));
        }

        return terms.OrderBy(term => term.Resource.Id.Value).ToImmutableArray();
    }

    private static void ValidateReactionMatterBalance(
        string reactionKey,
        CompiledReaction reaction,
        ImmutableArray<CompiledResource> resources,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var balances = new Dictionary<ChemicalElement, Int128>();
        AddMatter(reaction.Inputs, 1, resources, balances);
        AddMatter(reaction.Outputs, -1, resources, balances);

        foreach (var balance in balances.OrderBy(pair => pair.Key))
        {
            if (balance.Value != 0)
            {
                AddError(
                    "LYFE-COMPILE-REACTION-005",
                    $"Reaction '{reactionKey}' is not mass-balanced for {balance.Key}: net input is {balance.Value} elemental units.",
                    diagnostics);
            }
        }
    }

    private static void AddMatter(
        ImmutableArray<CompiledResourceTerm> terms,
        int direction,
        ImmutableArray<CompiledResource> resources,
        Dictionary<ChemicalElement, Int128> balances)
    {
        foreach (var term in terms)
        {
            var resource = resources[term.Resource.DenseSlot];
            foreach (var component in resource.Composition)
            {
                var amount = (Int128)term.Quantity * component.Quantity * direction;
                balances.TryGetValue(component.Element, out var current);
                balances[component.Element] = current + amount;
            }
        }
    }

    private static bool TryParseBiologicalForm(string value, out BiologicalForm result)
    {
        result = value switch
        {
            "inorganic" => BiologicalForm.Inorganic,
            "organic" => BiologicalForm.Organic,
            "boundary" => BiologicalForm.Boundary,
            _ => default,
        };
        return result != default;
    }

    private static bool TryParseEnvironmentalPhase(string value, out EnvironmentalPhase result)
    {
        result = value switch
        {
            "gas" => EnvironmentalPhase.Gas,
            "dissolved" => EnvironmentalPhase.Dissolved,
            "boundary" => EnvironmentalPhase.Boundary,
            _ => default,
        };
        return result != default;
    }

    private static bool TryParseElement(string value, out ChemicalElement result)
    {
        result = value switch
        {
            "hydrogen" => ChemicalElement.Hydrogen,
            "carbon" => ChemicalElement.Carbon,
            "nitrogen" => ChemicalElement.Nitrogen,
            "oxygen" => ChemicalElement.Oxygen,
            "phosphorus" => ChemicalElement.Phosphorus,
            "sulfur" => ChemicalElement.Sulfur,
            _ => default,
        };
        return result != default;
    }

    private static bool TryParseProcessKind(string value, out ProcessKind result)
    {
        result = value switch
        {
            "external-energy-capture" => ProcessKind.ExternalEnergyCapture,
            _ => default,
        };
        return result != default;
    }

    private static bool TryParseDefinitionKind(string value, out DefinitionKind result)
    {
        result = value switch
        {
            "resource" => DefinitionKind.Resource,
            "reaction" => DefinitionKind.Reaction,
            "founderGenome" => DefinitionKind.FounderGenome,
            "scenario" => DefinitionKind.Scenario,
            _ => default,
        };
        return result != default;
    }

    private static void AddError(
        string code,
        string message,
        ICollection<RuleDiagnostic> diagnostics) =>
        diagnostics.Add(new RuleDiagnostic(
            RuleDiagnosticSeverity.Error,
            code,
            message,
            CompilationSource,
            1,
            1));

    private static bool IsError(RuleDiagnostic diagnostic) =>
        diagnostic.Severity == RuleDiagnosticSeverity.Error;

    private static RuleCompilationResult Failure(IEnumerable<RuleDiagnostic> diagnostics) =>
        new(null, RuleDiagnosticOrdering.Sort(diagnostics));
}

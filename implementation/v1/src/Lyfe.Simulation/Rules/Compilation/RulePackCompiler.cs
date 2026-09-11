using System.Collections.Immutable;
using Lyfe.Simulation.Evolution;
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
        var traits = CompileTraits(source.Traits, diagnostics, out var traitByKey);
        var founderAllocations = CompileFounderAllocations(
            source.FounderAllocations,
            diagnostics,
            out var founderAllocationByKey,
            out var baselineFounderAllocation);
        var founderPhenotypes = CompileFounderPhenotypes(
            source.FounderGenomes,
            resources,
            resourceByKey,
            reactionByKey,
            traits,
            traitByKey,
            baselineFounderAllocation,
            diagnostics,
            out var founderByKey);
        var scenarios = CompileScenarios(
            source.Scenarios,
            founderByKey,
            founderAllocationByKey,
            founderPhenotypes,
            diagnostics);
        var registry = CompileRegistry(source.RegistryLock, diagnostics);
        var effectivePopulationQ = MutationIncomeMath.EffectivePopulationTable;

        if (diagnostics.Any(IsError))
        {
            return Failure(diagnostics);
        }

        var mechanicsHash = CanonicalRuleHashWriter.HashMechanics(
            source,
            resources,
            reactions,
            traits,
            founderAllocations,
            founderPhenotypes,
            scenarios,
            effectivePopulationQ);
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
                traits,
                founderAllocations,
                founderPhenotypes,
                scenarios,
                effectivePopulationQ));

        return new RuleCompilationResult(
            new CompiledRulePack(
                identity,
                registry,
                resources,
                reactions,
                traits,
                founderAllocations,
                founderPhenotypes,
                scenarios,
                effectivePopulationQ),
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
                definition.StableKey,
                definition.DisplayName,
                biologicalForm,
                environmentalPhase,
                composition.OrderBy(component => component.Element).ToImmutableArray()));
        }

        return resources.ToImmutable();
    }

    private static ImmutableArray<CompiledReaction> CompileReactions(
        IReadOnlyList<ReactionDefinition> definitions,
        ImmutableArray<CompiledResource> resources,
        Dictionary<string, ResourceHandle> resourceByKey,
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
                definition.StableKey,
                definition.DisplayName,
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
        ImmutableArray<CompiledResource> resources,
        Dictionary<string, ResourceHandle> resourceByKey,
        Dictionary<string, ReactionHandle> reactionByKey,
        ImmutableArray<CompiledTrait> traits,
        Dictionary<string, TraitId> traitByKey,
        FounderAllocationHandle baselineFounderAllocation,
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

            if (!TryCompilePhysiology(
                    definition,
                    resources,
                    resourceByKey,
                    diagnostics,
                    out var physiology))
            {
                continue;
            }

            var processes = reactions
                .OrderBy(reaction => reaction.Id.Value)
                .Select(reaction => new CompiledProcessPlan(reaction))
                .ToImmutableArray();
            var acquiredTraits = new List<TraitId>();
            var seenTraits = new HashSet<TraitId>();
            foreach (var traitKey in definition.FoundationTraitKeys ?? [])
            {
                if (!traitByKey.TryGetValue(traitKey, out var traitId) || !seenTraits.Add(traitId))
                {
                    AddError(
                        "LYFE-COMPILE-GENOME-005",
                        $"Founder genome '{definition.StableKey}' has an unresolved or duplicated trait '{traitKey}'.",
                        diagnostics);
                    continue;
                }

                acquiredTraits.Add(traitId);
            }

            if (!TryApplyTraitEffects(
                    physiology,
                    acquiredTraits,
                    traits,
                    diagnostics,
                    definition.StableKey,
                    out var compiledPhysiology,
                    out var mutationModifierQ,
                    out var maximumChangeComplexity))
            {
                continue;
            }

            phenotypes.Add(new CompiledPhenotype(
                id,
                definition.StableKey,
                definition.DisplayName,
                baselineFounderAllocation.Id,
                acquiredTraits.OrderBy(trait => trait.Value).ToImmutableArray(),
                processes,
                compiledPhysiology,
                mutationModifierQ,
                maximumChangeComplexity,
                string.Empty));
        }

        return phenotypes.ToImmutable();
    }

    private static ImmutableArray<CompiledFounderAllocation> CompileFounderAllocations(
        IReadOnlyList<FounderAllocationDefinition> definitions,
        ICollection<RuleDiagnostic> diagnostics,
        out Dictionary<string, FounderAllocationHandle> allocationByKey,
        out FounderAllocationHandle baseline)
    {
        allocationByKey = new Dictionary<string, FounderAllocationHandle>(StringComparer.Ordinal);
        baseline = default;
        var allocations = ImmutableArray.CreateBuilder<CompiledFounderAllocation>(definitions.Count);

        foreach (var definition in definitions.OrderBy(definition => definition.NumericId))
        {
            if (definition.NumericId == 0 ||
                definition.CaptureEfficiencyMultiplierQ is < 250_000 or > 2_000_000 ||
                definition.ChemicalToleranceMultiplierQ is < 250_000 or > 2_000_000)
            {
                AddError(
                    "LYFE-COMPILE-ALLOCATION-001",
                    $"Founder allocation '{definition.StableKey}' has invalid fields.",
                    diagnostics);
                continue;
            }

            var id = FounderAllocationId.From(definition.NumericId);
            var handle = new FounderAllocationHandle(id, allocations.Count);
            if (!allocationByKey.TryAdd(definition.StableKey, handle))
            {
                AddError(
                    "LYFE-COMPILE-ALLOCATION-002",
                    $"Founder allocation key '{definition.StableKey}' is duplicated.",
                    diagnostics);
                continue;
            }

            if (definition.IsBaseline)
            {
                if (baseline != default)
                {
                    AddError(
                        "LYFE-COMPILE-ALLOCATION-003",
                        "Exactly one founder allocation must be the baseline.",
                        diagnostics);
                }
                baseline = handle;
            }

            allocations.Add(new CompiledFounderAllocation(
                id,
                definition.StableKey,
                definition.DisplayName,
                definition.IsBaseline,
                definition.CaptureEfficiencyMultiplierQ,
                definition.ChemicalToleranceMultiplierQ));
        }

        if (baseline == default)
        {
            AddError(
                "LYFE-COMPILE-ALLOCATION-004",
                "Exactly one founder allocation must be the baseline.",
                diagnostics);
        }

        return allocations.ToImmutable();
    }

    private static ImmutableArray<CompiledTrait> CompileTraits(
        IReadOnlyList<TraitDefinition> definitions,
        ICollection<RuleDiagnostic> diagnostics,
        out Dictionary<string, TraitId> traitByKey)
    {
        traitByKey = new Dictionary<string, TraitId>(StringComparer.Ordinal);
        foreach (var definition in definitions.OrderBy(definition => definition.NumericId))
        {
            if (definition.NumericId == 0 || !traitByKey.TryAdd(
                    definition.StableKey,
                    TraitId.From(definition.NumericId)))
            {
                AddError("LYFE-COMPILE-TRAIT-001", $"Trait '{definition.StableKey}' has an invalid or duplicate identity.", diagnostics);
            }
        }

        var result = ImmutableArray.CreateBuilder<CompiledTrait>(definitions.Count);
        foreach (var definition in definitions.OrderBy(definition => definition.NumericId))
        {
            if (!traitByKey.TryGetValue(definition.StableKey, out var id))
            {
                continue;
            }

            var prerequisites = ResolveTraitIds(definition.StableKey, definition.PrerequisiteTraitKeys, traitByKey, diagnostics);
            var incompatibilities = ResolveTraitIds(definition.StableKey, definition.IncompatibleTraitKeys, traitByKey, diagnostics);
            var pressureTags = ImmutableArray.CreateBuilder<EvolutionPressureTag>();
            foreach (var tag in definition.PressureTags ?? [])
            {
                var compiled = tag switch
                {
                    "energy-shortage" => EvolutionPressureTag.EnergyShortage,
                    "starvation" => EvolutionPressureTag.Starvation,
                    _ => default,
                };
                if (compiled == default || pressureTags.Contains(compiled))
                {
                    AddError("LYFE-COMPILE-TRAIT-002", $"Trait '{definition.StableKey}' has an invalid or duplicate pressure tag '{tag}'.", diagnostics);
                }
                else
                {
                    pressureTags.Add(compiled);
                }
            }

            var strategicIntents = ImmutableArray.CreateBuilder<EvolutionStrategicIntent>();
            foreach (var intent in definition.StrategicIntents ?? [])
            {
                var compiled = intent switch
                {
                    "exploit-current-niche" => EvolutionStrategicIntent.ExploitCurrentNiche,
                    "endure-environmental-pressure" => EvolutionStrategicIntent.EndureEnvironmentalPressure,
                    "alter-dispersal" => EvolutionStrategicIntent.AlterDispersal,
                    "diversify-resource-energy-access" => EvolutionStrategicIntent.DiversifyResourceEnergyAccess,
                    "biological-interaction" => EvolutionStrategicIntent.BiologicalInteraction,
                    "invest-in-complexity" => EvolutionStrategicIntent.InvestInComplexity,
                    _ => default,
                };
                if (compiled == default || strategicIntents.Contains(compiled))
                {
                    AddError("LYFE-COMPILE-TRAIT-004", $"Trait '{definition.StableKey}' has an invalid or duplicate strategic intent '{intent}'.", diagnostics);
                }
                else
                {
                    strategicIntents.Add(compiled);
                }
            }

            EvolutionFollowUpEvidenceKind? followUpEvidenceKind =
                definition.ConsequenceEvidenceKind switch
                {
                    null => null,
                    "capability-activation" => EvolutionFollowUpEvidenceKind.CapabilityActivation,
                    "condition-and-pressure" => EvolutionFollowUpEvidenceKind.ConditionAndPressure,
                    "geographic-spread" => EvolutionFollowUpEvidenceKind.GeographicSpread,
                    "reserve-storage" => EvolutionFollowUpEvidenceKind.ReserveStorage,
                    _ => default(EvolutionFollowUpEvidenceKind),
                };
            if (definition.ConsequenceFollowUpHours.HasValue !=
                    (definition.ConsequenceEvidenceKind is not null) ||
                definition.ConsequenceFollowUpHours is > 0 and <= 168 ||
                (definition.ConsequenceEvidenceKind is not null &&
                    (!followUpEvidenceKind.HasValue ||
                        followUpEvidenceKind.Value == (EvolutionFollowUpEvidenceKind)0)))
            {
                AddError(
                    "LYFE-COMPILE-TRAIT-005",
                    $"Trait '{definition.StableKey}' has an invalid consequence follow-up contract.",
                    diagnostics);
            }

            if (definition.MutationIncomeMultiplierQ is < 250_000 or > 3_000_000 ||
                definition.BaseEvolutionWeightQ == 0 ||
                definition.MaximumChangeComplexity is 0)
            {
                AddError("LYFE-COMPILE-TRAIT-003", $"Trait '{definition.StableKey}' has invalid compiled bounds.", diagnostics);
                continue;
            }

            result.Add(new CompiledTrait(
                id,
                definition.StableKey,
                definition.DisplayName,
                definition.Family,
                definition.Selectable,
                checked(definition.MutationPointCost * 1_000_000),
                definition.ChangeComplexity,
                prerequisites,
                incompatibilities,
                definition.BaseEvolutionWeightQ,
                pressureTags.ToImmutable(),
                strategicIntents.Order().ToImmutableArray(),
                definition.ConsequenceFollowUpHours,
                followUpEvidenceKind,
                definition.EnablesResourceConservation,
                definition.MutationIncomeMultiplierQ,
                definition.MaximumChangeComplexity));
        }

        var compiledTraits = result.ToImmutable();
        ValidateTraitGraph(compiledTraits, diagnostics);
        return compiledTraits;
    }

    private static ImmutableArray<TraitId> ResolveTraitIds(
        string ownerKey,
        IEnumerable<string>? keys,
        Dictionary<string, TraitId> traitByKey,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var result = new HashSet<TraitId>();
        foreach (var key in keys ?? [])
        {
            if (!traitByKey.TryGetValue(key, out var id) || !result.Add(id))
            {
                AddError("LYFE-COMPILE-TRAIT-004", $"Trait '{ownerKey}' has an unresolved or duplicate trait reference '{key}'.", diagnostics);
            }
        }

        return result.OrderBy(id => id.Value).ToImmutableArray();
    }

    private static void ValidateTraitGraph(
        ImmutableArray<CompiledTrait> traits,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var byId = traits.ToDictionary(trait => trait.Id);
        var visiting = new HashSet<TraitId>();
        var visited = new HashSet<TraitId>();
        bool Visit(TraitId id)
        {
            if (!visiting.Add(id))
            {
                return false;
            }
            if (visited.Contains(id))
            {
                visiting.Remove(id);
                return true;
            }
            foreach (var prerequisite in byId[id].Prerequisites)
            {
                if (!byId.ContainsKey(prerequisite) || !Visit(prerequisite))
                {
                    return false;
                }
            }
            visiting.Remove(id);
            visited.Add(id);
            return true;
        }

        foreach (var trait in traits)
        {
            if (!Visit(trait.Id))
            {
                AddError("LYFE-COMPILE-TRAIT-005", "The trait prerequisite graph contains a cycle or missing node.", diagnostics);
                break;
            }
        }
    }

    internal static bool TryApplyTraitEffects(
        CompiledOrganismPhysiology founderPhysiology,
        IEnumerable<TraitId> acquiredTraits,
        ImmutableArray<CompiledTrait> traits,
        ICollection<RuleDiagnostic> diagnostics,
        string ownerKey,
        out CompiledOrganismPhysiology physiology,
        out uint mutationIncomeModifierQ,
        out uint maximumChangeComplexity)
    {
        physiology = founderPhysiology;
        mutationIncomeModifierQ = 1_000_000;
        maximumChangeComplexity = 0;
        var byId = traits.ToDictionary(trait => trait.Id);
        var acquired = acquiredTraits.ToHashSet();
        foreach (var traitId in acquired.OrderBy(id => id.Value))
        {
            if (!byId.TryGetValue(traitId, out var trait) ||
                trait.Prerequisites.Any(prerequisite => !acquired.Contains(prerequisite)) ||
                trait.Incompatibilities.Any(acquired.Contains))
            {
                AddError("LYFE-COMPILE-TRAIT-006", $"Genome '{ownerKey}' has an invalid acquired-trait closure.", diagnostics);
                return false;
            }

            if (trait.EnablesResourceConservation)
            {
                physiology = physiology with
                {
                    Behavior = physiology.Behavior with { ResourceConservation = true },
                };
            }
            mutationIncomeModifierQ = checked((uint)FixedMultiply(mutationIncomeModifierQ, trait.MutationIncomeMultiplierQ));
            if (trait.MaximumChangeComplexity.HasValue)
            {
                maximumChangeComplexity = Math.Max(maximumChangeComplexity, trait.MaximumChangeComplexity.Value);
            }
        }

        if (mutationIncomeModifierQ is < 250_000 or > 3_000_000 || maximumChangeComplexity == 0)
        {
            AddError("LYFE-COMPILE-TRAIT-007", $"Genome '{ownerKey}' has invalid evolution capabilities.", diagnostics);
            return false;
        }

        return true;
    }

    private static long FixedMultiply(uint left, uint right)
    {
        var numerator = (long)left * right;
        var quotient = numerator / 1_000_000;
        var remainder = numerator % 1_000_000;
        if (remainder > 500_000 || (remainder == 500_000 && (quotient & 1) != 0))
        {
            quotient++;
        }
        return quotient;
    }

    private static bool TryCompilePhysiology(
        FounderGenomeDefinition founder,
        ImmutableArray<CompiledResource> resources,
        Dictionary<string, ResourceHandle> resourceByKey,
        ICollection<RuleDiagnostic> diagnostics,
        out CompiledOrganismPhysiology physiology)
    {
        physiology = null!;
        var value = founder.Physiology;
        var opening = value?.OpeningMetabolism;
        var metabolismKind = opening?.MetabolismKind switch
        {
            "hydrogen-acetogenesis" => FoundingMetabolismKind.HydrogenAcetogenesis,
            "sulfide-anoxygenic-phototrophy" =>
                FoundingMetabolismKind.SulfideAnoxygenicPhototrophy,
            _ => default,
        };
        if (value is null ||
            opening is null ||
            metabolismKind == default ||
            opening.MaximumCaptureExtentsPerHour == 0 ||
            opening.FavorableCaptureEfficiencyQ is 0 or > 1_000_000 ||
            opening.IlluminatedHoursPerDay is 0 or > 24 ||
            (opening.RequiresLight && opening.GeneratedLightCaptureExtentsPerUnitHour == 0) ||
            (!opening.RequiresLight && opening.GeneratedLightCaptureExtentsPerUnitHour != 0) ||
            opening.StructuralGrowthExtentsPerHour == 0 ||
            opening.MaintenanceCostQPerHour < 0 ||
            opening.GrowthReserveFloorQ < 0 ||
            opening.GrowthReserveFloorQ >= value.ChargedReserveCapacityQ ||
            (metabolismKind == FoundingMetabolismKind.HydrogenAcetogenesis &&
                (opening.RequiresLight || opening.IlluminatedHoursPerDay != 24)) ||
            (metabolismKind == FoundingMetabolismKind.SulfideAnoxygenicPhototrophy &&
                !opening.RequiresLight) ||
            value.MatureStructureQ <= 0 ||
            value.StructuralHardFloorQ <= 0 ||
            value.StructuralHardFloorQ >= value.MatureStructureQ ||
            value.ChargedReserveCapacityQ <= 0 ||
            value.TerminalReserveThresholdQ < 0 ||
            value.TerminalReserveThresholdQ >= value.ChargedReserveCapacityQ ||
            value.DissolvedMacronutrientCapacityLoadQ < 0 ||
            value.FreeMicronutrientCapacityLoadQ < 0 ||
            value.PassiveMicronutrientUptakePerMillionPerHour > 1_000_000 ||
            value.CommittedMicronutrientQuotas is null ||
            value.IngestedMatterCapacityLoadQ < 0 ||
            value.SenescenceOnsetHours == 0 ||
            value.AgeDeclineSpanHours == 0 ||
            value.MinimumAgeFactorQ > 1_000_000 ||
            value.SenescenceRiskBaseQ > value.SenescenceRiskCapQ ||
            value.SenescenceRiskCapQ > 1_000_000 ||
            value.SenescenceRiskEscalationHours == 0 ||
            value.Reproduction is null ||
            value.Reproduction.MinimumHealthQ > 1_000_000 ||
            value.Reproduction.RequiredStructureQ <= 0 ||
            value.Reproduction.ResultStructureMinimumQ <= 0 ||
            value.Reproduction.RequiredStructureQ <
                checked(value.Reproduction.ResultStructureMinimumQ * 2) ||
            value.Reproduction.RequiredReserveQ <= 0 ||
            value.Reproduction.RequiredReserveQ > value.ChargedReserveCapacityQ ||
            value.Reproduction.ResultReserveMinimumQ < 0 ||
            value.Reproduction.WorkCostQ < 0 ||
            value.Reproduction.RequiredReserveQ <
                checked(value.Reproduction.ResultReserveMinimumQ * 2 +
                    value.Reproduction.WorkCostQ) ||
            value.Reproduction.BaseCooldownHours == 0 ||
            value.Recycling is null ||
            value.Recycling.ReserveDecayPerMillionPerHour > 1_000_000 ||
            value.Recycling.StructureDecayPerMillionPerHour > 1_000_000 ||
            value.Recycling.ScavengeReserveCapQ < 0 ||
            value.Recycling.ScavengeActionCostQ < 0 ||
            value.Recycling.ParticulateScavengeActionCostQ < 0 ||
            value.Recycling.ScavengeCooldownHours == 0 ||
            value.Spatial is null ||
            value.Spatial.MatureBodyRadiusQ == 0 ||
            value.Spatial.GeometricStructureTargetQ <= 0 ||
            value.Spatial.MinimumBodyRadiusQ == 0 ||
            value.Spatial.MinimumBodyRadiusQ > value.Spatial.MatureBodyRadiusQ ||
            value.Spatial.BrownianRmsQPerSqrtHour == 0 ||
            value.Spatial.EnvironmentalSpreadMultiplierQ > 4_000_000 ||
            value.Spatial.TerrestrialBrownianMultiplierQ > 1_000_000 ||
            value.Spatial.PassiveMigrationProbabilityQ > 1_000_000 ||
            value.Spatial.ActiveMigrationProbabilityQ > 1_000_000 ||
            value.Spatial.PassiveMigrationProbabilityQ >
                value.Spatial.ActiveMigrationProbabilityQ ||
            value.Spatial.MediumTransitionFactorQ > 1_000_000 ||
            value.Spatial.DestinationCompatibilityFloorQ > 1_000_000 ||
            value.Behavior is null ||
            value.Behavior.MinimumDwellHours is 0 or > 8_760 ||
            value.Behavior.ConservationEnterReserveQ > 1_000_000 ||
            value.Behavior.ConservationEnterConditionalReserveQ > 1_000_000 ||
            value.Behavior.ConservationEnterReserveQ >
                value.Behavior.ConservationEnterConditionalReserveQ ||
            value.Behavior.ConservationEnterEnergyCoverageQ > 2_000_000 ||
            value.Behavior.ConservationCriticalReserveQ >
                value.Behavior.ConservationEnterReserveQ ||
            value.Behavior.ConservationEnterReserveQ >=
                value.Behavior.ConservationExitReserveQ ||
            value.Behavior.ConservationExitReserveQ >
                value.Behavior.ConservationExitHighReserveQ ||
            value.Behavior.ConservationExitHighReserveQ > 1_000_000 ||
            value.Behavior.ConservationExitEnergyCoverageQ > 2_000_000 ||
            value.Behavior.ConservationEnterEnergyCoverageQ >
                value.Behavior.ConservationExitEnergyCoverageQ ||
            !string.Equals(value.AllocationPolicy, "equal-shared", StringComparison.Ordinal))
        {
            AddError(
                "LYFE-COMPILE-GENOME-005",
                $"Founder genome '{founder.StableKey}' has invalid physiology capacities, age, behavior, or allocation policy.",
                diagnostics);
            return false;
        }

        var quotaTerms = ImmutableArray.CreateBuilder<CompiledResourceTerm>();
        var seenQuotaResources = new HashSet<ResourceId>();
        long quotaLoad = 0;
        foreach (var quota in value.CommittedMicronutrientQuotas)
        {
            if (quota.Quantity <= 0 ||
                !resourceByKey.TryGetValue(quota.ResourceKey, out var handle) ||
                handle.Id.Value < Physiology.MicronutrientInventory.FirstResourceId ||
                handle.Id.Value >= Physiology.MicronutrientInventory.FirstResourceId +
                    Physiology.MicronutrientInventory.Count ||
                !seenQuotaResources.Add(handle.Id))
            {
                AddError(
                    "LYFE-COMPILE-GENOME-008",
                    $"Founder genome '{founder.StableKey}' has an invalid micronutrient quota.",
                    diagnostics);
                return false;
            }
            var resource = resources[handle.DenseSlot];
            if (resource.Composition.Length != 1 || resource.Composition[0].Quantity != 1)
            {
                AddError(
                    "LYFE-COMPILE-GENOME-009",
                    $"Founder genome '{founder.StableKey}' micronutrients must be single-form unit resources.",
                    diagnostics);
                return false;
            }
            quotaLoad = checked(quotaLoad + quota.Quantity);
            quotaTerms.Add(new CompiledResourceTerm(handle, quota.Quantity));
        }
        if (quotaLoad == 0 || quotaLoad > value.FreeMicronutrientCapacityLoadQ)
        {
            AddError(
                "LYFE-COMPILE-GENOME-010",
                $"Founder genome '{founder.StableKey}' reproduction quota does not fit its free store.",
                diagnostics);
            return false;
        }

        var temperature = value.TemperatureResponse;
        if (temperature is null ||
            temperature.HardMinimumMilliC >= temperature.PreferredMinimumMilliC ||
            temperature.PreferredMinimumMilliC > temperature.PreferredMaximumMilliC ||
            temperature.PreferredMaximumMilliC >= temperature.HardMaximumMilliC ||
            temperature.HealthPenaltyAtHardQ > 1_000_000 ||
            temperature.HealthFactorFloorQ > 1_000_000 ||
            temperature.DeathChanceAtHardQ > temperature.DeathChanceCapQ ||
            temperature.DeathChanceCapQ > 1_000_000)
        {
            AddError(
                "LYFE-COMPILE-GENOME-006",
                $"Founder genome '{founder.StableKey}' has an invalid temperature response.",
                diagnostics);
            return false;
        }

        var chemical = value.ChemicalResponse;
        if (chemical is null ||
            chemical.HydrogenSulfideSoftThresholdQ < 0 ||
            chemical.HydrogenSulfideSoftThresholdQ >=
                chemical.HydrogenSulfideHardThresholdQ ||
            chemical.SulfurDioxideSoftThresholdQ < 0 ||
            chemical.SulfurDioxideSoftThresholdQ >=
                chemical.SulfurDioxideHardThresholdQ ||
            chemical.HealthPenaltyAtHardQ > 1_000_000 ||
            chemical.HealthFactorFloorQ > 1_000_000 ||
            chemical.DeathChanceAtHardQ > chemical.DeathChanceCapQ ||
            chemical.DeathChanceCapQ > 1_000_000)
        {
            AddError(
                "LYFE-COMPILE-GENOME-007",
                $"Founder genome '{founder.StableKey}' has an invalid chemical response.",
                diagnostics);
            return false;
        }

        physiology = new CompiledOrganismPhysiology(
            new CompiledOpeningMetabolismProfile(
                metabolismKind,
                opening.MaximumCaptureExtentsPerHour,
                opening.FavorableCaptureEfficiencyQ,
                opening.RequiresLight,
                opening.IlluminatedHoursPerDay,
                opening.GeneratedLightCaptureExtentsPerUnitHour,
                opening.StructuralGrowthExtentsPerHour,
                opening.MaintenanceCostQPerHour,
                opening.GrowthReserveFloorQ),
            value.MatureStructureQ,
            value.StructuralHardFloorQ,
            value.ChargedReserveCapacityQ,
            value.TerminalReserveThresholdQ,
            value.DissolvedMacronutrientCapacityLoadQ,
            value.FreeMicronutrientCapacityLoadQ,
            value.PassiveMicronutrientUptakePerMillionPerHour,
            quotaTerms.OrderBy(term => term.Resource.Id.Value).ToImmutableArray(),
            value.IngestedMatterCapacityLoadQ,
            ProcessAllocationPolicy.EqualShared,
            value.SenescenceOnsetHours,
            value.AgeDeclineSpanHours,
            value.MinimumAgeFactorQ,
            value.SenescenceRiskBaseQ,
            value.SenescenceRiskEscalationHours,
            value.SenescenceRiskCapQ,
            new CompiledReproductionProfile(
                value.Reproduction.MinimumHealthQ,
                value.Reproduction.RequiredStructureQ,
                value.Reproduction.ResultStructureMinimumQ,
                value.Reproduction.RequiredReserveQ,
                value.Reproduction.ResultReserveMinimumQ,
                value.Reproduction.WorkCostQ,
                value.Reproduction.BaseCooldownHours,
                value.Reproduction.CooldownJitterMaximumHours),
            new CompiledRecyclingProfile(
                value.Recycling.ReserveDecayPerMillionPerHour,
                value.Recycling.StructureDecayPerMillionPerHour,
                value.Recycling.SimpleRemnantScavenging,
                value.Recycling.ScavengeReserveCapQ,
                value.Recycling.ScavengeActionCostQ,
                value.Recycling.ParticulateScavengeActionCostQ,
                value.Recycling.ScavengeCooldownHours,
                value.Recycling.ScavengeRangeQ),
            new CompiledSpatialProfile(
                value.Spatial.MatureBodyRadiusQ,
                value.Spatial.GeometricStructureTargetQ,
                value.Spatial.MinimumBodyRadiusQ,
                value.Spatial.BrownianRmsQPerSqrtHour,
                value.Spatial.EnvironmentalSpreadMultiplierQ,
                value.Spatial.TerrestrialBrownianMultiplierQ,
                value.Spatial.ActiveSpeedLimitQPerHour,
                value.Spatial.MovementEnergyPerFounderRadiusQ,
                value.Spatial.CanOccupyTerrestrial,
                value.Spatial.PassiveMigrationProbabilityQ,
                value.Spatial.ActiveMigrationProbabilityQ,
                value.Spatial.MediumTransitionFactorQ,
                value.Spatial.DestinationCompatibilityFloorQ),
            new CompiledBehaviorProfile(
                value.Behavior.ResourceConservation,
                value.Behavior.MinimumDwellHours,
                value.Behavior.ConservationEnterReserveQ,
                value.Behavior.ConservationEnterConditionalReserveQ,
                value.Behavior.ConservationEnterEnergyCoverageQ,
                value.Behavior.ConservationCriticalReserveQ,
                value.Behavior.ConservationExitReserveQ,
                value.Behavior.ConservationExitEnergyCoverageQ,
                value.Behavior.ConservationExitHighReserveQ),
            new CompiledChemicalResponse(
                chemical.HydrogenSulfideSoftThresholdQ,
                chemical.HydrogenSulfideHardThresholdQ,
                chemical.SulfurDioxideSoftThresholdQ,
                chemical.SulfurDioxideHardThresholdQ,
                chemical.HealthPenaltyAtHardQ,
                chemical.HealthFactorFloorQ,
                chemical.DeathChanceAtHardQ,
                chemical.DeathChanceCapQ),
            new CompiledTemperatureResponse(
                temperature.PreferredMinimumMilliC,
                temperature.PreferredMaximumMilliC,
                temperature.HardMinimumMilliC,
                temperature.HardMaximumMilliC,
                temperature.HealthPenaltyAtHardQ,
                temperature.HealthFactorFloorQ,
                temperature.DeathChanceAtHardQ,
                temperature.DeathChanceCapQ));
        return true;
    }

    private static ImmutableArray<CompiledScenario> CompileScenarios(
        IReadOnlyList<ScenarioDefinition> definitions,
        Dictionary<string, FounderGenomeHandle> founderByKey,
        Dictionary<string, FounderAllocationHandle> founderAllocationByKey,
        ImmutableArray<CompiledPhenotype> founderPhenotypes,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var scenarios = ImmutableArray.CreateBuilder<CompiledScenario>(definitions.Count);
        var compiledFounderById = founderPhenotypes.ToDictionary(
            phenotype => phenotype.FounderGenomeId);

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

            var allocations = new List<FounderAllocationHandle>();
            var seenAllocations = new HashSet<FounderAllocationId>();
            foreach (var allocationKey in definition.FounderAllocationKeys ?? [])
            {
                if (!founderAllocationByKey.TryGetValue(allocationKey, out var allocation) ||
                    !seenAllocations.Add(allocation.Id))
                {
                    AddError(
                        "LYFE-COMPILE-SCENARIO-004",
                        $"Scenario '{definition.StableKey}' has an unresolved or duplicated founder allocation '{allocationKey}'.",
                        diagnostics);
                    continue;
                }
                allocations.Add(allocation);
            }

            if (allocations.Count == 0 ||
                !founderAllocationByKey.TryGetValue(
                    definition.DefaultCompetitorFounderAllocationKey,
                    out var defaultCompetitorAllocation) ||
                !seenAllocations.Contains(defaultCompetitorAllocation.Id))
            {
                AddError(
                    "LYFE-COMPILE-SCENARIO-005",
                    $"Scenario '{definition.StableKey}' has no allocations or an invalid default competitor allocation.",
                    diagnostics);
                continue;
            }

            var initialization = definition.FounderInitialization;
            if (initialization is null ||
                initialization.BiologicalAgeMaximumHours <
                    initialization.BiologicalAgeMinimumHours ||
                initialization.ReproductionReadinessMaximumHours <
                    initialization.ReproductionReadinessMinimumHours)
            {
                AddError(
                    "LYFE-COMPILE-SCENARIO-006",
                    $"Scenario '{definition.StableKey}' has invalid founder initialization ranges.",
                    diagnostics);
                continue;
            }
            if (founders.Any(founder =>
                    compiledFounderById.TryGetValue(founder.Id, out var phenotype) &&
                    initialization.BiologicalAgeMaximumHours >=
                    phenotype.Physiology.SenescenceOnsetHours))
            {
                AddError(
                    "LYFE-COMPILE-SCENARIO-007",
                    $"Scenario '{definition.StableKey}' can initialize a founder at or beyond senescence onset.",
                    diagnostics);
                continue;
            }

            scenarios.Add(new CompiledScenario(
                ScenarioId.From(definition.NumericId),
                definition.StableKey,
                definition.DisplayName,
                definition.TickDurationHours,
                founders.OrderBy(founder => founder.Id.Value).ToImmutableArray(),
                allocations.OrderBy(allocation => allocation.Id.Value).ToImmutableArray(),
                defaultCompetitorAllocation,
                new CompiledFounderInitializationProfile(
                    initialization.BiologicalAgeMinimumHours,
                    initialization.BiologicalAgeMaximumHours,
                    initialization.ReproductionReadinessMinimumHours,
                    initialization.ReproductionReadinessMaximumHours)));
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
        Dictionary<string, ResourceHandle> resourceByKey,
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
            "particulate" => EnvironmentalPhase.Particulate,
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
            "fluorine" => ChemicalElement.Fluorine,
            "sodium" => ChemicalElement.Sodium,
            "magnesium" => ChemicalElement.Magnesium,
            "phosphorus" => ChemicalElement.Phosphorus,
            "sulfur" => ChemicalElement.Sulfur,
            "potassium" => ChemicalElement.Potassium,
            "calcium" => ChemicalElement.Calcium,
            "manganese" => ChemicalElement.Manganese,
            "iron" => ChemicalElement.Iron,
            "cobalt" => ChemicalElement.Cobalt,
            "nickel" => ChemicalElement.Nickel,
            "copper" => ChemicalElement.Copper,
            "zinc" => ChemicalElement.Zinc,
            "selenium" => ChemicalElement.Selenium,
            "molybdenum" => ChemicalElement.Molybdenum,
            "iodine" => ChemicalElement.Iodine,
            _ => default,
        };
        return result != default;
    }

    private static bool TryParseProcessKind(string value, out ProcessKind result)
    {
        result = value switch
        {
            "external-energy-capture" => ProcessKind.ExternalEnergyCapture,
            "particulate-digestion" => ProcessKind.ParticulateDigestion,
            "biomass-assembly" => ProcessKind.BiomassAssembly,
            "mandatory-maintenance" => ProcessKind.MandatoryMaintenance,
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
            "trait" => DefinitionKind.Trait,
            "founderAllocation" => DefinitionKind.FounderAllocation,
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

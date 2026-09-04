using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Identity;

namespace Lyfe.Simulation.Rules.Runtime;

public sealed record RulePackIdentity(
    string PackId,
    string DeclaredVersion,
    uint EngineRuleApiVersion,
    uint RuleCompilerVersion,
    uint MechanicsHashSchemaVersion,
    string MechanicsHash,
    string PresentationHash,
    string RegistryManifestHash,
    string CompiledArtifactHash);

public sealed record DefinitionRegistryEntry(
    DefinitionKind Kind,
    uint NumericId,
    string StableKey,
    bool IsActive,
    string? RemovedInVersion);

public readonly record struct CompiledElementQuantity(
    ChemicalElement Element,
    uint Quantity);

public sealed record CompiledResource(
    ResourceId Id,
    BiologicalForm BiologicalForm,
    EnvironmentalPhase EnvironmentalPhase,
    ImmutableArray<CompiledElementQuantity> Composition);

public readonly record struct CompiledResourceTerm(
    ResourceHandle Resource,
    long Quantity);

public sealed record CompiledReaction(
    ReactionId Id,
    ProcessKind ProcessKind,
    ImmutableArray<CompiledResourceTerm> Inputs,
    ImmutableArray<CompiledResourceTerm> Outputs,
    long GrossEnergyQ,
    long StoredEnergyQ,
    long DissipatedEnergyQ);

public readonly record struct CompiledProcessPlan(ReactionHandle Reaction);

public sealed record CompiledPhenotype(
    FounderGenomeId FounderGenomeId,
    ImmutableArray<CompiledProcessPlan> Processes,
    string CanonicalCompiledHash);

public sealed record CompiledScenario(
    ScenarioId Id,
    uint TickDurationHours,
    ImmutableArray<FounderGenomeHandle> PermittedFounders);

public sealed class CompiledRulePack
{
    internal CompiledRulePack(
        RulePackIdentity identity,
        ImmutableArray<DefinitionRegistryEntry> definitionRegistry,
        ImmutableArray<CompiledResource> resources,
        ImmutableArray<CompiledReaction> reactions,
        ImmutableArray<CompiledPhenotype> founderPhenotypes,
        ImmutableArray<CompiledScenario> scenarios)
    {
        Identity = identity;
        DefinitionRegistry = definitionRegistry;
        Resources = resources;
        Reactions = reactions;
        FounderPhenotypes = founderPhenotypes;
        Scenarios = scenarios;
    }

    public RulePackIdentity Identity { get; }

    public ImmutableArray<DefinitionRegistryEntry> DefinitionRegistry { get; }

    public ImmutableArray<CompiledResource> Resources { get; }

    public ImmutableArray<CompiledReaction> Reactions { get; }

    public ImmutableArray<CompiledPhenotype> FounderPhenotypes { get; }

    public ImmutableArray<CompiledScenario> Scenarios { get; }
}

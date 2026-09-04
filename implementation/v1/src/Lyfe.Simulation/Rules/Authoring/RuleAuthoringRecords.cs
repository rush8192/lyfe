using System.Text.Json.Serialization;

namespace Lyfe.Simulation.Rules.Authoring;

public sealed record RulePackManifest
{
    public required string PackId { get; init; }

    public required string Version { get; init; }

    public required uint EngineRuleApiVersion { get; init; }

    public required uint RuleCompilerVersion { get; init; }

    public required uint MechanicsHashSchemaVersion { get; init; }

    public required string RegistryLockFile { get; init; }

    public required string[] IncludedFiles { get; init; }

    public required string[] RequiredFixtureIds { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ResourceDefinitionsDocument), "resources")]
[JsonDerivedType(typeof(ReactionDefinitionsDocument), "reactions")]
[JsonDerivedType(typeof(FounderGenomeDefinitionsDocument), "founderGenomes")]
[JsonDerivedType(typeof(ScenarioDefinitionsDocument), "scenarios")]
public abstract record RuleSourceDocument;

public sealed record ResourceDefinitionsDocument : RuleSourceDocument
{
    public required ResourceDefinition[] Resources { get; init; }
}

public sealed record ResourceDefinition
{
    public required uint NumericId { get; init; }

    public required string StableKey { get; init; }

    public required string DisplayName { get; init; }

    public required string BiologicalForm { get; init; }

    public required string EnvironmentalPhase { get; init; }

    public required ElementQuantityDefinition[] Composition { get; init; }
}

public sealed record ElementQuantityDefinition
{
    public required string Element { get; init; }

    public required uint Quantity { get; init; }
}

public sealed record ReactionDefinitionsDocument : RuleSourceDocument
{
    public required ReactionDefinition[] Reactions { get; init; }
}

public sealed record ReactionDefinition
{
    public required uint NumericId { get; init; }

    public required string StableKey { get; init; }

    public required string DisplayName { get; init; }

    public required string ProcessKind { get; init; }

    public required ResourceQuantityDefinition[] Inputs { get; init; }

    public required ResourceQuantityDefinition[] Outputs { get; init; }

    public required long GrossEnergyQ { get; init; }

    public required long StoredEnergyQ { get; init; }

    public required long DissipatedEnergyQ { get; init; }
}

public sealed record ResourceQuantityDefinition
{
    public required string ResourceKey { get; init; }

    public required long Quantity { get; init; }
}

public sealed record FounderGenomeDefinitionsDocument : RuleSourceDocument
{
    public required FounderGenomeDefinition[] FounderGenomes { get; init; }
}

public sealed record FounderGenomeDefinition
{
    public required uint NumericId { get; init; }

    public required string StableKey { get; init; }

    public required string DisplayName { get; init; }

    public required string[] EnabledReactionKeys { get; init; }
}

public sealed record ScenarioDefinitionsDocument : RuleSourceDocument
{
    public required ScenarioDefinition[] Scenarios { get; init; }
}

public sealed record ScenarioDefinition
{
    public required uint NumericId { get; init; }

    public required string StableKey { get; init; }

    public required string DisplayName { get; init; }

    public required uint TickDurationHours { get; init; }

    public required string[] FounderGenomeKeys { get; init; }
}

public sealed record RegistryLockFile
{
    public required uint SchemaVersion { get; init; }

    public required RegistryLockDefinition[] Registries { get; init; }
}

public sealed record RegistryLockDefinition
{
    public required string Kind { get; init; }

    public required RegistryLockEntry[] Entries { get; init; }
}

public sealed record RegistryLockEntry
{
    public required uint NumericId { get; init; }

    public required string StableKey { get; init; }

    public required string Status { get; init; }

    public string? RemovedInVersion { get; init; }
}

public sealed record AuthoringRulePack(
    RulePackManifest Manifest,
    RegistryLockFile RegistryLock,
    IReadOnlyList<ResourceDefinition> Resources,
    IReadOnlyList<ReactionDefinition> Reactions,
    IReadOnlyList<FounderGenomeDefinition> FounderGenomes,
    IReadOnlyList<ScenarioDefinition> Scenarios);


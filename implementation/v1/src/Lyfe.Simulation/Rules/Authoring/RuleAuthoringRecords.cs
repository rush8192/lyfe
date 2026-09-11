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
[JsonDerivedType(typeof(FounderAllocationDefinitionsDocument), "founderAllocations")]
[JsonDerivedType(typeof(TraitDefinitionsDocument), "traits")]
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

    public required string[] FoundationTraitKeys { get; init; }

    public required FounderPhysiologyDefinition Physiology { get; init; }
}

public sealed record FounderAllocationDefinitionsDocument : RuleSourceDocument
{
    public required FounderAllocationDefinition[] FounderAllocations { get; init; }
}

public sealed record FounderAllocationDefinition
{
    public required uint NumericId { get; init; }

    public required string StableKey { get; init; }

    public required string DisplayName { get; init; }

    public required bool IsBaseline { get; init; }

    public required uint CaptureEfficiencyMultiplierQ { get; init; }

    public required uint ChemicalToleranceMultiplierQ { get; init; }
}

public sealed record TraitDefinitionsDocument : RuleSourceDocument
{
    public required TraitDefinition[] Traits { get; init; }
}

public sealed record TraitDefinition
{
    public required uint NumericId { get; init; }

    public required string StableKey { get; init; }

    public required string DisplayName { get; init; }

    public required string Family { get; init; }

    public required bool Selectable { get; init; }

    public required long MutationPointCost { get; init; }

    public required uint ChangeComplexity { get; init; }

    public required string[] PrerequisiteTraitKeys { get; init; }

    public required string[] IncompatibleTraitKeys { get; init; }

    public required uint BaseEvolutionWeightQ { get; init; }

    public required string[] PressureTags { get; init; }

    public required string[] StrategicIntents { get; init; }

    public ulong? ConsequenceFollowUpHours { get; init; }

    public string? ConsequenceEvidenceKind { get; init; }

    public required bool EnablesResourceConservation { get; init; }

    public required uint MutationIncomeMultiplierQ { get; init; }

    public uint? MaximumChangeComplexity { get; init; }
}

public sealed record FounderPhysiologyDefinition
{
    public required OpeningMetabolismDefinition OpeningMetabolism { get; init; }

    public required long MatureStructureQ { get; init; }

    public required long StructuralHardFloorQ { get; init; }

    public required long ChargedReserveCapacityQ { get; init; }

    public required long TerminalReserveThresholdQ { get; init; }

    public required long DissolvedMacronutrientCapacityLoadQ { get; init; }

    public required long FreeMicronutrientCapacityLoadQ { get; init; }

    public required uint PassiveMicronutrientUptakePerMillionPerHour { get; init; }

    public required ResourceQuantityDefinition[] CommittedMicronutrientQuotas { get; init; }

    public required long IngestedMatterCapacityLoadQ { get; init; }

    public required string AllocationPolicy { get; init; }

    public required ulong SenescenceOnsetHours { get; init; }

    public required ulong AgeDeclineSpanHours { get; init; }

    public required uint MinimumAgeFactorQ { get; init; }

    public required uint SenescenceRiskBaseQ { get; init; }

    public required ulong SenescenceRiskEscalationHours { get; init; }

    public required uint SenescenceRiskCapQ { get; init; }

    public required ReproductionProfileDefinition Reproduction { get; init; }

    public required RecyclingProfileDefinition Recycling { get; init; }

    public required SpatialProfileDefinition Spatial { get; init; }

    public required BehaviorProfileDefinition Behavior { get; init; }

    public required TemperatureResponseDefinition TemperatureResponse { get; init; }

    public required ChemicalResponseDefinition ChemicalResponse { get; init; }
}

public sealed record OpeningMetabolismDefinition
{
    public required string MetabolismKind { get; init; }

    public required uint MaximumCaptureExtentsPerHour { get; init; }

    public required uint FavorableCaptureEfficiencyQ { get; init; }

    public required bool RequiresLight { get; init; }

    public required uint IlluminatedHoursPerDay { get; init; }

    public required uint GeneratedLightCaptureExtentsPerUnitHour { get; init; }

    public required uint StructuralGrowthExtentsPerHour { get; init; }

    public required long MaintenanceCostQPerHour { get; init; }

    public required long GrowthReserveFloorQ { get; init; }
}

public sealed record ChemicalResponseDefinition
{
    public required long HydrogenSulfideSoftThresholdQ { get; init; }

    public required long HydrogenSulfideHardThresholdQ { get; init; }

    public required long SulfurDioxideSoftThresholdQ { get; init; }

    public required long SulfurDioxideHardThresholdQ { get; init; }

    public required uint HealthPenaltyAtHardQ { get; init; }

    public required uint HealthFactorFloorQ { get; init; }

    public required uint DeathChanceAtHardQ { get; init; }

    public required uint DeathChanceCapQ { get; init; }
}

public sealed record BehaviorProfileDefinition
{
    public required bool ResourceConservation { get; init; }

    public required ulong MinimumDwellHours { get; init; }

    public required uint ConservationEnterReserveQ { get; init; }

    public required uint ConservationEnterConditionalReserveQ { get; init; }

    public required uint ConservationEnterEnergyCoverageQ { get; init; }

    public required uint ConservationCriticalReserveQ { get; init; }

    public required uint ConservationExitReserveQ { get; init; }

    public required uint ConservationExitEnergyCoverageQ { get; init; }

    public required uint ConservationExitHighReserveQ { get; init; }
}

public sealed record SpatialProfileDefinition
{
    public required uint MatureBodyRadiusQ { get; init; }

    public required long GeometricStructureTargetQ { get; init; }

    public required uint MinimumBodyRadiusQ { get; init; }

    public required uint BrownianRmsQPerSqrtHour { get; init; }

    public required uint EnvironmentalSpreadMultiplierQ { get; init; }

    public required uint TerrestrialBrownianMultiplierQ { get; init; }

    public required uint ActiveSpeedLimitQPerHour { get; init; }

    public required uint MovementEnergyPerFounderRadiusQ { get; init; }

    public required bool CanOccupyTerrestrial { get; init; }

    public required uint PassiveMigrationProbabilityQ { get; init; }

    public required uint ActiveMigrationProbabilityQ { get; init; }

    public required uint MediumTransitionFactorQ { get; init; }

    public required uint DestinationCompatibilityFloorQ { get; init; }
}

public sealed record ReproductionProfileDefinition
{
    public required uint MinimumHealthQ { get; init; }

    public required long RequiredStructureQ { get; init; }

    public required long ResultStructureMinimumQ { get; init; }

    public required long RequiredReserveQ { get; init; }

    public required long ResultReserveMinimumQ { get; init; }

    public required long WorkCostQ { get; init; }

    public required ulong BaseCooldownHours { get; init; }

    public required uint CooldownJitterMaximumHours { get; init; }
}

public sealed record RecyclingProfileDefinition
{
    public required uint ReserveDecayPerMillionPerHour { get; init; }

    public required uint StructureDecayPerMillionPerHour { get; init; }

    public required bool SimpleRemnantScavenging { get; init; }

    public required long ScavengeReserveCapQ { get; init; }

    public required long ScavengeActionCostQ { get; init; }

    public required long ParticulateScavengeActionCostQ { get; init; }

    public required ulong ScavengeCooldownHours { get; init; }

    public required uint ScavengeRangeQ { get; init; }
}

public sealed record TemperatureResponseDefinition
{
    public required int PreferredMinimumMilliC { get; init; }

    public required int PreferredMaximumMilliC { get; init; }

    public required int HardMinimumMilliC { get; init; }

    public required int HardMaximumMilliC { get; init; }

    public required uint HealthPenaltyAtHardQ { get; init; }

    public required uint HealthFactorFloorQ { get; init; }

    public required uint DeathChanceAtHardQ { get; init; }

    public required uint DeathChanceCapQ { get; init; }
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

    public required string[] FounderAllocationKeys { get; init; }

    public required string DefaultCompetitorFounderAllocationKey { get; init; }

    public required FounderInitializationDefinition FounderInitialization { get; init; }
}

public sealed record FounderInitializationDefinition
{
    public required ulong BiologicalAgeMinimumHours { get; init; }

    public required ulong BiologicalAgeMaximumHours { get; init; }

    public required ulong ReproductionReadinessMinimumHours { get; init; }

    public required ulong ReproductionReadinessMaximumHours { get; init; }
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
    IReadOnlyList<TraitDefinition> Traits,
    IReadOnlyList<FounderGenomeDefinition> FounderGenomes,
    IReadOnlyList<FounderAllocationDefinition> FounderAllocations,
    IReadOnlyList<ScenarioDefinition> Scenarios);

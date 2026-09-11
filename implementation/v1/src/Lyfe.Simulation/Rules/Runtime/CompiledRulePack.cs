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
    string StableKey,
    string DisplayName,
    BiologicalForm BiologicalForm,
    EnvironmentalPhase EnvironmentalPhase,
    ImmutableArray<CompiledElementQuantity> Composition);

public readonly record struct CompiledResourceTerm(
    ResourceHandle Resource,
    long Quantity);

public sealed record CompiledReaction(
    ReactionId Id,
    string StableKey,
    string DisplayName,
    ProcessKind ProcessKind,
    ImmutableArray<CompiledResourceTerm> Inputs,
    ImmutableArray<CompiledResourceTerm> Outputs,
    long GrossEnergyQ,
    long StoredEnergyQ,
    long DissipatedEnergyQ);

public readonly record struct CompiledProcessPlan(ReactionHandle Reaction);

public enum ProcessAllocationPolicy : byte
{
    EqualShared = 1,
}

public enum FoundingMetabolismKind : byte
{
    HydrogenAcetogenesis = 1,
    SulfideAnoxygenicPhototrophy = 2,
}

public sealed record CompiledOpeningMetabolismProfile(
    FoundingMetabolismKind Kind,
    uint MaximumCaptureExtentsPerHour,
    uint FavorableCaptureEfficiencyQ,
    bool RequiresLight,
    uint IlluminatedHoursPerDay,
    uint GeneratedLightCaptureExtentsPerUnitHour,
    uint StructuralGrowthExtentsPerHour,
    long MaintenanceCostQPerHour,
    long GrowthReserveFloorQ);

public sealed record CompiledTemperatureResponse(
    int PreferredMinimumMilliC,
    int PreferredMaximumMilliC,
    int HardMinimumMilliC,
    int HardMaximumMilliC,
    uint HealthPenaltyAtHardQ,
    uint HealthFactorFloorQ,
    uint DeathChanceAtHardQ,
    uint DeathChanceCapQ);

public sealed record CompiledChemicalResponse(
    long HydrogenSulfideSoftThresholdQ,
    long HydrogenSulfideHardThresholdQ,
    long SulfurDioxideSoftThresholdQ,
    long SulfurDioxideHardThresholdQ,
    uint HealthPenaltyAtHardQ,
    uint HealthFactorFloorQ,
    uint DeathChanceAtHardQ,
    uint DeathChanceCapQ);

public sealed record CompiledReproductionProfile(
    uint MinimumHealthQ,
    long RequiredStructureQ,
    long ResultStructureMinimumQ,
    long RequiredReserveQ,
    long ResultReserveMinimumQ,
    long WorkCostQ,
    ulong BaseCooldownHours,
    uint CooldownJitterMaximumHours);

public sealed record CompiledRecyclingProfile(
    uint ReserveDecayPerMillionPerHour,
    uint StructureDecayPerMillionPerHour,
    bool SimpleRemnantScavenging,
    long ScavengeReserveCapQ,
    long ScavengeActionCostQ,
    long ParticulateScavengeActionCostQ,
    ulong ScavengeCooldownHours,
    uint ScavengeRangeQ);

public sealed record CompiledSpatialProfile(
    uint MatureBodyRadiusQ,
    long GeometricStructureTargetQ,
    uint MinimumBodyRadiusQ,
    uint BrownianRmsQPerSqrtHour,
    uint EnvironmentalSpreadMultiplierQ,
    uint TerrestrialBrownianMultiplierQ,
    uint ActiveSpeedLimitQPerHour,
    uint MovementEnergyPerFounderRadiusQ,
    bool CanOccupyTerrestrial,
    uint PassiveMigrationProbabilityQ,
    uint ActiveMigrationProbabilityQ,
    uint MediumTransitionFactorQ,
    uint DestinationCompatibilityFloorQ);

public sealed record CompiledBehaviorProfile(
    bool ResourceConservation,
    ulong MinimumDwellHours,
    uint ConservationEnterReserveQ,
    uint ConservationEnterConditionalReserveQ,
    uint ConservationEnterEnergyCoverageQ,
    uint ConservationCriticalReserveQ,
    uint ConservationExitReserveQ,
    uint ConservationExitEnergyCoverageQ,
    uint ConservationExitHighReserveQ);

public enum EvolutionPressureTag : byte
{
    EnergyShortage = 1,
    Starvation = 2,
}

public enum EvolutionStrategicIntent : byte
{
    ExploitCurrentNiche = 1,
    EndureEnvironmentalPressure = 2,
    AlterDispersal = 3,
    DiversifyResourceEnergyAccess = 4,
    BiologicalInteraction = 5,
    InvestInComplexity = 6,
}

public enum EvolutionFollowUpEvidenceKind : byte
{
    CapabilityActivation = 1,
    ConditionAndPressure = 2,
    GeographicSpread = 3,
    ReserveStorage = 4,
}

public sealed record CompiledTrait(
    TraitId Id,
    string StableKey,
    string DisplayName,
    string Family,
    bool Selectable,
    long MutationPointCostQ,
    uint ChangeComplexity,
    ImmutableArray<TraitId> Prerequisites,
    ImmutableArray<TraitId> Incompatibilities,
    uint BaseEvolutionWeightQ,
    ImmutableArray<EvolutionPressureTag> PressureTags,
    ImmutableArray<EvolutionStrategicIntent> StrategicIntents,
    ulong? ConsequenceFollowUpHours,
    EvolutionFollowUpEvidenceKind? ConsequenceEvidenceKind,
    bool EnablesResourceConservation,
    uint MutationIncomeMultiplierQ,
    uint? MaximumChangeComplexity);

public sealed record CompiledFounderAllocation(
    FounderAllocationId Id,
    string StableKey,
    string DisplayName,
    bool IsBaseline,
    uint CaptureEfficiencyMultiplierQ,
    uint ChemicalToleranceMultiplierQ);

public sealed record CompiledOrganismPhysiology(
    CompiledOpeningMetabolismProfile OpeningMetabolism,
    long MatureStructureQ,
    long StructuralHardFloorQ,
    long ChargedReserveCapacityQ,
    long TerminalReserveThresholdQ,
    long DissolvedMacronutrientCapacityLoadQ,
    long FreeMicronutrientCapacityLoadQ,
    uint PassiveMicronutrientUptakePerMillionPerHour,
    ImmutableArray<CompiledResourceTerm> CommittedMicronutrientQuotas,
    long IngestedMatterCapacityLoadQ,
    ProcessAllocationPolicy AllocationPolicy,
    ulong SenescenceOnsetHours,
    ulong AgeDeclineSpanHours,
    uint MinimumAgeFactorQ,
    uint SenescenceRiskBaseQ,
    ulong SenescenceRiskEscalationHours,
    uint SenescenceRiskCapQ,
    CompiledReproductionProfile Reproduction,
    CompiledRecyclingProfile Recycling,
    CompiledSpatialProfile Spatial,
    CompiledBehaviorProfile Behavior,
    CompiledChemicalResponse ChemicalResponse,
    CompiledTemperatureResponse TemperatureResponse);

public sealed record CompiledPhenotype(
    FounderGenomeId FounderGenomeId,
    string StableKey,
    string DisplayName,
    FounderAllocationId FounderAllocationId,
    ImmutableArray<TraitId> AcquiredTraits,
    ImmutableArray<CompiledProcessPlan> Processes,
    CompiledOrganismPhysiology Physiology,
    uint MutationIncomeModifierQ,
    uint MaximumChangeComplexity,
    string CanonicalCompiledHash);

public sealed record CompiledScenario(
    ScenarioId Id,
    string StableKey,
    string DisplayName,
    uint TickDurationHours,
    ImmutableArray<FounderGenomeHandle> PermittedFounders,
    ImmutableArray<FounderAllocationHandle> PermittedFounderAllocations,
    FounderAllocationHandle DefaultCompetitorFounderAllocation,
    CompiledFounderInitializationProfile FounderInitialization);

public sealed record CompiledFounderInitializationProfile(
    ulong BiologicalAgeMinimumHours,
    ulong BiologicalAgeMaximumHours,
    ulong ReproductionReadinessMinimumHours,
    ulong ReproductionReadinessMaximumHours);

public sealed class CompiledRulePack
{
    internal CompiledRulePack(
        RulePackIdentity identity,
        ImmutableArray<DefinitionRegistryEntry> definitionRegistry,
        ImmutableArray<CompiledResource> resources,
        ImmutableArray<CompiledReaction> reactions,
        ImmutableArray<CompiledTrait> traits,
        ImmutableArray<CompiledFounderAllocation> founderAllocations,
        ImmutableArray<CompiledPhenotype> founderPhenotypes,
        ImmutableArray<CompiledScenario> scenarios,
        ImmutableArray<long> effectivePopulationQ)
    {
        Identity = identity;
        DefinitionRegistry = definitionRegistry;
        Resources = resources;
        Reactions = reactions;
        Traits = traits;
        FounderAllocations = founderAllocations;
        FounderPhenotypes = founderPhenotypes;
        Scenarios = scenarios;
        EffectivePopulationQ = effectivePopulationQ;
    }

    public RulePackIdentity Identity { get; }

    public ImmutableArray<DefinitionRegistryEntry> DefinitionRegistry { get; }

    public ImmutableArray<CompiledResource> Resources { get; }

    public ImmutableArray<CompiledReaction> Reactions { get; }

    public ImmutableArray<CompiledTrait> Traits { get; }

    public ImmutableArray<CompiledFounderAllocation> FounderAllocations { get; }

    public ImmutableArray<CompiledPhenotype> FounderPhenotypes { get; }

    public ImmutableArray<CompiledScenario> Scenarios { get; }

    public ImmutableArray<long> EffectivePopulationQ { get; }
}

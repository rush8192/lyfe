using System.Collections.Immutable;
using Lyfe.Simulation.World.Generation;

namespace Lyfe.Simulation.Rules.Runtime;

public sealed record WorldRulesIdentity(
    string WorldPackId,
    string WorldPackVersion,
    string WorldProfileKey,
    string WorldPackageHash,
    string CompiledWorldProfileHash,
    string WorldRulesHash);

public sealed record CompiledTileProfile(
    uint TileIndex,
    int X,
    int Y,
    int ElevationMeters,
    uint BaselineVolcanismQ,
    int GasEmissionProfileSlot,
    ImmutableArray<long> ResourceQuantitiesByDenseSlot);

public enum GasAccessibilityClass : byte
{
    VentAccessible = 1,
    AtmosphericDepthLimited = 2,
    MixedOrigin = 3,
}

public sealed record CompiledGasTransport(
    ResourceHandle Resource,
    GasAccessibilityClass AccessibilityClass,
    uint SinkRatePerMillionPerHour,
    uint ExchangeRatePerMillionPerEdgeHour,
    long DiffuseSourceQuantityPerHour);

public sealed record CompiledGasEmissionProfile(
    string StableKey,
    ImmutableArray<long> FullActivityQuantitiesPerHourByGasSlot);

public sealed record CompiledGasEnvironment(
    uint AquaticTerrestrialCompatibilityQ,
    uint MajorMountainCompatibilityQ,
    int MajorMountainElevationMeters,
    ImmutableArray<CompiledGasTransport> Gases,
    ImmutableArray<CompiledGasEmissionProfile> EmissionProfiles);

public sealed record CompiledWorldProfile(
    string WorldProfileKey,
    uint Width,
    uint Height,
    bool WrapX,
    bool WrapY,
    ImmutableArray<CompiledTileProfile> Tiles,
    CompiledGasEnvironment GasEnvironment,
    CompiledWorldGenerator? Generator = null);

public sealed record CompiledWorldGenerator(
    uint AlgorithmVersion,
    uint MaximumAttempts,
    uint TargetAquaticFractionMinimumQ,
    uint TargetAquaticFractionMaximumQ,
    CompiledElevationGenerator Elevation,
    CompiledClimateGenerator Climate,
    CompiledVolcanismGenerator Volcanism,
    CompiledStartingRegionGenerator StartingRegions,
    ImmutableArray<long> DefaultResourceQuantitiesByDenseSlot,
    ImmutableArray<long> HydrogenStartResourceQuantitiesByDenseSlot);

public sealed record CompiledElevationGenerator(
    ImmutableArray<uint> WavelengthsTiles,
    ImmutableArray<uint> AmplitudesQ,
    uint ContinentalAmplitudeQ,
    int MaximumOceanDepthMeters,
    int MaximumLandElevationMeters);

public sealed record CompiledClimateGenerator(
    uint HoursPerDay,
    uint DaysPerMonth,
    uint MonthsPerYear,
    int AxialTiltMilliDegrees,
    int EquatorialAnnualMeanMilliC,
    int LatitudeCoolingMilliC,
    int LandLapseMilliCPerKilometer,
    int GeothermalMilliC,
    int RegionalAnomalyMagnitudeMilliC,
    int BaseSeasonalAmplitudeMilliC,
    int LatitudeSeasonalAmplitudeMilliC,
    uint AquaticSeasonalityQ,
    uint TerrestrialSeasonalityQ,
    int AquaticDiurnalAmplitudeMilliC,
    int TerrestrialDiurnalAmplitudeMilliC,
    int MinimumTemperatureMilliC,
    int MaximumTemperatureMilliC,
    uint MaximumMonthlyPrecipitationMicrometersPerHour,
    uint CloudMinimumQ,
    uint CloudPrecipitationContributionQ,
    uint CloudSolarAttenuationQ,
    uint TurbidityAttenuationQ)
{
    public uint DaysPerYear => checked(DaysPerMonth * MonthsPerYear);

    public uint HoursPerYear => checked(HoursPerDay * DaysPerYear);
}

public sealed record CompiledVolcanismGenerator(
    uint TileFractionMinimumQ,
    uint TileFractionMaximumQ,
    uint TileThresholdQ,
    uint FounderActivityMinimumQ,
    uint FounderActivityMaximumQ,
    uint FounderActivityTargetQ);

public sealed record CompiledStartingRegionGenerator(
    uint RequiredPairs,
    uint MinimumPairSeparationTiles,
    int MaximumAbsoluteLatitudeMilliDegrees,
    int HydrogenMinimumDepthMeters,
    int HydrogenMaximumDepthMeters,
    int HydrogenTargetDepthMeters,
    int SulfurMinimumDepthMeters,
    int SulfurMaximumDepthMeters,
    int SulfurTargetDepthMeters,
    int AnnualTemperatureMinimumMilliC,
    int AnnualTemperatureMaximumMilliC,
    int CurrentTemperatureMinimumMilliC,
    int CurrentTemperatureMaximumMilliC,
    long HydrogenMaximumDailyLightQ,
    long SulfurMinimumDailyLightQ,
    long SulfurMaximumDailyLightQ,
    uint MaximumRepairedTiles,
    int HydrogenMaximumDepthRepairMeters,
    int SulfurMaximumDepthRepairMeters,
    uint MaximumVolcanismRepairQ,
    uint MaximumTotalRepairCostQ);

public sealed class CompiledWorldRules
{
    internal CompiledWorldRules(
        CompiledRulePack rulePack,
        ScenarioHandle scenario,
        WorldRulesIdentity identity,
        CompiledWorldProfile worldProfile,
        uint tickDurationHours,
        GeneratedWorldMap? generatedWorld = null)
    {
        RulePack = rulePack;
        Scenario = scenario;
        Identity = identity;
        WorldProfile = worldProfile;
        TickDurationHours = tickDurationHours;
        GeneratedWorld = generatedWorld;
    }

    public CompiledRulePack RulePack { get; }

    public ScenarioHandle Scenario { get; }

    public WorldRulesIdentity Identity { get; }

    public CompiledWorldProfile WorldProfile { get; }

    public uint TickDurationHours { get; }

    public GeneratedWorldMap? GeneratedWorld { get; }
}

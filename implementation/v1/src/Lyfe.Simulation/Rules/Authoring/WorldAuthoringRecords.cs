using System.Text.Json.Serialization;

namespace Lyfe.Simulation.Rules.Authoring;

public sealed record WorldPackManifest
{
    public required string WorldPackId { get; init; }

    public required string Version { get; init; }

    public required string DisplayName { get; init; }

    public required uint WorldGenerationApiVersion { get; init; }

    public required string CompatibleBasePackId { get; init; }

    public string? RequiredRegistryManifestHash { get; init; }

    public required string[] ProfileFiles { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(WorldProfileDefinitionsDocument), "worldProfiles")]
public abstract record WorldSourceDocument;

public sealed record WorldProfileDefinitionsDocument : WorldSourceDocument
{
    public required WorldProfileDefinition[] Profiles { get; init; }
}

public sealed record WorldProfileDefinition
{
    public required string StableKey { get; init; }

    public required string DisplayName { get; init; }

    public required uint Width { get; init; }

    public required uint Height { get; init; }

    public required bool WrapX { get; init; }

    public required bool WrapY { get; init; }

    public required GasEnvironmentDefinition GasEnvironment { get; init; }

    public TileProfileDefinition[]? Tiles { get; init; }

    public WorldGeneratorDefinition? Generator { get; init; }
}

public sealed record GasEnvironmentDefinition
{
    public required uint AquaticTerrestrialCompatibilityQ { get; init; }

    public required uint MajorMountainCompatibilityQ { get; init; }

    public required int MajorMountainElevationMeters { get; init; }

    public required GasTransportDefinition[] Gases { get; init; }

    public required GasEmissionProfileDefinition[] EmissionProfiles { get; init; }
}

public sealed record GasTransportDefinition
{
    public required string ResourceKey { get; init; }

    public required string AccessibilityClass { get; init; }

    public required uint SinkRatePerMillionPerHour { get; init; }

    public required uint ExchangeRatePerMillionPerEdgeHour { get; init; }

    public required long DiffuseSourceQuantityPerHour { get; init; }
}

public sealed record GasEmissionProfileDefinition
{
    public required string StableKey { get; init; }

    public required GasEmissionDefinition[] Emissions { get; init; }
}

public sealed record GasEmissionDefinition
{
    public required string ResourceKey { get; init; }

    public required long FullActivityQuantityPerHour { get; init; }
}

public sealed record WorldGeneratorDefinition
{
    public required uint AlgorithmVersion { get; init; }

    public required uint MaximumAttempts { get; init; }

    public required uint TargetAquaticFractionMinimumQ { get; init; }

    public required uint TargetAquaticFractionMaximumQ { get; init; }

    public required ElevationGeneratorDefinition Elevation { get; init; }

    public required ClimateGeneratorDefinition Climate { get; init; }

    public required VolcanismGeneratorDefinition Volcanism { get; init; }

    public required StartingRegionGeneratorDefinition StartingRegions { get; init; }

    public required ResourceStockDefinition[] DefaultResourceStocks { get; init; }

    public required ResourceStockDefinition[] HydrogenStartResourceStocks { get; init; }
}

public sealed record ElevationGeneratorDefinition
{
    public required uint[] WavelengthsTiles { get; init; }

    public required uint[] AmplitudesQ { get; init; }

    public required uint ContinentalAmplitudeQ { get; init; }

    public required int MaximumOceanDepthMeters { get; init; }

    public required int MaximumLandElevationMeters { get; init; }
}

public sealed record ClimateGeneratorDefinition
{
    public required uint HoursPerDay { get; init; }

    public required uint DaysPerMonth { get; init; }

    public required uint MonthsPerYear { get; init; }

    public required int AxialTiltMilliDegrees { get; init; }

    public required int EquatorialAnnualMeanMilliC { get; init; }

    public required int LatitudeCoolingMilliC { get; init; }

    public required int LandLapseMilliCPerKilometer { get; init; }

    public required int GeothermalMilliC { get; init; }

    public required int RegionalAnomalyMagnitudeMilliC { get; init; }

    public required int BaseSeasonalAmplitudeMilliC { get; init; }

    public required int LatitudeSeasonalAmplitudeMilliC { get; init; }

    public required uint AquaticSeasonalityQ { get; init; }

    public required uint TerrestrialSeasonalityQ { get; init; }

    public required int AquaticDiurnalAmplitudeMilliC { get; init; }

    public required int TerrestrialDiurnalAmplitudeMilliC { get; init; }

    public required int MinimumTemperatureMilliC { get; init; }

    public required int MaximumTemperatureMilliC { get; init; }

    public required uint MaximumMonthlyPrecipitationMicrometersPerHour { get; init; }

    public required uint CloudMinimumQ { get; init; }

    public required uint CloudPrecipitationContributionQ { get; init; }

    public required uint CloudSolarAttenuationQ { get; init; }

    public required uint TurbidityAttenuationQ { get; init; }
}

public sealed record VolcanismGeneratorDefinition
{
    public required uint TileFractionMinimumQ { get; init; }

    public required uint TileFractionMaximumQ { get; init; }

    public required uint TileThresholdQ { get; init; }

    public required uint FounderActivityMinimumQ { get; init; }

    public required uint FounderActivityMaximumQ { get; init; }

    public required uint FounderActivityTargetQ { get; init; }
}

public sealed record StartingRegionGeneratorDefinition
{
    public required uint RequiredPairs { get; init; }

    public required uint MinimumPairSeparationTiles { get; init; }

    public required int MaximumAbsoluteLatitudeMilliDegrees { get; init; }

    public required int HydrogenMinimumDepthMeters { get; init; }

    public required int HydrogenMaximumDepthMeters { get; init; }

    public required int HydrogenTargetDepthMeters { get; init; }

    public required int SulfurMinimumDepthMeters { get; init; }

    public required int SulfurMaximumDepthMeters { get; init; }

    public required int SulfurTargetDepthMeters { get; init; }

    public required int AnnualTemperatureMinimumMilliC { get; init; }

    public required int AnnualTemperatureMaximumMilliC { get; init; }

    public required int CurrentTemperatureMinimumMilliC { get; init; }

    public required int CurrentTemperatureMaximumMilliC { get; init; }

    public required long HydrogenMaximumDailyLightQ { get; init; }

    public required long SulfurMinimumDailyLightQ { get; init; }

    public required long SulfurMaximumDailyLightQ { get; init; }

    public required uint MaximumRepairedTiles { get; init; }

    public required int HydrogenMaximumDepthRepairMeters { get; init; }

    public required int SulfurMaximumDepthRepairMeters { get; init; }

    public required uint MaximumVolcanismRepairQ { get; init; }

    public required uint MaximumTotalRepairCostQ { get; init; }
}

public sealed record TileProfileDefinition
{
    public required int X { get; init; }

    public required int Y { get; init; }

    public required int ElevationMeters { get; init; }

    public required uint BaselineVolcanismQ { get; init; }

    public string? GasEmissionProfileKey { get; init; }

    public required ResourceStockDefinition[] ResourceStocks { get; init; }
}

public sealed record ResourceStockDefinition
{
    public required string ResourceKey { get; init; }

    public required long Quantity { get; init; }
}

public sealed record AuthoringWorldPack(
    WorldPackManifest Manifest,
    IReadOnlyList<WorldProfileDefinition> Profiles);

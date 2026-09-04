using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Simulation.World.Generation;

public enum GeneratedTerrain : byte
{
    ShallowOcean = 1,
    ShelfOcean = 2,
    DeepOcean = 3,
    Lowland = 4,
    Highland = 5,
    Mountain = 6,
}

public enum StartingTileRole : byte
{
    None = 0,
    Hydrogen = 1,
    Sulfur = 2,
}

public sealed record GeneratedTile(
    uint TileIndex,
    int X,
    int Y,
    int LatitudeMilliDegrees,
    int ElevationMeters,
    GeneratedTerrain Terrain,
    uint BaselineVolcanismQ,
    int RegionalTemperatureAnomalyMilliC,
    int AnnualMeanTemperatureMilliC,
    int SeasonalAmplitudeMilliC,
    ImmutableArray<int> MonthlyTemperatureMilliC,
    ImmutableArray<uint> MonthlyPrecipitationMicrometersPerHour,
    ImmutableArray<uint> MonthlyCloudQ,
    uint SurfaceMoistureBaselineQ,
    uint AquaticTurbidityQ,
    StartingTileRole StartingRole,
    ImmutableArray<long> ResourceQuantitiesByDenseSlot)
{
    public bool IsAquatic => ElevationMeters < 0;

    public int WaterDepthMeters => Math.Max(0, -ElevationMeters);
}

public sealed record StartingTilePair(
    uint HydrogenTileIndex,
    uint SulfurTileIndex,
    bool WasRepaired,
    uint RepairCostQ);

public sealed record WorldGenerationRepair(
    uint TileIndex,
    StartingTileRole Role,
    int OriginalElevationMeters,
    int FinalElevationMeters,
    uint OriginalVolcanismQ,
    uint FinalVolcanismQ,
    int OriginalRegionalTemperatureAnomalyMilliC,
    int FinalRegionalTemperatureAnomalyMilliC,
    uint OriginalCloudQ,
    uint FinalCloudQ,
    uint OriginalAquaticTurbidityQ,
    uint FinalAquaticTurbidityQ);

public sealed record GeneratedWorldMap(
    string WorldProfileKey,
    uint Width,
    uint Height,
    bool WrapX,
    bool WrapY,
    uint GenerationAttempt,
    uint TargetAquaticFractionQ,
    uint StartSolarOffsetQ,
    CompiledClimateGenerator Climate,
    ImmutableArray<GeneratedTile> Tiles,
    ImmutableArray<StartingTilePair> StartingPairs,
    ImmutableArray<WorldGenerationRepair> Repairs)
{
    public GeneratedTile GetTile(uint tileIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            tileIndex,
            checked((uint)Tiles.Length));

        return Tiles[(int)tileIndex];
    }
}

public readonly record struct WorldCalendarState(
    ulong Year,
    uint DayOfYear,
    uint Month,
    uint DayOfMonth,
    uint HourOfDay);

public readonly record struct TileCurrentClimate(
    int TemperatureMilliC,
    uint PrecipitationMicrometersPerHour,
    uint CloudQ,
    uint SurfaceMoistureQ,
    uint SurfaceLightQ,
    uint AccessibleLightQ);

public sealed record WorldClimateState(
    ulong Generation,
    ulong AbsoluteHour,
    WorldCalendarState Calendar,
    ImmutableArray<TileCurrentClimate> Tiles)
{
    public TileCurrentClimate GetTile(uint tileIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            tileIndex,
            checked((uint)Tiles.Length));
        return Tiles[checked((int)tileIndex)];
    }
}

public sealed class WorldGenerationException : InvalidOperationException
{
    public WorldGenerationException(string message)
        : base(message)
    {
    }
}

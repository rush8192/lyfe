using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Simulation.World.Generation;

public static class WorldClimateEvaluator
{
    private const long Scale = FixedWorldMath.Scale;

    public static WorldCalendarState GetCalendar(
        CompiledClimateGenerator climate,
        ulong absoluteHour)
    {
        ArgumentNullException.ThrowIfNull(climate);
        var hoursPerYear = climate.HoursPerYear;
        var year = absoluteHour / hoursPerYear;
        var hourWithinYear = checked((uint)(absoluteHour % hoursPerYear));
        var dayOfYear = hourWithinYear / climate.HoursPerDay;
        var hourOfDay = hourWithinYear % climate.HoursPerDay;
        return new WorldCalendarState(
            year,
            dayOfYear,
            dayOfYear / climate.DaysPerMonth,
            dayOfYear % climate.DaysPerMonth,
            hourOfDay);
    }

    public static TileCurrentClimate Evaluate(
        GeneratedWorldMap world,
        GeneratedTile tile,
        ulong absoluteHour)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tile);
        var climate = world.Climate;
        var calendar = GetCalendar(climate, absoluteHour);
        var yearPhaseQ = checked((long)calendar.DayOfYear * Scale / climate.DaysPerYear);
        var seasonalQ = FixedWorldMath.SinTurnQ(yearPhaseQ);
        if (tile.Y < 0)
        {
            seasonalQ = -seasonalQ;
        }

        var seasonal = FixedWorldMath.MultiplyQ(tile.SeasonalAmplitudeMilliC, seasonalQ);
        var localDayQ = FixedWorldMath.Mod(
            (checked((long)calendar.HourOfDay * Scale) / climate.HoursPerDay) +
            (checked((long)tile.X * Scale) / world.Width) +
            world.StartSolarOffsetQ,
            Scale);
        var diurnalAmplitude = tile.IsAquatic
            ? climate.AquaticDiurnalAmplitudeMilliC
            : climate.TerrestrialDiurnalAmplitudeMilliC;
        var diurnal = FixedWorldMath.MultiplyQ(
            diurnalAmplitude,
            -FixedWorldMath.CosTurnQ(localDayQ));
        var temperature = checked((int)Math.Clamp(
            (long)tile.AnnualMeanTemperatureMilliC + seasonal + diurnal,
            climate.MinimumTemperatureMilliC,
            climate.MaximumTemperatureMilliC));

        var month = checked((int)calendar.Month);
        var precipitation = tile.MonthlyPrecipitationMicrometersPerHour[month];
        var cloudQ = tile.MonthlyCloudQ[month];
        var surfaceLightQ = EvaluateSurfaceLightQ(world, tile, calendar, cloudQ);
        var depthFactorQ = tile.IsAquatic
            ? DepthLightFactorQ(tile.WaterDepthMeters)
            : (uint)Scale;
        var turbidityFactorQ = checked((uint)(Scale -
            FixedWorldMath.MultiplyQ(world.Climate.TurbidityAttenuationQ, tile.AquaticTurbidityQ)));
        var accessibleLightQ = FixedWorldMath.MultiplyQ(
            FixedWorldMath.MultiplyQ(surfaceLightQ, depthFactorQ),
            turbidityFactorQ);
        var precipitationContributionQ = climate.MaximumMonthlyPrecipitationMicrometersPerHour == 0
            ? 0U
            : checked((uint)Math.Min(
                Scale,
                ((ulong)precipitation * Scale) /
                climate.MaximumMonthlyPrecipitationMicrometersPerHour));
        var surfaceMoistureQ = tile.IsAquatic
            ? (uint)Scale
            : checked((uint)Math.Min(
                Scale,
                tile.SurfaceMoistureBaselineQ + (precipitationContributionQ / 3)));

        return new TileCurrentClimate(
            temperature,
            precipitation,
            cloudQ,
            surfaceMoistureQ,
            surfaceLightQ,
            accessibleLightQ);
    }

    public static WorldClimateState Materialize(
        GeneratedWorldMap world,
        ulong absoluteHour,
        ulong generation)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new WorldClimateState(
            generation,
            absoluteHour,
            GetCalendar(world.Climate, absoluteHour),
            world.Tiles
                .Select(tile => Evaluate(world, tile, absoluteHour))
                .ToImmutableArray());
    }

    public static long EvaluateDailyAccessibleLightQ(
        GeneratedWorldMap world,
        GeneratedTile tile,
        uint dayOfYear)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            dayOfYear,
            world.Climate.DaysPerYear);

        var firstHour = checked((ulong)dayOfYear * world.Climate.HoursPerDay);
        var sum = 0L;
        for (var hour = 0U; hour < world.Climate.HoursPerDay; hour++)
        {
            sum = checked(sum + Evaluate(world, tile, firstHour + hour).AccessibleLightQ);
        }

        return sum;
    }

    public static uint DepthLightFactorQ(int depthMeters)
    {
        if (depthMeters <= 0)
        {
            return (uint)Scale;
        }

        if (depthMeters >= 1_000)
        {
            return 0;
        }

        return depthMeters switch
        {
            <= 20 => LogLinearQ(depthMeters, 0, 20, 1_000_000, 800_000),
            <= 200 => LogLinearQ(depthMeters, 20, 200, 800_000, 150_000),
            _ => LogLinearQ(depthMeters, 200, 1_000, 150_000, 1_000),
        };
    }

    private static uint EvaluateSurfaceLightQ(
        GeneratedWorldMap world,
        GeneratedTile tile,
        WorldCalendarState calendar,
        uint cloudQ)
    {
        var climate = world.Climate;
        var yearPhaseQ = checked((long)calendar.DayOfYear * Scale / climate.DaysPerYear);
        var declinationMilliDegrees = FixedWorldMath.MultiplyQ(
            climate.AxialTiltMilliDegrees,
            FixedWorldMath.SinTurnQ(yearPhaseQ));
        var latitudeTurnQ = checked((long)tile.LatitudeMilliDegrees * Scale / 360_000);
        var declinationTurnQ = checked(declinationMilliDegrees * Scale / 360_000);
        var localDayQ = FixedWorldMath.Mod(
            (checked((long)calendar.HourOfDay * Scale) / climate.HoursPerDay) +
            (checked((long)tile.X * Scale) / world.Width) +
            world.StartSolarOffsetQ,
            Scale);
        var hourAngleQ = localDayQ - 500_000;
        var sinProduct = FixedWorldMath.MultiplyQ(
            FixedWorldMath.SinTurnQ(latitudeTurnQ),
            FixedWorldMath.SinTurnQ(declinationTurnQ));
        var cosProduct = FixedWorldMath.MultiplyQ(
            FixedWorldMath.MultiplyQ(
                FixedWorldMath.CosTurnQ(latitudeTurnQ),
                FixedWorldMath.CosTurnQ(declinationTurnQ)),
            FixedWorldMath.CosTurnQ(hourAngleQ));
        var clearLightQ = Math.Clamp(sinProduct + cosProduct, 0, Scale);
        var cloudLossQ = FixedWorldMath.MultiplyQ(climate.CloudSolarAttenuationQ, cloudQ);
        return checked((uint)FixedWorldMath.MultiplyQ(clearLightQ, Scale - cloudLossQ));
    }

    private static uint LogLinearQ(
        int value,
        int lowerValue,
        int upperValue,
        uint lowerFactorQ,
        uint upperFactorQ)
    {
        if (value <= lowerValue)
        {
            return lowerFactorQ;
        }

        if (value >= upperValue)
        {
            return upperFactorQ;
        }

        var fractionQ = checked((long)(value - lowerValue) * Scale /
            (upperValue - lowerValue));
        var ratioQ = checked((ulong)upperFactorQ * (ulong)Scale / lowerFactorQ);
        var powerQ = FractionalPowerQ(ratioQ, fractionQ);
        return checked((uint)((ulong)lowerFactorQ * powerQ / (ulong)Scale));
    }

    private static ulong FractionalPowerQ(ulong ratioQ, long fractionQ)
    {
        var resultQ = (ulong)Scale;
        var rootQ = ratioQ;
        var remainderQ = fractionQ;
        for (var bit = 0; bit < 24; bit++)
        {
            rootQ = IntegerSquareRoot(checked(rootQ * (ulong)Scale));
            remainderQ = checked(remainderQ * 2);
            if (remainderQ >= Scale)
            {
                resultQ = checked(resultQ * rootQ / (ulong)Scale);
                remainderQ -= Scale;
            }
        }

        return resultQ;
    }

    private static ulong IntegerSquareRoot(ulong value)
    {
        var low = 0UL;
        var high = Math.Min(value, 1_000_000UL) + 1;
        while (low + 1 < high)
        {
            var middle = low + ((high - low) / 2);
            if (middle <= value / middle)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}

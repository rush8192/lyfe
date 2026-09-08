using System.Numerics;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Spatial;

internal readonly record struct SpatialVector(long XQ, long YQ);

internal static class SpatialMath
{
    public const ulong LocalOne = 1UL << 32;
    public const uint RatioScale = 1_000_000;
    public const uint FounderRadiusQ = 4_194_304;
    public const uint FounderBrownianRmsQ = 8_388_608;

    private static readonly int[] QuarterWave =
    [
        0, 24541, 49068, 73565, 98017, 122411, 146730, 170962,
        195090, 219101, 242980, 266713, 290285, 313682, 336890, 359895,
        382683, 405241, 427555, 449611, 471397, 492898, 514103, 534998,
        555570, 575808, 595699, 615232, 634393, 653173, 671559, 689541,
        707107, 724247, 740951, 757209, 773010, 788346, 803208, 817585,
        831470, 844854, 857729, 870087, 881921, 893224, 903989, 914210,
        923880, 932993, 941544, 949528, 956940, 963776, 970031, 975702,
        980785, 985278, 989177, 992480, 995185, 997290, 998795, 999699,
        1000000,
    ];

    // Midpoint-quantile Rayleigh samples normalized so the complete discrete table
    // has the configured RMS. Stored authoring output avoids runtime logarithms.
    private static readonly int[] BrownianMagnitudes =
    [
        371160,643498,831570,984897,1117874,1237084,1346192,1447488,
        1542515,1632370,1717868,1799633,1878155,1953828,2026973,2097856,
        2166703,2233703,2299021,2362798,2425158,2486210,2546050,2604763,
        2662427,2719110,2774873,2829775,2883865,2937192,2989797,3041720,
        3092997,3143662,3193747,3243280,3292287,3340795,3388826,3436402,
        3483544,3530272,3576604,3622556,3668145,3713387,3758296,3802885,
        3847169,3891159,3934867,3978304,4021482,4064410,4107099,4149558,
        4191796,4233821,4275643,4317269,4358706,4399963,4441047,4481963,
        4522720,4563323,4603779,4644094,4684274,4724324,4764250,4804057,
        4843751,4883336,4922818,4962201,5001491,5040691,5079806,5118841,
        5157800,5196687,5235506,5274261,5312956,5351596,5390183,5428722,
        5467216,5505669,5544085,5582467,5620818,5659142,5697443,5735723,
        5773987,5812237,5850476,5888709,5926937,5965165,6003395,6041631,
        6079876,6118132,6156403,6194693,6233003,6271338,6309700,6348092,
        6386518,6424980,6463481,6502026,6540616,6579255,6617946,6656692,
        6695496,6734361,6773292,6812289,6851358,6890501,6929721,6969022,
        7008408,7047880,7087444,7127101,7166857,7206713,7246675,7286745,
        7326926,7367224,7407641,7448181,7488849,7529648,7570582,7611655,
        7652871,7694235,7735750,7777422,7819255,7861253,7903421,7945763,
        7988285,8030991,8073887,8116977,8160267,8203763,8247470,8291393,
        8335539,8379913,8424522,8469371,8514468,8559819,8605432,8651312,
        8697467,8743905,8790633,8837660,8884993,8932641,8980612,9028917,
        9077563,9126561,9175920,9225651,9275765,9326272,9377184,9428513,
        9480271,9532471,9585127,9638252,9691860,9745967,9800588,9855740,
        9911440,9967705,10024554,10082007,10140083,10198805,10258194,10318275,
        10379071,10440609,10502916,10566021,10629954,10694747,10760434,10827051,
        10894636,10963228,11032872,11103612,11175497,11248578,11322910,11398553,
        11475570,11554029,11634002,11715569,11798814,11883830,11970716,12059580,
        12150541,12243729,12339283,12437361,12538133,12641791,12748544,12858628,
        12972306,13089877,13211674,13338081,13469534,13606536,13749673,13899628,
        14057208,14223377,14399296,14586388,14786417,15001612,15234850,15489935,
        15772051,16088538,16450300,16874610,17391409,18060186,19030637,20966112,
    ];

    public static uint BodyRadiusQ(CompiledSpatialProfile profile, long geometricStructureQ)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (geometricStructureQ <= 0)
        {
            return profile.MinimumBodyRadiusQ;
        }

        var numerator = BigInteger.Pow(profile.MatureBodyRadiusQ, 3) * geometricStructureQ;
        var denominator = new BigInteger(profile.GeometricStructureTargetQ);
        var floor = IntegerCubeRoot(numerator / denominator);
        var midpointComparison = numerator * 8 -
            denominator * BigInteger.Pow((floor * 2) + 1, 3);
        var rounded = midpointComparison > 0 ||
            midpointComparison == 0 && !floor.IsEven
                ? floor + 1
                : floor;
        return (uint)BigInteger.Min(
            uint.MaxValue,
            BigInteger.Max(profile.MinimumBodyRadiusQ, rounded));
    }

    public static SpatialVector SampleBrownian(
        ISimulationRandom random,
        ulong tick,
        OrganismId organismId,
        CompiledSpatialProfile profile,
        uint currentRadiusQ,
        bool isTerrestrial,
        uint tickDurationHours)
    {
        var directionIndex = checked((int)random.UniformBelow(
            RandomAddress.Create(RandomDomains.BrownianDirection, tick, organismId.Value, 0),
            256));
        var magnitudeIndex = checked((int)random.UniformBelow(
            RandomAddress.Create(RandomDomains.BrownianMagnitude, tick, organismId.Value, 0),
            256));
        var direction = Direction(directionIndex);
        var magnitude = (Int128)BrownianMagnitudes[magnitudeIndex];
        magnitude = RoundEven(magnitude * profile.BrownianRmsQPerSqrtHour, FounderBrownianRmsQ);
        magnitude = RoundEven(magnitude * profile.EnvironmentalSpreadMultiplierQ, RatioScale);
        if (isTerrestrial)
        {
            magnitude = RoundEven(magnitude * profile.TerrestrialBrownianMultiplierQ, RatioScale);
        }

        // Brownian displacement varies inversely with the square root of physical scale.
        var bodyScaleQ = SqrtRatioQ(profile.MatureBodyRadiusQ, Math.Max(1U, currentRadiusQ));
        magnitude = RoundEven(magnitude * bodyScaleQ, RatioScale);
        var durationScaleQ = IntegerSquareRoot((UInt128)tickDurationHours * RatioScale * RatioScale);
        magnitude = RoundEven(magnitude * durationScaleQ, RatioScale);

        return new SpatialVector(
            checked((long)RoundEven(magnitude * direction.XQ, RatioScale)),
            checked((long)RoundEven(magnitude * direction.YQ, RatioScale)));
    }

    public static SpatialVector Direction(int index)
    {
        if ((uint)index >= 256)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var quadrant = index >> 6;
        var offset = index & 63;
        var sine = QuarterWave[offset];
        var cosine = QuarterWave[64 - offset];
        return quadrant switch
        {
            0 => new(cosine, sine),
            1 => new(-sine, cosine),
            2 => new(-cosine, -sine),
            _ => new(sine, -cosine),
        };
    }

    public static bool WithinRange(
        uint ax,
        uint ay,
        uint bx,
        uint by,
        ulong rangeQ)
    {
        var dx = (long)ax - bx;
        var dy = (long)ay - by;
        return (Int128)dx * dx + (Int128)dy * dy <= (Int128)rangeQ * rangeQ;
    }

    internal static ulong IntegerSquareRoot(UInt128 value)
    {
        UInt128 low = 0;
        UInt128 high = (UInt128)ulong.MaxValue + 1;
        while (low + 1 < high)
        {
            var middle = (low + high) >> 1;
            if (middle <= value / middle)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return (ulong)low;
    }

    private static ulong SqrtRatioQ(uint numerator, uint denominator) =>
        IntegerSquareRoot((UInt128)numerator * RatioScale * RatioScale / denominator);

    private static Int128 RoundEven(Int128 numerator, Int128 denominator)
    {
        var quotient = numerator / denominator;
        var remainder = numerator % denominator;
        var twice = Int128.Abs(remainder) * 2;
        if (twice > denominator || twice == denominator && (quotient & 1) != 0)
        {
            quotient += numerator >= 0 ? 1 : -1;
        }

        return quotient;
    }

    private static BigInteger IntegerCubeRoot(BigInteger value)
    {
        var low = BigInteger.Zero;
        var high = BigInteger.One;
        while (BigInteger.Pow(high, 3) <= value)
        {
            high <<= 1;
        }

        while (low + 1 < high)
        {
            var middle = (low + high) >> 1;
            if (BigInteger.Pow(middle, 3) <= value)
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

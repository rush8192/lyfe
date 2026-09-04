namespace Lyfe.Simulation.World.Generation;

internal static class FixedWorldMath
{
    public const long Scale = 1_000_000;
    public const long HalfTurnQ = 500_000;
    public const long QuarterTurnQ = 250_000;

    public static long SinTurnQ(long phaseQ)
    {
        var normalized = Mod(phaseQ, Scale);
        var negative = normalized > HalfTurnQ;
        var x = negative ? normalized - HalfTurnQ : normalized;
        var product = (Int128)x * (HalfTurnQ - x);
        var numerator = 16 * product * Scale;
        var denominator = (5 * (Int128)HalfTurnQ * HalfTurnQ) - (4 * product);
        var result = denominator == 0 ? 0 : checked((long)(numerator / denominator));
        return negative ? -result : result;
    }

    public static long CosTurnQ(long phaseQ) => SinTurnQ(phaseQ + QuarterTurnQ);

    public static long MultiplyQ(long left, long right) =>
        checked((long)(((Int128)left * right) / Scale));

    public static uint MultiplyQ(uint left, uint right) =>
        checked((uint)(((ulong)left * right) / Scale));

    public static long Lerp(long from, long to, long tQ) =>
        checked(from + MultiplyQ(to - from, tQ));

    public static long SmoothStepQ(long tQ)
    {
        var bounded = Math.Clamp(tQ, 0, Scale);
        var square = MultiplyQ(bounded, bounded);
        return MultiplyQ(square, (3 * Scale) - (2 * bounded));
    }

    public static long Mod(long value, long modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }
}

namespace Lyfe.Simulation.Randomness;

internal static class Philox4x64
{
    private const ulong Multiplier0 = 0xD2E7470EE14C6C93UL;
    private const ulong Multiplier1 = 0xCA5A826395121157UL;
    private const ulong Weyl0 = 0x9E3779B97F4A7C15UL;
    private const ulong Weyl1 = 0xBB67AE8584CAA73BUL;
    private const int Rounds = 10;

    internal const ulong MaximumWordsPerAddress = ((ulong)uint.MaxValue + 1) * 4;

    public static PhiloxBlock Generate(PhiloxBlock counter, PhiloxKey key)
    {
        for (var round = 0; round < Rounds; round++)
        {
            MultiplyHighLow(Multiplier0, counter.Word0, out var high0, out var low0);
            MultiplyHighLow(Multiplier1, counter.Word2, out var high1, out var low1);

            counter = new PhiloxBlock(
                high1 ^ counter.Word1 ^ key.Word0,
                low1,
                high0 ^ counter.Word3 ^ key.Word1,
                low0);

            if (round != Rounds - 1)
            {
                key = new PhiloxKey(
                    unchecked(key.Word0 + Weyl0),
                    unchecked(key.Word1 + Weyl1));
            }
        }

        return counter;
    }

    private static void MultiplyHighLow(
        ulong left,
        ulong right,
        out ulong high,
        out ulong low)
    {
        var product = (UInt128)left * right;
        high = (ulong)(product >> 64);
        low = (ulong)product;
    }
}

internal readonly record struct PhiloxKey(ulong Word0, ulong Word1);

internal readonly record struct PhiloxBlock(
    ulong Word0,
    ulong Word1,
    ulong Word2,
    ulong Word3)
{
    public ulong WordAt(int lane) => lane switch
    {
        0 => Word0,
        1 => Word1,
        2 => Word2,
        3 => Word3,
        _ => throw new ArgumentOutOfRangeException(nameof(lane)),
    };
}

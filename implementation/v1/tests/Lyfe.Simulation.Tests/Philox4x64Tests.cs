using System.Numerics;
using Lyfe.Simulation.Randomness;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class Philox4x64Tests
{
    [Fact]
    public void MatchesPublishedRandom123KnownAnswerVectors()
    {
        (PhiloxBlock Counter, PhiloxKey Key, PhiloxBlock Expected)[] vectors =
        [
            (
                new PhiloxBlock(0, 0, 0, 0),
                new PhiloxKey(0, 0),
                new PhiloxBlock(
                    0x16554D9ECA36314CUL,
                    0xDB20FE9D672D0FDCUL,
                    0xD7E772CEE186176BUL,
                    0x7E68B68AEC7BA23BUL)),
            (
                new PhiloxBlock(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue),
                new PhiloxKey(ulong.MaxValue, ulong.MaxValue),
                new PhiloxBlock(
                    0x87B092C3013FE90BUL,
                    0x438C3C67BE8D0224UL,
                    0x9CC7D7C69CD777B6UL,
                    0xA09CAEBF594F0BA0UL)),
            (
                new PhiloxBlock(
                    0x243F6A8885A308D3UL,
                    0x13198A2E03707344UL,
                    0xA4093822299F31D0UL,
                    0x082EFA98EC4E6C89UL),
                new PhiloxKey(0x452821E638D01377UL, 0xBE5466CF34E90C6CUL),
                new PhiloxBlock(
                    0xA528F45403E61D95UL,
                    0x38C72DBD566E9788UL,
                    0xA5A1610E72FD18B5UL,
                    0x57BD43B5E52B7FE6UL)),
        ];

        foreach (var vector in vectors)
        {
            Assert.Equal(vector.Expected, Philox4x64.Generate(vector.Counter, vector.Key));
        }
    }

    [Fact]
    public void MatchesIndependentBigIntegerScalarReference()
    {
        for (var index = 0UL; index < 128; index++)
        {
            var counter = new PhiloxBlock(
                index * 0x9E3779B97F4A7C15UL,
                index ^ 0x0123456789ABCDEFUL,
                index * 0xD2E7470EE14C6C93UL,
                ~index);
            var key = new PhiloxKey(
                index * 0xBB67AE8584CAA73BUL,
                index ^ 0xFEDCBA9876543210UL);

            Assert.Equal(ReferenceGenerate(counter, key), Philox4x64.Generate(counter, key));
        }
    }

    private static PhiloxBlock ReferenceGenerate(PhiloxBlock counter, PhiloxKey key)
    {
        const ulong multiplier0 = 0xD2E7470EE14C6C93UL;
        const ulong multiplier1 = 0xCA5A826395121157UL;
        const ulong weyl0 = 0x9E3779B97F4A7C15UL;
        const ulong weyl1 = 0xBB67AE8584CAA73BUL;
        var mask = (BigInteger.One << 64) - 1;

        for (var round = 0; round < 10; round++)
        {
            var product0 = new BigInteger(multiplier0) * counter.Word0;
            var product1 = new BigInteger(multiplier1) * counter.Word2;
            var low0 = (ulong)(product0 & mask);
            var high0 = (ulong)(product0 >> 64);
            var low1 = (ulong)(product1 & mask);
            var high1 = (ulong)(product1 >> 64);
            counter = new PhiloxBlock(
                high1 ^ counter.Word1 ^ key.Word0,
                low1,
                high0 ^ counter.Word3 ^ key.Word1,
                low0);
            if (round < 9)
            {
                key = new PhiloxKey(
                    unchecked(key.Word0 + weyl0),
                    unchecked(key.Word1 + weyl1));
            }
        }

        return counter;
    }
}

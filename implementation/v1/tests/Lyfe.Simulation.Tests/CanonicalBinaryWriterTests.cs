using Lyfe.Simulation.Serialization;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class CanonicalBinaryWriterTests
{
    [Fact]
    public void WritesFixedWidthIntegersInLittleEndianOrder()
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteByte(0xA5);
        writer.WriteBoolean(false);
        writer.WriteBoolean(true);
        writer.WriteUInt16(0x0123);
        writer.WriteInt16(short.MinValue);
        writer.WriteInt16(short.MaxValue);
        writer.WriteUInt32(0x01234567);
        writer.WriteInt32(int.MinValue);
        writer.WriteInt32(int.MaxValue);
        writer.WriteUInt64(0x0123456789ABCDEF);
        writer.WriteInt64(long.MinValue);
        writer.WriteInt64(long.MaxValue);

        Assert.Equal(
            "a5000123010080ff7f6745230100000080ffffff7f" +
            "efcdab89674523010000000000000080ffffffffffffff7f",
            Convert.ToHexStringLower(writer.WrittenSpan));
    }

    [Fact]
    public void WritesWideIntegersLowWordFirst()
    {
        var writer = new CanonicalBinaryWriter();
        var patterned = ((UInt128)0x0123456789ABCDEFUL << 64) |
            0xFEDCBA9876543210UL;

        writer.WriteUInt128(patterned);
        writer.WriteInt128(Int128.MinValue);
        writer.WriteInt128(Int128.MaxValue);

        Assert.Equal(
            "1032547698badcfeefcdab8967452301" +
            "00000000000000000000000000000080" +
            "ffffffffffffffffffffffffffffff7f",
            Convert.ToHexStringLower(writer.WrittenSpan));
    }

    [Fact]
    public void NormalizesAndLengthPrefixesUtf8Strings()
    {
        var decomposedAngstrom = "A\u030A";
        var composedAngstrom = "\u00C5";
        var first = new CanonicalBinaryWriter();
        var second = new CanonicalBinaryWriter();

        first.WriteUtf8Nfc(decomposedAngstrom);
        second.WriteUtf8Nfc(composedAngstrom);

        Assert.Equal("02000000c385", Convert.ToHexStringLower(first.WrittenSpan));
        Assert.True(first.WrittenSpan.SequenceEqual(second.WrittenSpan));
    }

    [Fact]
    public void LengthPrefixesOpaqueBytes()
    {
        var writer = new CanonicalBinaryWriter();

        writer.WriteLengthPrefixedBytes([0x00, 0x7F, 0xFF]);

        Assert.Equal("03000000007fff", Convert.ToHexStringLower(writer.WrittenSpan));
        Assert.Equal(7, writer.Length);
    }
}


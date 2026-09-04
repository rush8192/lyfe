using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Lyfe.Simulation.Serialization;

internal sealed class CanonicalBinaryWriter
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly ArrayBufferWriter<byte> buffer = new();

    public int Length => buffer.WrittenCount;

    public ReadOnlySpan<byte> WrittenSpan => buffer.WrittenSpan;

    public void WriteByte(byte value)
    {
        var destination = buffer.GetSpan(1);
        destination[0] = value;
        buffer.Advance(1);
    }

    public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    public void WriteUInt16(ushort value)
    {
        var destination = buffer.GetSpan(sizeof(ushort));
        BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
        buffer.Advance(sizeof(ushort));
    }

    public void WriteInt16(short value)
    {
        var destination = buffer.GetSpan(sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(destination, value);
        buffer.Advance(sizeof(short));
    }

    public void WriteUInt32(uint value)
    {
        var destination = buffer.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
        buffer.Advance(sizeof(uint));
    }

    public void WriteInt32(int value)
    {
        var destination = buffer.GetSpan(sizeof(int));
        BinaryPrimitives.WriteInt32LittleEndian(destination, value);
        buffer.Advance(sizeof(int));
    }

    public void WriteUInt64(ulong value)
    {
        var destination = buffer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
        buffer.Advance(sizeof(ulong));
    }

    public void WriteInt64(long value)
    {
        var destination = buffer.GetSpan(sizeof(long));
        BinaryPrimitives.WriteInt64LittleEndian(destination, value);
        buffer.Advance(sizeof(long));
    }

    public void WriteUInt128(UInt128 value)
    {
        WriteUInt64((ulong)value);
        WriteUInt64((ulong)(value >> 64));
    }

    public void WriteInt128(Int128 value) => WriteUInt128(unchecked((UInt128)value));

    public void WriteLengthPrefixedBytes(ReadOnlySpan<byte> value)
    {
        WriteUInt32(checked((uint)value.Length));
        WriteRawBytes(value);
    }

    public void WriteUtf8Nfc(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var normalized = value.Normalize(NormalizationForm.FormC);
        var byteCount = StrictUtf8.GetByteCount(normalized);
        WriteUInt32(checked((uint)byteCount));

        var destination = buffer.GetSpan(byteCount);
        var bytesWritten = StrictUtf8.GetBytes(normalized, destination);
        buffer.Advance(bytesWritten);
    }

    private void WriteRawBytes(ReadOnlySpan<byte> value)
    {
        var destination = buffer.GetSpan(value.Length);
        value.CopyTo(destination);
        buffer.Advance(value.Length);
    }
}


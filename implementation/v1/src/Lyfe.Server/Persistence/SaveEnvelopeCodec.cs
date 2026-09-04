using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lyfe.Simulation.World;

namespace Lyfe.Server.Persistence;

public static class SaveEnvelopeCodec
{
    public const uint ContainerVersion = 1;
    public const int MaximumMetadataBytes = 64 * 1024;
    public const int MaximumLogicalPayloadBytes = 1024 * 1024 * 1024;
    public const int MaximumEncodedPayloadBytes = 1024 * 1024 * 1024;

    internal const int PreambleSize = 136;
    private const int DigestSize = 32;
    private const int MaximumStringBytes = 16 * 1024;
    private static readonly byte[] Magic = "LYFESV1\0"u8.ToArray();

    public static byte[] Encode(
        SaveEnvelopeMetadata metadata,
        ReadOnlySpan<byte> logicalPayload)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ValidateMetadataForWrite(metadata);
        if (logicalPayload.IsEmpty || logicalPayload.Length > MaximumLogicalPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalPayload),
                "A save payload must be nonempty and within the configured logical limit.");
        }

        var metadataBytes = JsonSerializer.SerializeToUtf8Bytes(
            metadata,
            SaveEnvelopeJsonContext.Default.SaveEnvelopeMetadata);
        if (metadataBytes.Length == 0 || metadataBytes.Length > MaximumMetadataBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(metadata),
                "Serialized save metadata exceeds the configured limit.");
        }

        byte[] encodedPayload;
        using (var destination = new MemoryStream())
        {
            using (var compressor = new BrotliStream(
                destination,
                CompressionLevel.Optimal,
                leaveOpen: true))
            {
                compressor.Write(logicalPayload);
            }

            encodedPayload = destination.ToArray();
        }

        if (encodedPayload.Length == 0 || encodedPayload.Length > MaximumEncodedPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalPayload),
                "Compressed save payload exceeds the configured encoded limit.");
        }

        var metadataDigest = SHA256.HashData(metadataBytes);
        var encodedDigest = SHA256.HashData(encodedPayload);
        var logicalDigest = SHA256.HashData(logicalPayload);
        var totalLength = checked(PreambleSize + metadataBytes.Length + encodedPayload.Length);
        var result = new byte[totalLength];
        var preamble = result.AsSpan(0, PreambleSize);
        Magic.CopyTo(preamble);
        BinaryPrimitives.WriteUInt32LittleEndian(preamble[8..], ContainerVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(
            preamble[12..],
            (uint)SaveCompressionKind.Brotli);
        BinaryPrimitives.WriteUInt32LittleEndian(
            preamble[16..],
            checked((uint)metadataBytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(preamble[20..], 0);
        BinaryPrimitives.WriteUInt64LittleEndian(
            preamble[24..],
            checked((ulong)encodedPayload.Length));
        BinaryPrimitives.WriteUInt64LittleEndian(
            preamble[32..],
            checked((ulong)logicalPayload.Length));
        metadataDigest.CopyTo(preamble[40..72]);
        encodedDigest.CopyTo(preamble[72..104]);
        logicalDigest.CopyTo(preamble[104..136]);
        metadataBytes.CopyTo(result.AsSpan(PreambleSize));
        encodedPayload.CopyTo(result.AsSpan(PreambleSize + metadataBytes.Length));
        return result;
    }

    public static SaveEnvelopeDescriptor ReadDescriptor(
        Stream source,
        SaveCompatibilityIdentity? expectedCompatibility = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("The save stream must be readable.", nameof(source));
        }

        var header = ReadHeader(source);
        if (expectedCompatibility is not null)
        {
            RequireCompatible(header.Descriptor.Metadata.Compatibility, expectedCompatibility);
        }

        return header.Descriptor;
    }

    public static DecodedSaveEnvelope Decode(
        Stream source,
        SaveCompatibilityIdentity? expectedCompatibility = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("The save stream must be readable.", nameof(source));
        }

        var header = ReadHeader(source);
        if (expectedCompatibility is not null)
        {
            RequireCompatible(header.Descriptor.Metadata.Compatibility, expectedCompatibility);
        }

        var encodedLength = checked((int)header.Descriptor.EncodedPayloadLength);
        var encodedPayload = new byte[encodedLength];
        ReadExactly(source, encodedPayload);
        if (!SHA256.HashData(encodedPayload).AsSpan().SequenceEqual(header.EncodedDigest))
        {
            throw Failure(
                SaveLoadFailureCode.EncodedPayloadChecksumMismatch,
                "The encoded save payload checksum does not match.");
        }

        if (!source.CanSeek && source.ReadByte() != -1)
        {
            throw Failure(
                SaveLoadFailureCode.TrailingData,
                "The save contains trailing data after its declared payload.");
        }

        var logicalLength = checked((int)header.Descriptor.LogicalPayloadLength);
        var logicalPayload = new byte[logicalLength];
        try
        {
            using var encodedStream = new MemoryStream(encodedPayload, writable: false);
            using var decompressor = new BrotliStream(
                encodedStream,
                CompressionMode.Decompress,
                leaveOpen: false);
            ReadExactly(decompressor, logicalPayload, isCompressedPayload: true);
            if (decompressor.ReadByte() != -1)
            {
                throw Failure(
                    SaveLoadFailureCode.LogicalPayloadLengthMismatch,
                    "The decompressed save payload exceeds its declared length.");
            }
        }
        catch (SaveEnvelopeException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            throw new SaveEnvelopeException(
                SaveLoadFailureCode.PayloadDecompressionFailed,
                "The save payload could not be decompressed.",
                exception);
        }

        if (!SHA256.HashData(logicalPayload).AsSpan().SequenceEqual(header.LogicalDigest))
        {
            throw Failure(
                SaveLoadFailureCode.LogicalPayloadChecksumMismatch,
                "The logical save payload checksum does not match.");
        }

        return new DecodedSaveEnvelope(header.Descriptor, logicalPayload);
    }

    public static void RequireCompatible(
        SaveCompatibilityIdentity actual,
        SaveCompatibilityIdentity expected)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);
        if (actual != expected)
        {
            throw Failure(
                SaveLoadFailureCode.IncompatiblePayload,
                "The save simulation, rules, world, mod, options, or RNG identity is incompatible.");
        }
    }

    private static ParsedHeader ReadHeader(Stream source)
    {
        var startPosition = source.CanSeek ? source.Position : 0;
        Span<byte> preamble = stackalloc byte[PreambleSize];
        ReadExactly(source, preamble);
        if (!preamble[..Magic.Length].SequenceEqual(Magic))
        {
            throw Failure(SaveLoadFailureCode.InvalidMagic, "The file is not a LYFE save.");
        }

        var containerVersion = BinaryPrimitives.ReadUInt32LittleEndian(preamble[8..]);
        if (containerVersion != ContainerVersion)
        {
            throw Failure(
                SaveLoadFailureCode.UnsupportedContainerVersion,
                $"Save container version {containerVersion} is unsupported.");
        }

        var compressionValue = BinaryPrimitives.ReadUInt32LittleEndian(preamble[12..]);
        if (compressionValue != (uint)SaveCompressionKind.Brotli)
        {
            throw Failure(
                SaveLoadFailureCode.UnsupportedCompression,
                $"Save compression kind {compressionValue} is unsupported.");
        }

        var metadataLength = BinaryPrimitives.ReadUInt32LittleEndian(preamble[16..]);
        var reserved = BinaryPrimitives.ReadUInt32LittleEndian(preamble[20..]);
        var encodedLength = BinaryPrimitives.ReadUInt64LittleEndian(preamble[24..]);
        var logicalLength = BinaryPrimitives.ReadUInt64LittleEndian(preamble[32..]);
        if (reserved != 0)
        {
            throw Failure(
                SaveLoadFailureCode.MalformedPreamble,
                "The save preamble has nonzero reserved data.");
        }

        if (metadataLength is 0 or > MaximumMetadataBytes ||
            encodedLength is 0 or > MaximumEncodedPayloadBytes ||
            logicalLength is 0 or > MaximumLogicalPayloadBytes)
        {
            throw Failure(
                SaveLoadFailureCode.InvalidLength,
                "A save header declares an invalid metadata or payload length.");
        }

        var remainingLength = checked((ulong)metadataLength + encodedLength);
        if (source.CanSeek)
        {
            var declaredEnd = checked(startPosition + PreambleSize + (long)remainingLength);
            if (source.Length < declaredEnd)
            {
                throw Failure(
                    SaveLoadFailureCode.Truncated,
                    "The save ends before its declared payload boundary.");
            }

            if (source.Length > declaredEnd)
            {
                throw Failure(
                    SaveLoadFailureCode.TrailingData,
                    "The save contains trailing data after its declared payload.");
            }
        }

        var metadataBytes = new byte[checked((int)metadataLength)];
        ReadExactly(source, metadataBytes);
        var metadataDigest = preamble[40..72].ToArray();
        if (!SHA256.HashData(metadataBytes).AsSpan().SequenceEqual(metadataDigest))
        {
            throw Failure(
                SaveLoadFailureCode.MetadataChecksumMismatch,
                "The save metadata checksum does not match.");
        }

        SaveEnvelopeMetadata metadata;
        try
        {
            metadata = JsonSerializer.Deserialize(
                metadataBytes,
                SaveEnvelopeJsonContext.Default.SaveEnvelopeMetadata) ??
                throw new JsonException("Save metadata was null.");
            ValidateMetadataForRead(metadata);
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or FormatException)
        {
            throw new SaveEnvelopeException(
                SaveLoadFailureCode.MalformedMetadata,
                "The save metadata is malformed.",
                exception);
        }

        var encodedDigest = preamble[72..104].ToArray();
        var logicalDigest = preamble[104..136].ToArray();
        return new ParsedHeader(
            new SaveEnvelopeDescriptor(
                metadata,
                SaveCompressionKind.Brotli,
                encodedLength,
                logicalLength,
                Convert.ToHexStringLower(metadataDigest),
                Convert.ToHexStringLower(encodedDigest),
                Convert.ToHexStringLower(logicalDigest)),
            encodedDigest,
            logicalDigest);
    }

    private static void ValidateMetadataForWrite(SaveEnvelopeMetadata metadata)
    {
        try
        {
            ValidateMetadata(metadata);
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException)
        {
            throw new ArgumentException("Save metadata is invalid.", nameof(metadata), exception);
        }
    }

    private static void ValidateMetadataForRead(SaveEnvelopeMetadata metadata) =>
        ValidateMetadata(metadata);

    private static void ValidateMetadata(SaveEnvelopeMetadata metadata)
    {
        if (metadata.WorldId == 0 ||
            metadata.TickDurationHours == 0 ||
            metadata.WorldLifecycle != (byte)WorldRunnerStatus.PausedReady ||
            metadata.OrganismCount < 0)
        {
            throw new ArgumentException("Save boundary metadata is invalid.");
        }

        RequireHex(metadata.WorldStateHash, 64, nameof(metadata.WorldStateHash));
        RequireHex(metadata.RootRandomSeed, 32, nameof(metadata.RootRandomSeed));
        var identity = metadata.Compatibility ??
            throw new ArgumentException("Save compatibility identity is missing.");
        if (identity.PayloadSchemaVersion == 0 ||
            identity.WorldStateHashSchemaVersion == 0 ||
            identity.EngineRuleApiVersion == 0 ||
            identity.RuleCompilerVersion == 0 ||
            identity.MechanicsHashSchemaVersion == 0 ||
            identity.ScenarioId == 0 ||
            identity.RngSchemaVersion == 0)
        {
            throw new ArgumentException("Save compatibility versions and IDs must be nonzero.");
        }

        RequireText(identity.EngineSimulationVersion);
        RequireText(identity.BaseRulePackId);
        RequireText(identity.BaseRulePackVersion);
        RequireHex(identity.MechanicsHash, 64, nameof(identity.MechanicsHash));
        RequireHex(identity.PresentationHash, 64, nameof(identity.PresentationHash));
        RequireHex(identity.RegistryManifestHash, 64, nameof(identity.RegistryManifestHash));
        RequireHex(identity.CompiledRuleArtifactHash, 64, nameof(identity.CompiledRuleArtifactHash));
        RequireHex(identity.BalanceModSetHash, 64, nameof(identity.BalanceModSetHash));
        RequireText(identity.WorldPackId);
        RequireText(identity.WorldPackVersion);
        RequireText(identity.WorldProfileKey);
        RequireHex(identity.WorldPackageHash, 64, nameof(identity.WorldPackageHash));
        RequireHex(identity.CompiledWorldProfileHash, 64, nameof(identity.CompiledWorldProfileHash));
        RequireHex(identity.WorldRulesHash, 64, nameof(identity.WorldRulesHash));
        RequireHex(identity.SelectedWorldOptionsHash, 64, nameof(identity.SelectedWorldOptionsHash));
        RequireText(identity.RngAlgorithmId);
        RequireHex(identity.RngDomainManifestHash, 64, nameof(identity.RngDomainManifestHash));
        RequireText(identity.CertificationClass);
    }

    private static void RequireText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!value.IsNormalized(NormalizationForm.FormC) ||
            Encoding.UTF8.GetByteCount(value) > MaximumStringBytes)
        {
            throw new ArgumentException("Save metadata text must be bounded NFC UTF-8.");
        }
    }

    private static void RequireHex(string value, int length, string field)
    {
        if (value is null ||
            value.Length != length ||
            !value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw new FormatException($"Save metadata field {field} is not canonical hexadecimal text.");
        }
    }

    private static void ReadExactly(
        Stream source,
        Span<byte> destination,
        bool isCompressedPayload = false)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var count = source.Read(destination[offset..]);
            if (count == 0)
            {
                throw Failure(
                    isCompressedPayload
                        ? SaveLoadFailureCode.LogicalPayloadLengthMismatch
                        : SaveLoadFailureCode.Truncated,
                    isCompressedPayload
                        ? "The decompressed save payload is shorter than declared."
                        : "The save ends before its declared boundary.");
            }

            offset += count;
        }
    }

    private static SaveEnvelopeException Failure(SaveLoadFailureCode code, string message) =>
        new(code, message);

    private sealed record ParsedHeader(
        SaveEnvelopeDescriptor Descriptor,
        byte[] EncodedDigest,
        byte[] LogicalDigest);
}

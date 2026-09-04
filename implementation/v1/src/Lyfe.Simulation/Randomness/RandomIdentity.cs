using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using Lyfe.Simulation.Serialization;

namespace Lyfe.Simulation.Randomness;

public readonly record struct RootRandomSeed(ulong Low, ulong High)
{
    public const int CanonicalTextLength = 32;

    public static RootRandomSeed Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != CanonicalTextLength ||
            !ulong.TryParse(
                value.AsSpan(0, 16),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var high) ||
            !ulong.TryParse(
                value.AsSpan(16, 16),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var low))
        {
            throw new FormatException(
                "A root random seed must contain exactly 32 hexadecimal digits.");
        }

        return new RootRandomSeed(low, high);
    }

    public string ToCanonicalString() => $"{High:x16}{Low:x16}";

    public override string ToString() => ToCanonicalString();
}

public readonly record struct RandomDomainId
{
    internal RandomDomainId(uint value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        Value = value;
    }

    public uint Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct RandomAddress
{
    private RandomAddress(
        RandomDomainId domainId,
        ulong coordinate0,
        ulong coordinate1,
        ulong coordinate2,
        ulong sampleIndex)
    {
        RandomDomainRegistry.Require(domainId);
        if (sampleIndex / Philox4x64.MaximumWordsPerAddress > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleIndex),
                sampleIndex,
                "The sample index exceeds the version-one address space.");
        }

        DomainId = domainId;
        Coordinate0 = coordinate0;
        Coordinate1 = coordinate1;
        Coordinate2 = coordinate2;
        SampleIndex = sampleIndex;
    }

    public RandomDomainId DomainId { get; }

    public ulong Coordinate0 { get; }

    public ulong Coordinate1 { get; }

    public ulong Coordinate2 { get; }

    public ulong SampleIndex { get; }

    public static RandomAddress Create(
        RandomDomainId domainId,
        ulong coordinate0,
        ulong coordinate1,
        ulong coordinate2) =>
        new(domainId, coordinate0, coordinate1, coordinate2, 0);

    internal RandomAddress WithSampleIndex(ulong sampleIndex) =>
        new(DomainId, Coordinate0, Coordinate1, Coordinate2, sampleIndex);
}

public enum RandomOperationKind : byte
{
    Bernoulli = 1,
    UniformBelow = 2,
    StableRank = 3,
    WeightedChoice = 4,
    KeyedBinomial = 5,
}

public sealed record RandomDomainDefinition(
    RandomDomainId Id,
    string StableName,
    string Coordinate0Meaning,
    string Coordinate1Meaning,
    string Coordinate2Meaning,
    RandomOperationKind AllowedOperation,
    uint IntroducedRngSchemaVersion);

public static class RandomDomains
{
    public static RandomDomainId FounderPositionX { get; } = new(0x0101);
    public static RandomDomainId FounderPositionY { get; } = new(0x0102);
    public static RandomDomainId WorldGenerationFieldSample { get; } = new(0x0201);
    public static RandomDomainId WeatherEvent { get; } = new(0x0301);
    public static RandomDomainId IntrinsicExposureDeath { get; } = new(0x0401);
    public static RandomDomainId SenescenceDeath { get; } = new(0x0402);
    public static RandomDomainId MicronutrientUptake { get; } = new(0x0501);
    public static RandomDomainId MetabolicOpportunity { get; } = new(0x0601);
    public static RandomDomainId BrownianDirection { get; } = new(0x0701);
    public static RandomDomainId BrownianMagnitude { get; } = new(0x0702);
    public static RandomDomainId MigrationAdmission { get; } = new(0x0703);
    public static RandomDomainId BehaviorChoice { get; } = new(0x0801);
    public static RandomDomainId PredationTargetChoice { get; } = new(0x0901);
    public static RandomDomainId PredationKill { get; } = new(0x0902);
    public static RandomDomainId ResourceRemainderRank { get; } = new(0x0A01);
    public static RandomDomainId CoupledClaimRemainderRank { get; } = new(0x0A02);
    public static RandomDomainId PredationFeedingRemainderRank { get; } = new(0x0A03);
    public static RandomDomainId MicronutrientTargetRank { get; } = new(0x0A04);
    public static RandomDomainId ReproductionCooldownJitter { get; } = new(0x0B01);
    public static RandomDomainId ReproductionIndivisibleRemainder { get; } = new(0x0B02);
    public static RandomDomainId SpeciationFounderSelection { get; } = new(0x0C01);
    public static RandomDomainId AutonomousExplorationChoice { get; } = new(0x0D01);
    public static RandomDomainId AutonomousCommit { get; } = new(0x0D02);
}

public static class RandomDomainRegistry
{
    private const uint DomainManifestRecord = 0x524E4701;

    private static readonly ImmutableArray<RandomDomainDefinition> definitions =
    [
        Define(RandomDomains.FounderPositionX, "founder-position-x", "setup ordinal", "organism ID", "tile ID", RandomOperationKind.UniformBelow),
        Define(RandomDomains.FounderPositionY, "founder-position-y", "setup ordinal", "organism ID", "tile ID", RandomOperationKind.UniformBelow),
        Define(RandomDomains.WorldGenerationFieldSample, "world-generation-field-sample", "tile ID", "field octave/layer", "generation attempt", RandomOperationKind.UniformBelow),
        Define(RandomDomains.WeatherEvent, "weather-event", "tick", "tile ID", "weather event ordinal/type", RandomOperationKind.Bernoulli),
        Define(RandomDomains.IntrinsicExposureDeath, "intrinsic-exposure-death", "tick", "organism ID", "exposure ID", RandomOperationKind.Bernoulli),
        Define(RandomDomains.SenescenceDeath, "senescence-death", "tick", "organism ID", "unused (zero)", RandomOperationKind.Bernoulli),
        Define(RandomDomains.MicronutrientUptake, "micronutrient-uptake", "tick", "organism ID", "resource-selection stage", RandomOperationKind.WeightedChoice),
        Define(RandomDomains.MetabolicOpportunity, "metabolic-opportunity", "tick", "organism ID", "reaction ID", RandomOperationKind.KeyedBinomial),
        Define(RandomDomains.BrownianDirection, "brownian-direction", "tick", "organism ID", "unused (zero)", RandomOperationKind.UniformBelow),
        Define(RandomDomains.BrownianMagnitude, "brownian-magnitude", "tick", "organism ID", "unused (zero)", RandomOperationKind.UniformBelow),
        Define(RandomDomains.MigrationAdmission, "migration-admission", "tick", "organism ID", "edge ID", RandomOperationKind.Bernoulli),
        Define(RandomDomains.BehaviorChoice, "behavior-choice", "tick", "organism ID", "behavior-selection ordinal", RandomOperationKind.WeightedChoice),
        Define(RandomDomains.PredationTargetChoice, "predation-target-choice", "tick", "predator ID", "prey ID", RandomOperationKind.StableRank),
        Define(RandomDomains.PredationKill, "predation-kill", "tick", "predator ID", "prey ID", RandomOperationKind.Bernoulli),
        Define(RandomDomains.ResourceRemainderRank, "resource-remainder-rank", "tick", "claimant ID", "packed tile/resource IDs", RandomOperationKind.StableRank),
        Define(RandomDomains.CoupledClaimRemainderRank, "coupled-claim-remainder-rank", "tick", "claimant ID", "packed tile/reaction IDs", RandomOperationKind.StableRank),
        Define(RandomDomains.PredationFeedingRemainderRank, "predation-feeding-remainder-rank", "tick", "claimant ID", "packed tile/resource IDs", RandomOperationKind.StableRank),
        Define(RandomDomains.MicronutrientTargetRank, "micronutrient-target-rank", "tick", "organism ID", "resource ID", RandomOperationKind.StableRank),
        Define(RandomDomains.ReproductionCooldownJitter, "reproduction-cooldown-jitter", "organism ID", "successful reproduction ordinal", "unused (zero)", RandomOperationKind.UniformBelow),
        Define(RandomDomains.ReproductionIndivisibleRemainder, "reproduction-indivisible-remainder", "organism ID", "successful reproduction ordinal", "resource ID", RandomOperationKind.StableRank),
        Define(RandomDomains.SpeciationFounderSelection, "speciation-founder-selection", "speciation event ID", "tile ID", "organism ID", RandomOperationKind.StableRank),
        Define(RandomDomains.AutonomousExplorationChoice, "autonomous-exploration-choice", "autonomous evaluation ordinal", "species ID", "proposal-set hash", RandomOperationKind.WeightedChoice),
        Define(RandomDomains.AutonomousCommit, "autonomous-commit", "autonomous evaluation ordinal", "species ID", "proposal hash", RandomOperationKind.Bernoulli),
    ];

    static RandomDomainRegistry()
    {
        var priorId = 0U;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (definition.Id.Value <= priorId)
            {
                throw new InvalidOperationException(
                    "Random domain definitions must have unique ascending numeric IDs.");
            }

            if (!names.Add(definition.StableName))
            {
                throw new InvalidOperationException(
                    $"Duplicate random domain name '{definition.StableName}'.");
            }

            priorId = definition.Id.Value;
        }

        ManifestHash = ComputeManifestHash();
    }

    public static ImmutableArray<RandomDomainDefinition> Definitions => definitions;

    public static string ManifestHash { get; }

    public static RandomDomainDefinition Require(RandomDomainId id)
    {
        foreach (var definition in definitions)
        {
            if (definition.Id == id)
            {
                return definition;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(id),
            id,
            "The random domain is not registered in RNG schema version 1.");
    }

    internal static void RequireOperation(RandomDomainId id, RandomOperationKind operation)
    {
        var definition = Require(id);
        if (definition.AllowedOperation != operation)
        {
            throw new InvalidOperationException(
                $"Random domain '{definition.StableName}' permits {definition.AllowedOperation}, not {operation}.");
        }
    }

    private static RandomDomainDefinition Define(
        RandomDomainId id,
        string stableName,
        string coordinate0Meaning,
        string coordinate1Meaning,
        string coordinate2Meaning,
        RandomOperationKind operation) =>
        new(
            id,
            stableName,
            coordinate0Meaning,
            coordinate1Meaning,
            coordinate2Meaning,
            operation,
            RandomCompatibility.RngSchemaVersion);

    private static string ComputeManifestHash()
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(DomainManifestRecord);
        writer.WriteUInt32(RandomCompatibility.RngSchemaVersion);
        writer.WriteUInt32(checked((uint)definitions.Length));
        foreach (var definition in definitions)
        {
            writer.WriteUInt32(definition.Id.Value);
            writer.WriteUtf8Nfc(definition.StableName);
            writer.WriteUtf8Nfc(definition.Coordinate0Meaning);
            writer.WriteUtf8Nfc(definition.Coordinate1Meaning);
            writer.WriteUtf8Nfc(definition.Coordinate2Meaning);
            writer.WriteByte((byte)definition.AllowedOperation);
            writer.WriteUInt32(definition.IntroducedRngSchemaVersion);
        }

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(writer.WrittenSpan, digest);
        return Convert.ToHexStringLower(digest);
    }
}

public static class RandomCompatibility
{
    public const string AlgorithmId = "philox4x64-10-random123-v1";
    public const uint RngSchemaVersion = 1;

    public static RandomCompatibilityState ForSeed(RootRandomSeed seed) =>
        new(seed, AlgorithmId, RngSchemaVersion, RandomDomainRegistry.ManifestHash);
}

public sealed record RandomCompatibilityState(
    RootRandomSeed RootSeed,
    string AlgorithmId,
    uint RngSchemaVersion,
    string RngDomainManifestHash);

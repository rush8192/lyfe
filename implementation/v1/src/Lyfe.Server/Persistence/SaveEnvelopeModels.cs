using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Server.Persistence;

public enum SaveCompressionKind : uint
{
    Brotli = 1,
}

public enum SaveLoadFailureCode
{
    Truncated = 1,
    InvalidMagic = 2,
    UnsupportedContainerVersion = 3,
    UnsupportedCompression = 4,
    InvalidLength = 5,
    MalformedPreamble = 6,
    MetadataChecksumMismatch = 7,
    MalformedMetadata = 8,
    IncompatiblePayload = 9,
    EncodedPayloadChecksumMismatch = 10,
    PayloadDecompressionFailed = 11,
    LogicalPayloadLengthMismatch = 12,
    LogicalPayloadChecksumMismatch = 13,
    TrailingData = 14,
}

public sealed class SaveEnvelopeException : IOException
{
    internal SaveEnvelopeException(SaveLoadFailureCode code, string message)
        : base(message) => Code = code;

    internal SaveEnvelopeException(
        SaveLoadFailureCode code,
        string message,
        Exception innerException)
        : base(message, innerException) => Code = code;

    public SaveLoadFailureCode Code { get; }
}

public sealed record SaveCompatibilityIdentity(
    uint PayloadSchemaVersion,
    string EngineSimulationVersion,
    uint WorldStateHashSchemaVersion,
    string BaseRulePackId,
    string BaseRulePackVersion,
    uint EngineRuleApiVersion,
    uint RuleCompilerVersion,
    uint MechanicsHashSchemaVersion,
    string MechanicsHash,
    string PresentationHash,
    string RegistryManifestHash,
    string CompiledRuleArtifactHash,
    string BalanceModSetHash,
    uint ScenarioId,
    string WorldPackId,
    string WorldPackVersion,
    string WorldProfileKey,
    string WorldPackageHash,
    string CompiledWorldProfileHash,
    string WorldRulesHash,
    string SelectedWorldOptionsHash,
    string RngAlgorithmId,
    uint RngSchemaVersion,
    string RngDomainManifestHash,
    string CertificationClass);

public sealed record SaveEnvelopeMetadata(
    ulong WorldId,
    ulong CompletedTick,
    ulong WorldRevision,
    ulong SimulatedHours,
    uint TickDurationHours,
    byte WorldLifecycle,
    int OrganismCount,
    string WorldStateHash,
    string RootRandomSeed,
    SaveCompatibilityIdentity Compatibility);

public sealed record SaveEnvelopeDescriptor(
    SaveEnvelopeMetadata Metadata,
    SaveCompressionKind Compression,
    ulong EncodedPayloadLength,
    ulong LogicalPayloadLength,
    string MetadataSha256,
    string EncodedPayloadSha256,
    string LogicalPayloadSha256);

public sealed record DecodedSaveEnvelope(
    SaveEnvelopeDescriptor Descriptor,
    byte[] LogicalPayload);

public static class SaveEnvelopeMetadataFactory
{
    public const uint PayloadSchemaVersion = 11;
    public const string UnmodifiedCertificationClass = "official-unmodified-v1";
    public const string EmptyConfigurationHash =
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public static SaveEnvelopeMetadata Create(WorldPersistenceMetadata source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var boundary = source.Boundary;
        var rulePack = source.RulePackIdentity;
        var worldRules = source.WorldRulesIdentity;
        return new SaveEnvelopeMetadata(
            boundary.WorldId.Value,
            boundary.CompletedTick,
            boundary.WorldRevision,
            boundary.SimulatedHours,
            boundary.TickDurationHours,
            (byte)source.Lifecycle,
            boundary.OrganismCount,
            boundary.StateHash,
            source.RandomCompatibility.RootSeed.ToCanonicalString(),
            new SaveCompatibilityIdentity(
                PayloadSchemaVersion,
                source.EngineSimulationVersion,
                source.WorldStateHashSchemaVersion,
                rulePack.PackId,
                rulePack.DeclaredVersion,
                rulePack.EngineRuleApiVersion,
                rulePack.RuleCompilerVersion,
                rulePack.MechanicsHashSchemaVersion,
                rulePack.MechanicsHash,
                rulePack.PresentationHash,
                rulePack.RegistryManifestHash,
                rulePack.CompiledArtifactHash,
                EmptyConfigurationHash,
                source.ScenarioId.Value,
                worldRules.WorldPackId,
                worldRules.WorldPackVersion,
                worldRules.WorldProfileKey,
                worldRules.WorldPackageHash,
                worldRules.CompiledWorldProfileHash,
                worldRules.WorldRulesHash,
                EmptyConfigurationHash,
                source.RandomCompatibility.AlgorithmId,
                source.RandomCompatibility.RngSchemaVersion,
                source.RandomCompatibility.RngDomainManifestHash,
                UnmodifiedCertificationClass));
    }

    public static SaveCompatibilityIdentity CreateCompatibility(CompiledWorldRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var rulePack = rules.RulePack.Identity;
        var worldRules = rules.Identity;
        return new SaveCompatibilityIdentity(
            PayloadSchemaVersion,
            WorldPersistenceContract.EngineSimulationVersion,
            WorldPersistenceContract.WorldStateHashSchemaVersion,
            rulePack.PackId,
            rulePack.DeclaredVersion,
            rulePack.EngineRuleApiVersion,
            rulePack.RuleCompilerVersion,
            rulePack.MechanicsHashSchemaVersion,
            rulePack.MechanicsHash,
            rulePack.PresentationHash,
            rulePack.RegistryManifestHash,
            rulePack.CompiledArtifactHash,
            EmptyConfigurationHash,
            rules.Scenario.Id.Value,
            worldRules.WorldPackId,
            worldRules.WorldPackVersion,
            worldRules.WorldProfileKey,
            worldRules.WorldPackageHash,
            worldRules.CompiledWorldProfileHash,
            worldRules.WorldRulesHash,
            EmptyConfigurationHash,
            RandomCompatibility.AlgorithmId,
            RandomCompatibility.RngSchemaVersion,
            RandomDomainRegistry.ManifestHash,
            UnmodifiedCertificationClass);
    }
}

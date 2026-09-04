using System.Text.Json.Serialization;

namespace Lyfe.Server.Persistence;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(SaveEnvelopeMetadata))]
internal sealed partial class SaveEnvelopeJsonContext : JsonSerializerContext;

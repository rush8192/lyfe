using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lyfe.Simulation.Rules.Authoring;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    AllowTrailingCommas = false,
    ReadCommentHandling = JsonCommentHandling.Disallow,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(RulePackManifest))]
[JsonSerializable(typeof(RuleSourceDocument))]
[JsonSerializable(typeof(RegistryLockFile))]
[JsonSerializable(typeof(WorldPackManifest))]
[JsonSerializable(typeof(WorldSourceDocument))]
internal partial class RuleAuthoringJsonContext : JsonSerializerContext;


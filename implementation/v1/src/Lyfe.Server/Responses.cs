namespace Lyfe.Server;

public sealed record HealthResponse(string Status);

public sealed record ServerCapabilitiesResponse(
    uint ProtocolVersion,
    uint MinimumProtocolVersion,
    string SimulationVersion,
    IReadOnlyList<string> Features);

public sealed record ActiveWorldResponse(
    string WorldId,
    string CompletedTick,
    string WorldRevision,
    string StateHash,
    string Lifecycle);

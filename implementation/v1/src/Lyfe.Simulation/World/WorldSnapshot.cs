using Lyfe.Simulation.Core;

namespace Lyfe.Simulation.World;

public sealed record WorldSnapshot(
    WorldId WorldId,
    ulong CompletedTick,
    ulong WorldRevision,
    ulong SimulatedHours,
    uint TickDurationHours,
    int OrganismCount,
    string StateHash);

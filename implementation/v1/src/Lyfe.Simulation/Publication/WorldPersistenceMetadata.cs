using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.World;

namespace Lyfe.Simulation.Publication;

public static class WorldPersistenceContract
{
    public const string EngineSimulationVersion = "lyfe-simulation-v1";
    public const uint WorldStateHashSchemaVersion = 9;
}

public sealed record WorldPersistenceMetadata(
    WorldSnapshot Boundary,
    WorldRunnerStatus Lifecycle,
    string EngineSimulationVersion,
    uint WorldStateHashSchemaVersion,
    RandomCompatibilityState RandomCompatibility,
    RulePackIdentity RulePackIdentity,
    ScenarioId ScenarioId,
    WorldRulesIdentity WorldRulesIdentity);

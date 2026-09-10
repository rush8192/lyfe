using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Gameplay;

public enum NotableEventFamily : byte
{
    Speciation = 1,
    FirstReproduction = 2,
    PopulationMilestone = 3,
    FirstTileOccupation = 4,
    FirstReactionExecution = 5,
    SpeciesExtinction = 6,
    PopulationDangerThreshold = 7,
    PopulationDeclineThreshold = 8,
    SustainedLowHealth = 9,
    RealizedDeathMechanism = 10,
    SustainedResourcePressure = 11,
}

public enum NotableEventSignificance : byte
{
    Informational = 1,
    Strategic = 2,
    Critical = 3,
}

public sealed record NotableEvent(
    ulong EventId,
    NotableEventFamily Family,
    NotableEventSignificance Significance,
    uint SignificanceRuleVersion,
    ulong CompletedTick,
    ulong SimulatedHours,
    SpeciesId SpeciesId,
    SpeciesId? RelatedSpeciesId,
    TileId? TileId,
    ReactionId? ReactionId,
    ulong SourceEventId,
    ulong MilestoneValue,
    string DeduplicationKey,
    ulong BaselineValue = 0);

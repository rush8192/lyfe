using System.Collections.Immutable;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.Ticks;

namespace Lyfe.Simulation.Gameplay;

public enum OrganismJourneyEventFamily : byte
{
    Birth = 1,
    Reproduction = 2,
    ResourceAbsorption = 3,
    Feeding = 4,
    Migration = 5,
    Death = 6,
    Stress = 7,
    BehaviorTransition = 8,
}

public readonly record struct JourneyDeathCauseProbability(
    IntrinsicDeathCause Cause,
    uint ProbabilityQ,
    bool Triggered);

public sealed record OrganismJourneyEvent(
    ulong EventId,
    ulong Tick,
    TickPhase Phase,
    OrganismJourneyEventFamily Family,
    OrganismId SubjectOrganismId,
    SpeciesId SubjectSpeciesId,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    OrganismId? RelatedOrganismId,
    RemnantId? RelatedRemnantId,
    ResourceId? ResourceId,
    long AmountQ,
    uint DetailId,
    ImmutableArray<JourneyDeathCauseProbability> DeathCauseProbabilities);

public readonly record struct RoutineResourceAcquisition(
    ResourceId ResourceId,
    long AmountQ);

public sealed record OrganismRoutineActivitySummary(
    ulong BucketStartHour,
    uint PeriodHours,
    OrganismId SubjectOrganismId,
    SpeciesId SubjectSpeciesId,
    TileId TileId,
    ImmutableArray<RoutineResourceAcquisition> ResourceAcquisitions);

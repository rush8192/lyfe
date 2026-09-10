using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Gameplay;

public enum LineageReviewLandmarkKind : byte
{
    CooldownBoundary = 1,
    ProposalFollowUp = 2,
}

public enum LineageReviewEvidenceKind : byte
{
    GeneralOutcomes = 1,
    CapabilityActivation = 2,
    ConditionAndPressure = 3,
    GeographicSpread = 4,
    ReserveStorage = 5,
}

public enum LineageReviewObservationScope : byte
{
    WorldExact = 1,
    LiveTilesObserved = 2,
}

public enum LineageReviewEvidenceReferenceKind : byte
{
    SpeciationDecision = 1,
    ReviewedSpeciesSummary = 2,
    ComparisonSpeciesSummary = 3,
    ReviewedJourneyWindow = 4,
    LiveTileResourceWindow = 5,
}

public enum LineageReviewCapabilityKind : byte
{
    ResourceConservation = 1,
}

public sealed record LineageReviewEvidenceReference(
    LineageReviewEvidenceReferenceKind Kind,
    SpeciesId? SpeciesId,
    TileId? TileId,
    ulong FromExclusiveTick,
    ulong ThroughCompletedTick);

public readonly record struct LineageReviewBehaviorCount(
    OrganismBehaviorId BehaviorId,
    ulong Count);

public readonly record struct LineageReviewCapabilityActivation(
    LineageReviewCapabilityKind Kind,
    TraitId SourceTraitId,
    bool IntroducedByProposal,
    bool Installed,
    ulong ActivationCount);

public readonly record struct LineageReviewReactionActivation(
    ReactionId ReactionId,
    bool IntroducedByProposal,
    bool Installed,
    ulong ActivationCount);

public sealed record LineageReviewObservation(
    SpeciesId SpeciesId,
    LineageReviewObservationScope Scope,
    ulong Population,
    uint AverageHealthQ,
    uint AverageReserveQ,
    uint AverageAcquisitionCoverageQ,
    uint AverageResourcePressureQ,
    uint OccupiedTileCount,
    ImmutableArray<LineageReviewBehaviorCount> BehaviorCounts,
    bool ActivityCountsAvailable,
    ulong BirthCount,
    ulong DeathCount,
    ulong MigrationCount);

public sealed record LineageReviewSchedule(
    ulong SpeciationEventId,
    ulong AppliedTick,
    ulong AppliedSimulatedHours,
    SpeciesId AncestorSpeciesId,
    SpeciesId DescendantSpeciesId,
    SpeciesId PerspectiveSpeciesId,
    ImmutableArray<TraitId> TraitDelta,
    ulong CooldownBoundaryTick,
    ulong FollowUpBoundaryTick,
    ulong FollowUpHours,
    LineageReviewEvidenceKind FollowUpEvidenceKind,
    LineageReviewObservation PerspectiveBaseline,
    LineageReviewObservation ComparisonBaseline,
    ImmutableArray<LineageReviewCapabilityActivation> CapabilityActivations,
    ImmutableArray<LineageReviewReactionActivation> ReactionActivations);

public sealed record LineageReviewLandmark(
    ulong EventId,
    LineageReviewLandmarkKind Kind,
    ulong CompletedTick,
    ulong SimulatedHours,
    ulong WindowHours,
    ulong SpeciationEventId,
    SpeciesId AncestorSpeciesId,
    SpeciesId DescendantSpeciesId,
    SpeciesId PerspectiveSpeciesId,
    ImmutableArray<TraitId> TraitDelta,
    LineageReviewEvidenceKind EvidenceKind,
    LineageReviewObservation PerspectiveBaseline,
    LineageReviewObservation ComparisonBaseline,
    LineageReviewObservation PerspectiveCurrent,
    LineageReviewObservation ComparisonCurrent,
    ImmutableArray<LineageReviewCapabilityActivation> CapabilityActivations,
    ImmutableArray<LineageReviewReactionActivation> ReactionActivations,
    ImmutableArray<LineageReviewEvidenceReference> EvidenceReferences);

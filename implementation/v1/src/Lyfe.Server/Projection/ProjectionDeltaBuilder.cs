using System.Collections.Immutable;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Server.Projection;

public sealed record ProjectionStreamSnapshot(
    ulong ProjectionStreamId,
    ulong StreamRevision,
    ActorWorldProjection Projection);

public sealed record ProjectionDelta(
    ulong ProjectionStreamId,
    ulong BaseStreamRevision,
    ulong TargetStreamRevision,
    ulong FromExclusiveTick,
    ActorWorldProjection TargetBoundary,
    ImmutableArray<TileProjection> TileReplacements,
    ImmutableArray<TileId> RemovedTileIds,
    ImmutableArray<SpeciesProjection> SpeciesReplacements,
    ImmutableArray<SpeciesId> RemovedSpeciesIds,
    ImmutableArray<OrganismJourneyEventProjection> JourneyEventAppends);

public static class ProjectionDeltaBuilder
{
    public static ProjectionStreamSnapshot CreateSnapshot(
        ulong projectionStreamId,
        ulong streamRevision,
        ActorWorldProjection projection)
    {
        if (projectionStreamId == 0 || streamRevision == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectionStreamId),
                "Projection stream identity and revision must be nonzero.");
        }

        ArgumentNullException.ThrowIfNull(projection);
        return new ProjectionStreamSnapshot(projectionStreamId, streamRevision, projection);
    }

    public static ProjectionDelta CreateDelta(
        ProjectionStreamSnapshot prior,
        ActorWorldProjection current)
    {
        ArgumentNullException.ThrowIfNull(prior);
        ArgumentNullException.ThrowIfNull(current);
        var previous = prior.Projection;
        if (prior.ProjectionStreamId == 0 ||
            prior.StreamRevision == ulong.MaxValue ||
            previous.WorldId != current.WorldId ||
            previous.WorldRulesHash != current.WorldRulesHash ||
            previous.Width != current.Width ||
            previous.Height != current.Height ||
            previous.WrapX != current.WrapX ||
            previous.WrapY != current.WrapY ||
            current.CompletedTick < previous.CompletedTick ||
            current.WorldRevision < previous.WorldRevision)
        {
            throw new ArgumentException(
                "Projection snapshots are not compatible successive stream states.",
                nameof(current));
        }

        var previousTiles = UniqueBy(previous.Tiles, tile => tile.TileId);
        var currentTiles = UniqueBy(current.Tiles, tile => tile.TileId);
        var tileReplacements = currentTiles.Values
            .Where(tile =>
                !previousTiles.TryGetValue(tile.TileId, out var old) ||
                !TileEquivalent(old, tile))
            .OrderBy(tile => tile.TileId.Value)
            .ToImmutableArray();
        var removedTiles = previousTiles.Keys
            .Where(id => !currentTiles.ContainsKey(id))
            .OrderBy(id => id.Value)
            .ToImmutableArray();

        var previousSpecies = UniqueBy(previous.Species, species => species.SpeciesId);
        var currentSpecies = UniqueBy(current.Species, species => species.SpeciesId);
        var speciesReplacements = currentSpecies.Values
            .Where(species =>
                !previousSpecies.TryGetValue(species.SpeciesId, out var old) ||
                !SpeciesEquivalent(old, species))
            .OrderBy(species => species.SpeciesId.Value)
            .ToImmutableArray();
        var removedSpecies = previousSpecies.Keys
            .Where(id => !currentSpecies.ContainsKey(id))
            .OrderBy(id => id.Value)
            .ToImmutableArray();
        if (current.JourneyEvents.Length < previous.JourneyEvents.Length ||
            !previous.JourneyEvents.Select(value => value.EventId)
                .SequenceEqual(current.JourneyEvents
                    .Take(previous.JourneyEvents.Length)
                    .Select(value => value.EventId)))
        {
            throw new ArgumentException(
                "Journey events must be an append-only stream for a projection actor.",
                nameof(current));
        }
        var journeyEventAppends = current.JourneyEvents
            .Skip(previous.JourneyEvents.Length)
            .ToImmutableArray();

        return new ProjectionDelta(
            prior.ProjectionStreamId,
            prior.StreamRevision,
            checked(prior.StreamRevision + 1),
            previous.CompletedTick,
            current,
            tileReplacements,
            removedTiles,
            speciesReplacements,
            removedSpecies,
            journeyEventAppends);
    }

    private static bool TileEquivalent(TileProjection left, TileProjection right) =>
        (left, right) switch
        {
            (UnknownTileProjection a, UnknownTileProjection b) =>
                a.TileId == b.TileId && a.X == b.X && a.Y == b.Y,
            (ReducedTileProjection a, ReducedTileProjection b) =>
                a.TileId == b.TileId &&
                a.X == b.X &&
                a.Y == b.Y &&
                a.ElevationMeters == b.ElevationMeters &&
                a.ObservedAtTick == b.ObservedAtTick &&
                a.KnownPresentResourceIds.SequenceEqual(b.KnownPresentResourceIds),
            (LiveTileProjection a, LiveTileProjection b) =>
                a.TileId == b.TileId &&
                a.X == b.X &&
                a.Y == b.Y &&
                a.ElevationMeters == b.ElevationMeters &&
                a.ObservedAtTick == b.ObservedAtTick &&
                a.ResourceStocks.SequenceEqual(b.ResourceStocks) &&
                a.Organisms.SequenceEqual(b.Organisms) &&
                a.Remnants.SequenceEqual(b.Remnants) &&
                BehaviorDistributionsEquivalent(
                    a.BehaviorDistributions,
                    b.BehaviorDistributions),
            _ => false,
        };

    private static bool SpeciesEquivalent(SpeciesProjection left, SpeciesProjection right) =>
        left.SpeciesId == right.SpeciesId &&
        left.PopulationScope == right.PopulationScope &&
        left.Population == right.Population &&
        left.BehaviorCounts.SequenceEqual(right.BehaviorCounts) &&
        EvolutionEquivalent(left.Evolution, right.Evolution);

    private static bool EvolutionEquivalent(
        SpeciesEvolutionProjection? left,
        SpeciesEvolutionProjection? right) =>
        left is null && right is null ||
        left is not null && right is not null &&
        left.GenomeId == right.GenomeId &&
        string.Equals(left.GenomeHash, right.GenomeHash, StringComparison.Ordinal) &&
        left.AcquiredTraits.SequenceEqual(right.AcquiredTraits) &&
        left.MutationBalanceQ == right.MutationBalanceQ &&
        left.EvolutionRevision == right.EvolutionRevision &&
        left.SpeciationNotBeforeTick == right.SpeciationNotBeforeTick &&
        left.AverageHealthQ == right.AverageHealthQ &&
        left.LastMutationIncomeQ == right.LastMutationIncomeQ &&
        left.MutationIncomeModifierQ == right.MutationIncomeModifierQ &&
        left.ParentSpeciesId == right.ParentSpeciesId &&
        left.CreatedTick == right.CreatedTick &&
        left.ExtinctTick == right.ExtinctTick;

    private static bool BehaviorDistributionsEquivalent(
        ImmutableArray<BehaviorDistributionProjection> left,
        ImmutableArray<BehaviorDistributionProjection> right) =>
        left.Length == right.Length && left.Zip(right).All(pair =>
            pair.First.SpeciesId == pair.Second.SpeciesId &&
            pair.First.ObservedAtTick == pair.Second.ObservedAtTick &&
            pair.First.TotalObservedOrganisms == pair.Second.TotalObservedOrganisms &&
            pair.First.Counts.SequenceEqual(pair.Second.Counts));

    private static Dictionary<TKey, TValue> UniqueBy<TKey, TValue>(
        IEnumerable<TValue> values,
        Func<TValue, TKey> keySelector)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>();
        foreach (var value in values)
        {
            if (!result.TryAdd(keySelector(value), value))
            {
                throw new ArgumentException("Projection collections must contain unique stable IDs.");
            }
        }

        return result;
    }
}

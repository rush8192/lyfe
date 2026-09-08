using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.Spatial;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.World.Generation;

namespace Lyfe.Simulation.Ticks;

internal readonly record struct SpatialMovementCandidate(
    OrganismSnapshot Organism,
    CompiledSpatialProfile Profile,
    bool SourceIsTerrestrial);

internal sealed record SpatialMovementView(
    PhaseViewStamp Stamp,
    uint Width,
    uint Height,
    ImmutableArray<TileSnapshot> Tiles,
    ImmutableArray<SpatialMovementCandidate> Candidates) : IPhaseReadView
{
    public int WorkCount => Candidates.Length;
}

internal readonly record struct SpatialMovementMutation(
    OrganismId OrganismId,
    TileId SourceTileId,
    TileId DestinationTileId,
    uint PositionXQ,
    uint PositionYQ,
    long VelocityXQPerHour,
    long VelocityYQPerHour,
    long ActiveEnergyCostQ,
    SpatialVector BrownianDisplacement,
    bool AttemptedMigration,
    bool MigrationAdmitted);

internal sealed class SpatialMovementPhase :
    ScalarTickPhase<SpatialMovementView, SpatialMovementMutation,
        ImmutableArray<SpatialMovementMutation>>
{
    private const uint MovementOutcomeCategory = 1;

    public override TickPhase Phase => TickPhase.Movement;

    protected override PhaseExecutionClass ExecutionClass => PhaseExecutionClass.IndependentMap;

    protected override SpatialMovementView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var tiles = world.GetTileIdsInCanonicalOrder().Select(world.GetTile).ToImmutableArray();
        var candidates = world.GetOrganismIdsInCanonicalOrder()
            .Select(id =>
            {
                var organism = world.GetOrganism(id);
                return new SpatialMovementCandidate(
                    organism,
                    world.GetCompiledPhenotype(organism.SpeciesId).Physiology.Spatial,
                    world.GetTile(organism.TileId).ElevationMeters >= 0);
            })
            .ToImmutableArray();
        return new SpatialMovementView(
            CreateStamp(world, context),
            world.Rules.WorldProfile.Width,
            world.Rules.WorldProfile.Height,
            tiles,
            candidates);
    }

    protected override ImmutableArray<PhaseOutcome<SpatialMovementMutation>> Evaluate(
        SpatialMovementView view,
        TickExecutionContext context)
    {
        var outcomes = ImmutableArray.CreateBuilder<PhaseOutcome<SpatialMovementMutation>>(
            view.Candidates.Length);
        foreach (var candidate in view.Candidates)
        {
            var mutation = Resolve(view, candidate, context);
            outcomes.Add(new PhaseOutcome<SpatialMovementMutation>(
                view.Stamp,
                new OutcomeKey(
                    Phase,
                    MovementOutcomeCategory,
                    candidate.Organism.TileId.Value,
                    candidate.Organism.Id.Value,
                    mutation.DestinationTileId.Value,
                    0),
                mutation));
        }

        return outcomes.MoveToImmutable();
    }

    protected override ImmutableArray<SpatialMovementMutation> Preflight(
        SpatialMovementView view,
        ImmutableArray<PhaseOutcome<SpatialMovementMutation>> outcomes,
        TickExecutionContext context) => outcomes.Select(outcome => outcome.Payload).ToImmutableArray();

    protected override void Commit(
        MutableWorldState world,
        ImmutableArray<SpatialMovementMutation> plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        foreach (var mutation in plan)
        {
            if (mutation.ActiveEnergyCostQ > 0)
            {
                world.AdjustOrganismChargedReserve(
                    mutation.OrganismId,
                    -mutation.ActiveEnergyCostQ,
                    changes);
            }

            if (mutation.SourceTileId != mutation.DestinationTileId)
            {
                world.RelocateOrganism(
                    mutation.OrganismId,
                    mutation.DestinationTileId,
                    mutation.PositionXQ,
                    mutation.PositionYQ,
                    changes);
            }

            world.SetOrganismPosition(
                mutation.OrganismId,
                mutation.PositionXQ,
                mutation.PositionYQ,
                mutation.VelocityXQPerHour,
                mutation.VelocityYQPerHour,
                changes);
        }
    }

    internal static uint ComposeMigrationProbabilityQ(
        CompiledSpatialProfile profile,
        ulong activeOutwardQ,
        ulong passiveOutwardQ,
        bool mediumChanges,
        uint destinationCompatibilityQ)
    {
        var total = (UInt128)activeOutwardQ + passiveOutwardQ;
        var controlQ = total == 0
            ? 0U
            : checked((uint)((UInt128)activeOutwardQ * SpatialMath.RatioScale / total));
        var span = profile.ActiveMigrationProbabilityQ - profile.PassiveMigrationProbabilityQ;
        var probability = profile.PassiveMigrationProbabilityQ +
            checked((uint)((ulong)span * controlQ / SpatialMath.RatioScale));
        if (mediumChanges)
        {
            probability = checked((uint)((ulong)probability *
                profile.MediumTransitionFactorQ / SpatialMath.RatioScale));
        }

        destinationCompatibilityQ = Math.Clamp(
            destinationCompatibilityQ,
            profile.DestinationCompatibilityFloorQ,
            SpatialMath.RatioScale);
        return checked((uint)((ulong)probability * destinationCompatibilityQ /
            SpatialMath.RatioScale));
    }

    internal static SpatialMovementMutation Resolve(
        SpatialMovementView view,
        SpatialMovementCandidate candidate,
        TickExecutionContext context)
    {
        var organism = candidate.Organism;
        var profile = candidate.Profile;
        var radius = SpatialMath.BodyRadiusQ(profile, organism.StructuralMatterQ);
        var brownian = SpatialMath.SampleBrownian(
            context.Random,
            context.Tick,
            organism.Id,
            profile,
            radius,
            candidate.SourceIsTerrestrial,
            context.TickDurationHours);
        var activeVelocity = ClampVector(
            organism.Behavior.BehaviorId == OrganismBehaviorId.Conserving
                ? 0
                : organism.VelocityXQPerHour,
            organism.Behavior.BehaviorId == OrganismBehaviorId.Conserving
                ? 0
                : organism.VelocityYQPerHour,
            profile.ActiveSpeedLimitQPerHour);
        var active = new SpatialVector(
            checked(activeVelocity.XQ * context.TickDurationHours),
            checked(activeVelocity.YQ * context.TickDurationHours));
        var activeCost = ActiveMovementCost(profile, radius, active);
        if (activeCost > organism.ChargedReserveQ && activeCost > 0)
        {
            var affordable = (UInt128)organism.ChargedReserveQ * SpatialMath.RatioScale /
                (ulong)activeCost;
            var affordabilityQ = affordable >= SpatialMath.RatioScale
                ? SpatialMath.RatioScale
                : checked((uint)affordable);
            active = new SpatialVector(
                Scale(active.XQ, affordabilityQ),
                Scale(active.YQ, affordabilityQ));
            activeVelocity = new SpatialVector(
                Scale(activeVelocity.XQ, affordabilityQ),
                Scale(activeVelocity.YQ, affordabilityQ));
            activeCost = ActiveMovementCost(profile, radius, active);
        }

        var dx = checked(active.XQ + brownian.XQ);
        var dy = checked(active.YQ + brownian.YQ);
        var nextX = (long)organism.PositionXQ + dx;
        var nextY = (long)organism.PositionYQ + dy;
        var edge = FirstCrossedEdge(organism.PositionXQ, organism.PositionYQ, dx, dy);
        if (edge is null)
        {
            return NewMutation(
                organism,
                organism.TileId,
                (uint)nextX,
                (uint)nextY,
                activeVelocity,
                activeCost,
                brownian,
                false,
                false);
        }

        var (deltaTileX, deltaTileY, edgeId) = edge.Value switch
        {
            MovementEdge.West => (-1, 0, 0UL),
            MovementEdge.East => (1, 0, 1UL),
            MovementEdge.South => (0, -1, 2UL),
            _ => (0, 1, 3UL),
        };
        var neighborIndex = WorldGridTopology.Neighbor(
            view.Width,
            view.Height,
            organism.TileId.Value,
            deltaTileX,
            deltaTileY);
        var destination = neighborIndex is null
            ? organism.TileId
            : TileId.FromRowMajorIndex(neighborIndex.Value);
        var destinationIsTerrestrial = neighborIndex is not null &&
            view.Tiles[checked((int)neighborIndex.Value)].ElevationMeters >= 0;
        var hardEligible = neighborIndex is not null &&
            (!destinationIsTerrestrial || profile.CanOccupyTerrestrial);
        var activeOutward = Outward(edge.Value, active);
        var passiveOutward = Outward(edge.Value, brownian);
        var probabilityQ = hardEligible
            ? ComposeMigrationProbabilityQ(
                profile,
                activeOutward,
                passiveOutward,
                candidate.SourceIsTerrestrial != destinationIsTerrestrial,
                SpatialMath.RatioScale)
            : 0;
        var admitted = hardEligible && context.Random.Bernoulli(
            RandomAddress.Create(
                RandomDomains.MigrationAdmission,
                context.Tick,
                organism.Id.Value,
                edgeId),
            probabilityQ).Triggered;

        long finalX;
        long finalY;
        var finalVelocity = activeVelocity;
        if (admitted)
        {
            if (edge is MovementEdge.West or MovementEdge.East)
            {
                finalX = WrapCoordinate(nextX);
                finalY = Reflect(nextY);
            }
            else
            {
                finalX = Reflect(nextX);
                finalY = WrapCoordinate(nextY);
            }
        }
        else
        {
            finalX = Reflect(nextX);
            finalY = Reflect(nextY);
            finalVelocity = edge.Value switch
            {
                MovementEdge.West when finalVelocity.XQ < 0 => finalVelocity with { XQ = 0 },
                MovementEdge.East when finalVelocity.XQ > 0 => finalVelocity with { XQ = 0 },
                MovementEdge.South when finalVelocity.YQ < 0 => finalVelocity with { YQ = 0 },
                MovementEdge.North when finalVelocity.YQ > 0 => finalVelocity with { YQ = 0 },
                _ => finalVelocity,
            };
            destination = organism.TileId;
        }

        return NewMutation(
            organism,
            destination,
            checked((uint)finalX),
            checked((uint)finalY),
            finalVelocity,
            activeCost,
            brownian,
            true,
            admitted);
    }

    private static SpatialMovementMutation NewMutation(
        OrganismSnapshot organism,
        TileId destination,
        uint x,
        uint y,
        SpatialVector activeVelocity,
        long activeCost,
        SpatialVector brownian,
        bool attempted,
        bool admitted) => new(
            organism.Id,
            organism.TileId,
            destination,
            x,
            y,
            activeVelocity.XQ,
            activeVelocity.YQ,
            activeCost,
            brownian,
            attempted,
            admitted);

    private static SpatialVector ClampVector(long x, long y, uint maximum)
    {
        if (maximum == 0 || x == 0 && y == 0)
        {
            return maximum == 0 ? default : new SpatialVector(x, y);
        }

        var length = SpatialMath.IntegerSquareRoot(
            checked((UInt128)Int128.Abs(x) * (ulong)Int128.Abs(x) +
                (UInt128)Int128.Abs(y) * (ulong)Int128.Abs(y)));
        if (length <= maximum)
        {
            return new SpatialVector(x, y);
        }

        return new SpatialVector(
            checked((long)((Int128)x * maximum / length)),
            checked((long)((Int128)y * maximum / length)));
    }

    private static long ActiveMovementCost(
        CompiledSpatialProfile profile,
        uint radiusQ,
        SpatialVector displacement)
    {
        if (profile.MovementEnergyPerFounderRadiusQ == 0 ||
            displacement == default)
        {
            return 0;
        }

        var length = SpatialMath.IntegerSquareRoot(
            checked((UInt128)Int128.Abs(displacement.XQ) * (ulong)Int128.Abs(displacement.XQ) +
                (UInt128)Int128.Abs(displacement.YQ) * (ulong)Int128.Abs(displacement.YQ)));
        var effectiveRadius = Math.Max(SpatialMath.FounderRadiusQ,
            Math.Min(radiusQ, profile.MatureBodyRadiusQ));
        var numerator = (UInt128)length * profile.MovementEnergyPerFounderRadiusQ *
            effectiveRadius * effectiveRadius;
        var denominator = (UInt128)SpatialMath.FounderRadiusQ *
            SpatialMath.FounderRadiusQ * SpatialMath.FounderRadiusQ;
        return checked((long)((numerator + denominator - 1) / denominator));
    }

    private static MovementEdge? FirstCrossedEdge(uint x, uint y, long dx, long dy)
    {
        MovementEdge? xEdge = (long)x + dx < 0
            ? MovementEdge.West
            : (long)x + dx > uint.MaxValue
                ? MovementEdge.East
                : null;
        MovementEdge? yEdge = (long)y + dy < 0
            ? MovementEdge.South
            : (long)y + dy > uint.MaxValue
                ? MovementEdge.North
                : null;
        if (xEdge is null || yEdge is null)
        {
            return xEdge ?? yEdge;
        }

        var xDistance = xEdge == MovementEdge.West ? x : (ulong)uint.MaxValue - x;
        var yDistance = yEdge == MovementEdge.South ? y : (ulong)uint.MaxValue - y;
        var absX = (ulong)Math.Abs(dx);
        var absY = (ulong)Math.Abs(dy);
        return (UInt128)xDistance * absY <= (UInt128)yDistance * absX ? xEdge : yEdge;
    }

    private static ulong Outward(MovementEdge edge, SpatialVector value) => edge switch
    {
        MovementEdge.West => value.XQ < 0 ? checked((ulong)-value.XQ) : 0,
        MovementEdge.East => value.XQ > 0 ? checked((ulong)value.XQ) : 0,
        MovementEdge.South => value.YQ < 0 ? checked((ulong)-value.YQ) : 0,
        _ => value.YQ > 0 ? checked((ulong)value.YQ) : 0,
    };

    private static long Scale(long value, uint ratioQ) =>
        checked((long)((Int128)value * ratioQ / SpatialMath.RatioScale));

    private static long WrapCoordinate(long coordinate)
    {
        if (coordinate < 0)
        {
            return checked((long)SpatialMath.LocalOne + coordinate);
        }

        return coordinate > uint.MaxValue
            ? coordinate - checked((long)SpatialMath.LocalOne)
            : coordinate;
    }

    private static long Reflect(long coordinate)
    {
        while (coordinate < 0 || coordinate > uint.MaxValue)
        {
            coordinate = coordinate < 0
                ? -coordinate - 1
                : checked((long)(2UL * uint.MaxValue) - coordinate);
        }

        return coordinate;
    }

    private enum MovementEdge : byte
    {
        West,
        East,
        South,
        North,
    }
}

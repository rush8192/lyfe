using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.Spatial;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.Ticks;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class SpatialMechanicsTests
{
    [Fact]
    public void BodyRadiusUsesCubeRootStructureScalingAndConfiguredFloor()
    {
        var profile = Profile();

        Assert.Equal(SpatialMath.FounderRadiusQ, SpatialMath.BodyRadiusQ(profile, 1_000));
        Assert.Equal(3_329_021U, SpatialMath.BodyRadiusQ(profile, 500));
        Assert.Equal(profile.MinimumBodyRadiusQ, SpatialMath.BodyRadiusQ(profile, 1));
    }

    [Fact]
    public void DirectionTableHasExactAntipodesAndZeroAxisSums()
    {
        long xSum = 0;
        long ySum = 0;
        for (var index = 0; index < 256; index++)
        {
            var direction = SpatialMath.Direction(index);
            var antipode = SpatialMath.Direction((index + 128) & 255);
            Assert.Equal(-direction.XQ, antipode.XQ);
            Assert.Equal(-direction.YQ, antipode.YQ);
            xSum += direction.XQ;
            ySum += direction.YQ;
        }

        Assert.Equal(0, xSum);
        Assert.Equal(0, ySum);
    }

    [Fact]
    public void SpatialBinsUseAllCoordinateEdges()
    {
        Assert.Equal(0, SpatialEntityIndex.Bin(0, 0));
        Assert.Equal(1, SpatialEntityIndex.Bin(1U << 28, 0));
        Assert.Equal(16, SpatialEntityIndex.Bin(0, 1U << 28));
        Assert.Equal(255, SpatialEntityIndex.Bin(uint.MaxValue, uint.MaxValue));
    }

    [Fact]
    public void RemnantQueryAddsTargetRadiusAndNeverCrossesTiles()
    {
        var tile0 = TileId.FromRowMajorIndex(0);
        var tile1 = TileId.FromRowMajorIndex(1);
        var first = new IndexedRemnant(
            RemnantId.FromAllocatedValue(2), tile0, 130, 100, 20);
        var second = new IndexedRemnant(
            RemnantId.FromAllocatedValue(1), tile0, 140, 100, 20);
        var otherTile = new IndexedRemnant(
            RemnantId.FromAllocatedValue(3), tile1, 100, 100, 100);
        var index = new SpatialEntityIndex([], [first, second, otherTile]);

        var result = index.QueryRemnantsForFeeding(tile0, 100, 100, 10);

        Assert.Single(result);
        Assert.Equal(first.Id, result[0].Id);
    }

    [Fact]
    public void IndexedRemnantQueriesMatchExhaustiveOracleAcrossBinsAndEdges()
    {
        var pseudoRandom = new Random(1741);
        var remnants = Enumerable.Range(1, 500)
            .Select(ordinal => new IndexedRemnant(
                RemnantId.FromAllocatedValue((ulong)ordinal),
                TileId.FromRowMajorIndex((uint)(ordinal % 3)),
                ordinal == 1 ? 0U : ordinal == 2 ? uint.MaxValue : NextUInt32(pseudoRandom),
                ordinal == 3 ? 0U : ordinal == 4 ? uint.MaxValue : NextUInt32(pseudoRandom),
                checked((uint)pseudoRandom.Next(1, 8_388_609))))
            .ToArray();
        var index = new SpatialEntityIndex([], remnants);

        for (var query = 0; query < 100; query++)
        {
            var tile = TileId.FromRowMajorIndex((uint)(query % 3));
            var x = query == 0 ? 0U : query == 1 ? uint.MaxValue : NextUInt32(pseudoRandom);
            var y = query == 2 ? 0U : query == 3 ? uint.MaxValue : NextUInt32(pseudoRandom);
            var reach = checked((uint)pseudoRandom.Next(0, 67_108_865));
            var expected = remnants
                .Where(remnant => remnant.TileId == tile && SpatialMath.WithinRange(
                    x,
                    y,
                    remnant.PositionXQ,
                    remnant.PositionYQ,
                    (ulong)reach + remnant.BodyRadiusQ))
                .OrderBy(remnant => remnant.Id.Value)
                .Select(remnant => remnant.Id)
                .ToArray();

            Assert.Equal(
                expected,
                index.QueryRemnantsForFeeding(tile, x, y, reach).Select(value => value.Id));
        }
    }

    [Fact]
    public void MigrationProbabilityInterpolatesControlThenComposesMediumAndCompatibility()
    {
        var profile = Profile();

        Assert.Equal(100_000U, SpatialMovementPhase.ComposeMigrationProbabilityQ(
            profile, 0, 10, false, 1_000_000));
        Assert.Equal(800_000U, SpatialMovementPhase.ComposeMigrationProbabilityQ(
            profile, 10, 0, false, 1_000_000));
        Assert.Equal(337_500U, SpatialMovementPhase.ComposeMigrationProbabilityQ(
            profile, 10, 10, true, 1_000_000));
        Assert.Equal(6_750U, SpatialMovementPhase.ComposeMigrationProbabilityQ(
            profile, 10, 10, true, 20_000));
    }

    [Fact]
    public void BoundaryResolutionWrapsXButRejectsBoundedYAndCrossesAtMostOneTile()
    {
        var aquatic0 = new TileSnapshot(TileId.FromRowMajorIndex(0), 0, 0, -100);
        var aquatic1 = new TileSnapshot(TileId.FromRowMajorIndex(1), 1, 0, -100);
        var view = new SpatialMovementView(
            new PhaseViewStamp(1, 1, TickPhase.Movement, default, "rules"),
            2,
            1,
            [aquatic0, aquatic1],
            []);
        var east = OrganismAt(aquatic0.Id, uint.MaxValue - 1, 100);
        var eastResult = SpatialMovementPhase.Resolve(
            view,
            new SpatialMovementCandidate(east, Profile(), false),
            Context(new IndexedSpatialRandom(0, 0, true)));

        Assert.True(eastResult.AttemptedMigration);
        Assert.True(eastResult.MigrationAdmitted);
        Assert.Equal(aquatic1.Id, eastResult.DestinationTileId);
        Assert.InRange(eastResult.PositionXQ, 0U, 371_160U);

        var north = OrganismAt(aquatic0.Id, 100, uint.MaxValue - 1);
        var northResult = SpatialMovementPhase.Resolve(
            view,
            new SpatialMovementCandidate(north, Profile(), false),
            Context(new IndexedSpatialRandom(64, 0, true)));

        Assert.True(northResult.AttemptedMigration);
        Assert.False(northResult.MigrationAdmitted);
        Assert.Equal(aquatic0.Id, northResult.DestinationTileId);
        Assert.InRange(northResult.PositionYQ, uint.MaxValue - 371_160U, uint.MaxValue);
    }

    [Fact]
    public void BrownianSamplesDoNotAlterPersistentVelocityAndAreStableByAddress()
    {
        var profile = Profile();
        var random = new SemanticRandomOracle(RootRandomSeed.Parse(
            "0123456789abcdeffedcba9876543210"));
        var organismId = OrganismId.FromAllocatedValue(7);

        var first = SpatialMath.SampleBrownian(
            random, 11, organismId, profile, SpatialMath.FounderRadiusQ, false, 1);
        var second = SpatialMath.SampleBrownian(
            random, 11, organismId, profile, SpatialMath.FounderRadiusQ, false, 1);
        var terrestrial = SpatialMath.SampleBrownian(
            random, 11, organismId, profile, SpatialMath.FounderRadiusQ, true, 1);

        Assert.Equal(first, second);
        Assert.InRange(Math.Abs(terrestrial.XQ * 2 - first.XQ), 0, 1);
        Assert.InRange(Math.Abs(terrestrial.YQ * 2 - first.YQ), 0, 1);
    }

    [Fact]
    public void DiscreteBrownianMagnitudeTableMatchesFounderRms()
    {
        UInt128 squaredTotal = 0;
        for (ulong magnitude = 0; magnitude < 256; magnitude++)
        {
            var sample = SpatialMath.SampleBrownian(
                new IndexedSpatialRandom(0, magnitude),
                1,
                OrganismId.FromAllocatedValue(1),
                Profile(),
                SpatialMath.FounderRadiusQ,
                false,
                1);
            squaredTotal += (UInt128)Int128.Abs(sample.XQ) * (ulong)Int128.Abs(sample.XQ) +
                (UInt128)Int128.Abs(sample.YQ) * (ulong)Int128.Abs(sample.YQ);
        }

        var rms = SpatialMath.IntegerSquareRoot(squaredTotal / 256);
        Assert.InRange(Math.Abs((long)rms - SpatialMath.FounderBrownianRmsQ), 0, 1);
    }

    private static CompiledSpatialProfile Profile() => new(
        4_194_304,
        1_000,
        1_048_576,
        8_388_608,
        1_000_000,
        500_000,
        0,
        0,
        false,
        100_000,
        800_000,
        750_000,
        20_000);

    private static OrganismSnapshot OrganismAt(TileId tileId, uint x, uint y) => new(
        OrganismId.FromAllocatedValue(1),
        SpeciesId.FromAllocatedValue(1),
        tileId,
        x,
        y,
        0,
        0,
        0,
        0,
        LifecyclePhase.Mature,
        0,
        0,
        0,
        0,
        1_000,
        5_000,
        default,
        OrganismBehaviorState.Initial(0));

    private static TickExecutionContext Context(ISimulationRandom random) => new(
        1,
        1,
        1,
        "rules",
        NoTickFaultInjector.Instance,
        random,
        new TickScratch(1));

    private static uint NextUInt32(Random random)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        random.NextBytes(bytes);
        return BitConverter.ToUInt32(bytes);
    }

    private sealed class IndexedSpatialRandom(
        ulong direction,
        ulong magnitude,
        bool bernoulli = false) : ISimulationRandom
    {
        public ulong UniformBelow(RandomAddress address, ulong exclusiveUpper) =>
            address.DomainId == RandomDomains.BrownianDirection
                ? direction
                : address.DomainId == RandomDomains.BrownianMagnitude
                    ? magnitude
                    : throw new InvalidOperationException();

        public BernoulliDecision Bernoulli(RandomAddress address, uint probabilityQ) =>
            new(probabilityQ, null, null, bernoulli);

        public ulong UniformInclusive(RandomAddress address, ulong inclusiveUpper) =>
            throw new NotSupportedException();

        public RandomRank StableRank(RandomAddress address, ulong canonicalLogicalKey) =>
            throw new NotSupportedException();

        public ulong WeightedChoice(
            RandomAddress address,
            ReadOnlySpan<WeightedCandidate> candidates) => throw new NotSupportedException();

        public uint KeyedBinomial(RandomAddress address, uint trials, uint probabilityQ) =>
            throw new NotSupportedException();
    }
}

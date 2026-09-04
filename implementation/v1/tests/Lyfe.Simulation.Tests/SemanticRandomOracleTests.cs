using Lyfe.Simulation.Randomness;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class SemanticRandomOracleTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("fedcba98765432100123456789abcdef");

    [Fact]
    public void ResultsDependOnSemanticAddressNotCallOrder()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var addresses = Enumerable.Range(0, 64)
            .Select(index => RandomAddress.Create(
                RandomDomains.BrownianDirection,
                (ulong)(10_000 + index),
                (ulong)(2_000 + index * 7),
                0))
            .ToArray();

        var forward = addresses.ToDictionary(address => address, address => oracle.UniformBelow(address, 256));
        var reverse = addresses.Reverse().ToDictionary(address => address, address => oracle.UniformBelow(address, 256));

        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void SemanticCoordinatesAndDomainsProduceIndependentGoldenWords()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var baseline = RandomAddress.Create(RandomDomains.BrownianDirection, 19, 23, 29);
        var changedCoordinate = RandomAddress.Create(RandomDomains.BrownianDirection, 19, 23, 30);
        var changedDomain = RandomAddress.Create(RandomDomains.BrownianMagnitude, 19, 23, 29);

        Assert.Equal(0x946729AC6D507A6AUL, oracle.Raw64(baseline));
        Assert.Equal(0xACB376F3513F9089UL, oracle.Raw64(changedCoordinate));
        Assert.Equal(0x7FD2881126EA6A2AUL, oracle.Raw64(changedDomain));
    }

    [Fact]
    public void BernoulliHasExactEndpointsAndFrozenIntermediateThreshold()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var address = RandomAddress.Create(RandomDomains.SenescenceDeath, 500, 41, 0);

        var never = oracle.Bernoulli(address, 0);
        var sometimes = oracle.Bernoulli(address, 125_000);
        var always = oracle.Bernoulli(address, SemanticRandomOracle.ProbabilityScale);

        Assert.Equal(new BernoulliDecision(0, null, null, false), never);
        Assert.Equal((UInt128)1 << 61, sometimes.Threshold);
        Assert.NotNull(sometimes.RawWord);
        Assert.Equal(
            new BernoulliDecision(
                SemanticRandomOracle.ProbabilityScale,
                null,
                null,
                true),
            always);
    }

    [Fact]
    public void UniformBelowUsesDeterministicRejectionForAwkwardBound()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var address = RandomAddress.Create(RandomDomains.BrownianDirection, 4, 17, 0);
        const ulong bound = (1UL << 63) + 1;

        var result = oracle.UniformBelowWithSampleCount(address, bound, out var samplesUsed);

        Assert.Equal(3UL, samplesUsed);
        Assert.Equal(0x33CD4612C22CFB30UL, result);
        Assert.True(result < bound);
    }

    [Fact]
    public void UniformInclusiveSupportsEntireUInt64Range()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var address = RandomAddress.Create(RandomDomains.ReproductionCooldownJitter, 7, 3, 0);

        Assert.Equal(oracle.Raw64(address), oracle.UniformInclusive(address, ulong.MaxValue));
    }

    [Fact]
    public void UniformBelowHasReasonableSmallBoundSmokeDistribution()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var counts = new int[7];
        const int drawCount = 70_000;
        for (var index = 0; index < drawCount; index++)
        {
            var address = RandomAddress.Create(
                RandomDomains.BrownianDirection,
                (ulong)index,
                991,
                0);
            counts[checked((int)oracle.UniformBelow(address, (ulong)counts.Length))]++;
        }

        Assert.All(counts, count => Assert.InRange(count, 9_500, 10_500));
    }

    [Fact]
    public void DomainRejectsWrongTypedOperation()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var deathAddress = RandomAddress.Create(RandomDomains.SenescenceDeath, 1, 2, 0);

        Assert.Throws<InvalidOperationException>(() => oracle.UniformBelow(deathAddress, 5));
    }

    [Fact]
    public void StableRankUsesLogicalKeyAsDeterministicTieBreaker()
    {
        var first = new RandomRank(100, 3);
        var second = new RandomRank(100, 8);
        var lowerRandomWord = new RandomRank(99, ulong.MaxValue);

        Assert.True(first.CompareTo(second) < 0);
        Assert.True(lowerRandomWord.CompareTo(first) < 0);
    }

    [Fact]
    public void WeightedChoiceIsInvariantToEnumerationOrderAndIgnoresZeroWeights()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var address = RandomAddress.Create(RandomDomains.BehaviorChoice, 77, 88, 0);
        WeightedCandidate[] forward =
        [
            new(30, 25),
            new(10, 50),
            new(40, 0),
            new(20, 25),
        ];
        WeightedCandidate[] reverse = [.. forward.Reverse()];

        var first = oracle.WeightedChoice(address, forward);
        var second = oracle.WeightedChoice(address, reverse);

        Assert.Equal(first, second);
        Assert.Contains(first, new ulong[] { 10, 20, 30 });
    }

    [Fact]
    public void WeightedChoiceRejectsDuplicatePositiveKeysAndOverflow()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var address = RandomAddress.Create(RandomDomains.BehaviorChoice, 77, 88, 0);

        Assert.Throws<ArgumentException>(() => oracle.WeightedChoice(
            address,
            [new WeightedCandidate(1, 3), new WeightedCandidate(1, 4)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => oracle.WeightedChoice(
            address,
            [new WeightedCandidate(1, ulong.MaxValue), new WeightedCandidate(2, 1)]));
        Assert.Throws<ArgumentException>(() => oracle.WeightedChoice(
            address,
            [new WeightedCandidate(1, 0)]));
    }

    [Fact]
    public void KeyedBinomialMatchesPerTrialBernoulliReference()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var address = RandomAddress.Create(RandomDomains.MetabolicOpportunity, 90, 123, 7);
        const uint probabilityQ = 375_000;
        const uint trials = 41;
        var threshold = SemanticRandomOracle.CompileBernoulliThreshold(probabilityQ);
        var reference = 0U;
        for (var trial = 0UL; trial < trials; trial++)
        {
            if ((UInt128)oracle.Raw64(address.WithSampleIndex(trial)) < threshold)
            {
                reference++;
            }
        }

        Assert.Equal(reference, oracle.KeyedBinomial(address, trials, probabilityQ));
        Assert.Equal(0U, oracle.KeyedBinomial(address, trials, 0));
        Assert.Equal(
            trials,
            oracle.KeyedBinomial(address, trials, SemanticRandomOracle.ProbabilityScale));
    }

    [Fact]
    public void InvalidTypedInputsFailDeterministically()
    {
        var oracle = new SemanticRandomOracle(Seed);
        var rangeAddress = RandomAddress.Create(RandomDomains.BrownianDirection, 1, 2, 0);
        var deathAddress = RandomAddress.Create(RandomDomains.SenescenceDeath, 1, 2, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => oracle.UniformBelow(rangeAddress, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => oracle.Bernoulli(deathAddress, 1_000_001));
    }

    [Fact]
    public void Pack32IsInjectiveAcrossWordPositions()
    {
        Assert.Equal(0x0123456789ABCDEFUL, SemanticRandomOracle.Pack32(0x01234567, 0x89ABCDEF));
        Assert.NotEqual(
            SemanticRandomOracle.Pack32(1, 2),
            SemanticRandomOracle.Pack32(2, 1));
    }
}

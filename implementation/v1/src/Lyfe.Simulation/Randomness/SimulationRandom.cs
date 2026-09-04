namespace Lyfe.Simulation.Randomness;

public interface ISimulationRandom
{
    BernoulliDecision Bernoulli(RandomAddress address, uint probabilityQ);

    ulong UniformBelow(RandomAddress address, ulong exclusiveUpper);

    ulong UniformInclusive(RandomAddress address, ulong inclusiveUpper);

    RandomRank StableRank(RandomAddress address, ulong canonicalLogicalKey);

    ulong WeightedChoice(RandomAddress address, ReadOnlySpan<WeightedCandidate> candidates);

    uint KeyedBinomial(RandomAddress address, uint trials, uint probabilityQ);
}

public readonly record struct BernoulliDecision(
    uint ProbabilityQ,
    ulong? RawWord,
    UInt128? Threshold,
    bool Triggered);

public readonly record struct RandomRank(ulong RandomWord, ulong CanonicalLogicalKey) :
    IComparable<RandomRank>
{
    public static bool operator <(RandomRank left, RandomRank right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(RandomRank left, RandomRank right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(RandomRank left, RandomRank right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(RandomRank left, RandomRank right) =>
        left.CompareTo(right) >= 0;

    public int CompareTo(RandomRank other)
    {
        var randomComparison = RandomWord.CompareTo(other.RandomWord);
        return randomComparison != 0
            ? randomComparison
            : CanonicalLogicalKey.CompareTo(other.CanonicalLogicalKey);
    }
}

public readonly record struct WeightedCandidate(ulong CanonicalKey, ulong Weight);

public sealed class SemanticRandomOracle : ISimulationRandom
{
    public const uint ProbabilityScale = 1_000_000;

    private static readonly UInt128 FullUInt64Range = (UInt128)ulong.MaxValue + 1;
    private readonly RootRandomSeed seed;

    public SemanticRandomOracle(RootRandomSeed seed) => this.seed = seed;

    public BernoulliDecision Bernoulli(RandomAddress address, uint probabilityQ)
    {
        RequireBaseAddress(address);
        RandomDomainRegistry.RequireOperation(address.DomainId, RandomOperationKind.Bernoulli);
        return BernoulliCore(address, probabilityQ);
    }

    public ulong UniformBelow(RandomAddress address, ulong exclusiveUpper)
    {
        RequireBaseAddress(address);
        RandomDomainRegistry.RequireOperation(address.DomainId, RandomOperationKind.UniformBelow);
        return UniformBelowCore(address, exclusiveUpper);
    }

    public ulong UniformInclusive(RandomAddress address, ulong inclusiveUpper)
    {
        RequireBaseAddress(address);
        RandomDomainRegistry.RequireOperation(address.DomainId, RandomOperationKind.UniformBelow);
        if (inclusiveUpper == ulong.MaxValue)
        {
            return Raw64(address);
        }

        return UniformBelowCore(address, inclusiveUpper + 1);
    }

    public RandomRank StableRank(RandomAddress address, ulong canonicalLogicalKey)
    {
        RequireBaseAddress(address);
        RandomDomainRegistry.RequireOperation(address.DomainId, RandomOperationKind.StableRank);
        return new RandomRank(Raw64(address), canonicalLogicalKey);
    }

    public ulong WeightedChoice(
        RandomAddress address,
        ReadOnlySpan<WeightedCandidate> candidates)
    {
        RequireBaseAddress(address);
        RandomDomainRegistry.RequireOperation(address.DomainId, RandomOperationKind.WeightedChoice);

        var eligibleCount = 0;
        for (var index = 0; index < candidates.Length; index++)
        {
            if (candidates[index].Weight > 0)
            {
                eligibleCount++;
            }
        }

        if (eligibleCount == 0)
        {
            throw new ArgumentException(
                "Weighted choice requires at least one positive-weight candidate.",
                nameof(candidates));
        }

        var ordered = new WeightedCandidate[eligibleCount];
        var destination = 0;
        for (var index = 0; index < candidates.Length; index++)
        {
            if (candidates[index].Weight > 0)
            {
                ordered[destination++] = candidates[index];
            }
        }

        Array.Sort(
            ordered,
            static (left, right) => left.CanonicalKey.CompareTo(right.CanonicalKey));

        UInt128 total = 0;
        for (var index = 0; index < ordered.Length; index++)
        {
            if (index > 0 && ordered[index - 1].CanonicalKey == ordered[index].CanonicalKey)
            {
                throw new ArgumentException(
                    $"Duplicate positive-weight candidate key {ordered[index].CanonicalKey}.",
                    nameof(candidates));
            }

            total += ordered[index].Weight;
            if (total > ulong.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(candidates),
                    "The sum of candidate weights exceeds UInt64.MaxValue.");
            }
        }

        var target = UniformBelowCore(address, (ulong)total);
        UInt128 cumulative = 0;
        for (var index = 0; index < ordered.Length; index++)
        {
            cumulative += ordered[index].Weight;
            if (target < cumulative)
            {
                return ordered[index].CanonicalKey;
            }
        }

        throw new InvalidOperationException("Weighted choice failed to select a candidate.");
    }

    public uint KeyedBinomial(RandomAddress address, uint trials, uint probabilityQ)
    {
        RequireBaseAddress(address);
        RandomDomainRegistry.RequireOperation(address.DomainId, RandomOperationKind.KeyedBinomial);
        ValidateProbability(probabilityQ);
        if (trials == 0 || probabilityQ == 0)
        {
            return 0;
        }

        if (probabilityQ == ProbabilityScale)
        {
            return trials;
        }

        var threshold = CompileBernoulliThreshold(probabilityQ);
        var successes = 0U;
        var trial = 0UL;
        while (trial < trials)
        {
            var block = RawBlock(address, checked((uint)(trial / 4)));
            for (var lane = 0; lane < 4 && trial < trials; lane++, trial++)
            {
                if ((UInt128)block.WordAt(lane) < threshold)
                {
                    successes++;
                }
            }
        }

        return successes;
    }

    public static ulong Pack32(uint high, uint low) => ((ulong)high << 32) | low;

    internal ulong Raw64(RandomAddress address)
    {
        var blockIndex = checked((uint)(address.SampleIndex / 4));
        var block = RawBlock(address, blockIndex);
        return block.WordAt((int)(address.SampleIndex % 4));
    }

    internal ulong UniformBelowWithSampleCount(
        RandomAddress address,
        ulong exclusiveUpper,
        out ulong samplesUsed) =>
        UniformBelowCore(address, exclusiveUpper, out samplesUsed);

    internal static UInt128 CompileBernoulliThreshold(uint probabilityQ)
    {
        ValidateProbability(probabilityQ);
        return (UInt128)probabilityQ * FullUInt64Range / ProbabilityScale;
    }

    private BernoulliDecision BernoulliCore(RandomAddress address, uint probabilityQ)
    {
        ValidateProbability(probabilityQ);
        if (probabilityQ == 0)
        {
            return new BernoulliDecision(probabilityQ, null, null, false);
        }

        if (probabilityQ == ProbabilityScale)
        {
            return new BernoulliDecision(probabilityQ, null, null, true);
        }

        var threshold = CompileBernoulliThreshold(probabilityQ);
        var rawWord = Raw64(address);
        return new BernoulliDecision(
            probabilityQ,
            rawWord,
            threshold,
            (UInt128)rawWord < threshold);
    }

    private ulong UniformBelowCore(RandomAddress address, ulong exclusiveUpper) =>
        UniformBelowCore(address, exclusiveUpper, out _);

    private ulong UniformBelowCore(
        RandomAddress address,
        ulong exclusiveUpper,
        out ulong samplesUsed)
    {
        ArgumentOutOfRangeException.ThrowIfZero(exclusiveUpper);
        var acceptanceLimit = FullUInt64Range - (FullUInt64Range % exclusiveUpper);
        var sampleIndex = address.SampleIndex;
        samplesUsed = 0;
        while (true)
        {
            var value = Raw64(address.WithSampleIndex(sampleIndex));
            sampleIndex = checked(sampleIndex + 1);
            samplesUsed++;
            if ((UInt128)value < acceptanceLimit)
            {
                return value % exclusiveUpper;
            }
        }
    }

    private PhiloxBlock RawBlock(RandomAddress address, uint blockIndex)
    {
        var counter = new PhiloxBlock(
            ((ulong)address.DomainId.Value << 32) | blockIndex,
            address.Coordinate0,
            address.Coordinate1,
            address.Coordinate2);
        return Philox4x64.Generate(counter, new PhiloxKey(seed.Low, seed.High));
    }

    private static void RequireBaseAddress(RandomAddress address)
    {
        if (address.SampleIndex != 0)
        {
            throw new ArgumentException(
                "Typed random operations require a base address with sample index zero.",
                nameof(address));
        }
    }

    private static void ValidateProbability(uint probabilityQ)
    {
        if (probabilityQ > ProbabilityScale)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probabilityQ),
                probabilityQ,
                $"Probability must be at most {ProbabilityScale}.");
        }
    }
}

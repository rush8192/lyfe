using System.Globalization;

namespace Lyfe.Simulation.State.Identity;

public readonly record struct TileId
{
    private TileId(uint value) => Value = value;

    public uint Value { get; }

    public static TileId FromRowMajorIndex(uint value) => new(value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct GenomeId
{
    private GenomeId(ulong value) => Value = value;

    public ulong Value { get; }

    internal static GenomeId FromAllocatedValue(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        return new GenomeId(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct SpeciesId
{
    private SpeciesId(ulong value) => Value = value;

    public ulong Value { get; }

    internal static SpeciesId FromAllocatedValue(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        return new SpeciesId(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct OrganismId
{
    private OrganismId(ulong value) => Value = value;

    public ulong Value { get; }

    internal static OrganismId FromAllocatedValue(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        return new OrganismId(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct RemnantId
{
    private RemnantId(ulong value) => Value = value;

    public ulong Value { get; }

    internal static RemnantId FromAllocatedValue(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        return new RemnantId(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

internal sealed class WorldEntityIdAllocator
{
    private ulong nextGenomeId = 1;
    private ulong nextSpeciesId = 1;
    private ulong nextOrganismId = 1;
    private ulong nextRemnantId = 1;

    public ulong MutationEpoch { get; private set; }

    public GenomeId AllocateGenome()
    {
        var id = GenomeId.FromAllocatedValue(nextGenomeId);
        nextGenomeId = checked(nextGenomeId + 1);
        MutationEpoch = checked(MutationEpoch + 1);
        return id;
    }

    public SpeciesId AllocateSpecies()
    {
        var id = SpeciesId.FromAllocatedValue(nextSpeciesId);
        nextSpeciesId = checked(nextSpeciesId + 1);
        MutationEpoch = checked(MutationEpoch + 1);
        return id;
    }

    public OrganismId AllocateOrganism()
    {
        var id = OrganismId.FromAllocatedValue(nextOrganismId);
        nextOrganismId = checked(nextOrganismId + 1);
        MutationEpoch = checked(MutationEpoch + 1);
        return id;
    }

    public RemnantId AllocateRemnant()
    {
        var id = RemnantId.FromAllocatedValue(nextRemnantId);
        nextRemnantId = checked(nextRemnantId + 1);
        MutationEpoch = checked(MutationEpoch + 1);
        return id;
    }

    public WorldEntityIdContinuationState CaptureContinuationState() =>
        new(nextGenomeId, nextSpeciesId, nextOrganismId, nextRemnantId);

    public void RestoreContinuationState(WorldEntityIdContinuationState state)
    {
        if (state.NextGenomeId == 0 ||
            state.NextSpeciesId == 0 ||
            state.NextOrganismId == 0 ||
            state.NextRemnantId == 0 ||
            MutationEpoch != 0)
        {
            throw new ArgumentException(
                "Entity ID continuation values must be nonzero and restored only once.",
                nameof(state));
        }

        nextGenomeId = state.NextGenomeId;
        nextSpeciesId = state.NextSpeciesId;
        nextOrganismId = state.NextOrganismId;
        nextRemnantId = state.NextRemnantId;
    }

    public ulong ComputeCoverageFingerprint()
    {
        var hash = new ShadowHashAccumulator();
        hash.Add(nextGenomeId);
        hash.Add(nextSpeciesId);
        hash.Add(nextOrganismId);
        hash.Add(nextRemnantId);
        return hash.Value;
    }
}

internal readonly record struct WorldEntityIdContinuationState(
    ulong NextGenomeId,
    ulong NextSpeciesId,
    ulong NextOrganismId,
    ulong NextRemnantId);

internal struct ShadowHashAccumulator
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;
    private ulong value;

    public readonly ulong Value => value == 0 ? OffsetBasis : value;

    public void Add(ulong input)
    {
        if (value == 0)
        {
            value = OffsetBasis;
        }

        for (var index = 0; index < sizeof(ulong); index++)
        {
            value ^= (byte)(input >> (index * 8));
            value *= Prime;
        }
    }

    public void Add(long input) => Add(unchecked((ulong)input));

    public void Add(uint input) => Add((ulong)input);

    public void Add(int input) => Add((long)input);
}

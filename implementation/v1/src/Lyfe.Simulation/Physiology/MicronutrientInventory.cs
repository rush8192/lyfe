using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Simulation.Physiology;

/// <summary>
/// Fixed-width, allocation-free storage for the fourteen v1 micronutrients.
/// Slots are canonical and deliberately independent of rule-pack dense resource slots.
/// </summary>
public readonly record struct MicronutrientInventory(
    long CalciumQ,
    long IronQ,
    long PotassiumQ,
    long SodiumQ,
    long MagnesiumQ,
    long ZincQ,
    long CopperQ,
    long IodineQ,
    long FluorideQ,
    long SeleniumQ,
    long ManganeseQ,
    long MolybdenumQ,
    long NickelQ,
    long CobaltQ)
{
    public const int Count = 14;
    public const uint FirstResourceId = 18;

    public static MicronutrientInventory Empty => default;

    public long TotalLoadQ => checked(
        CalciumQ + IronQ + PotassiumQ + SodiumQ + MagnesiumQ + ZincQ + CopperQ +
        IodineQ + FluorideQ + SeleniumQ + ManganeseQ + MolybdenumQ + NickelQ + CobaltQ);

    public long this[int slot] => slot switch
    {
        0 => CalciumQ,
        1 => IronQ,
        2 => PotassiumQ,
        3 => SodiumQ,
        4 => MagnesiumQ,
        5 => ZincQ,
        6 => CopperQ,
        7 => IodineQ,
        8 => FluorideQ,
        9 => SeleniumQ,
        10 => ManganeseQ,
        11 => MolybdenumQ,
        12 => NickelQ,
        13 => CobaltQ,
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };

    public MicronutrientInventory With(int slot, long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return slot switch
        {
            0 => this with { CalciumQ = value },
            1 => this with { IronQ = value },
            2 => this with { PotassiumQ = value },
            3 => this with { SodiumQ = value },
            4 => this with { MagnesiumQ = value },
            5 => this with { ZincQ = value },
            6 => this with { CopperQ = value },
            7 => this with { IodineQ = value },
            8 => this with { FluorideQ = value },
            9 => this with { SeleniumQ = value },
            10 => this with { ManganeseQ = value },
            11 => this with { MolybdenumQ = value },
            12 => this with { NickelQ = value },
            13 => this with { CobaltQ = value },
            _ => throw new ArgumentOutOfRangeException(nameof(slot)),
        };
    }

    public MicronutrientInventory Add(MicronutrientInventory other) => new(
        checked(CalciumQ + other.CalciumQ),
        checked(IronQ + other.IronQ),
        checked(PotassiumQ + other.PotassiumQ),
        checked(SodiumQ + other.SodiumQ),
        checked(MagnesiumQ + other.MagnesiumQ),
        checked(ZincQ + other.ZincQ),
        checked(CopperQ + other.CopperQ),
        checked(IodineQ + other.IodineQ),
        checked(FluorideQ + other.FluorideQ),
        checked(SeleniumQ + other.SeleniumQ),
        checked(ManganeseQ + other.ManganeseQ),
        checked(MolybdenumQ + other.MolybdenumQ),
        checked(NickelQ + other.NickelQ),
        checked(CobaltQ + other.CobaltQ));

    public MicronutrientInventory Subtract(MicronutrientInventory other) => new(
        checked(CalciumQ - other.CalciumQ),
        checked(IronQ - other.IronQ),
        checked(PotassiumQ - other.PotassiumQ),
        checked(SodiumQ - other.SodiumQ),
        checked(MagnesiumQ - other.MagnesiumQ),
        checked(ZincQ - other.ZincQ),
        checked(CopperQ - other.CopperQ),
        checked(IodineQ - other.IodineQ),
        checked(FluorideQ - other.FluorideQ),
        checked(SeleniumQ - other.SeleniumQ),
        checked(ManganeseQ - other.ManganeseQ),
        checked(MolybdenumQ - other.MolybdenumQ),
        checked(NickelQ - other.NickelQ),
        checked(CobaltQ - other.CobaltQ));

    public bool Contains(MicronutrientInventory required)
    {
        for (var slot = 0; slot < Count; slot++)
        {
            if (this[slot] < required[slot]) return false;
        }
        return true;
    }

    public ImmutableArray<long> ToImmutableArray()
    {
        var result = ImmutableArray.CreateBuilder<long>(Count);
        for (var slot = 0; slot < Count; slot++) result.Add(this[slot]);
        return result.MoveToImmutable();
    }

    public static MicronutrientInventory FromValues(IEnumerable<long> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var array = values.ToArray();
        if (array.Length != Count || array.Any(value => value < 0))
        {
            throw new ArgumentException($"A micronutrient vector must contain {Count} nonnegative values.", nameof(values));
        }
        return new MicronutrientInventory(
            array[0], array[1], array[2], array[3], array[4], array[5], array[6],
            array[7], array[8], array[9], array[10], array[11], array[12], array[13]);
    }

    public static int Slot(ResourceId resourceId)
    {
        if (resourceId.Value < FirstResourceId || resourceId.Value >= FirstResourceId + Count)
        {
            throw new ArgumentOutOfRangeException(nameof(resourceId), "The resource is not a v1 micronutrient.");
        }
        return checked((int)(resourceId.Value - FirstResourceId));
    }

    public static ResourceId ResourceIdAt(int slot)
    {
        if ((uint)slot >= Count) throw new ArgumentOutOfRangeException(nameof(slot));
        return ResourceId.From(checked(FirstResourceId + (uint)slot));
    }

    public static MicronutrientInventory Compile(
        IEnumerable<CompiledResourceTerm> terms)
    {
        var result = Empty;
        foreach (var term in terms)
        {
            var slot = Slot(term.Resource.Id);
            result = result.With(slot, checked(result[slot] + term.Quantity));
        }
        return result;
    }
}

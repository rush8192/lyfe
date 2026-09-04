using System.Globalization;

namespace Lyfe.Simulation.Core;

public readonly record struct WorldId
{
    private WorldId(ulong value)
    {
        Value = value;
    }

    public ulong Value { get; }

    public bool IsValid => Value != 0;

    public static WorldId From(ulong value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        return new WorldId(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

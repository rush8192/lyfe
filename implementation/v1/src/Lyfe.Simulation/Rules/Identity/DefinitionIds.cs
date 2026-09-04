using System.Globalization;

namespace Lyfe.Simulation.Rules.Identity;

public readonly record struct ResourceId
{
    private ResourceId(uint value) => Value = value;

    public uint Value { get; }

    public static ResourceId From(uint value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        return new ResourceId(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct ReactionId
{
    private ReactionId(uint value) => Value = value;

    public uint Value { get; }

    public static ReactionId From(uint value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        return new ReactionId(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct FounderGenomeId
{
    private FounderGenomeId(uint value) => Value = value;

    public uint Value { get; }

    public static FounderGenomeId From(uint value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        return new FounderGenomeId(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public readonly record struct ScenarioId
{
    private ScenarioId(uint value) => Value = value;

    public uint Value { get; }

    public static ScenarioId From(uint value)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        return new ScenarioId(value);
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}


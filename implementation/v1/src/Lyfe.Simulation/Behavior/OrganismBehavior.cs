namespace Lyfe.Simulation.Behavior;

public enum OrganismBehaviorId : byte
{
    Baseline = 1,
    Conserving = 2,
    Foraging = 3,
    Dispersing = 4,
    Fleeing = 5,
}

public enum BehaviorTargetKind : byte
{
    None = 0,
    Organism = 1,
    Remnant = 2,
    Edge = 3,
    LocalPoint = 4,
}

internal readonly record struct OrganismBehaviorState(
    OrganismBehaviorId BehaviorId,
    BehaviorTargetKind TargetKind,
    ulong TargetId,
    uint TargetPositionXQ,
    uint TargetPositionYQ,
    ulong SelectedAtTick,
    ulong MinimumDwellUntilTick,
    uint RecentEnergyCoverageQ,
    uint RecentAcquisitionCoverageQ,
    uint LimitingMaterialDeficitQ)
{
    public const uint NeutralCoverageQ = 1_000_000;

    public static OrganismBehaviorState Initial(ulong selectedAtTick) => new(
        OrganismBehaviorId.Baseline,
        BehaviorTargetKind.None,
        0,
        0,
        0,
        selectedAtTick,
        selectedAtTick,
        NeutralCoverageQ,
        NeutralCoverageQ,
        0);
}

internal static class BehaviorMath
{
    public const uint OneQ = 1_000_000;
    public const uint MaximumCoverageQ = 2_000_000;

    public static uint UpdateOneDayEma(uint previousQ, uint sampleQ, uint tickDurationHours)
    {
        if (previousQ > MaximumCoverageQ || sampleQ > MaximumCoverageQ)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleQ));
        }

        var retainedHours = 24U - Math.Min(24U, tickDurationHours);
        var incomingHours = Math.Min(24U, tickDurationHours);
        var numerator = checked((ulong)retainedHours * previousQ +
            (ulong)incomingHours * sampleQ);
        return checked((uint)((numerator + 12UL) / 24UL));
    }

    public static uint FractionQ(long numerator, long denominator, uint capQ = OneQ)
    {
        if (numerator < 0 || denominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numerator));
        }

        var scaled = (UInt128)(ulong)numerator * OneQ;
        var rounded = (scaled + (ulong)denominator / 2UL) / (ulong)denominator;
        return checked((uint)(rounded > capQ ? capQ : rounded));
    }
}

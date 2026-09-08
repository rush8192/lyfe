using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Simulation.Mechanics;

public static class GasAccessibility
{
    private const int SurfaceReferenceMeters = 100;

    public static long AccessibleQuantity(
        long tileStockQ,
        int elevationMeters,
        uint baselineVolcanismQ,
        GasAccessibilityClass accessibilityClass)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tileStockQ);
        if (elevationMeters >= 0 ||
            accessibilityClass == GasAccessibilityClass.VentAccessible ||
            (accessibilityClass == GasAccessibilityClass.MixedOrigin && baselineVolcanismQ > 0))
        {
            return tileStockQ;
        }

        var depthMeters = checked(-(long)elevationMeters);
        return checked((long)((Int128)tileStockQ * SurfaceReferenceMeters /
            (SurfaceReferenceMeters + depthMeters)));
    }
}

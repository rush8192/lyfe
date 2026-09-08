using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Ticks;

internal enum OrganismActionProcessKind : byte
{
    BiomassGrowth = 1,
    Reproduction = 2,
}

internal enum OrganismActionGateReason : byte
{
    MissingCapability = 1,
    BehaviorSuppressed = 2,
    CooldownActive = 3,
    HealthBelowMinimum = 4,
    StructureBelowMinimum = 5,
    ReserveBelowMinimum = 6,
    ConstitutiveMicronutrientQuotaMissing = 7,
    OffspringMicronutrientQuotaMissing = 8,
    MaintenanceShortfall = 9,
    ReserveProtectionFloor = 10,
    InternalCapacity = 11,
    ResourceSupply = 12,
    ClaimContention = 13,
    LifecycleIneligible = 14,
}

internal readonly record struct OrganismActionGateSample(
    OrganismId OrganismId,
    OrganismActionProcessKind Process,
    OrganismActionGateReason Reason,
    long AvailableQ,
    long RequiredQ,
    ResourceId? ResourceId,
    ulong ClearsAtTick);

using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.Ticks;

namespace Lyfe.Simulation.Ledger;

public enum MatterAccountOwnerKind : byte
{
    Tile = 1,
    Organism = 2,
    Boundary = 3,
}

public enum MatterCompartment : byte
{
    Atmosphere = 1,
    OrganicPool = 2,
    InorganicPool = 3,
    EnergyReserve = 4,
    OceanWaterBoundary = 5,
}

public enum EnergyAccountKind : byte
{
    ChemicalOpportunity = 1,
    StoredChemical = 2,
    DissipatedHeat = 3,
}

public enum LedgerCause : byte
{
    ExternalEnergyCapture = 1,
}

public readonly record struct LedgerTransactionKey(
    ulong Tick,
    TickPhase Phase,
    ulong ScopeId,
    ulong ActorId,
    uint ReactionId,
    uint LocalOrdinal) : IComparable<LedgerTransactionKey>
{
    public static bool operator <(LedgerTransactionKey left, LedgerTransactionKey right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(LedgerTransactionKey left, LedgerTransactionKey right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(LedgerTransactionKey left, LedgerTransactionKey right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(LedgerTransactionKey left, LedgerTransactionKey right) =>
        left.CompareTo(right) >= 0;

    public int CompareTo(LedgerTransactionKey other)
    {
        var comparison = Tick.CompareTo(other.Tick);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Phase.CompareTo(other.Phase);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = ScopeId.CompareTo(other.ScopeId);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = ActorId.CompareTo(other.ActorId);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = ReactionId.CompareTo(other.ReactionId);
        return comparison != 0 ? comparison : LocalOrdinal.CompareTo(other.LocalOrdinal);
    }
}

public readonly record struct MatterAccountKey(
    MatterAccountOwnerKind OwnerKind,
    ulong OwnerId,
    MatterCompartment Compartment,
    ResourceId ResourceId) : IComparable<MatterAccountKey>
{
    public static bool operator <(MatterAccountKey left, MatterAccountKey right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(MatterAccountKey left, MatterAccountKey right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(MatterAccountKey left, MatterAccountKey right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(MatterAccountKey left, MatterAccountKey right) =>
        left.CompareTo(right) >= 0;

    public int CompareTo(MatterAccountKey other)
    {
        var comparison = OwnerKind.CompareTo(other.OwnerKind);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = OwnerId.CompareTo(other.OwnerId);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Compartment.CompareTo(other.Compartment);
        return comparison != 0 ? comparison : ResourceId.Value.CompareTo(other.ResourceId.Value);
    }
}

public readonly record struct MatterLedgerEntry(MatterAccountKey Account, long DeltaQ);

public readonly record struct EnergyAccountKey(EnergyAccountKind Kind, ulong OwnerId) :
    IComparable<EnergyAccountKey>
{
    public static bool operator <(EnergyAccountKey left, EnergyAccountKey right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(EnergyAccountKey left, EnergyAccountKey right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(EnergyAccountKey left, EnergyAccountKey right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(EnergyAccountKey left, EnergyAccountKey right) =>
        left.CompareTo(right) >= 0;

    public int CompareTo(EnergyAccountKey other)
    {
        var comparison = Kind.CompareTo(other.Kind);
        return comparison != 0 ? comparison : OwnerId.CompareTo(other.OwnerId);
    }
}

public readonly record struct EnergyLedgerEntry(EnergyAccountKey Account, long DeltaQ);

public sealed record ResourceTransaction(
    LedgerTransactionKey Key,
    LedgerCause Cause,
    ReactionId ReactionId,
    OrganismId ActorId,
    TileId TileId,
    long Extent,
    ImmutableArray<MatterLedgerEntry> MatterEntries,
    ImmutableArray<EnergyLedgerEntry> EnergyEntries);

public readonly record struct ElementReconciliation(
    ChemicalElement Element,
    Int128 NetQ);

public sealed record LedgerReconciliationReport(
    int TransactionCount,
    ImmutableArray<ElementReconciliation> Elements,
    Int128 NetEnergyQ)
{
    public bool IsBalanced =>
        NetEnergyQ == 0 && Elements.All(element => element.NetQ == 0);
}

internal static class ExternalCaptureTransactionFactory
{
    public static ResourceTransaction Create(
        CompiledRulePack rules,
        ulong tick,
        TileId tileId,
        OrganismId organismId,
        ReactionHandle reactionHandle,
        long extent)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extent);
        var reaction = RequireReaction(rules, reactionHandle);
        if (reaction.ProcessKind != ProcessKind.ExternalEnergyCapture)
        {
            throw new ArgumentException(
                "The reaction is not an external energy-capture process.",
                nameof(reactionHandle));
        }

        var internalOutputQ = reaction.Outputs
            .Where(output =>
                RequireResource(rules, output.Resource).BiologicalForm !=
                BiologicalForm.Boundary)
            .Aggregate(0L, static (sum, output) => checked(sum + output.Quantity));
        if (internalOutputQ != reaction.StoredEnergyQ)
        {
            throw new InvalidOperationException(
                "The first external-capture slice requires one-energy-per-quantum internal reserve output.");
        }

        var matter = new List<MatterLedgerEntry>();
        foreach (var input in reaction.Inputs)
        {
            var resource = RequireResource(rules, input.Resource);
            if (resource.BiologicalForm == BiologicalForm.Boundary)
            {
                throw new InvalidOperationException(
                    "The first external-capture slice does not support boundary matter inputs.");
            }

            matter.Add(new MatterLedgerEntry(
                new MatterAccountKey(
                    MatterAccountOwnerKind.Tile,
                    tileId.Value,
                    TileCompartment(resource),
                    resource.Id),
                checked(-input.Quantity * extent)));
        }

        foreach (var output in reaction.Outputs)
        {
            var resource = RequireResource(rules, output.Resource);
            var amount = checked(output.Quantity * extent);
            matter.Add(resource.BiologicalForm == BiologicalForm.Boundary
                ? new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Boundary,
                        1,
                        MatterCompartment.OceanWaterBoundary,
                        resource.Id),
                    amount)
                : new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Organism,
                        organismId.Value,
                        MatterCompartment.EnergyReserve,
                        resource.Id),
                    amount));
        }

        var grossEnergy = checked(reaction.GrossEnergyQ * extent);
        var storedEnergy = checked(reaction.StoredEnergyQ * extent);
        var dissipatedEnergy = checked(reaction.DissipatedEnergyQ * extent);
        var key = new LedgerTransactionKey(
            tick,
            TickPhase.ExternalResolution,
            tileId.Value,
            organismId.Value,
            reaction.Id.Value,
            0);
        return new ResourceTransaction(
            key,
            LedgerCause.ExternalEnergyCapture,
            reaction.Id,
            organismId,
            tileId,
            extent,
            ConsolidateMatter(matter),
            [
                new EnergyLedgerEntry(
                    new EnergyAccountKey(
                        EnergyAccountKind.ChemicalOpportunity,
                        reaction.Id.Value),
                    -grossEnergy),
                new EnergyLedgerEntry(
                    new EnergyAccountKey(
                        EnergyAccountKind.StoredChemical,
                        organismId.Value),
                    storedEnergy),
                new EnergyLedgerEntry(
                    new EnergyAccountKey(EnergyAccountKind.DissipatedHeat, 1),
                    dissipatedEnergy),
            ]);
    }

    private static ImmutableArray<MatterLedgerEntry> ConsolidateMatter(
        IEnumerable<MatterLedgerEntry> entries) =>
        entries
            .GroupBy(entry => entry.Account)
            .Select(group => new MatterLedgerEntry(
                group.Key,
                group.Aggregate(0L, static (sum, entry) => checked(sum + entry.DeltaQ))))
            .Where(entry => entry.DeltaQ != 0)
            .OrderBy(entry => entry.Account)
            .ToImmutableArray();

    private static MatterCompartment TileCompartment(CompiledResource resource) =>
        resource.EnvironmentalPhase == EnvironmentalPhase.Gas
            ? MatterCompartment.Atmosphere
            : resource.BiologicalForm == BiologicalForm.Organic
                ? MatterCompartment.OrganicPool
                : MatterCompartment.InorganicPool;

    private static CompiledReaction RequireReaction(
        CompiledRulePack rules,
        ReactionHandle handle)
    {
        if (handle.DenseSlot < 0 ||
            handle.DenseSlot >= rules.Reactions.Length ||
            rules.Reactions[handle.DenseSlot].Id != handle.Id)
        {
            throw new ArgumentException(
                "The reaction handle does not belong to this rule pack.",
                nameof(handle));
        }

        return rules.Reactions[handle.DenseSlot];
    }

    private static CompiledResource RequireResource(
        CompiledRulePack rules,
        ResourceHandle handle)
    {
        if (handle.DenseSlot < 0 ||
            handle.DenseSlot >= rules.Resources.Length ||
            rules.Resources[handle.DenseSlot].Id != handle.Id)
        {
            throw new ArgumentException(
                "The resource handle does not belong to this rule pack.",
                nameof(handle));
        }

        return rules.Resources[handle.DenseSlot];
    }
}

internal static class ResourceLedgerOracle
{
    public static LedgerReconciliationReport Reconcile(
        CompiledRulePack rules,
        ImmutableArray<ResourceTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var elementTotals = Enum.GetValues<ChemicalElement>()
            .ToDictionary(element => element, static _ => (Int128)0);
        Int128 energyTotal = 0;
        LedgerTransactionKey? previousKey = null;

        foreach (var transaction in transactions)
        {
            if (previousKey is not null && previousKey.Value.CompareTo(transaction.Key) >= 0)
            {
                throw new InvalidOperationException(
                    "Ledger transactions must have unique canonical ascending keys.");
            }

            ValidateExactReactionBundle(rules, transaction);
            foreach (var entry in transaction.MatterEntries)
            {
                var resource = RequireResource(rules, entry.Account.ResourceId);
                foreach (var component in resource.Composition)
                {
                    elementTotals[component.Element] = checked(
                        elementTotals[component.Element] +
                        ((Int128)entry.DeltaQ * component.Quantity));
                }
            }

            foreach (var entry in transaction.EnergyEntries)
            {
                energyTotal = checked(energyTotal + entry.DeltaQ);
            }

            previousKey = transaction.Key;
        }

        var elements = elementTotals
            .OrderBy(pair => pair.Key)
            .Select(pair => new ElementReconciliation(pair.Key, pair.Value))
            .ToImmutableArray();
        var report = new LedgerReconciliationReport(transactions.Length, elements, energyTotal);
        if (!report.IsBalanced)
        {
            throw new InvalidOperationException("The resource ledger does not reconcile exactly.");
        }

        return report;
    }

    private static void ValidateExactReactionBundle(
        CompiledRulePack rules,
        ResourceTransaction transaction)
    {
        if (transaction.Extent <= 0 ||
            transaction.Key.Tick == 0 ||
            transaction.Key.Phase != TickPhase.ExternalResolution ||
            transaction.Key.ActorId != transaction.ActorId.Value ||
            transaction.Key.ScopeId != transaction.TileId.Value ||
            transaction.Key.ReactionId != transaction.ReactionId.Value ||
            transaction.Cause != LedgerCause.ExternalEnergyCapture)
        {
            throw new InvalidOperationException("The ledger transaction identity is invalid.");
        }

        var reactionHandle = FindReaction(rules, transaction.ReactionId);
        var expected = ExternalCaptureTransactionFactory.Create(
            rules,
            transaction.Key.Tick,
            transaction.TileId,
            transaction.ActorId,
            reactionHandle,
            transaction.Extent);
        if (!expected.MatterEntries.SequenceEqual(transaction.MatterEntries) ||
            !expected.EnergyEntries.SequenceEqual(transaction.EnergyEntries))
        {
            throw new InvalidOperationException(
                "The ledger transaction does not match its compiled reaction bundle.");
        }
    }

    private static ReactionHandle FindReaction(CompiledRulePack rules, ReactionId id)
    {
        for (var index = 0; index < rules.Reactions.Length; index++)
        {
            if (rules.Reactions[index].Id == id)
            {
                return new ReactionHandle(id, index);
            }
        }

        throw new InvalidOperationException($"Ledger transaction references unknown reaction {id}.");
    }

    private static CompiledResource RequireResource(CompiledRulePack rules, ResourceId id)
    {
        foreach (var resource in rules.Resources)
        {
            if (resource.Id == id)
            {
                return resource;
            }
        }

        throw new InvalidOperationException($"Ledger entry references unknown resource {id}.");
    }
}

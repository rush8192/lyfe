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
    IngestedMatter = 6,
    AvailableStoreIngress = 7,
    AvailableStoreConsumption = 8,
    Structure = 9,
    FreeMicronutrient = 10,
    CommittedMicronutrient = 11,
    RemnantMatter = 12,
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
    ParticulateDigestion = 2,
    EnvironmentalGasSource = 3,
    EnvironmentalGasSink = 4,
    EnvironmentalGasExchange = 5,
    MandatoryMaintenance = 6,
    BiomassAssembly = 7,
    MicronutrientUptake = 8,
}

internal static class MicronutrientTransactionFactory
{
    public static ResourceTransaction CreateUptake(
        ulong tick,
        TileId tileId,
        OrganismId organismId,
        ResourceId resourceId,
        long quantityQ)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantityQ);
        return new ResourceTransaction(
            new LedgerTransactionKey(
                tick,
                TickPhase.InternalMetabolism,
                tileId.Value,
                organismId.Value,
                resourceId.Value,
                1),
            LedgerCause.MicronutrientUptake,
            resourceId.Value == 0 ? default : ReactionId.From(resourceId.Value),
            organismId,
            tileId,
            quantityQ,
            [
                new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Tile,
                        tileId.Value,
                        MatterCompartment.InorganicPool,
                        resourceId),
                    -quantityQ),
                new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Organism,
                        organismId.Value,
                        MatterCompartment.FreeMicronutrient,
                        resourceId),
                    quantityQ),
            ],
            []);
    }
}

internal static class FounderMetabolismTransactionFactory
{
    private static readonly ResourceId ReserveOrganicResourceId = ResourceId.From(3);

    public static ResourceTransaction CreateMaintenance(
        CompiledRulePack rules,
        ulong tick,
        TileId tileId,
        OrganismId organismId,
        ReactionHandle reactionHandle,
        long extent)
    {
        var reaction = RequireReaction(rules, reactionHandle, ProcessKind.MandatoryMaintenance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extent);
        if (reaction.Inputs.Length != 1 || reaction.Outputs.Length != 1 ||
            reaction.Inputs[0].Resource.Id != ReserveOrganicResourceId)
        {
            throw new ArgumentException("The reaction is not the supported maintenance recipe.");
        }

        var input = reaction.Inputs[0];
        var output = reaction.Outputs[0];
        return new ResourceTransaction(
            Key(tick, tileId, organismId, reaction.Id),
            LedgerCause.MandatoryMaintenance,
            reaction.Id,
            organismId,
            tileId,
            extent,
            [
                Entry(MatterAccountOwnerKind.Organism, organismId.Value,
                    MatterCompartment.EnergyReserve, input.Resource.Id,
                    checked(-input.Quantity * extent)),
                Entry(MatterAccountOwnerKind.Tile, tileId.Value,
                    MatterCompartment.OrganicPool, output.Resource.Id,
                    checked(output.Quantity * extent)),
            ],
            [
                new EnergyLedgerEntry(
                    new EnergyAccountKey(EnergyAccountKind.StoredChemical, organismId.Value),
                    checked(-reaction.GrossEnergyQ * extent)),
                new EnergyLedgerEntry(
                    new EnergyAccountKey(EnergyAccountKind.DissipatedHeat, 1),
                    checked(reaction.DissipatedEnergyQ * extent)),
            ]);
    }

    public static ResourceTransaction CreateBiomassAssembly(
        CompiledRulePack rules,
        ulong tick,
        TileId tileId,
        OrganismId organismId,
        ReactionHandle reactionHandle,
        long extent)
    {
        var reaction = RequireReaction(rules, reactionHandle, ProcessKind.BiomassAssembly);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extent);
        var matter = new List<MatterLedgerEntry>();
        foreach (var input in reaction.Inputs)
        {
            var amount = checked(input.Quantity * extent);
            if (input.Resource.Id == ReserveOrganicResourceId)
            {
                matter.Add(Entry(MatterAccountOwnerKind.Organism, organismId.Value,
                    MatterCompartment.EnergyReserve, input.Resource.Id, -amount));
                continue;
            }

            var resource = rules.Resources[input.Resource.DenseSlot];
            matter.Add(Entry(MatterAccountOwnerKind.Tile, tileId.Value,
                TileCompartment(resource), input.Resource.Id, -amount));
            // The needs-only primitive store stages the exact atomic bundle and is
            // empty again when this transaction completes. Keeping both entries
            // makes that boundary auditable without adding false retained state.
            matter.Add(Entry(MatterAccountOwnerKind.Organism, organismId.Value,
                MatterCompartment.AvailableStoreIngress, input.Resource.Id, amount));
            matter.Add(Entry(MatterAccountOwnerKind.Organism, organismId.Value,
                MatterCompartment.AvailableStoreConsumption, input.Resource.Id, -amount));
        }

        foreach (var output in reaction.Outputs)
        {
            var resource = rules.Resources[output.Resource.DenseSlot];
            var amount = checked(output.Quantity * extent);
            if (resource.Id == ResourceId.From(5))
            {
                matter.Add(Entry(MatterAccountOwnerKind.Organism, organismId.Value,
                    MatterCompartment.Structure, resource.Id, amount));
            }
            else if (resource.BiologicalForm == BiologicalForm.Boundary)
            {
                matter.Add(Entry(MatterAccountOwnerKind.Boundary, 1,
                    MatterCompartment.OceanWaterBoundary, resource.Id, amount));
            }
            else
            {
                matter.Add(Entry(MatterAccountOwnerKind.Tile, tileId.Value,
                    TileCompartment(resource), resource.Id, amount));
            }
        }

        return new ResourceTransaction(
            Key(tick, tileId, organismId, reaction.Id),
            LedgerCause.BiomassAssembly,
            reaction.Id,
            organismId,
            tileId,
            extent,
            matter.ToImmutableArray(),
            [
                new EnergyLedgerEntry(
                    new EnergyAccountKey(EnergyAccountKind.StoredChemical, organismId.Value),
                    checked(-reaction.GrossEnergyQ * extent)),
                new EnergyLedgerEntry(
                    new EnergyAccountKey(EnergyAccountKind.DissipatedHeat, 1),
                    checked(reaction.DissipatedEnergyQ * extent)),
            ]);
    }

    public static long StagingLoadPerExtent(
        CompiledRulePack rules,
        ReactionHandle reactionHandle)
    {
        var reaction = RequireReaction(rules, reactionHandle, ProcessKind.BiomassAssembly);
        var load = 0L;
        foreach (var input in reaction.Inputs.Where(input =>
                     input.Resource.Id != ReserveOrganicResourceId))
        {
            var resource = rules.Resources[input.Resource.DenseSlot];
            var atoms = resource.Composition.Aggregate(
                0L,
                static (sum, component) => checked(sum + component.Quantity));
            load = checked(load + input.Quantity * atoms);
        }
        return load;
    }

    private static LedgerTransactionKey Key(
        ulong tick, TileId tileId, OrganismId organismId, ReactionId reactionId) =>
        new(tick, TickPhase.InternalMetabolism, tileId.Value, organismId.Value,
            reactionId.Value, 0);

    private static MatterLedgerEntry Entry(
        MatterAccountOwnerKind ownerKind,
        ulong ownerId,
        MatterCompartment compartment,
        ResourceId resourceId,
        long delta) => new(new MatterAccountKey(ownerKind, ownerId, compartment, resourceId), delta);

    private static MatterCompartment TileCompartment(CompiledResource resource) =>
        resource.EnvironmentalPhase == EnvironmentalPhase.Gas
            ? MatterCompartment.Atmosphere
            : resource.BiologicalForm == BiologicalForm.Organic
                ? MatterCompartment.OrganicPool
                : MatterCompartment.InorganicPool;

    private static CompiledReaction RequireReaction(
        CompiledRulePack rules,
        ReactionHandle handle,
        ProcessKind kind)
    {
        if (handle.DenseSlot < 0 || handle.DenseSlot >= rules.Reactions.Length ||
            rules.Reactions[handle.DenseSlot].Id != handle.Id ||
            rules.Reactions[handle.DenseSlot].ProcessKind != kind)
        {
            throw new ArgumentException("The reaction handle does not identify the required founder process.");
        }
        return rules.Reactions[handle.DenseSlot];
    }
}

internal static class ParticulateDigestionTransactionFactory
{
    public static ResourceTransaction Create(
        CompiledRulePack rules,
        ulong tick,
        TileId tileId,
        OrganismId organismId,
        ReactionHandle reactionHandle,
        long extent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extent);
        var reaction = rules.Reactions[reactionHandle.DenseSlot];
        if (reaction.Id != reactionHandle.Id ||
            reaction.ProcessKind != ProcessKind.ParticulateDigestion ||
            reaction.Inputs.Length != 1 ||
            reaction.Outputs.Length != 2)
        {
            throw new ArgumentException("The reaction is not the supported particulate digestion recipe.");
        }

        var input = reaction.Inputs[0];
        var reserve = reaction.Outputs.Single(output =>
            rules.Resources[output.Resource.DenseSlot].Id == ResourceId.From(3));
        var residue = reaction.Outputs.Single(output => output.Resource != reserve.Resource);
        return new ResourceTransaction(
            new LedgerTransactionKey(
                tick,
                TickPhase.InternalMetabolism,
                tileId.Value,
                organismId.Value,
                reaction.Id.Value,
                0),
            LedgerCause.ParticulateDigestion,
            reaction.Id,
            organismId,
            tileId,
            extent,
            [
                new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Organism,
                        organismId.Value,
                        MatterCompartment.IngestedMatter,
                        input.Resource.Id),
                    checked(-input.Quantity * extent)),
                new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Organism,
                        organismId.Value,
                        MatterCompartment.EnergyReserve,
                        reserve.Resource.Id),
                    checked(reserve.Quantity * extent)),
                new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Tile,
                        tileId.Value,
                        MatterCompartment.OrganicPool,
                        residue.Resource.Id),
                    checked(residue.Quantity * extent)),
            ],
            [
                new EnergyLedgerEntry(
                    new EnergyAccountKey(EnergyAccountKind.ChemicalOpportunity, reaction.Id.Value),
                    checked(-reaction.GrossEnergyQ * extent)),
                new EnergyLedgerEntry(
                    new EnergyAccountKey(EnergyAccountKind.StoredChemical, organismId.Value),
                    checked(reaction.StoredEnergyQ * extent)),
                new EnergyLedgerEntry(
                    new EnergyAccountKey(EnergyAccountKind.DissipatedHeat, 1),
                    checked(reaction.DissipatedEnergyQ * extent)),
            ]);
    }
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
                RequireResource(rules, output.Resource).BiologicalForm ==
                BiologicalForm.Organic)
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
            matter.Add(resource.BiologicalForm switch
            {
                BiologicalForm.Boundary => new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Boundary,
                        1,
                        MatterCompartment.OceanWaterBoundary,
                        resource.Id),
                    amount),
                BiologicalForm.Organic => new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Organism,
                        organismId.Value,
                        MatterCompartment.EnergyReserve,
                        resource.Id),
                    amount),
                BiologicalForm.Inorganic => new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Tile,
                        tileId.Value,
                        TileCompartment(resource),
                        resource.Id),
                    amount),
                _ => throw new InvalidOperationException(
                    "Unknown biological form in an external-capture output."),
            });
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
        if (transaction.Cause == LedgerCause.MicronutrientUptake)
        {
            var expectedUptake = MicronutrientTransactionFactory.CreateUptake(
                transaction.Key.Tick,
                transaction.TileId,
                transaction.ActorId,
                ResourceId.From(transaction.ReactionId.Value),
                transaction.Extent);
            if (transaction.Key.Phase != TickPhase.InternalMetabolism ||
                !expectedUptake.MatterEntries.SequenceEqual(transaction.MatterEntries) ||
                !expectedUptake.EnergyEntries.SequenceEqual(transaction.EnergyEntries) ||
                transaction.Key != expectedUptake.Key)
            {
                throw new InvalidOperationException("The micronutrient uptake bundle is invalid.");
            }
            return;
        }

        if (transaction.Cause is LedgerCause.EnvironmentalGasSource or
            LedgerCause.EnvironmentalGasSink or LedgerCause.EnvironmentalGasExchange)
        {
            ValidateEnvironmentalGasBundle(rules, transaction);
            return;
        }

        if (transaction.Extent <= 0 ||
            transaction.Key.Tick == 0 ||
            transaction.Key.ActorId != transaction.ActorId.Value ||
            transaction.Key.ScopeId != transaction.TileId.Value ||
            transaction.Key.ReactionId != transaction.ReactionId.Value)
        {
            throw new InvalidOperationException("The ledger transaction identity is invalid.");
        }

        var reactionHandle = FindReaction(rules, transaction.ReactionId);
        var expected = transaction.Cause switch
        {
            LedgerCause.ExternalEnergyCapture when
                transaction.Key.Phase == TickPhase.ExternalResolution =>
                ExternalCaptureTransactionFactory.Create(
                    rules,
                    transaction.Key.Tick,
                    transaction.TileId,
                    transaction.ActorId,
                    reactionHandle,
                    transaction.Extent),
            LedgerCause.ParticulateDigestion when
                transaction.Key.Phase == TickPhase.InternalMetabolism =>
                ParticulateDigestionTransactionFactory.Create(
                    rules,
                    transaction.Key.Tick,
                    transaction.TileId,
                    transaction.ActorId,
                    reactionHandle,
                    transaction.Extent),
            LedgerCause.MandatoryMaintenance when
                transaction.Key.Phase == TickPhase.InternalMetabolism =>
                FounderMetabolismTransactionFactory.CreateMaintenance(
                    rules,
                    transaction.Key.Tick,
                    transaction.TileId,
                    transaction.ActorId,
                    reactionHandle,
                    transaction.Extent),
            LedgerCause.BiomassAssembly when
                transaction.Key.Phase == TickPhase.InternalMetabolism =>
                FounderMetabolismTransactionFactory.CreateBiomassAssembly(
                    rules,
                    transaction.Key.Tick,
                    transaction.TileId,
                    transaction.ActorId,
                    reactionHandle,
                    transaction.Extent),
            _ => throw new InvalidOperationException(
                "The ledger cause and phase do not identify a supported reaction bundle."),
        };
        if (!expected.MatterEntries.SequenceEqual(transaction.MatterEntries) ||
            !expected.EnergyEntries.SequenceEqual(transaction.EnergyEntries))
        {
            throw new InvalidOperationException(
                "The ledger transaction does not match its compiled reaction bundle.");
        }
    }

    private static void ValidateEnvironmentalGasBundle(
        CompiledRulePack rules,
        ResourceTransaction transaction)
    {
        if (transaction.Extent <= 0 ||
            transaction.Key.Tick == 0 ||
            transaction.Key.Phase != TickPhase.EnvironmentalLedger ||
            transaction.ActorId.Value != 0 ||
            transaction.Key.ScopeId != transaction.TileId.Value ||
            transaction.Key.ReactionId != transaction.ReactionId.Value ||
            transaction.MatterEntries.Length != 2 ||
            transaction.EnergyEntries.Length != 0)
        {
            throw new InvalidOperationException("The environmental gas transaction identity is invalid.");
        }

        var resource = RequireResource(rules, ResourceId.From(transaction.ReactionId.Value));
        if (resource.EnvironmentalPhase != EnvironmentalPhase.Gas ||
            transaction.MatterEntries.Any(entry =>
                entry.Account.ResourceId != resource.Id ||
                entry.Account.Compartment != MatterCompartment.Atmosphere) ||
            transaction.MatterEntries.Sum(entry => entry.DeltaQ) != 0 ||
            transaction.MatterEntries.Count(entry => entry.DeltaQ == -transaction.Extent) != 1 ||
            transaction.MatterEntries.Count(entry => entry.DeltaQ == transaction.Extent) != 1)
        {
            throw new InvalidOperationException("The environmental gas transfer is not exact or mass balanced.");
        }

        var debit = transaction.MatterEntries.Single(entry => entry.DeltaQ < 0).Account;
        var credit = transaction.MatterEntries.Single(entry => entry.DeltaQ > 0).Account;
        var valid = transaction.Cause switch
        {
            LedgerCause.EnvironmentalGasSource =>
                debit.OwnerKind == MatterAccountOwnerKind.Boundary &&
                credit.OwnerKind == MatterAccountOwnerKind.Tile &&
                credit.OwnerId == transaction.TileId.Value &&
                transaction.Key.ActorId == 0 && transaction.Key.LocalOrdinal == 0,
            LedgerCause.EnvironmentalGasSink =>
                debit.OwnerKind == MatterAccountOwnerKind.Tile &&
                debit.OwnerId == transaction.TileId.Value &&
                credit.OwnerKind == MatterAccountOwnerKind.Boundary &&
                transaction.Key.ActorId == 0 && transaction.Key.LocalOrdinal == 1,
            LedgerCause.EnvironmentalGasExchange =>
                debit.OwnerKind == MatterAccountOwnerKind.Tile &&
                debit.OwnerId == transaction.TileId.Value &&
                credit.OwnerKind == MatterAccountOwnerKind.Tile &&
                transaction.Key.ActorId == credit.OwnerId && transaction.Key.LocalOrdinal == 2,
            _ => false,
        };
        if (!valid)
        {
            throw new InvalidOperationException("The environmental gas transfer endpoints are invalid.");
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

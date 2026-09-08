using System.Collections.Immutable;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.State.Storage;

internal readonly record struct SpeciesSnapshot(
    SpeciesId Id,
    GenomeId GenomeId,
    PhenotypeHandle Phenotype,
    ulong Population,
    SpeciesEvolutionAccount Evolution,
    SpeciesLineageState Lineage);

internal sealed class SpeciesStore
{
    private readonly List<SpeciesId> ids = [];
    private readonly List<GenomeId> genomeIds = [];
    private readonly List<PhenotypeHandle> phenotypes = [];
    private readonly List<ulong> populations = [];
    private readonly List<SpeciesEvolutionAccount> evolution = [];
    private readonly List<SpeciesLineageState> lineage = [];
    private readonly List<SpeciationEventRecord> speciationEvents = [];
    private readonly Dictionary<SpeciesId, int> locations = [];

    public int Count => ids.Count;

    public ulong MutationEpoch { get; private set; }

    public void Restore(
        SpeciesId id,
        GenomeId genomeId,
        PhenotypeHandle phenotype,
        ulong population,
        SpeciesEvolutionAccount account,
        SpeciesLineageState lineageState)
    {
        if (MutationEpoch != 0 || id == default || genomeId == default ||
            phenotype == default || lineageState.FounderCounts.IsDefault ||
            lineageState.AcquiredTraitDelta.IsDefault || !IsValidEvolution(account) ||
            !locations.TryAdd(id, ids.Count))
        {
            throw new ArgumentException("Persisted species state is invalid or duplicated.", nameof(id));
        }

        ids.Add(id);
        genomeIds.Add(genomeId);
        phenotypes.Add(phenotype);
        populations.Add(population);
        evolution.Add(account);
        lineage.Add(lineageState);
    }

    public void RestoreSpeciationEvent(SpeciationEventRecord value)
    {
        if (MutationEpoch != 0 || value.EventId == 0 ||
            (speciationEvents.Count > 0 && speciationEvents[^1].EventId >= value.EventId))
        {
            throw new ArgumentException("Persisted speciation events must have ascending nonzero IDs.", nameof(value));
        }
        speciationEvents.Add(value);
    }

    public void Create(
        SpeciesId id,
        GenomeId genomeId,
        PhenotypeHandle phenotype,
        SpeciesEvolutionAccount account,
        SpeciesLineageState lineageState,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (id == default || genomeId == default || phenotype == default ||
            lineageState.FounderCounts.IsDefault || lineageState.AcquiredTraitDelta.IsDefault ||
            !IsValidEvolution(account))
        {
            throw new ArgumentException("Species, genome, phenotype, evolution, and lineage must be valid.");
        }
        if (!locations.TryAdd(id, ids.Count))
        {
            throw new InvalidOperationException($"Species {id} already exists.");
        }

        ids.Add(id);
        genomeIds.Add(genomeId);
        phenotypes.Add(phenotype);
        populations.Add(0);
        evolution.Add(account);
        lineage.Add(lineageState);
        RecordMutation(changes);
        changes.RecordCreate(StateEntityReference.From(id));
    }

    public bool Contains(SpeciesId id) => locations.ContainsKey(id);

    public SpeciesSnapshot Get(SpeciesId id)
    {
        var index = GetIndex(id);
        return new SpeciesSnapshot(
            ids[index], genomeIds[index], phenotypes[index], populations[index],
            evolution[index], lineage[index]);
    }

    public ImmutableArray<SpeciesId> GetIdsInCanonicalOrder() =>
        locations.Keys.OrderBy(id => id.Value).ToImmutableArray();

    public ImmutableArray<SpeciationEventRecord> GetSpeciationEvents() =>
        speciationEvents.ToImmutableArray();

    public void IncrementPopulation(SpeciesId id, PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var index = GetIndex(id);
        populations[index] = checked(populations[index] + 1);
        if (lineage[index].ExtinctTick.HasValue)
        {
            lineage[index] = lineage[index] with { ExtinctTick = null };
        }
        RecordPopulationChange(id, changes);
    }

    public void DecrementPopulation(SpeciesId id, PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var index = GetIndex(id);
        if (populations[index] == 0)
        {
            throw new InvalidOperationException($"Species {id} population is already zero.");
        }
        populations[index]--;
        RecordPopulationChange(id, changes);
    }

    public void SetEvolutionAggregate(
        SpeciesId id,
        SpeciesEvolutionAccount account,
        ulong completedTick,
        PhaseChangeBuilder changes)
    {
        if (!IsValidEvolution(account))
        {
            throw new ArgumentOutOfRangeException(nameof(account), "Species evolution state is invalid.");
        }
        var index = GetIndex(id);
        if (evolution[index] == account &&
            (populations[index] != 0 || lineage[index].ExtinctTick.HasValue))
        {
            return;
        }

        evolution[index] = account;
        if (populations[index] == 0 && !lineage[index].ExtinctTick.HasValue)
        {
            lineage[index] = lineage[index] with { ExtinctTick = completedTick };
        }
        RecordMutation(changes);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.SpeciesEvolution);
    }

    public void SetSpeciationState(
        SpeciesId id,
        SpeciesEvolutionAccount account,
        PhaseChangeBuilder changes)
    {
        if (!IsValidEvolution(account))
        {
            throw new ArgumentOutOfRangeException(nameof(account), "Species evolution state is invalid.");
        }
        var index = GetIndex(id);
        evolution[index] = account;
        RecordMutation(changes);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.SpeciesEvolution);
    }

    public void AppendSpeciationEvent(SpeciationEventRecord value, PhaseChangeBuilder changes)
    {
        if (value.EventId == 0 ||
            (speciationEvents.Count > 0 && speciationEvents[^1].EventId >= value.EventId))
        {
            throw new InvalidOperationException("Speciation event IDs must be ascending and nonzero.");
        }
        speciationEvents.Add(value);
        RecordMutation(changes);
    }

    public ulong ComputeCoverageFingerprint()
    {
        var hash = new ShadowHashAccumulator();
        for (var index = 0; index < ids.Count; index++)
        {
            hash.Add(ids[index].Value);
            hash.Add(genomeIds[index].Value);
            hash.Add(phenotypes[index].FounderGenomeId.Value);
            hash.Add(phenotypes[index].DenseSlot);
            hash.Add(populations[index]);
            AddEvolution(ref hash, evolution[index]);
            var record = lineage[index];
            hash.Add(record.ParentSpeciesId?.Value ?? 0);
            hash.Add(record.FoundingEventId);
            hash.Add(record.CreatedTick);
            hash.Add(record.ExtinctTick ?? 0);
            foreach (var count in record.FounderCounts)
            {
                hash.Add(count.TileId.Value);
                hash.Add(count.Count);
            }
            foreach (var trait in record.AcquiredTraitDelta)
            {
                hash.Add(trait.Value);
            }
        }
        foreach (var value in speciationEvents)
        {
            hash.Add(value.EventId);
            hash.Add(value.AncestorSpeciesId.Value);
            hash.Add(value.DescendantSpeciesId.Value);
        }
        return hash.Value;
    }

    private static void AddEvolution(ref ShadowHashAccumulator hash, SpeciesEvolutionAccount value)
    {
        hash.Add((uint)value.Authority);
        hash.Add(value.MutationBalanceQ);
        hash.Add((ulong)value.MutationIncomeRemainder);
        hash.Add((ulong)(value.MutationIncomeRemainder >> 64));
        hash.Add(value.SpeciationNotBeforeTick);
        hash.Add(value.SpeciationOrdinal);
        hash.Add(value.EvolutionRevision);
        hash.Add(value.AverageHealthQ);
        hash.Add(value.LastIncomeQ);
        hash.Add(value.Pressure.EnergyShortageQ);
        hash.Add(value.Pressure.StarvationQ);
    }

    private static bool IsValidEvolution(SpeciesEvolutionAccount value) =>
        Enum.IsDefined(value.Authority) &&
        value.MutationBalanceQ >= 0 &&
        value.MutationIncomeRemainder < MutationIncomeMath.Denominator &&
        value.EvolutionRevision > 0 &&
        value.AverageHealthQ <= MutationIncomeMath.RatioScale &&
        value.LastIncomeQ >= 0 &&
        value.Pressure.EnergyShortageQ <= MutationIncomeMath.RatioScale &&
        value.Pressure.StarvationQ <= MutationIncomeMath.RatioScale;

    private int GetIndex(SpeciesId id)
    {
        if (!locations.TryGetValue(id, out var index))
        {
            throw new KeyNotFoundException($"Species {id} does not exist.");
        }
        return index;
    }

    private void RecordPopulationChange(SpeciesId id, PhaseChangeBuilder changes)
    {
        RecordMutation(changes);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.SpeciesPopulation);
    }

    private void RecordMutation(PhaseChangeBuilder changes)
    {
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Species);
    }
}

using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using System.Collections.Immutable;

namespace Lyfe.Simulation.State.Storage;

internal readonly record struct SpeciesSnapshot(
    SpeciesId Id,
    GenomeId GenomeId,
    PhenotypeHandle Phenotype,
    ulong Population);

internal sealed class SpeciesStore
{
    private readonly List<SpeciesId> ids = [];
    private readonly List<GenomeId> genomeIds = [];
    private readonly List<PhenotypeHandle> phenotypes = [];
    private readonly List<ulong> populations = [];
    private readonly Dictionary<SpeciesId, int> locations = [];

    public int Count => ids.Count;

    public ulong MutationEpoch { get; private set; }

    public void Restore(
        SpeciesId id,
        GenomeId genomeId,
        PhenotypeHandle phenotype,
        ulong population)
    {
        if (MutationEpoch != 0 ||
            id == default ||
            genomeId == default ||
            phenotype == default ||
            !locations.TryAdd(id, ids.Count))
        {
            throw new ArgumentException("Persisted species state is invalid or duplicated.", nameof(id));
        }

        ids.Add(id);
        genomeIds.Add(genomeId);
        phenotypes.Add(phenotype);
        populations.Add(population);
    }

    public void Create(
        SpeciesId id,
        GenomeId genomeId,
        PhenotypeHandle phenotype,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (id == default || genomeId == default || phenotype == default)
        {
            throw new ArgumentException("Species, genome, and phenotype identities must be nonzero.");
        }

        if (!locations.TryAdd(id, ids.Count))
        {
            throw new InvalidOperationException($"Species {id} already exists.");
        }

        ids.Add(id);
        genomeIds.Add(genomeId);
        phenotypes.Add(phenotype);
        populations.Add(0);
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Species);
        changes.RecordCreate(StateEntityReference.From(id));
    }

    public bool Contains(SpeciesId id) => locations.ContainsKey(id);

    public SpeciesSnapshot Get(SpeciesId id)
    {
        var index = GetIndex(id);
        return new SpeciesSnapshot(
            ids[index],
            genomeIds[index],
            phenotypes[index],
            populations[index]);
    }

    public ImmutableArray<SpeciesId> GetIdsInCanonicalOrder() =>
        locations.Keys.OrderBy(id => id.Value).ToImmutableArray();

    public void IncrementPopulation(SpeciesId id, PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var index = GetIndex(id);
        populations[index] = checked(populations[index] + 1);
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
        }

        return hash.Value;
    }

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
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Species);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.SpeciesPopulation);
    }
}

using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using System.Collections.Immutable;

namespace Lyfe.Simulation.State.Storage;

internal readonly record struct GenomeSnapshot(
    GenomeId Id,
    FounderGenomeId FounderGenomeId,
    FounderAllocationId FounderAllocationId,
    PhenotypeHandle Phenotype,
    ImmutableArray<TraitId> AcquiredTraits,
    string GenomeHash);

internal sealed class GenomeStore
{
    private readonly List<GenomeId> ids = [];
    private readonly List<FounderGenomeId> founderGenomeIds = [];
    private readonly List<FounderAllocationId> founderAllocationIds = [];
    private readonly List<PhenotypeHandle> phenotypes = [];
    private readonly List<ImmutableArray<TraitId>> acquiredTraits = [];
    private readonly List<string> genomeHashes = [];
    private readonly Dictionary<GenomeId, int> locations = [];

    public int Count => ids.Count;

    public ulong MutationEpoch { get; private set; }

    public void Restore(
        GenomeId id,
        FounderGenomeId founderGenomeId,
        FounderAllocationId founderAllocationId,
        PhenotypeHandle phenotype,
        ImmutableArray<TraitId> traits,
        string genomeHash)
    {
        if (MutationEpoch != 0 ||
            id == default ||
            founderGenomeId == default ||
            founderAllocationId == default ||
            phenotype.FounderGenomeId != founderGenomeId ||
            traits.IsDefault ||
            string.IsNullOrWhiteSpace(genomeHash) ||
            !locations.TryAdd(id, ids.Count))
        {
            throw new ArgumentException("Persisted genome state is invalid or duplicated.", nameof(id));
        }

        ids.Add(id);
        founderGenomeIds.Add(founderGenomeId);
        founderAllocationIds.Add(founderAllocationId);
        phenotypes.Add(phenotype);
        acquiredTraits.Add(traits);
        genomeHashes.Add(genomeHash);
    }

    public void Create(
        GenomeId id,
        FounderGenomeId founderGenomeId,
        FounderAllocationId founderAllocationId,
        PhenotypeHandle phenotype,
        ImmutableArray<TraitId> traits,
        string genomeHash,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (id == default || founderGenomeId == default || founderAllocationId == default ||
            phenotype.FounderGenomeId != founderGenomeId || traits.IsDefault ||
            string.IsNullOrWhiteSpace(genomeHash))
        {
            throw new ArgumentException("Genome identity and phenotype must be nonzero and consistent.");
        }

        if (!locations.TryAdd(id, ids.Count))
        {
            throw new InvalidOperationException($"Genome {id} already exists.");
        }

        ids.Add(id);
        founderGenomeIds.Add(founderGenomeId);
        founderAllocationIds.Add(founderAllocationId);
        phenotypes.Add(phenotype);
        acquiredTraits.Add(traits);
        genomeHashes.Add(genomeHash);
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Genomes);
        changes.RecordCreate(StateEntityReference.From(id));
    }

    public GenomeSnapshot Get(GenomeId id)
    {
        if (!locations.TryGetValue(id, out var index))
        {
            throw new KeyNotFoundException($"Genome {id} does not exist.");
        }

        return new GenomeSnapshot(
            ids[index],
            founderGenomeIds[index],
            founderAllocationIds[index],
            phenotypes[index],
            acquiredTraits[index],
            genomeHashes[index]);
    }

    public bool Contains(GenomeId id) => locations.ContainsKey(id);

    public ImmutableArray<GenomeId> GetIdsInCanonicalOrder() =>
        locations.Keys.OrderBy(id => id.Value).ToImmutableArray();

    public ulong ComputeCoverageFingerprint()
    {
        var hash = new ShadowHashAccumulator();
        for (var index = 0; index < ids.Count; index++)
        {
            hash.Add(ids[index].Value);
            hash.Add(founderGenomeIds[index].Value);
            hash.Add(founderAllocationIds[index].Value);
            hash.Add(phenotypes[index].FounderGenomeId.Value);
            hash.Add(phenotypes[index].DenseSlot);
            hash.Add(acquiredTraits[index].Length);
            foreach (var trait in acquiredTraits[index])
            {
                hash.Add(trait.Value);
            }
            foreach (var character in genomeHashes[index])
            {
                hash.Add(character);
            }
        }

        return hash.Value;
    }
}

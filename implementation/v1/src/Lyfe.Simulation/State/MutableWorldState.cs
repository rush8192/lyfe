using System.Collections.Immutable;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.Ticks;

namespace Lyfe.Simulation.State;

internal sealed class MutableWorldState
{
    private readonly object ownerToken = new();
    private readonly WorldEntityIdAllocator ids = new();
    private readonly TileStore tiles;
    private readonly TileResourceStore tileResources;
    private readonly GenomeStore genomes = new();
    private readonly SpeciesStore species = new();
    private readonly OrganismStore organisms;
    private PhaseChangeBuilder? activeChanges;

    public MutableWorldState(CompiledWorldRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = rules;
        tiles = new TileStore(rules.WorldProfile);
        tileResources = new TileResourceStore(rules.RulePack, rules.WorldProfile);
        organisms = new OrganismStore(tiles.Count);
    }

    public static MutableWorldState Restore(
        CompiledWorldRules rules,
        WorldPersistenceState persisted)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(persisted);
        if (persisted.TileResources.IsDefault ||
            persisted.Genomes.IsDefault ||
            persisted.Species.IsDefault ||
            persisted.Organisms.IsDefault ||
            persisted.LastCompletedTransactions.IsDefault)
        {
            throw new ArgumentException(
                "Persisted world collections must be initialized.",
                nameof(persisted));
        }

        var result = new MutableWorldState(rules);
        result.tileResources.Restore(persisted.TileResources);

        ulong maximumGenomeId = 0;
        foreach (var persistedGenome in persisted.Genomes)
        {
            if (persistedGenome.GenomeId <= maximumGenomeId)
            {
                throw new ArgumentException(
                    "Persisted genomes must use unique ascending IDs.",
                    nameof(persisted));
            }

            var id = GenomeId.FromAllocatedValue(persistedGenome.GenomeId);
            var founder = FounderGenomeId.From(persistedGenome.FounderGenomeId);
            result.genomes.Restore(id, founder, result.ResolvePhenotype(founder));
            maximumGenomeId = persistedGenome.GenomeId;
        }

        ulong maximumSpeciesId = 0;
        foreach (var persistedSpecies in persisted.Species)
        {
            if (persistedSpecies.SpeciesId <= maximumSpeciesId)
            {
                throw new ArgumentException(
                    "Persisted species must use unique ascending IDs.",
                    nameof(persisted));
            }

            var id = SpeciesId.FromAllocatedValue(persistedSpecies.SpeciesId);
            var genomeId = GenomeId.FromAllocatedValue(persistedSpecies.GenomeId);
            var genome = result.genomes.Get(genomeId);
            result.species.Restore(
                id,
                genomeId,
                genome.Phenotype,
                persistedSpecies.Population);
            maximumSpeciesId = persistedSpecies.SpeciesId;
        }

        ulong maximumOrganismId = 0;
        foreach (var persistedOrganism in persisted.Organisms)
        {
            if (persistedOrganism.OrganismId <= maximumOrganismId)
            {
                throw new ArgumentException(
                    "Persisted organisms must use unique ascending IDs.",
                    nameof(persisted));
            }

            var id = OrganismId.FromAllocatedValue(persistedOrganism.OrganismId);
            var speciesId = SpeciesId.FromAllocatedValue(persistedOrganism.SpeciesId);
            if (!result.species.Contains(speciesId) ||
                persistedOrganism.LifecyclePhase != (byte)LifecyclePhase.Mature)
            {
                throw new ArgumentException(
                    "A persisted organism has an invalid species or lifecycle reference.",
                    nameof(persisted));
            }

            result.organisms.Restore(
                id,
                new OrganismInitialState(
                    speciesId,
                    TileId.FromRowMajorIndex(persistedOrganism.TileId),
                    persistedOrganism.PositionXQ,
                    persistedOrganism.PositionYQ,
                    persistedOrganism.VelocityXQPerHour,
                    persistedOrganism.VelocityYQPerHour,
                    persistedOrganism.BirthTick,
                    persistedOrganism.BiologicalAgeHours,
                    LifecyclePhase.Mature,
                    persistedOrganism.StructuralMatterQ,
                    persistedOrganism.ChargedReserveQ));
            maximumOrganismId = persistedOrganism.OrganismId;
        }

        if (persisted.NextGenomeId <= maximumGenomeId ||
            persisted.NextSpeciesId <= maximumSpeciesId ||
            persisted.NextOrganismId <= maximumOrganismId)
        {
            throw new ArgumentException(
                "Persisted next entity IDs must exceed every retained entity ID.",
                nameof(persisted));
        }

        result.ids.RestoreContinuationState(new WorldEntityIdContinuationState(
            persisted.NextGenomeId,
            persisted.NextSpeciesId,
            persisted.NextOrganismId));
        result.ValidateInvariants();
        return result;
    }

    public CompiledWorldRules Rules { get; }

    public int TileCount => tiles.Count;

    public int GenomeCount => genomes.Count;

    public int SpeciesCount => species.Count;

    public int OrganismCount => organisms.Count;

    public bool IsFaulted { get; private set; }

    public WorldStateGenerationStamp CaptureGenerationStamp() =>
        new(
            ids.MutationEpoch,
            tileResources.MutationEpoch,
            genomes.MutationEpoch,
            species.MutationEpoch,
            organisms.MutationEpoch);

    public PhaseChangeBuilder BeginChanges()
    {
        RequireUsable();
        if (activeChanges is not null)
        {
            throw new InvalidOperationException("A phase change builder is already active.");
        }

        activeChanges = new PhaseChangeBuilder(ownerToken, CaptureCoverage());
        return activeChanges;
    }

    public PhaseChangeSet SealChanges(PhaseChangeBuilder changes)
    {
        RequireActive(changes);

        try
        {
            ValidateMutationCoverage(changes);
            var result = changes.Seal();
            activeChanges = null;
            return result;
        }
        catch
        {
            IsFaulted = true;
            activeChanges = null;
            throw;
        }
    }

    public GenomeId CreateGenome(
        FounderGenomeId founderGenomeId,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var phenotype = ResolvePhenotype(founderGenomeId);
        var id = ids.AllocateGenome();
        changes.RecordMutation(WorldStoreKind.Identity);
        genomes.Create(id, founderGenomeId, phenotype, changes);
        return id;
    }

    public SpeciesId CreateSpecies(GenomeId genomeId, PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var genome = genomes.Get(genomeId);
        var id = ids.AllocateSpecies();
        changes.RecordMutation(WorldStoreKind.Identity);
        species.Create(id, genomeId, genome.Phenotype, changes);
        return id;
    }

    public OrganismId CreateOrganism(
        OrganismInitialState initialState,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        if (!species.Contains(initialState.SpeciesId) || !tiles.Contains(initialState.TileId))
        {
            throw new ArgumentException("The organism must reference an existing species and tile.");
        }

        if (initialState.StructuralMatterQ < 0 ||
            initialState.ChargedReserveQ < 0 ||
            initialState.LifecyclePhase == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialState),
                "The organism lifecycle and nonnegative balances must be valid.");
        }

        var id = ids.AllocateOrganism();
        changes.RecordMutation(WorldStoreKind.Identity);
        organisms.Create(id, initialState, changes);
        species.IncrementPopulation(initialState.SpeciesId, changes);
        return id;
    }

    public OrganismSnapshot RemoveOrganism(
        OrganismId organismId,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var current = organisms.Get(organismId);
        if (species.Get(current.SpeciesId).Population == 0)
        {
            throw new InvalidOperationException("The organism's species population is inconsistent.");
        }

        var removed = organisms.Remove(organismId, changes);
        species.DecrementPopulation(current.SpeciesId, changes);
        return removed;
    }

    public void SetOrganismPosition(
        OrganismId organismId,
        uint positionXQ,
        uint positionYQ,
        long velocityXQPerHour,
        long velocityYQPerHour,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        organisms.SetPosition(
            organismId,
            positionXQ,
            positionYQ,
            velocityXQPerHour,
            velocityYQPerHour,
            changes);
    }

    public void RelocateOrganism(
        OrganismId organismId,
        TileId destinationTileId,
        uint positionXQ,
        uint positionYQ,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        if (!tiles.Contains(destinationTileId))
        {
            throw new KeyNotFoundException($"Tile {destinationTileId} does not exist.");
        }

        organisms.Relocate(
            organismId,
            destinationTileId,
            positionXQ,
            positionYQ,
            changes);
    }

    public void AdjustOrganismStructure(
        OrganismId organismId,
        long delta,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        organisms.AdjustStructure(organismId, delta, changes);
    }

    public void AdjustOrganismChargedReserve(
        OrganismId organismId,
        long delta,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        organisms.AdjustChargedReserve(organismId, delta, changes);
    }

    public void AdvanceOrganismBiologicalAge(
        OrganismId organismId,
        ulong elapsedHours,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        organisms.AdvanceBiologicalAge(organismId, elapsedHours, changes);
    }

    public void SetOrganismBiologicalAge(
        OrganismId organismId,
        ulong expectedAgeHours,
        ulong nextAgeHours,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        organisms.SetBiologicalAge(
            organismId,
            expectedAgeHours,
            nextAgeHours,
            changes);
    }

    public void ApplyTileResourceDelta(
        TileId tileId,
        ResourceHandle resource,
        long delta,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        if (!tiles.Contains(tileId))
        {
            throw new KeyNotFoundException($"Tile {tileId} does not exist.");
        }

        tileResources.ApplyDelta(tileId, resource, delta, changes);
    }

    public TileSnapshot GetTile(TileId id) => tiles.Get(id);

    public long GetTileResource(TileId tileId, ResourceHandle resource) =>
        tileResources.Get(tileId, resource);

    public GenomeSnapshot GetGenome(GenomeId id) => genomes.Get(id);

    public SpeciesSnapshot GetSpecies(SpeciesId id) => species.Get(id);

    public CompiledPhenotype GetCompiledPhenotype(SpeciesId id)
    {
        var handle = species.Get(id).Phenotype;
        return Rules.RulePack.FounderPhenotypes[handle.DenseSlot];
    }

    public OrganismSnapshot GetOrganism(OrganismId id) => organisms.Get(id);

    public ImmutableArray<OrganismId> GetOrganismIdsInCanonicalOrder() =>
        organisms.GetIdsInCanonicalOrder();

    public ImmutableArray<GenomeId> GetGenomeIdsInCanonicalOrder() =>
        genomes.GetIdsInCanonicalOrder();

    public ImmutableArray<SpeciesId> GetSpeciesIdsInCanonicalOrder() =>
        species.GetIdsInCanonicalOrder();

    public ImmutableArray<TileId> GetTileIdsInCanonicalOrder() =>
        Enumerable.Range(0, tiles.Count)
            .Select(index => TileId.FromRowMajorIndex(checked((uint)index)))
            .ToImmutableArray();

    public ImmutableArray<ResourceHandle> GetResourceHandlesInCanonicalOrder() =>
        Rules.RulePack.Resources
            .Select((resource, denseSlot) => new ResourceHandle(resource.Id, denseSlot))
            .OrderBy(handle => handle.Id.Value)
            .ToImmutableArray();

    public WorldEntityIdContinuationState CaptureIdContinuationState() =>
        ids.CaptureContinuationState();

    public ResourceHandle GetResourceHandle(ResourceId id)
    {
        for (var index = 0; index < Rules.RulePack.Resources.Length; index++)
        {
            if (Rules.RulePack.Resources[index].Id == id)
            {
                return new ResourceHandle(id, index);
            }
        }

        throw new KeyNotFoundException($"Resource {id} is not compiled for this world.");
    }

    public void ValidateInvariants()
    {
        RequireUsable();
        organisms.ValidateLocators();

        foreach (var genomeId in genomes.GetIdsInCanonicalOrder())
        {
            var genome = genomes.Get(genomeId);
            if (genome.Phenotype.DenseSlot < 0 ||
                genome.Phenotype.DenseSlot >= Rules.RulePack.FounderPhenotypes.Length ||
                Rules.RulePack.FounderPhenotypes[genome.Phenotype.DenseSlot].FounderGenomeId !=
                genome.FounderGenomeId)
            {
                throw new InvalidOperationException($"Genome {genomeId} has an invalid phenotype handle.");
            }
        }

        var populations = new Dictionary<SpeciesId, ulong>();
        foreach (var organismId in organisms.GetIdsInCanonicalOrder())
        {
            var organism = organisms.Get(organismId);
            if (!species.Contains(organism.SpeciesId) || !tiles.Contains(organism.TileId))
            {
                throw new InvalidOperationException($"Organism {organismId} has a dangling reference.");
            }

            populations.TryGetValue(organism.SpeciesId, out var population);
            populations[organism.SpeciesId] = checked(population + 1);
        }

        foreach (var speciesId in species.GetIdsInCanonicalOrder())
        {
            var speciesState = species.Get(speciesId);
            if (!genomes.Contains(speciesState.GenomeId))
            {
                throw new InvalidOperationException($"Species {speciesId} has a dangling genome reference.");
            }

            if (genomes.Get(speciesState.GenomeId).Phenotype != speciesState.Phenotype)
            {
                throw new InvalidOperationException($"Species {speciesId} has an inconsistent phenotype handle.");
            }

            populations.TryGetValue(speciesId, out var actualPopulation);
            if (speciesState.Population != actualPopulation)
            {
                throw new InvalidOperationException($"Species {speciesId} population is inconsistent.");
            }
        }
    }

    public int GetAllocatedOrganismChunkCount(TileId tileId) =>
        organisms.GetAllocatedChunkCount(tileId);

    public void MarkFaulted(PhaseChangeBuilder? changes = null)
    {
        if (IsFaulted)
        {
            activeChanges = null;
            return;
        }

        if (changes is not null && !ReferenceEquals(activeChanges, changes))
        {
            throw new InvalidOperationException(
                "Only the active phase change builder can fault this world.");
        }

        IsFaulted = true;
        activeChanges = null;
    }

#if DEBUG
    public void DebugSetChargedReserveWithoutTracking(OrganismId id, long value)
    {
        RequireUsable();
        organisms.DebugSetChargedReserveWithoutTracking(id, value);
    }
#endif

    private PhenotypeHandle ResolvePhenotype(FounderGenomeId founderGenomeId)
    {
        for (var index = 0; index < Rules.RulePack.FounderPhenotypes.Length; index++)
        {
            var phenotype = Rules.RulePack.FounderPhenotypes[index];
            if (phenotype.FounderGenomeId == founderGenomeId)
            {
                return new PhenotypeHandle(founderGenomeId, index);
            }
        }

        throw new KeyNotFoundException(
            $"Founder genome {founderGenomeId} has no compiled phenotype in this world.");
    }

    private Dictionary<WorldStoreKind, StoreCoverage> CaptureCoverage()
    {
#if DEBUG
        var identityFingerprint = ids.ComputeCoverageFingerprint();
        var tileResourceFingerprint = tileResources.ComputeCoverageFingerprint();
        var genomeFingerprint = genomes.ComputeCoverageFingerprint();
        var speciesFingerprint = species.ComputeCoverageFingerprint();
        var organismFingerprint = organisms.ComputeCoverageFingerprint();
#else
        const ulong identityFingerprint = 0;
        const ulong tileResourceFingerprint = 0;
        const ulong genomeFingerprint = 0;
        const ulong speciesFingerprint = 0;
        const ulong organismFingerprint = 0;
#endif

        return new Dictionary<WorldStoreKind, StoreCoverage>
        {
            [WorldStoreKind.Identity] = new StoreCoverage(
                ids.MutationEpoch,
                identityFingerprint),
            [WorldStoreKind.TileResources] = new StoreCoverage(
                tileResources.MutationEpoch,
                tileResourceFingerprint),
            [WorldStoreKind.Genomes] = new StoreCoverage(
                genomes.MutationEpoch,
                genomeFingerprint),
            [WorldStoreKind.Species] = new StoreCoverage(
                species.MutationEpoch,
                speciesFingerprint),
            [WorldStoreKind.Organisms] = new StoreCoverage(
                organisms.MutationEpoch,
                organismFingerprint),
        };
    }

    private void ValidateMutationCoverage(PhaseChangeBuilder changes)
    {
        var finalCoverage = CaptureCoverage();
        foreach (var storeKind in Enum.GetValues<WorldStoreKind>())
        {
            var before = changes.InitialCoverage[storeKind];
            var after = finalCoverage[storeKind];
            var actualMutations = checked(after.MutationEpoch - before.MutationEpoch);
            changes.MutationCounts.TryGetValue(storeKind, out var recordedMutations);
            if (actualMutations != recordedMutations)
            {
                throw new InvalidOperationException(
                    $"Store {storeKind} changed {actualMutations} times but recorded {recordedMutations} mutations.");
            }

#if DEBUG
            if (actualMutations == 0 && before.Fingerprint != after.Fingerprint)
            {
                throw new InvalidOperationException(
                    $"Store {storeKind} changed without passing through a tracked mutator.");
            }
#endif
        }
    }

    private void RequireActive(PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        RequireUsable();
        if (!ReferenceEquals(activeChanges, changes) || !changes.BelongsTo(ownerToken))
        {
            throw new InvalidOperationException(
                "Mutations require this world's currently active phase change builder.");
        }
    }

    private void RequireUsable()
    {
        if (IsFaulted)
        {
            throw new InvalidOperationException("The mutable world is faulted.");
        }
    }

}

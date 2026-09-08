using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.Ticks;
using Lyfe.Simulation.World.Generation;

namespace Lyfe.Simulation.State;

internal sealed class MutableWorldState
{
    private static readonly ResourceId HydrogenSulfideResourceId = ResourceId.From(10);
    private static readonly ResourceId SulfurDioxideResourceId = ResourceId.From(11);
    private readonly object ownerToken = new();
    private readonly WorldEntityIdAllocator ids = new();
    private readonly TileStore tiles;
    private readonly TileResourceStore tileResources;
    private readonly GenomeStore genomes = new();
    private readonly SpeciesStore species = new();
    private readonly List<CompiledPhenotype> compiledPhenotypes;
    private readonly OrganismStore organisms;
    private readonly RemnantStore remnants = new();
    private readonly GameplayStateStore gameplay = new();
    private PhaseChangeBuilder? activeChanges;

    public MutableWorldState(CompiledWorldRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = rules;
        tiles = new TileStore(rules.WorldProfile);
        tileResources = new TileResourceStore(rules.RulePack, rules.WorldProfile);
        organisms = new OrganismStore(tiles.Count);
        compiledPhenotypes = [.. rules.RulePack.FounderPhenotypes];
    }

    public static MutableWorldState Restore(
        CompiledWorldRules rules,
        WorldPersistenceState persisted)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(persisted);
        if (persisted.TileResources.IsDefault ||
            persisted.GasTileRemainders.IsDefault ||
            persisted.GasEdgeRemainders.IsDefault ||
            persisted.Genomes.IsDefault ||
            persisted.Species.IsDefault ||
            persisted.SpeciationEvents.IsDefault ||
            persisted.Organisms.IsDefault ||
            persisted.Remnants.IsDefault ||
            persisted.LastCompletedTransactions.IsDefault ||
            persisted.Gameplay is null)
        {
            throw new ArgumentException(
                "Persisted world collections must be initialized.",
                nameof(persisted));
        }

        var result = new MutableWorldState(rules);
        result.tileResources.Restore(
            persisted.TileResources,
            persisted.GasTileRemainders,
            persisted.GasEdgeRemainders);

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
            var allocation = FounderAllocationId.From(persistedGenome.FounderAllocationId);
            var traits = persistedGenome.AcquiredTraitIds
                .Select(TraitId.From)
                .OrderBy(trait => trait.Value)
                .ToImmutableArray();
            var phenotype = GenomeCompiler.Compile(rules.RulePack, founder, allocation, traits);
            if (!string.Equals(phenotype.CanonicalCompiledHash, persistedGenome.GenomeHash, StringComparison.Ordinal))
            {
                throw new ArgumentException("A persisted genome hash does not match its compiled DNA.", nameof(persisted));
            }
            var handle = result.InternCompiledPhenotype(phenotype);
            result.genomes.Restore(id, founder, allocation, handle, traits, phenotype.CanonicalCompiledHash);
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
                persistedSpecies.Population,
                persistedSpecies.ToEvolutionAccount(),
                persistedSpecies.ToLineageState());
            maximumSpeciesId = persistedSpecies.SpeciesId;
        }

        foreach (var persistedEvent in persisted.SpeciationEvents)
        {
            result.species.RestoreSpeciationEvent(persistedEvent.ToRecord());
        }

        result.gameplay.Restore(persisted.Gameplay.ToGameState());

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

            var initialState = new OrganismInitialState(
                speciesId,
                TileId.FromRowMajorIndex(persistedOrganism.TileId),
                persistedOrganism.PositionXQ,
                persistedOrganism.PositionYQ,
                persistedOrganism.VelocityXQPerHour,
                persistedOrganism.VelocityYQPerHour,
                persistedOrganism.BirthTick,
                persistedOrganism.BiologicalAgeHours,
                LifecyclePhase.Mature,
                persistedOrganism.ReproductionNotBeforeTick,
                persistedOrganism.SuccessfulReproductionCount,
                persistedOrganism.ScavengeNotBeforeTick,
                persistedOrganism.IngestedStructuralMatterQ,
                persistedOrganism.StructuralMatterQ,
            persistedOrganism.ChargedReserveQ,
            new OrganismBehaviorState(
                    (OrganismBehaviorId)persistedOrganism.BehaviorId,
                    (BehaviorTargetKind)persistedOrganism.BehaviorTargetKind,
                    persistedOrganism.BehaviorTargetId,
                    persistedOrganism.BehaviorTargetPositionXQ,
                    persistedOrganism.BehaviorTargetPositionYQ,
                    persistedOrganism.BehaviorSelectedAtTick,
                    persistedOrganism.BehaviorMinimumDwellUntilTick,
                    persistedOrganism.RecentEnergyCoverageQ,
                    persistedOrganism.RecentAcquisitionCoverageQ,
                    persistedOrganism.LimitingMaterialDeficitQ),
                MicronutrientInventory.FromValues(persistedOrganism.CommittedMicronutrientsQ),
                MicronutrientInventory.FromValues(persistedOrganism.FreeMicronutrientsQ));
            var condition = new MaterializedOrganismCondition(
                persistedOrganism.ConditionEvaluatedTick,
                (ConditionSnapshotKind)persistedOrganism.ConditionSnapshotKind,
                persistedOrganism.ReserveFactorQ,
                persistedOrganism.StructureFactorQ,
                persistedOrganism.NutrientFactorQ,
                persistedOrganism.AgeFactorQ,
                persistedOrganism.LifecycleFactorQ,
                persistedOrganism.EnvironmentalFactorQ,
                persistedOrganism.RelativeHealthQ,
                persistedOrganism.ConditionTemperatureMilliC,
                persistedOrganism.TemperatureSeverityQ);
            var canonicalCondition = OrganismConditionBuilder.Build(
                result.GetCompiledPhenotype(speciesId).Physiology,
                new OrganismConditionInput(
                    initialState.StructuralMatterQ,
                    initialState.ChargedReserveQ,
                    initialState.BiologicalAgeHours),
                result.GetOrganismEnvironment(
                    initialState.TileId,
                    temperatureOverrideMilliC: condition.TemperatureMilliC),
                condition.EvaluatedTick,
                condition.SnapshotKind);
            if (condition.SnapshotKind != ConditionSnapshotKind.End ||
                condition != canonicalCondition)
            {
                throw new ArgumentException(
                    "A persisted organism condition is not a canonical end snapshot.",
                    nameof(persisted));
            }

            result.organisms.Restore(id, initialState, condition);
            maximumOrganismId = persistedOrganism.OrganismId;
        }


        ulong maximumRemnantId = 0;
        foreach (var persistedRemnant in persisted.Remnants)
        {
            if (persistedRemnant.RemnantId <= maximumRemnantId)
            {
                throw new ArgumentException(
                    "Persisted remnants must use unique ascending IDs.",
                    nameof(persisted));
            }

            var id = RemnantId.FromAllocatedValue(persistedRemnant.RemnantId);
            result.remnants.Restore(id, new RemnantInitialState(
                OrganismId.FromAllocatedValue(persistedRemnant.SourceOrganismId),
                SpeciesId.FromAllocatedValue(persistedRemnant.SourceSpeciesId),
                persistedRemnant.CreatedTick,
                TileId.FromRowMajorIndex(persistedRemnant.TileId),
                persistedRemnant.PositionXQ,
                persistedRemnant.PositionYQ,
                persistedRemnant.StructuralMatterQ,
                persistedRemnant.ChargedReserveQ,
                persistedRemnant.StructureDecayRemainderQ,
                persistedRemnant.ReserveDecayRemainderQ,
                MicronutrientInventory.FromValues(persistedRemnant.MicronutrientsQ)));
            maximumRemnantId = persistedRemnant.RemnantId;
        }

        if (persisted.NextGenomeId <= maximumGenomeId ||
            persisted.NextSpeciesId <= maximumSpeciesId ||
            persisted.NextOrganismId <= maximumOrganismId ||
            persisted.NextRemnantId <= maximumRemnantId)
        {
            throw new ArgumentException(
                "Persisted next entity IDs must exceed every retained entity ID.",
                nameof(persisted));
        }

        result.ids.RestoreContinuationState(new WorldEntityIdContinuationState(
            persisted.NextGenomeId,
            persisted.NextSpeciesId,
            persisted.NextOrganismId,
            persisted.NextRemnantId));
        result.ValidateInvariants();
        return result;
    }

    public CompiledWorldRules Rules { get; }

    public int TileCount => tiles.Count;

    public int GenomeCount => genomes.Count;

    public int SpeciesCount => species.Count;

    public int OrganismCount => organisms.Count;

    public int RemnantCount => remnants.Count;

    public bool IsFaulted { get; private set; }

    public WorldStateGenerationStamp CaptureGenerationStamp() =>
        new(
            ids.MutationEpoch,
            tileResources.MutationEpoch,
            genomes.MutationEpoch,
            species.MutationEpoch,
            organisms.MutationEpoch,
            remnants.MutationEpoch,
            gameplay.MutationEpoch);

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
        var baseline = Rules.RulePack.FounderAllocations.Single(allocation => allocation.IsBaseline);
        return CreateGenome(founderGenomeId, baseline.Id, changes);
    }

    public GenomeId CreateGenome(
        FounderGenomeId founderGenomeId,
        FounderAllocationId founderAllocationId,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var compiled = GenomeCompiler.Compile(
            Rules.RulePack,
            founderGenomeId,
            founderAllocationId,
            ResolveFounderPhenotype(founderGenomeId).AcquiredTraits);
        var phenotype = InternCompiledPhenotype(compiled);
        var id = ids.AllocateGenome();
        changes.RecordMutation(WorldStoreKind.Identity);
        genomes.Create(
            id,
            founderGenomeId,
            founderAllocationId,
            phenotype,
            compiled.AcquiredTraits,
            compiled.CanonicalCompiledHash,
            changes);
        return id;
    }

    public SpeciesId CreateSpecies(GenomeId genomeId, PhaseChangeBuilder changes)
        => CreateRootSpecies(genomeId, EvolutionAuthorityKind.Controlled, changes);

    public SpeciesId CreateRootSpecies(
        GenomeId genomeId,
        EvolutionAuthorityKind authority,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        if (authority is not (EvolutionAuthorityKind.Controlled or EvolutionAuthorityKind.Autonomous))
        {
            throw new ArgumentOutOfRangeException(nameof(authority));
        }
        var genome = genomes.Get(genomeId);
        var id = ids.AllocateSpecies();
        changes.RecordMutation(WorldStoreKind.Identity);
        species.Create(
            id,
            genomeId,
            genome.Phenotype,
            new SpeciesEvolutionAccount(
                authority,
                0,
                0,
                0,
                0,
                1,
                0,
                0,
                default),
            new SpeciesLineageState(null, 0, 0, null, [], []),
            changes);
        return id;
    }

    public void InitializeGameplay(GameStateSnapshot state, PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        gameplay.Initialize(state, changes);
    }

    public GameStateSnapshot GetGameplayState() => gameplay.Get();

    public void ReplaceGameplayState(GameStateSnapshot state, PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        gameplay.Replace(state, changes);
    }

    public GenomeId InternGenome(
        FounderGenomeId founderGenomeId,
        FounderAllocationId founderAllocationId,
        ImmutableArray<TraitId> acquiredTraits,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var phenotype = GenomeCompiler.Compile(
            Rules.RulePack,
            founderGenomeId,
            founderAllocationId,
            acquiredTraits);
        foreach (var genomeId in genomes.GetIdsInCanonicalOrder())
        {
            var current = genomes.Get(genomeId);
            if (string.Equals(current.GenomeHash, phenotype.CanonicalCompiledHash, StringComparison.Ordinal))
            {
                return current.Id;
            }
        }

        var handle = InternCompiledPhenotype(phenotype);
        var id = ids.AllocateGenome();
        changes.RecordMutation(WorldStoreKind.Identity);
        genomes.Create(
            id,
            founderGenomeId,
            founderAllocationId,
            handle,
            phenotype.AcquiredTraits,
            phenotype.CanonicalCompiledHash,
            changes);
        return id;
    }

    public SpeciesId CreateDescendantSpecies(
        GenomeId genomeId,
        SpeciesEvolutionAccount account,
        SpeciesLineageState lineage,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var genome = genomes.Get(genomeId);
        var id = ids.AllocateSpecies();
        changes.RecordMutation(WorldStoreKind.Identity);
        species.Create(id, genomeId, genome.Phenotype, account, lineage, changes);
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

        if (initialState.IngestedStructuralMatterQ < 0 ||
            initialState.StructuralMatterQ < 0 ||
            initialState.ChargedReserveQ < 0 ||
            initialState.LifecyclePhase == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialState),
                "The organism lifecycle and nonnegative balances must be valid.");
        }

        var phenotype = GetCompiledPhenotype(initialState.SpeciesId);
        if (initialState.IngestedStructuralMatterQ >
            phenotype.Physiology.IngestedMatterCapacityLoadQ)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialState),
                "Ingested matter exceeds the compiled buffer capacity.");
        }
        if (!initialState.CommittedMicronutrients.Contains(
                MicronutrientInventory.Compile(
                    phenotype.Physiology.CommittedMicronutrientQuotas)) ||
            initialState.FreeMicronutrients.TotalLoadQ >
                phenotype.Physiology.FreeMicronutrientCapacityLoadQ)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialState),
                "Micronutrient quotas or free-store capacity are invalid for the compiled DNA.");
        }
        OrganismConditionBuilder.ValidatePrimaryState(
            phenotype.Physiology,
            new OrganismConditionInput(
                initialState.StructuralMatterQ,
                initialState.ChargedReserveQ,
                initialState.BiologicalAgeHours));
        var materialized = OrganismConditionBuilder.Build(
            phenotype.Physiology,
            new OrganismConditionInput(
                initialState.StructuralMatterQ,
                initialState.ChargedReserveQ,
                initialState.BiologicalAgeHours),
            GetOrganismEnvironment(initialState.TileId),
            initialState.BirthTick,
            ConditionSnapshotKind.End);

        var id = ids.AllocateOrganism();
        changes.RecordMutation(WorldStoreKind.Identity);
        organisms.Create(id, initialState, materialized, changes);
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

    public void SetOrganismMicronutrients(
        OrganismId organismId,
        MicronutrientInventory committed,
        MicronutrientInventory free,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var organism = organisms.Get(organismId);
        var physiology = GetCompiledPhenotype(organism.SpeciesId).Physiology;
        if (!committed.Contains(MicronutrientInventory.Compile(
                physiology.CommittedMicronutrientQuotas)) ||
            free.TotalLoadQ > physiology.FreeMicronutrientCapacityLoadQ)
        {
            throw new ArgumentOutOfRangeException(nameof(free));
        }
        organisms.SetMicronutrients(organismId, committed, free, changes);
    }

    public RemnantId CreateRemnant(
        RemnantInitialState initialState,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        if (!species.Contains(initialState.SourceSpeciesId) ||
            !tiles.Contains(initialState.TileId))
        {
            throw new ArgumentException("The remnant must reference an existing species and tile.");
        }

        var id = ids.AllocateRemnant();
        changes.RecordMutation(WorldStoreKind.Identity);
        remnants.Create(id, initialState, changes);
        return id;
    }

    public void SetRemnantContents(
        RemnantId remnantId,
        long structuralMatterQ,
        long chargedReserveQ,
        uint structureDecayRemainderQ,
        uint reserveDecayRemainderQ,
        PhaseChangeBuilder changes,
        MicronutrientInventory? micronutrients = null)
    {
        RequireActive(changes);
        remnants.SetContents(
            remnantId,
            structuralMatterQ,
            chargedReserveQ,
            structureDecayRemainderQ,
            reserveDecayRemainderQ,
            micronutrients,
            changes);
    }

    public RemnantSnapshot RemoveRemnant(RemnantId remnantId, PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        return remnants.Remove(remnantId, changes);
    }

    public RemnantSnapshot GetRemnant(RemnantId id) => remnants.Get(id);

    public ImmutableArray<RemnantId> GetRemnantIdsInCanonicalOrder() =>
        remnants.GetIdsInCanonicalOrder();

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

    public void SetOrganismBehavior(
        OrganismId organismId,
        OrganismBehaviorState behavior,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        organisms.SetBehavior(organismId, behavior, changes);
    }

    public void TransferOrganismSpecies(
        OrganismId organismId,
        SpeciesId destinationSpeciesId,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        if (!species.Contains(destinationSpeciesId))
        {
            throw new KeyNotFoundException($"Species {destinationSpeciesId} does not exist.");
        }
        var current = organisms.Get(organismId);
        if (current.SpeciesId == destinationSpeciesId)
        {
            return;
        }
        species.DecrementPopulation(current.SpeciesId, changes);
        organisms.SetSpecies(organismId, destinationSpeciesId, changes);
        species.IncrementPopulation(destinationSpeciesId, changes);
    }

    public void SetSpeciesEvolutionAggregate(
        SpeciesId speciesId,
        SpeciesEvolutionAccount account,
        ulong completedTick,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        species.SetEvolutionAggregate(speciesId, account, completedTick, changes);
    }

    public void SetSpeciesSpeciationState(
        SpeciesId speciesId,
        SpeciesEvolutionAccount account,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        species.SetSpeciationState(speciesId, account, changes);
    }

    public void AppendSpeciationEvent(
        SpeciationEventRecord value,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        species.AppendSpeciationEvent(value, changes);
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

    public void AdjustOrganismIngestedStructuralMatter(
        OrganismId organismId,
        long delta,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var organism = organisms.Get(organismId);
        var capacity = GetCompiledPhenotype(organism.SpeciesId)
            .Physiology.IngestedMatterCapacityLoadQ;
        var next = checked(organism.IngestedStructuralMatterQ + delta);
        if (next > capacity)
        {
            throw new InvalidOperationException(
                $"Organism {organismId} ingested matter {next} exceeds capacity {capacity}.");
        }

        organisms.AdjustIngestedStructuralMatter(organismId, delta, changes);
    }

    public void AdjustOrganismChargedReserve(
        OrganismId organismId,
        long delta,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var organism = organisms.Get(organismId);
        var capacity = GetCompiledPhenotype(organism.SpeciesId)
            .Physiology.ChargedReserveCapacityQ;
        var next = checked(organism.ChargedReserveQ + delta);
        if (next > capacity)
        {
            throw new InvalidOperationException(
                $"Organism {organismId} reserve {next} exceeds compiled capacity {capacity}.");
        }

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

    public void SetOrganismLifecycleSchedule(
        OrganismId organismId,
        ulong reproductionNotBeforeTick,
        ulong successfulReproductionCount,
        ulong scavengeNotBeforeTick,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        organisms.SetLifecycleSchedule(
            organismId,
            reproductionNotBeforeTick,
            successfulReproductionCount,
            scavengeNotBeforeTick,
            changes);
    }

    public void MaterializeOrganismCondition(
        OrganismId organismId,
        ulong evaluatedTick,
        ConditionSnapshotKind snapshotKind,
        OrganismEnvironmentInput environment,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var organism = organisms.Get(organismId);
        var physiology = GetCompiledPhenotype(organism.SpeciesId).Physiology;
        var condition = OrganismConditionBuilder.Build(
            physiology,
            new OrganismConditionInput(
                organism.StructuralMatterQ,
                organism.ChargedReserveQ,
                organism.BiologicalAgeHours),
            environment,
            evaluatedTick,
            snapshotKind);
        organisms.SetCondition(organismId, condition, changes);
    }

    public void SetMaterializedOrganismCondition(
        OrganismId organismId,
        MaterializedOrganismCondition condition,
        PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        var organism = organisms.Get(organismId);
        var physiology = GetCompiledPhenotype(organism.SpeciesId).Physiology;
        var expected = OrganismConditionBuilder.Build(
            physiology,
            new OrganismConditionInput(
                organism.StructuralMatterQ,
                organism.ChargedReserveQ,
                organism.BiologicalAgeHours),
            GetOrganismEnvironment(
                organism.TileId,
                temperatureOverrideMilliC: condition.TemperatureMilliC),
            condition.EvaluatedTick,
            condition.SnapshotKind);
        if (condition != expected)
        {
            throw new InvalidOperationException(
                "Only the canonical health builder's value may be materialized.");
        }

        organisms.SetCondition(organismId, condition, changes);
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

    public OrganismEnvironmentInput GetOrganismEnvironment(
        TileId tileId,
        ulong absoluteHour = 0,
        int? temperatureOverrideMilliC = null)
    {
        var temperatureMilliC = temperatureOverrideMilliC ??
            (Rules.GeneratedWorld is null
                ? 45_000
                : WorldClimateEvaluator.Evaluate(
                    Rules.GeneratedWorld,
                    Rules.GeneratedWorld.GetTile(tileId.Value),
                    absoluteHour).TemperatureMilliC);
        return new OrganismEnvironmentInput(
            temperatureMilliC,
            GetTileResource(tileId, GetResourceHandle(HydrogenSulfideResourceId)),
            GetTileResource(tileId, GetResourceHandle(SulfurDioxideResourceId)));
    }

    public ImmutableArray<GasEdge> GetGasEdges() => tileResources.GasEdges;

    public (long SourceQ, long SinkQ) GetGasTileRemainders(int gasSlot, TileId tileId) =>
        tileResources.GetGasTileRemainders(gasSlot, tileId);

    public long GetGasExchangeRemainder(int gasSlot, int edgeSlot) =>
        tileResources.GetGasExchangeRemainder(gasSlot, edgeSlot);

    public void SetGasTileRemainders(
        int gasSlot, TileId tileId, long sourceQ, long sinkQ, PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        tileResources.SetGasTileRemainders(gasSlot, tileId, sourceQ, sinkQ, changes);
    }

    public void SetGasExchangeRemainder(
        int gasSlot, int edgeSlot, long remainderQ, PhaseChangeBuilder changes)
    {
        RequireActive(changes);
        tileResources.SetGasExchangeRemainder(gasSlot, edgeSlot, remainderQ, changes);
    }

    public GenomeSnapshot GetGenome(GenomeId id) => genomes.Get(id);

    public SpeciesSnapshot GetSpecies(SpeciesId id) => species.Get(id);

    public CompiledPhenotype GetCompiledPhenotype(SpeciesId id)
    {
        var handle = species.Get(id).Phenotype;
        return compiledPhenotypes[handle.DenseSlot];
    }

    public ImmutableArray<SpeciationEventRecord> GetSpeciationEvents() =>
        species.GetSpeciationEvents();

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

        foreach (var remnantId in remnants.GetIdsInCanonicalOrder())
        {
            var remnant = remnants.Get(remnantId);
            if (!species.Contains(remnant.SourceSpeciesId) || !tiles.Contains(remnant.TileId) ||
                remnant.IsEmpty)
            {
                throw new InvalidOperationException($"Remnant {remnantId} has invalid references or no contents.");
            }
        }

        foreach (var genomeId in genomes.GetIdsInCanonicalOrder())
        {
            var genome = genomes.Get(genomeId);
            if (genome.Phenotype.DenseSlot < 0 ||
                genome.Phenotype.DenseSlot >= compiledPhenotypes.Count ||
                compiledPhenotypes[genome.Phenotype.DenseSlot].FounderGenomeId != genome.FounderGenomeId ||
                compiledPhenotypes[genome.Phenotype.DenseSlot].FounderAllocationId != genome.FounderAllocationId ||
                !compiledPhenotypes[genome.Phenotype.DenseSlot].AcquiredTraits.SequenceEqual(genome.AcquiredTraits) ||
                !string.Equals(compiledPhenotypes[genome.Phenotype.DenseSlot].CanonicalCompiledHash, genome.GenomeHash, StringComparison.Ordinal))
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

            var physiology = GetCompiledPhenotype(organism.SpeciesId).Physiology;
            OrganismConditionBuilder.ValidatePrimaryState(
                physiology,
                new OrganismConditionInput(
                    organism.StructuralMatterQ,
                    organism.ChargedReserveQ,
                    organism.BiologicalAgeHours));
            if (organism.IngestedStructuralMatterQ < 0 ||
                organism.IngestedStructuralMatterQ > physiology.IngestedMatterCapacityLoadQ)
            {
                throw new InvalidOperationException(
                    $"Organism {organismId} has invalid ingested-matter storage.");
            }
            RatioQ.RequireRatio(organism.Condition.RelativeHealthQ);
            if (organism.Condition.SnapshotKind == default)
            {
                throw new InvalidOperationException(
                    $"Organism {organismId} has no materialized condition snapshot.");
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

        if (!gameplay.IsInitialized)
        {
            return;
        }

        var game = gameplay.Get();
        if (!species.Contains(game.ControlledSpeciesId) ||
            game.Roots.Any(root => !species.Contains(root.SpeciesId) ||
                !tiles.Contains(root.StartingTileId) ||
                species.Get(root.SpeciesId).Lineage.ParentSpeciesId.HasValue) ||
            game.MutationLockedSpeciesIds.Any(id => !species.Contains(id)))
        {
            throw new InvalidOperationException("Gameplay state has dangling world references.");
        }

        var locked = game.MutationLockedSpeciesIds.ToHashSet();
        foreach (var speciesId in species.GetIdsInCanonicalOrder())
        {
            var authority = species.Get(speciesId).Evolution.Authority;
            var expected = locked.Contains(speciesId)
                ? EvolutionAuthorityKind.Locked
                : speciesId == game.ControlledSpeciesId
                    ? EvolutionAuthorityKind.Controlled
                    : EvolutionAuthorityKind.Autonomous;
            if (authority != expected)
            {
                throw new InvalidOperationException(
                    $"Species {speciesId} authority is inconsistent with gameplay control and locks.");
            }
        }

    }

    public void ValidateGameplayOutcomeInvariants()
    {
        if (!gameplay.IsInitialized)
        {
            throw new InvalidOperationException("Gameplay state is not initialized.");
        }

        var game = gameplay.Get();
        var totalPopulation = species.GetIdsInCanonicalOrder()
            .Aggregate(0UL, (sum, id) => checked(sum + species.Get(id).Population));
        var controlledPopulation = species.Get(game.ControlledSpeciesId).Population;
        var expectedLoss = game.Mode switch
        {
            GameMode.FreeSandbox when totalPopulation == 0 => GameLossReason.AllLifeExtinct,
            GameMode.Survival when controlledPopulation == 0 => GameLossReason.ControlledSpeciesExtinct,
            _ => GameLossReason.None,
        };
        var recordedLoss = game.RunStatus == GameRunStatus.Lost
            ? game.LossReason
            : GameLossReason.None;
        if (recordedLoss != expectedLoss)
        {
            throw new InvalidOperationException("Gameplay outcome is inconsistent with living populations.");
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

    private CompiledPhenotype ResolveFounderPhenotype(FounderGenomeId founderGenomeId)
    {
        for (var index = 0; index < Rules.RulePack.FounderPhenotypes.Length; index++)
        {
            var phenotype = Rules.RulePack.FounderPhenotypes[index];
            if (phenotype.FounderGenomeId == founderGenomeId)
            {
                return phenotype;
            }
        }

        throw new KeyNotFoundException(
            $"Founder genome {founderGenomeId} has no compiled phenotype in this world.");
    }

    private PhenotypeHandle InternCompiledPhenotype(CompiledPhenotype phenotype)
    {
        for (var index = 0; index < compiledPhenotypes.Count; index++)
        {
            if (string.Equals(
                    compiledPhenotypes[index].CanonicalCompiledHash,
                    phenotype.CanonicalCompiledHash,
                    StringComparison.Ordinal))
            {
                return new PhenotypeHandle(phenotype.FounderGenomeId, index);
            }
        }
        var slot = compiledPhenotypes.Count;
        compiledPhenotypes.Add(phenotype);
        return new PhenotypeHandle(phenotype.FounderGenomeId, slot);
    }

    private Dictionary<WorldStoreKind, StoreCoverage> CaptureCoverage()
    {
#if DEBUG
        var identityFingerprint = ids.ComputeCoverageFingerprint();
        var tileResourceFingerprint = tileResources.ComputeCoverageFingerprint();
        var genomeFingerprint = genomes.ComputeCoverageFingerprint();
        var speciesFingerprint = species.ComputeCoverageFingerprint();
        var organismFingerprint = organisms.ComputeCoverageFingerprint();
        var remnantFingerprint = remnants.ComputeCoverageFingerprint();
        var gameplayFingerprint = gameplay.ComputeCoverageFingerprint();
#else
        const ulong identityFingerprint = 0;
        const ulong tileResourceFingerprint = 0;
        const ulong genomeFingerprint = 0;
        const ulong speciesFingerprint = 0;
        const ulong organismFingerprint = 0;
        const ulong remnantFingerprint = 0;
        const ulong gameplayFingerprint = 0;
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
            [WorldStoreKind.Remnants] = new StoreCoverage(
                remnants.MutationEpoch,
                remnantFingerprint),
            [WorldStoreKind.Gameplay] = new StoreCoverage(
                gameplay.MutationEpoch,
                gameplayFingerprint),
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

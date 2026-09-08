using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class MutableWorldStateTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(1_000, 4)]
    public void LoadsFounderPopulationIntoTilePartitionedChunks(
        int founderCount,
        int expectedChunks)
    {
        var state = new MutableWorldState(CompileWorld());
        var initialized = CreatePopulation(state, founderCount, TileId.FromRowMajorIndex(0));

        Assert.Equal(1, state.GenomeCount);
        Assert.Equal(1, state.SpeciesCount);
        Assert.Equal(founderCount, state.OrganismCount);
        Assert.Equal((ulong)founderCount, state.GetSpecies(initialized.SpeciesId).Population);
        Assert.Equal(
            expectedChunks,
            state.GetAllocatedOrganismChunkCount(TileId.FromRowMajorIndex(0)));
        Assert.Equal(founderCount + 2, initialized.Changes.Creates.Length);
        Assert.Single(
            initialized.Changes.DirtyEntities,
            dirty => dirty.FieldGroup == LogicalFieldGroup.SpeciesPopulation);

        var first = state.GetOrganism(initialized.OrganismIds[0]);
        var last = state.GetOrganism(initialized.OrganismIds[^1]);
        Assert.Equal(1UL, first.Id.Value);
        Assert.Equal((ulong)founderCount, last.Id.Value);
        Assert.Equal(1_000L, first.StructuralMatterQ);
        Assert.Equal(5_000L, first.ChargedReserveQ);
        state.ValidateInvariants();
    }

    [Fact]
    public void TypedMutatorsCaptureEveryLogicalFieldChange()
    {
        var state = new MutableWorldState(CompileWorld());
        var initialized = CreatePopulation(state, 1, TileId.FromRowMajorIndex(0));
        var organismId = Assert.Single(initialized.OrganismIds);
        var tileId = TileId.FromRowMajorIndex(0);
        var hydrogen = state.GetResourceHandle(ResourceId.From(1));
        var initialHydrogen = state.GetTileResource(tileId, hydrogen);
        var changes = state.BeginChanges();

        state.ApplyTileResourceDelta(tileId, hydrogen, -10, changes);
        state.SetOrganismPosition(organismId, 123, 456, 7, -8, changes);
        state.AdjustOrganismStructure(organismId, 50, changes);
        state.AdjustOrganismChargedReserve(organismId, -100, changes);
        state.AdvanceOrganismBiologicalAge(organismId, 1, changes);
        var sealedChanges = state.SealChanges(changes);

        Assert.Equal(initialHydrogen - 10, state.GetTileResource(tileId, hydrogen));
        var organism = state.GetOrganism(organismId);
        Assert.Equal(123U, organism.PositionXQ);
        Assert.Equal(456U, organism.PositionYQ);
        Assert.Equal(7L, organism.VelocityXQPerHour);
        Assert.Equal(-8L, organism.VelocityYQPerHour);
        Assert.Equal(1_050L, organism.StructuralMatterQ);
        Assert.Equal(4_900L, organism.ChargedReserveQ);
        Assert.Equal(1UL, organism.BiologicalAgeHours);
        Assert.Equal(
            [
                LogicalFieldGroup.TileResources,
                LogicalFieldGroup.OrganismPosition,
                LogicalFieldGroup.OrganismLifecycle,
                LogicalFieldGroup.OrganismStructure,
                LogicalFieldGroup.OrganismReserve,
            ],
            sealedChanges.DirtyEntities.Select(dirty => dirty.FieldGroup));
    }

    [Fact]
    public void InvalidBalanceMutationChangesNeitherStateNorJournal()
    {
        var state = new MutableWorldState(CompileWorld());
        var initialized = CreatePopulation(state, 1, TileId.FromRowMajorIndex(0));
        var organismId = Assert.Single(initialized.OrganismIds);
        var changes = state.BeginChanges();

        Assert.Throws<InvalidOperationException>(() =>
            state.AdjustOrganismChargedReserve(organismId, -5_001, changes));

        var sealedChanges = state.SealChanges(changes);
        Assert.Equal(5_000L, state.GetOrganism(organismId).ChargedReserveQ);
        Assert.Empty(sealedChanges.DirtyEntities);
    }

    [Fact]
    public void ReserveCannotExceedCompiledCapacity()
    {
        var state = new MutableWorldState(CompileWorld());
        var initialized = CreatePopulation(state, 1, TileId.FromRowMajorIndex(0));
        var organismId = Assert.Single(initialized.OrganismIds);
        var changes = state.BeginChanges();

        Assert.Throws<InvalidOperationException>(() =>
            state.AdjustOrganismChargedReserve(organismId, 5_001, changes));

        Assert.Empty(state.SealChanges(changes).DirtyEntities);
        Assert.Equal(5_000, state.GetOrganism(organismId).ChargedReserveQ);
    }

    [Fact]
    public void SwapRemovalPreservesLocatorsAndNeverReusesIds()
    {
        var state = new MutableWorldState(CompileWorld());
        var initialized = CreatePopulation(state, 300, TileId.FromRowMajorIndex(0));
        var removedId = initialized.OrganismIds[49];
        var previousTailId = initialized.OrganismIds[^1];
        var changes = state.BeginChanges();

        state.RemoveOrganism(removedId, changes);
        var removalChanges = state.SealChanges(changes);

        Assert.Equal(299, state.OrganismCount);
        Assert.Equal(299UL, state.GetSpecies(initialized.SpeciesId).Population);
        Assert.Equal(previousTailId, state.GetOrganism(previousTailId).Id);
        Assert.Single(removalChanges.Removes);
        Assert.Equal(removedId.Value, removalChanges.Removes[0].Value);
        state.ValidateInvariants();

        var createChanges = state.BeginChanges();
        var next = state.CreateOrganism(
            CreateInitialOrganism(initialized.SpeciesId, TileId.FromRowMajorIndex(0), 999),
            createChanges);
        state.SealChanges(createChanges);

        Assert.Equal(301UL, next.Value);
        state.ValidateInvariants();
    }

    [Fact]
    public void CrossTileRelocationIsOneStableLogicalOperation()
    {
        var state = new MutableWorldState(CompileWorld(twoTiles: true));
        var initialized = CreatePopulation(state, 300, TileId.FromRowMajorIndex(0));
        var organismId = initialized.OrganismIds[49];
        var changes = state.BeginChanges();

        state.RelocateOrganism(
            organismId,
            TileId.FromRowMajorIndex(1),
            17,
            19,
            changes);
        var sealedChanges = state.SealChanges(changes);

        var organism = state.GetOrganism(organismId);
        Assert.Equal(organismId, organism.Id);
        Assert.Equal(TileId.FromRowMajorIndex(1), organism.TileId);
        Assert.Equal(17U, organism.PositionXQ);
        Assert.Equal(19U, organism.PositionYQ);
        var relocation = Assert.Single(sealedChanges.Relocations);
        Assert.Equal(organismId, relocation.OrganismId);
        Assert.Equal(TileId.FromRowMajorIndex(0), relocation.SourceTileId);
        Assert.Equal(TileId.FromRowMajorIndex(1), relocation.DestinationTileId);
        state.ValidateInvariants();
    }

    [Fact]
    public void GenomeAndSpeciesRetainCompiledPhenotypeWhileOrganismsStoreOnlySpecies()
    {
        var state = new MutableWorldState(CompileWorld());
        var initialized = CreatePopulation(state, 1, TileId.FromRowMajorIndex(0));

        var genome = state.GetGenome(initialized.GenomeId);
        var species = state.GetSpecies(initialized.SpeciesId);
        var organism = state.GetOrganism(Assert.Single(initialized.OrganismIds));

        Assert.Equal(1U, genome.FounderGenomeId.Value);
        Assert.Equal(0, genome.Phenotype.DenseSlot);
        Assert.Equal(genome.Phenotype, species.Phenotype);
        Assert.Equal(initialized.SpeciesId, organism.SpeciesId);
        Assert.DoesNotContain(
            typeof(OrganismSnapshot).GetProperties(),
            property => property.Name.Contains("Phenotype", StringComparison.Ordinal) ||
                property.Name.Contains("Chunk", StringComparison.Ordinal) ||
                property.Name.Contains("Row", StringComparison.Ordinal));
    }

    [Fact]
    public void ChangeBuilderCannotMutateAnotherWorld()
    {
        var first = new MutableWorldState(CompileWorld());
        var second = new MutableWorldState(CompileWorld());
        var firstChanges = first.BeginChanges();
        var secondChanges = second.BeginChanges();

        Assert.Throws<InvalidOperationException>(() =>
            first.CreateGenome(FounderGenomeId.From(1), secondChanges));

        Assert.Empty(first.SealChanges(firstChanges).Creates);
        Assert.Empty(second.SealChanges(secondChanges).Creates);
    }

    [Fact]
    public void EquivalentCanonicalCreationSequencesAllocateTheSameStableIds()
    {
        var first = new MutableWorldState(CompileWorld());
        var second = new MutableWorldState(CompileWorld());

        var firstPopulation = CreatePopulation(first, 300, TileId.FromRowMajorIndex(0));
        var secondPopulation = CreatePopulation(second, 300, TileId.FromRowMajorIndex(0));

        Assert.Equal(firstPopulation.GenomeId, secondPopulation.GenomeId);
        Assert.Equal(firstPopulation.SpeciesId, secondPopulation.SpeciesId);
        Assert.Equal(firstPopulation.OrganismIds, secondPopulation.OrganismIds);
        Assert.Equal(firstPopulation.Changes.Creates, secondPopulation.Changes.Creates);
        Assert.Equal(firstPopulation.Changes.DirtyEntities, secondPopulation.Changes.DirtyEntities);
    }

#if DEBUG
    [Fact]
    public void DebugCoverageFaultsWorldAfterUntrackedMutation()
    {
        var state = new MutableWorldState(CompileWorld());
        var initialized = CreatePopulation(state, 1, TileId.FromRowMajorIndex(0));
        var organismId = Assert.Single(initialized.OrganismIds);
        var changes = state.BeginChanges();

        state.DebugSetChargedReserveWithoutTracking(organismId, 4_999);

        var exception = Assert.Throws<InvalidOperationException>(() => state.SealChanges(changes));
        Assert.Contains("without passing through a tracked mutator", exception.Message);
        Assert.True(state.IsFaulted);
        Assert.Throws<InvalidOperationException>(state.BeginChanges);
    }
#endif

    private static InitializedPopulation CreatePopulation(
        MutableWorldState state,
        int count,
        TileId tileId)
    {
        var changes = state.BeginChanges();
        var genomeId = state.CreateGenome(FounderGenomeId.From(1), changes);
        var speciesId = state.CreateSpecies(genomeId, changes);
        var organismIds = new OrganismId[count];
        for (var index = 0; index < count; index++)
        {
            organismIds[index] = state.CreateOrganism(
                CreateInitialOrganism(speciesId, tileId, index),
                changes);
        }

        return new InitializedPopulation(
            genomeId,
            speciesId,
            organismIds,
            state.SealChanges(changes));
    }

    private static OrganismInitialState CreateInitialOrganism(
        SpeciesId speciesId,
        TileId tileId,
        int ordinal)
    {
        var coordinate = unchecked((uint)((ordinal + 1L) * 2_654_435_761L));
        return new OrganismInitialState(
            speciesId,
            tileId,
            coordinate,
            ~coordinate,
            0,
            0,
            0,
            0,
            LifecyclePhase.Mature,
            0,
            0,
            0,
            0,
            1_000,
            5_000,
            OrganismBehaviorState.Initial(0),
            new MicronutrientInventory(0, 20, 10, 10, 10, 0, 0, 0, 0, 0, 0, 0, 5, 2));
    }

    private static CompiledWorldRules CompileWorld(bool twoTiles = false)
    {
        var sourceRules = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(sourceRules.IsSuccess, FormatDiagnostics(sourceRules.Diagnostics));
        var compiledRules = RulePackCompiler.Compile(
            Assert.IsType<AuthoringRulePack>(sourceRules.Pack));
        Assert.True(compiledRules.IsSuccess, FormatDiagnostics(compiledRules.Diagnostics));
        var rulePack = Assert.IsType<CompiledRulePack>(compiledRules.RulePack);

        var sourceWorld = WorldPackSourceLoader.Load(new CopiedPackageSource("OfficialWorld"));
        Assert.True(sourceWorld.IsSuccess, FormatDiagnostics(sourceWorld.Diagnostics));
        var authoringWorld = Assert.IsType<AuthoringWorldPack>(sourceWorld.Pack);
        if (twoTiles)
        {
            var profile = Assert.Single(authoringWorld.Profiles);
            var firstTile = Assert.Single(profile.Tiles!);
            authoringWorld = authoringWorld with
            {
                Profiles =
                [
                    profile with
                    {
                        Width = 2,
                        Tiles = [firstTile, firstTile with { X = 1 }],
                    },
                ],
            };
        }

        var compiledWorld = WorldRulesCompiler.Compile(
            rulePack,
            authoringWorld,
            "scenario.foundation-sandbox",
            "world.primordial-foundation");
        Assert.True(compiledWorld.IsSuccess, FormatDiagnostics(compiledWorld.Diagnostics));
        return Assert.IsType<CompiledWorldRules>(compiledWorld.WorldRules);
    }

    private static string FormatDiagnostics(IEnumerable<RuleDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"{diagnostic.SourceFile}: {diagnostic.Code}: {diagnostic.Message}"));

    private sealed class CopiedPackageSource : IContentSource
    {
        private readonly string root;

        public CopiedPackageSource(string directoryName) =>
            root = Path.Combine(AppContext.BaseDirectory, directoryName);

        public bool TryRead(string normalizedRelativePath, out ReadOnlyMemory<byte> content)
        {
            var path = Path.Combine(root, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                content = default;
                return false;
            }

            content = File.ReadAllBytes(path);
            return true;
        }
    }

    private sealed record InitializedPopulation(
        GenomeId GenomeId,
        SpeciesId SpeciesId,
        OrganismId[] OrganismIds,
        PhaseChangeSet Changes);
}

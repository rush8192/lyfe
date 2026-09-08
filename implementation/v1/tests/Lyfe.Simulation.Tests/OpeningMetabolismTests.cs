using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.Ticks;
using Lyfe.Simulation.World;
using Lyfe.Simulation.World.Generation;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class OpeningMetabolismTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("13579bdf2468ace0fedcba9876543210");

    [Fact]
    public void FoundersExposeDistinctExecutableOpeningProfiles()
    {
        var rules = CompileWorld().RulePack;
        var hydrogen = rules.FounderPhenotypes.Single(phenotype =>
            phenotype.FounderGenomeId.Value == 1);
        var sulfur = rules.FounderPhenotypes.Single(phenotype =>
            phenotype.FounderGenomeId.Value == 2);

        Assert.Equal(FoundingMetabolismKind.HydrogenAcetogenesis,
            hydrogen.Physiology.OpeningMetabolism.Kind);
        Assert.Equal(250U,
            hydrogen.Physiology.OpeningMetabolism.MaximumCaptureExtentsPerHour);
        Assert.Equal(800_000U,
            hydrogen.Physiology.OpeningMetabolism.FavorableCaptureEfficiencyQ);
        Assert.False(hydrogen.Physiology.OpeningMetabolism.RequiresLight);
        Assert.Contains(hydrogen.Processes, process => process.Reaction.Id.Value == 1);
        Assert.Contains(hydrogen.Processes, process => process.Reaction.Id.Value == 4);
        Assert.Contains(hydrogen.Processes, process => process.Reaction.Id.Value == 5);

        Assert.Equal(FoundingMetabolismKind.SulfideAnoxygenicPhototrophy,
            sulfur.Physiology.OpeningMetabolism.Kind);
        Assert.Equal(1_000U,
            sulfur.Physiology.OpeningMetabolism.MaximumCaptureExtentsPerHour);
        Assert.Equal(950_000U,
            sulfur.Physiology.OpeningMetabolism.FavorableCaptureEfficiencyQ);
        Assert.True(sulfur.Physiology.OpeningMetabolism.RequiresLight);
        Assert.Equal(12U, sulfur.Physiology.OpeningMetabolism.IlluminatedHoursPerDay);
        Assert.Equal(2_083U,
            sulfur.Physiology.OpeningMetabolism.GeneratedLightCaptureExtentsPerUnitHour);
        Assert.Contains(sulfur.Processes, process => process.Reaction.Id.Value == 3);
        Assert.Contains(sulfur.Processes, process => process.Reaction.Id.Value == 4);
        Assert.Contains(sulfur.Processes, process => process.Reaction.Id.Value == 5);
    }

    [Fact]
    public void FounderMicronutrientsAreDebitedPersistedAndStockpiledToOneExtraQuota()
    {
        var runner = CreateSandbox(CompileWorld(), FounderGenomeId.From(1));
        var world = runner.MutableWorld;
        var organismId = Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        var initial = world.GetOrganism(organismId);
        Assert.Equal(57, initial.CommittedMicronutrients.TotalLoadQ);
        Assert.Equal(0, initial.FreeMicronutrients.TotalLoadQ);
        Assert.Equal(1_999_980, world.GetTileResource(
            initial.TileId, world.GetResourceHandle(ResourceId.From(19))));

        for (var tick = 0; tick < 240; tick++) runner.AdvanceOneTick();

        var stocked = world.GetOrganism(organismId);
        Assert.Equal(stocked.CommittedMicronutrients, stocked.FreeMicronutrients);
        Assert.Equal(57, stocked.FreeMicronutrients.TotalLoadQ);
        Assert.Equal(1_999_960, world.GetTileResource(
            stocked.TileId, world.GetResourceHandle(ResourceId.From(19))));
    }

    [Fact]
    public void MissingMicronutrientQuotaBlocksOtherwiseEligibleReproduction()
    {
        var runner = CreateSandbox(CompileWorld(), FounderGenomeId.From(1));
        var world = runner.MutableWorld;
        var organismId = Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        var organism = world.GetOrganism(organismId);
        var cobalt = world.GetResourceHandle(ResourceId.From(31));
        var changes = world.BeginChanges();
        world.AdjustOrganismStructure(organismId, 1_000, changes);
        world.AdjustOrganismChargedReserve(organismId, 3_500, changes);
        world.SetOrganismLifecycleSchedule(organismId, 0, 0, 0, changes);
        world.SetOrganismMicronutrients(
            organismId,
            organism.CommittedMicronutrients,
            organism.CommittedMicronutrients.With(
                MicronutrientInventory.Slot(cobalt.Id),
                organism.CommittedMicronutrients[MicronutrientInventory.Slot(cobalt.Id)] - 1),
            changes);
        world.ApplyTileResourceDelta(
            organism.TileId,
            cobalt,
            -world.GetTileResource(organism.TileId, cobalt),
            changes);
        world.SealChanges(changes);

        runner.AdvanceOneTick();

        Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        Assert.Equal(0UL, world.GetOrganism(organismId).SuccessfulReproductionCount);
        var gate = Assert.Single(
            Assert.Single(runner.CapturePublicationSnapshot().Organisms).ActionGateEvidence,
            value => value.Process == PublicationOrganismActionProcessKind.Reproduction);
        Assert.Equal(
            PublicationOrganismActionGateReason.OffspringMicronutrientQuotaMissing,
            gate.Reason);
        Assert.Equal(cobalt.Id, gate.ResourceId);
        Assert.Equal(1, gate.AvailableQ);
        Assert.Equal(2, gate.RequiredQ);
    }

    [Fact]
    public void FounderAllocationsCompileIntoDistinctCachedPhysiologyAndGenomeIdentity()
    {
        var rules = CompileWorld();
        var balanced = CreateSandbox(
            rules,
            FounderGenomeId.From(1),
            FounderAllocationId.From(1));
        var throughput = CreateSandbox(
            rules,
            FounderGenomeId.From(1),
            FounderAllocationId.From(2));
        var tolerant = CreateSandbox(
            rules,
            FounderGenomeId.From(1),
            FounderAllocationId.From(3));

        var balancedSpecies = Assert.Single(balanced.CapturePublicationSnapshot().Species);
        var throughputSpecies = Assert.Single(throughput.CapturePublicationSnapshot().Species);
        var tolerantSpecies = Assert.Single(tolerant.CapturePublicationSnapshot().Species);
        var throughputPhysiology = throughput.MutableWorld
            .GetCompiledPhenotype(throughputSpecies.SpeciesId).Physiology;
        var tolerantPhysiology = tolerant.MutableWorld
            .GetCompiledPhenotype(tolerantSpecies.SpeciesId).Physiology;

        Assert.Equal(FounderAllocationId.From(2), throughputSpecies.FounderAllocationId);
        Assert.Equal(850_000U, throughputPhysiology.OpeningMetabolism.FavorableCaptureEfficiencyQ);
        Assert.Equal(16_000_000, throughputPhysiology.ChemicalResponse.HydrogenSulfideSoftThresholdQ);
        Assert.Equal(100_000_000, tolerantPhysiology.ChemicalResponse.HydrogenSulfideHardThresholdQ);
        Assert.Equal(25_000_000, tolerantPhysiology.ChemicalResponse.HydrogenSulfideSoftThresholdQ);
        Assert.Equal(750_000U, tolerantPhysiology.OpeningMetabolism.FavorableCaptureEfficiencyQ);
        Assert.Equal(3, new[]
        {
            balancedSpecies.GenomeHash,
            throughputSpecies.GenomeHash,
            tolerantSpecies.GenomeHash,
        }.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void FavorableFirstHourMakesSulfurFasterAndReturnsElementalSulfur()
    {
        var rules = CompileWorld();
        var hydrogen = CreateSandbox(rules, FounderGenomeId.From(1));
        var sulfur = CreateSandbox(rules, FounderGenomeId.From(2));
        var sulfurResource = sulfur.MutableWorld.GetResourceHandle(ResourceId.From(15));

        var hydrogenResult = hydrogen.AdvanceOneTick();
        var sulfurResult = sulfur.AdvanceOneTick();

        var hydrogenOrganism = Assert.Single(hydrogen.MutableWorld
            .GetOrganismIdsInCanonicalOrder().Select(hydrogen.MutableWorld.GetOrganism))
            ;
        var sulfurOrganism = Assert.Single(sulfur.MutableWorld
            .GetOrganismIdsInCanonicalOrder().Select(sulfur.MutableWorld.GetOrganism))
            ;
        Assert.Equal(5_044, hydrogenOrganism.ChargedReserveQ);
        Assert.Equal(1_003, hydrogenOrganism.StructuralMatterQ);
        Assert.Equal(5_494, sulfurOrganism.ChargedReserveQ);
        Assert.Equal(1_004, sulfurOrganism.StructuralMatterQ);
        Assert.Equal(1_900, sulfur.MutableWorld.GetTileResource(
            TileId.FromRowMajorIndex(0), sulfurResource));
        Assert.Equal(200, Assert.Single(CaptureTransactions(hydrogenResult)).Extent);
        Assert.Equal(950, Assert.Single(CaptureTransactions(sulfurResult)).Extent);
        Assert.True(ResourceLedgerOracle.Reconcile(
            rules.RulePack,
            CaptureTransactions(sulfurResult)).IsBalanced);
    }

    [Fact]
    public void FounderMaintenanceAndBiomassAssemblyAreExactConservativeBundles()
    {
        var rules = CompileWorld();
        var runner = CreateSandbox(rules, FounderGenomeId.From(1));
        var tileId = TileId.FromRowMajorIndex(0);
        var ammonia = runner.MutableWorld.GetResourceHandle(ResourceId.From(14));
        var hydrogenSulfide = runner.MutableWorld.GetResourceHandle(ResourceId.From(10));
        var phosphorus = runner.MutableWorld.GetResourceHandle(ResourceId.From(16));
        var spentCarrier = runner.MutableWorld.GetResourceHandle(ResourceId.From(8));
        var organicOxygen = runner.MutableWorld.GetResourceHandle(ResourceId.From(17));
        var beforePhosphorus = runner.MutableWorld.GetTileResource(tileId, phosphorus);

        var result = runner.AdvanceOneTick();

        var transactions = result.Changes.Phases.Single(phase =>
            phase.Phase == TickPhase.InternalMetabolism).ResourceTransactions
            .Where(transaction => transaction.Cause is
                LedgerCause.MandatoryMaintenance or LedgerCause.BiomassAssembly)
            .ToImmutableArray();
        Assert.Collection(
            transactions,
            maintenance =>
            {
                Assert.Equal(LedgerCause.MandatoryMaintenance, maintenance.Cause);
                Assert.Equal(56, maintenance.Extent);
            },
            assembly =>
            {
                Assert.Equal(LedgerCause.BiomassAssembly, assembly.Cause);
                Assert.Equal(3, assembly.Extent);
                Assert.Equal(255,
                    FounderMetabolismTransactionFactory.StagingLoadPerExtent(
                        rules.RulePack,
                        new ReactionHandle(assembly.ReactionId, 4)) * assembly.Extent);
            });
        Assert.True(ResourceLedgerOracle.Reconcile(rules.RulePack, transactions).IsBalanced);
        Assert.Equal(9_999_938, runner.MutableWorld.GetTileResource(tileId, ammonia));
        Assert.Equal(19_999_497,
            runner.MutableWorld.GetTileResource(tileId, hydrogenSulfide));
        Assert.Equal(beforePhosphorus - 6,
            runner.MutableWorld.GetTileResource(tileId, phosphorus));
        Assert.Equal(56, runner.MutableWorld.GetTileResource(tileId, spentCarrier));
        Assert.Equal(42, runner.MutableWorld.GetTileResource(tileId, organicOxygen));
    }

    [Fact]
    public void MissingAssemblyNutrientSuppressesGrowthButNotMandatoryMaintenance()
    {
        var runner = CreateSandbox(CompileWorld(), FounderGenomeId.From(1));
        var tileId = TileId.FromRowMajorIndex(0);
        var phosphorus = runner.MutableWorld.GetResourceHandle(ResourceId.From(16));
        var changes = runner.MutableWorld.BeginChanges();
        runner.MutableWorld.ApplyTileResourceDelta(
            tileId,
            phosphorus,
            -runner.MutableWorld.GetTileResource(tileId, phosphorus),
            changes);
        runner.MutableWorld.SealChanges(changes);

        var result = runner.AdvanceOneTick();

        var organism = runner.MutableWorld.GetOrganism(
            Assert.Single(runner.MutableWorld.GetOrganismIdsInCanonicalOrder()));
        Assert.Equal(1_000, organism.StructuralMatterQ);
        Assert.Equal(5_344, organism.ChargedReserveQ);
        var internalTransactions = result.Changes.Phases.Single(phase =>
            phase.Phase == TickPhase.InternalMetabolism).ResourceTransactions;
        Assert.Single(internalTransactions, transaction =>
            transaction.Cause == LedgerCause.MandatoryMaintenance);
        Assert.DoesNotContain(internalTransactions, transaction =>
            transaction.Cause == LedgerCause.BiomassAssembly);
        var gate = Assert.Single(
            Assert.Single(runner.CapturePublicationSnapshot().Organisms).ActionGateEvidence,
            value => value.Process == PublicationOrganismActionProcessKind.BiomassGrowth);
        Assert.Equal(PublicationOrganismActionGateReason.ResourceSupply, gate.Reason);
        Assert.Equal(phosphorus.Id, gate.ResourceId);
        Assert.Equal(0, gate.AvailableQ);
        Assert.Equal(2, gate.RequiredQ);
    }

    [Fact]
    public void ConservationSuppressesOptionalGrowthAndPublishesItsGate()
    {
        var runner = CreateSandbox(CompileWorld(), FounderGenomeId.From(1));
        var world = runner.MutableWorld;
        var organismId = Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        var organism = world.GetOrganism(organismId);
        var setup = world.BeginChanges();
        world.SetOrganismBehavior(
            organismId,
            organism.Behavior with { BehaviorId = OrganismBehaviorId.Conserving },
            setup);
        world.SealChanges(setup);

        var result = runner.AdvanceOneTick();

        Assert.DoesNotContain(
            result.Changes.Phases.Single(phase => phase.Phase == TickPhase.InternalMetabolism)
                .ResourceTransactions,
            transaction => transaction.Cause == LedgerCause.BiomassAssembly);
        var gates = Assert.Single(runner.CapturePublicationSnapshot().Organisms)
            .ActionGateEvidence;
        Assert.Contains(gates, value =>
            value.Process == PublicationOrganismActionProcessKind.BiomassGrowth &&
            value.Reason == PublicationOrganismActionGateReason.BehaviorSuppressed);
        Assert.Contains(gates, value =>
            value.Process == PublicationOrganismActionProcessKind.Reproduction &&
            value.Reason == PublicationOrganismActionGateReason.BehaviorSuppressed);
    }

    [Fact]
    public void UnpayableMaintenanceConsumesTheRemainingReserveAndDiesInMetabolism()
    {
        var runner = CreateSandbox(CompileWorld(), FounderGenomeId.From(2));
        for (var tick = 0; tick < 12; tick++)
        {
            runner.AdvanceOneTick();
        }
        var organismId = Assert.Single(runner.MutableWorld.GetOrganismIdsInCanonicalOrder());
        var organism = runner.MutableWorld.GetOrganism(organismId);
        var changes = runner.MutableWorld.BeginChanges();
        runner.MutableWorld.AdjustOrganismChargedReserve(
            organismId,
            10 - organism.ChargedReserveQ,
            changes);
        runner.MutableWorld.SealChanges(changes);

        var result = runner.AdvanceOneTick();

        Assert.Equal(0, runner.MutableWorld.OrganismCount);
        var death = Assert.Single(result.DeathRecords);
        Assert.Equal(TickPhase.InternalMetabolism, death.TerminalPhase);
        Assert.Equal(IntrinsicDeathCause.MaintenanceFailure, death.ActualCause);
        var evidence = Assert.Single(death.NonzeroCauseProbabilities);
        Assert.Equal(RatioQ.Scale, evidence.ProbabilityQ);
        Assert.Equal(10, evidence.ObservedValue);
        Assert.Equal(56, evidence.BoundaryValue);
        Assert.Equal(0, runner.MutableWorld.GetRemnant(death.RemnantId).ChargedReserveQ);
    }

    [Fact]
    public void SulfidePhototrophyStopsAtHourTwelveAndResumesNextDay()
    {
        var runner = CreateSandbox(CompileWorld(), FounderGenomeId.From(2));
        for (var tick = 0; tick < 12; tick++)
        {
            runner.AdvanceOneTick();
        }

        var organismId = Assert.Single(runner.MutableWorld.GetOrganismIdsInCanonicalOrder());
        var setup = runner.MutableWorld.BeginChanges();
        runner.MutableWorld.AdjustOrganismChargedReserve(organismId, -5_000, setup);
        runner.MutableWorld.SealChanges(setup);

        for (var tick = 13; tick <= 24; tick++)
        {
            Assert.Empty(CaptureTransactions(runner.AdvanceOneTick()));
            var gate = Assert.Single(Assert.Single(
                runner.CapturePublicationSnapshot().Organisms).AcquisitionGateEvidence);
            Assert.Equal(
                PublicationAcquisitionProcessKind.ExternalEnergyCapture,
                gate.Process);
            Assert.Equal(PublicationAcquisitionGateReason.InaccessibleLight, gate.Reason);
            Assert.Equal(0, gate.AvailableQ);
            Assert.Equal(0, gate.RequiredQ);
        }
        Assert.Equal(3_472, runner.MutableWorld.GetOrganism(organismId).ChargedReserveQ);

        var nextDawn = runner.AdvanceOneTick();
        Assert.Equal(950, Assert.Single(CaptureTransactions(nextDawn)).Extent);
        Assert.Equal(4_066, runner.MutableWorld.GetOrganism(organismId).ChargedReserveQ);
    }

    [Fact]
    public void GeneratedSulfurStartUsesActualHourlyDepthAdjustedLight()
    {
        var rules = CompileGeneratedWorld();
        var generated = DeterministicWorldGenerator.Generate(rules.WorldProfile, Seed);
        var sulfurTile = generated.StartingPairs[0].SulfurTileIndex;
        var runner = WorldRunner.CreateGame(
            WorldId.From(31),
            rules,
            Seed,
            new GameSetupCommand(
                GameMode.FreeSandbox,
                FounderGenomeId.From(2),
                TileId.FromRowMajorIndex(sulfurTile),
                1));

        Assert.Equal(544, runner.Rules.WorldProfile.Tiles.Length);
        Assert.NotNull(runner.Rules.GeneratedWorld);
        var dailyExtent = 0L;
        for (var hour = 0; hour < 24; hour++)
        {
            var organismId = Assert.Single(runner.MutableWorld.GetOrganismIdsInCanonicalOrder());
            var organism = runner.MutableWorld.GetOrganism(organismId);
            var changes = runner.MutableWorld.BeginChanges();
            runner.MutableWorld.AdjustOrganismChargedReserve(
                organismId,
                1_000 - organism.ChargedReserveQ,
                changes);
            runner.MutableWorld.SealChanges(changes);
            dailyExtent += CaptureTransactions(runner.AdvanceOneTick()).Sum(value => value.Extent);
        }

        var tile = generated.GetTile(sulfurTile);
        var expected = Enumerable.Range(0, 24).Sum(hour => checked((long)(
            (UInt128)2_083 * 950_000 *
            WorldClimateEvaluator.Evaluate(generated, tile, (ulong)hour).AccessibleLightQ /
            1_000_000 / 1_000_000)));
        Assert.Equal(expected, dailyExtent);
        Assert.InRange(dailyExtent, 11_000, 11_394);
    }

    [Fact]
    public void GeneratedWorldRestoresTheSameRealizationAndNextTick()
    {
        var rules = CompileGeneratedWorld();
        var generated = DeterministicWorldGenerator.Generate(rules.WorldProfile, Seed);
        var sulfurTile = generated.StartingPairs[0].SulfurTileIndex;
        var runner = WorldRunner.CreateGame(
            WorldId.From(32),
            rules,
            Seed,
            new GameSetupCommand(
                GameMode.FreeSandbox,
                FounderGenomeId.From(2),
                TileId.FromRowMajorIndex(sulfurTile),
                1));
        runner.AdvanceOneTick();

        var restored = WorldRunner.Restore(rules, runner.CapturePersistenceSnapshot());

        Assert.Equal(runner.CaptureSnapshot(), restored.CaptureSnapshot());
        Assert.Equal(
            runner.AdvanceOneTick().Snapshot,
            restored.AdvanceOneTick().Snapshot);
    }

    private static ImmutableArray<ResourceTransaction> CaptureTransactions(TickResult result) =>
        result.Changes.Phases.Single(phase => phase.Phase == TickPhase.ExternalResolution)
            .ResourceTransactions;

    private static WorldRunner CreateSandbox(
        CompiledWorldRules rules,
        FounderGenomeId founder,
        FounderAllocationId? allocation = null) => WorldRunner.CreateGame(
            WorldId.From(founder.Value),
            rules,
            Seed,
            new GameSetupCommand(
                GameMode.FreeSandbox,
                founder,
                TileId.FromRowMajorIndex(0),
                1,
                PlayerFounderAllocationId: allocation));

    private static CompiledWorldRules CompileWorld()
    {
        var result = WorldRulesPipeline.Compile(
            new CopiedPackageSource("OfficialRules"),
            new CopiedPackageSource("OfficialWorld"),
            "scenario.foundation-sandbox",
            "world.primordial-foundation");
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine,
            result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.IsType<CompiledWorldRules>(result.WorldRules);
    }

    private static CompiledWorldRules CompileGeneratedWorld()
    {
        var result = WorldRulesPipeline.Compile(
            new CopiedPackageSource("OfficialRules"),
            new CopiedPackageSource("OfficialGeneratedWorld"),
            "scenario.foundation-sandbox",
            "world.primordial-earth-v1");
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine,
            result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.IsType<CompiledWorldRules>(result.WorldRules);
    }

    private sealed class CopiedPackageSource(string directoryName) : IContentSource
    {
        private readonly string root = Path.Combine(AppContext.BaseDirectory, directoryName);

        public bool TryRead(string normalizedRelativePath, out ReadOnlyMemory<byte> content)
        {
            var path = Path.Combine(
                root,
                normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                content = default;
                return false;
            }

            content = File.ReadAllBytes(path);
            return true;
        }
    }
}

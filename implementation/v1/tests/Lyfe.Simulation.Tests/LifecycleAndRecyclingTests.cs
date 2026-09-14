using System.Collections.Immutable;
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
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.Ticks;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class LifecycleAndRecyclingTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("0123456789abcdeffedcba9876543210");

    [Fact]
    public void EligiblePrimitiveFissionIsDeterministicMassBalancedAndDefersNewbornAction()
    {
        var runner = CreateRunner();
        var world = runner.MutableWorld;
        var parentId = Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        var parentBefore = world.GetOrganism(parentId);
        var spentCarrier = world.GetResourceHandle(ResourceId.From(8));
        var tileBefore = world.GetTileResource(parentBefore.TileId, spentCarrier);
        var setup = world.BeginChanges();
        world.AdjustOrganismStructure(parentId, 1_000, setup);
        world.AdjustOrganismChargedReserve(parentId, 3_500, setup);
        world.SetOrganismMicronutrients(
            parentId,
            parentBefore.CommittedMicronutrients,
            parentBefore.CommittedMicronutrients,
            setup);
        world.SetOrganismLifecycleSchedule(parentId, 0, 0, 0, setup);
        world.SealChanges(setup);

        var result = runner.AdvanceOneTick();

        var organisms = world.GetOrganismIdsInCanonicalOrder()
            .Select(world.GetOrganism)
            .ToArray();
        Assert.Equal(2, organisms.Length);
        var parent = Assert.Single(organisms, organism => organism.Id == parentId);
        var offspring = Assert.Single(organisms, organism => organism.Id != parentId);
        Assert.Equal(1UL, parent.BiologicalAgeHours);
        Assert.Equal(0UL, offspring.BiologicalAgeHours);
        Assert.Equal(result.Snapshot.CompletedTick, offspring.BirthTick);
        Assert.Equal(2_003, parent.StructuralMatterQ + offspring.StructuralMatterQ);
        Assert.Equal(8_044, parent.ChargedReserveQ + offspring.ChargedReserveQ);
        Assert.Equal(tileBefore + 556, world.GetTileResource(parent.TileId, spentCarrier));
        Assert.Equal(1UL, parent.SuccessfulReproductionCount);
        Assert.Equal(parentBefore.CommittedMicronutrients, parent.CommittedMicronutrients);
        Assert.Equal(parentBefore.CommittedMicronutrients, offspring.CommittedMicronutrients);
        Assert.Equal(MicronutrientInventory.Empty, parent.FreeMicronutrients);
        Assert.Equal(MicronutrientInventory.Empty, offspring.FreeMicronutrients);
        Assert.InRange(parent.ReproductionNotBeforeTick, 25UL, 28UL);
        Assert.InRange(offspring.ReproductionNotBeforeTick, 25UL, 28UL);
        Assert.NotEqual((parent.PositionXQ, parent.PositionYQ),
            (offspring.PositionXQ, offspring.PositionYQ));
        Assert.Contains(
            result.Changes.MergedChanges.StoreChanges,
            store => store.EntityKind == State.Changes.StateEntityKind.Organism &&
                store.Creates.Any(created => created.Value == offspring.Id.Value));
        var journey = runner.CapturePublicationSnapshot().JourneyEvents;
        var reproduction = Assert.Single(journey.Where(value =>
            value.Family == OrganismJourneyEventFamily.Reproduction));
        Assert.Equal(parent.Id, reproduction.SubjectOrganismId);
        Assert.Equal(offspring.Id, reproduction.RelatedOrganismId);
        var birth = Assert.Single(journey.Where(value =>
            value.Family == OrganismJourneyEventFamily.Birth &&
            value.SubjectOrganismId == offspring.Id));
        Assert.Equal(parent.Id, birth.RelatedOrganismId);
        Assert.True(birth.EventId > reproduction.EventId);

        runner.AdvanceOneTick();

        foreach (var current in runner.CapturePublicationSnapshot().Organisms)
        {
            var gate = Assert.Single(
                current.ActionGateEvidence,
                value => value.Process ==
                    PublicationOrganismActionProcessKind.Reproduction);
            Assert.Equal(PublicationOrganismActionGateReason.CooldownActive, gate.Reason);
            Assert.Equal(world.GetOrganism(current.OrganismId).ReproductionNotBeforeTick,
                gate.ClearsAtTick);
        }
    }

    [Fact]
    public void ReproductionCooldownHoursAreRoundedUpToConfiguredTickDuration()
    {
        var runner = CreateRunner(tickDurationHours: 6);
        var world = runner.MutableWorld;
        var parentId = Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        var parentBefore = world.GetOrganism(parentId);
        var setup = world.BeginChanges();
        world.AdjustOrganismStructure(parentId, 1_000, setup);
        world.AdjustOrganismChargedReserve(parentId, 3_500, setup);
        world.SetOrganismMicronutrients(
            parentId,
            parentBefore.CommittedMicronutrients,
            parentBefore.CommittedMicronutrients,
            setup);
        world.SetOrganismLifecycleSchedule(parentId, 0, 0, 0, setup);
        world.SealChanges(setup);

        runner.AdvanceOneTick();

        var organisms = world.GetOrganismIdsInCanonicalOrder()
            .Select(world.GetOrganism)
            .ToArray();
        Assert.Equal(2, organisms.Length);
        Assert.All(organisms, organism =>
            Assert.InRange(organism.ReproductionNotBeforeTick, 5UL, 6UL));
    }

    [Fact]
    public void IntrinsicDeathRecordsEveryPositiveRiskAndMovesMatterOnceToARemnant()
    {
        var runner = CreateRunner();
        var world = runner.MutableWorld;
        var organismId = Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        var setup = world.BeginChanges();
        world.AdjustOrganismStructure(organismId, -600, setup);
        world.AdjustOrganismChargedReserve(organismId, -5_000, setup);
        world.SealChanges(setup);

        var result = runner.AdvanceOneTick();

        Assert.Equal(0, world.OrganismCount);
        var death = Assert.Single(result.DeathRecords);
        Assert.Equal(result.DeathRecords, runner.LastDeathRecords);
        Assert.Equal(IntrinsicDeathCause.ReserveExhaustion, death.ActualCause);
        Assert.Equal(
            [IntrinsicDeathCause.ReserveExhaustion, IntrinsicDeathCause.StructuralFailure],
            death.NonzeroCauseProbabilities.Select(cause => cause.Cause));
        Assert.All(death.NonzeroCauseProbabilities, cause =>
        {
            Assert.Equal(RatioQ.Scale, cause.ProbabilityQ);
            Assert.True(cause.Triggered);
        });
        var remnant = world.GetRemnant(death.RemnantId);
        Assert.Equal(
            world.GetCompiledPhenotype(remnant.SourceSpeciesId).Physiology
                .CommittedMicronutrientQuotas.Sum(quota => quota.Quantity),
            remnant.Micronutrients.TotalLoadQ);
        Assert.Equal(400, remnant.StructuralMatterQ);
        Assert.Equal(0, remnant.ChargedReserveQ);
        Assert.Equal(organismId, remnant.SourceOrganismId);

        var journeyDeath = Assert.Single(runner.CapturePublicationSnapshot().JourneyEvents.Where(
            value => value.Family == OrganismJourneyEventFamily.Death));
        Assert.Equal(death.RemnantId, journeyDeath.RelatedRemnantId);
        Assert.Equal((uint)IntrinsicDeathCause.ReserveExhaustion, journeyDeath.DetailId);
        Assert.Equal(
            death.NonzeroCauseProbabilities.Select(value => value.ProbabilityQ),
            journeyDeath.DeathCauseProbabilities.Select(value => value.ProbabilityQ));
        var publication = runner.CapturePublicationSnapshot();
        var deathMechanism = Assert.Single(publication.NotableEvents.Where(value =>
            value.Family == NotableEventFamily.RealizedDeathMechanism));
        Assert.Equal(journeyDeath.EventId, deathMechanism.SourceEventId);
        Assert.Equal(journeyDeath.SubjectSpeciesId, deathMechanism.SpeciesId);
        Assert.Equal(journeyDeath.TileId, deathMechanism.TileId);
        Assert.Equal((ulong)IntrinsicDeathCause.ReserveExhaustion,
            deathMechanism.MilestoneValue);
        Assert.Contains(publication.AttentionAlerts, alert =>
            alert.AlertClass == AttentionAlertClass.Critical &&
            alert.EventFamily == NotableEventFamily.RealizedDeathMechanism &&
            alert.ChronicleEventIds.SequenceEqual([deathMechanism.EventId]));

        var persisted = runner.CapturePersistenceSnapshot();
        var restored = WorldRunner.Restore(runner.Rules, persisted);
        Assert.Equal(runner.CaptureSnapshot(), restored.CaptureSnapshot());
        var restoredPublication = restored.CapturePublicationSnapshot();
        Assert.Single(restoredPublication.Remnants);
        var restoredDeath = Assert.Single(restoredPublication.JourneyEvents.Where(
            value => value.Family == OrganismJourneyEventFamily.Death));
        Assert.Equal(journeyDeath with
        {
            DeathCauseProbabilities = restoredDeath.DeathCauseProbabilities,
        }, restoredDeath);
        Assert.Equal(journeyDeath.DeathCauseProbabilities,
            restoredDeath.DeathCauseProbabilities);
        Assert.Equal(publication.NotableEvents, restoredPublication.NotableEvents);
        Assert.Equal(publication.AttentionAlerts, restoredPublication.AttentionAlerts);
    }

    [Fact]
    public void OldRemnantsDecayIntoExactPhosphorusReleasingProducts()
    {
        var runner = CreateRunner();
        var world = runner.MutableWorld;
        var source = world.GetOrganism(Assert.Single(world.GetOrganismIdsInCanonicalOrder()));
        var setup = world.BeginChanges();
        var remnantId = world.CreateRemnant(
            new RemnantInitialState(
                source.Id,
                source.SpeciesId,
                0,
                source.TileId,
                source.PositionXQ,
                source.PositionYQ,
                2_000,
                2_000,
                0,
                0),
            setup);
        world.SealChanges(setup);

        var phosphorus = world.GetResourceHandle(ResourceId.From(16));
        var phosphorusBefore = world.GetTileResource(source.TileId, phosphorus);
        var phase = new RemnantDecayPhase();
        var journal = phase.Execute(world, new TickExecutionContext(
            1,
            1,
            1,
            runner.Rules.Identity.WorldRulesHash,
            NoTickFaultInjector.Instance,
            new SemanticRandomOracle(Seed),
            new TickScratch(1)));

        var remnant = world.GetRemnant(remnantId);
        Assert.Equal(1_999, remnant.StructuralMatterQ);
        Assert.Equal(1_996, remnant.ChargedReserveQ);
        Assert.Equal(924_000U, remnant.StructureDecayRemainderQ);
        Assert.Equal(122_000U, remnant.ReserveDecayRemainderQ);
        Assert.Equal(4, world.GetTileResource(source.TileId,
            world.GetResourceHandle(ResourceId.From(3))));
        Assert.Equal(1, world.GetTileResource(source.TileId,
            world.GetResourceHandle(ResourceId.From(32))));
        Assert.Equal(phosphorusBefore + 2,
            world.GetTileResource(source.TileId, phosphorus));
        Assert.Equal(4, world.GetTileResource(source.TileId,
            world.GetResourceHandle(ResourceId.From(8))));
        Assert.Contains(journal.ResourceTransactions, value =>
            value.Cause == LedgerCause.RemnantDecay &&
            value.Key.ActorId == remnantId.Value &&
            value.ReactionId.Value == 5 &&
            value.Extent == 1);
        Assert.True(ResourceLedgerOracle.Reconcile(
            runner.Rules.RulePack,
            journal.ResourceTransactions).IsBalanced);
    }

    [Fact]
    public void ExhaustedRemnantIsRemovedAndReturnsTerminalMicronutrients()
    {
        var runner = CreateRunner();
        var world = runner.MutableWorld;
        var source = world.GetOrganism(Assert.Single(world.GetOrganismIdsInCanonicalOrder()));
        var calcium = world.GetResourceHandle(ResourceId.From(18));
        var phosphorus = world.GetResourceHandle(ResourceId.From(16));
        var depletedResidue = world.GetResourceHandle(ResourceId.From(32));
        var spentReserve = world.GetResourceHandle(ResourceId.From(8));
        var calciumBefore = world.GetTileResource(source.TileId, calcium);
        var phosphorusBefore = world.GetTileResource(source.TileId, phosphorus);
        var depletedBefore = world.GetTileResource(source.TileId, depletedResidue);
        var spentBefore = world.GetTileResource(source.TileId, spentReserve);
        var setup = world.BeginChanges();
        var remnantId = world.CreateRemnant(
            new RemnantInitialState(
                source.Id,
                source.SpeciesId,
                0,
                source.TileId,
                source.PositionXQ,
                source.PositionYQ,
                1,
                1,
                0,
                0,
                MicronutrientInventory.Empty.With(0, 7)),
            setup);
        world.SealChanges(setup);

        var phase = new RemnantDecayPhase();
        var random = new SemanticRandomOracle(Seed);
        TickPhaseJournal finalJournal = default!;
        ulong removedAtTick = 0;
        for (ulong tick = 1; tick <= 1_100; tick++)
        {
            finalJournal = phase.Execute(world, new TickExecutionContext(
                tick,
                tick,
                1,
                runner.Rules.Identity.WorldRulesHash,
                NoTickFaultInjector.Instance,
                random,
                new TickScratch(tick)));
            Assert.True(ResourceLedgerOracle.Reconcile(
                runner.Rules.RulePack,
                finalJournal.ResourceTransactions).IsBalanced);
            if (!world.GetRemnantIdsInCanonicalOrder().Contains(remnantId))
            {
                removedAtTick = tick;
                break;
            }
        }

        Assert.InRange(removedAtTick, 1UL, 1_100UL);
        Assert.Equal(calciumBefore + 7, world.GetTileResource(source.TileId, calcium));
        Assert.Equal(phosphorusBefore + 2, world.GetTileResource(source.TileId, phosphorus));
        Assert.Equal(depletedBefore + 1,
            world.GetTileResource(source.TileId, depletedResidue));
        Assert.Equal(spentBefore + 1, world.GetTileResource(source.TileId, spentReserve));
        Assert.Contains(finalJournal.ResourceTransactions, value =>
            value.Cause == LedgerCause.RemnantDecay &&
            value.ReactionId.Value == calcium.Id.Value &&
            value.Extent == 7);
        Assert.Contains(finalJournal.Changes.Removes,
            value => value.Value == remnantId.Value);
    }

    [Fact]
    public void ParticulateScavengingIsRangeLocalCooldownBoundAndDigestsByExactRecipe()
    {
        var runner = CreateRunner(enableParticulateScavenging: true);
        var world = runner.MutableWorld;
        var organismId = Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        var organism = world.GetOrganism(organismId);
        var setup = world.BeginChanges();
        var remnantId = world.CreateRemnant(
            new RemnantInitialState(
                organism.Id,
                organism.SpeciesId,
                1,
                organism.TileId,
                organism.PositionXQ,
                organism.PositionYQ,
                100,
                1_000,
                0,
                0),
            setup);
        world.SealChanges(setup);

        var result = runner.AdvanceOneTick();

        organism = world.GetOrganism(organismId);
        var remnant = world.GetRemnant(remnantId);
        Assert.Equal(40, organism.IngestedStructuralMatterQ);
        Assert.Equal(5_514, organism.ChargedReserveQ);
        Assert.Equal(3UL, organism.ScavengeNotBeforeTick);
        Assert.Equal(50, remnant.StructuralMatterQ);
        Assert.Equal(800, remnant.ChargedReserveQ);
        Assert.Equal(10, world.GetTileResource(
            organism.TileId,
            world.GetResourceHandle(ResourceId.From(6))));
        var digestion = Assert.Single(
            result.Changes.Phases.Single(phase =>
                    phase.Phase == TickPhase.InternalMetabolism).ResourceTransactions,
            transaction => transaction.Cause == LedgerCause.ParticulateDigestion);
        Assert.Equal(LedgerCause.ParticulateDigestion, digestion.Cause);
        Assert.Equal(10, digestion.Extent);
        Assert.True(ResourceLedgerOracle.Reconcile(
            runner.Rules.RulePack,
            [digestion]).IsBalanced);

        var cooldownSetup = world.BeginChanges();
        world.CreateRemnant(
            new RemnantInitialState(
                organism.Id,
                organism.SpeciesId,
                2,
                organism.TileId,
                organism.PositionXQ,
                organism.PositionYQ,
                1_000,
                1_000,
                0,
                0),
            cooldownSetup);
        world.SealChanges(cooldownSetup);
        runner.AdvanceOneTick();

        var gate = Assert.Single(
            Assert.Single(runner.CapturePublicationSnapshot().Organisms)
                .AcquisitionGateEvidence,
            value => value.Process == PublicationAcquisitionProcessKind.Scavenging);
        Assert.Equal(PublicationAcquisitionGateReason.CooldownActive, gate.Reason);
        Assert.Equal(0, gate.AvailableQ);
        Assert.Equal(0, gate.RequiredQ);
        Assert.Equal(3UL, gate.ClearsAtTick);
    }

    [Fact]
    public void RemnantsOutsideLocalInteractionRangeCannotBeScavenged()
    {
        var runner = CreateRunner(enableParticulateScavenging: true);
        var world = runner.MutableWorld;
        var organismId = Assert.Single(world.GetOrganismIdsInCanonicalOrder());
        var organism = world.GetOrganism(organismId);
        var farX = organism.PositionXQ < uint.MaxValue / 2 ? uint.MaxValue : 0U;
        var setup = world.BeginChanges();
        var remnantId = world.CreateRemnant(
            new RemnantInitialState(
                organism.Id,
                organism.SpeciesId,
                1,
                organism.TileId,
                farX,
                organism.PositionYQ,
                100,
                1_000,
                0,
                0),
            setup);
        world.SealChanges(setup);

        runner.AdvanceOneTick();

        organism = world.GetOrganism(organismId);
        var remnant = world.GetRemnant(remnantId);
        Assert.Equal(0, organism.IngestedStructuralMatterQ);
        Assert.Equal(0UL, organism.ScavengeNotBeforeTick);
        Assert.Equal(100, remnant.StructuralMatterQ);
        Assert.Equal(1_000, remnant.ChargedReserveQ);
        Assert.DoesNotContain(
            Assert.Single(runner.CapturePublicationSnapshot().Organisms)
                .AcquisitionGateEvidence,
            value => value.Process == PublicationAcquisitionProcessKind.Scavenging);
    }

    [Fact]
    public void LocalRemnantPublishesMissingScavengingCapabilityGate()
    {
        var runner = CreateRunner();
        var world = runner.MutableWorld;
        var organism = world.GetOrganism(Assert.Single(world.GetOrganismIdsInCanonicalOrder()));
        var setup = world.BeginChanges();
        world.CreateRemnant(
            new RemnantInitialState(
                organism.Id,
                organism.SpeciesId,
                1,
                organism.TileId,
                organism.PositionXQ,
                organism.PositionYQ,
                100,
                1_000,
                0,
                0),
            setup);
        world.SealChanges(setup);

        runner.AdvanceOneTick();

        var gate = Assert.Single(
            Assert.Single(runner.CapturePublicationSnapshot().Organisms)
                .AcquisitionGateEvidence,
            value => value.Process == PublicationAcquisitionProcessKind.Scavenging);
        Assert.Equal(PublicationAcquisitionGateReason.MissingCapability, gate.Reason);
    }

    [Fact]
    public void LocalRemnantPublishesScavengingEnergyAndDigestionCapacityGates()
    {
        var energyRunner = CreateRunner(
            enableParticulateScavenging: true,
            scavengeActionCostQ: 6_000,
            scavengeRangeQ: uint.MaxValue);
        var energyWorld = energyRunner.MutableWorld;
        var energyOrganism = energyWorld.GetOrganism(
            Assert.Single(energyWorld.GetOrganismIdsInCanonicalOrder()));
        var energySetup = energyWorld.BeginChanges();
        energyWorld.CreateRemnant(
            new RemnantInitialState(
                energyOrganism.Id,
                energyOrganism.SpeciesId,
                1,
                energyOrganism.TileId,
                energyOrganism.PositionXQ,
                energyOrganism.PositionYQ,
                0,
                1_000,
                0,
                0),
            energySetup);
        energyWorld.SealChanges(energySetup);

        energyRunner.AdvanceOneTick();

        var energyGate = Assert.Single(
            Assert.Single(energyRunner.CapturePublicationSnapshot().Organisms)
                .AcquisitionGateEvidence,
            value => value.Process == PublicationAcquisitionProcessKind.Scavenging);
        Assert.Equal(PublicationAcquisitionGateReason.InsufficientActionEnergy,
            energyGate.Reason);
        Assert.Equal(5_000, energyGate.AvailableQ);
        Assert.Equal(6_000, energyGate.RequiredQ);

        var capacityRunner = CreateRunner(
            enableParticulateScavenging: true,
            scavengeRangeQ: uint.MaxValue);
        var capacityWorld = capacityRunner.MutableWorld;
        var capacityOrganism = capacityWorld.GetOrganism(
            Assert.Single(capacityWorld.GetOrganismIdsInCanonicalOrder()));
        var capacitySetup = capacityWorld.BeginChanges();
        capacityWorld.AdjustOrganismIngestedStructuralMatter(
            capacityOrganism.Id,
            250,
            capacitySetup);
        capacityWorld.CreateRemnant(
            new RemnantInitialState(
                capacityOrganism.Id,
                capacityOrganism.SpeciesId,
                1,
                capacityOrganism.TileId,
                capacityOrganism.PositionXQ,
                capacityOrganism.PositionYQ,
                1_000,
                0,
                0,
                0),
            capacitySetup);
        capacityWorld.SealChanges(capacitySetup);

        capacityRunner.AdvanceOneTick();

        var capacityGate = Assert.Single(
            Assert.Single(capacityRunner.CapturePublicationSnapshot().Organisms)
                .AcquisitionGateEvidence,
            value => value.Process == PublicationAcquisitionProcessKind.Scavenging);
        Assert.Equal(PublicationAcquisitionGateReason.InternalCapacity,
            capacityGate.Reason);
        Assert.Equal(0, capacityGate.AvailableQ);
        Assert.Equal(1, capacityGate.RequiredQ);
    }

    private static WorldRunner CreateRunner(
        bool enableParticulateScavenging = false,
        long? scavengeActionCostQ = null,
        uint? scavengeRangeQ = null,
        uint tickDurationHours = 1)
    {
        var rulesLoad = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(rulesLoad.IsSuccess, Format(rulesLoad.Diagnostics));
        var authored = Assert.IsType<AuthoringRulePack>(rulesLoad.Pack);
        if (enableParticulateScavenging)
        {
            var founder = authored.FounderGenomes.Single(candidate => candidate.NumericId == 1);
            authored = authored with
            {
                FounderGenomes = authored.FounderGenomes
                    .Select(candidate => candidate.NumericId == founder.NumericId
                        ? founder with
                        {
                            EnabledReactionKeys =
                                [.. founder.EnabledReactionKeys,
                                    "reaction.particulate-biomass-digestion"],
                            Physiology = founder.Physiology with
                            {
                                IngestedMatterCapacityLoadQ = 250,
                                Recycling = founder.Physiology.Recycling with
                                {
                                    SimpleRemnantScavenging = true,
                                    ScavengeActionCostQ = scavengeActionCostQ ??
                                        founder.Physiology.Recycling.ScavengeActionCostQ,
                                    ParticulateScavengeActionCostQ = scavengeActionCostQ ??
                                        founder.Physiology.Recycling
                                            .ParticulateScavengeActionCostQ,
                                    ScavengeRangeQ = scavengeRangeQ ??
                                        founder.Physiology.Recycling.ScavengeRangeQ,
                                },
                            },
                        }
                        : candidate)
                    .ToArray(),
            };
        }

        authored = authored with
        {
            Scenarios = authored.Scenarios
                .Select(scenario => scenario with { TickDurationHours = tickDurationHours })
                .ToArray(),
        };

        var compiledRules = RulePackCompiler.Compile(authored);
        Assert.True(compiledRules.IsSuccess, Format(compiledRules.Diagnostics));
        var worldLoad = WorldPackSourceLoader.Load(new CopiedPackageSource("OfficialWorld"));
        Assert.True(worldLoad.IsSuccess, Format(worldLoad.Diagnostics));
        var compiledWorld = WorldRulesCompiler.Compile(
            Assert.IsType<CompiledRulePack>(compiledRules.RulePack),
            Assert.IsType<AuthoringWorldPack>(worldLoad.Pack),
            "scenario.foundation-sandbox",
            "world.primordial-foundation");
        Assert.True(compiledWorld.IsSuccess, Format(compiledWorld.Diagnostics));
        return WorldRunner.CreateFoundation(
            WorldId.From(99),
            Assert.IsType<CompiledWorldRules>(compiledWorld.WorldRules),
            Seed,
            1);
    }

    private static string Format(IEnumerable<RuleDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message));

    private sealed class CopiedPackageSource(string directoryName) : IContentSource
    {
        private readonly string root = Path.Combine(AppContext.BaseDirectory, directoryName);

        public bool TryRead(string normalizedRelativePath, out ReadOnlyMemory<byte> content)
        {
            var path = Path.Combine(root,
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

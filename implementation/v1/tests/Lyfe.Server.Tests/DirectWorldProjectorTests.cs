using System.Collections.Immutable;
using Lyfe.Server.Projection;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Server.Tests;

public sealed class DirectWorldProjectorTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("0123456789abcdeffedcba9876543210");

    [Fact]
    public void ProjectionUsesDistinctLiveReducedAndUnknownShapes()
    {
        var runner = CreateRunner();
        var source = runner.CapturePublicationSnapshot();
        var controlled = Assert.Single(source.Species).SpeciesId;
        var reducedSource = source.Tiles[1];
        var knownResources = reducedSource.ResourceStocks
            .Where(stock => stock.QuantityQ > 0)
            .Select(stock => stock.ResourceId)
            .Reverse()
            .ToImmutableArray();
        var knowledge = new ActorKnowledgeSnapshot(
            controlled,
            [new DiscoveredTileKnowledge(reducedSource.TileId, 0, knownResources)]);

        var projection = DirectWorldProjector.Project(source, knowledge);

        Assert.Equal(source.WorldId, projection.WorldId);
        Assert.Equal(source.CompletedTick, projection.CompletedTick);
        Assert.Equal(source.WorldRevision, projection.WorldRevision);
        Assert.Equal(source.WorldRulesHash, projection.WorldRulesHash);
        Assert.Equal(controlled, projection.ControlledSpeciesId);
        Assert.Equal(GameMode.FreeSandbox, projection.Gameplay.Mode);
        Assert.Equal(GameRunStatus.Active, projection.Gameplay.RunStatus);
        Assert.Equal(controlled, projection.Gameplay.ControlledSpeciesId);
        Assert.Equal(1UL, projection.Gameplay.GameplayRevision);
        Assert.True(Assert.Single(projection.Gameplay.Roots).PlayerSelected);
        Assert.Equal([0U, 1U, 2U, 3U], projection.Tiles.Select(tile => tile.TileId.Value));

        var live = Assert.IsType<LiveTileProjection>(projection.Tiles[0]);
        Assert.Equal(source.CompletedTick, live.ObservedAtTick);
        Assert.Equal(source.Tiles[0].ResourceStocks.Length, live.ResourceStocks.Length);
        Assert.Equal(100, live.Organisms.Length);
        Assert.Equal(source.ResourceFlowPeriodHours, live.ResourceFlowPeriodHours);
        Assert.Empty(live.ResourceFlows);
        Assert.Empty(live.ResourceFlowHistory);
        Assert.Equal(
            source.ResourceDefinitions.Select(value => value.DisplayName),
            projection.ResourceDefinitions.Select(value => value.DisplayName));
        Assert.All(live.Organisms, organism => Assert.Equal(controlled, organism.SpeciesId));
        Assert.All(live.Organisms, organism =>
        {
            Assert.Equal(10_000, organism.ChargedReserveCapacityQ);
            Assert.Equal(483_334U, organism.RelativeHealthQ);
            Assert.Equal(500_000U, organism.ReserveFactorQ);
            Assert.Equal(966_667U, organism.EnvironmentalFactorQ);
            Assert.Equal(4_194_304U, organism.BodyRadiusQ);
            Assert.Equal(OrganismBehaviorId.Baseline, organism.BehaviorId);
            Assert.Equal(500_000U, organism.ResourcePressureQ);
            Assert.Equal(6, organism.CommittedMicronutrients.Length);
            Assert.Equal(57, organism.CommittedMicronutrients.Sum(stock => stock.QuantityQ));
            Assert.Empty(organism.FreeMicronutrients);
        });
        Assert.Empty(live.Remnants);
        var tileBehavior = Assert.Single(live.BehaviorDistributions);
        Assert.Equal(controlled, tileBehavior.SpeciesId);
        Assert.Equal(100UL, tileBehavior.TotalObservedOrganisms);
        Assert.Equal(
            new BehaviorCountProjection(OrganismBehaviorId.Baseline, 100),
            Assert.Single(tileBehavior.Counts));

        var reduced = Assert.IsType<ReducedTileProjection>(projection.Tiles[1]);
        Assert.Equal(0UL, reduced.ObservedAtTick);
        Assert.Equal(
            knownResources.OrderBy(id => id.Value),
            reduced.KnownPresentResourceIds);
        Assert.IsType<UnknownTileProjection>(projection.Tiles[2]);
        Assert.IsType<UnknownTileProjection>(projection.Tiles[3]);

        var species = Assert.Single(projection.Species);
        Assert.Equal(controlled, species.SpeciesId);
        Assert.Equal(SpeciesPopulationScope.WorldExact, species.PopulationScope);
        Assert.Equal(100UL, species.Population);
        Assert.Equal(
            new BehaviorCountProjection(OrganismBehaviorId.Baseline, 100),
            Assert.Single(species.BehaviorCounts));
        Assert.DoesNotContain(
            typeof(ActorWorldProjection).GetProperties(),
            property => property.Name.Contains("StateHash", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(ReducedTileProjection).GetProperties(),
            property => property.Name is "ResourceStocks" or "Organisms");
        Assert.DoesNotContain(
            typeof(UnknownTileProjection).GetProperties(),
            property => property.Name is "ElevationMeters" or "ResourceStocks" or "Organisms");
    }

    [Fact]
    public void LiveResourceFlowsComeFromAppliedLedgerEntriesForThatTileOnly()
    {
        var runner = CreateRunner();
        runner.AdvanceOneTick();
        var source = runner.CapturePublicationSnapshot();
        var controlled = Assert.Single(source.Species).SpeciesId;

        var projection = DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(controlled, []));
        var live = Assert.IsType<LiveTileProjection>(projection.Tiles[0]);
        var expected = source.ResourceFlows
            .Where(flow => flow.TileId == live.TileId)
            .OrderBy(flow => flow.ResourceId.Value)
            .ThenBy(flow => flow.Kind)
            .Select(flow => new ResourceFlowProjection(
                flow.ResourceId,
                flow.Kind,
                flow.AmountQ));

        Assert.NotEmpty(live.ResourceFlows);
        Assert.Equal(expected, live.ResourceFlows);
        Assert.All(live.ResourceFlows, flow => Assert.True(flow.AmountQ > 0));
        var interval = Assert.Single(live.ResourceFlowHistory);
        Assert.Equal(source.CompletedTick, interval.CompletedTick);
        Assert.Equal(source.SimulatedHours, interval.EndSimulatedHour);
        Assert.Equal(expected, interval.ResourceFlows);
        Assert.NotEmpty(projection.ReactionDefinitions);
        Assert.NotEmpty(projection.AttentionAlerts);
        Assert.All(projection.AttentionAlerts, alert =>
        {
            Assert.Equal(controlled, alert.SpeciesId);
            Assert.All(alert.ChronicleEventIds, eventId =>
                Assert.Contains(projection.NotableEvents, value => value.EventId == eventId));
        });
        Assert.NotEmpty(live.ResourceFlowContributors);
        Assert.Equal(
            live.ResourceFlows.Select(flow => (flow.ResourceId, flow.Kind, flow.AmountQ)),
            live.ResourceFlowContributors
                .GroupBy(value => (value.ResourceId, value.Kind))
                .OrderBy(group => group.Key.ResourceId.Value)
                .ThenBy(group => group.Key.Kind)
                .Select(group => (
                    group.Key.ResourceId,
                    group.Key.Kind,
                    group.Sum(value => value.AmountQ))));
        Assert.All(
            live.ResourceFlowContributors.Where(value => value.SpeciesId.HasValue),
            value => Assert.Equal(controlled, value.SpeciesId));
        var sourceOrganism = source.Organisms[0];
        var projectedOrganism = live.Organisms.Single(value =>
            value.OrganismId == sourceOrganism.OrganismId);
        Assert.NotEmpty(projectedOrganism.ResourceAcquisitionEvidence);
        Assert.Equal(
            sourceOrganism.ResourceAcquisitionEvidence.Select(value =>
                new ResourceAcquisitionEvidenceProjection(
                    value.ResourceId,
                    value.RequestedQ,
                    value.GrantedQ,
                    value.TileSupplyConstrained,
                    value.ClaimContentionConstrained)),
            projectedOrganism.ResourceAcquisitionEvidence);
    }

    [Fact]
    public void LiveOrganismCarriesAuthoritativeAcquisitionGateEvidence()
    {
        var runner = CreateRunner();
        var source = runner.CapturePublicationSnapshot();
        var organismId = source.Organisms[0].OrganismId;
        source = source with
        {
            Organisms = source.Organisms.SetItem(
                0,
                source.Organisms[0] with
                {
                    AcquisitionGateEvidence =
                    [
                        new PublicationAcquisitionGateEvidence(
                            PublicationAcquisitionProcessKind.ExternalEnergyCapture,
                            PublicationAcquisitionGateReason.InternalCapacity,
                            0,
                            2,
                            0),
                        new PublicationAcquisitionGateEvidence(
                            PublicationAcquisitionProcessKind.Scavenging,
                            PublicationAcquisitionGateReason.CooldownActive,
                            0,
                            0,
                            source.CompletedTick + 1),
                    ],
                    ActionGateEvidence =
                    [
                        new PublicationOrganismActionGateEvidence(
                            PublicationOrganismActionProcessKind.Reproduction,
                            PublicationOrganismActionGateReason.CooldownActive,
                            0,
                            0,
                            null,
                            source.CompletedTick + 1),
                    ],
                }),
        };
        var controlled = Assert.Single(source.Species).SpeciesId;

        var projection = DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(controlled, []));
        var live = Assert.IsType<LiveTileProjection>(projection.Tiles[0]);
        var organism = live.Organisms.Single(value => value.OrganismId == organismId);
        Assert.Equal(2, organism.AcquisitionGateEvidence.Length);
        var gate = organism.AcquisitionGateEvidence[0];

        Assert.Equal(PublicationAcquisitionProcessKind.ExternalEnergyCapture, gate.Process);
        Assert.Equal(PublicationAcquisitionGateReason.InternalCapacity, gate.Reason);
        Assert.Equal(0, gate.AvailableQ);
        Assert.Equal(2, gate.RequiredQ);
        Assert.Equal(0UL, gate.ClearsAtTick);
        var cooldownGate = organism.AcquisitionGateEvidence[1];
        Assert.Equal(PublicationAcquisitionProcessKind.Scavenging, cooldownGate.Process);
        Assert.Equal(PublicationAcquisitionGateReason.CooldownActive, cooldownGate.Reason);
        Assert.Equal(source.CompletedTick + 1, cooldownGate.ClearsAtTick);
        var actionGate = Assert.Single(organism.ActionGateEvidence);
        Assert.Equal(PublicationOrganismActionProcessKind.Reproduction,
            actionGate.Process);
        Assert.Equal(PublicationOrganismActionGateReason.CooldownActive,
            actionGate.Reason);
        Assert.Equal(source.CompletedTick + 1, actionGate.ClearsAtTick);

        var invalid = source with
        {
            Organisms = source.Organisms.SetItem(
                0,
                source.Organisms[0] with
                {
                    AcquisitionGateEvidence =
                    [
                        new PublicationAcquisitionGateEvidence(
                            PublicationAcquisitionProcessKind.ExternalEnergyCapture,
                            PublicationAcquisitionGateReason.InternalCapacity,
                            2,
                            2,
                            0),
                    ],
                }),
        };
        Assert.Throws<ArgumentException>(() => DirectWorldProjector.Project(
            invalid,
            new ActorKnowledgeSnapshot(controlled, [])));

        var invalidPair = source with
        {
            Organisms = source.Organisms.SetItem(
                0,
                source.Organisms[0] with
                {
                    AcquisitionGateEvidence =
                    [
                        new PublicationAcquisitionGateEvidence(
                            PublicationAcquisitionProcessKind.ExternalEnergyCapture,
                            PublicationAcquisitionGateReason.CooldownActive,
                            0,
                            0,
                            source.CompletedTick + 1),
                    ],
                }),
        };
        Assert.Throws<ArgumentException>(() => DirectWorldProjector.Project(
            invalidPair,
            new ActorKnowledgeSnapshot(controlled, [])));

        var invalidActionPair = source with
        {
            Organisms = source.Organisms.SetItem(
                0,
                source.Organisms[0] with
                {
                    ActionGateEvidence =
                    [
                        new PublicationOrganismActionGateEvidence(
                            PublicationOrganismActionProcessKind.BiomassGrowth,
                            PublicationOrganismActionGateReason.CooldownActive,
                            0,
                            0,
                            null,
                            source.CompletedTick + 1),
                    ],
                }),
        };
        Assert.Throws<ArgumentException>(() => DirectWorldProjector.Project(
            invalidActionPair,
            new ActorKnowledgeSnapshot(controlled, [])));
    }

    [Fact]
    public void LiveProjectionIncludesAuthoritativeRemnants()
    {
        var runner = CreateRunner();
        WorldPublicationSnapshot source;
        do
        {
            runner.AdvanceOneTick();
            source = runner.CapturePublicationSnapshot();
        }
        while (source.Remnants.IsEmpty && source.CompletedTick < 2_000);

        Assert.NotEmpty(source.Remnants);
        Assert.NotEmpty(source.Organisms);
        var controlled = Assert.Single(source.Species).SpeciesId;
        var projection = DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(controlled, []));

        var live = Assert.IsType<LiveTileProjection>(projection.Tiles[0]);
        Assert.Equal(
            source.Remnants.Select(remnant => remnant.RemnantId),
            live.Remnants.Select(remnant => remnant.RemnantId));
        Assert.Equal(source.Remnants[0].StructuralMatterQ,
            live.Remnants[0].StructuralMatterQ);
        Assert.Equal(source.Remnants[0].BodyRadiusQ, live.Remnants[0].BodyRadiusQ);
    }

    [Fact]
    public void ProjectionIsCanonicalAcrossDetachedSourceOrdering()
    {
        var source = CreateRunner().CapturePublicationSnapshot();
        var controlled = Assert.Single(source.Species).SpeciesId;
        var knowledge = new ActorKnowledgeSnapshot(
            controlled,
            [
                new DiscoveredTileKnowledge(
                    source.Tiles[1].TileId,
                    0,
                    source.Tiles[1].ResourceStocks
                        .Where(stock => stock.QuantityQ > 0)
                        .Select(stock => stock.ResourceId)
                        .Reverse()
                        .ToImmutableArray()),
            ]);
        var permuted = source with
        {
            Tiles = source.Tiles
                .Reverse()
                .Select(tile => tile with
                {
                    ResourceStocks = tile.ResourceStocks.Reverse().ToImmutableArray(),
                })
                .ToImmutableArray(),
            Species = source.Species.Reverse().ToImmutableArray(),
            Organisms = source.Organisms
                .Reverse()
                .Select(organism => organism with
                {
                    CommittedMicronutrients = organism.CommittedMicronutrients
                        .Reverse().ToImmutableArray(),
                    FreeMicronutrients = organism.FreeMicronutrients
                        .Reverse().ToImmutableArray(),
                })
                .ToImmutableArray(),
            Remnants = source.Remnants
                .Reverse()
                .Select(remnant => remnant with
                {
                    Micronutrients = remnant.Micronutrients.Reverse().ToImmutableArray(),
                })
                .ToImmutableArray(),
            ResourceDefinitions = source.ResourceDefinitions.Reverse().ToImmutableArray(),
            ResourceFlows = source.ResourceFlows.Reverse().ToImmutableArray(),
        };

        var first = DirectWorldProjector.Project(source, knowledge);
        var second = DirectWorldProjector.Project(permuted, knowledge);

        Assert.Equal(ProjectionSignature(first), ProjectionSignature(second));
    }

    [Fact]
    public void ProjectionIsReadOnlyAndPublicationSnapshotsAreDetached()
    {
        var runner = CreateRunner();
        var boundaryBefore = runner.CaptureSnapshot();
        var sourceBefore = runner.CapturePublicationSnapshot();
        var controlled = Assert.Single(sourceBefore.Species).SpeciesId;
        var knowledge = new ActorKnowledgeSnapshot(controlled, []);

        _ = DirectWorldProjector.Project(sourceBefore, knowledge);

        Assert.Equal(boundaryBefore, runner.CaptureSnapshot());
        Assert.All(sourceBefore.Organisms, organism => Assert.Equal(0UL, organism.BiologicalAgeHours));

        runner.AdvanceOneTick();
        var sourceAfter = runner.CapturePublicationSnapshot();

        Assert.All(sourceBefore.Organisms, organism => Assert.Equal(0UL, organism.BiologicalAgeHours));
        Assert.All(sourceAfter.Organisms, organism => Assert.Equal(1UL, organism.BiologicalAgeHours));
    }

    [Fact]
    public void ProjectionRejectsInvalidActorKnowledge()
    {
        var source = CreateRunner().CapturePublicationSnapshot();
        var controlled = Assert.Single(source.Species).SpeciesId;
        var tile = source.Tiles[1];
        var valid = new DiscoveredTileKnowledge(tile.TileId, 0, []);

        Assert.Throws<ArgumentException>(() => DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(controlled, [valid, valid])));
        Assert.Throws<ArgumentException>(() => DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(
                controlled,
                [valid with { ObservedAtTick = source.CompletedTick + 1 }])));
        Assert.Throws<ArgumentException>(() => DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(
                controlled,
                [valid with { KnownPresentResourceIds = [ResourceId.From(999)] }])));
        Assert.Throws<ArgumentException>(() => DirectWorldProjector.Project(
            source,
            new ActorKnowledgeSnapshot(default, [])));
    }

    [Fact]
    public void PublicPublicationAndProjectionShapesContainNoDenseLocations()
    {
        var types = new[]
        {
            typeof(WorldPublicationSnapshot),
            typeof(PublicationTile),
            typeof(PublicationSpecies),
            typeof(PublicationGameState),
            typeof(PublicationOrganism),
            typeof(PublicationRemnant),
            typeof(ActorWorldProjection),
            typeof(UnknownTileProjection),
            typeof(ReducedTileProjection),
            typeof(LiveTileProjection),
            typeof(OrganismProjection),
            typeof(RemnantProjection),
            typeof(SpeciesProjection),
            typeof(GameProjection),
        };

        Assert.All(
            types,
            type => Assert.DoesNotContain(
                type.GetProperties(),
                property =>
                    property.Name.Contains("Dense", StringComparison.Ordinal) ||
                    property.Name.Contains("Slot", StringComparison.Ordinal) ||
                    property.Name.Contains("Chunk", StringComparison.Ordinal) ||
                    property.Name.Contains("RowIndex", StringComparison.Ordinal)));
    }

    private static WorldRunner CreateRunner() =>
        WorldRunner.CreateFoundation(WorldId.From(1), CompileWorld(), Seed);

    private static CompiledWorldRules CompileWorld()
    {
        var sourceRules = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(sourceRules.IsSuccess, FormatDiagnostics(sourceRules.Diagnostics));
        var compiledRules = RulePackCompiler.Compile(
            Assert.IsType<AuthoringRulePack>(sourceRules.Pack));
        Assert.True(compiledRules.IsSuccess, FormatDiagnostics(compiledRules.Diagnostics));

        var sourceWorld = WorldPackSourceLoader.Load(new CopiedPackageSource("OfficialWorld"));
        Assert.True(sourceWorld.IsSuccess, FormatDiagnostics(sourceWorld.Diagnostics));
        var authoringWorld = Assert.IsType<AuthoringWorldPack>(sourceWorld.Pack);
        var profile = Assert.Single(authoringWorld.Profiles);
        var firstTile = Assert.Single(profile.Tiles!);
        authoringWorld = authoringWorld with
        {
            Profiles =
            [
                profile with
                {
                    Width = 4,
                    Height = 1,
                    WrapX = true,
                    WrapY = false,
                    Tiles = Enumerable.Range(0, 4)
                        .Select(index => firstTile with
                        {
                            X = index,
                            ElevationMeters = firstTile.ElevationMeters - index,
                        })
                        .ToArray(),
                },
            ],
        };

        var compiledWorld = WorldRulesCompiler.Compile(
            Assert.IsType<CompiledRulePack>(compiledRules.RulePack),
            authoringWorld,
            "scenario.foundation-sandbox",
            "world.primordial-foundation");
        Assert.True(compiledWorld.IsSuccess, FormatDiagnostics(compiledWorld.Diagnostics));
        return Assert.IsType<CompiledWorldRules>(compiledWorld.WorldRules);
    }

    private static string ProjectionSignature(ActorWorldProjection projection) =>
        string.Join(
            ';',
            projection.Tiles.Select(TileSignature)
                .Concat(projection.Species.Select(species =>
                    $"S:{species.SpeciesId.Value}:{(byte)species.PopulationScope}:{species.Population}")));

    private static string TileSignature(TileProjection tile) =>
        tile switch
        {
            UnknownTileProjection unknown =>
                $"U:{unknown.TileId.Value}:{unknown.X}:{unknown.Y}",
            ReducedTileProjection reduced =>
                $"R:{reduced.TileId.Value}:{reduced.X}:{reduced.Y}:{reduced.ElevationMeters}:" +
                $"{reduced.ObservedAtTick}:" +
                string.Join(',', reduced.KnownPresentResourceIds.Select(id => id.Value)),
            LiveTileProjection live =>
                $"L:{live.TileId.Value}:{live.X}:{live.Y}:{live.ElevationMeters}:" +
                $"{live.ObservedAtTick}:" +
                string.Join(',', live.ResourceStocks.Select(stock =>
                    $"{stock.ResourceId.Value}={stock.QuantityQ}")) +
                ':' +
                string.Join(',', live.ResourceFlows.Select(flow =>
                    $"{flow.ResourceId.Value}={(byte)flow.Kind}={flow.AmountQ}")) +
                ':' +
                string.Join(',', live.Organisms.Select(organism =>
                    $"{organism.OrganismId.Value}={organism.SpeciesId.Value}=" +
                    $"{organism.PositionXQ}={organism.PositionYQ}=" +
                    $"{organism.BiologicalAgeHours}={organism.ChargedReserveQ}")) +
                ':' +
                string.Join(',', live.Remnants.Select(remnant =>
                    $"{remnant.RemnantId.Value}={remnant.SourceOrganismId.Value}=" +
                    $"{remnant.StructuralMatterQ}={remnant.ChargedReserveQ}")),
            _ => throw new InvalidOperationException("Unknown projection shape."),
        };

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

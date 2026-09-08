using System.Collections.Immutable;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Ledger;
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
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class ResourceLedgerTests
{
    private static readonly RootRandomSeed Seed =
        RootRandomSeed.Parse("fedcba98765432100123456789abcdef");

    [Fact]
    public void ExternalCaptureCommitsExactMatterAndEnergyBundles()
    {
        var runner = CreateRunner(founderCount: 3);
        var tileId = TileId.FromRowMajorIndex(0);
        var hydrogen = runner.MutableWorld.GetResourceHandle(ResourceId.From(1));
        var carbonDioxide = runner.MutableWorld.GetResourceHandle(ResourceId.From(2));
        var hydrogenBefore = runner.MutableWorld.GetTileResource(tileId, hydrogen);
        var carbonBefore = runner.MutableWorld.GetTileResource(tileId, carbonDioxide);
        var organismIds = runner.MutableWorld.GetOrganismIdsInCanonicalOrder();

        var result = runner.AdvanceOneTick();

        var phase = Assert.Single(
            result.Changes.Phases,
            journal => journal.Phase == TickPhase.ExternalResolution);
        Assert.Equal(PhaseExecutionClass.MapThenGroupedResolve, phase.ExecutionClass);
        Assert.Equal(3, phase.EvaluatedWorkCount);
        Assert.Equal(3, phase.ResourceTransactions.Length);
        Assert.All(phase.ResourceTransactions, transaction =>
        {
            Assert.Equal(200, transaction.Extent);
            Assert.Equal(LedgerCause.ExternalEnergyCapture, transaction.Cause);
            Assert.Equal(4, transaction.MatterEntries.Length);
            Assert.Equal(3, transaction.EnergyEntries.Length);
        });
        Assert.Equal(
            hydrogenBefore - 2_400,
            runner.MutableWorld.GetTileResource(tileId, hydrogen));
        Assert.Equal(
            carbonBefore - 1_200,
            runner.MutableWorld.GetTileResource(tileId, carbonDioxide));
        Assert.All(
            organismIds,
            organismId => Assert.Equal(
                5_044,
                runner.MutableWorld.GetOrganism(organismId).ChargedReserveQ));

        var report = ResourceLedgerOracle.Reconcile(
            runner.Rules.RulePack,
            phase.ResourceTransactions);
        Assert.True(report.IsBalanced);
        Assert.Equal(3, report.TransactionCount);
        Assert.All(report.Elements, element => Assert.Equal((Int128)0, element.NetQ));
        Assert.Equal((Int128)0, report.NetEnergyQ);
        Assert.Equal(
            1_200,
            phase.ResourceTransactions
                .SelectMany(transaction => transaction.MatterEntries)
                .Where(entry =>
                    entry.Account.OwnerKind == MatterAccountOwnerKind.Boundary &&
                    entry.Account.Compartment == MatterCompartment.OceanWaterBoundary)
                .Sum(entry => entry.DeltaQ));
        Assert.Equal(
            600,
            phase.ResourceTransactions
                .SelectMany(transaction => transaction.EnergyEntries)
                .Where(entry => entry.Account.Kind == EnergyAccountKind.DissipatedHeat)
                .Sum(entry => entry.DeltaQ));
    }

    [Fact]
    public void CoupledScarcityAdmitsOnlyWholeBundlesByStableKeyedRank()
    {
        var first = CreateConstrainedRunner();
        var second = CreateConstrainedRunner();

        var firstResult = first.AdvanceOneTick();
        var secondResult = second.AdvanceOneTick();

        var firstLedger = GetExternalLedger(firstResult);
        var secondLedger = GetExternalLedger(secondResult);
        AssertLedgerEqual(firstLedger, secondLedger);
        Assert.Equal(2, firstLedger.Length);
        Assert.All(firstLedger, transaction => Assert.Equal(1, transaction.Extent));

        var tileId = TileId.FromRowMajorIndex(0);
        Assert.Equal(
            0,
            first.MutableWorld.GetTileResource(
                tileId,
                first.MutableWorld.GetResourceHandle(ResourceId.From(1))));
        Assert.Equal(
            96,
            first.MutableWorld.GetTileResource(
                tileId,
                first.MutableWorld.GetResourceHandle(ResourceId.From(2))));
        Assert.Equal(
            [4_644L, 4_646L, 4_646L],
            first.MutableWorld
                .GetOrganismIdsInCanonicalOrder()
                .Select(id => first.MutableWorld.GetOrganism(id).ChargedReserveQ)
                .Order()
                .ToArray());

        var published = first.CapturePublicationSnapshot();
        Assert.All(published.Organisms, organism =>
        {
            var hydrogen = Assert.Single(organism.ResourceAcquisitionEvidence,
                value => value.ResourceId == ResourceId.From(1));
            var carbonDioxide = Assert.Single(organism.ResourceAcquisitionEvidence,
                value => value.ResourceId == ResourceId.From(2));
            Assert.Equal(800, hydrogen.RequestedQ);
            Assert.True(hydrogen.GrantedQ is 0 or 4);
            Assert.True(hydrogen.TileSupplyConstrained);
            Assert.False(hydrogen.ClaimContentionConstrained);
            Assert.Equal(400, carbonDioxide.RequestedQ);
            Assert.True(carbonDioxide.GrantedQ is 0 or 2);
            Assert.False(carbonDioxide.TileSupplyConstrained);
            Assert.False(carbonDioxide.ClaimContentionConstrained);
        });
    }

    [Fact]
    public void InsufficientCoupledInputLeavesAllAccountsUnchanged()
    {
        var runner = CreateRunner(founderCount: 2);
        SetTileResource(runner, ResourceId.From(1), 3);
        SetTileResource(runner, ResourceId.From(2), 4);
        var organismIds = runner.MutableWorld.GetOrganismIdsInCanonicalOrder();

        var result = runner.AdvanceOneTick();

        Assert.Empty(GetExternalLedger(result));
        Assert.Equal(
            3,
            runner.MutableWorld.GetTileResource(
                TileId.FromRowMajorIndex(0),
                runner.MutableWorld.GetResourceHandle(ResourceId.From(1))));
        Assert.All(
            organismIds,
            id => Assert.Equal(4_644, runner.MutableWorld.GetOrganism(id).ChargedReserveQ));
    }

    [Fact]
    public void ReserveCapacitySuppressesCaptureBeforeSharedClaims()
    {
        var runner = CreateRunner(founderCount: 1);
        var organismId = Assert.Single(runner.MutableWorld.GetOrganismIdsInCanonicalOrder());
        var setup = runner.MutableWorld.BeginChanges();
        runner.MutableWorld.AdjustOrganismChargedReserve(organismId, 5_000, setup);
        runner.MutableWorld.SealChanges(setup);
        var hydrogenBefore = runner.MutableWorld.GetTileResource(
            TileId.FromRowMajorIndex(0),
            runner.MutableWorld.GetResourceHandle(ResourceId.From(1)));

        var result = runner.AdvanceOneTick();

        Assert.Empty(GetExternalLedger(result));
        Assert.Equal(9_644, runner.MutableWorld.GetOrganism(organismId).ChargedReserveQ);
        Assert.Equal(
            hydrogenBefore,
            runner.MutableWorld.GetTileResource(
                TileId.FromRowMajorIndex(0),
                runner.MutableWorld.GetResourceHandle(ResourceId.From(1))));
        var gate = Assert.Single(Assert.Single(
            runner.CapturePublicationSnapshot().Organisms).AcquisitionGateEvidence);
        Assert.Equal(PublicationAcquisitionProcessKind.ExternalEnergyCapture, gate.Process);
        Assert.Equal(PublicationAcquisitionGateReason.InternalCapacity, gate.Reason);
        Assert.Equal(0, gate.AvailableQ);
        Assert.Equal(2, gate.RequiredQ);
    }

    [Fact]
    public void OracleRejectsAReactionMissingItsBoundaryOutput()
    {
        var runner = CreateRunner(founderCount: 1);
        var valid = Assert.Single(GetExternalLedger(runner.AdvanceOneTick()));
        var malformed = valid with
        {
            MatterEntries = valid.MatterEntries
                .Where(entry => entry.Account.OwnerKind != MatterAccountOwnerKind.Boundary)
                .ToImmutableArray(),
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ResourceLedgerOracle.Reconcile(runner.Rules.RulePack, [malformed]));

        Assert.Contains("compiled reaction bundle", exception.Message, StringComparison.Ordinal);
    }

    private static ImmutableArray<ResourceTransaction> GetExternalLedger(TickResult result) =>
        Assert.Single(
            result.Changes.Phases,
            journal => journal.Phase == TickPhase.ExternalResolution)
        .ResourceTransactions;

    private static void AssertLedgerEqual(
        IReadOnlyList<ResourceTransaction> expected,
        IReadOnlyList<ResourceTransaction> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].Key, actual[index].Key);
            Assert.Equal(expected[index].MatterEntries.AsEnumerable(), actual[index].MatterEntries.AsEnumerable());
            Assert.Equal(expected[index].EnergyEntries.AsEnumerable(), actual[index].EnergyEntries.AsEnumerable());
        }
    }

    private static WorldRunner CreateConstrainedRunner()
    {
        var runner = CreateRunner(founderCount: 3);
        SetTileResource(runner, ResourceId.From(1), 8);
        SetTileResource(runner, ResourceId.From(2), 100);
        return runner;
    }

    private static void SetTileResource(
        WorldRunner runner,
        ResourceId resourceId,
        long quantity)
    {
        var tileId = TileId.FromRowMajorIndex(0);
        var handle = runner.MutableWorld.GetResourceHandle(resourceId);
        var current = runner.MutableWorld.GetTileResource(tileId, handle);
        var changes = runner.MutableWorld.BeginChanges();
        runner.MutableWorld.ApplyTileResourceDelta(tileId, handle, quantity - current, changes);
        runner.MutableWorld.SealChanges(changes);
    }

    private static WorldRunner CreateRunner(uint founderCount) =>
        WorldRunner.CreateFoundation(WorldId.From(1), CompileWorld(), Seed, founderCount);

    private static CompiledWorldRules CompileWorld()
    {
        var sourceRules = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(sourceRules.IsSuccess, FormatDiagnostics(sourceRules.Diagnostics));
        var compiledRules = RulePackCompiler.Compile(
            Assert.IsType<AuthoringRulePack>(sourceRules.Pack));
        Assert.True(compiledRules.IsSuccess, FormatDiagnostics(compiledRules.Diagnostics));

        var sourceWorld = WorldPackSourceLoader.Load(new CopiedPackageSource("OfficialWorld"));
        Assert.True(sourceWorld.IsSuccess, FormatDiagnostics(sourceWorld.Diagnostics));
        var authoring = Assert.IsType<AuthoringWorldPack>(sourceWorld.Pack);
        var profile = Assert.Single(authoring.Profiles);
        var tile = Assert.Single(profile.Tiles!);
        authoring = authoring with
        {
            Profiles = [profile with
            {
                GasEnvironment = profile.GasEnvironment with
                {
                    Gases = profile.GasEnvironment.Gases.Select(gas => gas with
                    {
                        SinkRatePerMillionPerHour = 0,
                        ExchangeRatePerMillionPerEdgeHour = 0,
                        DiffuseSourceQuantityPerHour = 0,
                    }).ToArray(),
                },
                Tiles = [tile with { BaselineVolcanismQ = 0, GasEmissionProfileKey = null }],
            }],
        };
        var compiledWorld = WorldRulesCompiler.Compile(
            Assert.IsType<CompiledRulePack>(compiledRules.RulePack),
            authoring,
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

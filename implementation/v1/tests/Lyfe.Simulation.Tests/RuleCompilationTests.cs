using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class RuleCompilationTests
{
    [Fact]
    public void OfficialFoundationCompilesToFrozenIdentities()
    {
        var compiled = CompileOfficialRules();

        Assert.Equal(
            "42030eb34544faada34c7a4603f896a3c37b0b289ceb77017342fb58f5abace7",
            compiled.Identity.MechanicsHash);
        Assert.Equal(
            "bc940982373f88b8f315f4564c5b132ab2b207a7796fd9c9ed43367ffda9a345",
            compiled.Identity.PresentationHash);
        Assert.Equal(
            "9817cefa04b9a8ceae73e383838f8d4406dba65db9db75fc14db19bc87b786c1",
            compiled.Identity.RegistryManifestHash);
        Assert.Equal(
            "fd5f33f42585eb540cd5d630019ef661a9d819d652d71cdd128d477d1a9b6f4e",
            compiled.Identity.CompiledArtifactHash);
    }

    [Fact]
    public void CompilerProducesDenseNumericReactionAndPhenotypePlans()
    {
        var compiled = CompileOfficialRules();

        Assert.Equal(Enumerable.Range(1, 31).Select(value => (uint)value),
            compiled.Resources.Select(resource => resource.Id.Value));
        var capture = compiled.Reactions[0];
        Assert.Equal(ProcessKind.ExternalEnergyCapture, capture.ProcessKind);
        Assert.Equal([1U, 2U], capture.Inputs.Select(term => term.Resource.Id.Value));
        Assert.Equal([0, 1], capture.Inputs.Select(term => term.Resource.DenseSlot));
        Assert.Equal([4L, 2L], capture.Inputs.Select(term => term.Quantity));
        Assert.Equal([3U, 4U], capture.Outputs.Select(term => term.Resource.Id.Value));
        Assert.Equal([2, 3], capture.Outputs.Select(term => term.Resource.DenseSlot));

        var digestion = compiled.Reactions[1];
        Assert.Equal(ProcessKind.ParticulateDigestion, digestion.ProcessKind);
        Assert.Equal([5U], digestion.Inputs.Select(term => term.Resource.Id.Value));
        Assert.Equal([1L], digestion.Inputs.Select(term => term.Quantity));
        Assert.Equal([3U, 6U], digestion.Outputs.Select(term => term.Resource.Id.Value));
        Assert.Equal([32L, 1L], digestion.Outputs.Select(term => term.Quantity));
        Assert.Equal((40L, 32L, 8L),
            (digestion.GrossEnergyQ, digestion.StoredEnergyQ, digestion.DissipatedEnergyQ));

        var phenotype = compiled.FounderPhenotypes.Single(candidate =>
            candidate.FounderGenomeId.Value == 1);
        var process = phenotype.Processes.Single(value => value.Reaction.Id.Value == 1);
        Assert.Equal(1U, process.Reaction.Id.Value);
        Assert.Equal(0, process.Reaction.DenseSlot);
        Assert.Matches("^[0-9a-f]{64}$", phenotype.CanonicalCompiledHash);
        Assert.Equal(1_000, phenotype.Physiology.MatureStructureQ);
        Assert.Equal(10_000, phenotype.Physiology.ChargedReserveCapacityQ);
        Assert.Equal(ProcessAllocationPolicy.EqualShared, phenotype.Physiology.AllocationPolicy);
        Assert.Equal(25, phenotype.Physiology.Recycling.ScavengeActionCostQ);
        Assert.Equal(50, phenotype.Physiology.Recycling.ParticulateScavengeActionCostQ);
        Assert.False(phenotype.Physiology.Behavior.ResourceConservation);
        Assert.Equal(4UL, phenotype.Physiology.Behavior.MinimumDwellHours);
        Assert.Equal(500_000U,
            phenotype.Physiology.Behavior.ConservationEnterReserveQ);
        Assert.Equal(FoundingMetabolismKind.HydrogenAcetogenesis,
            phenotype.Physiology.OpeningMetabolism.Kind);
        Assert.Equal(250U,
            phenotype.Physiology.OpeningMetabolism.MaximumCaptureExtentsPerHour);

        var sulfur = compiled.FounderPhenotypes.Single(candidate =>
            candidate.FounderGenomeId.Value == 2);
        Assert.Contains(sulfur.Processes, process => process.Reaction.Id.Value == 3);
        Assert.Equal(FoundingMetabolismKind.SulfideAnoxygenicPhototrophy,
            sulfur.Physiology.OpeningMetabolism.Kind);
        Assert.True(sulfur.Physiology.OpeningMetabolism.RequiresLight);

        var scenario = Assert.Single(compiled.Scenarios);
        Assert.Equal(1U, scenario.TickDurationHours);
        Assert.Equal([1U, 2U], scenario.PermittedFounders.Select(founder => founder.Id.Value));
        Assert.Equal([1U, 2U, 3U], scenario.PermittedFounderAllocations
            .Select(allocation => allocation.Id.Value));
        Assert.Equal(1U, scenario.DefaultCompetitorFounderAllocation.Id.Value);
        Assert.Equal(
            [(1U, 1_000_000U, 1_000_000U), (2U, 1_062_500U, 800_000U), (3U, 937_500U, 1_250_000U)],
            compiled.FounderAllocations.Select(allocation => (
                allocation.Id.Value,
                allocation.CaptureEfficiencyMultiplierQ,
                allocation.ChemicalToleranceMultiplierQ)));
    }

    [Fact]
    public void DefinitionAndTermOrderDoNotAffectCanonicalCompilation()
    {
        var source = LoadOfficialRules();
        var reordered = source with
        {
            Resources = source.Resources.Reverse().ToArray(),
            Reactions = source.Reactions
                .Reverse()
                .Select(reaction => reaction with
                {
                    Inputs = reaction.Inputs.Reverse().ToArray(),
                    Outputs = reaction.Outputs.Reverse().ToArray(),
                })
                .ToArray(),
            FounderGenomes = source.FounderGenomes.Reverse().ToArray(),
            Scenarios = source.Scenarios.Reverse().ToArray(),
            RegistryLock = source.RegistryLock with
            {
                Registries = source.RegistryLock.Registries
                    .Reverse()
                    .Select(registry => registry with
                    {
                        Entries = registry.Entries.Reverse().ToArray(),
                    })
                    .ToArray(),
            },
        };

        var original = AssertCompiled(RulePackCompiler.Compile(source));
        var second = AssertCompiled(RulePackCompiler.Compile(reordered));

        Assert.Equal(original.Identity, second.Identity);
    }

    [Fact]
    public void PresentationEditDoesNotChangeSimulationIdentity()
    {
        var source = LoadOfficialRules();
        var renamed = source with
        {
            Resources = source.Resources
                .Select(resource => resource.NumericId == 1
                    ? resource with { DisplayName = "Hydrogen" }
                    : resource)
                .ToArray(),
        };

        var original = AssertCompiled(RulePackCompiler.Compile(source));
        var second = AssertCompiled(RulePackCompiler.Compile(renamed));

        Assert.Equal(original.Identity.MechanicsHash, second.Identity.MechanicsHash);
        Assert.Equal(original.Identity.RegistryManifestHash, second.Identity.RegistryManifestHash);
        Assert.Equal(original.Identity.CompiledArtifactHash, second.Identity.CompiledArtifactHash);
        Assert.NotEqual(original.Identity.PresentationHash, second.Identity.PresentationHash);
    }

    [Fact]
    public void SemanticEditChangesSourceAndCompiledIdentities()
    {
        var source = LoadOfficialRules();
        var rebalanced = source with
        {
            Reactions = source.Reactions
                .Select(reaction => reaction with
                {
                    GrossEnergyQ = 4,
                    StoredEnergyQ = 3,
                    DissipatedEnergyQ = 1,
                })
                .ToArray(),
        };

        var original = AssertCompiled(RulePackCompiler.Compile(source));
        var second = AssertCompiled(RulePackCompiler.Compile(rebalanced));

        Assert.NotEqual(original.Identity.MechanicsHash, second.Identity.MechanicsHash);
        Assert.NotEqual(original.Identity.CompiledArtifactHash, second.Identity.CompiledArtifactHash);
    }

    [Fact]
    public void InvalidFounderPhysiologyCannotCompile()
    {
        var source = LoadOfficialRules();
        var invalid = source with
        {
            FounderGenomes = source.FounderGenomes
                .Select(founder => founder with
                {
                    Physiology = founder.Physiology with
                    {
                        StructuralHardFloorQ = founder.Physiology.MatureStructureQ,
                    },
                })
                .ToArray(),
        };

        var result = RulePackCompiler.Compile(invalid);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-COMPILE-GENOME-005");
    }

    [Fact]
    public void PhysiologyEditChangesMechanicsAndCompiledIdentity()
    {
        var source = LoadOfficialRules();
        var edited = source with
        {
            FounderGenomes = source.FounderGenomes
                .Select(founder => founder with
                {
                    Physiology = founder.Physiology with
                    {
                        ChargedReserveCapacityQ = 10_001,
                    },
                })
                .ToArray(),
        };

        var original = AssertCompiled(RulePackCompiler.Compile(source));
        var changed = AssertCompiled(RulePackCompiler.Compile(edited));

        Assert.NotEqual(original.Identity.MechanicsHash, changed.Identity.MechanicsHash);
        Assert.NotEqual(
            original.Identity.CompiledArtifactHash,
            changed.Identity.CompiledArtifactHash);
        Assert.NotEqual(
            original.FounderPhenotypes[0].CanonicalCompiledHash,
            changed.FounderPhenotypes[0].CanonicalCompiledHash);
    }

    [Fact]
    public void LifecycleProfileEditChangesMechanicsAndCompiledIdentity()
    {
        var source = LoadOfficialRules();
        var edited = source with
        {
            FounderGenomes = source.FounderGenomes
                .Select(founder => founder with
                {
                    Physiology = founder.Physiology with
                    {
                        Reproduction = founder.Physiology.Reproduction with
                        {
                            BaseCooldownHours =
                                founder.Physiology.Reproduction.BaseCooldownHours + 1,
                        },
                    },
                })
                .ToArray(),
        };

        var original = AssertCompiled(RulePackCompiler.Compile(source));
        var changed = AssertCompiled(RulePackCompiler.Compile(edited));

        Assert.NotEqual(original.Identity.MechanicsHash, changed.Identity.MechanicsHash);
        Assert.NotEqual(original.Identity.CompiledArtifactHash,
            changed.Identity.CompiledArtifactHash);
        Assert.NotEqual(original.FounderPhenotypes[0].CanonicalCompiledHash,
            changed.FounderPhenotypes[0].CanonicalCompiledHash);
    }

    [Fact]
    public void BehaviorProfileEditChangesMechanicsAndCompiledIdentity()
    {
        var source = LoadOfficialRules();
        var edited = source with
        {
            FounderGenomes = source.FounderGenomes
                .Select(founder => founder with
                {
                    Physiology = founder.Physiology with
                    {
                        Behavior = founder.Physiology.Behavior with
                        {
                            ResourceConservation = true,
                        },
                    },
                })
                .ToArray(),
        };

        var original = AssertCompiled(RulePackCompiler.Compile(source));
        var changed = AssertCompiled(RulePackCompiler.Compile(edited));

        Assert.NotEqual(original.Identity.MechanicsHash, changed.Identity.MechanicsHash);
        Assert.NotEqual(original.Identity.CompiledArtifactHash,
            changed.Identity.CompiledArtifactHash);
    }

    [Fact]
    public void UnbalancedReactionCannotCompile()
    {
        var source = LoadOfficialRules();
        var unbalanced = source with
        {
            Reactions = source.Reactions
                .Select(reaction => reaction with
                {
                    Outputs = reaction.Outputs
                        .Select(output => output.ResourceKey == "resource.ocean-water"
                            ? output with { Quantity = 1 }
                            : output)
                        .ToArray(),
                })
                .ToArray(),
        };

        var result = RulePackCompiler.Compile(unbalanced);

        Assert.False(result.IsSuccess);
        Assert.Null(result.RulePack);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-COMPILE-REACTION-005");
    }

    [Fact]
    public void UnknownEngineVocabularyCannotCompile()
    {
        var source = LoadOfficialRules();
        var unsupported = source with
        {
            Reactions = source.Reactions
                .Select(reaction => reaction with { ProcessKind = "arbitrary-script" })
                .ToArray(),
        };

        var result = RulePackCompiler.Compile(unsupported);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-COMPILE-REACTION-001");
    }

    [Fact]
    public void OfficialWorldBindsToFrozenWorldIdentities()
    {
        var rules = CompileOfficialRules();
        var sourceWorld = LoadOfficialWorld();

        var result = WorldRulesCompiler.Compile(
            rules,
            sourceWorld,
            "scenario.foundation-sandbox",
            "world.primordial-foundation");

        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert.NotNull(result.WorldRules);
        Assert.Equal(
            "9c9941f6311a159d3d7206b1c47d4dd38c6ac3222e967f647f13b488a23463d5",
            result.WorldRules.Identity.WorldPackageHash);
        Assert.Equal(
            "1b82aef52f4ca811c2ad0e237a58c546cc1c569c9fb0fa5567d37a77a2e7f77f",
            result.WorldRules.Identity.CompiledWorldProfileHash);
        Assert.Equal(
            "763d49ed1cb3516d644f7398cd38aea864dfb12e325f3ef3819d1488636a9c78",
            result.WorldRules.Identity.WorldRulesHash);
        Assert.Equal(1U, result.WorldRules.TickDurationHours);

        var tile = Assert.Single(result.WorldRules.WorldProfile.Tiles);
        Assert.Equal(0U, tile.TileIndex);
        Assert.Equal(800_000U, tile.BaselineVolcanismQ);
        Assert.Equal(0, tile.GasEmissionProfileSlot);
        var gases = result.WorldRules.WorldProfile.GasEnvironment;
        Assert.Equal(8, gases.Gases.Length);
        Assert.Equal(2, gases.EmissionProfiles.Length);
        var hydrogen = gases.Gases.Single(gas => gas.Resource.Id.Value == 1);
        Assert.Equal(GasAccessibilityClass.VentAccessible, hydrogen.AccessibilityClass);
        Assert.Equal(2_500U, hydrogen.SinkRatePerMillionPerHour);
        Assert.Equal(100U, hydrogen.ExchangeRatePerMillionPerEdgeHour);
        Assert.Equal(312_500,
            gases.EmissionProfiles[0].FullActivityQuantitiesPerHourByGasSlot[0]);
        Assert.Equal(
            [
                50_000_000L, 100_000_000L, 0L, 0L, 0L, 0L, 0L, 0L, 0L,
                20_000_000L, 5_000_000L, 0L, 500_000_000L, 10_000_000L, 0L,
                5_000_000L, 0L, 50_000L, 2_000_000L, 3_000_000L, 10_000_000L,
                5_000_000L, 1_000L, 0L, 0L, 100_000L, 500L, 0L, 0L, 500_000L,
                100_000L,
            ],
            tile.ResourceQuantitiesByDenseSlot);
    }

    [Fact]
    public void WorldCannotBindAgainstAChangedRegistry()
    {
        var rules = CompileOfficialRules();
        var sourceWorld = LoadOfficialWorld();
        var incompatible = sourceWorld with
        {
            Manifest = sourceWorld.Manifest with
            {
                RequiredRegistryManifestHash = new string('0', 64),
            },
        };

        var result = WorldRulesCompiler.Compile(
            rules,
            incompatible,
            "scenario.foundation-sandbox",
            "world.primordial-foundation");

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-COMPILE-WORLD-002");
    }

    private static CompiledRulePack CompileOfficialRules() =>
        AssertCompiled(RulePackCompiler.Compile(LoadOfficialRules()));

    private static CompiledRulePack AssertCompiled(RuleCompilationResult result)
    {
        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        return Assert.IsType<CompiledRulePack>(result.RulePack);
    }

    private static AuthoringRulePack LoadOfficialRules()
    {
        var result = RulePackSourceLoader.Load(new CopiedPackageSource("OfficialRules"));
        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        return Assert.IsType<AuthoringRulePack>(result.Pack);
    }

    private static AuthoringWorldPack LoadOfficialWorld()
    {
        var result = WorldPackSourceLoader.Load(new CopiedPackageSource("OfficialWorld"));
        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        return Assert.IsType<AuthoringWorldPack>(result.Pack);
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
}

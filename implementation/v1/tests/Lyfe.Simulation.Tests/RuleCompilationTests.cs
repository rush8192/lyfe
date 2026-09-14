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
            "58fc849e13524f95d9f373e949d33193e82d6c8753d9f60795db8fa6ce479a95",
            compiled.Identity.MechanicsHash);
        Assert.Equal(
            "b5725ef99924b1de9cc613ae66ff16bb47f73e7be4574ae8beea2d3201c145cb",
            compiled.Identity.PresentationHash);
        Assert.Equal(
            "3e37dcd7cdf8e079fdfe119733a6244a9b4dbee3c0a9f3636a841f3e4f2faacd",
            compiled.Identity.RegistryManifestHash);
        Assert.Equal(
            "bddfb5ed706a06fcc011d067053600a541a6250ce8d1b58977488400dedaa309",
            compiled.Identity.CompiledArtifactHash);
    }

    [Fact]
    public void CompilerProducesDenseNumericReactionAndPhenotypePlans()
    {
        var compiled = CompileOfficialRules();

        Assert.Equal(Enumerable.Range(1, 32).Select(value => (uint)value),
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
        Assert.Equal(
            [EvolutionStrategicIntent.InvestInComplexity],
            compiled.Traits.Single(trait => trait.Id.Value == 3).StrategicIntents);
        var conservation = compiled.Traits.Single(trait => trait.Id.Value == 4);
        Assert.Equal(720UL, conservation.ConsequenceFollowUpHours);
        Assert.Equal(EvolutionFollowUpEvidenceKind.ConditionAndPressure,
            conservation.ConsequenceEvidenceKind);
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
            new CompiledFounderInitializationProfile(0, 168, 240, 360),
            scenario.FounderInitialization);
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
    public void StrategicIntentEditChangesOnlyPresentationIdentity()
    {
        var source = LoadOfficialRules();
        var regrouped = source with
        {
            Traits = source.Traits
                .Select(trait => trait.NumericId == 3
                    ? trait with
                    {
                        StrategicIntents = ["endure-environmental-pressure"],
                    }
                    : trait)
                .ToArray(),
        };

        var original = AssertCompiled(RulePackCompiler.Compile(source));
        var second = AssertCompiled(RulePackCompiler.Compile(regrouped));

        Assert.Equal(original.Identity.MechanicsHash, second.Identity.MechanicsHash);
        Assert.Equal(original.Identity.CompiledArtifactHash, second.Identity.CompiledArtifactHash);
        Assert.NotEqual(original.Identity.PresentationHash, second.Identity.PresentationHash);
    }

    [Fact]
    public void UnknownStrategicIntentFailsClosed()
    {
        var source = LoadOfficialRules();
        var invalid = source with
        {
            Traits = source.Traits
                .Select(trait => trait.NumericId == 3
                    ? trait with { StrategicIntents = ["become-unbeatable"] }
                    : trait)
                .ToArray(),
        };

        var result = RulePackCompiler.Compile(invalid);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-COMPILE-TRAIT-004");
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
    public void InvalidFounderInitializationRangeFailsClosed()
    {
        var source = LoadOfficialRules();
        var scenario = Assert.Single(source.Scenarios);
        var invalid = source with
        {
            Scenarios =
            [
                scenario with
                {
                    FounderInitialization = scenario.FounderInitialization with
                    {
                        BiologicalAgeMinimumHours = 200,
                        BiologicalAgeMaximumHours = 100,
                    },
                },
            ],
        };

        var result = RulePackCompiler.Compile(invalid);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-COMPILE-SCENARIO-006");
    }

    [Fact]
    public void FounderInitializationCannotBeginAtSenescence()
    {
        var source = LoadOfficialRules();
        var scenario = Assert.Single(source.Scenarios);
        var invalid = source with
        {
            Scenarios =
            [
                scenario with
                {
                    FounderInitialization = scenario.FounderInitialization with
                    {
                        BiologicalAgeMaximumHours = 720,
                    },
                },
            ],
        };

        var result = RulePackCompiler.Compile(invalid);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-COMPILE-SCENARIO-007");
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
            "bfea01d44cbb9178b0dfc95d234ea35faa9cefbaa0bdf3e0058843e149fde504",
            result.WorldRules.Identity.WorldPackageHash);
        Assert.Equal(
            "f4ddc3a4c28d9e765d9ffe8723bff58699a63f288dd0ca14049df12ca804b3dc",
            result.WorldRules.Identity.CompiledWorldProfileHash);
        Assert.Equal(
            "4762b46dcdd0f1b4b6a3f43d86e07c2d4b5d8be28ca778b2ceabea4ef9d1a813",
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
                100_000L, 0L,
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

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
            "d95a8a7fdf28f9fe1aef9e0049a31f7d72783bcb3f92b90f1236fbc1f7b24a54",
            compiled.Identity.MechanicsHash);
        Assert.Equal(
            "7c62355f50316537d1af366a49265befa65e78c16474251b8a4e6badfae32880",
            compiled.Identity.PresentationHash);
        Assert.Equal(
            "48c4f080abd2edb9c5612856a05d882d727e9bc5d6a06661eb5fa0a9b32bdedc",
            compiled.Identity.RegistryManifestHash);
        Assert.Equal(
            "fe247f49751950f020cae560e5c3a6c1de07b138a11144850ef92908c14c3c94",
            compiled.Identity.CompiledArtifactHash);
    }

    [Fact]
    public void CompilerProducesDenseNumericReactionAndPhenotypePlans()
    {
        var compiled = CompileOfficialRules();

        Assert.Equal([1U, 2U, 3U, 4U], compiled.Resources.Select(resource => resource.Id.Value));
        var reaction = Assert.Single(compiled.Reactions);
        Assert.Equal(ProcessKind.ExternalEnergyCapture, reaction.ProcessKind);
        Assert.Equal([1U, 2U], reaction.Inputs.Select(term => term.Resource.Id.Value));
        Assert.Equal([0, 1], reaction.Inputs.Select(term => term.Resource.DenseSlot));
        Assert.Equal([4L, 2L], reaction.Inputs.Select(term => term.Quantity));
        Assert.Equal([3U, 4U], reaction.Outputs.Select(term => term.Resource.Id.Value));
        Assert.Equal([2, 3], reaction.Outputs.Select(term => term.Resource.DenseSlot));

        var phenotype = Assert.Single(compiled.FounderPhenotypes);
        var process = Assert.Single(phenotype.Processes);
        Assert.Equal(1U, process.Reaction.Id.Value);
        Assert.Equal(0, process.Reaction.DenseSlot);
        Assert.Matches("^[0-9a-f]{64}$", phenotype.CanonicalCompiledHash);

        var scenario = Assert.Single(compiled.Scenarios);
        Assert.Equal(1U, scenario.TickDurationHours);
        Assert.Equal(1U, Assert.Single(scenario.PermittedFounders).Id.Value);
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
            "0c0c7e977567c3cc2d0885dd21e3b5a71278e4546a7e0239dbacddd510c16cc7",
            result.WorldRules.Identity.WorldPackageHash);
        Assert.Equal(
            "da617c97fde943bc26f2bab72d5306e10ab195f6d84be7ed309dcb712ecaf261",
            result.WorldRules.Identity.CompiledWorldProfileHash);
        Assert.Equal(
            "846dc18f45622007c77180c4d3b7328a8c62bcbf29c96ac4ce795e41abee77f6",
            result.WorldRules.Identity.WorldRulesHash);
        Assert.Equal(1U, result.WorldRules.TickDurationHours);

        var tile = Assert.Single(result.WorldRules.WorldProfile.Tiles);
        Assert.Equal(0U, tile.TileIndex);
        Assert.Equal([50_000_000L, 100_000_000L, 0L, 0L], tile.ResourceQuantitiesByDenseSlot);
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

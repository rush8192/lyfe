using System.Text;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class ContentLoadingTests
{
    [Fact]
    public void OfficialFoundationRulePackLoads()
    {
        var result = RulePackSourceLoader.Load(LoadCopiedPackage("OfficialRules"));

        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert.NotNull(result.Pack);
        Assert.Equal("lyfe.official.v1-foundation", result.Pack.Manifest.PackId);
        Assert.Equal(31, result.Pack.Resources.Count);
        Assert.Equal(5, result.Pack.Reactions.Count);
        Assert.Equal(2, result.Pack.FounderGenomes.Count);
        Assert.Single(result.Pack.Scenarios);
    }

    [Fact]
    public void OfficialFoundationWorldPackLoads()
    {
        var result = WorldPackSourceLoader.Load(LoadCopiedPackage("OfficialWorld"));

        Assert.True(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert.NotNull(result.Pack);
        var profile = Assert.Single(result.Pack.Profiles);
        Assert.True(profile.WrapX);
        Assert.False(profile.WrapY);
        Assert.Single(profile.Tiles!);
    }

    [Theory]
    [InlineData("unknown", "\"unexpected\": true, \"packId\":", "LYFE-CONTENT-JSON-001")]
    [InlineData("duplicate", "\"packId\": \"duplicate\", \"packId\":", "LYFE-CONTENT-JSON-002")]
    public void ManifestRejectsUnknownAndDuplicateProperties(
        string _,
        string replacement,
        string expectedCode)
    {
        var source = LoadCopiedPackage("OfficialRules");
        source.ReplaceText("pack.json", "\"packId\":", replacement);

        var result = RulePackSourceLoader.Load(source);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Fact]
    public void AuthoritativeNumbersRejectScientificNotation()
    {
        var source = LoadCopiedPackage("OfficialRules");
        source.ReplaceText(
            "resources/resources.json",
            "\"numericId\": 1,",
            "\"numericId\": 1e0,");

        var result = RulePackSourceLoader.Load(source);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-CONTENT-JSON-004");
    }

    [Fact]
    public void ManifestCannotEscapePackageRoot()
    {
        var source = LoadCopiedPackage("OfficialRules");
        source.ReplaceText(
            "pack.json",
            "resources/resources.json",
            "../resources.json");

        var result = RulePackSourceLoader.Load(source);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-CONTENT-MANIFEST-003");
    }

    [Fact]
    public void DuplicateDefinitionIdProducesDiagnosticsRatherThanThrowing()
    {
        var source = LoadCopiedPackage("OfficialRules");
        source.ReplaceText(
            "resources/resources.json",
            "\"numericId\": 2,",
            "\"numericId\": 1,");

        var result = RulePackSourceLoader.Load(source);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-CONTENT-IDENTITY-002");
    }

    [Fact]
    public void V1WorldTopologyRejectsNorthSouthWrapping()
    {
        var source = LoadCopiedPackage("OfficialWorld");
        source.ReplaceText(
            "profiles/foundation-volcanic-ocean.json",
            "\"wrapY\": false",
            "\"wrapY\": true");

        var result = WorldPackSourceLoader.Load(source);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "LYFE-WORLD-PROFILE-004");
    }

    [Fact]
    public void DiagnosticsAreCanonicalAcrossRepeatedLoads()
    {
        var source = LoadCopiedPackage("OfficialRules");
        source.ReplaceText(
            "resources/resources.json",
            "\"numericId\": 2,",
            "\"numericId\": 1,");

        var first = RulePackSourceLoader.Load(source);
        var second = RulePackSourceLoader.Load(source);

        Assert.Equal(first.Diagnostics, second.Diagnostics);
    }

    [Fact]
    public void PermanentDefinitionIdsReserveZero()
    {
        Assert.Equal(1U, ResourceId.From(1).Value);
        Assert.Equal(2U, ReactionId.From(2).Value);
        Assert.Equal(3U, FounderGenomeId.From(3).Value);
        Assert.Equal(4U, ScenarioId.From(4).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceId.From(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReactionId.From(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FounderGenomeId.From(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioId.From(0));
    }

    private static MutableContentSource LoadCopiedPackage(string directoryName)
    {
        var root = Path.Combine(AppContext.BaseDirectory, directoryName);
        var files = Directory
            .EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .ToDictionary(
                file => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);
        return new MutableContentSource(files);
    }

    private static string FormatDiagnostics(IEnumerable<RuleDiagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
            $"{diagnostic.SourceFile}: {diagnostic.Code}: {diagnostic.Message}"));

    private sealed class MutableContentSource(Dictionary<string, byte[]> files) : IContentSource
    {
        public bool TryRead(string normalizedRelativePath, out ReadOnlyMemory<byte> content)
        {
            if (files.TryGetValue(normalizedRelativePath, out var bytes))
            {
                content = bytes;
                return true;
            }

            content = default;
            return false;
        }

        public void ReplaceText(string path, string oldValue, string newValue)
        {
            var text = Encoding.UTF8.GetString(files[path]);
            var replaced = text.Replace(oldValue, newValue, StringComparison.Ordinal);
            Assert.NotEqual(text, replaced);
            files[path] = Encoding.UTF8.GetBytes(replaced);
        }
    }
}

using Lyfe.Simulation.Randomness;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class RandomIdentityTests
{
    [Theory]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("0123456789abcdeffedcba9876543210")]
    [InlineData("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    public void RootSeedRoundTripsThroughCanonicalText(string input)
    {
        var parsed = RootRandomSeed.Parse(input);

        Assert.Equal(input.ToLowerInvariant(), parsed.ToCanonicalString());
        Assert.Equal(parsed, RootRandomSeed.Parse(parsed.ToCanonicalString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("000000000000000000000000000000000")]
    [InlineData("0000000000000000000000000000000g")]
    public void RootSeedRejectsNonCanonicalShape(string input)
    {
        Assert.Throws<FormatException>(() => RootRandomSeed.Parse(input));
    }

    [Fact]
    public void RegistryHasStableUniqueNonzeroAscendingIds()
    {
        var definitions = RandomDomainRegistry.Definitions;
        var declaredDomainIds = typeof(RandomDomains)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(RandomDomainId))
            .Select(property => Assert.IsType<RandomDomainId>(property.GetValue(null)))
            .OrderBy(id => id.Value)
            .ToArray();

        Assert.NotEmpty(definitions);
        Assert.All(definitions, definition => Assert.NotEqual(0U, definition.Id.Value));
        Assert.Equal(
            definitions.Length,
            definitions.Select(definition => definition.Id).Distinct().Count());
        Assert.Equal(
            definitions.Length,
            definitions.Select(definition => definition.StableName).Distinct().Count());
        Assert.Equal(
            definitions.Select(definition => definition.Id.Value).Order().ToArray(),
            definitions.Select(definition => definition.Id.Value).ToArray());
        Assert.Equal(
            declaredDomainIds,
            definitions.Select(definition => definition.Id).ToArray());
        Assert.All(definitions, definition => Assert.InRange(
            definition.IntroducedRngSchemaVersion,
            1U,
            RandomCompatibility.RngSchemaVersion));
        Assert.Equal(
            [RandomDomains.FounderBiologicalAge, RandomDomains.FounderReproductionReadiness],
            definitions
                .Where(definition => definition.IntroducedRngSchemaVersion == 2)
                .Select(definition => definition.Id));
    }

    [Fact]
    public void AddressRejectsSampleIndexOutsideVersionOneSpace()
    {
        var address = RandomAddress.Create(RandomDomains.BrownianDirection, 1, 2, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            address.WithSampleIndex(Philox4x64.MaximumWordsPerAddress));
    }

    [Fact]
    public void CompatibilityStateUsesFrozenAlgorithmAndManifest()
    {
        var seed = new RootRandomSeed(0x0123456789ABCDEF, 0xFEDCBA9876543210);

        var state = RandomCompatibility.ForSeed(seed);

        Assert.Equal(seed, state.RootSeed);
        Assert.Equal("philox4x64-10-random123-v1", state.AlgorithmId);
        Assert.Equal(2U, state.RngSchemaVersion);
        Assert.Equal(
            "f14df02ab340673f7bc3fb5e0968371ff519c9ad5289743f38cd4510448ec2e0",
            state.RngDomainManifestHash);
        Assert.Equal(RandomDomainRegistry.ManifestHash, state.RngDomainManifestHash);
    }
}

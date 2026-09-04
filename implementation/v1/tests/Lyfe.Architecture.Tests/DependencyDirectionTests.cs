using Lyfe.Protocol;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Architecture.Tests;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void SimulationDoesNotReferenceServerOrProtocol()
    {
        var references = typeof(WorldRunner).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Lyfe.Server", references);
        Assert.DoesNotContain("Lyfe.Protocol", references);
        Assert.DoesNotContain(
            references,
            name => name is not null && name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void ProtocolDoesNotReferenceSimulationOrServer()
    {
        var references = typeof(ProtocolVersions).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Lyfe.Simulation", references);
        Assert.DoesNotContain("Lyfe.Server", references);
    }
}

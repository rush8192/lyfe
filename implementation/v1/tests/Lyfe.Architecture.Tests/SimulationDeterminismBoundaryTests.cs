using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Lyfe.Simulation.World;
using Xunit;

namespace Lyfe.Architecture.Tests;

public sealed class SimulationDeterminismBoundaryTests
{
    private static readonly HashSet<string> ProhibitedTypes = new(StringComparer.Ordinal)
    {
        "System.DateTime",
        "System.DateTimeOffset",
        "System.Diagnostics.Stopwatch",
        "System.Environment",
        "System.IO.Directory",
        "System.IO.DirectoryInfo",
        "System.IO.DriveInfo",
        "System.IO.File",
        "System.IO.FileInfo",
        "System.IO.FileStream",
        "System.IO.FileSystemInfo",
        "System.IO.FileSystemWatcher",
        "System.IO.Path",
        "System.Random",
        "System.Security.Cryptography.RandomNumberGenerator",
        "System.Threading.PeriodicTimer",
        "System.Threading.Timer",
        "System.TimeProvider",
        "System.Timers.Timer",
    };

    private static readonly HashSet<(string TypeName, string MemberName)> ProhibitedMembers = new()
    {
        ("System.Guid", "NewGuid"),
        ("System.Threading.Thread", "Sleep"),
        ("System.Threading.Tasks.Task", "Delay"),
    };

    [Fact]
    public void SimulationDoesNotReferenceAmbientOrHostIoApis()
    {
        var apiReferences = ReadApiReferences(typeof(WorldRunner).Assembly);
        Assert.Contains(
            apiReferences,
            reference => reference.TypeName == "System.Security.Cryptography.SHA256" &&
                reference.MemberName == "HashData");

        var prohibited = apiReferences
            .Where(reference => IsProhibited(reference.TypeName, reference.MemberName))
            .Select(reference => reference.MemberName is null
                ? reference.TypeName
                : $"{reference.TypeName}.{reference.MemberName}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(prohibited);
    }

    [Theory]
    [InlineData("System.DateTime", null, true)]
    [InlineData("System.Random", ".ctor", true)]
    [InlineData("System.Net.Http.HttpClient", ".ctor", true)]
    [InlineData("System.IO.File", "OpenRead", true)]
    [InlineData("System.Security.Cryptography.RandomNumberGenerator", "Fill", true)]
    [InlineData("System.Guid", "NewGuid", true)]
    [InlineData("System.Threading.Tasks.Task", "Delay", true)]
    [InlineData("System.Security.Cryptography.SHA256", "HashData", false)]
    [InlineData("System.Math", "Min", false)]
    public void PolicyClassifiesRepresentativeApis(
        string typeName,
        string? memberName,
        bool expected)
    {
        Assert.Equal(expected, IsProhibited(typeName, memberName));
    }

    private static bool IsProhibited(string typeName, string? memberName)
    {
        if (typeName.StartsWith("System.Net.", StringComparison.Ordinal) ||
            ProhibitedTypes.Contains(typeName))
        {
            return true;
        }

        return memberName is not null && ProhibitedMembers.Contains((typeName, memberName));
    }

    private static List<ApiReference> ReadApiReferences(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var portableExecutable = new PEReader(stream);
        var metadata = portableExecutable.GetMetadataReader();
        var references = new List<ApiReference>();

        foreach (var handle in metadata.TypeReferences)
        {
            references.Add(new ApiReference(GetFullTypeName(metadata, handle), null));
        }

        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            var typeName = GetFullTypeName(metadata, member.Parent);
            if (typeName is not null)
            {
                references.Add(new ApiReference(typeName, metadata.GetString(member.Name)));
            }
        }

        return references;
    }

    private static string GetFullTypeName(MetadataReader metadata, TypeReferenceHandle handle)
    {
        var reference = metadata.GetTypeReference(handle);
        return JoinTypeName(metadata.GetString(reference.Namespace), metadata.GetString(reference.Name));
    }

    private static string? GetFullTypeName(MetadataReader metadata, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.TypeReference)
        {
            return GetFullTypeName(metadata, (TypeReferenceHandle)handle);
        }

        if (handle.Kind == HandleKind.TypeDefinition)
        {
            var definition = metadata.GetTypeDefinition((TypeDefinitionHandle)handle);
            return JoinTypeName(
                metadata.GetString(definition.Namespace),
                metadata.GetString(definition.Name));
        }

        return null;
    }

    private static string JoinTypeName(string namespaceName, string typeName) =>
        string.IsNullOrEmpty(namespaceName) ? typeName : $"{namespaceName}.{typeName}";

    private readonly record struct ApiReference(string TypeName, string? MemberName);
}

using System.Globalization;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Loading;

const string usage = """
LYFE rule tool

Usage:
  dotnet run --project tools/Lyfe.RuleTool -- validate <rule-pack-root>
  dotnet run --project tools/Lyfe.RuleTool -- validate-world <world-pack-root>
  dotnet run --project tools/Lyfe.RuleTool -- compile <rule-pack-root> <world-pack-root> <scenario-key> <profile-key>
  dotnet run --project tools/Lyfe.RuleTool -- help

Validation is strict and fail closed. Manifests explicitly enumerate source files;
unknown or duplicate JSON properties, non-integer numeric tokens, invalid paths,
identity drift, and unresolved references are errors.
""";

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    Console.WriteLine(usage);
    return 0;
}

switch (args[0])
{
    case "validate" when args.Length == 2:
        {
            var source = new DirectoryContentSource(args[1]);
            var result = RulePackSourceLoader.Load(source);
            WriteDiagnostics(result.Diagnostics);
            if (!result.IsSuccess || result.Pack is null)
            {
                return 1;
            }

            var compiled = RulePackCompiler.Compile(result.Pack);
            WriteDiagnostics(compiled.Diagnostics);
            if (!compiled.IsSuccess || compiled.RulePack is null)
            {
                return 1;
            }

            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Valid rule pack '{result.Pack.Manifest.PackId}': {result.Pack.Resources.Count} resources, {result.Pack.Reactions.Count} reactions, {result.Pack.FounderGenomes.Count} founder genomes, {result.Pack.Scenarios.Count} scenarios; mechanics {compiled.RulePack.Identity.MechanicsHash}."));
            return 0;
        }
    case "validate-world" when args.Length == 2:
        {
            var source = new DirectoryContentSource(args[1]);
            var result = WorldPackSourceLoader.Load(source);
            WriteDiagnostics(result.Diagnostics);
            if (!result.IsSuccess || result.Pack is null)
            {
                return 1;
            }

            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Structurally valid world pack '{result.Pack.Manifest.WorldPackId}': {result.Pack.Profiles.Count} profiles; use compile to validate rule compatibility and references."));
            return 0;
        }
    case "compile" when args.Length == 5:
        {
            var sourceRules = RulePackSourceLoader.Load(new DirectoryContentSource(args[1]));
            WriteDiagnostics(sourceRules.Diagnostics);
            if (!sourceRules.IsSuccess || sourceRules.Pack is null)
            {
                return 1;
            }

            var compiledRules = RulePackCompiler.Compile(sourceRules.Pack);
            WriteDiagnostics(compiledRules.Diagnostics);
            if (!compiledRules.IsSuccess || compiledRules.RulePack is null)
            {
                return 1;
            }

            var sourceWorld = WorldPackSourceLoader.Load(new DirectoryContentSource(args[2]));
            WriteDiagnostics(sourceWorld.Diagnostics);
            if (!sourceWorld.IsSuccess || sourceWorld.Pack is null)
            {
                return 1;
            }

            var compiledWorld = WorldRulesCompiler.Compile(
                compiledRules.RulePack,
                sourceWorld.Pack,
                args[3],
                args[4]);
            WriteDiagnostics(compiledWorld.Diagnostics);
            if (!compiledWorld.IsSuccess || compiledWorld.WorldRules is null)
            {
                return 1;
            }

            var ruleIdentity = compiledRules.RulePack.Identity;
            var worldIdentity = compiledWorld.WorldRules.Identity;
            Console.WriteLine($"packId={ruleIdentity.PackId}");
            Console.WriteLine($"mechanicsHash={ruleIdentity.MechanicsHash}");
            Console.WriteLine($"presentationHash={ruleIdentity.PresentationHash}");
            Console.WriteLine($"registryManifestHash={ruleIdentity.RegistryManifestHash}");
            Console.WriteLine($"compiledArtifactHash={ruleIdentity.CompiledArtifactHash}");
            Console.WriteLine($"worldPackageHash={worldIdentity.WorldPackageHash}");
            Console.WriteLine($"compiledWorldProfileHash={worldIdentity.CompiledWorldProfileHash}");
            Console.WriteLine($"worldRulesHash={worldIdentity.WorldRulesHash}");
            return 0;
        }
    default:
        Console.Error.WriteLine("Unknown command or invalid argument count.");
        Console.Error.WriteLine(usage);
        return 2;
}

static void WriteDiagnostics(IEnumerable<RuleDiagnostic> diagnostics)
{
    foreach (var diagnostic in diagnostics)
    {
        var path = diagnostic.JsonPath is null ? string.Empty : $" {diagnostic.JsonPath}";
        Console.Error.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{diagnostic.SourceFile}({diagnostic.Line},{diagnostic.Column}): {diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Code}:{path} {diagnostic.Message}"));
    }
}

internal sealed class DirectoryContentSource(string root) : IContentSource
{
    private readonly string rootPath = Path.GetFullPath(root);

    public bool TryRead(string normalizedRelativePath, out ReadOnlyMemory<byte> content)
    {
        content = default;

        try
        {
            var relativeOperatingSystemPath = normalizedRelativePath.Replace(
                '/',
                Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(rootPath, relativeOperatingSystemPath));
            var rootPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(rootPrefix, StringComparison.Ordinal) || !File.Exists(candidate))
            {
                return false;
            }

            content = File.ReadAllBytes(candidate);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

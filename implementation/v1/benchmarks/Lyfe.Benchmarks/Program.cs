using System.Diagnostics;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.World;

const int iterationCount = 10_000;
var compiled = WorldRulesPipeline.Compile(
    new CopiedContentSource("OfficialRules"),
    new CopiedContentSource("OfficialWorld"),
    "scenario.foundation-sandbox",
    "world.primordial-foundation");
if (!compiled.IsSuccess || compiled.WorldRules is null)
{
    throw new InvalidOperationException(string.Join(
        "; ",
        compiled.Diagnostics.Select(diagnostic =>
            $"{diagnostic.SourceFile}:{diagnostic.Code}:{diagnostic.Message}")));
}

var runner = WorldRunner.CreateFoundation(
    WorldId.From(1),
    compiled.WorldRules,
    RootRandomSeed.Parse("00000000000000000000000000000001"));
var stopwatch = Stopwatch.StartNew();

for (var index = 0; index < iterationCount; index++)
{
    runner.AdvanceOneTick();
}

stopwatch.Stop();
Console.WriteLine(
    $"Scalar 12-phase/100-organism tick with first capture ledger: {iterationCount:N0} ticks in " +
    $"{stopwatch.Elapsed.TotalMilliseconds:N2} ms. Biological systems remain incomplete.");

internal sealed class CopiedContentSource(string directoryName) : IContentSource
{
    private readonly string root = Path.Combine(AppContext.BaseDirectory, directoryName);

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

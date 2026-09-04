using System.Globalization;
using System.Text.Json;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.World;

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
var creationStateHash = runner.CaptureSnapshot().StateHash;
string? firstTickStateHash = null;

for (var tick = 0; tick < 10; tick++)
{
    var tickResult = runner.AdvanceOneTick();
    firstTickStateHash ??= tickResult.Snapshot.StateHash;
}

var snapshot = runner.CaptureSnapshot();
var lastChanges = runner.LastCompletedTickChanges;
var result = new
{
    scenario = "scalar-tick-smoke",
    worldId = snapshot.WorldId.ToString(),
    completedTick = snapshot.CompletedTick.ToString(CultureInfo.InvariantCulture),
    worldRevision = snapshot.WorldRevision.ToString(CultureInfo.InvariantCulture),
    simulatedHours = snapshot.SimulatedHours.ToString(CultureInfo.InvariantCulture),
    organismCount = snapshot.OrganismCount,
    phaseCount = lastChanges?.Phases.Length,
    mergedStoreChangeCount = lastChanges?.MergedChanges.StoreChanges.Length,
    mergedStructuralChangeCount = lastChanges?.MergedChanges.StructuralChangeCount,
    mergedDirtyEntityReferenceCount = lastChanges?.MergedChanges.DirtyEntityReferenceCount,
    resourceTransactionCount = lastChanges?.MergedChanges.ResourceTransactionReferences.Length,
    creationStateHash,
    firstTickStateHash,
    snapshot.StateHash,
};

Console.WriteLine(JsonSerializer.Serialize(result));

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

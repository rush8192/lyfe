using Lyfe.Simulation.Core;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World;
using Lyfe.Simulation.World.Generation;
using System.Globalization;

internal static class FounderAllocationMatrix
{
    private const uint FounderPopulation = 100;
    private const long FirstMeaningfulMutationCostQ = 40_000_000;

    public static object Run(
        int seedCount,
        int horizonHours,
        int parallelism,
        bool includeRuns)
    {
        var compiled = WorldRulesPipeline.Compile(
            new CopiedContentSource("OfficialRules"),
            new CopiedContentSource("OfficialGeneratedWorld"),
            "scenario.foundation-sandbox",
            "world.primordial-earth-v1");
        if (!compiled.IsSuccess || compiled.WorldRules is null)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                compiled.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.SourceFile}:{diagnostic.Code}:{diagnostic.Message}")));
        }

        var rules = compiled.WorldRules;
        var cases = new List<FounderAllocationCase>();
        foreach (var founder in rules.RulePack.FounderPhenotypes
                     .OrderBy(value => value.FounderGenomeId.Value))
        {
            foreach (var allocation in rules.RulePack.FounderAllocations
                         .OrderBy(value => value.Id.Value))
            {
                var runs = new FounderAllocationRun[seedCount];
                var firstWorldId = checked((ulong)cases.Count * (ulong)seedCount + 1UL);
                Parallel.For(
                    1,
                    seedCount + 1,
                    new ParallelOptions { MaxDegreeOfParallelism = parallelism },
                    seedOrdinal =>
                {
                    var seed = RootRandomSeed.Parse(seedOrdinal.ToString("x32", CultureInfo.InvariantCulture));
                    var generated = DeterministicWorldGenerator.Generate(rules.WorldProfile, seed);
                    var pair = generated.StartingPairs[0];
                    var playerIsHydrogen = founder.FounderGenomeId.Value == 1;
                    var playerTile = playerIsHydrogen
                        ? pair.HydrogenTileIndex
                        : pair.SulfurTileIndex;
                    var competitorTile = playerIsHydrogen
                        ? pair.SulfurTileIndex
                        : pair.HydrogenTileIndex;
                    var competitorFounder = FounderGenomeId.From(playerIsHydrogen ? 2U : 1U);
                    var runner = WorldRunner.CreateGame(
                        WorldId.From(checked(firstWorldId + (ulong)seedOrdinal - 1UL)),
                        rules,
                        seed,
                        new GameSetupCommand(
                            GameMode.Survival,
                            founder.FounderGenomeId,
                            TileId.FromRowMajorIndex(playerTile),
                            FounderPopulation,
                            competitorFounder,
                            TileId.FromRowMajorIndex(competitorTile),
                            PlayerFounderAllocationId: allocation.Id));

                    runs[seedOrdinal - 1] = RunOne(runner, seedOrdinal, horizonHours);
                });

                cases.Add(new FounderAllocationCase(
                    founder.FounderGenomeId.Value,
                    allocation.Id.Value,
                    allocation.StableKey,
                    runs.Count(run => run.ReachedFirstReproduction),
                    runs.Count(run => run.FounderSurvivorsAtFirstReproduction >= 95),
                    runs.Count(run => run.ReachedMutationDecision),
                    runs.Min(run => run.FounderSurvivorsAtFirstReproduction),
                    runs.Max(run => run.FirstReproductionHour ?? 0),
                    runs.Max(run => run.MutationDecisionHour ?? 0),
                    includeRuns ? runs : null));
            }
        }

        return new
        {
            Scenario = "founder-allocation-generated-world-matrix",
            SeedCount = seedCount,
            HorizonHours = horizonHours,
            Parallelism = parallelism,
            FounderPopulation,
            RequiredFounderSurvivors = 95,
            FirstMeaningfulMutationCostQ,
            Passed = cases.All(value =>
                value.ReproductionReachedCount == seedCount &&
                value.CohortSurvivalPassCount == seedCount &&
                value.MutationDecisionReachedCount == seedCount),
            Cases = cases,
        };
    }

    private static FounderAllocationRun RunOne(
        WorldRunner runner,
        int seedOrdinal,
        int horizonHours)
    {
        var speciesId = runner.GameState.ControlledSpeciesId;
        ulong? reproductionHour = null;
        ulong? mutationHour = null;
        var survivors = 0;

        for (var hour = 1; hour <= horizonHours; hour++)
        {
            runner.AdvanceOneTick();
            var species = runner.MutableWorld.GetSpecies(speciesId);
            var organisms = runner.MutableWorld.GetOrganismIdsInCanonicalOrder()
                .Select(runner.MutableWorld.GetOrganism)
                .Where(organism => organism.SpeciesId == speciesId)
                .ToArray();

            if (!reproductionHour.HasValue &&
                organisms.Any(organism => organism.SuccessfulReproductionCount > 0))
            {
                reproductionHour = checked((ulong)hour);
                survivors = organisms.Count(organism => organism.BirthTick == 0);
            }
            if (!mutationHour.HasValue && species.Evolution.MutationBalanceQ >= FirstMeaningfulMutationCostQ)
            {
                mutationHour = checked((ulong)hour);
            }
            if (reproductionHour.HasValue && mutationHour.HasValue)
            {
                break;
            }
            if (runner.GameState.RunStatus != GameRunStatus.Active)
            {
                break;
            }
        }

        var finalSpecies = runner.MutableWorld.GetSpecies(speciesId);
        return new FounderAllocationRun(
            seedOrdinal,
            reproductionHour.HasValue,
            reproductionHour,
            survivors,
            mutationHour.HasValue,
            mutationHour,
            finalSpecies.Population,
            finalSpecies.Evolution.AverageHealthQ,
            runner.CaptureSnapshot().StateHash);
    }

    private sealed record FounderAllocationCase(
        uint FounderGenomeId,
        uint FounderAllocationId,
        string FounderAllocationKey,
        int ReproductionReachedCount,
        int CohortSurvivalPassCount,
        int MutationDecisionReachedCount,
        int MinimumFounderSurvivorsAtFirstReproduction,
        ulong LatestFirstReproductionHour,
        ulong LatestMutationDecisionHour,
        IReadOnlyList<FounderAllocationRun>? Runs);

    private sealed record FounderAllocationRun(
        int SeedOrdinal,
        bool ReachedFirstReproduction,
        ulong? FirstReproductionHour,
        int FounderSurvivorsAtFirstReproduction,
        bool ReachedMutationDecision,
        ulong? MutationDecisionHour,
        ulong FinalPopulation,
        uint FinalAverageHealthQ,
        string StateHash);
}

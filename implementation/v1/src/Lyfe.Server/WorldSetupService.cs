using Lyfe.Simulation.Core;
using Lyfe.Server.Persistence;
using Lyfe.Simulation.Gameplay;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.World;
using Lyfe.Simulation.World.Generation;
using Proto = Lyfe.Protocol.V1;

namespace Lyfe.Server;

public sealed class WorldSetupService(
    CompiledWorldRules rules,
    WorldClockService clock,
    WorldCatalogueService catalogue)
{
    public const string DefaultSeed = "00112233445566778899aabbccddeeff";
    public const uint FounderPopulation = WorldRunner.DefaultFoundationPopulation;

    private readonly object gate = new();

    public Proto.WorldSetupSurface Capture(string? seedText)
    {
        var seed = ParseSeed(seedText);
        var generated = DeterministicWorldGenerator.Generate(rules.WorldProfile, seed);
        var scenario = rules.RulePack.Scenarios[rules.Scenario.DenseSlot];
        var result = new Proto.WorldSetupSurface
        {
            HasActiveWorld = clock.HasActiveWorld,
            RootSeedHex = seed.ToCanonicalString(),
            WorldPackId = rules.Identity.WorldPackId,
            WorldPackVersion = rules.Identity.WorldPackVersion,
            WorldProfileKey = rules.WorldProfile.WorldProfileKey,
            WorldProfileDisplayName = rules.WorldProfile.DisplayName,
            Width = rules.WorldProfile.Width,
            Height = rules.WorldProfile.Height,
            TickDurationHours = rules.TickDurationHours,
            FounderPopulation = FounderPopulation,
        };
        result.Founders.Add(scenario.PermittedFounders
            .OrderBy(value => value.Id.Value)
            .Select(value => ToProtocol(rules.RulePack.FounderPhenotypes.Single(
                candidate => candidate.FounderGenomeId == value.Id))));
        result.Allocations.Add(scenario.PermittedFounderAllocations
            .OrderBy(value => value.Id.Value)
            .Select(value => ToProtocol(rules.RulePack.FounderAllocations.Single(
                candidate => candidate.Id == value.Id))));
        result.StartingRegions.Add(generated.StartingPairs
            .Select((pair, index) => ToProtocol(generated, pair, checked((uint)index))));
        return result;
    }

    public Proto.CreateWorldResponse Create(Proto.CreateWorldRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            if (clock.HasActiveWorld && !request.ConfirmReplaceActive)
            {
                throw new WorldControlRejectedException(
                    "Replacing the active world requires explicit confirmation.");
            }
            var seed = RootRandomSeed.Parse(request.RootSeedHex);
            var mode = request.Mode switch
            {
                Proto.GameMode.FreeSandbox => GameMode.FreeSandbox,
                Proto.GameMode.Survival => GameMode.Survival,
                _ => throw new ArgumentException("A supported game mode is required.", nameof(request)),
            };
            var scenario = rules.RulePack.Scenarios[rules.Scenario.DenseSlot];
            var founder = scenario.PermittedFounders.SingleOrDefault(value =>
                value.Id.Value == request.FounderGenomeId);
            var allocation = scenario.PermittedFounderAllocations.SingleOrDefault(value =>
                value.Id.Value == request.FounderAllocationId);
            if (founder == default || allocation == default)
            {
                throw new ArgumentException(
                    "The founder genome or allocation is not permitted by this scenario.",
                    nameof(request));
            }

            var generated = DeterministicWorldGenerator.Generate(rules.WorldProfile, seed);
            if (request.StartingPairIndex >= generated.StartingPairs.Length)
            {
                throw new ArgumentException("The selected starting region does not exist.",
                    nameof(request));
            }
            var pair = generated.StartingPairs[checked((int)request.StartingPairIndex)];
            var playerPhenotype = rules.RulePack.FounderPhenotypes.Single(value =>
                value.FounderGenomeId == founder.Id);
            var playerTile = PlayerTile(pair, playerPhenotype.Physiology.OpeningMetabolism.Kind);

            FounderGenomeId? competitorFounderId = null;
            TileId? competitorTile = null;
            if (mode == GameMode.Survival)
            {
                var competitor = scenario.PermittedFounders
                    .Select(value => rules.RulePack.FounderPhenotypes.Single(candidate =>
                        candidate.FounderGenomeId == value.Id))
                    .Single(value => value.Physiology.OpeningMetabolism.Kind !=
                        playerPhenotype.Physiology.OpeningMetabolism.Kind);
                competitorFounderId = competitor.FounderGenomeId;
                competitorTile = PlayerTile(pair, competitor.Physiology.OpeningMetabolism.Kind);
            }

            var worldId = catalogue.ReserveWorldId();
            var runner = WorldRunner.CreateGame(
                worldId,
                rules,
                seed,
                new GameSetupCommand(
                    mode,
                    founder.Id,
                    playerTile,
                    FounderPopulation,
                    competitorFounderId,
                    competitorTile,
                    allocation.Id,
                    mode == GameMode.Survival
                        ? scenario.DefaultCompetitorFounderAllocation.Id
                        : null));
            clock.ReplacePaused(runner, request.ConfirmReplaceActive);
            return new Proto.CreateWorldResponse { WorldId = worldId.Value };
        }
    }

    private static RootRandomSeed ParseSeed(string? value) =>
        RootRandomSeed.Parse(string.IsNullOrWhiteSpace(value) ? DefaultSeed : value);

    private static TileId PlayerTile(
        StartingTilePair pair,
        FoundingMetabolismKind metabolism) => TileId.FromRowMajorIndex(
        metabolism == FoundingMetabolismKind.HydrogenAcetogenesis
            ? pair.HydrogenTileIndex
            : pair.SulfurTileIndex);

    private static Proto.FounderSetupOption ToProtocol(CompiledPhenotype value)
    {
        var metabolism = value.Physiology.OpeningMetabolism;
        var reproduction = value.Physiology.Reproduction;
        return new Proto.FounderSetupOption
        {
            FounderGenomeId = value.FounderGenomeId.Value,
            StableKey = value.StableKey,
            DisplayName = value.DisplayName,
            Metabolism = metabolism.Kind switch
            {
                FoundingMetabolismKind.HydrogenAcetogenesis =>
                    Proto.FoundingMetabolism.HydrogenAcetogenesis,
                FoundingMetabolismKind.SulfideAnoxygenicPhototrophy =>
                    Proto.FoundingMetabolism.SulfidePhototrophy,
                _ => throw new InvalidOperationException("Unsupported founding metabolism."),
            },
            RequiresLight = metabolism.RequiresLight,
            FavorableCaptureEfficiencyQ = metabolism.FavorableCaptureEfficiencyQ,
            MaximumCaptureExtentsPerHour = metabolism.MaximumCaptureExtentsPerHour,
            MaintenanceCostQPerHour = metabolism.MaintenanceCostQPerHour,
            BaseReproductionCooldownHours = reproduction.BaseCooldownHours,
            ReproductionCooldownJitterMaximumHours =
                reproduction.CooldownJitterMaximumHours,
        };
    }

    private static Proto.FounderAllocationOption ToProtocol(CompiledFounderAllocation value) =>
        new()
        {
            FounderAllocationId = value.Id.Value,
            StableKey = value.StableKey,
            DisplayName = value.DisplayName,
            IsBaseline = value.IsBaseline,
            CaptureEfficiencyMultiplierQ = value.CaptureEfficiencyMultiplierQ,
            ChemicalToleranceMultiplierQ = value.ChemicalToleranceMultiplierQ,
        };

    private static Proto.StartingRegionPreview ToProtocol(
        GeneratedWorldMap generated,
        StartingTilePair pair,
        uint index)
    {
        var hydrogen = generated.GetTile(pair.HydrogenTileIndex);
        var sulfur = generated.GetTile(pair.SulfurTileIndex);
        return new Proto.StartingRegionPreview
        {
            StartingPairIndex = index,
            HydrogenDepthMeters = hydrogen.WaterDepthMeters,
            SulfurDepthMeters = sulfur.WaterDepthMeters,
            HydrogenTemperatureMilliC = WorldClimateEvaluator
                .Evaluate(generated, hydrogen, 0).TemperatureMilliC,
            SulfurTemperatureMilliC = WorldClimateEvaluator
                .Evaluate(generated, sulfur, 0).TemperatureMilliC,
            HydrogenVolcanismQ = hydrogen.BaselineVolcanismQ,
            SulfurVolcanismQ = sulfur.BaselineVolcanismQ,
            WasRepaired = pair.WasRepaired,
        };
    }
}

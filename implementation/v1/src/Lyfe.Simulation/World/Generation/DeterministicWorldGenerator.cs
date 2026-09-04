using System.Collections.Immutable;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Simulation.World.Generation;

public static class DeterministicWorldGenerator
{
    private const long Scale = FixedWorldMath.Scale;

    public static GeneratedWorldMap Generate(
        CompiledWorldProfile profile,
        RootRandomSeed seed)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var generator = profile.Generator ?? throw new ArgumentException(
            "The selected world profile is explicit and has no generator.",
            nameof(profile));
        if (profile.Tiles.Length != 0 || generator.AlgorithmVersion != 1)
        {
            throw new ArgumentException(
                "Generated v1 profiles require algorithm version 1 and no explicit tiles.",
                nameof(profile));
        }

        var random = new SemanticRandomOracle(seed);
        for (var attempt = 0U; attempt < generator.MaximumAttempts; attempt++)
        {
            var result = TryGenerate(profile, generator, random, attempt);
            if (result is not null)
            {
                return result;
            }
        }

        throw new WorldGenerationException(
            $"World profile '{profile.WorldProfileKey}' could not produce " +
            $"{generator.StartingRegions.RequiredPairs} separated founding pairs in " +
            $"{generator.MaximumAttempts} deterministic attempts.");
    }

    private static GeneratedWorldMap? TryGenerate(
        CompiledWorldProfile profile,
        CompiledWorldGenerator generator,
        ISimulationRandom random,
        uint attempt)
    {
        var tileCount = checked((int)(profile.Width * profile.Height));
        var aquaticFractionQ = SampleRange(
            random,
            0,
            1,
            attempt,
            generator.TargetAquaticFractionMinimumQ,
            generator.TargetAquaticFractionMaximumQ);
        var aquaticCount = checked((int)(((long)tileCount * aquaticFractionQ +
            (Scale / 2)) / Scale));
        var elevationScores = new (long Score, uint TileIndex)[tileCount];
        for (var index = 0; index < tileCount; index++)
        {
            var tileIndex = checked((uint)index);
            var (x, y) = WorldGridTopology.ToCoordinates(
                profile.Width,
                profile.Height,
                tileIndex);
            elevationScores[index] = (
                CombinedFieldQ(
                    profile.Width,
                    profile.Height,
                    x,
                    y,
                    generator.Elevation,
                    random,
                    attempt,
                    100),
                tileIndex);
        }

        Array.Sort(elevationScores, CompareScore);
        var elevations = AssignElevations(elevationScores, aquaticCount, generator.Elevation);
        var volcanism = AssignVolcanism(
            profile,
            generator,
            random,
            attempt,
            tileCount);
        if (!ApplyOriginGeographyBias(
                profile,
                generator,
                random,
                attempt,
                elevations,
                volcanism))
        {
            return null;
        }

        var drafts = new DraftTile[tileCount];
        for (var index = 0; index < tileCount; index++)
        {
            drafts[index] = CreateDraft(
                profile,
                generator,
                random,
                attempt,
                checked((uint)index),
                elevations[index],
                volcanism[index]);
        }

        var pairCandidates = BuildPairCandidates(
            profile,
            generator.StartingRegions,
            generator.Volcanism,
            drafts,
            random,
            attempt);
        var pairs = SelectPairs(profile, generator.StartingRegions, pairCandidates);
        if (pairs.Length != checked((int)generator.StartingRegions.RequiredPairs))
        {
            return null;
        }

        var firstHydrogenX = drafts[pairs[0].HydrogenIndex].X;
        var startSolarOffsetQ = checked((uint)FixedWorldMath.Mod(
            FixedWorldMath.QuarterTurnQ -
            (checked((long)firstHydrogenX * Scale) / profile.Width),
            Scale));
        var repairs = ApplyStartingRepairs(
            profile,
            generator,
            drafts,
            pairs,
            startSolarOffsetQ);
        var world = BuildWorld(
            profile,
            generator,
            drafts,
            pairs,
            repairs,
            attempt,
            aquaticFractionQ,
            startSolarOffsetQ);
        return ValidateStartingPairs(world, generator.StartingRegions, generator.Volcanism)
            ? world
            : null;
    }

    private static int[] AssignElevations(
        (long Score, uint TileIndex)[] ordered,
        int aquaticCount,
        CompiledElevationGenerator elevation)
    {
        var result = new int[ordered.Length];
        for (var ordinal = 0; ordinal < ordered.Length; ordinal++)
        {
            int meters;
            if (ordinal < aquaticCount)
            {
                var depthRankQ = aquaticCount == 1
                    ? 0L
                    : checked((long)ordinal * Scale / (aquaticCount - 1));
                meters = -AquaticDepth(depthRankQ, elevation.MaximumOceanDepthMeters);
            }
            else
            {
                var landCount = ordered.Length - aquaticCount;
                var landOrdinal = ordinal - aquaticCount;
                var landRankQ = landCount == 1
                    ? 0L
                    : checked((long)landOrdinal * Scale / (landCount - 1));
                meters = LandElevation(landRankQ, elevation.MaximumLandElevationMeters);
            }

            result[checked((int)ordered[ordinal].TileIndex)] = meters;
        }

        return result;
    }

    private static int AquaticDepth(long rankQ, int maximumDepth)
    {
        if (rankQ < 500_000)
        {
            var localQ = rankQ * 2;
            return checked(1_001 + (int)(((long)(maximumDepth - 1_001) *
                (Scale - localQ)) / Scale));
        }

        if (rankQ < 850_000)
        {
            var localQ = (rankQ - 500_000) * Scale / 350_000;
            return checked(201 + (int)((799 * (Scale - localQ)) / Scale));
        }

        var shallowQ = (rankQ - 850_000) * Scale / 150_000;
        return checked(1 + (int)((199 * (Scale - shallowQ)) / Scale));
    }

    private static int LandElevation(long rankQ, int maximumElevation)
    {
        if (rankQ < 600_000)
        {
            return checked(1 + (int)((999 * rankQ) / 600_000));
        }

        if (rankQ < 900_000)
        {
            return checked(1_001 + (int)((1_499 * (rankQ - 600_000)) / 300_000));
        }

        return checked(2_501 + (int)(((long)(maximumElevation - 2_501) *
            (rankQ - 900_000)) / 100_000));
    }

    private static uint[] AssignVolcanism(
        CompiledWorldProfile profile,
        CompiledWorldGenerator generator,
        ISimulationRandom random,
        uint attempt,
        int tileCount)
    {
        var minimumCount = checked((uint)(((long)tileCount *
            generator.Volcanism.TileFractionMinimumQ + Scale - 1) / Scale));
        var maximumCount = checked((uint)((long)tileCount *
            generator.Volcanism.TileFractionMaximumQ / Scale));
        var maximumInitialCount = checked(maximumCount -
            generator.StartingRegions.MaximumRepairedTiles);
        var volcanicCount = checked((int)SampleRange(
            random,
            0,
            2,
            attempt,
            minimumCount,
            maximumInitialCount));
        var ranked = new (long Score, uint TileIndex)[tileCount];
        for (var index = 0; index < tileCount; index++)
        {
            var tileIndex = checked((uint)index);
            var (x, y) = WorldGridTopology.ToCoordinates(profile.Width, profile.Height, tileIndex);
            ranked[index] = (FieldQ(profile.Width, profile.Height, x, y, 8, random, attempt, 300), tileIndex);
        }

        Array.Sort(ranked, CompareScore);
        var result = new uint[tileCount];
        for (var ordinal = 0; ordinal < volcanicCount; ordinal++)
        {
            var selected = ranked[tileCount - 1 - ordinal].TileIndex;
            result[checked((int)selected)] = SampleRange(
                random,
                selected,
                301,
                attempt,
                generator.Volcanism.TileThresholdQ,
                generator.Volcanism.FounderActivityMaximumQ);
        }

        return result;
    }

    private static DraftTile CreateDraft(
        CompiledWorldProfile profile,
        CompiledWorldGenerator generator,
        ISimulationRandom random,
        uint attempt,
        uint tileIndex,
        int elevation,
        uint volcanismQ)
    {
        var (x, y) = WorldGridTopology.ToCoordinates(profile.Width, profile.Height, tileIndex);
        var halfHeight = checked((int)profile.Height / 2);
        var latitude = checked(y * 90_000 / (halfHeight + 1));
        var anomalyFieldQ = FieldQ(profile.Width, profile.Height, x, y, 8, random, attempt, 400);
        var anomaly = checked((int)(anomalyFieldQ *
            generator.Climate.RegionalAnomalyMagnitudeMilliC / Scale));
        var precipitation = ImmutableArray.CreateBuilder<uint>(checked((int)generator.Climate.MonthsPerYear));
        var cloud = ImmutableArray.CreateBuilder<uint>(checked((int)generator.Climate.MonthsPerYear));
        var temperature = ImmutableArray.CreateBuilder<int>(checked((int)generator.Climate.MonthsPerYear));
        var annualPrecipitation = 0UL;
        for (var month = 0U; month < generator.Climate.MonthsPerYear; month++)
        {
            var precipitationFieldQ = FieldQ(
                profile.Width,
                profile.Height,
                x,
                y,
                4,
                random,
                attempt,
                checked(500U + month));
            var normalizedQ = checked((uint)((precipitationFieldQ + Scale) / 2));
            var latitudeWetnessQ = checked((uint)(Scale -
                (Math.Abs(FixedWorldMath.SinTurnQ(checked((long)latitude * Scale / 360_000))) / 2)));
            var precipitationValue = checked((uint)(
                (ulong)generator.Climate.MaximumMonthlyPrecipitationMicrometersPerHour *
                normalizedQ * latitudeWetnessQ / ((ulong)Scale * Scale)));
            precipitation.Add(precipitationValue);
            annualPrecipitation += precipitationValue;
            var precipitationQ = generator.Climate.MaximumMonthlyPrecipitationMicrometersPerHour == 0
                ? 0U
                : checked((uint)((ulong)precipitationValue * Scale /
                    generator.Climate.MaximumMonthlyPrecipitationMicrometersPerHour));
            cloud.Add(checked((uint)Math.Min(
                Scale,
                generator.Climate.CloudMinimumQ +
                FixedWorldMath.MultiplyQ(
                    generator.Climate.CloudPrecipitationContributionQ,
                    precipitationQ))));
        }

        var terrain = ClassifyTerrain(elevation);
        var annualMean = AnnualMeanTemperature(generator.Climate, latitude, elevation, volcanismQ, anomaly);
        var seasonalityQ = elevation < 0
            ? generator.Climate.AquaticSeasonalityQ
            : generator.Climate.TerrestrialSeasonalityQ;
        var absoluteLatitudeSinQ = Math.Abs(FixedWorldMath.SinTurnQ(
            checked((long)latitude * Scale / 360_000)));
        var seasonalAmplitude = checked((int)FixedWorldMath.MultiplyQ(
            generator.Climate.BaseSeasonalAmplitudeMilliC +
            FixedWorldMath.MultiplyQ(
                generator.Climate.LatitudeSeasonalAmplitudeMilliC,
                absoluteLatitudeSinQ),
            seasonalityQ));
        for (var month = 0U; month < generator.Climate.MonthsPerYear; month++)
        {
            var midpointDay = checked((month * generator.Climate.DaysPerMonth) +
                (generator.Climate.DaysPerMonth / 2));
            var seasonalQ = FixedWorldMath.SinTurnQ(
                checked((long)midpointDay * Scale / generator.Climate.DaysPerYear));
            if (y < 0)
            {
                seasonalQ = -seasonalQ;
            }

            temperature.Add(checked((int)Math.Clamp(
                annualMean + FixedWorldMath.MultiplyQ(seasonalAmplitude, seasonalQ),
                generator.Climate.MinimumTemperatureMilliC,
                generator.Climate.MaximumTemperatureMilliC)));
        }

        var meanPrecipitationQ = generator.Climate.MaximumMonthlyPrecipitationMicrometersPerHour == 0
            ? 0U
            : checked((uint)(annualPrecipitation * Scale /
                generator.Climate.MonthsPerYear /
                generator.Climate.MaximumMonthlyPrecipitationMicrometersPerHour));
        var surfaceMoistureQ = elevation < 0
            ? (uint)Scale
            : checked((uint)Math.Min(Scale, 100_000 + (meanPrecipitationQ / 2)));
        var depthQ = elevation < 0
            ? checked((uint)Math.Min(Scale, (long)(-elevation) * Scale /
                generator.Elevation.MaximumOceanDepthMeters))
            : 0U;
        var turbidityQ = elevation < 0
            ? checked((uint)Math.Min(Scale,
                40_000 + ((Scale - depthQ) / 5) + (volcanismQ / 5)))
            : 0U;
        return new DraftTile(
            tileIndex,
            x,
            y,
            latitude,
            elevation,
            terrain,
            volcanismQ,
            anomaly,
            annualMean,
            seasonalAmplitude,
            temperature.ToImmutable(),
            precipitation.ToImmutable(),
            cloud.ToImmutable(),
            surfaceMoistureQ,
            turbidityQ,
            StartingTileRole.None,
            generator.DefaultResourceQuantitiesByDenseSlot);
    }

    private static bool ApplyOriginGeographyBias(
        CompiledWorldProfile profile,
        CompiledWorldGenerator generator,
        ISimulationRandom random,
        uint attempt,
        int[] elevations,
        uint[] volcanism)
    {
        var starts = generator.StartingRegions;
        var candidates = new List<(uint First, uint Second, ulong Rank)>();
        for (var index = 0U; index < profile.Width * profile.Height; index++)
        {
            var (x, y) = WorldGridTopology.ToCoordinates(profile.Width, profile.Height, index);
            var halfHeight = checked((int)profile.Height / 2);
            var latitude = checked(y * 90_000 / (halfHeight + 1));
            var neighbor = WorldGridTopology.Neighbor(profile.Width, profile.Height, index, 1, 0);
            if (neighbor is null ||
                Math.Abs(latitude) > starts.MaximumAbsoluteLatitudeMilliDegrees ||
                elevations[checked((int)index)] >= 0 ||
                elevations[checked((int)neighbor.Value)] >= 0)
            {
                continue;
            }

            var rank = random.UniformBelow(
                RandomAddress.Create(
                    RandomDomains.WorldGenerationFieldSample,
                    index,
                    250,
                    attempt),
                uint.MaxValue);
            candidates.Add((index, neighbor.Value, rank));
        }

        var selected = new List<(uint First, uint Second)>();
        var used = new HashSet<uint>();
        foreach (var candidate in candidates
                     .OrderBy(candidate => candidate.Rank)
                     .ThenBy(candidate => candidate.First))
        {
            if (used.Contains(candidate.First) || used.Contains(candidate.Second) ||
                selected.Any(existing => WorldGridTopology.CylindricalManhattanDistance(
                    profile.Width,
                    profile.Height,
                    existing.First,
                    candidate.First) < starts.MinimumPairSeparationTiles))
            {
                continue;
            }

            selected.Add((candidate.First, candidate.Second));
            used.Add(candidate.First);
            used.Add(candidate.Second);
            if (selected.Count == starts.RequiredPairs)
            {
                break;
            }
        }

        if (selected.Count != starts.RequiredPairs)
        {
            return false;
        }

        var originActivityQ = checked(generator.Volcanism.FounderActivityTargetQ -
            Math.Min(
                generator.Volcanism.FounderActivityTargetQ,
                Math.Min(generator.StartingRegions.MaximumVolcanismRepairQ, 200_000U)));
        foreach (var pair in selected)
        {
            elevations[checked((int)pair.First)] = checked(-(
                starts.HydrogenTargetDepthMeters +
                Math.Min(starts.HydrogenMaximumDepthRepairMeters, 50)));
            elevations[checked((int)pair.Second)] = checked(-(
                starts.SulfurTargetDepthMeters +
                Math.Min(starts.SulfurMaximumDepthRepairMeters, 15)));
            volcanism[checked((int)pair.First)] = Math.Max(
                volcanism[checked((int)pair.First)],
                originActivityQ);
            volcanism[checked((int)pair.Second)] = Math.Max(
                volcanism[checked((int)pair.Second)],
                originActivityQ);
        }

        return true;
    }

    private static PairCandidate[] BuildPairCandidates(
        CompiledWorldProfile profile,
        CompiledStartingRegionGenerator starts,
        CompiledVolcanismGenerator volcanism,
        DraftTile[] tiles,
        ISimulationRandom random,
        uint attempt)
    {
        var result = new List<PairCandidate>();
        foreach (var tile in tiles)
        {
            if (!tile.IsAquatic || Math.Abs(tile.LatitudeMilliDegrees) > starts.MaximumAbsoluteLatitudeMilliDegrees)
            {
                continue;
            }

            var neighborIndex = WorldGridTopology.Neighbor(
                profile.Width,
                profile.Height,
                tile.TileIndex,
                1,
                0);
            if (neighborIndex is null)
            {
                continue;
            }

            var neighbor = tiles[checked((int)neighborIndex.Value)];
            if (!neighbor.IsAquatic ||
                Math.Abs(neighbor.LatitudeMilliDegrees) > starts.MaximumAbsoluteLatitudeMilliDegrees)
            {
                continue;
            }

            var directCost = Math.Abs(tile.WaterDepthMeters - starts.HydrogenTargetDepthMeters) +
                Math.Abs(neighbor.WaterDepthMeters - starts.SulfurTargetDepthMeters);
            var reverseCost = Math.Abs(neighbor.WaterDepthMeters - starts.HydrogenTargetDepthMeters) +
                Math.Abs(tile.WaterDepthMeters - starts.SulfurTargetDepthMeters);
            var hydrogen = directCost <= reverseCost ? tile.TileIndex : neighbor.TileIndex;
            var sulfur = directCost <= reverseCost ? neighbor.TileIndex : tile.TileIndex;
            var hydrogenTile = tiles[checked((int)hydrogen)];
            var sulfurTile = tiles[checked((int)sulfur)];
            var hydrogenDepthDelta = Math.Abs(
                hydrogenTile.WaterDepthMeters - starts.HydrogenTargetDepthMeters);
            var sulfurDepthDelta = Math.Abs(
                sulfurTile.WaterDepthMeters - starts.SulfurTargetDepthMeters);
            var hydrogenVolcanismDelta = AbsoluteDifference(
                hydrogenTile.BaselineVolcanismQ,
                volcanism.FounderActivityTargetQ);
            var sulfurVolcanismDelta = AbsoluteDifference(
                sulfurTile.BaselineVolcanismQ,
                volcanism.FounderActivityTargetQ);
            if (hydrogenDepthDelta > starts.HydrogenMaximumDepthRepairMeters ||
                sulfurDepthDelta > starts.SulfurMaximumDepthRepairMeters ||
                hydrogenVolcanismDelta > starts.MaximumVolcanismRepairQ ||
                sulfurVolcanismDelta > starts.MaximumVolcanismRepairQ)
            {
                continue;
            }

            var depthDeficitQ = checked((uint)((
                ((long)hydrogenDepthDelta * Scale / starts.HydrogenMaximumDepthRepairMeters) +
                ((long)sulfurDepthDelta * Scale / starts.SulfurMaximumDepthRepairMeters)) / 2));
            var volcanismDeficitQ = checked((uint)((
                ((ulong)hydrogenVolcanismDelta * Scale / starts.MaximumVolcanismRepairQ) +
                ((ulong)sulfurVolcanismDelta * Scale / starts.MaximumVolcanismRepairQ)) / 2));
            // V1 charges the complete light adjustment and hydrogen founding-stock
            // adjustment conservatively. Later gas/endowment slices replace these
            // fixed dimension charges with exact per-resource deficits.
            var costQ = checked((uint)(
                FixedWorldMath.MultiplyQ(depthDeficitQ, 150_000U) +
                FixedWorldMath.MultiplyQ(volcanismDeficitQ, 100_000U) +
                100_000U +
                200_000U));
            if (costQ > starts.MaximumTotalRepairCostQ)
            {
                continue;
            }

            var tieBreak = random.UniformBelow(
                RandomAddress.Create(
                    RandomDomains.WorldGenerationFieldSample,
                    tile.TileIndex,
                    700,
                    attempt),
                uint.MaxValue);
            result.Add(new PairCandidate(hydrogen, sulfur, costQ, tieBreak));
        }

        return result
            .OrderBy(candidate => candidate.RepairCostQ)
            .ThenBy(candidate => candidate.TieBreak)
            .ThenBy(candidate => candidate.HydrogenIndex)
            .ToArray();
    }

    private static PairCandidate[] SelectPairs(
        CompiledWorldProfile profile,
        CompiledStartingRegionGenerator starts,
        IEnumerable<PairCandidate> candidates)
    {
        var selected = new List<PairCandidate>(checked((int)starts.RequiredPairs));
        var used = new HashSet<uint>();
        foreach (var candidate in candidates)
        {
            if (used.Contains(candidate.HydrogenIndex) || used.Contains(candidate.SulfurIndex))
            {
                continue;
            }

            var sufficientlySeparated = selected.All(existing =>
                WorldGridTopology.CylindricalManhattanDistance(
                    profile.Width,
                    profile.Height,
                    existing.HydrogenIndex,
                    candidate.HydrogenIndex) >= starts.MinimumPairSeparationTiles);
            if (!sufficientlySeparated)
            {
                continue;
            }

            selected.Add(candidate);
            used.Add(candidate.HydrogenIndex);
            used.Add(candidate.SulfurIndex);
            if (selected.Count == starts.RequiredPairs)
            {
                break;
            }
        }

        return selected.ToArray();
    }

    private static ImmutableArray<WorldGenerationRepair> ApplyStartingRepairs(
        CompiledWorldProfile profile,
        CompiledWorldGenerator generator,
        DraftTile[] drafts,
        IReadOnlyList<PairCandidate> pairs,
        uint startSolarOffsetQ)
    {
        var repairs = ImmutableArray.CreateBuilder<WorldGenerationRepair>();
        foreach (var pair in pairs)
        {
            RepairTile(pair.HydrogenIndex, StartingTileRole.Hydrogen, generator.StartingRegions.HydrogenTargetDepthMeters);
            RepairTile(pair.SulfurIndex, StartingTileRole.Sulfur, generator.StartingRegions.SulfurTargetDepthMeters);
        }

        return repairs.ToImmutable();

        void RepairTile(uint tileIndex, StartingTileRole role, int depthMeters)
        {
            var draft = drafts[checked((int)tileIndex)];
            var originalElevation = draft.ElevationMeters;
            var originalVolcanism = draft.BaselineVolcanismQ;
            var originalAnomaly = draft.RegionalTemperatureAnomalyMilliC;
            var originalCloudQ = draft.MonthlyCloudQ[0];
            var originalTurbidityQ = draft.AquaticTurbidityQ;
            draft.ElevationMeters = -depthMeters;
            draft.Terrain = ClassifyTerrain(draft.ElevationMeters);
            draft.BaselineVolcanismQ = generator.Volcanism.FounderActivityTargetQ;
            draft.RegionalTemperatureAnomalyMilliC = 0;
            draft.AquaticTurbidityQ = 0;
            draft.MonthlyCloudQ = Enumerable
                .Repeat(100_000U, checked((int)generator.Climate.MonthsPerYear))
                .ToImmutableArray();
            RecalculateClimate(draft, generator.Climate);
            draft.StartingRole = role;
            draft.ResourceQuantitiesByDenseSlot = role == StartingTileRole.Hydrogen
                ? generator.HydrogenStartResourceQuantitiesByDenseSlot
                : generator.DefaultResourceQuantitiesByDenseSlot;

            var provisional = BuildWorld(
                profile,
                generator,
                drafts,
                [],
                [],
                0,
                0,
                startSolarOffsetQ);
            var current = WorldClimateEvaluator.Evaluate(provisional, draft.ToGenerated(), 0);
            var correction = 45_000 - current.TemperatureMilliC;
            draft.RegionalTemperatureAnomalyMilliC = checked((int)Math.Clamp(
                correction,
                -generator.Climate.RegionalAnomalyMagnitudeMilliC,
                generator.Climate.RegionalAnomalyMagnitudeMilliC));
            RecalculateClimate(draft, generator.Climate);
            repairs.Add(new WorldGenerationRepair(
                tileIndex,
                role,
                originalElevation,
                draft.ElevationMeters,
                originalVolcanism,
                draft.BaselineVolcanismQ,
                originalAnomaly,
                draft.RegionalTemperatureAnomalyMilliC,
                originalCloudQ,
                draft.MonthlyCloudQ[0],
                originalTurbidityQ,
                draft.AquaticTurbidityQ));
        }
    }

    private static void RecalculateClimate(DraftTile tile, CompiledClimateGenerator climate)
    {
        tile.AnnualMeanTemperatureMilliC = AnnualMeanTemperature(
            climate,
            tile.LatitudeMilliDegrees,
            tile.ElevationMeters,
            tile.BaselineVolcanismQ,
            tile.RegionalTemperatureAnomalyMilliC);
        var seasonalityQ = tile.IsAquatic ? climate.AquaticSeasonalityQ : climate.TerrestrialSeasonalityQ;
        var latitudeSinQ = Math.Abs(FixedWorldMath.SinTurnQ(
            checked((long)tile.LatitudeMilliDegrees * Scale / 360_000)));
        tile.SeasonalAmplitudeMilliC = checked((int)FixedWorldMath.MultiplyQ(
            climate.BaseSeasonalAmplitudeMilliC +
            FixedWorldMath.MultiplyQ(climate.LatitudeSeasonalAmplitudeMilliC, latitudeSinQ),
            seasonalityQ));
        tile.MonthlyTemperatureMilliC = Enumerable.Range(0, checked((int)climate.MonthsPerYear))
            .Select(month =>
            {
                var midpoint = checked(((uint)month * climate.DaysPerMonth) +
                    (climate.DaysPerMonth / 2));
                var seasonalQ = FixedWorldMath.SinTurnQ(
                    checked((long)midpoint * Scale / climate.DaysPerYear));
                if (tile.Y < 0)
                {
                    seasonalQ = -seasonalQ;
                }

                return checked((int)Math.Clamp(
                    tile.AnnualMeanTemperatureMilliC +
                    FixedWorldMath.MultiplyQ(tile.SeasonalAmplitudeMilliC, seasonalQ),
                    climate.MinimumTemperatureMilliC,
                    climate.MaximumTemperatureMilliC));
            })
            .ToImmutableArray();
    }

    private static GeneratedWorldMap BuildWorld(
        CompiledWorldProfile profile,
        CompiledWorldGenerator generator,
        IEnumerable<DraftTile> drafts,
        IEnumerable<PairCandidate> pairs,
        ImmutableArray<WorldGenerationRepair> repairs,
        uint attempt,
        uint aquaticFractionQ,
        uint startSolarOffsetQ) =>
        new(
            profile.WorldProfileKey,
            profile.Width,
            profile.Height,
            profile.WrapX,
            profile.WrapY,
            attempt,
            aquaticFractionQ,
            startSolarOffsetQ,
            generator.Climate,
            drafts.Select(draft => draft.ToGenerated()).ToImmutableArray(),
            pairs.Select(pair => new StartingTilePair(
                pair.HydrogenIndex,
                pair.SulfurIndex,
                true,
                pair.RepairCostQ))
                .ToImmutableArray(),
            repairs);

    private static bool ValidateStartingPairs(
        GeneratedWorldMap world,
        CompiledStartingRegionGenerator starts,
        CompiledVolcanismGenerator volcanism)
    {
        if (world.StartingPairs.Length != starts.RequiredPairs ||
            world.Repairs.Length > starts.MaximumRepairedTiles)
        {
            return false;
        }

        foreach (var pair in world.StartingPairs)
        {
            var hydrogen = world.GetTile(pair.HydrogenTileIndex);
            var sulfur = world.GetTile(pair.SulfurTileIndex);
            if (WorldGridTopology.CylindricalManhattanDistance(
                    world.Width,
                    world.Height,
                    pair.HydrogenTileIndex,
                    pair.SulfurTileIndex) != 1 ||
                !Eligible(hydrogen, starts.HydrogenMinimumDepthMeters, starts.HydrogenMaximumDepthMeters) ||
                !Eligible(sulfur, starts.SulfurMinimumDepthMeters, starts.SulfurMaximumDepthMeters))
            {
                return false;
            }

            var hydrogenLightQ = WorldClimateEvaluator.EvaluateDailyAccessibleLightQ(world, hydrogen, 0);
            var sulfurLightQ = WorldClimateEvaluator.EvaluateDailyAccessibleLightQ(world, sulfur, 0);
            if (hydrogenLightQ > starts.HydrogenMaximumDailyLightQ ||
                sulfurLightQ < starts.SulfurMinimumDailyLightQ ||
                sulfurLightQ > starts.SulfurMaximumDailyLightQ)
            {
                return false;
            }
        }

        return true;

        bool Eligible(GeneratedTile tile, int minimumDepth, int maximumDepth)
        {
            var current = WorldClimateEvaluator.Evaluate(world, tile, 0);
            return tile.WaterDepthMeters >= minimumDepth &&
                tile.WaterDepthMeters <= maximumDepth &&
                Math.Abs(tile.LatitudeMilliDegrees) <= starts.MaximumAbsoluteLatitudeMilliDegrees &&
                tile.BaselineVolcanismQ >= volcanism.FounderActivityMinimumQ &&
                tile.BaselineVolcanismQ <= volcanism.FounderActivityMaximumQ &&
                tile.AnnualMeanTemperatureMilliC >= starts.AnnualTemperatureMinimumMilliC &&
                tile.AnnualMeanTemperatureMilliC <= starts.AnnualTemperatureMaximumMilliC &&
                current.TemperatureMilliC >= starts.CurrentTemperatureMinimumMilliC &&
                current.TemperatureMilliC <= starts.CurrentTemperatureMaximumMilliC;
        }
    }

    private static int AnnualMeanTemperature(
        CompiledClimateGenerator climate,
        int latitudeMilliDegrees,
        int elevationMeters,
        uint volcanismQ,
        int anomalyMilliC)
    {
        var absoluteLatitudeSinQ = Math.Abs(FixedWorldMath.SinTurnQ(
            checked((long)latitudeMilliDegrees * Scale / 360_000)));
        var latitudeCooling = FixedWorldMath.MultiplyQ(
            climate.LatitudeCoolingMilliC,
            absoluteLatitudeSinQ);
        var lapse = elevationMeters > 0
            ? checked((long)elevationMeters * climate.LandLapseMilliCPerKilometer / 1_000)
            : 0;
        var geothermal = FixedWorldMath.MultiplyQ(climate.GeothermalMilliC, volcanismQ);
        return checked((int)(climate.EquatorialAnnualMeanMilliC -
            latitudeCooling - lapse + geothermal + anomalyMilliC));
    }

    private static GeneratedTerrain ClassifyTerrain(int elevationMeters) => elevationMeters switch
    {
        < -1_000 => GeneratedTerrain.DeepOcean,
        < -200 => GeneratedTerrain.ShelfOcean,
        < 0 => GeneratedTerrain.ShallowOcean,
        <= 1_000 => GeneratedTerrain.Lowland,
        <= 2_500 => GeneratedTerrain.Highland,
        _ => GeneratedTerrain.Mountain,
    };

    private static long CombinedFieldQ(
        uint width,
        uint height,
        int x,
        int y,
        CompiledElevationGenerator elevation,
        ISimulationRandom random,
        uint attempt,
        uint firstLayer)
    {
        var weighted = 0L;
        var weights = 0L;
        for (var index = 0; index < elevation.WavelengthsTiles.Length; index++)
        {
            var amplitude = elevation.AmplitudesQ[index];
            weighted = checked(weighted + FixedWorldMath.MultiplyQ(
                FieldQ(
                    width,
                    height,
                    x,
                    y,
                    elevation.WavelengthsTiles[index],
                    random,
                    attempt,
                    checked(firstLayer + (uint)index)),
                amplitude));
            weights += amplitude;
        }

        weighted = checked(weighted + FixedWorldMath.MultiplyQ(
            FieldQ(width, height, x, y, elevation.WavelengthsTiles[0], random, attempt, firstLayer + 20),
            elevation.ContinentalAmplitudeQ));
        weights += elevation.ContinentalAmplitudeQ;
        return weights == 0 ? 0 : checked(weighted * Scale / weights);
    }

    private static long FieldQ(
        uint width,
        uint height,
        int x,
        int y,
        uint wavelength,
        ISimulationRandom random,
        uint attempt,
        uint layer)
    {
        var row = checked(y + ((int)height / 2));
        var cellX = x / checked((int)wavelength);
        var cellY = row / checked((int)wavelength);
        var nextCellX = (cellX + 1) % checked((int)(width / wavelength));
        var nextCellY = Math.Min(
            cellY + 1,
            checked(((int)height - 1) / (int)wavelength) + 1);
        var localXQ = checked((long)(x % wavelength) * Scale / wavelength);
        var localYQ = checked((long)(row % wavelength) * Scale / wavelength);
        var smoothXQ = FixedWorldMath.SmoothStepQ(localXQ);
        var smoothYQ = FixedWorldMath.SmoothStepQ(localYQ);
        var lowerLeft = Lattice(cellX, cellY);
        var lowerRight = Lattice(nextCellX, cellY);
        var upperLeft = Lattice(cellX, nextCellY);
        var upperRight = Lattice(nextCellX, nextCellY);
        var lower = FixedWorldMath.Lerp(lowerLeft, lowerRight, smoothXQ);
        var upper = FixedWorldMath.Lerp(upperLeft, upperRight, smoothXQ);
        return FixedWorldMath.Lerp(lower, upper, smoothYQ);

        long Lattice(int latticeX, int latticeY)
        {
            var latticeId = checked((ulong)((latticeY * 2_048) + latticeX));
            var sample = random.UniformBelow(
                RandomAddress.Create(
                    RandomDomains.WorldGenerationFieldSample,
                    latticeId,
                    layer,
                    attempt),
                2_000_001);
            return checked((long)sample - Scale);
        }
    }

    private static uint SampleRange(
        ISimulationRandom random,
        ulong coordinate0,
        ulong layer,
        uint attempt,
        uint minimum,
        uint maximum)
    {
        if (minimum == maximum)
        {
            return minimum;
        }

        return checked(minimum + (uint)random.UniformBelow(
            RandomAddress.Create(
                RandomDomains.WorldGenerationFieldSample,
                coordinate0,
                layer,
                attempt),
            checked((ulong)maximum - minimum + 1)));
    }

    private static uint AbsoluteDifference(uint left, uint right) =>
        left >= right ? left - right : right - left;

    private static int CompareScore(
        (long Score, uint TileIndex) left,
        (long Score, uint TileIndex) right)
    {
        var score = left.Score.CompareTo(right.Score);
        return score != 0 ? score : left.TileIndex.CompareTo(right.TileIndex);
    }

    private sealed record PairCandidate(
        uint HydrogenIndex,
        uint SulfurIndex,
        uint RepairCostQ,
        ulong TieBreak);

    private sealed class DraftTile
    {
        public DraftTile(
            uint tileIndex,
            int x,
            int y,
            int latitudeMilliDegrees,
            int elevationMeters,
            GeneratedTerrain terrain,
            uint baselineVolcanismQ,
            int regionalTemperatureAnomalyMilliC,
            int annualMeanTemperatureMilliC,
            int seasonalAmplitudeMilliC,
            ImmutableArray<int> monthlyTemperatureMilliC,
            ImmutableArray<uint> monthlyPrecipitationMicrometersPerHour,
            ImmutableArray<uint> monthlyCloudQ,
            uint surfaceMoistureBaselineQ,
            uint aquaticTurbidityQ,
            StartingTileRole startingRole,
            ImmutableArray<long> resourceQuantitiesByDenseSlot)
        {
            TileIndex = tileIndex;
            X = x;
            Y = y;
            LatitudeMilliDegrees = latitudeMilliDegrees;
            ElevationMeters = elevationMeters;
            Terrain = terrain;
            BaselineVolcanismQ = baselineVolcanismQ;
            RegionalTemperatureAnomalyMilliC = regionalTemperatureAnomalyMilliC;
            AnnualMeanTemperatureMilliC = annualMeanTemperatureMilliC;
            SeasonalAmplitudeMilliC = seasonalAmplitudeMilliC;
            MonthlyTemperatureMilliC = monthlyTemperatureMilliC;
            MonthlyPrecipitationMicrometersPerHour = monthlyPrecipitationMicrometersPerHour;
            MonthlyCloudQ = monthlyCloudQ;
            SurfaceMoistureBaselineQ = surfaceMoistureBaselineQ;
            AquaticTurbidityQ = aquaticTurbidityQ;
            StartingRole = startingRole;
            ResourceQuantitiesByDenseSlot = resourceQuantitiesByDenseSlot;
        }

        public uint TileIndex { get; }
        public int X { get; }
        public int Y { get; }
        public int LatitudeMilliDegrees { get; }
        public int ElevationMeters { get; set; }
        public GeneratedTerrain Terrain { get; set; }
        public uint BaselineVolcanismQ { get; set; }
        public int RegionalTemperatureAnomalyMilliC { get; set; }
        public int AnnualMeanTemperatureMilliC { get; set; }
        public int SeasonalAmplitudeMilliC { get; set; }
        public ImmutableArray<int> MonthlyTemperatureMilliC { get; set; }
        public ImmutableArray<uint> MonthlyPrecipitationMicrometersPerHour { get; }
        public ImmutableArray<uint> MonthlyCloudQ { get; set; }
        public uint SurfaceMoistureBaselineQ { get; }
        public uint AquaticTurbidityQ { get; set; }
        public StartingTileRole StartingRole { get; set; }
        public ImmutableArray<long> ResourceQuantitiesByDenseSlot { get; set; }
        public bool IsAquatic => ElevationMeters < 0;
        public int WaterDepthMeters => Math.Max(0, -ElevationMeters);

        public GeneratedTile ToGenerated() => new(
            TileIndex,
            X,
            Y,
            LatitudeMilliDegrees,
            ElevationMeters,
            Terrain,
            BaselineVolcanismQ,
            RegionalTemperatureAnomalyMilliC,
            AnnualMeanTemperatureMilliC,
            SeasonalAmplitudeMilliC,
            MonthlyTemperatureMilliC,
            MonthlyPrecipitationMicrometersPerHour,
            MonthlyCloudQ,
            SurfaceMoistureBaselineQ,
            AquaticTurbidityQ,
            StartingRole,
            ResourceQuantitiesByDenseSlot);
    }
}

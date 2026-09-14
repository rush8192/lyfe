using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Loading;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Simulation.Rules.Compilation;

public static class WorldRulesCompiler
{
    private const string CompilationSource = "<world-rules-compilation>";
    private const uint SupportedWorldGenerationApiVersion = 1;

    public static WorldRulesCompilationResult Compile(
        CompiledRulePack rulePack,
        AuthoringWorldPack worldPack,
        string scenarioKey,
        string worldProfileKey)
    {
        ArgumentNullException.ThrowIfNull(rulePack);
        ArgumentNullException.ThrowIfNull(worldPack);
        ArgumentException.ThrowIfNullOrEmpty(scenarioKey);
        ArgumentException.ThrowIfNullOrEmpty(worldProfileKey);

        var diagnostics = new List<RuleDiagnostic>();
        ValidateCompatibility(rulePack, worldPack, diagnostics);

        var scenario = ResolveScenario(rulePack, scenarioKey, diagnostics);
        var profileMatches = worldPack.Profiles
            .Where(profile => string.Equals(
                profile.StableKey,
                worldProfileKey,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        var sourceProfile = profileMatches.Length == 1 ? profileMatches[0] : null;
        if (sourceProfile is null)
        {
            AddError(
                "LYFE-COMPILE-WORLD-004",
                $"World profile '{worldProfileKey}' does not exist exactly once in the selected package.",
                diagnostics);
        }

        CompiledWorldProfile? compiledProfile = null;
        if (sourceProfile is not null)
        {
            compiledProfile = CompileProfile(rulePack, sourceProfile, diagnostics);
        }

        if (scenario is null || compiledProfile is null || diagnostics.Any(IsError))
        {
            return Failure(diagnostics);
        }

        var worldPackageHash = CanonicalRuleHashWriter.HashWorldPackage(worldPack);
        var compiledWorldProfileHash = CanonicalRuleHashWriter.HashWorldProfile(
            compiledProfile,
            rulePack.Resources);
        var worldRulesHash = CanonicalRuleHashWriter.HashWorldRules(
            rulePack.Identity,
            scenario.Value.Handle,
            worldPack.Manifest.WorldPackId,
            worldPack.Manifest.Version,
            compiledProfile.WorldProfileKey,
            worldPackageHash,
            compiledWorldProfileHash);
        var identity = new WorldRulesIdentity(
            worldPack.Manifest.WorldPackId,
            worldPack.Manifest.Version,
            compiledProfile.WorldProfileKey,
            worldPackageHash,
            compiledWorldProfileHash,
            worldRulesHash);

        return new WorldRulesCompilationResult(
            new CompiledWorldRules(
                rulePack,
                scenario.Value.Handle,
                identity,
                compiledProfile,
                scenario.Value.Scenario.TickDurationHours),
            RuleDiagnosticOrdering.Sort(diagnostics));
    }

    private static void ValidateCompatibility(
        CompiledRulePack rulePack,
        AuthoringWorldPack worldPack,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (!string.Equals(
                worldPack.Manifest.CompatibleBasePackId,
                rulePack.Identity.PackId,
                StringComparison.Ordinal))
        {
            AddError(
                "LYFE-COMPILE-WORLD-001",
                $"World pack requires base pack '{worldPack.Manifest.CompatibleBasePackId}', not '{rulePack.Identity.PackId}'.",
                diagnostics);
        }

        if (worldPack.Manifest.RequiredRegistryManifestHash is null ||
            !string.Equals(
                worldPack.Manifest.RequiredRegistryManifestHash,
                rulePack.Identity.RegistryManifestHash,
                StringComparison.Ordinal))
        {
            AddError(
                "LYFE-COMPILE-WORLD-002",
                $"World pack requires registry hash '{worldPack.Manifest.RequiredRegistryManifestHash ?? "<missing>"}', but the compiled rule pack exposes '{rulePack.Identity.RegistryManifestHash}'.",
                diagnostics);
        }

        if (worldPack.Manifest.WorldGenerationApiVersion != SupportedWorldGenerationApiVersion)
        {
            AddError(
                "LYFE-COMPILE-WORLD-003",
                $"World-generation API {worldPack.Manifest.WorldGenerationApiVersion} is unsupported; expected {SupportedWorldGenerationApiVersion}.",
                diagnostics);
        }
    }

    private static (ScenarioHandle Handle, CompiledScenario Scenario)? ResolveScenario(
        CompiledRulePack rulePack,
        string scenarioKey,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var entries = rulePack.DefinitionRegistry
            .Where(entry => entry.Kind == DefinitionKind.Scenario &&
                entry.IsActive &&
                string.Equals(entry.StableKey, scenarioKey, StringComparison.Ordinal))
            .ToArray();
        if (entries.Length != 1)
        {
            AddError(
                "LYFE-COMPILE-WORLD-005",
                $"Scenario '{scenarioKey}' does not resolve to exactly one active definition.",
                diagnostics);
            return null;
        }

        for (var index = 0; index < rulePack.Scenarios.Length; index++)
        {
            var scenario = rulePack.Scenarios[index];
            if (scenario.Id.Value == entries[0].NumericId)
            {
                return (new ScenarioHandle(scenario.Id, index), scenario);
            }
        }

        AddError(
            "LYFE-COMPILE-WORLD-006",
            $"Scenario '{scenarioKey}' is active but has no compiled artifact.",
            diagnostics);
        return null;
    }

    private static CompiledWorldProfile? CompileProfile(
        CompiledRulePack rulePack,
        WorldProfileDefinition source,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var resourcesByKey = BuildResourceLookup(rulePack);
        var gasEnvironment = CompileGasEnvironment(
            rulePack, source.GasEnvironment, resourcesByKey, diagnostics);
        if (source.Generator is not null)
        {
            var generator = CompileGenerator(
                rulePack,
                source.Generator,
                resourcesByKey,
                diagnostics);
            return diagnostics.Any(IsError) || generator is null || gasEnvironment is null
                ? null
                : new CompiledWorldProfile(
                    source.StableKey,
                    source.DisplayName,
                    source.Width,
                    source.Height,
                    source.WrapX,
                    source.WrapY,
                    [],
                    gasEnvironment,
                    generator);
        }

        if (source.Tiles is null)
        {
            AddError(
                "LYFE-COMPILE-WORLD-008",
                $"World profile '{source.StableKey}' has neither compiled tiles nor a generator.",
                diagnostics);
            return null;
        }

        var tiles = ImmutableArray.CreateBuilder<CompiledTileProfile>(source.Tiles.Length);

        foreach (var tile in source.Tiles.OrderBy(tile => tile.Y).ThenBy(tile => tile.X))
        {
            var quantities = new long[rulePack.Resources.Length];
            foreach (var stock in tile.ResourceStocks)
            {
                if (!resourcesByKey.TryGetValue(stock.ResourceKey, out var resource))
                {
                    AddError(
                        "LYFE-COMPILE-WORLD-007",
                        $"Tile ({tile.X}, {tile.Y}) references unknown resource '{stock.ResourceKey}'.",
                        diagnostics);
                    continue;
                }

                quantities[resource.DenseSlot] = stock.Quantity;
            }

            var tileIndex = checked((uint)(((long)tile.Y * source.Width) + tile.X));
            var emissionSlot = -1;
            if (tile.GasEmissionProfileKey is not null)
            {
                if (gasEnvironment is not null)
                {
                    for (var index = 0; index < gasEnvironment.EmissionProfiles.Length; index++)
                    {
                        if (string.Equals(
                                gasEnvironment.EmissionProfiles[index].StableKey,
                                tile.GasEmissionProfileKey,
                                StringComparison.Ordinal))
                        {
                            emissionSlot = index;
                            break;
                        }
                    }
                }
                if (emissionSlot < 0)
                {
                    AddError(
                        "LYFE-COMPILE-WORLD-011",
                        $"Tile ({tile.X}, {tile.Y}) references unknown gas emission profile '{tile.GasEmissionProfileKey}'.",
                        diagnostics);
                }
            }

            tiles.Add(new CompiledTileProfile(
                tileIndex,
                tile.X,
                tile.Y,
                tile.ElevationMeters,
                tile.BaselineVolcanismQ,
                emissionSlot,
                ImmutableArray.Create(quantities)));
        }

        if (diagnostics.Any(IsError))
        {
            return null;
        }

        return new CompiledWorldProfile(
            source.StableKey,
            source.DisplayName,
            source.Width,
            source.Height,
            source.WrapX,
            source.WrapY,
            tiles.ToImmutable(),
            gasEnvironment!);
    }

    private static CompiledGasEnvironment? CompileGasEnvironment(
        CompiledRulePack rulePack,
        GasEnvironmentDefinition source,
        Dictionary<string, ResourceHandle> resourcesByKey,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (source.AquaticTerrestrialCompatibilityQ > 1_000_000 ||
            source.MajorMountainCompatibilityQ > 1_000_000 ||
            source.MajorMountainElevationMeters < 0)
        {
            AddError("LYFE-COMPILE-WORLD-012", "Gas compatibility values are outside their valid ranges.", diagnostics);
        }

        var gasKeys = new HashSet<string>(StringComparer.Ordinal);
        var gases = ImmutableArray.CreateBuilder<CompiledGasTransport>();
        foreach (var gas in source.Gases)
        {
            if (!gasKeys.Add(gas.ResourceKey) ||
                !resourcesByKey.TryGetValue(gas.ResourceKey, out var resource))
            {
                AddError("LYFE-COMPILE-WORLD-013", $"Gas resource '{gas.ResourceKey}' is unknown or duplicated.", diagnostics);
                continue;
            }

            if (gas.SinkRatePerMillionPerHour > 1_000_000 ||
                gas.ExchangeRatePerMillionPerEdgeHour > 100_000 ||
                gas.DiffuseSourceQuantityPerHour < 0 ||
                !Enum.TryParse<GasAccessibilityClass>(gas.AccessibilityClass, true, out var accessibility) ||
                rulePack.Resources[resource.DenseSlot].EnvironmentalPhase != EnvironmentalPhase.Gas)
            {
                AddError("LYFE-COMPILE-WORLD-014", $"Gas rule '{gas.ResourceKey}' has invalid rates or accessibility.", diagnostics);
                continue;
            }

            gases.Add(new CompiledGasTransport(
                resource,
                accessibility,
                gas.SinkRatePerMillionPerHour,
                gas.ExchangeRatePerMillionPerEdgeHour,
                gas.DiffuseSourceQuantityPerHour));
        }

        var compiledGases = gases.OrderBy(gas => gas.Resource.Id.Value).ToImmutableArray();
        var gasSlots = compiledGases.Select((gas, slot) => (gas.Resource.Id, slot))
            .ToDictionary(item => item.Id, item => item.slot);
        var profileKeys = new HashSet<string>(StringComparer.Ordinal);
        var profiles = ImmutableArray.CreateBuilder<CompiledGasEmissionProfile>();
        foreach (var profile in source.EmissionProfiles)
        {
            if (string.IsNullOrWhiteSpace(profile.StableKey) || !profileKeys.Add(profile.StableKey))
            {
                AddError("LYFE-COMPILE-WORLD-015", $"Gas emission profile '{profile.StableKey}' is empty or duplicated.", diagnostics);
                continue;
            }

            var quantities = new long[compiledGases.Length];
            var emittedResources = new HashSet<ResourceId>();
            foreach (var emission in profile.Emissions)
            {
                if (!resourcesByKey.TryGetValue(emission.ResourceKey, out var resource) ||
                    !gasSlots.TryGetValue(resource.Id, out var gasSlot) ||
                    emission.FullActivityQuantityPerHour < 0 ||
                    !emittedResources.Add(resource.Id))
                {
                    AddError("LYFE-COMPILE-WORLD-016", $"Emission '{emission.ResourceKey}' in '{profile.StableKey}' is invalid or duplicated.", diagnostics);
                    continue;
                }

                quantities[gasSlot] = emission.FullActivityQuantityPerHour;
            }

            profiles.Add(new CompiledGasEmissionProfile(profile.StableKey, ImmutableArray.Create(quantities)));
        }

        return diagnostics.Any(IsError)
            ? null
            : new CompiledGasEnvironment(
                source.AquaticTerrestrialCompatibilityQ,
                source.MajorMountainCompatibilityQ,
                source.MajorMountainElevationMeters,
                compiledGases,
                profiles.ToImmutable());
    }

    private static CompiledWorldGenerator? CompileGenerator(
        CompiledRulePack rulePack,
        WorldGeneratorDefinition source,
        IReadOnlyDictionary<string, ResourceHandle> resourcesByKey,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var defaults = CompileResourceVector(
            rulePack,
            source.DefaultResourceStocks,
            resourcesByKey,
            "defaultResourceStocks",
            diagnostics);
        var hydrogenStart = CompileResourceVector(
            rulePack,
            source.HydrogenStartResourceStocks,
            resourcesByKey,
            "hydrogenStartResourceStocks",
            diagnostics);
        if (diagnostics.Any(IsError))
        {
            return null;
        }

        var elevation = source.Elevation;
        var climate = source.Climate;
        var volcanism = source.Volcanism;
        var starts = source.StartingRegions;
        if (source.InorganicPhosphorusWeatheringQuantityPerHour < 0)
        {
            AddError(
                "LYFE-COMPILE-WORLD-021",
                "Inorganic-phosphorus weathering cannot be negative.",
                diagnostics);
            return null;
        }
        return new CompiledWorldGenerator(
            source.AlgorithmVersion,
            source.MaximumAttempts,
            source.TargetAquaticFractionMinimumQ,
            source.TargetAquaticFractionMaximumQ,
            source.InorganicPhosphorusWeatheringQuantityPerHour,
            new CompiledElevationGenerator(
                ImmutableArray.CreateRange(elevation.WavelengthsTiles),
                ImmutableArray.CreateRange(elevation.AmplitudesQ),
                elevation.ContinentalAmplitudeQ,
                elevation.MaximumOceanDepthMeters,
                elevation.MaximumLandElevationMeters),
            new CompiledClimateGenerator(
                climate.HoursPerDay,
                climate.DaysPerMonth,
                climate.MonthsPerYear,
                climate.AxialTiltMilliDegrees,
                climate.EquatorialAnnualMeanMilliC,
                climate.LatitudeCoolingMilliC,
                climate.LandLapseMilliCPerKilometer,
                climate.GeothermalMilliC,
                climate.RegionalAnomalyMagnitudeMilliC,
                climate.BaseSeasonalAmplitudeMilliC,
                climate.LatitudeSeasonalAmplitudeMilliC,
                climate.AquaticSeasonalityQ,
                climate.TerrestrialSeasonalityQ,
                climate.AquaticDiurnalAmplitudeMilliC,
                climate.TerrestrialDiurnalAmplitudeMilliC,
                climate.MinimumTemperatureMilliC,
                climate.MaximumTemperatureMilliC,
                climate.MaximumMonthlyPrecipitationMicrometersPerHour,
                climate.CloudMinimumQ,
                climate.CloudPrecipitationContributionQ,
                climate.CloudSolarAttenuationQ,
                climate.TurbidityAttenuationQ),
            new CompiledVolcanismGenerator(
                volcanism.TileFractionMinimumQ,
                volcanism.TileFractionMaximumQ,
                volcanism.TileThresholdQ,
                volcanism.FounderActivityMinimumQ,
                volcanism.FounderActivityMaximumQ,
                volcanism.FounderActivityTargetQ),
            new CompiledStartingRegionGenerator(
                starts.RequiredPairs,
                starts.MinimumPairSeparationTiles,
                starts.MaximumAbsoluteLatitudeMilliDegrees,
                starts.HydrogenMinimumDepthMeters,
                starts.HydrogenMaximumDepthMeters,
                starts.HydrogenTargetDepthMeters,
                starts.SulfurMinimumDepthMeters,
                starts.SulfurMaximumDepthMeters,
                starts.SulfurTargetDepthMeters,
                starts.AnnualTemperatureMinimumMilliC,
                starts.AnnualTemperatureMaximumMilliC,
                starts.CurrentTemperatureMinimumMilliC,
                starts.CurrentTemperatureMaximumMilliC,
                starts.HydrogenMaximumDailyLightQ,
                starts.SulfurMinimumDailyLightQ,
                starts.SulfurMaximumDailyLightQ,
                starts.MaximumRepairedTiles,
                starts.HydrogenMaximumDepthRepairMeters,
                starts.SulfurMaximumDepthRepairMeters,
                starts.MaximumVolcanismRepairQ,
                starts.MaximumTotalRepairCostQ),
            defaults,
            hydrogenStart);
    }

    private static ImmutableArray<long> CompileResourceVector(
        CompiledRulePack rulePack,
        IEnumerable<ResourceStockDefinition> stocks,
        IReadOnlyDictionary<string, ResourceHandle> resourcesByKey,
        string label,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var result = new long[rulePack.Resources.Length];
        foreach (var stock in stocks)
        {
            if (!resourcesByKey.TryGetValue(stock.ResourceKey, out var resource))
            {
                AddError(
                    "LYFE-COMPILE-WORLD-007",
                    $"{label} references unknown resource '{stock.ResourceKey}'.",
                    diagnostics);
                continue;
            }

            result[resource.DenseSlot] = stock.Quantity;
        }

        return ImmutableArray.Create(result);
    }

    private static Dictionary<string, ResourceHandle> BuildResourceLookup(CompiledRulePack rulePack)
    {
        var activeResourceKeys = rulePack.DefinitionRegistry
            .Where(entry => entry.Kind == DefinitionKind.Resource && entry.IsActive)
            .ToDictionary(entry => entry.NumericId, entry => entry.StableKey);
        var resourcesByKey = new Dictionary<string, ResourceHandle>(StringComparer.Ordinal);
        for (var index = 0; index < rulePack.Resources.Length; index++)
        {
            var resource = rulePack.Resources[index];
            if (activeResourceKeys.TryGetValue(resource.Id.Value, out var stableKey))
            {
                resourcesByKey.Add(stableKey, new ResourceHandle(resource.Id, index));
            }
        }

        return resourcesByKey;
    }

    private static void AddError(
        string code,
        string message,
        ICollection<RuleDiagnostic> diagnostics) =>
        diagnostics.Add(new RuleDiagnostic(
            RuleDiagnosticSeverity.Error,
            code,
            message,
            CompilationSource,
            1,
            1));

    private static bool IsError(RuleDiagnostic diagnostic) =>
        diagnostic.Severity == RuleDiagnosticSeverity.Error;

    private static WorldRulesCompilationResult Failure(IEnumerable<RuleDiagnostic> diagnostics) =>
        new(null, RuleDiagnosticOrdering.Sort(diagnostics));
}

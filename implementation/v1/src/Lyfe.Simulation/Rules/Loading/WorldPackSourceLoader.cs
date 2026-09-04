using Lyfe.Simulation.Rules.Authoring;

namespace Lyfe.Simulation.Rules.Loading;

public static class WorldPackSourceLoader
{
    private const string ManifestFile = "world-pack.json";
    private const uint MaximumDimension = 1024;
    private const ulong MaximumTileCount = 1024 * 1024;
    private const uint RatioScale = 1_000_000;

    public static WorldPackLoadResult Load(IContentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var diagnostics = new List<RuleDiagnostic>();
        var manifestContent = ContentSourceReader.Read(
            source,
            ManifestFile,
            ContentSourceReader.MaximumManifestBytes,
            diagnostics);
        if (manifestContent is null)
        {
            return Failure(diagnostics);
        }

        var manifest = StrictJson.Deserialize(
            ManifestFile,
            manifestContent.Value,
            RuleAuthoringJsonContext.Default.WorldPackManifest,
            diagnostics);
        if (manifest is null)
        {
            return Failure(diagnostics);
        }

        ValidateManifest(manifest, diagnostics);
        if (!ContentSourceReader.ValidateIncludedPaths(
                ManifestFile,
                manifest.ProfileFiles,
                diagnostics))
        {
            return Failure(diagnostics);
        }

        var profiles = new List<WorldProfileDefinition>();
        foreach (var path in manifest.ProfileFiles)
        {
            var content = ContentSourceReader.Read(
                source,
                path,
                ContentSourceReader.MaximumSourceBytes,
                diagnostics);
            if (content is null)
            {
                continue;
            }

            var document = StrictJson.Deserialize(
                path,
                content.Value,
                RuleAuthoringJsonContext.Default.WorldSourceDocument,
                diagnostics);
            if (document is WorldProfileDefinitionsDocument profileDocument)
            {
                AddProfiles(path, profileDocument, profiles, diagnostics);
            }
        }

        ValidateUniqueProfileKeys(profiles, diagnostics);
        if (profiles.Count == 0)
        {
            AddError(
                "LYFE-WORLD-PROFILE-001",
                "A world pack must define at least one complete profile.",
                ManifestFile,
                diagnostics);
        }

        if (diagnostics.Any(IsError))
        {
            return Failure(diagnostics);
        }

        return new WorldPackLoadResult(
            new AuthoringWorldPack(manifest, profiles.ToArray()),
            RuleDiagnosticOrdering.Sort(diagnostics));
    }

    private static void ValidateManifest(
        WorldPackManifest manifest,
        ICollection<RuleDiagnostic> diagnostics)
    {
        ValidateStableKey(manifest.WorldPackId, "world pack", ManifestFile, diagnostics);
        ValidateStableKey(manifest.CompatibleBasePackId, "compatible base pack", ManifestFile, diagnostics);

        if (string.IsNullOrWhiteSpace(manifest.Version) ||
            string.IsNullOrWhiteSpace(manifest.DisplayName))
        {
            AddError(
                "LYFE-WORLD-MANIFEST-001",
                "World-pack version and display name are required.",
                ManifestFile,
                diagnostics);
        }

        if (manifest.WorldGenerationApiVersion == 0)
        {
            AddError(
                "LYFE-WORLD-MANIFEST-002",
                "worldGenerationApiVersion must be nonzero.",
                ManifestFile,
                diagnostics);
        }

        if (manifest.RequiredRegistryManifestHash is not null &&
            !IsLowercaseHexHash(manifest.RequiredRegistryManifestHash))
        {
            AddError(
                "LYFE-WORLD-MANIFEST-003",
                "requiredRegistryManifestHash must be a 64-character lowercase hexadecimal value when present.",
                ManifestFile,
                diagnostics);
        }
    }

    private static void AddProfiles(
        string path,
        WorldProfileDefinitionsDocument document,
        List<WorldProfileDefinition> profiles,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (document.Profiles is null)
        {
            AddError("LYFE-WORLD-DOCUMENT-001", "profiles must be an array.", path, diagnostics);
            return;
        }

        foreach (var profile in document.Profiles)
        {
            profiles.Add(profile);
            ValidateProfile(path, profile, diagnostics);
        }
    }

    private static void ValidateProfile(
        string path,
        WorldProfileDefinition profile,
        ICollection<RuleDiagnostic> diagnostics)
    {
        ValidateStableKey(profile.StableKey, "world profile", path, diagnostics);
        if (string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            AddError(
                "LYFE-WORLD-PROFILE-002",
                $"World profile '{profile.StableKey}' requires a display name.",
                path,
                diagnostics);
        }

        var tileCount = (ulong)profile.Width * profile.Height;
        if (profile.Width == 0 ||
            profile.Height == 0 ||
            profile.Width > MaximumDimension ||
            profile.Height > MaximumDimension ||
            tileCount > MaximumTileCount)
        {
            AddError(
                "LYFE-WORLD-PROFILE-003",
                $"World profile '{profile.StableKey}' dimensions must be nonzero and contain at most {MaximumTileCount} tiles with neither dimension above {MaximumDimension}.",
                path,
                diagnostics);
        }

        if (!profile.WrapX || profile.WrapY)
        {
            AddError(
                "LYFE-WORLD-PROFILE-004",
                $"V1 world profile '{profile.StableKey}' must wrap east-west and must not wrap north-south.",
                path,
                diagnostics);
        }

        if ((profile.Tiles is null) == (profile.Generator is null))
        {
            AddError(
                "LYFE-WORLD-PROFILE-005",
                $"World profile '{profile.StableKey}' must supply exactly one of tiles or generator.",
                path,
                diagnostics);
            return;
        }

        if (profile.Generator is not null)
        {
            ValidateGenerator(path, profile, profile.Generator, diagnostics);
            return;
        }

        var seenCoordinates = new HashSet<(int X, int Y)>();
        foreach (var tile in profile.Tiles!)
        {
            ValidateTile(path, profile, tile, seenCoordinates, diagnostics);
        }

        if ((ulong)profile.Tiles!.Length != tileCount)
        {
            AddError(
                "LYFE-WORLD-PROFILE-006",
                $"World profile '{profile.StableKey}' must define exactly one tile for each coordinate.",
                path,
                diagnostics);
        }
    }

    private static void ValidateTile(
        string path,
        WorldProfileDefinition profile,
        TileProfileDefinition tile,
        HashSet<(int X, int Y)> seenCoordinates,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (tile.X < 0 ||
            tile.Y < 0 ||
            (uint)tile.X >= profile.Width ||
            (uint)tile.Y >= profile.Height ||
            !seenCoordinates.Add((tile.X, tile.Y)))
        {
            AddError(
                "LYFE-WORLD-TILE-001",
                $"Tile coordinate ({tile.X}, {tile.Y}) is outside profile '{profile.StableKey}' or duplicated.",
                path,
                diagnostics);
        }

        if (tile.ResourceStocks is null)
        {
            AddError(
                "LYFE-WORLD-TILE-002",
                $"Tile ({tile.X}, {tile.Y}) must supply resourceStocks.",
                path,
                diagnostics);
            return;
        }

        var seenResourceKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stock in tile.ResourceStocks)
        {
            if (!StableKey.IsValid(stock.ResourceKey) ||
                !seenResourceKeys.Add(stock.ResourceKey) ||
                stock.Quantity < 0)
            {
                AddError(
                    "LYFE-WORLD-TILE-003",
                    $"Tile ({tile.X}, {tile.Y}) has an invalid, duplicated, or negative resource stock '{stock.ResourceKey}'.",
                    path,
                    diagnostics);
            }
        }
    }

    private static void ValidateGenerator(
        string path,
        WorldProfileDefinition profile,
        WorldGeneratorDefinition generator,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (profile.Width < 8 || profile.Height < 5 || profile.Height % 2 == 0)
        {
            AddError(
                "LYFE-WORLD-GENERATOR-001",
                $"Generated profile '{profile.StableKey}' requires width >= 8 and odd height >= 5.",
                path,
                diagnostics);
        }

        if (generator.AlgorithmVersion != 1 || generator.MaximumAttempts is 0 or > 64)
        {
            AddError(
                "LYFE-WORLD-GENERATOR-002",
                "The v1 generator requires algorithmVersion 1 and 1..64 attempts.",
                path,
                diagnostics);
        }

        if (generator.TargetAquaticFractionMinimumQ > generator.TargetAquaticFractionMaximumQ ||
            generator.TargetAquaticFractionMaximumQ > RatioScale)
        {
            AddError(
                "LYFE-WORLD-GENERATOR-003",
                "The generated aquatic-fraction range must be ordered inside RatioQ.",
                path,
                diagnostics);
        }

        if (generator.Elevation is null ||
            generator.Climate is null ||
            generator.Volcanism is null ||
            generator.StartingRegions is null)
        {
            AddError(
                "LYFE-WORLD-GENERATOR-010",
                "Generator elevation, climate, volcanism, and startingRegions records are required.",
                path,
                diagnostics);
            return;
        }

        ValidateElevation(path, profile, generator.Elevation, diagnostics);
        ValidateClimate(path, generator.Climate, diagnostics);
        ValidateVolcanism(path, generator.Volcanism, diagnostics);
        ValidateStartingRegions(path, generator.StartingRegions, diagnostics);
        var tileCount = (ulong)profile.Width * profile.Height;
        var maximumVolcanicTiles = tileCount * generator.Volcanism.TileFractionMaximumQ /
            RatioScale;
        var minimumVolcanicTiles = (tileCount * generator.Volcanism.TileFractionMinimumQ +
            RatioScale - 1) / RatioScale;
        if (minimumVolcanicTiles + generator.StartingRegions.MaximumRepairedTiles >
            maximumVolcanicTiles)
        {
            AddError(
                "LYFE-WORLD-GENERATOR-011",
                "The volcanic range cannot reserve the maximum starting-tile repair budget.",
                path,
                diagnostics);
        }

        ValidateResourceStocks(path, "defaultResourceStocks", generator.DefaultResourceStocks, diagnostics);
        ValidateResourceStocks(
            path,
            "hydrogenStartResourceStocks",
            generator.HydrogenStartResourceStocks,
            diagnostics);
    }

    private static void ValidateElevation(
        string path,
        WorldProfileDefinition profile,
        ElevationGeneratorDefinition elevation,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (elevation.WavelengthsTiles is null ||
            elevation.AmplitudesQ is null ||
            elevation.WavelengthsTiles.Length is 0 or > 8 ||
            elevation.WavelengthsTiles.Length != elevation.AmplitudesQ.Length ||
            elevation.WavelengthsTiles.Any(value =>
                value == 0 || value > profile.Width || profile.Width % value != 0) ||
            elevation.AmplitudesQ.Any(value => value == 0 || value > RatioScale) ||
            elevation.ContinentalAmplitudeQ > RatioScale ||
            elevation.MaximumOceanDepthMeters is < 1 or > 20_000 ||
            elevation.MaximumLandElevationMeters is < 1 or > 20_000)
        {
            AddError(
                "LYFE-WORLD-GENERATOR-004",
                "Elevation wavelengths/amplitudes and vertical bounds are invalid for this profile.",
                path,
                diagnostics);
        }
    }

    private static void ValidateClimate(
        string path,
        ClimateGeneratorDefinition climate,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (climate.HoursPerDay is 0 or > 48 ||
            climate.DaysPerMonth is 0 or > 60 ||
            climate.MonthsPerYear is 0 or > 24 ||
            climate.AxialTiltMilliDegrees is < 0 or > 90_000 ||
            climate.RegionalAnomalyMagnitudeMilliC < 0 ||
            climate.AquaticSeasonalityQ > RatioScale ||
            climate.TerrestrialSeasonalityQ > RatioScale ||
            climate.MinimumTemperatureMilliC >= climate.MaximumTemperatureMilliC ||
            climate.MaximumMonthlyPrecipitationMicrometersPerHour > 100_000 ||
            climate.CloudMinimumQ > RatioScale ||
            climate.CloudPrecipitationContributionQ > RatioScale ||
            climate.CloudSolarAttenuationQ > RatioScale ||
            climate.TurbidityAttenuationQ > RatioScale)
        {
            AddError(
                "LYFE-WORLD-GENERATOR-005",
                "Calendar or climate coefficients are outside v1 numeric bounds.",
                path,
                diagnostics);
        }
    }

    private static void ValidateVolcanism(
        string path,
        VolcanismGeneratorDefinition volcanism,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (volcanism.TileFractionMinimumQ > volcanism.TileFractionMaximumQ ||
            volcanism.TileFractionMaximumQ > RatioScale ||
            volcanism.TileThresholdQ > RatioScale ||
            volcanism.FounderActivityMinimumQ > volcanism.FounderActivityTargetQ ||
            volcanism.FounderActivityTargetQ > volcanism.FounderActivityMaximumQ ||
            volcanism.FounderActivityMaximumQ > RatioScale)
        {
            AddError(
                "LYFE-WORLD-GENERATOR-006",
                "Volcanism fractions and founder activity ranges are invalid.",
                path,
                diagnostics);
        }
    }

    private static void ValidateStartingRegions(
        string path,
        StartingRegionGeneratorDefinition starts,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (starts.RequiredPairs == 0 ||
            starts.MinimumPairSeparationTiles == 0 ||
            starts.MaximumAbsoluteLatitudeMilliDegrees is < 0 or > 90_000 ||
            !ValidDepthRange(
                starts.HydrogenMinimumDepthMeters,
                starts.HydrogenTargetDepthMeters,
                starts.HydrogenMaximumDepthMeters) ||
            !ValidDepthRange(
                starts.SulfurMinimumDepthMeters,
                starts.SulfurTargetDepthMeters,
                starts.SulfurMaximumDepthMeters) ||
            starts.AnnualTemperatureMinimumMilliC >= starts.AnnualTemperatureMaximumMilliC ||
            starts.CurrentTemperatureMinimumMilliC >= starts.CurrentTemperatureMaximumMilliC ||
            starts.HydrogenMaximumDailyLightQ < 0 ||
            starts.SulfurMinimumDailyLightQ < 0 ||
            starts.SulfurMinimumDailyLightQ > starts.SulfurMaximumDailyLightQ ||
            (ulong)starts.MaximumRepairedTiles < (ulong)starts.RequiredPairs * 2 ||
            starts.HydrogenMaximumDepthRepairMeters <= 0 ||
            starts.SulfurMaximumDepthRepairMeters <= 0 ||
            starts.MaximumVolcanismRepairQ == 0 ||
            starts.MaximumVolcanismRepairQ > RatioScale ||
            starts.MaximumTotalRepairCostQ == 0 ||
            starts.MaximumTotalRepairCostQ > RatioScale)
        {
            AddError(
                "LYFE-WORLD-GENERATOR-007",
                "Starting-region counts, ranges, or repair ceiling are invalid.",
                path,
                diagnostics);
        }
    }

    private static bool ValidDepthRange(int minimum, int target, int maximum) =>
        minimum > 0 && minimum <= target && target <= maximum;

    private static void ValidateResourceStocks(
        string path,
        string label,
        ResourceStockDefinition[]? stocks,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (stocks is null)
        {
            AddError(
                "LYFE-WORLD-GENERATOR-008",
                $"{label} must be an array.",
                path,
                diagnostics);
            return;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (stocks.Any(stock =>
                !StableKey.IsValid(stock.ResourceKey) ||
                !keys.Add(stock.ResourceKey) ||
                stock.Quantity < 0))
        {
            AddError(
                "LYFE-WORLD-GENERATOR-009",
                $"{label} contains an invalid, duplicated, or negative resource stock.",
                path,
                diagnostics);
        }
    }

    private static void ValidateUniqueProfileKeys(
        IReadOnlyCollection<WorldProfileDefinition> profiles,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in profiles)
        {
            if (!keys.Add(profile.StableKey))
            {
                AddError(
                    "LYFE-WORLD-PROFILE-007",
                    $"World profile key '{profile.StableKey}' appears more than once.",
                    ManifestFile,
                    diagnostics);
            }
        }
    }

    private static bool IsLowercaseHexHash(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void ValidateStableKey(
        string value,
        string description,
        string path,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (!StableKey.IsValid(value))
        {
            AddError(
                "LYFE-WORLD-IDENTITY-001",
                $"The {description} key '{value}' is not a canonical stable key.",
                path,
                diagnostics);
        }
    }

    private static bool IsError(RuleDiagnostic diagnostic) =>
        diagnostic.Severity == RuleDiagnosticSeverity.Error;

    private static void AddError(
        string code,
        string message,
        string sourceFile,
        ICollection<RuleDiagnostic> diagnostics) =>
        diagnostics.Add(new RuleDiagnostic(
            RuleDiagnosticSeverity.Error,
            code,
            message,
            sourceFile,
            1,
            1));

    private static WorldPackLoadResult Failure(IEnumerable<RuleDiagnostic> diagnostics) =>
        new(null, RuleDiagnosticOrdering.Sort(diagnostics));
}

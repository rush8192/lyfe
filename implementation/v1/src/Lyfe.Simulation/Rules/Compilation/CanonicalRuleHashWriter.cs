using System.Collections.Immutable;
using System.Security.Cryptography;
using Lyfe.Simulation.Evolution;
using Lyfe.Simulation.Rules.Authoring;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.Serialization;

namespace Lyfe.Simulation.Rules.Compilation;

internal static class CanonicalRuleHashWriter
{
    // These tags are the initial RuleHashSchemaV1 compatibility vocabulary.
    private const uint MechanicsRecord = 0x1000;
    private const uint PresentationRecord = 0x1100;
    private const uint RegistryRecord = 0x1200;
    private const uint CompiledRecord = 0x1300;
    private const uint PhenotypeRecord = 0x1400;
    private const uint WorldPackageRecord = 0x2000;
    private const uint WorldProfileRecord = 0x2100;
    private const uint WorldRulesRecord = 0x2200;

    public static string HashMechanics(
        AuthoringRulePack source,
        ImmutableArray<CompiledResource> resources,
        ImmutableArray<CompiledReaction> reactions,
        ImmutableArray<CompiledTrait> traits,
        ImmutableArray<CompiledFounderAllocation> founderAllocations,
        ImmutableArray<CompiledPhenotype> founderPhenotypes,
        ImmutableArray<CompiledScenario> scenarios,
        ImmutableArray<long> effectivePopulationQ)
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(MechanicsRecord);
        WriteUInt32Field(writer, 1, source.Manifest.MechanicsHashSchemaVersion);
        WriteStringField(writer, 2, source.Manifest.PackId);
        WriteUInt32Field(writer, 3, source.Manifest.EngineRuleApiVersion);
        WriteUInt32Field(writer, 4, source.Manifest.RuleCompilerVersion);
        WriteCompiledSemanticFields(writer, resources, reactions, traits, founderAllocations, founderPhenotypes, scenarios, effectivePopulationQ);

        WriteIdentityKeys(writer, 20, source.Resources.Select(resource =>
            (resource.NumericId, resource.StableKey)));
        WriteIdentityKeys(writer, 21, source.Reactions.Select(reaction =>
            (reaction.NumericId, reaction.StableKey)));
        WriteIdentityKeys(writer, 22, source.FounderGenomes.Select(genome =>
            (genome.NumericId, genome.StableKey)));
        WriteIdentityKeys(writer, 23, source.Scenarios.Select(scenario =>
            (scenario.NumericId, scenario.StableKey)));
        WriteIdentityKeys(writer, 24, source.Traits.Select(trait =>
            (trait.NumericId, trait.StableKey)));
        WriteIdentityKeys(writer, 25, source.FounderAllocations.Select(allocation =>
            (allocation.NumericId, allocation.StableKey)));
        return Hash(writer);
    }

    public static string HashPresentation(AuthoringRulePack source)
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(PresentationRecord);
        WriteStringField(writer, 1, source.Manifest.PackId);
        WriteStringField(writer, 2, source.Manifest.Version);
        WritePresentationDefinitions(
            writer,
            10,
            source.Resources.Select(resource =>
                (resource.NumericId, resource.StableKey, resource.DisplayName)));
        WritePresentationDefinitions(
            writer,
            11,
            source.Reactions.Select(reaction =>
                (reaction.NumericId, reaction.StableKey, reaction.DisplayName)));
        WritePresentationDefinitions(
            writer,
            12,
            source.FounderGenomes.Select(genome =>
                (genome.NumericId, genome.StableKey, genome.DisplayName)));
        WritePresentationDefinitions(
            writer,
            13,
            source.Scenarios.Select(scenario =>
                (scenario.NumericId, scenario.StableKey, scenario.DisplayName)));
        WritePresentationDefinitions(
            writer,
            14,
            source.Traits.Select(trait =>
                (trait.NumericId, trait.StableKey, trait.DisplayName)));
        WritePresentationDefinitions(
            writer,
            15,
            source.FounderAllocations.Select(allocation =>
                (allocation.NumericId, allocation.StableKey, allocation.DisplayName)));
        Field(writer, 16);
        var traits = source.Traits.OrderBy(trait => trait.NumericId).ToArray();
        writer.WriteUInt32(checked((uint)traits.Length));
        foreach (var trait in traits)
        {
            writer.WriteUInt32(0x1102);
            WriteUInt32Field(writer, 1, trait.NumericId);
            var intents = (trait.StrategicIntents ?? [])
                .Order(StringComparer.Ordinal)
                .ToArray();
            Field(writer, 2);
            writer.WriteUInt32(checked((uint)intents.Length));
            foreach (var intent in intents)
            {
                writer.WriteUtf8Nfc(intent);
            }
            Field(writer, 3);
            writer.WriteBoolean(trait.ConsequenceFollowUpHours.HasValue);
            if (trait.ConsequenceFollowUpHours.HasValue)
            {
                writer.WriteUInt64(trait.ConsequenceFollowUpHours.Value);
                writer.WriteUtf8Nfc(trait.ConsequenceEvidenceKind!);
            }
        }
        return Hash(writer);
    }

    public static string HashRegistry(ImmutableArray<DefinitionRegistryEntry> registry)
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(RegistryRecord);
        Field(writer, 1);
        writer.WriteUInt32(checked((uint)registry.Length));
        foreach (var entry in registry
                     .OrderBy(entry => entry.Kind)
                     .ThenBy(entry => entry.NumericId))
        {
            writer.WriteUInt32(0x1201);
            WriteByteField(writer, 1, (byte)entry.Kind);
            WriteUInt32Field(writer, 2, entry.NumericId);
            WriteStringField(writer, 3, entry.StableKey);
            WriteBooleanField(writer, 4, entry.IsActive);
            Field(writer, 5);
            writer.WriteBoolean(entry.RemovedInVersion is not null);
            if (entry.RemovedInVersion is not null)
            {
                writer.WriteUtf8Nfc(entry.RemovedInVersion);
            }
        }

        return Hash(writer);
    }

    public static string HashCompiledArtifacts(
        uint compilerVersion,
        ImmutableArray<CompiledResource> resources,
        ImmutableArray<CompiledReaction> reactions,
        ImmutableArray<CompiledTrait> traits,
        ImmutableArray<CompiledFounderAllocation> founderAllocations,
        ImmutableArray<CompiledPhenotype> founderPhenotypes,
        ImmutableArray<CompiledScenario> scenarios,
        ImmutableArray<long> effectivePopulationQ)
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(CompiledRecord);
        WriteUInt32Field(writer, 1, compilerVersion);
        WriteCompiledSemanticFields(writer, resources, reactions, traits, founderAllocations, founderPhenotypes, scenarios, effectivePopulationQ);
        Field(writer, 20);
        writer.WriteUInt32(checked((uint)founderPhenotypes.Length));
        foreach (var phenotype in founderPhenotypes)
        {
            writer.WriteUInt32(0x1301);
            WriteUInt32Field(writer, 1, phenotype.FounderGenomeId.Value);
            WriteUInt32Field(writer, 2, phenotype.FounderAllocationId.Value);
            WriteStringField(writer, 3, phenotype.CanonicalCompiledHash);
        }

        return Hash(writer);
    }

    public static string HashPhenotype(
        string mechanicsHash,
        CompiledPhenotype phenotype)
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(PhenotypeRecord);
        WriteStringField(writer, 1, mechanicsHash);
        WriteUInt32Field(writer, 2, phenotype.FounderGenomeId.Value);
        WriteUInt32Field(writer, 8, phenotype.FounderAllocationId.Value);
        WriteUInt32Values(writer, 5, phenotype.AcquiredTraits.Select(trait => trait.Value));
        Field(writer, 3);
        writer.WriteUInt32(checked((uint)phenotype.Processes.Length));
        foreach (var process in phenotype.Processes)
        {
            writer.WriteUInt32(0x1401);
            WriteUInt32Field(writer, 1, process.Reaction.Id.Value);
        }

        WritePhysiology(writer, 4, phenotype.Physiology);
        WriteUInt32Field(writer, 6, phenotype.MutationIncomeModifierQ);
        WriteUInt32Field(writer, 7, phenotype.MaximumChangeComplexity);

        return Hash(writer);
    }

    public static string HashWorldPackage(AuthoringWorldPack source)
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(WorldPackageRecord);
        WriteStringField(writer, 1, source.Manifest.WorldPackId);
        WriteStringField(writer, 2, source.Manifest.Version);
        WriteStringField(writer, 3, source.Manifest.DisplayName);
        WriteUInt32Field(writer, 4, source.Manifest.WorldGenerationApiVersion);
        WriteStringField(writer, 5, source.Manifest.CompatibleBasePackId);
        Field(writer, 6);
        writer.WriteBoolean(source.Manifest.RequiredRegistryManifestHash is not null);
        if (source.Manifest.RequiredRegistryManifestHash is not null)
        {
            writer.WriteUtf8Nfc(source.Manifest.RequiredRegistryManifestHash);
        }

        Field(writer, 10);
        writer.WriteUInt32(checked((uint)source.Profiles.Count));
        foreach (var profile in source.Profiles.OrderBy(profile => profile.StableKey, StringComparer.Ordinal))
        {
            writer.WriteUInt32(0x2001);
            WriteStringField(writer, 1, profile.StableKey);
            WriteStringField(writer, 2, profile.DisplayName);
            WriteUInt32Field(writer, 3, profile.Width);
            WriteUInt32Field(writer, 4, profile.Height);
            WriteBooleanField(writer, 5, profile.WrapX);
            WriteBooleanField(writer, 6, profile.WrapY);
            WriteGasEnvironment(writer, 8, profile.GasEnvironment);
            Field(writer, 7);
            writer.WriteUInt32(checked((uint)(profile.Tiles?.Length ?? 0)));
            foreach (var tile in (profile.Tiles ?? []).OrderBy(tile => tile.Y).ThenBy(tile => tile.X))
            {
                writer.WriteUInt32(0x2002);
                WriteInt32Field(writer, 1, tile.X);
                WriteInt32Field(writer, 2, tile.Y);
                WriteInt32Field(writer, 3, tile.ElevationMeters);
                WriteUInt32Field(writer, 5, tile.BaselineVolcanismQ);
                Field(writer, 6);
                writer.WriteBoolean(tile.GasEmissionProfileKey is not null);
                if (tile.GasEmissionProfileKey is not null)
                {
                    writer.WriteUtf8Nfc(tile.GasEmissionProfileKey);
                }
                Field(writer, 4);
                writer.WriteUInt32(checked((uint)tile.ResourceStocks.Length));
                foreach (var stock in tile.ResourceStocks
                             .OrderBy(stock => stock.ResourceKey, StringComparer.Ordinal))
                {
                    writer.WriteUInt32(0x2003);
                    WriteStringField(writer, 1, stock.ResourceKey);
                    WriteInt64Field(writer, 2, stock.Quantity);
                }
            }

            if (profile.Generator is not null)
            {
                WriteWorldGenerator(writer, profile.Generator);
            }
        }

        return Hash(writer);
    }

    public static string HashWorldProfile(
        CompiledWorldProfile profile,
        ImmutableArray<CompiledResource> resources)
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(WorldProfileRecord);
        WriteStringField(writer, 1, profile.WorldProfileKey);
        WriteUInt32Field(writer, 2, profile.Width);
        WriteUInt32Field(writer, 3, profile.Height);
        WriteBooleanField(writer, 4, profile.WrapX);
        WriteBooleanField(writer, 5, profile.WrapY);
        Field(writer, 6);
        writer.WriteUInt32(checked((uint)profile.Tiles.Length));
        foreach (var tile in profile.Tiles)
        {
            writer.WriteUInt32(0x2101);
            WriteUInt32Field(writer, 1, tile.TileIndex);
            WriteInt32Field(writer, 2, tile.X);
            WriteInt32Field(writer, 3, tile.Y);
            WriteInt32Field(writer, 4, tile.ElevationMeters);
            WriteUInt32Field(writer, 6, tile.BaselineVolcanismQ);
            WriteInt32Field(writer, 7, tile.GasEmissionProfileSlot);
            Field(writer, 5);
            writer.WriteUInt32(checked((uint)resources.Length));
            for (var index = 0; index < resources.Length; index++)
            {
                writer.WriteUInt32(0x2102);
                WriteUInt32Field(writer, 1, resources[index].Id.Value);
                WriteInt64Field(writer, 2, tile.ResourceQuantitiesByDenseSlot[index]);
            }
        }

        WriteCompiledGasEnvironment(writer, 8, profile.GasEnvironment);

        if (profile.Generator is not null)
        {
            WriteCompiledWorldGenerator(writer, profile.Generator, resources);
        }

        return Hash(writer);
    }

    private static void WriteGasEnvironment(
        CanonicalBinaryWriter writer,
        ushort field,
        GasEnvironmentDefinition environment)
    {
        Field(writer, field);
        writer.WriteUInt32(0x2004);
        WriteUInt32Field(writer, 1, environment.AquaticTerrestrialCompatibilityQ);
        WriteUInt32Field(writer, 2, environment.MajorMountainCompatibilityQ);
        WriteInt32Field(writer, 3, environment.MajorMountainElevationMeters);
        Field(writer, 4);
        writer.WriteUInt32(checked((uint)environment.Gases.Length));
        foreach (var gas in environment.Gases.OrderBy(gas => gas.ResourceKey, StringComparer.Ordinal))
        {
            writer.WriteUInt32(0x2005);
            WriteStringField(writer, 1, gas.ResourceKey);
            WriteStringField(writer, 2, gas.AccessibilityClass);
            WriteUInt32Field(writer, 3, gas.SinkRatePerMillionPerHour);
            WriteUInt32Field(writer, 4, gas.ExchangeRatePerMillionPerEdgeHour);
            WriteInt64Field(writer, 5, gas.DiffuseSourceQuantityPerHour);
        }
        Field(writer, 5);
        writer.WriteUInt32(checked((uint)environment.EmissionProfiles.Length));
        foreach (var profile in environment.EmissionProfiles.OrderBy(profile => profile.StableKey, StringComparer.Ordinal))
        {
            writer.WriteUInt32(0x2006);
            WriteStringField(writer, 1, profile.StableKey);
            Field(writer, 2);
            writer.WriteUInt32(checked((uint)profile.Emissions.Length));
            foreach (var emission in profile.Emissions.OrderBy(emission => emission.ResourceKey, StringComparer.Ordinal))
            {
                writer.WriteUInt32(0x2007);
                WriteStringField(writer, 1, emission.ResourceKey);
                WriteInt64Field(writer, 2, emission.FullActivityQuantityPerHour);
            }
        }
    }

    private static void WriteCompiledGasEnvironment(
        CanonicalBinaryWriter writer,
        ushort field,
        CompiledGasEnvironment environment)
    {
        Field(writer, field);
        writer.WriteUInt32(0x2103);
        WriteUInt32Field(writer, 1, environment.AquaticTerrestrialCompatibilityQ);
        WriteUInt32Field(writer, 2, environment.MajorMountainCompatibilityQ);
        WriteInt32Field(writer, 3, environment.MajorMountainElevationMeters);
        Field(writer, 4);
        writer.WriteUInt32(checked((uint)environment.Gases.Length));
        foreach (var gas in environment.Gases.OrderBy(gas => gas.Resource.Id.Value))
        {
            writer.WriteUInt32(0x2104);
            WriteUInt32Field(writer, 1, gas.Resource.Id.Value);
            WriteByteField(writer, 2, (byte)gas.AccessibilityClass);
            WriteUInt32Field(writer, 3, gas.SinkRatePerMillionPerHour);
            WriteUInt32Field(writer, 4, gas.ExchangeRatePerMillionPerEdgeHour);
            WriteInt64Field(writer, 5, gas.DiffuseSourceQuantityPerHour);
        }
        Field(writer, 5);
        writer.WriteUInt32(checked((uint)environment.EmissionProfiles.Length));
        foreach (var profile in environment.EmissionProfiles.OrderBy(profile => profile.StableKey, StringComparer.Ordinal))
        {
            writer.WriteUInt32(0x2105);
            WriteStringField(writer, 1, profile.StableKey);
            Field(writer, 2);
            writer.WriteUInt32(checked((uint)profile.FullActivityQuantitiesPerHourByGasSlot.Length));
            foreach (var quantity in profile.FullActivityQuantitiesPerHourByGasSlot)
            {
                writer.WriteInt64(quantity);
            }
        }
    }

    private static void WriteWorldGenerator(
        CanonicalBinaryWriter writer,
        WorldGeneratorDefinition generator)
    {
        Field(writer, 20);
        writer.WriteUInt32(0x2200);
        WriteUInt32Field(writer, 1, generator.AlgorithmVersion);
        WriteUInt32Field(writer, 2, generator.MaximumAttempts);
        WriteUInt32Field(writer, 3, generator.TargetAquaticFractionMinimumQ);
        WriteUInt32Field(writer, 4, generator.TargetAquaticFractionMaximumQ);
        WriteInt64Field(writer, 11, generator.InorganicPhosphorusWeatheringQuantityPerHour);
        WriteElevation(writer, generator.Elevation);
        WriteClimate(writer, generator.Climate);
        WriteVolcanism(writer, generator.Volcanism);
        WriteStartingRegions(writer, generator.StartingRegions);
        WriteResourceStocks(writer, 9, generator.DefaultResourceStocks);
        WriteResourceStocks(writer, 10, generator.HydrogenStartResourceStocks);
    }

    private static void WriteCompiledWorldGenerator(
        CanonicalBinaryWriter writer,
        CompiledWorldGenerator generator,
        ImmutableArray<CompiledResource> resources)
    {
        Field(writer, 20);
        writer.WriteUInt32(0x2210);
        WriteUInt32Field(writer, 1, generator.AlgorithmVersion);
        WriteUInt32Field(writer, 2, generator.MaximumAttempts);
        WriteUInt32Field(writer, 3, generator.TargetAquaticFractionMinimumQ);
        WriteUInt32Field(writer, 4, generator.TargetAquaticFractionMaximumQ);
        WriteInt64Field(writer, 11, generator.InorganicPhosphorusWeatheringQuantityPerHour);
        WriteCompiledElevation(writer, generator.Elevation);
        WriteCompiledClimate(writer, generator.Climate);
        WriteCompiledVolcanism(writer, generator.Volcanism);
        WriteCompiledStartingRegions(writer, generator.StartingRegions);
        WriteCompiledResourceVector(
            writer,
            9,
            generator.DefaultResourceQuantitiesByDenseSlot,
            resources);
        WriteCompiledResourceVector(
            writer,
            10,
            generator.HydrogenStartResourceQuantitiesByDenseSlot,
            resources);
    }

    public static string HashWorldRules(
        RulePackIdentity ruleIdentity,
        ScenarioHandle scenario,
        string worldPackId,
        string worldPackVersion,
        string profileKey,
        string worldPackageHash,
        string compiledWorldProfileHash)
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(WorldRulesRecord);
        WriteStringField(writer, 1, ruleIdentity.MechanicsHash);
        WriteStringField(writer, 2, ruleIdentity.RegistryManifestHash);
        WriteUInt32Field(writer, 3, scenario.Id.Value);
        WriteStringField(writer, 4, worldPackId);
        WriteStringField(writer, 5, worldPackVersion);
        WriteStringField(writer, 6, profileKey);
        WriteStringField(writer, 7, worldPackageHash);
        WriteStringField(writer, 8, compiledWorldProfileHash);
        return Hash(writer);
    }

    private static void WriteElevation(
        CanonicalBinaryWriter writer,
        ElevationGeneratorDefinition value)
    {
        Field(writer, 5);
        writer.WriteUInt32(0x2201);
        WriteUInt32Array(writer, 1, value.WavelengthsTiles);
        WriteUInt32Array(writer, 2, value.AmplitudesQ);
        WriteUInt32Field(writer, 3, value.ContinentalAmplitudeQ);
        WriteInt32Field(writer, 4, value.MaximumOceanDepthMeters);
        WriteInt32Field(writer, 5, value.MaximumLandElevationMeters);
    }

    private static void WriteCompiledElevation(
        CanonicalBinaryWriter writer,
        CompiledElevationGenerator value)
    {
        Field(writer, 5);
        writer.WriteUInt32(0x2211);
        WriteUInt32Array(writer, 1, value.WavelengthsTiles);
        WriteUInt32Array(writer, 2, value.AmplitudesQ);
        WriteUInt32Field(writer, 3, value.ContinentalAmplitudeQ);
        WriteInt32Field(writer, 4, value.MaximumOceanDepthMeters);
        WriteInt32Field(writer, 5, value.MaximumLandElevationMeters);
    }

    private static void WriteClimate(
        CanonicalBinaryWriter writer,
        ClimateGeneratorDefinition value)
    {
        Field(writer, 6);
        writer.WriteUInt32(0x2202);
        WriteClimateValues(
            writer,
            value.HoursPerDay,
            value.DaysPerMonth,
            value.MonthsPerYear,
            value.AxialTiltMilliDegrees,
            value.EquatorialAnnualMeanMilliC,
            value.LatitudeCoolingMilliC,
            value.LandLapseMilliCPerKilometer,
            value.GeothermalMilliC,
            value.RegionalAnomalyMagnitudeMilliC,
            value.BaseSeasonalAmplitudeMilliC,
            value.LatitudeSeasonalAmplitudeMilliC,
            value.AquaticSeasonalityQ,
            value.TerrestrialSeasonalityQ,
            value.AquaticDiurnalAmplitudeMilliC,
            value.TerrestrialDiurnalAmplitudeMilliC,
            value.MinimumTemperatureMilliC,
            value.MaximumTemperatureMilliC,
            value.MaximumMonthlyPrecipitationMicrometersPerHour,
            value.CloudMinimumQ,
            value.CloudPrecipitationContributionQ,
            value.CloudSolarAttenuationQ,
            value.TurbidityAttenuationQ);
    }

    private static void WriteCompiledClimate(
        CanonicalBinaryWriter writer,
        CompiledClimateGenerator value)
    {
        Field(writer, 6);
        writer.WriteUInt32(0x2212);
        WriteClimateValues(
            writer,
            value.HoursPerDay,
            value.DaysPerMonth,
            value.MonthsPerYear,
            value.AxialTiltMilliDegrees,
            value.EquatorialAnnualMeanMilliC,
            value.LatitudeCoolingMilliC,
            value.LandLapseMilliCPerKilometer,
            value.GeothermalMilliC,
            value.RegionalAnomalyMagnitudeMilliC,
            value.BaseSeasonalAmplitudeMilliC,
            value.LatitudeSeasonalAmplitudeMilliC,
            value.AquaticSeasonalityQ,
            value.TerrestrialSeasonalityQ,
            value.AquaticDiurnalAmplitudeMilliC,
            value.TerrestrialDiurnalAmplitudeMilliC,
            value.MinimumTemperatureMilliC,
            value.MaximumTemperatureMilliC,
            value.MaximumMonthlyPrecipitationMicrometersPerHour,
            value.CloudMinimumQ,
            value.CloudPrecipitationContributionQ,
            value.CloudSolarAttenuationQ,
            value.TurbidityAttenuationQ);
    }

    private static void WriteClimateValues(
        CanonicalBinaryWriter writer,
        uint hoursPerDay,
        uint daysPerMonth,
        uint monthsPerYear,
        int axialTiltMilliDegrees,
        int equatorialAnnualMeanMilliC,
        int latitudeCoolingMilliC,
        int landLapseMilliCPerKilometer,
        int geothermalMilliC,
        int regionalAnomalyMagnitudeMilliC,
        int baseSeasonalAmplitudeMilliC,
        int latitudeSeasonalAmplitudeMilliC,
        uint aquaticSeasonalityQ,
        uint terrestrialSeasonalityQ,
        int aquaticDiurnalAmplitudeMilliC,
        int terrestrialDiurnalAmplitudeMilliC,
        int minimumTemperatureMilliC,
        int maximumTemperatureMilliC,
        uint maximumMonthlyPrecipitationMicrometersPerHour,
        uint cloudMinimumQ,
        uint cloudPrecipitationContributionQ,
        uint cloudSolarAttenuationQ,
        uint turbidityAttenuationQ)
    {
        WriteUInt32Field(writer, 1, hoursPerDay);
        WriteUInt32Field(writer, 2, daysPerMonth);
        WriteUInt32Field(writer, 3, monthsPerYear);
        WriteInt32Field(writer, 4, axialTiltMilliDegrees);
        WriteInt32Field(writer, 5, equatorialAnnualMeanMilliC);
        WriteInt32Field(writer, 6, latitudeCoolingMilliC);
        WriteInt32Field(writer, 7, landLapseMilliCPerKilometer);
        WriteInt32Field(writer, 8, geothermalMilliC);
        WriteInt32Field(writer, 9, regionalAnomalyMagnitudeMilliC);
        WriteInt32Field(writer, 10, baseSeasonalAmplitudeMilliC);
        WriteInt32Field(writer, 11, latitudeSeasonalAmplitudeMilliC);
        WriteUInt32Field(writer, 12, aquaticSeasonalityQ);
        WriteUInt32Field(writer, 13, terrestrialSeasonalityQ);
        WriteInt32Field(writer, 14, aquaticDiurnalAmplitudeMilliC);
        WriteInt32Field(writer, 15, terrestrialDiurnalAmplitudeMilliC);
        WriteInt32Field(writer, 16, minimumTemperatureMilliC);
        WriteInt32Field(writer, 17, maximumTemperatureMilliC);
        WriteUInt32Field(writer, 18, maximumMonthlyPrecipitationMicrometersPerHour);
        WriteUInt32Field(writer, 19, cloudMinimumQ);
        WriteUInt32Field(writer, 20, cloudPrecipitationContributionQ);
        WriteUInt32Field(writer, 21, cloudSolarAttenuationQ);
        WriteUInt32Field(writer, 22, turbidityAttenuationQ);
    }

    private static void WriteVolcanism(
        CanonicalBinaryWriter writer,
        VolcanismGeneratorDefinition value)
    {
        Field(writer, 7);
        writer.WriteUInt32(0x2203);
        WriteVolcanismValues(
            writer,
            value.TileFractionMinimumQ,
            value.TileFractionMaximumQ,
            value.TileThresholdQ,
            value.FounderActivityMinimumQ,
            value.FounderActivityMaximumQ,
            value.FounderActivityTargetQ);
    }

    private static void WriteCompiledVolcanism(
        CanonicalBinaryWriter writer,
        CompiledVolcanismGenerator value)
    {
        Field(writer, 7);
        writer.WriteUInt32(0x2213);
        WriteVolcanismValues(
            writer,
            value.TileFractionMinimumQ,
            value.TileFractionMaximumQ,
            value.TileThresholdQ,
            value.FounderActivityMinimumQ,
            value.FounderActivityMaximumQ,
            value.FounderActivityTargetQ);
    }

    private static void WriteVolcanismValues(
        CanonicalBinaryWriter writer,
        uint minimumQ,
        uint maximumQ,
        uint thresholdQ,
        uint founderMinimumQ,
        uint founderMaximumQ,
        uint founderTargetQ)
    {
        WriteUInt32Field(writer, 1, minimumQ);
        WriteUInt32Field(writer, 2, maximumQ);
        WriteUInt32Field(writer, 3, thresholdQ);
        WriteUInt32Field(writer, 4, founderMinimumQ);
        WriteUInt32Field(writer, 5, founderMaximumQ);
        WriteUInt32Field(writer, 6, founderTargetQ);
    }

    private static void WriteStartingRegions(
        CanonicalBinaryWriter writer,
        StartingRegionGeneratorDefinition value)
    {
        Field(writer, 8);
        writer.WriteUInt32(0x2204);
        WriteStartingRegionValues(
            writer,
            value.RequiredPairs,
            value.MinimumPairSeparationTiles,
            value.MaximumAbsoluteLatitudeMilliDegrees,
            value.HydrogenMinimumDepthMeters,
            value.HydrogenMaximumDepthMeters,
            value.HydrogenTargetDepthMeters,
            value.SulfurMinimumDepthMeters,
            value.SulfurMaximumDepthMeters,
            value.SulfurTargetDepthMeters,
            value.AnnualTemperatureMinimumMilliC,
            value.AnnualTemperatureMaximumMilliC,
            value.CurrentTemperatureMinimumMilliC,
            value.CurrentTemperatureMaximumMilliC,
            value.HydrogenMaximumDailyLightQ,
            value.SulfurMinimumDailyLightQ,
            value.SulfurMaximumDailyLightQ,
            value.MaximumRepairedTiles,
            value.HydrogenMaximumDepthRepairMeters,
            value.SulfurMaximumDepthRepairMeters,
            value.MaximumVolcanismRepairQ,
            value.MaximumTotalRepairCostQ);
    }

    private static void WriteCompiledStartingRegions(
        CanonicalBinaryWriter writer,
        CompiledStartingRegionGenerator value)
    {
        Field(writer, 8);
        writer.WriteUInt32(0x2214);
        WriteStartingRegionValues(
            writer,
            value.RequiredPairs,
            value.MinimumPairSeparationTiles,
            value.MaximumAbsoluteLatitudeMilliDegrees,
            value.HydrogenMinimumDepthMeters,
            value.HydrogenMaximumDepthMeters,
            value.HydrogenTargetDepthMeters,
            value.SulfurMinimumDepthMeters,
            value.SulfurMaximumDepthMeters,
            value.SulfurTargetDepthMeters,
            value.AnnualTemperatureMinimumMilliC,
            value.AnnualTemperatureMaximumMilliC,
            value.CurrentTemperatureMinimumMilliC,
            value.CurrentTemperatureMaximumMilliC,
            value.HydrogenMaximumDailyLightQ,
            value.SulfurMinimumDailyLightQ,
            value.SulfurMaximumDailyLightQ,
            value.MaximumRepairedTiles,
            value.HydrogenMaximumDepthRepairMeters,
            value.SulfurMaximumDepthRepairMeters,
            value.MaximumVolcanismRepairQ,
            value.MaximumTotalRepairCostQ);
    }

    private static void WriteStartingRegionValues(
        CanonicalBinaryWriter writer,
        uint requiredPairs,
        uint minimumPairSeparationTiles,
        int maximumAbsoluteLatitudeMilliDegrees,
        int hydrogenMinimumDepthMeters,
        int hydrogenMaximumDepthMeters,
        int hydrogenTargetDepthMeters,
        int sulfurMinimumDepthMeters,
        int sulfurMaximumDepthMeters,
        int sulfurTargetDepthMeters,
        int annualTemperatureMinimumMilliC,
        int annualTemperatureMaximumMilliC,
        int currentTemperatureMinimumMilliC,
        int currentTemperatureMaximumMilliC,
        long hydrogenMaximumDailyLightQ,
        long sulfurMinimumDailyLightQ,
        long sulfurMaximumDailyLightQ,
        uint maximumRepairedTiles,
        int hydrogenMaximumDepthRepairMeters,
        int sulfurMaximumDepthRepairMeters,
        uint maximumVolcanismRepairQ,
        uint maximumTotalRepairCostQ)
    {
        WriteUInt32Field(writer, 1, requiredPairs);
        WriteUInt32Field(writer, 2, minimumPairSeparationTiles);
        WriteInt32Field(writer, 3, maximumAbsoluteLatitudeMilliDegrees);
        WriteInt32Field(writer, 4, hydrogenMinimumDepthMeters);
        WriteInt32Field(writer, 5, hydrogenMaximumDepthMeters);
        WriteInt32Field(writer, 6, hydrogenTargetDepthMeters);
        WriteInt32Field(writer, 7, sulfurMinimumDepthMeters);
        WriteInt32Field(writer, 8, sulfurMaximumDepthMeters);
        WriteInt32Field(writer, 9, sulfurTargetDepthMeters);
        WriteInt32Field(writer, 10, annualTemperatureMinimumMilliC);
        WriteInt32Field(writer, 11, annualTemperatureMaximumMilliC);
        WriteInt32Field(writer, 12, currentTemperatureMinimumMilliC);
        WriteInt32Field(writer, 13, currentTemperatureMaximumMilliC);
        WriteInt64Field(writer, 14, hydrogenMaximumDailyLightQ);
        WriteInt64Field(writer, 15, sulfurMinimumDailyLightQ);
        WriteInt64Field(writer, 16, sulfurMaximumDailyLightQ);
        WriteUInt32Field(writer, 17, maximumRepairedTiles);
        WriteInt32Field(writer, 18, hydrogenMaximumDepthRepairMeters);
        WriteInt32Field(writer, 19, sulfurMaximumDepthRepairMeters);
        WriteUInt32Field(writer, 20, maximumVolcanismRepairQ);
        WriteUInt32Field(writer, 21, maximumTotalRepairCostQ);
    }

    private static void WriteResourceStocks(
        CanonicalBinaryWriter writer,
        ushort fieldTag,
        IEnumerable<ResourceStockDefinition> stocks)
    {
        Field(writer, fieldTag);
        var ordered = stocks.OrderBy(stock => stock.ResourceKey, StringComparer.Ordinal).ToArray();
        writer.WriteUInt32(checked((uint)ordered.Length));
        foreach (var stock in ordered)
        {
            writer.WriteUInt32(0x2205);
            WriteStringField(writer, 1, stock.ResourceKey);
            WriteInt64Field(writer, 2, stock.Quantity);
        }
    }

    private static void WriteCompiledResourceVector(
        CanonicalBinaryWriter writer,
        ushort fieldTag,
        ImmutableArray<long> values,
        ImmutableArray<CompiledResource> resources)
    {
        Field(writer, fieldTag);
        writer.WriteUInt32(checked((uint)resources.Length));
        for (var index = 0; index < resources.Length; index++)
        {
            writer.WriteUInt32(0x2215);
            WriteUInt32Field(writer, 1, resources[index].Id.Value);
            WriteInt64Field(writer, 2, values[index]);
        }
    }

    private static void WriteUInt32Array(
        CanonicalBinaryWriter writer,
        ushort fieldTag,
        IEnumerable<uint> values)
    {
        Field(writer, fieldTag);
        var materialized = values.ToArray();
        writer.WriteUInt32(checked((uint)materialized.Length));
        foreach (var value in materialized)
        {
            writer.WriteUInt32(value);
        }
    }

    private static void WriteCompiledSemanticFields(
        CanonicalBinaryWriter writer,
        ImmutableArray<CompiledResource> resources,
        ImmutableArray<CompiledReaction> reactions,
        ImmutableArray<CompiledTrait> traits,
        ImmutableArray<CompiledFounderAllocation> founderAllocations,
        ImmutableArray<CompiledPhenotype> founderPhenotypes,
        ImmutableArray<CompiledScenario> scenarios,
        ImmutableArray<long> effectivePopulationQ)
    {
        Field(writer, 10);
        writer.WriteUInt32(checked((uint)resources.Length));
        foreach (var resource in resources)
        {
            writer.WriteUInt32(0x1001);
            WriteUInt32Field(writer, 1, resource.Id.Value);
            WriteByteField(writer, 2, (byte)resource.BiologicalForm);
            WriteByteField(writer, 3, (byte)resource.EnvironmentalPhase);
            Field(writer, 4);
            writer.WriteUInt32(checked((uint)resource.Composition.Length));
            foreach (var component in resource.Composition)
            {
                writer.WriteUInt32(0x1002);
                WriteByteField(writer, 1, (byte)component.Element);
                WriteUInt32Field(writer, 2, component.Quantity);
            }
        }

        Field(writer, 11);
        writer.WriteUInt32(checked((uint)reactions.Length));
        foreach (var reaction in reactions)
        {
            writer.WriteUInt32(0x1003);
            WriteUInt32Field(writer, 1, reaction.Id.Value);
            WriteByteField(writer, 2, (byte)reaction.ProcessKind);
            WriteTerms(writer, 3, reaction.Inputs);
            WriteTerms(writer, 4, reaction.Outputs);
            WriteInt64Field(writer, 5, reaction.GrossEnergyQ);
            WriteInt64Field(writer, 6, reaction.StoredEnergyQ);
            WriteInt64Field(writer, 7, reaction.DissipatedEnergyQ);
        }

        Field(writer, 12);
        writer.WriteUInt32(checked((uint)founderPhenotypes.Length));
        foreach (var phenotype in founderPhenotypes)
        {
            writer.WriteUInt32(0x1004);
            WriteUInt32Field(writer, 1, phenotype.FounderGenomeId.Value);
            WriteUInt32Field(writer, 7, phenotype.FounderAllocationId.Value);
            Field(writer, 2);
            writer.WriteUInt32(checked((uint)phenotype.Processes.Length));
            foreach (var process in phenotype.Processes)
            {
                WriteUInt32Field(writer, 1, process.Reaction.Id.Value);
            }

            WritePhysiology(writer, 3, phenotype.Physiology);
            WriteUInt32Values(writer, 4, phenotype.AcquiredTraits.Select(trait => trait.Value));
            WriteUInt32Field(writer, 5, phenotype.MutationIncomeModifierQ);
            WriteUInt32Field(writer, 6, phenotype.MaximumChangeComplexity);
        }

        Field(writer, 13);
        writer.WriteUInt32(checked((uint)scenarios.Length));
        foreach (var scenario in scenarios)
        {
            writer.WriteUInt32(0x1005);
            WriteUInt32Field(writer, 1, scenario.Id.Value);
            WriteUInt32Field(writer, 2, scenario.TickDurationHours);
            Field(writer, 3);
            writer.WriteUInt32(checked((uint)scenario.PermittedFounders.Length));
            foreach (var founder in scenario.PermittedFounders)
            {
                WriteUInt32Field(writer, 1, founder.Id.Value);
            }
            Field(writer, 4);
            writer.WriteUInt32(checked((uint)scenario.PermittedFounderAllocations.Length));
            foreach (var allocation in scenario.PermittedFounderAllocations)
            {
                WriteUInt32Field(writer, 1, allocation.Id.Value);
            }
            WriteUInt32Field(writer, 5, scenario.DefaultCompetitorFounderAllocation.Id.Value);
            WriteUInt64Field(writer, 6, scenario.FounderInitialization.BiologicalAgeMinimumHours);
            WriteUInt64Field(writer, 7, scenario.FounderInitialization.BiologicalAgeMaximumHours);
            WriteUInt64Field(writer, 8, scenario.FounderInitialization.ReproductionReadinessMinimumHours);
            WriteUInt64Field(writer, 9, scenario.FounderInitialization.ReproductionReadinessMaximumHours);
        }

        Field(writer, 14);
        writer.WriteUInt32(checked((uint)traits.Length));
        foreach (var trait in traits)
        {
            writer.WriteUInt32(0x100F);
            WriteUInt32Field(writer, 1, trait.Id.Value);
            WriteStringField(writer, 2, trait.StableKey);
            WriteStringField(writer, 3, trait.Family);
            WriteBooleanField(writer, 4, trait.Selectable);
            WriteInt64Field(writer, 5, trait.MutationPointCostQ);
            WriteUInt32Field(writer, 6, trait.ChangeComplexity);
            WriteUInt32Values(writer, 7, trait.Prerequisites.Select(id => id.Value));
            WriteUInt32Values(writer, 8, trait.Incompatibilities.Select(id => id.Value));
            WriteUInt32Field(writer, 9, trait.BaseEvolutionWeightQ);
            WriteUInt32Values(writer, 10, trait.PressureTags.Select(tag => (uint)tag));
            WriteBooleanField(writer, 11, trait.EnablesResourceConservation);
            WriteUInt32Field(writer, 12, trait.MutationIncomeMultiplierQ);
            Field(writer, 13);
            writer.WriteBoolean(trait.MaximumChangeComplexity.HasValue);
            if (trait.MaximumChangeComplexity.HasValue)
            {
                writer.WriteUInt32(trait.MaximumChangeComplexity.Value);
            }
        }

        Field(writer, 15);
        writer.WriteUInt32(checked((uint)founderAllocations.Length));
        foreach (var allocation in founderAllocations)
        {
            writer.WriteUInt32(0x1011);
            WriteUInt32Field(writer, 1, allocation.Id.Value);
            WriteStringField(writer, 2, allocation.StableKey);
            WriteBooleanField(writer, 3, allocation.IsBaseline);
            WriteUInt32Field(writer, 4, allocation.CaptureEfficiencyMultiplierQ);
            WriteUInt32Field(writer, 5, allocation.ChemicalToleranceMultiplierQ);
        }

        Field(writer, 16);
        writer.WriteUInt32(0x1010);
        WriteUInt32Field(writer, 1, MutationIncomeMath.EffectivePopulationTableAlgorithmVersion);
        WriteUInt32Field(writer, 2, checked((uint)effectivePopulationQ.Length));
        WriteStringField(writer, 3, MutationIncomeMath.EffectivePopulationTableSha256);
    }

    private static void WritePhysiology(
        CanonicalBinaryWriter writer,
        ushort fieldTag,
        CompiledOrganismPhysiology value)
    {
        Field(writer, fieldTag);
        writer.WriteUInt32(0x1009);
        WriteInt64Field(writer, 1, value.MatureStructureQ);
        WriteInt64Field(writer, 2, value.StructuralHardFloorQ);
        WriteInt64Field(writer, 3, value.ChargedReserveCapacityQ);
        WriteInt64Field(writer, 4, value.TerminalReserveThresholdQ);
        WriteInt64Field(writer, 5, value.DissolvedMacronutrientCapacityLoadQ);
        WriteInt64Field(writer, 6, value.FreeMicronutrientCapacityLoadQ);
        WriteUInt32Field(writer, 21, value.PassiveMicronutrientUptakePerMillionPerHour);
        Field(writer, 22);
        writer.WriteUInt32(checked((uint)value.CommittedMicronutrientQuotas.Length));
        foreach (var quota in value.CommittedMicronutrientQuotas)
        {
            writer.WriteUInt32(quota.Resource.Id.Value);
            writer.WriteInt64(quota.Quantity);
        }
        WriteInt64Field(writer, 7, value.IngestedMatterCapacityLoadQ);
        WriteByteField(writer, 8, (byte)value.AllocationPolicy);
        WriteUInt64Field(writer, 9, value.SenescenceOnsetHours);
        WriteUInt64Field(writer, 10, value.AgeDeclineSpanHours);
        WriteUInt32Field(writer, 11, value.MinimumAgeFactorQ);
        WriteUInt32Field(writer, 12, value.SenescenceRiskBaseQ);
        WriteUInt64Field(writer, 13, value.SenescenceRiskEscalationHours);
        WriteUInt32Field(writer, 14, value.SenescenceRiskCapQ);
        var reproduction = value.Reproduction;
        Field(writer, 15);
        writer.WriteUInt32(0x100B);
        WriteUInt32Field(writer, 1, reproduction.MinimumHealthQ);
        WriteInt64Field(writer, 2, reproduction.RequiredStructureQ);
        WriteInt64Field(writer, 3, reproduction.ResultStructureMinimumQ);
        WriteInt64Field(writer, 4, reproduction.RequiredReserveQ);
        WriteInt64Field(writer, 5, reproduction.ResultReserveMinimumQ);
        WriteInt64Field(writer, 6, reproduction.WorkCostQ);
        WriteUInt64Field(writer, 7, reproduction.BaseCooldownHours);
        WriteUInt32Field(writer, 8, reproduction.CooldownJitterMaximumHours);
        var recycling = value.Recycling;
        Field(writer, 16);
        writer.WriteUInt32(0x100C);
        WriteUInt32Field(writer, 1, recycling.ReserveDecayPerMillionPerHour);
        WriteUInt32Field(writer, 2, recycling.StructureDecayPerMillionPerHour);
        WriteBooleanField(writer, 3, recycling.SimpleRemnantScavenging);
        WriteInt64Field(writer, 4, recycling.ScavengeReserveCapQ);
        WriteInt64Field(writer, 5, recycling.ScavengeActionCostQ);
        WriteInt64Field(writer, 6, recycling.ParticulateScavengeActionCostQ);
        WriteUInt64Field(writer, 7, recycling.ScavengeCooldownHours);
        WriteUInt32Field(writer, 8, recycling.ScavengeRangeQ);
        var spatial = value.Spatial;
        Field(writer, 17);
        writer.WriteUInt32(0x100D);
        WriteUInt32Field(writer, 1, spatial.MatureBodyRadiusQ);
        WriteInt64Field(writer, 2, spatial.GeometricStructureTargetQ);
        WriteUInt32Field(writer, 3, spatial.MinimumBodyRadiusQ);
        WriteUInt32Field(writer, 4, spatial.BrownianRmsQPerSqrtHour);
        WriteUInt32Field(writer, 5, spatial.EnvironmentalSpreadMultiplierQ);
        WriteUInt32Field(writer, 6, spatial.TerrestrialBrownianMultiplierQ);
        WriteUInt32Field(writer, 7, spatial.ActiveSpeedLimitQPerHour);
        WriteUInt32Field(writer, 8, spatial.MovementEnergyPerFounderRadiusQ);
        WriteBooleanField(writer, 9, spatial.CanOccupyTerrestrial);
        WriteUInt32Field(writer, 10, spatial.PassiveMigrationProbabilityQ);
        WriteUInt32Field(writer, 11, spatial.ActiveMigrationProbabilityQ);
        WriteUInt32Field(writer, 12, spatial.MediumTransitionFactorQ);
        WriteUInt32Field(writer, 13, spatial.DestinationCompatibilityFloorQ);
        var temperature = value.TemperatureResponse;
        Field(writer, 18);
        writer.WriteUInt32(0x100A);
        WriteInt32Field(writer, 1, temperature.PreferredMinimumMilliC);
        WriteInt32Field(writer, 2, temperature.PreferredMaximumMilliC);
        WriteInt32Field(writer, 3, temperature.HardMinimumMilliC);
        WriteInt32Field(writer, 4, temperature.HardMaximumMilliC);
        WriteUInt32Field(writer, 5, temperature.HealthPenaltyAtHardQ);
        WriteUInt32Field(writer, 6, temperature.HealthFactorFloorQ);
        WriteUInt32Field(writer, 7, temperature.DeathChanceAtHardQ);
        WriteUInt32Field(writer, 8, temperature.DeathChanceCapQ);
        var behavior = value.Behavior;
        Field(writer, 19);
        writer.WriteUInt32(0x100E);
        WriteBooleanField(writer, 1, behavior.ResourceConservation);
        WriteUInt64Field(writer, 2, behavior.MinimumDwellHours);
        WriteUInt32Field(writer, 3, behavior.ConservationEnterReserveQ);
        WriteUInt32Field(writer, 4, behavior.ConservationEnterConditionalReserveQ);
        WriteUInt32Field(writer, 5, behavior.ConservationEnterEnergyCoverageQ);
        WriteUInt32Field(writer, 6, behavior.ConservationCriticalReserveQ);
        WriteUInt32Field(writer, 7, behavior.ConservationExitReserveQ);
        WriteUInt32Field(writer, 8, behavior.ConservationExitEnergyCoverageQ);
        WriteUInt32Field(writer, 9, behavior.ConservationExitHighReserveQ);
        var opening = value.OpeningMetabolism;
        Field(writer, 20);
        writer.WriteUInt32(0x100F);
        WriteByteField(writer, 1, (byte)opening.Kind);
        WriteUInt32Field(writer, 2, opening.MaximumCaptureExtentsPerHour);
        WriteUInt32Field(writer, 3, opening.FavorableCaptureEfficiencyQ);
        WriteBooleanField(writer, 4, opening.RequiresLight);
        WriteUInt32Field(writer, 5, opening.IlluminatedHoursPerDay);
        WriteUInt32Field(writer, 6, opening.StructuralGrowthExtentsPerHour);
        WriteInt64Field(writer, 7, opening.MaintenanceCostQPerHour);
        WriteInt64Field(writer, 8, opening.GrowthReserveFloorQ);
        WriteUInt32Field(writer, 9, opening.GeneratedLightCaptureExtentsPerUnitHour);
        var chemical = value.ChemicalResponse;
        Field(writer, 21);
        writer.WriteUInt32(0x1011);
        WriteInt64Field(writer, 1, chemical.HydrogenSulfideSoftThresholdQ);
        WriteInt64Field(writer, 2, chemical.HydrogenSulfideHardThresholdQ);
        WriteInt64Field(writer, 3, chemical.SulfurDioxideSoftThresholdQ);
        WriteInt64Field(writer, 4, chemical.SulfurDioxideHardThresholdQ);
        WriteUInt32Field(writer, 5, chemical.HealthPenaltyAtHardQ);
        WriteUInt32Field(writer, 6, chemical.HealthFactorFloorQ);
        WriteUInt32Field(writer, 7, chemical.DeathChanceAtHardQ);
        WriteUInt32Field(writer, 8, chemical.DeathChanceCapQ);
    }

    private static void WriteTerms(
        CanonicalBinaryWriter writer,
        ushort fieldTag,
        ImmutableArray<CompiledResourceTerm> terms)
    {
        Field(writer, fieldTag);
        writer.WriteUInt32(checked((uint)terms.Length));
        foreach (var term in terms)
        {
            writer.WriteUInt32(0x1006);
            WriteUInt32Field(writer, 1, term.Resource.Id.Value);
            WriteInt64Field(writer, 2, term.Quantity);
        }
    }

    private static void WriteIdentityKeys(
        CanonicalBinaryWriter writer,
        ushort fieldTag,
        IEnumerable<(uint NumericId, string StableKey)> identities)
    {
        var ordered = identities.OrderBy(identity => identity.NumericId).ToArray();
        Field(writer, fieldTag);
        writer.WriteUInt32(checked((uint)ordered.Length));
        foreach (var identity in ordered)
        {
            writer.WriteUInt32(0x1007);
            WriteUInt32Field(writer, 1, identity.NumericId);
            WriteStringField(writer, 2, identity.StableKey);
        }
    }

    private static void WriteUInt32Values(
        CanonicalBinaryWriter writer,
        ushort fieldTag,
        IEnumerable<uint> values)
    {
        var materialized = values.ToArray();
        Field(writer, fieldTag);
        writer.WriteUInt32(checked((uint)materialized.Length));
        foreach (var value in materialized)
        {
            writer.WriteUInt32(value);
        }
    }

    private static void WritePresentationDefinitions(
        CanonicalBinaryWriter writer,
        ushort fieldTag,
        IEnumerable<(uint NumericId, string StableKey, string DisplayName)> definitions)
    {
        var ordered = definitions.OrderBy(definition => definition.NumericId).ToArray();
        Field(writer, fieldTag);
        writer.WriteUInt32(checked((uint)ordered.Length));
        foreach (var definition in ordered)
        {
            writer.WriteUInt32(0x1101);
            WriteUInt32Field(writer, 1, definition.NumericId);
            WriteStringField(writer, 2, definition.StableKey);
            WriteStringField(writer, 3, definition.DisplayName);
        }
    }

    private static string Hash(CanonicalBinaryWriter writer)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(writer.WrittenSpan, digest);
        return Convert.ToHexStringLower(digest);
    }

    private static void Field(CanonicalBinaryWriter writer, ushort tag) => writer.WriteUInt16(tag);

    private static void WriteByteField(CanonicalBinaryWriter writer, ushort tag, byte value)
    {
        Field(writer, tag);
        writer.WriteByte(value);
    }

    private static void WriteBooleanField(CanonicalBinaryWriter writer, ushort tag, bool value)
    {
        Field(writer, tag);
        writer.WriteBoolean(value);
    }

    private static void WriteUInt32Field(CanonicalBinaryWriter writer, ushort tag, uint value)
    {
        Field(writer, tag);
        writer.WriteUInt32(value);
    }

    private static void WriteUInt64Field(CanonicalBinaryWriter writer, ushort tag, ulong value)
    {
        Field(writer, tag);
        writer.WriteUInt64(value);
    }

    private static void WriteInt32Field(CanonicalBinaryWriter writer, ushort tag, int value)
    {
        Field(writer, tag);
        writer.WriteInt32(value);
    }

    private static void WriteInt64Field(CanonicalBinaryWriter writer, ushort tag, long value)
    {
        Field(writer, tag);
        writer.WriteInt64(value);
    }

    private static void WriteStringField(CanonicalBinaryWriter writer, ushort tag, string value)
    {
        Field(writer, tag);
        writer.WriteUtf8Nfc(value);
    }
}

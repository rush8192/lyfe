using Lyfe.Simulation.Rules.Authoring;

namespace Lyfe.Simulation.Rules.Loading;

public static class RulePackSourceLoader
{
    private const string ManifestFile = "pack.json";

    public static RulePackLoadResult Load(IContentSource source)
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
            RuleAuthoringJsonContext.Default.RulePackManifest,
            diagnostics);
        if (manifest is null)
        {
            return Failure(diagnostics);
        }

        ValidateManifest(manifest, diagnostics);
        if (!ContentSourceReader.ValidateIncludedPaths(
                ManifestFile,
                manifest.IncludedFiles,
                diagnostics) ||
            !ContentPath.IsValid(manifest.RegistryLockFile))
        {
            if (!ContentPath.IsValid(manifest.RegistryLockFile))
            {
                diagnostics.Add(new RuleDiagnostic(
                    RuleDiagnosticSeverity.Error,
                    "LYFE-CONTENT-MANIFEST-005",
                    "The registry lock path must be a normalized relative content path.",
                    ManifestFile,
                    1,
                    1));
            }

            return Failure(diagnostics);
        }

        var lockContent = ContentSourceReader.Read(
            source,
            manifest.RegistryLockFile,
            ContentSourceReader.MaximumSourceBytes,
            diagnostics);
        var registryLock = lockContent is null
            ? null
            : StrictJson.Deserialize(
                manifest.RegistryLockFile,
                lockContent.Value,
                RuleAuthoringJsonContext.Default.RegistryLockFile,
                diagnostics);

        var resources = new List<ResourceDefinition>();
        var reactions = new List<ReactionDefinition>();
        var founderGenomes = new List<FounderGenomeDefinition>();
        var scenarios = new List<ScenarioDefinition>();
        var identities = new List<SourcedIdentity>();

        foreach (var path in manifest.IncludedFiles)
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
                RuleAuthoringJsonContext.Default.RuleSourceDocument,
                diagnostics);
            switch (document)
            {
                case ResourceDefinitionsDocument resourceDocument:
                    AddResources(path, resourceDocument, resources, identities, diagnostics);
                    break;
                case ReactionDefinitionsDocument reactionDocument:
                    AddReactions(path, reactionDocument, reactions, identities, diagnostics);
                    break;
                case FounderGenomeDefinitionsDocument genomeDocument:
                    AddFounderGenomes(path, genomeDocument, founderGenomes, identities, diagnostics);
                    break;
                case ScenarioDefinitionsDocument scenarioDocument:
                    AddScenarios(path, scenarioDocument, scenarios, identities, diagnostics);
                    break;
            }
        }

        ValidateIdentities(identities, diagnostics);
        ValidateReferences(resources, reactions, founderGenomes, scenarios, diagnostics);
        if (registryLock is not null)
        {
            ValidateRegistryLock(registryLock, identities, manifest.RegistryLockFile, diagnostics);
        }

        if (registryLock is null || diagnostics.Any(IsError))
        {
            return Failure(diagnostics);
        }

        return new RulePackLoadResult(
            new AuthoringRulePack(
                manifest,
                registryLock,
                resources.ToArray(),
                reactions.ToArray(),
                founderGenomes.ToArray(),
                scenarios.ToArray()),
            RuleDiagnosticOrdering.Sort(diagnostics));
    }

    private static void ValidateManifest(
        RulePackManifest manifest,
        ICollection<RuleDiagnostic> diagnostics)
    {
        ValidateStableKey(manifest.PackId, "pack", ManifestFile, diagnostics);
        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            AddError("LYFE-CONTENT-MANIFEST-006", "Pack version is required.", ManifestFile, diagnostics);
        }

        if (manifest.EngineRuleApiVersion == 0 ||
            manifest.RuleCompilerVersion == 0 ||
            manifest.MechanicsHashSchemaVersion == 0)
        {
            AddError(
                "LYFE-CONTENT-MANIFEST-007",
                "API, compiler, and hash-schema versions must be nonzero.",
                ManifestFile,
                diagnostics);
        }

        if (manifest.RequiredFixtureIds is null)
        {
            AddError(
                "LYFE-CONTENT-MANIFEST-008",
                "requiredFixtureIds must be an array, even when empty.",
                ManifestFile,
                diagnostics);
        }
    }

    private static void AddResources(
        string path,
        ResourceDefinitionsDocument document,
        List<ResourceDefinition> resources,
        ICollection<SourcedIdentity> identities,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (document.Resources is null)
        {
            AddError("LYFE-CONTENT-DOCUMENT-001", "resources must be an array.", path, diagnostics);
            return;
        }

        foreach (var resource in document.Resources)
        {
            resources.Add(resource);
            identities.Add(new SourcedIdentity("resource", resource.NumericId, resource.StableKey, path));
            ValidateStableKey(resource.StableKey, "resource", path, diagnostics);
            if (string.IsNullOrWhiteSpace(resource.DisplayName) ||
                resource.Composition is null ||
                resource.Composition.Length == 0)
            {
                AddError(
                    "LYFE-CONTENT-RESOURCE-001",
                    $"Resource '{resource.StableKey}' requires a display name and nonempty composition.",
                    path,
                    diagnostics);
            }

            if (resource.BiologicalForm is not ("organic" or "inorganic" or "boundary") ||
                resource.EnvironmentalPhase is not ("gas" or "dissolved" or "boundary"))
            {
                AddError(
                    "LYFE-CONTENT-RESOURCE-002",
                    $"Resource '{resource.StableKey}' uses an unknown form or environmental phase.",
                    path,
                    diagnostics);
            }

            if (resource.Composition is not null &&
                resource.Composition.Any(component => component.Quantity == 0 ||
                    !StableKey.IsValid(component.Element)))
            {
                AddError(
                    "LYFE-CONTENT-RESOURCE-003",
                    $"Resource '{resource.StableKey}' has an invalid elemental component.",
                    path,
                    diagnostics);
            }
        }
    }

    private static void AddReactions(
        string path,
        ReactionDefinitionsDocument document,
        List<ReactionDefinition> reactions,
        ICollection<SourcedIdentity> identities,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (document.Reactions is null)
        {
            AddError("LYFE-CONTENT-DOCUMENT-002", "reactions must be an array.", path, diagnostics);
            return;
        }

        foreach (var reaction in document.Reactions)
        {
            reactions.Add(reaction);
            identities.Add(new SourcedIdentity("reaction", reaction.NumericId, reaction.StableKey, path));
            ValidateStableKey(reaction.StableKey, "reaction", path, diagnostics);
            if (string.IsNullOrWhiteSpace(reaction.DisplayName) ||
                !StableKey.IsValid(reaction.ProcessKind) ||
                reaction.Inputs is null || reaction.Inputs.Length == 0 ||
                reaction.Outputs is null || reaction.Outputs.Length == 0)
            {
                AddError(
                    "LYFE-CONTENT-REACTION-001",
                    $"Reaction '{reaction.StableKey}' has incomplete local fields.",
                    path,
                    diagnostics);
            }

            if (reaction.GrossEnergyQ < 0 ||
                reaction.StoredEnergyQ < 0 ||
                reaction.DissipatedEnergyQ < 0 ||
                reaction.StoredEnergyQ + reaction.DissipatedEnergyQ != reaction.GrossEnergyQ)
            {
                AddError(
                    "LYFE-CONTENT-REACTION-002",
                    $"Reaction '{reaction.StableKey}' energy outputs must be nonnegative and sum to grossEnergyQ.",
                    path,
                    diagnostics);
            }
        }
    }

    private static void AddFounderGenomes(
        string path,
        FounderGenomeDefinitionsDocument document,
        List<FounderGenomeDefinition> founderGenomes,
        ICollection<SourcedIdentity> identities,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (document.FounderGenomes is null)
        {
            AddError("LYFE-CONTENT-DOCUMENT-003", "founderGenomes must be an array.", path, diagnostics);
            return;
        }

        foreach (var genome in document.FounderGenomes)
        {
            founderGenomes.Add(genome);
            identities.Add(new SourcedIdentity("founderGenome", genome.NumericId, genome.StableKey, path));
            ValidateStableKey(genome.StableKey, "founder genome", path, diagnostics);
            if (string.IsNullOrWhiteSpace(genome.DisplayName) ||
                genome.EnabledReactionKeys is null ||
                genome.EnabledReactionKeys.Length == 0)
            {
                AddError(
                    "LYFE-CONTENT-GENOME-001",
                    $"Founder genome '{genome.StableKey}' must enable at least one reaction.",
                    path,
                    diagnostics);
            }
        }
    }

    private static void AddScenarios(
        string path,
        ScenarioDefinitionsDocument document,
        List<ScenarioDefinition> scenarios,
        ICollection<SourcedIdentity> identities,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (document.Scenarios is null)
        {
            AddError("LYFE-CONTENT-DOCUMENT-004", "scenarios must be an array.", path, diagnostics);
            return;
        }

        foreach (var scenario in document.Scenarios)
        {
            scenarios.Add(scenario);
            identities.Add(new SourcedIdentity("scenario", scenario.NumericId, scenario.StableKey, path));
            ValidateStableKey(scenario.StableKey, "scenario", path, diagnostics);
            if (string.IsNullOrWhiteSpace(scenario.DisplayName) ||
                scenario.TickDurationHours == 0 ||
                scenario.FounderGenomeKeys is null ||
                scenario.FounderGenomeKeys.Length == 0)
            {
                AddError(
                    "LYFE-CONTENT-SCENARIO-001",
                    $"Scenario '{scenario.StableKey}' requires a duration and at least one founder genome.",
                    path,
                    diagnostics);
            }
        }
    }

    private static void ValidateIdentities(
        IEnumerable<SourcedIdentity> identities,
        ICollection<RuleDiagnostic> diagnostics)
    {
        foreach (var kindGroup in identities.GroupBy(identity => identity.Kind, StringComparer.Ordinal))
        {
            foreach (var identity in kindGroup)
            {
                if (identity.NumericId == 0)
                {
                    AddError(
                        "LYFE-CONTENT-IDENTITY-001",
                        $"{identity.Kind} '{identity.StableKey}' uses reserved numeric ID zero.",
                        identity.SourceFile,
                        diagnostics);
                }
            }

            foreach (var duplicate in kindGroup
                         .GroupBy(identity => identity.NumericId)
                         .Where(group => group.Count() > 1))
            {
                AddError(
                    "LYFE-CONTENT-IDENTITY-002",
                    $"Registry '{kindGroup.Key}' repeats numeric ID {duplicate.Key}.",
                    duplicate.First().SourceFile,
                    diagnostics);
            }

            foreach (var duplicate in kindGroup
                         .GroupBy(identity => identity.StableKey, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                AddError(
                    "LYFE-CONTENT-IDENTITY-003",
                    $"Registry '{kindGroup.Key}' repeats stable key '{duplicate.Key}'.",
                    duplicate.First().SourceFile,
                    diagnostics);
            }
        }
    }

    private static void ValidateReferences(
        IReadOnlyCollection<ResourceDefinition> resources,
        IReadOnlyCollection<ReactionDefinition> reactions,
        IReadOnlyCollection<FounderGenomeDefinition> founderGenomes,
        IReadOnlyCollection<ScenarioDefinition> scenarios,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var resourceKeys = resources.Select(resource => resource.StableKey).ToHashSet(StringComparer.Ordinal);
        foreach (var reaction in reactions)
        {
            var resourceUses = (reaction.Inputs ?? []).Concat(reaction.Outputs ?? []);
            foreach (var use in resourceUses)
            {
                if (use.Quantity <= 0 || !resourceKeys.Contains(use.ResourceKey))
                {
                    AddError(
                        "LYFE-CONTENT-REFERENCE-001",
                        $"Reaction '{reaction.StableKey}' has an invalid resource use '{use.ResourceKey}'.",
                        ManifestFile,
                        diagnostics);
                }
            }
        }

        var reactionKeys = reactions.Select(reaction => reaction.StableKey).ToHashSet(StringComparer.Ordinal);
        foreach (var genome in founderGenomes)
        {
            foreach (var reactionKey in genome.EnabledReactionKeys ?? [])
            {
                if (!reactionKeys.Contains(reactionKey))
                {
                    AddError(
                        "LYFE-CONTENT-REFERENCE-002",
                        $"Founder genome '{genome.StableKey}' references unknown reaction '{reactionKey}'.",
                        ManifestFile,
                        diagnostics);
                }
            }
        }

        var genomeKeys = founderGenomes.Select(genome => genome.StableKey).ToHashSet(StringComparer.Ordinal);
        foreach (var scenario in scenarios)
        {
            foreach (var genomeKey in scenario.FounderGenomeKeys ?? [])
            {
                if (!genomeKeys.Contains(genomeKey))
                {
                    AddError(
                        "LYFE-CONTENT-REFERENCE-003",
                        $"Scenario '{scenario.StableKey}' references unknown founder genome '{genomeKey}'.",
                        ManifestFile,
                        diagnostics);
                }
            }
        }
    }

    private static void ValidateRegistryLock(
        RegistryLockFile registryLock,
        IReadOnlyCollection<SourcedIdentity> identities,
        string sourceFile,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (registryLock.SchemaVersion != 1 || registryLock.Registries is null)
        {
            AddError(
                "LYFE-CONTENT-LOCK-001",
                "Registry lock schemaVersion must be 1 and registries must be an array.",
                sourceFile,
                diagnostics);
            return;
        }

        var knownKinds = new HashSet<string>(
            ["resource", "reaction", "founderGenome", "scenario"],
            StringComparer.Ordinal);
        var seenKinds = new HashSet<string>(StringComparer.Ordinal);
        var identityLookup = new Dictionary<(string Kind, uint NumericId), SourcedIdentity>();
        foreach (var identity in identities)
        {
            identityLookup.TryAdd((identity.Kind, identity.NumericId), identity);
        }

        foreach (var registry in registryLock.Registries)
        {
            if (!knownKinds.Contains(registry.Kind) || !seenKinds.Add(registry.Kind) || registry.Entries is null)
            {
                AddError(
                    "LYFE-CONTENT-LOCK-002",
                    $"Registry lock kind '{registry.Kind}' is unknown, duplicated, or missing entries.",
                    sourceFile,
                    diagnostics);
                continue;
            }

            var seenIds = new HashSet<uint>();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in registry.Entries)
            {
                if (entry.NumericId == 0 ||
                    !StableKey.IsValid(entry.StableKey) ||
                    !seenIds.Add(entry.NumericId) ||
                    !seenKeys.Add(entry.StableKey) ||
                    entry.Status is not ("active" or "tombstone"))
                {
                    AddError(
                        "LYFE-CONTENT-LOCK-003",
                        $"Registry lock entry '{registry.Kind}:{entry.NumericId}:{entry.StableKey}' is invalid or duplicated.",
                        sourceFile,
                        diagnostics);
                    continue;
                }

                var hasDefinition = identityLookup.TryGetValue(
                    (registry.Kind, entry.NumericId),
                    out var identity);
                if (entry.Status == "active" &&
                    (!hasDefinition ||
                        identity is null ||
                        identity.StableKey != entry.StableKey ||
                        entry.RemovedInVersion is not null))
                {
                    AddError(
                        "LYFE-CONTENT-LOCK-004",
                        $"Active lock entry '{registry.Kind}:{entry.NumericId}:{entry.StableKey}' does not match an active definition.",
                        sourceFile,
                        diagnostics);
                }
                else if (entry.Status == "tombstone" &&
                    (hasDefinition || string.IsNullOrWhiteSpace(entry.RemovedInVersion)))
                {
                    AddError(
                        "LYFE-CONTENT-LOCK-005",
                        $"Tombstone '{registry.Kind}:{entry.NumericId}:{entry.StableKey}' is active or lacks removedInVersion.",
                        sourceFile,
                        diagnostics);
                }
            }
        }

        foreach (var missingKind in knownKinds.Except(seenKinds, StringComparer.Ordinal))
        {
            AddError(
                "LYFE-CONTENT-LOCK-006",
                $"Registry lock is missing registry '{missingKind}'.",
                sourceFile,
                diagnostics);
        }

        var activeEntries = registryLock.Registries
            .Where(registry => registry.Entries is not null)
            .SelectMany(registry => registry.Entries
                .Where(entry => entry.Status == "active")
                .Select(entry => (registry.Kind, entry.NumericId, entry.StableKey)))
            .ToHashSet();
        foreach (var identity in identities)
        {
            if (!activeEntries.Contains((identity.Kind, identity.NumericId, identity.StableKey)))
            {
                AddError(
                    "LYFE-CONTENT-LOCK-007",
                    $"Definition '{identity.Kind}:{identity.NumericId}:{identity.StableKey}' is absent from the active registry lock.",
                    sourceFile,
                    diagnostics);
            }
        }
    }

    private static void ValidateStableKey(
        string? value,
        string kind,
        string sourceFile,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (!StableKey.IsValid(value))
        {
            AddError(
                "LYFE-CONTENT-IDENTITY-004",
                $"{kind} stable key '{value}' is invalid.",
                sourceFile,
                diagnostics);
        }
    }

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

    private static bool IsError(RuleDiagnostic diagnostic) =>
        diagnostic.Severity == RuleDiagnosticSeverity.Error;

    private static RulePackLoadResult Failure(IEnumerable<RuleDiagnostic> diagnostics) =>
        new(null, RuleDiagnosticOrdering.Sort(diagnostics));

    private sealed record SourcedIdentity(
        string Kind,
        uint NumericId,
        string StableKey,
        string SourceFile);
}

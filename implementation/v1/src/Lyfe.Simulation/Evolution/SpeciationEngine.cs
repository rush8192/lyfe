using System.Collections.Immutable;
using System.Security.Cryptography;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.Serialization;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Evolution;

internal static class SpeciationEngine
{
    public static SpeciationPreview Preview(
        MutableWorldState world,
        SpeciationCommand command,
        ulong completedTick)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        if (!world.GetSpeciesIdsInCanonicalOrder().Contains(command.AncestorSpeciesId))
        {
            return Reject(SpeciationFailure.SpeciesExtinct);
        }
        var ancestor = world.GetSpecies(command.AncestorSpeciesId);
        if (ancestor.Evolution.Authority == EvolutionAuthorityKind.Locked)
        {
            return Reject(SpeciationFailure.SpeciesLocked);
        }
        if (command.ActorKind == SpeciationActorKind.Player &&
            ancestor.Evolution.Authority != EvolutionAuthorityKind.Controlled ||
            command.ActorKind == SpeciationActorKind.Autonomous &&
            ancestor.Evolution.Authority != EvolutionAuthorityKind.Autonomous)
        {
            return Reject(SpeciationFailure.WrongController);
        }
        if (ancestor.Population == 0) return Reject(SpeciationFailure.SpeciesExtinct);
        if (ancestor.Evolution.EvolutionRevision != command.ExpectedEvolutionRevision)
            return Reject(SpeciationFailure.StaleEvolutionRevision);
        var ancestorGenome = world.GetGenome(ancestor.GenomeId);
        if (!string.Equals(ancestorGenome.GenomeHash, command.ExpectedGenomeHash, StringComparison.Ordinal))
            return Reject(SpeciationFailure.GenomeChanged);
        if (completedTick < ancestor.Evolution.SpeciationNotBeforeTick)
            return Reject(SpeciationFailure.SpeciationCooldown);

        var proposedIds = command.NewTraitIds.IsDefault
            ? []
            : command.NewTraitIds.Distinct().OrderBy(id => id.Value).ToImmutableArray();
        if (proposedIds.IsEmpty || proposedIds.Length != command.NewTraitIds.Length)
            return Reject(SpeciationFailure.UnknownTrait);
        var byId = world.Rules.RulePack.Traits.ToDictionary(trait => trait.Id);
        var acquired = ancestorGenome.AcquiredTraits.ToHashSet();
        var proposedSet = proposedIds.ToHashSet();
        long priceQ = 0;
        uint complexity = 0;
        foreach (var id in proposedIds)
        {
            if (!byId.TryGetValue(id, out var trait) || !trait.Selectable)
                return Reject(SpeciationFailure.UnknownTrait);
            if (acquired.Contains(id)) return Reject(SpeciationFailure.TraitAlreadyAcquired);
            if (trait.Prerequisites.Any(id => !acquired.Contains(id) && !proposedSet.Contains(id)))
                return Reject(SpeciationFailure.MissingPrerequisite);
            if (trait.Incompatibilities.Any(id => acquired.Contains(id) || proposedSet.Contains(id)))
                return Reject(SpeciationFailure.IncompatibleTrait);
            priceQ = checked(priceQ + trait.MutationPointCostQ);
            complexity = checked(complexity + trait.ChangeComplexity);
        }
        if (complexity > world.GetCompiledPhenotype(ancestor.Id).MaximumChangeComplexity)
            return Reject(SpeciationFailure.ChangeComplexityExceeded);
        var tileIds = command.SelectedTileIds.IsDefault
            ? []
            : command.SelectedTileIds.Distinct().OrderBy(id => id.Value).ToImmutableArray();
        if (tileIds.Length is < 1 or > 4 || tileIds.Length != command.SelectedTileIds.Length)
            return Reject(SpeciationFailure.InvalidTileCount);
        var localCounts = world.GetOrganismIdsInCanonicalOrder()
            .Select(world.GetOrganism)
            .Where(organism => organism.SpeciesId == ancestor.Id)
            .GroupBy(organism => organism.TileId)
            .ToDictionary(group => group.Key, group => group.Count());
        var fraction = tileIds.Length switch
        {
            1 => (Numerator: 50, Denominator: 100),
            2 => (Numerator: 20, Denominator: 100),
            3 => (Numerator: 8, Denominator: 100),
            4 => (Numerator: 3, Denominator: 100),
            _ => throw new InvalidOperationException(),
        };
        var founders = ImmutableArray.CreateBuilder<FounderCount>(tileIds.Length);
        ulong totalFounders = 0;
        foreach (var tileId in tileIds)
        {
            if (!localCounts.TryGetValue(tileId, out var local))
                return Reject(SpeciationFailure.TileNotOccupied);
            var count = Math.Max(1, local * fraction.Numerator / fraction.Denominator);
            founders.Add(new FounderCount(tileId, checked((uint)count)));
            totalFounders = checked(totalFounders + (uint)count);
        }
        if (totalFounders >= ancestor.Population)
            return Reject(SpeciationFailure.AncestorWouldBeDepleted);

        var allTraits = acquired.Concat(proposedIds).OrderBy(id => id.Value).ToImmutableArray();
        var phenotype = GenomeCompiler.Compile(
            world.Rules.RulePack,
            ancestorGenome.FounderGenomeId,
            ancestorGenome.FounderAllocationId,
            allTraits);
        var currentPhenotype = Snapshot(world.GetCompiledPhenotype(ancestor.Id));
        var proposedPhenotype = Snapshot(phenotype);
        if (priceQ > ancestor.Evolution.MutationBalanceQ)
        {
            return new SpeciationPreview(
                false,
                SpeciationFailure.InsufficientMutationPoints,
                priceQ,
                complexity,
                founders.MoveToImmutable(),
                phenotype.CanonicalCompiledHash,
                currentPhenotype,
                proposedPhenotype);
        }
        return new SpeciationPreview(
            true,
            SpeciationFailure.None,
            priceQ,
            complexity,
            founders.MoveToImmutable(),
            phenotype.CanonicalCompiledHash,
            currentPhenotype,
            proposedPhenotype);
    }

    public static (SpeciationPreview Preview, ulong EventId, SpeciesId DescendantId, GenomeId GenomeId)
        Apply(
            MutableWorldState world,
            SpeciationCommand command,
        ulong completedTick,
        ISimulationRandom random,
        bool followPlayerControl,
        PhaseChangeBuilder changes)
    {
        var preview = Preview(world, command, completedTick);
        if (!preview.Accepted)
        {
            return (preview, 0, default, default);
        }

        var ancestor = world.GetSpecies(command.AncestorSpeciesId);
        var ancestorGenome = world.GetGenome(ancestor.GenomeId);
        var eventId = checked((ulong)world.GetSpeciationEvents().Length + 1);
        var selected = ImmutableArray.CreateBuilder<OrganismId>();
        foreach (var founderCount in preview.FounderCounts)
        {
            var ranked = world.GetOrganismIdsInCanonicalOrder()
                .Select(world.GetOrganism)
                .Where(organism => organism.SpeciesId == ancestor.Id && organism.TileId == founderCount.TileId)
                .Select(organism => (
                    organism.Id,
                    Rank: random.StableRank(
                        RandomAddress.Create(
                            RandomDomains.SpeciationFounderSelection,
                            eventId,
                            founderCount.TileId.Value,
                            organism.Id.Value),
                        organism.Id.Value)))
                .OrderBy(value => value.Rank)
                .Take(checked((int)founderCount.Count));
            selected.AddRange(ranked.Select(value => value.Id));
        }

        var traits = ancestorGenome.AcquiredTraits.Concat(command.NewTraitIds)
            .Distinct().OrderBy(id => id.Value).ToImmutableArray();
        var genomeId = world.InternGenome(
            ancestorGenome.FounderGenomeId,
            ancestorGenome.FounderAllocationId,
            traits,
            changes);
        var postPrice = checked(ancestor.Evolution.MutationBalanceQ - preview.MutationPriceQ);
        var cooldownTicks = checked((SpeciationRules.RefractoryHours + world.Rules.TickDurationHours - 1) /
            world.Rules.TickDurationHours);
        var notBefore = checked(completedTick + cooldownTicks);
        var descendantAccount = ancestor.Evolution with
        {
            Authority = command.ActorKind == SpeciationActorKind.Player && followPlayerControl
                ? EvolutionAuthorityKind.Controlled
                : EvolutionAuthorityKind.Autonomous,
            MutationBalanceQ = postPrice,
            SpeciationNotBeforeTick = notBefore,
            SpeciationOrdinal = 0,
            EvolutionRevision = 1,
        };
        var descendantId = world.CreateDescendantSpecies(
            genomeId,
            descendantAccount,
            new SpeciesLineageState(
                ancestor.Id,
                eventId,
                completedTick,
                null,
                preview.FounderCounts,
                command.NewTraitIds.OrderBy(id => id.Value).ToImmutableArray()),
            changes);
        var ancestorAccount = ancestor.Evolution with
        {
            Authority = command.ActorKind == SpeciationActorKind.Player && followPlayerControl
                ? EvolutionAuthorityKind.Autonomous
                : ancestor.Evolution.Authority,
            MutationBalanceQ = postPrice,
            SpeciationNotBeforeTick = notBefore,
            SpeciationOrdinal = checked(ancestor.Evolution.SpeciationOrdinal + 1),
            EvolutionRevision = checked(ancestor.Evolution.EvolutionRevision + 1),
        };
        world.SetSpeciesSpeciationState(ancestor.Id, ancestorAccount, changes);
        foreach (var organismId in selected.OrderBy(id => id.Value))
        {
            world.TransferOrganismSpecies(organismId, descendantId, changes);
        }

        var digest = FounderDigest(selected.ToImmutable());
        world.AppendSpeciationEvent(new SpeciationEventRecord(
            eventId,
            completedTick,
            ancestor.Id,
            descendantId,
            command.ActorKind,
            preview.FounderCounts,
            digest,
            command.NewTraitIds.OrderBy(id => id.Value).ToImmutableArray(),
            preview.MutationPriceQ,
            ancestor.Evolution.MutationBalanceQ,
            postPrice,
            preview.ChangeComplexity), changes);
        return (preview, eventId, descendantId, genomeId);
    }

    private static string FounderDigest(ImmutableArray<OrganismId> selected)
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(checked((uint)selected.Length));
        foreach (var id in selected.OrderBy(id => id.Value)) writer.WriteUInt64(id.Value);
        return Convert.ToHexStringLower(SHA256.HashData(writer.WrittenSpan));
    }

    private static EvolutionPhenotypeSnapshot Snapshot(CompiledPhenotype phenotype) => new(
        phenotype.MutationIncomeModifierQ,
        phenotype.MaximumChangeComplexity,
        phenotype.Physiology.Behavior.ResourceConservation,
        phenotype.Physiology.OpeningMetabolism.MaximumCaptureExtentsPerHour,
        phenotype.Physiology.OpeningMetabolism.FavorableCaptureEfficiencyQ,
        phenotype.Physiology.OpeningMetabolism.GeneratedLightCaptureExtentsPerUnitHour,
        phenotype.Physiology.OpeningMetabolism.MaintenanceCostQPerHour,
        phenotype.Physiology.ChargedReserveCapacityQ,
        phenotype.Physiology.Reproduction.MinimumHealthQ,
        phenotype.Physiology.OpeningMetabolism.RequiresLight,
        phenotype.Processes
            .Select(process => process.Reaction.Id)
            .ToImmutableArray());

    private static SpeciationPreview Reject(SpeciationFailure failure) =>
        new(false, failure, 0, 0, [], string.Empty);
}

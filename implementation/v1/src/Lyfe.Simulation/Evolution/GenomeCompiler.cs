using System.Collections.Immutable;
using Lyfe.Simulation.Rules.Compilation;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;

namespace Lyfe.Simulation.Evolution;

internal static class GenomeCompiler
{
    public static CompiledPhenotype Compile(
        CompiledRulePack rules,
        FounderGenomeId founderGenomeId,
        FounderAllocationId founderAllocationId,
        IEnumerable<TraitId> acquiredTraits)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var founder = rules.FounderPhenotypes.Single(
            phenotype => phenotype.FounderGenomeId == founderGenomeId);
        var allocation = rules.FounderAllocations.Single(
            candidate => candidate.Id == founderAllocationId);
        var canonical = acquiredTraits.Distinct().OrderBy(id => id.Value).ToImmutableArray();
        var byId = rules.Traits.ToDictionary(trait => trait.Id);
        var acquired = canonical.ToHashSet();
        var physiology = ApplyFounderAllocation(founder.Physiology, allocation);
        uint mutationModifierQ = 1_000_000;
        uint maximumChangeComplexity = 0;

        foreach (var traitId in canonical)
        {
            if (!byId.TryGetValue(traitId, out var trait) ||
                trait.Prerequisites.Any(prerequisite => !acquired.Contains(prerequisite)) ||
                trait.Incompatibilities.Any(acquired.Contains))
            {
                throw new InvalidOperationException("The acquired trait set is not a valid closed genome.");
            }

            if (trait.EnablesResourceConservation)
            {
                physiology = physiology with
                {
                    Behavior = physiology.Behavior with { ResourceConservation = true },
                };
            }
            mutationModifierQ = MultiplyRatios(mutationModifierQ, trait.MutationIncomeMultiplierQ);
            if (trait.MaximumChangeComplexity.HasValue)
            {
                maximumChangeComplexity = Math.Max(
                    maximumChangeComplexity,
                    trait.MaximumChangeComplexity.Value);
            }
        }

        if (mutationModifierQ is < 250_000 or > 3_000_000 || maximumChangeComplexity == 0)
        {
            throw new InvalidOperationException("The compiled genome violates evolution bounds.");
        }

        var result = new CompiledPhenotype(
            founderGenomeId,
            founder.StableKey,
            founder.DisplayName,
            founderAllocationId,
            canonical,
            founder.Processes,
            physiology,
            mutationModifierQ,
            maximumChangeComplexity,
            string.Empty);
        return result with
        {
            CanonicalCompiledHash = CanonicalRuleHashWriter.HashPhenotype(
                rules.Identity.MechanicsHash,
                result),
        };
    }

    private static CompiledOrganismPhysiology ApplyFounderAllocation(
        CompiledOrganismPhysiology physiology,
        CompiledFounderAllocation allocation)
    {
        var opening = physiology.OpeningMetabolism;
        var chemical = physiology.ChemicalResponse;
        return physiology with
        {
            OpeningMetabolism = opening with
            {
                FavorableCaptureEfficiencyQ = Math.Min(
                    1_000_000,
                    MultiplyRatios(
                        opening.FavorableCaptureEfficiencyQ,
                        allocation.CaptureEfficiencyMultiplierQ)),
            },
            ChemicalResponse = chemical with
            {
                HydrogenSulfideSoftThresholdQ = MultiplyQuantity(
                    chemical.HydrogenSulfideSoftThresholdQ,
                    allocation.ChemicalToleranceMultiplierQ),
                HydrogenSulfideHardThresholdQ = MultiplyQuantity(
                    chemical.HydrogenSulfideHardThresholdQ,
                    allocation.ChemicalToleranceMultiplierQ),
                SulfurDioxideSoftThresholdQ = MultiplyQuantity(
                    chemical.SulfurDioxideSoftThresholdQ,
                    allocation.ChemicalToleranceMultiplierQ),
                SulfurDioxideHardThresholdQ = MultiplyQuantity(
                    chemical.SulfurDioxideHardThresholdQ,
                    allocation.ChemicalToleranceMultiplierQ),
            },
        };
    }

    private static long MultiplyQuantity(long value, uint multiplierQ)
    {
        var numerator = checked((Int128)value * multiplierQ);
        var quotient = numerator / 1_000_000;
        var remainder = numerator % 1_000_000;
        if (remainder > 500_000 || (remainder == 500_000 && (quotient & 1) != 0))
        {
            quotient++;
        }
        return checked((long)quotient);
    }

    private static uint MultiplyRatios(uint left, uint right)
    {
        var numerator = (ulong)left * right;
        var quotient = numerator / 1_000_000;
        var remainder = numerator % 1_000_000;
        if (remainder > 500_000 || (remainder == 500_000 && (quotient & 1) != 0))
        {
            quotient++;
        }
        return checked((uint)quotient);
    }
}

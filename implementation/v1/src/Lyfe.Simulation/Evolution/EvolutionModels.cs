using System.Collections.Immutable;
using System.Security.Cryptography;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.Serialization;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Evolution;

public enum EvolutionAuthorityKind : byte
{
    Controlled = 1,
    Autonomous = 2,
    Locked = 3,
}

public enum SpeciationActorKind : byte
{
    Player = 1,
    Autonomous = 2,
    System = 3,
}

public enum SpeciationFailure : byte
{
    None = 0,
    WrongController = 1,
    SpeciesLocked = 2,
    SpeciesExtinct = 3,
    StaleEvolutionRevision = 4,
    GenomeChanged = 5,
    SpeciationCooldown = 6,
    UnknownTrait = 7,
    TraitAlreadyAcquired = 8,
    MissingPrerequisite = 9,
    IncompatibleTrait = 10,
    ChangeComplexityExceeded = 11,
    InsufficientMutationPoints = 12,
    InvalidTileCount = 13,
    TileNotOccupied = 14,
    AncestorWouldBeDepleted = 15,
}

public sealed record SpeciationCommand(
    SpeciesId AncestorSpeciesId,
    ulong ExpectedEvolutionRevision,
    string ExpectedGenomeHash,
    ImmutableArray<TraitId> NewTraitIds,
    ImmutableArray<TileId> SelectedTileIds,
    SpeciationActorKind ActorKind = SpeciationActorKind.Player,
    bool FollowDescendantIfPermitted = true);

public sealed record FounderCount(TileId TileId, uint Count);

public sealed record SpeciationPreview(
    bool Accepted,
    SpeciationFailure Failure,
    long MutationPriceQ,
    uint ChangeComplexity,
    ImmutableArray<FounderCount> FounderCounts,
    string ProposedGenomeHash);

public sealed record SpeciationResult(
    SpeciationPreview Preview,
    ulong EventId,
    SpeciesId DescendantSpeciesId,
    GenomeId DescendantGenomeId,
    ulong WorldRevision,
    string StateHash);

public sealed record SpeciationEventRecord(
    ulong EventId,
    ulong Tick,
    SpeciesId AncestorSpeciesId,
    SpeciesId DescendantSpeciesId,
    SpeciationActorKind ActorKind,
    ImmutableArray<FounderCount> FounderCounts,
    string FounderSelectionDigest,
    ImmutableArray<TraitId> TraitDelta,
    long MutationPriceQ,
    long BalanceBeforeQ,
    long DuplicatedBalanceAfterQ,
    uint ChangeComplexity);

public readonly record struct EvolutionPressureState(
    uint EnergyShortageQ,
    uint StarvationQ);

internal readonly record struct SpeciesEvolutionAccount(
    EvolutionAuthorityKind Authority,
    long MutationBalanceQ,
    UInt128 MutationIncomeRemainder,
    ulong SpeciationNotBeforeTick,
    uint SpeciationOrdinal,
    ulong EvolutionRevision,
    uint AverageHealthQ,
    long LastIncomeQ,
    EvolutionPressureState Pressure);

internal readonly record struct SpeciesLineageState(
    SpeciesId? ParentSpeciesId,
    ulong FoundingEventId,
    ulong CreatedTick,
    ulong? ExtinctTick,
    ImmutableArray<FounderCount> FounderCounts,
    ImmutableArray<TraitId> AcquiredTraitDelta);

public sealed record AutonomousOpportunityScore(
    ImmutableArray<TraitId> TraitIds,
    long MutationPriceQ,
    uint ChangeComplexity,
    uint PressureMatchQ,
    ulong Weight);

internal static class MutationIncomeMath
{
    public const long MutationPointScale = 1_000_000;
    public const uint RatioScale = 1_000_000;
    public const uint EffectivePopulationTableAlgorithmVersion = 2;
    public const uint MaximumSupportedPopulation = 100_000;
    public static readonly UInt128 Denominator = 750 * (UInt128)RatioScale * RatioScale;
    private const decimal Ln2 = 0.6931471805599453094172321215m;
    private static readonly Lazy<ImmutableArray<long>> Table = new(BuildTable);
    private static readonly Lazy<string> TableHash = new(BuildTableHash);

    public static ImmutableArray<long> EffectivePopulationTable => Table.Value;

    public static string EffectivePopulationTableSha256 => TableHash.Value;

    public static long EffectivePopulationQ(
        ImmutableArray<long> effectivePopulationTable,
        ulong population)
    {
        if (effectivePopulationTable.IsDefault ||
            effectivePopulationTable.Length != MaximumSupportedPopulation + 1 ||
            population > MaximumSupportedPopulation)
        {
            throw new OverflowException("Species population exceeds the compiled mutation-income table.");
        }

        return effectivePopulationTable[checked((int)population)];
    }

    public static (long CreditQ, UInt128 Remainder) Accumulate(
        ImmutableArray<long> effectivePopulationTable,
        ulong population,
        uint averageHealthQ,
        uint mutationModifierQ,
        uint tickDurationHours,
        UInt128 priorRemainder)
    {
        if (population == 0)
        {
            return (0, priorRemainder);
        }

        var numerator = checked((UInt128)EffectivePopulationQ(effectivePopulationTable, population) *
            averageHealthQ * mutationModifierQ * tickDurationHours + priorRemainder);
        var credit = numerator / Denominator;
        if (credit > long.MaxValue)
        {
            throw new OverflowException("Mutation income exceeds the species account range.");
        }
        return ((long)credit, numerator % Denominator);
    }

    private static ImmutableArray<long> BuildTable()
    {
        var result = new long[MaximumSupportedPopulation + 1];
        for (var population = 1; population < result.Length; population++)
        {
            var value = 1m + population / 100m;
            var whole = 0;
            while (value >= 2m)
            {
                value /= 2m;
                whole++;
            }

            var z = (value - 1m) / (value + 1m);
            var zSquared = z * z;
            var term = z;
            var series = 0m;
            for (var odd = 1; odd <= 55; odd += 2)
            {
                series += term / odd;
                term *= zSquared;
            }

            var log2 = whole + 2m * series / Ln2;
            result[population] = decimal.ToInt64(decimal.Round(
                100m * log2 * RatioScale,
                0,
                MidpointRounding.ToEven));
        }
        return result.ToImmutableArray();
    }

    private static string BuildTableHash()
    {
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(EffectivePopulationTableAlgorithmVersion);
        writer.WriteUInt32(checked((uint)EffectivePopulationTable.Length));
        foreach (var value in EffectivePopulationTable)
        {
            writer.WriteInt64(value);
        }
        return Convert.ToHexStringLower(SHA256.HashData(writer.WrittenSpan));
    }
}

public static class AutonomousEvolutionScorer
{
    public static ImmutableArray<AutonomousOpportunityScore> ScoreReachable(
        CompiledRulePack rules,
        ImmutableArray<TraitId> acquiredTraits,
        EvolutionPressureState pressure,
        long availableBalanceQ,
        uint maximumChangeComplexity)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var acquired = acquiredTraits.ToHashSet();
        var scores = ImmutableArray.CreateBuilder<AutonomousOpportunityScore>();
        foreach (var trait in rules.Traits.Where(trait => trait.Selectable && !acquired.Contains(trait.Id)))
        {
            var closure = ClosePrerequisites(trait, rules.Traits, acquired);
            if (closure.IsDefaultOrEmpty ||
                closure.Any(candidate => candidate.Incompatibilities.Any(acquired.Contains)))
            {
                continue;
            }

            var price = closure.Sum(candidate => candidate.MutationPointCostQ);
            var complexity = checked((uint)closure.Sum(candidate => candidate.ChangeComplexity));
            if (price > availableBalanceQ || complexity > maximumChangeComplexity)
            {
                continue;
            }

            var match = trait.PressureTags.Length == 0
                ? 0U
                : trait.PressureTags.Max(tag => tag switch
                {
                    EvolutionPressureTag.EnergyShortage => pressure.EnergyShortageQ,
                    EvolutionPressureTag.Starvation => pressure.StarvationQ,
                    _ => 0U,
                });
            var multiplierQ = checked(1_000_000UL + 3UL * match);
            var weight = checked((ulong)trait.BaseEvolutionWeightQ * multiplierQ / 1_000_000UL);
            scores.Add(new AutonomousOpportunityScore(
                closure.Select(candidate => candidate.Id).OrderBy(id => id.Value).ToImmutableArray(),
                price,
                complexity,
                match,
                weight));
        }

        return scores
            .OrderByDescending(score => score.Weight)
            .ThenBy(score => score.TraitIds[^1].Value)
            .ToImmutableArray();
    }

    private static ImmutableArray<CompiledTrait> ClosePrerequisites(
        CompiledTrait root,
        ImmutableArray<CompiledTrait> traits,
        HashSet<TraitId> acquired)
    {
        var byId = traits.ToDictionary(trait => trait.Id);
        var result = new Dictionary<TraitId, CompiledTrait>();
        var reachable = true;
        void Add(CompiledTrait trait)
        {
            if (acquired.Contains(trait.Id) || result.ContainsKey(trait.Id))
            {
                return;
            }
            if (!trait.Selectable)
            {
                reachable = false;
                return;
            }
            foreach (var prerequisite in trait.Prerequisites)
            {
                Add(byId[prerequisite]);
            }
            if (reachable)
            {
                result.Add(trait.Id, trait);
            }
        }
        Add(root);
        return reachable
            ? result.Values.OrderBy(trait => trait.Id.Value).ToImmutableArray()
            : [];
    }
}

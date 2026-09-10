using System.Collections.Immutable;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Gameplay;

public enum AttentionAlertClass : byte
{
    Informational = 1,
    Strategic = 2,
    Critical = 3,
}

public enum AttentionAlertKind : byte
{
    NotableEventGroup = 1,
    LineageReviewBoundary = 2,
}

public sealed record AttentionAlert(
    ulong AlertId,
    AttentionAlertClass AlertClass,
    AttentionAlertKind Kind,
    NotableEventFamily? EventFamily,
    ulong CompletedTick,
    ulong SimulatedHours,
    SpeciesId SpeciesId,
    ImmutableArray<ulong> ChronicleEventIds,
    string DeduplicationKey);

public readonly record struct PopulationAttentionState(
    SpeciesId SpeciesId,
    bool HasExceededDangerThreshold,
    bool LowPopulationArmed,
    uint LowPopulationEpisodeOrdinal);

public readonly record struct PopulationAttentionEvaluation(
    PopulationAttentionState State,
    bool EnteredLowPopulationBand);

public static class PopulationAttentionRules
{
    public const ulong DangerThreshold = 10;
    public const ulong RecoveryThreshold = 15;

    public static PopulationAttentionEvaluation Evaluate(
        PopulationAttentionState state,
        ulong priorPopulation,
        ulong currentPopulation)
    {
        var hasExceeded = state.HasExceededDangerThreshold ||
            priorPopulation > DangerThreshold || currentPopulation > DangerThreshold;
        var armed = state.LowPopulationArmed;
        if (!state.HasExceededDangerThreshold && currentPopulation > DangerThreshold)
        {
            armed = true;
        }
        else if (!armed && currentPopulation > RecoveryThreshold)
        {
            armed = true;
        }
        var episodeOrdinal = state.LowPopulationEpisodeOrdinal;
        var entered = armed && hasExceeded &&
            priorPopulation > DangerThreshold && currentPopulation <= DangerThreshold;
        if (entered)
        {
            episodeOrdinal = checked(episodeOrdinal + 1);
            armed = false;
        }
        return new PopulationAttentionEvaluation(
            new PopulationAttentionState(
                state.SpeciesId,
                hasExceeded,
                armed,
                episodeOrdinal),
            entered);
    }
}

public readonly record struct PopulationAttentionSample(
    ulong SimulatedHours,
    ulong Population);

public sealed record AttentionWindowState(
    SpeciesId SpeciesId,
    bool PopulationDeclineArmed,
    uint PopulationDeclineEpisodeOrdinal,
    uint LowHealthConsecutiveHours,
    bool LowHealthArmed,
    uint HealthyRecoveryConsecutiveHours,
    uint LowHealthEpisodeOrdinal,
    uint ResourcePressureConsecutiveHours,
    bool ResourcePressureArmed,
    uint ResourcePressureRecoveryConsecutiveHours,
    uint ResourcePressureEpisodeOrdinal,
    ImmutableArray<PopulationAttentionSample> PopulationSamples);

public sealed record AttentionWindowEvaluation(
    AttentionWindowState State,
    bool EnteredPopulationDecline,
    ulong PopulationBaseline,
    uint PopulationDeclineQ,
    bool EnteredSustainedLowHealth,
    bool EnteredSustainedResourcePressure);

public static class AttentionWindowRules
{
    public const ulong PopulationWindowHours = 24;
    public const uint PopulationDeclineThresholdQ = 250_000;
    public const uint PopulationDeclineRecoveryQ = 100_000;
    public const uint LowHealthThresholdQ = 250_000;
    public const uint LowHealthRecoveryQ = 350_000;
    public const uint HealthDurationHours = 6;
    public const uint ResourcePressureThresholdQ = 750_000;
    public const uint ResourcePressureRecoveryQ = 550_000;
    public const uint ResourcePressureDurationHours = 6;

    public static AttentionWindowEvaluation Evaluate(
        AttentionWindowState state,
        ulong simulatedHours,
        uint tickDurationHours,
        ulong population,
        uint averageHealthQ,
        uint averageResourcePressureQ)
    {
        ArgumentOutOfRangeException.ThrowIfZero(tickDurationHours);
        var samples = state.PopulationSamples.Add(
            new PopulationAttentionSample(simulatedHours, population));
        var windowStart = simulatedHours >= PopulationWindowHours
            ? simulatedHours - PopulationWindowHours
            : 0;
        samples = samples.Where(value => value.SimulatedHours >= windowStart)
            .ToImmutableArray();
        var baseline = simulatedHours >= PopulationWindowHours
            ? samples.FirstOrDefault(value => value.SimulatedHours == windowStart).Population
            : 0;
        var declineQ = baseline > population && baseline > 0
            ? checked((uint)(((UInt128)(baseline - population) * 1_000_000) / baseline))
            : 0;
        var declineArmed = state.PopulationDeclineArmed;
        var declineEpisode = state.PopulationDeclineEpisodeOrdinal;
        var enteredDecline = baseline > 0 && declineArmed &&
            declineQ >= PopulationDeclineThresholdQ;
        if (enteredDecline)
        {
            declineArmed = false;
            declineEpisode = checked(declineEpisode + 1);
        }
        else if (!declineArmed && baseline > 0 && declineQ < PopulationDeclineRecoveryQ)
        {
            declineArmed = true;
        }

        var lowHours = population > 0 && state.LowHealthArmed &&
            averageHealthQ < LowHealthThresholdQ
            ? checked(state.LowHealthConsecutiveHours + tickDurationHours)
            : 0;
        var recoveryHours = population > 0 && !state.LowHealthArmed &&
            averageHealthQ > LowHealthRecoveryQ
            ? checked(state.HealthyRecoveryConsecutiveHours + tickDurationHours)
            : 0;
        var healthArmed = state.LowHealthArmed;
        var healthEpisode = state.LowHealthEpisodeOrdinal;
        var enteredLowHealth = healthArmed && lowHours >= HealthDurationHours;
        if (enteredLowHealth)
        {
            healthArmed = false;
            healthEpisode = checked(healthEpisode + 1);
        }
        else if (!healthArmed && recoveryHours >= HealthDurationHours)
        {
            healthArmed = true;
            recoveryHours = 0;
        }

        var pressureHours = population > 0 && state.ResourcePressureArmed &&
            averageResourcePressureQ >= ResourcePressureThresholdQ
            ? checked(state.ResourcePressureConsecutiveHours + tickDurationHours)
            : 0;
        var pressureRecoveryHours = population > 0 && !state.ResourcePressureArmed &&
            averageResourcePressureQ < ResourcePressureRecoveryQ
            ? checked(state.ResourcePressureRecoveryConsecutiveHours + tickDurationHours)
            : 0;
        var pressureArmed = state.ResourcePressureArmed;
        var pressureEpisode = state.ResourcePressureEpisodeOrdinal;
        var enteredPressure = pressureArmed &&
            pressureHours >= ResourcePressureDurationHours;
        if (enteredPressure)
        {
            pressureArmed = false;
            pressureEpisode = checked(pressureEpisode + 1);
        }
        else if (!pressureArmed &&
            pressureRecoveryHours >= ResourcePressureDurationHours)
        {
            pressureArmed = true;
            pressureRecoveryHours = 0;
        }

        return new AttentionWindowEvaluation(
            state with
            {
                PopulationDeclineArmed = declineArmed,
                PopulationDeclineEpisodeOrdinal = declineEpisode,
                LowHealthConsecutiveHours = lowHours,
                LowHealthArmed = healthArmed,
                HealthyRecoveryConsecutiveHours = recoveryHours,
                LowHealthEpisodeOrdinal = healthEpisode,
                ResourcePressureConsecutiveHours = pressureHours,
                ResourcePressureArmed = pressureArmed,
                ResourcePressureRecoveryConsecutiveHours = pressureRecoveryHours,
                ResourcePressureEpisodeOrdinal = pressureEpisode,
                PopulationSamples = samples,
            },
            enteredDecline,
            baseline,
            declineQ,
            enteredLowHealth,
            enteredPressure);
    }
}

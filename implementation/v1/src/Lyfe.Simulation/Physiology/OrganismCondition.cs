using System.Collections.Immutable;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.Physiology;

public static class RatioQ
{
    public const uint Scale = 1_000_000;

    public static uint Divide(long numerator, long denominator)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(numerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(denominator);
        var value = ((UInt128)(ulong)numerator * Scale + (ulong)denominator / 2) /
            (ulong)denominator;
        return checked((uint)UInt128.Min(value, Scale));
    }

    public static uint Multiply(uint left, uint right)
    {
        RequireRatio(left);
        RequireRatio(right);
        return checked((uint)(((ulong)left * right + Scale / 2) / Scale));
    }

    internal static uint Square(uint value) => Multiply(value, value);

    internal static void RequireRatio(uint value)
    {
        if (value > Scale)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "RatioQ must be in [0, 1].");
        }
    }
}

public enum ConditionSnapshotKind : byte
{
    IntrinsicStart = 1,
    Interaction = 2,
    Reproduction = 3,
    End = 4,
}

public readonly record struct OrganismConditionInput(
    long StructuralMatterQ,
    long ChargedReserveQ,
    ulong BiologicalAgeHours);

public readonly record struct OrganismEnvironmentInput(
    int TemperatureMilliC,
    long HydrogenSulfideExposureQ = 0,
    long SulfurDioxideExposureQ = 0)
{
    public static OrganismEnvironmentInput NeutralFounderFixture { get; } = new(45_000, 0, 0);
}

public readonly record struct MaterializedOrganismCondition(
    ulong EvaluatedTick,
    ConditionSnapshotKind SnapshotKind,
    uint ReserveFactorQ,
    uint StructureFactorQ,
    uint NutrientFactorQ,
    uint AgeFactorQ,
    uint LifecycleFactorQ,
    uint EnvironmentalFactorQ,
    uint RelativeHealthQ,
    int TemperatureMilliC,
    uint TemperatureSeverityQ);

public readonly record struct OrganismStorageCapacity(
    long ChargedReserveCapacityQ,
    long DissolvedMacronutrientCapacityLoadQ,
    long FreeMicronutrientCapacityLoadQ,
    long IngestedMatterCapacityLoadQ,
    ProcessAllocationPolicy AllocationPolicy);

public static class OrganismConditionBuilder
{
    public static MaterializedOrganismCondition Build(
        CompiledOrganismPhysiology physiology,
        OrganismConditionInput organism,
        OrganismEnvironmentInput environment,
        ulong evaluatedTick,
        ConditionSnapshotKind snapshotKind)
    {
        ArgumentNullException.ThrowIfNull(physiology);
        ValidatePrimaryState(physiology, organism);

        var reserve = RatioQ.Divide(
            organism.ChargedReserveQ,
            physiology.ChargedReserveCapacityQ);
        var structure = RatioQ.Divide(
            organism.StructuralMatterQ,
            physiology.MatureStructureQ);
        const uint neutral = RatioQ.Scale;
        var age = EvaluateAgeFactor(physiology, organism.BiologicalAgeHours);
        var temperature = EvaluateTemperature(
            physiology.TemperatureResponse,
            environment.TemperatureMilliC);
        var hydrogenSulfide = EvaluateChemicalExposure(
            physiology.ChemicalResponse,
            environment.HydrogenSulfideExposureQ,
            physiology.ChemicalResponse.HydrogenSulfideSoftThresholdQ,
            physiology.ChemicalResponse.HydrogenSulfideHardThresholdQ);
        var sulfurDioxide = EvaluateChemicalExposure(
            physiology.ChemicalResponse,
            environment.SulfurDioxideExposureQ,
            physiology.ChemicalResponse.SulfurDioxideSoftThresholdQ,
            physiology.ChemicalResponse.SulfurDioxideHardThresholdQ);
        var environmentalFactor = RatioQ.Multiply(
            temperature.FactorQ,
            hydrogenSulfide.FactorQ);
        environmentalFactor = RatioQ.Multiply(
            environmentalFactor,
            sulfurDioxide.FactorQ);

        var health = reserve;
        health = RatioQ.Multiply(health, structure);
        health = RatioQ.Multiply(health, neutral); // constitutive nutrient quotas land with internal resources
        health = RatioQ.Multiply(health, age);
        health = RatioQ.Multiply(health, neutral); // the foundation lifecycle is mature and active
        health = RatioQ.Multiply(health, environmentalFactor);

        return new MaterializedOrganismCondition(
            evaluatedTick,
            snapshotKind,
            reserve,
            structure,
            neutral,
            age,
            neutral,
            environmentalFactor,
            health,
            environment.TemperatureMilliC,
            temperature.SeverityQ);
    }

    public static OrganismStorageCapacity GetStorageCapacity(
        CompiledOrganismPhysiology physiology)
    {
        ArgumentNullException.ThrowIfNull(physiology);
        return new OrganismStorageCapacity(
            physiology.ChargedReserveCapacityQ,
            physiology.DissolvedMacronutrientCapacityLoadQ,
            physiology.FreeMicronutrientCapacityLoadQ,
            physiology.IngestedMatterCapacityLoadQ,
            physiology.AllocationPolicy);
    }

    internal static uint EvaluateAgeFactor(
        CompiledOrganismPhysiology physiology,
        ulong biologicalAgeHours)
    {
        if (biologicalAgeHours <= physiology.SenescenceOnsetHours)
        {
            return RatioQ.Scale;
        }

        var excess = biologicalAgeHours - physiology.SenescenceOnsetHours;
        var decline = excess >= physiology.AgeDeclineSpanHours
            ? RatioQ.Scale
            : checked((uint)(((UInt128)excess * RatioQ.Scale +
                physiology.AgeDeclineSpanHours / 2) / physiology.AgeDeclineSpanHours));
        var lossRange = RatioQ.Scale - physiology.MinimumAgeFactorQ;
        var loss = RatioQ.Multiply(lossRange, RatioQ.Square(decline));
        return RatioQ.Scale - loss;
    }

    internal static TemperatureAssessment EvaluateTemperature(
        CompiledTemperatureResponse response,
        int temperatureMilliC)
    {
        if (temperatureMilliC >= response.PreferredMinimumMilliC &&
            temperatureMilliC <= response.PreferredMaximumMilliC)
        {
            return new TemperatureAssessment(RatioQ.Scale, 0, 0);
        }

        var lower = temperatureMilliC < response.PreferredMinimumMilliC;
        var preferred = lower
            ? response.PreferredMinimumMilliC
            : response.PreferredMaximumMilliC;
        var hard = lower ? response.HardMinimumMilliC : response.HardMaximumMilliC;
        var distance = Math.Abs((long)temperatureMilliC - preferred);
        var softToHard = Math.Abs((long)hard - preferred);
        if ((lower && temperatureMilliC >= hard) || (!lower && temperatureMilliC <= hard))
        {
            var severity = RatioQ.Divide(distance, softToHard);
            var penalty = RatioQ.Multiply(
                response.HealthPenaltyAtHardQ,
                RatioQ.Square(severity));
            return new TemperatureAssessment(RatioQ.Scale - penalty, severity, 0);
        }

        var distanceBeyondHard = Math.Abs((long)temperatureMilliC - hard);
        var overageQ = checked((ulong)(((UInt128)(ulong)distanceBeyondHard * RatioQ.Scale +
            (ulong)softToHard / 2) / (ulong)softToHard));
        var onePlus = checked((ulong)RatioQ.Scale + overageQ);
        var squaredScale = (UInt128)onePlus * onePlus;
        var factorAtHard = RatioQ.Scale - response.HealthPenaltyAtHardQ;
        var factor = checked((uint)UInt128.Min(
            ((UInt128)factorAtHard * RatioQ.Scale * RatioQ.Scale + squaredScale / 2) /
                squaredScale,
            RatioQ.Scale));
        factor = Math.Max(factor, response.HealthFactorFloorQ);
        var death = checked((uint)UInt128.Min(
            ((UInt128)response.DeathChanceAtHardQ * squaredScale +
                (UInt128)RatioQ.Scale * RatioQ.Scale / 2) /
                ((UInt128)RatioQ.Scale * RatioQ.Scale),
            response.DeathChanceCapQ));
        return new TemperatureAssessment(factor, RatioQ.Scale, death);
    }

    internal static ChemicalAssessment EvaluateChemicalExposure(
        CompiledChemicalResponse response,
        long exposureQ,
        long softThresholdQ,
        long hardThresholdQ)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exposureQ);
        if (exposureQ <= softThresholdQ)
        {
            return new ChemicalAssessment(RatioQ.Scale, 0, 0);
        }

        if (exposureQ <= hardThresholdQ)
        {
            var severity = RatioQ.Divide(
                exposureQ - softThresholdQ,
                hardThresholdQ - softThresholdQ);
            var penalty = RatioQ.Multiply(
                response.HealthPenaltyAtHardQ,
                RatioQ.Square(severity));
            return new ChemicalAssessment(RatioQ.Scale - penalty, severity, 0);
        }

        var overageQ = checked((ulong)(((UInt128)(ulong)(exposureQ - hardThresholdQ) *
            RatioQ.Scale + (ulong)hardThresholdQ / 2) / (ulong)hardThresholdQ));
        var onePlus = checked((ulong)RatioQ.Scale + overageQ);
        var squaredScale = (UInt128)onePlus * onePlus;
        var factorAtHard = RatioQ.Scale - response.HealthPenaltyAtHardQ;
        var factor = checked((uint)UInt128.Min(
            ((UInt128)factorAtHard * RatioQ.Scale * RatioQ.Scale + squaredScale / 2) /
                squaredScale,
            RatioQ.Scale));
        factor = Math.Max(factor, response.HealthFactorFloorQ);
        var death = checked((uint)UInt128.Min(
            ((UInt128)response.DeathChanceAtHardQ * squaredScale +
                (UInt128)RatioQ.Scale * RatioQ.Scale / 2) /
                ((UInt128)RatioQ.Scale * RatioQ.Scale),
            response.DeathChanceCapQ));
        return new ChemicalAssessment(factor, RatioQ.Scale, death);
    }

    internal static void ValidatePrimaryState(
        CompiledOrganismPhysiology physiology,
        OrganismConditionInput organism)
    {
        if (organism.StructuralMatterQ < 0 ||
            organism.ChargedReserveQ < 0 ||
            organism.ChargedReserveQ > physiology.ChargedReserveCapacityQ)
        {
            throw new InvalidOperationException(
                "Organism structure and reserve must satisfy compiled capacity invariants.");
        }
    }
}

internal readonly record struct TemperatureAssessment(
    uint FactorQ,
    uint SeverityQ,
    uint DeathProbabilityQ);

internal readonly record struct ChemicalAssessment(
    uint FactorQ,
    uint SeverityQ,
    uint DeathProbabilityQ);

public enum IntrinsicDeathCause : byte
{
    ReserveExhaustion = 1,
    StructuralFailure = 2,
    Senescence = 3,
    TemperatureExposure = 4,
    HydrogenSulfideExposure = 5,
    SulfurDioxideExposure = 6,
    MaintenanceFailure = 7,
}

public readonly record struct IntrinsicDeathCauseEvidence(
    IntrinsicDeathCause Cause,
    uint ProbabilityQ,
    ulong? RandomWord,
    bool Triggered,
    long ObservedValue,
    long BoundaryValue);

public readonly record struct IntrinsicDeathEvidence(
    OrganismId OrganismId,
    ulong Tick,
    ImmutableArray<IntrinsicDeathCauseEvidence> NonzeroCauses,
    IntrinsicDeathCause? ActualCause)
{
    public bool Died => ActualCause is not null;
}

public static class IntrinsicDeathEvaluator
{
    private const ulong TemperatureExposureId = 1;
    private const ulong HydrogenSulfideExposureId = 2;
    private const ulong SulfurDioxideExposureId = 3;

    public static IntrinsicDeathEvidence Evaluate(
        OrganismId organismId,
        ulong tick,
        CompiledOrganismPhysiology physiology,
        OrganismConditionInput organism,
        OrganismEnvironmentInput environment,
        ISimulationRandom random)
    {
        ArgumentNullException.ThrowIfNull(physiology);
        ArgumentNullException.ThrowIfNull(random);
        OrganismConditionBuilder.ValidatePrimaryState(physiology, organism);
        var reserveFailure = organism.ChargedReserveQ <= physiology.TerminalReserveThresholdQ;
        var structureFailure = organism.StructuralMatterQ < physiology.StructuralHardFloorQ;
        var senescenceProbability = SenescenceProbability(
            physiology,
            organism.BiologicalAgeHours);
        var temperature = OrganismConditionBuilder.EvaluateTemperature(
            physiology.TemperatureResponse,
            environment.TemperatureMilliC);
        var hydrogenSulfide = OrganismConditionBuilder.EvaluateChemicalExposure(
            physiology.ChemicalResponse,
            environment.HydrogenSulfideExposureQ,
            physiology.ChemicalResponse.HydrogenSulfideSoftThresholdQ,
            physiology.ChemicalResponse.HydrogenSulfideHardThresholdQ);
        var sulfurDioxide = OrganismConditionBuilder.EvaluateChemicalExposure(
            physiology.ChemicalResponse,
            environment.SulfurDioxideExposureQ,
            physiology.ChemicalResponse.SulfurDioxideSoftThresholdQ,
            physiology.ChemicalResponse.SulfurDioxideHardThresholdQ);
        var causeCount = (reserveFailure ? 1 : 0) +
            (structureFailure ? 1 : 0) +
            (senescenceProbability > 0 ? 1 : 0) +
            (temperature.DeathProbabilityQ > 0 ? 1 : 0) +
            (hydrogenSulfide.DeathProbabilityQ > 0 ? 1 : 0) +
            (sulfurDioxide.DeathProbabilityQ > 0 ? 1 : 0);
        if (causeCount == 0)
        {
            return new IntrinsicDeathEvidence(organismId, tick, [], null);
        }

        var causes = ImmutableArray.CreateBuilder<IntrinsicDeathCauseEvidence>(causeCount);

        if (reserveFailure)
        {
            causes.Add(Certain(
                IntrinsicDeathCause.ReserveExhaustion,
                organism.ChargedReserveQ,
                physiology.TerminalReserveThresholdQ));
        }

        if (structureFailure)
        {
            causes.Add(Certain(
                IntrinsicDeathCause.StructuralFailure,
                organism.StructuralMatterQ,
                physiology.StructuralHardFloorQ));
        }

        if (senescenceProbability > 0)
        {
            var decision = random.Bernoulli(
                RandomAddress.Create(
                    RandomDomains.SenescenceDeath,
                    tick,
                    organismId.Value,
                    0),
                senescenceProbability);
            causes.Add(new IntrinsicDeathCauseEvidence(
                IntrinsicDeathCause.Senescence,
                decision.ProbabilityQ,
                decision.RawWord,
                decision.Triggered,
                checked((long)Math.Min(organism.BiologicalAgeHours, long.MaxValue)),
                checked((long)physiology.SenescenceOnsetHours)));
        }

        if (temperature.DeathProbabilityQ > 0)
        {
            var decision = random.Bernoulli(
                RandomAddress.Create(
                    RandomDomains.IntrinsicExposureDeath,
                    tick,
                    organismId.Value,
                    TemperatureExposureId),
                temperature.DeathProbabilityQ);
            var boundary = environment.TemperatureMilliC <
                physiology.TemperatureResponse.HardMinimumMilliC
                ? physiology.TemperatureResponse.HardMinimumMilliC
                : physiology.TemperatureResponse.HardMaximumMilliC;
            causes.Add(new IntrinsicDeathCauseEvidence(
                IntrinsicDeathCause.TemperatureExposure,
                decision.ProbabilityQ,
                decision.RawWord,
                decision.Triggered,
                environment.TemperatureMilliC,
                boundary));
        }

        AddChemicalExposureCause(
            causes,
            random,
            organismId,
            tick,
            IntrinsicDeathCause.HydrogenSulfideExposure,
            HydrogenSulfideExposureId,
            hydrogenSulfide.DeathProbabilityQ,
            environment.HydrogenSulfideExposureQ,
            physiology.ChemicalResponse.HydrogenSulfideHardThresholdQ);
        AddChemicalExposureCause(
            causes,
            random,
            organismId,
            tick,
            IntrinsicDeathCause.SulfurDioxideExposure,
            SulfurDioxideExposureId,
            sulfurDioxide.DeathProbabilityQ,
            environment.SulfurDioxideExposureQ,
            physiology.ChemicalResponse.SulfurDioxideHardThresholdQ);

        var evidence = causes.MoveToImmutable();
        var actual = evidence
            .Where(cause => cause.Triggered)
            .Select(cause => (IntrinsicDeathCause?)cause.Cause)
            .FirstOrDefault();
        return new IntrinsicDeathEvidence(organismId, tick, evidence, actual);
    }

    internal static uint SenescenceProbability(
        CompiledOrganismPhysiology physiology,
        ulong biologicalAgeHours)
    {
        if (biologicalAgeHours < physiology.SenescenceOnsetHours)
        {
            return 0;
        }

        var excess = biologicalAgeHours - physiology.SenescenceOnsetHours;
        var numerator = (UInt128)excess + physiology.SenescenceRiskEscalationHours;
        var denominator = physiology.SenescenceRiskEscalationHours;
        var probability = ((UInt128)physiology.SenescenceRiskBaseQ * numerator * numerator +
            (UInt128)denominator * denominator / 2) /
            ((UInt128)denominator * denominator);
        return checked((uint)UInt128.Min(probability, physiology.SenescenceRiskCapQ));
    }

    private static IntrinsicDeathCauseEvidence Certain(
        IntrinsicDeathCause cause,
        long observed,
        long boundary) =>
        new(cause, RatioQ.Scale, null, true, observed, boundary);

    private static void AddChemicalExposureCause(
        ImmutableArray<IntrinsicDeathCauseEvidence>.Builder causes,
        ISimulationRandom random,
        OrganismId organismId,
        ulong tick,
        IntrinsicDeathCause cause,
        ulong exposureId,
        uint probabilityQ,
        long observedQ,
        long boundaryQ)
    {
        if (probabilityQ == 0)
        {
            return;
        }

        var decision = random.Bernoulli(
            RandomAddress.Create(
                RandomDomains.IntrinsicExposureDeath,
                tick,
                organismId.Value,
                exposureId),
            probabilityQ);
        causes.Add(new IntrinsicDeathCauseEvidence(
            cause,
            decision.ProbabilityQ,
            decision.RawWord,
            decision.Triggered,
            observedQ,
            boundaryQ));
    }
}

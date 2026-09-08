using System.Collections.Immutable;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.Spatial;
using Lyfe.Simulation.World.Generation;

namespace Lyfe.Simulation.Ticks;

internal sealed class TickScratch
{
    private ImmutableArray<ExternalCaptureIntent> externalCaptureIntents;
    private bool hasExternalCaptureIntents;
    private ImmutableArray<IntrinsicDeathEvidence> intrinsicDeathAssessments;
    private bool hasIntrinsicDeathAssessments;
    private ImmutableArray<DeathRecord> deathRecords;
    private bool hasDeathRecords;
    private ImmutableArray<AcquisitionCoverageSample> acquisitionSamples;
    private bool hasAcquisitionSamples;
    private readonly List<ReproductionActivityReceipt> reproductionReceipts = [];
    private readonly List<ScavengeActivityReceipt> scavengeReceipts = [];
    private readonly List<StressActivityReceipt> stressReceipts = [];
    private readonly List<BehaviorTransitionActivityReceipt> behaviorTransitionReceipts = [];

    public TickScratch(ulong tick)
    {
        ArgumentOutOfRangeException.ThrowIfZero(tick);
        Tick = tick;
    }

    public ulong Tick { get; }

    public void PublishExternalCaptureIntents(
        ulong tick,
        ImmutableArray<ExternalCaptureIntent> intents)
    {
        if (tick != Tick || hasExternalCaptureIntents)
        {
            throw new InvalidOperationException(
                "External-capture intents must be published exactly once for their tick.");
        }

        externalCaptureIntents = intents;
        hasExternalCaptureIntents = true;
    }

    public ImmutableArray<ExternalCaptureIntent> RequireExternalCaptureIntents(ulong tick)
    {
        if (tick != Tick || !hasExternalCaptureIntents)
        {
            throw new InvalidOperationException(
                "External-capture resolution requires the current tick's sealed intents.");
        }

        return externalCaptureIntents;
    }

    public void PublishIntrinsicDeathAssessments(
        ulong tick,
        ImmutableArray<IntrinsicDeathEvidence> assessments)
    {
        if (tick != Tick || hasIntrinsicDeathAssessments)
        {
            throw new InvalidOperationException(
                "Intrinsic-death assessments must be published exactly once for their tick.");
        }

        intrinsicDeathAssessments = assessments;
        hasIntrinsicDeathAssessments = true;
    }

    public ImmutableArray<IntrinsicDeathEvidence> RequireIntrinsicDeathAssessments(ulong tick)
    {
        if (tick != Tick || !hasIntrinsicDeathAssessments)
        {
            throw new InvalidOperationException(
                "Intrinsic-death assessments are not available for this tick.");
        }

        return intrinsicDeathAssessments;
    }

    public void PublishDeathRecords(ulong tick, ImmutableArray<DeathRecord> records)
    {
        if (tick != Tick || hasDeathRecords)
        {
            throw new InvalidOperationException(
                "Death records must be published exactly once for their tick.");
        }

        deathRecords = records;
        hasDeathRecords = true;
    }

    public ImmutableArray<DeathRecord> RequireDeathRecords(ulong tick)
    {
        if (tick != Tick || !hasDeathRecords)
        {
            throw new InvalidOperationException("Death records are not available for this tick.");
        }

        return deathRecords;
    }

    public void AppendDeathRecords(ulong tick, ImmutableArray<DeathRecord> records)
    {
        if (tick != Tick || !hasDeathRecords)
        {
            throw new InvalidOperationException(
                "Later-phase death records require the current tick's intrinsic-death publication.");
        }
        if (records.IsDefaultOrEmpty)
        {
            return;
        }
        if (deathRecords.Select(record => record.OrganismId)
            .Intersect(records.Select(record => record.OrganismId)).Any())
        {
            throw new InvalidOperationException("An organism cannot die twice in one tick.");
        }

        deathRecords = deathRecords
            .Concat(records)
            .OrderBy(record => record.OrganismId.Value)
            .ToImmutableArray();
    }

    public void PublishAcquisitionSamples(
        ulong tick,
        ImmutableArray<AcquisitionCoverageSample> samples)
    {
        if (tick != Tick || hasAcquisitionSamples)
        {
            throw new InvalidOperationException(
                "Acquisition samples must be published exactly once for their tick.");
        }

        acquisitionSamples = samples;
        hasAcquisitionSamples = true;
    }

    public ImmutableArray<AcquisitionCoverageSample> RequireAcquisitionSamples(ulong tick)
    {
        if (tick != Tick || !hasAcquisitionSamples)
        {
            throw new InvalidOperationException(
                "Acquisition samples are not available for this tick.");
        }

        return acquisitionSamples;
    }

    public void AppendReproductionReceipt(ulong tick, ReproductionActivityReceipt receipt)
    {
        RequireTick(tick);
        reproductionReceipts.Add(receipt);
    }

    public ImmutableArray<ReproductionActivityReceipt> GetReproductionReceipts(ulong tick)
    {
        RequireTick(tick);
        return reproductionReceipts
            .OrderBy(value => value.ParentId.Value)
            .ThenBy(value => value.OffspringId.Value)
            .ToImmutableArray();
    }

    public void AppendScavengeReceipt(ulong tick, ScavengeActivityReceipt receipt)
    {
        RequireTick(tick);
        scavengeReceipts.Add(receipt);
    }

    public ImmutableArray<ScavengeActivityReceipt> GetScavengeReceipts(ulong tick)
    {
        RequireTick(tick);
        return scavengeReceipts
            .OrderBy(value => value.OrganismId.Value)
            .ThenBy(value => value.RemnantId.Value)
            .ToImmutableArray();
    }

    public void AppendStressReceipt(ulong tick, StressActivityReceipt receipt)
    {
        RequireTick(tick);
        stressReceipts.Add(receipt);
    }

    public ImmutableArray<StressActivityReceipt> GetStressReceipts(ulong tick)
    {
        RequireTick(tick);
        return stressReceipts.OrderBy(value => value.OrganismId.Value).ToImmutableArray();
    }

    public void AppendBehaviorTransitionReceipt(
        ulong tick,
        BehaviorTransitionActivityReceipt receipt)
    {
        RequireTick(tick);
        behaviorTransitionReceipts.Add(receipt);
    }

    public ImmutableArray<BehaviorTransitionActivityReceipt> GetBehaviorTransitionReceipts(
        ulong tick)
    {
        RequireTick(tick);
        return behaviorTransitionReceipts
            .OrderBy(value => value.OrganismId.Value)
            .ToImmutableArray();
    }

    private void RequireTick(ulong tick)
    {
        if (tick != Tick)
        {
            throw new InvalidOperationException("Activity receipts belong to one tick.");
        }
    }
}

internal readonly record struct AcquisitionCoverageSample(
    OrganismId OrganismId,
    long UsefulDemandQ,
    long GrantedQ);

internal readonly record struct ScavengeActivityReceipt(
    OrganismId OrganismId,
    RemnantId RemnantId,
    TileId TileId,
    long GrantedReserveQ,
    long GrantedStructureQ);

internal readonly record struct StressActivityReceipt(
    OrganismId OrganismId,
    SpeciesId SpeciesId,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    byte PriorBand,
    byte CurrentBand,
    uint EnvironmentalFactorQ);

internal readonly record struct BehaviorTransitionActivityReceipt(
    OrganismId OrganismId,
    SpeciesId SpeciesId,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    Behavior.OrganismBehaviorId PriorBehavior,
    Behavior.OrganismBehaviorId CurrentBehavior);

internal readonly record struct ExternalCaptureIntent(
    OrganismId OrganismId,
    TileId TileId,
    ReactionHandle Reaction,
    long RequestedExtent,
    long ExpectedReserveQ,
    long ReserveCapacityQ,
    RemnantId? ScavengeTargetId,
    long RequestedScavengeReserveQ,
    long RequestedScavengeStructureQ,
    long ScavengeActionCostQ,
    ulong ScavengeCooldownHours);

internal readonly record struct ExternalIntentCandidate(
    OrganismId OrganismId,
    TileId TileId,
    ReactionHandle? Reaction,
    long CurrentReserveQ,
    long ReserveCapacityQ,
    uint PositionXQ,
    uint PositionYQ,
    ulong ScavengeNotBeforeTick,
    long IngestedStructuralMatterQ,
    long MatureStructureQ,
    long IngestedMatterCapacityQ,
    bool SupportsParticulateDigestion,
    CompiledOpeningMetabolismProfile OpeningMetabolism,
    long CaptureExtentLimit,
    CompiledRecyclingProfile Recycling);

internal sealed record ExternalIntentView(
    PhaseViewStamp Stamp,
    CompiledRulePack Rules,
    ImmutableArray<ExternalIntentCandidate> Candidates,
    ImmutableDictionary<RemnantId, RemnantSnapshot> Remnants,
    SpatialEntityIndex SpatialIndex) : IPhaseReadView
{
    public int WorkCount => Candidates.Length;
}

internal sealed class ExternalCaptureIntentPhase :
    ScalarTickPhase<
        ExternalIntentView,
        ExternalCaptureIntent,
        ImmutableArray<ExternalCaptureIntent>>
{
    private const uint IntentOutcomeCategory = 1;

    public override TickPhase Phase => TickPhase.ExternalIntent;

    protected override PhaseExecutionClass ExecutionClass => PhaseExecutionClass.IndependentMap;

    protected override ExternalIntentView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var organismIds = world.GetOrganismIdsInCanonicalOrder();
        var candidates = ImmutableArray.CreateBuilder<ExternalIntentCandidate>(organismIds.Length);
        var captureLimits = new Dictionary<
            (TileId TileId, CompiledOpeningMetabolismProfile Metabolism),
            long>();
        foreach (var organismId in organismIds)
        {
            var organism = world.GetOrganism(organismId);
            var phenotype = world.GetCompiledPhenotype(organism.SpeciesId);
            ReactionHandle? reaction = null;
            foreach (var process in phenotype.Processes)
            {
                var compiledReaction = world.Rules.RulePack.Reactions[process.Reaction.DenseSlot];
                if (compiledReaction.ProcessKind == ProcessKind.ExternalEnergyCapture)
                {
                    reaction = process.Reaction;
                    break;
                }
            }

            var metabolism = phenotype.Physiology.OpeningMetabolism;
            var captureKey = (organism.TileId, metabolism);
            if (!captureLimits.TryGetValue(captureKey, out var captureExtentLimit))
            {
                captureExtentLimit = CalculateCaptureExtentLimit(
                    world.Rules,
                    organism.TileId,
                    metabolism,
                    context.Tick,
                    context.TickDurationHours);
                captureLimits.Add(captureKey, captureExtentLimit);
            }

            candidates.Add(new ExternalIntentCandidate(
                organismId,
                organism.TileId,
                reaction,
                organism.ChargedReserveQ,
                phenotype.Physiology.ChargedReserveCapacityQ,
                organism.PositionXQ,
                organism.PositionYQ,
                organism.ScavengeNotBeforeTick,
                organism.IngestedStructuralMatterQ,
                phenotype.Physiology.MatureStructureQ,
                phenotype.Physiology.IngestedMatterCapacityLoadQ,
                phenotype.Processes.Any(process =>
                    world.Rules.RulePack.Reactions[process.Reaction.DenseSlot].ProcessKind ==
                    ProcessKind.ParticulateDigestion),
                metabolism,
                captureExtentLimit,
                phenotype.Physiology.Recycling));
        }

        var remnantSnapshots = world.GetRemnantIdsInCanonicalOrder()
            .Select(world.GetRemnant)
            .ToImmutableArray();
        var index = new SpatialEntityIndex(
            organismIds.Select(id =>
            {
                var organism = world.GetOrganism(id);
                var spatial = world.GetCompiledPhenotype(organism.SpeciesId).Physiology.Spatial;
                return new IndexedOrganism(
                    id,
                    organism.TileId,
                    organism.PositionXQ,
                    organism.PositionYQ,
                    SpatialMath.BodyRadiusQ(spatial, organism.StructuralMatterQ));
            }),
            remnantSnapshots.Select(remnant =>
            {
                var spatial = world.GetCompiledPhenotype(remnant.SourceSpeciesId).Physiology.Spatial;
                return new IndexedRemnant(
                    remnant.Id,
                    remnant.TileId,
                    remnant.PositionXQ,
                    remnant.PositionYQ,
                    SpatialMath.BodyRadiusQ(spatial, remnant.StructuralMatterQ));
            }));

        return new ExternalIntentView(
            CreateStamp(world, context),
            world.Rules.RulePack,
            candidates.MoveToImmutable(),
            remnantSnapshots.ToImmutableDictionary(remnant => remnant.Id),
            index);
    }

    protected override ImmutableArray<PhaseOutcome<ExternalCaptureIntent>> Evaluate(
        ExternalIntentView view,
        TickExecutionContext context)
    {
        var outcomes = ImmutableArray.CreateBuilder<PhaseOutcome<ExternalCaptureIntent>>(
            view.Candidates.Length);
        foreach (var candidate in view.Candidates)
        {
            var requestedExtent = 0L;
            var reactionId = 0U;
            var reactionHandle = candidate.Reaction.GetValueOrDefault();
            if (candidate.Reaction is not null)
            {
                reactionId = reactionHandle.Id.Value;
                var reaction = RequireReaction(view.Rules, reactionHandle);
                var reserveOutputPerExtent = reaction.StoredEnergyQ;
                if (reserveOutputPerExtent > 0 && candidate.CaptureExtentLimit > 0 &&
                    candidate.CurrentReserveQ < candidate.ReserveCapacityQ)
                {
                    var capacityExtent =
                        (candidate.ReserveCapacityQ - candidate.CurrentReserveQ) /
                        reserveOutputPerExtent;
                    requestedExtent = Math.Min(candidate.CaptureExtentLimit, capacityExtent);
                }
            }


            RemnantId? targetId = null;
            var requestedScavenge = 0L;
            var requestedStructure = 0L;
            if ((candidate.Recycling.SimpleRemnantScavenging ||
                    candidate.SupportsParticulateDigestion) &&
                context.Tick >= candidate.ScavengeNotBeforeTick &&
                candidate.CurrentReserveQ >= candidate.Recycling.ScavengeActionCostQ)
            {
                var target = view.SpatialIndex
                    .QueryRemnantsForFeeding(
                        candidate.TileId,
                        candidate.PositionXQ,
                        candidate.PositionYQ,
                        candidate.Recycling.ScavengeRangeQ)
                    .Select(indexed => view.Remnants[indexed.Id])
                    .Where(remnant =>
                        (candidate.Recycling.SimpleRemnantScavenging &&
                            remnant.ChargedReserveQ > 0) ||
                        (candidate.SupportsParticulateDigestion &&
                            remnant.StructuralMatterQ > 0))
                    .FirstOrDefault();
                if (target.Id != default)
                {
                    targetId = target.Id;
                    if (candidate.Recycling.SimpleRemnantScavenging)
                    {
                        requestedScavenge = Math.Min(
                            candidate.Recycling.ScavengeReserveCapQ,
                            target.ChargedReserveQ);
                    }
                    if (candidate.SupportsParticulateDigestion)
                    {
                        requestedStructure = Math.Min(
                            Math.Min(candidate.MatureStructureQ / 20, target.StructuralMatterQ),
                            candidate.IngestedMatterCapacityQ -
                                candidate.IngestedStructuralMatterQ);
                    }
                }
            }

            if (requestedStructure > 0 &&
                candidate.CurrentReserveQ < candidate.Recycling.ParticulateScavengeActionCostQ)
            {
                requestedStructure = 0;
            }
            var scavengeActionCost = requestedStructure > 0
                ? candidate.Recycling.ParticulateScavengeActionCostQ
                : candidate.Recycling.ScavengeActionCostQ;
            if (targetId is not null &&
                (requestedScavenge == 0 && requestedStructure == 0 ||
                    candidate.CurrentReserveQ < scavengeActionCost))
            {
                targetId = null;
                requestedScavenge = 0;
                requestedStructure = 0;
                scavengeActionCost = 0;
            }

            outcomes.Add(new PhaseOutcome<ExternalCaptureIntent>(
                view.Stamp,
                new OutcomeKey(
                    Phase,
                    IntentOutcomeCategory,
                    candidate.TileId.Value,
                    candidate.OrganismId.Value,
                    reactionId,
                    0),
                new ExternalCaptureIntent(
                    candidate.OrganismId,
                    candidate.TileId,
                    reactionHandle,
                    requestedExtent,
                    candidate.CurrentReserveQ,
                    candidate.ReserveCapacityQ,
                    targetId,
                    requestedScavenge,
                    requestedStructure,
                    scavengeActionCost,
                    candidate.Recycling.ScavengeCooldownHours)));
        }

        return outcomes.MoveToImmutable();
    }

    protected override ImmutableArray<ExternalCaptureIntent> Preflight(
        ExternalIntentView view,
        ImmutableArray<PhaseOutcome<ExternalCaptureIntent>> outcomes,
        TickExecutionContext context) =>
        outcomes
            .Select(outcome => outcome.Payload)
            .Where(intent => intent.RequestedExtent > 0 || intent.ScavengeTargetId is not null)
            .ToImmutableArray();

    protected override void Commit(
        MutableWorldState world,
        ImmutableArray<ExternalCaptureIntent> plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context) =>
        context.Scratch.PublishExternalCaptureIntents(context.Tick, plan);

    private static CompiledReaction RequireReaction(
        CompiledRulePack rules,
        ReactionHandle handle)
    {
        // The handle was read from the immutable compiled phenotype. Its dense-slot
        // validity is checked by world invariants and by the transaction factory.
        return rules.Reactions[handle.DenseSlot];
    }

    private static ulong CountCaptureHours(
        CompiledOpeningMetabolismProfile profile,
        ulong tick,
        uint tickDurationHours)
    {
        if (!profile.RequiresLight)
        {
            return tickDurationHours;
        }

        const ulong hoursPerDay = 24;
        var startHour = checked((tick - 1) * tickDurationHours);
        var fullDays = tickDurationHours / hoursPerDay;
        var remainderHours = tickDurationHours % hoursPerDay;
        var illuminated = checked(fullDays * profile.IlluminatedHoursPerDay);
        var localHour = startHour % hoursPerDay;
        for (var offset = 0UL; offset < remainderHours; offset++)
        {
            if ((localHour + offset) % hoursPerDay < profile.IlluminatedHoursPerDay)
            {
                illuminated++;
            }
        }

        return illuminated;
    }

    private static long CalculateCaptureExtentLimit(
        CompiledWorldRules rules,
        TileId tileId,
        CompiledOpeningMetabolismProfile profile,
        ulong tick,
        uint tickDurationHours)
    {
        if (!profile.RequiresLight || rules.GeneratedWorld is null)
        {
            var captureHours = CountCaptureHours(profile, tick, tickDurationHours);
            var throughput =
                (UInt128)profile.MaximumCaptureExtentsPerHour *
                captureHours * profile.FavorableCaptureEfficiencyQ /
                RatioQ.Scale;
            return throughput > (UInt128)long.MaxValue
                ? long.MaxValue
                : (long)throughput;
        }

        var world = rules.GeneratedWorld;
        var tile = world.GetTile(tileId.Value);
        var startHour = checked((tick - 1) * tickDurationHours);
        var total = 0L;
        for (var offset = 0U; offset < tickDurationHours; offset++)
        {
            var lightQ = WorldClimateEvaluator.Evaluate(
                world,
                tile,
                checked(startHour + offset)).AccessibleLightQ;
            var hourly = (UInt128)profile.GeneratedLightCaptureExtentsPerUnitHour *
                profile.FavorableCaptureEfficiencyQ * lightQ /
                ((UInt128)RatioQ.Scale * RatioQ.Scale);
            total = checked(total + (hourly > (UInt128)long.MaxValue
                ? long.MaxValue
                : (long)hourly));
        }
        return total;
    }

}

internal readonly record struct TileResourcePhaseValue(
    TileId TileId,
    ResourceHandle Resource,
    long QuantityQ);

internal sealed record ExternalResolutionView(
    PhaseViewStamp Stamp,
    CompiledRulePack Rules,
    ImmutableArray<ExternalCaptureIntent> Intents,
    ImmutableArray<TileResourcePhaseValue> TileResources,
    ImmutableArray<RemnantSnapshot> Remnants) : IPhaseReadView
{
    public int WorkCount => Intents.Length;

    public long GetTileResource(TileId tileId, ResourceHandle resource)
    {
        foreach (var value in TileResources)
        {
            if (value.TileId == tileId && value.Resource == resource)
            {
                return value.QuantityQ;
            }
        }

        throw new InvalidOperationException(
            $"The external-resolution view did not capture tile {tileId} resource {resource.Id}.");
    }
}

internal sealed record ExternalResolutionPlan(
    ImmutableArray<ResourceTransaction> Transactions,
    LedgerReconciliationReport Reconciliation,
    ImmutableArray<ExpectedMatterAccountBalance> ExpectedFinalBalances,
    ImmutableArray<ScavengeTransfer> ScavengeTransfers,
    ImmutableArray<AcquisitionCoverageSample> AcquisitionSamples);

internal readonly record struct ScavengeTransfer(
    OrganismId OrganismId,
    RemnantId RemnantId,
    TileId TileId,
    long GrantedReserveQ,
    long GrantedStructureQ,
    long ActionCostQ,
    ulong NotBeforeTick);

internal readonly record struct ExpectedMatterAccountBalance(
    MatterAccountKey Account,
    long QuantityQ);

internal readonly record struct CaptureGrant(
    ExternalCaptureIntent Intent,
    long GrantedExtent);

internal sealed class ExternalCaptureResolutionPhase :
    ScalarTickPhase<
        ExternalResolutionView,
        ExternalCaptureIntent,
        ExternalResolutionPlan>
{
    private const uint ResolutionOutcomeCategory = 1;

    public override TickPhase Phase => TickPhase.ExternalResolution;

    protected override PhaseExecutionClass ExecutionClass => PhaseExecutionClass.MapThenGroupedResolve;

    protected override ExternalResolutionView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var intents = context.Scratch.RequireExternalCaptureIntents(context.Tick);
        var resources = new List<TileResourcePhaseValue>();
        foreach (var intent in intents)
        {
            if (intent.RequestedExtent == 0)
            {
                continue;
            }

            var reaction = world.Rules.RulePack.Reactions[intent.Reaction.DenseSlot];
            foreach (var input in reaction.Inputs)
            {
                if (resources.Any(value =>
                        value.TileId == intent.TileId && value.Resource == input.Resource))
                {
                    continue;
                }

                resources.Add(new TileResourcePhaseValue(
                    intent.TileId,
                    input.Resource,
                    world.GetTileResource(intent.TileId, input.Resource)));
            }
            foreach (var output in reaction.Outputs)
            {
                var resource = world.Rules.RulePack.Resources[output.Resource.DenseSlot];
                if (resource.BiologicalForm != BiologicalForm.Inorganic ||
                    resources.Any(value =>
                        value.TileId == intent.TileId && value.Resource == output.Resource))
                {
                    continue;
                }

                resources.Add(new TileResourcePhaseValue(
                    intent.TileId,
                    output.Resource,
                    world.GetTileResource(intent.TileId, output.Resource)));
            }
        }

        resources.Sort(static (left, right) =>
        {
            var comparison = left.TileId.Value.CompareTo(right.TileId.Value);
            return comparison != 0
                ? comparison
                : left.Resource.Id.Value.CompareTo(right.Resource.Id.Value);
        });
        return new ExternalResolutionView(
            CreateStamp(world, context),
            world.Rules.RulePack,
            intents,
            [.. resources],
            intents
                .Where(intent => intent.ScavengeTargetId is not null)
                .Select(intent => world.GetRemnant(intent.ScavengeTargetId!.Value))
                .DistinctBy(remnant => remnant.Id)
                .OrderBy(remnant => remnant.Id.Value)
                .ToImmutableArray());
    }

    protected override ImmutableArray<PhaseOutcome<ExternalCaptureIntent>> Evaluate(
        ExternalResolutionView view,
        TickExecutionContext context)
    {
        var outcomes = ImmutableArray.CreateBuilder<PhaseOutcome<ExternalCaptureIntent>>(
            view.Intents.Length);
        foreach (var intent in view.Intents)
        {
            outcomes.Add(new PhaseOutcome<ExternalCaptureIntent>(
                view.Stamp,
                new OutcomeKey(
                    Phase,
                    ResolutionOutcomeCategory,
                    intent.TileId.Value,
                    intent.OrganismId.Value,
                    intent.Reaction.Id.Value,
                    0),
                intent));
        }

        return outcomes.MoveToImmutable();
    }

    protected override ExternalResolutionPlan Preflight(
        ExternalResolutionView view,
        ImmutableArray<PhaseOutcome<ExternalCaptureIntent>> outcomes,
        TickExecutionContext context)
    {
        var admitted = new List<ExternalCaptureIntent>();
        var groups = outcomes
            .Select(outcome => outcome.Payload)
            .Where(intent => intent.RequestedExtent > 0)
            .GroupBy(intent => (intent.TileId, intent.Reaction))
            .OrderBy(group => group.Key.TileId.Value)
            .ThenBy(group => group.Key.Reaction.Id.Value);

        foreach (var group in groups)
        {
            var reaction = RequireReaction(view.Rules, group.Key.Reaction);
            if (reaction.Inputs.IsDefaultOrEmpty)
            {
                throw new InvalidOperationException(
                    "External capture requires at least one finite tile input.");
            }

            var supplyExtent = long.MaxValue;
            foreach (var input in reaction.Inputs)
            {
                supplyExtent = Math.Min(
                    supplyExtent,
                    view.GetTileResource(group.Key.TileId, input.Resource) / input.Quantity);
            }

            var candidates = group.OrderBy(intent => intent.OrganismId.Value).ToArray();
            var totalDemand = candidates.Aggregate(
                0L,
                static (sum, intent) => checked(sum + intent.RequestedExtent));
            var grantedTotal = Math.Min(supplyExtent, totalDemand);
            if (grantedTotal == totalDemand)
            {
                admitted.AddRange(candidates);
                continue;
            }

            var grants = candidates
                .Select(intent => new CaptureGrant(
                    intent,
                    checked((long)((UInt128)(ulong)intent.RequestedExtent *
                        (ulong)grantedTotal / (ulong)totalDemand))))
                .ToArray();
            var grantedByFloor = grants.Sum(grant => grant.GrantedExtent);
            var remainder = checked(grantedTotal - grantedByFloor);
            var rankedForRemainder = grants
                .Where(grant => grant.GrantedExtent < grant.Intent.RequestedExtent)
                .Select(intent => (
                    Grant: intent,
                    Rank: context.Random.StableRank(
                        RandomAddress.Create(
                            RandomDomains.CoupledClaimRemainderRank,
                            context.Tick,
                            intent.Intent.OrganismId.Value,
                            SemanticRandomOracle.Pack32(
                                intent.Intent.TileId.Value,
                                intent.Intent.Reaction.Id.Value)),
                        intent.Intent.OrganismId.Value)))
                .OrderBy(candidate => candidate.Rank)
                .Take(checked((int)remainder))
                .Select(candidate => candidate.Grant.Intent.OrganismId)
                .ToHashSet();
            foreach (var grant in grants)
            {
                var grantedExtent = grant.GrantedExtent +
                    (rankedForRemainder.Contains(grant.Intent.OrganismId) ? 1 : 0);
                if (grantedExtent > 0)
                {
                    admitted.Add(grant.Intent with { RequestedExtent = grantedExtent });
                }
            }
        }

        var transactions = admitted
            .Select(intent => ExternalCaptureTransactionFactory.Create(
                view.Rules,
                context.Tick,
                intent.TileId,
                intent.OrganismId,
                intent.Reaction,
                intent.RequestedExtent))
            .OrderBy(transaction => transaction.Key)
            .ToImmutableArray();
        var reconciliation = ResourceLedgerOracle.Reconcile(
            view.Rules,
            transactions);
        var expectedFinalBalances = PreflightAccountBalances(view, transactions);
        var scavengeTransfers = ResolveScavenging(view, outcomes, admitted, context);
        var grantedCapture = admitted
            .GroupBy(intent => intent.OrganismId)
            .ToDictionary(group => group.Key, group => group.Sum(intent => intent.RequestedExtent));
        var grantedScavenge = scavengeTransfers
            .GroupBy(transfer => transfer.OrganismId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(transfer => checked(
                    transfer.GrantedReserveQ + transfer.GrantedStructureQ)));
        var acquisitionSamples = view.Intents
            .GroupBy(intent => intent.OrganismId)
            .OrderBy(group => group.Key.Value)
            .Select(group => new AcquisitionCoverageSample(
                group.Key,
                group.Sum(intent => checked(
                    intent.RequestedExtent + intent.RequestedScavengeReserveQ +
                    intent.RequestedScavengeStructureQ)),
                checked(grantedCapture.GetValueOrDefault(group.Key) +
                    grantedScavenge.GetValueOrDefault(group.Key))))
            .Where(sample => sample.UsefulDemandQ > 0)
            .ToImmutableArray();
        return new ExternalResolutionPlan(
            transactions,
            reconciliation,
            expectedFinalBalances,
            scavengeTransfers,
            acquisitionSamples);
    }

    protected override void Commit(
        MutableWorldState world,
        ExternalResolutionPlan plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        var mutations = 0;
        foreach (var transaction in plan.Transactions)
        {
            foreach (var entry in transaction.MatterEntries)
            {
                switch (entry.Account.OwnerKind)
                {
                    case MatterAccountOwnerKind.Tile:
                        world.ApplyTileResourceDelta(
                            TileId.FromRowMajorIndex(checked((uint)entry.Account.OwnerId)),
                            world.GetResourceHandle(entry.Account.ResourceId),
                            entry.DeltaQ,
                            changes);
                        mutations++;
                        break;
                    case MatterAccountOwnerKind.Organism:
                        if (entry.Account.Compartment != MatterCompartment.EnergyReserve ||
                            entry.Account.OwnerId != transaction.ActorId.Value)
                        {
                            throw new InvalidOperationException(
                                "The first capture slice only commits actor energy-reserve credits.");
                        }

                        world.AdjustOrganismChargedReserve(
                            transaction.ActorId,
                            entry.DeltaQ,
                            changes);
                        mutations++;
                        break;
                    case MatterAccountOwnerKind.Boundary:
                        break;
                    default:
                        throw new InvalidOperationException("Unknown matter-ledger owner kind.");
                }

                if (mutations == 1)
                {
                    try
                    {
                        context.FaultInjector.ThrowIfRequested(
                            Phase,
                            TickFailureStage.CommitAfterFirstMutation);
                    }
                    catch (Exception exception)
                    {
                        throw new InjectedCommitFailureException(exception);
                    }
                }
            }
        }


        foreach (var transfer in plan.ScavengeTransfers)
        {
            var organism = world.GetOrganism(transfer.OrganismId);
            world.AdjustOrganismChargedReserve(
                transfer.OrganismId,
                checked(transfer.GrantedReserveQ - transfer.ActionCostQ),
                changes);
            if (transfer.ActionCostQ > 0)
            {
                world.ApplyTileResourceDelta(
                    transfer.TileId,
                    world.GetResourceHandle(ResourceId.From(8)),
                    transfer.ActionCostQ,
                    changes);
            }

            var remnant = world.GetRemnant(transfer.RemnantId);
            var remainingReserve = checked(remnant.ChargedReserveQ - transfer.GrantedReserveQ);
            var remainingStructure = checked(
                remnant.StructuralMatterQ - transfer.GrantedStructureQ);
            if (transfer.GrantedStructureQ > 0)
            {
                world.AdjustOrganismIngestedStructuralMatter(
                    organism.Id,
                    transfer.GrantedStructureQ,
                    changes);
            }

            if (remainingReserve == 0 && remainingStructure == 0)
            {
                var removed = world.RemoveRemnant(remnant.Id, changes);
                for (var slot = 0; slot < MicronutrientInventory.Count; slot++)
                {
                    if (removed.Micronutrients[slot] > 0)
                    {
                        world.ApplyTileResourceDelta(
                            removed.TileId,
                            world.GetResourceHandle(MicronutrientInventory.ResourceIdAt(slot)),
                            removed.Micronutrients[slot],
                            changes);
                    }
                }
            }
            else
            {
                world.SetRemnantContents(
                    remnant.Id,
                    remainingStructure,
                    remainingReserve,
                    remnant.StructureDecayRemainderQ,
                    remnant.ReserveDecayRemainderQ,
                    changes);
            }

            world.SetOrganismLifecycleSchedule(
                organism.Id,
                organism.ReproductionNotBeforeTick,
                organism.SuccessfulReproductionCount,
                transfer.NotBeforeTick,
                changes);
            context.Scratch.AppendScavengeReceipt(
                context.Tick,
                new ScavengeActivityReceipt(
                    organism.Id,
                    transfer.RemnantId,
                    transfer.TileId,
                    transfer.GrantedReserveQ,
                    transfer.GrantedStructureQ));
        }

        context.Scratch.PublishAcquisitionSamples(context.Tick, plan.AcquisitionSamples);
    }

    protected override ImmutableArray<ResourceTransaction> GetResourceTransactions(
        ExternalResolutionPlan plan) => plan.Transactions;

    protected override void RunMaterializationBuilders(
        MutableWorldState world,
        ExternalResolutionPlan plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        _ = changes;
        _ = context;
        if (!plan.Reconciliation.IsBalanced)
        {
            throw new InvalidOperationException("An unreconciled ledger reached commit validation.");
        }

        foreach (var expected in plan.ExpectedFinalBalances)
        {
            var expectedQuantity = expected.QuantityQ;
            if (expected.Account.OwnerKind == MatterAccountOwnerKind.Organism)
            {
                expectedQuantity = checked(expectedQuantity + plan.ScavengeTransfers
                    .Where(transfer => transfer.OrganismId.Value == expected.Account.OwnerId)
                    .Sum(transfer => transfer.GrantedReserveQ - transfer.ActionCostQ));
            }

            var actual = expected.Account.OwnerKind switch
            {
                MatterAccountOwnerKind.Tile => world.GetTileResource(
                    TileId.FromRowMajorIndex(checked((uint)expected.Account.OwnerId)),
                    world.GetResourceHandle(expected.Account.ResourceId)),
                MatterAccountOwnerKind.Organism => world.GetOrganism(
                    OrganismId.FromAllocatedValue(expected.Account.OwnerId)).ChargedReserveQ,
                _ => throw new InvalidOperationException(
                    "Only finite authoritative accounts have expected post-state balances."),
            };
            if (actual != expectedQuantity)
            {
                throw new InvalidOperationException(
                    $"Ledger account {expected.Account} committed {actual}, expected {expectedQuantity}.");
            }
        }
    }

    private static ImmutableArray<ExpectedMatterAccountBalance> PreflightAccountBalances(
        ExternalResolutionView view,
        ImmutableArray<ResourceTransaction> transactions)
    {
        var accountDeltas = new Dictionary<MatterAccountKey, long>();
        foreach (var transaction in transactions)
        {
            foreach (var entry in transaction.MatterEntries)
            {
                if (entry.Account.OwnerKind != MatterAccountOwnerKind.Boundary)
                {
                    accountDeltas.TryGetValue(entry.Account, out var delta);
                    accountDeltas[entry.Account] = checked(delta + entry.DeltaQ);
                }
            }
        }

        var expected = ImmutableArray.CreateBuilder<ExpectedMatterAccountBalance>(
            accountDeltas.Count);
        foreach (var pair in accountDeltas.OrderBy(pair => pair.Key))
        {
            var current = pair.Key.OwnerKind switch
            {
                MatterAccountOwnerKind.Tile => view.TileResources.Single(value =>
                    value.TileId.Value == pair.Key.OwnerId &&
                    value.Resource.Id == pair.Key.ResourceId).QuantityQ,
                MatterAccountOwnerKind.Organism => view.Intents.Single(intent =>
                    intent.OrganismId.Value == pair.Key.OwnerId).ExpectedReserveQ,
                _ => throw new InvalidOperationException(
                    "A finite-balance preflight received a boundary account."),
            };
            var next = checked(current + pair.Value);
            if (next < 0)
            {
                throw new InvalidOperationException(
                    "A resolved external-capture batch would overdraw an authoritative account.");
            }

            if (pair.Key.OwnerKind == MatterAccountOwnerKind.Organism &&
                next > view.Intents.Single(intent =>
                    intent.OrganismId.Value == pair.Key.OwnerId).ReserveCapacityQ)
            {
                throw new InvalidOperationException(
                    "A resolved external-capture batch exceeds the organism reserve capacity.");
            }

            expected.Add(new ExpectedMatterAccountBalance(pair.Key, next));
        }

        return expected.MoveToImmutable();
    }

    private static ImmutableArray<ScavengeTransfer> ResolveScavenging(
        ExternalResolutionView view,
        ImmutableArray<PhaseOutcome<ExternalCaptureIntent>> outcomes,
        IReadOnlyCollection<ExternalCaptureIntent> admittedCapture,
        TickExecutionContext context)
    {
        var remaining = view.Remnants.ToDictionary(
            remnant => remnant.Id,
            remnant => remnant.ChargedReserveQ);
        var remainingStructure = view.Remnants.ToDictionary(
            remnant => remnant.Id,
            remnant => remnant.StructuralMatterQ);
        var captureCredits = admittedCapture.ToDictionary(
            intent => intent.OrganismId,
            intent => view.Rules.Reactions[intent.Reaction.DenseSlot].StoredEnergyQ *
                intent.RequestedExtent);
        var transfers = ImmutableArray.CreateBuilder<ScavengeTransfer>();
        foreach (var intent in outcomes.Select(outcome => outcome.Payload)
                     .Where(intent => intent.ScavengeTargetId is not null)
                     .OrderBy(intent => intent.ScavengeTargetId!.Value.Value)
                     .ThenBy(intent => intent.OrganismId.Value))
        {
            var targetId = intent.ScavengeTargetId!.Value;
            var available = remaining[targetId];
            var availableStructure = remainingStructure[targetId];
            captureCredits.TryGetValue(intent.OrganismId, out var captureCredit);
            var afterCost = checked(intent.ExpectedReserveQ + captureCredit -
                intent.ScavengeActionCostQ);
            var capacityRoom = Math.Max(0, intent.ReserveCapacityQ - afterCost);
            var grant = Math.Min(Math.Min(intent.RequestedScavengeReserveQ, available), capacityRoom);
            var structureGrant = Math.Min(intent.RequestedScavengeStructureQ, availableStructure);
            remaining[targetId] = checked(available - grant);
            remainingStructure[targetId] = checked(availableStructure - structureGrant);
            var cooldownTicks = checked((intent.ScavengeCooldownHours +
                context.TickDurationHours - 1) / context.TickDurationHours);
            transfers.Add(new ScavengeTransfer(
                intent.OrganismId,
                targetId,
                intent.TileId,
                grant,
                structureGrant,
                intent.ScavengeActionCostQ,
                checked(context.Tick + cooldownTicks)));
        }

        return transfers.ToImmutable();
    }

    private static CompiledReaction RequireReaction(
        CompiledRulePack rules,
        ReactionHandle handle) => rules.Reactions[handle.DenseSlot];
}

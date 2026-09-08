using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;
using Lyfe.Simulation.Spatial;

namespace Lyfe.Simulation.Ticks;

public readonly record struct DeathRecord(
    OrganismId OrganismId,
    SpeciesId SpeciesId,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    ulong Tick,
    TickPhase TerminalPhase,
    ImmutableArray<IntrinsicDeathCauseEvidence> NonzeroCauseProbabilities,
    IntrinsicDeathCause ActualCause,
    RemnantId RemnantId);

internal readonly record struct ReproductionActivityReceipt(
    OrganismId ParentId,
    OrganismId OffspringId,
    SpeciesId SpeciesId,
    TileId TileId,
    uint ParentPositionXQ,
    uint ParentPositionYQ,
    uint OffspringPositionXQ,
    uint OffspringPositionYQ);

internal readonly record struct RemnantDecayCandidate(
    RemnantSnapshot Remnant,
    CompiledRecyclingProfile Profile);

internal sealed record RemnantDecayView(
    PhaseViewStamp Stamp,
    ImmutableArray<RemnantDecayCandidate> Candidates) : IPhaseReadView
{
    public int WorkCount => Candidates.Length;
}

internal readonly record struct RemnantDecayOutcome(
    RemnantId RemnantId,
    TileId TileId,
    long StructuralReleaseQ,
    long ReserveReleaseQ,
    long RemainingStructureQ,
    long RemainingReserveQ,
    uint StructureRemainderQ,
    uint ReserveRemainderQ);

internal sealed class RemnantDecayPhase :
    ScalarTickPhase<
        RemnantDecayView,
        RemnantDecayOutcome,
        ImmutableArray<RemnantDecayOutcome>>
{
    private const uint OutcomeCategory = 1;
    private static readonly ResourceId RecycledOrganicResourceId = ResourceId.From(3);
    private static readonly ResourceId StructuralRemainderResourceId = ResourceId.From(7);
    private static readonly ResourceId SpentReserveCarrierResourceId = ResourceId.From(8);

    public override TickPhase Phase => TickPhase.EnvironmentalLedger;

    protected override PhaseExecutionClass ExecutionClass =>
        PhaseExecutionClass.MapThenGroupedResolve;

    protected override RemnantDecayView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var candidates = world.GetRemnantIdsInCanonicalOrder()
            .Select(id => world.GetRemnant(id))
            .Where(remnant => remnant.CreatedTick < context.Tick)
            .Select(remnant => new RemnantDecayCandidate(
                remnant,
                world.GetCompiledPhenotype(remnant.SourceSpeciesId).Physiology.Recycling))
            .ToImmutableArray();
        return new RemnantDecayView(
            CreateStamp(world, context),
            candidates);
    }

    protected override ImmutableArray<PhaseOutcome<RemnantDecayOutcome>> Evaluate(
        RemnantDecayView view,
        TickExecutionContext context)
    {
        var outcomes = ImmutableArray.CreateBuilder<PhaseOutcome<RemnantDecayOutcome>>(
            view.Candidates.Length);
        foreach (var candidate in view.Candidates)
        {
            var remnant = candidate.Remnant;
            var structure = Decay(
                remnant.StructuralMatterQ,
                candidate.Profile.StructureDecayPerMillionPerHour,
                context.TickDurationHours,
                remnant.StructureDecayRemainderQ);
            var reserve = Decay(
                remnant.ChargedReserveQ,
                candidate.Profile.ReserveDecayPerMillionPerHour,
                context.TickDurationHours,
                remnant.ReserveDecayRemainderQ);
            var outcome = new RemnantDecayOutcome(
                remnant.Id,
                remnant.TileId,
                structure.ReleasedQ,
                reserve.ReleasedQ,
                checked(remnant.StructuralMatterQ - structure.ReleasedQ),
                checked(remnant.ChargedReserveQ - reserve.ReleasedQ),
                structure.RemainderQ,
                reserve.RemainderQ);
            outcomes.Add(new PhaseOutcome<RemnantDecayOutcome>(
                view.Stamp,
                new OutcomeKey(Phase, OutcomeCategory, remnant.TileId.Value, remnant.Id.Value, 0, 0),
                outcome));
        }

        return outcomes.MoveToImmutable();
    }

    protected override ImmutableArray<RemnantDecayOutcome> Preflight(
        RemnantDecayView view,
        ImmutableArray<PhaseOutcome<RemnantDecayOutcome>> outcomes,
        TickExecutionContext context) => outcomes.Select(outcome => outcome.Payload).ToImmutableArray();

    protected override void Commit(
        MutableWorldState world,
        ImmutableArray<RemnantDecayOutcome> plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        var resource = world.GetResourceHandle(RecycledOrganicResourceId);
        foreach (var outcome in plan)
        {
            if (outcome.StructuralReleaseQ > 0)
            {
                world.ApplyTileResourceDelta(
                    outcome.TileId,
                    resource,
                    checked(outcome.StructuralReleaseQ * 4),
                    changes);
            }


            if (outcome.ReserveReleaseQ > 0)
            {
                world.ApplyTileResourceDelta(
                    outcome.TileId,
                    world.GetResourceHandle(SpentReserveCarrierResourceId),
                    outcome.ReserveReleaseQ,
                    changes);
            }

            if (outcome.StructuralReleaseQ > 0)
            {
                world.ApplyTileResourceDelta(
                    outcome.TileId,
                    world.GetResourceHandle(StructuralRemainderResourceId),
                    outcome.StructuralReleaseQ,
                    changes);
            }

            if (outcome.RemainingStructureQ == 0 && outcome.RemainingReserveQ == 0)
            {
                var remnant = world.RemoveRemnant(outcome.RemnantId, changes);
                ReleaseMicronutrients(world, remnant.TileId, remnant.Micronutrients, changes);
            }
            else
            {
                world.SetRemnantContents(
                    outcome.RemnantId,
                    outcome.RemainingStructureQ,
                    outcome.RemainingReserveQ,
                    outcome.StructureRemainderQ,
                    outcome.ReserveRemainderQ,
                    changes);
            }
        }
    }

    private static void ReleaseMicronutrients(
        MutableWorldState world,
        TileId tileId,
        MicronutrientInventory inventory,
        PhaseChangeBuilder changes)
    {
        for (var slot = 0; slot < MicronutrientInventory.Count; slot++)
        {
            if (inventory[slot] > 0)
            {
                world.ApplyTileResourceDelta(
                    tileId,
                    world.GetResourceHandle(MicronutrientInventory.ResourceIdAt(slot)),
                    inventory[slot],
                    changes);
            }
        }
    }

    private static (long ReleasedQ, uint RemainderQ) Decay(
        long quantityQ,
        uint ratePerMillionPerHour,
        uint hours,
        uint priorRemainderQ)
    {
        var scaled = checked((UInt128)(ulong)quantityQ * ratePerMillionPerHour * hours +
            priorRemainderQ);
        var released = checked((long)UInt128.Min(scaled / RatioQ.Scale, (ulong)quantityQ));
        var remainder = released == quantityQ
            ? 0U
            : checked((uint)(scaled % RatioQ.Scale));
        return (released, remainder);
    }
}

internal readonly record struct ReproductionCandidate(
    OrganismSnapshot Organism,
    CompiledOrganismPhysiology Physiology,
    OrganismEnvironmentInput Environment);

internal sealed record ReproductionView(
    PhaseViewStamp Stamp,
    ImmutableArray<ReproductionCandidate> Candidates) : IPhaseReadView
{
    public int WorkCount => Candidates.Length;
}

internal readonly record struct ReproductionOutcome(
    OrganismId ParentId,
    bool Commits,
    long ParentStructureQ,
    long OffspringStructureQ,
    long ParentReserveQ,
    long OffspringReserveQ,
    long ParentIngestedStructureQ,
    long OffspringIngestedStructureQ,
    MicronutrientInventory ParentCommittedMicronutrients,
    MicronutrientInventory ParentFreeMicronutrients,
    MicronutrientInventory OffspringCommittedMicronutrients,
    MicronutrientInventory OffspringFreeMicronutrients,
    uint OffspringPositionXQ,
    uint OffspringPositionYQ,
    ulong ParentSuccessfulCount,
    ulong ParentNotBeforeTick,
    OrganismActionGateSample? Gate);

internal sealed class LifecycleReproductionPhase :
    ScalarTickPhase<ReproductionView, ReproductionOutcome, ImmutableArray<ReproductionOutcome>>
{
    private const uint OutcomeCategory = 1;
    public override TickPhase Phase => TickPhase.LifecycleAndReproduction;

    protected override PhaseExecutionClass ExecutionClass => PhaseExecutionClass.IndependentMap;

    protected override ReproductionView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var candidates = world.GetOrganismIdsInCanonicalOrder()
            .Select(id => world.GetOrganism(id))
            .Select(organism => new ReproductionCandidate(
                organism,
                world.GetCompiledPhenotype(organism.SpeciesId).Physiology,
                world.GetOrganismEnvironment(
                    organism.TileId,
                    checked((context.Tick - 1) * context.TickDurationHours +
                        context.TickDurationHours / 2))))
            .ToImmutableArray();
        return new ReproductionView(CreateStamp(world, context), candidates);
    }

    protected override ImmutableArray<PhaseOutcome<ReproductionOutcome>> Evaluate(
        ReproductionView view,
        TickExecutionContext context)
    {
        var outcomes = ImmutableArray.CreateBuilder<PhaseOutcome<ReproductionOutcome>>(
            view.Candidates.Length);
        foreach (var candidate in view.Candidates)
        {
            var organism = candidate.Organism;
            var profile = candidate.Physiology.Reproduction;
            var condition = OrganismConditionBuilder.Build(
                candidate.Physiology,
                new OrganismConditionInput(
                    organism.StructuralMatterQ,
                    organism.ChargedReserveQ,
                    organism.BiologicalAgeHours),
                candidate.Environment,
                context.Tick,
                ConditionSnapshotKind.Reproduction);
            var quota = MicronutrientInventory.Compile(
                candidate.Physiology.CommittedMicronutrientQuotas);
            var gate = FirstReproductionGate(
                candidate,
                condition,
                quota,
                context.Tick);
            var commits = gate is null;
            long parentStructure = organism.StructuralMatterQ;
            long offspringStructure = 0;
            long parentReserve = organism.ChargedReserveQ;
            long offspringReserve = 0;
            var offspringIngested = 0L;
            var parentIngested = organism.IngestedStructuralMatterQ;
            var parentCommittedMicronutrients = organism.CommittedMicronutrients;
            var parentFreeMicronutrients = organism.FreeMicronutrients;
            var offspringCommittedMicronutrients = MicronutrientInventory.Empty;
            var offspringFreeMicronutrients = MicronutrientInventory.Empty;
            var parentCount = organism.SuccessfulReproductionCount;
            var parentNotBefore = organism.ReproductionNotBeforeTick;
            var offspringX = organism.PositionXQ;
            var offspringY = organism.PositionYQ;
            if (commits)
            {
                offspringStructure = organism.StructuralMatterQ / 2;
                parentStructure = organism.StructuralMatterQ - offspringStructure;
                var distributableReserve = checked(organism.ChargedReserveQ - profile.WorkCostQ);
                offspringReserve = distributableReserve / 2;
                parentReserve = distributableReserve - offspringReserve;
                offspringIngested = organism.IngestedStructuralMatterQ / 2;
                parentIngested = organism.IngestedStructuralMatterQ - offspringIngested;
                parentFreeMicronutrients = organism.FreeMicronutrients.Subtract(quota);
                offspringCommittedMicronutrients = quota;
                commits = parentStructure >= profile.ResultStructureMinimumQ &&
                    offspringStructure >= profile.ResultStructureMinimumQ &&
                    parentReserve >= profile.ResultReserveMinimumQ &&
                    offspringReserve >= profile.ResultReserveMinimumQ;
                if (commits)
                {
                    parentCount = checked(parentCount + 1);
                    parentNotBefore = ScheduleCooldown(
                        organism.Id,
                        parentCount,
                        context.Tick,
                        profile,
                        context.TickDurationHours,
                        context.Random);
                    (offspringX, offspringY) = PlaceOffspring(
                        organism,
                        parentCount,
                        candidate.Physiology,
                        parentStructure,
                        offspringStructure,
                        context.Random);
                }
            }

            outcomes.Add(new PhaseOutcome<ReproductionOutcome>(
                view.Stamp,
                new OutcomeKey(Phase, OutcomeCategory, organism.TileId.Value, organism.Id.Value, 0, 0),
                new ReproductionOutcome(
                    organism.Id,
                    commits,
                    parentStructure,
                    offspringStructure,
                    parentReserve,
                    offspringReserve,
                    parentIngested,
                    offspringIngested,
                    parentCommittedMicronutrients,
                    parentFreeMicronutrients,
                    offspringCommittedMicronutrients,
                    offspringFreeMicronutrients,
                    offspringX,
                    offspringY,
                    parentCount,
                    parentNotBefore,
                    gate)));
        }

        return outcomes.MoveToImmutable();
    }

    protected override ImmutableArray<ReproductionOutcome> Preflight(
        ReproductionView view,
        ImmutableArray<PhaseOutcome<ReproductionOutcome>> outcomes,
        TickExecutionContext context) => outcomes.Select(outcome => outcome.Payload).ToImmutableArray();

    protected override void Commit(
        MutableWorldState world,
        ImmutableArray<ReproductionOutcome> plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        foreach (var gate in plan
                     .Where(outcome => outcome.Gate is not null)
                     .Select(outcome => outcome.Gate!.Value))
        {
            context.Scratch.AppendOrganismActionGate(context.Tick, gate);
        }

        foreach (var outcome in plan.Where(outcome => outcome.Commits))
        {
            var parent = world.GetOrganism(outcome.ParentId);
            var profile = world.GetCompiledPhenotype(parent.SpeciesId).Physiology.Reproduction;
            world.AdjustOrganismStructure(
                parent.Id,
                checked(outcome.ParentStructureQ - parent.StructuralMatterQ),
                changes);
            world.AdjustOrganismChargedReserve(
                parent.Id,
                checked(outcome.ParentReserveQ - parent.ChargedReserveQ),
                changes);
            world.AdjustOrganismIngestedStructuralMatter(
                parent.Id,
                checked(outcome.ParentIngestedStructureQ -
                    parent.IngestedStructuralMatterQ),
                changes);
            world.SetOrganismMicronutrients(
                parent.Id,
                outcome.ParentCommittedMicronutrients,
                outcome.ParentFreeMicronutrients,
                changes);
            if (profile.WorkCostQ > 0)
            {
                world.ApplyTileResourceDelta(
                    parent.TileId,
                    world.GetResourceHandle(ResourceId.From(8)),
                    profile.WorkCostQ,
                    changes);
            }

            world.SetOrganismLifecycleSchedule(
                parent.Id,
                outcome.ParentNotBeforeTick,
                outcome.ParentSuccessfulCount,
                parent.ScavengeNotBeforeTick,
                changes);
            var offspringId = world.CreateOrganism(
                new OrganismInitialState(
                    parent.SpeciesId,
                    parent.TileId,
                    outcome.OffspringPositionXQ,
                    outcome.OffspringPositionYQ,
                    0,
                    0,
                    context.Tick,
                    0,
                    LifecyclePhase.Mature,
                    0,
                    0,
                    0,
                    outcome.OffspringIngestedStructureQ,
                    outcome.OffspringStructureQ,
                    outcome.OffspringReserveQ,
                    OrganismBehaviorState.Initial(context.Tick),
                    outcome.OffspringCommittedMicronutrients,
                    outcome.OffspringFreeMicronutrients),
                changes);
            var offspringNotBefore = ScheduleCooldown(
                offspringId,
                0,
                context.Tick,
                profile,
                context.TickDurationHours,
                context.Random);
            world.SetOrganismLifecycleSchedule(
                offspringId,
                offspringNotBefore,
                0,
                0,
                changes);
            context.Scratch.AppendReproductionReceipt(
                context.Tick,
                new ReproductionActivityReceipt(
                    parent.Id,
                    offspringId,
                    parent.SpeciesId,
                    parent.TileId,
                    parent.PositionXQ,
                    parent.PositionYQ,
                    outcome.OffspringPositionXQ,
                    outcome.OffspringPositionYQ));
        }
    }

    private static ulong ScheduleCooldown(
        OrganismId identity,
        ulong ordinal,
        ulong completedTick,
        CompiledReproductionProfile profile,
        uint tickDurationHours,
        ISimulationRandom random)
    {
        var jitter = random.UniformInclusive(
            RandomAddress.Create(
                RandomDomains.ReproductionCooldownJitter,
                identity.Value,
                ordinal,
                0),
            profile.CooldownJitterMaximumHours);
        var cooldownHours = checked(profile.BaseCooldownHours + jitter);
        var cooldownTicks = checked(
            (cooldownHours + tickDurationHours - 1) / tickDurationHours);
        return checked(completedTick + cooldownTicks);
    }

    private static OrganismActionGateSample? FirstReproductionGate(
        ReproductionCandidate candidate,
        MaterializedOrganismCondition condition,
        MicronutrientInventory quota,
        ulong tick)
    {
        var organism = candidate.Organism;
        var profile = candidate.Physiology.Reproduction;
        if (organism.LifecyclePhase != LifecyclePhase.Mature)
        {
            return ReproductionGate(
                organism,
                OrganismActionGateReason.LifecycleIneligible);
        }
        if (organism.Behavior.BehaviorId == OrganismBehaviorId.Conserving)
        {
            return ReproductionGate(
                organism,
                OrganismActionGateReason.BehaviorSuppressed);
        }
        if (tick < organism.ReproductionNotBeforeTick)
        {
            return ReproductionGate(
                organism,
                OrganismActionGateReason.CooldownActive,
                clearsAtTick: organism.ReproductionNotBeforeTick);
        }
        if (condition.RelativeHealthQ < profile.MinimumHealthQ)
        {
            return ReproductionGate(
                organism,
                OrganismActionGateReason.HealthBelowMinimum,
                condition.RelativeHealthQ,
                profile.MinimumHealthQ);
        }
        if (organism.StructuralMatterQ < profile.RequiredStructureQ)
        {
            return ReproductionGate(
                organism,
                OrganismActionGateReason.StructureBelowMinimum,
                organism.StructuralMatterQ,
                profile.RequiredStructureQ);
        }
        if (organism.ChargedReserveQ < profile.RequiredReserveQ)
        {
            return ReproductionGate(
                organism,
                OrganismActionGateReason.ReserveBelowMinimum,
                organism.ChargedReserveQ,
                profile.RequiredReserveQ);
        }

        for (var slot = 0; slot < MicronutrientInventory.Count; slot++)
        {
            if (organism.CommittedMicronutrients[slot] < quota[slot])
            {
                return ReproductionGate(
                    organism,
                    OrganismActionGateReason.ConstitutiveMicronutrientQuotaMissing,
                    organism.CommittedMicronutrients[slot],
                    quota[slot],
                    MicronutrientInventory.ResourceIdAt(slot));
            }
        }
        for (var slot = 0; slot < MicronutrientInventory.Count; slot++)
        {
            if (organism.FreeMicronutrients[slot] < quota[slot])
            {
                return ReproductionGate(
                    organism,
                    OrganismActionGateReason.OffspringMicronutrientQuotaMissing,
                    organism.FreeMicronutrients[slot],
                    quota[slot],
                    MicronutrientInventory.ResourceIdAt(slot));
            }
        }

        return null;
    }

    private static OrganismActionGateSample ReproductionGate(
        OrganismSnapshot organism,
        OrganismActionGateReason reason,
        long availableQ = 0,
        long requiredQ = 0,
        ResourceId? resourceId = null,
        ulong clearsAtTick = 0) => new(
            organism.Id,
            OrganismActionProcessKind.Reproduction,
            reason,
            availableQ,
            requiredQ,
            resourceId,
            clearsAtTick);

    private static (uint X, uint Y) PlaceOffspring(
        OrganismSnapshot parent,
        ulong ordinal,
        CompiledOrganismPhysiology physiology,
        long parentStructureQ,
        long offspringStructureQ,
        ISimulationRandom random)
    {
        var directionIndex = checked((int)random.UniformBelow(
            RandomAddress.Create(
                RandomDomains.OffspringPlacementDirection,
                parent.Id.Value,
                ordinal,
                0),
            256));
        var direction = SpatialMath.Direction(directionIndex);
        var spatial = physiology.Spatial;
        var parentRadius = SpatialMath.BodyRadiusQ(spatial, parentStructureQ);
        var offspringRadius = SpatialMath.BodyRadiusQ(spatial, offspringStructureQ);
        var spawnDistance = checked((ulong)parentRadius + offspringRadius +
            spatial.MatureBodyRadiusQ / 2UL);
        var dx = checked((long)((Int128)direction.XQ * spawnDistance /
            SpatialMath.RatioScale));
        var dy = checked((long)((Int128)direction.YQ * spawnDistance /
            SpatialMath.RatioScale));
        return (
            OffsetCoordinate(parent.PositionXQ, dx),
            OffsetCoordinate(parent.PositionYQ, dy));
    }

    private static uint OffsetCoordinate(uint coordinate, long offset)
    {
        if (offset == 0)
        {
            return coordinate;
        }

        var proposed = (long)coordinate + offset;
        while (proposed < 0 || proposed > uint.MaxValue)
        {
            proposed = proposed < 0
                ? -proposed - 1
                : checked((long)(2UL * uint.MaxValue) - proposed);
        }

        return checked((uint)proposed);
    }
}

using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;

namespace Lyfe.Simulation.Ticks;

internal readonly record struct InternalMetabolismCandidate(
    OrganismSnapshot Organism,
    CompiledOrganismPhysiology Physiology,
    ReactionHandle? DigestionReaction,
    long MaximumDigestionExtent,
    ReactionHandle? MaintenanceReaction,
    ReactionHandle? AssemblyReaction);

internal readonly record struct InternalTileResource(
    TileId TileId,
    ResourceHandle Resource,
    long QuantityQ);

internal sealed record InternalMetabolismView(
    PhaseViewStamp Stamp,
    CompiledRulePack Rules,
    ImmutableArray<InternalMetabolismCandidate> Candidates,
    ImmutableArray<InternalTileResource> TileResources) : IPhaseReadView
{
    public int WorkCount => Candidates.Length;

    public long GetTileResource(TileId tileId, ResourceHandle resource) =>
        TileResources.Single(value => value.TileId == tileId && value.Resource == resource).QuantityQ;
}

internal readonly record struct InternalMetabolismIntent(
    InternalMetabolismCandidate Candidate,
    long DigestionExtent,
    long MaintenanceExtent,
    long RequestedAssemblyExtent,
    MicronutrientInventory RequestedMicronutrients,
    OrganismActionGateSample? GrowthGate);

internal sealed record InternalMetabolismPlan(
    ImmutableArray<ResourceTransaction> Transactions,
    LedgerReconciliationReport Reconciliation,
    ImmutableArray<MaintenanceFailure> MaintenanceFailures,
    ImmutableArray<OrganismActionGateSample> ActionGates);

internal readonly record struct MaintenanceFailure(
    OrganismId OrganismId,
    long AvailableReserveQ,
    long RequiredReserveQ);

internal sealed class InternalMetabolismPhase :
    ScalarTickPhase<InternalMetabolismView, InternalMetabolismIntent, InternalMetabolismPlan>
{
    private const uint OutcomeCategory = 1;

    public override TickPhase Phase => TickPhase.InternalMetabolism;

    protected override PhaseExecutionClass ExecutionClass =>
        PhaseExecutionClass.MapThenGroupedResolve;

    protected override InternalMetabolismView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var candidates = ImmutableArray.CreateBuilder<InternalMetabolismCandidate>();
        var resources = new Dictionary<(TileId, ResourceHandle), long>();
        foreach (var id in world.GetOrganismIdsInCanonicalOrder())
        {
            var organism = world.GetOrganism(id);
            var phenotype = world.GetCompiledPhenotype(organism.SpeciesId);
            ReactionHandle? digestion = null;
            ReactionHandle? maintenance = null;
            ReactionHandle? assembly = null;
            foreach (var process in phenotype.Processes)
            {
                var reaction = world.Rules.RulePack.Reactions[process.Reaction.DenseSlot];
                switch (reaction.ProcessKind)
                {
                    case ProcessKind.ParticulateDigestion:
                        digestion ??= process.Reaction;
                        break;
                    case ProcessKind.MandatoryMaintenance:
                        maintenance ??= process.Reaction;
                        break;
                    case ProcessKind.BiomassAssembly:
                        assembly ??= process.Reaction;
                        foreach (var input in reaction.Inputs.Where(input =>
                                     input.Resource.Id != ResourceId.From(3)))
                        {
                            resources.TryAdd(
                                (organism.TileId, input.Resource),
                                world.GetTileResource(organism.TileId, input.Resource));
                        }
                        break;
                }
            }

            var digestionExtent = 0L;
            if (digestion is not null && organism.IngestedStructuralMatterQ > 0)
            {
                var reaction = world.Rules.RulePack.Reactions[digestion.Value.DenseSlot];
                var throughput = Math.Max(1, phenotype.Physiology.MatureStructureQ / 100);
                var reserveRoom = phenotype.Physiology.ChargedReserveCapacityQ -
                    organism.ChargedReserveQ;
                digestionExtent = Math.Min(
                    organism.IngestedStructuralMatterQ,
                    Math.Min(
                        checked(throughput * context.TickDurationHours),
                        reserveRoom / reaction.StoredEnergyQ));
            }

            candidates.Add(new InternalMetabolismCandidate(
                organism,
                phenotype.Physiology,
                digestion,
                digestionExtent,
                maintenance,
                assembly));

            foreach (var quota in phenotype.Physiology.CommittedMicronutrientQuotas)
            {
                resources.TryAdd(
                    (organism.TileId, quota.Resource),
                    world.GetTileResource(organism.TileId, quota.Resource));
            }
        }

        return new InternalMetabolismView(
            CreateStamp(world, context),
            world.Rules.RulePack,
            candidates.ToImmutable(),
            resources
                .OrderBy(pair => pair.Key.Item1.Value)
                .ThenBy(pair => pair.Key.Item2.Id.Value)
                .Select(pair => new InternalTileResource(
                    pair.Key.Item1,
                    pair.Key.Item2,
                    pair.Value))
                .ToImmutableArray());
    }

    protected override ImmutableArray<PhaseOutcome<InternalMetabolismIntent>> Evaluate(
        InternalMetabolismView view,
        TickExecutionContext context)
    {
        var outcomes = ImmutableArray.CreateBuilder<PhaseOutcome<InternalMetabolismIntent>>(
            view.Candidates.Length);
        foreach (var candidate in view.Candidates)
        {
            var reserve = candidate.Organism.ChargedReserveQ;
            if (candidate.DigestionReaction is not null && candidate.MaximumDigestionExtent > 0)
            {
                var digestion = view.Rules.Reactions[candidate.DigestionReaction.Value.DenseSlot];
                reserve = checked(reserve + digestion.StoredEnergyQ * candidate.MaximumDigestionExtent);
            }

            var requiredMaintenance = checked(
                candidate.Physiology.OpeningMetabolism.MaintenanceCostQPerHour *
                context.TickDurationHours);
            var maintenance = Math.Min(reserve, requiredMaintenance);
            reserve = checked(reserve - maintenance);

            var assemblyExtent = 0L;
            OrganismActionGateSample? growthGate = null;
            if (candidate.AssemblyReaction is null)
            {
                growthGate = GrowthGate(
                    candidate,
                    OrganismActionGateReason.MissingCapability);
            }
            else if (maintenance != requiredMaintenance)
            {
                growthGate = GrowthGate(
                    candidate,
                    OrganismActionGateReason.MaintenanceShortfall,
                    maintenance,
                    requiredMaintenance);
            }
            else if (candidate.Organism.Behavior.BehaviorId ==
                OrganismBehaviorId.Conserving)
            {
                growthGate = GrowthGate(
                    candidate,
                    OrganismActionGateReason.BehaviorSuppressed);
            }
            else
            {
                var assembly = view.Rules.Reactions[candidate.AssemblyReaction.Value.DenseSlot];
                var reserveInput = assembly.Inputs.Single(input =>
                    input.Resource.Id == ResourceId.From(3)).Quantity;
                var protectedReserve = candidate.Physiology.OpeningMetabolism.GrowthReserveFloorQ;
                var affordable = Math.Max(0, reserve - protectedReserve) / reserveInput;
                var throughput = checked((long)
                    candidate.Physiology.OpeningMetabolism.StructuralGrowthExtentsPerHour *
                    context.TickDurationHours);
                var stagingLoad = FounderMetabolismTransactionFactory.StagingLoadPerExtent(
                    view.Rules,
                    candidate.AssemblyReaction.Value);
                var capacityExtents = candidate.Physiology.DissolvedMacronutrientCapacityLoadQ /
                    stagingLoad;
                if (affordable <= 0)
                {
                    growthGate = GrowthGate(
                        candidate,
                        OrganismActionGateReason.ReserveProtectionFloor,
                        reserve,
                        checked(protectedReserve + reserveInput));
                }
                else if (capacityExtents <= 0)
                {
                    growthGate = GrowthGate(
                        candidate,
                        OrganismActionGateReason.InternalCapacity,
                        candidate.Physiology.DissolvedMacronutrientCapacityLoadQ,
                        stagingLoad);
                }
                else
                {
                    assemblyExtent = Math.Min(throughput, Math.Min(affordable, capacityExtents));
                }
            }

            var secondary = candidate.AssemblyReaction?.Id.Value ??
                candidate.MaintenanceReaction?.Id.Value ??
                candidate.DigestionReaction?.Id.Value ?? 0;
            var requestedMicronutrients = BuildMicronutrientRequest(
                view,
                candidate,
                context);
            outcomes.Add(new PhaseOutcome<InternalMetabolismIntent>(
                view.Stamp,
                new OutcomeKey(
                    Phase,
                    OutcomeCategory,
                    candidate.Organism.TileId.Value,
                    candidate.Organism.Id.Value,
                    secondary,
                    0),
                new InternalMetabolismIntent(
                    candidate,
                    candidate.MaximumDigestionExtent,
                    maintenance,
                    assemblyExtent,
                    requestedMicronutrients,
                    growthGate)));
        }
        return outcomes.MoveToImmutable();
    }

    protected override InternalMetabolismPlan Preflight(
        InternalMetabolismView view,
        ImmutableArray<PhaseOutcome<InternalMetabolismIntent>> outcomes,
        TickExecutionContext context)
    {
        var intents = outcomes.Select(outcome => outcome.Payload).ToArray();
        var grantedAssembly = ResolveAssemblyContention(view, intents, context);
        var grantedMicronutrients = ResolveMicronutrientContention(view, intents, context);
        var actionGates = intents
            .Select(intent => intent.GrowthGate ??
                BuildAssemblyResolutionGate(view, intent, grantedAssembly))
            .Where(gate => gate is not null)
            .Select(gate => gate!.Value)
            .OrderBy(gate => gate.OrganismId.Value)
            .ToImmutableArray();
        var transactions = ImmutableArray.CreateBuilder<ResourceTransaction>();
        foreach (var intent in intents.OrderBy(intent => intent.Candidate.Organism.Id.Value))
        {
            var candidate = intent.Candidate;
            if (intent.DigestionExtent > 0)
            {
                transactions.Add(ParticulateDigestionTransactionFactory.Create(
                    view.Rules,
                    context.Tick,
                    candidate.Organism.TileId,
                    candidate.Organism.Id,
                    candidate.DigestionReaction!.Value,
                    intent.DigestionExtent));
            }
            if (intent.MaintenanceExtent > 0)
            {
                if (candidate.MaintenanceReaction is null)
                {
                    throw new InvalidOperationException(
                        "A positive maintenance obligation has no compiled reaction.");
                }
                transactions.Add(FounderMetabolismTransactionFactory.CreateMaintenance(
                    view.Rules,
                    context.Tick,
                    candidate.Organism.TileId,
                    candidate.Organism.Id,
                    candidate.MaintenanceReaction.Value,
                    intent.MaintenanceExtent));
            }
            if (grantedAssembly.TryGetValue(candidate.Organism.Id, out var extent) && extent > 0)
            {
                transactions.Add(FounderMetabolismTransactionFactory.CreateBiomassAssembly(
                    view.Rules,
                    context.Tick,
                    candidate.Organism.TileId,
                    candidate.Organism.Id,
                    candidate.AssemblyReaction!.Value,
                    extent));
            }
            if (grantedMicronutrients.TryGetValue(candidate.Organism.Id, out var micronutrients))
            {
                for (var slot = 0; slot < MicronutrientInventory.Count; slot++)
                {
                    if (micronutrients[slot] > 0)
                    {
                        transactions.Add(MicronutrientTransactionFactory.CreateUptake(
                            context.Tick,
                            candidate.Organism.TileId,
                            candidate.Organism.Id,
                            MicronutrientInventory.ResourceIdAt(slot),
                            micronutrients[slot]));
                    }
                }
            }
        }

        var ordered = transactions.OrderBy(transaction => transaction.Key).ToImmutableArray();
        var failures = intents
            .Where(intent => intent.MaintenanceExtent < checked(
                intent.Candidate.Physiology.OpeningMetabolism.MaintenanceCostQPerHour *
                context.TickDurationHours))
            .Select(intent => new MaintenanceFailure(
                intent.Candidate.Organism.Id,
                intent.MaintenanceExtent,
                checked(intent.Candidate.Physiology.OpeningMetabolism.MaintenanceCostQPerHour *
                    context.TickDurationHours)))
            .OrderBy(failure => failure.OrganismId.Value)
            .ToImmutableArray();
        return new InternalMetabolismPlan(
            ordered,
            ResourceLedgerOracle.Reconcile(view.Rules, ordered),
            failures,
            actionGates);
    }

    protected override void Commit(
        MutableWorldState world,
        InternalMetabolismPlan plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        foreach (var gate in plan.ActionGates)
        {
            context.Scratch.AppendOrganismActionGate(context.Tick, gate);
        }

        foreach (var transaction in plan.Transactions)
        {
            foreach (var entry in transaction.MatterEntries)
            {
                if (entry.Account.OwnerKind == MatterAccountOwnerKind.Boundary ||
                    entry.Account.Compartment is MatterCompartment.AvailableStoreIngress or
                        MatterCompartment.AvailableStoreConsumption)
                {
                    continue;
                }

                if (entry.Account.OwnerKind == MatterAccountOwnerKind.Tile)
                {
                    world.ApplyTileResourceDelta(
                        transaction.TileId,
                        world.GetResourceHandle(entry.Account.ResourceId),
                        entry.DeltaQ,
                        changes);
                }
                else if (entry.Account.Compartment == MatterCompartment.IngestedMatter)
                {
                    world.AdjustOrganismIngestedStructuralMatter(
                        transaction.ActorId,
                        entry.DeltaQ,
                        changes);
                }
                else if (entry.Account.Compartment == MatterCompartment.EnergyReserve)
                {
                    world.AdjustOrganismChargedReserve(
                        transaction.ActorId,
                        entry.DeltaQ,
                        changes);
                }
                else if (entry.Account.Compartment == MatterCompartment.Structure)
                {
                    world.AdjustOrganismStructure(transaction.ActorId, entry.DeltaQ, changes);
                }
                else if (entry.Account.Compartment == MatterCompartment.FreeMicronutrient)
                {
                    var organism = world.GetOrganism(transaction.ActorId);
                    var slot = MicronutrientInventory.Slot(entry.Account.ResourceId);
                    world.SetOrganismMicronutrients(
                        organism.Id,
                        organism.CommittedMicronutrients,
                        organism.FreeMicronutrients.With(
                            slot,
                            checked(organism.FreeMicronutrients[slot] + entry.DeltaQ)),
                        changes);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported internal-metabolism account.");
                }
            }
        }

        var deathRecords = ImmutableArray.CreateBuilder<DeathRecord>(
            plan.MaintenanceFailures.Length);
        foreach (var failure in plan.MaintenanceFailures)
        {
            var organism = world.GetOrganism(failure.OrganismId);
            var remnantId = world.CreateRemnant(
                new RemnantInitialState(
                    organism.Id,
                    organism.SpeciesId,
                    context.Tick,
                    organism.TileId,
                    organism.PositionXQ,
                    organism.PositionYQ,
                    checked(organism.StructuralMatterQ +
                        organism.IngestedStructuralMatterQ),
                    organism.ChargedReserveQ,
                    0,
                    0,
                    organism.CommittedMicronutrients.Add(organism.FreeMicronutrients)),
                changes);
            world.RemoveOrganism(organism.Id, changes);
            deathRecords.Add(new DeathRecord(
                organism.Id,
                organism.SpeciesId,
                organism.TileId,
                organism.PositionXQ,
                organism.PositionYQ,
                context.Tick,
                TickPhase.InternalMetabolism,
                [new IntrinsicDeathCauseEvidence(
                    IntrinsicDeathCause.MaintenanceFailure,
                    RatioQ.Scale,
                    null,
                    true,
                    failure.AvailableReserveQ,
                    failure.RequiredReserveQ)],
                IntrinsicDeathCause.MaintenanceFailure,
                remnantId));
        }
        context.Scratch.AppendDeathRecords(context.Tick, deathRecords.MoveToImmutable());
    }

    protected override ImmutableArray<ResourceTransaction> GetResourceTransactions(
        InternalMetabolismPlan plan) => plan.Transactions;

    protected override void RunMaterializationBuilders(
        MutableWorldState world,
        InternalMetabolismPlan plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        if (!plan.Reconciliation.IsBalanced)
        {
            throw new InvalidOperationException("An unreconciled metabolism batch reached commit.");
        }
    }

    private static Dictionary<OrganismId, long> ResolveAssemblyContention(
        InternalMetabolismView view,
        IReadOnlyCollection<InternalMetabolismIntent> intents,
        TickExecutionContext context)
    {
        var result = new Dictionary<OrganismId, long>();
        foreach (var group in intents
                     .Where(intent => intent.RequestedAssemblyExtent > 0)
                     .GroupBy(intent => (
                         intent.Candidate.Organism.TileId,
                         Reaction: intent.Candidate.AssemblyReaction!.Value))
                     .OrderBy(group => group.Key.TileId.Value)
                     .ThenBy(group => group.Key.Reaction.Id.Value))
        {
            var reaction = view.Rules.Reactions[group.Key.Reaction.DenseSlot];
            var supply = long.MaxValue;
            foreach (var input in reaction.Inputs.Where(input =>
                         input.Resource.Id != ResourceId.From(3)))
            {
                supply = Math.Min(
                    supply,
                    view.GetTileResource(group.Key.TileId, input.Resource) / input.Quantity);
            }

            var candidates = group.OrderBy(intent => intent.Candidate.Organism.Id.Value).ToArray();
            var demand = candidates.Sum(intent => intent.RequestedAssemblyExtent);
            var granted = Math.Min(supply, demand);
            var floorTotal = 0L;
            foreach (var intent in candidates)
            {
                var floor = checked((long)((UInt128)(ulong)intent.RequestedAssemblyExtent *
                    (ulong)granted / (ulong)demand));
                result[intent.Candidate.Organism.Id] = floor;
                floorTotal = checked(floorTotal + floor);
            }

            var remainder = checked(granted - floorTotal);
            foreach (var ranked in candidates
                         .Where(intent => result[intent.Candidate.Organism.Id] <
                             intent.RequestedAssemblyExtent)
                         .Select(intent => (
                             Intent: intent,
                             Rank: context.Random.StableRank(
                                 RandomAddress.Create(
                                     RandomDomains.CoupledClaimRemainderRank,
                                     context.Tick,
                                     intent.Candidate.Organism.Id.Value,
                                     SemanticRandomOracle.Pack32(
                                         group.Key.TileId.Value,
                                         group.Key.Reaction.Id.Value)),
                                 intent.Candidate.Organism.Id.Value)))
                         .OrderBy(value => value.Rank)
                         .ThenBy(value => value.Intent.Candidate.Organism.Id.Value)
                         .Take(checked((int)remainder)))
            {
                result[ranked.Intent.Candidate.Organism.Id]++;
            }
        }
        return result;
    }

    private static OrganismActionGateSample? BuildAssemblyResolutionGate(
        InternalMetabolismView view,
        InternalMetabolismIntent intent,
        IReadOnlyDictionary<OrganismId, long> grantedAssembly)
    {
        if (intent.RequestedAssemblyExtent <= 0 ||
            grantedAssembly.GetValueOrDefault(intent.Candidate.Organism.Id) > 0)
        {
            return null;
        }

        var reaction = view.Rules.Reactions[intent.Candidate.AssemblyReaction!.Value.DenseSlot];
        var limiting = reaction.Inputs
            .Where(input => input.Resource.Id != ResourceId.From(3))
            .Select(input => new
            {
                Input = input,
                AvailableQ = view.GetTileResource(
                    intent.Candidate.Organism.TileId,
                    input.Resource),
            })
            .OrderBy(value => value.AvailableQ / value.Input.Quantity)
            .ThenBy(value => value.Input.Resource.Id.Value)
            .First();
        if (limiting.AvailableQ / limiting.Input.Quantity <= 0)
        {
            return GrowthGate(
                intent.Candidate,
                OrganismActionGateReason.ResourceSupply,
                limiting.AvailableQ,
                limiting.Input.Quantity,
                limiting.Input.Resource.Id);
        }

        return GrowthGate(
            intent.Candidate,
            OrganismActionGateReason.ClaimContention,
            0,
            1);
    }

    private static OrganismActionGateSample GrowthGate(
        InternalMetabolismCandidate candidate,
        OrganismActionGateReason reason,
        long availableQ = 0,
        long requiredQ = 0,
        ResourceId? resourceId = null) => new(
            candidate.Organism.Id,
            OrganismActionProcessKind.BiomassGrowth,
            reason,
            availableQ,
            requiredQ,
            resourceId,
            0);

    private static MicronutrientInventory BuildMicronutrientRequest(
        InternalMetabolismView view,
        InternalMetabolismCandidate candidate,
        TickExecutionContext context)
    {
        var target = MicronutrientInventory.Compile(
            candidate.Physiology.CommittedMicronutrientQuotas);
        var projected = candidate.Organism.FreeMicronutrients;
        var rate = candidate.Physiology.PassiveMicronutrientUptakePerMillionPerHour;
        var scaled = checked((ulong)rate * context.TickDurationHours);
        var opportunities = checked((uint)(scaled / SemanticRandomOracle.ProbabilityScale));
        var fractionalQ = checked((uint)(scaled % SemanticRandomOracle.ProbabilityScale));
        if (fractionalQ > 0 && context.Random.Bernoulli(
                RandomAddress.Create(
                    RandomDomains.MicronutrientUptake,
                    context.Tick,
                    candidate.Organism.Id.Value,
                    opportunities),
                fractionalQ).Triggered)
        {
            opportunities++;
        }

        var requested = MicronutrientInventory.Empty;
        for (var opportunity = 0U; opportunity < opportunities; opportunity++)
        {
            if (projected.TotalLoadQ >= candidate.Physiology.FreeMicronutrientCapacityLoadQ)
            {
                break;
            }

            var selectedSlot = -1;
            var selectedDeficit = 0L;
            var selectedTarget = 1L;
            var selectedRank = default(RandomRank);
            for (var slot = 0; slot < MicronutrientInventory.Count; slot++)
            {
                var deficit = target[slot] - projected[slot];
                if (deficit <= 0) continue;

                var resourceId = MicronutrientInventory.ResourceIdAt(slot);
                var resource = candidate.Physiology.CommittedMicronutrientQuotas.Single(
                    quota => quota.Resource.Id == resourceId).Resource;
                if (view.GetTileResource(candidate.Organism.TileId, resource) <= 0) continue;

                var rank = context.Random.StableRank(
                    RandomAddress.Create(
                        RandomDomains.MicronutrientTargetRank,
                        context.Tick,
                        candidate.Organism.Id.Value,
                        SemanticRandomOracle.Pack32(opportunity, resourceId.Value)),
                    resourceId.Value);
                var ratioComparison = selectedSlot < 0
                    ? 1
                    : ((Int128)deficit * selectedTarget).CompareTo(
                        (Int128)selectedDeficit * target[slot]);
                if (ratioComparison > 0 ||
                    (ratioComparison == 0 && rank < selectedRank))
                {
                    selectedSlot = slot;
                    selectedDeficit = deficit;
                    selectedTarget = target[slot];
                    selectedRank = rank;
                }
            }

            if (selectedSlot < 0) break;
            projected = projected.With(selectedSlot, projected[selectedSlot] + 1);
            requested = requested.With(selectedSlot, requested[selectedSlot] + 1);
        }
        return requested;
    }

    private static Dictionary<OrganismId, MicronutrientInventory> ResolveMicronutrientContention(
        InternalMetabolismView view,
        IReadOnlyCollection<InternalMetabolismIntent> intents,
        TickExecutionContext context)
    {
        var result = new Dictionary<OrganismId, MicronutrientInventory>();
        for (var slot = 0; slot < MicronutrientInventory.Count; slot++)
        {
            var resourceId = MicronutrientInventory.ResourceIdAt(slot);
            foreach (var group in intents
                         .Where(intent => intent.RequestedMicronutrients[slot] > 0)
                         .GroupBy(intent => intent.Candidate.Organism.TileId)
                         .OrderBy(group => group.Key.Value))
            {
                var handle = group.First().Candidate.Physiology.CommittedMicronutrientQuotas
                    .Single(quota => quota.Resource.Id == resourceId).Resource;
                var available = view.GetTileResource(group.Key, handle);
                var claims = group
                    .SelectMany(intent => Enumerable.Range(
                        0,
                        checked((int)intent.RequestedMicronutrients[slot]))
                        .Select(ordinal => (Intent: intent, Ordinal: ordinal)))
                    .OrderBy(claim => context.Random.StableRank(
                        RandomAddress.Create(
                            RandomDomains.ResourceRemainderRank,
                            context.Tick,
                            claim.Intent.Candidate.Organism.Id.Value,
                            SemanticRandomOracle.Pack32(resourceId.Value, (uint)claim.Ordinal)),
                        claim.Intent.Candidate.Organism.Id.Value))
                    .ThenBy(claim => claim.Intent.Candidate.Organism.Id.Value)
                    .ThenBy(claim => claim.Ordinal)
                    .Take(checked((int)Math.Min(available, group.Sum(
                        intent => intent.RequestedMicronutrients[slot]))));
                foreach (var claim in claims)
                {
                    result.TryGetValue(claim.Intent.Candidate.Organism.Id, out var inventory);
                    result[claim.Intent.Candidate.Organism.Id] = inventory.With(
                        slot,
                        inventory[slot] + 1);
                }
            }
        }
        return result;
    }
}

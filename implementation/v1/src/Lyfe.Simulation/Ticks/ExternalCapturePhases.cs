using System.Collections.Immutable;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;

namespace Lyfe.Simulation.Ticks;

internal sealed class TickScratch
{
    private ImmutableArray<ExternalCaptureIntent> externalCaptureIntents;
    private bool hasExternalCaptureIntents;

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
}

internal readonly record struct ExternalCaptureIntent(
    OrganismId OrganismId,
    TileId TileId,
    ReactionHandle Reaction,
    long RequestedExtent,
    long ExpectedReserveQ);

internal readonly record struct ExternalIntentCandidate(
    OrganismId OrganismId,
    TileId TileId,
    ReactionHandle? Reaction,
    long CurrentReserveQ);

internal sealed record ExternalIntentView(
    PhaseViewStamp Stamp,
    CompiledRulePack Rules,
    ImmutableArray<ExternalIntentCandidate> Candidates) : IPhaseReadView
{
    public int WorkCount => Candidates.Length;
}

internal sealed class ExternalCaptureIntentPhase :
    ScalarTickPhase<
        ExternalIntentView,
        ExternalCaptureIntent,
        ImmutableArray<ExternalCaptureIntent>>
{
    internal const long FoundationReserveCapacityQ = 10_000;
    private const uint IntentOutcomeCategory = 1;

    public override TickPhase Phase => TickPhase.ExternalIntent;

    protected override PhaseExecutionClass ExecutionClass => PhaseExecutionClass.IndependentMap;

    protected override ExternalIntentView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var organismIds = world.GetOrganismIdsInCanonicalOrder();
        var candidates = ImmutableArray.CreateBuilder<ExternalIntentCandidate>(organismIds.Length);
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

            candidates.Add(new ExternalIntentCandidate(
                organismId,
                organism.TileId,
                reaction,
                organism.ChargedReserveQ));
        }

        return new ExternalIntentView(
            CreateStamp(world, context),
            world.Rules.RulePack,
            candidates.MoveToImmutable());
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
                if (reserveOutputPerExtent > 0 &&
                    candidate.CurrentReserveQ <= FoundationReserveCapacityQ - reserveOutputPerExtent)
                {
                    requestedExtent = 1;
                }
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
                    candidate.CurrentReserveQ)));
        }

        return outcomes.MoveToImmutable();
    }

    protected override ImmutableArray<ExternalCaptureIntent> Preflight(
        ExternalIntentView view,
        ImmutableArray<PhaseOutcome<ExternalCaptureIntent>> outcomes,
        TickExecutionContext context) =>
        outcomes
            .Select(outcome => outcome.Payload)
            .Where(intent => intent.RequestedExtent > 0)
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

}

internal readonly record struct TileResourcePhaseValue(
    TileId TileId,
    ResourceHandle Resource,
    long QuantityQ);

internal sealed record ExternalResolutionView(
    PhaseViewStamp Stamp,
    CompiledRulePack Rules,
    ImmutableArray<ExternalCaptureIntent> Intents,
    ImmutableArray<TileResourcePhaseValue> TileResources) : IPhaseReadView
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
    ImmutableArray<ExpectedMatterAccountBalance> ExpectedFinalBalances);

internal readonly record struct ExpectedMatterAccountBalance(
    MatterAccountKey Account,
    long QuantityQ);

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
            [.. resources]);
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
            .GroupBy(intent => (intent.TileId, intent.Reaction))
            .OrderBy(group => group.Key.TileId.Value)
            .ThenBy(group => group.Key.Reaction.Id.Value);

        foreach (var group in groups)
        {
            var reaction = RequireReaction(view.Rules, group.Key.Reaction);
            if (reaction.Inputs.IsDefaultOrEmpty ||
                group.Any(intent => intent.RequestedExtent != 1))
            {
                throw new InvalidOperationException(
                    "The first external-capture resolver accepts one whole requested extent with finite tile inputs.");
            }

            var supplyExtent = long.MaxValue;
            foreach (var input in reaction.Inputs)
            {
                supplyExtent = Math.Min(
                    supplyExtent,
                    view.GetTileResource(group.Key.TileId, input.Resource) / input.Quantity);
            }

            var candidates = group.OrderBy(intent => intent.OrganismId.Value).ToArray();
            var admittedCount = checked((int)Math.Min(supplyExtent, candidates.LongLength));
            if (admittedCount == candidates.Length)
            {
                admitted.AddRange(candidates);
                continue;
            }

            var ranked = candidates
                .Select(intent => (
                    Intent: intent,
                    Rank: context.Random.StableRank(
                        RandomAddress.Create(
                            RandomDomains.CoupledClaimRemainderRank,
                            context.Tick,
                            intent.OrganismId.Value,
                            SemanticRandomOracle.Pack32(
                                intent.TileId.Value,
                                intent.Reaction.Id.Value)),
                        intent.OrganismId.Value)))
                .OrderBy(candidate => candidate.Rank)
                .Take(admittedCount)
                .Select(candidate => candidate.Intent);
            admitted.AddRange(ranked);
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
        return new ExternalResolutionPlan(
            transactions,
            reconciliation,
            expectedFinalBalances);
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
            if (actual != expected.QuantityQ)
            {
                throw new InvalidOperationException(
                    $"Ledger account {expected.Account} committed {actual}, expected {expected.QuantityQ}.");
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
                next > ExternalCaptureIntentPhase.FoundationReserveCapacityQ)
            {
                throw new InvalidOperationException(
                    "A resolved external-capture batch exceeds the organism reserve capacity.");
            }

            expected.Add(new ExpectedMatterAccountBalance(pair.Key, next));
        }

        return expected.MoveToImmutable();
    }

    private static CompiledReaction RequireReaction(
        CompiledRulePack rules,
        ReactionHandle handle) => rules.Reactions[handle.DenseSlot];
}

using System.Collections.Immutable;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Rules.Identity;
using Lyfe.Simulation.Rules.Runtime;
using Lyfe.Simulation.State;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.State.Storage;

namespace Lyfe.Simulation.Ticks;

internal sealed record GasEnvironmentView(
    PhaseViewStamp Stamp,
    ImmutableArray<CompiledTileProfile> Tiles,
    ImmutableArray<GasEdge> Edges,
    ImmutableArray<long> Quantities,
    ImmutableArray<long> SourceRemainders,
    ImmutableArray<long> SinkRemainders,
    ImmutableArray<long> ExchangeRemainders,
    CompiledGasEnvironment Rules) : IPhaseReadView
{
    public int WorkCount => 1;
}

internal sealed record GasEnvironmentPlan(
    ImmutableArray<long> Deltas,
    ImmutableArray<long> SourceRemainders,
    ImmutableArray<long> SinkRemainders,
    ImmutableArray<long> ExchangeRemainders,
    ImmutableArray<ResourceTransaction> Transactions);

internal sealed class GasTransportPhase :
    ScalarTickPhase<GasEnvironmentView, GasEnvironmentPlan, GasEnvironmentPlan>
{
    private const long RateScale = 1_000_000;
    private const long ExchangeScale = 1_000_000_000_000;

    public override TickPhase Phase => TickPhase.EnvironmentalLedger;

    protected override PhaseExecutionClass ExecutionClass =>
        PhaseExecutionClass.ExactAggregateReduce;

    protected override GasEnvironmentView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var rules = world.Rules.WorldProfile.GasEnvironment;
        var tileIds = world.GetTileIdsInCanonicalOrder();
        var quantities = new long[checked(tileIds.Length * rules.Gases.Length)];
        var sourceRemainders = new long[quantities.Length];
        var sinkRemainders = new long[quantities.Length];
        for (var gasSlot = 0; gasSlot < rules.Gases.Length; gasSlot++)
        {
            for (var tileSlot = 0; tileSlot < tileIds.Length; tileSlot++)
            {
                var offset = GetTileOffset(gasSlot, tileSlot, tileIds.Length);
                quantities[offset] = world.GetTileResource(tileIds[tileSlot], rules.Gases[gasSlot].Resource);
                var remainder = world.GetGasTileRemainders(gasSlot, tileIds[tileSlot]);
                sourceRemainders[offset] = remainder.SourceQ;
                sinkRemainders[offset] = remainder.SinkQ;
            }
        }

        var edges = world.GetGasEdges();
        var exchangeRemainders = new long[checked(edges.Length * rules.Gases.Length)];
        for (var gasSlot = 0; gasSlot < rules.Gases.Length; gasSlot++)
        {
            for (var edgeSlot = 0; edgeSlot < edges.Length; edgeSlot++)
            {
                exchangeRemainders[GetEdgeOffset(gasSlot, edgeSlot, edges.Length)] =
                    world.GetGasExchangeRemainder(gasSlot, edgeSlot);
            }
        }

        return new GasEnvironmentView(
            CreateStamp(world, context),
            world.Rules.WorldProfile.Tiles,
            edges,
            ImmutableArray.Create(quantities),
            ImmutableArray.Create(sourceRemainders),
            ImmutableArray.Create(sinkRemainders),
            ImmutableArray.Create(exchangeRemainders),
            rules);
    }

    protected override ImmutableArray<PhaseOutcome<GasEnvironmentPlan>> Evaluate(
        GasEnvironmentView view,
        TickExecutionContext context)
    {
        var tileCount = view.Tiles.Length;
        var working = view.Quantities.ToArray();
        var deltas = new long[working.Length];
        var sourceRemainders = view.SourceRemainders.ToArray();
        var sinkRemainders = view.SinkRemainders.ToArray();
        var exchangeRemainders = view.ExchangeRemainders.ToArray();
        var transactions = ImmutableArray.CreateBuilder<ResourceTransaction>();

        // All sources read the phase-opening state; sinks then see source results.
        for (var gasSlot = 0; gasSlot < view.Rules.Gases.Length; gasSlot++)
        {
            var gas = view.Rules.Gases[gasSlot];
            for (var tileSlot = 0; tileSlot < tileCount; tileSlot++)
            {
                var offset = GetTileOffset(gasSlot, tileSlot, tileCount);
                var tile = view.Tiles[tileSlot];
                long volcanicRate = 0;
                if (tile.GasEmissionProfileSlot >= 0)
                {
                    volcanicRate = view.Rules.EmissionProfiles[tile.GasEmissionProfileSlot]
                        .FullActivityQuantitiesPerHourByGasSlot[gasSlot];
                }

                var scaledSource = checked(
                    (Int128)volcanicRate * tile.BaselineVolcanismQ * context.TickDurationHours +
                    sourceRemainders[offset]);
                var volcanicSource = checked((long)(scaledSource / RateScale));
                sourceRemainders[offset] = checked((long)(scaledSource % RateScale));
                var source = checked(volcanicSource +
                    gas.DiffuseSourceQuantityPerHour * context.TickDurationHours);
                if (source > 0)
                {
                    working[offset] = checked(working[offset] + source);
                    deltas[offset] = checked(deltas[offset] + source);
                    transactions.Add(GasTransactionFactory.Source(
                        context.Tick, TileId.FromRowMajorIndex((uint)tileSlot), gas.Resource.Id, source));
                }

                var scaledSink = checked(
                    (Int128)working[offset] * gas.SinkRatePerMillionPerHour *
                    context.TickDurationHours + sinkRemainders[offset]);
                var sink = checked((long)Int128.Min(scaledSink / RateScale, working[offset]));
                sinkRemainders[offset] = sink == working[offset]
                    ? 0
                    : checked((long)(scaledSink % RateScale));
                if (sink > 0)
                {
                    working[offset] -= sink;
                    deltas[offset] -= sink;
                    transactions.Add(GasTransactionFactory.Sink(
                        context.Tick, TileId.FromRowMajorIndex((uint)tileSlot), gas.Resource.Id, sink));
                }
            }
        }

        // Every signed edge flux reads the same post-source/post-sink field.
        var postSink = working.ToArray();
        for (var gasSlot = 0; gasSlot < view.Rules.Gases.Length; gasSlot++)
        {
            var gas = view.Rules.Gases[gasSlot];
            for (var edgeSlot = 0; edgeSlot < view.Edges.Length; edgeSlot++)
            {
                var edge = view.Edges[edgeSlot];
                var lowerSlot = checked((int)edge.LowerTileId.Value);
                var higherSlot = checked((int)edge.HigherTileId.Value);
                var lowerOffset = GetTileOffset(gasSlot, lowerSlot, tileCount);
                var higherOffset = GetTileOffset(gasSlot, higherSlot, tileCount);
                var compatibilityQ = EdgeCompatibility(
                    view.Tiles[lowerSlot], view.Tiles[higherSlot], view.Rules);
                var remainderOffset = GetEdgeOffset(gasSlot, edgeSlot, view.Edges.Length);
                var scaled = checked(
                    (Int128)(postSink[lowerOffset] - postSink[higherOffset]) *
                    gas.ExchangeRatePerMillionPerEdgeHour * compatibilityQ *
                    context.TickDurationHours + exchangeRemainders[remainderOffset]);
                var signedTransfer = checked((long)(scaled / ExchangeScale));
                exchangeRemainders[remainderOffset] = checked((long)(scaled % ExchangeScale));
                if (signedTransfer == 0) continue;

                working[lowerOffset] = checked(working[lowerOffset] - signedTransfer);
                working[higherOffset] = checked(working[higherOffset] + signedTransfer);
                deltas[lowerOffset] = checked(deltas[lowerOffset] - signedTransfer);
                deltas[higherOffset] = checked(deltas[higherOffset] + signedTransfer);
                var sourceTile = signedTransfer > 0 ? edge.LowerTileId : edge.HigherTileId;
                var destinationTile = signedTransfer > 0 ? edge.HigherTileId : edge.LowerTileId;
                transactions.Add(GasTransactionFactory.Exchange(
                    context.Tick, sourceTile, destinationTile, gas.Resource.Id, Math.Abs(signedTransfer)));
            }
        }

        var plan = new GasEnvironmentPlan(
            ImmutableArray.Create(deltas),
            ImmutableArray.Create(sourceRemainders),
            ImmutableArray.Create(sinkRemainders),
            ImmutableArray.Create(exchangeRemainders),
            transactions.OrderBy(transaction => transaction.Key).ToImmutableArray());
        return [new PhaseOutcome<GasEnvironmentPlan>(
            view.Stamp,
            new OutcomeKey(Phase, 1, 0, 0, 0, 0),
            plan)];
    }

    protected override GasEnvironmentPlan Preflight(
        GasEnvironmentView view,
        ImmutableArray<PhaseOutcome<GasEnvironmentPlan>> outcomes,
        TickExecutionContext context)
    {
        var plan = outcomes.Single().Payload;
        for (var index = 0; index < plan.Deltas.Length; index++)
        {
            if (checked(view.Quantities[index] + plan.Deltas[index]) < 0)
            {
                throw new InvalidOperationException("Gas transport would overdraw a tile stock.");
            }
        }
        return plan;
    }

    protected override void Commit(
        MutableWorldState world,
        GasEnvironmentPlan plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        var rules = world.Rules.WorldProfile.GasEnvironment;
        for (var gasSlot = 0; gasSlot < rules.Gases.Length; gasSlot++)
        {
            for (var tileSlot = 0; tileSlot < world.TileCount; tileSlot++)
            {
                var offset = GetTileOffset(gasSlot, tileSlot, world.TileCount);
                var tileId = TileId.FromRowMajorIndex((uint)tileSlot);
                if (plan.Deltas[offset] != 0)
                {
                    world.ApplyTileResourceDelta(tileId, rules.Gases[gasSlot].Resource, plan.Deltas[offset], changes);
                }
                world.SetGasTileRemainders(
                    gasSlot, tileId, plan.SourceRemainders[offset], plan.SinkRemainders[offset], changes);
            }

            for (var edgeSlot = 0; edgeSlot < world.GetGasEdges().Length; edgeSlot++)
            {
                world.SetGasExchangeRemainder(
                    gasSlot,
                    edgeSlot,
                    plan.ExchangeRemainders[GetEdgeOffset(gasSlot, edgeSlot, world.GetGasEdges().Length)],
                    changes);
            }
        }
    }

    protected override ImmutableArray<ResourceTransaction> GetResourceTransactions(
        GasEnvironmentPlan plan) => plan.Transactions;

    private static uint EdgeCompatibility(
        CompiledTileProfile left,
        CompiledTileProfile right,
        CompiledGasEnvironment rules)
    {
        UInt128 compatibility = RateScale;
        if ((left.ElevationMeters < 0) != (right.ElevationMeters < 0))
        {
            compatibility = compatibility * rules.AquaticTerrestrialCompatibilityQ / RateScale;
        }
        if (left.ElevationMeters >= rules.MajorMountainElevationMeters ||
            right.ElevationMeters >= rules.MajorMountainElevationMeters)
        {
            compatibility = compatibility * rules.MajorMountainCompatibilityQ / RateScale;
        }
        return checked((uint)compatibility);
    }

    private static int GetTileOffset(int gasSlot, int tileSlot, int tileCount) =>
        checked((gasSlot * tileCount) + tileSlot);

    private static int GetEdgeOffset(int gasSlot, int edgeSlot, int edgeCount) =>
        checked((gasSlot * edgeCount) + edgeSlot);
}

internal static class GasTransactionFactory
{
    public static ResourceTransaction Source(
        ulong tick, TileId tileId, ResourceId resourceId, long quantity) =>
        Create(tick, tileId, default, resourceId, quantity, LedgerCause.EnvironmentalGasSource, 0);

    public static ResourceTransaction Sink(
        ulong tick, TileId tileId, ResourceId resourceId, long quantity) =>
        Create(tick, tileId, default, resourceId, quantity, LedgerCause.EnvironmentalGasSink, 1);

    public static ResourceTransaction Exchange(
        ulong tick, TileId source, TileId destination, ResourceId resourceId, long quantity) =>
        Create(tick, source, destination, resourceId, quantity, LedgerCause.EnvironmentalGasExchange, 2);

    private static ResourceTransaction Create(
        ulong tick,
        TileId source,
        TileId destination,
        ResourceId resourceId,
        long quantity,
        LedgerCause cause,
        uint ordinal)
    {
        var isSource = cause == LedgerCause.EnvironmentalGasSource;
        var isSink = cause == LedgerCause.EnvironmentalGasSink;
        var from = isSource
            ? new MatterAccountKey(MatterAccountOwnerKind.Boundary, 1, MatterCompartment.Atmosphere, resourceId)
            : new MatterAccountKey(MatterAccountOwnerKind.Tile, source.Value, MatterCompartment.Atmosphere, resourceId);
        var to = isSink
            ? new MatterAccountKey(MatterAccountOwnerKind.Boundary, 1, MatterCompartment.Atmosphere, resourceId)
            : new MatterAccountKey(MatterAccountOwnerKind.Tile, isSource ? source.Value : destination.Value, MatterCompartment.Atmosphere, resourceId);
        return new ResourceTransaction(
            new LedgerTransactionKey(tick, TickPhase.EnvironmentalLedger, source.Value,
                destination.Value, resourceId.Value, ordinal),
            cause,
            ReactionId.From(resourceId.Value),
            default,
            source,
            quantity,
            [new MatterLedgerEntry(from, -quantity), new MatterLedgerEntry(to, quantity)],
            []);
    }
}

internal static class EnvironmentalResourceTransactionFactory
{
    public static ResourceTransaction Source(
        ulong tick,
        TileId tileId,
        ResourceId resourceId,
        long quantityQ) => new(
            new LedgerTransactionKey(
                tick,
                TickPhase.EnvironmentalLedger,
                tileId.Value,
                0,
                resourceId.Value,
                3),
            LedgerCause.EnvironmentalResourceSource,
            ReactionId.From(resourceId.Value),
            default,
            tileId,
            quantityQ,
            [
                new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Boundary,
                        1,
                        MatterCompartment.InorganicPool,
                        resourceId),
                    -quantityQ),
                new MatterLedgerEntry(
                    new MatterAccountKey(
                        MatterAccountOwnerKind.Tile,
                        tileId.Value,
                        MatterCompartment.InorganicPool,
                        resourceId),
                    quantityQ),
            ],
            []);
}

internal sealed record WeatheringResourceView(
    PhaseViewStamp Stamp,
    ImmutableArray<TileId> TileIds,
    long QuantityPerTileQ) : IPhaseReadView
{
    public int WorkCount => TileIds.Length;
}

internal sealed class WeatheringResourcePhase :
    ScalarTickPhase<
        WeatheringResourceView,
        ResourceTransaction,
        ImmutableArray<ResourceTransaction>>
{
    private static readonly ResourceId InorganicPhosphorus = ResourceId.From(16);

    public override TickPhase Phase => TickPhase.EnvironmentalLedger;

    protected override PhaseExecutionClass ExecutionClass =>
        PhaseExecutionClass.IndependentMap;

    protected override WeatheringResourceView SealView(
        MutableWorldState world,
        TickExecutionContext context)
    {
        var perHour = world.Rules.WorldProfile.Generator?
            .InorganicPhosphorusWeatheringQuantityPerHour ?? 0;
        var quantity = checked(perHour * context.TickDurationHours);
        return new WeatheringResourceView(
            CreateStamp(world, context),
            quantity == 0 ? [] : world.GetTileIdsInCanonicalOrder(),
            quantity);
    }

    protected override ImmutableArray<PhaseOutcome<ResourceTransaction>> Evaluate(
        WeatheringResourceView view,
        TickExecutionContext context) => view.TileIds
        .Select(tileId =>
        {
            var transaction = EnvironmentalResourceTransactionFactory.Source(
                context.Tick,
                tileId,
                InorganicPhosphorus,
                view.QuantityPerTileQ);
            return new PhaseOutcome<ResourceTransaction>(
                view.Stamp,
                new OutcomeKey(
                    Phase,
                    3,
                    tileId.Value,
                    0,
                    InorganicPhosphorus.Value,
                    3),
                transaction);
        })
        .ToImmutableArray();

    protected override ImmutableArray<ResourceTransaction> Preflight(
        WeatheringResourceView view,
        ImmutableArray<PhaseOutcome<ResourceTransaction>> outcomes,
        TickExecutionContext context) => outcomes
        .Select(outcome => outcome.Payload)
        .ToImmutableArray();

    protected override void Commit(
        MutableWorldState world,
        ImmutableArray<ResourceTransaction> plan,
        PhaseChangeBuilder changes,
        TickExecutionContext context)
    {
        var phosphorus = world.GetResourceHandle(InorganicPhosphorus);
        foreach (var transaction in plan)
        {
            world.ApplyTileResourceDelta(
                transaction.TileId,
                phosphorus,
                transaction.Extent,
                changes);
        }
    }

    protected override ImmutableArray<ResourceTransaction> GetResourceTransactions(
        ImmutableArray<ResourceTransaction> plan) => plan;
}

internal sealed class EnvironmentalLedgerPhase : IScalarTickPhase
{
    private readonly GasTransportPhase gas = new();
    private readonly WeatheringResourcePhase weathering = new();
    private readonly RemnantDecayPhase remnants = new();

    public TickPhase Phase => TickPhase.EnvironmentalLedger;

    public TickPhaseJournal Execute(MutableWorldState world, TickExecutionContext context)
    {
        var gasJournal = gas.Execute(world, context);
        var weatheringJournal = weathering.Execute(world, context);
        var remnantJournal = remnants.Execute(world, context);
        return new TickPhaseJournal(
            Phase,
            PhaseExecutionClass.MapThenGroupedResolve,
            gasJournal.EvaluatedWorkCount + weatheringJournal.EvaluatedWorkCount +
                remnantJournal.EvaluatedWorkCount,
            new PhaseChangeSet(
                gasJournal.Changes.Creates
                    .AddRange(weatheringJournal.Changes.Creates)
                    .AddRange(remnantJournal.Changes.Creates).Distinct().ToImmutableArray(),
                gasJournal.Changes.Removes
                    .AddRange(weatheringJournal.Changes.Removes)
                    .AddRange(remnantJournal.Changes.Removes).Distinct().ToImmutableArray(),
                gasJournal.Changes.Relocations
                    .AddRange(weatheringJournal.Changes.Relocations)
                    .AddRange(remnantJournal.Changes.Relocations).Distinct().ToImmutableArray(),
                gasJournal.Changes.DirtyEntities
                    .AddRange(weatheringJournal.Changes.DirtyEntities)
                    .AddRange(remnantJournal.Changes.DirtyEntities).Distinct().ToImmutableArray()),
            gasJournal.ResourceTransactions
                .AddRange(weatheringJournal.ResourceTransactions)
                .AddRange(remnantJournal.ResourceTransactions)
                .OrderBy(transaction => transaction.Key).ToImmutableArray());
    }
}

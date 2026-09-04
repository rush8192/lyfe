using System.Collections.Immutable;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;
using Lyfe.Simulation.Ticks;
using Xunit;

namespace Lyfe.Simulation.Tests;

public sealed class TickChangeMergerTests
{
    private static readonly PhaseChangeSet EmptyChanges = new([], [], [], []);

    [Fact]
    public void MergeAppliesCanonicalStructuralAndDirtyPrecedence()
    {
        var organism1 = OrganismId.FromAllocatedValue(1);
        var organism2 = OrganismId.FromAllocatedValue(2);
        var organism3 = OrganismId.FromAllocatedValue(3);
        var transient = OrganismId.FromAllocatedValue(11);
        var tile0 = TileId.FromRowMajorIndex(0);
        var tile1 = TileId.FromRowMajorIndex(1);
        var tile2 = TileId.FromRowMajorIndex(2);
        var journals = BuildJournals(new Dictionary<TickPhase, PhaseChangeSet>
        {
            [TickPhase.CommandAdmission] = new(
                [StateEntityReference.From(transient)],
                [],
                [],
                [
                    new DirtyStateEntity(
                        LogicalFieldGroup.OrganismReserve,
                        StateEntityReference.From(transient)),
                ]),
            [TickPhase.CalendarAndConditions] = new(
                [],
                [],
                [],
                [
                    new DirtyStateEntity(
                        LogicalFieldGroup.TileResources,
                        StateEntityReference.From(tile0)),
                    new DirtyStateEntity(
                        LogicalFieldGroup.OrganismPosition,
                        StateEntityReference.From(organism1)),
                ]),
            [TickPhase.EnvironmentalLedger] = new(
                [],
                [],
                [],
                [
                    new DirtyStateEntity(
                        LogicalFieldGroup.TileResources,
                        StateEntityReference.From(tile0)),
                ]),
            [TickPhase.IntrinsicDeath] = new(
                [],
                [StateEntityReference.From(organism3)],
                [],
                [
                    new DirtyStateEntity(
                        LogicalFieldGroup.OrganismLifecycle,
                        StateEntityReference.From(organism3)),
                ]),
            [TickPhase.Movement] = new(
                [],
                [],
                [
                    new EntityRelocation(organism1, tile0, tile1),
                    new EntityRelocation(organism2, tile0, tile1),
                ],
                []),
            [TickPhase.ExternalIntent] = new(
                [],
                [],
                [
                    new EntityRelocation(organism1, tile1, tile0),
                    new EntityRelocation(organism2, tile1, tile2),
                ],
                [
                    new DirtyStateEntity(
                        LogicalFieldGroup.OrganismReserve,
                        StateEntityReference.From(organism1)),
                ]),
            [TickPhase.LifecycleAndReproduction] = new(
                [],
                [StateEntityReference.From(transient)],
                [],
                []),
        });

        var merged = TickChangeMerger.Merge(1, journals);

        Assert.Equal(2, merged.StoreChanges.Length);
        var tileChanges = Assert.Single(
            merged.StoreChanges,
            store => store.EntityKind == StateEntityKind.Tile);
        Assert.Empty(tileChanges.Creates);
        Assert.Empty(tileChanges.Removes);
        Assert.Empty(tileChanges.Relocations);
        var tileDirty = Assert.Single(tileChanges.DirtyFieldGroups);
        Assert.Equal(LogicalFieldGroup.TileResources, tileDirty.FieldGroup);
        Assert.Equal([0UL], tileDirty.Entities.Select(entity => entity.Value));

        var organismChanges = Assert.Single(
            merged.StoreChanges,
            store => store.EntityKind == StateEntityKind.Organism);
        Assert.Empty(organismChanges.Creates);
        Assert.Equal(
            [organism3.Value],
            organismChanges.Removes.Select(entity => entity.Value));
        var relocation = Assert.Single(organismChanges.Relocations);
        Assert.Equal(organism2, relocation.OrganismId);
        Assert.Equal(tile0, relocation.SourceTileId);
        Assert.Equal(tile2, relocation.DestinationTileId);
        Assert.Equal(
            [LogicalFieldGroup.OrganismPosition, LogicalFieldGroup.OrganismReserve],
            organismChanges.DirtyFieldGroups.Select(group => group.FieldGroup));
        Assert.All(
            organismChanges.DirtyFieldGroups,
            group => Assert.Equal(
                [organism1.Value],
                group.Entities.Select(entity => entity.Value)));
        Assert.DoesNotContain(
            organismChanges.DirtyFieldGroups.SelectMany(group => group.Entities),
            entity => entity.Value == transient.Value || entity.Value == organism3.Value);
    }

    [Fact]
    public void MergeRejectsInvalidLifecycleAndRelocationSequences()
    {
        var organism = OrganismId.FromAllocatedValue(1);
        var reference = StateEntityReference.From(organism);
        var tile0 = TileId.FromRowMajorIndex(0);
        var tile1 = TileId.FromRowMajorIndex(1);
        var tile2 = TileId.FromRowMajorIndex(2);

        var removeThenCreate = BuildJournals(new Dictionary<TickPhase, PhaseChangeSet>
        {
            [TickPhase.CommandAdmission] = new([], [reference], [], []),
            [TickPhase.CalendarAndConditions] = new([reference], [], [], []),
        });
        Assert.Throws<InvalidOperationException>(() =>
            TickChangeMerger.Merge(1, removeThenCreate));

        var discontinuousRelocation = BuildJournals(new Dictionary<TickPhase, PhaseChangeSet>
        {
            [TickPhase.Movement] = new(
                [],
                [],
                [new EntityRelocation(organism, tile0, tile1)],
                []),
            [TickPhase.ExternalIntent] = new(
                [],
                [],
                [new EntityRelocation(organism, tile2, tile0)],
                []),
        });
        Assert.Throws<InvalidOperationException>(() =>
            TickChangeMerger.Merge(1, discontinuousRelocation));

        var wrongDirtyKind = BuildJournals(new Dictionary<TickPhase, PhaseChangeSet>
        {
            [TickPhase.Movement] = new(
                [],
                [],
                [],
                [
                    new DirtyStateEntity(
                        LogicalFieldGroup.TileResources,
                        reference),
                ]),
        });
        Assert.Throws<InvalidOperationException>(() =>
            TickChangeMerger.Merge(1, wrongDirtyKind));
    }

    [Fact]
    public void MergeRejectsNonCanonicalAndUnboundedInput()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TickChangeMerger.Merge(1, ImmutableArray<TickPhaseJournal>.Empty));

        var reference = StateEntityReference.From(OrganismId.FromAllocatedValue(1));
        var excessiveCreates = ImmutableArray.CreateRange(
            Enumerable.Repeat(reference, TickChangeLimits.MaximumEntityOperations + 1));
        var journals = BuildJournals(new Dictionary<TickPhase, PhaseChangeSet>
        {
            [TickPhase.CommandAdmission] = new(excessiveCreates, [], [], []),
        });

        Assert.Throws<InvalidOperationException>(() =>
            TickChangeMerger.Merge(1, journals));
    }

    [Fact]
    public void InspectorIsIndependentOfPhaseLocalSetEnumerationOrder()
    {
        var first = OrganismId.FromAllocatedValue(1);
        var second = OrganismId.FromAllocatedValue(2);
        var firstDirty = new DirtyStateEntity(
            LogicalFieldGroup.OrganismReserve,
            StateEntityReference.From(first));
        var secondDirty = new DirtyStateEntity(
            LogicalFieldGroup.OrganismReserve,
            StateEntityReference.From(second));
        var forward = BuildJournals(new Dictionary<TickPhase, PhaseChangeSet>
        {
            [TickPhase.InternalMetabolism] = new(
                [],
                [],
                [],
                [firstDirty, secondDirty]),
        });
        var reverse = BuildJournals(new Dictionary<TickPhase, PhaseChangeSet>
        {
            [TickPhase.InternalMetabolism] = new(
                [],
                [],
                [],
                [secondDirty, firstDirty]),
        });
        var firstTick = new TickChangeSet(
            1,
            1,
            forward,
            TickChangeMerger.Merge(1, forward));
        var secondTick = new TickChangeSet(
            1,
            1,
            reverse,
            TickChangeMerger.Merge(1, reverse));

        Assert.Equal(
            TickChangeInspector.ToCanonicalJson(firstTick),
            TickChangeInspector.ToCanonicalJson(secondTick));
    }

    private static ImmutableArray<TickPhaseJournal> BuildJournals(
        Dictionary<TickPhase, PhaseChangeSet> changes)
    {
        var journals = ImmutableArray.CreateBuilder<TickPhaseJournal>(
            Enum.GetValues<TickPhase>().Length);
        foreach (var phase in Enum.GetValues<TickPhase>())
        {
            journals.Add(new TickPhaseJournal(
                phase,
                PhaseExecutionClass.OwnerOnly,
                0,
                changes.TryGetValue(phase, out var phaseChanges)
                    ? phaseChanges
                    : EmptyChanges,
                ImmutableArray<ResourceTransaction>.Empty));
        }

        return journals.MoveToImmutable();
    }
}

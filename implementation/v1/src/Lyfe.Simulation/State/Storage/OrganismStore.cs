using System.Collections.Immutable;
using Lyfe.Simulation.Behavior;
using Lyfe.Simulation.Physiology;
using Lyfe.Simulation.State.Changes;
using Lyfe.Simulation.State.Identity;

namespace Lyfe.Simulation.State.Storage;

internal enum LifecyclePhase : byte
{
    Mature = 1,
}

internal readonly record struct OrganismInitialState(
    SpeciesId SpeciesId,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    long VelocityXQPerHour,
    long VelocityYQPerHour,
    ulong BirthTick,
    ulong BiologicalAgeHours,
    LifecyclePhase LifecyclePhase,
    ulong ReproductionNotBeforeTick,
    ulong SuccessfulReproductionCount,
    ulong ScavengeNotBeforeTick,
    long IngestedStructuralMatterQ,
    long StructuralMatterQ,
    long ChargedReserveQ,
    OrganismBehaviorState Behavior,
    MicronutrientInventory CommittedMicronutrients = default,
    MicronutrientInventory FreeMicronutrients = default);

internal readonly record struct OrganismSnapshot(
    OrganismId Id,
    SpeciesId SpeciesId,
    TileId TileId,
    uint PositionXQ,
    uint PositionYQ,
    long VelocityXQPerHour,
    long VelocityYQPerHour,
    ulong BirthTick,
    ulong BiologicalAgeHours,
    LifecyclePhase LifecyclePhase,
    ulong ReproductionNotBeforeTick,
    ulong SuccessfulReproductionCount,
    ulong ScavengeNotBeforeTick,
    long IngestedStructuralMatterQ,
    long StructuralMatterQ,
    long ChargedReserveQ,
    MaterializedOrganismCondition Condition,
    OrganismBehaviorState Behavior,
    MicronutrientInventory CommittedMicronutrients = default,
    MicronutrientInventory FreeMicronutrients = default);

internal readonly record struct OrganismLocation(
    TileId TileId,
    int ChunkIndex,
    int RowIndex);

internal sealed class OrganismStore
{
    internal const int ChunkCapacity = 256;

    private readonly OrganismPartition?[] partitions;
    private readonly Dictionary<OrganismId, OrganismLocation> locations = [];

    public OrganismStore(int tileCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileCount);
        partitions = new OrganismPartition[tileCount];
    }

    public int Count { get; private set; }

    public ulong MutationEpoch { get; private set; }

    public void Restore(
        OrganismId id,
        OrganismInitialState state,
        MaterializedOrganismCondition condition)
    {
        if (MutationEpoch != 0)
        {
            throw new InvalidOperationException(
                "Persisted organisms can only be restored into an unmutated store.");
        }

        ValidateInitialState(id, state);
        if (locations.ContainsKey(id))
        {
            throw new ArgumentException("Persisted organism IDs must be unique.", nameof(id));
        }

        var location = GetOrCreatePartition(state.TileId).Append(ToRow(id, state, condition));
        locations.Add(id, location);
        Count = checked(Count + 1);
    }

    public void Create(
        OrganismId id,
        OrganismInitialState state,
        MaterializedOrganismCondition condition,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ValidateInitialState(id, state);
        if (locations.ContainsKey(id))
        {
            throw new InvalidOperationException($"Organism {id} already exists.");
        }

        var partition = GetOrCreatePartition(state.TileId);
        var location = partition.Append(ToRow(id, state, condition));
        locations.Add(id, location);
        Count = checked(Count + 1);
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordCreate(StateEntityReference.From(id));
    }

    public OrganismSnapshot Get(OrganismId id)
    {
        var location = GetLocation(id);
        return GetPartition(location.TileId).Read(location);
    }

    public bool Contains(OrganismId id) => locations.ContainsKey(id);

    public void SetSpecies(
        OrganismId id,
        SpeciesId speciesId,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (speciesId == default)
        {
            throw new ArgumentException("Species identity must be nonzero.", nameof(speciesId));
        }
        var location = GetLocation(id);
        var partition = GetPartition(location.TileId);
        if (!partition.SetSpecies(location, speciesId))
        {
            return;
        }
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.OrganismSpecies);
    }

    public void SetPosition(
        OrganismId id,
        uint positionXQ,
        uint positionYQ,
        long velocityXQPerHour,
        long velocityYQPerHour,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var location = GetLocation(id);
        var partition = GetPartition(location.TileId);
        if (!partition.SetPosition(
                location,
                positionXQ,
                positionYQ,
                velocityXQPerHour,
                velocityYQPerHour))
        {
            return;
        }

        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.OrganismPosition);
    }

    public void AdjustStructure(
        OrganismId id,
        long delta,
        PhaseChangeBuilder changes)
    {
        AdjustNonnegativeBalance(
            id,
            delta,
            LogicalFieldGroup.OrganismStructure,
            static (partition, location, value) => partition.SetStructure(location, value),
            static (partition, location) => partition.Read(location).StructuralMatterQ,
            changes);
    }

    public void AdjustIngestedStructuralMatter(
        OrganismId id,
        long delta,
        PhaseChangeBuilder changes)
    {
        AdjustNonnegativeBalance(
            id,
            delta,
            LogicalFieldGroup.OrganismIngestedMatter,
            static (partition, location, value) =>
                partition.SetIngestedStructuralMatter(location, value),
            static (partition, location) =>
                partition.Read(location).IngestedStructuralMatterQ,
            changes);
    }

    public void AdjustChargedReserve(
        OrganismId id,
        long delta,
        PhaseChangeBuilder changes)
    {
        AdjustNonnegativeBalance(
            id,
            delta,
            LogicalFieldGroup.OrganismReserve,
            static (partition, location, value) => partition.SetChargedReserve(location, value),
            static (partition, location) => partition.Read(location).ChargedReserveQ,
            changes);
    }

    public void SetMicronutrients(
        OrganismId id,
        MicronutrientInventory committed,
        MicronutrientInventory free,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var location = GetLocation(id);
        var partition = GetPartition(location.TileId);
        var current = partition.Read(location);
        if (current.CommittedMicronutrients == committed && current.FreeMicronutrients == free)
        {
            return;
        }
        partition.SetMicronutrients(location, committed, free);
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.OrganismMicronutrients);
    }

    public void AdvanceBiologicalAge(
        OrganismId id,
        ulong elapsedHours,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (elapsedHours == 0)
        {
            return;
        }

        var location = GetLocation(id);
        var partition = GetPartition(location.TileId);
        var current = partition.Read(location).BiologicalAgeHours;
        SetBiologicalAge(id, current, checked(current + elapsedHours), changes);
    }

    public void SetBiologicalAge(
        OrganismId id,
        ulong expectedAgeHours,
        ulong nextAgeHours,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var location = GetLocation(id);
        var partition = GetPartition(location.TileId);
        var current = partition.Read(location).BiologicalAgeHours;
        if (current != expectedAgeHours)
        {
            throw new InvalidOperationException(
                $"Organism {id} has biological age {current}, not expected age {expectedAgeHours}.");
        }

        if (current == nextAgeHours)
        {
            return;
        }

        partition.SetBiologicalAge(location, nextAgeHours);
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.OrganismLifecycle);
    }

    public void SetCondition(
        OrganismId id,
        MaterializedOrganismCondition condition,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var location = GetLocation(id);
        var partition = GetPartition(location.TileId);
        if (partition.Read(location).Condition == condition)
        {
            return;
        }

        partition.SetCondition(location, condition);
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.OrganismCondition);
    }

    public void SetBehavior(
        OrganismId id,
        OrganismBehaviorState behavior,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ValidateBehavior(behavior);
        var location = GetLocation(id);
        var partition = GetPartition(location.TileId);
        if (!partition.SetBehavior(location, behavior))
        {
            return;
        }

        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.OrganismBehavior);
    }

    public void SetLifecycleSchedule(
        OrganismId id,
        ulong reproductionNotBeforeTick,
        ulong successfulReproductionCount,
        ulong scavengeNotBeforeTick,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var location = GetLocation(id);
        var partition = GetPartition(location.TileId);
        if (!partition.SetLifecycleSchedule(
                location,
                reproductionNotBeforeTick,
                successfulReproductionCount,
                scavengeNotBeforeTick))
        {
            return;
        }

        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.OrganismLifecycle);
    }

    public void Relocate(
        OrganismId id,
        TileId destinationTileId,
        uint positionXQ,
        uint positionYQ,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ValidateTile(destinationTileId);
        var sourceLocation = GetLocation(id);
        if (sourceLocation.TileId == destinationTileId)
        {
            var current = GetPartition(sourceLocation.TileId).Read(sourceLocation);
            SetPosition(
                id,
                positionXQ,
                positionYQ,
                current.VelocityXQPerHour,
                current.VelocityYQPerHour,
                changes);
            return;
        }

        var sourcePartition = GetPartition(sourceLocation.TileId);
        var row = sourcePartition.ReadRow(sourceLocation) with
        {
            PositionXQ = positionXQ,
            PositionYQ = positionYQ,
        };
        var swapped = sourcePartition.RemoveAt(sourceLocation);
        if (swapped is not null)
        {
            locations[swapped.Value.Id] = swapped.Value.Location;
        }

        var destination = GetOrCreatePartition(destinationTileId).Append(row);
        locations[id] = destination;
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordRelocation(new EntityRelocation(id, sourceLocation.TileId, destinationTileId));
        changes.RecordDirty(StateEntityReference.From(id), LogicalFieldGroup.OrganismPosition);
    }

    public OrganismSnapshot Remove(OrganismId id, PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var location = GetLocation(id);
        var partition = GetPartition(location.TileId);
        var snapshot = partition.Read(location);
        var swapped = partition.RemoveAt(location);
        if (swapped is not null)
        {
            locations[swapped.Value.Id] = swapped.Value.Location;
        }

        locations.Remove(id);
        Count--;
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordRemove(StateEntityReference.From(id));
        return snapshot;
    }

    public ImmutableArray<OrganismId> GetIdsInCanonicalOrder() =>
        locations.Keys.OrderBy(id => id.Value).ToImmutableArray();

    public int GetAllocatedChunkCount(TileId tileId)
    {
        ValidateTile(tileId);
        return partitions[tileId.Value]?.ChunkCount ?? 0;
    }

    public void ValidateLocators()
    {
        var visited = 0;
        for (var tileIndex = 0; tileIndex < partitions.Length; tileIndex++)
        {
            var partition = partitions[tileIndex];
            if (partition is null)
            {
                continue;
            }

            visited += partition.ValidateLocators(locations);
        }

        if (visited != Count || locations.Count != Count)
        {
            throw new InvalidOperationException("Organism row, locator, and count totals disagree.");
        }
    }

    public ulong ComputeCoverageFingerprint()
    {
        var hash = new ShadowHashAccumulator();
        hash.Add(Count);
        for (var tileIndex = 0; tileIndex < partitions.Length; tileIndex++)
        {
            hash.Add(tileIndex);
            partitions[tileIndex]?.AddCoverageFingerprint(ref hash);
        }

        return hash.Value;
    }

#if DEBUG
    public void DebugSetChargedReserveWithoutTracking(OrganismId id, long value)
    {
        var location = GetLocation(id);
        GetPartition(location.TileId).SetChargedReserve(location, value);
    }
#endif

    private void AdjustNonnegativeBalance(
        OrganismId id,
        long delta,
        LogicalFieldGroup fieldGroup,
        Action<OrganismPartition, OrganismLocation, long> setter,
        Func<OrganismPartition, OrganismLocation, long> getter,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (delta == 0)
        {
            return;
        }

        var location = GetLocation(id);
        var partition = GetPartition(location.TileId);
        var next = checked(getter(partition, location) + delta);
        if (next < 0)
        {
            throw new InvalidOperationException("An organism balance cannot become negative.");
        }

        setter(partition, location, next);
        MutationEpoch = checked(MutationEpoch + 1);
        changes.RecordMutation(WorldStoreKind.Organisms);
        changes.RecordDirty(StateEntityReference.From(id), fieldGroup);
    }

    private OrganismLocation GetLocation(OrganismId id)
    {
        if (!locations.TryGetValue(id, out var location))
        {
            throw new KeyNotFoundException($"Organism {id} does not exist.");
        }

        return location;
    }

    private OrganismPartition GetOrCreatePartition(TileId tileId)
    {
        ValidateTile(tileId);
        return partitions[tileId.Value] ??= new OrganismPartition(tileId);
    }

    private OrganismPartition GetPartition(TileId tileId) =>
        partitions[tileId.Value] ??
        throw new InvalidOperationException($"Tile {tileId} has no organism partition.");

    private void ValidateInitialState(OrganismId id, OrganismInitialState state)
    {
        if (id == default || state.SpeciesId == default || state.LifecyclePhase == default)
        {
            throw new ArgumentException("Organism identity, species, and lifecycle phase must be nonzero.");
        }

        ValidateTile(state.TileId);
        if (state.IngestedStructuralMatterQ < 0 ||
            state.StructuralMatterQ < 0 ||
            state.ChargedReserveQ < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Organism balances cannot be negative.");
        }

        ValidateBehavior(state.Behavior);
    }

    private static void ValidateBehavior(OrganismBehaviorState behavior)
    {
        if (!Enum.IsDefined(behavior.BehaviorId) ||
            !Enum.IsDefined(behavior.TargetKind) ||
            behavior.RecentEnergyCoverageQ > BehaviorMath.MaximumCoverageQ ||
            behavior.RecentAcquisitionCoverageQ > BehaviorMath.MaximumCoverageQ ||
            behavior.LimitingMaterialDeficitQ > BehaviorMath.OneQ ||
            (behavior.TargetKind == BehaviorTargetKind.None && behavior.TargetId != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(behavior), "Behavior state is invalid.");
        }
    }

    private void ValidateTile(TileId tileId)
    {
        if (tileId.Value >= (uint)partitions.Length)
        {
            throw new KeyNotFoundException($"Tile {tileId} does not exist.");
        }
    }

    private static OrganismRow ToRow(
        OrganismId id,
        OrganismInitialState state,
        MaterializedOrganismCondition condition) =>
        new(
            id,
            state.SpeciesId,
            state.PositionXQ,
            state.PositionYQ,
            state.VelocityXQPerHour,
            state.VelocityYQPerHour,
            state.BirthTick,
            state.BiologicalAgeHours,
            state.LifecyclePhase,
            state.ReproductionNotBeforeTick,
            state.SuccessfulReproductionCount,
            state.ScavengeNotBeforeTick,
            state.IngestedStructuralMatterQ,
            state.StructuralMatterQ,
            state.ChargedReserveQ,
            condition,
            state.Behavior,
            state.CommittedMicronutrients,
            state.FreeMicronutrients);

    private sealed class OrganismPartition(TileId tileId)
    {
        private readonly List<OrganismChunk> chunks = [];

        public int ChunkCount => chunks.Count;

        public OrganismLocation Append(OrganismRow row)
        {
            if (chunks.Count == 0 || chunks[^1].Count == ChunkCapacity)
            {
                chunks.Add(new OrganismChunk());
            }

            var chunkIndex = chunks.Count - 1;
            var rowIndex = chunks[chunkIndex].Append(row);
            return new OrganismLocation(tileId, chunkIndex, rowIndex);
        }

        public OrganismSnapshot Read(OrganismLocation location) =>
            chunks[location.ChunkIndex].Read(location.RowIndex, tileId);

        public OrganismRow ReadRow(OrganismLocation location) =>
            chunks[location.ChunkIndex].ReadRow(location.RowIndex);

        public bool SetPosition(
            OrganismLocation location,
            uint positionXQ,
            uint positionYQ,
            long velocityXQPerHour,
            long velocityYQPerHour) =>
            chunks[location.ChunkIndex].SetPosition(
                location.RowIndex,
                positionXQ,
                positionYQ,
                velocityXQPerHour,
                velocityYQPerHour);

        public bool SetSpecies(OrganismLocation location, SpeciesId speciesId) =>
            chunks[location.ChunkIndex].SetSpecies(location.RowIndex, speciesId);

        public void SetStructure(OrganismLocation location, long value) =>
            chunks[location.ChunkIndex].SetStructure(location.RowIndex, value);

        public void SetIngestedStructuralMatter(OrganismLocation location, long value) =>
            chunks[location.ChunkIndex].SetIngestedStructuralMatter(location.RowIndex, value);

        public void SetChargedReserve(OrganismLocation location, long value) =>
            chunks[location.ChunkIndex].SetChargedReserve(location.RowIndex, value);

        public void SetMicronutrients(
            OrganismLocation location,
            MicronutrientInventory committed,
            MicronutrientInventory free) =>
            chunks[location.ChunkIndex].SetMicronutrients(location.RowIndex, committed, free);

        public void SetBiologicalAge(OrganismLocation location, ulong value) =>
            chunks[location.ChunkIndex].SetBiologicalAge(location.RowIndex, value);

        public bool SetLifecycleSchedule(
            OrganismLocation location,
            ulong reproductionNotBeforeTick,
            ulong successfulReproductionCount,
            ulong scavengeNotBeforeTick) =>
            chunks[location.ChunkIndex].SetLifecycleSchedule(
                location.RowIndex,
                reproductionNotBeforeTick,
                successfulReproductionCount,
                scavengeNotBeforeTick);

        public void SetCondition(
            OrganismLocation location,
            MaterializedOrganismCondition value) =>
            chunks[location.ChunkIndex].SetCondition(location.RowIndex, value);

        public bool SetBehavior(OrganismLocation location, OrganismBehaviorState value) =>
            chunks[location.ChunkIndex].SetBehavior(location.RowIndex, value);

        public SwappedOrganism? RemoveAt(OrganismLocation location)
        {
            var tailChunkIndex = chunks.Count - 1;
            var tailChunk = chunks[tailChunkIndex];
            var tailRowIndex = tailChunk.Count - 1;
            SwappedOrganism? swapped = null;

            if (location.ChunkIndex != tailChunkIndex || location.RowIndex != tailRowIndex)
            {
                var tail = tailChunk.ReadRow(tailRowIndex);
                chunks[location.ChunkIndex].Write(location.RowIndex, tail);
                swapped = new SwappedOrganism(
                    tail.Id,
                    new OrganismLocation(tileId, location.ChunkIndex, location.RowIndex));
            }

            tailChunk.RemoveLast();
            if (tailChunk.Count == 0)
            {
                chunks.RemoveAt(tailChunkIndex);
            }

            return swapped;
        }

        public int ValidateLocators(Dictionary<OrganismId, OrganismLocation> locations)
        {
            var visited = 0;
            for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                for (var rowIndex = 0; rowIndex < chunks[chunkIndex].Count; rowIndex++)
                {
                    visited++;
                    var id = chunks[chunkIndex].GetId(rowIndex);
                    var expected = new OrganismLocation(tileId, chunkIndex, rowIndex);
                    if (!locations.TryGetValue(id, out var actual) || actual != expected)
                    {
                        throw new InvalidOperationException($"Organism locator for {id} is inconsistent.");
                    }
                }
            }

            return visited;
        }

        public void AddCoverageFingerprint(ref ShadowHashAccumulator hash)
        {
            hash.Add(chunks.Count);
            foreach (var chunk in chunks)
            {
                chunk.AddCoverageFingerprint(ref hash);
            }
        }
    }

    private sealed class OrganismChunk
    {
        private readonly OrganismId[] ids = new OrganismId[ChunkCapacity];
        private readonly SpeciesId[] speciesIds = new SpeciesId[ChunkCapacity];
        private readonly uint[] positionXQ = new uint[ChunkCapacity];
        private readonly uint[] positionYQ = new uint[ChunkCapacity];
        private readonly long[] velocityXQPerHour = new long[ChunkCapacity];
        private readonly long[] velocityYQPerHour = new long[ChunkCapacity];
        private readonly ulong[] birthTicks = new ulong[ChunkCapacity];
        private readonly ulong[] biologicalAgeHours = new ulong[ChunkCapacity];
        private readonly LifecyclePhase[] lifecyclePhases = new LifecyclePhase[ChunkCapacity];
        private readonly ulong[] reproductionNotBeforeTicks = new ulong[ChunkCapacity];
        private readonly ulong[] successfulReproductionCounts = new ulong[ChunkCapacity];
        private readonly ulong[] scavengeNotBeforeTicks = new ulong[ChunkCapacity];
        private readonly long[] ingestedStructuralMatterQ = new long[ChunkCapacity];
        private readonly long[] structuralMatterQ = new long[ChunkCapacity];
        private readonly long[] chargedReserveQ = new long[ChunkCapacity];
        private readonly MicronutrientInventory[] committedMicronutrients =
            new MicronutrientInventory[ChunkCapacity];
        private readonly MicronutrientInventory[] freeMicronutrients =
            new MicronutrientInventory[ChunkCapacity];
        private readonly MaterializedOrganismCondition[] conditions =
            new MaterializedOrganismCondition[ChunkCapacity];
        private readonly OrganismBehaviorState[] behaviors =
            new OrganismBehaviorState[ChunkCapacity];

        public int Count { get; private set; }

        public int Append(OrganismRow row)
        {
            if (Count == ChunkCapacity)
            {
                throw new InvalidOperationException("Organism chunk is full.");
            }

            var index = Count;
            Write(index, row);
            Count++;
            return index;
        }

        public OrganismId GetId(int rowIndex) => ids[rowIndex];

        public OrganismSnapshot Read(int rowIndex, TileId tileId)
        {
            ValidateRow(rowIndex);
            return new OrganismSnapshot(
                ids[rowIndex],
                speciesIds[rowIndex],
                tileId,
                positionXQ[rowIndex],
                positionYQ[rowIndex],
                velocityXQPerHour[rowIndex],
                velocityYQPerHour[rowIndex],
                birthTicks[rowIndex],
                biologicalAgeHours[rowIndex],
                lifecyclePhases[rowIndex],
                reproductionNotBeforeTicks[rowIndex],
                successfulReproductionCounts[rowIndex],
                scavengeNotBeforeTicks[rowIndex],
                ingestedStructuralMatterQ[rowIndex],
                structuralMatterQ[rowIndex],
                chargedReserveQ[rowIndex],
                conditions[rowIndex],
                behaviors[rowIndex],
                committedMicronutrients[rowIndex],
                freeMicronutrients[rowIndex]);
        }

        public OrganismRow ReadRow(int rowIndex)
        {
            ValidateRow(rowIndex);
            return new OrganismRow(
                ids[rowIndex],
                speciesIds[rowIndex],
                positionXQ[rowIndex],
                positionYQ[rowIndex],
                velocityXQPerHour[rowIndex],
                velocityYQPerHour[rowIndex],
                birthTicks[rowIndex],
                biologicalAgeHours[rowIndex],
                lifecyclePhases[rowIndex],
                reproductionNotBeforeTicks[rowIndex],
                successfulReproductionCounts[rowIndex],
                scavengeNotBeforeTicks[rowIndex],
                ingestedStructuralMatterQ[rowIndex],
                structuralMatterQ[rowIndex],
                chargedReserveQ[rowIndex],
                conditions[rowIndex],
                behaviors[rowIndex],
                committedMicronutrients[rowIndex],
                freeMicronutrients[rowIndex]);
        }

        public void Write(int rowIndex, OrganismRow row)
        {
            ids[rowIndex] = row.Id;
            speciesIds[rowIndex] = row.SpeciesId;
            positionXQ[rowIndex] = row.PositionXQ;
            positionYQ[rowIndex] = row.PositionYQ;
            velocityXQPerHour[rowIndex] = row.VelocityXQPerHour;
            velocityYQPerHour[rowIndex] = row.VelocityYQPerHour;
            birthTicks[rowIndex] = row.BirthTick;
            biologicalAgeHours[rowIndex] = row.BiologicalAgeHours;
            lifecyclePhases[rowIndex] = row.LifecyclePhase;
            reproductionNotBeforeTicks[rowIndex] = row.ReproductionNotBeforeTick;
            successfulReproductionCounts[rowIndex] = row.SuccessfulReproductionCount;
            scavengeNotBeforeTicks[rowIndex] = row.ScavengeNotBeforeTick;
            ingestedStructuralMatterQ[rowIndex] = row.IngestedStructuralMatterQ;
            structuralMatterQ[rowIndex] = row.StructuralMatterQ;
            chargedReserveQ[rowIndex] = row.ChargedReserveQ;
            conditions[rowIndex] = row.Condition;
            behaviors[rowIndex] = row.Behavior;
            committedMicronutrients[rowIndex] = row.CommittedMicronutrients;
            freeMicronutrients[rowIndex] = row.FreeMicronutrients;
        }

        public bool SetPosition(
            int rowIndex,
            uint newPositionXQ,
            uint newPositionYQ,
            long newVelocityXQPerHour,
            long newVelocityYQPerHour)
        {
            ValidateRow(rowIndex);
            if (positionXQ[rowIndex] == newPositionXQ &&
                positionYQ[rowIndex] == newPositionYQ &&
                velocityXQPerHour[rowIndex] == newVelocityXQPerHour &&
                velocityYQPerHour[rowIndex] == newVelocityYQPerHour)
            {
                return false;
            }

            positionXQ[rowIndex] = newPositionXQ;
            positionYQ[rowIndex] = newPositionYQ;
            velocityXQPerHour[rowIndex] = newVelocityXQPerHour;
            velocityYQPerHour[rowIndex] = newVelocityYQPerHour;
            return true;
        }

        public bool SetSpecies(int rowIndex, SpeciesId speciesId)
        {
            ValidateRow(rowIndex);
            if (speciesIds[rowIndex] == speciesId)
            {
                return false;
            }
            speciesIds[rowIndex] = speciesId;
            return true;
        }

        public void SetStructure(int rowIndex, long value)
        {
            ValidateRow(rowIndex);
            structuralMatterQ[rowIndex] = value;
        }

        public void SetIngestedStructuralMatter(int rowIndex, long value)
        {
            ValidateRow(rowIndex);
            ingestedStructuralMatterQ[rowIndex] = value;
        }

        public void SetChargedReserve(int rowIndex, long value)
        {
            ValidateRow(rowIndex);
            chargedReserveQ[rowIndex] = value;
        }

        public void SetMicronutrients(
            int rowIndex,
            MicronutrientInventory committed,
            MicronutrientInventory free)
        {
            ValidateRow(rowIndex);
            committedMicronutrients[rowIndex] = committed;
            freeMicronutrients[rowIndex] = free;
        }

        public void SetCondition(int rowIndex, MaterializedOrganismCondition value)
        {
            ValidateRow(rowIndex);
            conditions[rowIndex] = value;
        }

        public bool SetBehavior(int rowIndex, OrganismBehaviorState value)
        {
            ValidateRow(rowIndex);
            if (behaviors[rowIndex] == value)
            {
                return false;
            }

            behaviors[rowIndex] = value;
            return true;
        }

        public void SetBiologicalAge(int rowIndex, ulong value)
        {
            ValidateRow(rowIndex);
            biologicalAgeHours[rowIndex] = value;
        }

        public bool SetLifecycleSchedule(
            int rowIndex,
            ulong reproductionNotBeforeTick,
            ulong successfulReproductionCount,
            ulong scavengeNotBeforeTick)
        {
            ValidateRow(rowIndex);
            if (reproductionNotBeforeTicks[rowIndex] == reproductionNotBeforeTick &&
                successfulReproductionCounts[rowIndex] == successfulReproductionCount &&
                scavengeNotBeforeTicks[rowIndex] == scavengeNotBeforeTick)
            {
                return false;
            }

            reproductionNotBeforeTicks[rowIndex] = reproductionNotBeforeTick;
            successfulReproductionCounts[rowIndex] = successfulReproductionCount;
            scavengeNotBeforeTicks[rowIndex] = scavengeNotBeforeTick;
            return true;
        }

        public void RemoveLast()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Organism chunk is empty.");
            }

            Count--;
            Write(Count, default);
        }

        public void AddCoverageFingerprint(ref ShadowHashAccumulator hash)
        {
            hash.Add(Count);
            for (var index = 0; index < Count; index++)
            {
                hash.Add(ids[index].Value);
                hash.Add(speciesIds[index].Value);
                hash.Add(positionXQ[index]);
                hash.Add(positionYQ[index]);
                hash.Add(velocityXQPerHour[index]);
                hash.Add(velocityYQPerHour[index]);
                hash.Add(birthTicks[index]);
                hash.Add(biologicalAgeHours[index]);
                hash.Add((uint)lifecyclePhases[index]);
                hash.Add(reproductionNotBeforeTicks[index]);
                hash.Add(successfulReproductionCounts[index]);
                hash.Add(scavengeNotBeforeTicks[index]);
                hash.Add(ingestedStructuralMatterQ[index]);
                hash.Add(structuralMatterQ[index]);
                hash.Add(chargedReserveQ[index]);
                AddMicronutrients(ref hash, committedMicronutrients[index]);
                AddMicronutrients(ref hash, freeMicronutrients[index]);
                hash.Add(conditions[index].EvaluatedTick);
                hash.Add((uint)conditions[index].SnapshotKind);
                hash.Add(conditions[index].ReserveFactorQ);
                hash.Add(conditions[index].StructureFactorQ);
                hash.Add(conditions[index].NutrientFactorQ);
                hash.Add(conditions[index].AgeFactorQ);
                hash.Add(conditions[index].LifecycleFactorQ);
                hash.Add(conditions[index].EnvironmentalFactorQ);
                hash.Add(conditions[index].RelativeHealthQ);
                hash.Add(conditions[index].TemperatureMilliC);
                hash.Add(conditions[index].TemperatureSeverityQ);
                hash.Add((uint)behaviors[index].BehaviorId);
                hash.Add((uint)behaviors[index].TargetKind);
                hash.Add(behaviors[index].TargetId);
                hash.Add(behaviors[index].TargetPositionXQ);
                hash.Add(behaviors[index].TargetPositionYQ);
                hash.Add(behaviors[index].SelectedAtTick);
                hash.Add(behaviors[index].MinimumDwellUntilTick);
                hash.Add(behaviors[index].RecentEnergyCoverageQ);
                hash.Add(behaviors[index].RecentAcquisitionCoverageQ);
                hash.Add(behaviors[index].LimitingMaterialDeficitQ);
            }
        }

        private void ValidateRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            }
        }

        private static void AddMicronutrients(
            ref ShadowHashAccumulator hash,
            MicronutrientInventory inventory)
        {
            for (var slot = 0; slot < MicronutrientInventory.Count; slot++)
            {
                hash.Add(inventory[slot]);
            }
        }
    }

    private readonly record struct OrganismRow(
        OrganismId Id,
        SpeciesId SpeciesId,
        uint PositionXQ,
        uint PositionYQ,
        long VelocityXQPerHour,
        long VelocityYQPerHour,
        ulong BirthTick,
        ulong BiologicalAgeHours,
        LifecyclePhase LifecyclePhase,
        ulong ReproductionNotBeforeTick,
        ulong SuccessfulReproductionCount,
        ulong ScavengeNotBeforeTick,
        long IngestedStructuralMatterQ,
        long StructuralMatterQ,
        long ChargedReserveQ,
        MaterializedOrganismCondition Condition,
        OrganismBehaviorState Behavior,
        MicronutrientInventory CommittedMicronutrients,
        MicronutrientInventory FreeMicronutrients);

    private readonly record struct SwappedOrganism(
        OrganismId Id,
        OrganismLocation Location);
}

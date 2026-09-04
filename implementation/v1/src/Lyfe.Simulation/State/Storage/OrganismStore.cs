using System.Collections.Immutable;
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
    long StructuralMatterQ,
    long ChargedReserveQ);

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
    long StructuralMatterQ,
    long ChargedReserveQ);

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

    public void Restore(OrganismId id, OrganismInitialState state)
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

        var location = GetOrCreatePartition(state.TileId).Append(ToRow(id, state));
        locations.Add(id, location);
        Count = checked(Count + 1);
    }

    public void Create(
        OrganismId id,
        OrganismInitialState state,
        PhaseChangeBuilder changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ValidateInitialState(id, state);
        if (locations.ContainsKey(id))
        {
            throw new InvalidOperationException($"Organism {id} already exists.");
        }

        var partition = GetOrCreatePartition(state.TileId);
        var location = partition.Append(ToRow(id, state));
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
        if (state.StructuralMatterQ < 0 || state.ChargedReserveQ < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Organism balances cannot be negative.");
        }
    }

    private void ValidateTile(TileId tileId)
    {
        if (tileId.Value >= (uint)partitions.Length)
        {
            throw new KeyNotFoundException($"Tile {tileId} does not exist.");
        }
    }

    private static OrganismRow ToRow(OrganismId id, OrganismInitialState state) =>
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
            state.StructuralMatterQ,
            state.ChargedReserveQ);

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

        public void SetStructure(OrganismLocation location, long value) =>
            chunks[location.ChunkIndex].SetStructure(location.RowIndex, value);

        public void SetChargedReserve(OrganismLocation location, long value) =>
            chunks[location.ChunkIndex].SetChargedReserve(location.RowIndex, value);

        public void SetBiologicalAge(OrganismLocation location, ulong value) =>
            chunks[location.ChunkIndex].SetBiologicalAge(location.RowIndex, value);

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
        private readonly long[] structuralMatterQ = new long[ChunkCapacity];
        private readonly long[] chargedReserveQ = new long[ChunkCapacity];

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
                structuralMatterQ[rowIndex],
                chargedReserveQ[rowIndex]);
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
                structuralMatterQ[rowIndex],
                chargedReserveQ[rowIndex]);
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
            structuralMatterQ[rowIndex] = row.StructuralMatterQ;
            chargedReserveQ[rowIndex] = row.ChargedReserveQ;
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

        public void SetStructure(int rowIndex, long value)
        {
            ValidateRow(rowIndex);
            structuralMatterQ[rowIndex] = value;
        }

        public void SetChargedReserve(int rowIndex, long value)
        {
            ValidateRow(rowIndex);
            chargedReserveQ[rowIndex] = value;
        }

        public void SetBiologicalAge(int rowIndex, ulong value)
        {
            ValidateRow(rowIndex);
            biologicalAgeHours[rowIndex] = value;
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
                hash.Add(structuralMatterQ[index]);
                hash.Add(chargedReserveQ[index]);
            }
        }

        private void ValidateRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
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
        long StructuralMatterQ,
        long ChargedReserveQ);

    private readonly record struct SwappedOrganism(
        OrganismId Id,
        OrganismLocation Location);
}

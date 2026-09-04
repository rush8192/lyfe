using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using Lyfe.Simulation.Publication;

namespace Lyfe.Server.Persistence;

public enum WorldPayloadFailureCode
{
    Truncated = 1,
    InvalidMagic = 2,
    UnsupportedSchemaVersion = 3,
    InvalidCount = 4,
    InvalidOrdering = 5,
    InvalidValue = 6,
    TrailingData = 7,
}

public sealed class WorldPayloadException : IOException
{
    internal WorldPayloadException(WorldPayloadFailureCode code, string message)
        : base(message) => Code = code;

    public WorldPayloadFailureCode Code { get; }
}

public static class WorldPayloadCodecV1
{
    public const uint SchemaVersion = 1;
    public const int MaximumTileResourceCount = 4_000_000;
    public const int MaximumGenomeCount = 1_000_000;
    public const int MaximumSpeciesCount = 1_000_000;
    public const int MaximumOrganismCount = 1_000_000;
    public const int MaximumTransactionCount = 1_000_000;
    public const int MaximumEntriesPerTransaction = 128;

    private static ReadOnlySpan<byte> Magic => "LYFEWLD1"u8;

    public static byte[] Encode(WorldPersistenceState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateCollections(state);
        ValidateCanonicalOrder(state);

        var writer = new PayloadWriter();
        writer.WriteBytes(Magic);
        writer.WriteUInt32(SchemaVersion);
        writer.WriteUInt64(state.NextGenomeId);
        writer.WriteUInt64(state.NextSpeciesId);
        writer.WriteUInt64(state.NextOrganismId);
        WriteCount(writer, state.TileResources.Length);
        WriteCount(writer, state.Genomes.Length);
        WriteCount(writer, state.Species.Length);
        WriteCount(writer, state.Organisms.Length);
        WriteCount(writer, state.LastCompletedTransactions.Length);

        foreach (var resource in state.TileResources)
        {
            writer.WriteUInt32(resource.TileId);
            writer.WriteUInt32(resource.ResourceId);
            writer.WriteInt64(resource.QuantityQ);
        }

        foreach (var genome in state.Genomes)
        {
            writer.WriteUInt64(genome.GenomeId);
            writer.WriteUInt32(genome.FounderGenomeId);
        }

        foreach (var species in state.Species)
        {
            writer.WriteUInt64(species.SpeciesId);
            writer.WriteUInt64(species.GenomeId);
            writer.WriteUInt64(species.Population);
        }

        foreach (var organism in state.Organisms)
        {
            writer.WriteUInt64(organism.OrganismId);
            writer.WriteUInt64(organism.SpeciesId);
            writer.WriteUInt32(organism.TileId);
            writer.WriteUInt32(organism.PositionXQ);
            writer.WriteUInt32(organism.PositionYQ);
            writer.WriteInt64(organism.VelocityXQPerHour);
            writer.WriteInt64(organism.VelocityYQPerHour);
            writer.WriteUInt64(organism.BirthTick);
            writer.WriteUInt64(organism.BiologicalAgeHours);
            writer.WriteByte(organism.LifecyclePhase);
            writer.WriteInt64(organism.StructuralMatterQ);
            writer.WriteInt64(organism.ChargedReserveQ);
        }

        foreach (var transaction in state.LastCompletedTransactions)
        {
            writer.WriteUInt64(transaction.Tick);
            writer.WriteByte(transaction.Phase);
            writer.WriteUInt64(transaction.ScopeId);
            writer.WriteUInt64(transaction.KeyActorId);
            writer.WriteUInt32(transaction.KeyReactionId);
            writer.WriteUInt32(transaction.LocalOrdinal);
            writer.WriteByte(transaction.Cause);
            writer.WriteUInt32(transaction.ReactionId);
            writer.WriteUInt64(transaction.ActorId);
            writer.WriteUInt32(transaction.TileId);
            writer.WriteInt64(transaction.Extent);
            WriteCount(writer, transaction.MatterEntries.Length);
            WriteCount(writer, transaction.EnergyEntries.Length);
            foreach (var entry in transaction.MatterEntries)
            {
                writer.WriteByte(entry.OwnerKind);
                writer.WriteUInt64(entry.OwnerId);
                writer.WriteByte(entry.Compartment);
                writer.WriteUInt32(entry.ResourceId);
                writer.WriteInt64(entry.DeltaQ);
            }

            foreach (var entry in transaction.EnergyEntries)
            {
                writer.WriteByte(entry.Kind);
                writer.WriteUInt64(entry.OwnerId);
                writer.WriteInt64(entry.DeltaQ);
            }
        }

        return writer.ToArray();
    }

    public static WorldPersistenceState Decode(ReadOnlySpan<byte> payload)
    {
        var reader = new PayloadReader(payload);
        if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
        {
            throw Failure(WorldPayloadFailureCode.InvalidMagic, "The logical world payload magic is invalid.");
        }

        var schemaVersion = reader.ReadUInt32();
        if (schemaVersion != SchemaVersion)
        {
            throw Failure(
                WorldPayloadFailureCode.UnsupportedSchemaVersion,
                $"Logical world payload schema {schemaVersion} is unsupported.");
        }

        var nextGenomeId = reader.ReadUInt64();
        var nextSpeciesId = reader.ReadUInt64();
        var nextOrganismId = reader.ReadUInt64();
        if (nextGenomeId == 0 || nextSpeciesId == 0 || nextOrganismId == 0)
        {
            throw Failure(WorldPayloadFailureCode.InvalidValue, "Next entity IDs must be nonzero.");
        }

        var tileResourceCount = ReadCount(reader.ReadUInt32(), MaximumTileResourceCount);
        var genomeCount = ReadCount(reader.ReadUInt32(), MaximumGenomeCount);
        var speciesCount = ReadCount(reader.ReadUInt32(), MaximumSpeciesCount);
        var organismCount = ReadCount(reader.ReadUInt32(), MaximumOrganismCount);
        var transactionCount = ReadCount(reader.ReadUInt32(), MaximumTransactionCount);

        var tileResources = ImmutableArray.CreateBuilder<PersistenceTileResource>(tileResourceCount);
        for (var index = 0; index < tileResourceCount; index++)
        {
            tileResources.Add(new PersistenceTileResource(
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt64()));
        }

        var genomes = ImmutableArray.CreateBuilder<PersistenceGenome>(genomeCount);
        for (var index = 0; index < genomeCount; index++)
        {
            genomes.Add(new PersistenceGenome(reader.ReadUInt64(), reader.ReadUInt32()));
        }

        var species = ImmutableArray.CreateBuilder<PersistenceSpecies>(speciesCount);
        for (var index = 0; index < speciesCount; index++)
        {
            species.Add(new PersistenceSpecies(
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64()));
        }

        var organisms = ImmutableArray.CreateBuilder<PersistenceOrganism>(organismCount);
        for (var index = 0; index < organismCount; index++)
        {
            organisms.Add(new PersistenceOrganism(
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadInt64(),
                reader.ReadInt64(),
                reader.ReadUInt64(),
                reader.ReadUInt64(),
                reader.ReadByte(),
                reader.ReadInt64(),
                reader.ReadInt64()));
        }

        var transactions = ImmutableArray.CreateBuilder<PersistenceResourceTransaction>(transactionCount);
        for (var index = 0; index < transactionCount; index++)
        {
            var tick = reader.ReadUInt64();
            var phase = reader.ReadByte();
            var scopeId = reader.ReadUInt64();
            var keyActorId = reader.ReadUInt64();
            var keyReactionId = reader.ReadUInt32();
            var localOrdinal = reader.ReadUInt32();
            var cause = reader.ReadByte();
            var reactionId = reader.ReadUInt32();
            var actorId = reader.ReadUInt64();
            var tileId = reader.ReadUInt32();
            var extent = reader.ReadInt64();
            var matterCount = ReadCount(reader.ReadUInt32(), MaximumEntriesPerTransaction);
            var energyCount = ReadCount(reader.ReadUInt32(), MaximumEntriesPerTransaction);
            var matter = ImmutableArray.CreateBuilder<PersistenceMatterEntry>(matterCount);
            for (var matterIndex = 0; matterIndex < matterCount; matterIndex++)
            {
                matter.Add(new PersistenceMatterEntry(
                    reader.ReadByte(),
                    reader.ReadUInt64(),
                    reader.ReadByte(),
                    reader.ReadUInt32(),
                    reader.ReadInt64()));
            }

            var energy = ImmutableArray.CreateBuilder<PersistenceEnergyEntry>(energyCount);
            for (var energyIndex = 0; energyIndex < energyCount; energyIndex++)
            {
                energy.Add(new PersistenceEnergyEntry(
                    reader.ReadByte(),
                    reader.ReadUInt64(),
                    reader.ReadInt64()));
            }

            transactions.Add(new PersistenceResourceTransaction(
                tick,
                phase,
                scopeId,
                keyActorId,
                keyReactionId,
                localOrdinal,
                cause,
                reactionId,
                actorId,
                tileId,
                extent,
                matter.MoveToImmutable(),
                energy.MoveToImmutable()));
        }

        if (!reader.IsComplete)
        {
            throw Failure(WorldPayloadFailureCode.TrailingData, "The logical world payload has trailing bytes.");
        }

        var result = new WorldPersistenceState(
            nextGenomeId,
            nextSpeciesId,
            nextOrganismId,
            tileResources.MoveToImmutable(),
            genomes.MoveToImmutable(),
            species.MoveToImmutable(),
            organisms.MoveToImmutable(),
            transactions.MoveToImmutable());
        ValidateCanonicalOrder(result);
        return result;
    }

    private static void ValidateCollections(WorldPersistenceState state)
    {
        if (state.NextGenomeId == 0 || state.NextSpeciesId == 0 || state.NextOrganismId == 0 ||
            state.TileResources.IsDefault || state.Genomes.IsDefault || state.Species.IsDefault ||
            state.Organisms.IsDefault || state.LastCompletedTransactions.IsDefault)
        {
            throw new ArgumentException("Logical world state is incomplete.", nameof(state));
        }

        RequireCount(state.TileResources.Length, MaximumTileResourceCount, nameof(state.TileResources));
        RequireCount(state.Genomes.Length, MaximumGenomeCount, nameof(state.Genomes));
        RequireCount(state.Species.Length, MaximumSpeciesCount, nameof(state.Species));
        RequireCount(state.Organisms.Length, MaximumOrganismCount, nameof(state.Organisms));
        RequireCount(
            state.LastCompletedTransactions.Length,
            MaximumTransactionCount,
            nameof(state.LastCompletedTransactions));
        foreach (var transaction in state.LastCompletedTransactions)
        {
            if (transaction.MatterEntries.IsDefault || transaction.EnergyEntries.IsDefault)
            {
                throw new ArgumentException("Ledger entry collections must be initialized.", nameof(state));
            }

            RequireCount(transaction.MatterEntries.Length, MaximumEntriesPerTransaction, "matter entries");
            RequireCount(transaction.EnergyEntries.Length, MaximumEntriesPerTransaction, "energy entries");
        }
    }

    private static void ValidateCanonicalOrder(WorldPersistenceState state)
    {
        for (var index = 1; index < state.TileResources.Length; index++)
        {
            var prior = state.TileResources[index - 1];
            var current = state.TileResources[index];
            if (prior.TileId > current.TileId ||
                (prior.TileId == current.TileId && prior.ResourceId >= current.ResourceId))
            {
                throw Failure(WorldPayloadFailureCode.InvalidOrdering, "Tile resources are not canonical.");
            }
        }

        RequireAscending(state.Genomes.Select(value => value.GenomeId), "genomes");
        RequireAscending(state.Species.Select(value => value.SpeciesId), "species");
        RequireAscending(state.Organisms.Select(value => value.OrganismId), "organisms");
        for (var index = 1; index < state.LastCompletedTransactions.Length; index++)
        {
            if (CompareTransactions(
                    state.LastCompletedTransactions[index - 1],
                    state.LastCompletedTransactions[index]) >= 0)
            {
                throw Failure(WorldPayloadFailureCode.InvalidOrdering, "Transactions are not canonical.");
            }
        }
    }

    private static int CompareTransactions(
        PersistenceResourceTransaction left,
        PersistenceResourceTransaction right)
    {
        var comparison = left.Tick.CompareTo(right.Tick);
        if (comparison != 0) return comparison;
        comparison = left.Phase.CompareTo(right.Phase);
        if (comparison != 0) return comparison;
        comparison = left.ScopeId.CompareTo(right.ScopeId);
        if (comparison != 0) return comparison;
        comparison = left.KeyActorId.CompareTo(right.KeyActorId);
        if (comparison != 0) return comparison;
        comparison = left.KeyReactionId.CompareTo(right.KeyReactionId);
        return comparison != 0 ? comparison : left.LocalOrdinal.CompareTo(right.LocalOrdinal);
    }

    private static void RequireAscending(IEnumerable<ulong> values, string label)
    {
        ulong prior = 0;
        foreach (var value in values)
        {
            if (value == 0 || value <= prior)
            {
                throw Failure(WorldPayloadFailureCode.InvalidOrdering, $"Persisted {label} are not canonical.");
            }

            prior = value;
        }
    }

    private static int ReadCount(uint value, int maximum)
    {
        if (value > maximum)
        {
            throw Failure(WorldPayloadFailureCode.InvalidCount, "A logical payload count exceeds its limit.");
        }

        return checked((int)value);
    }

    private static void RequireCount(int value, int maximum, string label)
    {
        if (value < 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(label, value, "Logical payload count exceeds its limit.");
        }
    }

    private static void WriteCount(PayloadWriter writer, int value) =>
        writer.WriteUInt32(checked((uint)value));

    private static WorldPayloadException Failure(WorldPayloadFailureCode code, string message) =>
        new(code, message);

    private sealed class PayloadWriter
    {
        private readonly ArrayBufferWriter<byte> buffer = new();

        public void WriteByte(byte value)
        {
            buffer.GetSpan(1)[0] = value;
            buffer.Advance(1);
        }

        public void WriteUInt32(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.GetSpan(sizeof(uint)), value);
            buffer.Advance(sizeof(uint));
        }

        public void WriteUInt64(ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.GetSpan(sizeof(ulong)), value);
            buffer.Advance(sizeof(ulong));
        }

        public void WriteInt64(long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(buffer.GetSpan(sizeof(long)), value);
            buffer.Advance(sizeof(long));
        }

        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            value.CopyTo(buffer.GetSpan(value.Length));
            buffer.Advance(value.Length);
        }

        public byte[] ToArray() => buffer.WrittenSpan.ToArray();
    }

    private ref struct PayloadReader(ReadOnlySpan<byte> source)
    {
        private readonly ReadOnlySpan<byte> source = source;
        private int offset;

        public readonly bool IsComplete => offset == source.Length;

        public byte ReadByte() => ReadBytes(1)[0];

        public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(sizeof(uint)));

        public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(sizeof(ulong)));

        public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(long)));

        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            if (length < 0 || source.Length - offset < length)
            {
                throw Failure(WorldPayloadFailureCode.Truncated, "The logical world payload is truncated.");
            }

            var result = source.Slice(offset, length);
            offset += length;
            return result;
        }
    }
}

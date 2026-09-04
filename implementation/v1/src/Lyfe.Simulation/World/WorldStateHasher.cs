using System.Collections.Immutable;
using System.Security.Cryptography;
using Lyfe.Simulation.Core;
using Lyfe.Simulation.Ledger;
using Lyfe.Simulation.Randomness;
using Lyfe.Simulation.Publication;
using Lyfe.Simulation.Serialization;
using Lyfe.Simulation.State;

namespace Lyfe.Simulation.World;

internal static class WorldStateHasher
{
    public const uint SchemaVersion = WorldPersistenceContract.WorldStateHashSchemaVersion;
    public const string SimulationVersion = WorldPersistenceContract.EngineSimulationVersion;

    private const uint WorldRecord = 0x3000;
    private const uint CompatibilityRecord = 0x3001;
    private const uint CounterRecord = 0x3002;
    private const uint TileRecord = 0x3100;
    private const uint TileResourceRecord = 0x3101;
    private const uint GenomeRecord = 0x3200;
    private const uint SpeciesRecord = 0x3300;
    private const uint OrganismRecord = 0x3400;
    private const uint LedgerTransactionRecord = 0x3500;
    private const uint MatterEntryRecord = 0x3501;
    private const uint EnergyEntryRecord = 0x3502;

    public static string Hash(
        WorldSnapshotContent boundary,
        MutableWorldState world,
        ImmutableArray<ResourceTransaction> completedTickTransactions)
    {
        ArgumentNullException.ThrowIfNull(world);
        var writer = new CanonicalBinaryWriter();
        writer.WriteUInt32(WorldRecord);
        WriteUInt32Field(writer, 1, SchemaVersion);
        WriteStringField(writer, 2, SimulationVersion);
        WriteCompatibility(writer, boundary, world);
        WriteUInt64Field(writer, 4, boundary.WorldId.Value);
        WriteUInt64Field(writer, 5, boundary.CompletedTick);
        WriteUInt64Field(writer, 6, boundary.WorldRevision);
        WriteUInt64Field(writer, 7, boundary.SimulatedHours);
        WriteUInt32Field(writer, 8, boundary.TickDurationHours);
        WriteByteField(writer, 9, (byte)boundary.RunnerStatus);
        WriteCounters(writer, world);
        WriteTiles(writer, world);
        WriteGenomes(writer, world);
        WriteSpecies(writer, world);
        WriteOrganisms(writer, world);
        WriteLedger(writer, completedTickTransactions);

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(writer.WrittenSpan, digest);
        return Convert.ToHexStringLower(digest);
    }

    private static void WriteCompatibility(
        CanonicalBinaryWriter writer,
        WorldSnapshotContent boundary,
        MutableWorldState world)
    {
        var rulePack = world.Rules.RulePack.Identity;
        var worldRules = world.Rules.Identity;
        Field(writer, 3);
        writer.WriteUInt32(CompatibilityRecord);
        WriteStringField(writer, 1, rulePack.PackId);
        WriteStringField(writer, 2, rulePack.DeclaredVersion);
        WriteUInt32Field(writer, 3, rulePack.EngineRuleApiVersion);
        WriteUInt32Field(writer, 4, rulePack.RuleCompilerVersion);
        WriteUInt32Field(writer, 5, rulePack.MechanicsHashSchemaVersion);
        WriteStringField(writer, 6, rulePack.MechanicsHash);
        WriteStringField(writer, 7, rulePack.RegistryManifestHash);
        WriteStringField(writer, 8, rulePack.CompiledArtifactHash);
        WriteUInt32Field(writer, 9, world.Rules.Scenario.Id.Value);
        WriteStringField(writer, 10, worldRules.WorldPackId);
        WriteStringField(writer, 11, worldRules.WorldPackVersion);
        WriteStringField(writer, 12, worldRules.WorldProfileKey);
        WriteStringField(writer, 13, worldRules.WorldPackageHash);
        WriteStringField(writer, 14, worldRules.CompiledWorldProfileHash);
        WriteStringField(writer, 15, worldRules.WorldRulesHash);
        WriteStringField(writer, 16, boundary.RandomCompatibility.AlgorithmId);
        WriteUInt32Field(writer, 17, boundary.RandomCompatibility.RngSchemaVersion);
        WriteStringField(writer, 18, boundary.RandomCompatibility.RngDomainManifestHash);
        WriteUInt64Field(writer, 19, boundary.RandomCompatibility.RootSeed.Low);
        WriteUInt64Field(writer, 20, boundary.RandomCompatibility.RootSeed.High);
    }

    private static void WriteCounters(CanonicalBinaryWriter writer, MutableWorldState world)
    {
        var counters = world.CaptureIdContinuationState();
        Field(writer, 10);
        writer.WriteUInt32(CounterRecord);
        WriteUInt64Field(writer, 1, counters.NextGenomeId);
        WriteUInt64Field(writer, 2, counters.NextSpeciesId);
        WriteUInt64Field(writer, 3, counters.NextOrganismId);
    }

    private static void WriteTiles(CanonicalBinaryWriter writer, MutableWorldState world)
    {
        var tileIds = world.GetTileIdsInCanonicalOrder();
        var resourceHandles = world.GetResourceHandlesInCanonicalOrder();
        Field(writer, 20);
        writer.WriteUInt32(checked((uint)tileIds.Length));
        foreach (var tileId in tileIds)
        {
            var tile = world.GetTile(tileId);
            writer.WriteUInt32(TileRecord);
            WriteUInt32Field(writer, 1, tile.Id.Value);
            WriteInt32Field(writer, 2, tile.X);
            WriteInt32Field(writer, 3, tile.Y);
            WriteInt32Field(writer, 4, tile.ElevationMeters);
            Field(writer, 5);
            writer.WriteUInt32(checked((uint)resourceHandles.Length));
            foreach (var resource in resourceHandles)
            {
                writer.WriteUInt32(TileResourceRecord);
                WriteUInt32Field(writer, 1, resource.Id.Value);
                WriteInt64Field(writer, 2, world.GetTileResource(tileId, resource));
            }
        }
    }

    private static void WriteGenomes(CanonicalBinaryWriter writer, MutableWorldState world)
    {
        var ids = world.GetGenomeIdsInCanonicalOrder();
        Field(writer, 30);
        writer.WriteUInt32(checked((uint)ids.Length));
        foreach (var id in ids)
        {
            var genome = world.GetGenome(id);
            writer.WriteUInt32(GenomeRecord);
            WriteUInt64Field(writer, 1, genome.Id.Value);
            WriteUInt32Field(writer, 2, genome.FounderGenomeId.Value);
            WriteUInt32Field(writer, 3, genome.Phenotype.FounderGenomeId.Value);
        }
    }

    private static void WriteSpecies(CanonicalBinaryWriter writer, MutableWorldState world)
    {
        var ids = world.GetSpeciesIdsInCanonicalOrder();
        Field(writer, 40);
        writer.WriteUInt32(checked((uint)ids.Length));
        foreach (var id in ids)
        {
            var species = world.GetSpecies(id);
            writer.WriteUInt32(SpeciesRecord);
            WriteUInt64Field(writer, 1, species.Id.Value);
            WriteUInt64Field(writer, 2, species.GenomeId.Value);
            WriteUInt32Field(writer, 3, species.Phenotype.FounderGenomeId.Value);
            WriteUInt64Field(writer, 4, species.Population);
        }
    }

    private static void WriteOrganisms(CanonicalBinaryWriter writer, MutableWorldState world)
    {
        var ids = world.GetOrganismIdsInCanonicalOrder();
        Field(writer, 50);
        writer.WriteUInt32(checked((uint)ids.Length));
        foreach (var id in ids)
        {
            var organism = world.GetOrganism(id);
            writer.WriteUInt32(OrganismRecord);
            WriteUInt64Field(writer, 1, organism.Id.Value);
            WriteUInt64Field(writer, 2, organism.SpeciesId.Value);
            WriteUInt32Field(writer, 3, organism.TileId.Value);
            WriteUInt32Field(writer, 4, organism.PositionXQ);
            WriteUInt32Field(writer, 5, organism.PositionYQ);
            WriteInt64Field(writer, 6, organism.VelocityXQPerHour);
            WriteInt64Field(writer, 7, organism.VelocityYQPerHour);
            WriteUInt64Field(writer, 8, organism.BirthTick);
            WriteUInt64Field(writer, 9, organism.BiologicalAgeHours);
            WriteByteField(writer, 10, (byte)organism.LifecyclePhase);
            WriteInt64Field(writer, 11, organism.StructuralMatterQ);
            WriteInt64Field(writer, 12, organism.ChargedReserveQ);
        }
    }

    private static void WriteLedger(
        CanonicalBinaryWriter writer,
        ImmutableArray<ResourceTransaction> transactions)
    {
        var ordered = transactions.OrderBy(transaction => transaction.Key).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].Key == ordered[index].Key)
            {
                throw new InvalidOperationException(
                    "Completed-tick ledger transactions must have unique canonical keys.");
            }
        }

        Field(writer, 60);
        writer.WriteUInt32(checked((uint)ordered.Length));
        foreach (var transaction in ordered)
        {
            writer.WriteUInt32(LedgerTransactionRecord);
            WriteUInt64Field(writer, 1, transaction.Key.Tick);
            WriteByteField(writer, 2, (byte)transaction.Key.Phase);
            WriteUInt64Field(writer, 3, transaction.Key.ScopeId);
            WriteUInt64Field(writer, 4, transaction.Key.ActorId);
            WriteUInt32Field(writer, 5, transaction.Key.ReactionId);
            WriteUInt32Field(writer, 6, transaction.Key.LocalOrdinal);
            WriteByteField(writer, 7, (byte)transaction.Cause);
            WriteUInt32Field(writer, 8, transaction.ReactionId.Value);
            WriteUInt64Field(writer, 9, transaction.ActorId.Value);
            WriteUInt32Field(writer, 10, transaction.TileId.Value);
            WriteInt64Field(writer, 11, transaction.Extent);
            WriteMatterEntries(writer, transaction.MatterEntries);
            WriteEnergyEntries(writer, transaction.EnergyEntries);
        }
    }

    private static void WriteMatterEntries(
        CanonicalBinaryWriter writer,
        ImmutableArray<MatterLedgerEntry> entries)
    {
        var ordered = entries.OrderBy(entry => entry.Account).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].Account == ordered[index].Account)
            {
                throw new InvalidOperationException(
                    "A transaction must consolidate duplicate matter account entries before hashing.");
            }
        }

        Field(writer, 12);
        writer.WriteUInt32(checked((uint)ordered.Length));
        foreach (var entry in ordered)
        {
            writer.WriteUInt32(MatterEntryRecord);
            WriteByteField(writer, 1, (byte)entry.Account.OwnerKind);
            WriteUInt64Field(writer, 2, entry.Account.OwnerId);
            WriteByteField(writer, 3, (byte)entry.Account.Compartment);
            WriteUInt32Field(writer, 4, entry.Account.ResourceId.Value);
            WriteInt64Field(writer, 5, entry.DeltaQ);
        }
    }

    private static void WriteEnergyEntries(
        CanonicalBinaryWriter writer,
        ImmutableArray<EnergyLedgerEntry> entries)
    {
        var ordered = entries.OrderBy(entry => entry.Account).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].Account == ordered[index].Account)
            {
                throw new InvalidOperationException(
                    "A transaction must consolidate duplicate energy account entries before hashing.");
            }
        }

        Field(writer, 13);
        writer.WriteUInt32(checked((uint)ordered.Length));
        foreach (var entry in ordered)
        {
            writer.WriteUInt32(EnergyEntryRecord);
            WriteByteField(writer, 1, (byte)entry.Account.Kind);
            WriteUInt64Field(writer, 2, entry.Account.OwnerId);
            WriteInt64Field(writer, 3, entry.DeltaQ);
        }
    }

    private static void Field(CanonicalBinaryWriter writer, ushort tag) => writer.WriteUInt16(tag);

    private static void WriteByteField(CanonicalBinaryWriter writer, ushort tag, byte value)
    {
        Field(writer, tag);
        writer.WriteByte(value);
    }

    private static void WriteUInt32Field(CanonicalBinaryWriter writer, ushort tag, uint value)
    {
        Field(writer, tag);
        writer.WriteUInt32(value);
    }

    private static void WriteInt32Field(CanonicalBinaryWriter writer, ushort tag, int value)
    {
        Field(writer, tag);
        writer.WriteInt32(value);
    }

    private static void WriteUInt64Field(CanonicalBinaryWriter writer, ushort tag, ulong value)
    {
        Field(writer, tag);
        writer.WriteUInt64(value);
    }

    private static void WriteInt64Field(CanonicalBinaryWriter writer, ushort tag, long value)
    {
        Field(writer, tag);
        writer.WriteInt64(value);
    }

    private static void WriteStringField(CanonicalBinaryWriter writer, ushort tag, string value)
    {
        Field(writer, tag);
        writer.WriteUtf8Nfc(value);
    }
}

internal readonly record struct WorldSnapshotContent(
    WorldId WorldId,
    ulong CompletedTick,
    ulong WorldRevision,
    ulong SimulatedHours,
    uint TickDurationHours,
    WorldRunnerStatus RunnerStatus,
    RandomCompatibilityState RandomCompatibility);

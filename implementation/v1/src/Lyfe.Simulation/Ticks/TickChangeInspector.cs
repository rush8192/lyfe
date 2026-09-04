using System.Globalization;
using System.Text;
using Lyfe.Simulation.State.Changes;

namespace Lyfe.Simulation.Ticks;

/// <summary>
/// Produces a stable, human-readable diagnostic representation of a completed tick journal.
/// This is an inspection format, not the client synchronization protocol.
/// </summary>
public static class TickChangeInspector
{
    public const string Format = "TickChangeInspectorV1";

    public static string ToCanonicalJson(TickChangeSet changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var builder = new StringBuilder();
        builder.Append("{\"format\":\"");
        builder.Append(Format);
        builder.Append("\",\"completedTick\":");
        AppendQuoted(builder, changes.CompletedTick);
        builder.Append(",\"worldRevision\":");
        AppendQuoted(builder, changes.WorldRevision);
        builder.Append(",\"evaluatedWorkCount\":");
        AppendNumber(builder, changes.EvaluatedWorkCount);
        builder.Append(",\"stores\":[");

        for (var storeIndex = 0; storeIndex < changes.MergedChanges.StoreChanges.Length; storeIndex++)
        {
            if (storeIndex != 0)
            {
                builder.Append(',');
            }

            AppendStore(builder, changes.MergedChanges.StoreChanges[storeIndex]);
        }

        builder.Append("],\"resourceTransactions\":[");
        for (var referenceIndex = 0;
             referenceIndex < changes.MergedChanges.ResourceTransactionReferences.Length;
             referenceIndex++)
        {
            if (referenceIndex != 0)
            {
                builder.Append(',');
            }

            var key = changes.MergedChanges.ResourceTransactionReferences[referenceIndex].Key;
            builder.Append("{\"tick\":");
            AppendQuoted(builder, key.Tick);
            builder.Append(",\"phase\":");
            AppendNumber(builder, (byte)key.Phase);
            builder.Append(",\"scopeId\":");
            AppendQuoted(builder, key.ScopeId);
            builder.Append(",\"actorId\":");
            AppendQuoted(builder, key.ActorId);
            builder.Append(",\"reactionId\":");
            AppendNumber(builder, key.ReactionId);
            builder.Append(",\"localOrdinal\":");
            AppendNumber(builder, key.LocalOrdinal);
            builder.Append('}');
        }

        builder.Append("]}");
        return builder.ToString();
    }

    private static void AppendStore(StringBuilder builder, StoreChangeSet store)
    {
        builder.Append("{\"entityKind\":");
        AppendNumber(builder, (byte)store.EntityKind);
        builder.Append(",\"creates\":");
        AppendEntityIds(builder, store.Creates);
        builder.Append(",\"removes\":");
        AppendEntityIds(builder, store.Removes);
        builder.Append(",\"relocations\":[");
        for (var index = 0; index < store.Relocations.Length; index++)
        {
            if (index != 0)
            {
                builder.Append(',');
            }

            var relocation = store.Relocations[index];
            builder.Append("{\"organismId\":");
            AppendQuoted(builder, relocation.OrganismId.Value);
            builder.Append(",\"sourceTileId\":");
            AppendQuoted(builder, relocation.SourceTileId.Value);
            builder.Append(",\"destinationTileId\":");
            AppendQuoted(builder, relocation.DestinationTileId.Value);
            builder.Append('}');
        }

        builder.Append("],\"dirty\":[");
        for (var index = 0; index < store.DirtyFieldGroups.Length; index++)
        {
            if (index != 0)
            {
                builder.Append(',');
            }

            var group = store.DirtyFieldGroups[index];
            builder.Append("{\"fieldGroup\":");
            AppendNumber(builder, (byte)group.FieldGroup);
            builder.Append(",\"entityIds\":");
            AppendEntityIds(builder, group.Entities);
            builder.Append('}');
        }

        builder.Append("]}");
    }

    private static void AppendEntityIds(
        StringBuilder builder,
        IReadOnlyList<StateEntityReference> entities)
    {
        builder.Append('[');
        for (var index = 0; index < entities.Count; index++)
        {
            if (index != 0)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, entities[index].Value);
        }

        builder.Append(']');
    }

    private static void AppendQuoted(StringBuilder builder, ulong value)
    {
        builder.Append('"');
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
        builder.Append('"');
    }

    private static void AppendNumber(StringBuilder builder, int value) =>
        builder.Append(value.ToString(CultureInfo.InvariantCulture));

    private static void AppendNumber(StringBuilder builder, uint value) =>
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
}

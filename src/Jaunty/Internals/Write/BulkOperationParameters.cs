using System.Data;

namespace Jaunty.Internals.Write;

/// <summary>
/// What a bulk write reports to interceptors and to the logger in place of a parameter set.
/// </summary>
/// <remarks>
/// <para>
/// A bulk operation has no single parameter set - it has N of them, and on the native bulk-copy path
/// it has no <see cref="IDbCommand"/> parameters at all. Handing the entity list over directly is
/// worse than useless: <c>LoggingInterceptor.FormatParameters</c> reflects over the object's own
/// public properties, so a <c>List&lt;T&gt;</c> would log <c>Capacity=…, Count=…</c> and an array
/// would log <c>Length, LongLength, Rank, SyncRoot…</c> - the same meaningless reflection output
/// that batch 1 recorded against the mis-documented "positional parameters" path.
/// </para>
/// <para>
/// So the operation describes itself instead, in properties that format cleanly through the existing
/// interceptors and answer what an auditor actually asks: which operation, over what type, for how
/// many rows. Row-level capture is deliberately not offered - materialising 100,000 entities into a
/// log line is not auditing, and an interceptor that genuinely needs the rows has the connection and
/// the command text to work from.
/// </para>
/// </remarks>
internal sealed class BulkOperationParameters
{
    public BulkOperationParameters(string operation, Type entityType, int rowCount)
        : this(operation, entityType.Name, rowCount)
    {
    }

    public BulkOperationParameters(string operation, string? entityType, int? rowCount)
    {
        Operation = operation;
        EntityType = entityType;
        RowCount = rowCount;
    }

    /// <summary>
    /// The operation that was performed, e.g. <c>"BulkInsert"</c>.
    /// </summary>
    /// <remarks>
    /// AUD-R35-127. This used to read "the public API that was called", which is not what any of the
    /// four families report: <c>BulkInsertAsync</c> passes <c>"BulkInsert"</c>, and so do the update,
    /// delete and <c>ExecuteBatch</c> async twins. That is the right string and the wrong sentence -
    /// an audit trail records which operation touched which rows, and a synchronous and an
    /// asynchronous bulk insert are the same write with the same effect. Splitting them would put
    /// the caller's threading model into the audit record and force every interceptor filtering on
    /// <c>"BulkInsert"</c> to match a second name for no gain. The behaviour is now pinned by
    /// <c>WriteObservabilityTests.EveryBulkFamily_ReportsItsOwnNameTheEntityTypeAndTheRowCount</c>,
    /// so the pair cannot drift apart.
    /// </remarks>
    public string Operation { get; }

    /// <summary>The entity type name, e.g. <c>"Order"</c>, or <see langword="null"/> where the
    /// operation is not entity-typed - <c>ExecuteBatch</c> takes arbitrary caller objects.</summary>
    public string? EntityType { get; }

    /// <summary>
    /// How many rows or parameter sets the call was given, or <see langword="null"/> when that is
    /// not knowable without consuming the caller's sequence. <c>ExecuteBatch</c> accepts a lazy
    /// <see cref="IEnumerable{T}"/>, and draining it early to produce a count for a log line would
    /// change the method's memory behaviour - so it reports what it knows and no more.
    /// </summary>
    public int? RowCount { get; }

    public override string ToString()
    {
        string entity = EntityType is null ? string.Empty : $"<{EntityType}>";
        string count = RowCount is null ? string.Empty : $" x{RowCount.Value}";
        return $"{Operation}{entity}{count}";
    }
}

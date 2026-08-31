using System.Data;

namespace Jaunty.Configuration;

/// <summary>
/// Specifies options for table locking during bulk copy operations.
/// </summary>
public enum TableLockOption
{
    /// <summary>
    /// Uses the database provider's default locking behavior.
    /// </summary>
    Default,

    /// <summary>
    /// Acquires a bulk update lock on the table (SQL Server: TABLOCK).
    /// Improves performance for large bulk operations.
    /// </summary>
    BulkLock,

    /// <summary>
    /// No table lock is acquired.
    /// </summary>
    NoLock
}

/// <summary>
/// Specifies how identity/auto-increment columns are handled during bulk copy.
/// </summary>
public enum BulkCopyIdentityMode
{
    /// <summary>
    /// Uses the database provider's default behavior.
    /// </summary>
    Default,

    /// <summary>
    /// Preserves identity values from the source entities.
    /// Requires appropriate permissions on the target table.
    /// </summary>
    KeepIdentity,

    /// <summary>
    /// Lets the database generate identity values automatically.
    /// Source identity values are ignored.
    /// </summary>
    AutoGenerate
}

/// <summary>
/// Configuration options for bulk copy operations.
/// </summary>
public sealed class BulkCopyOptions
{
    /// <summary>
    /// Gets or sets the number of rows in each batch.
    /// Default is 10,000 rows.
    /// </summary>
    public int BatchSize { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the timeout in seconds for the bulk copy operation. Default is 30 seconds.
    /// </summary>
    /// <remarks>
    /// AUD-R35-105 (round-35 batch 04a). This used to say "Use 0 for no timeout" without
    /// qualification, which is true on exactly one of the three providers.
    /// <list type="bullet">
    /// <item><description><b>SQL Server:</b> assigned unconditionally to
    /// <c>SqlBulkCopy.BulkCopyTimeout</c>, where 0 does mean no timeout.</description></item>
    /// <item><description><b>MySQL:</b> assigned when non-negative, so 0 means no timeout here too.
    /// It guarded on <c>&gt; 0</c> until AUD-R35-067, which is what made the old sentence wrong on
    /// this provider; only a negative - which <c>CommandTimeout</c> rejects outright - is
    /// skipped.</description></item>
    /// <item><description><b>PostgreSQL:</b> not read at all; the binary COPY writer takes its
    /// timeout from the connection string.</description></item>
    /// </list>
    /// </remarks>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Gets or sets how identity columns are handled.
    /// Default is <see cref="BulkCopyIdentityMode.Default"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>AUD-R26-061: currently has no effect on any provider, including SQL Server.</b> Jaunty's
    /// bulk-insert path streams entities through <c>EntityDataReader&lt;T&gt;</c>, which exposes
    /// <c>EntityMetadata.InsertColumns</c> - built as "not identity and not computed". The identity
    /// value is therefore never in the data stream, and no provider-side flag can preserve a value
    /// it was never sent.
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>SQL Server</b> - <see cref="BulkCopyIdentityMode.KeepIdentity"/> is mapped onto
    /// <c>SqlBulkCopyOptions.KeepIdentity</c>, but that flag only says "do not reseed for identity
    /// values in the incoming data", and <c>ApplyColumnMappings</c> maps only the reader's columns -
    /// which exclude the identity column. The server generates identity values exactly as it would
    /// have without the flag.
    /// </description></item>
    /// <item><description>
    /// <b>PostgreSQL and MySQL</b> - not read at all. Same net result, reached one step earlier.
    /// </description></item>
    /// </list>
    /// <para>
    /// Making <see cref="BulkCopyIdentityMode.KeepIdentity"/> work means changing which columns
    /// <c>EntityDataReader</c> streams - which changes the SQL every provider emits and needs
    /// <c>SET IDENTITY_INSERT</c> on SQL Server, <c>OVERRIDING SYSTEM VALUE</c> on PostgreSQL for
    /// <c>GENERATED ALWAYS</c> columns, and the caller to hold the corresponding permissions. That
    /// is a feature rather than a wiring fix, so the property is documented as inert rather than
    /// quietly widened. Pinned by <c>BulkCopyIdentityModeTests</c>.
    /// </para>
    /// </remarks>
    public BulkCopyIdentityMode IdentityMode { get; set; } = BulkCopyIdentityMode.Default;

    /// <summary>
    /// Gets or sets whether to check constraints during bulk copy.
    /// Default is <see langword="true"/> - constraints are enforced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26. This defaulted to <see langword="false"/> alongside
    /// <c>BulkCopyConfiguration.DefaultCheckConstraints</c>. Jaunty's own callers always assign it
    /// explicitly, so flipping the configuration default was enough to fix the reported defect - but
    /// leaving this one <see langword="false"/> would mean a <see cref="BulkCopyOptions"/> built
    /// directly, by a custom <c>IBulkCopyProvider</c> or a future call site, silently opted out of
    /// validation again. The unsafe value should not be the one you get by saying nothing.
    /// </para>
    /// <para>
    /// AUD-R26-061: honored on SQL Server only, where it maps onto
    /// <c>SqlBulkCopyOptions.CheckConstraints</c>. PostgreSQL and MySQL do not read it and cannot:
    /// <c>NpgsqlBinaryImporter</c> exposes no per-import constraint control, and the MySQL provider
    /// issues ordinary multi-row INSERTs, which always enforce constraints - so on those two
    /// providers the behaviour is permanently that of <see langword="true"/> and
    /// <see langword="false"/> is not expressible. Setting it false therefore weakens validation on
    /// SQL Server alone.
    /// </para>
    /// </remarks>
    public bool CheckConstraints { get; set; } = true;

    /// <summary>
    /// Gets or sets the table locking option.
    /// Default is <see cref="TableLockOption.Default"/>.
    /// </summary>
    /// <remarks>
    /// AUD-R26-061: honored on SQL Server only, where
    /// <see cref="TableLockOption.BulkLock"/> maps onto <c>SqlBulkCopyOptions.TableLock</c>.
    /// PostgreSQL and MySQL do not read it - neither <c>NpgsqlBinaryImporter</c> nor a multi-row
    /// INSERT takes a table-level lock hint, and acquiring one would mean issuing a separate
    /// <c>LOCK TABLE</c>/<c>LOCK TABLES</c> statement with different transactional semantics than
    /// the flag implies. Left unread rather than approximated.
    /// </remarks>
    public TableLockOption TableLock { get; set; } = TableLockOption.Default;

    /// <summary>
    /// Gets or sets whether to enable streaming mode for large datasets.
    /// When enabled, rows are streamed to the database without buffering.
    /// Default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Honored per provider, because the underlying bulk APIs differ in whether buffering is even
    /// expressible:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>SQL Server</b> - mapped directly onto <c>SqlBulkCopy.EnableStreaming</c>, so both values
    /// take effect.
    /// </description></item>
    /// <item><description>
    /// <b>PostgreSQL</b> - always streams. The binary COPY protocol behind
    /// <c>NpgsqlBinaryImporter</c> has no buffered mode, so <see langword="false"/> cannot be
    /// honored and is ignored. This is the same constraint the provider already documents for
    /// timeout, batch size, check-constraints and table-lock.
    /// </description></item>
    /// <item><description>
    /// <b>MySQL</b> - always buffers one chunk at a time. Rows are accumulated into a chunk of at
    /// most <see cref="BatchSize"/> rows and sent as a single multi-row INSERT, so
    /// <see langword="true"/> does not eliminate buffering; it bounds it. Use
    /// <see cref="BatchSize"/> to control how much is held at once.
    /// </description></item>
    /// </list>
    /// <para>
    /// AUD-R25: this was public, defaulted to true and documented as above, but no provider read
    /// it - setting it to <see langword="false"/> changed nothing anywhere. SQL Server now wires
    /// it; the other two document what they actually do rather than claim a knob they cannot offer.
    /// </para>
    /// </remarks>
    public bool EnableStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets the transaction to use for the bulk copy operation.
    /// </summary>
    /// <remarks>
    /// AUD-R35-051: this used to say "if null, a new transaction will be created", which is true
    /// on exactly one of the three providers. Every other property on this type carries a
    /// per-provider list (AUD-R25-024 / AUD-R26-061); this was the one still making an
    /// unqualified promise, and the one where the promise is about atomicity.
    /// <list type="bullet">
    /// <item><description>
    /// <strong>MySQL</strong>: a transaction is begun when this is null, so the copy is
    /// all-or-nothing.
    /// </description></item>
    /// <item><description>
    /// <strong>SQL Server</strong>: the value is passed straight to <c>SqlBulkCopy</c>, null
    /// included, and <c>SqlBulkCopyOptions.UseInternalTransaction</c> is not set - so a null
    /// transaction means each batch commits on its own and a mid-copy failure leaves the rows
    /// already written in place. Pass a transaction to make the copy atomic.
    /// </description></item>
    /// <item><description>
    /// <strong>PostgreSQL</strong>: the value is validated against the connection and nothing is
    /// begun. Same consequence as SQL Server for a null.
    /// </description></item>
    /// </list>
    /// </remarks>
    public IDbTransaction? Transaction { get; set; }
}
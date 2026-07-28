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
    /// Gets or sets the timeout in seconds for the bulk copy operation.
    /// Default is 30 seconds. Use 0 for no timeout.
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Gets or sets how identity columns are handled.
    /// Default is <see cref="BulkCopyIdentityMode.Default"/>.
    /// </summary>
    public BulkCopyIdentityMode IdentityMode { get; set; } = BulkCopyIdentityMode.Default;

    /// <summary>
    /// Gets or sets whether to check constraints during bulk copy.
    /// Default is false (constraints are not checked).
    /// </summary>
    public bool CheckConstraints { get; set; } = false;

    /// <summary>
    /// Gets or sets the table locking option.
    /// Default is <see cref="TableLockOption.Default"/>.
    /// </summary>
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
    /// If null, a new transaction will be created.
    /// </summary>
    public IDbTransaction? Transaction { get; set; }
}
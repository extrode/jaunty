namespace Jaunty.Configuration;

/// <summary>
/// Global configuration for bulk copy operations.
/// Configure these settings at application startup before executing any bulk operations.
/// </summary>
public static class BulkCopyConfiguration
{
    /// <summary>
    /// Gets or sets the default batch size for bulk copy operations.
    /// Default is 10,000 rows.
    /// </summary>
    public static int DefaultBatchSize { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the default timeout in seconds for bulk copy operations.
    /// Default is 30 seconds. Use 0 for no timeout.
    /// </summary>
    public static int DefaultTimeout { get; set; } = 30;

    /// <summary>
    /// Gets or sets the default identity mode for bulk copy operations.
    /// Default is <see cref="BulkCopyIdentityMode.Default"/>.
    /// </summary>
    public static BulkCopyIdentityMode DefaultIdentityMode { get; set; } = BulkCopyIdentityMode.Default;

    /// <summary>
    /// Gets or sets whether to check constraints by default during bulk copy.
    /// Default is <see langword="true"/> - constraints are enforced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26 (batch 6, medium/security). This defaulted to <see langword="false"/>, which made a
    /// plain <c>BulkInsert&lt;T&gt;</c> silently skip CHECK and FOREIGN KEY validation - but only on
    /// SQL Server, and only above <see cref="MinimumRowsForNativeBulkCopy"/>. Every other route
    /// validated: below the threshold the call uses multi-row INSERT, PostgreSQL uses <c>COPY</c>,
    /// MySQL's native provider is a chunked INSERT, and SQLite has no native provider. Measured:
    /// the same call with a CHECK-violating row threw at 50 rows and succeeded at 200.
    /// </para>
    /// <para>
    /// A caller who wants the bypass has always had <c>BulkInsertIgnoreConstraints</c>, whose name
    /// says so and whose remarks disclose it. Plain <c>BulkInsert</c>'s remarks disclosed only an
    /// identity caveat, so nothing told a reader the two APIs differed on validation - which is the
    /// entire distinction their names draw.
    /// </para>
    /// <para>
    /// <b>This is a behaviour change.</b> Bulk inserts on SQL Server above the threshold now enforce
    /// constraints, which is slower and which will surface violations that previously landed in the
    /// table unreported. Setting this back to <see langword="false"/> restores the old behaviour
    /// globally; per-call, use <c>BulkInsertIgnoreConstraints</c>.
    /// </para>
    /// </remarks>
    public static bool DefaultCheckConstraints { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum number of rows required to use native bulk copy.
    /// Below this threshold, the standard multi-row INSERT is used instead.
    /// Default is 100 rows.
    /// </summary>
    public static int MinimumRowsForNativeBulkCopy { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether native bulk copy is enabled.
    /// Set to false to force use of standard INSERT statements for all bulk operations.
    /// Default is true.
    /// </summary>
    public static bool EnableNativeBulkCopy { get; set; } = true;

    /// <summary>
    /// Resets all configuration values to their defaults.
    /// </summary>
    public static void Reset()
    {
        DefaultBatchSize = 10000;
        DefaultTimeout = 30;
        DefaultIdentityMode = BulkCopyIdentityMode.Default;
        DefaultCheckConstraints = true;
        MinimumRowsForNativeBulkCopy = 100;
        EnableNativeBulkCopy = true;
    }
}
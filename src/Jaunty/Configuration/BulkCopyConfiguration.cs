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
    /// Default is false (constraints are not checked).
    /// </summary>
    public static bool DefaultCheckConstraints { get; set; } = false;

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
        DefaultCheckConstraints = false;
        MinimumRowsForNativeBulkCopy = 100;
        EnableNativeBulkCopy = true;
    }
}

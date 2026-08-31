namespace Jaunty.Configuration;

/// <summary>
/// Global configuration for bulk copy operations.
/// Configure these settings at application startup before executing any bulk operations.
/// </summary>
public static class BulkCopyConfiguration
{
    private static volatile int _defaultBatchSize = 10000;
    private static volatile int _defaultTimeout = 30;
    private static volatile int _minimumRowsForNativeBulkCopy = 100;
    private static volatile bool _defaultCheckConstraints = true;
    private static volatile bool _enableNativeBulkCopy = true;

    // BulkCopyIdentityMode cannot be volatile - the CLR permits the modifier on an enum field only
    // when its underlying type is one of the volatile-legal primitives named by reference, and an
    // enum is not among them. Reads and writes of an int-backed enum are atomic regardless; what
    // volatile adds elsewhere here is ordering against the neighbouring fields, which no consumer
    // depends on: every read site takes one value at a time to seed a BulkCopyOptions.
    private static BulkCopyIdentityMode _defaultIdentityMode = BulkCopyIdentityMode.Default;

    /// <summary>
    /// Gets or sets the default batch size for bulk copy operations.
    /// Default is 10,000 rows.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not greater than zero.</exception>
    /// <remarks>
    /// AUD-R35-144. None of these settings validated, and a zero or negative batch size was copied
    /// straight into <c>BulkCopyOptions.BatchSize</c> and handed to the provider, which threw its own
    /// message from inside a bulk write - a stack with nothing in it pointing back at the startup
    /// line that set the value. The fields were also plain statics where <c>JauntyConfig</c>
    /// deliberately uses <c>volatile</c>, so a value written on a startup thread was not guaranteed
    /// to be seen by a worker thread that had already read it.
    /// <para>
    /// These throw where <c>JauntyConfig</c>'s capacities clamp, and the difference is deliberate:
    /// <c>ParameterParsingCapacity</c> and its siblings are pre-sizing hints, where a nonsensical
    /// value costs a reallocation and nothing else, so silently using the default is a fair reading
    /// of the intent. A batch size, a timeout and a native-copy threshold change what the database
    /// is asked to do, and quietly substituting a different one for the one that was asked for is
    /// how a misconfiguration survives to production looking like it took effect.
    /// </para>
    /// </remarks>
    public static int DefaultBatchSize
    {
        get => _defaultBatchSize;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The default batch size must be greater than zero.");

            _defaultBatchSize = value;
        }
    }

    /// <summary>
    /// Gets or sets the default timeout in seconds for bulk copy operations.
    /// Default is 30 seconds. Use 0 for no timeout.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <remarks>AUD-R35-144; see <see cref="DefaultBatchSize"/>. Zero is valid and means no timeout.</remarks>
    public static int DefaultTimeout
    {
        get => _defaultTimeout;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The default timeout cannot be negative. Use 0 for no timeout.");

            _defaultTimeout = value;
        }
    }

    /// <summary>
    /// Gets or sets the default identity mode for bulk copy operations.
    /// Default is <see cref="BulkCopyIdentityMode.Default"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not a defined <see cref="BulkCopyIdentityMode"/> member.
    /// </exception>
    /// <remarks>AUD-R35-144; see <see cref="DefaultBatchSize"/>.</remarks>
    public static BulkCopyIdentityMode DefaultIdentityMode
    {
        get => _defaultIdentityMode;
        set
        {
            if (value is not (BulkCopyIdentityMode.Default or BulkCopyIdentityMode.KeepIdentity or BulkCopyIdentityMode.AutoGenerate))
                throw new ArgumentOutOfRangeException(nameof(value), value, "The default identity mode must be a defined BulkCopyIdentityMode member.");

            _defaultIdentityMode = value;
        }
    }

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
    public static bool DefaultCheckConstraints
    {
        get => _defaultCheckConstraints;
        set => _defaultCheckConstraints = value;
    }

    /// <summary>
    /// Gets or sets the minimum number of rows required to use native bulk copy.
    /// Below this threshold, the standard multi-row INSERT is used instead.
    /// Default is 100 rows.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <remarks>
    /// AUD-R35-144; see <see cref="DefaultBatchSize"/>. Zero is valid and means every bulk write
    /// takes the native path where the provider has one.
    /// </remarks>
    public static int MinimumRowsForNativeBulkCopy
    {
        get => _minimumRowsForNativeBulkCopy;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The minimum row count for native bulk copy cannot be negative.");

            _minimumRowsForNativeBulkCopy = value;
        }
    }

    /// <summary>
    /// Gets or sets whether native bulk copy is enabled.
    /// Set to false to force use of standard INSERT statements for all bulk operations.
    /// Default is true.
    /// </summary>
    public static bool EnableNativeBulkCopy
    {
        get => _enableNativeBulkCopy;
        set => _enableNativeBulkCopy = value;
    }

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
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.Import;

/// <summary>
/// Options for controlling how data is imported from a flat file into a target database.
/// </summary>
public readonly struct ImportOptions
{
    // Backing field is nullable so that default(ImportOptions) — which bypasses the constructor
    // and zero-initializes all value-type fields — can still be distinguished from an explicit
    // batchSize: 0. BatchSize below falls back to 1000 only when this is null (unset).
    private readonly int? _batchSize;

    /// <summary>
    /// Gets the number of rows per batch during import. Default: 1000.
    /// </summary>
    /// <remarks>
    /// Falls back to 1000 even for <c>default(ImportOptions)</c>, since a struct's parameterless
    /// default-initialization bypasses the constructor and zero-initializes value-type fields.
    /// </remarks>
    public int BatchSize => _batchSize ?? 1000;

    /// <summary>
    /// Gets or sets how primary key conflicts are handled. Default: <see cref="ConflictStrategy.Error"/>.
    /// </summary>
    public readonly ConflictStrategy OnConflict;

    /// <summary>
    /// Gets or sets whether to create the target table if it does not exist.
    /// When true, the table is created from the entity's property-to-column mappings before import.
    /// Default: false.
    /// </summary>
    public readonly bool CreateTableIfMissing;

    /// <summary>
    /// Gets or sets an optional progress callback invoked after each batch.
    /// Parameters: (rowsImportedSoFar, totalRowsOrNull).
    /// </summary>
    public readonly Action<long, long?>? OnProgress;

    /// <summary>
    /// Gets or sets a custom import dialect for generating database-specific DDL and INSERT SQL.
    /// When null, the import pipeline auto-detects the target database from the connection type.
    /// Set this when importing into a database engine that is not natively supported.
    /// </summary>
    public readonly IImportDialect? Dialect;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportOptions"/> struct.
    /// </summary>
    /// <param name="batchSize">The number of rows per batch during import. Default: 1000.</param>
    /// <param name="onConflict">How primary key conflicts are handled. Default: <see cref="ConflictStrategy.Error"/>.</param>
    /// <param name="createTableIfMissing">Whether to create the target table if it does not exist.</param>
    /// <param name="onProgress">An optional progress callback invoked after each batch.</param>
    /// <param name="dialect">An optional custom import dialect for database-specific SQL generation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="batchSize"/> is not positive.
    /// </exception>
    public ImportOptions(int batchSize = 1000, ConflictStrategy onConflict = ConflictStrategy.Error, bool createTableIfMissing = false, Action<long, long?>? onProgress = null, IImportDialect? dialect = null)
    {
        // AUD-R26-064: reject a non-positive batch size here, where "unset" and "explicitly zero"
        // are still distinguishable. Both import loops compare `>= batchSize`, so a 0 or negative
        // value flushed after every single row - silently turning the batched import into a
        // row-at-a-time one and firing the progress callback per row. The DbBatch path exists
        // specifically to avoid that round-trip pattern, so the option quietly defeated the
        // optimisation it configures. `default(ImportOptions)` bypasses this constructor entirely
        // and still falls back to 1000, which is the behaviour the nullable backing field is for.
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be greater than zero.");

        _batchSize = batchSize;
        OnConflict = onConflict;
        CreateTableIfMissing = createTableIfMissing;
        OnProgress = onProgress;
        Dialect = dialect;
    }
}
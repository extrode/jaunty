namespace Jaunty.FlatFiles;

/// <summary>
/// Options for controlling how data is imported from a flat file into a target database.
/// </summary>
public readonly struct ImportOptions
{
    /// <summary>
    /// Gets or sets the number of rows per batch during import. Default: 1000.
    /// </summary>
    public readonly int BatchSize;

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

    public ImportOptions(int batchSize = 1000, ConflictStrategy onConflict = ConflictStrategy.Error, bool createTableIfMissing = false, Action<long, long?>? onProgress = null, IImportDialect? dialect = null)
    {
        BatchSize = batchSize;
        OnConflict = onConflict;
        CreateTableIfMissing = createTableIfMissing;
        OnProgress = onProgress;
        Dialect = dialect;
    }
}

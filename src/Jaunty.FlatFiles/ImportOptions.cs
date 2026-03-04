namespace Jaunty.FlatFiles;

/// <summary>
/// Options for controlling how data is imported from a flat file into a target database.
/// </summary>
public sealed class ImportOptions
{
    /// <summary>
    /// Gets or sets the number of rows per batch during import. Default: 1000.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets how primary key conflicts are handled. Default: <see cref="ConflictStrategy.Error"/>.
    /// </summary>
    public ConflictStrategy OnConflict { get; set; } = ConflictStrategy.Error;

    /// <summary>
    /// Gets or sets whether to create the target table if it does not exist.
    /// When true, the table is created from the entity's property-to-column mappings before import.
    /// Default: false.
    /// </summary>
    public bool CreateTableIfMissing { get; set; }

    /// <summary>
    /// Gets or sets an optional progress callback invoked after each batch.
    /// Parameters: (rowsImportedSoFar, totalRowsOrNull).
    /// </summary>
    public Action<long, long?>? OnProgress { get; set; }

    /// <summary>
    /// Gets or sets a custom import dialect for generating database-specific DDL and INSERT SQL.
    /// When null, the import pipeline auto-detects the target database from the connection type.
    /// Set this when importing into a database engine that is not natively supported.
    /// </summary>
    public IImportDialect? Dialect { get; set; }
}

namespace Jaunty.FlatFiles;

/// <summary>
/// Represents a TSV (tab-separated values) file data source.
/// </summary>
public sealed class TsvFileSource : IFileSource
{
    /// <inheritdoc />
    public string TableName { get; }

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public FileFormat Format => FileFormat.Tsv;

    /// <inheritdoc />
    public Type EntityType { get; }

    /// <inheritdoc />
    public bool IsPromotedToTable { get; set; }

    /// <inheritdoc />
    public bool IsPreloaded { get; set; }

    /// <summary>
    /// Gets or sets whether the TSV file has a header row.
    /// Null means auto-detect. Default: null.
    /// </summary>
    public bool? HasHeader { get; set; }

    /// <summary>
    /// Gets or sets the string that represents NULL values.
    /// </summary>
    public string? NullString { get; set; }

    /// <summary>
    /// Gets or sets the number of rows to skip at the beginning of the file.
    /// </summary>
    public int SkipRows { get; set; }

    /// <summary>
    /// Creates a new TSV file source.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePath">The path to the TSV file.</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public TsvFileSource(string tableName, string filePath, Type entityType)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
    }
}

namespace Jaunty.FlatFiles;

/// <summary>
/// Represents an Apache Iceberg table source backed by DuckDB's <c>iceberg_scan</c> function.
/// Requires the DuckDB <c>iceberg</c> extension to be installed and loaded.
/// </summary>
public sealed class IcebergFileSource : IFileSource
{
    /// <inheritdoc />
    public string TableName { get; }

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public string Format => FileFormats.Iceberg;

    /// <inheritdoc />
    public Type EntityType { get; }

    /// <inheritdoc />
    public bool IsPromotedToTable { get; set; }

    /// <inheritdoc />
    public bool IsPreloaded { get; set; }

    /// <inheritdoc />
    public string DuckDbFormatName => "ICEBERG";

    /// <summary>
    /// Gets or sets whether to allow moved paths.
    /// When true, DuckDB will try to read files that have been moved from their original location.
    /// Default: false.
    /// </summary>
    public bool AllowMovedPaths { get; set; }

    /// <summary>
    /// Creates a new Iceberg file source.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePath">The path to the Iceberg table directory or metadata file.</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public IcebergFileSource(string tableName, string filePath, Type entityType)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
    }

    /// <inheritdoc />
    public string GenerateReadFunction(string escapedFilePath)
    {
        if (AllowMovedPaths)
            return $"iceberg_scan('{escapedFilePath}', allow_moved_paths = true)";

        return $"iceberg_scan('{escapedFilePath}')";
    }

    /// <inheritdoc />
    public string? GenerateCopyToOptions() => null;
}

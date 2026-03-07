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
    public IReadOnlyList<string> FilePaths { get; }

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
        : this(tableName, [filePath], entityType)
    {
    }

    /// <summary>
    /// Creates a new Iceberg file source from multiple paths.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePaths">The paths to the Iceberg table directories or metadata files.</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public IcebergFileSource(string tableName, string[] filePaths, Type entityType)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        if (filePaths is null) throw new ArgumentNullException(nameof(filePaths));
        if (filePaths.Length == 0) throw new ArgumentException("At least one file path is required.", nameof(filePaths));
        if (filePaths[0] is null) throw new ArgumentNullException(nameof(filePaths), "File path must not be null.");
        FilePaths = filePaths;
        FilePath = filePaths[0];
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
    }

    /// <inheritdoc />
    public string GenerateReadFunction(string pathExpression)
    {
        if (AllowMovedPaths)
            return $"iceberg_scan({pathExpression}, allow_moved_paths = true)";

        return $"iceberg_scan({pathExpression})";
    }

    /// <inheritdoc />
    public string? GenerateCopyToOptions() => null;
}

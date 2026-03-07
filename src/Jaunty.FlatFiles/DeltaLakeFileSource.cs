namespace Jaunty.FlatFiles;

/// <summary>
/// Represents a Delta Lake table source backed by DuckDB's <c>delta_scan</c> function.
/// Requires the DuckDB <c>delta</c> extension to be installed and loaded.
/// </summary>
public sealed class DeltaLakeFileSource : IFileSource
{
    /// <inheritdoc />
    public string TableName { get; }

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public string Format => FileFormats.DeltaLake;

    /// <inheritdoc />
    public Type EntityType { get; }

    /// <inheritdoc />
    public bool IsPromotedToTable { get; set; }

    /// <inheritdoc />
    public bool IsPreloaded { get; set; }

    /// <inheritdoc />
    public string DuckDbFormatName => "DELTA";

    /// <summary>
    /// Creates a new Delta Lake file source.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePath">The path to the Delta Lake table directory.</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public DeltaLakeFileSource(string tableName, string filePath, Type entityType)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
    }

    /// <inheritdoc />
    public string GenerateReadFunction(string escapedFilePath)
        => $"delta_scan('{escapedFilePath}')";

    /// <inheritdoc />
    public string? GenerateCopyToOptions() => null;
}

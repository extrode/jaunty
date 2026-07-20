using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.FileSources;

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
    public IReadOnlyList<string> FilePaths { get; }

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
        : this(tableName, [filePath], entityType)
    {
    }

    /// <summary>
    /// Creates a new Delta Lake file source from multiple paths.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePaths">The paths to the Delta Lake table directories. Must contain exactly one path;
    /// DuckDB's <c>delta_scan</c> function only accepts a single table location, unlike <c>read_csv</c>/
    /// <c>read_parquet</c>/<c>read_json</c>, which accept a list.</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public DeltaLakeFileSource(string tableName, string[] filePaths, Type entityType)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        if (filePaths is null) throw new ArgumentNullException(nameof(filePaths));
        if (filePaths.Length == 0) throw new ArgumentException("At least one file path is required.", nameof(filePaths));
        if (filePaths.Length > 1) throw new ArgumentException("DeltaLakeFileSource does not support multiple paths: DuckDB's delta_scan function only accepts a single table location.", nameof(filePaths));
        if (filePaths[0] is null) throw new ArgumentNullException(nameof(filePaths), "File path must not be null.");
        FilePaths = filePaths;
        FilePath = filePaths[0];
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
    }

    /// <inheritdoc />
    public string GenerateReadFunction(string pathExpression)
        => $"delta_scan({pathExpression})";

    /// <inheritdoc />
    public string? GenerateCopyToOptions() => null;
}
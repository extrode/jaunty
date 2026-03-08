using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.FileSources;

/// <summary>
/// Represents a Parquet file data source.
/// </summary>
public sealed class ParquetFileSource : IFileSource
{
    /// <inheritdoc />
    public string TableName { get; }

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> FilePaths { get; }

    /// <inheritdoc />
    public string Format => FileFormats.Parquet;

    /// <inheritdoc />
    public Type EntityType { get; }

    /// <inheritdoc />
    public bool IsPromotedToTable { get; set; }

    /// <inheritdoc />
    public bool IsPreloaded { get; set; }

    /// <inheritdoc />
    public string DuckDbFormatName => "PARQUET";

    /// <summary>
    /// Gets or sets whether to enable Hive partitioning.
    /// Default: false.
    /// </summary>
    public bool HivePartitioning { get; set; }

    /// <summary>
    /// Creates a new Parquet file source.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePath">The path to the Parquet file.</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public ParquetFileSource(string tableName, string filePath, Type entityType)
        : this(tableName, [filePath], entityType)
    {
    }

    /// <summary>
    /// Creates a new Parquet file source from multiple files.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePaths">The paths to the Parquet files (local, glob, or remote).</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public ParquetFileSource(string tableName, string[] filePaths, Type entityType)
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
        var sb = new System.Text.StringBuilder();
        sb.Append($"read_parquet({pathExpression}");

        if (HivePartitioning)
            sb.Append(", hive_partitioning = true");

        sb.Append(')');
        return sb.ToString();
    }

    /// <inheritdoc />
    public string? GenerateCopyToOptions() => null;
}
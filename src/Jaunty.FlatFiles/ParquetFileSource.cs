namespace Jaunty.FlatFiles;

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
    public FileFormat Format => FileFormat.Parquet;

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
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
    }

    /// <inheritdoc />
    public string GenerateReadFunction(string escapedFilePath)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"read_parquet('{escapedFilePath}'");

        if (HivePartitioning)
            sb.Append(", hive_partitioning = true");

        sb.Append(')');
        return sb.ToString();
    }

    /// <inheritdoc />
    public string? GenerateCopyToOptions() => null;
}

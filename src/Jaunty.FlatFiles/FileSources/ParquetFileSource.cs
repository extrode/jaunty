using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.Interfaces;

using Jaunty.FlatFiles.Internals;

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
    /// Gets or sets whether to enable Hive partitioning <b>on the read path only</b>.
    /// Default: false.
    /// </summary>
    /// <remarks>
    /// AUD-R35-237. This is not symmetric with the write path and cannot be made so from here:
    /// <c>read_parquet(..., hive_partitioning = true)</c> infers the partition columns from the
    /// directory names it finds, but DuckDB's <c>COPY ... TO ... (FORMAT PARQUET, PARTITION_BY
    /// (col, ...))</c> has to be told which columns to partition on, and <see cref="IFileSource"/>
    /// carries no such list. So exporting a hive-partitioned source through <c>Save</c>/<c>Export</c>
    /// writes one flat file, and reading that file back with this flag still set will not
    /// reconstruct the partition columns. Documented rather than fixed: emitting a guessed
    /// <c>PARTITION_BY</c> would be a new feature with a new option, not a correction.
    /// </remarks>
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
        FilePathValidator.ThrowIfInvalid(filePaths, nameof(filePaths));
        FilePaths = FilePathValidator.Snapshot(filePaths);
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
    /// <remarks>
    /// Nothing to emit. <c>FORMAT PARQUET</c> is supplied by the dialect and the only read-side
    /// option this source carries is <see cref="HivePartitioning"/>, which has no writable
    /// counterpart here - see its remarks.
    /// </remarks>
    public string? GenerateCopyToOptions() => null;
}
using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.FileSources;

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
    public IReadOnlyList<string> FilePaths { get; }

    /// <inheritdoc />
    public string Format => FileFormats.Tsv;

    /// <inheritdoc />
    public Type EntityType { get; }

    /// <inheritdoc />
    public bool IsPromotedToTable { get; set; }

    /// <inheritdoc />
    public bool IsPreloaded { get; set; }

    /// <inheritdoc />
    public string DuckDbFormatName => "CSV";

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
        : this(tableName, [filePath], entityType)
    {
    }

    /// <summary>
    /// Creates a new TSV file source from multiple files.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePaths">The paths to the TSV files (local, glob, or remote).</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public TsvFileSource(string tableName, string[] filePaths, Type entityType)
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
        sb.Append($"read_csv({pathExpression}, delim = '\t'");

        if (HasHeader.HasValue)
            sb.Append($", header = {(HasHeader.Value ? "true" : "false")}");

        if (NullString is not null)
            sb.Append($", nullstr = '{NullString.Replace("'", "''")}'");

        if (SkipRows > 0)
            sb.Append($", skip = {SkipRows}");

        sb.Append(", auto_detect = true)");
        return sb.ToString();
    }

    /// <inheritdoc />
    public string? GenerateCopyToOptions() => "DELIMITER '\t', HEADER true";
}
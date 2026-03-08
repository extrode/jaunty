namespace Jaunty.FlatFiles.Interfaces;

/// <summary>
/// Represents a flat file data source that can be registered with an <see cref="IFlatFile"/>.
/// </summary>
public interface IFileSource
{
    /// <summary>
    /// Gets the logical table name used in SQL queries.
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// Gets the primary file path pointing to the data file.
    /// For multi-file sources, this is the first path.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets all file paths for this source. Returns a single-element list for single-file sources.
    /// Supports local paths, glob patterns, and remote URLs.
    /// </summary>
    IReadOnlyList<string> FilePaths { get; }

    /// <summary>
    /// Gets the file format identifier (e.g. <see cref="FileFormats.Csv"/>, <see cref="FileFormats.Parquet"/>).
    /// Custom implementations can return any string value.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Gets the entity type associated with this file source.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Gets or sets whether this source has been promoted from a VIEW to a TABLE (for mutation support).
    /// </summary>
    bool IsPromotedToTable { get; set; }

    /// <summary>
    /// Gets or sets whether this source was preloaded into memory as a TABLE.
    /// </summary>
    bool IsPreloaded { get; set; }

    /// <summary>
    /// Gets the DuckDB format name used in COPY TO statements (e.g. "CSV", "PARQUET", "JSON").
    /// </summary>
    string DuckDbFormatName { get; }

    /// <summary>
    /// Generates the DuckDB read function expression for this file source
    /// (e.g. <c>read_csv('path', header = true, auto_detect = true)</c>).
    /// This allows custom file source implementations to define their own read functions.
    /// </summary>
    /// <param name="pathExpression">A pre-formatted path expression: either <c>'path'</c> for single files
    /// or <c>['path1', 'path2']</c> for multiple files. Already escaped.</param>
    /// <returns>A DuckDB read function expression.</returns>
    string GenerateReadFunction(string pathExpression);

    /// <summary>
    /// Generates any additional COPY TO options specific to this file format
    /// (e.g. <c>DELIMITER '\t', HEADER true</c>). Return null or empty for no additional options.
    /// </summary>
    /// <returns>Additional COPY TO option clauses, or null.</returns>
    string? GenerateCopyToOptions();
}
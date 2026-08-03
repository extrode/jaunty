using Jaunty.FlatFiles.Core;

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
    /// Gets or sets whether this source has been promoted from a VIEW to a TABLE (for mutation
    /// support) on <b>some</b> connection. Advisory only - nothing reads it to make a decision.
    /// </summary>
    /// <remarks>
    /// AUD-R35-240. This was the promotion state until AUD-R26-069: promotion is something one
    /// connection did to one view, but a source instance is owned by the caller and can be
    /// registered into several, so a second connection saw <c>true</c>, skipped promotion, and then
    /// issued an UPDATE or DELETE against a view, which DuckDB rejects. The authority is now a
    /// per-connection table inside <c>TablePromoter</c>, which still sets this flag on the way
    /// through because it is public API and reads usefully as "promoted somewhere". Setting it
    /// yourself has no effect, and reading it does not tell you about any particular connection.
    /// </remarks>
    bool IsPromotedToTable { get; set; }

    /// <summary>
    /// Gets or sets whether this source opts in to being preloaded into memory as a TABLE rather
    /// than registered as a VIEW - the per-source override of
    /// <c>FlatFileOptions.PreloadIntoMemory</c>. Read once, at registration.
    /// </summary>
    /// <remarks>
    /// AUD-R35-240. This is an input, not a state flag. It used to be both: <c>DuckDb</c>'s
    /// constructor wrote <c>true</c> back into it and never cleared it, so the same leak described
    /// on <see cref="IsPromotedToTable"/> applied here too - see AUD-R35-026. Whether a source
    /// actually is preloaded on a given connection is now recorded per connection in
    /// <c>PreloadRegistry</c>; this property is only ever read.
    /// </remarks>
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
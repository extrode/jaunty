using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.Interfaces;

using Jaunty.FlatFiles.Internals;

namespace Jaunty.FlatFiles.FileSources;

/// <summary>
/// Represents a JSON file data source.
/// </summary>
public sealed class JsonFileSource : IFileSource
{
    /// <inheritdoc />
    public string TableName { get; }

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> FilePaths { get; }

    /// <inheritdoc />
    public string Format => FileFormats.Json;

    /// <inheritdoc />
    public Type EntityType { get; }

    /// <inheritdoc />
    public bool IsPromotedToTable { get; set; }

    /// <inheritdoc />
    public bool IsPreloaded { get; set; }

    /// <inheritdoc />
    public string DuckDbFormatName => "JSON";

    /// <summary>
    /// Gets or sets the JSON file format (auto, array, or newline-delimited).
    /// Default: Auto.
    /// </summary>
    public JsonFileFormat JsonFormat { get; set; } = JsonFileFormat.Auto;

    /// <summary>
    /// Gets or sets the maximum JSON nesting depth.
    /// Null means use the default.
    /// </summary>
    public int? MaxDepth { get; set; }

    /// <summary>
    /// Creates a new JSON file source.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePath">The path to the JSON file.</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public JsonFileSource(string tableName, string filePath, Type entityType)
        : this(tableName, [filePath], entityType)
    {
    }

    /// <summary>
    /// Creates a new JSON file source from multiple files.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePaths">The paths to the JSON files (local, glob, or remote).</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public JsonFileSource(string tableName, string[] filePaths, Type entityType)
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        FilePathValidator.ThrowIfInvalid(filePaths, nameof(filePaths));
        FilePaths = filePaths;
        FilePath = filePaths[0];
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
    }

    /// <inheritdoc />
    public string GenerateReadFunction(string pathExpression)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"read_json_auto({pathExpression}");

        if (JsonFormat != JsonFileFormat.Auto)
        {
            var formatValue = JsonFormat switch
            {
                JsonFileFormat.Array => "array",
                JsonFileFormat.NewlineDelimited => "newline_delimited",
                _ => "auto"
            };
            sb.Append($", format = '{formatValue}'");
        }

        if (MaxDepth.HasValue)
            sb.Append($", maximum_depth = {MaxDepth.Value}");

        sb.Append(')');
        return sb.ToString();
    }

    /// <inheritdoc />
    /// <remarks>
    /// AUD-R35-074. <see cref="JsonFormat"/> was honoured on the read path and dropped on the write
    /// path, so a source configured <c>JsonFormat = Array</c> exported a file that this same source
    /// could not read back - DuckDB's <c>COPY ... TO ... (FORMAT JSON)</c> writes one object per
    /// line unless told otherwise, and the next read passed <c>format = 'array'</c> against
    /// newline-delimited content. Same round-trip-loss class as the already-fixed
    /// <c>CsvFileSource</c> HEADER and <c>TsvFileSource</c> NullString items.
    /// <para>
    /// <c>ARRAY true</c>/<c>ARRAY false</c> measured against the pinned DuckDB 1.3.0: the first
    /// writes <c>[\n\t{...}\n]</c>, the second one object per line. <see cref="JsonFileFormat.Auto"/>
    /// emits nothing, because the read side does not constrain the format either.
    /// </para>
    /// </remarks>
    public string? GenerateCopyToOptions() => JsonFormat switch
    {
        JsonFileFormat.Array => "ARRAY true",
        JsonFileFormat.NewlineDelimited => "ARRAY false",
        _ => null
    };
}
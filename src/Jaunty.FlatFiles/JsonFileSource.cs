namespace Jaunty.FlatFiles;

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
    {
        TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
    }

    /// <inheritdoc />
    public string GenerateReadFunction(string escapedFilePath)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"read_json_auto('{escapedFilePath}'");

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
    public string? GenerateCopyToOptions() => null;
}

namespace Jaunty.FlatFiles;

/// <summary>
/// Represents an Excel (.xlsx) file source backed by DuckDB's <c>read_xlsx</c> function.
/// Requires the DuckDB <c>excel</c> extension to be installed and loaded.
/// </summary>
public sealed class ExcelFileSource : IFileSource
{
    /// <inheritdoc />
    public string TableName { get; }

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> FilePaths { get; }

    /// <inheritdoc />
    public string Format => FileFormats.Excel;

    /// <inheritdoc />
    public Type EntityType { get; }

    /// <inheritdoc />
    public bool IsPromotedToTable { get; set; }

    /// <inheritdoc />
    public bool IsPreloaded { get; set; }

    /// <inheritdoc />
    public string DuckDbFormatName => "XLSX";

    /// <summary>
    /// Gets or sets the sheet name to read. When null, reads the first sheet.
    /// </summary>
    public string? SheetName { get; set; }

    /// <summary>
    /// Gets or sets whether the first row contains column headers.
    /// Null means auto-detect. Default: null.
    /// </summary>
    public bool? HasHeader { get; set; }

    /// <summary>
    /// Gets or sets the cell range to read (e.g. "A1:D100").
    /// When null, reads the entire sheet.
    /// </summary>
    public string? Range { get; set; }

    /// <summary>
    /// Creates a new Excel file source.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePath">The path to the Excel (.xlsx) file.</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public ExcelFileSource(string tableName, string filePath, Type entityType)
        : this(tableName, [filePath], entityType)
    {
    }

    /// <summary>
    /// Creates a new Excel file source from multiple files.
    /// </summary>
    /// <param name="tableName">The logical table name for SQL queries.</param>
    /// <param name="filePaths">The paths to the Excel files (local or remote).</param>
    /// <param name="entityType">The entity type to map rows to.</param>
    public ExcelFileSource(string tableName, string[] filePaths, Type entityType)
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
        sb.Append($"read_xlsx({pathExpression}");

        if (SheetName is not null)
            sb.Append($", sheet = '{SheetName.Replace("'", "''")}'");

        if (HasHeader.HasValue)
            sb.Append($", header = {(HasHeader.Value ? "true" : "false")}");

        if (Range is not null)
            sb.Append($", range = '{Range}'");

        sb.Append(')');
        return sb.ToString();
    }

    /// <inheritdoc />
    public string? GenerateCopyToOptions() => null;
}

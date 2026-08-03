using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.Interfaces;

using Jaunty.FlatFiles.Internals;

namespace Jaunty.FlatFiles.FileSources;

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
        FilePathValidator.ThrowIfInvalid(filePaths, nameof(filePaths));
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
            sb.Append($", range = '{Range.Replace("'", "''")}'");

        sb.Append(')');
        return sb.ToString();
    }

    /// <inheritdoc />
    /// <remarks>
    /// AUD-R35-073. <see cref="SheetName"/> and <see cref="HasHeader"/> were honoured on the read
    /// side and dropped on the write side, so a source configured <c>SheetName = "Data"</c> or
    /// <c>HasHeader = false</c> did not round-trip: the written file landed on DuckDB's default
    /// sheet with a header row, and re-reading it through the same source ate the first data row as
    /// column names. Same defect already fixed for the two siblings that carry write-relevant read
    /// options - <c>CsvFileSource</c> (AUD-R21-003) and <c>TsvFileSource</c>.
    /// <para>
    /// <see cref="Range"/> is deliberately not written back: it selects a sub-rectangle of an
    /// existing sheet, which has no meaning for a file being created from scratch.
    /// </para>
    /// <para>
    /// Option spellings measured against the pinned DuckDB 1.3.0 <c>excel</c> extension, not taken
    /// from documentation: <c>SHEET '&lt;name&gt;'</c> is the one that names the sheet.
    /// <c>SHEET_NAME</c> is accepted by the parser and then silently does nothing - the written
    /// file's sheet keeps its default name and a subsequent <c>read_xlsx(..., sheet = '&lt;name&gt;')</c>
    /// fails with "Sheet not found".
    /// </para>
    /// </remarks>
    public string? GenerateCopyToOptions()
    {
        var sb = new System.Text.StringBuilder();

        if (SheetName is not null)
            sb.Append($"SHEET '{SheetName.Replace("'", "''")}'");

        // HasHeader == false means the file genuinely has no header row, so writing one back would
        // produce a file this very source can never read correctly - the same reasoning, and the
        // same null-means-leave-it-alone default, as CsvFileSource.
        if (HasHeader == false)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append("HEADER false");
        }

        return sb.Length == 0 ? null : sb.ToString();
    }
}
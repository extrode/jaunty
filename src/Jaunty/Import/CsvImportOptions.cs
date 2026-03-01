using System.Text;

namespace Jaunty;

/// <summary>
/// Options for CSV import operations.
/// </summary>
public class CsvImportOptions
{
    /// <summary>
    /// Field delimiter character. Default is comma.
    /// </summary>
    public char Delimiter { get; set; } = ',';

    /// <summary>
    /// Whether the first row contains column headers. Default is true.
    /// </summary>
    public bool HasHeader { get; set; } = true;

    /// <summary>
    /// String value to interpret as NULL. Default is null (no null mapping).
    /// </summary>
    public string? NullValue { get; set; }

    /// <summary>
    /// File encoding. Default is UTF-8.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Quote character for fields containing delimiters or newlines. Default is double-quote.
    /// </summary>
    public char Quote { get; set; } = '"';
}

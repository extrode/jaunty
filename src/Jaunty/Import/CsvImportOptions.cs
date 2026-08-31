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
    /// String value to interpret as NULL. Default is <see langword="null"/> (no null mapping).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Honored per import path, because the native import commands differ in whether a NULL
    /// sentinel is expressible at all. Where it is not, setting this throws rather than importing
    /// the sentinel as literal text:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>SQLite, in-memory</b> - honored. Jaunty parses the file and binds the values itself.
    /// </description></item>
    /// <item><description>
    /// <b>PostgreSQL</b> - honored, as <c>COPY ... WITH (NULL '...')</c>.
    /// </description></item>
    /// <item><description>
    /// <b>SQLite, file-backed</b> - not supported. The sqlite3 CLI's <c>.nullvalue</c> affects
    /// output formatting only; <c>.import</c> does not consult it, verified against the CLI
    /// directly (AUD-R11).
    /// </description></item>
    /// <item><description>
    /// <b>MySQL/MariaDB</b> and <b>SQL Server</b> - not supported by <c>LOAD DATA</c> and
    /// <c>BULK INSERT</c> respectively.
    /// </description></item>
    /// </list>
    /// </remarks>
    public string? NullValue { get; set; }

    /// <summary>
    /// File encoding. Default is UTF-8.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Honored only where Jaunty opens the file. Elsewhere the database engine or its driver opens
    /// it and the encoding is theirs to determine, so a non-default value throws rather than being
    /// discarded:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>SQLite, in-memory</b> - honored. The prepared-statement fallback opens the file with
    /// this encoding.
    /// </description></item>
    /// <item><description>
    /// <b>PostgreSQL</b> - honored on the <c>COPY ... FROM STDIN</c> path, which streams the file
    /// from here.
    /// </description></item>
    /// <item><description>
    /// <b>SQLite, file-backed</b> - not supported; the sqlite3 CLI opens the file.
    /// </description></item>
    /// <item><description>
    /// <b>MySQL/MariaDB</b> - not supported; the client driver opens the file for
    /// <c>LOAD DATA LOCAL INFILE</c>. Use the connection string's charset settings.
    /// </description></item>
    /// <item><description>
    /// <b>SQL Server</b> - not supported; the server opens the file for <c>BULK INSERT</c>. Use
    /// <c>CODEPAGE</c> on the server side.
    /// </description></item>
    /// </list>
    /// <para>
    /// AUD-R26 (batch 4, low/consistency). This type exposed five options as if all five applied
    /// everywhere. <see cref="NullValue"/> at least failed loudly on the paths that cannot honor it;
    /// <c>Encoding</c> was discarded in silence, so a caller who set Latin-1 for a Latin-1 file got
    /// UTF-8 behavior and mojibake with nothing to indicate why. It now throws on those paths, which
    /// makes the two behave alike. The default is never rejected. Same shape, and the same fix, as
    /// AUD-R25 on <c>BulkCopyOptions.EnableStreaming</c>.
    /// </para>
    /// </remarks>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Quote character for fields containing delimiters or newlines. Default is double-quote.
    /// </summary>
    public char Quote { get; set; } = '"';
}
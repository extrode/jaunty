using System.IO.Compression;

using DuckDB.NET.Data;

using Extrode.Jaunty.FlatFiles.Core;
using Extrode.Jaunty.FlatFiles.DuckDB.Internals;
using Extrode.Jaunty.FlatFiles.FileSources;
using Extrode.Jaunty.FlatFiles.Interfaces;
using Extrode.Jaunty.FlatFiles.WriteBack;

namespace Extrode.Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public void Save<T>(string outputPath) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        IFileSource source = GetSourceOrThrow<T>();
        var sql = BuildCopyToSql(source, outputPath);

        NonQueryExecutor.Execute(_connection, sql, []);
    }

    /// <inheritdoc />
    public void Save<T>(WriteBackMode mode) where T : class, new()
    {
        IFileSource source = GetSourceOrThrow<T>();

        // AUD-R26-063: reject anything that is not Overwrite, rather than rejecting NewFile and
        // treating everything else as "overwrite the caller's file in place". WriteBackMode has two
        // members today so a correct caller sees no difference, but C# permits an undefined enum
        // value without a cast diagnostic - (WriteBackMode)99, or a value deserialized from
        // configuration - and the old shape let that fall through to the irreversible branch. For an
        // operation whose entire risk is that it destroys the original, the destructive path must be
        // the one you have to ask for by name.
        if (mode != WriteBackMode.Overwrite)
        {
            throw mode == WriteBackMode.NewFile
                ? new InvalidOperationException(
                    "WriteBackMode.NewFile requires an output path. Use the Save<T>(string outputPath) overload instead.")
                : new ArgumentOutOfRangeException(
                    nameof(mode), mode,
                    $"'{mode}' is not a defined WriteBackMode. In-place write-back overwrites the source file " +
                    "irreversibly, so only WriteBackMode.Overwrite is accepted here.");
        }

        RequireInPlaceWriteBackIsLossless(source);

        var originalPath = source.FilePath;
        // Stryker disable once String : the fallback is reached only for a null result (root path), and "" and "." combine to the same path
        var directory = Path.GetDirectoryName(originalPath) ?? ".";

        // AUD-R26-063: unique per call, not derived only from the original name. The old
        // ".{name}.tmp{ext}" was deterministic, so two in-place saves of the same source running
        // concurrently wrote to the same path - the second COPY TO overwrote the first's output
        // before either File.Move ran, and one save silently persisted the other's data. A leftover
        // temp file from a hard exit (cleanup only runs in the catch, so a killed process leaves one
        // behind) was also indistinguishable from the current run's; now it is at least diagnosable.
        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileNameWithoutExtension(originalPath)}.tmp.{Guid.NewGuid():N}{Path.GetExtension(originalPath)}");

        try
        {
            var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(tempPath), source);
            NonQueryExecutor.Execute(_connection, sql, []);

            File.Move(tempPath, originalPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    /// <inheritdoc />
    public void Export<T>(string outputPath) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        IFileSource source = GetSourceOrThrow<T>();
        var sql = BuildCopyToSql(source, outputPath);

        NonQueryExecutor.Execute(_connection, sql, []);
    }

    /// <summary>
    /// Builds the COPY TO statement for writing <paramref name="source"/> out to
    /// <paramref name="outputPath"/>, preferring the source-aware overload when the output format
    /// matches the source's own.
    /// </summary>
    /// <remarks>
    /// The format-string overload knows nothing but the format name, so it emits a bare
    /// <c>FORMAT CSV, HEADER true</c> and drops everything the source was configured with -
    /// DELIMITER, QUOTE, NULL and (since AUD-R25) HEADER. Writing a semicolon-delimited source back
    /// out through it therefore silently produced a comma-delimited file. Same-format writes now go
    /// through the <see cref="IFileSource"/> overload, which is what Save&lt;T&gt;(WriteBackMode)
    /// already used.
    ///
    /// <para>
    /// Cross-format writes still take the format-string overload: a CSV source's DELIMITER/QUOTE
    /// options are not valid COPY arguments for PARQUET or JSON, so carrying them across would turn
    /// the documented "CSV source -&gt; Parquet output" export into a DuckDB binder error.
    /// </para>
    /// </remarks>
    private string BuildCopyToSql(IFileSource source, string outputPath)
    {
        var format = InferFormatFromExtension(outputPath);
        var fullPath = Path.GetFullPath(outputPath);

        RequireNotTheSourceItself(source, fullPath);

        return string.Equals(format, source.Format, StringComparison.OrdinalIgnoreCase)
            ? _dialect.GenerateCopyToSql(source.TableName, fullPath, source)
            : _dialect.GenerateCopyToSql(source.TableName, fullPath, format);
    }

    /// <summary>
    /// Rejects an output path that is one of <paramref name="source"/>'s own files.
    /// </summary>
    /// <remarks>
    /// AUD-R35-034. <c>Save&lt;T&gt;(string)</c> and <c>Export&lt;T&gt;(string)</c> both document
    /// that the original file is not modified - <c>IFlatFile.Save&lt;T&gt;(string)</c> says "Saves
    /// modified data to a new file (non-destructive). The original file is not modified." - and both
    /// took the direct <c>COPY ... TO</c> route whatever path they were handed, so passing the
    /// source's own path truncated it in place. Where the source is still an unmutated VIEW over the
    /// file, the COPY is writing to the very path its scan is reading. That is precisely the hazard
    /// <c>Save&lt;T&gt;(WriteBackMode.Overwrite)</c> spends a temp file and an atomic
    /// <see cref="File.Move(string, string, bool)"/> to avoid, and the same-path case was silent:
    /// no exception, the loss visible only in the file afterwards. AUD-R26-063 hardened the
    /// <c>WriteBackMode</c> path and left this one.
    ///
    /// <para>
    /// Every path is compared fully resolved, and every file of a multi-file source is checked, not
    /// just <see cref="IFileSource.FilePath"/> - a glob or explicit list source has no single
    /// original to overwrite, and clobbering any member of it is the same loss.
    /// </para>
    /// <para>
    /// AUD-R38-003: a glob is stored unexpanded, so <c>part*.csv</c> was compared to
    /// <c>part1.csv</c> as a literal string and never matched. Local globs are now expanded with
    /// DuckDB's own <c>glob()</c>, the same matcher the view reads through.
    /// </para>
    /// </remarks>
    /// <param name="source">The source being written out.</param>
    /// <param name="fullOutputPath">The already-resolved output path.</param>
    /// <exception cref="ArgumentException">The output path is one of the source's own files.</exception>
    private void RequireNotTheSourceItself(IFileSource source, string fullOutputPath)
    {
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (string sourcePath in ExpandLocalGlobs(source.FilePaths))
        {
            string fullSourcePath;

            try
            {
                fullSourcePath = Path.GetFullPath(sourcePath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // A source path that cannot be resolved (a URL scheme such as s3:// among them)
                // cannot collide with a local output path, so it is not this guard's business.
                continue;
            }

            if (!string.Equals(fullSourcePath, fullOutputPath, comparison))
                continue;

            throw new ArgumentException(
                $"'{fullOutputPath}' is the file source '{source.TableName}' reads from, and this " +
                "overload is documented as non-destructive - writing there would truncate the " +
                "original while it is still being read. Write to a different path, or call " +
                "Save<T>(WriteBackMode.Overwrite), which replaces the file atomically through a " +
                "temporary one.",
                "outputPath");
        }
    }

    /// <summary>
    /// True when <paramref name="path"/> holds a DuckDB glob wildcard (<c>*</c>, <c>?</c>, <c>[</c>).
    /// </summary>
    private static bool IsGlobPattern(string path) => path.AsSpan().ContainsAny('*', '?', '[');

    private static bool IsRemote(string path) => path.Contains("://", StringComparison.Ordinal);

    /// <summary>
    /// Yields each path, with every local glob replaced by the files DuckDB's <c>glob()</c> matches.
    /// Remote paths are passed through: they cannot collide with a local output path.
    /// </summary>
    private IEnumerable<string> ExpandLocalGlobs(IReadOnlyList<string> paths)
    {
        foreach (string path in paths)
        {
            if (IsGlobPattern(path) && !IsRemote(path))
            {
                using DuckDBCommand cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT file FROM glob($pattern)";
                cmd.Parameters.Add(new DuckDBParameter("pattern", path));
                using DuckDBDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                    yield return reader.GetString(0);
            }
            else
            {
                yield return path;
            }
        }
    }

    /// <summary>
    /// Rejects an in-place write-back that cannot reproduce the file it replaces.
    /// </summary>
    /// <remarks>
    /// In-place <c>Save</c> rewrites the source file from only the rows and columns the source
    /// reads, then moves the result over the original. Anything in the file the source does not
    /// read is lost:
    /// <list type="bullet">
    /// <item>several files (AUD-R26-063), or one glob pattern matching several (AUD-R38-004): the
    /// glob was a single <see cref="IFileSource.FilePaths"/> entry, so the move targeted a file
    /// literally named <c>part*.csv</c>, and the originals were left beside a new file the glob
    /// also matches;</item>
    /// <item>an Excel workbook's other sheets, or the cells outside <c>Range</c> (AUD-R38-006);</item>
    /// <item>the preamble lines a CSV/TSV <c>SkipRows</c> skips, after which the same source
    /// configuration skips the header instead (AUD-R38-027).</item>
    /// </list>
    /// </remarks>
    private static void RequireInPlaceWriteBackIsLossless(IFileSource source)
    {
        if (source.FilePaths.Count > 1)
        {
            throw new InvalidOperationException(
                $"In-place WriteBack (WriteBackMode) is not supported for multi-file sources " +
                $"(source '{source.TableName}' spans {source.FilePaths.Count} files). " +
                "Use the overload that accepts an explicit output path instead.");
        }

        if (IsGlobPattern(source.FilePath))
        {
            throw new InvalidOperationException(
                $"In-place WriteBack (WriteBackMode) is not supported for glob sources " +
                $"(source '{source.TableName}' reads the pattern '{source.FilePath}', which can match several files). " +
                "Use the overload that accepts an explicit output path instead.");
        }

        string? lost = source switch
        {
            CsvFileSource { SkipRows: > 0 } csv => $"the {csv.SkipRows} line(s) SkipRows skips before the header",
            TsvFileSource { SkipRows: > 0 } tsv => $"the {tsv.SkipRows} line(s) SkipRows skips before the header",
            ExcelFileSource { Range: not null } excel => $"every cell outside Range '{excel.Range}'",
            ExcelFileSource excel => DescribeOtherSheets(excel.FilePath),
            _ => null,
        };

        if (lost is not null)
        {
            throw new InvalidOperationException(
                $"In-place WriteBack (WriteBackMode) of source '{source.TableName}' would rewrite " +
                $"'{source.FilePath}' from only the data the source reads, losing {lost}. " +
                "Use the overload that accepts an explicit output path instead.");
        }
    }

    /// <summary>
    /// Returns a description of what an in-place rewrite of the workbook would lose, or
    /// <see langword="null"/> when it holds exactly one sheet. An unreadable workbook is refused
    /// rather than assumed safe: the operation it guards is irreversible.
    /// </summary>
    private static string? DescribeOtherSheets(string workbookPath)
    {
        int sheets;
        try
        {
            sheets = CountWorkbookSheets(workbookPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Xml.XmlException or ArgumentException or NotSupportedException)
        {
            return $"any sheet other than the one it reads (the workbook's sheets could not be listed: {ex.Message})";
        }

        return sheets == 1 ? null : $"the workbook's other {sheets - 1} sheet(s)";
    }

    /// <summary>Counts the <c>&lt;sheet&gt;</c> entries in an .xlsx file's <c>xl/workbook.xml</c>.</summary>
    internal static int CountWorkbookSheets(string workbookPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(workbookPath);
        ZipArchiveEntry entry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidDataException("xl/workbook.xml is missing.");

        using Stream stream = entry.Open();
        // Stryker disable once Initializer : Prohibit is already XmlReaderSettings' default, so an empty initializer behaves the same; it is spelled out because the input is an untrusted file
        using var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit });
        int count = 0;
        while (reader.Read())
        {
            if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.LocalName == "sheet")
                count++;
        }
        return count;
    }

    private static string InferFormatFromExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        var format = ext switch
        {
            ".csv" => FileFormats.Csv,
            ".tsv" => FileFormats.Tsv,
            ".parquet" => FileFormats.Parquet,
            ".json" or ".ndjson" => FileFormats.Json,
            ".xlsx" => FileFormats.Excel,
            _ => (string?)null
        };

        if (format is not null)
            return format;

        // ".xls" (legacy binary Excel) is deliberately unsupported here, matching FlatFile.cs's
        // read-side _extensionRegistry: DuckDB's write path can only produce modern ".xlsx" files,
        // and writing that content under a ".xls" name would produce a file FlatFile.Open() can
        // never read back and that isn't a genuine legacy .xls file despite the extension.
        throw new ArgumentException(
            $"Cannot infer output format from extension '{ext}'. " +
            $"Supported extensions: .csv, .tsv, .parquet, .json, .ndjson, .xlsx." +
            (ext == ".xls" ? " \".xls\" (legacy binary Excel) is not supported for writing - use \".xlsx\" instead." : string.Empty),
            nameof(path));
    }
}
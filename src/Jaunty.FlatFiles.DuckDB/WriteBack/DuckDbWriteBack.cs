using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.Interfaces;
using Jaunty.FlatFiles.WriteBack;

namespace Jaunty.FlatFiles.DuckDB;

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

        if (source.FilePaths.Count > 1)
        {
            throw new InvalidOperationException(
                $"In-place WriteBack (WriteBackMode) is not supported for multi-file sources " +
                $"(source '{source.TableName}' spans {source.FilePaths.Count} files). " +
                "Use the overload that accepts an explicit output path instead.");
        }

        var originalPath = source.FilePath;
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

        return string.Equals(format, source.Format, StringComparison.OrdinalIgnoreCase)
            ? _dialect.GenerateCopyToSql(source.TableName, fullPath, source)
            : _dialect.GenerateCopyToSql(source.TableName, fullPath, format);
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
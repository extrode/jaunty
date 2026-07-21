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
        var format = InferFormatFromExtension(outputPath);
        var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(outputPath), format);

        NonQueryExecutor.Execute(_connection, sql, []);
    }

    /// <inheritdoc />
    public void Save<T>(WriteBackMode mode) where T : class, new()
    {
        IFileSource source = GetSourceOrThrow<T>();

        if (mode == WriteBackMode.NewFile)
        {
            throw new InvalidOperationException(
                "WriteBackMode.NewFile requires an output path. Use the Save<T>(string outputPath) overload instead.");
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
        var tempPath = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(originalPath)}.tmp{Path.GetExtension(originalPath)}");

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
        var format = InferFormatFromExtension(outputPath);
        var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(outputPath), format);

        NonQueryExecutor.Execute(_connection, sql, []);
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
using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.WriteBack;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public void Save<T>(string outputPath) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        var source = GetSourceOrThrow<T>();
        var format = InferFormatFromExtension(outputPath);
        var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(outputPath), format);

        NonQueryExecutor.Execute(_connection, sql, []);
    }

    /// <inheritdoc />
    public void Save<T>(WriteBackMode mode) where T : class, new()
    {
        var source = GetSourceOrThrow<T>();

        if (mode == WriteBackMode.NewFile)
        {
            throw new InvalidOperationException(
                "WriteBackMode.NewFile requires an output path. Use the Save<T>(string outputPath) overload instead.");
        }

        var originalPath = source.FilePath;
        var directory = Path.GetDirectoryName(originalPath) ?? ".";
        var tempPath = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(originalPath)}.tmp{Path.GetExtension(originalPath)}");

        try
        {
            var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(tempPath), source);
            NonQueryExecutor.Execute(_connection, sql, []);

            File.Delete(originalPath);
            File.Move(tempPath, originalPath);
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

        var source = GetSourceOrThrow<T>();
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
            ".xlsx" or ".xls" => FileFormats.Excel,
            _ => (string?)null
        };

        if (format is not null)
            return format;

        throw new ArgumentException(
            $"Cannot infer output format from extension '{ext}'. " +
            $"Supported extensions: .csv, .tsv, .parquet, .json, .ndjson, .xlsx, .xls.",
            nameof(path));
    }
}

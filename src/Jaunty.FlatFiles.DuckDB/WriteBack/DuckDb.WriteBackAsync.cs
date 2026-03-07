using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public async ValueTask SaveAsync<T>(string outputPath, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        var source = GetSourceOrThrow<T>();
        var format = InferFormatFromExtension(outputPath);
        var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(outputPath), format);

        await NonQueryExecutor.ExecuteAsync(_connection, sql, [], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync<T>(WriteBackMode mode, CancellationToken cancellationToken = default) where T : class, new()
    {
        var source = GetSourceOrThrow<T>();

        if (mode == WriteBackMode.NewFile)
        {
            throw new InvalidOperationException(
                "WriteBackMode.NewFile requires an output path. Use the SaveAsync<T>(string outputPath) overload instead.");
        }

        var originalPath = source.FilePath;
        var directory = Path.GetDirectoryName(originalPath) ?? ".";
        var tempPath = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(originalPath)}.tmp{Path.GetExtension(originalPath)}");

        try
        {
            var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(tempPath), source);
            await NonQueryExecutor.ExecuteAsync(_connection, sql, [], cancellationToken).ConfigureAwait(false);

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
    public async ValueTask ExportAsync<T>(string outputPath, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        var source = GetSourceOrThrow<T>();
        var format = InferFormatFromExtension(outputPath);
        var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(outputPath), format);

        await NonQueryExecutor.ExecuteAsync(_connection, sql, [], cancellationToken).ConfigureAwait(false);
    }
}

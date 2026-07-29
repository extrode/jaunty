using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.Interfaces;
using Jaunty.FlatFiles.WriteBack;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public async ValueTask SaveAsync<T>(string outputPath, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        IFileSource source = GetSourceOrThrow<T>();
        var sql = BuildCopyToSql(source, outputPath);

        await NonQueryExecutor.ExecuteAsync(_connection, sql, [], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync<T>(WriteBackMode mode, CancellationToken cancellationToken = default) where T : class, new()
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
                    "WriteBackMode.NewFile requires an output path. Use the SaveAsync<T>(string outputPath) overload instead.")
                : new ArgumentOutOfRangeException(
                    nameof(mode), mode,
                    $"'{{mode}}' is not a defined WriteBackMode. In-place write-back overwrites the source file " +
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
            await NonQueryExecutor.ExecuteAsync(_connection, sql, [], cancellationToken).ConfigureAwait(false);

            File.Move(tempPath, originalPath, overwrite: true);
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

        IFileSource source = GetSourceOrThrow<T>();
        var sql = BuildCopyToSql(source, outputPath);

        await NonQueryExecutor.ExecuteAsync(_connection, sql, [], cancellationToken).ConfigureAwait(false);
    }
}
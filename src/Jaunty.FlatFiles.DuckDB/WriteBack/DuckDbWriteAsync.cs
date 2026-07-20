using System.Text;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public async ValueTask<int> InsertAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await InsertCoreAsync(entity, default, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<int> InsertAsync<T>(T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await InsertCoreAsync(entity, options, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> InsertCoreAsync<T>(T entity, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(entity);

        IFileSource source = GetSourceOrThrow<T>();
        await TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(T));
        var columns = new StringBuilder(mappings.Count * 20);
        var values = new StringBuilder(mappings.Count * 10);
        var parameters = new List<DuckDBParameter>(mappings.Count);

        var mappingList = mappings.Values.ToList();
        for (int i = 0; i < mappingList.Count; i++)
        {
            if (i > 0) { columns.Append(", "); values.Append(", "); }
            ColumnMapping mapping = mappingList[i];
            columns.Append($"\"{mapping.ColumnName}\"");
            values.Append($"${i + 1}");
            parameters.Add(new DuckDBParameter { Value = mapping.Getter(entity) ?? DBNull.Value });
        }

        var sql = $"INSERT INTO \"{source.TableName}\" ({columns}) VALUES ({values})";
        var result = await NonQueryExecutor.ExecuteAsync(_connection, sql, parameters, options, cancellationToken).ConfigureAwait(false);
        _modified.TryAdd(typeof(T), true);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<int> InsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await InsertCoreAsync(entities, default, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<int> InsertAsync<T>(IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await InsertCoreAsync(entities, options, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> InsertCoreAsync<T>(IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(entities);

        IList<T> entityList = entities switch
        {
            IList<T> list => list,
            ICollection<T> collection => collection.ToList(),
            _ => entities.ToList()
        };

        if (entityList.Count == 0) return 0;

        IFileSource source = GetSourceOrThrow<T>();
        await TablePromoter.EnsurePromotedToTableAsync(_connection, source, _dialect, cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(T));
        var mappingList = mappings.Values.ToList();

        var columns = new StringBuilder(mappings.Count * 20);
        for (int i = 0; i < mappingList.Count; i++)
        {
            if (i > 0) columns.Append(", ");
            columns.Append($"\"{mappingList[i].ColumnName}\"");
        }
        var columnsSql = columns.ToString();

        // Chunk the batch so each generated multi-row INSERT stays under the dialect's
        // MaxParametersPerStatement limit (32768 for DuckDB) instead of building one enormous
        // statement for the whole collection.
        int rowsPerChunk = Math.Max(1, _dialect.MaxParametersPerStatement / mappingList.Count);
        var totalInserted = 0;

        for (int chunkStart = 0; chunkStart < entityList.Count; chunkStart += rowsPerChunk)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int chunkCount = Math.Min(rowsPerChunk, entityList.Count - chunkStart);

            var sb = new StringBuilder(chunkCount * mappingList.Count * 10);
            var parameters = new List<DuckDBParameter>(chunkCount * mappingList.Count);
            var paramCounter = 0;

            for (int row = 0; row < chunkCount; row++)
            {
                if (row > 0) sb.Append(", ");
                sb.Append('(');
                T entity = entityList[chunkStart + row];

                for (int col = 0; col < mappingList.Count; col++)
                {
                    if (col > 0) sb.Append(", ");
                    paramCounter++;
                    sb.Append($"${paramCounter}");
                    parameters.Add(new DuckDBParameter { Value = mappingList[col].Getter(entity) ?? DBNull.Value });
                }
                sb.Append(')');
            }

            var sql = $"INSERT INTO \"{source.TableName}\" ({columnsSql}) VALUES {sb}";
            totalInserted += await NonQueryExecutor.ExecuteAsync(_connection, sql, parameters, options, cancellationToken).ConfigureAwait(false);
        }

        if (totalInserted > 0) _modified.TryAdd(typeof(T), true);
        return totalInserted;
    }
}
using System.Text;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public int Insert<T>(T entity) where T : class, new()
    {
        return InsertCore(entity, default);
    }

    /// <inheritdoc />
    public int Insert<T>(T entity, CommandOptions options) where T : class, new()
    {
        return InsertCore(entity, options);
    }

    private int InsertCore<T>(T entity, CommandOptions options) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(entity);

        IFileSource source = GetSourceOrThrow<T>();
        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(T));
        var columns = new StringBuilder(mappings.Count * 20);
        var values = new StringBuilder(mappings.Count * 10);
        var parameters = new List<DuckDBParameter>(mappings.Count);

        var mappingList = mappings.Values.ToList();
        for (int i = 0; i < mappingList.Count; i++)
        {
            if (i > 0) { columns.Append(", "); values.Append(", "); }
            ColumnMapping mapping = mappingList[i];
            columns.Append(_dialect.EscapeColumnName(mapping.ColumnName));
            values.Append($"${i + 1}");
            parameters.Add(new DuckDBParameter { Value = mapping.Getter(entity) ?? DBNull.Value });
        }

        var sql = $"INSERT INTO {_dialect.EscapeTableName(null, source.TableName)} ({columns}) VALUES ({values})";
        var result = NonQueryExecutor.Execute(_connection, sql, parameters, options);
        // Guarded on rows-affected like the other six write sites (batch Insert, Update, Delete and
        // their async twins). A single-row VALUES insert affects exactly one row or throws today, so
        // this changes nothing now - it keeps the flag's meaning uniform if this statement ever grows
        // a conflict clause, which is exactly how the SQLite import path acquired DO NOTHING.
        if (result > 0) _modified.TryAdd(typeof(T), true);
        return result;
    }

    /// <inheritdoc />
    public int Insert<T>(IEnumerable<T> entities) where T : class, new()
    {
        return InsertCore(entities, default);
    }

    /// <inheritdoc />
    public int Insert<T>(IEnumerable<T> entities, CommandOptions options) where T : class, new()
    {
        return InsertCore(entities, options);
    }

    private int InsertCore<T>(IEnumerable<T> entities, CommandOptions options) where T : class, new()
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
        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(T));
        var mappingList = mappings.Values.ToList();

        var columns = new StringBuilder(mappings.Count * 20);
        for (int i = 0; i < mappingList.Count; i++)
        {
            if (i > 0) columns.Append(", ");
            columns.Append(_dialect.EscapeColumnName(mappingList[i].ColumnName));
        }
        var columnsSql = columns.ToString();

        // Chunk the batch so each generated multi-row INSERT stays under the dialect's
        // MaxParametersPerStatement limit (32768 for DuckDB) instead of building one enormous
        // statement for the whole collection.
        int rowsPerChunk = Math.Max(1, _dialect.MaxParametersPerStatement / mappingList.Count);
        var totalInserted = 0;

        for (int chunkStart = 0; chunkStart < entityList.Count; chunkStart += rowsPerChunk)
        {
            int chunkCount = Math.Min(rowsPerChunk, entityList.Count - chunkStart);

            var sb = new StringBuilder(chunkCount * mappingList.Count * 10);
            var parameters = new List<DuckDBParameter>(chunkCount * mappingList.Count);
            var paramCounter = 0;

            for (int row = 0; row < chunkCount; row++)
            {
                if (row > 0) sb.Append(", ");
                sb.Append('(');
                T entity = entityList[chunkStart + row];

                // AUD-R35-032: a null element used to surface as a bare NullReferenceException out
                // of the compiled getter, naming neither the row nor the entity type. The
                // single-entity overloads have guarded the same condition all along.
                if (entity is null)
                {
                    throw new ArgumentException(
                        $"The element at index {chunkStart + row} is null. A batch insert of " +
                        $"{typeof(T).Name} cannot contain null entities.",
                        nameof(entities));
                }

                for (int col = 0; col < mappingList.Count; col++)
                {
                    if (col > 0) sb.Append(", ");
                    paramCounter++;
                    sb.Append($"${paramCounter}");
                    parameters.Add(new DuckDBParameter { Value = mappingList[col].Getter(entity) ?? DBNull.Value });
                }
                sb.Append(')');
            }

            var sql = $"INSERT INTO {_dialect.EscapeTableName(null, source.TableName)} ({columnsSql}) VALUES {sb}";
            int chunkInserted = NonQueryExecutor.Execute(_connection, sql, parameters, options);
            totalInserted += chunkInserted;

            // AUD-R35-033: marked per chunk, not once after the loop. DuckDB is in autocommit and
            // each chunk is its own statement, so a throw - or a cancellation at the per-chunk check
            // in the async twin - on a later chunk left the earlier chunks' rows committed while the
            // flag stayed unset. IsModified<T>() then reported false for a table that had genuinely
            // changed, and a caller driving Save/Export off that flag silently skipped the
            // write-back. Only the chunked write can reach this; the single-statement sites cannot.
            if (chunkInserted > 0) _modified.TryAdd(typeof(T), true);
        }

        return totalInserted;
    }
}
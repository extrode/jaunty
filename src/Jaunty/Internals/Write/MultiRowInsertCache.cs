using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty.Internals.Write;

/// <summary>
/// Caches multi-row INSERT SQL statements keyed by (entity type, connection type, batch size).
/// Generates SQL like: INSERT INTO "table" ("col1", "col2") VALUES (@col1_0, @col2_0), (@col1_1, @col2_1), ...
/// </summary>
internal static class MultiRowInsertCache
{
    private static readonly ConcurrentDictionary<(Type EntityType, Type ConnectionType, int BatchSize), string> _cache = new();
    private static readonly ConcurrentDictionary<Type, Delegate[]> _getterCache = new();

    /// <summary>
    /// Gets or creates cached compiled property getters for multi-row parameter binding.
    /// Avoids re-compiling Expression trees on every BulkInsert call.
    /// </summary>
    public static Func<T, object?>[] GetOrBuildGetters<T>(EntityMetadata metadata)
    {
        if (_getterCache.TryGetValue(typeof(T), out var cached))
            return (Func<T, object?>[])cached;

        var columns = ColumnMetadataHelper.GetInsertableColumns(metadata);
        var getters = new Func<T, object?>[columns.Count];
        for (int c = 0; c < columns.Count; c++)
        {
            var prop = columns[c].Property;
            var param = Expression.Parameter(typeof(T), "e");
            var access = Expression.Property(param, prop);
            var box = Expression.Convert(access, typeof(object));
            getters[c] = Expression.Lambda<Func<T, object?>>(box, param).Compile();
        }

        _getterCache.TryAdd(typeof(T), getters);
        return getters;
    }

    /// <summary>
    /// Gets or creates cached multi-row INSERT SQL for the specified batch size.
    /// </summary>
    public static string GetOrBuild(
        Type entityType,
        Type connectionType,
        int batchSize,
        EntityMetadata metadata,
        ISqlDialect dialect)
    {
        var key = (entityType, connectionType, batchSize);

        if (_cache.TryGetValue(key, out string? cached))
            return cached;

        string sql = Build(metadata, dialect, batchSize);
        _cache.TryAdd(key, sql);
        return sql;
    }

    /// <summary>
    /// Builds multi-row INSERT SQL for the given batch size.
    /// Parameters are named @PropertyName_RowIndex (e.g., @ProductName_0, @ProductName_1, ...).
    /// </summary>
    private static string Build(EntityMetadata metadata, ISqlDialect dialect, int batchSize)
    {
        string escapedTableName = dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);

        // Get insertable columns (non-identity, non-computed)
        var insertableColumns = ColumnMetadataHelper.GetInsertableColumns(metadata);
        int colCount = insertableColumns.Count;

        // Estimate capacity: table + columns + per-row params
        var sb = new StringBuilder(128 + (colCount * 20 * batchSize));

        // INSERT INTO table (col1, col2, ...)
        sb.Append("INSERT INTO ");
        sb.Append(escapedTableName);
        sb.Append(" (");
        for (int c = 0; c < colCount; c++)
        {
            if (c > 0) sb.Append(", ");
            sb.Append(dialect.EscapeColumnName(insertableColumns[c].ColumnName));
        }
        sb.Append(") VALUES ");

        // (@col1_0, @col2_0), (@col1_1, @col2_1), ...
        for (int row = 0; row < batchSize; row++)
        {
            if (row > 0) sb.Append(", ");
            sb.Append('(');
            for (int c = 0; c < colCount; c++)
            {
                if (c > 0) sb.Append(", ");
                sb.Append('@');
                sb.Append(insertableColumns[c].ColumnName);
                sb.Append('_');
                sb.Append(row);
            }
            sb.Append(')');
        }

        return sb.ToString();
    }
}

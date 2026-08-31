using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
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
    // Not keyed by column layout, unlike _getterCache below. The old justification was that
    // ColumnMetadataHelper.GetInsertableColumns(metadata) is deterministic per entity type, so the
    // column set and order for T never varies across calls - and AUD-R26 (batch 4) established that
    // it does vary, because metadata is built from JauntyConfig's schema/table/column name resolvers
    // and those are public and settable at any time. That is exactly the failure the old comment
    // predicted: SQL whose @col_row parameter names no longer match the getters _getterCache
    // produces for the new layout.
    //
    // The generation on the entry closes it without a layout key: _getterCache already keys on the
    // layout, so the two agree by construction once a configuration change retires this entry.
    //
    // AUD-R35-058: bounded. The batch-size component of the key makes this grow with the *shapes of
    // the caller's collections*, not with the application's type surface: BulkInsertMultiRow sends
    // Math.Min(maxBatchSize, entityCount - offset), so every bulk insert whose row count is not an
    // exact multiple of maxBatchSize leaves behind a permanent entry for its remainder size. With
    // nothing evicting, a workload with varying collection sizes retains up to maxBatchSize distinct
    // SQL strings per (entity type, connection type) pair, each O(columns x batchSize) characters -
    // O(columns x maxBatchSize^2) in total, which is a few MB per entity type for a 10-column entity
    // on SQL Server (maxBatchSize = (2100-1)/10 = 209) and tens of MB where maxBatchSize reaches its
    // hard cap of 1000. AUD-R26-053 converted thirteen caller-shaped caches to BoundedCache and
    // listed only _getterCache below as deliberately left alone, because it holds compiled delegates
    // keyed on column metadata; neither half of that reasoning covers this one, which holds plain
    // strings rebuilt by a StringBuilder loop under a caller-shaped key.
    //
    // 256 rather than the 4096 default: entries here are whole SQL statements rather than the small
    // per-parameter records the default was chosen for, and a miss costs one StringBuilder pass.
    private static readonly BoundedCache<(Type EntityType, Type ConnectionType, int BatchSize), CachedSql> _cache = new(BoundedCacheLimits.SchemaCacheMaxEntries);

    /// <summary>
    /// A generation-stamped SQL string. <see cref="ConfigurationScoped{TValue}"/> is a struct and
    /// <see cref="BoundedCache{TKey, TValue}"/> requires a reference type, so this carries the same
    /// two fields as a class.
    /// </summary>
    private sealed class CachedSql
    {
        internal CachedSql(int generation, string sql)
        {
            Generation = generation;
            Sql = sql;
        }

        internal int Generation { get; }

        internal string Sql { get; }
    }

    // Keyed by (entity type, column layout) - not just entity type - so a second bulk insert of
    // the same T with a different column subset/order gets its own correctly-matching getters
    // instead of reusing a stale layout's getters. Mirrors EntityDataReaderCache<TEntity>'s
    // layout-keyed cache in Internals/BulkCopy/EntityDataReader.cs.
    private static readonly ConcurrentDictionary<(Type EntityType, string LayoutKey), Delegate[]> _getterCache = new();

    /// <summary>
    /// Gets or creates cached compiled property getters for multi-row parameter binding.
    /// Avoids re-compiling Expression trees on every BulkInsert call.
    /// </summary>
    public static Func<T, object?>[] GetOrBuildGetters<T>(EntityMetadata metadata)
    {
        IReadOnlyList<ColumnMetadata> columns = ColumnMetadataHelper.GetInsertableColumns(metadata);
        (Type, string) key = (typeof(T), BuildLayoutKey(columns));

        if (_getterCache.TryGetValue(key, out Delegate[]? cached))
            return (Func<T, object?>[])cached;

        var getters = new Func<T, object?>[columns.Count];
        for (int c = 0; c < columns.Count; c++)
        {
            ColumnMetadata column = columns[c];
            if (column.Getter is { } getter)
            {
                getters[c] = entity => getter(entity!);
                continue;
            }

            ParameterExpression param = Expression.Parameter(typeof(T), "e");
            MemberExpression access = Expression.Property(param, column.Property!);
            UnaryExpression box = Expression.Convert(access, typeof(object));
            getters[c] = Expression.Lambda<Func<T, object?>>(box, param).Compile();
        }

        _getterCache.TryAdd(key, getters);
        return getters;
    }

    private static string BuildLayoutKey(IReadOnlyList<ColumnMetadata> columns)
    {
        int columnCount = columns.Count;
        var parts = new string[columnCount + 1];
        parts[0] = columnCount.ToString();
        for (int i = 0; i < columnCount; i++) parts[i + 1] = columns[i].ColumnName ?? string.Empty;
        // Unit Separator (0x1F) prevents adjacent column names from colliding when concatenated
        // (e.g. ["ab","c"] and ["a","bc"] would otherwise both key to "2abc") - mirrors
        // EntityDataReaderCache<TEntity>.BuildLayoutKey in Internals/BulkCopy/EntityDataReader.cs.
        return string.Join("\x1F", parts);
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
        (Type entityType, Type connectionType, int batchSize) key = (entityType, connectionType, batchSize);

        // Read the generation before the lookup, never after: see ConfigurationGeneration.Current.
        int generation = ConfigurationGeneration.Current;

        if (_cache.Get(key) is { } cached && cached.Generation == generation)
            return cached.Sql;

        string sql = Build(metadata, dialect, batchSize);
        _cache.Set(key, new CachedSql(generation, sql));
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
        IReadOnlyList<ColumnMetadata> insertableColumns = ColumnMetadataHelper.GetInsertableColumns(metadata);
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
using System.Collections.Concurrent;
using System.Data;
using System.Text;

using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty.Internals;

/// <summary>
/// Caches generated CRUD SQL statements per entity type and dialect.
/// SQL is built once and reused for all subsequent operations.
/// </summary>
internal static class CrudSqlCache
{
    private static readonly ConcurrentDictionary<(Type, Type), CachedCrudSql> _cache = new();

    /// <summary>
    /// Gets or creates cached SQL for the specified entity type and connection.
    /// </summary>
    public static CachedCrudSql GetSql<T>(IDbConnection connection) where T : class, new()
    {
        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
        Type dialectType = dialect.GetType();
        var key = (typeof(T), dialectType);

        if (_cache.TryGetValue(key, out CachedCrudSql? cached))
            return cached;

        cached = BuildCachedSql<T>(dialect);
        _cache.TryAdd(key, cached);
        return cached;
    }

    private static CachedCrudSql BuildCachedSql<T>(ISqlDialect dialect) where T : class, new()
    {
        EntityMetadata metadata = MetadataCache<T>.Metadata;
        string escapedTableName = dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);

        string insertSql = BuildInsertSql(metadata, dialect, escapedTableName);
        string updateSql = BuildUpdateSql(metadata, dialect, escapedTableName);
        string deleteSql = BuildDeleteSql(metadata, dialect, escapedTableName);
        string deleteByIdSql = BuildDeleteByIdSql(metadata, dialect, escapedTableName);
        string upsertSql = dialect.SupportsUpsert ? BuildUpsertSql(metadata, dialect, escapedTableName) : string.Empty;
        string lastInsertIdSql = dialect.GetLastInsertIdSql();

        return new CachedCrudSql(
            insertSql,
            updateSql,
            deleteSql,
            deleteByIdSql,
            upsertSql,
            lastInsertIdSql,
            metadata,
            dialect.SupportsUpsert);
    }

    private static string BuildInsertSql(EntityMetadata metadata, ISqlDialect dialect, string escapedTableName)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.NonIdentityColumns;

        // Filter out computed columns
        var insertableColumns = new List<ColumnMetadata>(columns.Count);
        for (int i = 0; i < columns.Count; i++)
        {
            if (!columns[i].IsComputed)
                insertableColumns.Add(columns[i]);
        }

        if (insertableColumns.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(256);
        sb.Append("INSERT INTO ");
        sb.Append(escapedTableName);
        sb.Append(" (");

        for (int i = 0; i < insertableColumns.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(dialect.EscapeColumnName(insertableColumns[i].ColumnName));
        }

        sb.Append(") VALUES (");

        for (int i = 0; i < insertableColumns.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append('@');
            sb.Append(insertableColumns[i].Property.Name);
        }

        sb.Append(')');

        return sb.ToString();
    }

    private static string BuildUpdateSql(EntityMetadata metadata, ISqlDialect dialect, string escapedTableName)
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;
        if (primaryKeys.Count == 0)
            return string.Empty;

        // Get columns to update (non-key, non-identity, non-computed)
        IReadOnlyList<ColumnMetadata> allColumns = metadata.Columns;
        var updateColumns = new List<ColumnMetadata>(allColumns.Count);

        for (int i = 0; i < allColumns.Count; i++)
        {
            ColumnMetadata col = allColumns[i];
            if (!col.IsPrimaryKey && !col.IsIdentity && !col.IsComputed)
                updateColumns.Add(col);
        }

        if (updateColumns.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(256);
        sb.Append("UPDATE ");
        sb.Append(escapedTableName);
        sb.Append(" SET ");

        for (int i = 0; i < updateColumns.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(dialect.EscapeColumnName(updateColumns[i].ColumnName));
            sb.Append(" = @");
            sb.Append(updateColumns[i].Property.Name);
        }

        sb.Append(" WHERE ");
        AppendWhereClause(sb, primaryKeys, dialect, usePropertyNames: true);

        return sb.ToString();
    }

    private static string BuildDeleteSql(EntityMetadata metadata, ISqlDialect dialect, string escapedTableName)
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;
        if (primaryKeys.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(128);
        sb.Append("DELETE FROM ");
        sb.Append(escapedTableName);
        sb.Append(" WHERE ");
        AppendWhereClause(sb, primaryKeys, dialect, usePropertyNames: true);

        return sb.ToString();
    }

    private static string BuildDeleteByIdSql(EntityMetadata metadata, ISqlDialect dialect, string escapedTableName)
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;
        if (primaryKeys.Count != 1)
            return string.Empty; // Delete by ID only works for single primary key

        var sb = new StringBuilder(128);
        sb.Append("DELETE FROM ");
        sb.Append(escapedTableName);
        sb.Append(" WHERE ");
        sb.Append(dialect.EscapeColumnName(primaryKeys[0].ColumnName));
        sb.Append(" = @Id");

        return sb.ToString();
    }

    private static string BuildUpsertSql(EntityMetadata metadata, ISqlDialect dialect, string escapedTableName)
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;
        if (primaryKeys.Count == 0)
            return string.Empty; // Upsert requires primary key

        // Get all columns for INSERT (non-identity, non-computed)
        IReadOnlyList<ColumnMetadata> allColumns = metadata.NonIdentityColumns;
        var insertColumns = new List<string>(allColumns.Count);
        var insertParams = new List<string>(allColumns.Count);

        for (int i = 0; i < allColumns.Count; i++)
        {
            ColumnMetadata col = allColumns[i];
            if (!col.IsComputed)
            {
                insertColumns.Add(dialect.EscapeColumnName(col.ColumnName));
                insertParams.Add("@" + col.Property.Name);
            }
        }

        if (insertColumns.Count == 0)
            return string.Empty;

        // Get columns for UPDATE (non-key, non-identity, non-computed)
        var updateColumns = new List<string>(allColumns.Count);
        var updateParams = new List<string>(allColumns.Count);

        for (int i = 0; i < allColumns.Count; i++)
        {
            ColumnMetadata col = allColumns[i];
            if (!col.IsPrimaryKey && !col.IsIdentity && !col.IsComputed)
            {
                updateColumns.Add(dialect.EscapeColumnName(col.ColumnName));
                updateParams.Add("@" + col.Property.Name);
            }
        }

        // Get key columns
        var keyColumns = new string[primaryKeys.Count];
        for (int i = 0; i < primaryKeys.Count; i++)
        {
            keyColumns[i] = dialect.EscapeColumnName(primaryKeys[i].ColumnName);
        }

        return dialect.GenerateUpsertSql(
            escapedTableName,
            insertColumns.ToArray(),
            insertParams.ToArray(),
            updateColumns.ToArray(),
            updateParams.ToArray(),
            keyColumns);
    }

    private static void AppendWhereClause(StringBuilder sb, IReadOnlyList<ColumnMetadata> keys, ISqlDialect dialect, bool usePropertyNames)
    {
        for (int i = 0; i < keys.Count; i++)
        {
            if (i > 0)
                sb.Append(" AND ");

            sb.Append(dialect.EscapeColumnName(keys[i].ColumnName));
            sb.Append(" = @");
            sb.Append(usePropertyNames ? keys[i].Property.Name : "Id");
        }
    }
}

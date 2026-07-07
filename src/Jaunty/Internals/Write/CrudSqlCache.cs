using System.Collections.Concurrent;
using System.Data;
using System.Text;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Interfaces;
using Jaunty.Internals.Entity;

namespace Jaunty.Internals.Write;

/// <summary>
/// Caches generated CRUD SQL statements per entity type and dialect.
/// SQL is built once and reused for all subsequent operations.
/// </summary>
internal static class CrudSqlCache
{
    private static readonly ConcurrentDictionary<(Type, Type), CachedCrudSql> _cache = new();

    /// <summary>
    /// Gets or creates cached SQL for the specified entity type and connection.
    /// Uses (Type, connectionType) as key — dialect is resolved from connection type via cached factory.
    /// </summary>
    public static CachedCrudSql GetSql<T>(IDbConnection connection) where T : new()
    {
        (Type, Type) key = (typeof(T), connection.GetType());

        if (_cache.TryGetValue(key, out CachedCrudSql? cached))
            return cached;

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
        cached = BuildCachedSql<T>(dialect);
        _cache.TryAdd(key, cached);
        return cached;
    }

    private static CachedCrudSql BuildCachedSql<T>(ISqlDialect dialect) where T : new()
    {
        EntityMetadata? metadata = TryResolveMetadata<T>();

        if (metadata == null)
        {
            throw new InvalidOperationException(
                $"Cannot build CRUD SQL for type '{typeof(T).Name}'. " +
                "Ensure the class has [Table] and is processed by the Jaunty source generator " +
                "(the class must be declared 'partial'), or call " +
                "Jaunty.Extensions.Reflection's UseReflectionMapping().");
        }

        string escapedTableName = dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);

        string insertSql = BuildInsertSql(metadata, dialect, escapedTableName);
        string updateSql = BuildUpdateSql(metadata, dialect, escapedTableName);
        string deleteSql = BuildDeleteSql(metadata, dialect, escapedTableName);
        string deleteByIdSql = BuildDeleteByIdSql(metadata, dialect, escapedTableName);
        string selectByIdSql = BuildSelectByIdSql(metadata, dialect, escapedTableName);
        string selectAllSql = BuildSelectAllSql(metadata, dialect, escapedTableName);
        string upsertSql = dialect.SupportsUpsert ? BuildUpsertSql(metadata, dialect, escapedTableName) : string.Empty;

        // Extract identity column names without LINQ (zero allocation)
        var identityColumnNames = new string[metadata.PrimaryKeys.Count];
        int identityCount = 0;
        for (int i = 0; i < metadata.PrimaryKeys.Count; i++)
        {
            if (metadata.PrimaryKeys[i].IsIdentity)
                identityColumnNames[identityCount++] = dialect.EscapeColumnName(metadata.PrimaryKeys[i].ColumnName);
        }
        string lastInsertIdSql = dialect.GetLastInsertIdSql(System.Array.Empty<string>());
        if (identityCount > 0)
        {
            var trimmed = new string[identityCount];
            System.Array.Copy(identityColumnNames, 0, trimmed, 0, identityCount);
            lastInsertIdSql = dialect.GetLastInsertIdSql(trimmed);
        }

        return new CachedCrudSql(insertSql, updateSql, deleteSql, deleteByIdSql, upsertSql, lastInsertIdSql, selectByIdSql, selectAllSql, metadata, dialect.SupportsUpsert);
    }

    private static EntityMetadata? TryResolveMetadata<T>() where T : new()
    {
        // 1. Source-generated static surface (TableName/SchemaName/ParameterMap) - reflection-free
        if (SourceGeneratedMetadataResolver.TryBuild(typeof(T)) is EntityMetadata sourceGenMetadata)
        {
            return sourceGenMetadata;
        }

        // 2. Fallback to extension hook (Jaunty.Extensions.Reflection)
        if (JauntyConfig.ReflectionTableMetadataResolver?.Invoke(typeof(T)) is EntityMetadata metadata)
        {
            return metadata;
        }

        return null;
    }

    private static string BuildInsertSql(EntityMetadata metadata, ISqlDialect dialect, string escapedTableName)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.InsertColumns;

        if (columns.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(256);
        sb.Append("INSERT INTO ");
        sb.Append(escapedTableName);
        sb.Append(" (");

        for (int i = 0; i < columns.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(dialect.EscapeColumnName(columns[i].ColumnName));
        }

        sb.Append(") VALUES (");

        for (int i = 0; i < columns.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append('@');
            sb.Append(columns[i].ColumnName);
        }

        sb.Append(')');

        return sb.ToString();
    }

    private static string BuildUpdateSql(EntityMetadata metadata, ISqlDialect dialect, string escapedTableName)
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;
        if (primaryKeys.Count == 0)
            return string.Empty;

        IReadOnlyList<ColumnMetadata> updateColumns = metadata.UpdateColumns;
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
            sb.Append(updateColumns[i].ColumnName);
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
            return string.Empty;

        var sb = new StringBuilder(128);
        sb.Append("DELETE FROM ");
        sb.Append(escapedTableName);
        sb.Append(" WHERE ");
        sb.Append(dialect.EscapeColumnName(primaryKeys[0].ColumnName));
        sb.Append(" = @");
        sb.Append(primaryKeys[0].ColumnName);

        return sb.ToString();
    }

    private static string BuildUpsertSql(EntityMetadata metadata, ISqlDialect dialect, string escapedTableName)
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;
        if (primaryKeys.Count == 0)
            return string.Empty;

        IReadOnlyList<ColumnMetadata> insertColumns = metadata.InsertColumns;
        IReadOnlyList<ColumnMetadata> updateColumns = metadata.UpdateColumns;

        if (insertColumns.Count == 0)
            return string.Empty;

        var insertColNames = new string[insertColumns.Count];
        var insertParams = new string[insertColumns.Count];
        for (int i = 0; i < insertColumns.Count; i++)
        {
            insertColNames[i] = dialect.EscapeColumnName(insertColumns[i].ColumnName);
            insertParams[i] = "@" + insertColumns[i].ColumnName;
        }

        var updateColNames = new string[updateColumns.Count];
        var updateParams = new string[updateColumns.Count];
        for (int i = 0; i < updateColumns.Count; i++)
        {
            updateColNames[i] = dialect.EscapeColumnName(updateColumns[i].ColumnName);
            updateParams[i] = "@" + updateColumns[i].ColumnName;
        }

        var keyColumns = new string[primaryKeys.Count];
        for (int i = 0; i < primaryKeys.Count; i++)
        {
            keyColumns[i] = dialect.EscapeColumnName(primaryKeys[i].ColumnName);
        }

        return dialect.GenerateUpsertSql(
            escapedTableName,
            insertColNames,
            insertParams,
            updateColNames,
            updateParams,
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
            sb.Append(usePropertyNames ? keys[i].ColumnName : "Id");
        }
    }

    private static string BuildSelectByIdSql(EntityMetadata metadata, ISqlDialect dialect, string escapedTableName)
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;
        if (primaryKeys.Count != 1)
            return string.Empty;

        var sb = new StringBuilder(256);
        sb.Append("SELECT ");
        
        // List all columns
        for (int i = 0; i < metadata.Columns.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(dialect.EscapeColumnName(metadata.Columns[i].ColumnName));
        }

        sb.Append(" FROM ");
        sb.Append(escapedTableName);
        sb.Append(" WHERE ");
        sb.Append(dialect.EscapeColumnName(primaryKeys[0].ColumnName));
        sb.Append(" = @");
        sb.Append(primaryKeys[0].ColumnName);

        return sb.ToString();
    }

    private static string BuildSelectAllSql(EntityMetadata metadata, ISqlDialect dialect, string escapedTableName)
    {
        var sb = new StringBuilder(256);
        sb.Append("SELECT ");
        
        // List all columns
        for (int i = 0; i < metadata.Columns.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(dialect.EscapeColumnName(metadata.Columns[i].ColumnName));
        }

        sb.Append(" FROM ");
        sb.Append(escapedTableName);

        return sb.ToString();
    }

}
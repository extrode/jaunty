using System.Data;
using System.Data.Common;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Internals;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Providers.SqlServer;

/// <summary>
/// Reads schema information from SQL Server databases.
/// </summary>
public sealed class SqlServerSchemaReader : ISchemaReader
{
    private const string TablesSql = @"
        SELECT
            s.name AS SchemaName,
            t.name AS TableName
        FROM sys.tables t
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE t.is_ms_shipped = 0
        ORDER BY s.name, t.name";

    private const string ColumnsSql = @"
        SELECT
            c.name AS ColumnName,
            TYPE_NAME(ty.system_type_id) AS DataType,
            c.is_nullable AS IsNullable,
            c.is_identity AS IsIdentity,
            c.is_computed AS IsComputed,
            c.max_length AS MaxLength,
            c.precision AS [Precision],
            c.scale AS Scale,
            dc.definition AS DefaultValue,
            c.column_id AS OrdinalPosition
        FROM sys.columns c
        INNER JOIN sys.tables t ON c.object_id = t.object_id
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
        LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
        WHERE s.name = @SchemaName AND t.name = @TableName
        ORDER BY c.column_id";

    private const string PrimaryKeysSql = @"
        SELECT
            kc.name AS ConstraintName,
            c.name AS ColumnName,
            ic.key_ordinal AS KeyOrdinal
        FROM sys.key_constraints kc
        INNER JOIN sys.index_columns ic ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        INNER JOIN sys.tables t ON kc.parent_object_id = t.object_id
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE kc.type = 'PK' AND s.name = @SchemaName AND t.name = @TableName
        ORDER BY ic.key_ordinal";

    private const string ForeignKeysSql = @"
        SELECT
            fk.name AS ConstraintName,
            COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ForeignKeyColumn,
            OBJECT_SCHEMA_NAME(fkc.referenced_object_id) AS ReferencedSchema,
            OBJECT_NAME(fkc.referenced_object_id) AS ReferencedTable,
            COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumn
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE s.name = @SchemaName AND t.name = @TableName";

    /// <inheritdoc />
    public async Task<DatabaseSchema> ReadSchemaAsync(
        string connectionString,
        SchemaReaderOptions options,
        CancellationToken cancellationToken = default)
    {
        using DbConnection connection = CreateConnection(connectionString);
        await OpenConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var tables = new List<TableSchema>();
        List<(string SchemaName, string TableName)> tableInfos = await GetTableInfosAsync(connection, options, cancellationToken).ConfigureAwait(false);

        foreach ((string? schemaName, string? tableName) in tableInfos)
        {
            TableSchema tableSchema = await ReadTableSchemaAsync(connection, schemaName, tableName, options, cancellationToken).ConfigureAwait(false);
            tables.Add(tableSchema);
        }

        return new DatabaseSchema
        {
            DatabaseName = connection.Database,
            Tables = tables
        };
    }

    private static DbConnection CreateConnection(string connectionString)
    {
        // Try Microsoft.Data.SqlClient first, then System.Data.SqlClient
        var connectionTypes = new[]
        {
            "Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient",
            "System.Data.SqlClient.SqlConnection, System.Data.SqlClient",
        };

        foreach (var typeName in connectionTypes)
        {
#pragma warning disable IL2057 // Type name is from trusted source list
            var type = Type.GetType(typeName);
#pragma warning restore IL2057
            if (type != null)
            {
                return ReflectedConnectionFactory.Create(type, connectionString);
            }
        }

        throw new InvalidOperationException(
            "Could not find SQL Server provider. Please install Microsoft.Data.SqlClient or System.Data.SqlClient.");
    }

    private static async Task OpenConnectionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<(string SchemaName, string TableName)>> GetTableInfosAsync(
        DbConnection connection,
        SchemaReaderOptions options,
        CancellationToken cancellationToken)
    {
        var tables = new List<(string, string)>();

        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = TablesSql;

        using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var schemaName = reader.GetString(0);
            var tableName = reader.GetString(1);

            // Apply schema filter
            if (options.IncludeSchemas?.Count > 0 &&
                !options.IncludeSchemas.Contains(schemaName, StringComparer.OrdinalIgnoreCase))
                continue;

            // Apply table filters
            if (options.IncludeTables?.Count > 0 &&
                !options.IncludeTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                continue;

            if (options.ExcludeTables?.Count > 0 &&
                options.ExcludeTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                continue;

            tables.Add((schemaName, tableName));
        }

        return tables;
    }

    private static async Task<TableSchema> ReadTableSchemaAsync(
        DbConnection connection,
        string schemaName,
        string tableName,
        SchemaReaderOptions options,
        CancellationToken cancellationToken)
    {
        List<ColumnSchema> columns = await ReadColumnsAsync(connection, schemaName, tableName, cancellationToken).ConfigureAwait(false);
        PrimaryKeyInfo? primaryKey = await ReadPrimaryKeyAsync(connection, schemaName, tableName, cancellationToken).ConfigureAwait(false);
        List<ForeignKeyInfo> foreignKeys = options.IncludeForeignKeys
            ? await ReadForeignKeysAsync(connection, schemaName, tableName, cancellationToken).ConfigureAwait(false)
            : [];

        MarkPrimaryKeyColumns(columns, primaryKey);

        return new TableSchema
        {
            SchemaName = schemaName,
            TableName = tableName,
            Columns = columns,
            PrimaryKey = primaryKey,
            ForeignKeys = foreignKeys
        };
    }

    internal static void MarkPrimaryKeyColumns(List<ColumnSchema> columns, PrimaryKeyInfo? primaryKey)
    {
        if (primaryKey == null)
            return;

        for (var index = 0; index < columns.Count; index++)
        {
            ColumnSchema col = columns[index];
            if (primaryKey.Columns.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase))
            {
                // Re-create with IsPrimaryKey set (since ColumnSchema is init-only)
                columns[index] = new ColumnSchema
                {
                    ColumnName = col.ColumnName,
                    DataType = col.DataType,
                    IsNullable = col.IsNullable,
                    IsPrimaryKey = true,
                    IsIdentity = col.IsIdentity,
                    IsComputed = col.IsComputed,
                    MaxLength = col.MaxLength,
                    Precision = col.Precision,
                    Scale = col.Scale,
                    DefaultValue = col.DefaultValue,
                    OrdinalPosition = col.OrdinalPosition
                };
            }
        }
    }

    private static async Task<List<ColumnSchema>> ReadColumnsAsync(
        DbConnection connection,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new List<ColumnSchema>();

        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = ColumnsSql;

        DbParameter schemaParam = cmd.CreateParameter();
        schemaParam.ParameterName = "@SchemaName";
        schemaParam.Value = schemaName;
        cmd.Parameters.Add(schemaParam);

        DbParameter tableParam = cmd.CreateParameter();
        tableParam.ParameterName = "@TableName";
        tableParam.Value = tableName;
        cmd.Parameters.Add(tableParam);

        using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string dataType = reader.GetString(1);

            columns.Add(new ColumnSchema
            {
                ColumnName = reader.GetString(0),
                DataType = dataType,
                IsNullable = reader.GetBoolean(2),
                IsIdentity = reader.GetBoolean(3),
                IsComputed = reader.GetBoolean(4),
                MaxLength = NormalizeMaxLength(dataType, reader.IsDBNull(5) ? null : reader.GetInt16(5)),
                Precision = reader.IsDBNull(6) ? null : reader.GetByte(6),
                Scale = reader.IsDBNull(7) ? null : reader.GetByte(7),
                DefaultValue = reader.IsDBNull(8) ? null : reader.GetString(8),
                OrdinalPosition = reader.GetInt32(9)
            });
        }

        return columns;
    }

    /// <summary>
    /// <c>sys.columns.max_length</c> is reported in bytes. For unicode character types
    /// (<c>nchar</c>/<c>nvarchar</c>) SQL Server stores 2 bytes per character, so the raw value
    /// must be halved to get the actual character length; <c>-1</c> (the MAX sentinel) is left
    /// untouched. The legacy LOB types <c>text</c>/<c>ntext</c>/<c>image</c> always report a fixed
    /// sentinel <c>max_length</c> of 16 (the size of the internal data pointer) regardless of the
    /// column's actual, effectively unbounded, capacity - AUD-R22: treat them as unbounded (null)
    /// like the MAX sentinel instead of halving/passing through the meaningless sentinel value.
    /// </summary>
    internal static short? NormalizeMaxLength(string dataType, short? maxLength)
    {
        if (maxLength is null or -1)
            return maxLength;

        if (dataType is "text" or "ntext" or "image")
            return null;

        return dataType is "nchar" or "nvarchar"
            ? (short)(maxLength.Value / 2)
            : maxLength;
    }

    private static async Task<PrimaryKeyInfo?> ReadPrimaryKeyAsync(
        DbConnection connection,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = PrimaryKeysSql;

        DbParameter schemaParam = cmd.CreateParameter();
        schemaParam.ParameterName = "@SchemaName";
        schemaParam.Value = schemaName;
        cmd.Parameters.Add(schemaParam);

        DbParameter tableParam = cmd.CreateParameter();
        tableParam.ParameterName = "@TableName";
        tableParam.Value = tableName;
        cmd.Parameters.Add(tableParam);

        string? constraintName = null;
        var columns = new List<string>();

        using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            constraintName ??= reader.GetString(0);
            columns.Add(reader.GetString(1));
        }

        if (columns.Count == 0)
            return null;

        return new PrimaryKeyInfo
        {
            ConstraintName = constraintName!,
            Columns = columns
        };
    }

    private static async Task<List<ForeignKeyInfo>> ReadForeignKeysAsync(
        DbConnection connection,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        var foreignKeys = new List<ForeignKeyInfo>();

        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = ForeignKeysSql;

        DbParameter schemaParam = cmd.CreateParameter();
        schemaParam.ParameterName = "@SchemaName";
        schemaParam.Value = schemaName;
        cmd.Parameters.Add(schemaParam);

        DbParameter tableParam = cmd.CreateParameter();
        tableParam.ParameterName = "@TableName";
        tableParam.Value = tableName;
        cmd.Parameters.Add(tableParam);

        using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            foreignKeys.Add(new ForeignKeyInfo
            {
                ConstraintName = reader.GetString(0),
                ForeignKeyColumn = reader.GetString(1),
                ReferencedSchema = reader.IsDBNull(2) ? null : reader.GetString(2),
                ReferencedTable = reader.GetString(3),
                ReferencedColumn = reader.GetString(4)
            });
        }

        return foreignKeys;
    }
}
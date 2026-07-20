using System.Data;
using System.Data.Common;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Providers.PostgreSql;

/// <summary>
/// Reads schema information from PostgreSQL databases.
/// </summary>
public sealed class PostgreSqlSchemaReader : ISchemaReader
{
    private const string TablesSql = @"
        SELECT
            table_schema AS SchemaName,
            table_name AS TableName
        FROM information_schema.tables
        WHERE table_type = 'BASE TABLE'
          AND table_schema NOT IN ('pg_catalog', 'information_schema')
        ORDER BY table_schema, table_name";

    private const string ColumnsSql = @"
        SELECT
            c.column_name AS ColumnName,
            c.udt_name AS DataType,
            c.is_nullable = 'YES' AS IsNullable,
            COALESCE(c.column_default LIKE 'nextval(%', c.is_identity = 'YES') AS IsIdentity,
            c.is_generated = 'ALWAYS' AS IsComputed,
            c.character_maximum_length AS MaxLength,
            c.numeric_precision AS Precision,
            c.numeric_scale AS Scale,
            c.column_default AS DefaultValue,
            c.ordinal_position AS OrdinalPosition
        FROM information_schema.columns c
        WHERE c.table_schema = @SchemaName AND c.table_name = @TableName
        ORDER BY c.ordinal_position";

    private const string PrimaryKeysSql = @"
        SELECT
            tc.constraint_name AS ConstraintName,
            kcu.column_name AS ColumnName,
            kcu.ordinal_position AS KeyOrdinal
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
            AND tc.table_name = kcu.table_name
        WHERE tc.constraint_type = 'PRIMARY KEY'
          AND tc.table_schema = @SchemaName
          AND tc.table_name = @TableName
        ORDER BY kcu.ordinal_position";

    private const string ForeignKeysSql = @"
        SELECT
            tc.constraint_name AS ConstraintName,
            kcu.column_name AS ForeignKeyColumn,
            ccu.table_schema AS ReferencedSchema,
            ccu.table_name AS ReferencedTable,
            ccu.column_name AS ReferencedColumn
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu
            ON tc.constraint_name = kcu.constraint_name
            AND tc.table_schema = kcu.table_schema
            AND tc.table_name = kcu.table_name
        JOIN information_schema.constraint_column_usage ccu
            ON tc.constraint_name = ccu.constraint_name
            AND tc.table_schema = ccu.constraint_schema
        WHERE tc.constraint_type = 'FOREIGN KEY'
          AND tc.table_schema = @SchemaName
          AND tc.table_name = @TableName";

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
        var type = Type.GetType("Npgsql.NpgsqlConnection, Npgsql");
        if (type != null)
            return (DbConnection)Activator.CreateInstance(type, connectionString)!;

        throw new InvalidOperationException("Could not find PostgreSQL provider. Please install Npgsql.");
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

            if (options.IncludeSchemas?.Count > 0 &&
                !options.IncludeSchemas.Contains(schemaName, StringComparer.OrdinalIgnoreCase))
                continue;

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

        if (primaryKey != null)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                ColumnSchema col = columns[i];
                if (primaryKey.Columns.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase))
                {
                    columns[i] = new ColumnSchema
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

        return new TableSchema
        {
            SchemaName = schemaName,
            TableName = tableName,
            Columns = columns,
            PrimaryKey = primaryKey,
            ForeignKeys = foreignKeys
        };
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
            columns.Add(new ColumnSchema
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetBoolean(2),
                IsIdentity = reader.GetBoolean(3),
                IsComputed = reader.GetBoolean(4),
                MaxLength = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                Precision = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                Scale = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                DefaultValue = reader.IsDBNull(8) ? null : reader.GetString(8),
                OrdinalPosition = reader.GetInt32(9)
            });
        }

        return columns;
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
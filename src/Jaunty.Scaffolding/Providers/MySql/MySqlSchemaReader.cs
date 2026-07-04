using System.Data;
using System.Data.Common;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Providers.MySql;

/// <summary>
/// Reads schema information from MySQL databases.
/// </summary>
public sealed class MySqlSchemaReader : ISchemaReader
{
    private const string TablesSql = @"
        SELECT
            '' AS SchemaName,
            TABLE_NAME AS TableName
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_TYPE = 'BASE TABLE'
        ORDER BY TABLE_NAME";

    private const string ColumnsSql = @"
        SELECT
            COLUMN_NAME AS ColumnName,
            DATA_TYPE AS DataType,
            IS_NULLABLE = 'YES' AS IsNullable,
            EXTRA LIKE '%auto_increment%' AS IsIdentity,
            GENERATION_EXPRESSION IS NOT NULL AND GENERATION_EXPRESSION != '' AS IsComputed,
            CHARACTER_MAXIMUM_LENGTH AS MaxLength,
            NUMERIC_PRECISION AS `Precision`,
            NUMERIC_SCALE AS Scale,
            COLUMN_DEFAULT AS DefaultValue,
            ORDINAL_POSITION AS OrdinalPosition
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName
        ORDER BY ORDINAL_POSITION";

    private const string PrimaryKeysSql = @"
        SELECT
            CONSTRAINT_NAME AS ConstraintName,
            COLUMN_NAME AS ColumnName,
            ORDINAL_POSITION AS KeyOrdinal
        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @TableName
          AND CONSTRAINT_NAME = 'PRIMARY'
        ORDER BY ORDINAL_POSITION";

    private const string ForeignKeysSql = @"
        SELECT
            CONSTRAINT_NAME AS ConstraintName,
            COLUMN_NAME AS ForeignKeyColumn,
            '' AS ReferencedSchema,
            REFERENCED_TABLE_NAME AS ReferencedTable,
            REFERENCED_COLUMN_NAME AS ReferencedColumn
        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @TableName
          AND REFERENCED_TABLE_NAME IS NOT NULL";

    /// <inheritdoc />
    public async Task<DatabaseSchema> ReadSchemaAsync(
        string connectionString,
        SchemaReaderOptions options,
        CancellationToken cancellationToken = default)
    {
        using DbConnection connection = CreateConnection(connectionString);
        await OpenConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var tables = new List<TableSchema>();
        List<string> tableNames = await GetTableNamesAsync(connection, options, cancellationToken).ConfigureAwait(false);

        foreach (var tableName in tableNames)
        {
            TableSchema tableSchema = await ReadTableSchemaAsync(connection, tableName, options, cancellationToken).ConfigureAwait(false);
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
        var connectionTypes = new[]
        {
            "MySqlConnector.MySqlConnection, MySqlConnector",
            "MySql.Data.MySqlClient.MySqlConnection, MySql.Data",
        };

        foreach (var typeName in connectionTypes)
        {
#pragma warning disable IL2057 // Type name is from trusted source list
            var type = Type.GetType(typeName);
#pragma warning restore IL2057
            if (type != null)
                return (DbConnection)Activator.CreateInstance(type, connectionString)!;
        }

        throw new InvalidOperationException("Could not find MySQL provider. Please install MySqlConnector or MySql.Data.");
    }

    private static async Task OpenConnectionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<string>> GetTableNamesAsync(
        DbConnection connection,
        SchemaReaderOptions options,
        CancellationToken cancellationToken)
    {
        var tables = new List<string>();

        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = TablesSql;

        using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var tableName = reader.GetString(1);

            if (options.IncludeTables?.Count > 0 &&
                !options.IncludeTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                continue;

            if (options.ExcludeTables?.Count > 0 &&
                options.ExcludeTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                continue;

            tables.Add(tableName);
        }

        return tables;
    }

    private static async Task<TableSchema> ReadTableSchemaAsync(
        DbConnection connection,
        string tableName,
        SchemaReaderOptions options,
        CancellationToken cancellationToken)
    {
        List<ColumnSchema> columns = await ReadColumnsAsync(connection, tableName, cancellationToken).ConfigureAwait(false);
        PrimaryKeyInfo? primaryKey = await ReadPrimaryKeyAsync(connection, tableName, cancellationToken).ConfigureAwait(false);
        List<ForeignKeyInfo> foreignKeys = options.IncludeForeignKeys
            ? await ReadForeignKeysAsync(connection, tableName, cancellationToken).ConfigureAwait(false)
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
            SchemaName = string.Empty,
            TableName = tableName,
            Columns = columns,
            PrimaryKey = primaryKey,
            ForeignKeys = foreignKeys
        };
    }

    private static async Task<List<ColumnSchema>> ReadColumnsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new List<ColumnSchema>();

        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = ColumnsSql;

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
                MaxLength = reader.IsDBNull(5) ? null : ToClampedInt32(reader.GetValue(5)),
                Precision = reader.IsDBNull(6) ? null : ToClampedInt32(reader.GetValue(6)),
                Scale = reader.IsDBNull(7) ? null : ToClampedInt32(reader.GetValue(7)),
                DefaultValue = reader.IsDBNull(8) ? null : reader.GetString(8),
                OrdinalPosition = ToClampedInt32(reader.GetValue(9))
            });
        }

        return columns;
    }

    /// <summary>
    /// Converts a numeric schema-metadata value (e.g. MySQL's unsigned BIGINT
    /// CHARACTER_MAXIMUM_LENGTH, which is 4294967295 for LONGTEXT/LONGBLOB) to an
    /// <see cref="int"/>, clamping to <see cref="int.MaxValue"/> instead of throwing
    /// <see cref="OverflowException"/> when the underlying value exceeds Int32 range.
    /// </summary>
    private static int ToClampedInt32(object value)
    {
        var decimalValue = Convert.ToDecimal(value);
        return decimalValue > int.MaxValue ? int.MaxValue : Convert.ToInt32(decimalValue);
    }

    private static async Task<PrimaryKeyInfo?> ReadPrimaryKeyAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = PrimaryKeysSql;

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
        string tableName,
        CancellationToken cancellationToken)
    {
        var foreignKeys = new List<ForeignKeyInfo>();

        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = ForeignKeysSql;

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
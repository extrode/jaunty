using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Providers.SQLite;

/// <summary>
/// Reads schema information from SQLite databases.
/// </summary>
public sealed class SQLiteSchemaReader : ISchemaReader
{
    /// <inheritdoc />
    public async Task<DatabaseSchema> ReadSchemaAsync(
        string connectionString,
        SchemaReaderOptions options,
        CancellationToken cancellationToken = default)
    {
        // Create connection using reflection to avoid compile-time dependency
        using var connection = CreateConnection(connectionString);
        await OpenConnectionAsync(connection, cancellationToken);

        var tables = new List<TableSchema>();
        var tableNames = await GetTableNamesAsync(connection, options, cancellationToken);

        foreach (var tableName in tableNames)
        {
            var tableSchema = await ReadTableSchemaAsync(connection, tableName, options, cancellationToken);
            tables.Add(tableSchema);
        }

        // Extract database name from connection string
        var databaseName = ExtractDatabaseName(connectionString);

        return new DatabaseSchema
        {
            DatabaseName = databaseName,
            Tables = tables
        };
    }

    private static DbConnection CreateConnection(string connectionString)
    {
        // Try to load Microsoft.Data.Sqlite first, then System.Data.SQLite
        var connectionTypes = new[]
        {
            ("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite", connectionString),
            ("System.Data.SQLite.SQLiteConnection, System.Data.SQLite", connectionString),
        };

        foreach (var (typeName, connStr) in connectionTypes)
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                var connection = (DbConnection)Activator.CreateInstance(type, connStr)!;
                return connection;
            }
        }

        throw new InvalidOperationException(
            "Could not find SQLite provider. Please install Microsoft.Data.Sqlite or System.Data.SQLite.");
    }

    private static async Task OpenConnectionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
    }

    private static async Task<List<string>> GetTableNamesAsync(
        DbConnection connection,
        SchemaReaderOptions options,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name";

        var tableNames = new List<string>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var tableName = reader.GetString(0);

            // Apply filters
            if (options.IncludeTables?.Count > 0 &&
                !options.IncludeTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                continue;

            if (options.ExcludeTables?.Count > 0 &&
                options.ExcludeTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                continue;

            tableNames.Add(tableName);
        }

        return tableNames;
    }

    private static async Task<TableSchema> ReadTableSchemaAsync(
        DbConnection connection,
        string tableName,
        SchemaReaderOptions options,
        CancellationToken cancellationToken)
    {
        var columns = await ReadColumnsAsync(connection, tableName, cancellationToken);
        var primaryKey = GetPrimaryKeyFromColumns(tableName, columns);
        var foreignKeys = options.IncludeForeignKeys
            ? await ReadForeignKeysAsync(connection, tableName, cancellationToken)
            : [];

        return new TableSchema
        {
            SchemaName = string.Empty, // SQLite doesn't have schemas
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

        // Get the CREATE TABLE statement to check for AUTOINCREMENT
        var createSql = await GetCreateTableSqlAsync(connection, tableName, cancellationToken);
        var hasAutoIncrement = createSql != null &&
            createSql.Contains("AUTOINCREMENT", StringComparison.OrdinalIgnoreCase);

        // Use PRAGMA table_info to get column information
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{tableName.Replace("'", "''")}')";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            // PRAGMA table_info returns: cid, name, type, notnull, dflt_value, pk
            var columnName = reader.GetString(1);
            var dataType = reader.IsDBNull(2) ? "TEXT" : reader.GetString(2);
            var notNull = reader.GetInt32(3) != 0;
            var defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4);
            var isPk = reader.GetInt32(5) != 0;

            // Check if this is an INTEGER PRIMARY KEY (which is an alias for ROWID)
            var isIdentity = isPk &&
                dataType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase) &&
                hasAutoIncrement;

            columns.Add(new ColumnSchema
            {
                ColumnName = columnName,
                DataType = dataType,
                IsNullable = !notNull && !isPk,
                IsPrimaryKey = isPk,
                IsIdentity = isIdentity,
                IsComputed = false, // SQLite doesn't have computed columns in the same way
                DefaultValue = defaultValue,
                OrdinalPosition = columns.Count + 1
            });
        }

        return columns;
    }

    private static async Task<string?> GetCreateTableSqlAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name = '{tableName.Replace("'", "''")}'";

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private static PrimaryKeyInfo? GetPrimaryKeyFromColumns(string tableName, List<ColumnSchema> columns)
    {
        var pkColumns = columns.Where(c => c.IsPrimaryKey).Select(c => c.ColumnName).ToList();

        if (pkColumns.Count == 0)
            return null;

        return new PrimaryKeyInfo
        {
            ConstraintName = $"pk_{tableName}",
            Columns = pkColumns
        };
    }

    private static async Task<List<ForeignKeyInfo>> ReadForeignKeysAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var foreignKeys = new List<ForeignKeyInfo>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA foreign_key_list('{tableName.Replace("'", "''")}')";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            // PRAGMA foreign_key_list returns: id, seq, table, from, to, on_update, on_delete, match
            var referencedTable = reader.GetString(2);
            var fromColumn = reader.GetString(3);
            var toColumn = reader.GetString(4);

            foreignKeys.Add(new ForeignKeyInfo
            {
                ConstraintName = $"fk_{tableName}_{fromColumn}",
                ForeignKeyColumn = fromColumn,
                ReferencedSchema = null,
                ReferencedTable = referencedTable,
                ReferencedColumn = toColumn
            });
        }

        return foreignKeys;
    }

    private static string ExtractDatabaseName(string connectionString)
    {
        // Try to extract database name from connection string
        var match = Regex.Match(connectionString, @"Data Source=([^;]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var path = match.Groups[1].Value;
            return Path.GetFileNameWithoutExtension(path);
        }

        return "SQLite";
    }
}

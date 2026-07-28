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
    // SQLite PRAGMA statements don't accept bind parameters for their argument in the common
    // ADO providers, so the table name must be interpolated into the command text. Table names
    // can legitimately contain spaces, quotes, and other punctuation when created via a quoted
    // identifier (see the "order's notes" fixture in SQLiteSchemaReaderTests), which rules out
    // restricting to a plain-identifier shape. Doubling embedded single quotes is the standard
    // SQL string-literal escape and is sufficient here since the argument is always wrapped in
    // single quotes below.
    private static string EscapeForPragmaLiteral(string tableName) => tableName.Replace("'", "''");

    /// <inheritdoc />
    public async Task<DatabaseSchema> ReadSchemaAsync(
        string connectionString,
        SchemaReaderOptions options,
        CancellationToken cancellationToken = default)
    {
        // Create connection using reflection to avoid compile-time dependency
        using DbConnection connection = CreateConnection(connectionString);
        await OpenConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var tables = new List<TableSchema>();
        List<string> tableNames = await GetTableNamesAsync(connection, options, cancellationToken).ConfigureAwait(false);

        foreach (var tableName in tableNames)
        {
            TableSchema tableSchema = await ReadTableSchemaAsync(connection, tableName, options, cancellationToken).ConfigureAwait(false);
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
        (string, string connectionString)[] connectionTypes = new[]
        {
            ("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite", connectionString),
            ("System.Data.SQLite.SQLiteConnection, System.Data.SQLite", connectionString),
        };

        foreach ((string? typeName, string? connStr) in connectionTypes)
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
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
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

        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
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
        (List<ColumnSchema> columns, List<string> keyColumnsInDeclarationOrder) =
            await ReadColumnsAsync(connection, tableName, cancellationToken).ConfigureAwait(false);
        PrimaryKeyInfo? primaryKey = BuildPrimaryKey(tableName, keyColumnsInDeclarationOrder);
        List<ForeignKeyInfo> foreignKeys = options.IncludeForeignKeys
            ? await ReadForeignKeysAsync(connection, tableName, cancellationToken).ConfigureAwait(false)
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

    /// <summary>
    /// Reads the table's columns, and alongside them the primary-key column names in
    /// *declaration* order. PRAGMA table_info reports rows in physical column order, so the
    /// two can differ (PRIMARY KEY (b, a) on a table declared (a, b)); the pk field carries
    /// the 1-based position within the key, which is what the ordering has to come from.
    /// </summary>
    private static async Task<(List<ColumnSchema> Columns, List<string> KeyColumns)> ReadColumnsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new List<ColumnSchema>();

        // Get the CREATE TABLE statement to check for WITHOUT ROWID, which suppresses
        // rowid aliasing for INTEGER PRIMARY KEY columns.
        var createSql = await GetCreateTableSqlAsync(connection, tableName, cancellationToken).ConfigureAwait(false);
        var isWithoutRowId = createSql != null &&
            Regex.IsMatch(createSql, @"\)\s*WITHOUT\s+ROWID\s*;?\s*$", RegexOptions.IgnoreCase);

        // Use PRAGMA table_info to get column information
        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{EscapeForPragmaLiteral(tableName)}')";

        var rows = new List<(string ColumnName, string DataType, bool NotNull, string? DefaultValue, int PkOrdinal)>();
        using (DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // PRAGMA table_info returns: cid, name, type, notnull, dflt_value, pk
                // pk is 0 for non-key columns and the 1-based position within the primary key
                // otherwise - keep the ordinal rather than collapsing it to a bool, so composite
                // keys can be reported in declaration order.
                var columnName = reader.GetString(1);
                var dataType = reader.IsDBNull(2) ? "TEXT" : reader.GetString(2);
                var notNull = reader.GetInt32(3) != 0;
                var defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4);
                var pkOrdinal = reader.GetInt32(5);
                rows.Add((columnName, dataType, notNull, defaultValue, pkOrdinal));
            }
        }

        var pkColumnCount = rows.Count(r => r.PkOrdinal != 0);

        var keyColumns = rows
            .Where(r => r.PkOrdinal != 0)
            .OrderBy(r => r.PkOrdinal)
            .Select(r => r.ColumnName)
            .ToList();

        foreach ((string columnName, string dataType, bool notNull, string? defaultValue, int pkOrdinal) in rows)
        {
            var isPk = pkOrdinal != 0;

            // A single-column INTEGER PRIMARY KEY is an alias for the SQLite rowid and is
            // always auto-generated, regardless of whether AUTOINCREMENT was specified.
            // Composite primary keys and WITHOUT ROWID tables don't get rowid aliasing.
            var isIdentity = isPk &&
                pkColumnCount == 1 &&
                dataType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase) &&
                !isWithoutRowId;

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

        return (columns, keyColumns);
    }

    private static async Task<string?> GetCreateTableSqlAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = @TableName";

        DbParameter tableNameParam = cmd.CreateParameter();
        tableNameParam.ParameterName = "@TableName";
        tableNameParam.Value = tableName;
        cmd.Parameters.Add(tableNameParam);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    /// <summary>
    /// Builds the primary key from the key columns already ordered by their PRAGMA table_info
    /// pk ordinal. Deriving the order by filtering the column list instead would report the
    /// key in physical column order, which the SqlServer/PostgreSql/MySql readers avoid by
    /// ordering on key_ordinal/ordinal_position from the constraint metadata.
    /// </summary>
    private static PrimaryKeyInfo? BuildPrimaryKey(string tableName, List<string> keyColumns)
    {
        if (keyColumns.Count == 0)
            return null;

        return new PrimaryKeyInfo
        {
            ConstraintName = $"pk_{tableName}",
            Columns = keyColumns
        };
    }

    private static async Task<List<ForeignKeyInfo>> ReadForeignKeysAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var foreignKeys = new List<ForeignKeyInfo>();

        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA foreign_key_list('{EscapeForPragmaLiteral(tableName)}')";

        using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
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
        Match match = Regex.Match(connectionString, @"Data Source=([^;]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var path = match.Groups[1].Value;
            return Path.GetFileNameWithoutExtension(path);
        }

        return "SQLite";
    }
}
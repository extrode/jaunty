using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Internals;
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
                return ReflectedConnectionFactory.Create(type, connStr);
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
        var isWithoutRowId = createSql != null && IsWithoutRowId(createSql);

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

    /// <summary>
    /// Reports whether a CREATE TABLE statement carries the WITHOUT ROWID table option.
    /// </summary>
    /// <remarks>
    /// AUD-R26: this was previously <c>Regex.IsMatch(sql, @"\)\s*WITHOUT\s+ROWID\s*;?\s*$")</c>,
    /// which requires the option to be the *only* thing after the column list and to sit
    /// immediately against the closing paren. SQLite accepts a comma-separated list of table
    /// options in either order, so both of these were misread as rowid tables:
    /// <code>
    /// CREATE TABLE t(...) STRICT, WITHOUT ROWID     -- ")" is not adjacent to WITHOUT
    /// CREATE TABLE t(...) WITHOUT ROWID, STRICT     -- ", STRICT" follows, so "$" does not match
    /// </code>
    /// The consequence is not cosmetic: a single-column INTEGER PRIMARY KEY in such a table
    /// was reported with <c>IsIdentity = true</c>, but WITHOUT ROWID tables get no rowid
    /// aliasing and never auto-generate the key, so the scaffolded entity tells callers to
    /// omit a value the database will not supply.
    ///
    /// <para>
    /// Relaxing the anchor to a bare <c>\bWITHOUT\s+ROWID\b</c> search over the whole statement
    /// is not sufficient either - it false-positives on <c>CREATE TABLE t(a INTEGER PRIMARY KEY,
    /// "without rowid" TEXT)</c>, which is a rowid table with an awkwardly named column. So the
    /// column-definition body is skipped by matching its parentheses (respecting <c>'..'</c>,
    /// <c>"..'"</c>, <c>`..`</c> and <c>[..]</c> quoting and both comment forms) and only the
    /// table-options tail that follows it is searched.
    /// </para>
    ///
    /// <para>
    /// Verified against SQLite itself - <c>SELECT rowid FROM t</c> fails with "no such column:
    /// rowid" exactly on WITHOUT ROWID tables - for the two option orderings above, the quoted
    /// -column trap, a lowercase spelling, a nested <c>CHECK (b IN (1,2,3))</c>, a column named
    /// <c>"weird)name"</c>, and a plain rowid table. This agrees with SQLite on all of them; the
    /// old pattern disagreed on two.
    /// </para>
    /// </remarks>
    internal static bool IsWithoutRowId(string createSql)
    {
        var depth = 0;
        var sawBody = false;
        var i = 0;

        while (i < createSql.Length)
        {
            char ch = createSql[i];

            // Quoted string literals and quoted identifiers. SQLite doubles the quote character
            // to escape it, so a doubled quote continues the run rather than ending it.
            if (ch is '\'' or '"' or '`')
            {
                char quote = ch;
                i++;
                while (i < createSql.Length)
                {
                    if (createSql[i] != quote)
                    {
                        i++;
                        continue;
                    }

                    if (i + 1 < createSql.Length && createSql[i + 1] == quote)
                    {
                        i += 2;
                        continue;
                    }

                    i++;
                    break;
                }

                continue;
            }

            if (ch == '[')
            {
                i++;
                while (i < createSql.Length && createSql[i] != ']')
                    i++;
                if (i < createSql.Length)
                    i++;
                continue;
            }

            if (ch == '-' && i + 1 < createSql.Length && createSql[i + 1] == '-')
            {
                while (i < createSql.Length && createSql[i] != '\n')
                    i++;
                continue;
            }

            if (ch == '/' && i + 1 < createSql.Length && createSql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < createSql.Length && !(createSql[i] == '*' && createSql[i + 1] == '/'))
                    i++;
                i = Math.Min(i + 2, createSql.Length);
                continue;
            }

            if (ch == '(')
            {
                depth++;
                sawBody = true;
                i++;
                continue;
            }

            if (ch == ')')
            {
                depth--;
                i++;

                // The column-definition body has closed; everything left is table options.
                if (sawBody && depth == 0)
                    return TailDeclaresWithoutRowId(createSql, i);

                continue;
            }

            i++;
        }

        return false;
    }

    /// <summary>
    /// Scans the table-options tail for the WITHOUT ROWID option, reading it as two bare
    /// keywords rather than as text. A regex over the raw tail would also match the option
    /// spelled inside a comment - <c>CREATE TABLE t (a INT) /* WITHOUT ROWID */</c> - which
    /// declares nothing.
    /// </summary>
    private static bool TailDeclaresWithoutRowId(string sql, int start)
    {
        var sawWithout = false;

        var i = start;
        while (i < sql.Length)
        {
            char ch = sql[i];

            if (ch == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n')
                    i++;
                continue;
            }

            if (ch == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                    i++;
                i = Math.Min(i + 2, sql.Length);
                continue;
            }

            if (!char.IsLetter(ch) && ch != '_')
            {
                i++;
                continue;
            }

            var wordStart = i;
            while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
                i++;

            ReadOnlySpan<char> word = sql.AsSpan(wordStart, i - wordStart);

            if (sawWithout && word.Equals("ROWID".AsSpan(), StringComparison.OrdinalIgnoreCase))
                return true;

            sawWithout = word.Equals("WITHOUT".AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        return false;
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

    /// <summary>
    /// Reads the table's foreign keys, resolving references that name no parent column.
    /// </summary>
    /// <remarks>
    /// AUD-R26: the <c>to</c> field of PRAGMA foreign_key_list is NULL whenever the constraint
    /// omits the parent column list - <c>REFERENCES parent</c> rather than
    /// <c>REFERENCES parent(id)</c> - which is ordinary, widely used SQLite. The previous
    /// <c>reader.GetString(4)</c> threw <c>InvalidOperationException: The data is NULL at
    /// ordinal 4</c> on the first such constraint, and since foreign keys are read inside the
    /// per-table loop of <see cref="ReadSchemaAsync"/>, that aborted the scaffold of the whole
    /// database, not just the one table.
    ///
    /// <para>
    /// Such a reference targets the parent's primary key, and for a composite key the
    /// <c>seq</c> field gives the position within it. Measured: for
    /// <c>p2(x TEXT, y TEXT, PRIMARY KEY (y, x))</c> and
    /// <c>FOREIGN KEY (a,b) REFERENCES p2</c>, SQLite reports seq=0 from=a and seq=1 from=b
    /// with both <c>to</c> values NULL, and the explicit spelling
    /// <c>REFERENCES p2(y,x)</c> reports to=y then to=x. So the parent key ordered by its
    /// PRAGMA table_info pk ordinal - which is declaration order, y then x, not the physical
    /// order x then y - indexed by seq reproduces the explicit form exactly.
    /// </para>
    /// </remarks>
    private static async Task<List<ForeignKeyInfo>> ReadForeignKeysAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var rows = new List<(int Seq, string ReferencedTable, string FromColumn, string? ToColumn)>();

        using (DbCommand cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA foreign_key_list('{EscapeForPragmaLiteral(tableName)}')";

            using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // PRAGMA foreign_key_list returns: id, seq, table, from, to, on_update, on_delete, match
                rows.Add((
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4)));
            }
        }

        // One lookup per distinct parent, not per row, so a composite implicit key costs a
        // single extra PRAGMA. Populated lazily - a schema whose references all name their
        // parent column issues no extra queries at all.
        Dictionary<string, List<string>>? parentKeys = null;

        var foreignKeys = new List<ForeignKeyInfo>(rows.Count);
        foreach ((int seq, string referencedTable, string fromColumn, string? toColumn) in rows)
        {
            var referencedColumn = toColumn;

            if (referencedColumn is null)
            {
                parentKeys ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                if (!parentKeys.TryGetValue(referencedTable, out List<string>? keyColumns))
                {
                    keyColumns = await GetPrimaryKeyColumnsAsync(connection, referencedTable, cancellationToken)
                        .ConfigureAwait(false);
                    parentKeys[referencedTable] = keyColumns;
                }

                // A parent with no primary key, or fewer key columns than the child names, is a
                // constraint SQLite itself rejects at DML time with "foreign key mismatch" - there
                // is no column to point at, so the row is dropped rather than reported with an
                // invented target.
                if (seq >= keyColumns.Count)
                    continue;

                referencedColumn = keyColumns[seq];
            }

            foreignKeys.Add(new ForeignKeyInfo
            {
                ConstraintName = $"fk_{tableName}_{fromColumn}",
                ForeignKeyColumn = fromColumn,
                ReferencedSchema = null,
                ReferencedTable = referencedTable,
                ReferencedColumn = referencedColumn
            });
        }

        return foreignKeys;
    }

    /// <summary>
    /// Returns a table's primary-key columns in declaration order - the order given by the
    /// PRAGMA table_info pk ordinal, which for <c>PRIMARY KEY (y, x)</c> is y then x however
    /// the columns are physically laid out.
    /// </summary>
    private static async Task<List<string>> GetPrimaryKeyColumnsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var keyColumns = new List<(int Ordinal, string ColumnName)>();

        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{EscapeForPragmaLiteral(tableName)}')";

        using DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var pkOrdinal = reader.GetInt32(5);
            if (pkOrdinal != 0)
                keyColumns.Add((pkOrdinal, reader.GetString(1)));
        }

        keyColumns.Sort(static (a, b) => a.Ordinal.CompareTo(b.Ordinal));

        var result = new List<string>(keyColumns.Count);
        foreach ((int _, string columnName) in keyColumns)
            result.Add(columnName);

        return result;
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
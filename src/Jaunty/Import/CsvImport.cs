using System.Data;
using System.Data.Common;
using System.Diagnostics;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Internals;

namespace Jaunty;

/// <summary>
/// Extension methods for importing CSV files using native database engine bulk import commands.
/// Each database uses its fastest native import mechanism:
/// - SQLite: sqlite3 CLI .import command
/// - PostgreSQL: COPY FROM STDIN
/// - MySQL/MariaDB: LOAD DATA LOCAL INFILE
/// - SQL Server: BULK INSERT
/// </summary>
public static class CsvImportExtensions
{
    /// <summary>
    /// Imports a CSV file into the specified table using the database engine's native bulk import.
    /// This is the fastest possible import method as it bypasses ORM overhead entirely.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="tableName">The target table name.</param>
    /// <param name="filePath">The absolute path to the CSV file.</param>
    /// <param name="options">Optional import configuration.</param>
    /// <returns>The number of rows imported (approximate for some engines).</returns>
    public static long ImportCsv(this IDbConnection connection, string tableName, string filePath, CsvImportOptions? options = null)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required.", nameof(tableName));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required.", nameof(filePath));
#endif

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV file not found: {filePath}", filePath);

        ValidateIdentifier(tableName, nameof(tableName));

        options ??= new CsvImportOptions();

        ValidateDelimiter(options.Delimiter);

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
        return dialect switch
        {
            SQLiteDialect => ImportSqlite(connection, tableName, filePath, options),
            PostgreSqlDialect => ImportPostgreSql(connection, tableName, filePath, options),
            MySqlDialect => ImportMySql(connection, tableName, filePath, options),
            SqlServerDialect => ImportSqlServer(connection, tableName, filePath, options),
            _ => throw new NotSupportedException($"CSV import is not supported for connection type: {connection.GetType().Name}")
        };
    }

    /// <summary>
    /// Imports a CSV file into the specified table using the database engine's native bulk import (async).
    /// </summary>
    public static async ValueTask<long> ImportCsvAsync(this DbConnection connection, string tableName, string filePath, CsvImportOptions? options = null, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required.", nameof(tableName));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required.", nameof(filePath));
#endif

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV file not found: {filePath}", filePath);

        ValidateIdentifier(tableName, nameof(tableName));

        options ??= new CsvImportOptions();

        ValidateDelimiter(options.Delimiter);

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
        return dialect switch
        {
            SQLiteDialect => await ImportSqliteAsync(connection, tableName, filePath, options, cancellationToken).ConfigureAwait(false),
            PostgreSqlDialect => await ImportPostgreSqlAsync(connection, tableName, filePath, options, cancellationToken).ConfigureAwait(false),
            MySqlDialect => await ImportMySqlAsync(connection, tableName, filePath, options, cancellationToken).ConfigureAwait(false),
            SqlServerDialect => await ImportSqlServerAsync(connection, tableName, filePath, options, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"CSV import is not supported for connection type: {connection.GetType().Name}")
        };
    }

    // =============================================
    // SQLite: Use sqlite3 CLI .import command
    // =============================================

    private static long ImportSqlite(IDbConnection connection, string tableName, string filePath, CsvImportOptions options)
    {
        // Get the database file path from the connection string
        string? dbPath = ExtractSqliteDbPath(connection.ConnectionString);
        if (dbPath is null || IsSqliteInMemoryDataSource(dbPath))
        {
            // For in-memory databases (including shared-cache forms like
            // "file::memory:?cache=shared"), fall back to prepared statement insert - the
            // sqlite3 CLI would otherwise treat the connection string as a real file path.
            return ImportViaPreparedStatements(connection, tableName, filePath, options);
        }

        // Use sqlite3 CLI for file-based databases
        return ImportViaSqliteCli(dbPath, tableName, filePath, options);
    }

    private static bool IsSqliteInMemoryDataSource(string dbPath)
    {
        // ":memory:" is a reserved SQLite keyword that can't legitimately appear in a real file
        // path, so a substring match also catches shared-cache forms such as
        // "file::memory:?cache=shared" that a literal ":memory:" comparison would miss.
        return dbPath.IndexOf(":memory:", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static async ValueTask<long> ImportSqliteAsync(DbConnection connection, string tableName, string filePath, CsvImportOptions options, CancellationToken cancellationToken)
    {
        // sqlite3 CLI is process-based; offload the blocking call to a thread-pool thread so the
        // calling async thread isn't blocked for the duration of the CLI process.
        return await Task.Run(() => ImportSqlite(connection, tableName, filePath, options), cancellationToken).ConfigureAwait(false);
    }

    private static long ImportViaSqliteCli(string dbPath, string tableName, string filePath, CsvImportOptions options)
    {
        // dbPath comes from the connection's own connection string ("Data Source=..."), unvalidated,
        // and is interpolated directly into the sqlite3 CLI process's command-line arguments; a quote
        // would let it break out of the quoted argument and inject additional CLI switches.
        if (dbPath.IndexOfAny(SqliteCliUnsafeChars) >= 0)
            throw new ArgumentException($"Database path contains characters that are not supported by the sqlite3 CLI: {dbPath}", nameof(dbPath));

        // tableName is already validated by ValidateIdentifier in the public entry point, but still
        // needs dialect escaping/quoting here to match ImportViaPreparedStatements' behavior for
        // keyword-collision table names (e.g. "GROUP") - the sqlite3 CLI's dot-command tokenizer
        // accepts double-quoted arguments the same way ImportViaPreparedStatements' SQL does.
        string escapedTableName = new SQLiteDialect().EscapeTableName(null, tableName);

        // filePath comes from the caller and is embedded verbatim in the sqlite3 CLI's dot-command
        // script (piped over stdin); a quote or newline would let it break out of the quoted argument
        // and inject arbitrary dot-commands (e.g. ".system") into the sqlite3 CLI process.
        if (filePath.IndexOfAny(SqliteCliUnsafeChars) >= 0)
            throw new ArgumentException($"File path contains characters that are not supported by the sqlite3 CLI import command: {filePath}", nameof(filePath));

        // The sqlite3 CLI's csv mode always quotes with double-quote; there's no dot-command to
        // override it, unlike the other providers' native import commands.
        if (options.Quote != '"')
            throw new NotSupportedException("CsvImportOptions.Quote is not supported by the sqlite3 CLI import path; the CLI's CSV mode always uses '\"' as the quote character.");

        // The sqlite3 CLI's ".nullvalue STRING" dot-command only affects output formatting
        // (e.g. how NULL is rendered by SELECT/.mode); ".import" does not consult it at all, so a
        // matching field is imported as literal text rather than converted to NULL (verified against
        // the sqlite3 CLI directly - see AUD-R11 batch-04). Fail loudly instead of silently
        // importing the sentinel as literal text.
        ThrowIfNullValueUnsupported(options, "sqlite3 CLI import");

        // Build sqlite3 commands
        var commands = new StringBuilder();
        commands.AppendLine(".mode csv");

        if (options.Delimiter != ',')
            commands.AppendLine($".separator \"{options.Delimiter}\"");

        if (options.HasHeader)
            commands.AppendLine($".import --skip 1 \"{filePath.Replace("\\", "/")}\" {escapedTableName}");
        else
            commands.AppendLine($".import \"{filePath.Replace("\\", "/")}\" {escapedTableName}");

        var psi = new ProcessStartInfo
        {
            FileName = "sqlite3",
            Arguments = $"\"{dbPath}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start sqlite3 CLI. Ensure sqlite3 is installed and on PATH.");

        process.StandardInput.Write(commands.ToString());
        process.StandardInput.Close();

        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"sqlite3 import failed (exit code {process.ExitCode}): {stderr}");

        // sqlite3 doesn't report row count; count lines in file minus header
        return CountCsvRows(filePath, options.HasHeader);
    }

    private static long ImportViaPreparedStatements(IDbConnection connection, string tableName, string filePath, CsvImportOptions options)
    {
        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

        // Read the first physical line to determine the column layout. When the CSV has a header row
        // its fields are the column names. When it does not, the first line is a data row: we insert
        // positionally (no column list) so the first row is not consumed as SQL identifiers, and it is
        // still imported as an ordinary data row by the streaming loop below (which only skips the
        // first line when HasHeader is true).
        string[]? headers;
        int columnCount;
        using (var reader = new StreamReader(filePath, options.Encoding))
        {
            string? firstLine = reader.ReadLine();
            if (firstLine == null)
                return 0;

            string[] firstFields = ParseCsvLine(firstLine, options.Delimiter, options.Quote);
            columnCount = firstFields.Length;
            headers = options.HasHeader ? firstFields : null;
        }

        // Build INSERT statement. Table and column identifiers are escaped through the dialect, which
        // validates them and quotes any that collide with a SQL keyword; anything that is not a bare
        // identifier is rejected before it can reach the command text.
        var sb = new StringBuilder();
        sb.Append("INSERT INTO ");
        sb.Append(EscapeQualifiedTableName(dialect, tableName));
        if (headers != null)
        {
            sb.Append(" (");
            for (int i = 0; i < headers.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(dialect.EscapeColumnName(headers[i]));
            }
            sb.Append(')');
        }
        sb.Append(" VALUES (");
        for (int i = 0; i < columnCount; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"@p{i}");
        }
        sb.Append(')');
        string insertSql = sb.ToString();

        bool wasClosed = connection.State == ConnectionState.Closed;
        long rowCount = 0;

        try
        {
            if (wasClosed) connection.Open();

            using IDbTransaction transaction = connection.BeginTransaction();
            using IDbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = insertSql;

            // Create parameters
            var parameters = new IDbDataParameter[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                IDbDataParameter param = command.CreateParameter();
                param.ParameterName = $"@p{i}";
                command.Parameters.Add(param);
                parameters[i] = param;
            }

            // Stream rows
            using var streamReader = new StreamReader(filePath, options.Encoding);

            // Skip header
            if (options.HasHeader)
                streamReader.ReadLine();

            string? line;
            while ((line = streamReader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] values = ParseCsvLine(line, options.Delimiter, options.Quote);

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i >= values.Length)
                    {
                        // Row has fewer fields than the header; without this, a parameter would keep
                        // whatever value the previous row left in it instead of representing a missing field.
                        parameters[i].Value = DBNull.Value;
                        continue;
                    }

                    string val = values[i];
                    if (options.NullValue != null && val == options.NullValue)
                        parameters[i].Value = DBNull.Value;
                    else
                        parameters[i].Value = val;
                }

                command.ExecuteNonQuery();
                rowCount++;
            }

            transaction.Commit();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }

        return rowCount;
    }

    // =============================================
    // PostgreSQL: COPY FROM STDIN
    // =============================================

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Npgsql BeginTextImport feature probe on the runtime connection type; the null check below handles a trimmed or non-Npgsql connection by falling back to server-side COPY FROM.")]
#endif
    private static long ImportPostgreSql(IDbConnection connection, string tableName, string filePath, CsvImportOptions options)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) connection.Open();

            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
            string escapedTable = EscapeQualifiedTableName(dialect, tableName);

            // Use COPY ... FROM STDIN via raw SQL (works with all Npgsql versions)
            string copyCommand = $"COPY {escapedTable} FROM STDIN WITH (FORMAT csv, HEADER {(options.HasHeader ? "true" : "false")}, DELIMITER '{options.Delimiter}'{BuildPostgresCopyExtraOptions(options)})";

            // Use reflection to call BeginTextImport on NpgsqlConnection

            // AOT-SAFE: Npgsql feature probe on the runtime connection type; null (trimmed or non-Npgsql) falls back to server-side COPY FROM below
            MethodInfo? beginTextImport = connection.GetType().GetMethod("BeginTextImport", new[] { typeof(string) });
            if (beginTextImport != null)
            {
                using var writer = (IDisposable)beginTextImport.Invoke(connection, new object[] { copyCommand })!;
                var textWriter = (TextWriter)writer;

                using var fileReader = new StreamReader(filePath, options.Encoding);
                string? line;
                while ((line = fileReader.ReadLine()) != null)
                {
                    textWriter.WriteLine(line);
                }

                return CountCsvRows(filePath, options.HasHeader);
            }

            // Fallback: Use COPY FROM with file path (requires server access to file)
            using IDbCommand cmd = connection.CreateCommand();
            cmd.CommandText = $"COPY {escapedTable} FROM '{filePath.Replace("'", "''")}' WITH (FORMAT csv, HEADER {(options.HasHeader ? "true" : "false")}, DELIMITER '{options.Delimiter}'{BuildPostgresCopyExtraOptions(options)})";
            return cmd.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Npgsql BeginTextImport feature probe on the runtime connection type; the null check below handles a trimmed or non-Npgsql connection by falling back to server-side COPY FROM.")]
#endif
    private static async ValueTask<long> ImportPostgreSqlAsync(DbConnection connection, string tableName, string filePath, CsvImportOptions options, CancellationToken cancellationToken)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
            string escapedTable = EscapeQualifiedTableName(dialect, tableName);

            string copyCommand = $"COPY {escapedTable} FROM STDIN WITH (FORMAT csv, HEADER {(options.HasHeader ? "true" : "false")}, DELIMITER '{options.Delimiter}'{BuildPostgresCopyExtraOptions(options)})";

            // AOT-SAFE: Npgsql feature probe on the runtime connection type; null (trimmed or non-Npgsql) falls back to server-side COPY FROM below
            MethodInfo? beginTextImport = connection.GetType().GetMethod("BeginTextImport", new[] { typeof(string) });
            if (beginTextImport != null)
            {
                using var writer = (IDisposable)beginTextImport.Invoke(connection, new object[] { copyCommand })!;
                var textWriter = (TextWriter)writer;

                using var fileReader = new StreamReader(filePath, options.Encoding);
                string? line;
                while ((line = await fileReader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    await textWriter.WriteLineAsync(line).ConfigureAwait(false);
                }

                return CountCsvRows(filePath, options.HasHeader);
            }

            using DbCommand cmd = connection.CreateCommand();
            cmd.CommandText = $"COPY {escapedTable} FROM '{filePath.Replace("'", "''")}' WITH (FORMAT csv, HEADER {(options.HasHeader ? "true" : "false")}, DELIMITER '{options.Delimiter}'{BuildPostgresCopyExtraOptions(options)})";
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }

    // =============================================
    // MySQL/MariaDB: LOAD DATA LOCAL INFILE
    // =============================================

    private static long ImportMySql(IDbConnection connection, string tableName, string filePath, CsvImportOptions options)
    {
        ThrowIfNullValueUnsupported(options, "MySQL/MariaDB LOAD DATA");

        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) connection.Open();

            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
            string escapedTable = EscapeQualifiedTableName(dialect, tableName);

            using IDbCommand cmd = connection.CreateCommand();
            var sb = new StringBuilder();
            // MySQL string literals treat backslash as an escape introducer unless the server's
            // sql_mode includes NO_BACKSLASH_ESCAPES, and which mode is active isn't known here.
            // Converting to forward slashes sidesteps the ambiguity entirely (matches ImportViaSqliteCli's
            // filePath handling above): LOCAL INFILE reads the file client-side via the .NET file APIs,
            // which accept '/' as a path separator on Windows too, so no OS-level meaning is lost. The
            // remaining quote-doubling is SQL-standard escaping, safe under any sql_mode.
            sb.Append($"LOAD DATA LOCAL INFILE '{filePath.Replace("\\", "/").Replace("'", "''")}' ");
            sb.Append($"INTO TABLE {escapedTable} ");
            sb.Append($"FIELDS TERMINATED BY '{options.Delimiter}' ");
            sb.Append($"OPTIONALLY ENCLOSED BY '{EscapeSqlCharLiteral(options.Quote)}' ");
            sb.Append("LINES TERMINATED BY '\\n' ");
            if (options.HasHeader)
                sb.Append("IGNORE 1 LINES");

            cmd.CommandText = sb.ToString();
            return cmd.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    private static async ValueTask<long> ImportMySqlAsync(DbConnection connection, string tableName, string filePath, CsvImportOptions options, CancellationToken cancellationToken)
    {
        ThrowIfNullValueUnsupported(options, "MySQL/MariaDB LOAD DATA");

        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
            string escapedTable = EscapeQualifiedTableName(dialect, tableName);

            using DbCommand cmd = connection.CreateCommand();
            var sb = new StringBuilder();
            // MySQL string literals treat backslash as an escape introducer unless the server's
            // sql_mode includes NO_BACKSLASH_ESCAPES, and which mode is active isn't known here.
            // Converting to forward slashes sidesteps the ambiguity entirely (matches ImportViaSqliteCli's
            // filePath handling above): LOCAL INFILE reads the file client-side via the .NET file APIs,
            // which accept '/' as a path separator on Windows too, so no OS-level meaning is lost. The
            // remaining quote-doubling is SQL-standard escaping, safe under any sql_mode.
            sb.Append($"LOAD DATA LOCAL INFILE '{filePath.Replace("\\", "/").Replace("'", "''")}' ");
            sb.Append($"INTO TABLE {escapedTable} ");
            sb.Append($"FIELDS TERMINATED BY '{options.Delimiter}' ");
            sb.Append($"OPTIONALLY ENCLOSED BY '{EscapeSqlCharLiteral(options.Quote)}' ");
            sb.Append("LINES TERMINATED BY '\\n' ");
            if (options.HasHeader)
                sb.Append("IGNORE 1 LINES");

            cmd.CommandText = sb.ToString();
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }

    // =============================================
    // SQL Server: BULK INSERT
    // =============================================

    private static long ImportSqlServer(IDbConnection connection, string tableName, string filePath, CsvImportOptions options)
    {
        ThrowIfNullValueUnsupported(options, "SQL Server BULK INSERT");

        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) connection.Open();

            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
            string escapedTable = EscapeQualifiedTableName(dialect, tableName);

            using IDbCommand cmd = connection.CreateCommand();
            var sb = new StringBuilder();
            sb.Append($"BULK INSERT {escapedTable} FROM '{filePath.Replace("'", "''")}' ");
            sb.Append("WITH (");
            sb.Append($"FIELDTERMINATOR = '{options.Delimiter}', ");
            sb.Append("ROWTERMINATOR = '\\n', ");
            sb.Append("TABLOCK, ");
            if (options.HasHeader)
                sb.Append("FIRSTROW = 2, ");
            sb.Append("FORMAT = 'CSV'");
            if (options.Quote != '"')
                sb.Append($", FIELDQUOTE = '{EscapeSqlCharLiteral(options.Quote)}'");
            sb.Append(')');

            cmd.CommandText = sb.ToString();
            return cmd.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    private static async ValueTask<long> ImportSqlServerAsync(DbConnection connection, string tableName, string filePath, CsvImportOptions options, CancellationToken cancellationToken)
    {
        ThrowIfNullValueUnsupported(options, "SQL Server BULK INSERT");

        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
            string escapedTable = EscapeQualifiedTableName(dialect, tableName);

            using DbCommand cmd = connection.CreateCommand();
            var sb = new StringBuilder();
            sb.Append($"BULK INSERT {escapedTable} FROM '{filePath.Replace("'", "''")}' ");
            sb.Append("WITH (");
            sb.Append($"FIELDTERMINATOR = '{options.Delimiter}', ");
            sb.Append("ROWTERMINATOR = '\\n', ");
            sb.Append("TABLOCK, ");
            if (options.HasHeader)
                sb.Append("FIRSTROW = 2, ");
            sb.Append("FORMAT = 'CSV'");
            if (options.Quote != '"')
                sb.Append($", FIELDQUOTE = '{EscapeSqlCharLiteral(options.Quote)}'");
            sb.Append(')');

            cmd.CommandText = sb.ToString();
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }

    // =============================================
    // Helpers
    // =============================================
    // Only letters, digits, and underscore, optionally schema-qualified (schema.table). This is
    // intentionally strict: tableName and CSV header column names are interpolated directly into
    // raw SQL (COPY/LOAD DATA/BULK INSERT/INSERT) or into the sqlite3 CLI's dot-command script, and
    // none of those contexts support parameterizing identifiers.
    private static readonly Regex ValidIdentifierPattern =
        new(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.Compiled);

    private static readonly char[] SqliteCliUnsafeChars = { '"', '\r', '\n' };

    private static void ValidateIdentifier(string identifier, string paramName)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !ValidIdentifierPattern.IsMatch(identifier))
            throw new ArgumentException($"'{identifier}' is not a valid SQL identifier.", paramName);
    }

    // Escapes a possibly schema-qualified table name through the dialect. The dialect validates each
    // segment and quotes any that collide with a SQL keyword, so a keyword table name stays usable
    // while an injection-shaped name is rejected instead of being interpolated raw.
    private static string EscapeQualifiedTableName(ISqlDialect dialect, string tableName)
    {
        int dot = tableName.IndexOf('.');
        if (dot >= 0)
            return dialect.EscapeTableName(tableName.Substring(0, dot), tableName.Substring(dot + 1));

        return dialect.EscapeTableName(null, tableName);
    }

    private static void ValidateDelimiter(char delimiter)
    {
        if (delimiter is '\'' or '"' or '\\' or '\r' or '\n')
            throw new ArgumentException($"Delimiter '{delimiter}' is not supported; it conflicts with SQL/CLI quoting.", nameof(delimiter));
    }

    // Doubles a single-quote so a single character can be embedded in a single-quoted SQL string
    // literal (e.g. QUOTE '''' for a literal apostrophe quote character); any other character is
    // already safe to embed as-is.
    private static string EscapeSqlCharLiteral(char c) => c == '\'' ? "''" : c.ToString();

    // Postgres COPY's WITH (...) clause natively supports NULL '<value>' and QUOTE '<char>', unlike
    // the other providers' native import commands, so these can be applied directly instead of
    // silently ignored.
    private static string BuildPostgresCopyExtraOptions(CsvImportOptions options)
    {
        var extra = new StringBuilder();
        if (options.NullValue != null)
            extra.Append($", NULL '{options.NullValue.Replace("'", "''")}'");
        if (options.Quote != '"')
            extra.Append($", QUOTE '{EscapeSqlCharLiteral(options.Quote)}'");

        return extra.ToString();
    }

    // MySQL's LOAD DATA and SQL Server's BULK INSERT have no clause for substituting an arbitrary
    // string as NULL (unlike Postgres COPY's NULL option or the sqlite3 CLI's .nullvalue), so rather
    // than silently importing the sentinel as literal text, fail loudly if a caller configured one.
    private static void ThrowIfNullValueUnsupported(CsvImportOptions options, string importMethodName)
    {
        if (options.NullValue != null)
            throw new NotSupportedException(
                $"CsvImportOptions.NullValue is not supported by the {importMethodName} import path. " +
                "Leave NullValue unset, or import into an in-memory SQLite database (which uses the prepared-statement fallback and honors NullValue).");
    }

    private static string? ExtractSqliteDbPath(string connectionString)
    {
        // Parse "Data Source=path" from connection string
        foreach (string part in connectionString.Split(';'))
        {
            string trimmed = part.Trim();
            if (trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring("Data Source=".Length).Trim();
            }
        }
        return null;
    }

    private static long CountCsvRows(string filePath, bool hasHeader)
    {
        long count = 0;
        using var reader = new StreamReader(filePath);
        while (reader.ReadLine() != null)
            count++;

        if (hasHeader && count > 0)
            count--;

        return count;
    }

    internal static string[] ParseCsvLine(string line, char delimiter, char quote)
    {
        var fields = new List<string>(JauntyConfig.CsvFieldCapacity);
        int i = 0;
        int len = line.Length;

        if (len == 0)
        {
            fields.Add(string.Empty);
            return fields.ToArray();
        }

        while (i < len)
        {
            if (line[i] == quote)
            {
                // Quoted field
                var sb = new StringBuilder();
                i++; // skip opening quote
                while (i < len)
                {
                    if (line[i] == quote)
                    {
                        if (i + 1 < len && line[i + 1] == quote)
                        {
                            sb.Append(quote);
                            i += 2;
                        }
                        else
                        {
                            i++; // skip closing quote
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                }
                fields.Add(sb.ToString());

                // Skip delimiter after quoted field
                if (i < len && line[i] == delimiter)
                {
                    i++;
                    // Trailing delimiter means one more empty field
                    if (i == len)
                        fields.Add(string.Empty);
                }
            }
            else
            {
                // Unquoted field
                int start = i;
                while (i < len && line[i] != delimiter)
                    i++;
                fields.Add(line.Substring(start, i - start));
                if (i < len)
                {
                    i++; // skip delimiter
                    // Trailing delimiter means one more empty field
                    if (i == len)
                        fields.Add(string.Empty);
                }
            }
        }

        return fields.ToArray();
    }
}
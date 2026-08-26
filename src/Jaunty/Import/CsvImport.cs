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
    /// <remarks>
    /// <para>
    /// <b>Which machine reads the file depends on the engine.</b> <paramref name="filePath"/> is
    /// not resolved in one place, and the difference decides whether a path on the calling machine
    /// is the right thing to pass:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>SQLite</b> - read <b>here</b>, by the sqlite3 CLI for a file-backed database or by
    /// Jaunty's own prepared-statement fallback for an in-memory one.
    /// </description></item>
    /// <item><description>
    /// <b>MySQL/MariaDB</b> - read <b>here</b>. <c>LOAD DATA LOCAL INFILE</c> streams the file from
    /// the client driver, which requires the server to permit <c>local_infile</c>.
    /// </description></item>
    /// <item><description>
    /// <b>PostgreSQL</b> - read <b>here</b> when Npgsql exposes <c>BeginTextImport</c>, which
    /// streams <c>COPY ... FROM STDIN</c>. Where that is unavailable - a non-Npgsql provider, or a
    /// trimmed build - it falls back to server-side <c>COPY ... FROM '&lt;path&gt;'</c>, and the
    /// path is then the <b>server's</b>.
    /// </description></item>
    /// <item><description>
    /// <b>SQL Server</b> - read <b>on the database server</b>, always. <c>BULK INSERT ... FROM</c>
    /// resolves the path on the server host, under the account SQL Server runs as, and the caller
    /// additionally needs <c>ADMINISTER BULK OPERATIONS</c> (or <c>bulkadmin</c>). A path from the
    /// calling machine is meaningful only when the two are the same host. To import a file that
    /// lives here, use <c>BulkInsert&lt;T&gt;</c> instead.
    /// </description></item>
    /// </list>
    /// <para>
    /// AUD-R26 (batch 4, medium/consistency). None of this was documented, and the file-existence
    /// precondition was applied to all four engines before dispatch - so against a remote server it
    /// passed and gave false assurance, while a file that existed <em>on the server</em> and not
    /// here was rejected outright, making the server-side capability unreachable. The check now runs
    /// only on the paths that read the file here, and a server-side failure reports which machine
    /// resolved the path.
    /// </para>
    /// </remarks>
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

        ValidateIdentifier(tableName, nameof(tableName));

        options ??= new CsvImportOptions();

        ValidateDelimiter(options.Delimiter);
        ValidateQuote(options.Quote);
        ValidateDelimiterQuoteDistinct(options.Delimiter, options.Quote);
        ValidateEncoding(options.Encoding);

        // Unwrap before the type test: SqlDialectFactory.GetDialect runs every dialect through the
        // bulk-copy enhancement step, which after UseNativeBulkCopy() substitutes a wrapper that
        // implements ISqlDialect rather than deriving from the engine dialect - so a direct type
        // test would fall through to the NotSupportedException arm for *every* supported engine.
        ISqlDialect dialect = SqlDialectFactory.Unwrap(SqlDialectFactory.GetDialect(connection));
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
    /// <remarks>
    /// <para>
    /// <b>Which machine reads the file depends on the engine.</b> <paramref name="filePath"/> is
    /// not resolved in one place, and the difference decides whether a path on the calling machine
    /// is the right thing to pass:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>SQLite</b> - read <b>here</b>, by the sqlite3 CLI for a file-backed database or by
    /// Jaunty's own prepared-statement fallback for an in-memory one.
    /// </description></item>
    /// <item><description>
    /// <b>MySQL/MariaDB</b> - read <b>here</b>. <c>LOAD DATA LOCAL INFILE</c> streams the file from
    /// the client driver, which requires the server to permit <c>local_infile</c>.
    /// </description></item>
    /// <item><description>
    /// <b>PostgreSQL</b> - read <b>here</b> when Npgsql exposes <c>BeginTextImport</c>, which
    /// streams <c>COPY ... FROM STDIN</c>. Where that is unavailable - a non-Npgsql provider, or a
    /// trimmed build - it falls back to server-side <c>COPY ... FROM '&lt;path&gt;'</c>, and the
    /// path is then the <b>server's</b>.
    /// </description></item>
    /// <item><description>
    /// <b>SQL Server</b> - read <b>on the database server</b>, always. <c>BULK INSERT ... FROM</c>
    /// resolves the path on the server host, under the account SQL Server runs as, and the caller
    /// additionally needs <c>ADMINISTER BULK OPERATIONS</c> (or <c>bulkadmin</c>). A path from the
    /// calling machine is meaningful only when the two are the same host. To import a file that
    /// lives here, use <c>BulkInsert&lt;T&gt;</c> instead.
    /// </description></item>
    /// </list>
    /// <para>
    /// AUD-R26 (batch 4, medium/consistency). None of this was documented, and the file-existence
    /// precondition was applied to all four engines before dispatch - so against a remote server it
    /// passed and gave false assurance, while a file that existed <em>on the server</em> and not
    /// here was rejected outright, making the server-side capability unreachable. The check now runs
    /// only on the paths that read the file here, and a server-side failure reports which machine
    /// resolved the path.
    /// </para>
    /// </remarks>
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

        ValidateIdentifier(tableName, nameof(tableName));

        options ??= new CsvImportOptions();

        ValidateDelimiter(options.Delimiter);
        ValidateQuote(options.Quote);
        ValidateDelimiterQuoteDistinct(options.Delimiter, options.Quote);
        ValidateEncoding(options.Encoding);

        // See the sync overload: unwrap before the type test so the bulk-copy wrapper doesn't
        // make every supported engine fall through to NotSupportedException.
        ISqlDialect dialect = SqlDialectFactory.Unwrap(SqlDialectFactory.GetDialect(connection));
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
        RequireFileOnThisMachine(filePath);

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
        string escapedTableName = EscapeQualifiedTableName(new SQLiteDialect(), tableName);

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
        ThrowIfEncodingUnsupported(options, "sqlite3 CLI import");

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
        return CountCsvRows(filePath, options.HasHeader, options.Quote, options.Encoding);
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
            string? firstLine = ReadCsvRecord(reader, options.Quote);
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
                ReadCsvRecord(streamReader, options.Quote);

            string? line;
            long recordNumber = options.HasHeader ? 1 : 0;
            while ((line = ReadCsvRecord(streamReader, options.Quote)) != null)
            {
                recordNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] values = ParseCsvLine(line, options.Delimiter, options.Quote);

                // A row with *more* fields than the layout row would otherwise be truncated to
                // parameters.Length and the surplus dropped without a word - the opposite of the
                // deliberately-safe DBNull handling for short rows below. Malformed input (an
                // unescaped delimiter inside an unquoted value, say) must not import as a
                // silently-partial row. The enclosing transaction is rolled back on the way out.
                if (values.Length > parameters.Length)
                    throw new InvalidDataException(
                        $"CSV record {recordNumber} in '{filePath}' has {values.Length} fields but the " +
                        $"{(options.HasHeader ? "header" : "first record")} defines {parameters.Length}. " +
                        "Extra fields would be discarded - fix the record, or quote values that contain the delimiter.");

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
                // This branch streams the file from here, so its absence here is an error Jaunty
                // can diagnose. The server-side fallback below is not gated on it - see
                // RequireFileOnThisMachine.
                RequireFileOnThisMachine(filePath);

                using var writer = (IDisposable)beginTextImport.Invoke(connection, new object[] { copyCommand })!;
                var textWriter = (TextWriter)writer;

                try
                {
                    // AUD-R34-010: this was a ReadLine/WriteLine loop, which re-terminates every
                    // line with the writer's NewLine - Environment.NewLine by default. A newline
                    // inside a quoted field (RFC 4180, and handled deliberately by ReadCsvRecord
                    // below) was therefore rewritten to the host's newline on the way to the
                    // server: an embedded LF arrived as CRLF on Windows, an embedded CRLF arrived
                    // as LF on Linux. Same class of defect as AUD-R26's ROWTERMINATOR finding.
                    // Copying characters through verbatim leaves the file's own bytes intact and
                    // lets COPY apply its own rules.
                    using var fileReader = new StreamReader(filePath, options.Encoding);
                    char[] buffer = new char[CopyBufferChars];
                    int read;
                    while ((read = fileReader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        textWriter.Write(buffer, 0, read);
                    }
                }
                catch
                {
                    // AUD-R34-011: Npgsql's copy writer *completes* the COPY when disposed and
                    // aborts it only on an explicit Cancel(). Without this, a failure part-way
                    // through the file - a decoding error under a strict Encoding, an I/O error -
                    // unwound through `using`, committed the rows written so far, and left the
                    // caller with an exception and a silently partial import.
                    CancelCopy(writer);
                    throw;
                }

                return CountCsvRows(filePath, options.HasHeader, options.Quote, options.Encoding);
            }

            // Fallback: Use COPY FROM with file path (requires server access to file).
            // Deliberately not preceded by RequireFileOnThisMachine: the server opens this one, so
            // the caller's filesystem says nothing about whether it will work.
            // AUD-R30: unlike the STDIN branch above, the server opens the file here, so a
            // non-default Encoding cannot be honoured and must fail loudly like the other
            // engine-opens-the-file paths - it was silently discarded before.
            ThrowIfEncodingUnsupported(options, "PostgreSQL server-side COPY FROM");
            using IDbCommand cmd = connection.CreateCommand();
            cmd.CommandText = $"COPY {escapedTable} FROM '{filePath.Replace("'", "''")}' WITH (FORMAT csv, HEADER {(options.HasHeader ? "true" : "false")}, DELIMITER '{options.Delimiter}'{BuildPostgresCopyExtraOptions(options)})";
            try
            {
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw ServerSideImportFailure(filePath, "PostgreSQL server-side COPY FROM", ex);
            }
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
                // This branch streams the file from here, so its absence here is an error Jaunty
                // can diagnose. The server-side fallback below is not gated on it - see
                // RequireFileOnThisMachine.
                RequireFileOnThisMachine(filePath);

                using var writer = (IDisposable)beginTextImport.Invoke(connection, new object[] { copyCommand })!;
                var textWriter = (TextWriter)writer;

                try
                {
                    // AUD-R34-010: see the sync sibling - a ReadLine/WriteLine loop rewrote every
                    // newline, including the ones inside quoted fields, to the host's newline.
                    using var fileReader = new StreamReader(filePath, options.Encoding);
                    char[] buffer = new char[CopyBufferChars];
                    int read;
                    while ((read = await fileReader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        await textWriter.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // AUD-R34-011: see the sync sibling - disposing the copy writer completes the
                    // COPY, so a mid-file failure has to cancel it explicitly or the rows written
                    // so far are committed behind the caller's back.
                    await CancelCopyAsync(writer).ConfigureAwait(false);
                    throw;
                }

                return CountCsvRows(filePath, options.HasHeader, options.Quote, options.Encoding);
            }

            // AUD-R30: see the sync sibling - the server opens the file on this branch, so a
            // non-default Encoding must be rejected rather than silently discarded.
            ThrowIfEncodingUnsupported(options, "PostgreSQL server-side COPY FROM");
            using DbCommand cmd = connection.CreateCommand();
            cmd.CommandText = $"COPY {escapedTable} FROM '{filePath.Replace("'", "''")}' WITH (FORMAT csv, HEADER {(options.HasHeader ? "true" : "false")}, DELIMITER '{options.Delimiter}'{BuildPostgresCopyExtraOptions(options)})";
            try
            {
                return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw ServerSideImportFailure(filePath, "PostgreSQL server-side COPY FROM", ex);
            }
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

    /// <summary>
    /// Characters copied per read on the PostgreSQL STDIN path. Sized to match
    /// <see cref="StreamReader"/>'s own default buffer so a read rarely spans two of them.
    /// </summary>
    private const int CopyBufferChars = 4096;

    /// <summary>
    /// Aborts an in-progress <c>COPY ... FROM STDIN</c> (AUD-R34-011). Npgsql's copy writer
    /// completes the operation on <c>Dispose</c>, so a failure part-way through the file has to
    /// call <c>Cancel()</c> or the rows already written are committed. Reflected for the same
    /// reason <c>BeginTextImport</c> is - Jaunty does not reference Npgsql.
    /// </summary>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Npgsql copy-writer Cancel() probe on the runtime writer type; a missing method means there is nothing to cancel and the exception propagates unchanged.")]
#endif
    private static void CancelCopy(IDisposable writer)
    {
        try
        {
            // AOT-SAFE: Npgsql feature probe on the runtime writer type; null means no cancel to make
            MethodInfo? cancel = writer.GetType().GetMethod("Cancel", Type.EmptyTypes);
            cancel?.Invoke(writer, null);
        }
        catch
        {
            // The copy is already failing and the caller's exception is the one worth surfacing;
            // a provider that refuses the cancel must not replace it with its own.
        }
    }

    /// <summary>
    /// Async twin of <see cref="CancelCopy"/>, preferring Npgsql's <c>CancelAsync()</c>.
    /// </summary>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Npgsql copy-writer CancelAsync()/Cancel() probe on the runtime writer type; a missing method means there is nothing to cancel and the exception propagates unchanged.")]
#endif
    private static async ValueTask CancelCopyAsync(IDisposable writer)
    {
        try
        {
            // AOT-SAFE: Npgsql feature probe on the runtime writer type; null falls back to Cancel()
            MethodInfo? cancelAsync = writer.GetType().GetMethod("CancelAsync", Type.EmptyTypes);
            if (cancelAsync is not null && cancelAsync.Invoke(writer, null) is Task pending)
            {
                await pending.ConfigureAwait(false);
                return;
            }

            // AOT-SAFE: as above, synchronous fallback
            MethodInfo? cancel = writer.GetType().GetMethod("Cancel", Type.EmptyTypes);
            cancel?.Invoke(writer, null);
        }
        catch
        {
            // See CancelCopy.
        }
    }

    // =============================================
    // MySQL/MariaDB: LOAD DATA LOCAL INFILE
    // =============================================

    private static long ImportMySql(IDbConnection connection, string tableName, string filePath, CsvImportOptions options)
    {
        ThrowIfNullValueUnsupported(options, "MySQL/MariaDB LOAD DATA");
        ThrowIfEncodingUnsupported(options, "MySQL/MariaDB LOAD DATA");
        RequireFileOnThisMachine(filePath);

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
            sb.Append($"LINES TERMINATED BY '{MySqlLineTerminator(filePath)}' ");
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
        ThrowIfEncodingUnsupported(options, "MySQL/MariaDB LOAD DATA");
        RequireFileOnThisMachine(filePath);

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
            sb.Append($"LINES TERMINATED BY '{MySqlLineTerminator(filePath)}' ");
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
        ThrowIfEncodingUnsupported(options, "SQL Server BULK INSERT");

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
            sb.Append($"ROWTERMINATOR = '{SqlServerRowTerminator(filePath)}', ");
            sb.Append("TABLOCK, ");
            if (options.HasHeader)
                sb.Append("FIRSTROW = 2, ");
            sb.Append("FORMAT = 'CSV'");
            if (options.Quote != '"')
                sb.Append($", FIELDQUOTE = '{EscapeSqlCharLiteral(options.Quote)}'");
            sb.Append(')');

            cmd.CommandText = sb.ToString();
            try
            {
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw ServerSideImportFailure(filePath, "SQL Server BULK INSERT", ex);
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    private static async ValueTask<long> ImportSqlServerAsync(DbConnection connection, string tableName, string filePath, CsvImportOptions options, CancellationToken cancellationToken)
    {
        ThrowIfNullValueUnsupported(options, "SQL Server BULK INSERT");
        ThrowIfEncodingUnsupported(options, "SQL Server BULK INSERT");

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
            sb.Append($"ROWTERMINATOR = '{SqlServerRowTerminator(filePath)}', ");
            sb.Append("TABLOCK, ");
            if (options.HasHeader)
                sb.Append("FIRSTROW = 2, ");
            sb.Append("FORMAT = 'CSV'");
            if (options.Quote != '"')
                sb.Append($", FIELDQUOTE = '{EscapeSqlCharLiteral(options.Quote)}'");
            sb.Append(')');

            cmd.CommandText = sb.ToString();
            try
            {
                return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw ServerSideImportFailure(filePath, "SQL Server BULK INSERT", ex);
            }
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

    // R35 (batch 03a, low/consistency): Encoding is non-nullable with an unguarded public setter,
    // and ThrowIfEncodingUnsupported already tests it for null - so null is reachable. Left to
    // itself it surfaces from new StreamReader(filePath, encoding) as an ArgumentNullException
    // naming "encoding", with nothing tying it back to CsvImportOptions. Every other option on that
    // type is validated at the entry point; this one now is too.
    private static void ValidateEncoding(Encoding encoding)
    {
        if (encoding is null)
            throw new ArgumentNullException(nameof(encoding),
                "CsvImportOptions.Encoding must not be null; leave it at its default of Encoding.UTF8 if you have no preference.");
    }

    private static void ValidateDelimiter(char delimiter)
    {
        if (delimiter is '\'' or '"' or '\\' or '\r' or '\n')
            throw new ArgumentException($"Delimiter '{delimiter}' is not supported; it conflicts with SQL/CLI quoting.", nameof(delimiter));
    }

    // R29: the two validators above check each character in isolation, so Delimiter == Quote
    // (both individually legal, e.g. ';') passed validation and produced silently corrupted
    // parsing - every field boundary is also a quote toggle.
    private static void ValidateDelimiterQuoteDistinct(char delimiter, char quote)
    {
        if (delimiter == quote)
            throw new ArgumentException($"Delimiter and Quote must differ; both are '{delimiter}'.", nameof(quote));
    }

    // Unlike Delimiter, Quote legitimately needs to allow '\'' (EscapeSqlCharLiteral doubles it)
    // and '"' (the default). A backslash reaches ImportMySql/ImportSqlServer/BuildPostgresCopyExtraOptions
    // as an unescaped single-quoted SQL string literal char; under MySQL's default sql_mode
    // (backslash escaping active unless NO_BACKSLASH_ESCAPES is set), the backslash escapes the
    // literal's closing quote instead of terminating it, breaking the generated statement. \r/\n
    // would break the single-quoted literal outright.
    private static void ValidateQuote(char quote)
    {
        if (quote is '\\' or '\r' or '\n')
            throw new ArgumentException($"Quote character '{quote}' is not supported; it conflicts with SQL quoting.", nameof(quote));
    }

    // Doubles a single-quote so a single character can be embedded in a single-quoted SQL string
    // literal (e.g. QUOTE '''' for a literal apostrophe quote character); any other character is
    // already safe to embed as-is.
    private static string EscapeSqlCharLiteral(char c) => c == '\'' ? "''" : c.ToString();

    // Both branches return a HEX terminator rather than the '\n' escape, because the escape is
    // interpreted differently depending on the OS SQL Server runs on. Measured 2026-07-29 with the
    // same two files against a local Windows instance and an Ubuntu container, reading DATALENGTH
    // of the last column back (17 = clean, 18 = a CR survived):
    //
    //   file  | '\n'                             | 0x0a          | 0x0d0a
    //   ------+----------------------------------+---------------+----------------
    //   CRLF  | Windows clean / Linux TRAILING CR | TRAILING CR   | clean on both
    //   LF    | Windows ERROR / Linux clean       | clean on both | ERROR on both
    //
    // Windows expands '\n' to \r\n; Linux takes it literally as 0x0a. Only the hex pair is right on
    // both, and hex is subject to no escape interpretation at all, so each file shape gets the
    // terminator it actually has regardless of where the server runs.
    //
    // AUD-R26: an earlier version emitted '\n' for CRLF, which is correct on Windows only. That is
    // why it passed the whole CsvImportTests suite against a local instance and failed exactly one
    // test in CI - ImportCsv_SqlServer_CrlfLineEndings_TrailingCarriageReturnStripped, where the row
    // round-tripped as "alice@example.com\r". The LF-only case fails outright rather than quietly,
    // with "Cannot obtain the required interface (IID_IColumnsInfo)".
    //
    // BULK INSERT reads the file server-side, so the path need not be readable from here at all;
    // when it is not, fall back to CRLF, which is what a file staged for a Windows SQL Server is
    // overwhelmingly likely to use.
    private const string SqlServerCrLfTerminator = "0x0d0a";
    private const string SqlServerLfTerminator = "0x0a";

    private static string SqlServerRowTerminator(string filePath) =>
        DetectCrLf(filePath) == false ? SqlServerLfTerminator : SqlServerCrLfTerminator;

    // R27 batch 03 (medium): MySQL LOAD DATA hardcoded LINES TERMINATED BY '\n', so a CRLF file -
    // the default output of Excel and most Windows tooling - imported every last field with a
    // trailing \r, silently. The same sniffing that picks SQL Server's ROWTERMINATOR now picks the
    // MySQL terminator. LOCAL INFILE reads the file client-side and RequireFileOnThisMachine has
    // already run, so the file is readable here; the unreadable fallback keeps the old '\n'.
    private static string MySqlLineTerminator(string filePath) =>
        DetectCrLf(filePath) == true ? "\\r\\n" : "\\n";

    private static bool? DetectCrLf(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[8192];
            int previous = -1;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    if (buffer[i] == 0x0A)
                        return previous == 0x0D;
                    previous = buffer[i];
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return null;
    }

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
    /// <summary>
    /// Requires the CSV file to exist on the machine running this code. Only for import paths that
    /// read it here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26 (batch 4, medium/consistency). This check used to sit in <c>ImportCsv</c> and
    /// <c>ImportCsvAsync</c>, ahead of dialect dispatch, and so applied to all four engines. Only
    /// some of them read the file here. SQLite (both the CLI and the prepared-statement fallback)
    /// and MySQL's <c>LOAD DATA LOCAL INFILE</c> do; PostgreSQL does when Npgsql's
    /// <c>BeginTextImport</c> is available and does not when it falls back to server-side
    /// <c>COPY ... FROM</c>; SQL Server's <c>BULK INSERT ... FROM</c> never does.
    /// </para>
    /// <para>
    /// The finding described the consequence as false assurance - against a remote server the check
    /// passes and the statement then fails there, or worse reads a <em>different</em> file that
    /// happens to exist at that path on the server host. It is also worse than that: because the
    /// check ran unconditionally and before dispatch, a file that exists on the database server and
    /// not on the calling machine - which is precisely what <c>BULK INSERT</c> is for - was rejected
    /// with <see cref="FileNotFoundException"/> before anything was sent. The capability was
    /// unreachable, not merely undocumented.
    /// </para>
    /// </remarks>
    private static void RequireFileOnThisMachine(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV file not found: {filePath}", filePath);
    }

    /// <summary>
    /// Reports a failure from a server-side import in terms of <em>which machine</em> resolves the
    /// path, which is the thing a caller looking at their own filesystem cannot see.
    /// </summary>
    /// <remarks>
    /// The provider's own message is accurate but describes the server's filesystem
    /// ("Cannot bulk load. The file ... does not exist or you don't have file access rights") while
    /// the caller is looking at theirs. Stating whether the path exists here is the whole diagnosis:
    /// present here and absent there means the two machines are different, and absent in both means
    /// an ordinary typo. The provider exception is kept as the inner exception - it carries the
    /// error number and the server's own wording, and nothing here is a substitute for that.
    /// </remarks>
    private static InvalidOperationException ServerSideImportFailure(string filePath, string importMethodName, Exception inner)
    {
        string clientSide = File.Exists(filePath)
            ? "That path exists on the calling machine, which means the database server is a different machine (or the account it runs as cannot read the path)."
            : "That path does not exist on the calling machine either.";

        return new InvalidOperationException(
            $"{importMethodName} failed for '{filePath}'. This import path is resolved by the database server, " +
            $"not by the process calling Jaunty, so the file must be readable by the server. {clientSide} " +
            "The account also needs the server's bulk-load permission (ADMINISTER BULK OPERATIONS, or membership " +
            "of bulkadmin). To import a file that lives on the calling machine, use a client-side route instead - " +
            "SQL Server's BulkInsert<T>, or one of the engines whose import streams from here.",
            inner);
    }

    /// <summary>
    /// Rejects a non-default <see cref="CsvImportOptions.Encoding"/> on an import path that cannot
    /// honour it.
    /// </summary>
    /// <remarks>
    /// AUD-R26 (batch 4, low/consistency). <c>Encoding</c> is read only where .NET opens the file -
    /// the SQLite prepared-statement fallback and PostgreSQL's <c>COPY FROM STDIN</c>. On the
    /// sqlite3 CLI, <c>LOAD DATA LOCAL INFILE</c>, <c>BULK INSERT</c> and (AUD-R30) PostgreSQL's
    /// server-side <c>COPY FROM</c> fallback, something else opens it and
    /// the setting was discarded in silence, so a caller who set Latin-1 for a Latin-1 file got
    /// UTF-8 behaviour and mojibake with nothing to indicate why. <c>NullValue</c> already failed
    /// loudly on these paths; this makes the type's two unhonourable options behave alike. The
    /// default is not rejected, because UTF-8 is what these paths produce anyway.
    /// </remarks>
    private static void ThrowIfEncodingUnsupported(CsvImportOptions options, string importMethodName)
    {
        if (options.Encoding is not null && options.Encoding.CodePage != Encoding.UTF8.CodePage)
            throw new NotSupportedException(
                $"CsvImportOptions.Encoding is not supported by the {importMethodName} import path - " +
                "the file is opened by the database engine rather than by Jaunty, so the encoding is the " +
                $"engine's to determine and '{options.Encoding.WebName}' cannot be applied. Leave Encoding at its " +
                "default (UTF-8), or import into an in-memory SQLite database or PostgreSQL via Npgsql's COPY FROM " +
                "STDIN, whose paths read the file here and honour it.");
    }

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

    // RFC 4180 allows a quoted field to contain embedded newlines. Reading physical lines with
    // StreamReader.ReadLine() and parsing each independently (as ImportViaPreparedStatements used
    // to) silently splits such a field across two "rows" instead of one, corrupting the imported
    // data. This joins consecutive physical lines with '\n' as long as the accumulated text has an
    // odd number of quote characters (RFC 4180's doubled "" escape always contributes quote chars
    // in pairs, so an odd running total means we are still inside an open quoted field), so a
    // logical record spanning multiple physical lines is read - and later parsed - as one.
    private static string? ReadCsvRecord(StreamReader reader, char quote)
    {
        string? line = reader.ReadLine();
        if (line is null)
            return null;

        while (!HasEvenQuoteCount(line, quote) && !reader.EndOfStream)
        {
            string? next = reader.ReadLine();
            if (next is null)
                break;
            line += "\n" + next;
        }

        return line;
    }

    private static bool HasEvenQuoteCount(string line, char quote)
    {
        int count = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == quote)
                count++;
        }
        return count % 2 == 0;
    }

    private static long CountCsvRows(string filePath, bool hasHeader, char quote, Encoding encoding)
    {
        long count = 0;
        using var reader = new StreamReader(filePath, encoding);
        while (ReadCsvRecord(reader, quote) != null)
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
                bool closed = false;
                int quoteStart = i;
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
                            closed = true;
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                }

                // AUD-R35-010: the other half of the AUD-R30 violation. The loop above can also
                // exit on i == len, having never seen a closing quote, and the field was then
                // returned holding everything to the end of the record - the same RFC 4180 breach
                // as characters-after-a-closing-quote, accepted in silence instead of thrown on.
                // It compounds with ReadCsvRecord, which glues physical lines while the running
                // quote count is odd: one stray quote absorbs the rest of the file into a single
                // record, and the caller then sees either a record number that corresponds to no
                // line in the file or a silently merged row.
                if (!closed)
                    throw new FormatException(
                        $"Malformed CSV: a quoted field opened at position {quoteStart} is never closed. " +
                        "A quoted field must end with a closing quote before the end of the record.");

                fields.Add(sb.ToString());

                // AUD-R30: anything between a closing quote and the next delimiter (e.g.
                // "abc"def,x) is malformed CSV per RFC 4180. It used to be parsed as the start of
                // a new unquoted field, silently inflating the field count.
                if (i < len && line[i] != delimiter)
                    throw new FormatException(
                        $"Malformed CSV: unexpected character '{line[i]}' after a closing quote at position {i}. " +
                        "A quoted field must be followed by the delimiter or the end of the line.");

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
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;

using Jaunty.Internals.Dialects;

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

        options ??= new CsvImportOptions();

        string connectionTypeName = connection.GetType().Name;
        return connectionTypeName switch
        {
            "SQLiteConnection" or "SqliteConnection" => ImportSqlite(connection, tableName, filePath, options),
            "NpgsqlConnection" => ImportPostgreSql(connection, tableName, filePath, options),
            "MySqlConnection" => ImportMySql(connection, tableName, filePath, options),
            "SqlConnection" => ImportSqlServer(connection, tableName, filePath, options),
            _ => throw new NotSupportedException($"CSV import is not supported for connection type: {connectionTypeName}")
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

        options ??= new CsvImportOptions();

        string connectionTypeName = connection.GetType().Name;
        return connectionTypeName switch
        {
            "SQLiteConnection" or "SqliteConnection" => await ImportSqliteAsync(connection, tableName, filePath, options, cancellationToken).ConfigureAwait(false),
            "NpgsqlConnection" => await ImportPostgreSqlAsync(connection, tableName, filePath, options, cancellationToken).ConfigureAwait(false),
            "MySqlConnection" => await ImportMySqlAsync(connection, tableName, filePath, options, cancellationToken).ConfigureAwait(false),
            "SqlConnection" => await ImportSqlServerAsync(connection, tableName, filePath, options, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"CSV import is not supported for connection type: {connectionTypeName}")
        };
    }

    // =============================================
    // SQLite: Use sqlite3 CLI .import command
    // =============================================

    private static long ImportSqlite(IDbConnection connection, string tableName, string filePath, CsvImportOptions options)
    {
        // Get the database file path from the connection string
        string? dbPath = ExtractSqliteDbPath(connection.ConnectionString);
        if (dbPath == null || dbPath == ":memory:")
        {
            // For in-memory databases, fall back to prepared statement insert
            return ImportViaPreparedStatements(connection, tableName, filePath, options);
        }

        // Use sqlite3 CLI for file-based databases
        return ImportViaSqliteCli(dbPath, tableName, filePath, options);
    }

    private static ValueTask<long> ImportSqliteAsync(DbConnection connection, string tableName, string filePath, CsvImportOptions options, CancellationToken cancellationToken)
    {
        // sqlite3 CLI is process-based; run synchronously wrapped in ValueTask
        long result = ImportSqlite(connection, tableName, filePath, options);
        return new ValueTask<long>(result);
    }

    private static long ImportViaSqliteCli(string dbPath, string tableName, string filePath, CsvImportOptions options)
    {
        // Build sqlite3 commands
        var commands = new StringBuilder();
        commands.AppendLine(".mode csv");

        if (options.Delimiter != ',')
            commands.AppendLine($".separator \"{options.Delimiter}\"");

        if (options.HasHeader)
            commands.AppendLine($".import --skip 1 \"{filePath.Replace("\\", "/")}\" {tableName}");
        else
            commands.AppendLine($".import \"{filePath.Replace("\\", "/")}\" {tableName}");

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
        // Read header to get column names
        string[] headers;
        using (var reader = new StreamReader(filePath, options.Encoding))
        {
            string? headerLine = reader.ReadLine();
            if (headerLine == null)
                return 0;

            headers = ParseCsvLine(headerLine, options.Delimiter, options.Quote);
        }

        // Build INSERT statement
        var sb = new StringBuilder();
        sb.Append($"INSERT INTO {tableName} (");
        sb.Append(string.Join(", ", headers));
        sb.Append(") VALUES (");
        for (int i = 0; i < headers.Length; i++)
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

            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = insertSql;

            // Create parameters
            var parameters = new IDbDataParameter[headers.Length];
            for (int i = 0; i < headers.Length; i++)
            {
                var param = command.CreateParameter();
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

                for (int i = 0; i < parameters.Length && i < values.Length; i++)
                {
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

    private static long ImportPostgreSql(IDbConnection connection, string tableName, string filePath, CsvImportOptions options)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) connection.Open();

            // Use COPY ... FROM STDIN via raw SQL (works with all Npgsql versions)
            string copyCommand = $"COPY {tableName} FROM STDIN WITH (FORMAT csv, HEADER {(options.HasHeader ? "true" : "false")}, DELIMITER '{options.Delimiter}')";

            // Use reflection to call BeginTextImport on NpgsqlConnection
            var beginTextImport = connection.GetType().GetMethod("BeginTextImport", new[] { typeof(string) });
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
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"COPY {tableName} FROM '{filePath.Replace("'", "''")}' WITH (FORMAT csv, HEADER {(options.HasHeader ? "true" : "false")}, DELIMITER '{options.Delimiter}')";
            return cmd.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed) connection.Close();
        }
    }

    private static async ValueTask<long> ImportPostgreSqlAsync(DbConnection connection, string tableName, string filePath, CsvImportOptions options, CancellationToken cancellationToken)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            string copyCommand = $"COPY {tableName} FROM STDIN WITH (FORMAT csv, HEADER {(options.HasHeader ? "true" : "false")}, DELIMITER '{options.Delimiter}')";

            var beginTextImport = connection.GetType().GetMethod("BeginTextImport", new[] { typeof(string) });
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

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"COPY {tableName} FROM '{filePath.Replace("'", "''")}' WITH (FORMAT csv, HEADER {(options.HasHeader ? "true" : "false")}, DELIMITER '{options.Delimiter}')";
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
        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) connection.Open();

            using var cmd = connection.CreateCommand();
            var sb = new StringBuilder();
            sb.Append($"LOAD DATA LOCAL INFILE '{filePath.Replace("\\", "\\\\").Replace("'", "\\'")}' ");
            sb.Append($"INTO TABLE {tableName} ");
            sb.Append($"FIELDS TERMINATED BY '{options.Delimiter}' ");
            sb.Append("OPTIONALLY ENCLOSED BY '\"' ");
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
        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = connection.CreateCommand();
            var sb = new StringBuilder();
            sb.Append($"LOAD DATA LOCAL INFILE '{filePath.Replace("\\", "\\\\").Replace("'", "\\'")}' ");
            sb.Append($"INTO TABLE {tableName} ");
            sb.Append($"FIELDS TERMINATED BY '{options.Delimiter}' ");
            sb.Append("OPTIONALLY ENCLOSED BY '\"' ");
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
        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) connection.Open();

            using var cmd = connection.CreateCommand();
            var sb = new StringBuilder();
            sb.Append($"BULK INSERT {tableName} FROM '{filePath.Replace("'", "''")}' ");
            sb.Append("WITH (");
            sb.Append($"FIELDTERMINATOR = '{options.Delimiter}', ");
            sb.Append("ROWTERMINATOR = '\\n', ");
            sb.Append("TABLOCK, ");
            if (options.HasHeader)
                sb.Append("FIRSTROW = 2, ");
            sb.Append("FORMAT = 'CSV')");

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
        bool wasClosed = connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var cmd = connection.CreateCommand();
            var sb = new StringBuilder();
            sb.Append($"BULK INSERT {tableName} FROM '{filePath.Replace("'", "''")}' ");
            sb.Append("WITH (");
            sb.Append($"FIELDTERMINATOR = '{options.Delimiter}', ");
            sb.Append("ROWTERMINATOR = '\\n', ");
            sb.Append("TABLOCK, ");
            if (options.HasHeader)
                sb.Append("FIRSTROW = 2, ");
            sb.Append("FORMAT = 'CSV')");

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
        var fields = new List<string>();
        int i = 0;
        int len = line.Length;

        while (i <= len)
        {
            if (i == len)
            {
                fields.Add(string.Empty);
                break;
            }

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
                    i++;
            }
            else
            {
                // Unquoted field
                int start = i;
                while (i < len && line[i] != delimiter)
                    i++;
                fields.Add(line.Substring(start, i - start));
                if (i < len)
                    i++; // skip delimiter
            }
        }

        return fields.ToArray();
    }
}

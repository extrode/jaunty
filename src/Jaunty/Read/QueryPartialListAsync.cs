using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Internals.Read;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query asynchronously and returns all results as dictionaries with column names as keys.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of dictionaries, each representing a row with column names as keys.</returns>
    /// <remarks>
    /// Row keys are compared with <see cref="StringComparer.OrdinalIgnoreCase"/>, matching
    /// <c>Query&lt;Dictionary&lt;string, object&gt;&gt;</c>, so a column the database returned as
    /// <c>ProductId</c> can be read as <c>row["productid"]</c>. If a result set contains two columns
    /// whose names differ only by case - or two identically named columns, the usual
    /// <c>SELECT a.id, b.id FROM a JOIN b</c> shape - only the last is kept, with no diagnostic;
    /// alias such columns in the SQL if you need both.
    /// <para>
    /// <strong>Duplicate column names.</strong> Keys are compared case-insensitively, so a result set
    /// with two columns of the same name - <c>SELECT o.Id, c.Id FROM Orders o JOIN Customers c ...</c>,
    /// or the same name differing only in case - cannot put both under one key. The first occurrence
    /// keeps the bare name and each later one gains an <c>_N</c> suffix (<c>Id</c>, <c>Id_2</c>), so no
    /// value is lost. Alias the columns in your SQL if you want names you chose.
    /// </para>
    /// </remarks>
    public static ValueTask<List<IDictionary<string, object?>>> QueryPartialListAsync(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryCoreListAsync(dbConnection, sql, null, default, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters asynchronously and returns all results as dictionaries with column names as keys.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of dictionaries, each representing a row with column names as keys.</returns>
    /// <remarks>
    /// Row keys are compared with <see cref="StringComparer.OrdinalIgnoreCase"/>, matching
    /// <c>Query&lt;Dictionary&lt;string, object&gt;&gt;</c>, so a column the database returned as
    /// <c>ProductId</c> can be read as <c>row["productid"]</c>. If a result set contains two columns
    /// whose names differ only by case - or two identically named columns, the usual
    /// <c>SELECT a.id, b.id FROM a JOIN b</c> shape - only the last is kept, with no diagnostic;
    /// alias such columns in the SQL if you need both.
    /// <para>
    /// <strong>Duplicate column names.</strong> Keys are compared case-insensitively, so a result set
    /// with two columns of the same name - <c>SELECT o.Id, c.Id FROM Orders o JOIN Customers c ...</c>,
    /// or the same name differing only in case - cannot put both under one key. The first occurrence
    /// keeps the bare name and each later one gains an <c>_N</c> suffix (<c>Id</c>, <c>Id_2</c>), so no
    /// value is lost. Alias the columns in your SQL if you want names you chose.
    /// </para>
    /// </remarks>
    public static ValueTask<List<IDictionary<string, object?>>> QueryPartialListAsync(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryCoreListAsync(dbConnection, sql, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query asynchronously and returns all results as dictionaries with column names as keys,
    /// using the specified command options (transaction, timeout, command type).
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">Options controlling transaction, timeout, and command type.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of dictionaries, each representing a row with column names as keys.</returns>
    /// <remarks>
    /// Row keys are compared with <see cref="StringComparer.OrdinalIgnoreCase"/>, matching
    /// <c>Query&lt;Dictionary&lt;string, object&gt;&gt;</c>, so a column the database returned as
    /// <c>ProductId</c> can be read as <c>row["productid"]</c>. If a result set contains two columns
    /// whose names differ only by case - or two identically named columns, the usual
    /// <c>SELECT a.id, b.id FROM a JOIN b</c> shape - only the last is kept, with no diagnostic;
    /// alias such columns in the SQL if you need both.
    /// <para>
    /// <strong>Duplicate column names.</strong> Keys are compared case-insensitively, so a result set
    /// with two columns of the same name - <c>SELECT o.Id, c.Id FROM Orders o JOIN Customers c ...</c>,
    /// or the same name differing only in case - cannot put both under one key. The first occurrence
    /// keeps the bare name and each later one gains an <c>_N</c> suffix (<c>Id</c>, <c>Id_2</c>), so no
    /// value is lost. Alias the columns in your SQL if you want names you chose.
    /// </para>
    /// </remarks>
    public static ValueTask<List<IDictionary<string, object?>>> QueryPartialListAsync(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryCoreListAsync(dbConnection, sql, null, options, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters asynchronously and returns all results as dictionaries with column
    /// names as keys, using the specified command options (transaction, timeout, command type).
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">Options controlling transaction, timeout, and command type.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of dictionaries, each representing a row with column names as keys.</returns>
    /// <remarks>
    /// Row keys are compared with <see cref="StringComparer.OrdinalIgnoreCase"/>, matching
    /// <c>Query&lt;Dictionary&lt;string, object&gt;&gt;</c>, so a column the database returned as
    /// <c>ProductId</c> can be read as <c>row["productid"]</c>. If a result set contains two columns
    /// whose names differ only by case - or two identically named columns, the usual
    /// <c>SELECT a.id, b.id FROM a JOIN b</c> shape - only the last is kept, with no diagnostic;
    /// alias such columns in the SQL if you need both.
    /// <para>
    /// <strong>Duplicate column names.</strong> Keys are compared case-insensitively, so a result set
    /// with two columns of the same name - <c>SELECT o.Id, c.Id FROM Orders o JOIN Customers c ...</c>,
    /// or the same name differing only in case - cannot put both under one key. The first occurrence
    /// keeps the bare name and each later one gains an <c>_N</c> suffix (<c>Id</c>, <c>Id_2</c>), so no
    /// value is lost. Alias the columns in your SQL if you want names you chose.
    /// </para>
    /// </remarks>
    public static ValueTask<List<IDictionary<string, object?>>> QueryPartialListAsync(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryCoreListAsync(dbConnection, sql, parameters, options, cancellationToken);
    }

    private static async ValueTask<List<IDictionary<string, object?>>> QueryCoreListAsync(DbConnection connection, string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
    {
        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in GetAllCore.cs.
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            return await JauntyConfig.InterceptorPipeline.ExecuteWithInterceptionAsync(
                sql,
                parameters,
                connection,
                options.CommandType,
                () => QueryCoreListDirectAsync(connection, sql, parameters, options, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return await QueryCoreListDirectAsync(connection, sql, parameters, options, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<List<IDictionary<string, object?>>> QueryCoreListDirectAsync(DbConnection connection, string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.BindParameters(parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            var results = new List<IDictionary<string, object?>>();

            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var columnNames = new string[reader.FieldCount];
            for (int i = 0; i < columnNames.Length; i++)
                columnNames[i] = reader.GetName(i);

            // AUD-R26: two columns of the same name would otherwise collapse to one dictionary key,
            // silently dropping a value. See DuplicateColumnNames for why this renames rather than
            // throws, and why the first occurrence keeps the bare name.
            columnNames = DuplicateColumnNames.Disambiguate(columnNames);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // OrdinalIgnoreCase, matching SpecialTypeMappers.CreateDictionaryMapper - the sibling
                // untyped-row path behind Query<Dictionary<string, object>>, which has always used it.
                // AUD-R25: this used the default ordinal comparer, so row["productid"] threw
                // KeyNotFoundException when the result set named the column "ProductId". Two public
                // APIs returning the same untyped-row shape disagreed on key lookup, and the
                // difference was silent - a caller moving between them got no compile error, just
                // runtime failures depending on how the database happened to case the column.
                var row = new Dictionary<string, object?>(columnNames.Length, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < columnNames.Length; i++)
                {
                    object? value = reader.GetValue(i);
                    row[columnNames[i]] = value == DBNull.Value ? null : value;
                }
                results.Add(row);
            }

            return results;
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                await Task.Run(() => connection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }
}

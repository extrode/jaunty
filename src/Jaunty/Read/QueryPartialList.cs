using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Internals.Read;
using Jaunty.Interceptors;
using Jaunty.Internals;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query and returns all results as dictionaries with column names as keys.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>A list of dictionaries, each representing a row with column names as keys.</returns>
    /// <remarks>
    /// Row keys are compared with <see cref="StringComparer.OrdinalIgnoreCase"/>, matching
    /// <c>Query&lt;Dictionary&lt;string, object&gt;&gt;</c>, so a column the database returned as
    /// <c>ProductId</c> can be read as <c>row["productid"]</c>. If a result set contains two columns
    /// whose names differ only by case - or two identically named columns, the usual
    /// <c>SELECT a.id, b.id FROM a JOIN b</c> shape - see the note below.
    /// <para>
    /// <strong>Duplicate column names.</strong> Keys are compared case-insensitively, so a result set
    /// with two columns of the same name - <c>SELECT o.Id, c.Id FROM Orders o JOIN Customers c ...</c>,
    /// or the same name differing only in case - cannot put both under one key. The first occurrence
    /// keeps the bare name and each later one gains an <c>_N</c> suffix (<c>Id</c>, <c>Id_2</c>), so no
    /// value is lost. Alias the columns in your SQL if you want names you chose.
    /// </para>
    /// </remarks>
    public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql)
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
        return QueryCoreList(connection, sql, null, default);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns all results as dictionaries with column names as keys.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>A list of dictionaries, each representing a row with column names as keys.</returns>
    /// <remarks>
    /// Row keys are compared with <see cref="StringComparer.OrdinalIgnoreCase"/>, matching
    /// <c>Query&lt;Dictionary&lt;string, object&gt;&gt;</c>, so a column the database returned as
    /// <c>ProductId</c> can be read as <c>row["productid"]</c>. If a result set contains two columns
    /// whose names differ only by case - or two identically named columns, the usual
    /// <c>SELECT a.id, b.id FROM a JOIN b</c> shape - see the note below.
    /// <para>
    /// <strong>Duplicate column names.</strong> Keys are compared case-insensitively, so a result set
    /// with two columns of the same name - <c>SELECT o.Id, c.Id FROM Orders o JOIN Customers c ...</c>,
    /// or the same name differing only in case - cannot put both under one key. The first occurrence
    /// keeps the bare name and each later one gains an <c>_N</c> suffix (<c>Id</c>, <c>Id_2</c>), so no
    /// value is lost. Alias the columns in your SQL if you want names you chose.
    /// </para>
    /// </remarks>
    public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql, object parameters)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryCoreList(connection, sql, parameters, default);
    }

    /// <summary>
    /// Executes a SQL query and returns all results as dictionaries with column names as keys, using the
    /// specified command options (transaction, timeout, command type).
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">Options controlling transaction, timeout, and command type.</param>
    /// <returns>A list of dictionaries, each representing a row with column names as keys.</returns>
    /// <remarks>
    /// Row keys are compared with <see cref="StringComparer.OrdinalIgnoreCase"/>, matching
    /// <c>Query&lt;Dictionary&lt;string, object&gt;&gt;</c>, so a column the database returned as
    /// <c>ProductId</c> can be read as <c>row["productid"]</c>. If a result set contains two columns
    /// whose names differ only by case - or two identically named columns, the usual
    /// <c>SELECT a.id, b.id FROM a JOIN b</c> shape - see the note below.
    /// <para>
    /// <strong>Duplicate column names.</strong> Keys are compared case-insensitively, so a result set
    /// with two columns of the same name - <c>SELECT o.Id, c.Id FROM Orders o JOIN Customers c ...</c>,
    /// or the same name differing only in case - cannot put both under one key. The first occurrence
    /// keeps the bare name and each later one gains an <c>_N</c> suffix (<c>Id</c>, <c>Id_2</c>), so no
    /// value is lost. Alias the columns in your SQL if you want names you chose.
    /// </para>
    /// </remarks>
    public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql, CommandOptions options)
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
        return QueryCoreList(connection, sql, null, options);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns all results as dictionaries with column names as keys,
    /// using the specified command options (transaction, timeout, command type).
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">Options controlling transaction, timeout, and command type.</param>
    /// <returns>A list of dictionaries, each representing a row with column names as keys.</returns>
    /// <remarks>
    /// Row keys are compared with <see cref="StringComparer.OrdinalIgnoreCase"/>, matching
    /// <c>Query&lt;Dictionary&lt;string, object&gt;&gt;</c>, so a column the database returned as
    /// <c>ProductId</c> can be read as <c>row["productid"]</c>. If a result set contains two columns
    /// whose names differ only by case - or two identically named columns, the usual
    /// <c>SELECT a.id, b.id FROM a JOIN b</c> shape - see the note below.
    /// <para>
    /// <strong>Duplicate column names.</strong> Keys are compared case-insensitively, so a result set
    /// with two columns of the same name - <c>SELECT o.Id, c.Id FROM Orders o JOIN Customers c ...</c>,
    /// or the same name differing only in case - cannot put both under one key. The first occurrence
    /// keeps the bare name and each later one gains an <c>_N</c> suffix (<c>Id</c>, <c>Id_2</c>), so no
    /// value is lost. Alias the columns in your SQL if you want names you chose.
    /// </para>
    /// </remarks>
    public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql, object parameters, CommandOptions options)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryCoreList(connection, sql, parameters, options);
    }

    private static List<IDictionary<string, object?>> QueryCoreList(IDbConnection connection, string sql, object? parameters, CommandOptions options)
    {
        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in GetAllCore.cs.
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            return pipeline.ExecuteWithInterception(
                sql,
                parameters,
                connection,
                options.CommandType,
                () => QueryCoreListDirect(connection, sql, parameters, options));
        }

        return QueryCoreListDirect(connection, sql, parameters, options);
    }

    private static List<IDictionary<string, object?>> QueryCoreListDirect(IDbConnection connection, string sql, object? parameters, CommandOptions options)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
            connection.Open();

        try
        {
            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
            // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
            // IDbTransaction through it throws an opaque InvalidCastException. Validate via
            // AsyncTransactionValidator first (mirroring GetByIdSimpleCoreDirect) so an incompatible
            // transaction gets Jaunty's clear ArgumentException instead.
            if (options.Transaction is not null)
            {
                command.Transaction = connection is DbConnection
                    ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                    : options.Transaction;
            }

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.BindParameters(parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            var results = new List<IDictionary<string, object?>>();

            using IDataReader reader = command.ExecuteReader();

            var columnNames = new string[reader.FieldCount];
            for (int i = 0; i < columnNames.Length; i++)
                columnNames[i] = reader.GetName(i);

            // AUD-R26: two columns of the same name would otherwise collapse to one dictionary key,
            // silently dropping a value. See DuplicateColumnNames for why this renames rather than
            // throws, and why the first occurrence keeps the bare name.
            columnNames = DuplicateColumnNames.Disambiguate(columnNames);

            while (reader.Read())
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
                connection.Close();
        }
    }
}

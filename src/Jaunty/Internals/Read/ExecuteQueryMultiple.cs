using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Interceptors;
using Jaunty.Internals.Parameters;

namespace Jaunty;

/// <summary>
/// Internal helper methods for executing multi-result queries.
/// </summary>
public static partial class Jaunty
{
    /// <summary>
    /// Executes a multi‑result query synchronously.
    /// </summary>
    /// <param name="connection">A <see cref="DbConnection"/> that will be opened if it is closed.</param>
    /// <param name="sql">SQL containing one or more result sets.</param>
    /// <param name="parameters">Optional parameters for the command.</param>
    /// <param name="options">Transaction, timeout, etc.</param>
    /// <returns>A <see cref="GridReader"/> that owns the <see cref="DbDataReader"/> and the command.</returns>
    private static GridReader ExecuteQueryMultiple(IDbConnection connection, string sql, object? parameters, CommandOptions options)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif

        // Use InterceptorPipeline if registered, otherwise execute directly. Unlike streaming,
        // ExecuteReader() here fully executes the command against the database in one round trip
        // (the multiple result sets are already available); GridReader only lazily walks rows the
        // caller already has. So wrapping just command-creation-through-ExecuteReader() is safe and
        // matches the interceptor semantics used by the other Query* cores, without requiring the
        // grid's result sets to be materialized upfront.
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            return JauntyConfig.InterceptorPipeline.ExecuteWithInterception(
                sql,
                parameters,
                connection,
                options.CommandType,
                () => ExecuteQueryMultipleDirect(connection, sql, parameters, options));
        }

        return ExecuteQueryMultipleDirect(connection, sql, parameters, options);
    }

    private static GridReader ExecuteQueryMultipleDirect(IDbConnection connection, string sql, object? parameters, CommandOptions options)
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
            connection.Open();

        // Do NOT use 'using' — the GridReader owns the command and disposes it.
        IDbCommand command = connection.CreateCommand();

        try
        {
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            IDataReader reader = command.ExecuteReader();
            return new GridReader(reader, connection, wasClosed, command);
        }
        catch
        {
            command.Dispose();
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
            throw;
        }
    }
}
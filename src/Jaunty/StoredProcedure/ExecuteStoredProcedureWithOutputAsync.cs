using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously executes a stored procedure with output parameters and returns the results as a list.
    /// Output parameter values can be retrieved from the SpParameters object after execution.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters including input and output parameters.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of mapped entities.</returns>
    public static Task<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteWithOutputParametersAsync(connection, procedureName, parameters, spOptions, async (reader, _, ct) =>
        {
            var results = new List<T>();
            var map = DrDispatcher.Resolve(reader, spOptions, MappingMode.Strict);
            while (await ReadAsync(reader, ct).ConfigureAwait(false))
            {
                results.Add(map(reader));
            }
            return results;
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with output parameters and returns the first result.
    /// Output parameter values can be retrieved from the SpParameters object after execution.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters including input and output parameters.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first mapped entity.</returns>
    public static Task<T> ExecuteStoredProcedureFirstAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteWithOutputParametersAsync(connection, procedureName, parameters, spOptions, async (reader, _, ct) =>
        {
            if (!await ReadAsync(reader, ct).ConfigureAwait(false))
                throw new InvalidOperationException("Sequence contains no elements.");
            var map = DrDispatcher.Resolve(reader, spOptions, MappingMode.Strict);
            return map(reader);
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with output parameters and returns the first result or default.
    /// Output parameter values can be retrieved from the SpParameters object after execution.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters including input and output parameters.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first mapped entity or default.</returns>
    public static Task<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteWithOutputParametersAsync<T?>(connection, procedureName, parameters, spOptions, async (reader, _, ct) =>
        {
            if (!await ReadAsync(reader, ct).ConfigureAwait(false))
                return default;
            var map = DrDispatcher.Resolve(reader, spOptions, MappingMode.Strict);
            return map(reader);
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with output parameters and returns a scalar value.
    /// Output parameter values can be retrieved from the SpParameters object after execution.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters including input and output parameters.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scalar value.</returns>
    public static Task<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default, CancellationToken cancellationToken = default)
    {
        var spOptions = new CommandOptions(options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteScalarWithOutputParametersAsync<T>(connection, procedureName, parameters, spOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with output parameters that does not return results (INSERT, UPDATE, DELETE).
    /// Output parameter values can be retrieved from the SpParameters object after execution.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters including input and output parameters.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows affected.</returns>
    public static Task<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default, CancellationToken cancellationToken = default)
    {
        var spOptions = new CommandOptions(options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteNonQueryWithOutputParametersAsync(connection, procedureName, parameters, spOptions, cancellationToken);
    }

    #region Core async execution with output parameters

    private static async Task<TResult> ExecuteWithOutputParametersAsync<TResult>(IDbConnection connection, string procedureName, SpParameters parameters,
        CommandOptions options, Func<IDataReader, SpParameters, CancellationToken, Task<TResult>> handler, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException(nameof(procedureName));
#endif

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (connection is DbConnection dbConnection)
            {
                if (wasClosed)
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                await using DbCommand command = dbConnection.CreateCommand();
#else
                using DbCommand command = dbConnection.CreateCommand();
#endif
                command.CommandText = procedureName;
                command.CommandType = CommandType.StoredProcedure;

                if (options.Transaction is DbTransaction dbTransaction)
                    command.Transaction = dbTransaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                BindSpParameters(command, parameters);

#if NET8_0_OR_GREATER
                await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
                using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
                TResult result = await handler(reader, parameters, cancellationToken).ConfigureAwait(false);

                // Close reader before reading output parameters
#if NET8_0_OR_GREATER
                await reader.CloseAsync().ConfigureAwait(false);
#else
                reader.Close();
#endif

                // Read output parameter values
                ReadOutputParameters(parameters);

                return result;
            }
            else
            {
                // Fallback for non-DbConnection
                if (wasClosed)
                    connection.Open();

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = procedureName;
                command.CommandType = CommandType.StoredProcedure;

                if (options.Transaction is not null)
                    command.Transaction = options.Transaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                BindSpParameters(command, parameters);

                using IDataReader reader = command.ExecuteReader();
                TResult result = await handler(reader, parameters, cancellationToken).ConfigureAwait(false);

                reader.Close();
                ReadOutputParameters(parameters);

                return result;
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                if (connection is DbConnection dbConn)
                    await dbConn.CloseAsync().ConfigureAwait(false);
                else
                    connection.Close();
#else
                connection.Close();
#endif
            }
        }
    }

    private static async Task<T> ExecuteScalarWithOutputParametersAsync<T>(IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException(nameof(procedureName));
#endif

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (connection is DbConnection dbConnection)
            {
                if (wasClosed)
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                await using DbCommand command = dbConnection.CreateCommand();
#else
                using DbCommand command = dbConnection.CreateCommand();
#endif
                command.CommandText = procedureName;
                command.CommandType = CommandType.StoredProcedure;

                if (options.Transaction is DbTransaction dbTransaction)
                    command.Transaction = dbTransaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                BindSpParameters(command, parameters);

                object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

                ReadOutputParameters(parameters);

                if (result is null || result == DBNull.Value)
                {
                    if (default(T) is null)
                        return default!;
                    throw new InvalidOperationException("Scalar result is null but expected a non-nullable value.");
                }

                return (T)Convert.ChangeType(result, typeof(T));
            }
            else
            {
                // Fallback for non-DbConnection
                if (wasClosed)
                    connection.Open();

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = procedureName;
                command.CommandType = CommandType.StoredProcedure;

                if (options.Transaction is not null)
                    command.Transaction = options.Transaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                BindSpParameters(command, parameters);

                object? result = command.ExecuteScalar();

                ReadOutputParameters(parameters);

                if (result is null || result == DBNull.Value)
                {
                    if (default(T) is null)
                        return default!;
                    throw new InvalidOperationException("Scalar result is null but expected a non-nullable value.");
                }

                return (T)Convert.ChangeType(result, typeof(T));
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                if (connection is DbConnection dbConn)
                    await dbConn.CloseAsync().ConfigureAwait(false);
                else
                    connection.Close();
#else
                connection.Close();
#endif
            }
        }
    }

    private static async Task<int> ExecuteNonQueryWithOutputParametersAsync(IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException(nameof(procedureName));
#endif

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (connection is DbConnection dbConnection)
            {
                if (wasClosed)
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                await using DbCommand command = dbConnection.CreateCommand();
#else
                using DbCommand command = dbConnection.CreateCommand();
#endif
                command.CommandText = procedureName;
                command.CommandType = CommandType.StoredProcedure;

                if (options.Transaction is DbTransaction dbTransaction)
                    command.Transaction = dbTransaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                BindSpParameters(command, parameters);

                int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                ReadOutputParameters(parameters);

                return rowsAffected;
            }
            else
            {
                // Fallback for non-DbConnection
                if (wasClosed)
                    connection.Open();

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = procedureName;
                command.CommandType = CommandType.StoredProcedure;

                if (options.Transaction is not null)
                    command.Transaction = options.Transaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                BindSpParameters(command, parameters);

                int rowsAffected = command.ExecuteNonQuery();

                ReadOutputParameters(parameters);

                return rowsAffected;
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                if (connection is DbConnection dbConn)
                    await dbConn.CloseAsync().ConfigureAwait(false);
                else
                    connection.Close();
#else
                connection.Close();
#endif
            }
        }
    }

    private static async Task<bool> ReadAsync(IDataReader reader, CancellationToken cancellationToken)
    {
        if (reader is DbDataReader dbReader)
            return await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false);

        return reader.Read();
    }

    #endregion
}

using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Internals.Read;
using Jaunty.StoredProcedure;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously executes a stored procedure with output parameters and returns the results as a list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// A <see cref="SpParameters"/> object containing input and output parameters for the stored procedure.
    /// After execution, output parameter values can be retrieved using <c>parameters.Get&lt;T&gt;("parameterName")</c>.
    /// </param>
    /// <param name="options">
    /// Optional command options for configuring the stored procedure execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of mapped entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method executes a stored procedure using strict mapping mode.
    /// </para>
    /// <para>
    /// <strong>Output Parameters:</strong> After execution, retrieve output parameter values using:
    /// <code>
    /// var outputValue = parameters.Get&lt;int&gt;("TotalCount");
    /// </code>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }
    /// }
    /// 
    /// // Async execute stored procedure with output parameters
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddOutput("TotalCount", DbType.Int32);
    /// 
    /// var products = await connection.ExecuteStoredProcedureAsync&lt;Product&gt;("GetProductsByCategoryWithTotal", parameters);
    /// 
    /// // Get output parameter value
    /// int totalCount = parameters.Get&lt;int&gt;("TotalCount");
    /// Console.WriteLine($"Total products: {totalCount}");
    /// </code>
    /// </example>
    /// <seealso cref="SpParameters"/>
    /// <seealso cref="ExecuteStoredProcedure{T}(IDbConnection, string, SpParameters, CommandOptions{T})"/>
    public static ValueTask<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteWithOutputParametersAsync(connection, procedureName, parameters, spOptions, async (reader, _, ct) =>
        {
            var results = new List<T>(16);
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, spOptions, MappingMode.Strict);
            while (await ReadAsync(reader, ct).ConfigureAwait(false))
            {
                results.Add(map(reader));
            }
            return results;
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with output parameters and returns the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// A <see cref="SpParameters"/> object containing input and output parameters for the stored procedure.
    /// </param>
    /// <param name="options">
    /// Optional command options for configuring the stored procedure execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the first mapped entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// <para>
    /// Output parameter values can be retrieved after execution.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async execute stored procedure with output parameters and get first result
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddOutput("HasMore", DbType.Boolean);
    /// 
    /// var product = await connection.ExecuteStoredProcedureFirstAsync&lt;Product&gt;("GetFirstProductByCategory", parameters);
    /// 
    /// // Get output parameter value
    /// bool hasMore = parameters.Get&lt;bool&gt;("HasMore");
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stored procedure returns no results.
    /// </exception>
    /// <seealso cref="SpParameters"/>
    /// <seealso cref="ExecuteStoredProcedureFirst{T}(IDbConnection, string, SpParameters, CommandOptions{T})"/>
    public static ValueTask<T> ExecuteStoredProcedureFirstAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteWithOutputParametersAsync(connection, procedureName, parameters, spOptions, async (reader, _, ct) =>
        {
            if (!await ReadAsync(reader, ct).ConfigureAwait(false))
                throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, spOptions, MappingMode.Strict);
            return map(reader);
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with output parameters and returns the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// A <see cref="SpParameters"/> object containing input and output parameters for the stored procedure.
    /// </param>
    /// <param name="options">
    /// Optional command options for configuring the stored procedure execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the first mapped entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async execute stored procedure with output parameters and get first or null
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 999)
    ///     .AddOutput("TotalCount", DbType.Int32);
    /// 
    /// var product = await connection.ExecuteStoredProcedureFirstOrDefaultAsync&lt;Product&gt;("GetFirstProductByCategory", parameters);
    /// 
    /// if (product != null)
    /// {
    ///     int totalCount = parameters.Get&lt;int&gt;("TotalCount");
    ///     Console.WriteLine($"Found: {product.Name}, Total: {totalCount}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="SpParameters"/>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefault{T}(IDbConnection, string, SpParameters, CommandOptions{T})"/>
    public static ValueTask<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions<T> options = default, CancellationToken cancellationToken = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteWithOutputParametersAsync<T?>(connection, procedureName, parameters, spOptions, async (reader, _, ct) =>
        {
            if (!await ReadAsync(reader, ct).ConfigureAwait(false))
                return default;
            Func<IDataReader, T> map = DrDispatcher.Resolve(reader, spOptions, MappingMode.Strict);
            return map(reader);
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with output parameters and returns a scalar value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// A <see cref="SpParameters"/> object containing input and output parameters for the stored procedure.
    /// </param>
    /// <param name="options">
    /// Optional command options for configuring the stored procedure execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the scalar value returned by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that return a single value along with output parameters.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async execute stored procedure that returns scalar with output parameters
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddOutput("AveragePrice", DbType.Decimal);
    /// 
    /// var count = await connection.ExecuteStoredProcedureScalarAsync&lt;long&gt;("GetProductCountWithAverage", parameters);
    /// 
    /// // Get output parameter value
    /// decimal avgPrice = parameters.Get&lt;decimal&gt;("AveragePrice");
    /// </code>
    /// </example>
    /// <seealso cref="SpParameters"/>
    /// <seealso cref="ExecuteStoredProcedureScalar{T}(IDbConnection, string, SpParameters, CommandOptions)"/>
    public static ValueTask<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default, CancellationToken cancellationToken = default)
    {
        var spOptions = new CommandOptions(options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteScalarWithOutputParametersAsync<T>(connection, procedureName, parameters, spOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with output parameters that does not return results (INSERT, UPDATE, DELETE) 
    /// and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// A <see cref="SpParameters"/> object containing input and output parameters for the stored procedure.
    /// </param>
    /// <param name="options">
    /// Optional command options for configuring the stored procedure execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows affected by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that perform data modification operations with output parameters.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async execute stored procedure that updates prices with output parameter
    /// var parameters = new SpParameters()
    ///     .AddInput("CategoryId", 5)
    ///     .AddInput("PriceIncreasePercent", 10)
    ///     .AddOutput("RowsUpdated", DbType.Int32);
    /// 
    /// int affected = await connection.ExecuteStoredProcedureNonQueryAsync("UpdatePricesByCategory", parameters);
    /// 
    /// // Get output parameter value
    /// int rowsUpdated = parameters.Get&lt;int&gt;("RowsUpdated");
    /// </code>
    /// </example>
    /// <seealso cref="SpParameters"/>
    /// <seealso cref="ExecuteStoredProcedureNonQuery(IDbConnection, string, SpParameters, CommandOptions)"/>
    public static ValueTask<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options = default, CancellationToken cancellationToken = default)
    {
        var spOptions = new CommandOptions(options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return ExecuteNonQueryWithOutputParametersAsync(connection, procedureName, parameters, spOptions, cancellationToken);
    }

    #region Core async execution with output parameters

    // Internal (not private) so tests can pass a custom handler that cancels the operation's
    // CancellationToken right before returning a successful result, exercising the reader-close
    // cleanup below (which runs after the handler, not inside a finally block) with an
    // already-canceled token. Not reachable via the public API, whose handlers are fixed internal
    // lambdas.
    internal static async ValueTask<TResult> ExecuteWithOutputParametersAsync<TResult>(IDbConnection connection, string procedureName, SpParameters parameters,
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

        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        bool wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            BindSpParameters(command, parameters);

#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
            TResult result = await handler(reader, parameters, cancellationToken).ConfigureAwait(false);

            // Close reader before reading output parameters
#if NET8_0_OR_GREATER
            await reader.CloseAsync().ConfigureAwait(false);
#else
            await Task.Run(() => reader.Close()).ConfigureAwait(false);
#endif

            // Read output parameter values
            ReadOutputParameters(parameters);

            return result;
        }
        finally
        {
            if (wasClosed && dbConnection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await dbConnection.CloseAsync().ConfigureAwait(false);
#else
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }

    private static async ValueTask<T> ExecuteScalarWithOutputParametersAsync<T>(IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options, CancellationToken cancellationToken)
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

        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        bool wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            BindSpParameters(command, parameters);

            object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            ReadOutputParameters(parameters);

            return result is null || result == DBNull.Value
                ? default(T) is null
                    ? default!
                    : throw new InvalidOperationException("Scalar result is null but expected a non-nullable value.")
                : (T)Convert.ChangeType(result, typeof(T));
        }
        finally
        {
            if (wasClosed && dbConnection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await dbConnection.CloseAsync().ConfigureAwait(false);
#else
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }

    private static async ValueTask<int> ExecuteNonQueryWithOutputParametersAsync(IDbConnection connection, string procedureName, SpParameters parameters, CommandOptions options, CancellationToken cancellationToken)
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

        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        bool wasClosed = dbConnection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = dbConnection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = dbConnection.CreateCommand();
#endif
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            BindSpParameters(command, parameters);

            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            ReadOutputParameters(parameters);

            return rowsAffected;
        }
        finally
        {
            if (wasClosed && dbConnection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await dbConnection.CloseAsync().ConfigureAwait(false);
#else
                await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }

    private static async ValueTask<bool> ReadAsync(IDataReader reader, CancellationToken cancellationToken)
    {
        return reader is DbDataReader dbReader ? await dbReader.ReadAsync(cancellationToken).ConfigureAwait(false) : reader.Read();
    }

    #endregion
}

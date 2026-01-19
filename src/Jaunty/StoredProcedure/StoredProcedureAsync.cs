using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously executes a stored procedure and returns the results as a list.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of mapped entities.</returns>
    public static Task<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default) where T : new()
    {
        return ExecuteStoredProcedureAsync<T>(connection, procedureName, null, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and returns the results as a list.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of mapped entities.</returns>
    public static Task<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return ExecuteStoredProcedureAsync<T>(connection, procedureName, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and options, returning the results as a list.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of mapped entities.</returns>
    public static Task<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.QueryAsync<T>(procedureName, parameters!, spOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure and returns the first result.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first mapped entity.</returns>
    public static Task<T> ExecuteStoredProcedureFirstAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default) where T : new()
    {
        return ExecuteStoredProcedureFirstAsync<T>(connection, procedureName, null, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and returns the first result.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first mapped entity.</returns>
    public static Task<T> ExecuteStoredProcedureFirstAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return ExecuteStoredProcedureFirstAsync<T>(connection, procedureName, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and options, returning the first result.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first mapped entity.</returns>
    public static Task<T> ExecuteStoredProcedureFirstAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.QueryFirstAsync<T>(procedureName, parameters!, spOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure and returns the first result or default.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first mapped entity or default.</returns>
    public static Task<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default) where T : new()
    {
        return ExecuteStoredProcedureFirstOrDefaultAsync<T>(connection, procedureName, null, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and returns the first result or default.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first mapped entity or default.</returns>
    public static Task<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return ExecuteStoredProcedureFirstOrDefaultAsync<T>(connection, procedureName, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and options, returning the first result or default.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first mapped entity or default.</returns>
    public static Task<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.QueryFirstOrDefaultAsync<T>(procedureName, parameters!, spOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure and returns a scalar value.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scalar value.</returns>
    public static Task<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default)
    {
        return ExecuteStoredProcedureScalarAsync<T>(connection, procedureName, (object?)null, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and returns a scalar value.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scalar value.</returns>
    public static Task<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default)
    {
        return ExecuteStoredProcedureScalarAsync<T>(connection, procedureName, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and options, returning a scalar value.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The scalar value.</returns>
    public static Task<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.QueryScalarAsync<T>(procedureName, parameters!, spOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure that does not return results (INSERT, UPDATE, DELETE).
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows affected.</returns>
    public static Task<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default)
    {
        return ExecuteStoredProcedureNonQueryAsync(connection, procedureName, null, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters that does not return results.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows affected.</returns>
    public static Task<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default)
    {
        return ExecuteStoredProcedureNonQueryAsync(connection, procedureName, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and options that does not return results.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows affected.</returns>
    public static Task<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, object? parameters, CommandOptions options, CancellationToken cancellationToken = default)
    {
        return ExecuteNonQueryCoreAsync(connection, procedureName, parameters, options, CommandType.StoredProcedure, cancellationToken);
    }
}

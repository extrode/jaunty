using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a stored procedure and returns the results as a list.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <returns>A list of mapped entities.</returns>
    public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName) where T : new()
    {
        return ExecuteStoredProcedure<T>(connection, procedureName, null, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and returns the results as a list.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <returns>A list of mapped entities.</returns>
    public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName, object? parameters) where T : new()
    {
        return ExecuteStoredProcedure<T>(connection, procedureName, parameters, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and options, returning the results as a list.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <returns>A list of mapped entities.</returns>
    public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.Query<T>(procedureName, parameters!, spOptions);
    }

    /// <summary>
    /// Executes a stored procedure and returns the first result.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <returns>The first mapped entity.</returns>
    public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName) where T : new()
    {
        return ExecuteStoredProcedureFirst<T>(connection, procedureName, null, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and returns the first result.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <returns>The first mapped entity.</returns>
    public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName, object? parameters) where T : new()
    {
        return ExecuteStoredProcedureFirst<T>(connection, procedureName, parameters, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and options, returning the first result.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <returns>The first mapped entity.</returns>
    public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.QueryFirst<T>(procedureName, parameters!, spOptions);
    }

    /// <summary>
    /// Executes a stored procedure and returns the first result or default.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <returns>The first mapped entity or default.</returns>
    public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName) where T : new()
    {
        return ExecuteStoredProcedureFirstOrDefault<T>(connection, procedureName, null, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and returns the first result or default.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <returns>The first mapped entity or default.</returns>
    public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName, object? parameters) where T : new()
    {
        return ExecuteStoredProcedureFirstOrDefault<T>(connection, procedureName, parameters, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and options, returning the first result or default.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="options">Command options (transaction, timeout, mapper).</param>
    /// <returns>The first mapped entity or default.</returns>
    public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options) where T : new()
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.QueryFirstOrDefault<T>(procedureName, parameters!, spOptions);
    }

    /// <summary>
    /// Executes a stored procedure and returns a scalar value.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <returns>The scalar value.</returns>
    public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName)
    {
        return ExecuteStoredProcedureScalar<T>(connection, procedureName, (object?)null, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and returns a scalar value.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <returns>The scalar value.</returns>
    public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName, object? parameters)
    {
        return ExecuteStoredProcedureScalar<T>(connection, procedureName, parameters, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and options, returning a scalar value.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>The scalar value.</returns>
    public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options)
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.QueryScalar<T>(procedureName, parameters!, spOptions);
    }

    /// <summary>
    /// Executes a stored procedure that does not return results (INSERT, UPDATE, DELETE).
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <returns>The number of rows affected.</returns>
    public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName)
    {
        return ExecuteStoredProcedureNonQuery(connection, procedureName, null, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters that does not return results.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <returns>The number of rows affected.</returns>
    public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName, object? parameters)
    {
        return ExecuteStoredProcedureNonQuery(connection, procedureName, parameters, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and options that does not return results.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">The parameters to pass to the stored procedure.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>The number of rows affected.</returns>
    public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName, object? parameters, CommandOptions options)
    {
        return ExecuteNonQueryCore(connection, procedureName, parameters, options, CommandType.StoredProcedure);
    }
}

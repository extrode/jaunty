using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a query and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>The value of the first column of the first row in the result set.</returns>
    public static T QueryScalar<T>(this IDbConnection connection, string sql)
    {
        return QueryScalarCore<T>(connection, sql, null, default);
    }

    /// <summary>
    /// Executes a query with parameters and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <returns>The value of the first column of the first row in the result set.</returns>
    public static T QueryScalar<T>(this IDbConnection connection, string sql, object parameters)
    {
        return QueryScalarCore<T>(connection, sql, parameters, default);
    }

    /// <summary>
    /// Executes a query with command options and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">Command options (transaction, timeout, custom mapper).</param>
    /// <returns>The value of the first column of the first row in the result set.</returns>
    public static T QueryScalar<T>(this IDbConnection connection, string sql, CommandOptions<T> options)
    {
        return QueryScalarCore(connection, sql, null, options);
    }

    /// <summary>
    /// Executes a query with parameters and command options and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="options">Command options (transaction, timeout, custom mapper).</param>
    /// <returns>The value of the first column of the first row in the result set.</returns>
    public static T QueryScalar<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options)
    {
        return QueryScalarCore(connection, sql, parameters, options);
    }
}

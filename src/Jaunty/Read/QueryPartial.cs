using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a query and returns the results as a list of entities.
    /// Uses partial mapping mode where only properties with matching columns in the result set are mapped.
    /// Properties without matching columns are left with their default values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>A list of entities of type T.</returns>
    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql) where T : new()
    {
        return QueryCore<T>(connection, sql, null, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a query with parameters and returns the results as a list of entities.
    /// Uses partial mapping mode where only properties with matching columns in the result set are mapped.
    /// Properties without matching columns are left with their default values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <returns>A list of entities of type T.</returns>
    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object parameters) where T : new()
    {
        return QueryCore<T>(connection, sql, parameters, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a query with command options and returns the results as a list of entities.
    /// Uses partial mapping mode where only properties with matching columns in the result set are mapped.
    /// Properties without matching columns are left with their default values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">Command options (transaction, timeout, custom mapper).</param>
    /// <returns>A list of entities of type T.</returns>
    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
    {
        return QueryCore<T>(connection, sql, null, options, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a query with parameters and command options and returns the results as a list of entities.
    /// Uses partial mapping mode where only properties with matching columns in the result set are mapped.
    /// Properties without matching columns are left with their default values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="options">Command options (transaction, timeout, custom mapper).</param>
    /// <returns>A list of entities of type T.</returns>
    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
    {
        return QueryCore<T>(connection, sql, parameters, options, MappingMode.Projection);
    }
}

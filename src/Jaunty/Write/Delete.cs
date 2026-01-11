using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    #region Delete By Entity

    /// <summary>
    /// Deletes an entity from the database.
    /// Uses the primary key(s) to identify the row to delete.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to delete.</param>
    /// <returns>Number of rows affected.</returns>
    public static int Delete<T>(this IDbConnection connection, T entity) where T : class, new()
    {
        return DeleteByEntityCore(connection, entity, default);
    }

    /// <summary>
    /// Deletes an entity from the database with command options.
    /// Uses the primary key(s) to identify the row to delete.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>Number of rows affected.</returns>
    public static int Delete<T>(this IDbConnection connection, T entity, CommandOptions options) where T : class, new()
    {
        return DeleteByEntityCore(connection, entity, options);
    }

    #endregion

    #region Delete By ID

    /// <summary>
    /// Deletes an entity by its primary key value.
    /// Only works for entities with a single primary key.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <returns>Number of rows affected.</returns>
    public static int Delete<T>(this IDbConnection connection, object id) where T : class, new()
    {
        return DeleteByIdCore<T>(connection, id, default);
    }

    /// <summary>
    /// Deletes an entity by its primary key value with command options.
    /// Only works for entities with a single primary key.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>Number of rows affected.</returns>
    public static int Delete<T>(this IDbConnection connection, object id, CommandOptions options) where T : class, new()
    {
        return DeleteByIdCore<T>(connection, id, options);
    }

    #endregion
}

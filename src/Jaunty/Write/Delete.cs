using System.Data;

using Jaunty.Core;
using Jaunty.Interfaces;

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

    #region Delete by IEntity<T>

    /// <summary>
    /// Deletes an entity of type <typeparamref name="T"/> identified by the specified primary key value.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/>.</typeparam>
    /// <typeparam name="TId">The type of the primary key.</typeparam>
    /// <param name="connection">The open <see cref="IDbConnection"/> used to execute the delete command.</param>
    /// <param name="id">The primary key value of the entity to delete.</param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    public static int Delete<T, TId>(this IDbConnection connection, TId id) where T : IEntity<TId>
    {
        return DeleteByIdCore<T, TId>(connection, id, default);
    }

    /// <summary>
    /// Deletes an entity of type <typeparamref name="T"/> identified by the specified primary key value.
    /// </summary>
    /// <remarks>
    /// This overload works for entities that have a single primary key. The <paramref name="options"/>
    /// parameter allows callers to customize command execution (e.g., timeout, transaction, etc.).
    /// </remarks>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/>.</typeparam>
    /// <typeparam name="TId">The type of the primary key.</typeparam>
    /// <param name="connection">The open <see cref="IDbConnection"/> used to execute the delete command.</param>
    /// <param name="id">The primary key value of the entity to delete.</param>
    /// <param name="options">Additional command options such as timeout or transaction settings.</param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    public static int Delete<T, TId>(this IDbConnection connection, TId id, CommandOptions options) where T : IEntity<TId>
    {
        return DeleteByIdCore<T, TId>(connection, id, options);
    }

    #endregion
}

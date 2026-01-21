using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Interfaces;

namespace Jaunty;

public static partial class Jaunty
{
    #region DeleteAsync By Entity

    /// <summary>
    /// Asynchronously deletes an entity from the database.
    /// Uses the primary key(s) to identify the row to delete.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows affected.</returns>
    public static Task<int> DeleteAsync<T>(this DbConnection connection, T entity, CancellationToken cancellationToken = default) where T : class, new()
    {
        return DeleteByEntityCoreAsync(connection, entity, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes an entity from the database with command options.
    /// Uses the primary key(s) to identify the row to delete.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows affected.</returns>
    public static Task<int> DeleteAsync<T>(this DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return DeleteByEntityCoreAsync(connection, entity, options, cancellationToken);
    }

    #endregion

    #region DeleteAsync By ID

    /// <summary>
    /// Asynchronously deletes an entity by its primary key value.
    /// Only works for entities with a single primary key.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows affected.</returns>
    public static Task<int> DeleteAsync<T>(this DbConnection connection, object id, CancellationToken cancellationToken = default) where T : class, new()
    {
        return DeleteByIdCoreAsync<T>(connection, id, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes an entity by its primary key value with command options.
    /// Only works for entities with a single primary key.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows affected.</returns>
    public static Task<int> DeleteAsync<T>(this DbConnection connection, object id, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return DeleteByIdCoreAsync<T>(connection, id, options, cancellationToken);
    }

    #endregion

    #region DeleteAsync by IEntity<T>

    /// <summary>
    /// Deletes an entity of type <typeparamref name="T"/> identified by the specified primary key value.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/>.</typeparam>
    /// <typeparam name="TId">The type of the primary key.</typeparam>
    /// <param name="connection">The open <see cref="DbConnection"/> used to execute the delete command.</param>
    /// <param name="id">The primary key value of the entity to delete.</param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    public static async Task<int> DeleteAsync<T, TId>(this DbConnection connection, TId id, CancellationToken cancellationToken = default) where T : IEntity<TId>
    {
        return await DeleteByIdCoreAsync<T, TId>(connection, id, default, cancellationToken);
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
    /// <param name="connection">The open <see cref="DbConnection"/> used to execute the delete command.</param>
    /// <param name="id">The primary key value of the entity to delete.</param>
    /// <param name="options">Additional command options such as timeout or transaction settings.</param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    public static async Task<int> DeleteAsync<T, TId>(this DbConnection connection, TId id, CommandOptions options, CancellationToken cancellationToken = default) where T : IEntity<TId>
    {
        return await DeleteByIdCoreAsync<T, TId>(connection, id, options, cancellationToken);
    }

    #endregion
}

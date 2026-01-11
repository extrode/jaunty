using System.Data.Common;

using Jaunty.Core;

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
}

using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously updates an entity in the database.
    /// Uses the primary key(s) to identify the row to update.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows affected.</returns>
    public static Task<int> UpdateAsync<T>(this DbConnection connection, T entity, CancellationToken cancellationToken = default) where T : class, new()
    {
        return UpdateCoreAsync(connection, entity, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously updates an entity in the database with command options.
    /// Uses the primary key(s) to identify the row to update.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to update.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows affected.</returns>
    public static Task<int> UpdateAsync<T>(this DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return UpdateCoreAsync(connection, entity, options, cancellationToken);
    }
}

using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Updates an entity in the database.
    /// Uses the primary key(s) to identify the row to update.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to update.</param>
    /// <returns>Number of rows affected.</returns>
    public static int Update<T>(this IDbConnection connection, T entity) where T : class, new()
    {
        return UpdateCore(connection, entity, default);
    }

    /// <summary>
    /// Updates an entity in the database with command options.
    /// Uses the primary key(s) to identify the row to update.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to update.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>Number of rows affected.</returns>
    public static int Update<T>(this IDbConnection connection, T entity, CommandOptions options) where T : class, new()
    {
        return UpdateCore(connection, entity, options);
    }
}

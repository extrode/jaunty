using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Inserts an entity into the database.
    /// Returns the generated identity value for identity columns, or 1 for non-identity inserts.
    /// For entities implementing IEntity or IEntity&lt;T&gt;, the Id property is automatically populated.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to insert.</param>
    /// <returns>Generated identity value, or 1 for non-identity inserts.</returns>
    public static long Insert<T>(this IDbConnection connection, T entity) where T : class, new()
    {
        return InsertCore(connection, entity, default);
    }

    /// <summary>
    /// Inserts an entity into the database with command options.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <returns>Generated identity value, or 1 for non-identity inserts.</returns>
    public static long Insert<T>(this IDbConnection connection, T entity, CommandOptions options) where T : class, new()
    {
        return InsertCore(connection, entity, options);
    }
}

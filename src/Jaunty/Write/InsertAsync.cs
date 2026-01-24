using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously inserts an entity into the database.
    /// Returns the generated identity value for identity columns, or 1 for non-identity inserts.
    /// For entities implementing IEntity or IEntity&lt;T&gt;, the Id property is automatically populated.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated identity value, or 1 for non-identity inserts.</returns>
    public static Task<long> InsertAsync<T>(this IDbConnection connection, T entity, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : InsertCoreAsync(dbConnection, entity, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously inserts an entity into the database with command options.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated identity value, or 1 for non-identity inserts.</returns>
    public static Task<long> InsertAsync<T>(this IDbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : InsertCoreAsync(dbConnection, entity, options, cancellationToken);
    }
}

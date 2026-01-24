using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously performs an upsert (INSERT or UPDATE if exists) operation on an entity.
    /// If the entity exists (based on primary key), it updates; otherwise, it inserts.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection (must be DbConnection for async).</param>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of affected rows.</returns>
    public static Task<int> UpsertAsync<T>(this IDbConnection connection, T entity, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : UpsertCoreAsync(dbConnection, entity, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously performs an upsert (INSERT or UPDATE if exists) operation on an entity with command options.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection (must be DbConnection for async).</param>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="options">Command options (transaction, timeout).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of affected rows.</returns>
    public static Task<int> UpsertAsync<T>(this IDbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : UpsertCoreAsync(dbConnection, entity, options, cancellationToken);
    }

    private static async Task<int> UpsertCoreAsync<T>(DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (!cached.SupportsUpsert)
            throw new InvalidOperationException($"The database dialect does not support upsert operations.");

        if (string.IsNullOrEmpty(cached.UpsertSql))
            throw new InvalidOperationException($"Cannot upsert entity of type '{typeof(T).Name}': No primary key found or no upsertable columns.");

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            await using DbCommand command = connection.CreateCommand();
#else
            using DbCommand command = connection.CreateCommand();
#endif

            command.CommandText = cached.UpsertSql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            // Bind parameters from entity properties
            BindUpsertParameters(command, entity, cached.Metadata);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }
}

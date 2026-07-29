using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously deletes multiple entities from the database in a single transaction.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk delete against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to delete. Each entity's primary key is used to identify the row to delete.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows deleted.</returns>
    /// <remarks>
    /// <para>
    /// This method performs a bulk delete operation within a transaction. All entities are deleted 
    /// atomically - if any delete fails, the entire operation is rolled back.
    /// </para>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong>
    /// </para>
    /// <para>
    /// <strong>Performance note:</strong> This method issues one <c>DELETE</c> round trip per entity
    /// in a sequential loop; unlike <see cref="BulkInsertAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>,
    /// there is no multi-row batching for the delete path. For large collections, this is the dominant
    /// cost driver.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }
    /// }
    ///
    /// // Async bulk delete multiple products
    /// var productsToDelete = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1 },
    ///     new Product { Id = 2 },
    ///     new Product { Id = 3 }
    /// };
    /// 
    /// int deleted = await connection.BulkDeleteAsync(productsToDelete);
    /// Console.WriteLine($"Deleted {deleted} products");
    /// </code>
    /// </example>
    /// <seealso cref="BulkDeleteAsync{T}(IDbConnection, IEnumerable{T}, CommandOptions, CancellationToken)"/>
    /// <seealso cref="BulkDelete{T}(IDbConnection, IEnumerable{T})"/>
    public static ValueTask<int> BulkDeleteAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkDeleteAsync(dbConnection, entities, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes multiple entities from the database in a single transaction with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk delete against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to delete.</param>
    /// <param name="options">
    /// Command options for configuring the bulk delete execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows deleted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async bulk delete with transaction
    /// using var tx = connection.BeginTransaction();
    /// var productsToDelete = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1 },
    ///     new Product { Id = 2 }
    /// };
    /// 
    /// int deleted = await connection.BulkDeleteAsync(
    ///     productsToDelete, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="BulkDeleteAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    public static ValueTask<int> BulkDeleteAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkDeleteCoreAsync(dbConnection, entities, options, ignoreConstraints: false, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes multiple entities from the database, bypassing foreign key constraint checks.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk delete against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to delete.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows deleted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use only for migrations,
    /// data cleanup, or scenarios where you explicitly don't need FK validation.
    /// </para>
    /// <para>
    /// <strong>Caution:</strong> This can leave orphaned records in child tables.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async bulk delete without FK checks
    /// var productsToDelete = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1 },
    ///     new Product { Id = 2 }
    /// };
    /// 
    /// int deleted = await connection.BulkDeleteIgnoreConstraintsAsync(productsToDelete);
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling (e.g., SQL Server).
    /// </exception>
    /// <seealso cref="BulkDeleteIgnoreConstraintsAsync{T}(IDbConnection, IEnumerable{T}, CommandOptions, CancellationToken)"/>
    /// <seealso cref="BulkDeleteAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    public static ValueTask<int> BulkDeleteIgnoreConstraintsAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkDeleteIgnoreConstraintsAsync(dbConnection, entities, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes multiple entities from the database, bypassing foreign key constraint checks, with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk delete against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to delete.</param>
    /// <param name="options">
    /// Command options for configuring the bulk delete execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows deleted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use with caution.
    /// </para>
    /// <para>
    /// This can leave orphaned records in child tables.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async bulk delete without FK checks with transaction
    /// using var tx = connection.BeginTransaction();
    /// var productsToDelete = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1 },
    ///     new Product { Id = 2 }
    /// };
    /// 
    /// int deleted = await connection.BulkDeleteIgnoreConstraintsAsync(
    ///     productsToDelete, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling.
    /// </exception>
    /// <seealso cref="BulkDeleteIgnoreConstraintsAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<int> BulkDeleteIgnoreConstraintsAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : BulkDeleteCoreAsync(dbConnection, entities, options, ignoreConstraints: true, cancellationToken);
    }

    private static ValueTask<int> BulkDeleteCoreAsync<T>(DbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints, CancellationToken cancellationToken) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif

        IList<T> entityList = entities as IList<T> ?? entities.ToList();
        if (entityList.Count == 0)
            return new ValueTask<int>(0);

        BulkEntityValidator.ThrowIfAnyNull(entityList, nameof(entities));

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (!cached.HasPrimaryKey)
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': No primary key found.");

        if (string.IsNullOrEmpty(cached.DeleteSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': Delete SQL could not be generated.");

        var bulkParameters = new BulkOperationParameters("BulkDelete", typeof(T), entityList.Count);

        // AUD-R26: the whole operation is reported once, not once per statement - a 100,000-row
        // BulkInsert is one logical write, and firing the pipeline per row would both swamp an
        // auditor and cost more than the bulk path saves. The body below is unchanged; it lives in
        // a local function so the transaction, FK-toggle and rollback logic is captured rather than
        // re-threaded through a new signature.

        return WriteInterception.ExecuteAsync(
            cached.DeleteSql,
            bulkParameters,
            connection,
            options.CommandType,
            Body,
            cancellationToken);

        async ValueTask<int> Body()
        {
            WriteInterception.Log(cached.DeleteSql, bulkParameters);

            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

            if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
                throw new NotSupportedException(
                    $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                    "Use BulkDeleteAsync instead, or disable constraints manually before calling this method.");

            ForeignKeyToggleCoordinator.ValidateTransactionCompatibility(ignoreConstraints, dialect, options.Transaction, connection.GetType().Name);
            bool requiresAutocommit = ForeignKeyToggleCoordinator.RequiresPreTransactionToggle(ignoreConstraints, dialect);

            // Note: Native bulk DELETE is not widely supported by database providers.
            // We fall back to standard parameterized DELETE statements.

            bool wasClosed = connection.State == ConnectionState.Closed;
            DbTransaction? transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);
            bool ownTransaction = transaction is null;

            try
            {
                if (wasClosed)
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                if (ignoreConstraints && requiresAutocommit)
                    await ForeignKeyToggleCoordinator.DisableAsync(connection, dialect, null, cancellationToken).ConfigureAwait(false);

                if (ownTransaction)
                {
    #if NET8_0_OR_GREATER
                    transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    #else
                    transaction = (DbTransaction)connection.BeginTransaction();
    #endif
                }

                if (ignoreConstraints && !requiresAutocommit)
                    await ForeignKeyToggleCoordinator.DisableAsync(connection, dialect, transaction, cancellationToken).ConfigureAwait(false);

                int totalDeleted = 0;

                try
                {
    #if NET8_0_OR_GREATER
                    DbCommand command = connection.CreateCommand();
                    await using var commandDisposer = command.ConfigureAwait(false);
    #else
                    using DbCommand command = connection.CreateCommand();
    #endif
                    command.Transaction = transaction;
                    command.CommandText = cached.DeleteSql;

                    if (options.CommandTimeout.HasValue)
                        command.CommandTimeout = options.CommandTimeout.Value;

                    PrepareDeleteParameters(command, cached.Metadata);

                    Action<IDataParameterCollection, T>? valueSetter = WriteParameterCache<T>.DeleteValueSetter;
                    if (valueSetter == null)
                    {
                        throw new InvalidOperationException($"No parameter binder found for type '{typeof(T).Name}'. Ensure source generation or reflection extension is used.");
                    }

                    DbParameterCollection pCollection = command.Parameters;

                    // Set first entity values before Prepare() so providers can infer parameter types.
                    // Prepare() is a best-effort optimization; some providers (e.g. SQL Server on .NET Framework)
                    // require explicit DbType on all parameters, which we can't guarantee here.
                    bool isFirst = true;
                    foreach (T? entity in entityList)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        valueSetter(pCollection, entity);
                        if (isFirst)
                        {
                            try { command.Prepare(); } catch { /* Best effort — not all providers support this */ }
                            isFirst = false;
                        }
                        totalDeleted += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (ignoreConstraints && !requiresAutocommit)
                        await ForeignKeyToggleCoordinator.EnableAsync(connection, dialect, transaction, cancellationToken).ConfigureAwait(false);

                    if (ownTransaction)
                    {
    #if NET8_0_OR_GREATER
                        await transaction!.CommitAsync(cancellationToken).ConfigureAwait(false);
    #else
                        transaction!.Commit();
    #endif
                    }

                    if (ignoreConstraints && requiresAutocommit)
                        await ForeignKeyToggleCoordinator.EnableAsync(connection, dialect, null, cancellationToken).ConfigureAwait(false);

                    return totalDeleted;
                }
                catch
                {
                    if (ignoreConstraints && !requiresAutocommit)
                    {
                        try
                        {
                            await ForeignKeyToggleCoordinator.EnableAsync(connection, dialect, transaction, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch { /* Best effort */ }
                    }

                    if (ownTransaction)
                    {
                        try
                        {
    #if NET8_0_OR_GREATER
                            await transaction!.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
    #else
                            transaction?.Rollback();
    #endif
                        }
                        catch { /* Best effort */ }
                    }

                    if (ignoreConstraints && requiresAutocommit)
                    {
                        try
                        {
                            await ForeignKeyToggleCoordinator.EnableAsync(connection, dialect, null, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch { /* Best effort */ }
                    }

                    throw;
                }
            }
            finally
            {
                if (ownTransaction)
                {
    #if NET8_0_OR_GREATER
                    if (transaction is not null)
                        await transaction.DisposeAsync().ConfigureAwait(false);
    #else
                    transaction?.Dispose();
    #endif
                }

                if (wasClosed && connection.State != ConnectionState.Closed)
                {
    #if NET8_0_OR_GREATER
                    await connection.CloseAsync().ConfigureAwait(false);
    #else
                    await Task.Run(() => connection.Close()).ConfigureAwait(false);
    #endif
                }
            }
        }
    }
}

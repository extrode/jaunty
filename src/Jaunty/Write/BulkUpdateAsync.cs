using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously updates multiple entities in the database in a single transaction.
    /// </summary>
    /// <typeparam name="T">The entity type to update. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk update against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to update. Each entity's primary key is used to identify the row to update.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows updated.</returns>
    /// <remarks>
    /// <para>
    /// This method performs a bulk update operation within a transaction. All entities are updated 
    /// atomically - if any update fails, the entire operation is rolled back.
    /// </para>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong>
    /// </para>
    /// <para>
    /// <strong>Performance note:</strong> This method issues one <c>ExecuteNonQueryAsync</c> round trip
    /// per entity in a sequential loop; there is no multi-row batching for the update path. For large
    /// collections, this is the dominant cost driver.
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
    /// // Async bulk update multiple products
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget Pro", Price = 24.99m },
    ///     new Product { Id = 2, Name = "Gadget Plus", Price = 34.99m }
    /// };
    /// 
    /// int updated = await connection.BulkUpdateAsync(products);
    /// Console.WriteLine($"Updated {updated} products");
    /// </code>
    /// </example>
    /// <seealso cref="BulkUpdateAsync{T}(IDbConnection, IEnumerable{T}, CommandOptions, CancellationToken)"/>
    /// <seealso cref="BulkUpdate{T}(IDbConnection, IEnumerable{T})"/>
    public static ValueTask<int> BulkUpdateAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : new()
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
            : BulkUpdateAsync(dbConnection, entities, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously updates multiple entities in the database in a single transaction with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to update. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk update against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to update.</param>
    /// <param name="options">
    /// Command options for configuring the bulk update execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows updated.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async bulk update with transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget Pro", Price = 24.99m },
    ///     new Product { Id = 2, Name = "Gadget Plus", Price = 34.99m }
    /// };
    /// 
    /// int updated = await connection.BulkUpdateAsync(
    ///     products, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="BulkUpdateAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    public static ValueTask<int> BulkUpdateAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
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
            : BulkUpdateCoreAsync(dbConnection, entities, options, ignoreConstraints: false, cancellationToken);
    }

    /// <summary>
    /// Asynchronously updates multiple entities in the database, bypassing foreign key constraint checks.
    /// </summary>
    /// <typeparam name="T">The entity type to update. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk update against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to update.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows updated.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use only for migrations,
    /// data corrections, or scenarios where you explicitly don't need FK validation.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async bulk update without FK checks
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget", CategoryId = 999 },
    ///     new Product { Id = 2, Name = "Gadget", CategoryId = 999 }
    /// };
    /// 
    /// int updated = await connection.BulkUpdateIgnoreConstraintsAsync(products);
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling (e.g., SQL Server).
    /// </exception>
    /// <seealso cref="BulkUpdateIgnoreConstraintsAsync{T}(IDbConnection, IEnumerable{T}, CommandOptions, CancellationToken)"/>
    /// <seealso cref="BulkUpdateAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    public static ValueTask<int> BulkUpdateIgnoreConstraintsAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : new()
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
            : BulkUpdateIgnoreConstraintsAsync(dbConnection, entities, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously updates multiple entities in the database, bypassing foreign key constraint checks, with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to update. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk update against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entities">The collection of entities to update.</param>
    /// <param name="options">
    /// Command options for configuring the bulk update execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows updated.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use with caution.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async bulk update without FK checks with transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget", CategoryId = 999 },
    ///     new Product { Id = 2, Name = "Gadget", CategoryId = 999 }
    /// };
    /// 
    /// int updated = await connection.BulkUpdateIgnoreConstraintsAsync(
    ///     products, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling.
    /// </exception>
    /// <seealso cref="BulkUpdateIgnoreConstraintsAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<int> BulkUpdateIgnoreConstraintsAsync<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
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
            : BulkUpdateCoreAsync(dbConnection, entities, options, ignoreConstraints: true, cancellationToken);
    }

    private static ValueTask<int> BulkUpdateCoreAsync<T>(DbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints, CancellationToken cancellationToken) where T : new()
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
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No primary key found.");

        if (string.IsNullOrEmpty(cached.UpdateSql))
            throw new InvalidOperationException($"Cannot update entity of type '{typeof(T).Name}': No updateable columns found.");

        var bulkParameters = new BulkOperationParameters("BulkUpdate", typeof(T), entityList.Count);

        // AUD-R26: the whole operation is reported once, not once per statement - a 100,000-row
        // BulkInsert is one logical write, and firing the pipeline per row would both swamp an
        // auditor and cost more than the bulk path saves. The body below is unchanged; it lives in
        // a local function so the transaction, FK-toggle and rollback logic is captured rather than
        // re-threaded through a new signature.

        return CommandObservation.ExecuteAsync(
            cached.UpdateSql,
            bulkParameters,
            connection,
            options.CommandType,
            Body,
            cancellationToken);

        async ValueTask<int> Body()
        {
            CommandObservation.Log(cached.UpdateSql, bulkParameters);

            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

            if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
                throw new NotSupportedException(
                    $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                    "Use BulkUpdateAsync instead, or disable constraints manually before calling this method.");

            ForeignKeyToggleCoordinator.ValidateTransactionCompatibility(ignoreConstraints, dialect, options.Transaction, connection.GetType().Name);
            bool requiresAutocommit = ForeignKeyToggleCoordinator.RequiresPreTransactionToggle(ignoreConstraints, dialect);

            // Note: Native bulk UPDATE is not widely supported by database providers.
            // We fall back to standard parameterized UPDATE statements.

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

                int totalUpdated = 0;

                try
                {
    #if NET8_0_OR_GREATER
                    DbCommand command = connection.CreateCommand();
                    await using var commandDisposer = command.ConfigureAwait(false);
    #else
                    using DbCommand command = connection.CreateCommand();
    #endif
                    command.Transaction = transaction;
                    command.CommandText = cached.UpdateSql;

                    if (options.CommandTimeout.HasValue)
                        command.CommandTimeout = options.CommandTimeout.Value;

                    PrepareUpdateParameters(command, cached.Metadata);

                    Action<IDataParameterCollection, T>? valueSetter = WriteParameterCache<T>.UpdateValueSetter;
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
                        totalUpdated += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

                    return totalUpdated;
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

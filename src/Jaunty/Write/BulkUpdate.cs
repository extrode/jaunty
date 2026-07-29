using System.Data;

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
    /// Updates multiple entities in the database in a single transaction.
    /// </summary>
    /// <typeparam name="T">The entity type to update. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk update against.</param>
    /// <param name="entities">The collection of entities to update. Each entity's primary key is used to identify the row to update.</param>
    /// <returns>The number of rows updated.</returns>
    /// <remarks>
    /// <para>
    /// This method performs a bulk update operation within a transaction. All entities are updated 
    /// atomically - if any update fails, the entire operation is rolled back.
    /// </para>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong>
    /// </para>
    /// <para>
    /// Each entity must have its primary key property set to identify the row to update.
    /// </para>
    /// <para>
    /// <strong>Performance note:</strong> This method issues one <c>UPDATE</c> round trip per entity
    /// in a sequential loop; there is no multi-row batching for the update path. For large collections,
    /// this is the dominant cost driver.
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
    /// // Bulk update multiple products
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget Pro", Price = 24.99m },
    ///     new Product { Id = 2, Name = "Gadget Plus", Price = 34.99m },
    ///     new Product { Id = 3, Name = "Gizmo Max", Price = 44.99m }
    /// };
    /// 
    /// int updated = connection.BulkUpdate(products);
    /// Console.WriteLine($"Updated {updated} products");
    /// </code>
    /// </example>
    /// <seealso cref="BulkUpdate{T}(IDbConnection, IEnumerable{T}, CommandOptions)"/>
    /// <seealso cref="BulkUpdateIgnoreConstraints{T}(IDbConnection, IEnumerable{T})"/>
    /// <seealso cref="BulkUpdateAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    public static int BulkUpdate<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkUpdate(connection, entities, default);
    }

    /// <summary>
    /// Updates multiple entities in the database in a single transaction with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to update. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk update against.</param>
    /// <param name="entities">The collection of entities to update.</param>
    /// <param name="options">
    /// Command options for configuring the bulk update execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>The number of rows updated.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Bulk update with transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget Pro", Price = 24.99m },
    ///     new Product { Id = 2, Name = "Gadget Plus", Price = 34.99m }
    /// };
    /// 
    /// int updated = connection.BulkUpdate(products, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="BulkUpdate{T}(IDbConnection, IEnumerable{T})"/>
    public static int BulkUpdate<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkUpdateCore(connection, entities, options, ignoreConstraints: false);
    }

    /// <summary>
    /// Updates multiple entities in the database, bypassing foreign key constraint checks.
    /// </summary>
    /// <typeparam name="T">The entity type to update. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk update against.</param>
    /// <param name="entities">The collection of entities to update.</param>
    /// <returns>The number of rows updated.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use only for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Database migrations</description></item>
    /// <item><description>Data corrections where you control referential integrity manually</description></item>
    /// <item><description>Scenarios where you explicitly don't need FK validation</description></item>
    /// </list>
    /// <para>
    /// <strong>Note:</strong> This method is not supported on SQL Server and other databases that 
    /// don't support session-level foreign key toggling.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Bulk update without FK checks (useful for data corrections)
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget", CategoryId = 999 }, // Category 999 may not exist
    ///     new Product { Id = 2, Name = "Gadget", CategoryId = 999 }
    /// };
    /// 
    /// int updated = connection.BulkUpdateIgnoreConstraints(products);
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling (e.g., SQL Server).
    /// </exception>
    /// <seealso cref="BulkUpdateIgnoreConstraints{T}(IDbConnection, IEnumerable{T}, CommandOptions)"/>
    /// <seealso cref="BulkUpdate{T}(IDbConnection, IEnumerable{T})"/>
    public static int BulkUpdateIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkUpdateIgnoreConstraints(connection, entities, default);
    }

    /// <summary>
    /// Updates multiple entities in the database, bypassing foreign key constraint checks, with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to update. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk update against.</param>
    /// <param name="entities">The collection of entities to update.</param>
    /// <param name="options">
    /// Command options for configuring the bulk update execution.
    /// </param>
    /// <returns>The number of rows updated.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use with caution.
    /// </para>
    /// <para>
    /// Foreign key checks are automatically re-enabled after the update completes (or fails).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Bulk update without FK checks with transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget", CategoryId = 999 },
    ///     new Product { Id = 2, Name = "Gadget", CategoryId = 999 }
    /// };
    /// 
    /// int updated = connection.BulkUpdateIgnoreConstraints(
    ///     products, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling.
    /// </exception>
    /// <seealso cref="BulkUpdateIgnoreConstraints{T}(IDbConnection, IEnumerable{T})"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static int BulkUpdateIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkUpdateCore(connection, entities, options, ignoreConstraints: true);
    }

    private static int BulkUpdateCore<T>(IDbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints) where T : new()
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
            return 0;

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

        return CommandObservation.Execute(
            cached.UpdateSql,
            bulkParameters,
            connection,
            options.CommandType,
            Body);

        int Body()
        {
            CommandObservation.Log(cached.UpdateSql, bulkParameters);

            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

            if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
                throw new NotSupportedException(
                    $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                    "Use BulkUpdate instead, or disable constraints manually before calling this method.");

            ForeignKeyToggleCoordinator.ValidateTransactionCompatibility(ignoreConstraints, dialect, options.Transaction, connection.GetType().Name);
            bool requiresAutocommit = ForeignKeyToggleCoordinator.RequiresPreTransactionToggle(ignoreConstraints, dialect);

            // Note: Native bulk UPDATE is not widely supported by database providers.
            // Most databases (SQL Server, PostgreSQL, MySQL) don't have native bulk UPDATE APIs.
            // We fall back to standard parameterized UPDATE statements which are still efficient
            // when executed within a single transaction.

            bool wasClosed = connection.State == ConnectionState.Closed;

            // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
            // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
            // IDbTransaction through it throws an opaque InvalidCastException. Validate via
            // AsyncTransactionValidator first (mirroring InsertCoreDirect) so an incompatible
            // transaction gets Jaunty's clear ArgumentException instead.
            IDbTransaction? transaction = connection is System.Data.Common.DbConnection
                ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                : options.Transaction;
            bool ownTransaction = transaction is null;

            try
            {
                if (wasClosed)
                    connection.Open();

                if (ignoreConstraints && requiresAutocommit)
                    ForeignKeyToggleCoordinator.DisableSync(connection, dialect, null);

                if (ownTransaction)
                    transaction = connection.BeginTransaction();

                if (ignoreConstraints && !requiresAutocommit)
                    ForeignKeyToggleCoordinator.DisableSync(connection, dialect, transaction);

                int totalUpdated = 0;

                try
                {
                    using IDbCommand command = connection.CreateCommand();
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

                    IDataParameterCollection pCollection = command.Parameters;

                    // Set first entity values before Prepare() so providers can infer parameter types.
                    // Prepare() is a best-effort optimization; some providers (e.g. SQL Server on .NET Framework)
                    // require explicit DbType on all parameters, which we can't guarantee here.
                    bool isFirst = true;
                    foreach (T? entity in entityList)
                    {
                        valueSetter(pCollection, entity);
                        if (isFirst)
                        {
                            try { command.Prepare(); } catch { /* Best effort — not all providers support this */ }
                            isFirst = false;
                        }
                        totalUpdated += command.ExecuteNonQuery();
                    }

                    if (ignoreConstraints && !requiresAutocommit)
                        ForeignKeyToggleCoordinator.EnableSync(connection, dialect, transaction);

                    if (ownTransaction)
                        transaction!.Commit();

                    if (ignoreConstraints && requiresAutocommit)
                        ForeignKeyToggleCoordinator.EnableSync(connection, dialect, null);

                    return totalUpdated;
                }
                catch
                {
                    if (ignoreConstraints && !requiresAutocommit)
                    {
                        try { ForeignKeyToggleCoordinator.EnableSync(connection, dialect, transaction); }
                        catch { /* Best effort */ }
                    }

                    if (ownTransaction)
                    {
                        try { transaction?.Rollback(); }
                        catch { /* Best effort - do not mask the original exception */ }
                    }

                    if (ignoreConstraints && requiresAutocommit)
                    {
                        try { ForeignKeyToggleCoordinator.EnableSync(connection, dialect, null); }
                        catch { /* Best effort */ }
                    }

                    throw;
                }
            }
            finally
            {
                if (ownTransaction)
                    transaction?.Dispose();

                if (wasClosed && connection.State != ConnectionState.Closed)
                    connection.Close();
            }
        }
    }
}
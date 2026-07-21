using System.Data;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Deletes multiple entities from the database in a single transaction.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk delete against.</param>
    /// <param name="entities">The collection of entities to delete. Each entity's primary key is used to identify the row to delete.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <remarks>
    /// <para>
    /// This method performs a bulk delete operation within a transaction. All entities are deleted 
    /// atomically - if any delete fails, the entire operation is rolled back.
    /// </para>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong> All dependent records must be deleted 
    /// first, or the delete will fail.
    /// </para>
    /// <para>
    /// Each entity must have its primary key property set to identify the row to delete.
    /// </para>
    /// <para>
    /// <strong>Performance note:</strong> This method issues one <c>DELETE</c> round trip per entity
    /// in a sequential loop; unlike <see cref="BulkInsert{T}(IDbConnection, IEnumerable{T})"/>, there
    /// is no multi-row batching for the delete path. For large collections, this is the dominant cost
    /// driver.
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
    /// // Bulk delete multiple products
    /// var productsToDelete = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1 },
    ///     new Product { Id = 2 },
    ///     new Product { Id = 3 }
    /// };
    ///
    /// int deleted = connection.BulkDelete(productsToDelete);
    /// Console.WriteLine($"Deleted {deleted} products");
    /// </code>
    /// </example>
    /// <seealso cref="BulkDelete{T}(IDbConnection, IEnumerable{T}, CommandOptions)"/>
    /// <seealso cref="BulkDeleteIgnoreConstraints{T}(IDbConnection, IEnumerable{T})"/>
    /// <seealso cref="BulkDeleteAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    public static int BulkDelete<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkDelete(connection, entities, default);
    }

    /// <summary>
    /// Deletes multiple entities from the database in a single transaction with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk delete against.</param>
    /// <param name="entities">The collection of entities to delete.</param>
    /// <param name="options">
    /// Command options for configuring the bulk delete execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>The number of rows deleted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Bulk delete with transaction
    /// using var tx = connection.BeginTransaction();
    /// var productsToDelete = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1 },
    ///     new Product { Id = 2 }
    /// };
    /// 
    /// int deleted = connection.BulkDelete(productsToDelete, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="BulkDelete{T}(IDbConnection, IEnumerable{T})"/>
    public static int BulkDelete<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkDeleteCore(connection, entities, options, ignoreConstraints: false);
    }

    /// <summary>
    /// Deletes multiple entities from the database, bypassing foreign key constraint checks.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk delete against.</param>
    /// <param name="entities">The collection of entities to delete.</param>
    /// <returns>The number of rows deleted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use only for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Database migrations</description></item>
    /// <item><description>Cleanup operations where you will manually clean up child records</description></item>
    /// <item><description>Scenarios where you explicitly don't need FK validation</description></item>
    /// </list>
    /// <para>
    /// <strong>Caution:</strong> This can leave orphaned records in child tables.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> This method is not supported on SQL Server and other databases that 
    /// don't support session-level foreign key toggling.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Bulk delete without FK checks (useful for cleanup during migrations)
    /// var productsToDelete = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1 },
    ///     new Product { Id = 2 }
    /// };
    /// 
    /// int deleted = connection.BulkDeleteIgnoreConstraints(productsToDelete);
    /// // Note: Child records (e.g., OrderItems) may now be orphaned
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling (e.g., SQL Server).
    /// </exception>
    /// <seealso cref="BulkDeleteIgnoreConstraints{T}(IDbConnection, IEnumerable{T}, CommandOptions)"/>
    /// <seealso cref="BulkDelete{T}(IDbConnection, IEnumerable{T})"/>
    public static int BulkDeleteIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkDeleteIgnoreConstraints(connection, entities, default);
    }

    /// <summary>
    /// Deletes multiple entities from the database, bypassing foreign key constraint checks, with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk delete against.</param>
    /// <param name="entities">The collection of entities to delete.</param>
    /// <param name="options">
    /// Command options for configuring the bulk delete execution.
    /// </param>
    /// <returns>The number of rows deleted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use with caution.
    /// </para>
    /// <para>
    /// This can leave orphaned records in child tables. Ensure you handle child records appropriately.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Bulk delete without FK checks with transaction
    /// using var tx = connection.BeginTransaction();
    /// var productsToDelete = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1 },
    ///     new Product { Id = 2 }
    /// };
    /// 
    /// int deleted = connection.BulkDeleteIgnoreConstraints(
    ///     productsToDelete, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling.
    /// </exception>
    /// <seealso cref="BulkDeleteIgnoreConstraints{T}(IDbConnection, IEnumerable{T})"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static int BulkDeleteIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkDeleteCore(connection, entities, options, ignoreConstraints: true);
    }

    private static int BulkDeleteCore<T>(IDbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints) where T : new()
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
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': No primary key found.");

        if (string.IsNullOrEmpty(cached.DeleteSql))
            throw new InvalidOperationException($"Cannot delete entity of type '{typeof(T).Name}': Delete SQL could not be generated.");

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

        if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
            throw new NotSupportedException(
                $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                "Use BulkDelete instead, or disable constraints manually before calling this method.");

        ForeignKeyToggleCoordinator.ValidateTransactionCompatibility(ignoreConstraints, dialect, options.Transaction, connection.GetType().Name);
        bool requiresAutocommit = ForeignKeyToggleCoordinator.RequiresPreTransactionToggle(ignoreConstraints, dialect);

        // Note: Native bulk DELETE is not widely supported by database providers.
        // Most databases (SQL Server, PostgreSQL, MySQL) don't have native bulk DELETE APIs.
        // We fall back to standard parameterized DELETE statements which are still efficient
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

            int totalDeleted = 0;

            try
            {
                using IDbCommand command = connection.CreateCommand();
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
                    totalDeleted += command.ExecuteNonQuery();
                }

                if (ignoreConstraints && !requiresAutocommit)
                    ForeignKeyToggleCoordinator.EnableSync(connection, dialect, transaction);

                if (ownTransaction)
                    transaction!.Commit();

                if (ignoreConstraints && requiresAutocommit)
                    ForeignKeyToggleCoordinator.EnableSync(connection, dialect, null);

                return totalDeleted;
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
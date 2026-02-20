using System.Data;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Inserts multiple entities into the database in a single transaction.
    /// </summary>
    /// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk insert against.</param>
    /// <param name="entities">The collection of entities to insert.</param>
    /// <returns>The number of rows inserted.</returns>
    /// <remarks>
    /// <para>
    /// This method performs a bulk insert operation within a transaction. All entities are inserted 
    /// atomically - if any insert fails, the entire operation is rolled back.
    /// </para>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong> All referenced entities must exist 
    /// in the database before calling this method.
    /// </para>
    /// <para>
    /// For inserting entities without foreign key validation, use 
    /// <see cref="BulkInsertIgnoreConstraints{T}(IDbConnection, IEnumerable{T})"/>.
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
    /// // Bulk insert multiple products
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Name = "Widget", Price = 19.99m },
    ///     new Product { Name = "Gadget", Price = 29.99m },
    ///     new Product { Name = "Gizmo", Price = 39.99m }
    /// };
    /// 
    /// int inserted = connection.BulkInsert(products);
    /// Console.WriteLine($"Inserted {inserted} products");
    /// </code>
    /// </example>
    /// <seealso cref="BulkInsert{T}(IDbConnection, IEnumerable{T}, CommandOptions)"/>
    /// <seealso cref="BulkInsertIgnoreConstraints{T}(IDbConnection, IEnumerable{T})"/>
    /// <seealso cref="BulkInsertAsync{T}(IDbConnection, IEnumerable{T}, CancellationToken)"/>
    public static int BulkInsert<T>(this IDbConnection connection, IEnumerable<T> entities) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkInsert(connection, entities, default);
    }

    /// <summary>
    /// Inserts multiple entities into the database in a single transaction with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk insert against.</param>
    /// <param name="entities">The collection of entities to insert.</param>
    /// <param name="options">
    /// Command options for configuring the bulk insert execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> to use an existing transaction or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>The number of rows inserted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Foreign key constraints are enforced.</strong>
    /// </para>
    /// <para>
    /// If no transaction is provided via <paramref name="options"/>, a new transaction is created 
    /// automatically for the bulk operation.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Bulk insert with existing transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Name = "Widget", Price = 19.99m },
    ///     new Product { Name = "Gadget", Price = 29.99m }
    /// };
    /// 
    /// int inserted = connection.BulkInsert(products, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // Bulk insert with timeout
    /// int inserted = connection.BulkInsert(
    ///     products, 
    ///     CommandOptions.WithTimeout(60));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="BulkInsert{T}(IDbConnection, IEnumerable{T})"/>
    /// <seealso cref="BulkInsertIgnoreConstraints{T}(IDbConnection, IEnumerable{T}, CommandOptions)"/>
    public static int BulkInsert<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkInsertCore(connection, entities, options, ignoreConstraints: false);
    }

    /// <summary>
    /// Inserts multiple entities into the database, bypassing foreign key constraint checks.
    /// </summary>
    /// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk insert against.</param>
    /// <param name="entities">The collection of entities to insert.</param>
    /// <returns>The number of rows inserted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity during the insert. 
    /// Use only for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Database migrations</description></item>
    /// <item><description>Data imports where you control referential integrity manually</description></item>
    /// <item><description>Scenarios where you explicitly don't need FK validation</description></item>
    /// </list>
    /// <para>
    /// <strong>Note:</strong> This method is not supported on SQL Server and other databases that 
    /// don't support session-level foreign key toggling.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Bulk insert without FK checks (useful for data migrations)
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget", CategoryId = 999 }, // Category 999 may not exist
    ///     new Product { Id = 2, Name = "Gadget", CategoryId = 999 }
    /// };
    /// 
    /// int inserted = connection.BulkInsertIgnoreConstraints(products);
    /// Console.WriteLine($"Inserted {inserted} products (FK checks bypassed)");
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling (e.g., SQL Server).
    /// </exception>
    /// <seealso cref="BulkInsertIgnoreConstraints{T}(IDbConnection, IEnumerable{T}, CommandOptions)"/>
    /// <seealso cref="BulkInsert{T}(IDbConnection, IEnumerable{T})"/>
    public static int BulkInsertIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkInsertIgnoreConstraints(connection, entities, default);
    }

    /// <summary>
    /// Inserts multiple entities into the database, bypassing foreign key constraint checks, with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the bulk insert against.</param>
    /// <param name="entities">The collection of entities to insert.</param>
    /// <param name="options">
    /// Command options for configuring the bulk insert execution.
    /// </param>
    /// <returns>The number of rows inserted.</returns>
    /// <remarks>
    /// <para>
    /// <strong>WARNING:</strong> This method temporarily disables referential integrity. Use with caution.
    /// </para>
    /// <para>
    /// Foreign key checks are automatically re-enabled after the insert completes (or fails).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Bulk insert without FK checks with transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = new List&lt;Product&gt;
    /// {
    ///     new Product { Id = 1, Name = "Widget", CategoryId = 999 },
    ///     new Product { Id = 2, Name = "Gadget", CategoryId = 999 }
    /// };
    /// 
    /// int inserted = connection.BulkInsertIgnoreConstraints(
    ///     products, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <exception cref="NotSupportedException">
    /// Thrown if the database provider doesn't support foreign key toggling.
    /// </exception>
    /// <seealso cref="BulkInsertIgnoreConstraints{T}(IDbConnection, IEnumerable{T})"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static int BulkInsertIgnoreConstraints<T>(this IDbConnection connection, IEnumerable<T> entities, CommandOptions options) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif
        return BulkInsertCore(connection, entities, options, ignoreConstraints: true);
    }

    private static int BulkInsertCore<T>(IDbConnection connection, IEnumerable<T> entities, CommandOptions options, bool ignoreConstraints) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entities);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entities is null) throw new ArgumentNullException(nameof(entities));
#endif

        // Materialize to list to avoid multiple enumeration
        var entityList = entities as IList<T> ?? entities.ToList();
        if (entityList.Count == 0)
            return 0;

        CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);

        if (string.IsNullOrEmpty(cached.InsertSql))
            throw new InvalidOperationException($"Cannot insert entity of type '{typeof(T).Name}': No insertable columns found.");

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);

        if (ignoreConstraints && !dialect.SupportsForeignKeyToggle)
            throw new NotSupportedException(
                $"The database provider ({connection.GetType().Name}) does not support session-level foreign key toggling. " +
                "Use BulkInsert instead, or disable constraints manually before calling this method.");

        bool wasClosed = connection.State == ConnectionState.Closed;
        IDbTransaction? transaction = options.Transaction;
        bool ownTransaction = transaction is null;

        try
        {
            if (wasClosed)
                connection.Open();

            // Create our own transaction if one wasn't provided
            if (ownTransaction)
                transaction = connection.BeginTransaction();

            // Disable FK checks if requested
            if (ignoreConstraints)
            {
                using var fkOffCmd = connection.CreateCommand();
                fkOffCmd.Transaction = transaction;
                fkOffCmd.CommandText = dialect.GetDisableForeignKeyChecksSql()!;
                fkOffCmd.ExecuteNonQuery();
            }

            int totalInserted = 0;

            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = cached.InsertSql;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                // Prepare parameters once, reuse for each entity
                PrepareInsertParameters(command, cached.Metadata);

                var valueSetter = WriteParameterCache<T>.InsertValueSetter;
                var pCollection = command.Parameters;

                foreach (var entity in entityList)
                {
                    valueSetter(pCollection, entity);
                    totalInserted += command.ExecuteNonQuery();
                }

                // Re-enable FK checks before commit
                if (ignoreConstraints)
                {
                    using var fkOnCmd = connection.CreateCommand();
                    fkOnCmd.Transaction = transaction;
                    fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                    fkOnCmd.ExecuteNonQuery();
                }

                if (ownTransaction)
                    transaction!.Commit();

                return totalInserted;
            }
            catch
            {
                // Re-enable FK checks even on error
                if (ignoreConstraints)
                {
                    try
                    {
                        using var fkOnCmd = connection.CreateCommand();
                        fkOnCmd.Transaction = transaction;
                        fkOnCmd.CommandText = dialect.GetEnableForeignKeyChecksSql()!;
                        fkOnCmd.ExecuteNonQuery();
                    }
                    catch { /* Best effort */ }
                }

                if (ownTransaction)
                    transaction?.Rollback();

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

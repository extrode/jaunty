using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously performs an upsert (INSERT or UPDATE) operation on an entity.
    /// </summary>
    /// <typeparam name="T">The entity type to upsert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the upsert against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entity">
    /// The entity instance to upsert. The primary key property determines whether to insert or update:
    /// <list type="bullet">
    /// <item><description>If a row with the primary key exists, it is <strong>updated</strong></description></item>
    /// <item><description>If no row with the primary key exists, a new row is <strong>inserted</strong></description></item>
    /// </list>
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the number of affected rows. Typically <c>1</c> for either insert or update operations.
    /// </returns>
    /// <remarks>
    /// <para>
    /// An upsert (also known as MERGE or INSERT ... ON CONFLICT) is a database operation that combines 
    /// INSERT and UPDATE. It's useful when you want to ensure a record exists with specific values, 
    /// regardless of whether it already exists.
    /// </para>
    /// <para>
    /// <strong>Requirements:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>The entity must have a primary key defined</description></item>
    /// <item><description>The database dialect must support upsert operations</description></item>
    /// </list>
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
    /// // Async upsert a product
    /// var product = new Product { Id = 1, Name = "Widget", Price = 19.99m };
    /// int affected = await connection.UpsertAsync(product);
    /// 
    /// // First call: inserts the product (affected = 1)
    /// // Second call with same Id: updates the product (affected = 1)
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the connection is not a <see cref="DbConnection"/>, if the entity has no primary key, 
    /// if the database dialect doesn't support upsert operations, or if no upsertable columns are found.
    /// </exception>
    /// <seealso cref="UpsertAsync{T}(IDbConnection, T, CommandOptions, CancellationToken)"/>
    /// <seealso cref="Upsert{T}(IDbConnection, T)"/>
    /// <seealso cref="InsertAsync{T}(IDbConnection, T, CancellationToken)"/>
    /// <seealso cref="UpdateAsync{T}(IDbConnection, T, CancellationToken)"/>
    public static ValueTask<int> UpsertAsync<T>(this IDbConnection connection, T entity, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : UpsertCoreAsync(dbConnection, entity, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously performs an upsert (INSERT or UPDATE) operation on an entity with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to upsert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the upsert against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entity">The entity instance to upsert.</param>
    /// <param name="options">
    /// Command options for configuring the upsert execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the number of affected rows. Typically <c>1</c> for either insert or update operations.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the upsert within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async upsert with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = new Product { Id = 1, Name = "Widget", Price = 19.99m };
    /// int affected = await connection.UpsertAsync(product, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // Async upsert with timeout
    /// var product = new Product { Id = 1, Name = "Widget", Price = 19.99m };
    /// int affected = await connection.UpsertAsync(
    ///     product, 
    ///     CommandOptions.WithTimeout(30));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the connection is not a <see cref="DbConnection"/>, if the entity has no primary key, 
    /// if the database dialect doesn't support upsert operations, or if no upsertable columns are found.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="UpsertAsync{T}(IDbConnection, T, CancellationToken)"/>
    /// <seealso cref="Upsert{T}(IDbConnection, T, CommandOptions)"/>
    public static ValueTask<int> UpsertAsync<T>(this IDbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : UpsertCoreAsync(dbConnection, entity, options, cancellationToken);
    }

    private static async ValueTask<int> UpsertCoreAsync<T>(DbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken) where T : new()
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

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

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
                await Task.Run(() => connection.Close(), cancellationToken).ConfigureAwait(false);
#endif
            }
        }
    }
}
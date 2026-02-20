using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously updates an entity in the database using its primary key(s).
    /// </summary>
    /// <typeparam name="T">The entity type to update. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the update against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entity">The entity instance with updated values. Primary key properties are used in the WHERE clause.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the number of rows affected by the update. Typically <c>1</c> if the entity was found and updated.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method generates an UPDATE statement using all writable properties except the primary key(s), 
    /// which are used in the WHERE clause to identify the row to update.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> Ensure the entity's primary key property is set to the correct value 
    /// before calling this method, otherwise no rows will be updated.
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
    /// // Update a product's price
    /// var product = new Product { Id = 1, Name = "Widget", Price = 24.99m };
    /// var rows = await connection.UpdateAsync(product);
    /// 
    /// Console.WriteLine($"Updated {rows} row(s)");
    /// </code>
    /// </example>
    /// <seealso cref="Update{T}(IDbConnection, T)"/>
    /// <seealso cref="UpdateAsync{T}(IDbConnection, T, CommandOptions, CancellationToken)"/>
    public static ValueTask<int> UpdateAsync<T>(this IDbConnection connection, T entity, CancellationToken cancellationToken = default) where T : new()
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
            : UpdateCoreAsync(dbConnection, entity, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously updates an entity in the database with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to update. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the update against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entity">The entity instance with updated values.</param>
    /// <param name="options">
    /// Command options for configuring the update execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the number of rows affected by the update.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the update within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Update with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = new Product { Id = 1, Name = "Widget", Price = 24.99m };
    /// var rows = await connection.UpdateAsync(product, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // Update with timeout
    /// var product = new Product { Id = 1, Name = "Widget", Price = 24.99m };
    /// var rows = await connection.UpdateAsync(
    ///     product, 
    ///     CommandOptions.WithTimeout(30));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="UpdateAsync{T}(IDbConnection, T, CancellationToken)"/>
    public static ValueTask<int> UpdateAsync<T>(this IDbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
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
            : UpdateCoreAsync(dbConnection, entity, options, cancellationToken);
    }
}


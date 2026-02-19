using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Interfaces;

namespace Jaunty;

public static partial class Jaunty
{
    #region DeleteAsync By Entity

    /// <summary>
    /// Asynchronously deletes an entity from the database using its primary key(s).
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the delete against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entity">The entity instance to delete. Primary key properties are used in the WHERE clause.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the number of rows affected by the delete. Typically <c>1</c> if the entity was found and deleted.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method generates a DELETE statement using the primary key property(s) in the WHERE clause.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> Ensure the entity's primary key property is set to the correct value 
    /// before calling this method, otherwise no rows will be deleted.
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
    /// // Delete a product entity
    /// var product = new Product { Id = 1 };
    /// var rows = await connection.DeleteAsync(product);
    /// 
    /// Console.WriteLine($"Deleted {rows} row(s)");
    /// </code>
    /// </example>
    /// <seealso cref="DeleteAsync{T}(IDbConnection, T, CommandOptions, CancellationToken)"/>
    /// <seealso cref="DeleteAsync{T}(IDbConnection, object, CancellationToken)"/>
    public static Task<int> DeleteAsync<T>(this IDbConnection connection, T entity, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : DeleteByEntityCoreAsync(dbConnection, entity, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes an entity from the database with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the delete against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entity">The entity instance to delete.</param>
    /// <param name="options">
    /// Command options for configuring the delete execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the number of rows affected by the delete.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the delete within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Delete with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = new Product { Id = 1 };
    /// var rows = await connection.DeleteAsync(product, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // Delete with timeout
    /// var product = new Product { Id = 1 };
    /// var rows = await connection.DeleteAsync(
    ///     product, 
    ///     CommandOptions.WithTimeout(30));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="DeleteAsync{T}(IDbConnection, T, CancellationToken)"/>
    public static Task<int> DeleteAsync<T>(this IDbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : DeleteByEntityCoreAsync(dbConnection, entity, options, cancellationToken);
    }

    #endregion

    #region DeleteAsync By ID

    /// <summary>
    /// Asynchronously deletes an entity by its primary key value.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the delete against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">
    /// The primary key value of the entity to delete. The type must match the entity's primary key type.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the number of rows affected by the delete. Typically <c>1</c> if the entity was found and deleted.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method only works for entities with a single primary key property.
    /// </para>
    /// <para>
    /// For composite keys, use <see cref="DeleteAsync{T}(IDbConnection, T, CancellationToken)"/> with an entity instance.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Delete product by ID
    /// var rows = await connection.DeleteAsync&lt;Product&gt;(1);
    /// 
    /// Console.WriteLine($"Deleted {rows} row(s)");
    /// </code>
    /// </example>
    /// <seealso cref="DeleteAsync{T}(IDbConnection, T, CancellationToken)"/>
    /// <seealso cref="DeleteAsync{T}(IDbConnection, object, CommandOptions, CancellationToken)"/>
    public static Task<int> DeleteAsync<T>(this IDbConnection connection, object id, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : DeleteByIdCoreAsync<T>(dbConnection, id, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes an entity by its primary key value with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the delete against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value of the entity to delete.</param>
    /// <param name="options">
    /// Command options for configuring the delete execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the number of rows affected by the delete.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method only works for entities with a single primary key property.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Delete with transaction
    /// using var tx = connection.BeginTransaction();
    /// var rows = await connection.DeleteAsync&lt;Product&gt;(
    ///     1, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="DeleteAsync{T}(IDbConnection, object, CancellationToken)"/>
    public static Task<int> DeleteAsync<T>(this IDbConnection connection, object id, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : DeleteByIdCoreAsync<T>(dbConnection, id, options, cancellationToken);
    }

    #endregion

    #region DeleteAsync by IEntity&lt;T&gt;

    /// <summary>
    /// Asynchronously deletes an entity of type <typeparamref name="T"/> by its primary key value.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/>.</typeparam>
    /// <typeparam name="TId">The type of the primary key.</typeparam>
    /// <param name="connection">The database connection to execute the delete against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value of the entity to delete.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the number of rows affected by the delete operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This overload is specifically designed for entities that implement the <see cref="IEntity{TId}"/> interface,
    /// which provides type-safe access to the entity's primary key.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product : IEntity&lt;int&gt;
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }
    /// }
    /// 
    /// // Delete product by ID using IEntity interface
    /// var rows = await connection.DeleteAsync&lt;Product, int&gt;(1);
    /// 
    /// Console.WriteLine($"Deleted {rows} row(s)");
    /// </code>
    /// </example>
    /// <seealso cref="DeleteAsync{T}(IDbConnection, object, CancellationToken)"/>
    /// <seealso cref="DeleteAsync{T, TId}(IDbConnection, TId, CommandOptions, CancellationToken)"/>
    public static async Task<int> DeleteAsync<T, TId>(this IDbConnection connection, TId id, CancellationToken cancellationToken = default) where T : IEntity<TId>
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : await DeleteByIdCoreAsync<T, TId>(dbConnection, id, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes an entity of type <typeparamref name="T"/> by its primary key value with command options.
    /// </summary>
    /// <remarks>
    /// This overload works for entities that have a single primary key and allows customization of command execution
    /// through the <paramref name="options"/> parameter.
    /// </remarks>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/>.</typeparam>
    /// <typeparam name="TId">The type of the primary key.</typeparam>
    /// <param name="connection">The database connection to execute the delete against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value of the entity to delete.</param>
    /// <param name="options">Additional command options such as timeout or transaction settings.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    /// <example>
    /// <code>
    /// // Delete with transaction
    /// using var tx = connection.BeginTransaction();
    /// var rows = await connection.DeleteAsync&lt;Product, int&gt;(
    ///     1, 
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="DeleteAsync{T, TId}(IDbConnection, TId, CancellationToken)"/>
    public static async Task<int> DeleteAsync<T, TId>(this IDbConnection connection, TId id, CommandOptions options, CancellationToken cancellationToken = default) where T : IEntity<TId>
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : await DeleteByIdCoreAsync<T, TId>(dbConnection, id, options, cancellationToken);
    }

    #endregion
}

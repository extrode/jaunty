using System.Data;

using Jaunty.Core;
using Jaunty.Interfaces;

namespace Jaunty;

public static partial class Jaunty
{
    #region Delete By Entity

    /// <summary>
    /// Deletes an existing entity from the database based on its primary key.
    /// </summary>
    /// <typeparam name="T">The entity type to delete.</typeparam>
    /// <param name="connection">The database connection to execute the delete against.</param>
    /// <param name="entity">The entity to delete. Must not be null and must have a valid primary key.</param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    /// <remarks>
    /// <para>
    /// This method automatically generates a DELETE statement based on the entity's primary key property and 
    /// <see cref="Attributes.TableAttribute"/> / <see cref="Attributes.ColumnAttribute"/> attributes.
    /// </para>
    /// <para>
    /// <strong>Primary Key Detection:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>Properties marked with <see cref="Attributes.KeyAttribute"/> are used in the WHERE clause</description></item>
    /// <item><description>Properties named <c>Id</c> or <c>{TypeName}Id</c> are treated as potential primary keys</description></item>
    /// </list>
    /// <para>
    /// <strong>Return Value:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>1: One row was deleted successfully</description></item>
    /// <item><description>0: No rows were deleted (primary key not found)</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product
    /// {
    ///     [Key]
    ///     public int Id { get; set; }
    ///     
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }
    /// }
    /// 
    /// // Delete entity by object
    /// var product = connection.QueryFirst&lt;Product&gt;("SELECT * FROM products WHERE id = @Id", new { Id = 1 });
    /// var rows = connection.Delete(product);
    /// Console.WriteLine($"Deleted {rows} row(s)");
    /// 
    /// // Delete with table attribute
    /// [Table("products")]
    /// public class Product { ... }
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entity"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity has no primary key property or no table name can be resolved.
    /// </exception>
    /// <exception cref="DbException">
    /// Thrown when the database operation fails (e.g., constraint violation, syntax error).
    /// </exception>
    /// <seealso cref="Insert{T}(IDbConnection, T)"/>
    /// <seealso cref="Update{T}(IDbConnection, T)"/>
    /// <seealso cref="Delete{T}(IDbConnection, object)"/>
    /// <seealso cref="DeleteAsync{T}(IDbConnection, T)"/>
    /// <seealso cref="Attributes.KeyAttribute"/>
    /// <seealso cref="Attributes.TableAttribute"/>
    public static int Delete<T>(this IDbConnection connection, T entity) where T : class, new()
    {
        return DeleteByEntityCore(connection, entity, default);
    }

    /// <summary>
    /// Deletes an existing entity from the database with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to delete.</typeparam>
    /// <param name="connection">The database connection to execute the delete against.</param>
    /// <param name="entity">The entity to delete. Must not be null and must have a valid primary key.</param>
    /// <param name="options">
    /// Command options for configuring the delete. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the delete within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Delete with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = connection.QueryFirst&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE id = @Id",
    ///     new { Id = 1 });
    /// var rows = connection.Delete(product, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // Delete with timeout
    /// var rows = connection.Delete(
    ///     product, 
    ///     CommandOptions.WithTimeout(30));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entity"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity has no primary key property or no table name can be resolved.
    /// </exception>
    /// <exception cref="DbException">
    /// Thrown when the database operation fails.
    /// </exception>
    /// <seealso cref="Delete{T}(IDbConnection, T)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static int Delete<T>(this IDbConnection connection, T entity, CommandOptions options) where T : class, new()
    {
        return DeleteByEntityCore(connection, entity, options);
    }

    #endregion

    #region Delete By ID

    /// <summary>
    /// Deletes an entity from the database by its primary key value.
    /// </summary>
    /// <typeparam name="T">The entity type to delete.</typeparam>
    /// <param name="connection">The database connection to execute the delete against.</param>
    /// <param name="id">
    /// The primary key value of the entity to delete. Must match the type of the entity's primary key.
    /// </param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    /// <remarks>
    /// <para>
    /// This overload is useful when you only have the primary key value and don't need to load the entity first.
    /// </para>
    /// <para>
    /// <strong>Return Value:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>1: One row was deleted successfully</description></item>
    /// <item><description>0: No rows were deleted (primary key not found)</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product
    /// {
    ///     [Key]
    ///     public int Id { get; set; }
    /// }
    /// 
    /// // Delete by ID
    /// var rows = connection.Delete&lt;Product&gt;(1);
    /// Console.WriteLine($"Deleted {rows} row(s)");
    /// 
    /// // Delete with string key
    /// var rows = connection.Delete&lt;Category&gt;("CAT-001");
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity has no primary key property or no table name can be resolved.
    /// </exception>
    /// <exception cref="DbException">
    /// Thrown when the database operation fails (e.g., constraint violation, syntax error).
    /// </exception>
    /// <seealso cref="Delete{T}(IDbConnection, T)"/>
    /// <seealso cref="Delete{T, TId}(IDbConnection, TId)"/>
    /// <seealso cref="DeleteAsync{T}(IDbConnection, object)"/>
    public static int Delete<T>(this IDbConnection connection, object id) where T : class, new()
    {
        return DeleteByIdCore<T>(connection, id, default);
    }

    /// <summary>
    /// Deletes an entity from the database by its primary key value with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to delete.</typeparam>
    /// <param name="connection">The database connection to execute the delete against.</param>
    /// <param name="id">
    /// The primary key value of the entity to delete.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the delete. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the delete within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Delete by ID with transaction
    /// using var tx = connection.BeginTransaction();
    /// var rows = connection.Delete&lt;Product&gt;(1, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // Delete with timeout
    /// var rows = connection.Delete&lt;Product&gt;(
    ///     1, 
    ///     CommandOptions.WithTimeout(30));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity has no primary key property or no table name can be resolved.
    /// </exception>
    /// <exception cref="DbException">
    /// Thrown when the database operation fails.
    /// </exception>
    /// <seealso cref="Delete{T}(IDbConnection, object)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static int Delete<T>(this IDbConnection connection, object id, CommandOptions options) where T : class, new()
    {
        return DeleteByIdCore<T>(connection, id, options);
    }

    #endregion

    #region Delete by IEntity&lt;T&gt;

    /// <summary>
    /// Deletes an entity of type <typeparamref name="T"/> by its primary key value.
    /// </summary>
    /// <typeparam name="T">The entity type to delete. Must implement <see cref="IEntity{TId}"/>.</typeparam>
    /// <typeparam name="TId">The type of the primary key.</typeparam>
    /// <param name="connection">The database connection to execute the delete against.</param>
    /// <param name="id">The primary key value of the entity to delete.</param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    /// <remarks>
    /// <para>
    /// This overload is type-safe and works with entities that implement <see cref="IEntity{TId}"/>.
    /// </para>
    /// <para>
    /// <strong>Return Value:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>1: One row was deleted successfully</description></item>
    /// <item><description>0: No rows were deleted (primary key not found)</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product : IEntity&lt;int&gt;
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    /// }
    /// 
    /// // Type-safe delete by ID
    /// var rows = connection.Delete&lt;Product, int&gt;(1);
    /// Console.WriteLine($"Deleted {rows} row(s)");
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity has no primary key property or no table name can be resolved.
    /// </exception>
    /// <exception cref="DbException">
    /// Thrown when the database operation fails.
    /// </exception>
    /// <seealso cref="IEntity{TId}"/>
    /// <seealso cref="Delete{T}(IDbConnection, object)"/>
    public static int Delete<T, TId>(this IDbConnection connection, TId id) where T : IEntity<TId>
    {
        return DeleteByIdCore<T, TId>(connection, id, default);
    }

    /// <summary>
    /// Deletes an entity of type <typeparamref name="T"/> by its primary key value with command options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This overload is type-safe and works with entities that implement <see cref="IEntity{TId}"/>.
    /// </para>
    /// <para>
    /// Use this overload when you need to execute the delete within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The entity type to delete. Must implement <see cref="IEntity{TId}"/>.</typeparam>
    /// <typeparam name="TId">The type of the primary key.</typeparam>
    /// <param name="connection">The database connection to execute the delete against.</param>
    /// <param name="id">The primary key value of the entity to delete.</param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>The number of rows affected by the delete operation.</returns>
    /// <example>
    /// <code>
    /// // Type-safe delete with transaction
    /// using var tx = connection.BeginTransaction();
    /// var rows = connection.Delete&lt;Product, int&gt;(1, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity has no primary key property or no table name can be resolved.
    /// </exception>
    /// <exception cref="DbException">
    /// Thrown when the database operation fails.
    /// </exception>
    /// <seealso cref="IEntity{TId}"/>
    /// <seealso cref="Delete{T, TId}(IDbConnection, TId)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static int Delete<T, TId>(this IDbConnection connection, TId id, CommandOptions options) where T : IEntity<TId>
    {
        return DeleteByIdCore<T, TId>(connection, id, options);
    }

    #endregion
}

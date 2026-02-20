using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Updates an existing entity in the database based on its primary key.
    /// </summary>
    /// <typeparam name="T">The entity type to update.</typeparam>
    /// <param name="connection">The database connection to execute the update against.</param>
    /// <param name="entity">The entity with updated values. Must not be null and must have a valid primary key.</param>
    /// <returns>The number of rows affected by the update operation.</returns>
    /// <remarks>
    /// <para>
    /// This method automatically generates an UPDATE statement based on the entity's properties and 
    /// <see cref="Attributes.TableAttribute"/> / <see cref="Attributes.ColumnAttribute"/> attributes.
    /// </para>
    /// <para>
    /// <strong>Primary Key Detection:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>Properties marked with <see cref="Attributes.KeyAttribute"/> are used in the WHERE clause</description></item>
    /// <item><description>Properties named <c>Id</c> or <c>{TypeName}Id</c> are treated as potential primary keys</description></item>
    /// <item><description>For entities implementing <c>IEntity&lt;T&gt;</c>, the <c>Id</c> property is used as the key</description></item>
    /// </list>
    /// <para>
    /// <strong>Return Value:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>1: One row was updated successfully</description></item>
    /// <item><description>0: No rows were updated (primary key not found)</description></item>
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
    /// // Basic update
    /// var product = connection.QueryFirst&lt;Product&gt;("SELECT * FROM products WHERE id = @Id", new { Id = 1 });
    /// product.Price = 29.99m;
    /// var rows = connection.Update(product);
    /// Console.WriteLine($"Updated {rows} row(s)");
    /// 
    /// // Update with table attribute
    /// [Table("products")]
    /// public class Product { ... }
    /// 
    /// // Update with column attribute
    /// public class Product 
    /// {
    ///     [Column("product_id")]
    ///     public int Id { get; set; }
    ///     
    ///     [Column("product_name")]
    ///     public string Name { get; set; }
    /// }
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
    /// <seealso cref="Delete{T}(IDbConnection, T)"/>
    /// <seealso cref="UpdateAsync{T}(IDbConnection, T)"/>
    /// <seealso cref="Attributes.KeyAttribute"/>
    /// <seealso cref="Attributes.TableAttribute"/>
    public static int Update<T>(this IDbConnection connection, T entity) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return UpdateCore(connection, entity, default);
    }

    /// <summary>
    /// Updates an existing entity in the database with command options.
    /// </summary>
    /// <typeparam name="T">The entity type to update.</typeparam>
    /// <param name="connection">The database connection to execute the update against.</param>
    /// <param name="entity">The entity with updated values. Must not be null and must have a valid primary key.</param>
    /// <param name="options">
    /// Command options for configuring the update. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>The number of rows affected by the update operation.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the update within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Update with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = connection.QueryFirst&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE id = @Id",
    ///     new { Id = 1 });
    /// product.Price = 29.99m;
    /// var rows = connection.Update(product, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // Update with timeout
    /// var rows = connection.Update(
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
    /// <seealso cref="Update{T}(IDbConnection, T)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static int Update<T>(this IDbConnection connection, T entity, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return UpdateCore(connection, entity, options);
    }
}

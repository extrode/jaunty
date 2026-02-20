using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Inserts an entity into the database and returns the generated identity value.
    /// </summary>
    /// <typeparam name="T">The entity type to insert.</typeparam>
    /// <param name="connection">The database connection to execute the insert against.</param>
    /// <param name="entity">The entity to insert. Must not be null.</param>
    /// <returns>
    /// The generated identity value if the table has an identity column; otherwise, returns the number of rows affected.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method automatically generates an INSERT statement based on the entity's properties and 
    /// <see cref="Attributes.TableAttribute"/> / <see cref="Attributes.ColumnAttribute"/> attributes.
    /// </para>
    /// <para>
    /// <strong>Identity Column Detection:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>Properties marked with <see cref="Attributes.DatabaseGeneratedAttribute(DatabaseGeneratedOptions.Identity)"/> are excluded from INSERT</description></item>
    /// <item><description>Properties named <c>Id</c> or <c>{TypeName}Id</c> are treated as potential identity columns</description></item>
    /// <item><description>For entities implementing <c>IEntity&lt;T&gt;</c>, the <c>Id</c> property is automatically populated</description></item>
    /// </list>
    /// <para>
    /// <strong>Return Value:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>SQLite/SQL Server: Returns the generated identity value (e.g., <c>SCOPE_IDENTITY()</c>, <c>last_insert_rowid()</c>)</description></item>
    /// <item><description>PostgreSQL: Returns the value from <c>RETURNING</c> clause</description></item>
    /// <item><description>MySQL: Returns <c>LAST_INSERT_ID()</c></description></item>
    /// <item><description>No identity column: Returns 1 (rows affected)</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product
    /// {
    ///     [Key]
    ///     [DatabaseGenerated(DatabaseGeneratedOptions.Identity)]
    ///     public int Id { get; set; }
    ///     
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }
    /// }
    /// 
    /// // Basic insert
    /// var product = new Product { Name = "Widget", Price = 19.99m };
    /// var newId = connection.Insert(product);
    /// Console.WriteLine($"Created product with ID: {newId}");
    /// 
    /// // Insert with table attribute
    /// [Table("products")]
    /// public class Product { ... }
    /// 
    /// // Insert with column attribute
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
    /// Thrown when the entity has no writable properties or no table name can be resolved.
    /// </exception>
    /// <exception cref="DbException">
    /// Thrown when the database operation fails (e.g., constraint violation, syntax error).
    /// </exception>
    /// <seealso cref="Update{T}(IDbConnection, T)"/>
    /// <seealso cref="Delete{T}(IDbConnection, T)"/>
    /// <seealso cref="InsertAsync{T}(IDbConnection, T)"/>
    /// <seealso cref="Attributes.TableAttribute"/>
    /// <seealso cref="Attributes.ColumnAttribute"/>
    /// <seealso cref="Attributes.DatabaseGeneratedAttribute"/>
    public static long Insert<T>(this IDbConnection connection, T entity) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return InsertCore(connection, entity, default);
    }

    /// <summary>
    /// Inserts an entity into the database with command options and returns the generated identity value.
    /// </summary>
    /// <typeparam name="T">The entity type to insert.</typeparam>
    /// <param name="connection">The database connection to execute the insert against.</param>
    /// <param name="entity">The entity to insert. Must not be null.</param>
    /// <param name="options">
    /// Command options for configuring the insert. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>
    /// The generated identity value if the table has an identity column; otherwise, returns the number of rows affected.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the insert within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Insert with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = new Product { Name = "Widget", Price = 19.99m };
    /// var newId = connection.Insert(product, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // Insert with timeout
    /// var newId = connection.Insert(
    ///     product, 
    ///     CommandOptions.WithTimeout(30));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entity"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity has no writable properties or no table name can be resolved.
    /// </exception>
    /// <exception cref="DbException">
    /// Thrown when the database operation fails.
    /// </exception>
    /// <seealso cref="Insert{T}(IDbConnection, T)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static long Insert<T>(this IDbConnection connection, T entity, CommandOptions options) where T : class, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return InsertCore(connection, entity, options);
    }
}

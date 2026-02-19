using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously inserts an entity into the database and returns the generated identity value.
    /// </summary>
    /// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the insert against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entity">The entity instance to insert. All writable properties are mapped to columns.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the generated identity value (primary key) for identity columns, 
    /// or <c>1</c> for non-identity inserts.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method automatically detects the primary key property (named <c>Id</c> or <c>EntityNameId</c>) 
    /// and populates it after the insert if it's an identity column.
    /// </para>
    /// <para>
    /// For entities implementing <c>IEntity</c> or <c>IEntity&lt;T&gt;</c>, the Id property is 
    /// automatically populated with the generated identity value.
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
    /// // Insert a new product
    /// var product = new Product { Name = "Widget", Price = 19.99m };
    /// var id = await connection.InsertAsync(product);
    /// 
    /// // product.Id is now populated with the generated identity value
    /// Console.WriteLine($"Inserted product with ID: {product.Id}");
    /// </code>
    /// </example>
    /// <seealso cref="Insert{T}(IDbConnection, T)"/>
    /// <seealso cref="InsertAsync{T}(IDbConnection, T, CommandOptions, CancellationToken)"/>
    public static ValueTask<long> InsertAsync<T>(this IDbConnection connection, T entity, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : InsertCoreAsync(dbConnection, entity, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously inserts an entity into the database with command options and returns the generated identity value.
    /// </summary>
    /// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the insert against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="entity">The entity instance to insert.</param>
    /// <param name="options">
    /// Command options for configuring the insert execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the generated identity value, or <c>1</c> for non-identity inserts.
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
    /// var id = await connection.InsertAsync(product, CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // Insert with timeout
    /// var product = new Product { Name = "Widget", Price = 19.99m };
    /// var id = await connection.InsertAsync(
    ///     product, 
    ///     CommandOptions.WithTimeout(30));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="InsertAsync{T}(IDbConnection, T, CancellationToken)"/>
    public static ValueTask<long> InsertAsync<T>(this IDbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : InsertCoreAsync(dbConnection, entity, options, cancellationToken);
    }
}


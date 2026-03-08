namespace Jaunty.Interfaces;

/// <summary>
/// Represents an entity with a 64-bit integer primary key.
/// </summary>
/// <remarks>
/// <para>
/// This interface marks an entity as having a primary key property named <c>Id</c> of type 
/// <see cref="long"/>. Jaunty uses this interface to identify entities that can be operated 
/// on by key-based methods such as <see cref="DeleteAsync{T}(IDbConnection, object, CancellationToken)"/>.
/// </para>
/// <para>
/// Implementing this interface enables type-safe access to the entity's identifier and allows 
/// Jaunty to automatically handle primary key operations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Jaunty.Interfaces;
/// 
/// public class Product : IEntity
/// {
///     public long Id { get; set; }
///     public string Name { get; set; }
///     public decimal Price { get; set; }
/// }
/// 
/// // Now you can use key-based operations
/// await connection.DeleteAsync&lt;Product&gt;(1L);
/// </code>
/// </example>
/// <seealso cref="IEntity{T}"/>
public interface IEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    /// <remarks>
    /// This property represents the primary key of the entity in the database.
    /// For new entities that haven't been persisted, this value is typically 0 
    /// or the default value for <see cref="long"/>.
    /// </remarks>
    long Id { get; set; }
}

/// <summary>
/// Represents an entity with a primary key of a specified type.
/// </summary>
/// <typeparam name="T">The type of the primary key property.</typeparam>
/// <remarks>
/// <para>
/// This generic interface marks an entity as having a primary key property named <c>Id</c> 
/// of type <typeparamref name="T"/>. Jaunty uses this interface to identify entities that 
/// can be operated on by key-based methods.
/// </para>
/// <para>
/// Use this interface when your entity's primary key is not a <see cref="long"/>, such as 
/// when using <see cref="int"/>, <see cref="Guid"/>, or <see cref="string"/> as the key type.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Jaunty.Interfaces;
/// 
/// // Entity with int primary key
/// public class Product : IEntity&lt;int&gt;
/// {
///     public int Id { get; set; }
///     public string Name { get; set; }
///     public decimal Price { get; set; }
/// }
/// 
/// // Entity with Guid primary key
/// public class Order : IEntity&lt;Guid&gt;
/// {
///     public Guid Id { get; set; }
///     public DateTime OrderDate { get; set; }
///     public decimal Total { get; set; }
/// }
/// 
/// // Entity with string primary key
/// public class Country : IEntity&lt;string&gt;
/// {
///     public string Id { get; set; }  // e.g., "US", "UK", "CA"
///     public string Name { get; set; }
/// }
/// 
/// // Now you can use key-based operations
/// await connection.DeleteAsync&lt;Product&gt;(1);
/// await connection.DeleteAsync&lt;Order&gt;(Guid.NewGuid());
/// await connection.DeleteAsync&lt;Country&gt;("US");
/// </code>
/// </example>
/// <seealso cref="IEntity"/>
public interface IEntity<T>
{
    /// <summary>
    /// Gets or sets the unique identifier for this entity.
    /// </summary>
    /// <remarks>
    /// This property represents the primary key of the entity in the database.
    /// The type <typeparamref name="T"/> can be any type supported by your database 
    /// as a primary key (e.g., <see cref="int"/>, <see cref="long"/>, <see cref="Guid"/>, <see cref="string"/>).
    /// </remarks>
    T Id { get; set; }
}
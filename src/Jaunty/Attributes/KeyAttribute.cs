namespace Jaunty.Attributes;

/// <summary>
/// Marks a property as the primary key of the entity.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to a property to designate it as the primary key.
/// Jaunty uses the primary key for update, delete, and upsert operations.
/// </para>
/// <para>
/// This attribute is Jaunty's native primary key attribute. It also supports the standard
/// <c>KeyAttribute</c> from System.ComponentModel.DataAnnotations for compatibility.
/// </para>
/// <para>
/// <strong>Composite keys:</strong> apply <c>[Key]</c> to more than one property to declare a
/// composite primary key. The write paths support this throughout - <c>UPDATE</c>, <c>DELETE</c>
/// and upsert all emit a multi-column <c>WHERE</c>/conflict target, and <c>BulkUpdate</c>/
/// <c>BulkDelete</c> bind every key column.
/// </para>
/// <para>
/// <strong>Exception:</strong> the by-id convenience overloads (<c>Get&lt;T&gt;(id)</c>,
/// <c>Delete&lt;T&gt;(id)</c> and friends) take a single identifier and therefore require exactly
/// one key property; calling them on a composite-key entity throws with a message saying so. Use
/// the entity-taking overloads, or a Fluent predicate, for composite-key types.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Jaunty.Attributes;
/// 
/// public class Product
/// {
///     [Key]
///     public int Id { get; set; }
///     
///     public string Name { get; set; }
///     public decimal Price { get; set; }
/// }
/// 
/// // Update uses the Key property in the WHERE clause
/// var product = new Product { Id = 1, Name = "Widget", Price = 19.99m };
/// connection.Update(product);
/// // Generates: UPDATE Products SET Name = @Name, Price = @Price WHERE Id = @Id
/// </code>
/// </example>
/// <seealso cref="TableAttribute"/>
/// <seealso cref="ColumnAttribute"/>
/// <seealso cref="DatabaseGeneratedAttribute"/>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class KeyAttribute : Attribute
{
}
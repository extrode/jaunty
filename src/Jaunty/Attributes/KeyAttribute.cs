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
/// <see cref="System.ComponentModel.DataAnnotations.KeyAttribute"/> for compatibility.
/// </para>
/// <para>
/// <strong>Note:</strong> Currently, Jaunty only supports single-column primary keys. 
/// Composite primary keys are not supported.
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
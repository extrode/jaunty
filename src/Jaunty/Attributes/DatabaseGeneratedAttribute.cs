namespace Jaunty.Attributes;

/// <summary>
/// Specifies how the database generates values for a property.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to a property to indicate that the database generates its value,
/// rather than the application providing it.
/// </para>
/// <para>
/// This attribute is Jaunty's native database generation attribute. It also supports the standard
/// <c>DatabaseGeneratedAttribute</c> from System.ComponentModel.DataAnnotations.Schema for compatibility.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Jaunty.Attributes;
/// 
/// public class Product
/// {
///     [Key]
///     [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
///     public int Id { get; set; }
///     
///     public string Name { get; set; }
///     
///     [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
///     public DateTime CreatedAt { get; set; }
/// }
/// 
/// // When inserting, Id and CreatedAt are not included in the INSERT statement
/// var product = new Product { Name = "Widget" };
/// connection.Insert(product);
/// // product.Id is automatically populated with the generated identity value
/// </code>
/// </example>
/// <param name="option">The database generation option.</param>
/// <seealso cref="DatabaseGeneratedOption"/>
/// <seealso cref="KeyAttribute"/>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DatabaseGeneratedAttribute(DatabaseGeneratedOption option) : Attribute
{
    /// <summary>
    /// Gets the database generation option for this property.
    /// </summary>
    public DatabaseGeneratedOption Option { get; } = option;
}
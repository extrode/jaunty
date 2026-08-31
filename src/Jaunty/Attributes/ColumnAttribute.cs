namespace Jaunty.Attributes;

/// <summary>
/// Specifies the database column that a property is mapped to.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to a property to specify a custom column name in the database.
/// If not specified, Jaunty will use the property name as the column name.
/// </para>
/// <para>
/// This attribute is Jaunty's native column mapping attribute. It also supports the standard
/// <c>ColumnAttribute</c> from System.ComponentModel.DataAnnotations.Schema for compatibility.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Jaunty.Attributes;
/// 
/// public class Product
/// {
///     public int Id { get; set; }
///     
///     [Column("ProductName")]
///     public string Name { get; set; }
///     
///     [Column("UnitPrice")]
///     public decimal Price { get; set; }
/// }
/// 
/// // The properties map to columns with different names
/// var products = connection.Query&lt;Product&gt;("SELECT Id, ProductName, UnitPrice FROM Products");
/// </code>
/// </example>
/// <seealso cref="TableAttribute"/>
/// <seealso cref="KeyAttribute"/>
/// <seealso cref="IgnoreAttribute"/>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ColumnAttribute : Attribute
{
    /// <summary>
    /// Specifies the database column that a property is mapped to.
    /// </summary>
    /// <param name="name">The name of the database column.</param>
    public ColumnAttribute(string name)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(name);
#else
        if (name is null) throw new ArgumentNullException(nameof(name));
#endif
        Name = name;
    }

    /// <summary>
    /// Gets the name of the database column.
    /// </summary>
    public string Name { get; }
}
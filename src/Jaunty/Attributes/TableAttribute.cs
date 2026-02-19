namespace Jaunty.Attributes;

/// <summary>
/// Specifies the database table that a class is mapped to.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to a class to specify the database table name and optionally the schema.
/// If not specified, Jaunty will use the class name as the table name.
/// </para>
/// <para>
/// This attribute is Jaunty's native table mapping attribute. It also supports the standard 
/// <see cref="System.ComponentModel.DataAnnotations.Schema.TableAttribute"/> for compatibility.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Jaunty.Attributes;
/// 
/// [Table("Products", Schema = "dbo")]
/// public class Product
/// {
///     public int Id { get; set; }
///     public string Name { get; set; }
///     public decimal Price { get; set; }
/// }
/// 
/// // Query using the mapped table name
/// var products = connection.Query&lt;Product&gt;("SELECT * FROM Products");
/// </code>
/// </example>
/// <seealso cref="ColumnAttribute"/>
/// <seealso cref="KeyAttribute"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TableAttribute(string name, string? schema = null) : Attribute
{
    /// <summary>
    /// Gets the name of the database table.
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// Gets the optional schema name for the table.
    /// </summary>
    /// <remarks>
    /// If not specified, the default schema for the database connection will be used.
    /// </remarks>
    public string? Schema { get; } = schema;
}

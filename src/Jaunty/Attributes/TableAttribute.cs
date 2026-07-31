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
/// <c>TableAttribute</c> from System.ComponentModel.DataAnnotations.Schema for compatibility.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Jaunty.Attributes;
/// 
/// [Table("Products", "dbo")]
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
public sealed class TableAttribute : Attribute
{
    /// <summary>
    /// Specifies the database table that a class is mapped to.
    /// </summary>
    /// <param name="name">The name of the database table.</param>
    /// <param name="schema">The optional schema name.</param>
    public TableAttribute(string name, string? schema = null)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(name);
#else
        if (name is null) throw new ArgumentNullException(nameof(name));
#endif
        Name = name;
        Schema = schema;
    }

    /// <summary>
    /// Gets the name of the database table.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the optional schema name for the table.
    /// </summary>
    /// <remarks>
    /// If not specified, the default schema for the database connection will be used.
    /// </remarks>
    public string? Schema { get; }
}
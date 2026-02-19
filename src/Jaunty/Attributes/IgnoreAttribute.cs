namespace Jaunty.Attributes;

/// <summary>
/// Marks a property to be ignored during database mapping.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to a property to exclude it from INSERT, UPDATE, and SELECT operations.
/// The property will not be mapped to any database column.
/// </para>
/// <para>
/// This attribute is Jaunty's native ignore attribute. It also supports the standard 
/// <see cref="System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute"/> for compatibility.
/// </para>
/// <para>
/// Use this attribute for:
/// </para>
/// <list type="bullet">
/// <item><description>Computed properties that are calculated in code</description></item>
/// <item><description>Navigation properties for related data</description></item>
/// <item><description>Temporary or transient data that shouldn't be persisted</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// using Jaunty.Attributes;
/// 
/// public class Product
/// {
///     public int Id { get; set; }
///     public string Name { get; set; }
///     public decimal Price { get; set; }
///     
///     // This property won't be mapped to the database
///     [Ignore]
///     public string DisplayPrice => $"${Price:N2}";
///     
///     // Computed property for view models
///     [Ignore]
///     public bool IsExpensive => Price > 100;
/// }
/// 
/// // Only Id, Name, and Price columns are used
/// var products = connection.Query&lt;Product&gt;("SELECT Id, Name, Price FROM Products");
/// </code>
/// </example>
/// <seealso cref="ColumnAttribute"/>
/// <seealso cref="DatabaseGeneratedAttribute"/>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class IgnoreAttribute : Attribute
{
}

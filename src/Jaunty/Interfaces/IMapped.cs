using System.Data;

namespace Jaunty.Interfaces;

/// <summary>
/// Represents an entity that can map itself from a database data reader.
/// </summary>
/// <typeparam name="T">The entity type that implements this interface.</typeparam>
/// <remarks>
/// <para>
/// This interface allows entities to define custom mapping logic from an <see cref="IDataReader"/> 
/// instead of relying on Jaunty's automatic property-to-column mapping. This is useful for:
/// </para>
/// <list type="bullet">
/// <item><description>Complex mapping scenarios that can't be handled by conventions</description></item>
/// <item><description>Performance-critical code paths where manual mapping is faster</description></item>
/// <item><description>Handling database-specific data types or formats</description></item>
/// <item><description>Implementing inheritance hierarchies with custom mapping</description></item>
/// </list>
/// <para>
/// <strong>Note for .NET 8 and later:</strong> The <see cref="ReadEntity(IDataReader)"/> method 
/// is defined as a static abstract member, requiring implementation as a static method. This enables 
/// more efficient invocation without requiring an instance.
/// </para>
/// <para>
/// <strong>Note for earlier .NET versions:</strong> The method is an instance method. While this 
/// requires creating an instance to access the mapping logic, it maintains compatibility with 
/// older frameworks.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using System.Data;
/// using Jaunty.Interfaces;
/// 
/// public class Product : IMapped&lt;Product&gt;
/// {
///     public int Id { get; set; }
///     public string Name { get; set; }
///     public decimal Price { get; set; }
///     public DateTime CreatedAt { get; set; }
/// 
/// #if NET8_0_OR_GREATER
///     public static Product ReadEntity(IDataReader reader)
///     {
///         return new Product
///         {
///             Id = reader.GetInt32(reader.GetOrdinal("Id")),
///             Name = reader.GetString(reader.GetOrdinal("Name")),
///             Price = reader.GetDecimal(reader.GetOrdinal("Price")),
///             CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
///         };
///     }
/// #else
///     public Product ReadEntity(IDataReader reader)
///     {
///         return new Product
///         {
///             Id = reader.GetInt32(reader.GetOrdinal("Id")),
///             Name = reader.GetString(reader.GetOrdinal("Name")),
///             Price = reader.GetDecimal(reader.GetOrdinal("Price")),
///             CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
///         };
///     }
/// #endif
/// }
/// 
/// // Use with custom mapper option
/// var options = CommandOptions&lt;Product&gt;.WithMapper(Product.ReadEntity);
/// var products = connection.Query("SELECT * FROM Products", options: options);
/// </code>
/// </example>
public interface IMapped<T> where T : IMapped<T>, new()
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Reads an entity of type <typeparamref name="T"/> from the current row of the data reader.
    /// </summary>
    /// <param name="reader">The data reader positioned on the current row.</param>
    /// <returns>An entity of type <typeparamref name="T"/> populated from the data reader.</returns>
    /// <remarks>
    /// <para>
    /// This static method is called by Jaunty to map a row from the database to an entity instance.
    /// The reader is positioned on the row to read when this method is called.
    /// </para>
    /// <para>
    /// Implementors should:
    /// </para>
    /// <list type="number">
    /// <item><description>Call <c>reader.GetOrdinal(columnName)</c> to get column ordinals (cache these for performance)</description></item>
    /// <item><description>Check for NULL values using <c>reader.IsDBNull(ordinal)</c> before reading nullable columns</description></item>
    /// <item><description>Use appropriate Get methods (GetInt32, GetString, etc.) based on column types</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidCastException">
    /// Thrown when a column value cannot be converted to the expected property type.
    /// </exception>
    static abstract T ReadEntity(IDataReader reader);
#else
    /// <summary>
    /// Reads an entity of type <typeparamref name="T"/> from the current row of the data reader.
    /// </summary>
    /// <param name="reader">The data reader positioned on the current row.</param>
    /// <returns>An entity of type <typeparamref name="T"/> populated from the data reader.</returns>
    /// <remarks>
    /// <para>
    /// This instance method is called by Jaunty to map a row from the database to an entity instance.
    /// The reader is positioned on the row to read when this method is called.
    /// </para>
    /// <para>
    /// Although this is an instance method, the typical pattern is to create a new instance 
    /// within the implementation and populate its properties.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public Product ReadEntity(IDataReader reader)
    /// {
    ///     return new Product
    ///     {
    ///         Id = reader.GetInt32(0),
    ///         Name = reader.GetString(1),
    ///         Price = reader.GetDecimal(2)
    ///     };
    /// }
    /// </code>
    /// </example>
    T ReadEntity(IDataReader reader);
#endif
}

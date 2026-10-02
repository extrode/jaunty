namespace Extrode.Jaunty.TypeHandlers;

/// <summary>
/// Abstract base class for implementing type handlers with conversion logic for a specific type.
/// </summary>
/// <typeparam name="T">The CLR type to be converted.</typeparam>
/// <remarks>
/// <para>
/// Implement this class to provide custom conversion logic for a type <typeparamref name="T"/>.
/// The handler is invoked when a value of type <typeparamref name="T"/> is read from or written
/// to the database via Extrode.Jaunty's mapping and parameter binding mechanisms.
/// </para>
/// <para>
/// For most use cases the delegate-based registration API,
/// <c>JauntyConfigBuilder.RegisterTypeHandler&lt;T&gt;(Func&lt;object?, T&gt;, Func&lt;T?, object?&gt;)</c>, is simpler and
/// preferred. Use this base class when you need structured handler logic or state management.
/// </para>
/// <para>
/// Type handlers participate in the following operations:
/// <list type="bullet">
/// <item>Parameter binding (write path): <see cref="ToDbValue"/> converts a CLR value to a database value</item>
/// <item>Query mapping (read path): <see cref="Parse"/> converts a database value to a CLR value</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Extrode.Jaunty.TypeHandlers;
/// 
/// public class GuidAsStringHandler : TypeHandler&lt;Guid&gt;
/// {
///     public override Guid Parse(object dbValue)
///     {
///         if (dbValue is null)
///             return Guid.Empty;
///         
///         if (dbValue is string str)
///             return Guid.Parse(str);
///         
///         throw new InvalidOperationException($"Cannot convert {dbValue.GetType().Name} to Guid");
///     }
///     
///     public override object? ToDbValue(Guid value)
///     {
///         return value == Guid.Empty ? null : value.ToString("D");
///     }
/// }
/// 
/// // Register the handler, once, at startup
/// JauntyConfig.Configure(c => c.RegisterTypeHandler(new GuidAsStringHandler()));
/// </code>
/// </example>
/// <seealso cref="Configuration.JauntyConfig"/>
/// <seealso cref="ITypeHandler"/>
public abstract class TypeHandler<T>
{
    /// <summary>
    /// Converts a database value to a CLR value of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="dbValue">The value from the database. May be null or DBNull.</param>
    /// <returns>The converted CLR value, or <see langword="null"/> for a database NULL.</returns>
    /// <remarks>
    /// <para>
    /// The implementation should handle null and DBNull gracefully, and throw
    /// <see cref="InvalidOperationException"/> or <see cref="FormatException"/> if conversion is not possible.
    /// </para>
    /// <para>
    /// AUD-R35-174. The return type was the non-nullable <typeparamref name="T"/> while this very
    /// paragraph said the result may be null and the registry's since-removed <c>TryConvertFromDb</c> had
    /// a dedicated branch for a handler that returns one (AUD-R27-014). A <c>TypeHandler&lt;string&gt;</c>
    /// returning null for a DBNull input - the documented, registry-handled case - therefore produced
    /// CS8603 at the implementation site. The annotation is now <c>T?</c>, matching both the
    /// documented behaviour and the sibling <see cref="ToDbValue"/>, which has always taken
    /// <c>T?</c>. This is an annotation change only: a handler that never returns null is unaffected.
    /// </para>
    /// </remarks>
    public abstract T? Parse(object? dbValue);

    /// <summary>
    /// Converts a CLR value to a database value.
    /// </summary>
    /// <param name="value">
    /// The CLR value to convert. Extrode.Jaunty never passes null: a null property value is bound as
    /// <see cref="DBNull.Value"/> without calling the handler, because the handler is found from the
    /// value's runtime type.
    /// </param>
    /// <returns>The database value, or null if the value should be stored as NULL.</returns>
    /// <remarks>
    /// <para>
    /// The implementation should return a value that can be bound as a SQL parameter (e.g., string, int, byte[], etc.),
    /// or null to represent a SQL NULL value. If null is returned, Extrode.Jaunty converts it to DBNull.Value for parameter binding.
    /// </para>
    /// <para>
    /// A handler cannot change how a null property is stored; it is always SQL NULL. The parameter
    /// stays nullable so a handler can also be called directly.
    /// </para>
    /// </remarks>
    public abstract object? ToDbValue(T? value);
}

namespace Jaunty.TypeHandlers;

/// <summary>
/// Abstract base class for implementing type handlers with conversion logic for a specific type.
/// </summary>
/// <typeparam name="T">The CLR type to be converted.</typeparam>
/// <remarks>
/// <para>
/// Implement this class to provide custom conversion logic for a type <typeparamref name="T"/>.
/// The handler is invoked when a value of type <typeparamref name="T"/> is read from or written
/// to the database via Jaunty's mapping and parameter binding mechanisms.
/// </para>
/// <para>
/// For most use cases, the delegate-based registration APIs on <see cref="Configuration.JauntyConfig"/>
/// (via <see cref="Configuration.JauntyConfig"/>) are simpler and preferred.
/// Use this base class when you need structured handler logic or state management.
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
/// using Jaunty.TypeHandlers;
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
/// // Register the handler
/// JauntyConfig.RegisterTypeHandler(new GuidAsStringHandler());
/// </code>
/// </example>
/// <seealso cref="Configuration.JauntyConfig"/>
/// <seealso cref="Configuration.JauntyConfig"/>
public abstract class TypeHandler<T>
{
    /// <summary>
    /// Converts a database value to a CLR value of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="dbValue">The value from the database. May be null or DBNull.</param>
    /// <returns>The converted CLR value. May be null if <typeparamref name="T"/> is nullable.</returns>
    /// <remarks>
    /// The implementation should handle null and DBNull gracefully, and throw
    /// <see cref="InvalidOperationException"/> or <see cref="FormatException"/> if conversion is not possible.
    /// </remarks>
    public abstract T Parse(object? dbValue);

    /// <summary>
    /// Converts a CLR value to a database value.
    /// </summary>
    /// <param name="value">The CLR value to convert. May be null.</param>
    /// <returns>The database value, or null if the value is null or should be stored as NULL.</returns>
    /// <remarks>
    /// The implementation should return a value that can be bound as a SQL parameter (e.g., string, int, byte[], etc.),
    /// or null to represent a SQL NULL value. If null is returned, Jaunty converts it to DBNull.Value for parameter binding.
    /// </remarks>
    public abstract object? ToDbValue(T? value);
}

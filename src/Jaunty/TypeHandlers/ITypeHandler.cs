namespace Jaunty.TypeHandlers;

/// <summary>
/// Internal contract for type handlers. Provides a unified interface for converting values
/// between database representation and CLR types.
/// </summary>
/// <remarks>
/// <para>
/// This interface is used internally by Jaunty's type-handler registry and is not intended
/// to be directly implemented by user code. Instead, implement <see cref="TypeHandler{T}"/> or
/// use the delegate-based registration APIs on <see cref="Configuration.JauntyConfig"/>.
/// </para>
/// </remarks>
internal interface ITypeHandler
{
    /// <summary>
    /// Converts a database value to a CLR object.
    /// </summary>
    /// <param name="dbValue">The value from the database. May be null or DBNull.</param>
    /// <returns>The converted CLR object, or null if the input was null/DBNull.</returns>
    object? Parse(object? dbValue);

    /// <summary>
    /// Converts a CLR object to a database value.
    /// </summary>
    /// <param name="value">The CLR object to convert. May be null.</param>
    /// <returns>The database value, or null if the input was null.</returns>
    object? ToDbValue(object? value);
}

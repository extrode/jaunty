using System;
using System.Globalization;

namespace Jaunty.Internals.Read;

/// <summary>
/// The single terminal conversion from a raw ADO.NET value to a CLR target type.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26-062. Jaunty had three of these and they disagreed. <c>DbValueConverter.ChangeType</c>
/// (Jaunty.Extensions.Reflection), <c>GroupedJoinedResultMapper.ConvertColumnValue</c>
/// (Jaunty.Fluent) and <c>ScalarConverter&lt;T&gt;.Convert</c> (here) all pinned
/// <see cref="CultureInfo.InvariantCulture"/> and all special-cased enums identically, then
/// diverged on everything else:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b><see cref="Guid"/> from something that is neither a Guid nor a well-formed string.</b> The
/// Reflection one did <c>Guid.Parse((string)value)</c> - a hard cast, so a provider returning a
/// 16-byte <c>byte[]</c> got <see cref="InvalidCastException"/>. The Fluent one did
/// <c>Guid.Parse(value.ToString()!)</c>, which turned the same input into
/// <see cref="FormatException"/> after stringifying it to <c>"System.Byte[]"</c>. This one only
/// converted from a string at all and otherwise fell through to
/// <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>, which throws
/// <see cref="InvalidCastException"/> because <see cref="Guid"/> does not implement
/// <see cref="IConvertible"/>. Three inputs, three different exceptions, decided by which mapper
/// happened to run.
/// </description></item>
/// <item><description>
/// <b><see cref="char"/>.</b> Only the Fluent one mapped a single-character string to a
/// <see cref="char"/> and <c>""</c> to <c>'\0'</c>. The other two left it to
/// <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>, which throws on the empty
/// string.
/// </description></item>
/// <item><description>
/// <b><see cref="Nullable{T}"/> unwrapping.</b> Two unwrapped the target type up front; the Fluent
/// one required its caller to have done so already.
/// </description></item>
/// <item><description>
/// <b><see cref="DateTimeOffset"/>, <see cref="TimeSpan"/>, <c>DateOnly</c>, <c>TimeOnly</c> from a
/// string.</b> Only this one handled them - the other two threw
/// <see cref="InvalidCastException"/> for a SQLite column that came back as ISO-8601-ish text.
/// The finding named the first three divergences; this fourth one was found while consolidating,
/// and it is the one most likely to have been hit in practice.
/// </description></item>
/// </list>
/// <para>
/// The Fluent helper's own summary claimed its callers "can't drift out of sync with each other" -
/// true within its assembly, and beside the point, since a fourth implementation of the same
/// contract lived one assembly over and already had. All three now delegate here.
/// </para>
/// <para>
/// <b>What deliberately still throws.</b> A 16-byte <c>byte[]</c> is not converted to a
/// <see cref="Guid"/>, even though several providers store <c>UNIQUEIDENTIFIER</c>/<c>BINARY(16)</c>
/// that way. <c>new Guid(byte[])</c> reads the first three fields little-endian, which is SQL
/// Server's on-disk order but not the byte order MySQL, SQLite or PostgreSQL use for a
/// <c>BINARY(16)</c> written from a string - so guessing produces a <em>silently wrong</em> Guid
/// rather than a failure. A clear exception naming both types is the better answer, and it is now
/// the same exception on all three paths.
/// </para>
/// </remarks>
internal static class DbValueConversion
{
    /// <summary>
    /// Converts <paramref name="value"/> to <paramref name="targetType"/>, which may be a
    /// <see cref="Nullable{T}"/>.
    /// </summary>
    /// <exception cref="InvalidCastException">
    /// No conversion from the value's runtime type to <paramref name="targetType"/> exists.
    /// </exception>
    public static object Convert(object value, Type targetType)
    {
        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsEnum)
        {
            return value is string enumString
                ? Enum.Parse(underlyingType, enumString, ignoreCase: true)
                : Enum.ToObject(underlyingType, System.Convert.ChangeType(value, Enum.GetUnderlyingType(underlyingType), CultureInfo.InvariantCulture));
        }

        // None of the types below implement IConvertible, so Convert.ChangeType cannot produce any
        // of them - a provider that returns a GUID/date/time column as text (SQLite in particular)
        // would otherwise throw InvalidCastException here.
        if (underlyingType == typeof(Guid))
        {
            if (value is Guid guid) return guid;
            if (value is string guidString) return Guid.Parse(guidString);
            throw NoConversion(value, underlyingType);
        }

        if (underlyingType == typeof(char) && value is string charString)
            return charString.Length > 0 ? charString[0] : '\0';

        if (underlyingType == typeof(DateTimeOffset) && value is string dtoString)
            return DateTimeOffset.Parse(dtoString, CultureInfo.InvariantCulture);

        if (underlyingType == typeof(TimeSpan) && value is string tsString)
            return TimeSpan.Parse(tsString, CultureInfo.InvariantCulture);

#if NET8_0_OR_GREATER
        if (underlyingType == typeof(DateOnly) && value is string dateOnlyString)
            return DateOnly.Parse(dateOnlyString, CultureInfo.InvariantCulture);

        if (underlyingType == typeof(TimeOnly) && value is string timeOnlyString)
            return TimeOnly.Parse(timeOnlyString, CultureInfo.InvariantCulture);
#endif

        // CultureInfo.InvariantCulture, not the ambient CurrentCulture: providers routinely hand
        // back a string where the column is TEXT/NUMERIC (SQLite in particular), and under a
        // comma-decimal culture (de-DE, fr-FR, ...) Convert.ChangeType("1.5", typeof(decimal)) does
        // not throw - it reads the period as a group separator and returns 15. This is the terminal
        // conversion for every mapped column read, so the ambient culture would otherwise silently
        // scale numeric values by the host's locale.
        return System.Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The one exception every path throws when no conversion exists, naming both types - as
    /// opposed to the <see cref="InvalidCastException"/>/<see cref="FormatException"/>/
    /// <c>"System.Byte[]" is not a Guid</c> trio the three converters used to produce for the same
    /// input.
    /// </summary>
    private static InvalidCastException NoConversion(object value, Type targetType) =>
        new($"Cannot convert a value of type '{value.GetType().FullName}' to '{targetType.FullName}'. " +
            "If the provider returns this column as a byte array, map the property as byte[] and " +
            "convert it yourself - the byte order of a binary GUID is provider-specific and Jaunty " +
            "will not guess it.");
}

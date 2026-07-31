using System.Globalization;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Converts a value produced by a DuckDB reader into the CLR type a mapped property expects.
/// </summary>
/// <remarks>
/// AUD-R26: this logic previously existed twice and only one copy was complete.
/// <c>ImportExecutor.ConvertValue</c> special-cased the two type quirks of the pinned DuckDB.NET
/// 1.3.0 - <see cref="DateOnly"/> for DATE columns and <see cref="TimeOnly"/> for TIME columns -
/// before falling through to <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>. The
/// read path in <c>DuckDbRead</c>/<c>DuckDbReadAsync</c> called <c>Convert.ChangeType</c> directly
/// inside a bare <c>catch { }</c> and never got them, so a <see cref="TimeSpan"/> property could not
/// be read at all.
///
/// <para>
/// Neither copy handled enums, which <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>
/// cannot reach from either representation DuckDB produces: an integer column arrives as
/// <see cref="long"/> and a text column as <see cref="string"/>, and both threw
/// <see cref="InvalidCastException"/>. Core supports enums through
/// <c>JauntyConfig.DefaultEnumStorage</c> and <c>EnumStorageAttribute</c>; the flat-file surface
/// silently did not.
/// </para>
///
/// <para>
/// Both representations are accepted on read regardless of any configured storage mode: a flat file
/// is an external artifact whose producer is not this library, so rejecting the form it happens to
/// use would be a guess dressed up as a rule.
/// </para>
/// </remarks>
internal static class ReaderValueConverter
{
    /// <summary>
    /// Attempts to convert <paramref name="value"/> to <paramref name="targetType"/>.
    /// </summary>
    /// <param name="value">The value read from the DuckDB reader. Must not be null or DBNull.</param>
    /// <param name="targetType">The mapped property's type, which may be <see cref="Nullable{T}"/>.</param>
    /// <param name="converted">The converted value when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the value was converted or already had the right type.</returns>
    /// <param name="convertEnums">
    /// Whether an enum target should be converted. <see langword="true"/> on the read path, where
    /// the destination is a typed CLR property that can hold nothing else. <see langword="false"/>
    /// on the import path, where the destination is an ADO.NET parameter: handing a provider a boxed
    /// enum is worse than handing it the raw value. Npgsql rejects an unmapped enum CLR type
    /// outright, and SQLite and SQL Server infer the parameter type from
    /// <see cref="Type.GetTypeCode(Type)"/>, which reports an enum as its underlying integral type -
    /// so a text column that used to receive <c>"Closed"</c> would silently start receiving <c>1</c>.
    /// Measured on SQLite: raw string stored <c>Closed</c>, boxed enum stored <c>1</c>.
    /// </param>
    public static bool TryConvert(object value, Type targetType, bool convertEnums, out object? converted)
    {
        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value.GetType() == underlyingType)
        {
            converted = value;
            return true;
        }

        // A value already assignable to the target needs no conversion, and must not be pushed
        // through Convert.ChangeType - that throws for anything not IConvertible, which would reject
        // an object-typed or interface-typed property reading a Guid/BLOB column that previously
        // bound fine.
        if (underlyingType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        // DuckDB.NET (1.3.0, pinned) returns DateOnly for DATE columns and TimeOnly for TIME columns.
        if (value is DateOnly dateOnly && underlyingType == typeof(DateTime))
        {
            converted = dateOnly.ToDateTime(TimeOnly.MinValue);
            return true;
        }

        if (value is TimeOnly timeOnly && underlyingType == typeof(TimeSpan))
        {
            converted = timeOnly.ToTimeSpan();
            return true;
        }

        // R29: Convert.ChangeType has no path to DateTimeOffset from either representation a
        // DuckDB reader produces (DateTime for TIMESTAMP, string for VARCHAR), so DateTimeOffset
        // properties could not be read at all. new DateTimeOffset honors the value's Kind
        // (Unspecified assumes local), matching DbValueConversion and the source-generated
        // converter.
        if (underlyingType == typeof(DateTimeOffset))
        {
            if (value is DateTime dateTime)
            {
                converted = new DateTimeOffset(dateTime);
                return true;
            }

            if (value is string offsetText
                && DateTimeOffset.TryParse(offsetText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsedOffset))
            {
                converted = parsedOffset;
                return true;
            }
        }

        if (underlyingType.IsEnum)
        {
            if (!convertEnums)
            {
                converted = null;
                return false;
            }

            return TryConvertEnum(value, underlyingType, out converted);
        }

        try
        {
            converted = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            converted = null;
            return false;
        }
    }

    /// <summary>
    /// Converts <paramref name="value"/> or throws an exception that names the column, the property
    /// and both types - rather than letting the caller's compiled setter throw a bare
    /// <see cref="InvalidCastException"/> that identifies neither.
    /// </summary>
    /// <param name="value">The value read from the DuckDB reader.</param>
    /// <param name="mapping">The column mapping the value is destined for.</param>
    /// <param name="entityType">The entity being materialized, for the error message.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value cannot be converted.</exception>
    public static object? ConvertOrThrow(object value, in ColumnMapping mapping, Type entityType)
    {
        if (TryConvert(value, mapping.PropertyType, convertEnums: true, out object? converted))
            return converted;

        throw new InvalidOperationException(
            $"Cannot read column '{mapping.ColumnName}' into {entityType.Name}.{mapping.Property.Name}: " +
            $"the source produced {value.GetType().Name} but the property is {DescribeType(mapping.PropertyType)}.");
    }

    private static bool TryConvertEnum(object value, Type enumType, out object? converted)
    {
        // A text column arrives as string; accept both the member name and its numeric form.
        if (value is string text)
        {
            if (Enum.TryParse(enumType, text, ignoreCase: true, out object? parsed))
            {
                converted = parsed;
                return true;
            }

            converted = null;
            return false;
        }

        // An integer column arrives as long (or another integral type). Enum.ToObject requires the
        // value to already be integral, so narrow through the enum's own underlying type first -
        // that is also what makes an out-of-range value surface as OverflowException here rather
        // than as a silently invalid enum instance.
        try
        {
            object integral = Convert.ChangeType(
                value, Enum.GetUnderlyingType(enumType), CultureInfo.InvariantCulture);
            converted = Enum.ToObject(enumType, integral);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            converted = null;
            return false;
        }
    }

    private static string DescribeType(Type type)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        return underlying is null ? type.Name : underlying.Name + "?";
    }
}

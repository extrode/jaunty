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
        => TryConvert(value, targetType, convertEnums, out converted, out _);

    /// <summary>
    /// <see cref="TryConvert(object, Type, bool, out object?)"/>, additionally reporting <em>why</em>
    /// a conversion failed.
    /// </summary>
    /// <remarks>
    /// AUD-R35-252: <see cref="ConvertOrThrow"/> used to attribute every failure to a type mismatch
    /// - "the source produced X but the property is Y" - but a value can also be the right shape and
    /// out of range, or text in a format the target cannot parse. A <c>long</c> 99999 read into a
    /// <c>byte</c> property is not a type mismatch, and telling the caller it is sends them to
    /// change a property type that was never wrong. The real diagnostic used to be discarded by the
    /// catch filter before the message was composed; it now travels back out.
    /// </remarks>
    /// <param name="value">The value read from the DuckDB reader. Must not be null or DBNull.</param>
    /// <param name="targetType">The mapped property's type, which may be <see cref="Nullable{T}"/>.</param>
    /// <param name="convertEnums">As on <see cref="TryConvert(object, Type, bool, out object?)"/>.</param>
    /// <param name="converted">The converted value when this returns <see langword="true"/>.</param>
    /// <param name="reason">
    /// A sentence describing the failure when this returns <see langword="false"/>, or
    /// <see langword="null"/> when the failure is a plain type mismatch and the caller's own
    /// wording is the best available.
    /// </param>
    /// <returns><see langword="true"/> when the value was converted or already had the right type.</returns>
    public static bool TryConvert(
        object value, Type targetType, bool convertEnums, out object? converted, out string? reason)
    {
        reason = null;
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

            if (value is string offsetText)
            {
                if (!DateTimeOffset.TryParse(offsetText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsedOffset))
                {
                    // AUD-R35-252: an unparsable string used to fall through to Convert.ChangeType,
                    // which cannot produce a DateTimeOffset at all, so the failure surfaced as a
                    // type mismatch against a property whose type was never the problem.
                    converted = null;
                    reason = Unparsable(offsetText, typeof(DateTimeOffset));
                    return false;
                }

                converted = parsedOffset;
                return true;
            }
        }

        // AUD-R35-027: none of the types below implement IConvertible, so Convert.ChangeType below
        // cannot produce any of them from the representation a flat file actually carries. Only
        // DateTimeOffset had a parsing branch, so a Guid/TimeSpan/DateOnly/TimeOnly property backed
        // by a CSV or JSON column - text, always - threw InvalidCastException and could not be read
        // at all, even though the import path writes those columns as text quite happily. Core's
        // DbValueConversion.cs has carried these same branches since AUD-R29.
        if (underlyingType == typeof(Guid) && value is string guidText)
        {
            if (!Guid.TryParse(guidText, out Guid parsedGuid))
            {
                converted = null;
                reason = Unparsable(guidText, typeof(Guid));
                return false;
            }

            converted = parsedGuid;
            return true;
        }

        if (underlyingType == typeof(TimeSpan) && value is string timeSpanText)
        {
            if (!TimeSpan.TryParse(timeSpanText, CultureInfo.InvariantCulture, out TimeSpan parsedTimeSpan))
            {
                converted = null;
                reason = Unparsable(timeSpanText, typeof(TimeSpan));
                return false;
            }

            converted = parsedTimeSpan;
            return true;
        }

        if (underlyingType == typeof(char) && value is string charText)
        {
            if (charText.Length != 1)
            {
                converted = null;
                reason = $"the source produced the {charText.Length}-character string '{charText}', " +
                    "and a char property holds exactly one character.";
                return false;
            }

            converted = charText[0];
            return true;
        }

        if (underlyingType == typeof(DateOnly))
        {
            // The DATE case is handled above by the assignability check; this is the TIMESTAMP and
            // VARCHAR case, which the reader hands back as DateTime and string respectively.
            if (value is DateTime dateSource)
            {
                converted = DateOnly.FromDateTime(dateSource);
                return true;
            }

            if (value is string dateText)
            {
                if (!DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsedDate))
                {
                    converted = null;
                    reason = Unparsable(dateText, typeof(DateOnly));
                    return false;
                }

                converted = parsedDate;
                return true;
            }
        }

        if (underlyingType == typeof(TimeOnly))
        {
            if (value is TimeSpan timeSource)
            {
                // FromTimeSpan throws outside [0, 24h), and an INTERVAL column can hold either.
                if (timeSource < TimeSpan.Zero || timeSource >= TimeSpan.FromDays(1))
                {
                    converted = null;
                    reason = $"the source produced the interval {timeSource}, and a TimeOnly property " +
                        "only holds a time of day - at least zero and under 24 hours.";
                    return false;
                }

                converted = TimeOnly.FromTimeSpan(timeSource);
                return true;
            }

            if (value is DateTime timeFromTimestamp)
            {
                converted = TimeOnly.FromDateTime(timeFromTimestamp);
                return true;
            }

            if (value is string timeText)
            {
                if (!TimeOnly.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly parsedTime))
                {
                    converted = null;
                    reason = Unparsable(timeText, typeof(TimeOnly));
                    return false;
                }

                converted = parsedTime;
                return true;
            }
        }

        if (underlyingType.IsEnum)
        {
            if (!convertEnums)
            {
                converted = null;
                reason = $"enum conversion is off on this path, so {underlyingType.Name} is left to the caller.";
                return false;
            }

            return TryConvertEnum(value, underlyingType, out converted, out reason);
        }

        try
        {
            converted = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            converted = null;
            reason = Describe(ex, value, underlyingType);
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
        if (TryConvert(value, mapping.PropertyType, convertEnums: true, out object? converted, out string? reason))
            return converted;

        throw new InvalidOperationException(
            $"Cannot read column '{mapping.ColumnName}' into {entityType.Name}.{mapping.Property.Name}: " +
            (reason ?? $"the source produced {value.GetType().Name} but the property is " +
                $"{DescribeType(mapping.PropertyType)}."));
    }

    private static bool TryConvertEnum(object value, Type enumType, out object? converted, out string? reason)
    {
        reason = null;

        // A text column arrives as string; accept both the member name and its numeric form.
        if (value is string text)
        {
            if (Enum.TryParse(enumType, text, ignoreCase: true, out object? parsed))
            {
                converted = parsed;
                return true;
            }

            converted = null;
            reason = $"the source produced '{text}', which is not a member of {enumType.Name} " +
                $"({string.Join(", ", Enum.GetNames(enumType))}) nor its numeric form.";
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
            reason = Describe(ex, value, enumType);
            return false;
        }
    }

    /// <summary>AUD-R35-252: the failure sentence for a value the target could not parse.</summary>
    private static string Unparsable(string text, Type targetType) =>
        $"the source produced the string '{text}', which is not a {targetType.Name} " +
        "in any format the invariant culture recognises.";

    /// <summary>
    /// AUD-R35-252: turns the exception <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>
    /// threw into the sentence that names what actually went wrong. Only
    /// <see cref="InvalidCastException"/> is a genuine type mismatch; the other two say the types
    /// are compatible and this particular value is not, which is a different thing to go and fix.
    /// </summary>
    private static string? Describe(Exception ex, object value, Type targetType) => ex switch
    {
        OverflowException =>
            $"the source produced {value.GetType().Name} {Convert.ToString(value, CultureInfo.InvariantCulture)}, " +
            $"which is outside the range of {targetType.Name}.",
        FormatException =>
            $"the source produced '{Convert.ToString(value, CultureInfo.InvariantCulture)}', " +
            $"which {targetType.Name} cannot parse.",
        _ => null
    };

    private static string DescribeType(Type type)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        return underlying is null ? type.Name : underlying.Name + "?";
    }
}

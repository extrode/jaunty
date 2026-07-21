namespace Jaunty.Internals.Read;

internal static class ScalarConverter<T>
{
    private static readonly Type TargetType;
    private static readonly bool IsNullable;
    private static readonly bool IsEnum;

    static ScalarConverter()
    {
        TargetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        IsNullable = Nullable.GetUnderlyingType(typeof(T)) is not null;
        IsEnum = TargetType.IsEnum;
    }

    internal static T Convert(object value)
    {
        // Fast path: direct type match
        if (value is T direct)
            return direct;

        // Defensive DBNull guard (callers already filter this out, but be correct standalone)
        if (value is DBNull)
            return default!;

        // Enum path
        if (IsEnum)
        {
            if (value is string enumString)
                return (T)Enum.Parse(TargetType, enumString, ignoreCase: true);

            Type enumUnderlyingType = Enum.GetUnderlyingType(TargetType);
            var numericEnum = System.Convert.ChangeType(value, enumUnderlyingType, System.Globalization.CultureInfo.InvariantCulture);
            return (T)Enum.ToObject(TargetType, numericEnum!);
        }

        // Guid / DateTimeOffset / TimeSpan / DateOnly / TimeOnly from string: none of these
        // implement IConvertible, so System.Convert.ChangeType cannot produce them (e.g. a
        // provider like SQLite that returns a "date"/"time" column as ISO-8601-ish text would
        // otherwise throw InvalidCastException here).
        object converted;
        if (TargetType == typeof(Guid) && value is string guidString)
            converted = Guid.Parse(guidString);
        else if (TargetType == typeof(DateTimeOffset) && value is string dtoString)
            converted = DateTimeOffset.Parse(dtoString, System.Globalization.CultureInfo.InvariantCulture);
        else if (TargetType == typeof(TimeSpan) && value is string tsString)
            converted = TimeSpan.Parse(tsString, System.Globalization.CultureInfo.InvariantCulture);
#if NET8_0_OR_GREATER
        else if (TargetType == typeof(DateOnly) && value is string doString)
            converted = DateOnly.Parse(doString, System.Globalization.CultureInfo.InvariantCulture);
        else if (TargetType == typeof(TimeOnly) && value is string toString)
            converted = TimeOnly.Parse(toString, System.Globalization.CultureInfo.InvariantCulture);
#endif
        else
            converted = System.Convert.ChangeType(value, TargetType, System.Globalization.CultureInfo.InvariantCulture);

        // Standard nullable unwrapping
        return IsNullable ? (T?)converted ?? default! : (T)converted!;
    }
}
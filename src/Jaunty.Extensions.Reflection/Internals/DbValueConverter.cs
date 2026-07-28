using System;
using System.Globalization;

namespace Jaunty.Extensions.Reflection;

/// <summary>
/// Converts raw ADO.NET values to CLR target types, working around <see cref="Convert.ChangeType(object, Type)"/>'s
/// inability to convert to <see cref="Nullable{T}"/> or enum target types (it always throws
/// <see cref="InvalidCastException"/> for both, regardless of the source value).
/// </summary>
internal static class DbValueConverter
{
    public static object ChangeType(object value, Type targetType)
    {
        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsEnum)
        {
            return value is string stringValue
                ? Enum.Parse(underlyingType, stringValue, ignoreCase: true)
                : Enum.ToObject(underlyingType, Convert.ChangeType(value, Enum.GetUnderlyingType(underlyingType), CultureInfo.InvariantCulture));
        }

        // Guid doesn't implement IConvertible, so Convert.ChangeType always throws for it -
        // needed for providers (e.g. SQLite) that store/return GUID columns as plain strings.
        if (underlyingType == typeof(Guid))
        {
            return value is Guid guidValue ? guidValue : Guid.Parse((string)value);
        }

        // CultureInfo.InvariantCulture, not the ambient CurrentCulture: providers routinely hand back
        // a string where the column is TEXT/NUMERIC (SQLite in particular), and under a comma-decimal
        // culture (de-DE, fr-FR, ...) Convert.ChangeType("1.5", typeof(decimal)) does not throw - it
        // reads the period as a group separator and returns 15.
        // This is the terminal conversion for every reflection-mapped column read, so the ambient
        // culture would otherwise silently scale numeric values by the host's locale.
        return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
    }
}

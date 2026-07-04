using System;

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
                : Enum.ToObject(underlyingType, Convert.ChangeType(value, Enum.GetUnderlyingType(underlyingType)));
        }

        return Convert.ChangeType(value, underlyingType);
    }
}

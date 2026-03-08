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

        // Enum path
        if (IsEnum)
        {
            var enumUnderlyingType = Enum.GetUnderlyingType(TargetType);
            var numeric = System.Convert.ChangeType(value, enumUnderlyingType);
            return (T)Enum.ToObject(TargetType, numeric!);
        }

        // Standard conversion with nullable unwrapping
        var converted = System.Convert.ChangeType(value, TargetType);
        return IsNullable ? (T?)converted ?? default! : (T)converted!;
    }
}
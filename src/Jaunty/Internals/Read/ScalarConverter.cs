namespace Jaunty.Internals.Read;

internal static class ScalarConverter<T>
{
    private static readonly Type TargetType;
    private static readonly bool IsNullable;

    static ScalarConverter()
    {
        TargetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        IsNullable = Nullable.GetUnderlyingType(typeof(T)) is not null;
    }

    internal static T Convert(object value)
    {
        // Fast path: direct type match
        if (value is T direct)
            return direct;

        // Defensive DBNull guard (callers already filter this out, but be correct standalone)
        if (value is DBNull)
            return default!;

        // AUD-R26-062: every branch that used to live here - enum, Guid/DateTimeOffset/TimeSpan/
        // DateOnly/TimeOnly from a string, and the InvariantCulture Convert.ChangeType fallback -
        // moved to DbValueConversion, which is now the single terminal conversion for the whole
        // library. Two sibling implementations in Jaunty.Fluent and Jaunty.Extensions.Reflection
        // had drifted from this one on four separate cases; see DbValueConversion's remarks.
        object converted = DbValueConversion.Convert(value, TargetType);

        // Standard nullable unwrapping
        return IsNullable ? (T?)converted ?? default! : (T)converted!;
    }
}
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
        // AUD-R35-126: this guard used to sit below the fast path, which made its own comment
        // false. `value is T direct` matches DBNull.Value whenever T is object or DBNull, so
        // ScalarConverter<object>.Convert(DBNull.Value) returned DBNull.Value while every other T
        // returned default - the one shape a caller trusting "correct standalone" would get wrong.
        // Neither current caller can reach it (ScalarExecution and GridReader both filter
        // `null or DBNull` before calling in), so nothing observable changes today; the point is
        // that the guard now means what it says for whoever calls next.
        if (value is DBNull)
            return default!;

        // Fast path: direct type match
        if (value is T direct)
            return direct;

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
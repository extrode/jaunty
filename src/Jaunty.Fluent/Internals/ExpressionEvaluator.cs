using System.Linq.Expressions;
using System.Reflection;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Evaluates the non-column operand of a translated expression - the "value" side of
/// <c>.Where(p =&gt; p.Id == someLocal)</c>.
/// </summary>
/// <remarks>
/// <para>
/// A closure-captured local or method parameter does not arrive as a <see cref="ConstantExpression"/>:
/// the C# compiler lifts it onto a generated closure class, so it reaches here as a
/// <see cref="MemberExpression"/> - a field read - over a <see cref="ConstantExpression"/> holding
/// the closure instance. That is the overwhelmingly common case, and it used to miss the constant
/// fast path entirely and fall through to <c>Expression.Lambda(...).Compile().DynamicInvoke()</c>:
/// a full expression compile (a <c>DynamicMethod</c> emit costing tens to hundreds of microseconds,
/// plus code heap that is never reclaimed) followed by a reflection invoke with its
/// <c>object[]</c> boxing, where reading the field is a handful of nanoseconds. A predicate
/// capturing five values paid it five times, on every query build, with nothing reused between
/// builds - AUD-R25.
/// </para>
/// <para>
/// Walking the member chain covers that case and the nested ones it produces (a captured field of a
/// captured object, a static field, a readable property). Anything genuinely computed - a method
/// call, arithmetic, a conversion - still falls back to compiling, so no expression that used to
/// evaluate stops evaluating.
/// </para>
/// <para>
/// This was previously eight byte-identical private copies: the six expression visitors, this
/// assembly's <see cref="HavingExpressionHelpers"/>, and
/// <c>Jaunty.FlatFiles.DuckDB.Internals.ExpressionTranslator</c>. The seven in this assembly now
/// share this one; the eighth is in a separate assembly and carries its own copy of the fast path.
/// </para>
/// </remarks>
internal static class ExpressionEvaluator
{
    public static object? Evaluate(Expression expression)
    {
        // The generic Lambda<TDelegate> with a statically-known delegate type, not the non-generic
        // Lambda(...).Compile().DynamicInvoke(): net10's ref assemblies annotate the non-generic
        // factory [RequiresDynamicCode] (IL3050) because it must construct a delegate type at
        // runtime, where Func<object?> is fixed at compile time and Compile() falls back to the
        // interpreter under NativeAOT. Also skips DynamicInvoke's reflection dispatch. Exceptions
        // from the evaluated member now surface unwrapped instead of inside
        // TargetInvocationException; the tests assert InnerException ?? ex for exactly this reason.
        return TryEvaluate(expression, out object? value)
            ? value
            : Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile()();
    }

    /// <summary>
    /// Attempts to read <paramref name="expression"/>'s value without compiling anything. Returns
    /// <see langword="false"/> - leaving the caller to compile - for anything this cannot resolve
    /// by a direct read, including cases where a direct read would report a different exception
    /// than the compiled form would.
    /// </summary>
    private static bool TryEvaluate(Expression expression, out object? value)
    {
        value = null;

        switch (expression)
        {
            case ConstantExpression constant:
                value = constant.Value;
                return true;

            case MemberExpression member:
                object? instance = null;

                // A null Expression means a static member. Otherwise the declaring object must
                // itself be readable without compiling, or there is nothing to be gained.
                if (member.Expression is not null && !TryEvaluate(member.Expression, out instance))
                    return false;

                return TryReadMember(member.Member, instance, out value);

            default:
                return false;
        }
    }

    private static bool TryReadMember(MemberInfo member, object? instance, out object? value)
    {
        value = null;

        switch (member)
        {
            case FieldInfo field:
                // A null instance on an instance field would throw TargetException here but
                // NullReferenceException from compiled code; leave it to the compiled path so the
                // failure a caller sees does not depend on which route was taken.
                if (instance is null && !field.IsStatic)
                    return false;

                value = field.GetValue(instance);
                return true;

            case PropertyInfo property:
                MethodInfo? getter = property.GetGetMethod(nonPublic: true);

                if (getter is null || property.GetIndexParameters().Length != 0)
                    return false;

                if (instance is null && !getter.IsStatic)
                    return false;

                value = property.GetValue(instance);
                return true;

            default:
                return false;
        }
    }
}

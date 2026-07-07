using System.Linq.Expressions;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Closure-safe HAVING expression evaluation, shared by the single-entity
/// GroupedQueryBuilder and the joined-group builders. Handles values that don't arrive as
/// a <see cref="ConstantExpression"/> - closure-captured local variables and method
/// parameters compile to a <see cref="MemberExpression"/> over a compiler-generated closure
/// class, so they must be evaluated rather than read as a literal.
/// </summary>
internal static class HavingExpressionHelpers
{
    public static string FormatLiteral(object? value)
    {
        return value switch
        {
            null => "NULL",
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            _ => value.ToString() ?? "NULL"
        };
    }

    public static object? EvaluateExpression(Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        LambdaExpression lambda = Expression.Lambda(expression);
        Delegate compiled = lambda.Compile();
        return compiled.DynamicInvoke();
    }
}

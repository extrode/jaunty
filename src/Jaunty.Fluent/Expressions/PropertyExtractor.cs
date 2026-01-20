using System.Linq.Expressions;
using System.Reflection;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Extracts property names from lambda expressions for column selection.
/// Handles boxing (value types wrapped in Convert) and unary expressions.
/// </summary>
internal static class PropertyExtractor
{
    /// <summary>
    /// Extracts the property name from an expression like p => p.Id or p => p.Name
    /// </summary>
    public static string ExtractPropertyName<T, TProperty>(Expression<Func<T, TProperty>> selector)
    {
        var memberInfo = GetMemberInfo(selector.Body);
        return memberInfo?.Name ?? throw new ArgumentException($"Expression '{selector}' does not refer to a property.", nameof(selector));
    }

    /// <summary>
    /// Extracts property names from multiple expressions.
    /// Handles Expression{Func{T, object}} which may wrap value types in Convert.
    /// </summary>
    public static string[] ExtractPropertyNames<T>(params Expression<Func<T, object?>>[] selectors)
    {
        if (selectors is null || selectors.Length == 0)
            return [];

        var names = new string[selectors.Length];
        for (int i = 0; i < selectors.Length; i++)
        {
            var memberInfo = GetMemberInfo(selectors[i].Body);
            names[i] = memberInfo?.Name ?? throw new ArgumentException($"Expression '{selectors[i]}' does not refer to a property.", nameof(selectors));
        }
        return names;
    }

    /// <summary>
    /// Gets the MemberInfo from an expression, handling Convert and other wrappers.
    /// </summary>
    private static MemberInfo? GetMemberInfo(Expression expression)
    {
        // Handle Convert (boxing for value types like p => p.Id where Id is int)
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        // Direct member access
        if (expression is MemberExpression member && member.Member is PropertyInfo prop)
        {
            // Handle nullable .Value access (e.g., p => p.CategoryId!.Value)
            // We want to return "CategoryId", not "Value"
            if (prop.Name == "Value" && prop.DeclaringType?.IsGenericType == true
                && prop.DeclaringType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // Look at the parent member expression
                if (member.Expression is MemberExpression parentMember && parentMember.Member is PropertyInfo)
                    return parentMember.Member;
            }
            return member.Member;
        }

        return null;
    }

    /// <summary>
    /// Extracts the property name from an order by expression that may return object.
    /// </summary>
    public static string ExtractOrderByProperty<T>(Expression<Func<T, object?>> selector)
    {
        var memberInfo = GetMemberInfo(selector.Body);
        return memberInfo?.Name ?? throw new ArgumentException($"Expression '{selector}' does not refer to a property.", nameof(selector));
    }
}

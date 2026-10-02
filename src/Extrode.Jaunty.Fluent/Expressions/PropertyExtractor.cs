using System.Linq.Expressions;
using System.Reflection;

namespace Extrode.Jaunty.Fluent.Expressions;

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
        MemberInfo? memberInfo = GetMemberInfo(selector.Body);
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
            MemberInfo? memberInfo = GetMemberInfo(selectors[i].Body);
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
                {
                    // The unwrapped receiver is the column; it still has to be one read directly
                    // off the parameter, so p => p.OrderDate.Value is rejected the same way
                    // p => p.OrderDate.Year is.
                    ColumnReference.RequireDirect(parentMember);
                    return parentMember.Member;
                }
            }

            // AUD-R35-019: the third copy of the AUD-R34-021 defect, on the path feeding join
            // keys, ORDER BY and INSERT column lists. Without this, p => p.OrderDate.Year yielded
            // "Year", which a column lookup that falls back to escaping the name itself turned
            // into a real-looking column reference.
            ColumnReference.RequireDirect(member);
            return member.Member;
        }

        return null;
    }

    /// <summary>
    /// Resolves a multi-parameter selector such as <c>(a, b, c) =&gt; c.Name</c> to the position of
    /// the parameter it reads and the property it reads off it.
    /// </summary>
    /// <remarks>
    /// AUD-R38-040: lets a joined ORDER BY name its entity by position, which stays unambiguous when
    /// two joined entities have the same type.
    /// </remarks>
    public static (int ParameterIndex, string PropertyName) ExtractParameterMember(LambdaExpression selector)
    {
        MemberInfo? memberInfo = GetMemberInfo(selector.Body);
        int index = memberInfo is null ? -1 : selector.Parameters.IndexOf(RootParameter(selector.Body)!);
        if (index < 0)
            throw new ArgumentException($"Expression '{selector}' does not refer to a property of one of its parameters.", nameof(selector));
        return (index, memberInfo!.Name);
    }

    private static ParameterExpression? RootParameter(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;
        while (expression is MemberExpression member)
            expression = member.Expression!;
        return expression as ParameterExpression;
    }

    /// <summary>
    /// Extracts the property name from an order by expression that may return object.
    /// </summary>
    public static string ExtractOrderByProperty<T>(Expression<Func<T, object?>> selector)
    {
        MemberInfo? memberInfo = GetMemberInfo(selector.Body);
        return memberInfo?.Name ?? throw new ArgumentException($"Expression '{selector}' does not refer to a property.", nameof(selector));
    }
}
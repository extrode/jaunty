using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Translates GROUP BY Select expressions into SQL SELECT and GROUP BY clauses.
/// Handles g.Key, g.Count(), g.Sum(), g.Avg(), g.Min(), g.Max() patterns.
/// </summary>
internal sealed class GroupByExpressionVisitor<T, TKey> : ExpressionVisitor where T : new()
{
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _metadata;
    private readonly string[] _groupByColumns;
    private readonly List<string> _selectColumns = new();
    private readonly List<string> _columnAliases = new();

    public GroupByExpressionVisitor(ISqlDialect dialect, string[] groupByColumns)
    {
        _dialect = dialect;
        _metadata = FluentMetadataCache.GetMetadata<T>();
        _groupByColumns = groupByColumns;
    }

    /// <summary>
    /// Translates a Select projection expression to SQL column list.
    /// </summary>
    public (string[] SelectColumns, string[] Aliases) TranslateSelect<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector)
    {
        _selectColumns.Clear();
        _columnAliases.Clear();

        var body = selector.Body;

        if (body is NewExpression newExpr)
        {
            // Anonymous type: new { Key = g.Key, Count = g.Count() }
            TranslateNewExpression(newExpr);
        }
        else if (body is MemberInitExpression memberInit)
        {
            // DTO initialization: new ProductStats { CategoryId = g.Key, Count = g.Count() }
            TranslateMemberInit(memberInit);
        }
        else
        {
            // Single expression: g.Key or g.Count()
            var (sql, alias) = TranslateExpression(body, "Value");
            _selectColumns.Add(sql);
            _columnAliases.Add(alias);
        }

        return (_selectColumns.ToArray(), _columnAliases.ToArray());
    }

    private void TranslateNewExpression(NewExpression newExpr)
    {
        var members = newExpr.Members;
        var arguments = newExpr.Arguments;

        for (int i = 0; i < arguments.Count; i++)
        {
            var memberName = members?[i]?.Name ?? $"Column{i}";
            var (sql, _) = TranslateExpression(arguments[i], memberName);
            _selectColumns.Add($"{sql} AS {_dialect.EscapeColumnName(memberName)}");
            _columnAliases.Add(memberName);
        }
    }

    private void TranslateMemberInit(MemberInitExpression memberInit)
    {
        foreach (var binding in memberInit.Bindings)
        {
            if (binding is MemberAssignment assignment)
            {
                var memberName = assignment.Member.Name;
                var (sql, _) = TranslateExpression(assignment.Expression, memberName);
                _selectColumns.Add($"{sql} AS {_dialect.EscapeColumnName(memberName)}");
                _columnAliases.Add(memberName);
            }
        }
    }

    private (string Sql, string Alias) TranslateExpression(Expression expr, string defaultAlias)
    {
        // Recursively unwrap Convert, Quote, and Lambda
        while (true)
        {
            if (expr is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.Quote))
            {
                expr = unary.Operand;
                continue;
            }
            if (expr is LambdaExpression lambda)
            {
                expr = lambda.Body;
                continue;
            }
            break;
        }

        // g.Key
        if (IsKeyAccess(expr))
        {
            // For single column key, just use the first grouping column
            return (_groupByColumns[0], defaultAlias);
        }

        // g.Key.Property (for composite keys like new { p.CategoryId, p.SupplierId })
        if (expr is MemberExpression memberExpr && IsKeyAccess(memberExpr.Expression))
        {
            var propertyName = memberExpr.Member.Name;
            var columnName = GetColumnName(propertyName);
            return (_dialect.EscapeColumnName(columnName), defaultAlias);
        }

        // g.Count(), g.Sum(p => p.Col), etc.
        if (expr is MethodCallExpression methodCall)
        {
            return TranslateMethodCall(methodCall, defaultAlias);
        }

        // Constant
        if (expr is ConstantExpression constant)
        {
            return (FormatConstant(constant.Value), defaultAlias);
        }

        throw new NotSupportedException($"Expression type '{expr.NodeType}' is not supported in GROUP BY Select.");
    }

    private (string Sql, string Alias) TranslateMethodCall(MethodCallExpression methodCall, string defaultAlias)
    {
        var methodName = methodCall.Method.Name;

        // Aggregate methods on IGrouping (extensions) usually have 'g' as first argument if static,
        // or node.Object if instance.
        bool isGroupingMethod = (methodCall.Object != null && IsGroupingParameter(methodCall.Object)) ||
                                (methodCall.Arguments.Count > 0 && IsGroupingParameter(methodCall.Arguments[0]));

        if (isGroupingMethod)
        {
            // Find the selector argument (if any)
            // For Sum(g, p => p.Price), it's argument 1.
            // For Count(g), it's none.
            // For g.Sum(p => p.Price), it's argument 0.
            Expression? selector = null;
            if (methodCall.Object == null) // Extension method
            {
                if (methodCall.Arguments.Count > 1) selector = methodCall.Arguments[1];
            }
            else // Instance method
            {
                if (methodCall.Arguments.Count > 0) selector = methodCall.Arguments[0];
            }

            return methodName switch
            {
                "Count" => (BuildAggregateWithColumn("COUNT", selector), defaultAlias),
                "Sum" => (BuildAggregateWithColumn("SUM", selector), defaultAlias),
                "Avg" => (BuildAggregateWithColumn("AVG", selector), defaultAlias),
                "Average" => (BuildAggregateWithColumn("AVG", selector), defaultAlias),
                "Min" => (BuildAggregateWithColumn("MIN", selector), defaultAlias),
                "Max" => (BuildAggregateWithColumn("MAX", selector), defaultAlias),
                _ => throw new NotSupportedException($"Method '{methodName}' is not supported in GROUP BY Select.")
            };
        }

        throw new NotSupportedException($"Method '{methodName}' on type '{methodCall.Method.DeclaringType?.Name}' is not supported in GROUP BY Select.");
    }

    private string BuildAggregateWithColumn(string aggregate, Expression? expr)
    {
        if (expr == null) return $"{aggregate}(*)";

        // Recursively unwrap the expression
        while (true)
        {
            if (expr is LambdaExpression lambda)
            {
                expr = lambda.Body;
                continue;
            }

            if (expr is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.Quote))
            {
                expr = unary.Operand;
                continue;
            }

            break;
        }

        if (expr is MemberExpression memberExpr)
        {
            var propertyName = memberExpr.Member.Name;
            var columnName = GetColumnName(propertyName);
            return $"{aggregate}({_dialect.EscapeColumnName(columnName)})";
        }

        if (expr is ConstantExpression constant)
        {
            return $"{aggregate}({FormatConstant(constant.Value)})";
        }

        throw new NotSupportedException($"Cannot extract column from aggregate expression of type '{expr.NodeType}'.");
    }

    private bool IsKeyAccess(Expression? expr)
    {
        if (expr is MemberExpression memberExpr && memberExpr.Member.Name == "Key")
        {
            return IsGroupingParameter(memberExpr.Expression);
        }
        return false;
    }

    private bool IsGroupingParameter(Expression? expr)
    {
        while (expr is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.Quote))
        {
            expr = unary.Operand;
        }

        if (expr is ParameterExpression param)
        {
            return param.Type.IsGenericType &&
                   param.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>);
        }

        // Also handle cases where it might be a MemberExpression to the parameter
        if (expr?.Type.IsGenericType == true && expr.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>))
        {
            return true;
        }

        return false;
    }

    private string GetColumnName(string propertyName)
    {
        var columns = _metadata.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
                return columns[i].ColumnName;
        }
        return propertyName;
    }

    private static string FormatConstant(object? value)
    {
        if (value is null) return "NULL";
        if (value is string s) return $"'{s.Replace("'", "''")}'";
        if (value is bool b) return b ? "1" : "0";
        return value.ToString() ?? "NULL";
    }
}
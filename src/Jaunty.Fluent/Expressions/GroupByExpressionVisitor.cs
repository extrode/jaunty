using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Fluent.Internals;
using Jaunty.Fluent.Interfaces;

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
    private readonly StringBuilder _currentExpression = new();

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
        // g.Key
        if (IsKeyAccess(expr))
        {
            // For single column key, just use the column
            if (_groupByColumns.Length == 1)
            {
                return (_groupByColumns[0], defaultAlias);
            }
            // For composite key, this is more complex - for now just use first column
            return (_groupByColumns[0], defaultAlias);
        }

        // g.Key.Property (for composite keys like new { p.CategoryId, p.SupplierId })
        if (expr is MemberExpression memberExpr && IsKeyAccess(memberExpr.Expression!))
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

        // Check if this is a method on IGrouping<TKey, T>
        if (methodCall.Object != null && IsGroupingParameter(methodCall.Object))
        {
            return methodName switch
            {
                "Count" when methodCall.Arguments.Count == 0 => ("COUNT(*)", defaultAlias),
                "Count" when methodCall.Arguments.Count == 1 => (BuildAggregateWithColumn("COUNT", methodCall.Arguments[0]), defaultAlias),
                "Sum" => (BuildAggregateWithColumn("SUM", methodCall.Arguments[0]), defaultAlias),
                "Avg" => (BuildAggregateWithColumn("AVG", methodCall.Arguments[0]), defaultAlias),
                "Min" => (BuildAggregateWithColumn("MIN", methodCall.Arguments[0]), defaultAlias),
                "Max" => (BuildAggregateWithColumn("MAX", methodCall.Arguments[0]), defaultAlias),
                _ => throw new NotSupportedException($"Method '{methodName}' is not supported in GROUP BY Select.")
            };
        }

        throw new NotSupportedException($"Method '{methodName}' is not supported in GROUP BY Select.");
    }

    private string BuildAggregateWithColumn(string aggregate, Expression selectorExpr)
    {
        // Extract column from expression like p => p.UnitPrice
        if (selectorExpr is UnaryExpression unary)
            selectorExpr = unary.Operand;

        if (selectorExpr is LambdaExpression lambda)
        {
            var body = lambda.Body;
            if (body is UnaryExpression unaryBody)
                body = unaryBody.Operand;

            if (body is MemberExpression memberExpr)
            {
                var propertyName = memberExpr.Member.Name;
                var columnName = GetColumnName(propertyName);
                return $"{aggregate}({_dialect.EscapeColumnName(columnName)})";
            }
        }

        throw new NotSupportedException($"Cannot extract column from aggregate expression.");
    }

    private bool IsKeyAccess(Expression? expr)
    {
        // g.Key
        if (expr is MemberExpression memberExpr && memberExpr.Member.Name == "Key")
        {
            return IsGroupingParameter(memberExpr.Expression);
        }
        return false;
    }

    private bool IsGroupingParameter(Expression? expr)
    {
        // Check if this is the grouping parameter (g)
        return expr is ParameterExpression param &&
               param.Type.IsGenericType &&
               param.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>);
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

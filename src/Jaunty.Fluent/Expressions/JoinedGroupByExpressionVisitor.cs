using System.Collections.ObjectModel;
using System.Linq.Expressions;

using Jaunty.Dialects;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Translates GROUP BY key and Select expressions for joined queries (2/3/4-way) into SQL.
/// Sibling to <see cref="GroupByExpressionVisitor{T,TKey}"/> rather than a widened version of
/// it: joined key/aggregate selectors are multi-parameter (<c>Expression&lt;Func&lt;TFrom,
/// TJoin,TResult&gt;&gt;</c>, not a single-entity <c>Expression&lt;Func&lt;T,TResult&gt;&gt;</c>),
/// so column resolution needs to know which of N joined entities a given member access is
/// rooted in - done here by matching parameter position against an ordered
/// <see cref="EntityMetadata"/> array (index 0 = TFrom, index 1 = TJoin, and so on, matching
/// every joined-query type parameter list's declared order throughout Jaunty.Fluent).
/// </summary>
internal sealed class JoinedGroupByExpressionVisitor
{
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata[] _metadata;
    private readonly string[] _groupByColumns;
    private readonly Dictionary<string, string> _keyPropertyToColumn;
    private readonly List<string> _selectColumns = new();
    private readonly List<string> _columnAliases = new();

    public JoinedGroupByExpressionVisitor(ISqlDialect dialect, EntityMetadata[] metadata, LambdaExpression keySelector)
    {
        _dialect = dialect;
        _metadata = metadata;
        (_groupByColumns, _keyPropertyToColumn) = ExtractGroupByColumns(keySelector);
    }

    public string[] GroupByColumns => _groupByColumns;

    /// <summary>
    /// Translates a Select projection expression (its single parameter is the
    /// IGroupingJoined{,3,4} instance) to a SQL column list.
    /// </summary>
    public (string[] SelectColumns, string[] Aliases) TranslateSelect(LambdaExpression selector)
    {
        _selectColumns.Clear();
        _columnAliases.Clear();

        ParameterExpression groupingParam = selector.Parameters[0];
        Expression body = selector.Body;

        if (body is NewExpression newExpr)
        {
            TranslateNewExpression(newExpr, groupingParam);
        }
        else if (body is MemberInitExpression memberInit)
        {
            TranslateMemberInit(memberInit, groupingParam);
        }
        else
        {
            (string sql, string alias) = TranslateExpression(body, "Value", groupingParam);
            _selectColumns.Add(sql);
            _columnAliases.Add(alias);
        }

        return (_selectColumns.ToArray(), _columnAliases.ToArray());
    }

    private void TranslateNewExpression(NewExpression newExpr, ParameterExpression groupingParam)
    {
        ReadOnlyCollection<System.Reflection.MemberInfo>? members = newExpr.Members;
        ReadOnlyCollection<Expression> arguments = newExpr.Arguments;

        for (int i = 0; i < arguments.Count; i++)
        {
            var memberName = members?[i]?.Name ?? $"Column{i}";
            (string sql, string _) = TranslateExpression(arguments[i], memberName, groupingParam);
            _selectColumns.Add($"{sql} AS {_dialect.EscapeColumnName(memberName)}");
            _columnAliases.Add(memberName);
        }
    }

    private void TranslateMemberInit(MemberInitExpression memberInit, ParameterExpression groupingParam)
    {
        foreach (MemberBinding binding in memberInit.Bindings)
        {
            if (binding is MemberAssignment assignment)
            {
                var memberName = assignment.Member.Name;
                (string sql, string _) = TranslateExpression(assignment.Expression, memberName, groupingParam);
                _selectColumns.Add($"{sql} AS {_dialect.EscapeColumnName(memberName)}");
                _columnAliases.Add(memberName);
            }
        }
    }

    private (string Sql, string Alias) TranslateExpression(Expression expr, string defaultAlias, ParameterExpression groupingParam)
    {
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

        // g.Key (single-column key)
        if (expr is MemberExpression keyMember && keyMember.Member.Name == "Key" && IsGroupingAccess(keyMember.Expression, groupingParam))
        {
            return (_groupByColumns[0], defaultAlias);
        }

        // g.Key.PropertyName (composite key)
        if (expr is MemberExpression compositeMember &&
            compositeMember.Expression is MemberExpression innerKey &&
            innerKey.Member.Name == "Key" &&
            IsGroupingAccess(innerKey.Expression, groupingParam))
        {
            if (_keyPropertyToColumn.TryGetValue(compositeMember.Member.Name, out string? columnSql))
                return (columnSql, defaultAlias);

            throw new NotSupportedException($"Unknown GROUP BY key property '{compositeMember.Member.Name}'.");
        }

        // g.Count(), g.Sum((t1,t2) => t1.Col), etc.
        if (expr is MethodCallExpression methodCall)
        {
            return TranslateMethodCall(methodCall, defaultAlias, groupingParam);
        }

        if (expr is ConstantExpression constant)
        {
            return (HavingExpressionHelpers.FormatLiteral(constant.Value), defaultAlias);
        }

        throw new NotSupportedException($"Expression type '{expr.NodeType}' is not supported in GROUP BY Select.");
    }

    private (string Sql, string Alias) TranslateMethodCall(MethodCallExpression methodCall, string defaultAlias, ParameterExpression groupingParam)
    {
        var methodName = methodCall.Method.Name;

        bool isGroupingMethod = IsGroupingAccess(methodCall.Object, groupingParam) ||
                                 (methodCall.Arguments.Count > 0 && IsGroupingAccess(methodCall.Arguments[0], groupingParam));

        if (isGroupingMethod)
        {
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

        while (true)
        {
            if (expr is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.Quote))
            {
                expr = unary.Operand;
                continue;
            }
            break;
        }

        if (expr is LambdaExpression lambda)
        {
            Expression body = lambda.Body;

            if (body is UnaryExpression unaryBody && (unaryBody.NodeType == ExpressionType.Convert || unaryBody.NodeType == ExpressionType.Quote))
                body = unaryBody.Operand;

            if (body is MemberExpression memberExpr)
            {
                int paramIndex = GetParameterIndex(memberExpr, lambda.Parameters);
                string columnName = GetColumnName(_metadata[paramIndex], memberExpr.Member.Name);
                return $"{aggregate}({_dialect.EscapeColumnName(columnName)})";
            }

            if (body is ConstantExpression constant)
            {
                return $"{aggregate}({HavingExpressionHelpers.FormatLiteral(constant.Value)})";
            }
        }

        throw new NotSupportedException("Cannot extract column from HAVING/GROUP BY aggregate expression.");
    }

    private (string[] Columns, Dictionary<string, string> PropertyToColumn) ExtractGroupByColumns(LambdaExpression keySelector)
    {
        Expression body = keySelector.Body;

        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        // Single property: (t1,t2) => t1.CategoryId
        if (body is MemberExpression member)
        {
            int paramIndex = GetParameterIndex(member, keySelector.Parameters);
            string columnName = GetColumnName(_metadata[paramIndex], member.Member.Name);
            return ([_dialect.EscapeColumnName(columnName)], new Dictionary<string, string>());
        }

        // Composite key: (t1,t2) => new { t1.CategoryId, t2.SupplierId }
        if (body is NewExpression newExpr)
        {
            var columns = new string[newExpr.Arguments.Count];
            var propertyToColumn = new Dictionary<string, string>();

            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                if (newExpr.Arguments[i] is not MemberExpression memberArg)
                    throw new NotSupportedException("GROUP BY key must be property expressions.");

                int paramIndex = GetParameterIndex(memberArg, keySelector.Parameters);
                string columnName = GetColumnName(_metadata[paramIndex], memberArg.Member.Name);
                string escaped = _dialect.EscapeColumnName(columnName);
                columns[i] = escaped;

                var keyPropertyName = newExpr.Members?[i]?.Name ?? memberArg.Member.Name;
                propertyToColumn[keyPropertyName] = escaped;
            }

            return (columns, propertyToColumn);
        }

        throw new NotSupportedException($"Cannot extract GROUP BY columns from expression type '{body.NodeType}'.");
    }

    private static int GetParameterIndex(MemberExpression memberExpr, ReadOnlyCollection<ParameterExpression> parameters)
    {
        Expression? root = memberExpr.Expression;

        while (root is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.Quote))
            root = unary.Operand;

        if (root is ParameterExpression param)
        {
            for (int i = 0; i < parameters.Count; i++)
                if (ReferenceEquals(parameters[i], param))
                    return i;
        }

        throw new NotSupportedException($"Cannot resolve which joined entity member '{memberExpr.Member.Name}' belongs to.");
    }

    private static bool IsGroupingAccess(Expression? expr, ParameterExpression groupingParam)
    {
        while (expr is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.Quote))
            expr = unary.Operand;

        return expr is ParameterExpression p && ReferenceEquals(p, groupingParam);
    }

    private static string GetColumnName(EntityMetadata metadata, string propertyName)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].PropertyName == propertyName)
                return columns[i].ColumnName;
        }
        return propertyName;
    }
}

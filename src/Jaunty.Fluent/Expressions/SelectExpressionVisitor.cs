using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Converts SELECT projection expressions to SQL column lists.
/// Supports: entity properties, anonymous types, window functions, SQL functions.
/// </summary>
internal sealed class SelectExpressionVisitor<T> : ExpressionVisitor where T : new()
{
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _metadata;
    private readonly List<SelectColumn> _columns = new();

    public SelectExpressionVisitor(ISqlDialect dialect)
    {
        _dialect = dialect;
        _metadata = FluentMetadataCache.GetMetadata<T>();
    }

    /// <summary>
    /// Translates a projection expression to a list of SELECT columns with SQL.
    /// </summary>
    public List<SelectColumn> Translate<TResult>(Expression<Func<T, TResult>> selector)
    {
        _columns.Clear();

        var body = selector.Body;

        // Handle standalone expressions (not New or MemberInit)
        if (body is MethodCallExpression or MemberExpression or ConstantExpression or UnaryExpression or BinaryExpression)
        {
            var sql = TranslateProjectionExpression(body);
            // Try to find a meaningful alias
            string alias = "Value";
            if (body is MemberExpression member) alias = member.Member.Name;

            _columns.Add(new SelectColumn(sql, alias));
            return _columns;
        }

        Visit(body);
        return _columns;
    }

    protected override Expression VisitNew(NewExpression node)
    {
        // Handle anonymous type: new { p.ProductName, RowNum = Sql.RowNumber()... }
        if (node.Members == null)
        {
            throw new NotSupportedException("Only anonymous types with named members are supported in SELECT projections.");
        }

        for (int i = 0; i < node.Arguments.Count; i++)
        {
            var member = node.Members[i];
            var argument = node.Arguments[i];
            var alias = member.Name;

            var sql = TranslateProjectionExpression(argument);
            _columns.Add(new SelectColumn(sql, alias));
        }

        return node;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        // Handle object initializer: new ProductDto { Name = p.ProductName, ... }
        foreach (var binding in node.Bindings)
        {
            if (binding is MemberAssignment assignment)
            {
                var alias = assignment.Member.Name;
                var sql = TranslateProjectionExpression(assignment.Expression);
                _columns.Add(new SelectColumn(sql, alias));
            }
        }

        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Single member selection: p => p.ProductName
        if (IsParameterMember(node))
        {
            var columnName = GetColumnName(node);
            var escapedColumn = _dialect.EscapeColumnName(columnName);
            _columns.Add(new SelectColumn(escapedColumn, node.Member.Name));
        }
        return node;
    }

    private string TranslateProjectionExpression(Expression expr)
    {
        // Recursively unwrap Convert and Quote
        while (expr is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.Quote))
        {
            expr = unary.Operand;
        }

        // Entity property (p.ProductName)
        if (expr is MemberExpression member && IsParameterMember(member))
        {
            var columnName = GetColumnName(member);
            return _dialect.EscapeColumnName(columnName);
        }

        // Method call (Sql.RowNumber(), Sql.Length(), etc.)
        if (expr is MethodCallExpression methodCall)
        {
            return TranslateMethodCall(methodCall);
        }

        // Coalesce operator (??)
        if (expr is BinaryExpression binary && binary.NodeType == ExpressionType.Coalesce)
        {
            return _dialect.GenerateIsNull(TranslateProjectionExpression(binary.Left), TranslateProjectionExpression(binary.Right));
        }

        // Constant value
        if (expr is ConstantExpression constant)
        {
            return FormatConstant(constant.Value);
        }

        // Lambda
        if (expr is LambdaExpression lambda)
        {
            return TranslateProjectionExpression(lambda.Body);
        }

        throw new NotSupportedException($"Expression type '{expr.NodeType}' is not supported in SELECT projections.");
    }

    private string TranslateMethodCall(MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;

        // Handle Sql.* static methods
        if (declaringType == typeof(Sql))
        {
            return TranslateSqlFunction(node);
        }

        // Handle WindowBuilder / WindowAggregateBuilder method chains
        if (declaringType != null && declaringType.IsGenericType)
        {
            var genericDef = declaringType.GetGenericTypeDefinition();

            if (genericDef == typeof(WindowBuilder<,>) || genericDef == typeof(WindowAggregateBuilder<,>))
            {
                return TranslateWindowFunctionChain(node);
            }
        }

        throw new NotSupportedException($"Method '{node.Method.Name}' on type '{declaringType?.Name}' is not supported in SELECT projections.");
    }

    private string TranslateSqlFunction(MethodCallExpression node)
    {
        var methodName = node.Method.Name;

        switch (methodName)
        {
            // Window ranking and aggregate functions (return builder markers)
            case "RowNumber":
            case "Rank":
            case "DenseRank":
            case "NTile":
            case "Sum":
            case "Avg":
            case "Count":
            case "Min":
            case "Max":
                // Basic window function with empty OVER()
                return TranslateBaseWindowFunction(node) + _dialect.GenerateOverClause(null, null);

            // Regular SQL functions
            case "Coalesce": return TranslateCoalesce(node);
            case "IsNull": return TranslateIsNull(node);
            case "NullIf": return TranslateNullIf(node);
            case "Length": return TranslateLength(node);
            case "Upper": return TranslateUpper(node);
            case "Lower": return TranslateLower(node);
            case "Trim": return TranslateTrim(node);
            case "Substring": return TranslateSubstring(node);
            case "Year": return TranslateYear(node);
            case "Month": return TranslateMonth(node);
            case "Day": return TranslateDay(node);

            default:
                throw new NotSupportedException($"SQL function '{methodName}' is not supported in SELECT projections.");
        }
    }

    private string TranslateWindowFunctionChain(MethodCallExpression node)
    {
        var partitionBy = new List<string>();
        var orderBy = new List<(string column, bool descending)>();
        string? functionSql = null;

        Expression? current = node;
        while (current is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;
            var declaringType = methodCall.Method.DeclaringType;

            switch (methodName)
            {
                case "PartitionBy":
                    var partitionCol = TranslateColumnArgument(methodCall.Arguments[0]);
                    partitionBy.Insert(0, partitionCol);
                    current = methodCall.Object;
                    break;

                case "OrderBy":
                    var orderCol = TranslateColumnArgument(methodCall.Arguments[0]);
                    orderBy.Insert(0, (orderCol, false));
                    current = methodCall.Object;
                    break;

                case "OrderByDescending":
                    var orderDescCol = TranslateColumnArgument(methodCall.Arguments[0]);
                    orderBy.Insert(0, (orderDescCol, true));
                    current = methodCall.Object;
                    break;

                case "Over":
                    // Skip .Over() marker
                    current = methodCall.Object;
                    break;

                default:
                    if (declaringType == typeof(Sql))
                    {
                        // Found the base Sql.* method
                        functionSql = TranslateBaseWindowFunction(methodCall);
                        current = null;
                    }
                    else
                    {
                        throw new NotSupportedException($"Method '{methodName}' is not supported in window function chain.");
                    }
                    break;
            }
        }

        if (functionSql == null)
        {
            throw new InvalidOperationException("Window function chain must start with an appropriate Sql.* method.");
        }

        var overClause = _dialect.GenerateOverClause(
            partitionBy.Count > 0 ? partitionBy.ToArray() : null,
            orderBy.Count > 0 ? orderBy.ToArray() : null);

        return functionSql + overClause;
    }

    private string TranslateBaseWindowFunction(MethodCallExpression methodCall)
    {
        switch (methodCall.Method.Name)
        {
            case "RowNumber": return _dialect.GenerateRowNumber();
            case "Rank": return _dialect.GenerateRank();
            case "DenseRank": return _dialect.GenerateDenseRank();
            case "NTile":
                return _dialect.GenerateNTile((int)EvaluateExpression(methodCall.Arguments[0])!);
            case "Sum":
            case "Avg":
            case "Min":
            case "Max":
                string? aggregateColumn = null;
                if (methodCall.Arguments.Count > 0)
                {
                    aggregateColumn = TranslateColumnArgument(methodCall.Arguments[0]);
                }
                return _dialect.GenerateWindowAggregate(methodCall.Method.Name.ToUpperInvariant(), aggregateColumn);
            case "Count":
                return _dialect.GenerateWindowAggregate("COUNT", null);
            default:
                throw new NotSupportedException($"Window function base '{methodCall.Method.Name}' is not supported.");
        }
    }

    private string TranslateColumnArgument(Expression arg)
    {
        while (true)
        {
            if (arg is LambdaExpression lambda) { arg = lambda.Body; continue; }
            if (arg is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.Quote)) { arg = unary.Operand; continue; }
            break;
        }

        if (arg is MemberExpression member && IsParameterMember(member))
        {
            var columnName = GetColumnName(member);
            return _dialect.EscapeColumnName(columnName);
        }

        if (arg is MethodCallExpression or ConstantExpression || arg.NodeType == ExpressionType.Coalesce)
        {
            // For complex expressions in PartitionBy/OrderBy, we translate them to SQL
            // But we must remove any OVER() clause if they are window functions used inside another window function (rare but possible)
            // or just ensure they translate cleanly.
            return TranslateProjectionExpression(arg);
        }

        throw new NotSupportedException($"Window function PARTITION BY and ORDER BY must reference entity properties or supported SQL functions. Got: {arg.NodeType}");
    }

    private string TranslateCoalesce(MethodCallExpression node)
    {
        var args = node.Arguments.Select(TranslateProjectionExpression).ToArray();
        return _dialect.GenerateCoalesce(args);
    }

    private string TranslateIsNull(MethodCallExpression node)
    {
        var valueArg = TranslateProjectionExpression(node.Arguments[0]);
        var defaultArg = TranslateProjectionExpression(node.Arguments[1]);
        return _dialect.GenerateIsNull(valueArg, defaultArg);
    }

    private string TranslateNullIf(MethodCallExpression node)
    {
        var valueArg = TranslateProjectionExpression(node.Arguments[0]);
        var compareArg = TranslateProjectionExpression(node.Arguments[1]);
        return _dialect.GenerateNullIf(valueArg, compareArg);
    }

    private string TranslateLength(MethodCallExpression node)
    {
        var arg = TranslateProjectionExpression(node.Arguments[0]);
        return _dialect.GenerateLength(arg);
    }

    private string TranslateUpper(MethodCallExpression node)
    {
        var arg = TranslateProjectionExpression(node.Arguments[0]);
        return _dialect.GenerateUpper(arg);
    }

    private string TranslateLower(MethodCallExpression node)
    {
        var arg = TranslateProjectionExpression(node.Arguments[0]);
        return _dialect.GenerateLower(arg);
    }

    private string TranslateTrim(MethodCallExpression node)
    {
        var arg = TranslateProjectionExpression(node.Arguments[0]);
        return _dialect.GenerateTrim(arg);
    }

    private string TranslateSubstring(MethodCallExpression node)
    {
        var strArg = TranslateProjectionExpression(node.Arguments[0]);
        var startArg = TranslateProjectionExpression(node.Arguments[1]);
        var lengthArg = TranslateProjectionExpression(node.Arguments[2]);
        return _dialect.GenerateSubstring(strArg, startArg, lengthArg);
    }

    private string TranslateYear(MethodCallExpression node)
    {
        var arg = TranslateProjectionExpression(node.Arguments[0]);
        return _dialect.GenerateYear(arg);
    }

    private string TranslateMonth(MethodCallExpression node)
    {
        var arg = TranslateProjectionExpression(node.Arguments[0]);
        return _dialect.GenerateMonth(arg);
    }

    private string TranslateDay(MethodCallExpression node)
    {
        var arg = TranslateProjectionExpression(node.Arguments[0]);
        return _dialect.GenerateDay(arg);
    }

    private bool IsParameterMember(MemberExpression member)
    {
        Expression? current = member;
        while (current is MemberExpression me)
            current = me.Expression;

        return current is ParameterExpression;
    }

    private string GetColumnName(MemberExpression member)
    {
        var propertyName = member.Member.Name;
        var column = _metadata.Columns.FirstOrDefault(c => c.Property.Name == propertyName);
        return column?.ColumnName ?? propertyName;
    }

    private static object? EvaluateExpression(Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        var lambda = Expression.Lambda(expression);
        var compiled = lambda.Compile();
        return compiled.DynamicInvoke();
    }

    private static string FormatConstant(object? value)
    {
        return value switch
        {
            null => "NULL",
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            _ => value.ToString() ?? "NULL"
        };
    }
}

internal readonly struct SelectColumn
{
    public string Sql { get; }
    public string Alias { get; }

    public SelectColumn(string sql, string alias)
    {
        Sql = sql;
        Alias = alias;
    }
}
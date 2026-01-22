using System.Linq.Expressions;
using System.Text;

using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;

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
        _metadata = MetadataCache<T>.Metadata;
    }

    /// <summary>
    /// Translates a projection expression to a list of SELECT columns with SQL.
    /// </summary>
    public List<SelectColumn> Translate<TResult>(Expression<Func<T, TResult>> selector)
    {
        _columns.Clear();
        Visit(selector.Body);
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
        // Unwrap Convert
        if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expr = unary.Operand;

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

        // Constant value
        if (expr is ConstantExpression constant)
        {
            return FormatConstant(constant.Value);
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

        // Handle WindowBuilder method chains (PartitionBy, OrderBy, etc.)
        if (declaringType != null && declaringType.IsGenericType)
        {
            var genericDef = declaringType.GetGenericTypeDefinition();

            if (genericDef == typeof(WindowBuilder<>))
            {
                return TranslateWindowBuilderChain(node);
            }

            if (genericDef == typeof(WindowAggregateBuilder<>))
            {
                return TranslateWindowAggregateChain(node);
            }
        }

        throw new NotSupportedException($"Method '{node.Method.Name}' on type '{declaringType?.Name}' is not supported in SELECT projections.");
    }

    private string TranslateSqlFunction(MethodCallExpression node)
    {
        var methodName = node.Method.Name;

        return methodName switch
        {
            // Window ranking functions
            "RowNumber" => TranslateWindowFunction(node, _dialect.GenerateRowNumber()),
            "Rank" => TranslateWindowFunction(node, _dialect.GenerateRank()),
            "DenseRank" => TranslateWindowFunction(node, _dialect.GenerateDenseRank()),
            "NTile" => TranslateNTile(node),

            // Window aggregates
            "Sum" => TranslateWindowAggregate(node, "SUM"),
            "Avg" => TranslateWindowAggregate(node, "AVG"),
            "Count" => TranslateWindowAggregate(node, "COUNT"),
            "Min" => TranslateWindowAggregate(node, "MIN"),
            "Max" => TranslateWindowAggregate(node, "MAX"),

            // Regular SQL functions
            "Coalesce" => TranslateCoalesce(node),
            "IsNull" => TranslateIsNull(node),
            "NullIf" => TranslateNullIf(node),
            "Length" => TranslateLength(node),
            "Upper" => TranslateUpper(node),
            "Lower" => TranslateLower(node),
            "Trim" => TranslateTrim(node),
            "Substring" => TranslateSubstring(node),
            "Year" => TranslateYear(node),
            "Month" => TranslateMonth(node),
            "Day" => TranslateDay(node),

            _ => throw new NotSupportedException($"SQL function '{methodName}' is not supported in SELECT projections.")
        };
    }

    private string TranslateWindowFunction(MethodCallExpression node, string functionSql)
    {
        // Sql.RowNumber() returns WindowBuilder<long>, which should be followed by OVER clause methods
        // If called directly without chaining, generate just the function with empty OVER
        return functionSql + _dialect.GenerateOverClause(null, null);
    }

    private string TranslateNTile(MethodCallExpression node)
    {
        var buckets = (int)EvaluateExpression(node.Arguments[0])!;
        return _dialect.GenerateNTile(buckets) + _dialect.GenerateOverClause(null, null);
    }

    private string TranslateWindowAggregate(MethodCallExpression node, string function)
    {
        // Sql.Sum(p.Column) returns WindowAggregateBuilder - needs .Over() call to be window function
        // Without .Over(), this is just a marker and can't be used in projection
        string? columnExpr = null;

        if (node.Arguments.Count > 0)
        {
            var arg = node.Arguments[0];
            if (arg is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                arg = unary.Operand;

            if (arg is MemberExpression member && IsParameterMember(member))
            {
                var columnName = GetColumnName(member);
                columnExpr = _dialect.EscapeColumnName(columnName);
            }
        }

        var aggregateSql = _dialect.GenerateWindowAggregate(function, columnExpr);
        return aggregateSql + _dialect.GenerateOverClause(null, null);
    }

    private string TranslateWindowBuilderChain(MethodCallExpression node)
    {
        // Walk back through the method chain to collect PARTITION BY and ORDER BY
        var partitionBy = new List<string>();
        var orderBy = new List<(string column, bool descending)>();
        string? functionSql = null;
        bool isAggregate = false;
        string? aggregateColumn = null;

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
                    // This is WindowAggregateBuilder.Over() - continue to the Sql.* aggregate
                    current = methodCall.Object;
                    break;

                default:
                    // Check if this is a Sql.* function
                    if (declaringType == typeof(Sql))
                    {
                        switch (methodCall.Method.Name)
                        {
                            case "RowNumber":
                                functionSql = _dialect.GenerateRowNumber();
                                break;
                            case "Rank":
                                functionSql = _dialect.GenerateRank();
                                break;
                            case "DenseRank":
                                functionSql = _dialect.GenerateDenseRank();
                                break;
                            case "NTile":
                                functionSql = _dialect.GenerateNTile((int)EvaluateExpression(methodCall.Arguments[0])!);
                                break;
                            case "Sum":
                            case "Avg":
                            case "Min":
                            case "Max":
                                isAggregate = true;
                                if (methodCall.Arguments.Count > 0)
                                {
                                    var arg = methodCall.Arguments[0];
                                    if (arg is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                                        arg = unary.Operand;

                                    if (arg is MemberExpression member && IsParameterMember(member))
                                    {
                                        var colName = GetColumnName(member);
                                        aggregateColumn = _dialect.EscapeColumnName(colName);
                                    }
                                }
                                functionSql = _dialect.GenerateWindowAggregate(methodCall.Method.Name.ToUpperInvariant(), aggregateColumn);
                                break;
                            case "Count":
                                isAggregate = true;
                                functionSql = _dialect.GenerateWindowAggregate("COUNT", null);
                                break;
                            default:
                                throw new NotSupportedException($"Window function '{methodCall.Method.Name}' is not supported.");
                        }
                        current = null; // End of chain
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
            throw new InvalidOperationException("Window function chain must start with Sql.RowNumber(), Sql.Rank(), etc.");
        }

        var overClause = _dialect.GenerateOverClause(
            partitionBy.Count > 0 ? partitionBy.ToArray() : null,
            orderBy.Count > 0 ? orderBy.ToArray() : null);

        return functionSql + overClause;
    }

    private string TranslateWindowAggregateChain(MethodCallExpression node)
    {
        // Handle WindowAggregateBuilder.Over() and subsequent PartitionBy/OrderBy
        var partitionBy = new List<string>();
        var orderBy = new List<(string column, bool descending)>();
        string? aggregateSql = null;

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
                    // This is on WindowAggregateBuilder, walk to the Sql.* method
                    current = methodCall.Object;
                    break;

                default:
                    // Check if this is a Sql.* aggregate function
                    if (declaringType == typeof(Sql))
                    {
                        string? columnExpr = null;
                        if (methodCall.Arguments.Count > 0)
                        {
                            var arg = methodCall.Arguments[0];
                            if (arg is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                                arg = unary.Operand;

                            if (arg is MemberExpression member && IsParameterMember(member))
                            {
                                var colName = GetColumnName(member);
                                columnExpr = _dialect.EscapeColumnName(colName);
                            }
                        }

                        var function = methodCall.Method.Name.ToUpperInvariant();
                        aggregateSql = _dialect.GenerateWindowAggregate(function, columnExpr);
                        current = null;
                    }
                    else
                    {
                        throw new NotSupportedException($"Method '{methodName}' is not supported in window aggregate chain.");
                    }
                    break;
            }
        }

        if (aggregateSql == null)
        {
            throw new InvalidOperationException("Window aggregate chain must start with Sql.Sum(), Sql.Avg(), etc.");
        }

        var overClause = _dialect.GenerateOverClause(
            partitionBy.Count > 0 ? partitionBy.ToArray() : null,
            orderBy.Count > 0 ? orderBy.ToArray() : null);

        return aggregateSql + overClause;
    }

    private string TranslateColumnArgument(Expression arg)
    {
        if (arg is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            arg = unary.Operand;

        if (arg is MemberExpression member && IsParameterMember(member))
        {
            var columnName = GetColumnName(member);
            return _dialect.EscapeColumnName(columnName);
        }

        throw new NotSupportedException("Window function PARTITION BY and ORDER BY must reference entity properties.");
    }

    // Regular SQL function translations
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

/// <summary>
/// Represents a column in a SELECT projection with SQL and alias.
/// </summary>
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

using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Converts Expression{Func{T, bool}} predicates to SQL WHERE clauses.
/// Supports: ==, !=, &lt;, >, &lt;=, >=, &amp;&amp;, ||, Contains, StartsWith, EndsWith, null checks
/// </summary>
internal sealed class WhereExpressionVisitor<T> : ExpressionVisitor where T : new()
{
    private readonly ISqlDialect _dialect;
    private readonly StringBuilder _sql = new();
    private readonly List<(string Name, object? Value)> _parameters = new();
    private readonly Dictionary<string, int> _parameterCounts;

    /// <summary>
    /// Creates the visitor. <paramref name="parameterCounts"/>, when supplied, is shared across
    /// every WhereExpressionVisitor a query builder creates for the same logical query (one new
    /// instance per Where/And/Or call) so that filtering the same column more than once (e.g.
    /// <c>.Where(x => x.CategoryId == 1).Or(x => x.CategoryId == 2)</c>) produces distinct
    /// parameter names instead of two independently-numbered "@category_id" parameters that
    /// collide when merged into the query's shared <see cref="ParameterCollection"/>. Omit it
    /// (or pass null) for a standalone translation that doesn't need to coordinate names with
    /// any other visitor.
    /// </summary>
    public WhereExpressionVisitor(ISqlDialect dialect, Dictionary<string, int>? parameterCounts = null)
    {
        _dialect = dialect;
        _parameterCounts = parameterCounts ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<T, bool>> predicate)
    {
        _sql.Clear();
        _parameters.Clear();

        Visit(predicate.Body);

        return (_sql.ToString(), _parameters);
    }

    private string GetParameterName(string baseName)
    {
        var prefix = _dialect.ParameterPrefix;
        if (_parameterCounts.TryGetValue(baseName, out int count))
        {
            _parameterCounts[baseName] = count + 1;
            return $"{prefix}{baseName}{count + 1}";
        }
        else
        {
            _parameterCounts[baseName] = 1;
            return $"{prefix}{baseName}";
        }
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sql.Append('(');

        // Handle logical operators (&&, ||)
        if (node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            Visit(node.Left);
            _sql.Append(node.NodeType == ExpressionType.AndAlso ? " AND " : " OR ");
            Visit(node.Right);
            _sql.Append(')');
            return node;
        }

        // AUD-R12: a same-entity column-to-column comparison (e.g. p => p.CategoryId ==
        // p.SupplierId) has an unbound ParameterExpression on whichever side ExtractColumnAndValue
        // treats as "the value" - EvaluateExpression's Expression.Lambda(...).Compile() then
        // throws InvalidOperationException at runtime instead of producing "category_id =
        // supplier_id". Check both sides for a column reference first, mirroring how
        // JoinExpressionVisitor already handles cross-entity column comparisons.
        // R16: the same unbound-parameter crash also applies when one or both sides are a
        // string.Length comparison (e.g. p.Description.Length > p.MinLength, or
        // p.FirstName.Length > p.LastName.Length) - TryGetColumnOrLengthSql extends the check to
        // recognize those as renderable SQL expressions too, instead of falling through to
        // ExtractColumnAndValue's EvaluateExpression.
        if (TryGetColumnOrLengthSql(node.Left, out var leftSql) && TryGetColumnOrLengthSql(node.Right, out var rightSql))
        {
            _sql.Append(leftSql);
            _sql.Append(GetOperator(node.NodeType));
            _sql.Append(rightSql);
            _sql.Append(')');
            return node;
        }

        // Handle comparison operators
        (string? columnName, object? value, bool isLeftColumn) = ExtractColumnAndValue(node);

        if (columnName is null)
        {
            // Neither side is a plain column (e.g. a Sql.* function call on one side, such as
            // Sql.NullIf(...) != null). SQL's three-valued logic means "expr <> NULL"/"expr = NULL"
            // never matches (always UNKNOWN) even when expr is non-null, so a comparison against a
            // literal null must still become IS NULL/IS NOT NULL rather than a naive operator.
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            {
                if (IsNullConstant(node.Right))
                {
                    Visit(node.Left);
                    _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                    _sql.Append(')');
                    return node;
                }

                if (IsNullConstant(node.Left))
                {
                    Visit(node.Right);
                    _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                    _sql.Append(')');
                    return node;
                }
            }

            // Both sides are values or neither is a column - fall back to evaluating
            Visit(node.Left);
            _sql.Append(GetOperator(node.NodeType));
            Visit(node.Right);
            _sql.Append(')');
            return node;
        }

        var escapedColumn = _dialect.EscapeColumnName(columnName);

        // Handle null comparisons
        if (value is null)
        {
            _sql.Append(escapedColumn);
            _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
            _sql.Append(')');
            return node;
        }

        _sql.Append(escapedColumn);
        _sql.Append(GetOperator(isLeftColumn ? node.NodeType : MirrorOperator(node.NodeType)));
        var paramName = GetParameterName(columnName);
        _sql.Append(paramName);
        _parameters.Add((paramName, value));

        _sql.Append(')');
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Handle Sql.* functions (Coalesce, IsNull, NullIf)
        if (node.Method.DeclaringType == typeof(Sql))
        {
            return HandleSqlFunction(node);
        }

        // Handle CaseBuilder method calls (Else, End)
        if (node.Method.DeclaringType != null &&
            node.Method.DeclaringType.IsGenericType &&
            node.Method.DeclaringType.GetGenericTypeDefinition() == typeof(CaseBuilder<,>))
        {
            if (node.Method.Name is "Else" or "End")
            {
                return HandleCaseExpression(node);
            }
        }

        // Handle string methods: Contains, StartsWith, EndsWith, ToUpper, ToLower, Trim, Substring
        if (node.Object is not null && node.Object.Type == typeof(string))
        {
            var memberExpr = node.Object as MemberExpression;
            if (memberExpr is not null && IsParameterMember(memberExpr))
            {
                var columnName = GetColumnName(memberExpr);
                var escapedColumn = _dialect.EscapeColumnName(columnName);

                switch (node.Method.Name)
                {
                    case "Contains":
                        {
                            var value = EvaluateExpression(node.Arguments[0]);
                            var paramName = GetParameterName(columnName);
                            _sql.Append(_dialect.GenerateCaseSensitiveLike(escapedColumn, paramName, "\\"));
                            _parameters.Add((paramName, _dialect.FormatContainsPattern(value?.ToString() ?? "")));
                            return node;
                        }
                    case "StartsWith":
                        {
                            var value = EvaluateExpression(node.Arguments[0]);
                            var paramName = GetParameterName(columnName);
                            _sql.Append(_dialect.GenerateCaseSensitiveLike(escapedColumn, paramName, "\\"));
                            _parameters.Add((paramName, _dialect.FormatStartsWithPattern(value?.ToString() ?? "")));
                            return node;
                        }
                    case "EndsWith":
                        {
                            var value = EvaluateExpression(node.Arguments[0]);
                            var paramName = GetParameterName(columnName);
                            _sql.Append(_dialect.GenerateCaseSensitiveLike(escapedColumn, paramName, "\\"));
                            _parameters.Add((paramName, _dialect.FormatEndsWithPattern(value?.ToString() ?? "")));
                            return node;
                        }
                    case "Equals" when node.Arguments.Count >= 1:
                        {
                            var value = EvaluateExpression(node.Arguments[0]);
                            return HandleStringEquals(node, escapedColumn, columnName, value);
                        }
                    case "ToUpper":
                        _sql.Append(_dialect.GenerateUpper(escapedColumn));
                        return node;
                    case "ToLower":
                        _sql.Append(_dialect.GenerateLower(escapedColumn));
                        return node;
                    case "Trim":
                        _sql.Append(_dialect.GenerateTrim(escapedColumn));
                        return node;
                    case "Substring":
                        {
                            var startIndex = EvaluateExpression(node.Arguments[0]);
                            // SQL SUBSTRING is 1-based, C# is 0-based
                            var sqlStart = Convert.ToInt32(startIndex) + 1;
                            if (node.Arguments.Count >= 2)
                            {
                                var length = EvaluateExpression(node.Arguments[1]);
                                _sql.Append(_dialect.GenerateSubstring(escapedColumn, sqlStart.ToString(), length?.ToString() ?? "1"));
                            }
                            else
                            {
                                // No length specified - use large number for "rest of string"
                                _sql.Append(_dialect.GenerateSubstring(escapedColumn, sqlStart.ToString(), "8000"));
                            }
                            return node;
                        }
                }
            }
        }

        // Handle Enumerable.Contains for IN clauses (e.g., ids.Contains(p.Id)) as well as
        // instance Contains on List<T>/HashSet<T>/other ICollection<T> implementations, which
        // bind to the type's own Contains method rather than the Enumerable extension method.
        bool isEnumerableContains = node.Method.Name == "Contains" && node.Method.DeclaringType == typeof(Enumerable);
        bool isInstanceContains = node.Method.Name == "Contains"
            && node.Object is not null
            && node.Object.Type != typeof(string)
            && node.Arguments.Count == 1
            && typeof(System.Collections.IEnumerable).IsAssignableFrom(node.Object.Type);

        if (isEnumerableContains || isInstanceContains)
        {
            var collection = EvaluateExpression(isEnumerableContains ? node.Arguments[0] : node.Object!);
            var memberExpr = (isEnumerableContains ? node.Arguments[1] : node.Arguments[0]) as MemberExpression;

            if (memberExpr is not null && IsParameterMember(memberExpr) && collection is System.Collections.IEnumerable enumerable)
            {
                var columnName = GetColumnName(memberExpr);
                var escapedColumn = _dialect.EscapeColumnName(columnName);

                var values = enumerable.Cast<object>().ToList();
                if (values.Count == 0)
                {
                    _sql.Append("1 = 0"); // Empty collection always false
                    return node;
                }

                _sql.Append(escapedColumn);
                _sql.Append(" IN (");
                for (int i = 0; i < values.Count; i++)
                {
                    if (i > 0) _sql.Append(", ");
                    var paramName = GetParameterName($"{columnName}_{i}");
                    _sql.Append(paramName);
                    _parameters.Add((paramName, values[i]));
                }
                _sql.Append(')');
                return node;
            }
        }

        // Fallback: evaluate and use as constant
        var result = EvaluateExpression(node);
        if (result is bool boolResult)
        {
            _sql.Append(boolResult ? "1 = 1" : "1 = 0");
        }
        else
        {
            var paramName = GetParameterName("Value");
            _sql.Append(paramName);
            _parameters.Add((paramName, result));
        }
        return node;
    }

    private Expression HandleSqlFunction(MethodCallExpression node)
    {
        var methodName = node.Method.Name;

        switch (methodName)
        {
            case "Coalesce":
                return HandleCoalesce(node);
            case "IsNull":
                return HandleIsNull(node);
            case "NullIf":
                return HandleNullIf(node);
            // String functions
            case "Length":
                return HandleLength(node);
            case "Upper":
                return HandleUpper(node);
            case "Lower":
                return HandleLower(node);
            case "Trim":
                return HandleTrim(node);
            case "Substring":
                return HandleSubstring(node);
            // Date functions
            case "Year":
                return HandleYear(node);
            case "Month":
                return HandleMonth(node);
            case "Day":
                return HandleDay(node);
            // CASE expression (shouldn't reach here - handled in VisitMethodCall)
            case "Case":
                throw new NotSupportedException("Sql.Case() must be followed by .When() and .Else() or .End()");
            default:
                throw new NotSupportedException($"SQL function '{methodName}' is not supported.");
        }
    }

    private Expression HandleCoalesce(MethodCallExpression node)
    {
        var arguments = new List<string>();


        foreach (Expression? arg in node.Arguments)
        {
            arguments.Add(TranslateArgumentToSql(arg));
        }

        _sql.Append(_dialect.GenerateCoalesce(arguments.ToArray()));
        return node;
    }

    private Expression HandleIsNull(MethodCallExpression node)
    {
        var valueArg = TranslateArgumentToSql(node.Arguments[0]);
        var defaultArg = TranslateArgumentToSql(node.Arguments[1]);

        _sql.Append(_dialect.GenerateIsNull(valueArg, defaultArg));
        return node;
    }

    private Expression HandleNullIf(MethodCallExpression node)
    {
        var valueArg = TranslateArgumentToSql(node.Arguments[0]);
        var compareArg = TranslateArgumentToSql(node.Arguments[1]);

        _sql.Append(_dialect.GenerateNullIf(valueArg, compareArg));
        return node;
    }

    // String function handlers
    private Expression HandleLength(MethodCallExpression node)
    {
        var arg = TranslateArgumentToSql(node.Arguments[0]);
        _sql.Append(_dialect.GenerateLength(arg));
        return node;
    }

    private Expression HandleUpper(MethodCallExpression node)
    {
        var arg = TranslateArgumentToSql(node.Arguments[0]);
        _sql.Append(_dialect.GenerateUpper(arg));
        return node;
    }

    private Expression HandleLower(MethodCallExpression node)
    {
        var arg = TranslateArgumentToSql(node.Arguments[0]);
        _sql.Append(_dialect.GenerateLower(arg));
        return node;
    }

    private Expression HandleTrim(MethodCallExpression node)
    {
        var arg = TranslateArgumentToSql(node.Arguments[0]);
        _sql.Append(_dialect.GenerateTrim(arg));
        return node;
    }

    private Expression HandleSubstring(MethodCallExpression node)
    {
        var strArg = TranslateArgumentToSql(node.Arguments[0]);
        var startArg = TranslateArgumentToSql(node.Arguments[1]);
        var lengthArg = TranslateArgumentToSql(node.Arguments[2]);
        _sql.Append(_dialect.GenerateSubstring(strArg, startArg, lengthArg));
        return node;
    }

    // Date function handlers
    private Expression HandleYear(MethodCallExpression node)
    {
        var arg = TranslateArgumentToSql(node.Arguments[0]);
        _sql.Append(_dialect.GenerateYear(arg));
        return node;
    }

    private Expression HandleMonth(MethodCallExpression node)
    {
        var arg = TranslateArgumentToSql(node.Arguments[0]);
        _sql.Append(_dialect.GenerateMonth(arg));
        return node;
    }

    private Expression HandleDay(MethodCallExpression node)
    {
        var arg = TranslateArgumentToSql(node.Arguments[0]);
        _sql.Append(_dialect.GenerateDay(arg));
        return node;
    }

    // CASE expression handler
    private Expression HandleCaseExpression(MethodCallExpression node)
    {
        // Collect WHEN clauses by walking back through the method chain
        var whenClauses = new List<(Expression Condition, Expression Result)>();
        Expression? elseResult = null;

        // If this is .Else(), capture the else value
        if (node.Method.Name == "Else")
        {
            elseResult = node.Arguments[0];
        }

        // Walk back through the chain to collect When clauses
        Expression? current = node.Object;
        while (current is MethodCallExpression methodCall)
        {
            if (methodCall.Method.Name == "When")
            {
                // When(condition, result) - arguments[0] is condition, arguments[1] is result
                Expression condition = methodCall.Arguments[0];
                if (condition is LambdaExpression lambda)
                {
                    condition = lambda.Body;
                }
                whenClauses.Insert(0, (condition, methodCall.Arguments[1]));
                current = methodCall.Object;
            }
            else if (methodCall.Method.DeclaringType == typeof(Sql) && methodCall.Method.Name == "Case")
            {
                // Reached Sql.Case<T1, T2>() - end of chain
                break;
            }
            else
            {
                // Unknown method in chain
                throw new NotSupportedException($"Unexpected method '{methodCall.Method.Name}' in CASE expression chain.");
            }
        }

        if (whenClauses.Count == 0)
        {
            throw new InvalidOperationException("CASE expression requires at least one WHEN clause.");
        }

        // Build the CASE SQL
        _sql.Append("CASE");

        foreach ((Expression? condition, Expression? result) in whenClauses)
        {
            _sql.Append(" WHEN ");
            // Translate the condition expression to SQL
            TranslateCaseCondition(condition);
            _sql.Append(" THEN ");
            // Translate the result to SQL (may be constant or column)
            _sql.Append(TranslateArgumentToSql(result));
        }

        if (elseResult != null)
        {
            _sql.Append(" ELSE ");
            _sql.Append(TranslateArgumentToSql(elseResult));
        }

        _sql.Append(" END");

        return node;
    }

    private void TranslateCaseCondition(Expression condition)
    {
        // The condition might be a binary expression (e.g., p.Price < 10)
        // We need to translate it without wrapping in parentheses for cleaner SQL
        if (condition is BinaryExpression binary)
        {
            // Handle comparison operators
            (string? columnName, object? value, bool isLeftColumn) = ExtractColumnAndValue(binary);

            if (columnName != null)
            {
                var escapedColumn = _dialect.EscapeColumnName(columnName);
                _sql.Append(escapedColumn);
                _sql.Append(GetOperator(isLeftColumn ? binary.NodeType : MirrorOperator(binary.NodeType)));
                var paramName = GetParameterName(columnName);
                _sql.Append(paramName);
                _parameters.Add((paramName, value));
            }
            else
            {
                // Both sides might be columns or complex expressions
                Visit(binary.Left);
                _sql.Append(GetOperator(binary.NodeType));
                Visit(binary.Right);
            }
        }
        else if (condition is MethodCallExpression methodCall)
        {
            // Handle method calls in conditions (e.g., Sql.Length(p.Name) > 10)
            Visit(methodCall);
        }
        else if (condition is MemberExpression member)
        {
            // Handle boolean member access (e.g., p.IsActive)
            if (IsParameterMember(member) && member.Type == typeof(bool))
            {
                var columnName = GetColumnName(member);
                var escapedColumn = _dialect.EscapeColumnName(columnName);
                _sql.Append(escapedColumn);
                _sql.Append(" = 1");
            }
            else
            {
                Visit(member);
            }
        }
        else
        {
            // Fallback - try to evaluate and use as constant
            var value = EvaluateExpression(condition);
            if (value is bool boolValue)
            {
                _sql.Append(boolValue ? "1 = 1" : "1 = 0");
            }
            else
            {
                Visit(condition);
            }
        }
    }

    private string TranslateArgumentToSql(Expression arg)
    {
        // Unwrap Convert expression
        if (arg is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            arg = unary.Operand;

        // If it's a member access on the parameter, translate to column
        if (arg is MemberExpression member && IsParameterMember(member))
        {
            var columnName = GetColumnName(member);
            return _dialect.EscapeColumnName(columnName);
        }

        // Otherwise, evaluate and create a parameter
        var value = EvaluateExpression(arg);
        var paramName = GetParameterName("SqlFn");
        _parameters.Add((paramName, value));
        return paramName;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _sql.Append("NOT (");
            Visit(node.Operand);
            _sql.Append(')');
            return node;
        }

        if (node.NodeType == ExpressionType.Convert)
        {
            Visit(node.Operand);
            return node;
        }

        return base.VisitUnary(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Handle string.Length property (e.g., p.ProductName.Length > 10)
        if (node.Member.Name == "Length" && node.Expression is MemberExpression innerMember
            && innerMember.Type == typeof(string) && IsParameterMember(innerMember))
        {
            var columnName = GetColumnName(innerMember);
            var escapedColumn = _dialect.EscapeColumnName(columnName);
            _sql.Append(_dialect.GenerateLength(escapedColumn));
            return node;
        }

        // Handle boolean properties directly (e.g., p => p.IsActive)
        if (IsParameterMember(node) && node.Type == typeof(bool))
        {
            var columnName = GetColumnName(node);
            var escapedColumn = _dialect.EscapeColumnName(columnName);
            _sql.Append(escapedColumn);
            _sql.Append(" = 1");
            return node;
        }

        // If this is a member access on the parameter, it's a column reference
        if (IsParameterMember(node))
        {
            var columnName = GetColumnName(node);
            _sql.Append(_dialect.EscapeColumnName(columnName));
            return node;
        }

        // Otherwise, evaluate as a constant
        var value = EvaluateExpression(node);
        var paramName = GetParameterName("Value");
        _sql.Append(paramName);
        _parameters.Add((paramName, value));
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        switch (node.Value)
        {
            case null:
                _sql.Append("NULL");
                break;
            case bool boolValue:
                _sql.Append(boolValue ? "1 = 1" : "1 = 0");
                break;
            default:
                {
                    var paramName = GetParameterName("Value");
                    _sql.Append(paramName);
                    _parameters.Add((paramName, node.Value));
                    break;
                }
        }
        return node;
    }

    private (string? ColumnName, object? Value, bool IsLeftColumn) ExtractColumnAndValue(BinaryExpression node)
    {
        // Try left as column
        if (TryGetColumnName(node.Left, out var leftColumn))
        {
            var rightValue = EvaluateExpression(node.Right);
            return (leftColumn, rightValue, true);
        }

        // Try right as column
        if (TryGetColumnName(node.Right, out var rightColumn))
        {
            var leftValue = EvaluateExpression(node.Left);
            return (rightColumn, leftValue, false);
        }

        return (null, null, false);
    }

    private static bool IsNullConstant(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        return expression is ConstantExpression { Value: null };
    }

    private bool TryGetColumnName(Expression expression, out string? columnName)
    {
        columnName = null;

        // Unwrap Convert
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is MemberExpression member && IsParameterMember(member))
        {
            // Exclude string.Length - it should be handled as LENGTH() function, not a column
            if (member.Member.Name == "Length" && member.Expression is MemberExpression innerMember
                && innerMember.Type == typeof(string))
            {
                return false;
            }

            columnName = GetColumnName(member);
            return true;
        }

        return false;
    }

    // R16: treats both a plain column reference and a string.Length member (e.g. p.Name.Length)
    // as a renderable SQL expression, so VisitBinary can compare either against another column or
    // Length expression without routing through EvaluateExpression's Expression.Lambda(...).Compile(),
    // which throws when the expression still references the unbound lambda parameter.
    private bool TryGetColumnOrLengthSql(Expression expression, out string? sql)
    {
        sql = null;

        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is MemberExpression { Member.Name: "Length" } lengthMember
            && lengthMember.Expression is MemberExpression innerMember
            && innerMember.Type == typeof(string) && IsParameterMember(innerMember))
        {
            var innerColumn = GetColumnName(innerMember);
            sql = _dialect.GenerateLength(_dialect.EscapeColumnName(innerColumn));
            return true;
        }

        if (TryGetColumnName(expression, out var columnName))
        {
            sql = _dialect.EscapeColumnName(columnName!);
            return true;
        }

        return false;
    }

    private bool IsParameterMember(MemberExpression member)
    {
        // Walk up the chain to find the parameter
        Expression? current = member;
        while (current is MemberExpression me)
            current = me.Expression;

        return current is ParameterExpression;
    }

    private string GetColumnName(MemberExpression member)
    {
        var propertyName = member.Member.Name;

        // Look up the actual column name from metadata
        EntityMetadata metadata = FluentMetadataCache.GetMetadata<T>();

        ColumnMetadata? column = metadata.Columns.FirstOrDefault(c => c.PropertyName == propertyName);

        return column?.ColumnName ?? propertyName;
    }

    private static object? EvaluateExpression(Expression expression)
    {
        // Handle constants directly
        if (expression is ConstantExpression constant)
            return constant.Value;

        // Compile and execute
        LambdaExpression lambda = Expression.Lambda(expression);
        Delegate compiled = lambda.Compile();
        return compiled.DynamicInvoke();
    }

    private static string GetOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => " = ",
        ExpressionType.NotEqual => " <> ",
        ExpressionType.LessThan => " < ",
        ExpressionType.LessThanOrEqual => " <= ",
        ExpressionType.GreaterThan => " > ",
        ExpressionType.GreaterThanOrEqual => " >= ",
        _ => throw new NotSupportedException($"Operator {nodeType} is not supported in WHERE expressions.")
    };

    private static ExpressionType MirrorOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.LessThan => ExpressionType.GreaterThan,
        ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
        ExpressionType.GreaterThan => ExpressionType.LessThan,
        ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
        _ => nodeType
    };

    private Expression HandleStringEquals(MethodCallExpression node, string escapedColumn, string columnName, object? value)
    {
        var paramName = GetParameterName(columnName);
        var stringValue = value?.ToString() ?? "";

        bool isCaseInsensitive = false;
        if (node.Arguments.Count >= 2)
        {
            var comparisonArg = EvaluateExpression(node.Arguments[1]);
            if (comparisonArg is StringComparison comparison)
            {
                isCaseInsensitive = comparison is StringComparison.OrdinalIgnoreCase
                    or StringComparison.CurrentCultureIgnoreCase
                    or StringComparison.InvariantCultureIgnoreCase;
            }
        }

        if (isCaseInsensitive)
        {
            _sql.Append(_dialect.GenerateCaseInsensitiveEquals(escapedColumn, paramName));
        }
        else
        {
            _sql.Append(escapedColumn);
            _sql.Append(" = ");
            _sql.Append(paramName);
        }

        _parameters.Add((paramName, stringValue));
        return node;
    }
}
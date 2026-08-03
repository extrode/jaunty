using System.Linq.Expressions;
using System.Reflection;

using Jaunty.Dialects;
using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Converts SELECT projection expressions to SQL column lists.
/// Supports: entity properties, anonymous types, window functions, SQL functions.
/// </summary>
internal sealed class SelectExpressionVisitor<T> : ExpressionVisitor where T : new()
{
    private readonly ISqlDialect _dialect;
    private readonly List<SelectColumn> _columns = new();

    public SelectExpressionVisitor(ISqlDialect dialect)
    {
        _dialect = dialect;
    }

    /// <summary>
    /// Translates a projection expression to a list of SELECT columns with SQL.
    /// </summary>
    public List<SelectColumn> Translate<TResult>(Expression<Func<T, TResult>> selector)
    {
        _columns.Clear();

        Expression body = selector.Body;

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
            MemberInfo member = node.Members[i];
            Expression argument = node.Arguments[i];
            var alias = member.Name;

            var sql = TranslateProjectionExpression(argument);
            _columns.Add(new SelectColumn(sql, alias));
        }

        return node;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        // Handle object initializer: new ProductDto { Name = p.ProductName, ... }

        foreach (MemberBinding? binding in node.Bindings)
        {
            // AUD-R35-196: a MemberMemberBinding (`p => new Dto { Nested = { X = p.A } }`) or a
            // MemberListBinding (`p => new Dto { Items = { p.A } }`) - both legal in a C# expression
            // tree - used to be skipped in silence, producing a projection with a column missing and
            // no way to tell. The same gap was closed in the GROUP BY visitors as AUD-R35-195. Every
            // other untranslatable shape in this class throws and names what it saw.
            if (binding is not MemberAssignment assignment)
            {
                throw new NotSupportedException(
                    $"Member binding '{binding.BindingType}' is not supported in SELECT projections. " +
                    "Only member assignments (Member = expression) can be translated.");
            }

            var alias = assignment.Member.Name;
            var sql = TranslateProjectionExpression(assignment.Expression);
            _columns.Add(new SelectColumn(sql, alias));
        }

        return node;
    }

    /// <summary>
    /// AUD-R35-197. Unreachable as the class stands: <c>Translate</c>'s top-level switch names
    /// <see cref="MemberExpression"/> and routes it through <c>TranslateProjectionExpression</c>
    /// before <c>Visit</c> is ever called, <c>VisitNew</c>/<c>VisitMemberInit</c> translate their
    /// children the same way rather than visiting them, and every other override throws. Its last
    /// caller was the <c>NewArrayInit</c> hole AUD-R34-018 closed. It is kept rather than deleted
    /// so that a member reaching <c>Visit</c> again has a defined outcome, but the branch that used
    /// to return <c>node</c> having appended nothing - the silent-drop pattern the last three
    /// rounds have been removing - now throws instead.
    /// </summary>
    protected override Expression VisitMember(MemberExpression node)
    {
        // Single member selection: p => p.ProductName
        if (IsParameterMember(node))
        {
            var escapedColumn = GetEscapedColumnName(node);
            _columns.Add(new SelectColumn(escapedColumn, node.Member.Name));
            return node;
        }

        throw new NotSupportedException(
            $"'{node.Member.Name}' is not a property of the entity being projected. " +
            "Only entity properties and Sql.* functions can be translated in SELECT projections.");
    }

    /// <summary>
    /// AUD-R32-002. A ternary is a <see cref="ConditionalExpression"/>, which <c>Translate</c>'s
    /// top-level switch does not name, so it used to fall through to the base visitor's default
    /// traversal: Test/IfTrue/IfFalse were each walked independently and any member access inside
    /// them landed in <c>_columns</c> as a column of its own, producing a SELECT list that has
    /// nothing to do with the ternary. Every other untranslatable shape in this class throws;
    /// this one silently returned wrong SQL. If CASE WHEN support is ever wanted, this override
    /// is where it goes - see <c>Sql.Case</c> for the supported spelling.
    /// </summary>
    protected override Expression VisitConditional(ConditionalExpression node)
        => throw new NotSupportedException(
            "Conditional (ternary) expressions are not supported in SELECT projections. " +
            "Use Sql.Case(...) for a CASE WHEN, or project the operands and branch in memory.");

    /// <summary>
    /// AUD-R33-010, the SELECT half. <c>Translate</c> hands anything that is not a method call,
    /// member access, constant, unary or binary expression to <c>Visit</c>, and the only overrides
    /// on that path were <see cref="VisitNew"/>, <see cref="VisitMemberInit"/> and
    /// <see cref="VisitConditional"/> - so a projection body of any other shape descended into its
    /// children, added nothing to <c>_columns</c> for the wrapper node, and produced a silently
    /// empty or partial SELECT list instead of a translation error.
    /// </summary>
    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        => throw new NotSupportedException(
            "Type tests ('is', 'as') are not supported in SELECT projections. There is no SQL " +
            "equivalent of a CLR type test; project a discriminator column instead.");

    /// <summary>
    /// AUD-R34-018. The one shape AUD-R33-010's sweep missed: <c>p =&gt; new[] { p.A, p.B }</c> is a
    /// <c>NewArrayInit</c>, which <c>Translate</c>'s switch does not name, so it descended into its
    /// children and each element landed in <c>_columns</c> on its own - the projection silently
    /// became <c>SELECT a, b</c>, and any operator inside the initializer was dropped with it. The
    /// WHERE twin already threw here.
    /// </summary>
    protected override Expression VisitNewArray(NewArrayExpression node)
        => throw new NotSupportedException(
            "Array construction is not supported in SELECT projections. List the columns directly, " +
            "or project into an anonymous type and build the array from the results.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitListInit(ListInitExpression node)
        => throw new NotSupportedException(
            "Collection initializers are not supported in SELECT projections. Project the columns " +
            "and build the collection from the results.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitInvocation(InvocationExpression node)
        => throw new NotSupportedException(
            "Invoking a delegate or a nested lambda is not supported in SELECT projections. " +
            "Inline the projection, or apply the delegate to the results.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitIndex(IndexExpression node)
        => throw new NotSupportedException(
            "Indexer access is not supported in SELECT projections. Project the column and index " +
            "the result.");

    /// <summary>
    /// A bare parameter (<c>x =&gt; x</c>) is not a column list, and used to translate to one
    /// silently - the base visitor returns the node and nothing is appended, so the projection came
    /// back empty. AUD-R33-010.
    /// </summary>
    protected override Expression VisitParameter(ParameterExpression node)
        => throw new NotSupportedException(
            $"'{node.Name}' is the whole entity, not a projection. Select the columns you want, or " +
            "run the query without a Select to get the entity.");

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
            return GetEscapedColumnName(member);
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
        Type? declaringType = node.Method.DeclaringType;

        // Handle Sql.* static methods
        if (declaringType == typeof(Sql))
        {
            return TranslateSqlFunction(node);
        }

        // Handle WindowBuilder / WindowAggregateBuilder method chains
        if (declaringType != null && declaringType.IsGenericType)
        {
            Type genericDef = declaringType.GetGenericTypeDefinition();

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

            Type? declaringType = methodCall.Method.DeclaringType;

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
            return GetEscapedColumnName(member);
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

    // AUD-R25: this used to run metadata.Columns.FirstOrDefault(c => c.PropertyName == ...) - a
    // LINQ delegate allocation plus an O(columns) linear scan - and hand the result to
    // _dialect.EscapeColumnName, which re-runs SqlIdentifierValidator's regex match and a keyword
    // HashSet lookup, both per column reference per query build. CachedDialectMetadata holds an
    // OrdinalIgnoreCase dictionary of property name to already-escaped column name, built once per
    // (entity, dialect) pair; QueryBuilder, CteBuilder and InsertBuilder already used it.
    // AUD-R35-019: the SELECT twin of the WHERE guard AUD-R34-021 added. Without it
    // .Select(o => new { o.OrderDate.Year }) projected a column called [Year].
    private string GetEscapedColumnName(MemberExpression member)
    {
        ColumnReference.RequireDirect(member);
        return FluentMetadataCache.GetForDialect<T>(_dialect).GetColumnName(member.Member.Name);
    }

    // AUD-R25: this was one of eight byte-identical private copies. Kept as a one-line forwarder
    // rather than rewriting every call site, so the shared implementation - including its
    // closure-member fast path - is the only place the behaviour lives.
    private static object? EvaluateExpression(Expression expression) => ExpressionEvaluator.Evaluate(expression);

    private string FormatConstant(object? value) => HavingExpressionHelpers.FormatLiteral(value, _dialect);
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
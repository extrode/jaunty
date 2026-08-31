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

    // AUD-R26-058: parallel to _metadata, same indices. This class is non-generic by design - one
    // implementation serves arities 2, 3 and 4 - so it cannot call FluentMetadataCache.GetForDialect<T>
    // itself the way the other converted sites do. Its three callers are all generic on their entity
    // types and already build the _metadata array, so they build this one alongside it.
    private readonly CachedDialectMetadata[] _cachedMetadata;
    private readonly string[] _tablePrefixes;
    private readonly string[] _groupByColumns;
    private readonly Dictionary<string, string> _keyPropertyToColumn;
    private readonly List<string> _selectColumns = new();
    private readonly List<string> _columnAliases = new();
    private List<(string Name, object? Value)> _havingParameters = new();
    private int _havingParamSeq;

    /// <param name="dialect">The SQL dialect, for column escaping.</param>
    /// <param name="metadata">Entity metadata, ordered index 0 = TFrom, index 1 = TJoin, etc.</param>
    /// <param name="cachedMetadata">
    /// Pre-escaped per-dialect metadata, parallel to <paramref name="metadata"/> and in the same
    /// order. Supplied by the caller because this class is non-generic - one implementation serves
    /// arities 2, 3 and 4 - so it cannot resolve the cache entries itself, while all three callers
    /// are generic on their entity types and already build the metadata array (AUD-R26-058).
    /// </param>
    /// <param name="tablePrefixes">Table alias (or table name, if unaliased) per joined
    /// entity, same order as <paramref name="metadata"/> - every generated column reference
    /// is qualified with it, since two joined tables can share a column name (e.g. both
    /// having an "id" or shared FK column) and an unqualified GROUP BY/SELECT reference to it
    /// is ambiguous and fails at execution, unlike the single-entity case where it can't be.</param>
    /// <param name="keySelector">The GROUP BY key selector.</param>
    public JoinedGroupByExpressionVisitor(ISqlDialect dialect, EntityMetadata[] metadata, CachedDialectMetadata[] cachedMetadata, string[] tablePrefixes, LambdaExpression keySelector)
    {
        _dialect = dialect;
        _metadata = metadata;
        _cachedMetadata = cachedMetadata;
        _tablePrefixes = tablePrefixes;
        (_groupByColumns, _keyPropertyToColumn) = ExtractGroupByColumns(keySelector);
    }

    public string[] GroupByColumns => _groupByColumns;

    /// <summary>
    /// Translates a HAVING predicate (its single parameter is the IGroupingJoined{,3,4}
    /// instance) to a SQL boolean expression plus the query parameters its comparison operands
    /// were bound to (matching how <c>GroupedQueryBuilder.AddHavingParameter</c> parameterizes
    /// the single-entity HAVING path instead of inlining literal text). Reuses
    /// <see cref="HavingExpressionHelpers"/> (the same closure-safety fix single-entity HAVING
    /// uses) and the same aggregate-column resolution as <see cref="TranslateSelect"/>. The
    /// caller must add the returned parameters to its own parameter collection before binding
    /// the command.
    /// </summary>
    public (string Sql, List<(string Name, object? Value)> Parameters) TranslateHavingPredicate(LambdaExpression predicate)
    {
        _havingParameters = new List<(string, object?)>();
        string sql = TranslateHavingExpression(predicate.Body);
        return (sql, _havingParameters);
    }

    /// <summary>
    /// Recursively translates a HAVING predicate. Top-level and nested AndAlso/OrElse
    /// combinators (e.g. <c>g => g.Count() > 5 &amp;&amp; g.Sum(...) > 100</c>) are handled by
    /// translating both operands and joining them with the mapped SQL operator; comparison
    /// operators bottom out in <see cref="TranslateHavingOperand"/> for each side. Mirrors
    /// <c>GroupedQueryBuilder.TranslateHavingExpression</c>'s structure for the single-entity
    /// case.
    /// </summary>
    private string TranslateHavingExpression(Expression expr)
    {
        if (expr is UnaryExpression convert && convert.NodeType == ExpressionType.Convert)
            expr = convert.Operand;

        if (expr is BinaryExpression binary)
        {
            string op = GetSqlOperator(binary.NodeType);

            if (binary.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
            {
                string left = TranslateHavingExpression(binary.Left);
                string right = TranslateHavingExpression(binary.Right);
                return $"({left} {op} {right})";
            }

            string leftOperand = TranslateHavingOperand(binary.Left);
            string rightOperand = TranslateHavingOperand(binary.Right);
            return $"{leftOperand} {op} {rightOperand}";
        }

        return TranslateHavingOperand(expr);
    }

    /// <summary>
    /// Translates one side of a HAVING comparison: an aggregate method call (COUNT/SUM/...), or
    /// a value (literal constant, captured local, or method parameter) which is bound as a
    /// query parameter rather than being inlined into the SQL text.
    /// </summary>
    private string TranslateHavingOperand(Expression expr)
    {
        if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expr = unary.Operand;

        // g.Count() > 5, g.Sum((t1,t2) => t1.Price) > total, etc.
        if (expr is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;

            return methodName switch
            {
                "Count" when methodCall.Arguments.Count == 0 => "COUNT(*)",
                "Count" => BuildAggregateWithColumn("COUNT", methodCall.Arguments[0]),
                "Sum" => BuildAggregateWithColumn("SUM", methodCall.Arguments[0]),
                "Avg" => BuildAggregateWithColumn("AVG", methodCall.Arguments[0]),
                "Min" => BuildAggregateWithColumn("MIN", methodCall.Arguments[0]),
                "Max" => BuildAggregateWithColumn("MAX", methodCall.Arguments[0]),
                _ => throw new NotSupportedException($"Method '{methodName}' is not supported in HAVING.")
            };
        }

        if (expr is ConstantExpression constant)
        {
            return AddHavingParameter(constant.Value);
        }

        // Captured local variables, method parameters, and other closed-over values compile
        // to a MemberExpression over a compiler-generated closure class, not a
        // ConstantExpression - evaluate it (gap #14's closure-safety fix).
        if (expr is MemberExpression or UnaryExpression)
        {
            return AddHavingParameter(HavingExpressionHelpers.EvaluateExpression(expr));
        }

        throw new NotSupportedException($"HAVING expression type '{expr.NodeType}' is not supported.");
    }

    /// <summary>
    /// Adds a HAVING comparison operand as a bound query parameter and returns its placeholder
    /// name, instead of inlining it into the SQL text - matches
    /// <c>GroupedQueryBuilder.AddHavingParameter</c>'s fix for the same anti-pattern.
    /// </summary>
    private string AddHavingParameter(object? value)
    {
        string name = $"{_dialect.ParameterPrefix}jhp{_havingParamSeq++}";
        _havingParameters.Add((name, value));
        return name;
    }

    private static string GetSqlOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => "=",
        ExpressionType.NotEqual => "<>",
        ExpressionType.GreaterThan => ">",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThan => "<",
        ExpressionType.LessThanOrEqual => "<=",
        ExpressionType.AndAlso => "AND",
        ExpressionType.OrElse => "OR",
        _ => throw new NotSupportedException($"Operator '{nodeType}' is not supported.")
    };

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
        else if (IsBareKeyAccess(body, groupingParam) && _groupByColumns.Length > 1)
        {
            // Bare `g => g.Key` over a composite grouping key (e.g. `(t1,t2) => new { t1.A, t2.B }`).
            // Emit every GROUP BY column instead of silently dropping all but the first - mirrors
            // GroupByExpressionVisitor<T,TKey>.TranslateSelect's fix for the same single-entity gap;
            // that fix was never ported to this joined visitor.
            string[] aliases = GetCompositeKeyAliases();

            for (int i = 0; i < _groupByColumns.Length; i++)
            {
                _selectColumns.Add($"{_groupByColumns[i]} AS {_dialect.EscapeColumnName(aliases[i])}");
                _columnAliases.Add(aliases[i]);
            }
        }
        else
        {
            // AUD-R34-005: same omission as the single-entity visitor's else branch - the alias was
            // reported but not emitted, so a bare g.Key / g.Count() projection mapped to 0 (value
            // types) or threw IndexOutOfRangeException (constructor-bound types like string).
            (string sql, string alias) = TranslateExpression(body, "Value", groupingParam);
            _selectColumns.Add($"{sql} AS {_dialect.EscapeColumnName(alias)}");
            _columnAliases.Add(alias);
        }

        return (_selectColumns.ToArray(), _columnAliases.ToArray());
    }

    private static bool IsBareKeyAccess(Expression expr, ParameterExpression groupingParam) =>
        expr is MemberExpression keyMember && keyMember.Member.Name == "Key" && IsGroupingAccess(keyMember.Expression, groupingParam);

    private string[] GetCompositeKeyAliases()
    {
        var aliases = new string[_groupByColumns.Length];
        for (int i = 0; i < aliases.Length; i++)
            aliases[i] = $"Key{i}";
        return aliases;
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
            // AUD-R35-195, the joined copy - a non-assignment binding was skipped in silence,
            // leaving no column, no alias and no error.
            if (binding is not MemberAssignment assignment)
            {
                throw new NotSupportedException(
                    $"Member binding '{binding.BindingType}' is not supported in GROUP BY Select. " +
                    "Only member assignments (Member = expression) can be translated.");
            }

            var memberName = assignment.Member.Name;
            (string sql, string _) = TranslateExpression(assignment.Expression, memberName, groupingParam);
            _selectColumns.Add($"{sql} AS {_dialect.EscapeColumnName(memberName)}");
            _columnAliases.Add(memberName);
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
            // AUD-R35-193, the joined copy - see GroupByExpressionVisitor<T,TKey>.TranslateExpression
            // for why a composite key nested inside a projection is refused rather than expanded.
            if (_groupByColumns.Length > 1)
            {
                throw new NotSupportedException(
                    "A composite grouping key cannot be projected as a whole inside a projection. " +
                    "Project its parts instead - g.Key.PropertyName - or select the bare key, g => g.Key.");
            }

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
            return (HavingExpressionHelpers.FormatLiteral(constant.Value, _dialect), defaultAlias);
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

    /// <summary>
    /// AUD-R35-066. AVG used to be emitted bare, like every other aggregate. On SQL Server AVG
    /// takes its result type from its operand, so the average of an int column was truncated in the
    /// engine and then widened to the double the fluent Avg is declared to return - 12.0 where the
    /// true average is 12.6. The other three engines do not truncate, so they get the bare form
    /// still; the decision lives in FractionalAverage rather than here, because this translator and
    /// its joined twin held the same defect and would drift again.
    /// </summary>
    private string ApplyAggregate(string aggregate, string operand)
        => aggregate == "AVG"
            ? FractionalAverage.Generate(_dialect, operand)
            : $"{aggregate}({operand})";

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
                string qualified = GetQualifiedColumnName(paramIndex, memberExpr.Member.Name);
                return ApplyAggregate(aggregate, qualified);
            }

            if (body is ConstantExpression constant)
            {
                return ApplyAggregate(aggregate, HavingExpressionHelpers.FormatLiteral(constant.Value, _dialect));
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
            string qualified = GetQualifiedColumnName(paramIndex, member.Member.Name);
            return ([qualified], new Dictionary<string, string>());
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
                string qualified = GetQualifiedColumnName(paramIndex, memberArg.Member.Name);
                columns[i] = qualified;

                var keyPropertyName = newExpr.Members?[i]?.Name ?? memberArg.Member.Name;
                propertyToColumn[keyPropertyName] = qualified;
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

    // AUD-R26-058: was a linear scan of _metadata[paramIndex].Columns followed by
    // _dialect.EscapeColumnName - a SqlIdentifierValidator regex match and a keyword HashSet lookup
    // per column reference per query build. AUD-R25 replaced exactly that with the pre-escaped
    // lookup everywhere else and missed this site.
    private string GetQualifiedColumnName(int paramIndex, string propertyName) =>
        $"{_tablePrefixes[paramIndex]}.{_cachedMetadata[paramIndex].GetColumnName(propertyName)}";
}

using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Translates a two-parameter correlation predicate (outer, subquery) => bool
/// into SQL for EXISTS subqueries.
/// </summary>
internal sealed class ExistsExpressionVisitor<TOuter, TSubquery> : ExpressionVisitor
    where TOuter : new()
    where TSubquery : new()
{
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _outerMetadata;
    private readonly EntityMetadata _subqueryMetadata;
    private readonly string? _outerAlias;
    private readonly string _subqueryAlias;
    private readonly StringBuilder _sql = new();
    private readonly List<(string Name, object? Value)> _parameters = new();
    private readonly Dictionary<string, int> _parameterCounts;

    private ParameterExpression? _outerParam;
    private ParameterExpression? _subqueryParam;

    /// <summary>
    /// Creates the visitor. See <paramref name="outerAlias"/>/<paramref name="subqueryAlias"/>
    /// remarks below for why both are required for correct correlation.
    /// </summary>
    /// <param name="dialect">The SQL dialect used to escape identifiers.</param>
    /// <param name="outerMetadata">Entity metadata for the outer query's entity type.</param>
    /// <param name="subqueryMetadata">Entity metadata for the correlated subquery's entity type.</param>
    /// <param name="outerAlias">
    /// The alias the outer query's FROM clause was created with (e.g. via <c>db.From&lt;T&gt;("c")</c>),
    /// or <c>null</c> to fall back to the escaped table name. Outer column references must use this
    /// alias rather than the raw table name, or the generated SQL is invalid when the outer query
    /// itself uses an alias.
    /// </param>
    /// <param name="subqueryAlias">
    /// The alias assigned to the correlated subquery's FROM clause. Always required (even when
    /// <typeparamref name="TOuter"/> and <typeparamref name="TSubquery"/> differ) so a
    /// self-referencing EXISTS (TOuter == TSubquery) doesn't resolve both sides to the identical
    /// table prefix, which would make the correlation meaningless.
    /// </param>
    /// <param name="parameterCounts">
    /// Shared parameter-name counter, when supplied, so multiple correlated EXISTS clauses on
    /// the same outer query (e.g. <c>.WhereExists(...).AndExists(...)</c>) don't each generate
    /// an identically-named "@p_exists" parameter for their captured/constant operands. Omit
    /// (or pass null) for a standalone translation.
    /// </param>
    public ExistsExpressionVisitor(ISqlDialect dialect, EntityMetadata outerMetadata, EntityMetadata subqueryMetadata, string? outerAlias, string subqueryAlias, Dictionary<string, int>? parameterCounts = null)
    {
        _dialect = dialect;
        _outerMetadata = outerMetadata;
        _subqueryMetadata = subqueryMetadata;
        _outerAlias = outerAlias;
        _subqueryAlias = subqueryAlias;
        _parameterCounts = parameterCounts ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<TOuter, TSubquery, bool>> predicate)
    {
        _sql.Clear();
        _parameters.Clear();

        // Extract parameters from lambda
        _outerParam = predicate.Parameters[0];
        _subqueryParam = predicate.Parameters[1];

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

        // Handle comparison operators - need to determine which side is outer vs subquery
        (bool IsColumn, string Sql, object? Value) leftInfo = AnalyzeExpression(node.Left);
        (bool IsColumn, string Sql, object? Value) rightInfo = AnalyzeExpression(node.Right);

        // Handle null comparisons: emit IS NULL / IS NOT NULL instead of binding a NULL
        // parameter, since SQL's three-valued logic means "col = @p" with @p bound to NULL
        // never matches (same fix pattern as WhereExpressionVisitor.IsNullConstant).
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            if (leftInfo.IsColumn && !rightInfo.IsColumn && rightInfo.Value is null)
            {
                _sql.Append(leftInfo.Sql);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                _sql.Append(')');
                return node;
            }

            if (rightInfo.IsColumn && !leftInfo.IsColumn && leftInfo.Value is null)
            {
                _sql.Append(rightInfo.Sql);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                _sql.Append(')');
                return node;
            }
        }

        if (leftInfo.IsColumn)
        {
            _sql.Append(leftInfo.Sql);
        }
        else
        {
            // Constant or evaluated value
            var paramName = GetParameterName("p_exists");
            _sql.Append(paramName);
            _parameters.Add((paramName, leftInfo.Value));
        }

        _sql.Append(GetOperator(node.NodeType));

        if (rightInfo.IsColumn)
        {
            _sql.Append(rightInfo.Sql);
        }
        else
        {
            // Constant or evaluated value
            var paramName = GetParameterName("p_exists");
            _sql.Append(paramName);
            _parameters.Add((paramName, rightInfo.Value));
        }

        _sql.Append(')');
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        (bool IsColumn, string Sql, object? Value) info = AnalyzeExpression(node);
        if (info.IsColumn)
        {
            _sql.Append(info.Sql);
        }
        else
        {
            var paramName = GetParameterName("p_exists");
            _sql.Append(paramName);
            _parameters.Add((paramName, info.Value));
        }
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is null)
        {
            _sql.Append("NULL");
        }
        else
        {
            var paramName = GetParameterName("p_exists");
            _sql.Append(paramName);
            _parameters.Add((paramName, node.Value));
        }
        return node;
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

        // AUD-R35-020. ConvertChecked was missing here while the WHERE twin has handled both
        // since AUD-R34-020; a checked cast in a correlation predicate took the fall-through below.
        if (node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
        {
            Visit(node.Operand);
            return node;
        }

        // AUD-R35-020, the EXISTS copy of AUD-R34-020. Everything else used to reach
        // base.VisitUnary, which visits the operand and appends nothing for the operator itself,
        // silently dropping it. In practice a unary that is not Not or a Convert cannot be
        // bool-typed, so an operand of AndAlso/OrElse/Not never is one and RequireNoCorrelation-
        // Parameter on the AnalyzeExpression path is what actually reports these; this stays as the
        // safe default for any shape that does reach the dispatcher, matching the WHERE twin.
        throw new NotSupportedException(
            $"Unary operator '{node.NodeType}' is not supported in EXISTS correlation predicates. " +
            "Compute the value before the query, or express the condition without it.");
    }

    /// <summary>
    /// AUD-R33-003. Same gap AUD-R32-002 closed in <c>WhereExpressionVisitor</c> and
    /// <c>SelectExpressionVisitor</c>: with no override the base traversal walks Test/IfTrue/IfFalse
    /// and each appends its own fragment to the shared <c>_sql</c> builder with nothing joining
    /// them. Every other unsupported shape in this file throws, so this does too.
    /// </summary>
    protected override Expression VisitConditional(ConditionalExpression node)
        => throw new NotSupportedException(
            "Conditional (ternary) expressions are not supported in EXISTS correlation predicates. " +
            "Split the predicate into separate conditions.");

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        throw new NotSupportedException($"Method '{node.Method.Name}' is not supported in EXISTS correlation predicates.");
    }

    /// <summary>
    /// AUD-R35-020, the EXISTS copy of the AUD-R33-010 / AUD-R34-018 sweep the WHERE and SELECT
    /// visitors got. A ternary was not the only unhandled node type here, only the one AUD-R33-003
    /// had noticed.
    /// </summary>
    /// <remarks>
    /// All of these are reachable: <see cref="VisitBinary"/>'s AndAlso/OrElse branch visits both
    /// operands through the base dispatcher, and <see cref="VisitUnary"/> does the same for a
    /// <c>Not</c> or <c>Convert</c> operand - so <c>(o, s) =&gt; s.Id == o.Id &amp;&amp; o.Category is
    /// Category</c> reached <see cref="ExpressionVisitor"/>'s descend-into-children default. That
    /// default appends nothing for the wrapping node, only for whatever leaves hang off it, so the
    /// <c>is</c>-check was dropped while its receiver still emitted a bare column into the shared
    /// <c>_sql</c> builder: a syntactically plausible EXISTS asking a different question. Each of
    /// these throws because there is no SQL to translate them to - the value can be computed before
    /// the query and compared against.
    /// </remarks>
    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        => throw new NotSupportedException(
            "Type tests ('is', 'as') are not supported in EXISTS correlation predicates. There is " +
            "no SQL equivalent of a CLR type test over a column; correlate on a discriminator column.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitNew(NewExpression node)
        => throw new NotSupportedException(
            $"Constructing a '{node.Type.Name}' is not supported in EXISTS correlation predicates. " +
            "Compute the value before the query and compare against it.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitNewArray(NewArrayExpression node)
        => throw new NotSupportedException(
            "Array construction is not supported in EXISTS correlation predicates. Build the array " +
            "before the query and pass it in.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitMemberInit(MemberInitExpression node)
        => throw new NotSupportedException(
            $"Object initializers ('new {node.Type.Name} {{ ... }}') are not supported in EXISTS " +
            "correlation predicates. Compute the value before the query and compare against it.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitListInit(ListInitExpression node)
        => throw new NotSupportedException(
            "Collection initializers are not supported in EXISTS correlation predicates. Build the " +
            "collection before the query and pass it in.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitInvocation(InvocationExpression node)
        => throw new NotSupportedException(
            "Invoking a delegate or a nested lambda is not supported in EXISTS correlation " +
            "predicates. Inline the predicate, or evaluate the delegate before the query.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitIndex(IndexExpression node)
        => throw new NotSupportedException(
            "Indexer access is not supported in EXISTS correlation predicates. Read the element " +
            "before the query and compare against the value.");

    /// <summary>
    /// A bare parameter is not a condition. With no override it returned silently and appended
    /// nothing, leaving a dangling operator in <c>_sql</c>. AUD-R35-020.
    /// </summary>
    protected override Expression VisitParameter(ParameterExpression node)
        => throw new NotSupportedException(
            $"'{node.Name}' is the whole entity, not a condition. Correlate on a column of it.");

    private (bool IsColumn, string Sql, object? Value) AnalyzeExpression(Expression expression)
    {
        // Unwrap Convert. AUD-R35-021: ConvertChecked was missing, so a checked cast on a
        // correlated column fell through to the evaluate-as-a-constant tail below.
        if (expression is UnaryExpression unary
            && unary.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
            expression = unary.Operand;

        if (expression is MemberExpression member)
        {
            // Check if it's a member access on the outer parameter
            ParameterExpression? root = GetRootParameter(member);
            if (root == _outerParam || root == _subqueryParam)
            {
                // AUD-R35-020: the fourth copy of the AUD-R34-021 defect. GetRootParameter walks
                // the whole chain, so (o, s) => s.OrderDate.Year == o.Year resolved to a column
                // named "Year" - GetColumnName escapes anything unmapped rather than failing.
                ColumnReference.RequireDirect(member);
            }

            if (root == _outerParam)
            {
                var outerPrefix = _outerAlias ?? _dialect.EscapeTableName(_outerMetadata.SchemaName, _outerMetadata.TableName);
                return (true, $"{outerPrefix}.{GetEscapedOuterColumnName(member)}", null);
            }

            // Check if it's a member access on the subquery parameter
            if (root == _subqueryParam)
            {
                return (true, $"{_subqueryAlias}.{GetEscapedSubqueryColumnName(member)}", null);
            }

            // It's a captured variable - evaluate it
            RequireNoCorrelationParameter(expression);
            var value = EvaluateExpression(expression);
            return (false, string.Empty, value);
        }

        if (expression is ConstantExpression constant)
        {
            return (false, string.Empty, constant.Value);
        }

        // Evaluate other expressions
        RequireNoCorrelationParameter(expression);
        var evalValue = EvaluateExpression(expression);
        return (false, string.Empty, evalValue);
    }

    /// <summary>
    /// AUD-R35-021. Everything this method cannot translate is handed to
    /// <see cref="EvaluateExpression"/>, which compiles the sub-expression into a delegate. That
    /// only works for a closed-over value: an operand still mentioning one of the two correlation
    /// parameters - <c>-p.UnitPrice</c>, <c>checked((long)p.Id)</c>, <c>f(p.Id)</c> - compiled a
    /// lambda over a free parameter and surfaced as
    /// <c>InvalidOperationException: variable 'p' ... referenced from scope '', but it is not
    /// defined</c>, an internal LINQ message that names nothing the caller wrote and is not the
    /// <see cref="NotSupportedException"/> every other untranslatable shape in this file raises.
    /// The <c>Visit*</c> overrides do not cover this path: <see cref="VisitBinary"/> calls
    /// <see cref="AnalyzeExpression"/> on its operands directly rather than dispatching them.
    /// </summary>
    private void RequireNoCorrelationParameter(Expression expression)
    {
        if (!CorrelationParameterFinder.Contains(expression, _outerParam, _subqueryParam))
            return;

        throw new NotSupportedException(
            $"'{expression}' is not a translatable column reference, and it cannot be evaluated " +
            "before the query because it still refers to a correlation parameter. Compare columns " +
            "directly, or compute the value outside the predicate and capture it.");
    }

    private sealed class CorrelationParameterFinder : ExpressionVisitor
    {
        private ParameterExpression? _first;
        private ParameterExpression? _second;
        private bool _found;

        public static bool Contains(Expression expression, ParameterExpression? first, ParameterExpression? second)
        {
            if (first is null && second is null)
                return false;

            var finder = new CorrelationParameterFinder { _first = first, _second = second };
            finder.Visit(expression);
            return finder._found;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == _first || node == _second)
                _found = true;

            return node;
        }
    }

    private ParameterExpression? GetRootParameter(MemberExpression member)
    {
        Expression? current = member;
        while (current is MemberExpression me)
            current = me.Expression;

        return current as ParameterExpression;
    }

    // AUD-R25: this used to run metadata.Columns.FirstOrDefault(c => c.PropertyName == ...) - a
    // LINQ delegate allocation plus an O(columns) linear scan - and hand the result to
    // _dialect.EscapeColumnName, which re-runs SqlIdentifierValidator's regex match and a keyword
    // HashSet lookup, both per column reference per query build. CachedDialectMetadata holds an
    // OrdinalIgnoreCase dictionary of property name to already-escaped column name, built once per
    // (entity, dialect) pair; QueryBuilder, CteBuilder and InsertBuilder already used it.
    private string GetEscapedOuterColumnName(MemberExpression member) =>
        FluentMetadataCache.GetForDialect<TOuter>(_dialect).GetColumnName(member.Member.Name);

    private string GetEscapedSubqueryColumnName(MemberExpression member) =>
        FluentMetadataCache.GetForDialect<TSubquery>(_dialect).GetColumnName(member.Member.Name);

    // AUD-R25: this was one of eight byte-identical private copies. Kept as a one-line forwarder
    // rather than rewriting every call site, so the shared implementation - including its
    // closure-member fast path - is the only place the behaviour lives.
    private static object? EvaluateExpression(Expression expression) => ExpressionEvaluator.Evaluate(expression);

    private static string GetOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => " = ",
        ExpressionType.NotEqual => " <> ",
        ExpressionType.LessThan => " < ",
        ExpressionType.LessThanOrEqual => " <= ",
        ExpressionType.GreaterThan => " > ",
        ExpressionType.GreaterThanOrEqual => " >= ",
        _ => throw new NotSupportedException($"Operator {nodeType} is not supported in EXISTS expressions.")
    };
}
using System.Globalization;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;

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
        (string? columnName, string? rawColumnName, object? value, bool isLeftColumn) = ExtractColumnAndValue(node);

        if (columnName is null)
        {
            // Neither side is a plain column (e.g. a Sql.* function call on one side, such as
            // Sql.NullIf(...) != null). SQL's three-valued logic means "expr <> NULL"/"expr = NULL"
            // never matches (always UNKNOWN) even when expr is non-null, so a comparison against a
            // literal null must still become IS NULL/IS NOT NULL rather than a naive operator.
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            {
                if (IsNullOperand(node.Right))
                {
                    Visit(node.Left);
                    _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                    _sql.Append(')');
                    return node;
                }

                if (IsNullOperand(node.Left))
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

        var escapedColumn = columnName;

        // Handle null comparisons
        if (value is null)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            {
                _sql.Append(escapedColumn);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
            }
            else
            {
                // A relational comparison against NULL is UNKNOWN in SQL and false for C#'s
                // lifted operators - never true either way - so emit a match-nothing predicate
                // rather than IS NOT NULL, which would match every non-null row.
                _sql.Append("1 = 0");
            }
            _sql.Append(')');
            return node;
        }

        _sql.Append(escapedColumn);
        _sql.Append(GetOperator(isLeftColumn ? node.NodeType : MirrorOperator(node.NodeType)));
        var paramName = GetParameterName(rawColumnName!);
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
                var escapedColumn = GetEscapedColumnName(memberExpr);
                var columnName = GetRawColumnName(memberExpr);

                switch (node.Method.Name)
                {
                    case "Contains":
                        {
                            var value = EvaluateExpression(node.Arguments[0]);
                            var paramName = GetParameterName(columnName);
                            _sql.Append(IsCaseInsensitiveComparison(node)
                                ? _dialect.GenerateCaseInsensitiveLike(escapedColumn, paramName, "\\")
                                : _dialect.GenerateCaseSensitiveLike(escapedColumn, paramName, "\\"));
                            _parameters.Add((paramName, _dialect.FormatContainsPattern(value?.ToString() ?? "")));
                            return node;
                        }
                    case "StartsWith":
                        {
                            var value = EvaluateExpression(node.Arguments[0]);
                            var paramName = GetParameterName(columnName);
                            _sql.Append(IsCaseInsensitiveComparison(node)
                                ? _dialect.GenerateCaseInsensitiveLike(escapedColumn, paramName, "\\")
                                : _dialect.GenerateCaseSensitiveLike(escapedColumn, paramName, "\\"));
                            _parameters.Add((paramName, _dialect.FormatStartsWithPattern(value?.ToString() ?? "")));
                            return node;
                        }
                    case "EndsWith":
                        {
                            var value = EvaluateExpression(node.Arguments[0]);
                            var paramName = GetParameterName(columnName);
                            _sql.Append(IsCaseInsensitiveComparison(node)
                                ? _dialect.GenerateCaseInsensitiveLike(escapedColumn, paramName, "\\")
                                : _dialect.GenerateCaseSensitiveLike(escapedColumn, paramName, "\\"));
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
                            var sqlStart = Convert.ToInt32(startIndex, CultureInfo.InvariantCulture) + 1;
                            if (node.Arguments.Count >= 2)
                            {
                                var length = EvaluateExpression(node.Arguments[1]);
                                _sql.Append(_dialect.GenerateSubstring(
                                    escapedColumn,
                                    sqlStart.ToString(CultureInfo.InvariantCulture),
                                    FormatSubstringLength(length)));
                            }
                            else
                            {
                                // AUD-R26: single-argument Substring means "the rest of the string",
                                // which every engine but SQL Server can say natively. This used to
                                // emit a literal 8000 - a SQL Server convention, from a
                                // dialect-neutral visitor, applied to all of them - so anything past
                                // the 8000th character was silently dropped. Measured against a
                                // 10,000-character value: SQLite and DuckDB both returned 8,000.
                                _sql.Append(SubstringToEnd.Generate(
                                    _dialect,
                                    escapedColumn,
                                    sqlStart.ToString(CultureInfo.InvariantCulture)));
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

        // C# 14's first-class span conversions rebind an *array* receiver away from
        // Enumerable.Contains to MemoryExtensions.Contains(ReadOnlySpan<T>, T). The tree is built
        // by whoever writes the lambda, so this arrives from a consumer compiling with C# 14 no
        // matter which language version Jaunty itself was built with - the LangVersion pin in
        // Directory.Build.props does not protect against it. Arity and argument order match the
        // Enumerable form; only the collection differs, arriving wrapped in
        // ReadOnlySpan<T>.op_Implicit. Matched by name because MemoryExtensions does not exist on
        // netstandard2.0 and referencing it would add a System.Memory dependency to every consumer.
        bool isSpanContains = node.Method.Name == "Contains"
            && node.Object is null
            && node.Arguments.Count == 2
            && node.Method.DeclaringType?.FullName == "System.MemoryExtensions";

        if (isEnumerableContains || isInstanceContains || isSpanContains)
        {
            var collectionExpr = isInstanceContains ? node.Object! : node.Arguments[0];
            if (isSpanContains)
                collectionExpr = UnwrapSpanConversion(collectionExpr);

            var collection = EvaluateExpression(collectionExpr);
            var memberExpr = (isInstanceContains ? node.Arguments[0] : node.Arguments[1]) as MemberExpression;

            if (memberExpr is not null && IsParameterMember(memberExpr) && collection is System.Collections.IEnumerable enumerable)
            {
                var escapedColumn = GetEscapedColumnName(memberExpr);
                var columnName = GetRawColumnName(memberExpr);

                // AUD-R26-056: this was enumerable.Cast<object>().ToList(), which built a whole
                // List<object> - allocated by doubling, so a reallocation chain - purely to learn
                // the count and then read each element once, in order, straight into _parameters.
                // A collection that already knows its own size does not need the copy at all.
                // Buffering is still required for anything that does not, because the count has to
                // be known before any SQL is appended (the empty case emits "1 = 0" instead of an
                // IN clause) and a bare IEnumerable may be single-pass or have side effects, so it
                // cannot be walked twice. QueryBuilder.BuildInClause already made this distinction
                // for the same job.
                System.Collections.ICollection? sized = collection as System.Collections.ICollection;
                List<object?>? buffered = null;
                int valueCount;

                if (sized is not null)
                {
                    valueCount = sized.Count;
                }
                else
                {
                    buffered = new List<object?>();
                    foreach (object? item in enumerable) buffered.Add(item);
                    valueCount = buffered.Count;
                }

                if (valueCount == 0)
                {
                    _sql.Append("1 = 0"); // Empty collection always false
                    return node;
                }

                // AUD-R26: like QueryBuilder.BuildInClause, this expands the collection itself into
                // individually-named scalars and so never reached ParameterBinder's ceiling check.
                // The count is this visitor's own - a query can span several visitors, one per
                // Where/And/Or - so it is a lower bound on the statement total. That is the right
                // direction to err for a guard whose old failure mode was refusing valid queries.
                ParameterCeiling.EnsureWithinLimit(
                    _parameters.Count + valueCount,
                    _dialect,
                    _dialect.GetType().Name);

                _sql.Append(escapedColumn);
                _sql.Append(" IN (");
                int valueIndex = 0;
                foreach (object? value in (System.Collections.IEnumerable?)buffered ?? enumerable)
                {
                    if (valueIndex > 0) _sql.Append(", ");
                    var paramName = GetParameterName($"{columnName}_{valueIndex}");
                    _sql.Append(paramName);
                    _parameters.Add((paramName, value));
                    valueIndex++;
                }
                _sql.Append(')');
                return node;
            }
        }

        // AUD-R26-056: nothing above matched, so this expression is about to be handed to
        // EvaluateExpression, which compiles it with Expression.Lambda(...).Compile(). That works
        // for a call that does not touch the lambda parameter - Helper.Now(), a captured local's
        // method - and throws "variable 'p' of type 'Item' referenced from scope '', but it is not
        // defined" for one that does. That message names neither the method nor the limitation, and
        // it is what a caller got for the obvious next thing to try after seeing that both halves
        // work on their own:
        //
        //     p.Name.ToUpper()          translates       p.Name.Contains("alp")   translates
        //     p.Name.ToUpper().Contains("ALP")           does not
        //
        // because the string handler above requires node.Object to be a MemberExpression over the
        // parameter, and a chained call's receiver is another MethodCallExpression. Same failure
        // mode AUD-R12 and R16 fixed for column-to-column and string.Length comparisons: an
        // expression still referencing the lambda parameter reaching Compile(). Checking for that
        // first turns an unexplained runtime crash into a diagnosable one, for every unsupported
        // parameter-referencing call rather than only the chained-string shape.
        if (ReferencesLambdaParameter(node))
        {
            throw new NotSupportedException(
                $"Cannot translate '{node}' to SQL. " +
                $"'{node.Method.DeclaringType?.Name}.{node.Method.Name}' is not supported in a Where " +
                "expression in this position. String methods (Contains, StartsWith, EndsWith, ToUpper, " +
                "ToLower, Trim, Substring) are translated only when applied directly to a mapped " +
                "property, so 'p.Name.ToUpper()' translates but 'p.Name.ToUpper().Contains(...)' does " +
                "not - a chained call's receiver is another method call, not a column. Rewrite the " +
                "condition to use a single method call on the property, or express it as raw SQL.");
        }

        // Fallback: evaluate and use as constant. Only legitimate when the call is closed over
        // captured state; the guard above is what keeps a parameter-referencing subtree from
        // reaching Compile() and surfacing as an opaque "variable 'p' ... is not defined". The
        // C# 14 span-rebinding fix upstream hit exactly that: an unsupported translation looked
        // like an evaluation bug.
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

    /// <summary>
    /// Whether <paramref name="expression"/> still contains a reference to the lambda's parameter,
    /// which is what makes it impossible to evaluate as a constant.
    /// </summary>
    /// <remarks>
    /// AUD-R26-056. <c>EvaluateExpression</c> closes over nothing, so a surviving
    /// <see cref="ParameterExpression"/> makes <c>Expression.Lambda(...).Compile()</c> throw at
    /// runtime with a message about an undefined variable. Detecting it beforehand is what lets the
    /// caller be told which method was not translatable.
    /// </remarks>
    private static bool ReferencesLambdaParameter(Expression expression)
    {
        var finder = new ParameterFinder();
        finder.Visit(expression);
        return finder.Found;
    }

    private sealed class ParameterFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Found = true;
            return node;
        }

        public override Expression? Visit(Expression? node) => Found ? node : base.Visit(node);
    }

    private Expression HandleSqlFunction(MethodCallExpression node)
    {
        _sql.Append(TranslateSqlFunction(node));
        return node;
    }

    /// <summary>
    /// AUD-R35-064. Each <c>Sql.*</c> function used to have its own <c>Handle*</c> that appended
    /// straight to <c>_sql</c>, which is why a nested call could not be translated: an argument has
    /// to become a <em>string</em> to be handed to the next <c>Generate*</c>. Producing the SQL
    /// here instead lets <see cref="TranslateArgumentToSql"/> recurse, matching what the SELECT
    /// visitor's <c>TranslateProjectionExpression</c> has always done.
    /// </summary>
    private string TranslateSqlFunction(MethodCallExpression node)
    {
        var methodName = node.Method.Name;

        switch (methodName)
        {
            case "Coalesce":
                {
                    var arguments = new string[node.Arguments.Count];
                    for (int i = 0; i < node.Arguments.Count; i++)
                        arguments[i] = TranslateArgumentToSql(node.Arguments[i]);

                    return _dialect.GenerateCoalesce(arguments);
                }
            case "IsNull":
                return _dialect.GenerateIsNull(
                    TranslateArgumentToSql(node.Arguments[0]),
                    TranslateArgumentToSql(node.Arguments[1]));
            case "NullIf":
                return _dialect.GenerateNullIf(
                    TranslateArgumentToSql(node.Arguments[0]),
                    TranslateArgumentToSql(node.Arguments[1]));
            // String functions
            case "Length":
                return _dialect.GenerateLength(TranslateArgumentToSql(node.Arguments[0]));
            case "Upper":
                return _dialect.GenerateUpper(TranslateArgumentToSql(node.Arguments[0]));
            case "Lower":
                return _dialect.GenerateLower(TranslateArgumentToSql(node.Arguments[0]));
            case "Trim":
                return _dialect.GenerateTrim(TranslateArgumentToSql(node.Arguments[0]));
            case "Substring":
                return _dialect.GenerateSubstring(
                    TranslateArgumentToSql(node.Arguments[0]),
                    TranslateArgumentToSql(node.Arguments[1]),
                    TranslateArgumentToSql(node.Arguments[2]));
            // Date functions
            case "Year":
                return _dialect.GenerateYear(TranslateArgumentToSql(node.Arguments[0]));
            case "Month":
                return _dialect.GenerateMonth(TranslateArgumentToSql(node.Arguments[0]));
            case "Day":
                return _dialect.GenerateDay(TranslateArgumentToSql(node.Arguments[0]));
            // CASE expression (shouldn't reach here - handled in VisitMethodCall)
            case "Case":
                throw new NotSupportedException("Sql.Case() must be followed by .When() and .Else() or .End()");
            default:
                throw new NotSupportedException($"SQL function '{methodName}' is not supported.");
        }
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
        if (condition is BinaryExpression { NodeType: ExpressionType.AndAlso or ExpressionType.OrElse } logical)
        {
            // Defensive: a bare (unquoted) AndAlso/OrElse BinaryExpression reaching here would
            // otherwise fall through to GetOperator, which throws NotSupportedException. In
            // practice, a compound .When(x => a && b, ...) condition arrives as a Quote
            // UnaryExpression (not a bare LambdaExpression/BinaryExpression) because it's a
            // nested Expression<Func<TFrom,bool>> inside an already-being-built outer expression
            // tree, so it's actually resolved via the EvaluateExpression/Visit fallback below,
            // which re-enters this class's top-level VisitBinary AndAlso/OrElse handling. This
            // branch guards the case where a caller (or a future refactor) supplies an already
            // unwrapped BinaryExpression directly.
            _sql.Append('(');
            TranslateCaseCondition(logical.Left);
            _sql.Append(logical.NodeType == ExpressionType.AndAlso ? " AND " : " OR ");
            TranslateCaseCondition(logical.Right);
            _sql.Append(')');
            return;
        }

        if (condition is BinaryExpression binary)
        {
            // Handle comparison operators
            (string? columnName, string? rawColumnName, object? value, bool isLeftColumn) = ExtractColumnAndValue(binary);

            if (columnName != null)
            {
                var escapedColumn = columnName;

                // Mirror VisitBinary's null handling: per SQL three-valued logic, "col = NULL"
                // never matches, so a WHEN condition comparing to a literal null must become
                // IS NULL/IS NOT NULL rather than a parameterized comparison bound to NULL.
                if (value is null && binary.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
                {
                    _sql.Append(escapedColumn);
                    _sql.Append(binary.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                }
                else
                {
                    _sql.Append(escapedColumn);
                    _sql.Append(GetOperator(isLeftColumn ? binary.NodeType : MirrorOperator(binary.NodeType)));
                    var paramName = GetParameterName(rawColumnName!);
                    _sql.Append(paramName);
                    _parameters.Add((paramName, value));
                }
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
                var escapedColumn = GetEscapedColumnName(member);
                _sql.Append(escapedColumn);
                _sql.Append(" = ").Append(_dialect.FormatBooleanLiteral(true));
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

    /// <summary>
    /// AUD-R35-064. This handled exactly two shapes - a <c>Convert</c> wrapper and a direct
    /// parameter member - and sent everything else to <c>EvaluateExpression</c>. A nested
    /// <c>Sql.*</c> call still references the lambda parameter, so
    /// <c>Expression.Lambda(...).Compile()</c> threw
    /// <c>variable 'p' of type 'Product' referenced from scope ''</c>: the exact opaque message
    /// AUD-R26-056's guard exists to eliminate, and that guard sits in <c>VisitMethodCall</c>, which
    /// <c>HandleSqlFunction</c> has already branched out of by this point. The SELECT visitor's
    /// twin has always recursed, so <c>Sql.Upper(Sql.Trim(p.ProductName))</c> translated in a
    /// projection and crashed in a WHERE. <c>Quote</c> and the <c>??</c> binary, both handled on the
    /// SELECT side, were missing here too.
    /// </summary>
    private string TranslateArgumentToSql(Expression arg)
    {
        // Unwrap Convert and Quote, repeatedly - a nested call under a Convert under a Quote is one
        // expression, not three shapes to enumerate.
        while (arg is UnaryExpression unary
            && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.Quote))
        {
            arg = unary.Operand;
        }

        // If it's a member access on the parameter, translate to column
        if (arg is MemberExpression member && IsParameterMember(member))
        {
            return GetEscapedColumnName(member);
        }

        // A nested Sql.* call: translate it in place rather than trying to evaluate it.
        if (arg is MethodCallExpression call && call.Method.DeclaringType == typeof(Sql))
        {
            return TranslateSqlFunction(call);
        }

        // The ?? operator, which the SELECT side renders as the dialect's IS NULL form.
        if (arg is BinaryExpression coalesce && coalesce.NodeType == ExpressionType.Coalesce)
        {
            return _dialect.GenerateIsNull(
                TranslateArgumentToSql(coalesce.Left),
                TranslateArgumentToSql(coalesce.Right));
        }

        // Anything left that still references the lambda parameter cannot be evaluated as a
        // constant; say so rather than letting Compile() report an undefined variable.
        if (ReferencesLambdaParameter(arg))
        {
            throw new NotSupportedException(
                $"Cannot translate '{arg}' to SQL as an argument to a Sql.* function. Only a mapped " +
                "property, a nested Sql.* call, a ?? expression, or a value that does not reference " +
                "the lambda parameter can appear there.");
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

        if (node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
        {
            Visit(node.Operand);
            return node;
        }

        // AUD-R34-020. Quote is load-bearing: it is how a compound `Sql.Case().When(x => a && b, ...)`
        // condition gets re-entered (see the note above VisitLambda), and the base default - visit
        // the body, which dispatches straight back to VisitBinary - is exactly right there.
        if (node.NodeType == ExpressionType.Quote)
            return base.VisitUnary(node);

        // Everything else used to take that same tail, which visits the operand and appends nothing
        // for the operator: `p => -p.UnitPrice > 5m` translated to `([unit_price] > @Value)`, the
        // negation silently gone, so the caller got the opposite rows rather than an error. Negate,
        // NegateChecked, TypeAs, OnesComplement, ArrayLength and UnaryPlus all took that route.
        throw new NotSupportedException(
            $"Unary operator '{node.NodeType}' is not supported in WHERE predicates. Compute the " +
            "value before the query, or express the condition without it.");
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Handle string.Length property (e.g., p.ProductName.Length > 10)
        if (node.Member.Name == "Length" && node.Expression is MemberExpression innerMember
            && innerMember.Type == typeof(string) && IsParameterMember(innerMember))
        {
            var escapedColumn = GetEscapedColumnName(innerMember);
            _sql.Append(_dialect.GenerateLength(escapedColumn));
            return node;
        }

        // Handle boolean properties directly (e.g., p => p.IsActive)
        if (IsParameterMember(node) && node.Type == typeof(bool))
        {
            var escapedColumn = GetEscapedColumnName(node);
            _sql.Append(escapedColumn);
            _sql.Append(" = ").Append(_dialect.FormatBooleanLiteral(true));
            return node;
        }

        // If this is a member access on the parameter, it's a column reference
        if (IsParameterMember(node))
        {
            _sql.Append(GetEscapedColumnName(node));
            return node;
        }

        // Otherwise, evaluate as a constant
        var value = EvaluateExpression(node);
        var paramName = GetParameterName("Value");
        _sql.Append(paramName);
        _parameters.Add((paramName, value));
        return node;
    }

    /// <summary>
    /// AUD-R32-002. Same gap as <c>SelectExpressionVisitor.VisitConditional</c>: a ternary has no
    /// override here, so the base traversal walked Test/IfTrue/IfFalse in order and each appended
    /// its own fragment to the shared <c>_sql</c> builder with nothing joining them - broken or
    /// silently wrong SQL rather than a translation error. Every other unsupported shape in this
    /// file throws.
    /// </summary>
    protected override Expression VisitConditional(ConditionalExpression node)
        => throw new NotSupportedException(
            "Conditional (ternary) expressions are not supported in WHERE predicates. " +
            "Use Sql.Case(...) for a CASE WHEN, or split the predicate into separate conditions.");

    /// <summary>
    /// AUD-R33-010. The rest of the sweep AUD-R32-002 started: a ternary was not the only node type
    /// with no override here, only the one that had been noticed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are all reachable. <see cref="VisitBinary"/>'s no-column fallback visits both operands
    /// through the base dispatcher, as does <see cref="VisitUnary"/> for a <c>Not</c> or
    /// <c>Convert</c> operand - so <c>p =&gt; p.Category is Category</c>, or a <c>new Foo(...)</c>
    /// sub-expression reached through a chained call, lands on <see cref="ExpressionVisitor"/>'s
    /// descend-into-children default. That default appends nothing for the wrapping node itself,
    /// only for whatever leaves hang off it, so the <c>is</c>-check or the constructor call is
    /// silently dropped while fragments of its children still reach <c>_sql</c>. The caller gets a
    /// plausible-looking query that asks a different question, which is worse than an exception by
    /// exactly the margin that makes it hard to notice.
    /// </para>
    /// <para>
    /// Every one of these throws rather than being translated because there is no SQL for them to
    /// translate to: <c>is</c> has no relational equivalent over a column, and a constructor,
    /// initializer, delegate invocation or indexer is a value the caller can compute themselves and
    /// pass in as a constant.
    /// </para>
    /// </remarks>
    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        => throw new NotSupportedException(
            "Type tests ('is', 'as') are not supported in WHERE predicates. There is no SQL " +
            "equivalent of a CLR type test over a column; filter on a discriminator column instead.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitNew(NewExpression node)
        => throw new NotSupportedException(
            $"Constructing a '{node.Type.Name}' is not supported inside a WHERE predicate. " +
            "Compute the value before the query and compare against it.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitNewArray(NewArrayExpression node)
        => throw new NotSupportedException(
            "Array construction is not supported inside a WHERE predicate. Build the array before " +
            "the query and pass it in - Contains over a local collection translates to IN.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitMemberInit(MemberInitExpression node)
        => throw new NotSupportedException(
            $"Object initializers ('new {node.Type.Name} {{ ... }}') are not supported inside a " +
            "WHERE predicate. Compute the value before the query and compare against it.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitListInit(ListInitExpression node)
        => throw new NotSupportedException(
            "Collection initializers are not supported inside a WHERE predicate. Build the " +
            "collection before the query and pass it in.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitInvocation(InvocationExpression node)
        => throw new NotSupportedException(
            "Invoking a delegate or a nested lambda is not supported inside a WHERE predicate. " +
            "Inline the predicate, or evaluate the delegate before the query.");

    /// <inheritdoc cref="VisitTypeBinary"/>
    protected override Expression VisitIndex(IndexExpression node)
        => throw new NotSupportedException(
            "Indexer access is not supported inside a WHERE predicate. Read the element before " +
            "the query and compare against the value.");

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

    private (string? ColumnName, string? RawColumnName, object? Value, bool IsLeftColumn) ExtractColumnAndValue(BinaryExpression node)
    {
        // Try left as column
        if (TryGetEscapedColumnName(node.Left, out var leftColumn, out var leftRaw))
        {
            var rightValue = EvaluateExpression(node.Right);
            return (leftColumn, leftRaw, rightValue, true);
        }

        // Try right as column
        if (TryGetEscapedColumnName(node.Right, out var rightColumn, out var rightRaw))
        {
            var leftValue = EvaluateExpression(node.Left);
            return (rightColumn, rightRaw, leftValue, false);
        }

        return (null, null, null, false);
    }

    // R28: the no-column fallback above used to test only for a literal null, but the main
    // column path treats a runtime-null evaluated value as IS NULL / IS NOT NULL. A closure-
    // captured variable that is null at translation time arrives as a MemberExpression, so
    // Sql.NullIf(...) == capturedNull fell through to a bound parameter and generated
    // "expr = @p" with @p = NULL, which three-valued logic never matches.
    private bool IsNullOperand(Expression expression)
    {
        if (IsNullConstant(expression))
            return true;

        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is MemberExpression && !ReferencesLambdaParameter(expression))
            return EvaluateExpression(expression) is null;

        return false;
    }

    private static bool IsNullConstant(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        return expression is ConstantExpression { Value: null };
    }

    // Yields both forms of the column name - see GetEscapedColumnName (AUD-R25). The escaped one
    // goes into the SQL text; the raw one seeds the generated parameter name, which must not carry
    // the dialect's quoting characters.
    private bool TryGetEscapedColumnName(Expression expression, out string? columnName, out string? rawColumnName)
    {
        columnName = null;
        rawColumnName = null;

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

            columnName = GetEscapedColumnName(member);
            rawColumnName = GetRawColumnName(member);
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
            sql = _dialect.GenerateLength(GetEscapedColumnName(innerMember));
            return true;
        }

        if (TryGetEscapedColumnName(expression, out var columnName, out _))
        {
            sql = columnName!;
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

    // AUD-R25: this used to run metadata.Columns.FirstOrDefault(c => c.PropertyName == ...) - a
    // LINQ delegate allocation plus an O(columns) linear scan - and hand the result to
    // _dialect.EscapeColumnName, which re-runs SqlIdentifierValidator's regex match and a keyword
    // HashSet lookup. Both were paid per column reference per query build, so a predicate touching
    // six columns did six scans and six regex matches. CachedDialectMetadata exists precisely for
    // this: an OrdinalIgnoreCase dictionary of property name to already-escaped column name, built
    // once per (entity, dialect) pair. QueryBuilder, CteBuilder and InsertBuilder already used it;
    // the visitors, which do far more per-column work, were the only Fluent components that didn't.
    //
    // The name returned is now escaped, so callers must not escape it again. Unmapped property
    // names still fall through to EscapeColumnName inside CachedDialectMetadata.GetColumnName,
    // which is what the old code did too.
    private string GetEscapedColumnName(MemberExpression member)
    {
        RequireDirectColumnReference(member);
        return FluentMetadataCache.GetForDialect<T>(_dialect).GetColumnName(member.Member.Name);
    }

    /// <summary>
    /// AUD-R34-021. <see cref="IsParameterMember"/> walks the whole chain to the parameter and
    /// <c>CachedDialectMetadata.GetColumnName</c> falls back to <c>EscapeColumnName(propertyName)</c>
    /// for anything unmapped, so a nested member access over the parameter was emitted as a column
    /// named after its leaf: the EF-shaped <c>o =&gt; o.OrderDate.Year == 1997</c> produced
    /// <c>([Year] = @Year)</c>. Best case a provider error naming neither the entity nor the
    /// limitation; if the entity happens to map a column called <c>Year</c>, <c>Date</c> or
    /// <c>Day</c>, it silently filtered the wrong one. <c>string.Length</c> is the one nested shape
    /// with a translation, and its callers unwrap to the direct inner member before arriving here.
    /// </summary>
    /// <remarks>
    /// AUD-R35-019 moved the implementation to <see cref="ColumnReference.RequireDirect"/> so the
    /// SELECT visitor and <c>PropertyExtractor</c> - which had the identical hole and no guard -
    /// share it rather than growing a second and third copy.
    /// </remarks>
    private static void RequireDirectColumnReference(MemberExpression member) =>
        ColumnReference.RequireDirect(member);

    // The unescaped name, for the two places a column reference feeds a generated parameter name
    // rather than SQL text. Same lookup, same cache - no linear scan.
    private string GetRawColumnName(MemberExpression member) =>
        FluentMetadataCache.GetForDialect<T>(_dialect).GetRawColumnName(member.Member.Name);

    // AUD-R25: this was one of eight byte-identical private copies. Kept as a one-line forwarder
    // rather than rewriting every call site, so the shared implementation - including its
    // closure-member fast path - is the only place the behaviour lives.
    private static object? EvaluateExpression(Expression expression) => ExpressionEvaluator.Evaluate(expression);

    /// <summary>
    /// Strips the <c>ReadOnlySpan&lt;T&gt;.op_Implicit</c> (or <c>Span&lt;T&gt;</c>) wrapper C# 14
    /// emits around an array passed to a span-based overload, yielding the original array
    /// expression. Returns the expression unchanged when it is not such a conversion, so an
    /// unrecognised shape falls through to the normal evaluation path rather than being mangled.
    /// </summary>
    private static Expression UnwrapSpanConversion(Expression expression)
    {
        if (expression is MethodCallExpression { Method.Name: "op_Implicit", Object: null, Arguments.Count: 1 } conversion
            && conversion.Method.DeclaringType?.FullName is string declaring
            && (declaring.StartsWith("System.ReadOnlySpan`1", StringComparison.Ordinal)
                || declaring.StartsWith("System.Span`1", StringComparison.Ordinal)))
        {
            return conversion.Arguments[0];
        }

        return expression;
    }

    /// <summary>
    /// Formats the length operand of <c>string.Substring(start, length)</c> for the generated SQL.
    /// </summary>
    /// <remarks>
    /// AUD-R26: this was <c>length?.ToString() ?? "1"</c>, which formatted a boxed value under the
    /// ambient <see cref="CultureInfo.CurrentCulture"/> and, on a null operand, silently emitted a
    /// length of 1 rather than the caller's value. The parameter is typed <see langword="int"/>, so
    /// null is unreachable through the C# overload; it is rejected rather than substituted so a
    /// future caller cannot get a one-character result and no diagnostic.
    /// </remarks>
    private static string FormatSubstringLength(object? length) => length switch
    {
        null => throw new NotSupportedException(
            "The length argument of Substring(start, length) evaluated to null and cannot be translated to SQL."),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => length.ToString() ?? throw new NotSupportedException(
            "The length argument of Substring(start, length) could not be formatted for SQL.")
    };

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
        if (value is null)
        {
            // String.Equals(null) is a legitimate, non-throwing comparison in .NET - per SQL
            // three-valued logic that requires IS NULL, not coercing the null into an
            // empty-string match (which would silently exclude every actual NULL row).
            _sql.Append(escapedColumn);
            _sql.Append(" IS NULL");
            return node;
        }

        var paramName = GetParameterName(columnName);
        var stringValue = value.ToString()!;

        if (IsCaseInsensitiveComparison(node))
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

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="node"/> is one of the
    /// <c>(string, StringComparison)</c> overloads and the comparison requested is case-insensitive.
    /// </summary>
    /// <remarks>
    /// AUD-R25: only <c>string.Equals</c> consulted this argument. <c>Contains</c>,
    /// <c>StartsWith</c> and <c>EndsWith</c> read <c>Arguments[0]</c> and went straight to
    /// <c>GenerateCaseSensitiveLike</c>, so
    /// <c>.Where(p =&gt; p.Name.Contains("abc", StringComparison.OrdinalIgnoreCase))</c> filtered
    /// case-sensitively. Not merely "fell back to the column's collation" either: SQL Server's
    /// GenerateCaseSensitiveLike appends COLLATE Latin1_General_CS_AS and MySQL's appends COLLATE
    /// utf8mb4_bin, actively overriding a case-insensitive column collation to give the caller the
    /// exact opposite of what they asked for.
    ///
    /// <para>
    /// A comparison argument that is not one of the three IgnoreCase values - Ordinal,
    /// CurrentCulture, InvariantCulture - is case-sensitive, which is what the LIKE already was.
    /// The culture distinction between them is not expressible in SQL and is deliberately not
    /// attempted; the case sensitivity is the part that changes which rows come back.
    /// </para>
    /// </remarks>
    private bool IsCaseInsensitiveComparison(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return false;

        return EvaluateExpression(node.Arguments[1]) is StringComparison comparison
            && comparison is StringComparison.OrdinalIgnoreCase
                or StringComparison.CurrentCultureIgnoreCase
                or StringComparison.InvariantCultureIgnoreCase;
    }
}
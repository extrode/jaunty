using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals;

namespace Jaunty.Fluent;

/// <summary>
/// Query builder for grouped queries. Implements IGroupedQuery.
/// </summary>
internal sealed class GroupedQueryBuilder<T, TKey> : IGroupedQuery<T, TKey> where T : new()
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _metadata;
    private readonly List<WhereCondition> _whereConditions;
    private readonly ParameterCollection _parameters;
    private readonly string[] _groupByColumns;
    private readonly Expression<Func<T, TKey>> _keySelector;
    private readonly List<string> _havingConditions = [];
    private int _havingParamSeq;

    internal GroupedQueryBuilder(IDbConnection connection, ISqlDialect dialect, List<WhereCondition> whereConditions,
        ParameterCollection parameters, Expression<Func<T, TKey>> keySelector)
    {
        _connection = connection;
        _dialect = dialect;
        _metadata = FluentMetadataCache.GetMetadata<T>();
        _whereConditions = whereConditions;
        _parameters = parameters;
        _keySelector = keySelector;
        _groupByColumns = ExtractGroupByColumns(keySelector);
    }

    public IGroupedQuery<T, TKey> Having(Expression<Func<IGrouping<TKey, T>, bool>> predicate)
    {
        var havingSql = TranslateHavingPredicate(predicate);
        _havingConditions.Add(havingSql);
        return this;
    }

    public List<TResult> Select<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector)
    {
        var sql = BuildSelectSql(selector);
        return ExecuteQuery<TResult>(sql, selector);
    }

    public async Task<List<TResult>> SelectAsync<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectSql(selector);
        return await ExecuteQueryAsync<TResult>(sql, selector, cancellationToken).ConfigureAwait(false);
    }

    public string ToSql<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector)
    {
        return BuildSelectSql(selector);
    }

    /// <summary>
    /// Folds WHERE conditions left-to-right, wrapping each step in parentheses so the
    /// generated SQL evaluates in the same order the fluent Where/And/Or chain was built,
    /// instead of relying on SQL's AND-before-OR operator precedence.
    /// </summary>
    private static string BuildWhereExpression(List<WhereCondition> conditions)
    {
        var expr = conditions[0].Sql;

        for (var i = 1; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            var op = condition.Operator == LogicalOperator.Or ? "OR" : "AND";
            expr = $"({expr} {op} {condition.Sql})";
        }

        return expr;
    }

    private string BuildSelectSql<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector)
    {
        var visitor = new GroupByExpressionVisitor<T, TKey>(_dialect, _groupByColumns);
        (string[]? selectColumns, string[] _) = visitor.TranslateSelect(selector);

        var sb = new StringBuilder(256);

        // SELECT
        sb.Append("SELECT ");
        for (int i = 0; i < selectColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(selectColumns[i]);
        }

        // FROM
        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_metadata.SchemaName, _metadata.TableName));

        // WHERE
        if (_whereConditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_whereConditions));
        }

        // GROUP BY
        sb.Append(" GROUP BY ");
        for (int i = 0; i < _groupByColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_groupByColumns[i]);
        }

        // HAVING
        if (_havingConditions.Count > 0)
        {
            sb.Append(" HAVING ");
            for (int i = 0; i < _havingConditions.Count; i++)
            {
                if (i > 0) sb.Append(" AND ");
                sb.Append(_havingConditions[i]);
            }
        }

        return sb.ToString();
    }

    private List<TResult> ExecuteQuery<TResult>(string sql, Expression<Func<IGrouping<TKey, T>, TResult>> selector)
        => CommandObservation.Execute(
            sql, _parameters.ToParameterObject(), _connection, CommandType.Text,
            () => ExecuteQueryDirect(sql, selector));

    private List<TResult> ExecuteQueryDirect<TResult>(string sql, Expression<Func<IGrouping<TKey, T>, TResult>> selector)
    {
        var visitor = new GroupByExpressionVisitor<T, TKey>(_dialect, _groupByColumns);
        (string[] _, string[] aliases) = visitor.TranslateSelect(selector);
        GroupedJoinedResultMapper.ResultMapperPlan plan = GroupedJoinedResultMapper.ResultMapperPlan.Resolve<TResult>(aliases);

        var results = new List<TResult>();

        using IDbCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        CommandObservation.Log(sql, _parameters.ToParameterObject());

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                TResult? result = GroupedJoinedResultMapper.MapResult<TResult>(reader, aliases, in plan);
                results.Add(result);
            }
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }

        return results;
    }

    private async Task<List<TResult>> ExecuteQueryAsync<TResult>(string sql, Expression<Func<IGrouping<TKey, T>, TResult>> selector, CancellationToken cancellationToken)
        => await CommandObservation.ExecuteAsync(
            sql, _parameters.ToParameterObject(), _connection, CommandType.Text,
            () => ExecuteQueryDirectAsync(sql, selector, cancellationToken), cancellationToken).ConfigureAwait(false);

    private async ValueTask<List<TResult>> ExecuteQueryDirectAsync<TResult>(string sql, Expression<Func<IGrouping<TKey, T>, TResult>> selector, CancellationToken cancellationToken)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var visitor = new GroupByExpressionVisitor<T, TKey>(_dialect, _groupByColumns);
        (string[] _, string[] aliases) = visitor.TranslateSelect(selector);
        GroupedJoinedResultMapper.ResultMapperPlan plan = GroupedJoinedResultMapper.ResultMapperPlan.Resolve<TResult>(aliases);

        var results = new List<TResult>();

        using DbCommand command = dbConn.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        CommandObservation.Log(sql, _parameters.ToParameterObject());

        bool wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                TResult? result = GroupedJoinedResultMapper.MapResult<TResult>(reader, aliases, in plan);
                results.Add(result);
            }
        }
        finally
        {
            if (wasClosed) dbConn.Close();
        }

        return results;
    }

    // AUD-R25: ResultMapperPlan, ResolveResultMapperPlan and MapResult used to live here as a
    // byte-for-byte private copy of GroupedJoinedResultMapper's, right down to the per-row
    // GetOrdinal call. ConvertColumnValue was already shared with that type "so they can't drift
    // out of sync with each other"; the rest now is too, so the ordinal caching added there
    // benefits this builder as well instead of leaving the two halves divergent.

    private string[] ExtractGroupByColumns(Expression<Func<T, TKey>> keySelector)
    {
        Expression body = keySelector.Body;

        // Handle Convert expressions (boxing)
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        // Single property: p => p.CategoryId
        if (body is MemberExpression member)
        {
            string columnName = GetColumnName(member.Member.Name);
            return [_dialect.EscapeColumnName(columnName)];
        }

        // Composite key: p => new { p.CategoryId, p.SupplierId }
        if (body is NewExpression newExpr)
        {
            string[] columns = new string[newExpr.Arguments.Count];

            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                Expression arg = newExpr.Arguments[i];

                if (arg is MemberExpression memberArg)
                {
                    string columnName = GetColumnName(memberArg.Member.Name);
                    columns[i] = _dialect.EscapeColumnName(columnName);
                }
                else
                {
                    throw new NotSupportedException("GROUP BY key must be property expressions.");
                }
            }
            return columns;
        }

        throw new NotSupportedException($"Cannot extract GROUP BY columns from expression type '{body.NodeType}'.");
    }

    private string TranslateHavingPredicate(Expression<Func<IGrouping<TKey, T>, bool>> predicate)
    {
        return TranslateHavingExpression(predicate.Body);
    }

    /// <summary>
    /// Recursively translates a HAVING predicate. Top-level and nested AndAlso/OrElse
    /// combinators (e.g. <c>g => g.Count() > 5 &amp;&amp; g.Sum(x => x.Foo) > 10</c>) are handled
    /// by translating both operands and joining them with the mapped SQL operator; comparison
    /// operators bottom out in <see cref="TranslateHavingOperand"/> for each side.
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
    /// query parameter rather than being inlined into the SQL text, matching how every WHERE
    /// value in this codebase is parameterized.
    /// </summary>
    private string TranslateHavingOperand(Expression expr)
    {
        if (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expr = unary.Operand;

        // g.Count() > 5
        if (expr is MethodCallExpression methodCall)
        {
            string methodName = methodCall.Method.Name;

            return methodName switch
            {
                "Count" when methodCall.Arguments.Count == 0 => "COUNT(*)",
                "Count" => BuildHavingAggregate("COUNT", methodCall.Arguments[0]),
                "Sum" => BuildHavingAggregate("SUM", methodCall.Arguments[0]),
                "Avg" => BuildHavingAggregate("AVG", methodCall.Arguments[0]),
                "Min" => BuildHavingAggregate("MIN", methodCall.Arguments[0]),
                "Max" => BuildHavingAggregate("MAX", methodCall.Arguments[0]),
                _ => throw new NotSupportedException($"Method '{methodName}' is not supported in HAVING.")
            };
        }

        // Constants
        if (expr is ConstantExpression constant)
            return AddHavingParameter(constant.Value);

        // Captured local variables, method parameters, and other closed-over values
        // (e.g. `.Having(g => g.Count() > minFilms)`) compile to a MemberExpression
        // over a compiler-generated closure class, not a ConstantExpression. Evaluate
        // it the same way WhereExpressionVisitor/JoinExpressionVisitor/etc. already do.
        if (expr is MemberExpression or UnaryExpression)
            return AddHavingParameter(HavingExpressionHelpers.EvaluateExpression(expr));

        throw new NotSupportedException($"HAVING expression type '{expr.NodeType}' is not supported.");
    }

    /// <summary>
    /// Adds a HAVING operand value as a bound query parameter and returns its placeholder name,
    /// instead of inlining it into the SQL text (which previously quote-doubled strings and
    /// left the value vulnerable to injection/culture-formatting bugs).
    /// </summary>
    private string AddHavingParameter(object? value)
    {
        string name = $"{_dialect.ParameterPrefix}hp{_havingParamSeq++}";
        _parameters.Add(name, value);
        return name;
    }

    private string BuildHavingAggregate(string aggregate, Expression selectorExpr)
    {
        if (selectorExpr is UnaryExpression unary)
            selectorExpr = unary.Operand;

        if (selectorExpr is LambdaExpression lambda)
        {
            Expression body = lambda.Body;

            if (body is UnaryExpression unaryBody)
                body = unaryBody.Operand;

            if (body is MemberExpression memberExpr)
            {
                string columnName = GetColumnName(memberExpr.Member.Name);
                return $"{aggregate}({_dialect.EscapeColumnName(columnName)})";
            }
        }

        throw new NotSupportedException("Cannot extract column from HAVING aggregate expression.");
    }

    private string GetColumnName(string propertyName)
    {
        IReadOnlyList<ColumnMetadata> columns = _metadata.Columns;

        for (int i = 0; i < columns.Count; i++)
            if (columns[i].PropertyName == propertyName)
                return columns[i].ColumnName;

        return propertyName;
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
    /// Binds all accumulated parameters directly to the command via raw ADO.NET. Unlike
    /// QueryBuilder/CteBuilder/SetOperationBuilder, which execute through Jaunty's core
    /// Query&lt;T&gt;/ParameterBinder, this builder binds directly to support arbitrary projected
    /// shapes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26 (batch 5, medium/bug). This used to route every value through a local
    /// <c>NormalizeForBinding</c> that coerced any <see cref="decimal"/> to <see cref="double"/> -
    /// <c>value is decimal d ? (double)d : value</c> - unconditionally and on every dialect, on the
    /// strength of one provider's behaviour. It now asks the dialect, via
    /// <see cref="DecimalParameterBinding"/>: SQLite still gets the conversion, and SQL Server,
    /// PostgreSQL and MySQL no longer have a <c>DECIMAL(19,4)</c> or <c>NUMERIC</c> comparison
    /// downgraded to binary floating point on another engine's behalf.
    /// </para>
    /// <para>
    /// This builder is where the conversion earns its place, and it is why removing it outright did
    /// not survive. Every parameter it binds for a HAVING clause is compared against an
    /// <em>aggregate expression</em>, which is exactly the case both SQLite providers get wrong:
    /// they bind a <see cref="decimal"/> as TEXT, SQLite has no column affinity to apply to an
    /// expression operand, and TEXT sorts above every number - so
    /// <c>HAVING SUM(price) &gt; @p</c> matches no group and <c>&lt; @p</c> matches every group,
    /// whatever the values are. Four <c>GroupBy</c>/<c>Having</c> integration tests turn red without
    /// it. See <see cref="IDecimalBindingDialect"/> for the measurements.
    /// </para>
    /// </remarks>
    private void BindParameters(IDbCommand command)
    {
        foreach ((string name, object? value) in _parameters.GetAll())
        {
            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = DecimalParameterBinding.Normalize(_dialect, value) ?? DBNull.Value;
            command.Parameters.Add(p);
        }
    }

}
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

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
    {
        var visitor = new GroupByExpressionVisitor<T, TKey>(_dialect, _groupByColumns);
        (string[] _, string[] aliases) = visitor.TranslateSelect(selector);
        ResultMapperPlan plan = ResolveResultMapperPlan<TResult>(aliases);

        var results = new List<TResult>();

        using IDbCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                TResult? result = MapResult<TResult>(reader, aliases, in plan);
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
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var visitor = new GroupByExpressionVisitor<T, TKey>(_dialect, _groupByColumns);
        (string[] _, string[] aliases) = visitor.TranslateSelect(selector);
        ResultMapperPlan plan = ResolveResultMapperPlan<TResult>(aliases);

        var results = new List<TResult>();

        using DbCommand command = dbConn.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                TResult? result = MapResult<TResult>(reader, aliases, in plan);
                results.Add(result);
            }
        }
        finally
        {
            if (wasClosed) dbConn.Close();
        }

        return results;
    }

    /// <summary>
    /// Constructor/property lookups resolved once per query execution and reused across every
    /// row, instead of re-running reflection (GetConstructors/GetProperty) per row.
    /// </summary>
    private readonly struct ResultMapperPlan
    {
        public ResultMapperPlan(ConstructorInfo? constructor, ParameterInfo[]? constructorParameters, PropertyInfo?[]? properties)
        {
            Constructor = constructor;
            ConstructorParameters = constructorParameters;
            Properties = properties;
        }

        public ConstructorInfo? Constructor { get; }
        public ParameterInfo[]? ConstructorParameters { get; }
        public PropertyInfo?[]? Properties { get; }
    }

    private static ResultMapperPlan ResolveResultMapperPlan<TResult>(string[] aliases)
    {
        Type resultType = typeof(TResult);

        // For anonymous types, we need to use the constructor
#pragma warning disable IL2090 // Reflection on generic parameter for result mapping
        if (resultType.Name.StartsWith("<>") || resultType.GetConstructors().Any(c => c.GetParameters().Length == aliases.Length))
        {
            ConstructorInfo? constructor = resultType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == aliases.Length);

            if (constructor is not null)
                return new ResultMapperPlan(constructor, constructor.GetParameters(), properties: null);
        }

        // For regular classes/structs
        var properties = new PropertyInfo?[aliases.Length];
        for (int i = 0; i < aliases.Length; i++)
            properties[i] = resultType.GetProperty(aliases[i]);
#pragma warning restore IL2090

        return new ResultMapperPlan(constructor: null, constructorParameters: null, properties);
    }

    private static TResult MapResult<TResult>(IDataReader reader, string[] aliases, in ResultMapperPlan plan)
    {
        if (plan.Constructor is not null)
        {
            var values = new object?[aliases.Length];
            ParameterInfo[] parameters = plan.ConstructorParameters!;

            for (int i = 0; i < aliases.Length; i++)
            {
                int ordinal = reader.GetOrdinal(aliases[i]);

                if (!reader.IsDBNull(ordinal))
                {
                    object value = reader.GetValue(ordinal);
                    Type targetType = parameters[i].ParameterType;
                    Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    values[i] = GroupedJoinedResultMapper.ConvertColumnValue(value, underlyingType);
                }
            }

            return (TResult)plan.Constructor.Invoke(values);
        }

#pragma warning disable IL2091 // Activator.CreateInstance requires public parameterless constructor
        TResult? instance = Activator.CreateInstance<TResult>();
#pragma warning restore IL2091
        PropertyInfo?[] properties = plan.Properties!;

        for (int i = 0; i < aliases.Length; i++)
        {
            PropertyInfo? property = properties[i];

            if (property is not null && property.CanWrite)
            {
                int ordinal = reader.GetOrdinal(aliases[i]);

                if (!reader.IsDBNull(ordinal))
                {
                    object value = reader.GetValue(ordinal);
                    Type targetType = property.PropertyType;
                    Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    object converted = GroupedJoinedResultMapper.ConvertColumnValue(value, underlyingType);
                    property.SetValue(instance, converted);
                }
            }
        }

        return instance;
    }

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

    private void BindParameters(IDbCommand command)
    {
        foreach ((string name, object? value) in _parameters.GetAll())
        {
            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = NormalizeForBinding(value) ?? DBNull.Value;
            command.Parameters.Add(p);
        }
    }

    /// <summary>
    /// Some ADO.NET providers (observed with System.Data.SQLite) don't correctly compare a
    /// bound <see cref="decimal"/> parameter against a REAL/numeric column - the comparison
    /// silently never matches regardless of value (e.g. a HAVING "SUM(price) > @p" with @p
    /// bound as decimal 150m). SQLite's native numeric storage is INTEGER/REAL (double), so
    /// normalize decimal values to double before binding. Unlike QueryBuilder/CteBuilder/
    /// SetOperationBuilder, which execute through Jaunty's core Query&lt;T&gt;/ParameterBinder,
    /// this builder binds parameters directly via raw ADO.NET (to support arbitrary projected
    /// TResult shapes), so it doesn't benefit from any type handling that path may apply.
    /// </summary>
    private static object? NormalizeForBinding(object? value) => value is decimal d ? (double)d : value;
}
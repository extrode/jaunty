using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Dialects;
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
    private readonly List<string> _havingConditions = new();

    internal GroupedQueryBuilder(
        IDbConnection connection,
        ISqlDialect dialect,
        List<WhereCondition> whereConditions,
        ParameterCollection parameters,
        Expression<Func<T, TKey>> keySelector)
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

    private string BuildSelectSql<TResult>(Expression<Func<IGrouping<TKey, T>, TResult>> selector)
    {
        var visitor = new GroupByExpressionVisitor<T, TKey>(_dialect, _groupByColumns);
        var (selectColumns, _) = visitor.TranslateSelect(selector);

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
            for (int i = 0; i < _whereConditions.Count; i++)
            {
                var condition = _whereConditions[i];
                if (i > 0)
                {
                    sb.Append(condition.Operator == LogicalOperator.Or ? " OR " : " AND ");
                }
                sb.Append(condition.Sql);
            }
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
        var (_, aliases) = visitor.TranslateSelect(selector);

        var results = new List<TResult>();

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            var resultType = typeof(TResult);

            while (reader.Read())
            {
                var result = MapResult<TResult>(reader, aliases, selector);
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
        {
            return ExecuteQuery<TResult>(sql, selector);
        }

        var visitor = new GroupByExpressionVisitor<T, TKey>(_dialect, _groupByColumns);
        var (_, aliases) = visitor.TranslateSelect(selector);

        var results = new List<TResult>();

        using var command = dbConn.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var result = MapResult<TResult>(reader, aliases, selector);
                results.Add(result);
            }
        }
        finally
        {
            if (wasClosed) dbConn.Close();
        }

        return results;
    }

    private TResult MapResult<TResult>(IDataReader reader, string[] aliases, Expression<Func<IGrouping<TKey, T>, TResult>> selector)
    {
        var resultType = typeof(TResult);

        // For anonymous types, we need to use the constructor
        if (resultType.Name.StartsWith("<>") || resultType.GetConstructors().Any(c => c.GetParameters().Length == aliases.Length))
        {
            var values = new object?[aliases.Length];
            var constructor = resultType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == aliases.Length);

            if (constructor != null)
            {
                var parameters = constructor.GetParameters();
                for (int i = 0; i < aliases.Length; i++)
                {
                    var ordinal = reader.GetOrdinal(aliases[i]);
                    if (!reader.IsDBNull(ordinal))
                    {
                        var value = reader.GetValue(ordinal);
                        var targetType = parameters[i].ParameterType;
                        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                        values[i] = Convert.ChangeType(value, underlyingType);
                    }
                }
                return (TResult)constructor.Invoke(values);
            }
        }

        // For regular classes/structs
        var instance = Activator.CreateInstance<TResult>();
        for (int i = 0; i < aliases.Length; i++)
        {
            var property = resultType.GetProperty(aliases[i]);
            if (property != null && property.CanWrite)
            {
                var ordinal = reader.GetOrdinal(aliases[i]);
                if (!reader.IsDBNull(ordinal))
                {
                    var value = reader.GetValue(ordinal);
                    var targetType = property.PropertyType;
                    var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    var converted = Convert.ChangeType(value, underlyingType);
                    property.SetValue(instance, converted);
                }
            }
        }
        return instance;
    }

    private string[] ExtractGroupByColumns(Expression<Func<T, TKey>> keySelector)
    {
        var body = keySelector.Body;

        // Handle Convert expressions (boxing)
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        // Single property: p => p.CategoryId
        if (body is MemberExpression member)
        {
            var columnName = GetColumnName(member.Member.Name);
            return new[] { _dialect.EscapeColumnName(columnName) };
        }

        // Composite key: p => new { p.CategoryId, p.SupplierId }
        if (body is NewExpression newExpr)
        {
            var columns = new string[newExpr.Arguments.Count];
            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var arg = newExpr.Arguments[i];
                if (arg is MemberExpression memberArg)
                {
                    var columnName = GetColumnName(memberArg.Member.Name);
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
        var body = predicate.Body;

        if (body is BinaryExpression binary)
        {
            var left = TranslateHavingExpression(binary.Left);
            var right = TranslateHavingExpression(binary.Right);
            var op = GetSqlOperator(binary.NodeType);
            return $"{left} {op} {right}";
        }

        throw new NotSupportedException($"HAVING predicate type '{body.NodeType}' is not supported.");
    }

    private string TranslateHavingExpression(Expression expr)
    {
        // g.Count() > 5
        if (expr is MethodCallExpression methodCall)
        {
            var methodName = methodCall.Method.Name;
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
        {
            if (constant.Value is null) return "NULL";
            if (constant.Value is string s) return $"'{s.Replace("'", "''")}'";
            if (constant.Value is bool b) return b ? "1" : "0";
            return constant.Value.ToString() ?? "NULL";
        }

        throw new NotSupportedException($"HAVING expression type '{expr.NodeType}' is not supported.");
    }

    private string BuildHavingAggregate(string aggregate, Expression selectorExpr)
    {
        if (selectorExpr is UnaryExpression unary)
            selectorExpr = unary.Operand;

        if (selectorExpr is LambdaExpression lambda)
        {
            var body = lambda.Body;
            if (body is UnaryExpression unaryBody)
                body = unaryBody.Operand;

            if (body is MemberExpression memberExpr)
            {
                var columnName = GetColumnName(memberExpr.Member.Name);
                return $"{aggregate}({_dialect.EscapeColumnName(columnName)})";
            }
        }

        throw new NotSupportedException("Cannot extract column from HAVING aggregate expression.");
    }

    private string GetColumnName(string propertyName)
    {
        var columns = _metadata.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
                return columns[i].ColumnName;
        }
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
        var paramObj = _parameters.ToParameterObject();
        if (paramObj is IDictionary<string, object?> dict)
        {
            foreach (var kvp in dict)
            {
                var p = command.CreateParameter();
                p.ParameterName = kvp.Key;
                p.Value = kvp.Value ?? DBNull.Value;
                command.Parameters.Add(p);
            }
        }
    }
}

using System.Data;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent;

/// <summary>
/// Builder for CTE (Common Table Expression) queries.
/// </summary>
internal sealed class CteBuilder<T> : ICteClause<T>, ICteQueryClause<T> where T : new()
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly CachedDialectMetadata _cache;
    private readonly string _cteName;
    private readonly ParameterCollection _parameters = new();
    private readonly Dictionary<string, int> _whereParamCounts = new(StringComparer.OrdinalIgnoreCase);

    private string? _cteDefinitionSql;
    private readonly List<WhereCondition> _whereConditions = new();
    private readonly List<OrderByColumn> _orderByColumns = new();
    private int? _takeCount;
    private int? _skipCount;

    internal CteBuilder(IDbConnection connection, string cteName)
    {
        // Validate the CTE name is a safe plain identifier before it is ever
        // interpolated into generated SQL (prevents SQL injection via the name).
        global::Jaunty.Dialects.SqlIdentifierValidator.Validate(cteName, nameof(cteName));

        _connection = connection;
        _dialect = SqlDialectFactory.GetDialect(connection);
        _cache = FluentMetadataCache.GetForDialect<T>(_dialect);
        _cteName = cteName;
    }

    #region ICteClause implementation

    public ICteQueryClause<T> As(Func<IFromClause<T>, IWhereClause<T>> queryBuilder)
    {
        var innerQuery = new QueryBuilder<T>(_connection, null);
        IWhereClause<T> result = queryBuilder(innerQuery);

        // Get the SQL from the inner query (without executing)
        var sql = ((IWhereClause<T>)result).ToSql();

        // Copy parameters from inner query, renamed to a unique prefix so they can never
        // collide with a name this CteBuilder's own Where/And/Or calls generate later
        // (see ParameterRenamer.Rename).
        if (result is QueryBuilder<T> qb)
        {
            ParameterCollection innerParams = qb.GetParameters();
            (sql, ParameterCollection renamedParams) = ParameterRenamer.Rename(sql, innerParams, "cte_src");
            foreach ((string Name, object? Value) param in renamedParams.GetAll())
            {
                _parameters.Add(param.Name, param.Value);
            }
        }

        _cteDefinitionSql = sql;

        return this;
    }

    public ICteQueryClause<T> As(IWhereClause<T> query)
    {
        var sql = query.ToSql();

        // Copy parameters from the query, renamed to a unique prefix so they can never
        // collide with a name this CteBuilder's own Where/And/Or calls generate later
        // (see ParameterRenamer.Rename).
        if (query is QueryBuilder<T> qb)
        {
            ParameterCollection innerParams = qb.GetParameters();
            (sql, ParameterCollection renamedParams) = ParameterRenamer.Rename(sql, innerParams, "cte_src");
            foreach ((string Name, object? Value) param in renamedParams.GetAll())
            {
                _parameters.Add(param.Name, param.Value);
            }
        }

        _cteDefinitionSql = sql;

        return this;
    }

    #endregion

    #region ICteQueryClause implementation

    public ICteQueryClause<T> Where(Expression<Func<T, bool>> predicate)
    {
        var visitor = new WhereExpressionVisitor<T>(_dialect, _whereParamCounts);
        (string? sql, List<(string Name, object? Value)>? parameters) = visitor.Translate(predicate);
        LogicalOperator op = _whereConditions.Count == 0 ? LogicalOperator.None : LogicalOperator.And;
        _whereConditions.Add(WhereCondition.Expression(sql, op));
        _parameters.AddRange(parameters);
        return this;
    }

    public ICteQueryClause<T> Where(string column, object? value)
    {
        var escapedColumn = _dialect.EscapeColumnName(column);
        LogicalOperator op = _whereConditions.Count == 0 ? LogicalOperator.None : LogicalOperator.And;

        if (value is null)
        {
            // A bound null becomes DBNull, and "col = NULL" is UNKNOWN for every row under SQL's
            // three-valued logic - so the query silently returned nothing instead of the rows where
            // the column IS NULL. Every other string-column predicate in the assembly branches here
            // (QueryBuilder's six Where/And/Or overloads and their IUpdateWhereClause counterparts);
            // this was the one that didn't.
            _whereConditions.Add(WhereCondition.Column($"{escapedColumn} IS NULL", op));
            return this;
        }

        var paramName = $"{_dialect.ParameterPrefix}cte_p{_parameters.Count}";
        _parameters.Add(paramName, value);
        _whereConditions.Add(WhereCondition.Column($"{escapedColumn} = {paramName}", op));
        return this;
    }

    public ICteQueryClause<T> And(Expression<Func<T, bool>> predicate)
    {
        var visitor = new WhereExpressionVisitor<T>(_dialect, _whereParamCounts);
        (string? sql, List<(string Name, object? Value)>? parameters) = visitor.Translate(predicate);
        _whereConditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        _parameters.AddRange(parameters);
        return this;
    }

    public ICteQueryClause<T> Or(Expression<Func<T, bool>> predicate)
    {
        var visitor = new WhereExpressionVisitor<T>(_dialect, _whereParamCounts);
        (string? sql, List<(string Name, object? Value)>? parameters) = visitor.Translate(predicate);
        _whereConditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        _parameters.AddRange(parameters);
        return this;
    }

    public ICteQueryClause<T> OrderBy<TKey>(Expression<Func<T, TKey>> selector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(selector);
        // Already dialect-escaped (GetColumnNameFromProperty is backed by the pre-escaped
        // CachedDialectMetadata cache) - escaping again here would double-escape (and throw
        // for a keyword-named column, since SqlIdentifierValidator rejects the bracketed/
        // quoted text on the second pass).
        string columnName = GetColumnNameFromProperty(propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, false));
        return this;
    }

    public ICteQueryClause<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> selector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(selector);
        // Already dialect-escaped - see comment in the OrderBy(ascending) overload above.
        string columnName = GetColumnNameFromProperty(propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, true));
        return this;
    }

    public ICteQueryClause<T> Take(int count)
    {
        _takeCount = count;
        return this;
    }

    public ICteQueryClause<T> Skip(int count)
    {
        _skipCount = count;
        return this;
    }

    public List<T> Select()
    {
        var sql = BuildSql();
        return _connection.Query<T>(sql, _parameters.ToParameterObject()!);
    }

    public async Task<List<T>> SelectAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildSql();
        return await _connection.QueryAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public T SelectFirst()
    {
        // Save and restore rather than assign: a CteBuilder is exactly the kind of object a caller
        // holds onto and reuses, since building the CTE definition is the expensive part. Leaving
        // _takeCount at 1 meant a later Select()/ToSql() on the same instance silently returned one
        // row. QueryBuilder's 24 first/single terminals and SetOperationBuilder's 18 all restore;
        // these two were the only ones that didn't.
        var original = _takeCount;
        _takeCount = 1;
        var sql = BuildSql();
        _takeCount = original;
        return _connection.QueryFirst<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectFirstOrDefault()
    {
        var original = _takeCount;
        _takeCount = 1;
        var sql = BuildSql();
        _takeCount = original;
        return _connection.QueryFirstOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    public string ToSql() => BuildSql();

    #endregion

    #region Private helpers

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

    private string BuildSql()
    {
        if (string.IsNullOrEmpty(_cteDefinitionSql))
            throw new InvalidOperationException("CTE definition is required. Call As() before Select().");

        var sb = new StringBuilder(512);

        // WITH clause - CTE name is validated as a plain identifier in the constructor.
        sb.Append("WITH ");
        sb.Append(_cteName);
        sb.Append(" AS (");
        sb.Append(_cteDefinitionSql);
        sb.Append(") ");

        // Main SELECT from CTE
        sb.Append("SELECT * FROM ");
        sb.Append(_cteName);

        // WHERE clause (on CTE results)
        if (_whereConditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_whereConditions));
        }

        // ORDER BY
        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");
            for (int i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(_orderByColumns[i].ColumnName);
                if (_orderByColumns[i].Descending) sb.Append(" DESC");
            }
        }

        // LIMIT/OFFSET - use dialect's paging
        if (_skipCount.HasValue || _takeCount.HasValue)
        {
            var baseSql = sb.ToString();
            return _dialect.GetPagingSql(baseSql, _skipCount ?? 0, _takeCount ?? int.MaxValue);
        }

        return sb.ToString();
    }

    private string GetColumnNameFromProperty(string propertyName) => _cache.GetColumnName(propertyName);

    #endregion
}
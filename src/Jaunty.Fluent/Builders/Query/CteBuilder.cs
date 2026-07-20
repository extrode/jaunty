using System.Data;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Builder for CTE (Common Table Expression) queries.
/// </summary>
internal sealed class CteBuilder<T> : ICteClause<T>, ICteQueryClause<T> where T : new()
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _metadata;
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
        _metadata = FluentMetadataCache.GetMetadata<T>();
        _cache = FluentMetadataCache.GetForDialect<T>(_dialect);
        _cteName = cteName;
    }

    #region ICteClause implementation

    public ICteQueryClause<T> As(Func<IFromClause<T>, IWhereClause<T>> queryBuilder)
    {
        var innerQuery = new QueryBuilder<T>(_connection, null);
        IWhereClause<T> result = queryBuilder(innerQuery);

        // Get the SQL from the inner query (without executing)
        _cteDefinitionSql = ((IWhereClause<T>)result).ToSql();

        // Copy parameters from inner query
        if (result is QueryBuilder<T> qb)
        {
            ParameterCollection innerParams = qb.GetParameters();
            foreach ((string Name, object? Value) param in innerParams.GetAll())
            {
                _parameters.Add(param.Name, param.Value);
            }
        }

        return this;
    }

    public ICteQueryClause<T> As(IWhereClause<T> query)
    {
        _cteDefinitionSql = query.ToSql();

        // Copy parameters from the query
        if (query is QueryBuilder<T> qb)
        {
            ParameterCollection innerParams = qb.GetParameters();
            foreach ((string Name, object? Value) param in innerParams.GetAll())
            {
                _parameters.Add(param.Name, param.Value);
            }
        }

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
        var paramName = $"@cte_p{_parameters.Count}";
        _parameters.Add(paramName, value);
        var sql = $"{_dialect.EscapeColumnName(column)} = {paramName}";
        LogicalOperator op = _whereConditions.Count == 0 ? LogicalOperator.None : LogicalOperator.And;
        _whereConditions.Add(WhereCondition.Column(sql, op));
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
        _takeCount = 1;
        var sql = BuildSql();
        return _connection.QueryFirst<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectFirstOrDefault()
    {
        _takeCount = 1;
        var sql = BuildSql();
        return _connection.QueryFirstOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    public string ToSql() => BuildSql();

    #endregion

    #region Private helpers

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
            for (int i = 0; i < _whereConditions.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(_whereConditions[i].Operator == LogicalOperator.Or ? " OR " : " AND ");
                }
                sb.Append(_whereConditions[i].Sql);
            }
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
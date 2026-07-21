using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Configuration;

namespace Jaunty.Fluent;

/// <summary>
/// Main query builder implementation. Implements all fluent interfaces.
/// </summary>
internal sealed class QueryBuilder<T> : IFromClause<T>, IWhereClause<T>, IOrderByClause<T>, IDistinctClause<T>, ISetClause<T>, IUpdateWhereClause<T>
    where T : new()
{
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

    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _metadata;
    private readonly CachedDialectMetadata _cache;
    private readonly string? _alias;
    private readonly List<WhereCondition> _conditions = new();
    private readonly List<OrderByColumn> _orderByColumns = new();
    private readonly ParameterCollection _parameters = new();
    private readonly Dictionary<string, int> _whereParamCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SetColumn> _setColumns = new();
    private bool _distinct;
    private int? _take;
    private int? _skip;

    internal QueryBuilder(IDbConnection connection, string? alias = null)
    {
        _connection = connection;
        _dialect = SqlDialectFactory.GetDialect(connection);
        _metadata = FluentMetadataCache.GetMetadata<T>();
        _cache = FluentMetadataCache.GetForDialect<T>(_dialect);
        _alias = alias;
    }

    internal IDbConnection Connection => _connection;
    internal ISqlDialect Dialect => _dialect;
    internal string? Alias => _alias;
    internal string TableName => _metadata.TableName;
    internal string? SchemaName => _metadata.SchemaName;

    /// <summary>
    /// Gets a copy of the current parameters for set operations.
    /// </summary>
    internal ParameterCollection GetParameters() => _parameters.Clone();

    private string[] GetAllColumnNames() => _cache.ColumnNames.ToArray();

    private string[] ResolveColumns(Expression<Func<T, object?>>[] expressions)
    {
        var propertyNames = PropertyExtractor.ExtractPropertyNames(expressions);
        var columnNames = new string[propertyNames.Length];
        for (int i = 0; i < propertyNames.Length; i++)
        {
            columnNames[i] = GetColumnNameFromProperty(propertyNames[i]);
        }
        return columnNames;
    }

    /// <summary>
    /// Escapes caller-supplied raw column-name strings (e.g. SelectPartial(params string[])),
    /// matching the Where(string column, ...) overload's convention - unlike ResolveColumns()/
    /// GetAllColumnNames(), these strings come straight from the caller and aren't pre-escaped
    /// by CachedDialectMetadata.
    /// </summary>
    private string[] EscapeColumns(string[] columns)
    {
        var escaped = new string[columns.Length];
        for (int i = 0; i < columns.Length; i++)
        {
            escaped[i] = _dialect.EscapeColumnName(columns[i]);
        }
        return escaped;
    }

    #region WHERE clause

    public IWhereClause<T> Where(string column, object? value)
    {
        var escapedColumn = _dialect.EscapeColumnName(column);
        var paramName = $"{_dialect.ParameterPrefix}{column}";

        if (value is null)
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} IS NULL", LogicalOperator.None));
        }
        else
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} = {paramName}", LogicalOperator.None));
            _parameters.Add(paramName, value);
        }
        return this;
    }

    public IWhereClause<T> Where(Expression<Func<T, bool>> predicate)
    {
        var visitor = new WhereExpressionVisitor<T>(_dialect, _whereParamCounts);
        (string? sql, List<(string Name, object? Value)>? parameters) = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        _parameters.AddRange(parameters);
        return this;
    }

    public IWhereClause<T> WhereRaw(string rawSql)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.None));
        return this;
    }

    public IWhereClause<T> WhereRaw(string rawSql, object parameters)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.None));
        AddParametersFromObject(parameters);
        return this;
    }

    public IWhereClause<T> And(string column, object? value)
    {
        var escapedColumn = _dialect.EscapeColumnName(column);
        var paramName = GetUniqueParamName(column);

        if (value is null)
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} IS NULL", LogicalOperator.And));
        }
        else
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} = {paramName}", LogicalOperator.And));
            _parameters.Add(paramName, value);
        }
        return this;
    }

    public IWhereClause<T> And(Expression<Func<T, bool>> predicate)
    {
        var visitor = new WhereExpressionVisitor<T>(_dialect, _whereParamCounts);
        (string? sql, List<(string Name, object? Value)>? parameters) = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        _parameters.AddRange(parameters);
        return this;
    }

    public IWhereClause<T> AndRaw(string rawSql)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.And));
        return this;
    }

    public IWhereClause<T> AndRaw(string rawSql, object parameters)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.And));
        AddParametersFromObject(parameters);
        return this;
    }

    public IWhereClause<T> Or(string column, object? value)
    {
        var escapedColumn = _dialect.EscapeColumnName(column);
        var paramName = GetUniqueParamName(column);

        if (value is null)
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} IS NULL", LogicalOperator.Or));
        }
        else
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} = {paramName}", LogicalOperator.Or));
            _parameters.Add(paramName, value);
        }
        return this;
    }

    public IWhereClause<T> Or(Expression<Func<T, bool>> predicate)
    {
        var visitor = new WhereExpressionVisitor<T>(_dialect, _whereParamCounts);
        (string? sql, List<(string Name, object? Value)>? parameters) = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        _parameters.AddRange(parameters);
        return this;
    }

    public IWhereClause<T> OrRaw(string rawSql)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.Or));
        return this;
    }

    public IWhereClause<T> OrRaw(string rawSql, object parameters)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.Or));
        AddParametersFromObject(parameters);
        return this;
    }

    // WHERE IN / NOT IN
    public IWhereClause<T> WhereIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        var sql = BuildInClause(selector, values, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IWhereClause<T> WhereNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        var sql = BuildInClause(selector, values, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IWhereClause<T> AndIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        var sql = BuildInClause(selector, values, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    public IWhereClause<T> AndNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        var sql = BuildInClause(selector, values, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    public IWhereClause<T> OrIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        var sql = BuildInClause(selector, values, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    public IWhereClause<T> OrNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        var sql = BuildInClause(selector, values, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    // WHERE BETWEEN / NOT BETWEEN
    public IWhereClause<T> WhereBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        var sql = BuildBetweenClause(selector, from, to, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IWhereClause<T> WhereNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        var sql = BuildBetweenClause(selector, from, to, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IWhereClause<T> AndBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        var sql = BuildBetweenClause(selector, from, to, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    public IWhereClause<T> AndNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        var sql = BuildBetweenClause(selector, from, to, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    public IWhereClause<T> OrBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        var sql = BuildBetweenClause(selector, from, to, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    public IWhereClause<T> OrNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        var sql = BuildBetweenClause(selector, from, to, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    // WHERE EXISTS / NOT EXISTS
    public IWhereClause<T> WhereExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new()
    {
        var sql = BuildExistsClause<TSubquery>(predicate, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IWhereClause<T> WhereNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new()
    {
        var sql = BuildExistsClause<TSubquery>(predicate, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IWhereClause<T> AndExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new()
    {
        var sql = BuildExistsClause<TSubquery>(predicate, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    public IWhereClause<T> AndNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new()
    {
        var sql = BuildExistsClause<TSubquery>(predicate, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    public IWhereClause<T> OrExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new()
    {
        var sql = BuildExistsClause<TSubquery>(predicate, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    public IWhereClause<T> OrNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate) where TSubquery : new()
    {
        var sql = BuildExistsClause<TSubquery>(predicate, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    // WHERE IN SUBQUERY / NOT IN SUBQUERY
    public IWhereClause<T> WhereInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new()
    {
        var sql = BuildInSubqueryClause(selector, subquerySelector, subquery, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IWhereClause<T> WhereNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new()
    {
        var sql = BuildInSubqueryClause(selector, subquerySelector, subquery, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IWhereClause<T> AndInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new()
    {
        var sql = BuildInSubqueryClause(selector, subquerySelector, subquery, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    public IWhereClause<T> AndNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new()
    {
        var sql = BuildInSubqueryClause(selector, subquerySelector, subquery, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    public IWhereClause<T> OrInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new()
    {
        var sql = BuildInSubqueryClause(selector, subquerySelector, subquery, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    public IWhereClause<T> OrNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery) where TSubquery : new()
    {
        var sql = BuildInSubqueryClause(selector, subquerySelector, subquery, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    #endregion

    #region ORDER BY clause

    IOrderByClause<T> IFromClause<T>.OrderBy(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        // Raw (unescaped) column name: _orderByColumns also receives raw strings from the
        // string-overload OrderBy(string) methods below, and BuildSelectSql/
        // BuildSelectSqlWithProjection escape every entry exactly once at render time. Using
        // the dialect-escaped GetColumnNameFromProperty here would double-escape (and, for a
        // keyword-named column, throw in SqlIdentifierValidator on the second pass).
        var columnName = GetColumnNameFromMetadata(_metadata, propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    IOrderByClause<T> IFromClause<T>.OrderByDescending(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        // Raw (unescaped) column name - see comment in the OrderBy(ascending) overload above.
        var columnName = GetColumnNameFromMetadata(_metadata, propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    IOrderByClause<T> IFromClause<T>.OrderBy(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: false));
        return this;
    }

    IOrderByClause<T> IFromClause<T>.OrderByDescending(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: true));
        return this;
    }

    IOrderByClause<T> IWhereClause<T>.OrderBy(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        // Raw (unescaped) column name: _orderByColumns also receives raw strings from the
        // string-overload OrderBy(string) methods below, and BuildSelectSql/
        // BuildSelectSqlWithProjection escape every entry exactly once at render time. Using
        // the dialect-escaped GetColumnNameFromProperty here would double-escape (and, for a
        // keyword-named column, throw in SqlIdentifierValidator on the second pass).
        var columnName = GetColumnNameFromMetadata(_metadata, propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    IOrderByClause<T> IWhereClause<T>.OrderByDescending(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        // Raw (unescaped) column name - see comment in the OrderBy(ascending) overload above.
        var columnName = GetColumnNameFromMetadata(_metadata, propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    IOrderByClause<T> IWhereClause<T>.OrderBy(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: false));
        return this;
    }

    IOrderByClause<T> IWhereClause<T>.OrderByDescending(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: true));
        return this;
    }

    IOrderByClause<T> IDistinctClause<T>.OrderBy(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        // Raw (unescaped) column name: _orderByColumns also receives raw strings from the
        // string-overload OrderBy(string) methods below, and BuildSelectSql/
        // BuildSelectSqlWithProjection escape every entry exactly once at render time. Using
        // the dialect-escaped GetColumnNameFromProperty here would double-escape (and, for a
        // keyword-named column, throw in SqlIdentifierValidator on the second pass).
        var columnName = GetColumnNameFromMetadata(_metadata, propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    IOrderByClause<T> IDistinctClause<T>.OrderByDescending(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        // Raw (unescaped) column name - see comment in the OrderBy(ascending) overload above.
        var columnName = GetColumnNameFromMetadata(_metadata, propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    IOrderByClause<T> IDistinctClause<T>.OrderBy(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: false));
        return this;
    }

    IOrderByClause<T> IDistinctClause<T>.OrderByDescending(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: true));
        return this;
    }

    public IOrderByClause<T> ThenBy(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        // Raw (unescaped) column name: _orderByColumns also receives raw strings from the
        // string-overload OrderBy(string) methods below, and BuildSelectSql/
        // BuildSelectSqlWithProjection escape every entry exactly once at render time. Using
        // the dialect-escaped GetColumnNameFromProperty here would double-escape (and, for a
        // keyword-named column, throw in SqlIdentifierValidator on the second pass).
        var columnName = GetColumnNameFromMetadata(_metadata, propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IOrderByClause<T> ThenByDescending(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        // Raw (unescaped) column name - see comment in the OrderBy(ascending) overload above.
        var columnName = GetColumnNameFromMetadata(_metadata, propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public IOrderByClause<T> ThenBy(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: false));
        return this;
    }

    public IOrderByClause<T> ThenByDescending(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: true));
        return this;
    }

    #endregion

    #region DISTINCT

    public IDistinctClause<T> Distinct()
    {
        _distinct = true;
        return this;
    }

    IWhereClause<T> IDistinctClause<T>.Where(string column, object? value) => Where(column, value);
    IWhereClause<T> IDistinctClause<T>.Where(Expression<Func<T, bool>> predicate) => Where(predicate);

    #endregion

    #region Take/Skip

    IFromClause<T> IFromClause<T>.Take(int count) { _take = count; return this; }
    IFromClause<T> IFromClause<T>.Skip(int count) { _skip = count; return this; }
    IWhereClause<T> IWhereClause<T>.Take(int count) { _take = count; return this; }
    IWhereClause<T> IWhereClause<T>.Skip(int count) { _skip = count; return this; }
    IOrderByClause<T> IOrderByClause<T>.Take(int count) { _take = count; return this; }
    IOrderByClause<T> IOrderByClause<T>.Skip(int count) { _skip = count; return this; }
    IDistinctClause<T> IDistinctClause<T>.Take(int count) { _take = count; return this; }
    IDistinctClause<T> IDistinctClause<T>.Skip(int count) { _skip = count; return this; }

    #endregion

    #region JOIN operations

    public IJoinClause<T, TJoin> InnerJoin<TJoin>(string? alias = null) where TJoin : new()
    {
        return new JoinClauseBuilder<T, TJoin>(this, JoinType.Inner, alias);
    }

    public IJoinClause<T, TJoin> LeftJoin<TJoin>(string? alias = null) where TJoin : new()
    {
        return new JoinClauseBuilder<T, TJoin>(this, JoinType.Left, alias);
    }

    public IJoinClause<T, TJoin> RightJoin<TJoin>(string? alias = null) where TJoin : new()
    {
        return new JoinClauseBuilder<T, TJoin>(this, JoinType.Right, alias);
    }

    #endregion

    #region GROUP BY

    public IGroupedQuery<T, TKey> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        return new GroupedQueryBuilder<T, TKey>(
            _connection,
            _dialect,
            _conditions,
            _parameters,
            keySelector);
    }

    #endregion

    #region Terminal operations (sync) - Full entity (strict mapping)

    public List<T> Select()
    {
        var sql = BuildSelectSql(GetAllColumnNames());
        return _connection.Query<T>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectFirst()
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QueryFirst<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectFirstOrDefault()
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QueryFirstOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectSingle()
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QuerySingle<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectSingleOrDefault()
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QuerySingleOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    #endregion

    #region Terminal operations (sync) - Partial entity (string columns)

    public List<T> SelectPartial(params string[] columns)
    {
        var sql = BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());
        return _connection.QueryPartial<T>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectPartialFirst(params string[] columns)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());
        _take = original;
        return _connection.QueryPartialFirst<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectPartialFirstOrDefault(params string[] columns)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());
        _take = original;
        return _connection.QueryPartialFirstOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectPartialSingle(params string[] columns)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());
        _take = original;
        return _connection.QueryPartialSingle<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectPartialSingleOrDefault(params string[] columns)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());
        _take = original;
        return _connection.QueryPartialSingleOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    #endregion

    #region Terminal operations (sync) - Partial entity (expression columns)

    public List<T> SelectPartial(params Expression<Func<T, object?>>[] columns)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var sql = BuildSelectSql(columnNames);
        return _connection.QueryPartial<T>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectPartialFirst(params Expression<Func<T, object?>>[] columns)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        return _connection.QueryPartialFirst<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectPartialFirstOrDefault(params Expression<Func<T, object?>>[] columns)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        return _connection.QueryPartialFirstOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectPartialSingle(params Expression<Func<T, object?>>[] columns)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        return _connection.QueryPartialSingle<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectPartialSingleOrDefault(params Expression<Func<T, object?>>[] columns)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        return _connection.QueryPartialSingleOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    #endregion

    #region Scalar aggregates (sync)

    public int Count()
    {
        var sql = BuildCountSql();
        // SQLite returns Int64 for COUNT, so we need to handle conversion
        var result = _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!);
        return (int)result;
    }

    public long LongCount()
    {
        var sql = BuildCountSql();
        return _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!);
    }

    public int Count<TResult>(Expression<Func<T, TResult>> selector)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("COUNT", columnName);
        // SQLite returns Int64 for COUNT
        var result = _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!);
        return (int)result;
    }

    public long LongCount<TResult>(Expression<Func<T, TResult>> selector)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("COUNT", columnName);
        return _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!);
    }

    public TResult Sum<TResult>(Expression<Func<T, TResult>> selector)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("SUM", columnName);
        return ConvertScalarResult<TResult>(_connection.QueryScalar<object>(sql, _parameters.ToParameterObject()!));
    }

    public double Avg<TResult>(Expression<Func<T, TResult>> selector)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("AVG", columnName);
        return _connection.QueryScalar<double>(sql, _parameters.ToParameterObject()!);
    }

    public TResult Min<TResult>(Expression<Func<T, TResult>> selector)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("MIN", columnName);
        return ConvertScalarResult<TResult>(_connection.QueryScalar<object>(sql, _parameters.ToParameterObject()!));
    }

    public TResult Max<TResult>(Expression<Func<T, TResult>> selector)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("MAX", columnName);
        return ConvertScalarResult<TResult>(_connection.QueryScalar<object>(sql, _parameters.ToParameterObject()!));
    }

    // SelectX aliases
    public int SelectCount() => Count();
    public int SelectCount<TResult>(Expression<Func<T, TResult>> selector) => Count(selector);
    public TResult SelectSum<TResult>(Expression<Func<T, TResult>> selector) => Sum(selector);
    public double SelectAvg<TResult>(Expression<Func<T, TResult>> selector) => Avg(selector);
    public TResult SelectMin<TResult>(Expression<Func<T, TResult>> selector) => Min(selector);
    public TResult SelectMax<TResult>(Expression<Func<T, TResult>> selector) => Max(selector);

    #endregion

    #region Terminal operations (async) - Full entity

    public async Task<List<T>> SelectAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectSql(GetAllColumnNames());
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectFirstAsync(CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryFirstAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryFirstOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectSingleAsync(CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QuerySingleAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QuerySingleOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Terminal operations (async) - Partial entity (string columns)

    public async Task<List<T>> SelectPartialAsync(string[] columns, CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryPartialAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectPartialFirstAsync(string[] columns, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryPartialFirstAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectPartialFirstOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryPartialFirstOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectPartialSingleAsync(string[] columns, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryPartialSingleAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectPartialSingleOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryPartialSingleOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Terminal operations (async) - Partial entity (expression columns)

    public async Task<List<T>> SelectPartialAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var sql = BuildSelectSql(columnNames);
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryPartialAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectPartialFirstAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryPartialFirstAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectPartialFirstOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryPartialFirstOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectPartialSingleAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryPartialSingleAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectPartialSingleOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryPartialSingleOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Scalar aggregates (async)

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildCountSql();
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        var result = await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return (int)result;
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildCountSql();
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("COUNT", columnName);
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        var result = await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return (int)result;
    }

    public async Task<long> LongCountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("COUNT", columnName);
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResult> SumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("SUM", columnName);
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        var result = await dbConn.QueryScalarAsync<object>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return ConvertScalarResult<TResult>(result);
    }

    public async Task<double> AvgAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("AVG", columnName);
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryScalarAsync<double>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("MIN", columnName);
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        var result = await dbConn.QueryScalarAsync<object>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return ConvertScalarResult<TResult>(result);
    }

    public async Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("MAX", columnName);
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        var result = await dbConn.QueryScalarAsync<object>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return ConvertScalarResult<TResult>(result);
    }

    // Async SelectX aliases
    public Task<int> SelectCountAsync(CancellationToken cancellationToken = default) => CountAsync(cancellationToken);
    public Task<int> SelectCountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => CountAsync(selector, cancellationToken);
    public Task<TResult> SelectSumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => SumAsync(selector, cancellationToken);
    public Task<double> SelectAvgAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => AvgAsync(selector, cancellationToken);
    public Task<TResult> SelectMinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => MinAsync(selector, cancellationToken);
    public Task<TResult> SelectMaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => MaxAsync(selector, cancellationToken);

    #endregion

    #region SQL introspection

    public string ToSql() => BuildSelectSql(GetAllColumnNames());

    public string ToSql(params string[] columns)
        => BuildSelectSql(columns.Length > 0 ? EscapeColumns(columns) : GetAllColumnNames());

    public string ToSql(params Expression<Func<T, object?>>[] columns)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        return BuildSelectSql(columnNames);
    }

    public string ToSql<TResult>(Expression<Func<T, TResult>> selector)
    {
        var visitor = new SelectExpressionVisitor<T>(_dialect);
        List<SelectColumn> selectColumns = visitor.Translate(selector);
        return BuildSelectSqlWithProjection(selectColumns);
    }

    #endregion

    #region Set operations (UNION, UNION ALL, EXCEPT, INTERSECT)

    public ISetOperationClause<T> Union(IQueryTerminal<T> other)
    {
        var builder = new SetOperationBuilder<T>(_connection, _dialect, ToSql(), _parameters.Clone());
        return builder.Union(other);
    }

    public ISetOperationClause<T> UnionAll(IQueryTerminal<T> other)
    {
        var builder = new SetOperationBuilder<T>(_connection, _dialect, ToSql(), _parameters.Clone());
        return builder.UnionAll(other);
    }

    public ISetOperationClause<T> Except(IQueryTerminal<T> other)
    {
        var builder = new SetOperationBuilder<T>(_connection, _dialect, ToSql(), _parameters.Clone());
        return builder.Except(other);
    }

    public ISetOperationClause<T> Intersect(IQueryTerminal<T> other)
    {
        var builder = new SetOperationBuilder<T>(_connection, _dialect, ToSql(), _parameters.Clone());
        return builder.Intersect(other);
    }

    #endregion

    #region Private helpers

    private string BuildSelectSql(string[] columns)
    {
        var sb = new StringBuilder(256);
        sb.Append("SELECT ");

        if (_distinct)
            sb.Append("DISTINCT ");

        // Columns - already dialect-escaped (callers pass ResolveColumns()/GetAllColumnNames(),
        // both backed by the pre-escaped CachedDialectMetadata cache).
        for (int i = 0; i < columns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(columns[i]);
        }

        // FROM
        sb.Append(" FROM ");
        sb.Append(_cache.EscapedTableName);

        // WHERE
        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        // ORDER BY
        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");
            for (int i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                OrderByColumn orderBy = _orderByColumns[i];
                sb.Append(_dialect.EscapeColumnName(orderBy.ColumnName));
                if (orderBy.Descending)
                    sb.Append(" DESC");
            }
        }

        // LIMIT/OFFSET - use dialect's paging
        if (_skip.HasValue || _take.HasValue)
        {
            var baseSql = sb.ToString();
            return _dialect.GetPagingSql(baseSql, _skip ?? 0, _take ?? int.MaxValue);
        }

        return sb.ToString();
    }

    private string BuildSelectSqlWithProjection(List<SelectColumn> columns)
    {
        var sb = new StringBuilder(256);
        sb.Append("SELECT ");

        if (_distinct)
            sb.Append("DISTINCT ");

        // Columns with aliases
        for (int i = 0; i < columns.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            SelectColumn col = columns[i];
            sb.Append(col.Sql);
            // Add alias if SQL doesn't match alias (i.e., not just a column reference)
            if (!col.Sql.Equals(_dialect.EscapeColumnName(col.Alias), StringComparison.OrdinalIgnoreCase) &&
                !col.Sql.Equals(col.Alias, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" AS ");
                sb.Append(col.Alias);
            }
        }

        // FROM
        sb.Append(" FROM ");
        sb.Append(_cache.EscapedTableName);

        // WHERE
        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        // ORDER BY
        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");
            for (int i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                OrderByColumn orderBy = _orderByColumns[i];
                sb.Append(_dialect.EscapeColumnName(orderBy.ColumnName));
                if (orderBy.Descending)
                    sb.Append(" DESC");
            }
        }

        // LIMIT/OFFSET - use dialect's paging
        if (_skip.HasValue || _take.HasValue)
        {
            var baseSql = sb.ToString();
            return _dialect.GetPagingSql(baseSql, _skip ?? 0, _take ?? int.MaxValue);
        }

        return sb.ToString();
    }

    private string BuildCountSql()
    {
        var sb = new StringBuilder(128);
        sb.Append("SELECT COUNT(*)");

        // FROM
        sb.Append(" FROM ");
        sb.Append(_cache.EscapedTableName);

        // WHERE
        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        return sb.ToString();
    }

    private string BuildAggregateSql(string aggregateFunction, string columnName)
    {
        var sb = new StringBuilder(128);
        sb.Append("SELECT ");
        sb.Append(aggregateFunction);
        sb.Append('(');
        // columnName is already dialect-escaped - every call site passes the result of
        // GetColumnNameFromSelector, which is backed by the pre-escaped CachedDialectMetadata.
        sb.Append(columnName);
        sb.Append(')');

        // FROM
        sb.Append(" FROM ");
        sb.Append(_cache.EscapedTableName);

        // WHERE
        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        return sb.ToString();
    }

    private string GetColumnNameFromSelector<TResult>(Expression<Func<T, TResult>> selector)
    {
        var propertyName = PropertyExtractor.ExtractPropertyName(selector);
        return GetColumnNameFromProperty(propertyName);
    }

    private static TResult ConvertScalarResult<TResult>(object value)
    {
        if (value is null or DBNull)
            return default!;

        Type targetType = typeof(TResult);
        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Handle conversion from database types to C# types
        var converted = Convert.ChangeType(value, underlyingType);
        return (TResult)converted;
    }

    private string GetColumnNameFromProperty(string propertyName) => _cache.GetColumnName(propertyName);

    private string GetUniqueParamName(string baseName)
    {
        return $"{_dialect.ParameterPrefix}{baseName}_{_parameters.Count}";
    }

    private void AddParametersFromObject(object parameters)
    {
        Type type = parameters.GetType();
        PropertyInfo[] props = type.GetProperties();
        for (int i = 0; i < props.Length; i++)
        {
            PropertyInfo prop = props[i];
            var value = prop.GetValue(parameters);
            _parameters.Add($"{_dialect.ParameterPrefix}{prop.Name}", value);
        }
    }

    private string BuildInClause<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values, bool negate)
    {
        // Already dialect-escaped - see comment in BuildAggregateSql.
        var escapedColumn = GetColumnNameFromSelector(selector);

        IList<TValue> valueList = values as IList<TValue> ?? values.ToList();
        if (valueList.Count == 0)
        {
            // Empty collection: IN () is always false, NOT IN () is always true
            return negate ? "1=1" : "1=0";
        }

        var sb = new StringBuilder();
        sb.Append(escapedColumn);
        sb.Append(negate ? " NOT IN (" : " IN (");

        for (int i = 0; i < valueList.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            var paramName = $"{_dialect.ParameterPrefix}p_in_{_parameters.Count}";
            sb.Append(paramName);
            _parameters.Add(paramName, valueList[i]);
        }

        sb.Append(')');
        return sb.ToString();
    }

    private string BuildBetweenClause<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to, bool negate)
    {
        // Already dialect-escaped - see comment in BuildAggregateSql.
        var escapedColumn = GetColumnNameFromSelector(selector);

        var fromParamName = $"{_dialect.ParameterPrefix}p_between_from_{_parameters.Count}";
        _parameters.Add(fromParamName, from);

        var toParamName = $"{_dialect.ParameterPrefix}p_between_to_{_parameters.Count}";
        _parameters.Add(toParamName, to);

        return negate
            ? $"{escapedColumn} NOT BETWEEN {fromParamName} AND {toParamName}"
            : $"{escapedColumn} BETWEEN {fromParamName} AND {toParamName}";
    }

    private string BuildExistsClause<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate, bool negate)
        where TSubquery : new()
    {
        EntityMetadata subqueryMetadata = FluentMetadataCache.GetMetadata<TSubquery>();
        var subqueryTable = _dialect.EscapeTableName(subqueryMetadata.SchemaName, subqueryMetadata.TableName);

        // Always alias the subquery table, even when TSubquery != T, so its columns can be
        // unambiguously correlated against the outer table. Without this, a self-referencing
        // EXISTS (TSubquery == T) would resolve both sides to the identical table prefix,
        // making the correlation meaningless.
        var subqueryAlias = $"{subqueryMetadata.TableName}_ex";

        // Use ExistsExpressionVisitor to translate the correlation predicate
        var visitor = new ExistsExpressionVisitor<T, TSubquery>(_dialect, _metadata, subqueryMetadata, _alias, subqueryAlias, _whereParamCounts);
        (string? whereClause, List<(string Name, object? Value)>? parameters) = visitor.Translate(predicate);
        _parameters.AddRange(parameters);

        var sb = new StringBuilder();
        sb.Append(negate ? "NOT EXISTS" : "EXISTS");
        sb.Append(" (SELECT 1 FROM ");
        sb.Append(subqueryTable);
        sb.Append(' ');
        sb.Append(subqueryAlias);
        sb.Append(" WHERE ");
        sb.Append(whereClause);
        sb.Append(')');

        return sb.ToString();
    }

    private string BuildInSubqueryClause<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery,
        bool negate) where TSubquery : new()
    {
        // Get outer column name - already dialect-escaped, see comment in BuildAggregateSql.
        var escapedOuterColumn = GetColumnNameFromSelector(selector);

        // Get subquery column name
        var subqueryPropertyName = PropertyExtractor.ExtractPropertyName(subquerySelector);
        EntityMetadata subqueryMetadata = FluentMetadataCache.GetMetadata<TSubquery>();
        var subqueryColumnName = GetColumnNameFromMetadata(subqueryMetadata, subqueryPropertyName);
        var escapedSubqueryColumn = _dialect.EscapeColumnName(subqueryColumnName);

        // Get subquery SQL - we need to extract the FROM and WHERE parts
        // and rebuild with just the single column
        string subquerySql;
        ParameterCollection? subqueryParams = null;

        if (subquery is QueryBuilder<TSubquery> queryBuilder)
        {
            // Access internal method to get parameters
            subqueryParams = queryBuilder.GetParameters();
            // Get the full SQL and modify it to select only the needed column
            subquerySql = subquery.ToSql();
        }
        else
        {
            // Fallback for custom IQueryTerminal<TSubquery> implementations: there is no
            // generic way to extract their bound parameters. If the produced SQL has no
            // parameter placeholders this is still safe to inline as-is; otherwise those
            // placeholders would end up unbound in the outer query, so fail loudly instead
            // of silently emitting broken SQL.
            subquerySql = subquery.ToSql();
            if (subquerySql.IndexOf(_dialect.ParameterPrefix, StringComparison.Ordinal) >= 0)
            {
                throw new NotSupportedException(
                    $"WhereInSubquery/WhereNotInSubquery only supports merging parameters from " +
                    $"a subquery built via QueryBuilder<{typeof(TSubquery).Name}> (e.g. connection.From<{typeof(TSubquery).Name}>()...). " +
                    $"The provided IQueryTerminal<{typeof(TSubquery).Name}> implementation produced " +
                    $"parameterized SQL that cannot be safely merged into the outer query.");
            }
        }

        // Replace the SELECT columns with just our needed column
        // The SQL format is: SELECT col1, col2, ... FROM table WHERE ...
        var fromIndex = subquerySql.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        if (fromIndex > 0)
        {
            subquerySql = $"SELECT {escapedSubqueryColumn}{subquerySql.Substring(fromIndex)}";
        }

        // Merge subquery parameters with prefix to avoid conflicts
        if (subqueryParams != null)
        {
            var prefix = $"sq{_parameters.Count}";
            foreach ((string? name, object? value) in subqueryParams.GetAll())
            {
                var paramPrefix = _dialect.ParameterPrefix;
                var baseName = name.TrimStart('@').TrimStart('$');
                var newName = $"{paramPrefix}{prefix}_{baseName}";
                // Update the SQL with the new parameter name
                var pattern = $@"{System.Text.RegularExpressions.Regex.Escape(paramPrefix)}{System.Text.RegularExpressions.Regex.Escape(baseName)}(?![a-zA-Z0-9_])";
                subquerySql = System.Text.RegularExpressions.Regex.Replace(subquerySql, pattern, newName);
                _parameters.Add(newName, value);
            }
        }

        var sb = new StringBuilder();
        sb.Append(escapedOuterColumn);
        sb.Append(negate ? " NOT IN (" : " IN (");
        sb.Append(subquerySql);
        sb.Append(')');

        return sb.ToString();
    }

    private static string GetColumnNameFromMetadata(EntityMetadata metadata, string propertyName)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].PropertyName == propertyName)
                return columns[i].ColumnName;
        }
        return propertyName;
    }

    #endregion

    #region Delete operations

    /// <summary>
    /// Deletes rows matching the WHERE conditions.
    /// </summary>
    public int Delete() => Delete(default);

    /// <summary>
    /// Deletes rows matching the WHERE conditions, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    public int Delete(CommandOptions options)
    {
        if (_conditions.Count == 0)
            throw new InvalidOperationException("Delete() requires a WHERE clause. Use DeleteAll() to delete all rows.");

        var sql = BuildDeleteSql();
        return ExecuteNonQuery(sql, options);
    }

    /// <summary>
    /// Asynchronously deletes rows matching the WHERE conditions.
    /// </summary>
    public Task<int> DeleteAsync(CancellationToken cancellationToken = default) => DeleteAsync(default, cancellationToken);

    /// <summary>
    /// Asynchronously deletes rows matching the WHERE conditions, executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    public async Task<int> DeleteAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_conditions.Count == 0)
            throw new InvalidOperationException("DeleteAsync() requires a WHERE clause. Use DeleteAllAsync() to delete all rows.");

        var sql = BuildDeleteSql();
        return await ExecuteNonQueryAsync(sql, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes all rows from the table (no WHERE clause).
    /// </summary>
    public int DeleteAll() => DeleteAll(default);

    /// <summary>
    /// Deletes all rows from the table (no WHERE clause), executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    public int DeleteAll(CommandOptions options)
    {
        var sql = BuildDeleteSql();
        return ExecuteNonQuery(sql, options);
    }

    /// <summary>
    /// Asynchronously deletes all rows from the table (no WHERE clause).
    /// </summary>
    public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default) => DeleteAllAsync(default, cancellationToken);

    /// <summary>
    /// Asynchronously deletes all rows from the table (no WHERE clause), executing within the
    /// given <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    public async Task<int> DeleteAllAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        var sql = BuildDeleteSql();
        return await ExecuteNonQueryAsync(sql, options, cancellationToken).ConfigureAwait(false);
    }

    private string BuildDeleteSql()
    {
        var sb = new StringBuilder(128);
        sb.Append("DELETE FROM ");
        sb.Append(_cache.EscapedTableName);

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        return sb.ToString();
    }

    private int ExecuteNonQuery(string sql, CommandOptions options = default)
    {
        var wasClosed = _connection.State == System.Data.ConnectionState.Closed;
        try
        {
            if (wasClosed)
                _connection.Open();

            using IDbCommand command = _connection.CreateCommand();
            command.CommandText = sql;

            // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
            // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
            // IDbTransaction through it throws an opaque InvalidCastException. Validate via
            // AsyncTransactionValidator first (mirroring Jaunty core's GetByIdSimpleCoreDirect) so an
            // incompatible transaction gets Jaunty's clear ArgumentException instead.
            if (options.Transaction is not null)
            {
                command.Transaction = _connection is System.Data.Common.DbConnection
                    ? global::Jaunty.AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                    : options.Transaction;
            }

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            _parameters.BindTo(command);

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && _connection.State != System.Data.ConnectionState.Closed)
                _connection.Close();
        }
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, CommandOptions options, CancellationToken cancellationToken)
    {
        if (_connection is not System.Data.Common.DbConnection dbConnection)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var wasClosed = dbConnection.State == System.Data.ConnectionState.Closed;
        try
        {
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using DbCommand command = dbConnection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is not null)
                command.Transaction = global::Jaunty.AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            _parameters.BindTo(command);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (wasClosed && dbConnection.State != System.Data.ConnectionState.Closed)
                dbConnection.Close();
        }
    }

    #endregion

    #region Update operations (ISetClause, IUpdateWhereClause)

    /// <summary>
    /// Sets a column to a value using expression selector.
    /// </summary>
    public ISetClause<T> Set<TValue>(Expression<Func<T, TValue>> selector, TValue value)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(selector);
        // Already dialect-escaped - see comment in BuildAggregateSql.
        string columnName = GetColumnNameFromProperty(propertyName);
        string paramName = GetUniqueParamName(propertyName);
        _parameters.Add(paramName, value);
        _setColumns.Add(new SetColumn(columnName, paramName));
        return this;
    }

    /// <summary>
    /// Sets a column to a value using column name.
    /// </summary>
    ISetClause<T> IFromClause<T>.Set(string column, object? value)
    {
        string paramName = GetUniqueParamName(column);
        _parameters.Add(paramName, value);
        _setColumns.Add(new SetColumn(_dialect.EscapeColumnName(column), paramName));
        return this;
    }

    /// <summary>
    /// Sets a column to a value using column name (ISetClause chaining).
    /// </summary>
    ISetClause<T> ISetClause<T>.Set(string column, object? value)
    {
        string paramName = GetUniqueParamName(column);
        _parameters.Add(paramName, value);
        _setColumns.Add(new SetColumn(_dialect.EscapeColumnName(column), paramName));
        return this;
    }

    /// <summary>
    /// Sets multiple columns from an anonymous object.
    /// </summary>
    ISetClause<T> IFromClause<T>.Set(object values)
    {

        foreach (PropertyInfo? prop in values.GetType().GetProperties())
        {
            // Already dialect-escaped - see comment in BuildAggregateSql.
            string columnName = GetColumnNameFromProperty(prop.Name);
            string paramName = GetUniqueParamName(prop.Name);
            _parameters.Add(paramName, prop.GetValue(values));
            _setColumns.Add(new SetColumn(columnName, paramName));
        }
        return this;
    }

    /// <summary>
    /// Sets multiple columns from an anonymous object (ISetClause chaining).
    /// </summary>
    ISetClause<T> ISetClause<T>.Set(object values)
    {

        foreach (PropertyInfo? prop in values.GetType().GetProperties())
        {
            // Already dialect-escaped - see comment in BuildAggregateSql.
            string columnName = GetColumnNameFromProperty(prop.Name);
            string paramName = GetUniqueParamName(prop.Name);
            _parameters.Add(paramName, prop.GetValue(values));
            _setColumns.Add(new SetColumn(columnName, paramName));
        }
        return this;
    }

    /// <summary>
    /// Adds WHERE for update using expression predicate.
    /// </summary>
    IUpdateWhereClause<T> ISetClause<T>.Where(Expression<Func<T, bool>> predicate)
    {
        var visitor = new WhereExpressionVisitor<T>(_dialect, _whereParamCounts);
        (string? sql, List<(string Name, object? Value)>? parameters) = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        _parameters.AddRange(parameters);
        return this;
    }

    /// <summary>
    /// Adds WHERE for update using column and value.
    /// </summary>
    IUpdateWhereClause<T> ISetClause<T>.Where(string column, object? value)
    {
        var escapedColumn = _dialect.EscapeColumnName(column);
        var paramName = $"{_dialect.ParameterPrefix}{column}";

        if (value is null)
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} IS NULL", LogicalOperator.None));
        }
        else
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} = {paramName}", LogicalOperator.None));
            _parameters.Add(paramName, value);
        }
        return this;
    }

    /// <summary>
    /// Adds WHERE for update using raw SQL.
    /// </summary>
    IUpdateWhereClause<T> ISetClause<T>.WhereRaw(string rawSql)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.None));
        return this;
    }

    /// <summary>
    /// Adds WHERE for update using raw SQL with parameters.
    /// </summary>
    IUpdateWhereClause<T> ISetClause<T>.WhereRaw(string rawSql, object parameters)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.None));
        AddParametersFromObject(parameters);
        return this;
    }

    /// <summary>
    /// AND condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.And(string column, object? value)
    {
        var escapedColumn = _dialect.EscapeColumnName(column);
        var paramName = GetUniqueParamName(column);

        if (value is null)
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} IS NULL", LogicalOperator.And));
        }
        else
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} = {paramName}", LogicalOperator.And));
            _parameters.Add(paramName, value);
        }
        return this;
    }

    /// <summary>
    /// AND condition for update WHERE clause using expression.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.And(Expression<Func<T, bool>> predicate)
    {
        var visitor = new WhereExpressionVisitor<T>(_dialect, _whereParamCounts);
        (string? sql, List<(string Name, object? Value)>? parameters) = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        _parameters.AddRange(parameters);
        return this;
    }

    /// <summary>
    /// AND condition for update WHERE clause using raw SQL.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.AndRaw(string rawSql)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.And));
        return this;
    }

    /// <summary>
    /// AND condition for update WHERE clause using raw SQL with parameters.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.AndRaw(string rawSql, object parameters)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.And));
        AddParametersFromObject(parameters);
        return this;
    }

    /// <summary>
    /// OR condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.Or(string column, object? value)
    {
        var escapedColumn = _dialect.EscapeColumnName(column);
        var paramName = GetUniqueParamName(column);

        if (value is null)
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} IS NULL", LogicalOperator.Or));
        }
        else
        {
            _conditions.Add(WhereCondition.Column($"{escapedColumn} = {paramName}", LogicalOperator.Or));
            _parameters.Add(paramName, value);
        }
        return this;
    }

    /// <summary>
    /// OR condition for update WHERE clause using expression.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.Or(Expression<Func<T, bool>> predicate)
    {
        var visitor = new WhereExpressionVisitor<T>(_dialect, _whereParamCounts);
        (string? sql, List<(string Name, object? Value)>? parameters) = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        _parameters.AddRange(parameters);
        return this;
    }

    /// <summary>
    /// OR condition for update WHERE clause using raw SQL.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.OrRaw(string rawSql)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.Or));
        return this;
    }

    /// <summary>
    /// OR condition for update WHERE clause using raw SQL with parameters.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.OrRaw(string rawSql, object parameters)
    {
        _conditions.Add(WhereCondition.Raw(rawSql, LogicalOperator.Or));
        AddParametersFromObject(parameters);
        return this;
    }

    /// <summary>
    /// AND IN condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.AndIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        var sql = BuildInClause(selector, values, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    /// <summary>
    /// AND NOT IN condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.AndNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        var sql = BuildInClause(selector, values, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    /// <summary>
    /// OR IN condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.OrIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        var sql = BuildInClause(selector, values, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    /// <summary>
    /// OR NOT IN condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.OrNotIn<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values)
    {
        var sql = BuildInClause(selector, values, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    /// <summary>
    /// Updates all rows (no WHERE clause). Use with caution.
    /// </summary>
    public int UpdateAll() => UpdateAll(default);

    /// <summary>
    /// Updates all rows (no WHERE clause), executing within the given <see cref="CommandOptions"/>
    /// (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>). Use with caution.
    /// </summary>
    public int UpdateAll(CommandOptions options)
    {
        if (_setColumns.Count == 0)
            throw new InvalidOperationException("UpdateAll() requires at least one Set() call.");

        var sql = BuildUpdateSql();
        return ExecuteNonQuery(sql, options);
    }

    /// <summary>
    /// Asynchronously updates all rows (no WHERE clause).
    /// </summary>
    public Task<int> UpdateAllAsync(CancellationToken cancellationToken = default) => UpdateAllAsync(default, cancellationToken);

    /// <summary>
    /// Asynchronously updates all rows (no WHERE clause), executing within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    public async Task<int> UpdateAllAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_setColumns.Count == 0)
            throw new InvalidOperationException("UpdateAllAsync() requires at least one Set() call.");

        var sql = BuildUpdateSql();
        return await ExecuteNonQueryAsync(sql, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the UPDATE with WHERE conditions.
    /// </summary>
    public int Update() => Update(default);

    /// <summary>
    /// Executes the UPDATE with WHERE conditions, within the given <see cref="CommandOptions"/>
    /// (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    public int Update(CommandOptions options)
    {
        if (_setColumns.Count == 0)
            throw new InvalidOperationException("Update() requires at least one Set() call.");
        if (_conditions.Count == 0)
            throw new InvalidOperationException("Update() requires a WHERE clause. Use UpdateAll() to update all rows.");

        var sql = BuildUpdateSql();
        return ExecuteNonQuery(sql, options);
    }

    /// <summary>
    /// Asynchronously executes the UPDATE with WHERE conditions.
    /// </summary>
    public Task<int> UpdateAsync(CancellationToken cancellationToken = default) => UpdateAsync(default, cancellationToken);

    /// <summary>
    /// Asynchronously executes the UPDATE with WHERE conditions, within the given
    /// <see cref="CommandOptions"/> (e.g. <see cref="CommandOptions.WithTransaction(IDbTransaction)"/>).
    /// </summary>
    public async Task<int> UpdateAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_setColumns.Count == 0)
            throw new InvalidOperationException("UpdateAsync() requires at least one Set() call.");
        if (_conditions.Count == 0)
            throw new InvalidOperationException("UpdateAsync() requires a WHERE clause. Use UpdateAllAsync() to update all rows.");

        var sql = BuildUpdateSql();
        return await ExecuteNonQueryAsync(sql, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the UPDATE SQL for debugging.
    /// </summary>
    string ISetClause<T>.ToSql() => BuildUpdateSql();

    /// <summary>
    /// Returns the UPDATE SQL for debugging.
    /// </summary>
    string IUpdateWhereClause<T>.ToSql() => BuildUpdateSql();

    private string BuildUpdateSql()
    {
        var sb = new StringBuilder(256);
        sb.Append("UPDATE ");
        sb.Append(_cache.EscapedTableName);

        sb.Append(" SET ");
        for (int i = 0; i < _setColumns.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            SetColumn setCol = _setColumns[i];
            sb.Append(setCol.ColumnName);
            sb.Append(" = ");
            sb.Append(setCol.ParameterName);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        return sb.ToString();
    }

    #endregion
}
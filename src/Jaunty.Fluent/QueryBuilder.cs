using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Core;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Enums;

namespace Jaunty.Fluent;

/// <summary>
/// Main query builder implementation. Implements all fluent interfaces.
/// </summary>
internal sealed class QueryBuilder<T> : IFromClause<T>, IWhereClause<T>, IOrderByClause<T>, IDistinctClause<T>
    where T : new()
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _metadata;
    private readonly string? _alias;
    private readonly List<WhereCondition> _conditions = new();
    private readonly List<OrderByColumn> _orderByColumns = new();
    private readonly ParameterCollection _parameters = new();
    private bool _distinct;
    private int? _take;
    private int? _skip;

    internal QueryBuilder(IDbConnection connection, string? alias = null)
    {
        _connection = connection;
        _dialect = SqlDialectFactory.GetDialect(connection);
        _metadata = MetadataCache<T>.Metadata;
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

    private string[] GetAllColumnNames()
    {
        var columns = _metadata.Columns;
        var names = new string[columns.Count];
        for (int i = 0; i < columns.Count; i++)
            names[i] = columns[i].ColumnName;
        return names;
    }

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

    #region WHERE clause

    public IWhereClause<T> Where(string column, object? value)
    {
        var escapedColumn = _dialect.EscapeColumnName(column);
        var paramName = $"@{column}";

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
        var visitor = new WhereExpressionVisitor<T>(_dialect);
        var (sql, parameters) = visitor.Translate(predicate);
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
        var visitor = new WhereExpressionVisitor<T>(_dialect);
        var (sql, parameters) = visitor.Translate(predicate);
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
        var visitor = new WhereExpressionVisitor<T>(_dialect);
        var (sql, parameters) = visitor.Translate(predicate);
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
        var columnName = GetColumnNameFromProperty(propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    IOrderByClause<T> IFromClause<T>.OrderByDescending(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        var columnName = GetColumnNameFromProperty(propertyName);
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
        var columnName = GetColumnNameFromProperty(propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    IOrderByClause<T> IWhereClause<T>.OrderByDescending(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        var columnName = GetColumnNameFromProperty(propertyName);
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
        var columnName = GetColumnNameFromProperty(propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    IOrderByClause<T> IDistinctClause<T>.OrderByDescending(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        var columnName = GetColumnNameFromProperty(propertyName);
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
        var columnName = GetColumnNameFromProperty(propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IOrderByClause<T> ThenByDescending(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        var columnName = GetColumnNameFromProperty(propertyName);
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
        var sql = BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());
        return _connection.QueryPartial<T>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectPartialFirst(params string[] columns)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());
        _take = original;
        return _connection.QueryPartialFirst<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectPartialFirstOrDefault(params string[] columns)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());
        _take = original;
        return _connection.QueryPartialFirstOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectPartialSingle(params string[] columns)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());
        _take = original;
        return _connection.QueryPartialSingle<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectPartialSingleOrDefault(params string[] columns)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());
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
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return Select();
    }

    public async Task<T> SelectFirstAsync(CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryFirstAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectFirst();
    }

    public async Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryFirstOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectFirstOrDefault();
    }

    public async Task<T> SelectSingleAsync(CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QuerySingleAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectSingle();
    }

    public async Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QuerySingleOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectSingleOrDefault();
    }

    #endregion

    #region Terminal operations (async) - Partial entity (string columns)

    public async Task<List<T>> SelectPartialAsync(string[] columns, CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectPartial(columns);
    }

    public async Task<T> SelectPartialFirstAsync(string[] columns, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialFirstAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectPartialFirst(columns);
    }

    public async Task<T?> SelectPartialFirstOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialFirstOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectPartialFirstOrDefault(columns);
    }

    public async Task<T> SelectPartialSingleAsync(string[] columns, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialSingleAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectPartialSingle(columns);
    }

    public async Task<T?> SelectPartialSingleOrDefaultAsync(string[] columns, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialSingleOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectPartialSingleOrDefault(columns);
    }

    #endregion

    #region Terminal operations (async) - Partial entity (expression columns)

    public async Task<List<T>> SelectPartialAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var sql = BuildSelectSql(columnNames);
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectPartial(columns);
    }

    public async Task<T> SelectPartialFirstAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialFirstAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectPartialFirst(columns);
    }

    public async Task<T?> SelectPartialFirstOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialFirstOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectPartialFirstOrDefault(columns);
    }

    public async Task<T> SelectPartialSingleAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialSingleAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectPartialSingle(columns);
    }

    public async Task<T?> SelectPartialSingleOrDefaultAsync(Expression<Func<T, object?>>[] columns, CancellationToken cancellationToken = default)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(columnNames);
        _take = original;
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialSingleOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return SelectPartialSingleOrDefault(columns);
    }

    #endregion

    #region Scalar aggregates (async)

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildCountSql();
        if (_connection is DbConnection dbConn)
        {
            var result = await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
            return (int)result;
        }
        return Count();
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildCountSql();
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return LongCount();
    }

    public async Task<int> CountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("COUNT", columnName);
        if (_connection is DbConnection dbConn)
        {
            var result = await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
            return (int)result;
        }
        return Count(selector);
    }

    public async Task<long> LongCountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("COUNT", columnName);
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return LongCount(selector);
    }

    public async Task<TResult> SumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("SUM", columnName);
        if (_connection is DbConnection dbConn)
        {
            var result = await dbConn.QueryScalarAsync<object>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
            return ConvertScalarResult<TResult>(result);
        }
        return Sum(selector);
    }

    public async Task<double> AvgAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("AVG", columnName);
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryScalarAsync<double>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return Avg(selector);
    }

    public async Task<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("MIN", columnName);
        if (_connection is DbConnection dbConn)
        {
            var result = await dbConn.QueryScalarAsync<object>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
            return ConvertScalarResult<TResult>(result);
        }
        return Min(selector);
    }

    public async Task<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("MAX", columnName);
        if (_connection is DbConnection dbConn)
        {
            var result = await dbConn.QueryScalarAsync<object>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
            return ConvertScalarResult<TResult>(result);
        }
        return Max(selector);
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
        => BuildSelectSql(columns.Length > 0 ? columns : GetAllColumnNames());

    public string ToSql(params Expression<Func<T, object?>>[] columns)
    {
        var columnNames = columns.Length > 0 ? ResolveColumns(columns) : GetAllColumnNames();
        return BuildSelectSql(columnNames);
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

        // Columns
        for (int i = 0; i < columns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_dialect.EscapeColumnName(columns[i]));
        }

        // FROM
        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_metadata.SchemaName, _metadata.TableName));

        // WHERE
        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            for (int i = 0; i < _conditions.Count; i++)
            {
                var condition = _conditions[i];
                if (i > 0)
                {
                    sb.Append(condition.Operator == LogicalOperator.Or ? " OR " : " AND ");
                }
                sb.Append(condition.Sql);
            }
        }

        // ORDER BY
        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");
            for (int i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var orderBy = _orderByColumns[i];
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
        sb.Append(_dialect.EscapeTableName(_metadata.SchemaName, _metadata.TableName));

        // WHERE
        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            for (int i = 0; i < _conditions.Count; i++)
            {
                var condition = _conditions[i];
                if (i > 0)
                {
                    sb.Append(condition.Operator == LogicalOperator.Or ? " OR " : " AND ");
                }
                sb.Append(condition.Sql);
            }
        }

        return sb.ToString();
    }

    private string BuildAggregateSql(string aggregateFunction, string columnName)
    {
        var sb = new StringBuilder(128);
        sb.Append("SELECT ");
        sb.Append(aggregateFunction);
        sb.Append('(');
        sb.Append(_dialect.EscapeColumnName(columnName));
        sb.Append(')');

        // FROM
        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_metadata.SchemaName, _metadata.TableName));

        // WHERE
        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            for (int i = 0; i < _conditions.Count; i++)
            {
                var condition = _conditions[i];
                if (i > 0)
                {
                    sb.Append(condition.Operator == LogicalOperator.Or ? " OR " : " AND ");
                }
                sb.Append(condition.Sql);
            }
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
        if (value is null || value is DBNull)
            return default!;

        var targetType = typeof(TResult);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Handle conversion from database types to C# types
        var converted = Convert.ChangeType(value, underlyingType);
        return (TResult)converted;
    }

    private string GetColumnNameFromProperty(string propertyName)
    {
        var columns = _metadata.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
                return columns[i].ColumnName;
        }
        return propertyName;
    }

    private string GetUniqueParamName(string baseName)
    {
        return $"@{baseName}_{_parameters.Count}";
    }

    private void AddParametersFromObject(object parameters)
    {
        var type = parameters.GetType();
        var props = type.GetProperties();
        for (int i = 0; i < props.Length; i++)
        {
            var prop = props[i];
            var value = prop.GetValue(parameters);
            _parameters.Add($"@{prop.Name}", value);
        }
    }

    private string BuildInClause<TValue>(Expression<Func<T, TValue>> selector, IEnumerable<TValue> values, bool negate)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var escapedColumn = _dialect.EscapeColumnName(columnName);

        var valueList = values as IList<TValue> ?? values.ToList();
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
            var paramName = $"@p_in_{_parameters.Count}";
            sb.Append(paramName);
            _parameters.Add(paramName, valueList[i]);
        }

        sb.Append(')');
        return sb.ToString();
    }

    private string BuildBetweenClause<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to, bool negate)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var escapedColumn = _dialect.EscapeColumnName(columnName);

        var fromParamName = $"@p_between_from_{_parameters.Count}";
        _parameters.Add(fromParamName, from);

        var toParamName = $"@p_between_to_{_parameters.Count}";
        _parameters.Add(toParamName, to);

        return negate
            ? $"{escapedColumn} NOT BETWEEN {fromParamName} AND {toParamName}"
            : $"{escapedColumn} BETWEEN {fromParamName} AND {toParamName}";
    }

    private string BuildExistsClause<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate, bool negate)
        where TSubquery : new()
    {
        var subqueryMetadata = MetadataCache<TSubquery>.Metadata;
        var subqueryTable = _dialect.EscapeTableName(subqueryMetadata.SchemaName, subqueryMetadata.TableName);

        // Use ExistsExpressionVisitor to translate the correlation predicate
        var visitor = new ExistsExpressionVisitor<T, TSubquery>(_dialect, _metadata, subqueryMetadata);
        var (whereClause, parameters) = visitor.Translate(predicate);
        _parameters.AddRange(parameters);

        var sb = new StringBuilder();
        sb.Append(negate ? "NOT EXISTS" : "EXISTS");
        sb.Append(" (SELECT 1 FROM ");
        sb.Append(subqueryTable);
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
        // Get outer column name
        var outerColumnName = GetColumnNameFromSelector(selector);
        var escapedOuterColumn = _dialect.EscapeColumnName(outerColumnName);

        // Get subquery column name
        var subqueryPropertyName = PropertyExtractor.ExtractPropertyName(subquerySelector);
        var subqueryMetadata = MetadataCache<TSubquery>.Metadata;
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
            // Fallback for other implementations
            subquerySql = subquery.ToSql();
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
            foreach (var (name, value) in subqueryParams.GetAll())
            {
                var baseName = name.TrimStart('@');
                var newName = $"@{prefix}_{baseName}";
                // Update the SQL with the new parameter name
                var pattern = $@"@{System.Text.RegularExpressions.Regex.Escape(baseName)}(?![a-zA-Z0-9_])";
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
        var columns = metadata.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
                return columns[i].ColumnName;
        }
        return propertyName;
    }

    #endregion
}

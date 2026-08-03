using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;
using Jaunty.Configuration;
using System.Globalization;
using Jaunty.Internals;

namespace Jaunty.Fluent;

/// <summary>
/// Main query builder implementation. Implements all fluent interfaces.
/// </summary>
internal sealed partial class QueryBuilder<T> : IFromClause<T>, IWhereClause<T>, IOrderByClause<T>, IDistinctClause<T>, ISetClause<T>, IUpdateWhereClause<T>
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
    private bool _aliasReferencedInConditions;

    internal QueryBuilder(IDbConnection connection, string? alias = null)
    {
        _connection = connection;
        _dialect = SqlDialectFactory.GetDialect(connection);
        _metadata = FluentMetadataCache.GetMetadata<T>();
        _cache = FluentMetadataCache.GetForDialect<T>(_dialect);

        // AUD-R35: the alias is interpolated into SQL text verbatim (here, and as the outer
        // prefix handed to ExistsExpressionVisitor), so it is validated at the boundary.
        if (alias is not null)
            SqlIdentifierValidator.Validate(alias, nameof(alias));

        _alias = alias;
    }

    /// <summary>
    /// AUD-R35: appends the FROM/UPDATE target, declaring <see cref="_alias"/> when
    /// <c>From&lt;T&gt;(alias)</c> supplied one.
    /// </summary>
    /// <remarks>
    /// The alias used to be stored and read in exactly one place - <see cref="BuildExistsClause"/>,
    /// which hands it to <see cref="ExistsExpressionVisitor{T, TSubquery}"/> as the prefix for every
    /// outer column reference. No builder ever declared it, so
    /// <c>From&lt;Category&gt;("c").WhereExists&lt;Product&gt;(...)</c> emitted a correlation on
    /// <c>c.category_id</c> against a bare <c>FROM "categories"</c> and failed at execution with
    /// "multi-part identifier could not be bound". Only the un-joined path was affected: the join
    /// builders read the <see cref="Alias"/> property and emit <c>FROM table alias</c> themselves.
    /// </remarks>
    private void AppendAliasedTable(StringBuilder sb)
    {
        sb.Append(_cache.EscapedTableName);

        if (_alias is not null)
        {
            sb.Append(' ');
            sb.Append(_alias);
        }
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

    /// <summary>
    /// True if this query already has its own ORDER BY, Take, or Skip applied - used by
    /// <see cref="SetOperationBuilder{T}"/> to reject operands whose own ordering/paging
    /// would otherwise be spliced into the middle of a combined UNION/EXCEPT/INTERSECT
    /// statement instead of applying to the combined result.
    /// </summary>
    internal bool HasOrderingOrPaging() => _orderByColumns.Count > 0 || _take.HasValue || _skip.HasValue;

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
        var paramName = GetUniqueParamName(column);

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

    IPagedClause<T> IFromClause<T>.Take(int count) { _take = count; return this; }
    IPagedClause<T> IFromClause<T>.Skip(int count) { _skip = count; return this; }
    IPagedWhereClause<T> IWhereClause<T>.Take(int count) { _take = count; return this; }
    IPagedWhereClause<T> IWhereClause<T>.Skip(int count) { _skip = count; return this; }
    IOrderByClause<T> IOrderByClause<T>.Take(int count) { _take = count; return this; }
    IOrderByClause<T> IOrderByClause<T>.Skip(int count) { _skip = count; return this; }
    IDistinctClause<T> IDistinctClause<T>.Take(int count) { _take = count; return this; }
    IDistinctClause<T> IDistinctClause<T>.Skip(int count) { _skip = count; return this; }

    #endregion

    #region JOIN operations

    public IJoinClause<T, TJoin> InnerJoin<TJoin>(string? alias = null) where TJoin : new()
    {
        ThrowIfPagedBeforeJoin();
        return new JoinClauseBuilder<T, TJoin>(this, JoinType.Inner, alias);
    }

    public IJoinClause<T, TJoin> LeftJoin<TJoin>(string? alias = null) where TJoin : new()
    {
        ThrowIfPagedBeforeJoin();
        return new JoinClauseBuilder<T, TJoin>(this, JoinType.Left, alias);
    }

    public IJoinClause<T, TJoin> RightJoin<TJoin>(string? alias = null) where TJoin : new()
    {
        ThrowIfPagedBeforeJoin();
        return new JoinClauseBuilder<T, TJoin>(this, JoinType.Right, alias);
    }

    /// <summary>
    /// AUD-R26 (batch 5, medium/bug). <c>Take</c>/<c>Skip</c> applied before a join were silently
    /// discarded. <c>IFromClause&lt;T&gt;.Take</c>/<c>Skip</c> return <c>IFromClause&lt;T&gt;</c>,
    /// which exposes the join methods, so <c>From&lt;T&gt;().Take(5).InnerJoin&lt;U&gt;()</c>
    /// compiles - and <c>JoinClauseBuilder.CreateJoinedQuery</c> constructs the
    /// <c>JoinedQueryBuilder</c> from the connection, dialect, table, schema, alias and join info
    /// only. <c>_take</c> and <c>_skip</c> are left behind, <c>JoinedQueryBuilder</c> has no field
    /// for them and <c>IJoinedQuery&lt;,&gt;</c> exposes no <c>Take</c>/<c>Skip</c>, so there is
    /// nowhere to re-apply them either. Measured against a 3-row table:
    /// <code>
    /// From&lt;Item&gt;().Take(1).ToSql()                     -> ... LIMIT 1 OFFSET 0
    /// From&lt;Item&gt;().Take(1).InnerJoin&lt;Cat&gt;().On(..)  -> no LIMIT at all, 3 rows
    /// From&lt;Item&gt;("i").Skip(2).InnerJoin&lt;Cat&gt;(..)     -> 3 rows
    /// </code>
    /// A caller paging a joined result got the whole table, with no exception and no warning.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This throws rather than carrying the paging across, because the chain does not say which of
    /// two different results the caller wants: <c>Take(1)</c> written before the join reads as
    /// "limit the source table, then join" - a derived table - while the only cheap implementation
    /// is "page the joined result", which is a different set of rows whenever the join is not
    /// one-to-one. Guessing either way silently would replace one wrong answer with another.
    /// </para>
    /// <para>
    /// The same class of bug, guarded the same way, as
    /// <see cref="ThrowIfHasOrderingOrPaging"/> for UNION/EXCEPT/INTERSECT: state that belongs to
    /// the outer query gets attached to an inner one. That guard's message tells the caller to move
    /// the paging to the outer chain; this one cannot, because the joined query has no paging
    /// surface to move it to, so it says what to do instead.
    /// </para>
    /// <para>
    /// <c>_conditions</c>, <c>_parameters</c>, <c>_distinct</c> and <c>_orderByColumns</c> are
    /// dropped by the same line, but none is reachable before a join - <c>Where</c> returns
    /// <c>IWhereClause&lt;T&gt;</c>, <c>Distinct</c> returns <c>IDistinctClause&lt;T&gt;</c> and
    /// <c>OrderBy</c> returns <c>IOrderByClause&lt;T&gt;</c>, none of which exposes a join. If any
    /// of them ever does, it needs the same treatment and this guard needs widening.
    /// </para>
    /// </remarks>
    private void ThrowIfPagedBeforeJoin()
    {
        if (_take.HasValue || _skip.HasValue)
        {
            throw new NotSupportedException(
                "Take/Skip applied before a join are not carried into the joined query. Jaunty " +
                "cannot tell whether you meant to limit the source table before joining or to page " +
                "the joined result, and the two return different rows whenever the join is not " +
                "one-to-one. Remove the Take/Skip from before the join, or page the source " +
                "explicitly and join against the result.");
        }
    }

    private void ThrowIfPagedBeforeGroupBy()
    {
        if (_take.HasValue || _skip.HasValue)
        {
            throw new NotSupportedException(
                "Take/Skip applied before a GroupBy are not carried into the grouped query. Jaunty " +
                "cannot tell whether you meant to group only the paged rows or to page the groups, " +
                "and the two return different results. Remove the Take/Skip from before the " +
                "GroupBy, or page the source explicitly and group the result.");
        }
    }

    /// <summary>
    /// AUD-R35. DELETE and UPDATE cannot portably declare a table alias - the syntax differs
    /// across every dialect Jaunty targets - so <see cref="AppendAliasedTable"/> is deliberately
    /// not used by <see cref="BuildDeleteSql"/> or <see cref="BuildUpdateSql"/>. That is correct
    /// only while nothing in the statement references the alias. A correlated EXISTS built from
    /// an aliased query does reference it, and the resulting DELETE/UPDATE names an alias it never
    /// declares. Fail with that explanation rather than letting the database report an unbound
    /// identifier.
    /// </summary>
    private void ThrowIfAliasReferencedByWrite(string statement)
    {
        if (_aliasReferencedInConditions)
        {
            throw new NotSupportedException(
                $"A correlated EXISTS built from From<T>(\"{_alias}\") references the alias, and " +
                $"{statement} cannot declare a table alias portably. Drop the alias from the " +
                $"From<T>(...) call - an un-aliased outer table correlates just as well when the " +
                $"subquery entity differs - or run the {statement} without the EXISTS correlation.");
        }
    }

    #endregion

    #region GROUP BY

    /// <remarks>
    /// AUD-R33-002. <c>_take</c>/<c>_skip</c> are not passed to <see cref="GroupedQueryBuilder{T, TKey}"/>
    /// and it has no field for them, so paging written before the grouping used to vanish without a
    /// word. <c>IFromClause&lt;T&gt;.Take</c>/<c>Skip</c> return <c>IFromClause&lt;T&gt;</c> and
    /// <c>IWhereClause&lt;T&gt;.Take</c>/<c>Skip</c> return <c>IWhereClause&lt;T&gt;</c>, and both of
    /// those interfaces declare <c>GroupBy</c>, so <c>From&lt;T&gt;().Take(5).GroupBy(...)</c>
    /// compiles and silently groups the whole table.
    /// <para>
    /// Exactly the case <see cref="ThrowIfPagedBeforeJoin"/> guards, and rejected for the same
    /// reason: <c>Take(5)</c> before a <c>GroupBy</c> can mean "group the first five rows" or
    /// "return the first five groups", the two give different answers, and picking one silently
    /// would swap a wrong result for a different wrong result.
    /// </para>
    /// <para>
    /// <c>_distinct</c> and <c>_orderByColumns</c> are dropped by the same line but are not
    /// reachable here - <c>Distinct</c> returns <c>IDistinctClause&lt;T&gt;</c> and <c>OrderBy</c>
    /// returns <c>IOrderByClause&lt;T&gt;</c>, neither of which exposes <c>GroupBy</c>. If either
    /// ever does, this guard needs widening.
    /// </para>
    /// </remarks>
    public IGroupedQuery<T, TKey> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        ThrowIfPagedBeforeGroupBy();

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

    public List<T> Select(CommandOptions options)
    {
        var sql = BuildSelectSql(GetAllColumnNames());
        return _connection.Query<T>(sql, _parameters.ToParameterObject()!, ToTypedOptions<T>(options));
    }

    public T SelectFirst()
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QueryFirst<T>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectFirst(CommandOptions options)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QueryFirst<T>(sql, _parameters.ToParameterObject()!, ToTypedOptions<T>(options));
    }

    public T? SelectFirstOrDefault()
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QueryFirstOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectFirstOrDefault(CommandOptions options)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QueryFirstOrDefault<T>(sql, _parameters.ToParameterObject()!, ToTypedOptions<T>(options));
    }

    public T SelectSingle()
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QuerySingle<T>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectSingle(CommandOptions options)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QuerySingle<T>(sql, _parameters.ToParameterObject()!, ToTypedOptions<T>(options));
    }

    public T? SelectSingleOrDefault()
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QuerySingleOrDefault<T>(sql, _parameters.ToParameterObject()!);
    }

    public T? SelectSingleOrDefault(CommandOptions options)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        return _connection.QuerySingleOrDefault<T>(sql, _parameters.ToParameterObject()!, ToTypedOptions<T>(options));
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
        return CountConversion.ToInt32(result);
    }

    public int Count(CommandOptions options)
    {
        var sql = BuildCountSql();
        // SQLite returns Int64 for COUNT, so we need to handle conversion
        var result = _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!, ToTypedOptions<long>(options));
        return CountConversion.ToInt32(result);
    }

    public long LongCount()
    {
        var sql = BuildCountSql();
        return _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!);
    }

    public long LongCount(CommandOptions options)
    {
        var sql = BuildCountSql();
        return _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!, ToTypedOptions<long>(options));
    }

    public int Count<TResult>(Expression<Func<T, TResult>> selector)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("COUNT", columnName);
        // SQLite returns Int64 for COUNT
        var result = _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!);
        return CountConversion.ToInt32(result);
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

    public async Task<List<T>> SelectAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectSql(GetAllColumnNames());
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryAsync<T>(sql, _parameters.ToParameterObject()!, ToTypedOptions<T>(options), cancellationToken).ConfigureAwait(false);
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

    public async Task<T> SelectFirstAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryFirstAsync<T>(sql, _parameters.ToParameterObject()!, ToTypedOptions<T>(options), cancellationToken).ConfigureAwait(false);
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

    public async Task<T?> SelectFirstOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 1;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryFirstOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, ToTypedOptions<T>(options), cancellationToken).ConfigureAwait(false);
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

    public async Task<T> SelectSingleAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QuerySingleAsync<T>(sql, _parameters.ToParameterObject()!, ToTypedOptions<T>(options), cancellationToken).ConfigureAwait(false);
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

    public async Task<T?> SelectSingleOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        var original = _take;
        _take = 2;
        var sql = BuildSelectSql(GetAllColumnNames());
        _take = original;
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QuerySingleOrDefaultAsync<T>(sql, _parameters.ToParameterObject()!, ToTypedOptions<T>(options), cancellationToken).ConfigureAwait(false);
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
        return CountConversion.ToInt32(result);
    }

    public async Task<int> CountAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        var sql = BuildCountSql();
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        var result = await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, ToTypedOptions<long>(options), cancellationToken).ConfigureAwait(false);
        return CountConversion.ToInt32(result);
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildCountSql();
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> LongCountAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        var sql = BuildCountSql();
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        return await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, ToTypedOptions<long>(options), cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var columnName = GetColumnNameFromSelector(selector);
        var sql = BuildAggregateSql("COUNT", columnName);
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");
        var result = await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return CountConversion.ToInt32(result);
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
        ThrowIfHasOrderingOrPaging();
        var builder = new SetOperationBuilder<T>(_connection, _dialect, ToSql(), _parameters.Clone());
        return builder.Union(other);
    }

    public ISetOperationClause<T> UnionAll(IQueryTerminal<T> other)
    {
        ThrowIfHasOrderingOrPaging();
        var builder = new SetOperationBuilder<T>(_connection, _dialect, ToSql(), _parameters.Clone());
        return builder.UnionAll(other);
    }

    public ISetOperationClause<T> Except(IQueryTerminal<T> other)
    {
        ThrowIfHasOrderingOrPaging();
        var builder = new SetOperationBuilder<T>(_connection, _dialect, ToSql(), _parameters.Clone());
        return builder.Except(other);
    }

    public ISetOperationClause<T> Intersect(IQueryTerminal<T> other)
    {
        ThrowIfHasOrderingOrPaging();
        var builder = new SetOperationBuilder<T>(_connection, _dialect, ToSql(), _parameters.Clone());
        return builder.Intersect(other);
    }

    // The same class of bug SetOperationBuilder<T>.ThrowIfOperandHasOrderingOrPaging guards
    // against on the operand side: if this query already has its own ORDER BY/Take/Skip
    // applied (e.g. db.From<T>().OrderBy(...).Take(5).Union(...)), that ordering/paging would
    // be baked into ToSql() and spliced in as the first segment of the combined statement
    // instead of applying to the combined result.
    private void ThrowIfHasOrderingOrPaging()
    {
        if (HasOrderingOrPaging())
        {
            throw new NotSupportedException(
                "Union/UnionAll/Except/Intersect must not be called on a query that already has " +
                "its own OrderBy/Take/Skip applied. Ordering and paging apply to the combined result - " +
                "call OrderBy/Take/Skip on the outer set-operation chain (after Union/UnionAll/Except/Intersect) instead.");
        }
    }

    #endregion

    #region Private helpers

    private string BuildSelectSql(string[] columns) => BuildSelectSql(columns, includeOrderBy: true);

    /// <param name="columns">Pre-escaped column expressions.</param>
    /// <param name="includeOrderBy">
    /// False only from <see cref="WrapScalarInDerivedTable"/> when there is no paging. An ORDER BY
    /// cannot change an aggregate taken over the whole derived set, and SQL Server rejects ORDER BY
    /// in a derived table that has no TOP/OFFSET/FOR XML - so emitting it there would turn a
    /// working query into a syntax error for no gain. With paging it is emitted, because then it
    /// decides which rows survive.
    /// </param>
    private string BuildSelectSql(string[] columns, bool includeOrderBy)
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
        AppendAliasedTable(sb);

        // WHERE
        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        // ORDER BY
        if (includeOrderBy && _orderByColumns.Count > 0)
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

    /// <summary>
    /// The alias given to the derived table in <see cref="WrapScalarInDerivedTable"/>. SQL Server
    /// and MySQL both require a derived table to be named; the others accept one. Prefixed so it
    /// cannot collide with a caller's table or alias.
    /// </summary>
    private const string ScalarDerivedTableAlias = "jaunty_scalar_src";

    /// <summary>
    /// True when a scalar terminal has to run over a derived table rather than straight over the
    /// base table, because <c>DISTINCT</c>, <c>Take</c> or <c>Skip</c> changes which rows it should
    /// see.
    /// </summary>
    private bool ScalarNeedsDerivedTable => _distinct || _take.HasValue || _skip.HasValue;

    /// <summary>
    /// AUD-R26 (batch 5, medium/consistency). <see cref="BuildCountSql"/> and
    /// <see cref="BuildAggregateSql"/> emitted only <c>SELECT ... FROM &lt;table&gt; [WHERE ...]</c>
    /// and read none of <c>_take</c>, <c>_skip</c> or <c>_distinct</c> - all three of which
    /// <see cref="BuildSelectSql(string[])"/> honours sixty lines above. Every scalar terminal on
    /// the builder therefore ignored paging and DISTINCT while every row terminal on the same
    /// builder honoured them. Measured against a 3-row table:
    /// <code>
    /// From&lt;Item&gt;().Count()            = 3
    /// From&lt;Item&gt;().Take(1).Count()    = 3    LINQ's Take(1).Count() is 1
    /// From&lt;Item&gt;().Skip(2).Count()    = 3    LINQ's Skip(2).Count() is 1
    /// From&lt;Item&gt;().Take(1).Sum(CatId) = 4    the full-table sum
    /// From&lt;Item&gt;().Distinct().Count() = 3    SELECT COUNT(*), not over the DISTINCT rows
    /// </code>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wrapping is what makes this correct rather than clamping: an aggregate cannot share a SELECT
    /// with the LIMIT it is supposed to respect, because the LIMIT would apply to the single row
    /// the aggregate produces rather than to the rows it consumes. The derived table is the paged -
    /// or distinct - row set, and the aggregate runs over that.
    /// </para>
    /// <para>
    /// <c>Distinct()</c> on this builder means distinct <em>rows of the projection</em>, which is
    /// what <see cref="BuildSelectSql(string[])"/> emits, so the derived table carries that meaning
    /// through unchanged. Note that this makes <c>Distinct().Count(x =&gt; x.Col)</c> a count of
    /// <c>Col</c> over the distinct rows, not <c>COUNT(DISTINCT Col)</c> - a different question,
    /// which this builder has no syntax for asking.
    /// </para>
    /// <para>
    /// The unpaged, non-distinct case is left exactly as it was: no derived table, byte-identical
    /// SQL. That is the overwhelming majority of scalar calls and none of them should pay for this.
    /// </para>
    /// </remarks>
    private string WrapScalarInDerivedTable(string scalarExpression)
    {
        bool paged = _take.HasValue || _skip.HasValue;
        string inner = BuildSelectSql(GetAllColumnNames(), includeOrderBy: paged);

        var sb = new StringBuilder(inner.Length + 64);
        sb.Append("SELECT ");
        sb.Append(scalarExpression);
        sb.Append(" FROM (");
        sb.Append(inner);
        sb.Append(") ");
        sb.Append(ScalarDerivedTableAlias);
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
        AppendAliasedTable(sb);

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
        if (ScalarNeedsDerivedTable)
            return WrapScalarInDerivedTable("COUNT(*)");

        var sb = new StringBuilder(128);
        sb.Append("SELECT COUNT(*)");

        // FROM
        sb.Append(" FROM ");
        AppendAliasedTable(sb);

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
        // columnName is already dialect-escaped - every call site passes the result of
        // GetColumnNameFromSelector, which is backed by the pre-escaped CachedDialectMetadata.
        if (ScalarNeedsDerivedTable)
            return WrapScalarInDerivedTable($"{aggregateFunction}({columnName})");

        var sb = new StringBuilder(128);
        sb.Append("SELECT ");
        sb.Append(aggregateFunction);
        sb.Append('(');
        sb.Append(columnName);
        sb.Append(')');

        // FROM
        sb.Append(" FROM ");
        AppendAliasedTable(sb);

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

        // Handle conversion from database types to C# types, using
        // CultureInfo.InvariantCulture, not the ambient CurrentCulture: providers routinely hand back
        // a string where the column is TEXT/NUMERIC (SQLite in particular), and under a comma-decimal
        // culture (de-DE, fr-FR, ...) Convert.ChangeType("1.5", typeof(decimal)) does not throw - it
        // reads the period as a group separator and returns 15.
        // Matches GroupedJoinedResultMapper.ConvertColumnValue and GridReader.ReadScalar.
        var converted = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
        return (TResult)converted;
    }

    private static CommandOptions<TResult> ToTypedOptions<TResult>(CommandOptions options) =>
        new(transaction: options.Transaction, commandTimeout: options.CommandTimeout, commandType: options.CommandType);

    private string GetColumnNameFromProperty(string propertyName) => _cache.GetColumnName(propertyName);

    private string GetUniqueParamName(string baseName)
    {
        return $"{_dialect.ParameterPrefix}{SanitizeParamName(baseName)}_{_parameters.Count}";
    }

    // AUD-R22: baseName is the raw caller-supplied column name from the string-based
    // Where/And/Or/Set overloads. A space or other character invalid in a SQL parameter
    // identifier (e.g. Where("Order Date", value)) used to be interpolated unsanitized,
    // producing a malformed placeholder (e.g. "@Order Date_0") that fails at execution time.
    private static string SanitizeParamName(string name)
    {
        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                chars[i] = '_';
        }
        return new string(chars);
    }

    private void AddParametersFromObject(object parameters)
    {
        ParameterMetadata[] props = ParameterCache.Get(parameters.GetType());
        for (int i = 0; i < props.Length; i++)
        {
            ParameterMetadata prop = props[i];
            _parameters.Add($"{_dialect.ParameterPrefix}{prop.Name}", prop.Getter(parameters));
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

        // AUD-R26: this route expands the collection itself into individually-named scalars, so it
        // never reached ParameterBinder's ceiling check - .WhereIn(p => p.Id, ids) executed lists
        // that core's Query("... IN @ids") rejected. Same limit, same wording, both routes.
        ParameterCeiling.EnsureWithinLimit(
            _parameters.Count + valueList.Count,
            _dialect,
            ParameterCeiling.Describe(_connection, _dialect));

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

        // AUD-R35: the outer prefix is the alias whenever one was supplied, so from here on the
        // conditions name it and any write terminal has to declare it - which DELETE/UPDATE cannot.
        if (_alias is not null)
            _aliasReferencedInConditions = true;

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
            (subquerySql, ParameterCollection renamedParams) = ParameterRenamer.Rename(subquerySql, subqueryParams, prefix);
            foreach ((string? name, object? value) in renamedParams.GetAll())
            {
                _parameters.Add(name, value);
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
        ThrowIfAliasReferencedByWrite("DELETE");

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
        => CommandObservation.Execute(
            sql, _parameters.ToParameterObject(), _connection, System.Data.CommandType.Text,
            () => ExecuteNonQueryDirect(sql, options));

    private int ExecuteNonQueryDirect(string sql, CommandOptions options)
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

            CommandObservation.Log(sql, _parameters.ToParameterObject());

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && _connection.State != System.Data.ConnectionState.Closed)
                _connection.Close();
        }
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, CommandOptions options, CancellationToken cancellationToken)
        => await CommandObservation.ExecuteAsync(
            sql, _parameters.ToParameterObject(), _connection, System.Data.CommandType.Text,
            () => ExecuteNonQueryDirectAsync(sql, options, cancellationToken), cancellationToken).ConfigureAwait(false);

    private async ValueTask<int> ExecuteNonQueryDirectAsync(string sql, CommandOptions options, CancellationToken cancellationToken)
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

            CommandObservation.Log(sql, _parameters.ToParameterObject());

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

        ParameterMetadata[] props = ParameterCache.Get(values.GetType());
        for (int i = 0; i < props.Length; i++)
        {
            ParameterMetadata prop = props[i];

            // Already dialect-escaped - see comment in BuildAggregateSql.
            string columnName = GetColumnNameFromProperty(prop.Name);
            string paramName = GetUniqueParamName(prop.Name);
            _parameters.Add(paramName, prop.Getter(values));
            _setColumns.Add(new SetColumn(columnName, paramName));
        }
        return this;
    }

    /// <summary>
    /// Sets multiple columns from an anonymous object (ISetClause chaining).
    /// </summary>
    ISetClause<T> ISetClause<T>.Set(object values)
    {

        ParameterMetadata[] props = ParameterCache.Get(values.GetType());
        for (int i = 0; i < props.Length; i++)
        {
            ParameterMetadata prop = props[i];

            // Already dialect-escaped - see comment in BuildAggregateSql.
            string columnName = GetColumnNameFromProperty(prop.Name);
            string paramName = GetUniqueParamName(prop.Name);
            _parameters.Add(paramName, prop.Getter(values));
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
        var paramName = GetUniqueParamName(column);

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

    // The BETWEEN, EXISTS and IN-SUBQUERY families below delegate to the same private Build*Clause
    // helpers the IWhereClause<T> members use; only the returned interface differs, which is why
    // they are explicit implementations rather than another set of public methods.
    /// <summary>
    /// AND BETWEEN condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.AndBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        var sql = BuildBetweenClause(selector, from, to, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    /// <summary>
    /// AND NOT BETWEEN condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.AndNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        var sql = BuildBetweenClause(selector, from, to, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    /// <summary>
    /// OR BETWEEN condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.OrBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        var sql = BuildBetweenClause(selector, from, to, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    /// <summary>
    /// OR NOT BETWEEN condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.OrNotBetween<TValue>(Expression<Func<T, TValue>> selector, TValue from, TValue to)
    {
        var sql = BuildBetweenClause(selector, from, to, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    /// <summary>
    /// AND EXISTS condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.AndExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate)
    {
        var sql = BuildExistsClause<TSubquery>(predicate, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    /// <summary>
    /// AND NOT EXISTS condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.AndNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate)
    {
        var sql = BuildExistsClause<TSubquery>(predicate, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    /// <summary>
    /// OR EXISTS condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.OrExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate)
    {
        var sql = BuildExistsClause<TSubquery>(predicate, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    /// <summary>
    /// OR NOT EXISTS condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.OrNotExists<TSubquery>(Expression<Func<T, TSubquery, bool>> predicate)
    {
        var sql = BuildExistsClause<TSubquery>(predicate, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    /// <summary>
    /// AND IN SUBQUERY condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.AndInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery)
    {
        var sql = BuildInSubqueryClause(selector, subquerySelector, subquery, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    /// <summary>
    /// AND NOT IN SUBQUERY condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.AndNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery)
    {
        var sql = BuildInSubqueryClause(selector, subquerySelector, subquery, negate: true);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    /// <summary>
    /// OR IN SUBQUERY condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.OrInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery)
    {
        var sql = BuildInSubqueryClause(selector, subquerySelector, subquery, negate: false);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    /// <summary>
    /// OR NOT IN SUBQUERY condition for update WHERE clause.
    /// </summary>
    IUpdateWhereClause<T> IUpdateWhereClause<T>.OrNotInSubquery<TValue, TSubquery>(
        Expression<Func<T, TValue>> selector,
        Expression<Func<TSubquery, TValue>> subquerySelector,
        IQueryTerminal<TSubquery> subquery)
    {
        var sql = BuildInSubqueryClause(selector, subquerySelector, subquery, negate: true);
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
        ThrowIfAliasReferencedByWrite("UPDATE");

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
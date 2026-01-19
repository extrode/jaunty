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
    private readonly List<WhereCondition> _conditions = new();
    private readonly List<OrderByColumn> _orderByColumns = new();
    private readonly ParameterCollection _parameters = new();
    private bool _distinct;
    private int? _take;
    private int? _skip;

    internal QueryBuilder(IDbConnection connection)
    {
        _connection = connection;
        _dialect = SqlDialectFactory.GetDialect(connection);
        _metadata = MetadataCache<T>.Metadata;
    }

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
        return _connection.QueryScalar<int>(sql, _parameters.ToParameterObject()!);
    }

    public long LongCount()
    {
        var sql = BuildCountSql();
        return _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!);
    }

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
            return await dbConn.QueryScalarAsync<int>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return Count();
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        var sql = BuildCountSql();
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return LongCount();
    }

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

    #endregion
}

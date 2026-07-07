using System.Data;
using System.Data.Common;
using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Builder for the third JOIN clause in a 3-table join.
/// </summary>
internal sealed class JoinClause3Builder<T1, T2, T3> : IJoinClause<T1, T2, T3>
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    private readonly JoinedQueryBuilder<T1, T2> _parent;
    private readonly JoinType _joinType;
    private readonly string? _alias;
    private readonly EntityMetadata _metadata;

    public JoinClause3Builder(JoinedQueryBuilder<T1, T2> parent, JoinType joinType, string? alias)
    {
        _parent = parent;
        _joinType = joinType;
        _alias = alias;
        _metadata = FluentMetadataCache.GetMetadata<T3>();
    }

    public IJoinedQuery3<T1, T2, T3> On<TLeftKey, TRightKey>(Expression<Func<T1, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T1>(), leftProp, _parent.FromAlias);
        string rightColumn = GetColumnName(_metadata, rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery3(condition);
    }

    public IJoinedQuery3<T1, T2, T3> OnFromSecond<TLeftKey, TRightKey>(Expression<Func<T2, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T2>(), leftProp, _parent.Joins[0].Alias);
        string rightColumn = GetColumnName(_metadata, rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery3(condition);
    }

    public IJoinedQuery3<T1, T2, T3> On(string leftColumn, string rightColumn)
        => CreateJoinedQuery3($"{leftColumn} = {rightColumn}");

    public IJoinedQuery3<T1, T2, T3> On(string condition)
        => CreateJoinedQuery3(condition);

    private JoinedQuery3Builder<T1, T2, T3> CreateJoinedQuery3(string onCondition)
    {
        var joinInfo = new JoinInfo(
            _joinType,
            _metadata.TableName,
            _metadata.SchemaName,
            _alias,
            onCondition);

        _parent.AddJoin(joinInfo);
        return new JoinedQuery3Builder<T1, T2, T3>(_parent);
    }

    private string GetColumnName(EntityMetadata metadata, string propertyName, string? alias)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        string columnName = propertyName;

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].PropertyName == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        string escaped = _parent.Dialect.EscapeColumnName(columnName);
        string prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }
}

/// <summary>
/// Query builder for 3-table joins.
/// </summary>
internal sealed partial class JoinedQuery3Builder<T1, T2, T3> : IJoinedQuery3<T1, T2, T3>
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    internal readonly JoinedQueryBuilder<T1, T2> _parent;

    public JoinedQuery3Builder(JoinedQueryBuilder<T1, T2> parent) => _parent = parent;

    // ==================== WHERE ====================

    public IJoinedQuery3<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor3<T1, T2, T3>(
            _parent.Dialect,
            _parent.FromAlias,
            _parent.Joins[0].Alias,
            _parent.Joins[1].Alias);

        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        _parent.AddWhereExpression(sql, parameters, LogicalOperator.None);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> Where(string condition)
    {
        _parent.AddWhereCondition(WhereCondition.Raw(condition, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> And(Expression<Func<T1, T2, T3, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor3<T1, T2, T3>(
            _parent.Dialect,
            _parent.FromAlias,
            _parent.Joins[0].Alias,
            _parent.Joins[1].Alias);

        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        _parent.AddWhereExpression(sql, parameters, LogicalOperator.And);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> Or(Expression<Func<T1, T2, T3, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor3<T1, T2, T3>(
            _parent.Dialect,
            _parent.FromAlias,
            _parent.Joins[0].Alias,
            _parent.Joins[1].Alias);

        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        _parent.AddWhereExpression(sql, parameters, LogicalOperator.Or);
        return this;
    }

    // ==================== ORDER BY ====================

    public IJoinedQuery3<T1, T2, T3> OrderBy<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T1>(), propertyName, _parent.FromAlias);
        _parent.AddOrderByColumn(columnName, "ASC", isFirst: _parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> OrderByDescending<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T1>(), propertyName, _parent.FromAlias);
        _parent.AddOrderByColumn(columnName, "DESC", isFirst: _parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> OrderByJoined<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T2>(), propertyName, _parent.Joins[0].Alias);
        _parent.AddOrderByColumn(columnName, "ASC", isFirst: _parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> OrderByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T2>(), propertyName, _parent.Joins[0].Alias);
        _parent.AddOrderByColumn(columnName, "DESC", isFirst: _parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> OrderByJoined<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T3>(), propertyName, _parent.Joins[1].Alias);
        _parent.AddOrderByColumn(columnName, "ASC", isFirst: _parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> OrderByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T3>(), propertyName, _parent.Joins[1].Alias);
        _parent.AddOrderByColumn(columnName, "DESC", isFirst: _parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenBy<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T1>(), propertyName, _parent.FromAlias);
        _parent.AddOrderByColumn(columnName, "ASC", isFirst: false);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenByDescending<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T1>(), propertyName, _parent.FromAlias);
        _parent.AddOrderByColumn(columnName, "DESC", isFirst: false);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenByJoined<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T2>(), propertyName, _parent.Joins[0].Alias);
        _parent.AddOrderByColumn(columnName, "ASC", isFirst: false);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T2>(), propertyName, _parent.Joins[0].Alias);
        _parent.AddOrderByColumn(columnName, "DESC", isFirst: false);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenByJoined<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T3>(), propertyName, _parent.Joins[1].Alias);
        _parent.AddOrderByColumn(columnName, "ASC", isFirst: false);
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T3>(), propertyName, _parent.Joins[1].Alias);
        _parent.AddOrderByColumn(columnName, "DESC", isFirst: false);
        return this;
    }

    // ==================== SELECT ====================

    public List<T1> Select() => _parent.Select();

    public List<(T1, T2, T3)> SelectAll()
    {
        EntityMetadata t1Metadata = FluentMetadataCache.GetMetadata<T1>();
        EntityMetadata t2Metadata = FluentMetadataCache.GetMetadata<T2>();
        EntityMetadata t3Metadata = FluentMetadataCache.GetMetadata<T3>();

        string[] t1Columns = _parent.GetPrefixedColumnsWithAlias(t1Metadata, _parent.FromAlias, "t1_");
        string[] t2Columns = _parent.GetPrefixedColumnsWithAlias(t2Metadata, _parent.Joins[0].Alias, "t2_");
        string[] t3Columns = _parent.GetPrefixedColumnsWithAlias(t3Metadata, _parent.Joins[1].Alias, "t3_");
        string[] allColumns = t1Columns.Concat(t2Columns).Concat(t3Columns).ToArray();

        string sql = _parent.BuildSelectSql(allColumns);
        var results = new List<(T1, T2, T3)>();

        using IDbCommand command = _parent.Connection.CreateCommand();
        command.CommandText = sql;
        _parent.BindParameters(command);

        bool wasClosed = _parent.Connection.State == ConnectionState.Closed;
        if (wasClosed)
            _parent.Connection.Open();

        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                T1? t1 = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_");
                T2? t2 = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_");
                T3? t3 = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_");
                results.Add((t1, t2, t3));
            }
        }
        finally
        {
            if (wasClosed)
                _parent.Connection.Close();
        }

        return results;
    }

    public T1 SelectFirst()
    {
        var results = Select();
        return results.FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public T1? SelectFirstOrDefault()
    {
        var results = Select();
        return results.FirstOrDefault();
    }

    public T1 SelectSingle()
    {
        var results = Select();
        if (results.Count == 0) throw new InvalidOperationException("Sequence contains no elements");
        if (results.Count > 1) throw new InvalidOperationException("Sequence contains more than one element");
        return results[0];
    }

    public T1? SelectSingleOrDefault()
    {
        var results = Select();
        if (results.Count == 0) return default;
        if (results.Count > 1) throw new InvalidOperationException("Sequence contains more than one element");
        return results[0];
    }

    public int Count()
    {
        string sql = _parent.BuildCountSql();
        return _parent.Connection.QueryScalar<int>(sql, _parent.GetParameters().ToParameterObject()!);
    }

    public long LongCount()
    {
        string sql = _parent.BuildCountSql();
        return _parent.Connection.QueryScalar<long>(sql, _parent.GetParameters().ToParameterObject()!);
    }

    public string ToSql() => _parent.ToSql();

    // ==================== SELECT PARTIAL ====================

    public List<IDictionary<string, object?>> SelectPartial(string columns)
    {
        string sql = _parent.BuildSelectPartialSql(columns);
        return _parent.Connection.QueryPartialList(sql, _parent.GetParameters().ToParameterObject()!);
    }

    public IDictionary<string, object?> SelectPartialFirst(string columns)
    {
        var results = SelectPartial(columns);
        return results.FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public IDictionary<string, object?>? SelectPartialFirstOrDefault(string columns)
    {
        var results = SelectPartial(columns);
        return results.FirstOrDefault();
    }

    public IDictionary<string, object?> SelectPartialSingle(string columns)
    {
        List<IDictionary<string, object?>> results = SelectPartial(columns);
        if (results.Count == 0) throw new InvalidOperationException("Sequence contains no elements");
        if (results.Count > 1) throw new InvalidOperationException("Sequence contains more than one element");
        return results[0];
    }

    public IDictionary<string, object?>? SelectPartialSingleOrDefault(string columns)
    {
        var results = SelectPartial(columns);
        if (results.Count == 0) return null;
        if (results.Count > 1) throw new InvalidOperationException("Sequence contains more than one element");
        return results[0];
    }

    public async Task<List<T1>> SelectAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildSelectSql(_parent.GetSelectColumns<T1>());
        return await _parent.Connection.QueryPartialAsync<T1>(sql, _parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<(T1, T2, T3)>> SelectAllAsync(CancellationToken cancellationToken = default)
    {
        if (_parent.Connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async operations require a DbConnection");

        EntityMetadata t1Metadata = FluentMetadataCache.GetMetadata<T1>();
        EntityMetadata t2Metadata = FluentMetadataCache.GetMetadata<T2>();
        EntityMetadata t3Metadata = FluentMetadataCache.GetMetadata<T3>();

        string[] t1Columns = _parent.GetPrefixedColumnsWithAlias(t1Metadata, _parent.FromAlias, "t1_");
        string[] t2Columns = _parent.GetPrefixedColumnsWithAlias(t2Metadata, _parent.Joins[0].Alias, "t2_");
        string[] t3Columns = _parent.GetPrefixedColumnsWithAlias(t3Metadata, _parent.Joins[1].Alias, "t3_");
        string[] allColumns = t1Columns.Concat(t2Columns).Concat(t3Columns).ToArray();

        string sql = _parent.BuildSelectSql(allColumns);
        var results = new List<(T1, T2, T3)>();

        using DbCommand command = dbConnection.CreateCommand();
        command.CommandText = sql;
        _parent.BindParameters(command);

        bool wasClosed = _parent.Connection.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                T1? t1 = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_");
                T2? t2 = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_");
                T3? t3 = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_");
                results.Add((t1, t2, t3));
            }
        }
        finally
        {
            if (wasClosed)
                _parent.Connection.Close();
        }

        return results;
    }

    public async Task<T1> SelectFirstAsync(CancellationToken cancellationToken = default)
    {
        List<T1> results = await SelectAsync(cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public async Task<T1?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        List<T1> results = await SelectAsync(cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildCountSql();
        return await _parent.Connection.QueryScalarAsync<int>(sql, _parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildCountSql();
        return await _parent.Connection.QueryScalarAsync<long>(sql, _parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<IDictionary<string, object?>>> SelectPartialAsync(string columns, CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildSelectPartialSql(columns);
        return await _parent.Connection.QueryPartialListAsync(sql, _parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IDictionary<string, object?>> SelectPartialFirstAsync(string columns, CancellationToken cancellationToken = default)
    {
        var results = await SelectPartialAsync(columns, cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public async Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(string columns, CancellationToken cancellationToken = default)
    {
        List<IDictionary<string, object?>> results = await SelectPartialAsync(columns, cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    // ==================== HELPERS ====================

    private string GetColumnNameForOrderBy(EntityMetadata metadata, string propertyName, string? alias)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        string columnName = propertyName;

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].PropertyName == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        string escaped = _parent.Dialect.EscapeColumnName(columnName);
        string prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }
}

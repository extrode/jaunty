using System.Data;
using System.Data.Common;
using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Builder for the fourth JOIN clause in a 4-table join.
/// </summary>
internal sealed class JoinClause4Builder<T1, T2, T3, T4> : IJoinClause<T1, T2, T3, T4>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    private readonly JoinedQuery3Builder<T1, T2, T3> _parent;
    private readonly JoinType _joinType;
    private readonly string? _alias;
    private readonly EntityMetadata _metadata;

    public JoinClause4Builder(JoinedQuery3Builder<T1, T2, T3> parent, JoinType joinType, string? alias)
    {
        _parent = parent;
        _joinType = joinType;
        _alias = alias;
        _metadata = FluentMetadataCache.GetMetadata<T4>();
    }

    public IJoinedQuery4<T1, T2, T3, T4> On<TLeftKey, TRightKey>(
        Expression<Func<T1, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T1>(), leftProp, _parent._parent.FromAlias);
        string rightColumn = GetColumnName(_metadata, rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery4(condition);
    }

    public IJoinedQuery4<T1, T2, T3, T4> OnFromSecond<TLeftKey, TRightKey>(
        Expression<Func<T2, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T2>(), leftProp, _parent._parent.Joins[0].Alias);
        string rightColumn = GetColumnName(_metadata, rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery4(condition);
    }

    public IJoinedQuery4<T1, T2, T3, T4> OnFromThird<TLeftKey, TRightKey>(
        Expression<Func<T3, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T3>(), leftProp, _parent._parent.Joins[1].Alias);
        string rightColumn = GetColumnName(_metadata, rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery4(condition);
    }

    public IJoinedQuery4<T1, T2, T3, T4> On(string leftColumn, string rightColumn) =>
        CreateJoinedQuery4($"{leftColumn} = {rightColumn}");

    public IJoinedQuery4<T1, T2, T3, T4> On(string condition) => CreateJoinedQuery4(condition);

    private JoinedQuery4Builder<T1, T2, T3, T4> CreateJoinedQuery4(string onCondition)
    {
        var joinInfo = new JoinInfo(
            _joinType,
            _metadata.TableName,
            _metadata.SchemaName,
            _alias,
            onCondition);

        _parent._parent.AddJoin(joinInfo);
        return new JoinedQuery4Builder<T1, T2, T3, T4>(_parent);
    }

    private string GetColumnName(EntityMetadata metadata, string propertyName, string? alias)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        string columnName = propertyName;

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        string escaped = _parent._parent.Dialect.EscapeColumnName(columnName);
        string prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }
}

/// <summary>
/// Query builder for 4-table joins.
/// </summary>
internal sealed partial class JoinedQuery4Builder<T1, T2, T3, T4> : IJoinedQuery4<T1, T2, T3, T4>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    private readonly JoinedQuery3Builder<T1, T2, T3> _parent;

    public JoinedQuery4Builder(JoinedQuery3Builder<T1, T2, T3> parent) => _parent = parent;

    // ==================== WHERE ====================

    public IJoinedQuery4<T1, T2, T3, T4> Where(Expression<Func<T1, T2, T3, T4, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor4<T1, T2, T3, T4>(
            _parent._parent.Dialect,
            _parent._parent.FromAlias,
            _parent._parent.Joins[0].Alias,
            _parent._parent.Joins[1].Alias,
            _parent._parent.Joins[2].Alias);

        string sql = visitor.Translate(predicate);
        _parent._parent.AddWhereCondition(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> Where(string condition)
    {
        _parent._parent.AddWhereCondition(WhereCondition.Raw(condition, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> And(Expression<Func<T1, T2, T3, T4, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor4<T1, T2, T3, T4>(
            _parent._parent.Dialect,
            _parent._parent.FromAlias,
            _parent._parent.Joins[0].Alias,
            _parent._parent.Joins[1].Alias,
            _parent._parent.Joins[2].Alias);

        string sql = visitor.Translate(predicate);
        _parent._parent.AddWhereCondition(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> Or(Expression<Func<T1, T2, T3, T4, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor4<T1, T2, T3, T4>(
            _parent._parent.Dialect,
            _parent._parent.FromAlias,
            _parent._parent.Joins[0].Alias,
            _parent._parent.Joins[1].Alias,
            _parent._parent.Joins[2].Alias);

        string sql = visitor.Translate(predicate);
        _parent._parent.AddWhereCondition(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    // ==================== ORDER BY ====================

    public IJoinedQuery4<T1, T2, T3, T4> OrderBy<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T1>(), propertyName, _parent._parent.FromAlias);
        _parent._parent.AddOrderByColumn(columnName, "ASC", isFirst: _parent._parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByDescending<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T1>(), propertyName, _parent._parent.FromAlias);
        _parent._parent.AddOrderByColumn(columnName, "DESC", isFirst: _parent._parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoined<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T2>(), propertyName, _parent._parent.Joins[0].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC", isFirst: _parent._parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T2>(), propertyName, _parent._parent.Joins[0].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC", isFirst: _parent._parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoined<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T3>(), propertyName, _parent._parent.Joins[1].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC", isFirst: _parent._parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T3>(), propertyName, _parent._parent.Joins[1].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC", isFirst: _parent._parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoined<TKey>(Expression<Func<T4, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T4>(), propertyName, _parent._parent.Joins[2].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC", isFirst: _parent._parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoinedDescending<TKey>(Expression<Func<T4, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T4>(), propertyName, _parent._parent.Joins[2].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC", isFirst: _parent._parent.GetOrderByColumns().Count == 0);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenBy<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T1>(), propertyName, _parent._parent.FromAlias);
        _parent._parent.AddOrderByColumn(columnName, "ASC", isFirst: false);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByDescending<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T1>(), propertyName, _parent._parent.FromAlias);
        _parent._parent.AddOrderByColumn(columnName, "DESC", isFirst: false);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoined<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T2>(), propertyName, _parent._parent.Joins[0].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC", isFirst: false);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T2>(), propertyName, _parent._parent.Joins[0].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC", isFirst: false);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoined<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T3>(), propertyName, _parent._parent.Joins[1].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC", isFirst: false);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T3>(), propertyName, _parent._parent.Joins[1].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC", isFirst: false);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoined<TKey>(Expression<Func<T4, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T4>(), propertyName, _parent._parent.Joins[2].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC", isFirst: false);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoinedDescending<TKey>(Expression<Func<T4, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy(FluentMetadataCache.GetMetadata<T4>(), propertyName, _parent._parent.Joins[2].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC", isFirst: false);
        return this;
    }

    // ==================== SELECT ====================

    public List<T1> Select() => _parent._parent.Select();

    public List<(T1, T2, T3, T4)> SelectAll()
    {
        EntityMetadata t1Metadata = FluentMetadataCache.GetMetadata<T1>();
        EntityMetadata t2Metadata = FluentMetadataCache.GetMetadata<T2>();
        EntityMetadata t3Metadata = FluentMetadataCache.GetMetadata<T3>();
        EntityMetadata t4Metadata = FluentMetadataCache.GetMetadata<T4>();

        string[] t1Columns = _parent._parent.GetPrefixedColumnsWithAlias(t1Metadata, _parent._parent.FromAlias, "t1_");
        string[] t2Columns = _parent._parent.GetPrefixedColumnsWithAlias(t2Metadata, _parent._parent.Joins[0].Alias, "t2_");
        string[] t3Columns = _parent._parent.GetPrefixedColumnsWithAlias(t3Metadata, _parent._parent.Joins[1].Alias, "t3_");
        string[] t4Columns = _parent._parent.GetPrefixedColumnsWithAlias(t4Metadata, _parent._parent.Joins[2].Alias, "t4_");
        string[] allColumns = t1Columns.Concat(t2Columns).Concat(t3Columns).Concat(t4Columns).ToArray();

        string sql = _parent._parent.BuildSelectSql(allColumns);
        var results = new List<(T1, T2, T3, T4)>();

        using IDbCommand command = _parent._parent.Connection.CreateCommand();
        command.CommandText = sql;
        _parent._parent.BindParameters(command);

        bool wasClosed = _parent._parent.Connection.State == ConnectionState.Closed;
        if (wasClosed)
            _parent._parent.Connection.Open();

        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                T1? t1 = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_");
                T2? t2 = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_");
                T3? t3 = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_");
                T4? t4 = JoinedQueryBuilder<T1, T2>.MapEntity<T4>(t4Metadata, reader, "t4_");
                results.Add((t1, t2, t3, t4));
            }
        }
        finally
        {
            if (wasClosed)
                _parent._parent.Connection.Close();
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
        string sql = _parent._parent.BuildCountSql();
        return _parent._parent.Connection.QueryScalar<int>(sql);
    }

    public long LongCount()
    {
        string sql = _parent._parent.BuildCountSql();
        return _parent._parent.Connection.QueryScalar<long>(sql);
    }

    public string ToSql() => _parent._parent.ToSql();

    // ==================== SELECT PARTIAL ====================

    public List<IDictionary<string, object?>> SelectPartial(string columns)
    {
        string sql = _parent._parent.BuildSelectPartialSql(columns);
        return _parent._parent.Connection.QueryPartialList(sql);
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
        var results = SelectPartial(columns);
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

    // ==================== ASYNC ====================

    public async Task<List<T1>> SelectAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent._parent.BuildSelectSql(_parent._parent.GetSelectColumns<T1>());
        return await _parent._parent.Connection.QueryPartialAsync<T1>(sql, cancellationToken);
    }

    public async Task<List<(T1, T2, T3, T4)>> SelectAllAsync(CancellationToken cancellationToken = default)
    {
        EntityMetadata t1Metadata = FluentMetadataCache.GetMetadata<T1>();
        EntityMetadata t2Metadata = FluentMetadataCache.GetMetadata<T2>();
        EntityMetadata t3Metadata = FluentMetadataCache.GetMetadata<T3>();
        EntityMetadata t4Metadata = FluentMetadataCache.GetMetadata<T4>();

        string[] t1Columns = _parent._parent.GetPrefixedColumnsWithAlias(t1Metadata, _parent._parent.FromAlias, "t1_");
        string[] t2Columns = _parent._parent.GetPrefixedColumnsWithAlias(t2Metadata, _parent._parent.Joins[0].Alias, "t2_");
        string[] t3Columns = _parent._parent.GetPrefixedColumnsWithAlias(t3Metadata, _parent._parent.Joins[1].Alias, "t3_");
        string[] t4Columns = _parent._parent.GetPrefixedColumnsWithAlias(t4Metadata, _parent._parent.Joins[2].Alias, "t4_");
        string[] allColumns = t1Columns.Concat(t2Columns).Concat(t3Columns).Concat(t4Columns).ToArray();

        string sql = _parent._parent.BuildSelectSql(allColumns);
        var results = new List<(T1, T2, T3, T4)>();

        if (_parent._parent.Connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async operations require a DbConnection");

        using DbCommand command = dbConnection.CreateCommand();
        command.CommandText = sql;
        _parent._parent.BindParameters(command);

        bool wasClosed = _parent._parent.Connection.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConnection.OpenAsync(cancellationToken);

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                T1? t1 = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_");
                T2? t2 = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_");
                T3? t3 = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_");
                T4? t4 = JoinedQueryBuilder<T1, T2>.MapEntity<T4>(t4Metadata, reader, "t4_");
                results.Add((t1, t2, t3, t4));
            }
        }
        finally
        {
            if (wasClosed)
                _parent._parent.Connection.Close();
        }

        return results;
    }

    public async Task<T1> SelectFirstAsync(CancellationToken cancellationToken = default)
    {
        var results = await SelectAsync(cancellationToken);
        return results.FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public async Task<T1?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var results = await SelectAsync(cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent._parent.BuildCountSql();
        return await _parent._parent.Connection.QueryScalarAsync<int>(sql, cancellationToken);
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent._parent.BuildCountSql();
        return await _parent._parent.Connection.QueryScalarAsync<long>(sql, cancellationToken);
    }

    public async Task<List<IDictionary<string, object?>>> SelectPartialAsync(string columns, CancellationToken cancellationToken = default)
    {
        string sql = _parent._parent.BuildSelectPartialSql(columns);
        return await _parent._parent.Connection.QueryPartialListAsync(sql, cancellationToken);
    }

    public async Task<IDictionary<string, object?>> SelectPartialFirstAsync(string columns, CancellationToken cancellationToken = default)
    {
        var results = await SelectPartialAsync(columns, cancellationToken);
        return results.FirstOrDefault() ?? throw new InvalidOperationException("Sequence contains no elements");
    }

    public async Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(string columns, CancellationToken cancellationToken = default)
    {
        var results = await SelectPartialAsync(columns, cancellationToken);
        return results.FirstOrDefault();
    }

    // ==================== HELPERS ====================

    private string GetColumnNameForOrderBy(EntityMetadata metadata, string propertyName, string? alias)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        string columnName = propertyName;

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        string escaped = _parent._parent.Dialect.EscapeColumnName(columnName);
        string prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }
}

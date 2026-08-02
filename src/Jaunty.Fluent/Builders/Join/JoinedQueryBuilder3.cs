using System.Data;
using System.Data.Common;
using System.Linq.Expressions;

using Jaunty.Core;
using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals;

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

        string leftColumn = GetColumnName<T1>(leftProp, _parent.FromAlias);
        string rightColumn = GetColumnName<T3>(rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery3(condition);
    }

    public IJoinedQuery3<T1, T2, T3> OnFromSecond<TLeftKey, TRightKey>(Expression<Func<T2, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName<T2>(leftProp, _parent.Joins[0].Alias);
        string rightColumn = GetColumnName<T3>(rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery3(condition);
    }

    public IJoinedQuery3<T1, T2, T3> On(Expression<Func<T1, T2, T3, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor3<T1, T2, T3>(
            _parent.Dialect,
            _parent.FromAlias,
            _parent.Joins[0].Alias,
            _alias);

        (string condition, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);

        // Renumbered against the query-wide sequence: each visitor mints its value parameters
        // from a counter that restarts at 0, so by the third join "jp0" is usually already bound.
        return CreateJoinedQuery3(_parent.RegisterExpressionParameters(condition, parameters));
    }

    public IJoinedQuery3<T1, T2, T3> On(string leftColumn, string rightColumn)
        => CreateJoinedQuery3($"{leftColumn} = {rightColumn}");

    public IJoinedQuery3<T1, T2, T3> On(string condition)
        => CreateJoinedQuery3(condition);

    public IJoinedQuery3<T1, T2, T3> On<TValue>(string condition, TValue value)
        => On(condition, JoinParameterName.Default, value);

    public IJoinedQuery3<T1, T2, T3> On<TValue>(string condition, string parameterName, TValue value)
    {
        string qualified = JoinParameterName.Qualify(_parent.Dialect.ParameterPrefix, parameterName, nameof(parameterName));

        if (_parent.HasParameter(qualified))
            throw JoinParameterName.DuplicateError(qualified, nameof(parameterName));

        JoinedQuery3Builder<T1, T2, T3> joinedQuery = CreateJoinedQuery3(condition);
        _parent.AddParameter(qualified, value);
        return joinedQuery;
    }

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

    private string GetColumnName<T>(string propertyName, string? alias) where T : new()
    {
        // R29: was a linear scan of metadata.Columns plus a fresh EscapeColumnName per call; the
        // arity-2 helpers were converted to the pre-escaped CachedDialectMetadata lookup under
        // AUD-R26-058 and these string-overload helpers were left on the old shape.
        EntityMetadata metadata = FluentMetadataCache.GetMetadata<T>();
        CachedDialectMetadata cached = FluentMetadataCache.GetForDialect<T>(_parent.Dialect);

        string escaped = cached.GetColumnName(propertyName);
        string prefix = alias ?? _parent.Dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
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

    public IJoinedQuery3<T1, T2, T3> Where(string column, object value)
    {
        string paramName = $"{_parent.Dialect.ParameterPrefix}{column.Replace(".", "_")}";
        string escapedColumn = EscapeQualifiedColumn(column);
        _parent.AddWhereCondition(WhereCondition.Column($"{escapedColumn} = {paramName}", LogicalOperator.None));
        _parent.GetParameters().Add(paramName, value);
        return this;
    }

    // The alias prefix (if any) is validated as a plain identifier - not dialect-escaped, since
    // aliases are library-controlled bare names in the generated SQL, not user data - so a caller
    // can't smuggle arbitrary SQL text through the alias segment while the column name is escaped.
    private string EscapeQualifiedColumn(string column)
    {
        int dotIndex = column.IndexOf('.');
        if (dotIndex < 0)
            return _parent.Dialect.EscapeColumnName(column);

        string alias = column.Substring(0, dotIndex);
        string columnName = column.Substring(dotIndex + 1);
        SqlIdentifierValidator.Validate(alias, nameof(column));
        return $"{alias}.{_parent.Dialect.EscapeColumnName(columnName)}";
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
        string columnName = GetColumnNameForOrderBy<T1>(propertyName, _parent.FromAlias);
        _parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> OrderByDescending<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T1>(propertyName, _parent.FromAlias);
        _parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> OrderByJoined<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T2>(propertyName, _parent.Joins[0].Alias);
        _parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> OrderByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T2>(propertyName, _parent.Joins[0].Alias);
        _parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> OrderByJoined<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T3>(propertyName, _parent.Joins[1].Alias);
        _parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> OrderByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T3>(propertyName, _parent.Joins[1].Alias);
        _parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenBy<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T1>(propertyName, _parent.FromAlias);
        _parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenByDescending<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T1>(propertyName, _parent.FromAlias);
        _parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenByJoined<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T2>(propertyName, _parent.Joins[0].Alias);
        _parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T2>(propertyName, _parent.Joins[0].Alias);
        _parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenByJoined<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T3>(propertyName, _parent.Joins[1].Alias);
        _parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> ThenByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T3>(propertyName, _parent.Joins[1].Alias);
        _parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    // ==================== GROUP BY ====================

    public IGroupedJoinedQuery3<T1, T2, T3, TKey> GroupBy<TKey>(Expression<Func<T1, T2, T3, TKey>> keySelector)
    {
        return new GroupedJoinedQueryBuilder3<T1, T2, T3, TKey>(this, keySelector);
    }

    // ==================== SELECT ====================

    public List<T1> Select() => _parent.Select();

    public List<T1> Select(CommandOptions options) => _parent.Select(options);

    public List<(T1, T2, T3)> SelectAll() => SelectAll(default);

    /// <summary>
    /// AUD-R34-016. <c>SelectAll</c> builds and executes its own command, and had no
    /// <c>CommandOptions</c> overload to take a transaction or timeout from - so on a provider
    /// that validates the pairing (SqlClient, Microsoft.Data.Sqlite) the whole tuple-returning
    /// surface of a 3-way join threw inside a caller's transaction rather than joining it.
    /// </summary>
    public List<(T1, T2, T3)> SelectAll(CommandOptions options)
    {
        EntityMetadata t1Metadata = FluentMetadataCache.GetMetadata<T1>();
        EntityMetadata t2Metadata = FluentMetadataCache.GetMetadata<T2>();
        EntityMetadata t3Metadata = FluentMetadataCache.GetMetadata<T3>();

        string[] t1Columns = _parent.GetPrefixedColumnsWithAlias(t1Metadata, _parent.FromAlias, "t1_");
        string[] t2Columns = _parent.GetPrefixedColumnsWithAlias(t2Metadata, _parent.Joins[0].Alias, "t2_");
        string[] t3Columns = _parent.GetPrefixedColumnsWithAlias(t3Metadata, _parent.Joins[1].Alias, "t3_");
        string[] allColumns = t1Columns.Concat(t2Columns).Concat(t3Columns).ToArray();

        string sql = _parent.BuildSelectSql(allColumns);

        return CommandObservation.Execute(
            sql, _parent.DescribeParameters(), _parent.Connection, CommandType.Text, Body);

        List<(T1, T2, T3)> Body()
        {
            var results = new List<(T1, T2, T3)>();

            using IDbCommand command = _parent.Connection.CreateCommand();
            command.CommandText = sql;
            _parent.BindParameters(command);
            FluentCommandOptions.Apply(command, _parent.Connection, options);

            CommandObservation.Log(sql, _parent.DescribeParameters());

            bool wasClosed = _parent.Connection.State == ConnectionState.Closed;
            if (wasClosed)
                _parent.Connection.Open();

            try
            {
                using IDataReader reader = command.ExecuteReader();
                Dictionary<string, int> ordinals = JoinedQueryBuilder<T1, T2>.BuildOrdinalLookup(reader);

                while (reader.Read())
                {
                    T1? t1 = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_", ordinals);
                    T2? t2 = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_", ordinals);
                    T3? t3 = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_", ordinals);
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
    }

    // AUD-R12: these previously fetched the entire result set via Select() and took the
    // first/only element in C#, instead of using SQL-level LIMIT/paging. _parent (the 2-way
    // JoinedQueryBuilder<T1,T2> that carries this query's T1 metadata/alias and every registered
    // join, including T3's) already implements these efficiently via _dialect.GetPagingSql, so
    // delegate to it directly - mirroring how Select() above already delegates to _parent.Select().
    public T1 SelectFirst() => _parent.SelectFirst();

    public T1 SelectFirst(CommandOptions options) => _parent.SelectFirst(options);

    public T1? SelectFirstOrDefault() => _parent.SelectFirstOrDefault();

    public T1? SelectFirstOrDefault(CommandOptions options) => _parent.SelectFirstOrDefault(options);

    public T1 SelectSingle() => _parent.SelectSingle();

    public T1 SelectSingle(CommandOptions options) => _parent.SelectSingle(options);

    public T1? SelectSingleOrDefault() => _parent.SelectSingleOrDefault();

    public T1? SelectSingleOrDefault(CommandOptions options) => _parent.SelectSingleOrDefault(options);

    public int Count()
    {
        string sql = _parent.BuildCountSql();
        return _parent.Connection.QueryScalar<int>(sql, _parent.GetParameters().ToParameterObject()!);
    }

    public int Count(CommandOptions options)
    {
        string sql = _parent.BuildCountSql();
        return _parent.Connection.QueryScalar<int>(sql, _parent.GetParameters().ToParameterObject()!, ToTypedOptions<int>(options));
    }

    public long LongCount()
    {
        string sql = _parent.BuildCountSql();
        return _parent.Connection.QueryScalar<long>(sql, _parent.GetParameters().ToParameterObject()!);
    }

    public long LongCount(CommandOptions options)
    {
        string sql = _parent.BuildCountSql();
        return _parent.Connection.QueryScalar<long>(sql, _parent.GetParameters().ToParameterObject()!, ToTypedOptions<long>(options));
    }

    public string ToSql() => _parent.ToSql();

    // ==================== SELECT PARTIAL ====================

    public List<IDictionary<string, object?>> SelectPartial(string columns)
    {
        string sql = _parent.BuildSelectPartialSql(columns);
        return _parent.Connection.QueryPartialList(sql, _parent.GetParameters().ToParameterObject()!);
    }

    // AUD-R12: same fetch-all-then-take-first/single issue as the SelectFirst/SelectSingle
    // family above - delegate to _parent, which already applies GetPagingSql(0, 1)/(0, 2).
    public IDictionary<string, object?> SelectPartialFirst(string columns) => _parent.SelectPartialFirst(columns);

    public IDictionary<string, object?>? SelectPartialFirstOrDefault(string columns) => _parent.SelectPartialFirstOrDefault(columns);

    public IDictionary<string, object?> SelectPartialSingle(string columns) => _parent.SelectPartialSingle(columns);

    public IDictionary<string, object?>? SelectPartialSingleOrDefault(string columns) => _parent.SelectPartialSingleOrDefault(columns);

    public async Task<List<T1>> SelectAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildSelectSql(_parent.GetSelectColumns<T1>());
        return await _parent.Connection.QueryPartialAsync<T1>(sql, _parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<T1>> SelectAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildSelectSql(_parent.GetSelectColumns<T1>());
        return await _parent.Connection.QueryPartialAsync<T1>(sql, _parent.GetParameters().ToParameterObject()!, ToTypedOptions<T1>(options), cancellationToken).ConfigureAwait(false);
    }

    public Task<List<(T1, T2, T3)>> SelectAllAsync(CancellationToken cancellationToken = default)
        => SelectAllAsync(default, cancellationToken);

    /// <summary>AUD-R34-016: see the synchronous <see cref="SelectAll(CommandOptions)"/>.</summary>
    public async Task<List<(T1, T2, T3)>> SelectAllAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_parent.Connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        EntityMetadata t1Metadata = FluentMetadataCache.GetMetadata<T1>();
        EntityMetadata t2Metadata = FluentMetadataCache.GetMetadata<T2>();
        EntityMetadata t3Metadata = FluentMetadataCache.GetMetadata<T3>();

        string[] t1Columns = _parent.GetPrefixedColumnsWithAlias(t1Metadata, _parent.FromAlias, "t1_");
        string[] t2Columns = _parent.GetPrefixedColumnsWithAlias(t2Metadata, _parent.Joins[0].Alias, "t2_");
        string[] t3Columns = _parent.GetPrefixedColumnsWithAlias(t3Metadata, _parent.Joins[1].Alias, "t3_");
        string[] allColumns = t1Columns.Concat(t2Columns).Concat(t3Columns).ToArray();

        string sql = _parent.BuildSelectSql(allColumns);

        return await CommandObservation.ExecuteAsync(
            sql, _parent.DescribeParameters(), _parent.Connection, CommandType.Text, Body, cancellationToken).ConfigureAwait(false);

        async ValueTask<List<(T1, T2, T3)>> Body()
        {
            var results = new List<(T1, T2, T3)>();

            using DbCommand command = dbConnection.CreateCommand();
            command.CommandText = sql;
            _parent.BindParameters(command);
            FluentCommandOptions.Apply(command, _parent.Connection, options);

            CommandObservation.Log(sql, _parent.DescribeParameters());

            bool wasClosed = _parent.Connection.State == ConnectionState.Closed;
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                Dictionary<string, int> ordinals = JoinedQueryBuilder<T1, T2>.BuildOrdinalLookup(reader);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    T1? t1 = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_", ordinals);
                    T2? t2 = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_", ordinals);
                    T3? t3 = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_", ordinals);
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
    }

    // AUD-R12: same fetch-all-then-take-first issue as the sync members above - delegate to
    // _parent, which already applies GetPagingSql(0, 1).
    public Task<T1> SelectFirstAsync(CancellationToken cancellationToken = default) => _parent.SelectFirstAsync(cancellationToken);

    public Task<T1> SelectFirstAsync(CommandOptions options, CancellationToken cancellationToken = default) => _parent.SelectFirstAsync(options, cancellationToken);

    public Task<T1?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default) => _parent.SelectFirstOrDefaultAsync(cancellationToken);

    public Task<T1?> SelectFirstOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default) => _parent.SelectFirstOrDefaultAsync(options, cancellationToken);

    public Task<T1> SelectSingleAsync(CancellationToken cancellationToken = default) => _parent.SelectSingleAsync(cancellationToken);

    public Task<T1> SelectSingleAsync(CommandOptions options, CancellationToken cancellationToken = default) => _parent.SelectSingleAsync(options, cancellationToken);

    public Task<T1?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default) => _parent.SelectSingleOrDefaultAsync(cancellationToken);

    public Task<T1?> SelectSingleOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default) => _parent.SelectSingleOrDefaultAsync(options, cancellationToken);

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildCountSql();
        return await _parent.Connection.QueryScalarAsync<int>(sql, _parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildCountSql();
        return await _parent.Connection.QueryScalarAsync<int>(sql, _parent.GetParameters().ToParameterObject()!, ToTypedOptions<int>(options), cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildCountSql();
        return await _parent.Connection.QueryScalarAsync<long>(sql, _parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> LongCountAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildCountSql();
        return await _parent.Connection.QueryScalarAsync<long>(sql, _parent.GetParameters().ToParameterObject()!, ToTypedOptions<long>(options), cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<IDictionary<string, object?>>> SelectPartialAsync(string columns, CancellationToken cancellationToken = default)
    {
        string sql = _parent.BuildSelectPartialSql(columns);
        return await _parent.Connection.QueryPartialListAsync(sql, _parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    // AUD-R12: same fetch-all-then-take-first issue - delegate to _parent, which already
    // applies GetPagingSql(0, 1).
    public Task<IDictionary<string, object?>> SelectPartialFirstAsync(string columns, CancellationToken cancellationToken = default)
        => _parent.SelectPartialFirstAsync(columns, cancellationToken);

    public Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(string columns, CancellationToken cancellationToken = default)
        => _parent.SelectPartialFirstOrDefaultAsync(columns, cancellationToken);

    public Task<IDictionary<string, object?>> SelectPartialSingleAsync(string columns, CancellationToken cancellationToken = default)
        => _parent.SelectPartialSingleAsync(columns, cancellationToken);

    public Task<IDictionary<string, object?>?> SelectPartialSingleOrDefaultAsync(string columns, CancellationToken cancellationToken = default)
        => _parent.SelectPartialSingleOrDefaultAsync(columns, cancellationToken);

    // ==================== HELPERS ====================

    private string GetColumnNameForOrderBy<T>(string propertyName, string? alias) where T : new()
    {
        // R29: was a linear scan of metadata.Columns plus a fresh EscapeColumnName per call; the
        // arity-2 helpers were converted to the pre-escaped CachedDialectMetadata lookup under
        // AUD-R26-058 and these string-overload helpers were left on the old shape.
        EntityMetadata metadata = FluentMetadataCache.GetMetadata<T>();
        CachedDialectMetadata cached = FluentMetadataCache.GetForDialect<T>(_parent.Dialect);

        string escaped = cached.GetColumnName(propertyName);
        string prefix = alias ?? _parent.Dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
        return $"{prefix}.{escaped}";
    }

    private static CommandOptions<TResult> ToTypedOptions<TResult>(CommandOptions options) =>
        new(transaction: options.Transaction, commandTimeout: options.CommandTimeout, commandType: options.CommandType);
}

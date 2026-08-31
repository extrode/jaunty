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

    // AUD-R35-179. The join this clause builder has already contributed to the shared root. A second
    // On(...) on the same instance redefines it rather than appending a duplicate.
    private JoinInfo? _addedJoin;

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

        string leftColumn = GetColumnName<T1>(leftProp, _parent._parent.FromAlias);
        string rightColumn = GetColumnName<T4>(rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery4(condition);
    }

    public IJoinedQuery4<T1, T2, T3, T4> OnFromSecond<TLeftKey, TRightKey>(
        Expression<Func<T2, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName<T2>(leftProp, _parent._parent.Joins[0].Alias);
        string rightColumn = GetColumnName<T4>(rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery4(condition);
    }

    public IJoinedQuery4<T1, T2, T3, T4> OnFromThird<TLeftKey, TRightKey>(
        Expression<Func<T3, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName<T3>(leftProp, _parent._parent.Joins[1].Alias);
        string rightColumn = GetColumnName<T4>(rightProp, _alias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery4(condition);
    }

    public IJoinedQuery4<T1, T2, T3, T4> On(Expression<Func<T1, T2, T3, T4, bool>> predicate)
    {
        JoinedQueryBuilder<T1, T2> root = _parent._parent;

        var visitor = new JoinExpressionVisitor4<T1, T2, T3, T4>(
            root.Dialect,
            root.FromAlias,
            root.Joins[0].Alias,
            root.Joins[1].Alias,
            _alias);

        (string condition, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);

        // Renumbered against the query-wide sequence for the same reason as the arity-3 overload:
        // by the fourth join the query can already hold "jp0", and each visitor restarts at 0.
        return CreateJoinedQuery4(root.RegisterExpressionParameters(condition, parameters));
    }

    /// <inheritdoc cref="JoinClauseBuilder{TFrom, TJoin}.On(string, string)"/>
    public IJoinedQuery4<T1, T2, T3, T4> On(string leftColumn, string rightColumn)
    {
        // AUD-R34-022.
        JoinColumnReference.Require(leftColumn, nameof(leftColumn));
        JoinColumnReference.Require(rightColumn, nameof(rightColumn));

        return CreateJoinedQuery4($"{leftColumn} = {rightColumn}");
    }

    public IJoinedQuery4<T1, T2, T3, T4> On(string condition) => CreateJoinedQuery4(condition);

    public IJoinedQuery4<T1, T2, T3, T4> On<TValue>(string condition, TValue value)
        => On(condition, JoinParameterName.Default, value);

    public IJoinedQuery4<T1, T2, T3, T4> On<TValue>(string condition, string parameterName, TValue value)
    {
        JoinedQueryBuilder<T1, T2> root = _parent._parent;
        string qualified = JoinParameterName.Qualify(root.Dialect.ParameterPrefix, parameterName, nameof(parameterName));

        if (root.HasParameter(qualified))
            throw JoinParameterName.DuplicateError(qualified, nameof(parameterName));

        JoinedQuery4Builder<T1, T2, T3, T4> joinedQuery = CreateJoinedQuery4(condition);
        root.AddParameter(qualified, value);
        return joinedQuery;
    }

    private JoinedQuery4Builder<T1, T2, T3, T4> CreateJoinedQuery4(string onCondition)
    {
        var joinInfo = new JoinInfo(
            _joinType,
            _metadata.TableName,
            _metadata.SchemaName,
            _alias,
            onCondition);

        if (_addedJoin is JoinInfo previous)
            _parent._parent.ReplaceJoin(previous, joinInfo);
        else
            _parent._parent.AddJoin(joinInfo);

        _addedJoin = joinInfo;

        return new JoinedQuery4Builder<T1, T2, T3, T4>(_parent);
    }

    private string GetColumnName<T>(string propertyName, string? alias) where T : new()
    {
        // R29: was a linear scan of metadata.Columns plus a fresh EscapeColumnName per call; the
        // arity-2 helpers were converted to the pre-escaped CachedDialectMetadata lookup under
        // AUD-R26-058 and these string-overload helpers were left on the old shape.
        EntityMetadata metadata = FluentMetadataCache.GetMetadata<T>();
        CachedDialectMetadata cached = FluentMetadataCache.GetForDialect<T>(_parent._parent.Dialect);

        string escaped = cached.GetColumnName(propertyName);
        string prefix = alias ?? _parent._parent.Dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
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
    internal readonly JoinedQuery3Builder<T1, T2, T3> _parent;

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

        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        _parent._parent.AddWhereExpression(sql, parameters, LogicalOperator.None);
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> Where(string condition)
    {
        _parent._parent.AddWhereCondition(WhereCondition.Raw(condition, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> Where(string column, object? value)
    {
        // AUD-R35-014: see ParameterCollection.CreateUniqueName.
        string paramName = _parent._parent.GetParameters()
            .CreateUniqueName(_parent._parent.Dialect.ParameterPrefix, column);
        string escapedColumn = EscapeQualifiedColumn(column);

        // AUD-R35-184: a null is IS NULL. See JoinedQueryBuilderWhere.Where(string, object?).
        if (value is null)
        {
            _parent._parent.AddWhereCondition(WhereCondition.Column($"{escapedColumn} IS NULL", LogicalOperator.None));
            return this;
        }

        _parent._parent.AddWhereCondition(WhereCondition.Column($"{escapedColumn} = {paramName}", LogicalOperator.None));
        _parent._parent.GetParameters().Add(paramName, value);
        return this;
    }

    // The alias prefix (if any) is validated as a plain identifier - not dialect-escaped, since
    // aliases are library-controlled bare names in the generated SQL, not user data - so a caller
    // can't smuggle arbitrary SQL text through the alias segment while the column name is escaped.
    private string EscapeQualifiedColumn(string column)
    {
        int dotIndex = column.IndexOf('.');
        if (dotIndex < 0)
            return _parent._parent.Dialect.EscapeColumnName(column);

        string alias = column.Substring(0, dotIndex);
        string columnName = column.Substring(dotIndex + 1);
        SqlIdentifierValidator.Validate(alias, nameof(column));
        return $"{alias}.{_parent._parent.Dialect.EscapeColumnName(columnName)}";
    }

    public IJoinedQuery4<T1, T2, T3, T4> And(Expression<Func<T1, T2, T3, T4, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor4<T1, T2, T3, T4>(
            _parent._parent.Dialect,
            _parent._parent.FromAlias,
            _parent._parent.Joins[0].Alias,
            _parent._parent.Joins[1].Alias,
            _parent._parent.Joins[2].Alias);

        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        _parent._parent.AddWhereExpression(sql, parameters, LogicalOperator.And);
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

        (string sql, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        _parent._parent.AddWhereExpression(sql, parameters, LogicalOperator.Or);
        return this;
    }

    // ==================== ORDER BY ====================

    public IJoinedQuery4<T1, T2, T3, T4> OrderBy<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T1>(propertyName, _parent._parent.FromAlias);
        _parent._parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByDescending<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T1>(propertyName, _parent._parent.FromAlias);
        _parent._parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoined<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T2>(propertyName, _parent._parent.Joins[0].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T2>(propertyName, _parent._parent.Joins[0].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoined<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T3>(propertyName, _parent._parent.Joins[1].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T3>(propertyName, _parent._parent.Joins[1].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoined<TKey>(Expression<Func<T4, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T4>(propertyName, _parent._parent.Joins[2].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> OrderByJoinedDescending<TKey>(Expression<Func<T4, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T4>(propertyName, _parent._parent.Joins[2].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenBy<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T1>(propertyName, _parent._parent.FromAlias);
        _parent._parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByDescending<TKey>(Expression<Func<T1, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T1>(propertyName, _parent._parent.FromAlias);
        _parent._parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoined<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T2>(propertyName, _parent._parent.Joins[0].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T2>(propertyName, _parent._parent.Joins[0].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoined<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T3>(propertyName, _parent._parent.Joins[1].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T3>(propertyName, _parent._parent.Joins[1].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoined<TKey>(Expression<Func<T4, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T4>(propertyName, _parent._parent.Joins[2].Alias);
        _parent._parent.AddOrderByColumn(columnName, "ASC");
        return this;
    }

    public IJoinedQuery4<T1, T2, T3, T4> ThenByJoinedDescending<TKey>(Expression<Func<T4, TKey>> keySelector)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        string columnName = GetColumnNameForOrderBy<T4>(propertyName, _parent._parent.Joins[2].Alias);
        _parent._parent.AddOrderByColumn(columnName, "DESC");
        return this;
    }

    // ==================== GROUP BY ====================

    public IGroupedJoinedQuery4<T1, T2, T3, T4, TKey> GroupBy<TKey>(Expression<Func<T1, T2, T3, T4, TKey>> keySelector)
    {
        return new GroupedJoinedQueryBuilder4<T1, T2, T3, T4, TKey>(this, keySelector);
    }

    // ==================== SELECT ====================

    public List<T1> Select() => _parent._parent.Select();

    public List<T1> Select(CommandOptions options) => _parent._parent.Select(options);

    public List<(T1, T2, T3, T4)> SelectAll() => SelectAll(default);

    /// <summary>
    /// AUD-R34-016. <c>SelectAll</c> builds and executes its own command, and had no
    /// <c>CommandOptions</c> overload to take a transaction or timeout from - so on a provider
    /// that validates the pairing (SqlClient, Microsoft.Data.Sqlite) the whole tuple-returning
    /// surface of a 4-way join threw inside a caller's transaction rather than joining it.
    /// </summary>
    public List<(T1, T2, T3, T4)> SelectAll(CommandOptions options)
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

        return CommandObservation.Execute(
            sql, _parent._parent.DescribeParameters(), _parent._parent.Connection, CommandType.Text, Body);

        List<(T1, T2, T3, T4)> Body()
        {
            var results = new List<(T1, T2, T3, T4)>();

            using IDbCommand command = _parent._parent.Connection.CreateCommand();
            command.CommandText = sql;
            _parent._parent.BindParameters(command);
            FluentCommandOptions.Apply(command, _parent._parent.Connection, options);

            CommandObservation.Log(sql, _parent._parent.DescribeParameters());

            bool wasClosed = _parent._parent.Connection.State == ConnectionState.Closed;
            if (wasClosed)
                _parent._parent.Connection.Open();

            try
            {
                using IDataReader reader = command.ExecuteReader();
                Dictionary<string, int> ordinals = JoinedQueryBuilder<T1, T2>.BuildOrdinalLookup(reader);

                while (reader.Read())
                {
                    T1? t1 = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_", ordinals);
                    T2? t2 = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_", ordinals);
                    T3? t3 = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_", ordinals);
                    T4? t4 = JoinedQueryBuilder<T1, T2>.MapEntity<T4>(t4Metadata, reader, "t4_", ordinals);
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
    }

    // AUD-R12: these previously fetched the entire result set via Select() and took the
    // first/only element in C#, instead of using SQL-level LIMIT/paging. _parent._parent (the
    // 2-way JoinedQueryBuilder<T1,T2> that carries this query's T1 metadata/alias and every
    // registered join, including T3's and T4's) already implements these efficiently via
    // _dialect.GetPagingSql, so delegate to it directly.
    public T1 SelectFirst() => _parent._parent.SelectFirst();

    public T1 SelectFirst(CommandOptions options) => _parent._parent.SelectFirst(options);

    public T1? SelectFirstOrDefault() => _parent._parent.SelectFirstOrDefault();

    public T1? SelectFirstOrDefault(CommandOptions options) => _parent._parent.SelectFirstOrDefault(options);

    public T1 SelectSingle() => _parent._parent.SelectSingle();

    public T1 SelectSingle(CommandOptions options) => _parent._parent.SelectSingle(options);

    public T1? SelectSingleOrDefault() => _parent._parent.SelectSingleOrDefault();

    public T1? SelectSingleOrDefault(CommandOptions options) => _parent._parent.SelectSingleOrDefault(options);

    public int Count()
    {
        string sql = _parent._parent.BuildCountSql();
        return _parent._parent.Connection.QueryScalar<int>(sql, _parent._parent.GetParameters().ToParameterObject()!);
    }

    public int Count(CommandOptions options)
    {
        string sql = _parent._parent.BuildCountSql();
        return _parent._parent.Connection.QueryScalar<int>(sql, _parent._parent.GetParameters().ToParameterObject()!, ToTypedOptions<int>(options));
    }

    public long LongCount()
    {
        string sql = _parent._parent.BuildCountSql();
        return _parent._parent.Connection.QueryScalar<long>(sql, _parent._parent.GetParameters().ToParameterObject()!);
    }

    public long LongCount(CommandOptions options)
    {
        string sql = _parent._parent.BuildCountSql();
        return _parent._parent.Connection.QueryScalar<long>(sql, _parent._parent.GetParameters().ToParameterObject()!, ToTypedOptions<long>(options));
    }

    public string ToSql() => _parent._parent.ToSql();

    // ==================== SELECT PARTIAL ====================

    // AUD-R35-063: this used to build the SQL here and run it through the core
    // Connection.QueryPartialList, whose row builder is an OrdinalIgnoreCase dictionary filled by
    // assignment - a duplicate column name resolved silently last-wins, and lookups were
    // case-insensitive. Every other member of the family delegates to _parent._parent, whose
    // MapToDictionary is case-sensitive and throws on a duplicate. Selecting a caller-written
    // column list over a four-table join is exactly where a duplicate name arises, and this is the
    // overload callers reach first. Delegating also puts both halves on the same parameter binder.
    public List<IDictionary<string, object?>> SelectPartial(string columns) => _parent._parent.SelectPartial(columns);

    // AUD-R12: same fetch-all-then-take-first/single issue as the SelectFirst/SelectSingle
    // family above - delegate to _parent._parent, which already applies GetPagingSql(0, 1)/(0, 2).
    public IDictionary<string, object?> SelectPartialFirst(string columns) => _parent._parent.SelectPartialFirst(columns);

    public IDictionary<string, object?>? SelectPartialFirstOrDefault(string columns) => _parent._parent.SelectPartialFirstOrDefault(columns);

    public IDictionary<string, object?> SelectPartialSingle(string columns) => _parent._parent.SelectPartialSingle(columns);

    public IDictionary<string, object?>? SelectPartialSingleOrDefault(string columns) => _parent._parent.SelectPartialSingleOrDefault(columns);

    // ==================== ASYNC ====================

    public async Task<List<T1>> SelectAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent._parent.BuildSelectSql(_parent._parent.GetSelectColumns<T1>());
        return await _parent._parent.Connection.QueryPartialAsync<T1>(sql, _parent._parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<T1>> SelectAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        string sql = _parent._parent.BuildSelectSql(_parent._parent.GetSelectColumns<T1>());
        return await _parent._parent.Connection.QueryPartialAsync<T1>(sql, _parent._parent.GetParameters().ToParameterObject()!, ToTypedOptions<T1>(options), cancellationToken).ConfigureAwait(false);
    }

    public Task<List<(T1, T2, T3, T4)>> SelectAllAsync(CancellationToken cancellationToken = default)
        => SelectAllAsync(default, cancellationToken);

    /// <summary>AUD-R34-016: see the synchronous <see cref="SelectAll(CommandOptions)"/>.</summary>
    public async Task<List<(T1, T2, T3, T4)>> SelectAllAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_parent._parent.Connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async operations require a DbConnection.");

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

        return await CommandObservation.ExecuteAsync(
            sql, _parent._parent.DescribeParameters(), _parent._parent.Connection, CommandType.Text, Body, cancellationToken).ConfigureAwait(false);

        async ValueTask<List<(T1, T2, T3, T4)>> Body()
        {
            var results = new List<(T1, T2, T3, T4)>();

            using DbCommand command = dbConnection.CreateCommand();
            command.CommandText = sql;
            _parent._parent.BindParameters(command);
            FluentCommandOptions.Apply(command, _parent._parent.Connection, options);

            CommandObservation.Log(sql, _parent._parent.DescribeParameters());

            bool wasClosed = _parent._parent.Connection.State == ConnectionState.Closed;
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                Dictionary<string, int> ordinals = JoinedQueryBuilder<T1, T2>.BuildOrdinalLookup(reader);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    T1? t1 = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_", ordinals);
                    T2? t2 = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_", ordinals);
                    T3? t3 = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_", ordinals);
                    T4? t4 = JoinedQueryBuilder<T1, T2>.MapEntity<T4>(t4Metadata, reader, "t4_", ordinals);
                    results.Add((t1, t2, t3, t4));
                }
            }
            finally
            {
                // AUD-R35-178. Was a blocking Close() at the end of a fully async read, on a
                // connection this method opened asynchronously three lines above. dbConnection is
                // already the DbConnection the async open went through, so CloseAsync is available
                // without a cast; on a provider whose close does network I/O the old call blocked
                // the calling thread.
                if (wasClosed)
#if NET8_0_OR_GREATER
                    await dbConnection.CloseAsync().ConfigureAwait(false);
#else
                    await Task.Run(() => dbConnection.Close()).ConfigureAwait(false);
#endif
            }

            return results;
        }
    }

    // AUD-R12: same fetch-all-then-take-first issue as the sync members above - delegate to
    // _parent._parent, which already applies GetPagingSql(0, 1).
    public Task<T1> SelectFirstAsync(CancellationToken cancellationToken = default) => _parent._parent.SelectFirstAsync(cancellationToken);

    public Task<T1> SelectFirstAsync(CommandOptions options, CancellationToken cancellationToken = default) => _parent._parent.SelectFirstAsync(options, cancellationToken);

    public Task<T1?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default) => _parent._parent.SelectFirstOrDefaultAsync(cancellationToken);

    public Task<T1?> SelectFirstOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default) => _parent._parent.SelectFirstOrDefaultAsync(options, cancellationToken);

    public Task<T1> SelectSingleAsync(CancellationToken cancellationToken = default) => _parent._parent.SelectSingleAsync(cancellationToken);

    public Task<T1> SelectSingleAsync(CommandOptions options, CancellationToken cancellationToken = default) => _parent._parent.SelectSingleAsync(options, cancellationToken);

    public Task<T1?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default) => _parent._parent.SelectSingleOrDefaultAsync(cancellationToken);

    public Task<T1?> SelectSingleOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default) => _parent._parent.SelectSingleOrDefaultAsync(options, cancellationToken);

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent._parent.BuildCountSql();
        return await _parent._parent.Connection.QueryScalarAsync<int>(sql, _parent._parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        string sql = _parent._parent.BuildCountSql();
        return await _parent._parent.Connection.QueryScalarAsync<int>(sql, _parent._parent.GetParameters().ToParameterObject()!, ToTypedOptions<int>(options), cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        string sql = _parent._parent.BuildCountSql();
        return await _parent._parent.Connection.QueryScalarAsync<long>(sql, _parent._parent.GetParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> LongCountAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        string sql = _parent._parent.BuildCountSql();
        return await _parent._parent.Connection.QueryScalarAsync<long>(sql, _parent._parent.GetParameters().ToParameterObject()!, ToTypedOptions<long>(options), cancellationToken).ConfigureAwait(false);
    }

    // AUD-R35-063: the async twin of the same split; see SelectPartial above.
    public Task<List<IDictionary<string, object?>>> SelectPartialAsync(string columns, CancellationToken cancellationToken = default)
        => _parent._parent.SelectPartialAsync(columns, cancellationToken);

    // AUD-R12: same fetch-all-then-take-first issue - delegate to _parent._parent, which
    // already applies GetPagingSql(0, 1).
    public Task<IDictionary<string, object?>> SelectPartialFirstAsync(string columns, CancellationToken cancellationToken = default)
        => _parent._parent.SelectPartialFirstAsync(columns, cancellationToken);

    public Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(string columns, CancellationToken cancellationToken = default)
        => _parent._parent.SelectPartialFirstOrDefaultAsync(columns, cancellationToken);

    public Task<IDictionary<string, object?>> SelectPartialSingleAsync(string columns, CancellationToken cancellationToken = default)
        => _parent._parent.SelectPartialSingleAsync(columns, cancellationToken);

    public Task<IDictionary<string, object?>?> SelectPartialSingleOrDefaultAsync(string columns, CancellationToken cancellationToken = default)
        => _parent._parent.SelectPartialSingleOrDefaultAsync(columns, cancellationToken);

    // ==================== HELPERS ====================

    private string GetColumnNameForOrderBy<T>(string propertyName, string? alias) where T : new()
    {
        // R29: was a linear scan of metadata.Columns plus a fresh EscapeColumnName per call; the
        // arity-2 helpers were converted to the pre-escaped CachedDialectMetadata lookup under
        // AUD-R26-058 and these string-overload helpers were left on the old shape.
        EntityMetadata metadata = FluentMetadataCache.GetMetadata<T>();
        CachedDialectMetadata cached = FluentMetadataCache.GetForDialect<T>(_parent._parent.Dialect);

        string escaped = cached.GetColumnName(propertyName);
        string prefix = alias ?? _parent._parent.Dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
        return $"{prefix}.{escaped}";
    }

    private static CommandOptions<TResult> ToTypedOptions<TResult>(CommandOptions options) =>
        new(transaction: options.Transaction, commandTimeout: options.CommandTimeout, commandType: options.CommandType);
}

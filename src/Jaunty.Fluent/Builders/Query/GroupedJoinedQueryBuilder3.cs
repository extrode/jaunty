using System.Diagnostics.CodeAnalysis;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Core;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals;

namespace Jaunty.Fluent;

/// <summary>
/// Query builder for grouped 3-way joined queries. Implements IGroupedJoinedQuery3. Sibling
/// to GroupedJoinedQueryBuilder for the 3-entity case - reuses the parent JoinedQuery3Builder
/// (and, through it, the root JoinedQueryBuilder's) already-accumulated join/WHERE state.
/// </summary>
internal sealed class GroupedJoinedQueryBuilder3<T1, T2, T3, TKey> : IGroupedJoinedQuery3<T1, T2, T3, TKey>
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    private readonly JoinedQuery3Builder<T1, T2, T3> _parent;
    private readonly EntityMetadata[] _metadata;
    private readonly JoinedGroupByExpressionVisitor _visitor;
    private readonly List<string> _havingConditions = [];

    internal GroupedJoinedQueryBuilder3(JoinedQuery3Builder<T1, T2, T3> parent, Expression<Func<T1, T2, T3, TKey>> keySelector)
    {
        _parent = parent;
        _metadata = [FluentMetadataCache.GetMetadata<T1>(), FluentMetadataCache.GetMetadata<T2>(), FluentMetadataCache.GetMetadata<T3>()];
        CachedDialectMetadata[] cachedMetadata = [FluentMetadataCache.GetForDialect<T1>(_parent._parent.Dialect), FluentMetadataCache.GetForDialect<T2>(_parent._parent.Dialect), FluentMetadataCache.GetForDialect<T3>(_parent._parent.Dialect)];
        // AUD-R35-015: see GroupedJoinTablePrefixes.Resolve.
        Dialects.ISqlDialect dialect = _parent._parent.Dialect;
        string[] tablePrefixes =
        [
            GroupedJoinTablePrefixes.Resolve(dialect, _parent._parent.FromAlias, _metadata[0]),
            GroupedJoinTablePrefixes.Resolve(dialect, _parent._parent.Joins[0].Alias, _metadata[1]),
            GroupedJoinTablePrefixes.Resolve(dialect, _parent._parent.Joins[1].Alias, _metadata[2]),
        ];
        _visitor = new JoinedGroupByExpressionVisitor(_parent._parent.Dialect, _metadata, cachedMetadata, tablePrefixes, keySelector);
    }

    public IGroupedJoinedQuery3<T1, T2, T3, TKey> Having(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, bool>> predicate)
    {
        (string havingSql, List<(string Name, object? Value)> parameters) = _visitor.TranslateHavingPredicate(predicate);
        // AUD-R35-016: see JoinedQueryBuilder.RegisterHavingParameters.
        _havingConditions.Add(_parent._parent.RegisterHavingParameters(havingSql, parameters));
        return this;
    }

    public List<TResult> Select<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector)
        => Select(selector, default);

    public List<TResult> Select<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector, CommandOptions options)
    {
        var sql = BuildSelectSql(selector);
        return ExecuteQuery<TResult>(sql, selector, options);
    }

    public Task<List<TResult>> SelectAsync<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector, CancellationToken cancellationToken = default)
        => SelectAsync(selector, default, cancellationToken);

    public async Task<List<TResult>> SelectAsync<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector, CommandOptions options, CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectSql(selector);
        return await ExecuteQueryAsync<TResult>(sql, selector, options, cancellationToken).ConfigureAwait(false);
    }

    public string ToSql<TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector)
    {
        return BuildSelectSql(selector);
    }

    private string BuildSelectSql<TResult>(Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector)
    {
        (string[] selectColumns, string[] _) = _visitor.TranslateSelect(selector);

        var sb = new StringBuilder(256);

        sb.Append("SELECT ");
        for (int i = 0; i < selectColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(selectColumns[i]);
        }

        sb.Append(" FROM ");
        sb.Append(_parent._parent.BuildFromJoinWhereSql());

        sb.Append(" GROUP BY ");
        string[] groupByColumns = _visitor.GroupByColumns;
        for (int i = 0; i < groupByColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(groupByColumns[i]);
        }

        if (_havingConditions.Count > 0)
        {
            sb.Append(" HAVING ");
            for (int i = 0; i < _havingConditions.Count; i++)
            {
                if (i > 0) sb.Append(" AND ");
                sb.Append(_havingConditions[i]);
            }
        }

        return sb.ToString();
    }

    private List<TResult> ExecuteQuery<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(string sql, Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector, CommandOptions options)
        => CommandObservation.Execute(
            sql, _parent._parent.DescribeParameters(), _parent._parent.Connection, FluentCommandOptions.Describe(options),
            () => ExecuteQueryDirect(sql, selector, options));

    private List<TResult> ExecuteQueryDirect<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(string sql, Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector, CommandOptions options)
    {
        (string[] _, string[] aliases) = _visitor.TranslateSelect(selector);
        GroupedJoinedResultMapper.ResultMapperPlan plan = GroupedJoinedResultMapper.ResultMapperPlan.Resolve<TResult>(aliases);

        var results = new List<TResult>();

        using IDbCommand command = _parent._parent.Connection.CreateCommand();
        command.CommandText = sql;
        FluentCommandOptions.Apply(command, _parent._parent.Connection, options);
        _parent._parent.BindParameters(command);

        CommandObservation.Log(sql, _parent._parent.DescribeParameters());

        var wasClosed = _parent._parent.Connection.State == ConnectionState.Closed;
        if (wasClosed) _parent._parent.Connection.Open();
        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                TResult result = GroupedJoinedResultMapper.MapResult<TResult>(reader, aliases, in plan);
                results.Add(result);
            }
        }
        finally
        {
            if (wasClosed) _parent._parent.Connection.Close();
        }

        return results;
    }

    private async Task<List<TResult>> ExecuteQueryAsync<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(string sql, Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector, CommandOptions options, CancellationToken cancellationToken)
        => await CommandObservation.ExecuteAsync(
            sql, _parent._parent.DescribeParameters(), _parent._parent.Connection, FluentCommandOptions.Describe(options),
            () => ExecuteQueryDirectAsync(sql, selector, options, cancellationToken), cancellationToken).ConfigureAwait(false);

    private async ValueTask<List<TResult>> ExecuteQueryDirectAsync<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TResult>(string sql, Expression<Func<IGroupingJoined3<TKey, T1, T2, T3>, TResult>> selector, CommandOptions options, CancellationToken cancellationToken)
    {
        if (_parent._parent.Connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        (string[] _, string[] aliases) = _visitor.TranslateSelect(selector);
        GroupedJoinedResultMapper.ResultMapperPlan plan = GroupedJoinedResultMapper.ResultMapperPlan.Resolve<TResult>(aliases);

        var results = new List<TResult>();

        using DbCommand command = dbConn.CreateCommand();
        command.CommandText = sql;
        FluentCommandOptions.Apply(command, _parent._parent.Connection, options);
        _parent._parent.BindParameters(command);

        CommandObservation.Log(sql, _parent._parent.DescribeParameters());

        bool wasClosed = _parent._parent.Connection.State == ConnectionState.Closed;
        if (wasClosed) await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                TResult result = GroupedJoinedResultMapper.MapResult<TResult>(reader, aliases, in plan);
                results.Add(result);
            }
        }
        finally
        {
            if (wasClosed) dbConn.Close();
        }

        return results;
    }
}

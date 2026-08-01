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
/// Query builder for grouped 4-way joined queries. Implements IGroupedJoinedQuery4. Sibling
/// to GroupedJoinedQueryBuilder for the 4-entity case - reuses the parent JoinedQuery4Builder
/// (and, through its parent chain, the root JoinedQueryBuilder's) already-accumulated
/// join/WHERE state.
/// </summary>
internal sealed class GroupedJoinedQueryBuilder4<T1, T2, T3, T4, TKey> : IGroupedJoinedQuery4<T1, T2, T3, T4, TKey>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    private readonly JoinedQuery4Builder<T1, T2, T3, T4> _parent;
    private readonly EntityMetadata[] _metadata;
    private readonly JoinedGroupByExpressionVisitor _visitor;
    private readonly List<string> _havingConditions = [];

    internal GroupedJoinedQueryBuilder4(JoinedQuery4Builder<T1, T2, T3, T4> parent, Expression<Func<T1, T2, T3, T4, TKey>> keySelector)
    {
        _parent = parent;
        _metadata =
        [
            FluentMetadataCache.GetMetadata<T1>(),
            FluentMetadataCache.GetMetadata<T2>(),
            FluentMetadataCache.GetMetadata<T3>(),
            FluentMetadataCache.GetMetadata<T4>()
        ];

        JoinedQueryBuilder<T1, T2> root = _parent._parent._parent;
        string[] tablePrefixes =
        [
            root.FromAlias ?? _metadata[0].TableName,
            root.Joins[0].Alias ?? _metadata[1].TableName,
            root.Joins[1].Alias ?? _metadata[2].TableName,
            root.Joins[2].Alias ?? _metadata[3].TableName
        ];
        CachedDialectMetadata[] cachedMetadata =
        [
            FluentMetadataCache.GetForDialect<T1>(root.Dialect),
            FluentMetadataCache.GetForDialect<T2>(root.Dialect),
            FluentMetadataCache.GetForDialect<T3>(root.Dialect),
            FluentMetadataCache.GetForDialect<T4>(root.Dialect)
        ];
        _visitor = new JoinedGroupByExpressionVisitor(root.Dialect, _metadata, cachedMetadata, tablePrefixes, keySelector);
    }

    public IGroupedJoinedQuery4<T1, T2, T3, T4, TKey> Having(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, bool>> predicate)
    {
        (string havingSql, List<(string Name, object? Value)> parameters) = _visitor.TranslateHavingPredicate(predicate);
        _havingConditions.Add(havingSql);
        foreach ((string name, object? value) in parameters)
            _parent._parent._parent.AddParameter(name, value);
        return this;
    }

    public List<TResult> Select<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector)
        => Select(selector, default);

    public List<TResult> Select<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector, CommandOptions options)
    {
        var sql = BuildSelectSql(selector);
        return ExecuteQuery<TResult>(sql, selector, options);
    }

    public Task<List<TResult>> SelectAsync<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector, CancellationToken cancellationToken = default)
        => SelectAsync(selector, default, cancellationToken);

    public async Task<List<TResult>> SelectAsync<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector, CommandOptions options, CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectSql(selector);
        return await ExecuteQueryAsync<TResult>(sql, selector, options, cancellationToken).ConfigureAwait(false);
    }

    public string ToSql<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector)
    {
        return BuildSelectSql(selector);
    }

    private string BuildSelectSql<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector)
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
        sb.Append(_parent._parent._parent.BuildFromJoinWhereSql());

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

    private List<TResult> ExecuteQuery<TResult>(string sql, Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector, CommandOptions options)
        => CommandObservation.Execute(
            sql, _parent._parent._parent.DescribeParameters(), _parent._parent._parent.Connection, FluentCommandOptions.Describe(options),
            () => ExecuteQueryDirect(sql, selector, options));

    private List<TResult> ExecuteQueryDirect<TResult>(string sql, Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector, CommandOptions options)
    {
        (string[] _, string[] aliases) = _visitor.TranslateSelect(selector);
        GroupedJoinedResultMapper.ResultMapperPlan plan = GroupedJoinedResultMapper.ResultMapperPlan.Resolve<TResult>(aliases);

        var results = new List<TResult>();

        IDbConnection connection = _parent._parent._parent.Connection;
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        FluentCommandOptions.Apply(command, connection, options);
        _parent._parent._parent.BindParameters(command);

        CommandObservation.Log(sql, _parent._parent._parent.DescribeParameters());

        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed) connection.Open();
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
            if (wasClosed) connection.Close();
        }

        return results;
    }

    private async Task<List<TResult>> ExecuteQueryAsync<TResult>(string sql, Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector, CommandOptions options, CancellationToken cancellationToken)
        => await CommandObservation.ExecuteAsync(
            sql, _parent._parent._parent.DescribeParameters(), _parent._parent._parent.Connection, FluentCommandOptions.Describe(options),
            () => ExecuteQueryDirectAsync(sql, selector, options, cancellationToken), cancellationToken).ConfigureAwait(false);

    private async ValueTask<List<TResult>> ExecuteQueryDirectAsync<TResult>(string sql, Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector, CommandOptions options, CancellationToken cancellationToken)
    {
        IDbConnection connection = _parent._parent._parent.Connection;
        if (connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        (string[] _, string[] aliases) = _visitor.TranslateSelect(selector);
        GroupedJoinedResultMapper.ResultMapperPlan plan = GroupedJoinedResultMapper.ResultMapperPlan.Resolve<TResult>(aliases);

        var results = new List<TResult>();

        using DbCommand command = dbConn.CreateCommand();
        command.CommandText = sql;
        FluentCommandOptions.Apply(command, connection, options);
        _parent._parent._parent.BindParameters(command);

        CommandObservation.Log(sql, _parent._parent._parent.DescribeParameters());

        bool wasClosed = connection.State == ConnectionState.Closed;
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

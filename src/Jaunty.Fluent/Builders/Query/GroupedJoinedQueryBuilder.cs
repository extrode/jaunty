using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals;

namespace Jaunty.Fluent;

/// <summary>
/// Query builder for grouped 2-way joined queries. Implements IGroupedJoinedQuery. Sibling to
/// GroupedQueryBuilder for the joined case - reuses the parent JoinedQueryBuilder's
/// already-accumulated join/WHERE state (spec 004's state-reuse seam) rather than rebuilding
/// the FROM/JOIN/WHERE fragment.
/// </summary>
internal sealed class GroupedJoinedQueryBuilder<TFrom, TJoin, TKey> : IGroupedJoinedQuery<TFrom, TJoin, TKey>
    where TFrom : new()
    where TJoin : new()
{
    private readonly JoinedQueryBuilder<TFrom, TJoin> _parent;
    private readonly EntityMetadata[] _metadata;
    private readonly JoinedGroupByExpressionVisitor _visitor;
    private readonly List<string> _havingConditions = [];

    internal GroupedJoinedQueryBuilder(JoinedQueryBuilder<TFrom, TJoin> parent, Expression<Func<TFrom, TJoin, TKey>> keySelector)
    {
        _parent = parent;
        _metadata = [FluentMetadataCache.GetMetadata<TFrom>(), FluentMetadataCache.GetMetadata<TJoin>()];
        CachedDialectMetadata[] cachedMetadata = [FluentMetadataCache.GetForDialect<TFrom>(_parent.Dialect), FluentMetadataCache.GetForDialect<TJoin>(_parent.Dialect)];
        string[] tablePrefixes = [_parent.FromAlias ?? _metadata[0].TableName, _parent.Joins[0].Alias ?? _metadata[1].TableName];
        _visitor = new JoinedGroupByExpressionVisitor(_parent.Dialect, _metadata, cachedMetadata, tablePrefixes, keySelector);
    }

    public IGroupedJoinedQuery<TFrom, TJoin, TKey> Having(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, bool>> predicate)
    {
        (string havingSql, List<(string Name, object? Value)> parameters) = _visitor.TranslateHavingPredicate(predicate);
        _havingConditions.Add(havingSql);
        foreach ((string name, object? value) in parameters)
            _parent.AddParameter(name, value);
        return this;
    }

    public List<TResult> Select<TResult>(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector)
    {
        var sql = BuildSelectSql(selector);
        return ExecuteQuery<TResult>(sql, selector);
    }

    public async Task<List<TResult>> SelectAsync<TResult>(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectSql(selector);
        return await ExecuteQueryAsync<TResult>(sql, selector, cancellationToken).ConfigureAwait(false);
    }

    public string ToSql<TResult>(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector)
    {
        return BuildSelectSql(selector);
    }

    private string BuildSelectSql<TResult>(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector)
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
        sb.Append(_parent.BuildFromJoinWhereSql());

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

    private List<TResult> ExecuteQuery<TResult>(string sql, Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector)
        => CommandObservation.Execute(
            sql, _parent.DescribeParameters(), _parent.Connection, CommandType.Text,
            () => ExecuteQueryDirect(sql, selector));

    private List<TResult> ExecuteQueryDirect<TResult>(string sql, Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector)
    {
        (string[] _, string[] aliases) = _visitor.TranslateSelect(selector);
        GroupedJoinedResultMapper.ResultMapperPlan plan = GroupedJoinedResultMapper.ResultMapperPlan.Resolve<TResult>(aliases);

        var results = new List<TResult>();

        using IDbCommand command = _parent.Connection.CreateCommand();
        command.CommandText = sql;
        _parent.BindParameters(command);

        CommandObservation.Log(sql, _parent.DescribeParameters());

        var wasClosed = _parent.Connection.State == ConnectionState.Closed;
        if (wasClosed) _parent.Connection.Open();
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
            if (wasClosed) _parent.Connection.Close();
        }

        return results;
    }

    private async Task<List<TResult>> ExecuteQueryAsync<TResult>(string sql, Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector, CancellationToken cancellationToken)
        => await CommandObservation.ExecuteAsync(
            sql, _parent.DescribeParameters(), _parent.Connection, CommandType.Text,
            () => ExecuteQueryDirectAsync(sql, selector, cancellationToken), cancellationToken).ConfigureAwait(false);

    private async ValueTask<List<TResult>> ExecuteQueryDirectAsync<TResult>(string sql, Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, TResult>> selector, CancellationToken cancellationToken)
    {
        if (_parent.Connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        (string[] _, string[] aliases) = _visitor.TranslateSelect(selector);
        GroupedJoinedResultMapper.ResultMapperPlan plan = GroupedJoinedResultMapper.ResultMapperPlan.Resolve<TResult>(aliases);

        var results = new List<TResult>();

        using DbCommand command = dbConn.CreateCommand();
        command.CommandText = sql;
        _parent.BindParameters(command);

        CommandObservation.Log(sql, _parent.DescribeParameters());

        bool wasClosed = _parent.Connection.State == ConnectionState.Closed;
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

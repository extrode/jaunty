using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

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
        _visitor = new JoinedGroupByExpressionVisitor(root.Dialect, _metadata, tablePrefixes, keySelector);
    }

    public IGroupedJoinedQuery4<T1, T2, T3, T4, TKey> Having(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, bool>> predicate)
    {
        var havingSql = _visitor.TranslateHavingPredicate(predicate);
        _havingConditions.Add(havingSql);
        return this;
    }

    public List<TResult> Select<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector)
    {
        var sql = BuildSelectSql(selector);
        return ExecuteQuery<TResult>(sql, selector);
    }

    public async Task<List<TResult>> SelectAsync<TResult>(Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var sql = BuildSelectSql(selector);
        return await ExecuteQueryAsync<TResult>(sql, selector, cancellationToken).ConfigureAwait(false);
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

    private List<TResult> ExecuteQuery<TResult>(string sql, Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector)
    {
        (string[] _, string[] aliases) = _visitor.TranslateSelect(selector);

        var results = new List<TResult>();

        IDbConnection connection = _parent._parent._parent.Connection;
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        _parent._parent._parent.BindParameters(command);

        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed) connection.Open();
        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                TResult result = GroupedJoinedResultMapper.MapResult<TResult>(reader, aliases);
                results.Add(result);
            }
        }
        finally
        {
            if (wasClosed) connection.Close();
        }

        return results;
    }

    private async Task<List<TResult>> ExecuteQueryAsync<TResult>(string sql, Expression<Func<IGroupingJoined4<TKey, T1, T2, T3, T4>, TResult>> selector, CancellationToken cancellationToken)
    {
        IDbConnection connection = _parent._parent._parent.Connection;
        if (connection is not DbConnection dbConn)
            return ExecuteQuery(sql, selector);

        (string[] _, string[] aliases) = _visitor.TranslateSelect(selector);

        var results = new List<TResult>();

        using DbCommand command = dbConn.CreateCommand();
        command.CommandText = sql;
        _parent._parent._parent.BindParameters(command);

        bool wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed) await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                TResult result = GroupedJoinedResultMapper.MapResult<TResult>(reader, aliases);
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

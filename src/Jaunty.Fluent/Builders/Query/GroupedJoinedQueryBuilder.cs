using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

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
        string[] tablePrefixes = [_parent.FromAlias ?? _metadata[0].TableName, _parent.Joins[0].Alias ?? _metadata[1].TableName];
        _visitor = new JoinedGroupByExpressionVisitor(_parent.Dialect, _metadata, tablePrefixes, keySelector);
    }

    public IGroupedJoinedQuery<TFrom, TJoin, TKey> Having(Expression<Func<IGroupingJoined<TKey, TFrom, TJoin>, bool>> predicate)
    {
        var havingSql = _visitor.TranslateHavingPredicate(predicate);
        _havingConditions.Add(havingSql);
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
    {
        (string[] _, string[] aliases) = _visitor.TranslateSelect(selector);

        var results = new List<TResult>();

        using IDbCommand command = _parent.Connection.CreateCommand();
        command.CommandText = sql;
        _parent.BindParameters(command);

        var wasClosed = _parent.Connection.State == ConnectionState.Closed;
        if (wasClosed) _parent.Connection.Open();
        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                TResult result = MapResult<TResult>(reader, aliases);
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
    {
        if (_parent.Connection is not DbConnection dbConn)
            return ExecuteQuery(sql, selector);

        (string[] _, string[] aliases) = _visitor.TranslateSelect(selector);

        var results = new List<TResult>();

        using DbCommand command = dbConn.CreateCommand();
        command.CommandText = sql;
        _parent.BindParameters(command);

        bool wasClosed = _parent.Connection.State == ConnectionState.Closed;
        if (wasClosed) await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                TResult result = MapResult<TResult>(reader, aliases);
                results.Add(result);
            }
        }
        finally
        {
            if (wasClosed) dbConn.Close();
        }

        return results;
    }

    private static TResult MapResult<TResult>(IDataReader reader, string[] aliases)
    {
        Type resultType = typeof(TResult);

#pragma warning disable IL2090 // Reflection on generic parameter for result mapping
        if (resultType.Name.StartsWith("<>") || resultType.GetConstructors().Any(c => c.GetParameters().Length == aliases.Length))
        {
            var values = new object?[aliases.Length];

            ConstructorInfo? constructor = resultType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == aliases.Length);

            if (constructor is not null)
            {
                ParameterInfo[] parameters = constructor.GetParameters();

                for (int i = 0; i < aliases.Length; i++)
                {
                    int ordinal = reader.GetOrdinal(aliases[i]);

                    if (!reader.IsDBNull(ordinal))
                    {
                        object value = reader.GetValue(ordinal);
                        Type targetType = parameters[i].ParameterType;
                        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                        values[i] = Convert.ChangeType(value, underlyingType);
                    }
                }

                return (TResult)constructor.Invoke(values);
            }
        }
#pragma warning restore IL2090

#pragma warning disable IL2091 // Activator.CreateInstance requires public parameterless constructor
        TResult? instance = Activator.CreateInstance<TResult>();
#pragma warning restore IL2091
        for (int i = 0; i < aliases.Length; i++)
        {
#pragma warning disable IL2090
            PropertyInfo? property = resultType.GetProperty(aliases[i]);
#pragma warning restore IL2090

            if (property is not null && property.CanWrite)
            {
                int ordinal = reader.GetOrdinal(aliases[i]);

                if (!reader.IsDBNull(ordinal))
                {
                    object value = reader.GetValue(ordinal);
                    Type targetType = property.PropertyType;
                    Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    object converted = Convert.ChangeType(value, underlyingType);
                    property.SetValue(instance, converted);
                }
            }
        }

        return instance!;
    }
}

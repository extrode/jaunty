using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Builder for the JOIN...ON clause. Implements IJoinClause.
/// </summary>
internal sealed class JoinClauseBuilder<TFrom, TJoin> : IJoinClause<TFrom, TJoin> where TFrom : new() where TJoin : new()
{
    private readonly QueryBuilder<TFrom> _fromBuilder;
    private readonly JoinType _joinType;
    private readonly string? _joinAlias;
    private readonly EntityMetadata _joinMetadata;

    internal JoinClauseBuilder(QueryBuilder<TFrom> fromBuilder, JoinType joinType, string? joinAlias)
    {
        _fromBuilder = fromBuilder;
        _joinType = joinType;
        _joinAlias = joinAlias;
        _joinMetadata = FluentMetadataCache.GetMetadata<TJoin>();
    }

    public IJoinedQuery<TFrom, TJoin> On<TLeftKey, TRightKey>(Expression<Func<TFrom, TLeftKey>> leftKey, Expression<Func<TJoin, TRightKey>> rightKey)
    {
        string leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        string rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        string leftColumn = GetColumnName<TFrom>(leftProp, _fromBuilder.Alias);
        string rightColumn = GetColumnName<TJoin>(rightProp, _joinAlias);

        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery(condition);
    }

    public IJoinedQuery<TFrom, TJoin> On(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(
            _fromBuilder.Dialect,
            _fromBuilder.Alias,
            _joinAlias);

        (string condition, List<(string Name, object? Value)> parameters) = visitor.Translate(predicate);
        JoinedQueryBuilder<TFrom, TJoin> joinedQuery = CreateJoinedQuery(condition);
        for (int i = 0; i < parameters.Count; i++)
        {
            joinedQuery.AddParameter(parameters[i].Name, parameters[i].Value);
        }
        return joinedQuery;
    }

    public IJoinedQuery<TFrom, TJoin> On(string leftColumn, string rightColumn)
    {
        string condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery(condition);
    }

    public IJoinedQuery<TFrom, TJoin> On(string condition)
    {
        return CreateJoinedQuery(condition);
    }

    public IJoinedQuery<TFrom, TJoin> On<TValue>(string condition, TValue value)
    {
        JoinedQueryBuilder<TFrom, TJoin> joinedQuery = CreateJoinedQuery(condition);
        joinedQuery.AddParameter($"{_fromBuilder.Dialect.ParameterPrefix}value", value);
        return joinedQuery;
    }

    private JoinedQueryBuilder<TFrom, TJoin> CreateJoinedQuery(string onCondition)
    {
        var joinInfo = new JoinInfo(
            _joinType,
            _joinMetadata.TableName,
            _joinMetadata.SchemaName,
            _joinAlias,
            onCondition);

        return new JoinedQueryBuilder<TFrom, TJoin>(
            _fromBuilder.Connection,
            _fromBuilder.Dialect,
            _fromBuilder.TableName,
            _fromBuilder.SchemaName,
            _fromBuilder.Alias,
            joinInfo);
    }

    private string GetColumnName<T>(string propertyName, string? alias) where T : new()
    {
        EntityMetadata metadata = FluentMetadataCache.GetMetadata<T>();
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

        var escaped = _fromBuilder.Dialect.EscapeColumnName(columnName);
        var prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }
}